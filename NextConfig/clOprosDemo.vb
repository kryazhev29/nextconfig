Imports System.IO.Ports
Imports System.Threading

' Демонстрационный режим: программа показывает экраны на выдуманных данных
' и не открывает ни одного порта. Нужен, чтобы проверять внешний вид,
' состояния и ошибки, ничего не отправляя на плату.
'
' Запуск:
'   NextConfig.exe --demo        устройство найдено, статус идёт
'   NextConfig.exe --demo:none   портов нет
'   NextConfig.exe --demo:boot   на порту отвечает загрузчик
'   NextConfig.exe --demo:oldboot   на порту старый загрузчик 0x21 (шаг 1 перевода)
'   NextConfig.exe --demo:oldfw     новая прошивка под старым загрузчиком (шаг 2 перевода)
Public Enum eDemoMode
    Off = 0
    Found = 1
    NoPorts = 2
    Boot = 3
    OldBoot = 4
    OldFw = 5
    OldFw48 = 6
End Enum

Public Module Demo

    Public ReadOnly Mode As eDemoMode = ParseArgs()

    Public ReadOnly Property IsOn As Boolean
        Get
            Return Mode <> eDemoMode.Off
        End Get
    End Property

    ' Синтетические ответы v5 проходят те же проверки длины и парсеры, что COM.
    ' FW/User/user_fun.c:421,476,495,626,4806; structures.h:79,266,336.
    ' Никакие SerialPort API в этой функции не вызываются.
    Public Property LegacyFwStarted As Boolean
    Public Property LegacyBootReplaced As Boolean

    Public Function OldFirmwareReply(cmd As Byte, data As Byte()) As Byte()
        Select Case cmd
            Case 0
                Dim b(25) As Byte
                b(0) = &H55 : b(2) = 1 : b(3) = &H21 : b(4) = 139
                b(5) = 48 : b(6) = &H8B
                PutText(b, 10, "000000000000048")
                Return b
            Case &H30
                Return New Byte() {&H55, &H30, 250}
            Case &H31, &H32
                Return Nothing
            Case &H25
                ' В демо нет настоящего пароля; имитируем уже разрешённый доступ.
                Return New Byte() {&H55, &H25, 0, &H20}
            Case &H20
                Dim b(517) As Byte
                b(0) = &H55 : b(1) = cmd : Put16(b, 4, 512)
                Dim d(511) As Byte
                d(0) = &HAD : d(1) = 1 : d(3) = 1 : d(4) = &H8B
                Put32(d, 5, 1790640000UI)
                d(13) = &H2E : d(14) = 1
                PutText(d, 15, "internet")
                d(60) = 1 : d(61) = 0 : Put16(d, 64, 60)
                PutText(d, 66, "demo.example") : Put16(d, 90, 15010)
                PutText(d, 92, "pool.ntp.org") : Put16(d, 116, 123)
                PutText(d, 118, "79000000048")
                d(134) = 1 : d(135) = 5 : d(136) = 2 : d(137) = 1
                For i = 0 To 6
                    d(138 + 3 * i) = CByte(1 << i)
                    d(139 + 3 * i) = 8 : d(140 + 3 * i) = 20
                Next
                d(160) = 7 : d(161) = 4 : d(162) = 8 : d(163) = 20
                d(164) = 24 : d(167) = &H7F : d(168) = 2
                d(169) = 1 : d(170) = 2 : d(171) = 3
                Put16(d, 172, 100) : PutText(d, 176, "*100#")
                d(193) = 2 : d(194) = 1
                Array.Copy(d, 0, b, 6, d.Length)
                Return b
            Case &H22
                ' structures.h:79-262: packed DEVICE = 513, несмотря на комментарий 512.
                Dim d(512) As Byte
                PutText(d, 0, "000000000000048")
                d(16) = 48 : d(17) = &H8B : d(23) = &H11 : d(24) = 24
                Put16(d, 25, 2026) : d(27) = 9 : d(28) = 29
                d(29) = 12 : d(32) = &H41
                Put32(d, 49, 86400UI + CUInt((Environment.TickCount And &HFFFF) \ 1000))
                d(53) = 1 : Put16(d, 54, 35) : d(60) = &H20 : d(61) = 27
                Put16(d, 62, 3200) : Put16(d, 64, 25)
                Put32(d, 66, CUInt(DateTimeToUnixTimestamp(DateTime.Now)))
                d(70) = &H89 : Put16(d, 79, 127)
                Put16(d, 160, 15000) : Put16(d, 166, 350)
                d(237) = 2 : d(238) = 3 : d(269) = 4 : Put16(d, 270, 12150)
                Dim b(d.Length + 4) As Byte
                b(0) = &H55 : b(1) = cmd : Put16(b, 3, d.Length)
                Array.Copy(d, 0, b, 5, d.Length)
                Return b
            Case &H2C
                If data(0) = 0 Then Return New Byte() {&H55, cmd, 0, 35, 0}
                Dim index = CInt(BitConverter.ToUInt16(data, 1))
                Dim count = Math.Min(31, 35 - index) ' USB: до 2000 / 64 записей
                Dim b(6 + count * 64) As Byte
                b(0) = &H55 : b(1) = cmd : b(2) = 1
                Put16(b, 3, index) : Put16(b, 5, count)
                For n = 0 To count - 1
                    Dim k = index + n
                    Dim p = 7 + 64 * n
                    b(p) = &HAA
                    PutText(b, p + 1, If(k Mod 3 = 0, "900000" & k.ToString("0000"), "00" & k.ToString("X8")))
                    PutText(b, p + 12, If(k = 0, "Администратор", "Житель " & (k + 1)))
                    b(p + 32) = CByte(((k Mod 3) << 4) Or If(k = 0, 2, 0))
                    Put32(b, p + 33, If(k Mod 2 = 0, 0UI, 1800000000UI))
                    b(p + 37) = 20 : b(p + 38) = CByte(k Mod 7) : b(p + 39) = CByte(k Mod 10)
                Next
                Return b
            Case Else
                Throw New InvalidOperationException("Нет демо-ответа для этой команды чтения.")
        End Select
    End Function

    Private Sub PutText(b As Byte(), offset As Integer, value As String)
        Dim bytes = System.Text.Encoding.GetEncoding(1251).GetBytes(value)
        Array.Copy(bytes, 0, b, offset, bytes.Length)
    End Sub
    Private Sub Put16(b As Byte(), offset As Integer, value As Integer)
        Array.Copy(BitConverter.GetBytes(CUShort(value)), 0, b, offset, 2)
    End Sub
    Private Sub Put32(b As Byte(), offset As Integer, value As UInteger)
        Array.Copy(BitConverter.GetBytes(value), 0, b, offset, 4)
    End Sub

    Private Function ParseArgs() As eDemoMode
        For Each a As String In Environment.GetCommandLineArgs()
            Select Case a.Trim().ToLowerInvariant()
                Case "--demo", "/demo", "--demo:found"
                    Return eDemoMode.Found
                Case "--demo:none", "/demo:none"
                    Return eDemoMode.NoPorts
                Case "--demo:boot", "/demo:boot"
                    Return eDemoMode.Boot
                Case "--demo:oldboot", "/demo:oldboot"
                    Return eDemoMode.OldBoot
                Case "--demo:oldfw", "/demo:oldfw"
                    Return eDemoMode.OldFw
                Case "--demo:oldfw48", "/demo:oldfw48"
                    Return eDemoMode.OldFw48
            End Select
        Next
        Return eDemoMode.Off
    End Function

End Module

Partial Public Class clOpros
    Private demoLegacyBoot As Boolean

    Private demoTick As Integer = 0

    ' Вместо опроса портов - поток, который выдаёт те же события с выдуманными данными.
    Private Sub StartDemo()
        Comport = New ClComPort(New SerialPort("COM9"), False)

        _exit = False
        _continue = True

        thread_opros = New Threading.Thread(AddressOf Me.DemoCikl)
        thread_opros.Priority = Threading.ThreadPriority.Normal
        thread_opros.Name = "opros demo"
        thread_opros.IsBackground = True
        thread_opros.Start()
    End Sub

    Private Sub DemoCikl()

        Thread.Sleep(700)

        Select Case Demo.Mode

            Case eDemoMode.NoPorts
                RaiseEvent onFind("")

            Case eDemoMode.OldFw48
                RaiseEvent onFind("COM9")
                Thread.Sleep(900)
                rejim = eRejim.OldFirmware
                RaiseEvent onOldFirmwareFound("COM9", Demo.OldFirmwareReply(0, Nothing))

            Case eDemoMode.Boot
                RaiseEvent onFind("COM9")
                Thread.Sleep(900)
                rejim = eRejim.Boot
                RaiseEvent onBootFound("COM9", &H32, 139)

            Case eDemoMode.OldBoot
                RaiseEvent onFind("COM9")
                Thread.Sleep(900)
                rejim = eRejim.Boot
                RaiseEvent onBootFound("COM9", &H21, 139)

            Case Else
                RaiseEvent onFind("COM9")
                Thread.Sleep(900)
                rejim = eRejim.WaitConnect
                RaiseEvent isFinded("COM9", "867442070217472", 1)

        End Select

        While _continue
            Thread.Sleep(500)

            If rejim = eRejim.Search Then
                If demoLegacyBoot Then
                    demoLegacyBoot = False : rejim = eRejim.Boot
                    RaiseEvent onBootFound("COM9", &H21, 139)
                Else
                    rejim = eRejim.WaitConnect
                    RaiseEvent isFinded("COM9", "867442070217472", 1)
                End If
            End If
            If rejim = eRejim.Work AndAlso Not _stopGetStatus Then
                demoTick += 1
                RaiseEvent onStatus(DemoStatus())

                If (demoTick Mod 4) = 0 Then
                    Comport.DemoReceive(DemoLogLine())
                End If
            End If
        End While

        _exit = True
    End Sub

    ' Правдоподобный статус платы №1: FW 71, загрузчик 0x32, плата 139, на сервере.
    Private Function DemoStatus() As s_status
        Dim s As New s_status

        s.readyToUpdate = PhotoDemoWire.Scenario = "updating"
        s.time = CUInt(DateTimeToUnixTimestamp(DateTime.UtcNow))
        s.in1 = ((demoTick \ 8) Mod 2) = 1
        s.in2 = False
        s.out1 = ((demoTick \ 12) Mod 2) = 1
        s.out2 = False
        s.fw_version = 71
        s.fw_build = 1789482488
        s.boot_version = 50
        s.hw_version = 139

        ' под старым загрузчиком прошивка знает его версию, а плату - нет
        If (Demo.Mode = eDemoMode.OldFw OrElse Demo.LegacyFwStarted) AndAlso Not Demo.LegacyBootReplaced Then
            s.boot_version = &H21
            s.hw_version = 0
        End If
        s.signal = CByte(24 + (demoTick Mod 6))
        s.balance = 0
        s.gsm = 1
        s.gprs = 1
        s.connectToServer = 1
        s.netType = eNetType._4G
        s.peripheryState = New Byte(7) {}
        s.peripheryState(0) = 1
        s.usedMem = 1274
        s.moduleType = eModuleType.M_SLM336

        Return s
    End Function

    ' Строки журнала, похожие на настоящие: обычное событие, переписка с
    ' модемом, предупреждение и ошибка - чтобы было видно разметку цветом.
    Private ReadOnly demoLog As String() = {
        "USER: status sent, conn=1",
        "AT+CSQ",
        "+CSQ: 24,99",
        "GSM: registered in network",
        "AT+QIOPEN=1,0,""TCP"",""87.244.4.92"",15010,0,1",
        "USER: server answer 24",
        "GSM: WARN low signal level",
        "USER: ERROR not answer to status"
    }

    Private Function DemoLogLine() As String
        Return demoLog((demoTick \ 4) Mod demoLog.Length) & vbCrLf
    End Function

    ' Обновление прошивки в демонстрационном режиме: блоки уходят «в никуда»,
    ' зато виден весь ход операции и её итог.
    Private demoBlocks As Integer = 0

    Private Function DemoStartUpdate(ByVal fwI As s_fwinfo) As eCmdAnswer
        demoBlocks = fwI.nBlocks
        Return eCmdAnswer.CMD_SUCCESS
    End Function

    Private Function DemoWriteBlock(ByVal nBlock As UInt16, ByRef percent As Byte) As eCmdAnswer
        Threading.Thread.Sleep(20)

        ' «Протокол2»: ход 0…100, код 0 — блок принят
        percent = CByte(Math.Min(100, CInt((nBlock + 1) * 100L \ Math.Max(1, demoBlocks))))

        Return eCmdAnswer.CMD_SUCCESS
    End Function

    ' Замена загрузчика командой 110 без платы: принимает всё подряд.
    Private demoBootReceived As UInt32 = 0

    Private Function DemoBootManual(ByVal op As eBootOp, ByVal payload As Byte(),
                                    ByRef received As UInt32, ByRef hw As Byte, ByRef armed As Byte) As eBootStatus
        Threading.Thread.Sleep(5)

        Select Case op
            Case eBootOp.OP_BEGIN, eBootOp.OP_CANCEL
                demoBootReceived = 0
            Case eBootOp.OP_DATA
                demoBootReceived = BitConverter.ToUInt32(payload, 0) + CUInt(payload.Length - 4)
            Case eBootOp.OP_COMMIT
                armed = 1
                Demo.LegacyBootReplaced = True
        End Select

        received = demoBootReceived
        hw = 139
        Return eBootStatus.BOOT_OK
    End Function

    ' Настройки, какие обычно стоят на стендовой плате.
    Private Function DemoConfig() As s_config
        Dim c As New s_config

        c.time = CUInt(DateTimeToUnixTimestamp(DateTime.UtcNow))
        c.pin = BitConverter.ToUInt32(System.Text.Encoding.UTF8.GetBytes("4321"), 0)
        c.server1 = "87.244.4.92:15010"
        c.server2 = "93.189.229.197:15020"
        c.ntpServer = "pool.ntp.org"
        c.opType = "internet"
        c.netType = eNetType._Авто
        c.countryCode = "7"
        c.simNumber = "+7 900 000-00-00"
        c.ussdBalance = "*100#"
        c.resetTime = 0
        c.fullLog = 0
        c.VoLTE = 1
        c.rs485Mode = If(PhotoDemoWire.Scenario = "no-camera", CByte(0), CByte(1))
        c.rs485Baud = 0
        c.periph1.rs485Adr = 1
        c.periph1.rs485Type = 3
        c.enableNTPsync = 1
        c.wiegandType = 0
        c.in1Func = 0
        c.in2Func = 0
        c.useServer1 = 1
        c.useserver2 = 0

        c.rawConfig = EncodeConfig(c)
        Return c
    End Function

End Class
