Imports System.Globalization
Imports System.IO

Public Class Form2

    Private WithEvents opros As clOpros
    Private WithEvents tmr1 As New Windows.Forms.Timer
    Private status As clOpros.s_status
    Private config As clOpros.s_config
    Private pinCode As Integer
    Private logBits As Byte = 0
    Private thread_getInfo As Threading.Thread


    Public Sub New(ByVal _opros As clOpros, ByVal _info As String, ByVal _imei As String, ByVal _pin As Integer)

        ' Этот вызов является обязательным для конструктора.
        InitializeComponent()

        ' Добавить код инициализации после вызова InitializeComponent().
        opros = _opros
        lblPortInfo.Text = _info
        lblIMEI.Text = _imei
        lblPIN.Text = System.Text.Encoding.UTF8.GetString(BitConverter.GetBytes(_pin))
        pinCode = _pin
    End Sub

    Private Sub Form2_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        AddHandler opros.onStatus, AddressOf opros_onStatus
        AddHandler tmr1.Tick, AddressOf TmrTick
        tmr1.Interval = 100
        tmr1.Enabled = False

        AddHandler opros.GetComPort.ReceiveComand, AddressOf OnRecStr
        opros.StopGetStatus(False)

        Button1_Click(Nothing, Nothing)
        AddHandler opros.GetComPort.ErrorPort, AddressOf OnErrorPort
    End Sub

    Private Sub Form2_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        RemoveHandler opros.onStatus, AddressOf opros_onStatus

        CheckBox1.Checked = False
        CheckBox2.Checked = False
        CheckBox3.Checked = False
    End Sub

    Private Sub TmrTick()
        Label3.ForeColor = Color.White
        tmr1.Enabled = False
    End Sub

    Private Delegate Sub delegate_onStatus(ByVal status As clOpros.s_status)

    Private Sub opros_onStatus(ByVal status As clOpros.s_status)
        Dim UIUpdater As New delegate_onStatus(AddressOf refOnStatus)
        Me.BeginInvoke(UIUpdater, status)
    End Sub

    Private Sub refOnStatus(ByVal _status As clOpros.s_status)

        status = _status

        lblTime.Text = UnixTimestampToDateTime(status.time).ToString("dd.MM.yyyy HH:mm:ss")
        lblFW.Text = status.fw_version
        lblBoot.Text = status.boot_version
        lblHW.Text = status.hw_version

        If status.in1 Then
            lbIN1.Text = "Вкл"
            lbIN1.ForeColor = Color.LimeGreen
        Else
            lbIN1.Text = "Выкл"
            lbIN1.ForeColor = Color.Red
        End If

        If status.in2 Then
            lblIN2.Text = "Вкл"
            lblIN2.ForeColor = Color.LimeGreen
        Else
            lblIN2.Text = "Выкл"
            lblIN2.ForeColor = Color.Red
        End If

        If status.out1 Then
            lblOUT1.Text = "Вкл"
            lblOUT1.ForeColor = Color.LimeGreen
        Else
            lblOUT1.Text = "Выкл"
            lblOUT1.ForeColor = Color.Red
        End If

        If status.out2 Then
            lbOUT2.Text = "Вкл"
            lbOUT2.ForeColor = Color.LimeGreen
        Else
            lbOUT2.Text = "Выкл"
            lbOUT2.ForeColor = Color.Red
        End If

        lblGSM.Text = If(status.gsm = 1, "Подключено", "Отключено")
        lblSig.Text = status.signal
        pbSig.Value = status.signal
        lblInt.Text = If(status.gprs = 1, "Подключено", "Отключено")
        lblNet.Text = opros.NetTypeToString(status.netType)
        lblServerConnect.Text = If(status.connectToServer, "Подключено", "Отключено")

        lblUsedMem.Text = status.usedMem & "/50000"
        lblModuleType.Text = status.moduleType.ToString

        Label3.ForeColor = Color.Black
        tmr1.Enabled = True

    End Sub

    Private Sub btnUpdateFW_Click(sender As Object, e As EventArgs) Handles btnUpdateFW.Click
        OpenFileDialog1.Filter = "Файлы прошивки (*.bin)|*.bin|Все файлы (*.*)|*.*"
        OpenFileDialog1.FileName = "FW_xx.bin"
        If OpenFileDialog1.ShowDialog = DialogResult.OK Then
            Using f As New frmFW(OpenFileDialog1.FileName, opros)
                If f.ShowDialog() = DialogResult.OK Then
                    Me.Close()
                End If
            End Using
        End If
    End Sub

    Private Sub btnUpdateBoot_Click(sender As Object, e As EventArgs) Handles btnUpdateBoot.Click
        OpenFileDialog1.Filter = "Файлы прошивки (*.bin)|*.bin|Все файлы (*.*)|*.*"
        OpenFileDialog1.FileName = "Boot_xx.bin"
        If OpenFileDialog1.ShowDialog = DialogResult.OK Then
            '777
        End If
    End Sub

    Private Sub btnTestOUT_Click(sender As Object, e As EventArgs) Handles btnTestOUT2.Click, btnTestOUT1.Click
        Dim _o1 As Boolean = Not status.out1
        Dim _o2 As Boolean = Not status.out2

        Select Case CType(sender, Label).Tag
            Case 0
                opros.SetOut(_o1, status.out2)

            Case 1
                opros.SetOut(status.out1, _o2)

        End Select
    End Sub

    Private Sub btnTranspMode_Click(sender As Object, e As EventArgs) Handles btnTranspMode.Click
        Using f As New frmInfo("Информация", "Перевести контроллер в прозрачный режим с GSM модулем?" & vbCrLf & "Конфигуратор будет закрыт!",, True)
            f.Button1.Text = "Да"
            f.Button2.Text = "Нет"
            If f.ShowDialog() = DialogResult.OK Then
                If opros.SetTransparentMode() Then
                    Using f2 As New frmInfo("Информация", "Контроллер переведен в прозрачный режим с GSM модулем!" & vbCrLf &
                                            "Программа будет закрыта")
                        f2.ShowDialog()
                    End Using
                    Me.DialogResult = DialogResult.Abort
                    Me.Close()
                Else
                    Using f2 As New frmInfo("Информация", "Команда не поддерживается!" & vbCrLf &
                                            "Или нет ответа контроллера")
                        f2.ShowDialog()
                    End Using
                End If
            End If
        End Using
    End Sub

    Private Sub btnFactory_Click(sender As Object, e As EventArgs) Handles btnFactory.Click
        Using f As New frmInfo("Информация", "Вы уверены что хотите сбросить контроллер на заводские настройки?",, True)
            f.Button1.Text = "Да"
            f.Button2.Text = "Нет"
            If f.ShowDialog() = DialogResult.OK Then
                Dim str As String
                If opros.SetDefault() Then
                    str = "Настройки установлены по умолчанию!"
                Else
                    str = "Не удалось установить настройки!" & vbCrLf & "Повторите операцию!"
                End If
                Using f2 As New frmInfo("Информация", str)
                    f2.ShowDialog()
                End Using
            End If
        End Using
    End Sub

    Private Sub btnChangePIN_Click(sender As Object, e As EventArgs) Handles btnChangePIN.Click
        Using f As New frmPIN(pinCode)
            If f.ShowDialog = DialogResult.OK Then
                config.pin = f.nPin
                UpdateConfigFields()
            End If
        End Using
    End Sub

    Private Sub lblOperator_Click(sender As Object, e As EventArgs) Handles lblOperator.Click
        Dim arr As New ArrayList

        arr.Add(New frmSelecter.mItem With {.ID = 1, .Name = "Автоматически"})
        arr.Add(New frmSelecter.mItem With {.ID = 2, .Name = "Указать в ручную"})
        arr.Add(New frmSelecter.mItem With {.ID = 0, .Name = "Отмена", .ButtonBackColor = Color.Red})

        Using f As New frmSelecter("Тип оператора", arr, 1, 14)
            f.ShowDialog()

            Select Case f.ID
                Case 0
                    Exit Sub

                Case 1
                    config.opType = ""

                Case 2
                    Using f2 As New frmText("Наименование", "Введите значение", config.opType)
                        If f2.ShowDialog = DialogResult.OK Then
                            config.opType = f2.vText
                        End If
                    End Using

            End Select

            UpdateConfigFields()

        End Using
    End Sub

    Private Sub lblVoLTE_Click(sender As Object, e As EventArgs) Handles lblVoLTE.Click
        Dim arr As New ArrayList

        arr.Add(New frmSelecter.mItem With {.ID = 1, .Name = "Использовать"})
        arr.Add(New frmSelecter.mItem With {.ID = 0, .Name = "Не использовать"})
        arr.Add(New frmSelecter.mItem With {.ID = -1, .Name = "Отмена", .ButtonBackColor = Color.Red})

        Using f As New frmSelecter("VoLTE", arr, 1, 14)
            f.ShowDialog()

            Select Case f.ID
                Case -1
                    Exit Sub

                Case Else
                    config.VoLTE = f.ID

            End Select

            UpdateConfigFields()

        End Using
    End Sub

    Private Sub btnSave_Click(sender As Object, e As EventArgs) Handles btnSave.Click
        Using f As New frmInfo("Информация", "Сохранить настройки?",, True)
            f.Button1.Text = "Да"
            f.Button2.Text = "Нет"
            If f.ShowDialog() = DialogResult.OK Then
                Dim str As String
                If opros.SetConfig(config) Then
                    str = "Настройки сохранены!"
                Else
                    str = "Не удалось сохранить настройки!" & vbCrLf & "Повторите операцию!"
                End If
                Using f2 As New frmInfo("Информация", str)
                    f2.ShowDialog()
                End Using
            End If
        End Using
    End Sub

    Private Sub btnDefault_Click(sender As Object, e As EventArgs) Handles btnDefault.Click
        Using f As New frmInfo("Информация", "Вы уверены что хотите установить настройки по умолчанию?",, True)
            f.Button1.Text = "Да"
            f.Button2.Text = "Нет"
            If f.ShowDialog() = DialogResult.OK Then
                Dim str As String

                SetDefaultConfig()
                If opros.SetConfig(config) Then
                    str = "Настройки установлены по умолчанию!"
                Else
                    str = "Не удалось установить настройки!" & vbCrLf & "Повторите операцию!"
                End If
                Using f2 As New frmInfo("Информация", str)
                    f2.ShowDialog()
                End Using
            End If
        End Using
    End Sub

    Private Sub SetDefaultConfig()
        With config
            .time = DateTimeToUnixTimestamp(Now)
            .pin = BitConverter.ToUInt32(System.Text.Encoding.UTF8.GetBytes("4321"), 0)
            .server1 = "93.189.229.197:15010"
            .useServer1 = 1
            .server2 = "93.189.229.197:15020"
            .useserver2 = 0
            .ntpServer = ""
            .opType = ""
            .netType = clOpros.eNetType._Авто
            .countryCode = "+7"
            .simNumber = ""
            .ussdBalance = ""
            .resetTime = 0
            .fullLog = 0
            .VoLTE = 0
            .rs485Mode = 1
            .enableNTPsync = 0
            .wiegandType = 0
            .periph1.rs485Adr = 0
            .periph1.rs485Type = 0
            .periph2.rs485Adr = 0
            .periph2.rs485Type = 0
            .periph3.rs485Adr = 0
            .periph3.rs485Type = 0
            .periph4.rs485Adr = 0
            .periph4.rs485Type = 0
            .periph5.rs485Adr = 0
            .periph5.rs485Type = 0
            .periph6.rs485Adr = 0
            .periph6.rs485Type = 0
            .periph7.rs485Adr = 0
            .periph7.rs485Type = 0
            .periph8.rs485Adr = 0
            .periph8.rs485Type = 0
            With .out1
                .unBlockUse = 0
                .outMode = 0
                .onServerCmd = 1
                .onCam = 0
                .onOther485 = 0
                .onRF433 = 0
                .onRFID = 0
                .onWiegand = 0
                .impulse = 1
                .pause = 3
                .repeate = 1
            End With
            With .out2
                .unBlockUse = 0
                .outMode = 0
                .onServerCmd = 1
                .onCam = 0
                .onOther485 = 0
                .onRF433 = 0
                .onRFID = 0
                .onWiegand = 0
                .impulse = 1
                .pause = 3
                .repeate = 1
            End With
            .in1Func = 0
            .in2Func = 0

        End With

        UpdateConfigFields()
    End Sub

    Private Sub UpdateConfigFields()
        With config
            lblPIN.Text = System.Text.Encoding.UTF8.GetString(BitConverter.GetBytes(.pin))
            lblOperator.Text = If(.opType = "", "Автоматически", .opType)
            lblVoLTE.Text = If(.VoLTE = 0, "Выключено", "Включено")
            lblServer1.Text = .server1
            lblServer2.Text = .server2
            chServer1.Checked = .useServer1
            chServer2.Checked = .useserver2
        End With

    End Sub

    Private Sub btnExport_Click(sender As Object, e As EventArgs) Handles btnExport.Click
        '777
    End Sub

    Private Sub btnImport_Click(sender As Object, e As EventArgs) Handles btnImport.Click
        '777
    End Sub

    Private Sub btnClear_Click(sender As Object, e As EventArgs) Handles btnClear.Click
        Using f As New frmInfo("Информация", "Вы уверены что хотите удалить из памяти все идентификаторы?",, True)
            f.Button1.Text = "Да"
            f.Button2.Text = "Нет"
            If f.ShowDialog() = DialogResult.OK Then
                Dim str As String
                If opros.ClearMem() Then
                    str = "Память идентификаторов очищена!"
                Else
                    str = "Не удалось очистить память идентифиаторов!" & vbCrLf & "Повторите операцию!"
                End If
                Using f2 As New frmInfo("Информация", str)
                    f2.ShowDialog()
                End Using
            End If
        End Using
    End Sub

    Private Sub btnUpdateObj_Click(sender As Object, e As EventArgs) Handles btnUpdateObj.Click

        thread_getInfo = New Threading.Thread(AddressOf tGetInfo)
        thread_getInfo.Priority = Threading.ThreadPriority.Normal
        thread_getInfo.Name = "getInfo"
        thread_getInfo.Start()

    End Sub

    Private Sub btnUnConnect_Click(sender As Object, e As EventArgs) Handles btnUnConnect.Click
        Using f As New frmInfo("Информация", "Вы уверены что хотите отвязать контроллер от объекта?",, True)
            f.Button1.Text = "Да"
            f.Button2.Text = "Нет"
            If f.ShowDialog() = DialogResult.OK Then
                thread_getInfo = New Threading.Thread(AddressOf tUnconnectController)
                thread_getInfo.Priority = Threading.ThreadPriority.Normal
                thread_getInfo.Name = "UnconnectController"
                thread_getInfo.Start()
                'Dim str As String
                'If opros.UnConnectObject() Then
                '    str = "Команда выполнена!"
                'Else
                '    str = "Не удалось выполнить!" & vbCrLf & "Повторите операцию!"
                'End If
                'Using f2 As New frmInfo("Информация", str)
                '    f2.ShowDialog()
                'End Using
            End If
        End Using
    End Sub

    Private Sub btnClearLog_Click(sender As Object, e As EventArgs) Handles btnClearLog.Click
        RichTextBox1.Clear()
    End Sub

    Private Sub onLogTextReceive(ByVal _str As String)
        RichTextBox1.Text &= _str
    End Sub

    Private Sub btnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
        Me.Close()
    End Sub

    Private Sub Label16_Click(sender As Object, e As EventArgs) Handles Label16.Click
        opros.SetTime(DateTimeToUnixTimestamp(Now))
    End Sub

    Private Sub Label19_Click(sender As Object, e As EventArgs) Handles Label19.Click
        Dim arr As New ArrayList

        arr.Add(New frmSelecter.mItem With {.ID = 1, .Name = "UG95/UG915"})
        arr.Add(New frmSelecter.mItem With {.ID = 2, .Name = "SLM336"})
        arr.Add(New frmSelecter.mItem With {.ID = 3, .Name = "M66"})
        arr.Add(New frmSelecter.mItem With {.ID = 0, .Name = "Отмена", .ButtonBackColor = Color.Red})

        Using f As New frmSelecter("Тип модуля", arr, 2, 14)
            f.ShowDialog()

            If f.ID = 0 Then
                Exit Sub
            End If
            Dim str As String
            If opros.SetModuleType(f.ID) Then
                str = "Команда выполнена!" & vbCrLf & "Контроллер будет перезагружен!"
            Else
                str = "Не удалось выполнить!" & vbCrLf & "Повторите операцию!"
            End If
            Using f2 As New frmInfo("Информация", str)
                f2.ShowDialog()
            End Using
            Me.Close()
        End Using
    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        Dim str As String
        If opros.GetConfig(config) Then
            str = "Конфигурация считана!"
            UpdateConfigFields()
        Else
            str = "Не удалось считать конфигурацию!" & vbCrLf & "Повторите операцию!"
        End If
        Using f2 As New frmInfo("Информация", str)
            f2.ShowDialog()
        End Using
    End Sub

    Private Sub lblServer1_Click(sender As Object, e As EventArgs) Handles lblServer1.Click
        Using f As New frmText("Наименование", "Адрес сервера 1", lblServer1.Text)
            If f.ShowDialog = DialogResult.OK Then
                config.server1 = f.vText
                UpdateConfigFields()
            End If
        End Using
    End Sub

    Private Sub lblServer2_Click(sender As Object, e As EventArgs) Handles lblServer2.Click
        Using f As New frmText("Наименование", "Адрес сервера 2", lblServer2.Text)
            If f.ShowDialog = DialogResult.OK Then
                config.server2 = f.vText
                UpdateConfigFields()
            End If
        End Using
    End Sub

    Private Sub chServer1_CheckedChanged(sender As Object, e As EventArgs) Handles chServer1.CheckedChanged
        config.useServer1 = chServer1.Checked
    End Sub

    Private Sub Label17_Click(sender As Object, e As EventArgs) Handles Label17.Click
        Dim arr As New ArrayList

        arr.Add(New frmSelecter.mItem With {.ID = 0, .Name = "Авто"})
        arr.Add(New frmSelecter.mItem With {.ID = 1, .Name = "GPRS/EDGE"})
        arr.Add(New frmSelecter.mItem With {.ID = 2, .Name = "3G/4G"})
        arr.Add(New frmSelecter.mItem With {.ID = 3, .Name = "3G"})
        arr.Add(New frmSelecter.mItem With {.ID = 4, .Name = "4G"})
        arr.Add(New frmSelecter.mItem With {.ID = -1, .Name = "Отмена", .ButtonBackColor = Color.Red})

        Using f As New frmSelecter("Тип сети", arr, 2, 14)
            f.ShowDialog()

            If f.ID = -1 Then
                Exit Sub
            End If

            config.netType = CType(f.ID, clOpros.eNetType)
        End Using
    End Sub

    Private Sub chServer2_CheckedChanged(sender As Object, e As EventArgs) Handles chServer2.CheckedChanged
        config.useserver2 = chServer2.Checked
    End Sub

    Private Sub CheckBox1_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBox1.CheckedChanged
        If CheckBox1.Checked Then
            logBits = logBits Or 1
        Else
            logBits = logBits And 254
        End If
        If Not opros.EnableLog(logBits) Then
            If CheckBox1.Checked Then
                CheckBox1.Checked = False
            End If
        End If
    End Sub

    Private Sub CheckBox2_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBox2.CheckedChanged
        If CheckBox2.Checked Then
            logBits = logBits Or 2
        Else
            logBits = logBits And 253
        End If
        If Not opros.EnableLog(logBits) Then
            If CheckBox2.Checked Then
                CheckBox2.Checked = False
            End If
        End If
    End Sub

    Private Sub CheckBox3_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBox3.CheckedChanged
        If CheckBox3.Checked Then
            logBits = logBits Or 4
        Else
            logBits = logBits And 251
        End If
        If Not opros.EnableLog(logBits) Then
            If CheckBox3.Checked Then
                CheckBox3.Checked = False
            End If
        End If
    End Sub

    Private Sub Label20_Click(sender As Object, e As EventArgs) Handles Label20.Click
        If opros.ResetController() Then
            Me.Close()
        End If
    End Sub

    Private Delegate Sub delegate_onRecStr(ByVal str As String)

    Private Sub OnRecStr(ByVal str As String)
        Dim UIUpdater As New delegate_onRecStr(AddressOf onLogTextReceive)
        Me.BeginInvoke(UIUpdater, str)
    End Sub

    Private Sub CheckBox4_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBox4.CheckedChanged
        If CheckBox4.Checked Then
            'RemoveHandler opros.onStatus, AddressOf opros_onStatus
            opros.StopGetStatus(True)
        Else
            'AddHandler opros.onStatus, AddressOf opros_onStatus
            opros.StopGetStatus(False)
        End If
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        Clipboard.Clear()
        Clipboard.SetText(RichTextBox1.Text)
    End Sub

    Private Sub tGetInfo()
        Dim socket As New Net.Sockets.TcpClient

        Try
            socket.Connect("93.189.229.197", 15010)
        Catch ex As Exception
            Using f As New frmInfo("Подключение", ex.Message)
                f.ShowDialog()
            End Using
            Exit Sub
        End Try

        If socket.Connected Then

            Dim imei As Byte() = System.Text.Encoding.UTF8.GetBytes(lblIMEI.Text)
            Dim send As Byte()
            Dim len As Integer = 18
            Dim crc As Integer

            ReDim send(len + 7)
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
            send(8) = 17

            Array.Copy(imei, 0, send, 9, 15)

            crc = opros.GetComPort.Crc16(send, 4, 20)

            send(24) = crc And &HFF
            send(25) = (crc >> 8) And &HFF

            Dim tout As Integer = System.Environment.TickCount

            socket.GetStream.Write(send, 0, 26)

            While (System.Environment.TickCount - tout) < 2000
                If socket.GetStream.DataAvailable Then
                    Dim rec(1023) As Byte

                    socket.GetStream.Read(rec, 0, 1023)

                    Select Case rec(9)
                        Case 0

                        Case 1
                            Using f As New frmInfo("Подключение", "Контроллер " & lblIMEI.Text & vbCrLf &
                                                               "отсутствует в базе !")
                                f.ShowDialog()
                            End Using
                        Case 2
                            Using f As New frmInfo("Подключение", "Контроллер " & lblIMEI.Text & vbCrLf &
                                                               "не привязан ни к одному объекту !")
                                f.ShowDialog()
                            End Using

                        Case 3
                            lblObjNum.Text = System.Text.Encoding.UTF8.GetString(rec, 10, 8)
                            lblObjName.Text = System.Text.Encoding.UTF8.GetString(rec, 18, 128)
                            lblObjController.Text = System.Text.Encoding.UTF8.GetString(rec, 146, 128)
                            lblAdmin.Text = System.Text.Encoding.UTF8.GetString(rec, 274, 16)

                            Using f As New frmInfo("Подключение", "Данные о привязке получены !")
                                f.ShowDialog()
                            End Using

                    End Select

                    socket.Close()
                    Exit Sub
                End If
            End While

            socket.Close()

            Using f As New frmInfo("Подключение", "Нет ответа от сервера на запрос" & vbCrLf &
                                   "команда 17 !")
                f.ShowDialog()
            End Using
        End If
    End Sub

    Private Sub tUnconnectController()
        Dim socket As New Net.Sockets.TcpClient

        Try
            socket.Connect("93.189.229.197", 15010)
        Catch ex As Exception
            Using f As New frmInfo("Подключение", ex.Message)
                f.ShowDialog()
            End Using
            Exit Sub
        End Try

        If socket.Connected Then

            Dim imei As Byte() = System.Text.Encoding.UTF8.GetBytes(lblIMEI.Text)
            Dim send As Byte()
            Dim len As Integer = 18
            Dim crc As Integer

            ReDim send(len + 7)
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
            send(8) = 18

            Array.Copy(imei, 0, send, 9, 15)

            crc = opros.GetComPort.Crc16(send, 4, 20)

            send(24) = crc And &HFF
            send(25) = (crc >> 8) And &HFF

            Dim tout As Integer = System.Environment.TickCount

            socket.GetStream.Write(send, 0, 26)

            While (System.Environment.TickCount - tout) < 2000
                If socket.GetStream.DataAvailable Then
                    Dim rec(1023) As Byte

                    socket.GetStream.Read(rec, 0, 1023)

                    Select Case rec(9)
                        Case 0

                        Case 1
                            Using f As New frmInfo("Подключение", "Контроллер " & lblIMEI.Text & vbCrLf &
                                                               "отсутствует в базе !")
                                f.ShowDialog()
                            End Using
                        Case 2
                            Using f As New frmInfo("Подключение", "Контроллер " & lblIMEI.Text & vbCrLf &
                                                               "отвязан от всех объектов !")
                                f.ShowDialog()
                            End Using

                        Case 3
                            Using f As New frmInfo("Подключение", "Не удалось отвязать контроллер!" & vbCrLf &
                                                   "Повторите попытку.")
                                f.ShowDialog()
                            End Using

                    End Select

                    socket.Close()
                    Exit Sub
                End If
            End While

            socket.Close()

            Using f As New frmInfo("Подключение", "Нет ответа от сервера на запрос" & vbCrLf &
                                   "команда 17 !")
                f.ShowDialog()
            End Using
        End If
    End Sub

    Private Sub Label21_Click(sender As Object, e As EventArgs) Handles Label21.Click
        Dim socket As New Net.Sockets.TcpClient
        Dim sList As String() = lblServer1.Text.Split(":")

        If sList.Count <> 2 Then
            Using f As New frmInfo("Подключение", "Не правильная запись адреса сервера!")
                f.ShowDialog()
            End Using
            Exit Sub
        End If

        Try
            socket.Connect(sList(0), Integer.Parse(sList(1)))
        Catch ex As Exception
            Using f As New frmInfo("Подключение", ex.Message)
                f.ShowDialog()
            End Using
            Exit Sub
        End Try

        If socket.Connected Then
            Using f As New frmInfo("Подключение", "Соединение установлено!")
                f.ShowDialog()
            End Using

            socket.Close()
        End If
    End Sub

    Private Delegate Sub delegate_onErrorPort()
    Private Sub OnErrorPort()
        Dim UIUpdater As New delegate_onErrorPort(AddressOf UIonErrorPort)
        Me.BeginInvoke(UIUpdater)
    End Sub

    Private Sub UIonErrorPort()
        RemoveHandler opros.GetComPort.ErrorPort, AddressOf OnErrorPort

        Using f As New frmInfo("Подключение", "Потеряно соединение с контроллером!")
            f.ShowDialog()
        End Using

        Me.Close()

    End Sub
End Class