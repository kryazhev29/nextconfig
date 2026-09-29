Imports System.IO

Partial Public Class OldFirmware
    Public Function ReadChronology() As DataTable
        ' Один запрос количества, без очистки и без серверных 40/41.
        Dim request(19) As Byte
        Array.Copy(BitConverter.GetBytes(UInteger.MaxValue), 0, request, 16, 4)
        Dim reply = If(demoMode, Demo.OldFirmwareReply(&H2E, request), clBoot.ReadOldChronology(port, request))
        If reply Is Nothing Then Throw New NotSupportedException("Хронология не поддерживается этой прошивкой.")
        Header(reply, &H2E, 5)
        If reply(2) <> 0 OrElse (reply.Length <> 5 AndAlso reply.Length <> 10) Then Throw New InvalidDataException("Неизвестный ответ запроса хронологии.")
        Dim table = Fields()
        Add(table, "Хронология", "Записей по ответу платы", U16(reply, 3))
        Add(table, "Хронология", "Поддержка", "Плата ответила на запрос количества. Формат чтения записей этой сборки требует подтверждения.")
        Return table
    End Function
    Public Function ReadWorkLog() As DataTable
        Dim reply = If(demoMode, Demo.OldFirmwareReply(&H29, Nothing), clBoot.ReadOldWorkLog(port))
        If reply Is Nothing Then Throw New NotSupportedException("Лог работы по USB не поддерживается этой прошивкой: обработчик 0x29 пуст.")
        Header(reply, &H29, 3)
        If reply.Length <> 3 + CInt(reply(2)) Then Throw New InvalidDataException("Неверная длина лога работы.")
        Dim table = Fields()
        Add(table, "Лог", "Ответ платы", Ansi.GetString(reply, 3, reply(2)))
        Return table
    End Function
    Public Function AutoregistrationState() As Boolean
        Dim reply = Request(&H22, New Byte() {0})
        ParseStatus(reply)
        Return (reply(37) And 128) <> 0
    End Function
    Public Sub SetAutoregistration(before As Boolean, enabled As Boolean)
        RequireAdministrator()
        If AutoregistrationState() <> before Then Throw New IOException("Авторегистрация изменилась. Обновите состояние и подтвердите заново.")
        If demoMode Then
            Demo.OldFirmwareReply(&H27, New Byte() {If(enabled, CByte(1), CByte(0))})
        Else
            clBoot.SetOldAutoregistration(port, enabled)
        End If
        If AutoregistrationState() <> enabled Then Throw New IOException("Авторегистрация не подтверждена чтением состояния.")
    End Sub
    Public Sub TestOutput()
        RequireAccess(&H10)
        If demoMode Then
            Demo.OldFirmwareReply(&H26, Nothing)
        Else
            clBoot.TestOldOutput(port)
        End If
    End Sub
    Public Shared Function FactorySettings(version As Byte, hardware As Byte) As Byte()
        ' user_setdefault_c1 обеих веток. Для 47 семейство не предполагаем.
        If version <> 45 AndAlso version <> 48 Then Return Nothing
        Dim block(511) As Byte
        block(0) = &HAD : block(1) = 1 : block(2) = 1 : block(3) = 1 : block(4) = hardware
        Array.Copy(BitConverter.GetBytes(CUShort(4321)), 0, block, 9, 2)
        Array.Copy(BitConverter.GetBytes(CUShort(5678)), 0, block, 11, 2)
        block(13) = If(version = 45, CByte(2), CByte(3))
        Array.Copy(Ansi.GetBytes(",,"), 0, block, 15, 2)
        block(60) = 1
        Array.Copy(Ansi.GetBytes("gate.naviset.su"), 0, block, 66, 14)
        Array.Copy(BitConverter.GetBytes(CUShort(10950)), 0, block, 90, 2)
        Array.Copy(Ansi.GetBytes("89.109.251.21"), 0, block, 92, 12)
        Array.Copy(BitConverter.GetBytes(CUShort(123)), 0, block, 116, 2)
        For p = 134 To 137 : block(p) = 1 : Next
        For Each p In {142, 145} : block(p) = 8 : block(p + 1) = 20 : Next
        Dim days As Byte() = {31, 31, 127, 96, 85, 42}
        For i = 0 To days.Length - 1 : block(138 + i * 3) = days(i) : Next
        block(159) = If(version = 45, CByte(5), CByte(0))
        OldSettings.UpdateCrc(block)
        Return block
    End Function
    Public Sub FactoryReset(before As Byte(), baseBefore As DataTable, backupFolder As String)
        RequireAdministrator()
        ReadSettings()
        If Not before.SequenceEqual(lastSettings) Then Throw New IOException("Настройки изменились; перечитайте перед сбросом.")
        CheckBase(baseBefore)
        Dim prefix = Path.Combine(backupFolder, "Domovoi_" & DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") & "_" & Guid.NewGuid().ToString("N").Substring(0, 8))
        Using file As New FileStream(prefix & ".bin", FileMode.CreateNew, FileAccess.Write)
            file.Write(before, 0, before.Length) : file.Flush(True)
        End Using
        BackupNumbers(baseBefore, prefix & ".csv")
        Dim reply = If(demoMode, Demo.OldFirmwareReply(&H2F, Nothing), clBoot.ResetOldFactory(port))
        accessLevel = 0
        If reply Is Nothing OrElse Not reply.SequenceEqual(New Byte() {&H55, &H2F, 0}) Then Throw New IOException("Плата не подтвердила сброс. Прочитайте настройки и базу; резерв сохранён.")
        Try
            ReadSettings()
            Dim expected = FactorySettings(Identification(5), Identification(6))
            If expected IsNot Nothing AndAlso Not expected.SequenceEqual(lastSettings) Then Throw New IOException("После сброса настройки отличаются от эталона. Проверьте их вручную.")
            If ReadNumbers().Rows.Count <> 0 Then Throw New IOException("После сброса база не пуста.")
        Finally
            accessLevel = 0
        End Try
    End Sub
End Class
