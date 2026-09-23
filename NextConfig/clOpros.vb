Imports System.Globalization
Imports System.Threading

Partial Public Class clOpros

    Const cmdIdx As Integer = 8
    Const payloadIdx As Integer = 9

    Public Event isFinded(ByVal port As String, ByVal imei As String, ByVal isDefPIN As Byte)
    Public Event onFind(ByVal port As String)
    Public Event onStatus(ByVal status As s_status)
    ' на порту отозвался загрузчик: приложения на плате нет или его держат кнопкой
    Public Event onBootFound(ByVal port As String, ByVal bootVer As Byte, ByVal hw As Byte)

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
    Private ports As String() 'список портов в системе
    Private portIndex As Integer 'индекс текущего порта

    Public Enum eRejim
        None
        Search
        GetInfo
        WaitConnect
        Work
        Boot ' на порту загрузчик: опрос стоит, портом распоряжается окно записи
    End Enum

    Public Enum eCmd
        CMD_RESET = 2
        CMD_SETDEF = 3
        REC_FWSTATUS = 4
        CMD_SETOUT = 8
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
                Return "GPRS/EDGE"
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
                ' поиск устройств
                Case eRejim.Search

                    If ports.Count Then

                        If Comport IsNot Nothing AndAlso Comport.Port.IsOpen Then
                            Comport.Port.Close()
                        End If

                        Dim serial As New System.IO.Ports.SerialPort(ports(portIndex))

                        Try
                            With serial
                                .BaudRate = 115200
                                .DataBits = 8
                                .Parity = System.IO.Ports.Parity.None
                                .StopBits = System.IO.Ports.StopBits.One
                                .Handshake = System.IO.Ports.Handshake.None
                                .ReceivedBytesThreshold = 1
                                .ReadBufferSize = 1000
                                .ReadTimeout = 50
                            End With

                            Comport = New ClComPort(serial, False)

                            With Comport
                                .Protokol = ClComPort.EProtokol.BIN
                                .Port.Open()
                            End With

                            If Comport.Port.IsOpen Then
                                RaiseEvent onFind(ports(portIndex))
                                rejim = eRejim.GetInfo
                            Else
                                Thread.Sleep(1000)
                                NextPort()
                            End If

                        Catch ex As Exception
                            Thread.Sleep(1000)
                            NextPort()
                        End Try
                    Else
                        Thread.Sleep(3000)
                        StartFind()
                    End If

                ' пробуем узнать кто на порту
                Case eRejim.GetInfo
                    Dim rec As Byte()

                    If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_IDENT, Nothing), 500) Then
                        If Not ParseRec(rec) Then
                            NextPort()
                        End If
                    Else
                        ' прошивка не ответила — спросим загрузчик его протоколом
                        Dim bootVer As Byte = 0
                        Dim bootHw As Byte = 0

                        If clBoot.Info(Comport, bootVer, bootHw) Then
                            rejim = eRejim.Boot
                            RaiseEvent onBootFound(ports(portIndex), bootVer, bootHw)
                        Else
                            NextPort()
                        End If
                    End If

                ' портом распоряжается окно записи прошивки
                Case eRejim.Boot
                    Thread.Sleep(200)

                Case eRejim.WaitConnect, eRejim.Work

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
                        ' запрос статуса
                        If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_STATUS, Nothing), 3000) Then
                            errCnt = 0

                            If rejim = eRejim.Work Then
                                If Not ParseRec(rec) Then
                                    NextPort()
                                End If
                            End If

                        Else
                            errCnt += 1
                            If errCnt = 5 Then
                                NextPort()
                            End If
                        End If
                        Thread.Sleep(100)
                    Catch ex As Exception
                        NextPort()
                    End Try

                    _isSending = False

                    Thread.Sleep(1000)

                Case Else
                    ' режим None: ждём, а не крутим цикл вхолостую
                    Thread.Sleep(100)

            End Select

        End While

        Debug.WriteLine("opros finish")
        _exit = True

    End Sub

    Private Sub StartFind()
        ports = System.IO.Ports.SerialPort.GetPortNames
        portIndex = 0
        rejim = eRejim.Search
    End Sub

    Private Sub NextPort()
        Comport.Port.Close()
        rejim = eRejim.Search
        portIndex += 1
        If portIndex = ports.Count Then
            StartFind()
        End If
    End Sub

    Private Function SetCmd(ByVal cmd As Byte, ByVal payload As Byte()) As Byte()

        Dim send As Byte()
        Dim len As Integer

        If payload Is Nothing Then
            ReDim send(8)
            len = 3
        Else
            ReDim send(payload.Length + 8)
            len = payload.Length + 3
        End If

        'идентификатор
        send(0) = 83
        send(1) = 76
        send(2) = 68
        send(3) = 78
        'длина
        send(4) = len And &HFF
        send(5) = (len >> 8) And &HFF

        send(6) = &HFF
        send(7) = 0
        send(8) = cmd

        If payload IsNot Nothing Then
            Array.Copy(payload, 0, send, 9, payload.Length)
        End If

        Return send
    End Function

    Private Function ParseRec(ByVal rec As Byte()) As Boolean
        'проверим идентификатор
        If rec.Length < 6 Then Return False

        Dim len As Integer = rec(5)
        len = (len << 8) + rec(4)

        If (len + 8) > rec.Length Then Return False

        Select Case rec(8)
            Case 1 'пакет регистрации
            Case 2 'статус 1
            Case 3 'статус 2
            Case 11 'подтверждение

            Case eCmd.CMD_IDENT 'идентификация
                rejim = eRejim.WaitConnect
                RaiseEvent isFinded(Comport.Port.PortName, System.Text.Encoding.UTF8.GetString(rec, 9, 15), rec(24))
                Return True

            Case eCmd.CMD_STATUS 'статус для конфигуратора
                Dim status As s_status

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

                RaiseEvent onStatus(status)
                Return True

        End Select

        Return False
    End Function

    Public Sub StopGetStatus(ByVal _st As Boolean)
        _stopGetStatus = _st
    End Sub

End Class