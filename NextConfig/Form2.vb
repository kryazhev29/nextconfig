Imports System.Globalization
Imports System.IO

Public Class Form2

    Private WithEvents opros As clOpros
    Private WithEvents tmr1 As New System.Windows.Forms.Timer
    Private status As clOpros.s_status
    Private config As clOpros.s_config
    Private pinCode As Integer
    Private logBits As Byte = 0
    Private thread_getInfo As Threading.Thread

    ' пока настройки раскладываются по полям, их события не считаем правкой
    Private updatingFields As Boolean = False

    ' незаконченная строка журнала ждёт продолжения
    Private logTail As String = ""
    Private Const LogLimit As Integer = 500000

    ' когда последний раз приходил статус: по нему видно, свежие данные на экране или нет
    Private lastStatus As DateTime = DateTime.MinValue
    Private Const StaleSeconds As Integer = 6


    Public Sub New(ByVal _opros As clOpros, ByVal _info As String, ByVal _imei As String, ByVal _pin As Integer)

        ' Этот вызов является обязательным для конструктора.
        InitializeComponent()

        ' Добавить код инициализации после вызова InitializeComponent().
        opros = _opros
        lblPortInfo.Text = _info
        lblIMEI.Text = _imei
        lblPIN.Text = System.Text.Encoding.UTF8.GetString(BitConverter.GetBytes(_pin))
        pinCode = _pin

        If Demo.IsOn Then Me.Text = "Домовой NEXT — демонстрационный режим"
    End Sub

    Private Sub Form2_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        FitToScreen()
        Theme.EnableDoubleBuffer(Me)

        AddHandler opros.onStatus, AddressOf opros_onStatus
        AddHandler tmr1.Tick, AddressOf TmrTick
        tmr1.Interval = 1000
        tmr1.Enabled = True

        AddHandler opros.GetComPort.ReceiveComand, AddressOf OnRecStr
        opros.StopGetStatus(False)

        ReadConfig(True)
        AddHandler opros.GetComPort.ErrorPort, AddressOf OnErrorPort
    End Sub

    Private Sub Form2_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        RemoveHandler opros.onStatus, AddressOf opros_onStatus

        CheckBox1.Checked = False
        CheckBox2.Checked = False
        CheckBox3.Checked = False
    End Sub

    ' На маленьком экране или при крупном масштабе Windows окно нужного размера
    ' может не поместиться: ужимаем его до рабочей области, содержимое прокручивается.
    Private Sub FitToScreen()
        Dim area As Rectangle = Screen.FromControl(Me).WorkingArea
        Dim w As Integer = Math.Min(Me.Width, area.Width - Theme.GapSection)
        Dim h As Integer = Math.Min(Me.Height, area.Height - Theme.GapSection)

        Me.MinimumSize = New Size(Math.Min(Me.MinimumSize.Width, w), Math.Min(Me.MinimumSize.Height, h))
        Me.Size = New Size(w, h)
    End Sub

    ' Раз в секунду смотрим, давно ли приходил статус. Если связь пропала,
    ' в шапке видно, что на экране последнее полученное, а не текущее состояние.
    Private Sub TmrTick()
        If lastStatus = DateTime.MinValue Then
            SetHeadState(Theme.eDot.Waiting, "Ожидание данных")
        ElseIf DateTime.Now.Subtract(lastStatus).TotalSeconds > StaleSeconds Then
            SetHeadState(Theme.eDot.Bad, "Данные не обновляются")
        End If
    End Sub

    Private Sub SetHeadState(ByVal state As Theme.eDot, ByVal text As String)
        Theme.Dot(dotConn, state)
        Theme.DotText(lblConnState, state, text)
    End Sub

    ' Состояние строкой и цветом сразу: точка плюс подпись.
    Private Sub SetDot(ByVal dot As Label, ByVal text As Label, ByVal good As Boolean,
                       ByVal onText As String, ByVal offText As String,
                       Optional ByVal offIsError As Boolean = False)
        Dim state As Theme.eDot

        If good Then
            state = Theme.eDot.Good
        ElseIf offIsError Then
            state = Theme.eDot.Bad
        Else
            state = Theme.eDot.Idle
        End If

        Theme.Dot(dot, state)
        Theme.DotText(text, state, If(good, onText, offText))
    End Sub

    ' Контроллер держит время сервера, а не местное: без подсказки это выглядит
    ' как сбитые часы. Пишем расхождение с компьютером, когда оно заметное.
    Private Function DeviceTimeText(ByVal unix As UInt32) As String
        Dim t As DateTime = UnixTimestampToDateTime(unix)
        Dim text As String = t.ToString("dd.MM.yyyy HH:mm:ss")
        Dim diff As TimeSpan = DateTime.Now.Subtract(t)

        If Math.Abs(diff.TotalMinutes) < 2 Then Return text

        Return text & " · расходится с компьютером на " & FormatDiff(diff)
    End Function

    Private Function FormatDiff(ByVal diff As TimeSpan) As String
        Dim d As TimeSpan = diff.Duration()

        If d.TotalHours >= 1 Then
            Return CInt(Math.Floor(d.TotalHours)) & " ч " & d.Minutes & " мин"
        End If

        Return CInt(Math.Floor(d.TotalMinutes)) & " мин"
    End Function

    Private Function ModuleName(ByVal m As clOpros.eModuleType) As String
        Select Case m
            Case clOpros.eModuleType.M_UG95 : Return "UG95/UG915"
            Case clOpros.eModuleType.M_SLM336 : Return "SLM336"
            Case clOpros.eModuleType.M_M66 : Return "M66"
            Case clOpros.eModuleType.M_OTHER : Return "другой"
            Case Else : Return "не определён"
        End Select
    End Function

    ' Строка итога внизу окна: сохранены ли настройки.
    Private Sub SetSaveState(ByVal saved As Boolean)
        If saved Then
            lblSaveState.Text = "Настройки сохранены"
            lblSaveState.ForeColor = Theme.OkText
        Else
            lblSaveState.Text = "Есть несохранённые изменения"
            lblSaveState.ForeColor = Theme.Warning
        End If
    End Sub

    Private Delegate Sub delegate_onStatus(ByVal status As clOpros.s_status)

    Private Sub opros_onStatus(ByVal status As clOpros.s_status)
        Dim UIUpdater As New delegate_onStatus(AddressOf refOnStatus)
        Me.BeginInvoke(UIUpdater, status)
    End Sub

    Private Sub refOnStatus(ByVal _status As clOpros.s_status)

        status = _status
        lastStatus = DateTime.Now

        Theme.SetText(lblTime, DeviceTimeText(status.time))
        Theme.SetText(lblFW, status.fw_version & ", сборка " & status.fw_build)
        Theme.SetText(lblBoot, "0x" & status.boot_version.ToString("X2"))
        Theme.SetText(lblHW, status.hw_version.ToString())
        Theme.SetText(lblModuleType, ModuleName(status.moduleType))

        ' то же самое коротко в шапке: она видна на любой вкладке
        Theme.SetText(lblHeadModel, "Модуль " & ModuleName(status.moduleType))
        Theme.SetText(lblHeadHW, "Плата " & status.hw_version)
        Theme.SetText(lblHeadFW, "Прошивка " & status.fw_version)
        Theme.SetText(lblHeadBoot, "Загрузчик 0x" & status.boot_version.ToString("X2"))

        SetDot(dotIN1, lbIN1, status.in1, "Включён", "Выключен")
        SetDot(dotIN2, lblIN2, status.in2, "Включён", "Выключен")
        SetDot(dotOUT1, lblOUT1, status.out1, "Включён", "Выключен")
        SetDot(dotOUT2, lbOUT2, status.out2, "Включён", "Выключен")

        SetDot(dotGSM, lblGSM, status.gsm = 1, "Подключено", "Нет сети", True)
        SetDot(dotInt, lblInt, status.gprs = 1, "Подключено", "Нет интернета", True)
        SetDot(dotServer, lblServerConnect, status.connectToServer <> 0, "Подключено", "Нет связи с сервером", True)

        Theme.SetText(lblSig, status.signal & " из 31")
        pbSig.Value = Math.Max(0, Math.Min(CInt(status.signal), pbSig.Maximum))
        Theme.SetText(lblNet, opros.NetTypeToString(status.netType))

        Theme.SetText(lblUsedMem, status.usedMem & " из 50 000")
        meterMem.Value = status.usedMem

        SetHeadState(Theme.eDot.Good, "На связи")
        Theme.SetText(lblLastUpdate, "Последнее обновление: " & lastStatus.ToString("HH:mm:ss"))

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
        OpenFileDialog1.Filter = "Файлы загрузчика (*.bin)|*.bin|Все файлы (*.*)|*.*"
        OpenFileDialog1.FileName = "Boot_xx.bin"
        If OpenFileDialog1.ShowDialog = DialogResult.OK Then
            ' опрос статуса на время замены не нужен: порт занимает команда 110
            tmr1.Enabled = False
            opros.StopGetStatus(True)

            Using f As New frmFW(OpenFileDialog1.FileName, opros, status.hw_version)
                If f.ShowDialog() = DialogResult.OK Then
                    ' контроллер перезагружается с новым загрузчиком, связь потеряна
                    Me.Close()
                    Exit Sub
                End If
            End Using

            opros.StopGetStatus(False)
            tmr1.Enabled = True
        End If
    End Sub

    Private Sub btnTestOUT_Click(sender As Object, e As EventArgs) Handles btnTestOUT2.Click, btnTestOUT1.Click
        Dim _o1 As Boolean = Not status.out1
        Dim _o2 As Boolean = Not status.out2

        Select Case CType(sender, Control).Tag
            Case 0
                opros.SetOut(_o1, status.out2)

            Case 1
                opros.SetOut(status.out1, _o2)

        End Select
    End Sub

    Private Sub btnTranspMode_Click(sender As Object, e As EventArgs) Handles btnTranspMode.Click
        Using f As New frmInfo("Информация", "Перевести контроллер в прозрачный режим с GSM модулем?" & vbCrLf & "Конфигуратор будет закрыт!",, True)
            f.Button1.Text = "Перевести"
            f.Button2.Text = "Отмена"
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
            f.Button1.Text = "Сбросить"
            f.Button2.Text = "Отмена"
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

    Private Sub btnSave_Click(sender As Object, e As EventArgs) Handles btnSave.Click

        If Not CollectConfig() Then
            lblSaveState.Text = "Проверьте отмеченные значения"
            lblSaveState.ForeColor = Theme.Danger
            Exit Sub
        End If

        Using f As New frmInfo("Информация", "Сохранить настройки?",, True)
            f.Button1.Text = "Сохранить"
            f.Button2.Text = "Отмена"
            If f.ShowDialog() = DialogResult.OK Then
                Dim str As String
                If opros.SetConfig(config) Then
                    str = "Настройки сохранены!"
                    SetSaveState(True)
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
            f.Button1.Text = "Записать"
            f.Button2.Text = "Отмена"
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
        SetSaveState(False)

        ' пока раскладываем значения по полям, их события не считаем правкой
        updatingFields = True

        With config
            lblPIN.Text = System.Text.Encoding.UTF8.GetString(BitConverter.GetBytes(.pin))
            txtServer1.Text = .server1
            txtServer2.Text = .server2
            chServer1.Checked = .useServer1
            chServer2.Checked = .useserver2
            cmbOperator.SelectedIndex = If(.opType = "", 0, 1)
            txtOperator.Text = .opType
            txtOperator.Enabled = (.opType <> "")
            cmbVoLTE.SelectedIndex = If(.VoLTE = 0, 0, 1)
            cmbNetType.SelectedIndex = Math.Max(0, Math.Min(CInt(.netType), cmbNetType.Items.Count - 1))
        End With

        updatingFields = False

    End Sub

    ' Собрать настройки из полей. Ошибку пишем возле того значения, из-за
    ' которого она возникла, и ничего на плату не отправляем.
    Private Function CollectConfig() As Boolean
        lblServer1Err.Text = ""
        lblServer2Err.Text = ""

        lblServer1Err.Text = ServerError(txtServer1.Text, True)
        lblServer2Err.Text = ServerError(txtServer2.Text, False)

        If lblServer1Err.Text <> "" OrElse lblServer2Err.Text <> "" Then Return False

        With config
            .server1 = txtServer1.Text.Trim()
            .server2 = txtServer2.Text.Trim()
            .useServer1 = If(chServer1.Checked, 1, 0)
            .useserver2 = If(chServer2.Checked, 1, 0)
            .opType = If(cmbOperator.SelectedIndex = 0, "", txtOperator.Text.Trim())
            .VoLTE = CByte(Math.Max(0, cmbVoLTE.SelectedIndex))
            .netType = CType(Math.Max(0, cmbNetType.SelectedIndex), clOpros.eNetType)
        End With

        Return True
    End Function

    ' Адрес сервера пишется как узел:порт, например 87.244.4.92:15010.
    Private Function ServerError(ByVal text As String, ByVal required As Boolean) As String
        Dim v As String = text.Trim()

        If v = "" Then
            Return If(required, "Укажите адрес в виде 87.244.4.92:15010", "")
        End If

        Dim parts As String() = v.Split(":"c)

        If parts.Length <> 2 Then Return "Адрес пишется как узел:порт, например 87.244.4.92:15010"
        If parts(0).Trim() = "" Then Return "Не указан адрес узла"

        Dim port As Integer

        If Not Integer.TryParse(parts(1).Trim(), port) Then Return "Порт - это число"
        If port < 1 OrElse port > 65535 Then Return "Порт от 1 до 65535"

        Return ""
    End Function

    Private Sub Settings_Changed(sender As Object, e As EventArgs) _
        Handles txtServer1.TextChanged, txtServer2.TextChanged, txtOperator.TextChanged,
                cmbVoLTE.SelectedIndexChanged, cmbNetType.SelectedIndexChanged
        If updatingFields Then Exit Sub
        SetSaveState(False)
    End Sub

    Private Sub cmbOperator_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cmbOperator.SelectedIndexChanged
        txtOperator.Enabled = (cmbOperator.SelectedIndex = 1)
        If Not txtOperator.Enabled Then txtOperator.Text = ""

        If updatingFields Then Exit Sub
        SetSaveState(False)
    End Sub

    ' Отменить изменения - это вернуть то, что стоит в контроллере.
    Private Sub btnRevert_Click(sender As Object, e As EventArgs) Handles btnRevert.Click
        ReadConfig(False)
    End Sub

    ' Выгрузки и загрузки базы идентификаторов в программе нет: в протоколе с
    ' контроллером она не заведена. Кнопка должна об этом сказать, а не молчать.
    Private Sub btnExport_Click(sender As Object, e As EventArgs) Handles btnExport.Click
        Using f As New frmInfo("Информация", "Выгрузка базы идентификаторов пока не сделана.")
            f.ShowDialog(Me)
        End Using
    End Sub

    Private Sub btnImport_Click(sender As Object, e As EventArgs) Handles btnImport.Click
        Using f As New frmInfo("Информация", "Загрузка базы идентификаторов пока не сделана.")
            f.ShowDialog(Me)
        End Using
    End Sub

    Private Sub btnClear_Click(sender As Object, e As EventArgs) Handles btnClear.Click
        Using f As New frmInfo("Информация", "Вы уверены что хотите удалить из памяти все идентификаторы?",, True)
            f.Button1.Text = "Очистить"
            f.Button2.Text = "Отмена"
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
            f.Button1.Text = "Отвязать"
            f.Button2.Text = "Отмена"
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
        logTail = ""
    End Sub

    ' Журнал приходит кусками, строка может прийти не целиком: красим только
    ' дописанные до конца строки, хвост ждёт продолжения.
    Private Sub onLogTextReceive(ByVal _str As String)
        logTail &= _str

        Dim lines As String() = logTail.Split(New String() {vbCrLf, vbLf}, StringSplitOptions.None)

        For i As Integer = 0 To lines.Length - 2
            AppendLogLine(lines(i))
        Next

        logTail = lines(lines.Length - 1)

        If chAutoScroll.Checked Then
            RichTextBox1.SelectionStart = RichTextBox1.TextLength
            RichTextBox1.ScrollToCaret()
        End If
    End Sub

    Private Sub AppendLogLine(ByVal line As String)
        ' журнал за долгий прогон вырастает на сотни тысяч знаков и начинает тормозить
        If RichTextBox1.TextLength > LogLimit Then
            RichTextBox1.Clear()
            RichTextBox1.SelectionColor = Theme.TextMuted
            RichTextBox1.AppendText("--- журнал очищен: он вырос больше " & LogLimit & " знаков ---" & vbCrLf)
        End If

        RichTextBox1.SelectionStart = RichTextBox1.TextLength
        RichTextBox1.SelectionLength = 0
        RichTextBox1.SelectionColor = LogColor(line)
        RichTextBox1.AppendText(line & vbCrLf)
    End Sub

    ' Цвет по заметным признакам в строке. Красным - только ошибки.
    Private Function LogColor(ByVal line As String) As Color
        Dim l As String = line.ToUpperInvariant()

        If l.Contains("ERROR") OrElse l.Contains("FAIL") OrElse l.Contains("ОШИБ") Then Return Theme.Danger
        If l.Contains("WARN") OrElse l.Contains("ПРЕДУПРЕ") Then Return Theme.Warning

        ' переписка с модемом - подробность, а не событие
        Dim t As String = line.TrimStart()
        If t.StartsWith("AT") OrElse t.StartsWith("+") Then Return Theme.TextMuted

        Return Theme.TextMain
    End Function

    Private Sub Label16_Click(sender As Object, e As EventArgs) Handles Label16.Click
        opros.SetTime(DateTimeToUnixTimestamp(Now))
    End Sub

    Private Sub Label19_Click(sender As Object, e As EventArgs) Handles Label19.Click
        Dim arr As New ArrayList

        arr.Add(New frmSelecter.mItem With {.ID = 1, .Name = "UG95/UG915"})
        arr.Add(New frmSelecter.mItem With {.ID = 2, .Name = "SLM336"})
        arr.Add(New frmSelecter.mItem With {.ID = 3, .Name = "M66"})
        arr.Add(New frmSelecter.mItem With {.ID = 0, .Name = "Отмена"})

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
        ReadConfig(False)
    End Sub

    ' Чтение настроек с платы. При открытии окна - молча, по кнопке - с ответом.
    Private Sub ReadConfig(ByVal silent As Boolean)
        If opros.GetConfig(config) Then
            UpdateConfigFields()
            lblSaveState.Text = ""
            lblLastUpdate.Text = "Настройки считаны: " & DateTime.Now.ToString("HH:mm:ss")
            Exit Sub
        End If

        lblSaveState.Text = "Не удалось считать настройки"
        lblSaveState.ForeColor = Theme.Danger

        If silent Then Exit Sub

        Using f2 As New frmInfo("Информация", "Не удалось считать конфигурацию!" & vbCrLf & "Повторите операцию!")
            f2.ShowDialog()
        End Using
    End Sub

    Private Sub chServer1_CheckedChanged(sender As Object, e As EventArgs) Handles chServer1.CheckedChanged
        config.useServer1 = chServer1.Checked
        If updatingFields Then Exit Sub
        SetSaveState(False)
    End Sub

    Private Sub chServer2_CheckedChanged(sender As Object, e As EventArgs) Handles chServer2.CheckedChanged
        config.useserver2 = chServer2.Checked
        If updatingFields Then Exit Sub
        SetSaveState(False)
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
        Dim sList As String() = txtServer1.Text.Trim().Split(":"c)

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