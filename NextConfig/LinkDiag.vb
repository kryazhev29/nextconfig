Imports System.Globalization
Imports System.Text

Public Structure LinkFailure
    Public Stage As Byte
    Public Reason As Byte
    Public Seconds As UInteger
End Structure

Public Class LinkSnapshot
    Public State As Byte
    Public Channel As Byte
    Public Csq As Byte
    Public Network As Byte
    Public Probe As Byte
    Public Plmn As String
    Public RetryMilliseconds As UInteger
    Public StageMilliseconds As UInteger()
    Public Failures As New List(Of LinkFailure)
End Class

Public Module LinkDiag
    Public Const Command As Byte = 112
    Public ReadOnly Stages As String() = {"Питание", "Настройка модема", "SIM", "Сеть", "Интернет", "Сервер"}
    Private ReadOnly States As String() = {
        "Идёт подключение — дождитесь завершения этапов.",
        "SIM недоступна — проверьте установку карты и снимите блокировку PIN/PUK.",
        "Нет сети — проверьте антенну и покрытие оператора.",
        "Нет интернета — проверьте APN, баланс и доступность передачи данных у оператора.",
        "Сервер недоступен — проверьте адрес, порт и прохождение трафика до сервера.",
        "Сервер не принял IMEI — проверьте регистрацию контроллера на сервере.",
        "Ожидается перезапуск модема — дождитесь окончания отсчёта, причину смотрите в истории сбоев.",
        "Связь установлена — дополнительных действий не требуется."
    }
    Private ReadOnly Reasons As String() = {
        "Нет ошибки", "Истекло время ожидания на этапе", "Модем ответил AT ERROR",
        "Неподходящий ответ модема", "SIM не установлена", "SIM требует PIN", "SIM требует PUK",
        "Нет регистрации в сети", "Не установлен или потерян доступ в интернет (PDP)",
        "Ошибка соединения с сервером (TCP)", "Нарушены границы ответа модема",
        "Модем не отвечает", "Неподдерживаемый тип модема", "Сервер отказал в регистрации IMEI",
        "Сервер не ответил на регистрацию или перестал отвечать",
        "Ping и DNS не подтвердили доступ в интернет"
    }
    Private ReadOnly Operators As New Dictionary(Of String, String) From {
        {"25001", "МТС"}, {"25002", "МегаФон"}, {"25011", "Yota"},
        {"25020", "T2"}, {"25035", "Мотив"}, {"25099", "Билайн"}
    }

    Public Function Parse(frame As Byte()) As LinkSnapshot
        If frame Is Nothing OrElse frame.Length <> 93 Then Throw New ArgumentException("Длина ответа 112")
        If Encoding.ASCII.GetString(frame, 0, 4) <> "SLDN" OrElse
            BitConverter.ToUInt16(frame, 4) <> 85 OrElse frame(6) <> 0 OrElse
            frame(8) <> Command OrElse frame(9) <> 3 Then Throw New ArgumentException("Формат ответа 112")
        If ClComPort.Crc16(frame, 4, 87) <> BitConverter.ToUInt16(frame, 91) Then Throw New ArgumentException("CRC ответа 112")
        If frame(10) > 7 OrElse frame(11) > 1 OrElse
            (frame(12) > 31 AndAlso frame(12) <> 99) OrElse
            Not New Byte() {0, 1, 3, 4}.Contains(frame(13)) OrElse frame(14) > 3 OrElse frame(50) > 10 Then
            Throw New ArgumentException("Поля ответа 112")
        End If
        Dim plmnLength = Array.IndexOf(frame, CByte(0), 15, 7) - 15
        If plmnLength <> 0 AndAlso plmnLength <> 5 AndAlso plmnLength <> 6 Then Throw New ArgumentException("Длина PLMN")
        For i = 15 To 21
            If i < 15 + plmnLength Then
                If frame(i) < 48 OrElse frame(i) > 57 Then Throw New ArgumentException("Цифры PLMN")
            ElseIf frame(i) <> 0 Then
                Throw New ArgumentException("Окончание PLMN")
            End If
        Next
        Dim snapshot As New LinkSnapshot With {
            .State = frame(10), .Channel = frame(11), .Csq = frame(12), .Network = frame(13),
            .Probe = frame(14), .Plmn = Encoding.ASCII.GetString(frame, 15, plmnLength),
            .RetryMilliseconds = BitConverter.ToUInt32(frame, 22), .StageMilliseconds = New UInteger(5) {}}
        For i = 0 To 5
            snapshot.StageMilliseconds(i) = BitConverter.ToUInt32(frame, 26 + i * 4)
        Next
        ' Прошивка уже развернула кольцо: на экране нужен обратный порядок, не сортировка по времени.
        For i = CInt(frame(50)) - 1 To 0 Step -1
            Dim word = BitConverter.ToUInt32(frame, 51 + i * 4)
            Dim failure As New LinkFailure With {.Stage = CByte(word And 7UI),
                .Reason = CByte((word >> 3) And 31UI), .Seconds = word >> 8}
            If failure.Stage > 5 OrElse failure.Reason = 0 OrElse failure.Reason > 15 Then Throw New ArgumentException("Запись сбоя")
            snapshot.Failures.Add(failure)
        Next
        Return snapshot
    End Function

    Public Function StateText(state As Byte) As String
        Return States(state)
    End Function

    Public Function ReasonText(reason As Byte) As String
        Return Reasons(reason)
    End Function

    Public Function OperatorText(plmn As String) As String
        If String.IsNullOrEmpty(plmn) Then Return "нет данных"
        Dim name As String = Nothing
        If Operators.TryGetValue(plmn, name) Then Return plmn & " · " & name
        Return plmn
    End Function

    Public Function NetworkText(network As Byte) As String
        Select Case network
            Case 1
                Return "2G"
            Case 3
                Return "3G"
            Case 4
                Return "4G"
            Case Else
                Return "нет данных"
        End Select
    End Function

    Public Function ProbeText(probe As Byte) As String
        Return New String() {"Проверка ещё не выполнялась", "Есть ответ на ping", "Есть ответ DNS", "Нет ответа на ping и DNS"}(probe)
    End Function

    Public Function DurationText(milliseconds As UInteger) As String
        If milliseconds = 0 Then Return "—"
        Return (milliseconds / 1000.0).ToString("0.0", CultureInfo.GetCultureInfo("ru-RU")) & " с"
    End Function
End Module
