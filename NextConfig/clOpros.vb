Imports System.Globalization
Imports System.Threading

Public Class clOpros

    Const cmdIdx As Integer = 8
    Const payloadIdx As Integer = 9

    Public Event isFinded(ByVal port As String, ByVal imei As String, ByVal isDefPIN As Byte)
    Public Event onFind(ByVal port As String)
    Public Event onStatus(ByVal status As s_status)

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
    End Enum

    Public Enum eCmdAnswer
        CMD_SUCCESS = 0
        CMD_PARAMERROR = 1
        CMD_RUNERROR = 2
        CMD_UNDEFRESULT = 3
        CMD_GETPASSWORD = 4
        CMD_RUNNING = 5
        CMD_CANCEL = 6
        CMD_GOTONEXT = 7
        CMD_UNDEFIMEI = 8
        CMD_UNDEFOBJECT = 9
        CMD_GOTOBOOLOADER = 253
        CMD_CRCERROR = 254
        CMD_UNDEFINE = 255
    End Enum

    Private rejim As eRejim = eRejim.None

    Public Sub New(ByVal _parent As Form)

        parrent = _parent

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
                        NextPort()
                    End If

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

    Public Function SetOut(ByVal _o1 As Byte, ByVal _o2 As Byte) As Boolean
        Dim rez As Boolean = False

        SyncLock Me
            _isPause = True

            While _isSending
                Thread.Sleep(1)
            End While

            Dim rec As Byte()

            If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_SETOUT, New Byte() {255, _o1, _o2}), 500) Then
                If rec(cmdIdx) = eCmd.REC_ANSWER Then
                    If rec(payloadIdx + 1) = 0 Then
                        rez = True
                    End If
                End If
            End If

            _isPause = False
        End SyncLock

        Return rez
    End Function

    Public Function ResetController() As Boolean
        Dim rez As Boolean = False

        SyncLock Me

            _isPause = True

            While _isSending
                Thread.Sleep(1)
            End While

            Dim rec As Byte()

            If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_RESET, New Byte() {0}), 500) Then
                If rec(cmdIdx) = eCmd.REC_ANSWER Then
                    If rec(payloadIdx + 1) = 0 Then
                        rez = True
                    End If
                End If
            End If

            _isPause = False
        End SyncLock

        Return rez
    End Function

    Public Function SetDefault() As Boolean
        Dim rez As Boolean = False

        SyncLock Me

            _isPause = True

            While _isSending
                Thread.Sleep(1)
            End While

            Dim rec As Byte()

            If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_SETDEF, New Byte() {0}), 15000) Then
                If rec(cmdIdx) = eCmd.REC_ANSWER Then
                    If rec(payloadIdx + 1) = 0 Then
                        rez = True
                    End If
                End If
            End If

            _isPause = False
        End SyncLock

        Return rez
    End Function

    Public Function SetTransparentMode() As Boolean
        Dim rez As Boolean = False

        SyncLock Me

            _isPause = True

            While _isSending
                Thread.Sleep(1)
            End While

            Dim rec As Byte()

            If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_TRANSPARENT, New Byte() {0}), 300) Then
                If rec(cmdIdx) = eCmd.CMD_TRANSPARENT Then
                    rez = True
                End If
            End If

            _isPause = False
        End SyncLock

        Return rez
    End Function

    Public Function SetConfig(ByVal _conf As s_config) As Boolean

        Dim bt_conf(511) As Byte
        Dim rez As Boolean = False

        SyncLock Me
            _isPause = True

            While _isSending
                Thread.Sleep(1)
            End While

            Dim rec As Byte()
            Dim bts As Byte()

            bts = BitConverter.GetBytes(_conf.time)
            Array.Copy(bts, 0, bt_conf, 0, 4)
            bts = BitConverter.GetBytes(_conf.pin)
            Array.Copy(bts, 0, bt_conf, 4, 4)
            CopyStrToBuffer(_conf.server1, bt_conf, 8, 32)
            CopyStrToBuffer(_conf.server2, bt_conf, 40, 32)
            CopyStrToBuffer(_conf.ntpServer, bt_conf, 72, 32)
            CopyStrToBuffer(_conf.opType, bt_conf, 104, 32)
            bt_conf(136) = _conf.netType
            CopyStrToBuffer(_conf.countryCode, bt_conf, 137, 8)
            CopyStrToBuffer(_conf.simNumber, bt_conf, 145, 16)
            CopyStrToBuffer(_conf.ussdBalance, bt_conf, 161, 8)
            bt_conf(169) = _conf.resetTime
            bt_conf(170) = _conf.fullLog
            bt_conf(171) = _conf.VoLTE
            bt_conf(172) = _conf.rs485Mode
            bt_conf(173) = _conf.enableNTPsync
            '''
            bt_conf(178) = _conf.wiegandType
            bt_conf(179) = _conf.periph1.rs485Adr
            bt_conf(180) = _conf.periph1.rs485Type
            bt_conf(181) = _conf.periph2.rs485Adr
            bt_conf(182) = _conf.periph2.rs485Type
            bt_conf(183) = _conf.periph3.rs485Adr
            bt_conf(184) = _conf.periph3.rs485Type
            bt_conf(185) = _conf.periph4.rs485Adr
            bt_conf(186) = _conf.periph4.rs485Type
            bt_conf(187) = _conf.periph5.rs485Adr
            bt_conf(188) = _conf.periph5.rs485Type
            bt_conf(189) = _conf.periph6.rs485Adr
            bt_conf(190) = _conf.periph6.rs485Type
            bt_conf(191) = _conf.periph7.rs485Adr
            bt_conf(192) = _conf.periph7.rs485Type
            bt_conf(193) = _conf.periph8.rs485Adr
            bt_conf(194) = _conf.periph8.rs485Type
            bt_conf(195) = _conf.out1.unBlockUse
            bt_conf(196) = _conf.out1.outMode
            bt_conf(197) = _conf.out1.onServerCmd
            bt_conf(198) = _conf.out1.onWiegand
            bt_conf(199) = _conf.out1.onCam
            bt_conf(200) = _conf.out1.onRFID
            bt_conf(201) = _conf.out1.onOther485
            bt_conf(202) = _conf.out1.onRF433
            bt_conf(203) = _conf.out1.impulse
            bt_conf(204) = _conf.out1.pause
            bt_conf(205) = _conf.out1.repeate
            bt_conf(206) = _conf.out2.unBlockUse
            bt_conf(207) = _conf.out2.outMode
            bt_conf(208) = _conf.out2.onServerCmd
            bt_conf(209) = _conf.out2.onWiegand
            bt_conf(210) = _conf.out2.onCam
            bt_conf(211) = _conf.out2.onRFID
            bt_conf(212) = _conf.out2.onOther485
            bt_conf(213) = _conf.out2.onRF433
            bt_conf(214) = _conf.out2.impulse
            bt_conf(215) = _conf.out2.pause
            bt_conf(216) = _conf.out2.repeate
            bt_conf(217) = _conf.in1Func
            bt_conf(218) = _conf.in2Func
            bt_conf(219) = _conf.useServer1
            bt_conf(220) = _conf.useserver2

            If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_SETCONFIG, bt_conf), 3000) Then
                If rec(cmdIdx) = eCmd.REC_TIMESHTAMP Then
                    If rec(payloadIdx) = 4 Then
                        rez = True
                    End If
                End If
            End If

            _isPause = False
        End SyncLock

        Return rez
    End Function

    Public Sub CopyStrToBuffer(ByVal _s As String, ByRef bt_conf As Byte(), ByVal idx As Integer, ByVal sz As Integer)
        If _s Is Nothing Then
            bt_conf(idx) = 0
            Exit Sub
        End If

        Dim bts As Byte() = System.Text.Encoding.UTF8.GetBytes(_s)

        If bts.Length = 0 Then
            bt_conf(idx) = 0
        Else
            If bts.Length < sz Then
                Array.Copy(bts, 0, bt_conf, idx, bts.Length)
                bt_conf(idx + bts.Length) = 0
            Else
                Array.Copy(bts, 0, bt_conf, idx, sz)
                bt_conf(idx + sz - 1) = 0
            End If
        End If
    End Sub

    Public Function GetConfig(ByRef _conf As s_config) As Boolean
        Dim rez As Boolean = False

        SyncLock Me

            _isPause = True

            While _isSending
                Thread.Sleep(1)
            End While

            Dim rec As Byte()

            If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_GETCONFIG, Nothing), 500) Then
                If rec(cmdIdx) = eCmd.CMD_GETCONFIG Then

                    _conf.time = BitConverter.ToUInt32(rec, 9)
                    _conf.pin = BitConverter.ToUInt32(rec, 13)
                    _conf.server1 = System.Text.Encoding.UTF8.GetString(rec, 17, 32)
                    _conf.server2 = System.Text.Encoding.UTF8.GetString(rec, 49, 32)
                    _conf.ntpServer = System.Text.Encoding.UTF8.GetString(rec, 81, 32)
                    _conf.opType = System.Text.Encoding.UTF8.GetString(rec, 113, 32)
                    _conf.netType = rec(145)
                    _conf.countryCode = System.Text.Encoding.UTF8.GetString(rec, 146, 8)
                    _conf.simNumber = System.Text.Encoding.UTF8.GetString(rec, 154, 16)
                    _conf.ussdBalance = System.Text.Encoding.UTF8.GetString(rec, 170, 8)
                    _conf.resetTime = rec(178)
                    _conf.fullLog = rec(179)
                    _conf.VoLTE = rec(180)
                    _conf.rs485Mode = rec(181)
                    _conf.enableNTPsync = rec(182)
                    '''
                    _conf.wiegandType = rec(187)
                    _conf.periph1.rs485Adr = rec(188)
                    _conf.periph1.rs485Type = rec(189)
                    _conf.periph2.rs485Adr = rec(190)
                    _conf.periph2.rs485Type = rec(191)
                    _conf.periph3.rs485Adr = rec(192)
                    _conf.periph3.rs485Type = rec(193)
                    _conf.periph4.rs485Adr = rec(194)
                    _conf.periph4.rs485Type = rec(195)
                    _conf.periph5.rs485Adr = rec(196)
                    _conf.periph5.rs485Type = rec(197)
                    _conf.periph6.rs485Adr = rec(198)
                    _conf.periph6.rs485Type = rec(199)
                    _conf.periph7.rs485Adr = rec(200)
                    _conf.periph7.rs485Type = rec(201)
                    _conf.periph8.rs485Adr = rec(202)
                    _conf.periph8.rs485Type = rec(203)
                    _conf.out1.unBlockUse = rec(204)
                    _conf.out1.outMode = rec(205)
                    _conf.out1.onServerCmd = rec(206)
                    _conf.out1.onWiegand = rec(207)
                    _conf.out1.onCam = rec(208)
                    _conf.out1.onRFID = rec(209)
                    _conf.out1.onOther485 = rec(210)
                    _conf.out1.onRF433 = rec(211)
                    _conf.out1.impulse = rec(212)
                    _conf.out1.pause = rec(213)
                    _conf.out1.repeate = rec(214)
                    _conf.out2.unBlockUse = rec(215)
                    _conf.out2.outMode = rec(216)
                    _conf.out2.onServerCmd = rec(217)
                    _conf.out2.onWiegand = rec(218)
                    _conf.out2.onCam = rec(219)
                    _conf.out2.onRFID = rec(220)
                    _conf.out2.onOther485 = rec(221)
                    _conf.out2.onRF433 = rec(222)
                    _conf.out2.impulse = rec(223)
                    _conf.out2.pause = rec(224)
                    _conf.out2.repeate = rec(225)
                    _conf.in1Func = rec(226)
                    _conf.in2Func = rec(227)
                    _conf.useServer1 = rec(228)
                    _conf.useserver2 = rec(229)

                    rez = True
                End If
            End If

            _isPause = False
        End SyncLock

        Return rez
    End Function

    Public Function ClearMem() As Boolean
        Dim rez As Boolean = False

        SyncLock Me

            _isPause = True

            While _isSending
                Thread.Sleep(1)
            End While

            Dim rec As Byte()

            If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_CLEARMEM, Nothing), 15000) Then
                If rec(cmdIdx) = eCmd.CMD_CLEARMEM Then
                    'If rec(payloadIdx) = 0 Then
                    rez = True
                    'End If
                End If
            End If

            _isPause = False
        End SyncLock

        Return rez
    End Function

    Public Function SetTime(ByVal _t As UInteger) As Boolean
        Dim rez As Boolean = False

        SyncLock Me

            _isPause = True

            While _isSending
                Thread.Sleep(1)
            End While

            Dim rec As Byte()

            If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_SETTIME, BitConverter.GetBytes(_t)), 500) Then
                If rec(cmdIdx) = eCmd.REC_ANSWER Then
                    If rec(payloadIdx) = 0 Then
                        rez = True
                    End If
                End If
            End If

            _isPause = False
        End SyncLock

        Return rez
    End Function

    'Public Function GetInfoObject(ByRef _oi As s_objinfo)
    '    Dim rez As Boolean = False

    '    SyncLock Me

    '        _isPause = True

    '        While _isSending
    '            Thread.Sleep(1)
    '        End While

    '        Dim rec As Byte()

    '        If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_INFOOBJ, Nothing), 500) Then
    '            If rec(cmdIdx) = eCmd.CMD_INFOOBJ Then
    '                '777
    '                rez = True
    '            End If
    '        End If

    '        _isPause = False
    '    End SyncLock

    '    Return rez
    'End Function

    'Public Function UnConnectObject() As Boolean
    '    Return False
    'End Function

    Public Function Autorize(ByVal pin As UInteger) As Boolean
        Dim rez As Boolean = False

        SyncLock Me

            _isPause = True

            While _isSending
                Thread.Sleep(1)
            End While

            Dim rec As Byte()

            If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_AUTORIZE, BitConverter.GetBytes(pin)), 500) Then
                If rec(cmdIdx) = eCmd.CMD_AUTORIZE Then
                    If rec(payloadIdx) <> 0 Then
                        rez = True
                    End If
                End If
            End If

            _isPause = False
        End SyncLock

        Return rez
    End Function

    Public Sub SetRegim(ByVal _rj As eRejim)
        rejim = _rj
    End Sub

    'Private Sub SendRec(ByVal cmds() As s_command)
    '    If cmds IsNot Nothing Then
    '        If cmds.Length > 0 Then
    '            For i As Integer = 0 To cmds.GetUpperBound(0)

    '                If _reset Then
    '                    Exit Sub
    '                End If

    '                With CType(cmds(i), s_command)

    '                    Dim rec As String = ""

    '                    If .cmd_type = eCommandType.ASC Then
    '                        If Comport.SendRec(rec, .send, .timeout, .checksum, .SendVbCr, False, .removeCS, .ParamArray) Then
    '                            Debug.WriteLine("asc ok")
    '                            If Not _reset Then RaiseEvent ValueChangeDCON(.send, rec, True, .cmd_type, rejim)
    '                        Else
    '                            If Not _reset Then RaiseEvent ValueChangeDCON(.send, rec, False, .cmd_type, rejim)
    '                        End If

    '                    Else
    '                    End If

    '                End With

    '            Next

    '        End If

    '        cmds = Nothing
    '        Thread.Sleep(_pause)

    '    End If
    'End Sub

    'Public Sub SendComand(ByVal value As s_command)
    '    'If Not _reset Then
    '    SyncLock arraySendCommand
    '        arraySendCommand.Add(value)
    '    End SyncLock
    '    'End If

    '    Waiter.Set()
    'End Sub


    Public Sub Reset()
        '_reset = True
        'rejim = eRejim.Manual
    End Sub

    Public ReadOnly Property GetComPort As ClComPort
        Get
            Return Comport
        End Get
    End Property


    Public Sub SendCommand(ByVal cmdId As Byte, ByVal payload As Byte())
        'rejim = eRejim.Manual
        'With command
        '    .bsend = send
        '    .cmd_type = clOpros.eCommandType.Bin
        'End With

        'SendComand(command)
    End Sub

    Public WriteOnly Property SetCRC() As Boolean
        Set(ByVal value As Boolean)
            command.checksum = value
        End Set
    End Property

    Public ReadOnly Property GetRejim() As eRejim
        Get
            Return rejim
        End Get
    End Property

    Public Property Pause() As Integer
        Get
            Return _pause
        End Get
        Set(ByVal value As Integer)
            _pause = value
        End Set
    End Property

    Public ReadOnly Property IsExit() As Boolean
        Get
            Return _exit
        End Get
    End Property

    Public Sub Ended()
        _continue = False
    End Sub

    Protected Overrides Sub Finalize()
        MyBase.Finalize()
    End Sub

    Public Function SetModuleType(ByVal _o1 As Byte) As Boolean
        Dim rez As Boolean = False

        SyncLock Me
            _isPause = True

            While _isSending
                Thread.Sleep(1)
            End While

            Dim rec As Byte()

            If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_SETMODULE, New Byte() {_o1}), 5000) Then
                If rec(cmdIdx) = eCmd.CMD_SETMODULE Then
                    rez = True
                End If
            End If

            _isPause = False
        End SyncLock

        Return rez
    End Function

    Public Function EnableLog(ByVal _o1 As Byte) As Boolean
        Dim rez As Boolean = False

        SyncLock Me
            _isPause = True

            While _isSending
                Thread.Sleep(1)
            End While

            Dim rec As Byte()

            If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_ENABLELOG, New Byte() {_o1}), 500) Then
                If rec(cmdIdx) = eCmd.CMD_ENABLELOG Then
                    rez = True
                End If
            End If

            _isPause = False
        End SyncLock

        Return rez
    End Function

    Public Function StartUpdate(ByVal fwI As s_fwinfo) As eCmdAnswer
        Dim rez As eCmdAnswer = eCmdAnswer.CMD_UNDEFRESULT

        SyncLock Me
            _isPause = True

            While _isSending
                Thread.Sleep(1)
            End While

            Dim rec As Byte()
            Dim send(15) As Byte
            Dim bts As Byte()

            bts = BitConverter.GetBytes(fwI.version)
            Array.Copy(bts, 0, send, 0, 2)
            bts = BitConverter.GetBytes(fwI.build)
            Array.Copy(bts, 0, send, 2, 4)
            bts = BitConverter.GetBytes(fwI.size)
            Array.Copy(bts, 0, send, 6, 4)
            bts = BitConverter.GetBytes(fwI.crc)
            Array.Copy(bts, 0, send, 10, 4)
            bts = BitConverter.GetBytes(fwI.nBlocks)
            Array.Copy(bts, 0, send, 14, 2)

            If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_STARTUPDATE, send), 3000) Then
                If rec(cmdIdx) = eCmd.REC_ANSWER Then
                    rez = rec(payloadIdx + 1)
                End If
            End If

            _isPause = False
        End SyncLock

        Return rez
    End Function

    Public Function WriteFWBlock(ByVal nBlock As UInt16, ByVal buf As Byte(), ByRef _percent As Byte) As eCmdAnswer
        Dim rez As eCmdAnswer = eCmdAnswer.CMD_UNDEFRESULT

        SyncLock Me
            _isPause = True

            While _isSending
                Thread.Sleep(1)
            End While

            Dim rec As Byte()
            Dim send(1025) As Byte
            Dim bts As Byte()

            bts = BitConverter.GetBytes(nBlock)
            Array.Copy(bts, 0, send, 0, 2)
            Array.Copy(buf, 0, send, 2, buf.Length)

            If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_SENDBLOCKUPDATE, send), 3000) Then
                If rec(cmdIdx) = eCmd.REC_FWSTATUS Then
                    rez = rec(payloadIdx + 1)
                    _percent = rec(payloadIdx)
                End If
            End If

            _isPause = False
        End SyncLock

        Return rez
    End Function

    Public Sub StopGetStatus(ByVal _st As Boolean)
        _stopGetStatus = _st
    End Sub

End Class