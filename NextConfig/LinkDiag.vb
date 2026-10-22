Imports System.Globalization
Imports System.Text

Public Structure LinkFailure
    Public Stage As Byte
    Public Reason As Byte
    Public Seconds As UInteger
End Structure

Public Class LinkSnapshot
    Public Version As Byte
    Public Rsrp As Byte = 255
    Public Rsrq As Byte = 255
    Public Sinr As Byte = 255
    Public Band As Byte = 255
    Public Bandwidth As Byte = 255
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

Public Structure LinkSignalView
    Public LevelText As String
    Public QualityText As String
    Public Maximum As Integer
    Public Value As Integer
    Public Grade As Integer
    Public Bars As Integer
    Public Lte As Boolean
End Structure

Public Module LinkDiag
    Public Const Command As Byte = 112
    Public Const RsrpMinimum As Integer = -120
    Public Const RsrpMaximum As Integer = -70
    Public Const RsrqMinimum As Integer = -20
    Public Const RsrqMaximum As Integer = -3
    Public Const RsrpGoodThreshold As Integer = -90
    Public Const RsrpPoorThreshold As Integer = -100
    Public Const RsrqGoodThreshold As Integer = -10
    Public Const RsrqPoorThreshold As Integer = -15
    Public Const Poor As Integer = 0
    Public Const Medium As Integer = 1
    Public Const Good As Integer = 2
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
        If frame Is Nothing OrElse (frame.Length <> 93 AndAlso frame.Length <> 96 AndAlso frame.Length <> 98) Then Throw New ArgumentException("Длина ответа 112")
        If Encoding.ASCII.GetString(frame, 0, 4) <> "SLDN" OrElse
            BitConverter.ToUInt16(frame, 4) <> frame.Length - 8 OrElse frame(6) <> 0 OrElse
            frame(8) <> Command OrElse Not ((frame(9) = 3 AndAlso frame.Length = 93) OrElse (frame(9) = 4 AndAlso frame.Length = 96) OrElse (frame(9) = 5 AndAlso frame.Length = 98)) Then Throw New ArgumentException("Формат ответа 112")
        If ClComPort.Crc16(frame, 4, frame.Length - 6) <> BitConverter.ToUInt16(frame, frame.Length - 2) Then Throw New ArgumentException("CRC ответа 112")
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
            .Version = frame(9), .State = frame(10), .Channel = frame(11), .Csq = frame(12), .Network = frame(13),
            .Probe = frame(14), .Plmn = Encoding.ASCII.GetString(frame, 15, plmnLength),
            .RetryMilliseconds = BitConverter.ToUInt32(frame, 22), .StageMilliseconds = New UInteger(5) {}}
        If snapshot.Version >= 4 Then
            If frame(93) > 250 AndAlso frame(93) <> 255 Then Throw New ArgumentException("SINR ответа 112")
            snapshot.Rsrp = frame(91) : snapshot.Rsrq = frame(92) : snapshot.Sinr = frame(93)
        End If
        If snapshot.Version = 5 Then
            If frame(95) > 5 AndAlso frame(95) <> 255 Then Throw New ArgumentException("Полоса ответа 112")
            snapshot.Band = frame(94) : snapshot.Bandwidth = frame(95)
        End If
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

    Public Function SignalView(snapshot As LinkSnapshot, csq As Byte) As LinkSignalView
        ' Вид 4G выбирается по типу сети: пропущенный замер (255) даёт «нет данных», а не переключение экрана на CSQ.
        ' Итог — по уровню RSRP: качество RSRQ падает при передаче данных и нагрузке соты (плата №1: −9…−13 дБ
        ' при том же уровне) и показывается своей шкалой, не занижая итог.
        If snapshot IsNot Nothing AndAlso snapshot.Version >= 4 AndAlso snapshot.Network = 4 Then
            Dim known = snapshot.Rsrp <> 255
            Dim grade = RsrpGrade(snapshot.Rsrp)
            Return New LinkSignalView With {
                .LevelText = MetricText(snapshot.Rsrp, RsrpGrade(snapshot.Rsrp), "дБм"),
                .QualityText = MetricText(snapshot.Rsrq, RsrqGrade(snapshot.Rsrq), "дБ"),
                .Maximum = RsrpMaximum - RsrpMinimum, .Value = If(known, Math.Max(0, Math.Min(RsrpMaximum - RsrpMinimum, -CInt(snapshot.Rsrp) - RsrpMinimum)), 0),
                .Grade = grade, .Bars = If(Not known, 0, If(snapshot.Rsrp <= 80, 5, If(snapshot.Rsrp <= 90, 4, If(snapshot.Rsrp <= 100, 3, If(snapshot.Rsrp <= 110, 2, 1))))), .Lte = True}
        End If
        Return New LinkSignalView With {.LevelText = If(csq <= 31, csq & " из 31", "Не определён"),
            .QualityText = "", .Maximum = 31, .Value = If(csq <= 31, CInt(csq), 0),
            .Grade = If(csq > 31, -1, If(csq >= 20, Good, If(csq >= 10, Medium, Poor))),
            .Bars = If(csq > 31, 0, If(csq >= 20, 5, If(csq >= 15, 4, If(csq >= 10, 3, If(csq >= 5, 2, 1))))) }
    End Function

    ' 4G до первого ответа 112: CSQ в 4G завышает оценку (плата №1: 22 из 31 при RSRP −100 дБм).
    Public Function MeasuringView() As LinkSignalView
        Return New LinkSignalView With {.LevelText = "Измеряется", .QualityText = "", .Maximum = 31, .Value = 0, .Grade = -1, .Bars = 0}
    End Function

    Public Function RsrpGrade(raw As Byte) As Integer
        Return If(raw = 255, -1, If(raw < -RsrpGoodThreshold, Good, If(raw <= -RsrpPoorThreshold, Medium, Poor)))
    End Function

    Public Function RsrqGrade(raw As Byte) As Integer
        Return If(raw = 255, -1, If(raw < -RsrqGoodThreshold, Good, If(raw <= -RsrqPoorThreshold, Medium, Poor)))
    End Function

    Public Function GradeText(grade As Integer) As String
        Return If(grade < 0, "Нет данных", New String() {"Плохо", "Средне", "Хорошо"}(grade))
    End Function

    Public Function SignalTitle(grade As Integer) As String
        Return If(grade < 0, "Сигнал не определён", New String() {"Сигнал слабый", "Сигнал средний", "Сигнал хороший"}(grade))
    End Function

    Private Function MetricText(raw As Byte, grade As Integer, unit As String) As String
        If raw = 255 Then Return "Нет данных"
        Return GradeText(grade) & " · −" & raw & " " & unit
    End Function

    Public Function BannerText(snapshot As LinkSnapshot) As String
        If snapshot Is Nothing Then Return "Нет ответа от платы — проверьте USB-подключение."
        If snapshot.State <> 7 Then Return StateText(snapshot.State)
        Dim grade = SignalView(snapshot, snapshot.Csq).Grade
        Dim detail = If(grade = Medium OrElse grade = Poor, BannerDetail(snapshot), "")
        Return BannerHeading(snapshot) & If(detail = "", "", " — " & Char.ToLowerInvariant(detail(0)) & detail.Substring(1))
    End Function

    Public Function BannerHeading(snapshot As LinkSnapshot) As String
        If snapshot Is Nothing Then Return "Нет ответа от платы"
        If snapshot.State = 7 Then Return "На связи · " & SignalTitle(SignalView(snapshot, snapshot.Csq).Grade).ToLowerInvariant()
        Return StateText(snapshot.State).Split(New String() {" — "}, StringSplitOptions.None)(0)
    End Function

    Public Function BannerDetail(snapshot As LinkSnapshot) As String
        If snapshot Is Nothing Then Return "Проверьте USB-подключение и нажмите «Обновить»."
        If snapshot.State = 7 Then
            Dim grade = SignalView(snapshot, snapshot.Csq).Grade
            If grade = Medium Then Return "В плохую погоду и при нагрузке связь может пропадать"
            If grade = Poor Then Return "Поднимите антенну выше, ближе к окну, дальше от металла, проверьте кабель"
            Return "Сервер отвечает. Связь установлена."
        End If
        Dim parts = StateText(snapshot.State).Split(New String() {" — "}, StringSplitOptions.None)
        Return If(parts.Length > 1, parts(1), "")
    End Function

    Public Function CellChips(snapshot As LinkSnapshot) As String()
        If snapshot Is Nothing Then Return New String() {"Нет данных"}
        Dim parts As New List(Of String), name As String = Nothing
        If snapshot.Plmn <> "" Then parts.Add(If(Operators.TryGetValue(snapshot.Plmn, name), name, snapshot.Plmn))
        If snapshot.Network <> 0 Then
            Dim network = NetworkText(snapshot.Network)
            If snapshot.Version >= 5 AndAlso snapshot.Network = 4 AndAlso snapshot.Band <> 255 Then network &= " · " & BandText(snapshot.Band)
            parts.Add(network)
        End If
        If snapshot.Version >= 5 AndAlso snapshot.Network = 4 AndAlso snapshot.Bandwidth <> 255 Then parts.Add(BandwidthText(snapshot.Bandwidth))
        parts.Add(If(snapshot.Channel = 0, "SIM-карта", "WiFi"))
        Return parts.ToArray()
    End Function

    Public Function BandText(band As Byte) As String
        If band = 255 Then Return ""
        Dim suffix As String = ""
        Select Case band
            Case 1 : suffix = " 2100"
            Case 3 : suffix = " 1800"
            Case 7 : suffix = " 2600"
            Case 8 : suffix = " 900"
            Case 20 : suffix = " 800"
            Case 31 : suffix = " 450"
            Case 38 : suffix = " TDD 2600"
            Case 40 : suffix = " TDD 2300"
        End Select
        Return "B" & band & suffix
    End Function

    Public Function BandwidthText(bandwidth As Byte) As String
        If bandwidth > 5 Then Return ""
        Return "полоса " & New String() {"1,4", "3", "5", "10", "15", "20"}(bandwidth) & " МГц"
    End Function

    Public Function CellText(snapshot As LinkSnapshot, Optional network As Byte = 0) As String
        Dim parts As New List(Of String)
        If snapshot IsNot Nothing Then
            If snapshot.Plmn <> "" Then
                Dim name As String = Nothing
                parts.Add(If(Operators.TryGetValue(snapshot.Plmn, name), name, snapshot.Plmn))
            End If
            network = snapshot.Network
        End If
        If network <> 0 Then parts.Add(NetworkText(network))
        If snapshot IsNot Nothing AndAlso network = 4 AndAlso snapshot.Version >= 5 AndAlso snapshot.Band <> 255 Then parts.Add(BandText(snapshot.Band))
        Return String.Join(" · ", parts)
    End Function

    Public Function ConnectionSteps(snapshot As LinkSnapshot, gsm As Byte, internet As Byte, server As Byte) As Integer
        Dim steps = If(gsm <> 1, 1, If(internet <> 1, 2, If(server = 0, 3, 4)))
        If snapshot Is Nothing OrElse snapshot.State = 7 Then Return steps
        Select Case snapshot.State
            Case 1 : Return 0
            Case 2 : Return 1
            Case 3 : Return 2
            Case 4, 5 : Return 3
            Case Else : Return Math.Min(steps, 3)
        End Select
    End Function

    Public Function ConnectionProblem(snapshot As LinkSnapshot, steps As Integer) As String
        If steps = 4 Then Return ""
        If snapshot IsNot Nothing AndAlso (snapshot.State = 0 OrElse snapshot.State = 5 OrElse snapshot.State = 6) Then Return StateText(snapshot.State)
        Return New String() {"Нет SIM-карты — вставьте SIM", "Нет сети — проверьте антенну и покрытие оператора",
            "Нет интернета — проверьте APN, баланс и передачу данных у оператора",
            "Сервер не отвечает, интернет есть — проверьте адрес сервера в настройках"}(steps)
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
