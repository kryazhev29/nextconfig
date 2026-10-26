Imports System.Globalization
Imports System.Threading

' Часть класса clOpros: команды прибора по USB
Partial Public Class clOpros

    ' источник команды 8: 1 - мобильное приложение, 2 - API, 3 - тест из админки,
    ' 4 - голосовой помощник
    Private Const SRC_TEST As Byte = 3

    ' Возвращает код ответа контроллера на команду 8: 0 - выполнено, 1 - ошибка параметра,
    ' 2 - выход не включился (не разрешён настройками реле или занят); -1 - ответа нет.
    ' Выходы задаются строго 0 или 1: по Протоколу2 (строки 185-188) значение больше 1 -
    ' ошибка параметра.
    Public Function SetOut(ByVal _o1 As Boolean, ByVal _o2 As Boolean) As Integer
        Dim rez As Integer = -1
        If Demo.IsOn Then Return 0

        SyncLock Me
            _isPause = True
            Try

                While _isSending
                    Thread.Sleep(1)
                End While

                Dim rec As Byte()

                ' Первый байт - источник команды (3 - тест из админки), от него зависит только
                ' номер события в журнале. Выходом контроллер управляет, если настройки реле
                ' разрешают команды сервера.
                ' Полный пакет команды 8: источник, два выхода и 10 байт ID абонента (пустой).
                Dim p(12) As Byte
                p(0) = SRC_TEST
                p(1) = If(_o1, CByte(1), CByte(0))
                p(2) = If(_o2, CByte(1), CByte(0))

                If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_SETOUT, p), 500) Then
                    If SldnStream.IsAnswer(rec, eCmd.CMD_SETOUT) Then
                        rez = SldnStream.AnswerCode(rec)
                    End If
                End If

                ' По ТЗ после ТЕСТ сразу запрашивается статус: включение выхода видно на экране,
                ' даже если импульс короче периода опроса.
                If rez = 0 AndAlso Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_STATUS, Nothing), 500) Then
                    ParseRec(rec)
                End If

            Finally
                _isPause = False
            End Try
        End SyncLock

        Return rez
    End Function

    Public Function ResetController() As Boolean
        Dim rez As Boolean = False

        SyncLock Me

            _isPause = True
            Try

                While _isSending
                    Thread.Sleep(1)
                End While

                Dim rec As Byte()

                If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_RESET, New Byte() {0}), 500) Then
                    If SldnStream.IsAnswer(rec, eCmd.CMD_RESET) Then
                        If SldnStream.AnswerCode(rec) = 0 Then
                            rez = True
                        End If
                    End If
                End If

            Finally
                _isPause = False
            End Try
        End SyncLock

        Return rez
    End Function

    Public Function SetDefault() As Boolean
        Dim rez As Boolean = False

        SyncLock Me

            _isPause = True
            Try

                While _isSending
                    Thread.Sleep(1)
                End While

                Dim rec As Byte()

                If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_SETDEF, New Byte() {0}), 15000) Then
                    If SldnStream.IsAnswer(rec, eCmd.CMD_SETDEF) Then
                        If SldnStream.AnswerCode(rec) = 0 Then
                            rez = True
                        End If
                    End If
                End If

            Finally
                _isPause = False
            End Try
        End SyncLock

        Return rez
    End Function

    Public Function SetTransparentMode() As Boolean
        Dim rez As Boolean = False

        SyncLock Me

            _isPause = True
            Try

                While _isSending
                    Thread.Sleep(1)
                End While

                Dim rec As Byte()

                If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_TRANSPARENT, New Byte() {0}), 300) Then
                    If rec(cmdIdx) = eCmd.CMD_TRANSPARENT Then
                        rez = True
                    End If
                End If

            Finally
                _isPause = False
            End Try
        End SyncLock

        Return rez
    End Function

    ' Строки в пакете настроек добиты нулями до полной длины поля. Если их не
    ' срезать, значение с платы не совпадает с тем же значением из окна.
    Private Shared Function ZStr(ByVal rec As Byte(), ByVal offset As Integer, ByVal length As Integer) As String
        Dim s As String = System.Text.Encoding.UTF8.GetString(rec, offset, length)
        Dim i As Integer = s.IndexOf(ChrW(0))

        If i >= 0 Then s = s.Substring(0, i)

        Return s
    End Function

    Public Function SetConfig(ByVal _conf As s_config) As Boolean

        If _conf.rawConfig Is Nothing OrElse _conf.rawConfig.Length <> 512 Then Return False
        Dim bt_conf = EncodeConfig(_conf)
        If Demo.IsOn Then Return True
        Dim rez As Boolean = False

        SyncLock Me
            _isPause = True
            Try

                While _isSending
                    Thread.Sleep(1)
                End While

                Dim rec As Byte()


                If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_SETCONFIG, bt_conf), 3000) Then
                    If rec(cmdIdx) = eCmd.REC_TIMESHTAMP Then
                        If rec(payloadIdx) = 4 Then
                            rez = True
                        End If
                    End If
                End If

            Catch ex As ArgumentException
                Return False
            Finally
                _isPause = False
            End Try
        End SyncLock

        Return rez
    End Function

    Public Shared Function EncodeConfig(_conf As s_config) As Byte()
        Dim bt_conf(511) As Byte
        If _conf.rawConfig IsNot Nothing Then Array.Copy(_conf.rawConfig, bt_conf, 512)
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
        bt_conf(174) = _conf.rs485Baud
        ' Остальные резервные байты сохраняются.
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

        Return bt_conf
    End Function

    Public Shared Sub CopyStrToBuffer(ByVal _s As String, ByRef bt_conf As Byte(), ByVal idx As Integer, ByVal sz As Integer)
        Dim old = System.Text.Encoding.UTF8.GetString(bt_conf, idx, sz).Split(ChrW(0))(0)
        If old = If(_s, "") Then Return
        If System.Text.Encoding.UTF8.GetByteCount(If(_s, "")) >= sz Then
            Throw New ArgumentException("Значение превышает допустимую длину " & (sz - 1) & " байт.", NameOf(_s))
        End If
        Array.Clear(bt_conf, idx, sz)
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

    Public Shared Function DecodeConfig(rec As Byte()) As s_config
        If rec Is Nothing OrElse rec.Length < 523 OrElse rec(8) <> eCmd.CMD_GETCONFIG Then
            Throw New ArgumentException("Неполный ответ с настройками контроллера.", NameOf(rec))
        End If
        Dim parsed As New s_config
        parsed.rawConfig = New Byte(511) {}
        Array.Copy(rec, 9, parsed.rawConfig, 0, 512)
        parsed.time = BitConverter.ToUInt32(rec, 9)
        parsed.pin = BitConverter.ToUInt32(rec, 13)
        parsed.server1 = ZStr(rec, 17, 32)
        parsed.server2 = ZStr(rec, 49, 32)
        parsed.ntpServer = ZStr(rec, 81, 32)
        parsed.opType = ZStr(rec, 113, 32)
        parsed.netType = rec(145)
        parsed.countryCode = ZStr(rec, 146, 8)
        parsed.simNumber = ZStr(rec, 154, 16)
        parsed.ussdBalance = ZStr(rec, 170, 8)
        parsed.resetTime = rec(178)
        parsed.fullLog = rec(179)
        parsed.VoLTE = rec(180)
        parsed.rs485Mode = rec(181)
        parsed.enableNTPsync = rec(182)
        parsed.rs485Baud = rec(183)
        ' Остальные резервные байты сохраняются.
        parsed.wiegandType = rec(187)
        parsed.periph1.rs485Adr = rec(188)
        parsed.periph1.rs485Type = rec(189)
        parsed.periph2.rs485Adr = rec(190)
        parsed.periph2.rs485Type = rec(191)
        parsed.periph3.rs485Adr = rec(192)
        parsed.periph3.rs485Type = rec(193)
        parsed.periph4.rs485Adr = rec(194)
        parsed.periph4.rs485Type = rec(195)
        parsed.periph5.rs485Adr = rec(196)
        parsed.periph5.rs485Type = rec(197)
        parsed.periph6.rs485Adr = rec(198)
        parsed.periph6.rs485Type = rec(199)
        parsed.periph7.rs485Adr = rec(200)
        parsed.periph7.rs485Type = rec(201)
        parsed.periph8.rs485Adr = rec(202)
        parsed.periph8.rs485Type = rec(203)
        parsed.out1.unBlockUse = rec(204)
        parsed.out1.outMode = rec(205)
        parsed.out1.onServerCmd = rec(206)
        parsed.out1.onWiegand = rec(207)
        parsed.out1.onCam = rec(208)
        parsed.out1.onRFID = rec(209)
        parsed.out1.onOther485 = rec(210)
        parsed.out1.onRF433 = rec(211)
        parsed.out1.impulse = rec(212)
        parsed.out1.pause = rec(213)
        parsed.out1.repeate = rec(214)
        parsed.out2.unBlockUse = rec(215)
        parsed.out2.outMode = rec(216)
        parsed.out2.onServerCmd = rec(217)
        parsed.out2.onWiegand = rec(218)
        parsed.out2.onCam = rec(219)
        parsed.out2.onRFID = rec(220)
        parsed.out2.onOther485 = rec(221)
        parsed.out2.onRF433 = rec(222)
        parsed.out2.impulse = rec(223)
        parsed.out2.pause = rec(224)
        parsed.out2.repeate = rec(225)
        parsed.in1Func = rec(226)
        parsed.in2Func = rec(227)
        parsed.useServer1 = rec(228)
        parsed.useserver2 = rec(229)

        Return parsed
    End Function

    Public Function GetLinkDiag(cancel As CancellationToken) As LinkSnapshot
        SyncLock Me
            cancel.ThrowIfCancellationRequested()
            _isPause = True
            Try
                Dim request = SetCmd(LinkDiag.Command, Nothing)
                Dim reply As Byte() = Nothing
                If Demo.IsOn Then
                    reply = DemoLinkReply(request(7))
                    AgentLog.SldnExchange(If(demoNoNetwork, "COM12", "COM9"), request, reply, 0)
                ElseIf Not Comport.SendRecCmd(reply, request, 1000) Then
                    Return Nothing
                End If
                Return LinkDiag.Parse(reply)
            Catch ex As ArgumentException
                Return Nothing
            Finally
                _isPause = False
            End Try
        End SyncLock
    End Function

    Public Function GetConfig(ByRef _conf As s_config) As Boolean
        Dim rez As Boolean = False

        If Demo.IsOn Then
            _conf = DemoConfig()
            Return True
        End If

        SyncLock Me

            _isPause = True
            Try

                While _isSending
                    Thread.Sleep(1)
                End While

                Dim rec As Byte()

                If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_GETCONFIG, Nothing), 500) Then
                    If rec(cmdIdx) = eCmd.CMD_GETCONFIG Then

                        _conf = DecodeConfig(rec)

                        rez = True
                    End If
                End If

            Catch ex As ArgumentException
                Return False
            Finally
                _isPause = False
            End Try
        End SyncLock

        Return rez
    End Function

    Public Function SetTime(ByVal _t As UInteger) As Boolean
        Dim rez As Boolean = False

        SyncLock Me

            _isPause = True
            Try

                While _isSending
                    Thread.Sleep(1)
                End While

                Dim rec As Byte()

                If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_SETTIME, BitConverter.GetBytes(_t)), 500) Then
                    If SldnStream.IsAnswer(rec, eCmd.CMD_SETTIME) Then
                        If SldnStream.AnswerCode(rec) = 0 Then
                            rez = True
                        End If
                    End If
                End If

            Finally
                _isPause = False
            End Try
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

    ' ПИН уходит текстом, 4 или 5 цифр: так его разбирает прошивка (user_parse_pin).
    Public Function Autorize(ByVal pin As String) As Boolean
        Dim rez As Boolean = False

        If Demo.IsOn Then Return True

        SyncLock Me

            _isPause = True
            Try

                While _isSending
                    Thread.Sleep(1)
                End While

                Dim rec As Byte()

                If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_AUTORIZE, System.Text.Encoding.ASCII.GetBytes(pin)), 500) Then
                    If rec(cmdIdx) = eCmd.CMD_AUTORIZE Then
                        If rec(payloadIdx) <> 0 Then
                            rez = True
                        End If
                    End If
                End If

            Finally
                _isPause = False
            End Try
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
            Try

                While _isSending
                    Thread.Sleep(1)
                End While

                Dim rec As Byte()

                If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_SETMODULE, New Byte() {_o1}), 5000) Then
                    If rec(cmdIdx) = eCmd.CMD_SETMODULE Then
                        rez = True
                    End If
                End If

            Finally
                _isPause = False
            End Try
        End SyncLock

        Return rez
    End Function

    Public Function EnableLog(ByVal _o1 As Byte) As Boolean
        LogBits = CByte(_o1 And 7) ' выбор сеанса сохраняется и при потере ответа 108
        If Demo.IsOn Then
            Return True
        End If
        Dim rez As Boolean = False

        SyncLock Me
            _isPause = True
            Try

                While _isSending
                    Thread.Sleep(1)
                End While

                Dim rec As Byte()

                If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_ENABLELOG, New Byte() {_o1}), 500) Then
                    If rec(cmdIdx) = eCmd.CMD_ENABLELOG Then
                        rez = True
                    End If
                End If

            Finally
                _isPause = False
            End Try
        End SyncLock

        Return rez
    End Function

    ' Wi-Fi-мост DR164 (команда 113). Тело "SSID\0ключ\0" пишет точку доступа в мост, пустое — состояние.
    ' Ответ два байта: после записи — код (0 принято, 1 занято, 2 неверные данные);
    ' при запросе — канал сервера через мост (0/1) и связь (0 нет точки, 1 точка есть, 2 TCP открыт). Nothing — ответа нет.
    Public Function WifiBridge(ByVal payload As Byte()) As Byte()
        If Demo.IsOn Then Return Demo.WifiReply()
        Dim reply As Byte() = Nothing
        SyncLock Me
            _isPause = True
            Try
                While _isSending
                    Thread.Sleep(1)
                End While
                Dim rec As Byte()
                Dim body = If(payload Is Nothing OrElse payload.Length = 0, Nothing, payload)
                If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_WIFI, body), 1000) Then
                    If rec IsNot Nothing AndAlso rec.Length >= cmdIdx + 5 AndAlso rec(cmdIdx) = eCmd.CMD_WIFI Then
                        reply = rec.Skip(cmdIdx + 1).Take(rec.Length - cmdIdx - 3).ToArray()
                    End If
                End If
            Finally
                _isPause = False
            End Try
        End SyncLock
        Return reply
    End Function

    ' Локальная запись device: серверная команда 12 не содержит этих полей.
    Public Function SetWifiModem(model As Byte, policy As Byte) As Boolean
        If model > 1 OrElse policy > 2 OrElse (model = 0 AndAlso policy <> 0) Then Return False
        If Demo.IsOn Then
            Demo.WifiChoice = {model, policy}
            Return True
        End If
        SyncLock Me
            _isPause = True
            Try
                While _isSending
                    Thread.Sleep(1)
                End While
                Dim rec As Byte() = Nothing
                Return Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_WIFI_MODEM, New Byte() {model, policy}), 5000) AndAlso
                    SldnStream.IsAnswer(rec, eCmd.CMD_WIFI_MODEM) AndAlso SldnStream.AnswerCode(rec) = 0
            Finally
                _isPause = False
            End Try
        End SyncLock
    End Function

    ' Одна страница списка сетей (команда 115): тело [строка], 255 — искать заново. Nothing — ответа нет.
    Public Function WifiScanPage(row As Byte) As Byte()
        If Demo.IsOn Then Return Demo.WifiScanReply(row)
        Dim reply As Byte() = Nothing
        SyncLock Me
            _isPause = True
            Try
                While _isSending
                    Thread.Sleep(1)
                End While
                Dim rec As Byte() = Nothing
                If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_WIFI_SCAN, New Byte() {row}), 1500) Then
                    If rec IsNot Nothing AndAlso rec.Length >= cmdIdx + 4 AndAlso rec(cmdIdx) = eCmd.CMD_WIFI_SCAN Then
                        reply = rec.Skip(cmdIdx + 1).Take(rec.Length - cmdIdx - 3).ToArray()
                    End If
                End If
            Finally
                _isPause = False
            End Try
        End SyncLock
        Return reply
    End Function

    ' Одна страница базы ID (команда 116): тело [первая ячейка]. Nothing — ответа нет (и у прошивок без 116).
    Public Function IdsPage(first As UShort) As Byte()
        If Demo.IsOn Then Return Demo.IdsReply(first)
        Dim reply As Byte() = Nothing
        SyncLock Me
            _isPause = True
            Try
                While _isSending
                    Thread.Sleep(1)
                End While
                Dim rec As Byte() = Nothing
                If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_IDS_PAGE, BitConverter.GetBytes(first)), 1500) Then
                    If rec IsNot Nothing AndAlso rec.Length >= cmdIdx + 6 AndAlso rec(cmdIdx) = eCmd.CMD_IDS_PAGE Then
                        reply = rec.Skip(cmdIdx + 1).Take(rec.Length - cmdIdx - 3).ToArray()
                    End If
                End If
            Finally
                _isPause = False
            End Try
        End SyncLock
        Return reply
    End Function

    ' Вся база ID страницами; progress — сколько записей прочитано. Nothing — плата не отдала базу.
    Public Function ReadIds(progress As IProgress(Of Integer)) As List(Of IdCell)
        Dim cells As New List(Of IdCell)
        Dim cell = 1, misses = 0, busy = 0
        Do
            Dim nextCell = 0
            Select Case IdsExport.ParsePage(IdsPage(CUShort(cell)), cells, nextCell)
                Case eIdsPage.Ready
                    misses = 0 : busy = 0
                    If progress IsNot Nothing Then progress.Report(cells.Count)
                    If nextCell = 0 Then Return cells
                    cell = nextCell
                Case eIdsPage.Busy
                    busy += 1
                    If busy >= 50 Then Return Nothing
                    Thread.Sleep(200)
                Case Else
                    misses += 1
                    If misses >= 3 Then Return Nothing
            End Select
        Loop
    End Function

    ' Список сетей целиком: поиск заново, ожидание не дольше WifiNetworks.SearchLimitMs, затем страницы до конца.
    Public Function WifiScan() As WifiScanResult
        Dim result As New WifiScanResult
        Dim started = Environment.TickCount, row = 255, misses = 0
        Do
            Dim nextRow = 255
            Dim status = WifiNetworks.ParsePage(WifiScanPage(CByte(row)), result.Networks, nextRow)
            If status = eWifiScan.NoReply Then
                misses += 1
                If misses >= 3 Then result.Status = eWifiScan.NoReply : Return result
                Thread.Sleep(WifiNetworks.PollMs) : Continue Do
            End If
            misses = 0
            If status = eWifiScan.Ready Then
                If nextRow = 255 Then result.Status = eWifiScan.Ready : Return result
                row = nextRow : Continue Do
            End If
            If status <> eWifiScan.Searching Then result.Status = status : Return result
            If Environment.TickCount - started > WifiNetworks.SearchLimitMs Then result.Status = eWifiScan.Timeout : Return result
            row = 0
            Thread.Sleep(WifiNetworks.PollMs)
        Loop
    End Function

    Public Function StartUpdate(ByVal fwI As s_fwinfo) As eCmdAnswer
        If Demo.IsOn Then Return DemoStartUpdate(fwI)
        Dim rez As eCmdAnswer = eCmdAnswer.CMD_UNDEFRESULT

        SyncLock Me
            _isPause = True
            Try

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
                    If SldnStream.IsAnswer(rec, eCmd.CMD_STARTUPDATE) Then
                        rez = SldnStream.AnswerCode(rec)
                    End If
                End If

            Finally
                _isPause = False
            End Try
        End SyncLock

        Return rez
    End Function

    Public Function WriteFWBlock(ByVal nBlock As UInt16, ByVal buf As Byte(), ByRef _percent As Byte) As eCmdAnswer
        If Demo.IsOn Then Return DemoWriteBlock(nBlock, _percent)
        Dim rez As eCmdAnswer = eCmdAnswer.CMD_UNDEFRESULT

        SyncLock Me
            _isPause = True
            Try

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

            Finally
                _isPause = False
            End Try
        End SyncLock

        Return rez
    End Function

End Class
