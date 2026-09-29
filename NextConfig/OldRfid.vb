Imports System.IO

Public Class OldRfid
    Public Property Address As Byte
    Public Property Parameter As Byte
    Public Property Value As Byte
    Public Property ReaderType As Byte
    Public Shared Sub ValidateParameter(reader As Byte, parameter As Byte)
        If reader <> 0 AndAlso reader <> 2 Then Throw New NotSupportedException("Параметры этого типа считывателя по USB не поддерживаются: обработчик R1610/Wiegand пуст.")
        Dim allowed = If(reader = 0, New Byte() {3, 5, 7, &H3F}, New Byte() {3, 5, 7, &HA})
        If Not allowed.Contains(parameter) Then Throw New NotSupportedException("Этот параметр не поддерживается выбранным типом считывателя.")
    End Sub
    Public Shared Function Hint(reader As Byte, parameter As Byte) As String
        Select Case parameter
            Case 3 : Return "Адрес: 0–254. После записи чтение выполняется по новому адресу."
            Case 5 : Return "Мощность: 0–" & If(reader = 0, 26, 29) & "."
            Case 7 : Return "Скорость: 0=9600, 1=19200, 2=38400, 3=57600, 4=115200. Смена скорости может прервать связь считывателя с платой."
            Case &H3F : Return "Диапазон RU5300: 0=US, 1=EU."
            Case &HA : Return "Область PRIME: 1=EPC, 2=TID, 3=User."
        End Select
        Return "Выберите параметр."
    End Function
    Public Shared Sub ValidateValue(reader As Byte, parameter As Byte, value As Byte)
        ValidateParameter(reader, parameter)
        Dim maximum As Integer
        Select Case parameter
            Case 3 : maximum = 254
            Case 5 : maximum = If(reader = 0, 26, 29)
            Case 7 : maximum = 4
            Case &H3F : maximum = 1
            Case &HA : maximum = 3
        End Select
        If value > maximum OrElse (parameter = &HA AndAlso value = 0) Then Throw New InvalidDataException(Hint(reader, parameter))
    End Sub
    Public Shared Function Parse(reply As Byte(), address As Byte, parameter As Byte, reader As Byte) As OldRfid
        If reply Is Nothing OrElse reply.Length <> 4 OrElse reply(0) <> &H55 OrElse reply(1) <> &H50 OrElse reply(2) <> parameter Then Throw New InvalidDataException("Неожиданный ответ параметра RFID. Повторите чтение.")
        Return New OldRfid With {.Address = address, .Parameter = parameter, .Value = reply(3), .ReaderType = reader}
    End Function
End Class

Partial Public Class OldFirmware
    Public Function ReadRfid(address As Byte, parameter As Byte) As OldRfid
        If address = 255 Then Throw New InvalidDataException("Широковещательный адрес 255 не допускается.")
        ReadSettings()
        Dim reader = lastSettings(194)
        OldRfid.ValidateParameter(reader, parameter)
        Dim payload As Byte() = {address, parameter}
        Dim reply = If(demoMode, Demo.OldFirmwareReply(&H50, payload), clBoot.ReadOldRfid(port, address, parameter))
        Return OldRfid.Parse(reply, address, parameter, reader)
    End Function
    Public Function WriteRfid(before As OldRfid, value As Byte) As OldRfid
        RequireAdministrator()
        If before Is Nothing Then Throw New InvalidOperationException("Сначала прочитайте параметр RFID.")
        OldRfid.ValidateValue(before.ReaderType, before.Parameter, value)
        Dim current = ReadRfid(before.Address, before.Parameter)
        If current.Value <> before.Value OrElse current.ReaderType <> before.ReaderType Then Throw New IOException("Параметр или тип RFID изменился. Перечитайте и подтвердите заново.")
        Dim reply = If(demoMode, Demo.OldFirmwareReply(&H51, New Byte() {before.Address, before.Parameter, value}), clBoot.WriteOldRfid(port, before.Address, before.Parameter, value))
        If reply Is Nothing OrElse Not reply.SequenceEqual(New Byte() {&H55, &H51, 1}) Then Throw New IOException("Считыватель не подтвердил запись параметра RFID.")
        Dim after As OldRfid
        Try
            after = ReadRfid(If(before.Parameter = 3, value, before.Address), before.Parameter)
        Catch ex As Exception
            Throw New IOException("Считыватель подтвердил запись, но повторное чтение не удалось. Проверьте адрес и скорость; не повторяйте запись вслепую.", ex)
        End Try
        If after.Value <> value Then Throw New IOException("RFID: записанное значение не совпало с прочитанным.")
        Return after
    End Function
End Class
