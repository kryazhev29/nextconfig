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
'   NextConfig.exe --demo:oldfw48   старая прошивка v5 (чтение и перевод)
'   NextConfig.exe --demo:two       две платы NEXT: на связи и без сети
'   --demo-link=wifi | fallback | switch   вместе с --demo: сервер через Wi-Fi (сотовая в резерве),
'                       сети Wi-Fi нет и сервер через 4G, смена канала каждые 40 с (событие в строке состояния)
Public Enum eDemoMode
    Off = 0
    Found = 1
    NoPorts = 2
    Boot = 3
    OldBoot = 4
    OldFw = 5
    OldFw48 = 6
    OldFw45 = 8
    Two = 7
End Enum

Public Module Demo
    ' Только демонстрация: десять минут улучшения уровня с небольшими колебаниями.
    Public Function SignalRsrp(seconds As Double) As Byte
        Return CByte(103 - Math.Min(8, Math.Max(0, seconds) / 80.0) + Math.Sin(seconds / 45.0) * 2)
    End Function


    Public ReadOnly Mode As eDemoMode = ParseArgs()

    Private ReadOnly linkScenarioClock As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()

    ' Выбор, «записанный» в демо командой 114: без --demo-link ответ 113 его возвращает.
    Public WifiChoice As Byte() = {0, 0}

    ' Список сетей в демо (команда 115): 255 запускает поиск на 1,5 с, затем страницы по 60 байт — как у платы, но короче,
    ' чтобы окно выбора сети проходило несколько страниц.
    Private scanStarted As Long = -1
    Private ReadOnly scanNetworks As (String, Byte)() = {("Office-5G", 78), ("TP-Link_15CB", 100), ("Office", 70),
        ("MTS_Router_245888-", 52), ("Cafe Mocha", 47), ("Neighbour 2.4G", 25), ("RT-WiFi_27EB", 6)}

    Public Function WifiScanReply(row As Byte) As Byte()
        If row = 255 Then scanStarted = linkScenarioClock.ElapsedMilliseconds : Return New Byte() {1}
        If scanStarted < 0 Then scanStarted = linkScenarioClock.ElapsedMilliseconds
        If linkScenarioClock.ElapsedMilliseconds - scanStarted < 1500 Then Return New Byte() {1}
        Dim page As New List(Of Byte) From {0, 255}
        Dim i = CInt(row)
        While i < scanNetworks.Length
            Dim name = Text.Encoding.UTF8.GetBytes(scanNetworks(i).Item1)
            If page.Count + 2 + name.Length > 60 Then page(1) = CByte(i) : Exit While
            page.Add(scanNetworks(i).Item2) : page.Add(CByte(name.Length)) : page.AddRange(name)
            i += 1
        End While
        Return page.ToArray()
    End Function

    ' Ответ 113 в демо; без --demo-link Wi-Fi-модем не выбран, сервер через 4G.
    Public Function WifiReply() As Byte()
        Dim arg = Environment.GetCommandLineArgs().FirstOrDefault(Function(a) a.StartsWith("--demo-link="))
        Dim scenario = If(arg Is Nothing, "", arg.Substring(12))
        If scenario = "switch" Then scenario = If((linkScenarioClock.ElapsedMilliseconds \ 40000) Mod 2 = 0, "wifi", "fallback")
        Select Case scenario
            Case "wifi" : Return New Byte() {1, 2, 1, 1, 1, 1, 2, 78}.Concat(Text.Encoding.UTF8.GetBytes("Office-5G")).ToArray()
            Case "fallback" : Return New Byte() {0, 3, 1, 1, 1, 4, 1, 255}
            Case Else
                ' Выбранный мост в «Только GSM» выключает TCP (связь 3); других мостов в демо нет.
                Dim link As Byte = If(WifiChoice(0) = 1 AndAlso WifiChoice(1) = 0, CByte(3), CByte(0))
                Return New Byte() {0, link, 1, WifiChoice(0), WifiChoice(1), 4}
        End Select
    End Function

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

    Private oldSettingsBlock As Byte()
    Private oldNumbersState As List(Of Byte())
    Private oldReportedCount As Integer
    Private oldAutoregistration As Boolean
    Private oldOutput As Boolean
    Private ReadOnly oldRfidValues As New Dictionary(Of String, Byte)

    Public Function OldFirmwareReply(cmd As Byte, data As Byte()) As Byte()
        Select Case cmd
            Case 0
                Dim b(25) As Byte
                b(0) = &H55 : b(2) = 1 : b(3) = &H21 : b(4) = 139
                b(5) = If(Mode = eDemoMode.OldFw45, CByte(45), CByte(48)) : b(6) = &H8B
                PutText(b, 10, "000000000000048")
                Return b
            Case &H50
                Dim key = data(0) & ":" & data(1)
                Dim value As Byte
                If Not oldRfidValues.TryGetValue(key, value) Then
                    Select Case data(1)
                        Case 3 : value = data(0)
                        Case 5 : value = 20
                        Case 7 : value = 4
                        Case &HA : value = 2
                        Case &H3F : value = 1
                    End Select
                End If
                Return New Byte() {&H55, cmd, data(1), value}
            Case &H51
                If data(1) = 3 Then
                    Dim prefix = data(0) & ":"
                    For Each key In oldRfidValues.Keys.Where(Function(k) k.StartsWith(prefix)).ToList()
                        oldRfidValues(data(2) & ":" & key.Substring(prefix.Length)) = oldRfidValues(key)
                        oldRfidValues.Remove(key)
                    Next
                End If
                oldRfidValues(If(data(1) = 3, data(2), data(0)) & ":" & data(1)) = data(2)
                Return New Byte() {&H55, cmd, 1}
            Case &H26
                oldOutput = Not oldOutput : Return Nothing
            Case &H27
                oldAutoregistration = data(0) <> 0 : Return Nothing
            Case &H29, &H2E
                Return Nothing
            Case &H2F
                oldSettingsBlock = OldFirmware.FactorySettings(If(Mode = eDemoMode.OldFw45, CByte(45), CByte(48)), &H8B)
                oldNumbersState = New List(Of Byte())
                Return New Byte() {&H55, cmd, 0}
            Case &H30
                Return New Byte() {&H55, &H30, 250}
            Case &H31, &H32
                Return Nothing
            Case &H25
                ' В демо нет настоящего пароля; имитируем уже разрешённый доступ.
                Return New Byte() {&H55, &H25, 0, &H20}
            Case &H21
                If data Is Nothing OrElse data.Length <> 515 Then Throw New ArgumentException("Блок настроек")
                oldSettingsBlock = data.Skip(3).ToArray()
                Return New Byte() {&H55, cmd, 0, 0}
            Case &H20
                Dim b(517) As Byte
                b(0) = &H55 : b(1) = cmd : Put16(b, 4, 512)
                Dim d(511) As Byte
                d(0) = &HAD : d(1) = 1 : d(3) = 1 : d(4) = &H8B
                Put32(d, 5, 1790640000UI)
                d(13) = If(Mode = eDemoMode.OldFw45, CByte(&H3E), CByte(&H2E)) : d(14) = 1
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
                d(193) = 2 : d(194) = 0
                If oldSettingsBlock Is Nothing Then
                    OldSettings.UpdateCrc(d) : oldSettingsBlock = d
                End If
                Array.Copy(oldSettingsBlock, 0, b, 6, d.Length)
                Return b
            Case &H22
                ' structures.h:79-262: packed DEVICE = 513, несмотря на комментарий 512.
                Dim d(512) As Byte
                PutText(d, 0, "000000000000048")
                d(16) = If(Mode = eDemoMode.OldFw45, CByte(45), CByte(48)) : d(17) = &H8B : d(23) = &H11 : d(24) = 24
                Put16(d, 25, 2026) : d(27) = 9 : d(28) = 29
                d(29) = 12 : d(32) = CByte(&H41 Or If(oldAutoregistration, 128, 0))
                Put32(d, 49, 86400UI + CUInt((Environment.TickCount And &HFFFF) \ 1000))
                d(53) = 1 : Put16(d, 54, If(oldNumbersState Is Nothing, 35, oldNumbersState.Count)) : d(60) = &H20 : d(61) = 27
                Put16(d, 62, 3200) : Put16(d, 64, 25)
                Put32(d, 66, CUInt(DateTimeToUnixTimestamp(DateTime.Now)))
                d(70) = CByte(&H89 Or If(oldOutput, 4, 0)) : Put16(d, 79, 127)
                Put16(d, 160, 15000) : Put16(d, 166, 350)
                d(237) = 2 : d(238) = 3 : d(269) = 4 : Put16(d, 270, 12150)
                Dim b(d.Length + 4) As Byte
                b(0) = &H55 : b(1) = cmd : Put16(b, 3, d.Length)
                Array.Copy(d, 0, b, 5, d.Length)
                Return b
            Case &H28
                Return New Byte() {&H55, cmd, 1}
            Case &H2A, &H2B, &H2D
                If oldNumbersState Is Nothing Then OldFirmwareReply(&H2C, New Byte() {0})
                If cmd = &H2A Then
                    Dim record = CType(data.Clone(), Byte()) : record(39) = 0
                    oldNumbersState.Add(record)
                ElseIf cmd = &H2B Then
                    oldNumbersState.RemoveAll(Function(r) OldNumbers.Id(r) = OldNumbers.Id(data))
                Else
                    If data(0) = 1 Then oldNumbersState.Clear()
                    If data(0) = 0 Then
                        Dim start = CInt(BitConverter.ToUInt16(data, 1))
                        If start = 0 Then oldNumbersState.Clear()
                        If start <> oldNumbersState.Count Then Return New Byte() {&H55, cmd, 0, 10, 0, 0, 0, 0}
                        For p = 5 To data.Length - 1 Step 64
                            Dim record = data.Skip(p).Take(64).ToArray() : record(39) = 0
                            oldNumbersState.Add(record)
                        Next
                    End If
                    Return New Byte() {&H55, cmd, 0, data(0)}
                End If
                Return New Byte() {&H55, cmd, 0}
            Case &H2C
                If oldNumbersState Is Nothing Then
                    oldNumbersState = New List(Of Byte())
                    For k = 0 To 34
                        Dim record(63) As Byte
                        record(0) = &HAA
                        PutText(record, 1, If(k Mod 3 = 0, "900000" & k.ToString("0000"), "00" & k.ToString("X8")))
                        PutText(record, 12, If(k = 0, "Администратор", "Житель " & (k + 1)))
                        record(32) = CByte(((k Mod 3) << 4) Or If(k = 0, 2, 0))
                        Put32(record, 33, If(k Mod 2 = 0, 0UI, 1800000000UI))
                        record(37) = 20 : record(38) = CByte(k Mod 7) : record(39) = CByte(k Mod 10)
                        oldNumbersState.Add(record)
                    Next
                End If
                If data(0) = 0 Then
                    Dim count = If(Mode = eDemoMode.OldFw45, oldReportedCount, oldNumbersState.Count)
                    oldReportedCount = oldNumbersState.Count
                    Return New Byte() {&H55, cmd, 0, CByte(count And 255), CByte(count >> 8)}
                End If
                Dim index = CInt(BitConverter.ToUInt16(data, 1))
                Dim records = oldNumbersState.Skip(index).Take(31).ToList()
                Return New Byte() {&H55, cmd, 1, CByte(index And 255), CByte(index >> 8), CByte(records.Count), 0}.Concat(records.SelectMany(Function(r) r)).ToArray()
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
                Case "--demo:oldfw45"
                    Return eDemoMode.OldFw45
                Case "--demo:oldfw48", "/demo:oldfw48"
                    Return eDemoMode.OldFw48
                Case "--demo:two", "/demo:two"
                    Return eDemoMode.Two
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
        rejim = eRejim.Search

        thread_opros = New Threading.Thread(AddressOf Me.DemoCikl)
        thread_opros.Priority = Threading.ThreadPriority.Normal
        thread_opros.Name = "opros demo"
        thread_opros.IsBackground = True
        thread_opros.Start()
    End Sub

    Private Sub DemoCikl()

        While _continue
            Thread.Sleep(500)

            If rejim = eRejim.Search Then
                ' проход по портам в демо тоже занимает время
                Dim list = DemoDevices()
                Thread.Sleep(700)
                If rejim = eRejim.Search Then RaiseEvent onScan(list, If(Demo.Mode = eDemoMode.NoPorts, 0, 2))
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

    ' Какие платы «нашёл» проход по портам в демо.
    Private Function DemoDevices() As List(Of FoundDevice)
        Dim nextBoard = New FoundDevice With {.Port = "COM9", .Kind = eDeviceKind.NextFw, .Imei = "867442070217472", .DefaultPin = True}
        Dim identity = Demo.OldFirmwareReply(0, Nothing)
        Dim oldBoard = New FoundDevice With {.Port = "COM12", .Kind = eDeviceKind.OldFirmware, .Identity = identity,
            .BootVer = identity(3), .Hw = identity(4), .Imei = "000000000000048"}
        Dim list As New List(Of FoundDevice)

        Select Case Demo.Mode
            Case eDemoMode.NoPorts
            Case eDemoMode.Two
                list.Add(nextBoard)
                list.Add(New FoundDevice With {.Port = "COM12", .Kind = eDeviceKind.NextFw,
                    .Imei = "867442070217480", .DefaultPin = True})
            Case eDemoMode.OldFw48, eDemoMode.OldFw45
                If Demo.LegacyFwStarted Then
                    nextBoard.Port = "COM12" : list.Add(nextBoard)
                ElseIf demoLegacyBoot Then
                    list.Add(New FoundDevice With {.Port = "COM12", .Kind = eDeviceKind.Bootloader, .BootVer = &H21, .Hw = 139})
                Else
                    list.Add(oldBoard)
                End If
            Case eDemoMode.Boot
                list.Add(New FoundDevice With {.Port = "COM9", .Kind = eDeviceKind.Bootloader, .BootVer = &H32, .Hw = 139})
            Case eDemoMode.OldBoot
                If Demo.LegacyFwStarted Then
                    list.Add(nextBoard)
                Else
                    list.Add(New FoundDevice With {.Port = "COM9", .Kind = eDeviceKind.Bootloader, .BootVer = &H21, .Hw = 139})
                End If
            Case Else
                list.Add(nextBoard)
        End Select

        Return list
    End Function

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

        If demoNoNetwork Then
            s.signal = 99
            s.gsm = 0
            s.gprs = 0
            s.connectToServer = 0
            s.netType = eNetType._Авто
        End If

        Return s
    End Function

    Private demoNoNetwork As Boolean
    Private demoLinkClock As Diagnostics.Stopwatch

    Private Function DemoLinkReply(sequence As Byte) As Byte()
        If demoLinkClock Is Nothing Then demoLinkClock = Diagnostics.Stopwatch.StartNew()
        Dim frame(97) As Byte
        Text.Encoding.ASCII.GetBytes("SLDN").CopyTo(frame, 0)
        frame(4) = 90
        frame(7) = sequence
        frame(8) = LinkDiag.Command
        frame(9) = 5
        frame(10) = If(demoNoNetwork, CByte(2), CByte(7))
        frame(12) = If(demoNoNetwork, CByte(99), CByte(26))
        frame(13) = If(demoNoNetwork, CByte(0), CByte(4))
        frame(14) = If(demoNoNetwork, CByte(3), CByte(1))
        If Not demoNoNetwork Then Text.Encoding.ASCII.GetBytes("25002").CopyTo(frame, 15)
        frame(91) = If(demoNoNetwork, CByte(255), Demo.SignalRsrp(600 + demoLinkClock.Elapsed.TotalSeconds))
        frame(92) = If(demoNoNetwork, CByte(255), CByte(9))
        frame(93) = If(demoNoNetwork, CByte(255), CByte(61))
        frame(94) = If(demoNoNetwork, CByte(255), CByte(7))
        frame(95) = If(demoNoNetwork, CByte(255), CByte(5))
        Dim durations As UInteger() = If(demoNoNetwork,
            New UInteger() {1200, 2700, 600, 60000, 0, 0},
            New UInteger() {1200, 2700, 600, 4200, 1500, 800})
        For i = 0 To 5
            BitConverter.GetBytes(durations(i)).CopyTo(frame, 26 + 4 * i)
        Next
        If demoNoNetwork Then
            Dim failures As UInteger() = {(65UI << 8) Or (1UI << 3) Or 3UI,
                (14UI << 8) Or (7UI << 3) Or 3UI, (64UI << 8) Or (7UI << 3) Or 3UI}
            frame(50) = CByte(failures.Length)
            For i = 0 To failures.Length - 1
                BitConverter.GetBytes(failures(i)).CopyTo(frame, 51 + 4 * i)
            Next
        End If
        BitConverter.GetBytes(CUShort(ClComPort.Crc16(frame, 4, frame.Length - 6))).CopyTo(frame, frame.Length - 2)
        Return frame
    End Function

    ' Строки журнала, похожие на настоящие: обычное событие, переписка с
    ' модемом, предупреждение и ошибка - чтобы было видно разметку цветом.
    Private Function DemoLogLine() As String
        Dim body As Byte()
        Dim kind As Byte
        Select Case (demoTick \ 4) Mod 10
            Case 0
                kind = 1 : body = Text.Encoding.ASCII.GetBytes("AT+CEREG?")
            Case 1
                kind = 2 : body = Text.Encoding.ASCII.GetBytes("+CEREG: 0,1")
            Case 2
                kind = 3 : body = Text.Encoding.ASCII.GetBytes("RING")
            Case 3
                kind = 4 : body = New Byte() {0, 0, 0}
            Case 4
                kind = 5 : body = New Byte() {1, 128, 0}
            Case 5
                kind = 16 : body = New Byte() {4, 0, 156, 24, 0, 0, 3}
            Case 6
                kind = 17 : body = New Byte() {0, 0, 0, 0, 0}
            Case 7
                kind = 18 : body = New Byte() {1, 96, 234, 0, 0}
            Case 8
                kind = 19 : body = New Byte() {22, 1, 1, 4, 50, 53, 48, 48, 50, 0, 0, 85, 9, 30, 7, 3}
            Case Else
                kind = 32 : body = New Byte(31) {}
                body(0) = 7 : body(8) = 38 : body(9) = 49 : body(10) = 50
        End Select
        Dim payload = New Byte(6) {}
        payload(0) = kind
        BitConverter.GetBytes(CUInt(demoTick * 500)).CopyTo(payload, 1)
        If kind = 32 Then payload(5) = 37
        Return String.Join(vbCrLf, New UsbLog().Format(SldnStream.Frame(109, payload.Concat(body).ToArray()))) & vbCrLf
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
        c.simNumber = "+79000000000"
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
