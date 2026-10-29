Imports System.Globalization
Imports System.Threading

Partial Public Class clOpros

    Const cmdIdx As Integer = 8
    Const payloadIdx As Integer = 9

    ' Итог одного прохода по всем портам: какие платы на них отвечают.
    ' Сам поиск ничего не открывает - плату выбирает оператор.
    Public Event onScan(ByVal devices As List(Of FoundDevice), ByVal portCount As Integer)
    Public Event onStatus(ByVal status As s_status)
    ' выбранная плата перестала отвечать на статус
    Public Event onLost()

    Public Enum eDeviceKind
        NextFw      ' прошивка NEXT: вход по ПИН-коду
        OldFirmware ' старая прошивка v5: только чтение и перевод
        Bootloader  ' загрузчик: приложения нет или плату держат кнопкой
    End Enum

    Public Class FoundDevice
        Public Port As String
        Public Kind As eDeviceKind
        Public Imei As String = ""
        Public DefaultPin As Boolean
        Public Identity As Byte()
        Public BootVer As Byte
        Public Hw As Byte

        ' одна и та же плата на том же порту
        Public ReadOnly Property Key As String
            Get
                Return Port & "|" & CInt(Kind) & "|" & Imei & "|" & BootVer
            End Get
        End Property

        Public ReadOnly Property Title As String
            Get
                Select Case Kind
                    Case eDeviceKind.NextFw
                        Return Port & " · Домовой NEXT · " & Imei
                    Case eDeviceKind.OldFirmware
                        Return Port & " · старая прошивка " & Identity(5) & " · " & Imei
                    Case Else
                        Return Port & " · загрузчик 0x" & BootVer.ToString("X2") & " · плата " & Hw
                End Select
            End Get
        End Property

        Public Overrides Function ToString() As String
            Return Title
        End Function
    End Class

    Private parrent As Form
    Private WithEvents Comport As ClComPort
    Private command As New s_command
    Private _exit As Boolean
    Private _continue As Boolean
    'Private needClose As Boolean
    Private _pause As Integer = 20
    Private thread_opros As Threading.Thread

    Private _isPause As Boolean = False
    Private _isSending As Boolean = False
    Private _stopGetStatus As Boolean = False
    Public Property LogBits As Byte = 6 ' события GSM и общие; AT включается отдельно

    Public Enum eCommandType
        Bin
        ASC
    End Enum

    Public Enum eNetType
        _Авто = 0
        _GPRS_EDGE = 1
        _3G_4G = 2
        _3G = 3
        _4G = 4
    End Enum

    Public Enum eModuleType
        M_OTHER = 0
        M_UG95 = 1
        M_SLM336 = 2
        M_M66 = 3

        M_UNDEF = 255
    End Enum

    Public Structure s_command
        Dim rec As String
        Dim send As String
        Dim timeout As Integer
        Dim checksum As Boolean
        Dim SendVbCr As Boolean
        Dim removeCS As Boolean
        Dim PreviousDelay As Integer
        Dim [ParamArray] As String()
        Dim brec As Byte()
        Dim bsend As Byte()
        Dim cmd_type As eCommandType
    End Structure

    Public Structure s_status
        Dim time As UInt32
        Dim in1 As Boolean
        Dim in2 As Boolean
        Dim out1 As Boolean
        Dim out2 As Boolean
        Dim readyToUpdate As Boolean
        Dim fw_version As Byte
        Dim fw_build As Integer
        Dim boot_version As Byte
        Dim hw_version As Byte
        Dim signal As Byte
        Dim balance As UInt16
        Dim gsm As Byte
        Dim gprs As Byte
        Dim connectToServer As Byte
        Dim netType As eNetType
        Dim peripheryState() As Byte
        Dim usedMem As UInt16
        Dim moduleType As eModuleType
    End Structure

    Public Structure s_periphery
        Dim rs485Adr As Byte
        Dim rs485Type As Byte
    End Structure

    Public Structure s_outparam
        Dim unBlockUse As Byte
        Dim outMode As Byte
        Dim onServerCmd As Byte
        Dim onWiegand As Byte
        Dim onCam As Byte
        Dim onRFID As Byte
        Dim onOther485 As Byte
        Dim onRF433 As Byte
        Dim impulse As Byte
        Dim pause As Byte
        Dim repeate As Byte
    End Structure

    Public Structure s_config
        Dim time As UInt32
        Dim pin As UInt32
        Dim server1 As String
        Dim server2 As String
        Dim ntpServer As String
        Dim opType As String
        Dim netType As eNetType
        Dim countryCode As String
        Dim simNumber As String
        Dim ussdBalance As String
        Dim resetTime As Integer
        Dim fullLog As Byte
        Dim VoLTE As Byte
        Dim rs485Mode As Byte
        Dim enableNTPsync As Byte
        Dim rs485Baud As Byte
        Dim rawConfig As Byte()
        Dim wiegandType As Byte
        Dim periph1 As s_periphery
        Dim periph2 As s_periphery
        Dim periph3 As s_periphery
        Dim periph4 As s_periphery
        Dim periph5 As s_periphery
        Dim periph6 As s_periphery
        Dim periph7 As s_periphery
        Dim periph8 As s_periphery
        Dim out1 As s_outparam
        Dim out2 As s_outparam
        Dim in1Func As Byte
        Dim in2Func As Byte
        Dim useServer1 As Byte
        Dim useserver2 As Byte
    End Structure

    Public Structure s_fwinfo
        Dim hard As UInt16
        Dim version As UInt16
        Dim build As UInt32
        Dim size As UInt32
        Dim crc As UInt32
        Dim nBlocks As UInt16
    End Structure

    Private ReadOnly arraySendCommand As New ArrayList
    Private ReadOnly Waiter As New ManualResetEvent(True)

    Public Enum eRejim
        None
        Search      ' проход по всем портам, порты после опроса закрываются
        WaitConnect ' порт выбранной платы NEXT открыт, ждём входа по ПИН-коду
        Work
        Boot ' на порту загрузчик: опрос стоит, портом распоряжается окно записи
        OldFirmware ' портом распоряжается только экран чтения v5
    End Enum

    ' поиск и выбор платы не должны делить порт одновременно
    Private ReadOnly scanLock As New Object

    Public Enum eCmd
        CMD_RESET = 2
        CMD_SETDEF = 3
        REC_FWSTATUS = 4
        CMD_GETID = 6 ' захват ID: время, источник (3 — RS-485), адрес; ответ 8 приходит в канал запроса
        CMD_SETOUT = 8
        REC_READEDID = 8
        REC_ANSWER = 11
        CMD_SETCONFIG = 12
        REC_TIMESHTAMP = 12
        REC_GETUPDBLOCK = 13
        CMD_STARTUPDATE = 19
        CMD_SENDBLOCKUPDATE = 20
        CMD_SETTIME = 23

        CMD_IDENT = 100
        CMD_STATUS = 101
        CMD_AUTORIZE = 102
        CMD_CLEARMEM = 103
        'CMD_INFOOBJ = 104
        CMD_SETMODULE = 105
        CMD_GETCONFIG = 106
        CMD_TRANSPARENT = 107
        CMD_ENABLELOG = 108
        CMD_WIFI = 113
        CMD_WIFI_MODEM = 114
        CMD_WIFI_SCAN = 115
        CMD_IDS_PAGE = 116
        CMD_MANUALBOOT = 110
    End Enum

    Public Enum eCmdAnswer
        CMD_SUCCESS = 0
        CMD_PARAMERROR = 1
        CMD_RUNERROR = 2
        CMD_UNDEFRESULT = 3
        CMD_GETPASSWORD = 4
        CMD_RUNNING = 5
        CMD_READYTOUPDATE = 5 ' ответ на команду 19: готов принять прошивку
        CMD_CANCEL = 6
        CMD_GOTONEXT = 7
        CMD_UNDEFIMEI = 8
        CMD_UNDEFOBJECT = 9
        CMD_GOTOBOOLOADER = 253
        CMD_ROLLBACKRUN = 253 ' ответ на команду 19: запущена процедура отката
        CMD_CRCERROR = 254
        CMD_UNDEFINE = 255
    End Enum

    Private rejim As eRejim = eRejim.None

    Public Sub New(ByVal _parent As Form)

        parrent = _parent

        ' в демонстрационном режиме порты не открываем вовсе
        If Demo.IsOn Then
            StartDemo()
            Exit Sub
        End If

        StartFind()
        StartThread()

    End Sub

    Private Sub StartThread()
        _exit = False
        _continue = True

        thread_opros = New Threading.Thread(AddressOf Me.Cikl)
        thread_opros.Priority = Threading.ThreadPriority.Normal
        thread_opros.Name = "opros"
        thread_opros.Start()
    End Sub

    Public Function NetTypeToString(ByVal _nt As eNetType) As String
        Select Case _nt
            Case eNetType._3G
                Return "3G"
            Case eNetType._3G_4G
                Return "3G/4G"
            Case eNetType._4G
                Return "4G"
            Case eNetType._GPRS_EDGE
                Return "2G (GPRS/EDGE)"
            Case eNetType._Авто
                Return "Авто"
        End Select

        Return "---"
    End Function

    Private Sub Cikl()
        'Dim TimeStart As Integer
        Dim errCnt As Integer = 0

        While (_continue)

            Select Case rejim
                ' проход по всем портам
                Case eRejim.Search
                    ScanOnce()

                ' портом распоряжается окно платы; до входа по ПИН-коду опроса нет
                Case eRejim.Boot, eRejim.OldFirmware, eRejim.WaitConnect
                    Thread.Sleep(200)

                Case eRejim.Work
                    SyncLock Me

                    If _isPause Then
                        Thread.Sleep(100)
                        Exit Select
                    End If

                    If _stopGetStatus Then
                        Thread.Sleep(1000)
                        Exit Select
                    End If

                    _isSending = True

                    Dim rec As Byte()

                    Try
                        ' запрос статуса; чужой кадр в ответ - не обрыв, а просто неудачный опрос
                        If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_STATUS, Nothing), 3000) AndAlso ParseRec(rec) Then
                            errCnt = 0
                        Else
                            errCnt += 1
                            If errCnt = 5 Then
                                errCnt = 0
                                Lost()
                            End If
                        End If
                        Thread.Sleep(100)
                    Catch ex As Exception
                        errCnt = 0
                        Lost()
                    End Try

                    _isSending = False
                    End SyncLock

                    Thread.Sleep(1000)

                Case Else
                    ' режим None: ждём, а не крутим цикл вхолостую
                    Thread.Sleep(100)

            End Select

        End While

        Debug.WriteLine("opros finish")
        _exit = True

    End Sub

    ' Старая плата ушла в загрузчик: она появится в списке заново, уже загрузчиком.
    Public Sub FindAfterLegacyReset()
        If Demo.IsOn Then demoLegacyBoot = True
        Release()
    End Sub

    Private Sub StartFind()
        rejim = eRejim.Search
    End Sub

    Private Shared Function NewSerial(name As String) As System.IO.Ports.SerialPort
        Return New System.IO.Ports.SerialPort(name) With {
            .BaudRate = 115200, .DataBits = 8, .Parity = System.IO.Ports.Parity.None,
            .StopBits = System.IO.Ports.StopBits.One, .Handshake = System.IO.Ports.Handshake.None,
            .ReceivedBytesThreshold = 1, .ReadBufferSize = 1000, .ReadTimeout = 50, .WriteTimeout = 2000}
    End Function

    ' Один проход: каждый порт открыть, спросить, кто там, и закрыть.
    ' Если оператор выбрал плату посреди прохода, проход бросается.
    Private Sub ScanOnce()
        Dim names = System.IO.Ports.SerialPort.GetPortNames().Distinct().OrderBy(Function(n) n).ToArray()
        Dim found As New List(Of FoundDevice)

        For Each name In names
            SyncLock scanLock
                If rejim <> eRejim.Search Then Return
                Dim dev = Probe(name)
                If dev IsNot Nothing Then found.Add(dev)
            End SyncLock
        Next

        If rejim <> eRejim.Search Then Return
        RaiseEvent onScan(found, names.Length)

        ' пауза между проходами, прерывается выбором платы
        For i = 1 To 15
            If rejim <> eRejim.Search OrElse Not _continue Then Return
            Thread.Sleep(100)
        Next
    End Sub

    Private Function Probe(name As String) As FoundDevice
        Try
            Using probePort As New ClComPort(NewSerial(name), False)
                probePort.Port.Open()
                Dim rec As Byte() = Nothing

                If probePort.SendRecCmd(rec, SetCmd(eCmd.CMD_IDENT, Nothing), 500) AndAlso
                   rec.Length >= 25 AndAlso rec(8) = eCmd.CMD_IDENT Then
                    Return New FoundDevice With {.Port = name, .Kind = eDeviceKind.NextFw,
                        .Imei = System.Text.Encoding.UTF8.GetString(rec, 9, 15), .DefaultPin = rec(24) > 0}
                End If

                ' прошивка NEXT не ответила - спросим протоколом загрузчика и старой прошивки
                Dim identity = clBoot.IdentifyLegacy(probePort)
                If OldFirmware.IsFirmware(identity) Then
                    Return New FoundDevice With {.Port = name, .Kind = eDeviceKind.OldFirmware,
                        .Identity = identity, .BootVer = identity(3), .Hw = identity(4),
                        .Imei = System.Text.Encoding.GetEncoding(1251).GetString(identity, 10, 16).Split(ChrW(0))(0)}
                End If
                If OldFirmware.IsBootloader(identity) Then
                    Return New FoundDevice With {.Port = name, .Kind = eDeviceKind.Bootloader,
                        .Identity = identity, .BootVer = identity(3), .Hw = identity(4)}
                End If
            End Using
        Catch
            ' порт занят другой программой или пропал - не плата
        End Try
        Return Nothing
    End Function

    ' Оператор выбрал плату: открыть её порт и держать только его.
    Public Function Open(dev As FoundDevice, ByRef errText As String) As Boolean
        SyncLock scanLock
            rejim = eRejim.None
            If Not Demo.IsOn Then
                Try
                    Comport = New ClComPort(NewSerial(dev.Port), False)
                    Comport.Protokol = ClComPort.EProtokol.BIN
                    Comport.Port.Open()
                Catch ex As Exception
                    errText = "Порт " & dev.Port & " не открывается: " & ex.Message
                    AgentLog.Write("device", "event", "open failed", "port", dev.Port, "error", ex.Message)
                    rejim = eRejim.Search
                    Return False
                End Try
            Else
                demoNoNetwork = Demo.Mode = eDemoMode.Two AndAlso dev.Port = "COM12"
            End If
            AgentLog.Write("device", "event", "open", "port", dev.Port, "kind", dev.Kind.ToString(), "title", dev.Title)
            Select Case dev.Kind
                Case eDeviceKind.NextFw : rejim = eRejim.WaitConnect
                Case eDeviceKind.OldFirmware : rejim = eRejim.OldFirmware
                Case Else : rejim = eRejim.Boot
            End Select
            Return True
        End SyncLock
    End Function

    ' Работа с платой закончена: порт отпустить и вернуться к поиску всех плат.
    Public Sub Release()
        rejim = eRejim.None
        SyncLock Me
            If Not Demo.IsOn AndAlso Comport IsNot Nothing AndAlso Comport.Port.IsOpen Then Comport.Port.Close()
        End SyncLock
        AgentLog.Write("device", "event", "release")
        StartFind()
    End Sub

    ' Выбранная плата перестала отвечать: порт закрыть, окно платы решит, что делать.
    Private Sub Lost()
        rejim = eRejim.None
        Try
            If Comport.Port.IsOpen Then Comport.Port.Close()
        Catch
        End Try
        AgentLog.Write("device", "event", "lost")
        RaiseEvent onLost()
    End Sub

    Private commandSequence As Integer
    Private Function SetCmd(ByVal cmd As Byte, ByVal payload As Byte()) As Byte()

        commandSequence = (commandSequence + 1) And 255
        Return SldnStream.Command(cmd, payload, CByte(commandSequence))
    End Function

    Private Function ParseRec(ByVal rec As Byte()) As Boolean
        'проверим идентификатор
        If rec.Length < 11 Then Return False

        Dim len As Integer = rec(5)
        len = (len << 8) + rec(4)

        If (len + 8) > rec.Length Then Return False

        Select Case rec(8)
            Case 1 'пакет регистрации
            Case 2 'статус 1
            Case 3 'статус 2
            Case 11 'подтверждение

            Case eCmd.CMD_STATUS 'статус для конфигуратора
                If rec.Length < 42 Then Return False
                Dim status As s_status

                status.readyToUpdate = (BitConverter.ToUInt16(rec, 13) And &H800) <> 0
                status.time = BitConverter.ToUInt32(rec, 9)
                status.in1 = rec(13) And &H8
                status.in2 = rec(13) And &H4
                status.out1 = rec(13) And &H2
                status.out2 = rec(13) And &H1
                status.fw_version = rec(15)
                status.fw_build = BitConverter.ToUInt32(rec, 16)
                status.boot_version = rec(20)
                status.hw_version = rec(21)
                status.signal = rec(22)
                status.balance = BitConverter.ToUInt16(rec, 23)
                status.gsm = rec(25)
                status.gprs = rec(26)
                status.connectToServer = rec(27)
                status.netType = CType(rec(28), eNetType)

                Dim pS(7) As Byte

                Array.Copy(rec, 29, pS, 0, 8)
                status.peripheryState = pS

                status.usedMem = BitConverter.ToUInt16(rec, 37)
                status.moduleType = CType(rec(39), eModuleType)

                AgentLog.Status(status)
                RaiseEvent onStatus(status)
                Return True

        End Select

        Return False
    End Function

    Public Sub StopGetStatus(ByVal _st As Boolean)
        _stopGetStatus = _st
    End Sub

End Class
