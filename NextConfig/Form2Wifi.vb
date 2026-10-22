' Доработка: Кряжев А., 2026. Локальный выбор Wi-Fi-модема хранится в device платы.
' Сеть Wi-Fi выбирается из списка, найденного мостом (окно frmWifiPicker, USB 115), и записывается в мост командой 113.
Imports System.Linq
Imports System.Text
Imports System.Threading.Tasks
Imports System.Windows.Forms

Partial Public Class Form2
    Private wifiModel As ComboBox, wifiPolicy As ComboBox
    Private wifiState As Label, wifiRule As Label
    Private wifiWrite As Button, wifiCheck As Button
    Private wifiCurrent As WifiRow
    Private wifiBusy As Boolean, wifiLoaded As Boolean, wifiSelected As Boolean
    Private wifiLoading As Boolean, wifiPortBusy As Boolean, wifiStateKnown As Boolean, wifiMisses As Integer
    Private wifiBoardModel As Byte, wifiBoardPolicy As Byte
    Private wifiOptions As Control
    Private wifiSetupHint As Label

    ' Вкладка «Wi-Fi-модем»: полоса состояния моста, слева модем и канал связи, справа сеть Wi-Fi.
    ' Кирпичи раскладки (SettingsField, SettingsColumns, SettingsNote) — в Form2Settings.vb.
    Private Function BuildWifi() As Control
        wifiModel = New ComboBox With {.Name = "wifiModel"}
        wifiModel.Items.AddRange({"Не выбран", "USR-DR164"})
        wifiPolicy = New ComboBox With {.Name = "wifiPolicy"}
        wifiPolicy.Items.AddRange({"Только GSM", "GSM, если нет Wi-Fi", "Только Wi-Fi"})
        Theme.Combo(wifiModel, 240) : Theme.Combo(wifiPolicy, 300)
        wifiModel.SelectedIndex = 0 : wifiPolicy.SelectedIndex = 0
        wifiWrite = New Button With {.Name = "wifiWrite", .Text = "Выбрать сеть…"}
        wifiCheck = New Button With {.Name = "wifiCheck", .Text = "Обновить статус"}
        For Each button In {wifiWrite, wifiCheck}
            Theme.Secondary(button)
            button.Margin = New Padding(0, 0, Theme.GapRelated, 0)
        Next
        AddHandler wifiModel.SelectedIndexChanged, Sub() WifiEdited()
        AddHandler wifiPolicy.SelectedIndexChanged, Sub() WifiEdited()
        AddHandler wifiWrite.Click, Sub() ChooseWifiNetwork()
        AddHandler wifiCheck.Click, Sub() WifiRefresh()
        wifiRule = SettingsNote("Wi-Fi-модем занимает порт RS-485: камера недоступна, VoLTE выключен. В режиме «Только Wi-Fi» сотовая связь остаётся для звонков.")
        Dim modem = Theme.Card("Wi-Fi-модем",
            SettingsColumns(SettingsField("Модем", wifiModel), SettingsField("Канал связи с сервером", wifiPolicy), 600),
            SettingsNote("Выбор применяется кнопкой «Сохранить изменения» внизу окна. Сервер этот выбор не меняет."),
            wifiRule)
        ' Текущая сеть моста строкой «как в Windows»: веер уровня, имя, «Подключено»; щелчок открывает выбор сети.
        wifiCurrent = New WifiRow(New WifiNetwork With {.Name = "Сеть не выбрана", .Level = -1}, False)
        wifiCurrent.Name = "wifiCurrent"
        wifiCurrent.Anchor = AnchorStyles.Left Or AnchorStyles.Right
        wifiCurrent.Margin = New Padding(0, 0, 0, Theme.GapRelated)
        AddHandler wifiCurrent.Click, Sub() ChooseWifiNetwork()
        Dim network As New TableLayoutPanel With {.Name = "wifiNetwork", .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .ColumnCount = 1, .Anchor = AnchorStyles.Left Or AnchorStyles.Right, .Margin = New Padding(0)}
        network.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        network.Controls.Add(wifiCurrent)
        wifiOptions = network
        Dim actions As New FlowLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .WrapContents = True, .Anchor = AnchorStyles.Left Or AnchorStyles.Right,
            .Margin = New Padding(0, 0, 0, Theme.GapRelated)}
        actions.Controls.AddRange({wifiWrite, wifiCheck})
        wifiState = Theme.Band("Состояние моста не запрашивалось")
        wifiState.Name = "wifiState"
        wifiSetupHint = SettingsNote("")
        Dim networkCard = Theme.Card("Сеть Wi-Fi", network, actions, wifiSetupHint)
        modem.Dock = DockStyle.Fill : networkCard.Dock = DockStyle.Fill
        WifiControls()
        Return Page(wifiState, SettingsColumns(modem, networkCard))
    End Function

    Private Sub WifiEdited()
        WifiControls()
        If Not wifiLoading Then UpdateDirty()
    End Sub

    Private Sub WifiControls()
        If wifiLoading OrElse wifiCheck Is Nothing Then Return
        If wifiModel.SelectedIndex = 0 Then wifiPolicy.SelectedIndex = 0
        Dim selected = wifiModel.SelectedIndex = 1
        wifiModel.Enabled = wifiLoaded AndAlso Not wifiBusy
        wifiPolicy.Enabled = wifiLoaded AndAlso selected AndAlso Not wifiBusy
        wifiWrite.Enabled = wifiLoaded AndAlso wifiSelected AndAlso selected AndAlso Not wifiBusy
        wifiCheck.Enabled = Not wifiBusy
        wifiOptions.Visible = selected
        wifiRule.Visible = selected
        wifiCurrent.Enabled = wifiWrite.Enabled
        Theme.SetText(wifiSetupHint, If(Not selected, "Выберите Wi-Fi-модем и сохраните изменения, чтобы настроить сеть.",
            If(Not wifiSelected, "Сначала сохраните выбор модема кнопкой «Сохранить изменения» внизу окна.",
                "«Выбрать сеть…» показывает сети, которые видит мост; после выбора и ввода пароля мост перезапускается.")))
        If cmbVoLTE IsNot Nothing Then cmbVoLTE.Enabled = Not wifiSelected
    End Sub

    ' Текущая сеть моста из хвоста ответа 113: байт 6 — точка доступа (0 нет данных, 1 нет, 2 есть), 7 — уровень, 8… — имя.
    Private Sub WifiShowCurrent(reply As Byte())
        Dim ap = If(reply.Length >= 8, reply(6), CByte(0))
        Dim name = If(ap = 2 AndAlso reply.Length > 8, Encoding.UTF8.GetString(reply, 8, reply.Length - 8), "")
        Dim level = If(ap = 2 AndAlso reply(7) <= 100, CInt(reply(7)), -1)
        wifiCurrent.Network = If(name <> "", New WifiNetwork With {.Name = name, .Level = level, .Connected = True},
            New WifiNetwork With {.Name = If(ap = 1, "Мост не подключён к сети Wi-Fi", "Сеть не определена"), .Level = -1})
    End Sub

    Public ReadOnly Property WifiCurrentName As String
        Get
            Return If(wifiCurrent.Network.Connected, wifiCurrent.Network.Name, "")
        End Get
    End Property

    ' Автоопрос обновляет факт работы, но не перетирает несохранённый выбор пользователя.
    Private Sub WifiApplyReply(reply As Byte(), Optional reload As Boolean = False)
        If reply Is Nothing OrElse reply.Length < 2 Then
            Theme.DotText(wifiState, Theme.eDot.Bad, "Плата не ответила на запрос состояния Wi-Fi")
            ' Прошивка без команды 113 (до 19.10.2026) не ответит никогда: после двух пропусков порт RS-485 считаем
            ' свободным, как до выбора Wi-Fi. Один пропуск — ещё не старая прошивка, раскладка не дёргается.
            wifiMisses += 1
            If Not wifiStateKnown AndAlso wifiMisses >= 2 Then
                wifiStateKnown = True
                If warning485 IsNot Nothing Then Update485State()
            End If
            Return
        End If
        linkWifi = reply
        wifiStateKnown = True : wifiMisses = 0
        If StatusIsFresh() Then UpdateDeviceSignal()
        If reply.Length < 6 OrElse reply(2) <> 1 OrElse reply(3) > 1 OrElse reply(4) > 2 Then
            wifiLoaded = False : WifiControls()
            Theme.DotText(wifiState, Theme.eDot.Idle, "Прошивка не поддерживает локальный выбор Wi-Fi-модема")
            If warning485 IsNot Nothing Then Update485State()
            Return
        End If
        wifiBoardModel = reply(3) : wifiBoardPolicy = reply(4)
        wifiSelected = reply(3) = 1
        wifiPortBusy = wifiSelected OrElse reply(1) = 5
        If Not wifiLoaded OrElse reload Then
            wifiLoading = True
            wifiModel.SelectedIndex = reply(3) : wifiPolicy.SelectedIndex = reply(4)
            wifiLoading = False
        End If
        wifiLoaded = True
        WifiShowCurrent(reply)
        Dim text = "Wi-Fi-модем не выбран", dot = Theme.eDot.Idle
        If reply(1) = 4 Then
            text = "Мост не подтвердил выключение" : dot = Theme.eDot.Bad
        ElseIf reply(1) = 5 Then
            text = "Выключение моста…"
        ElseIf wifiSelected Then
            Select Case reply(1)
                Case 2 : text = "Wi-Fi-мост: соединение с сервером открыто" : dot = Theme.eDot.Good
                Case 3 : text = If(reply(4) = 0, "Выбран только GSM: Wi-Fi для сервера выключен", "Wi-Fi: соединение с сервером закрыто, ожидание повтора")
                Case 1 : text = "Wi-Fi-модем отвечает; соединение с сервером не открыто" : dot = Theme.eDot.Good
                Case Else : text = "Нет подтверждённой связи с Wi-Fi-модемом" : dot = Theme.eDot.Bad
            End Select
        End If
        Theme.DotText(wifiState, dot, text)
        WifiControls()
        If warning485 IsNot Nothing Then Update485State()
    End Sub

    ' Выбор модема и логика входят в общую кнопку «Сохранить изменения»; на плате — отдельная команда 114.
    Private Function WifiDiffers() As Boolean
        Return wifiLoaded AndAlso (wifiModel.SelectedIndex <> wifiBoardModel OrElse wifiPolicy.SelectedIndex <> wifiBoardPolicy)
    End Function

    Private Sub WifiChanges(list As List(Of String))
        If Not WifiDiffers() Then Return
        AddChange(list, "Wi-Fi-модем", CStr(wifiModel.Items(wifiBoardModel)), CStr(wifiModel.SelectedItem))
        AddChange(list, "Канал связи с сервером", CStr(wifiPolicy.Items(wifiBoardPolicy)), CStr(wifiPolicy.SelectedItem))
    End Sub

    Private Function SaveWifiChoice() As Boolean
        Dim model = CByte(wifiModel.SelectedIndex), policy = CByte(wifiPolicy.SelectedIndex)
        If Not opros.SetWifiModem(model, policy) Then Return False
        wifiBoardModel = model : wifiBoardPolicy = policy
        WifiApplyReply(opros.WifiBridge(Nothing), True)
        Return True
    End Function

    Private Sub RevertWifi()
        If Not wifiLoaded Then Return
        wifiLoading = True
        wifiModel.SelectedIndex = wifiBoardModel : wifiPolicy.SelectedIndex = wifiBoardPolicy
        wifiLoading = False
        WifiControls()
    End Sub

    ' Окно выбора сети: список от моста, пароль, «Сохранить сеть» → запись в мост командой 113.
    Private Sub ChooseWifiNetwork()
        If wifiBusy OrElse busyWithBoard OrElse opros Is Nothing OrElse Not wifiWrite.Enabled Then Return
        Using picker As New frmWifiPicker(opros, WifiCurrentName, wifiCurrent.Network.Level)
            If picker.ShowDialog(Me) <> DialogResult.OK Then Return
            WifiSetup(picker.Ssid, picker.Key)
        End Using
    End Sub

    Private Sub WifiRefresh()
        WifiExchange(Nothing)
    End Sub

    Private Sub WifiSetup(ssid As String, key As String)
        Dim name = Encoding.UTF8.GetBytes(ssid), pass = Encoding.UTF8.GetBytes(key)
        WifiExchange(name.Concat({CByte(0)}).Concat(pass).Concat({CByte(0)}).ToArray())
    End Sub

    ' Nothing — чтение состояния, иначе тело «SSID\0пароль\0» для записи в мост.
    Private Async Sub WifiExchange(payload As Byte())
        If wifiBusy OrElse busyWithBoard OrElse opros Is Nothing Then Return
        wifiBusy = True : WifiControls()
        Try
            Dim reply = Await Task.Run(Function() opros.WifiBridge(payload))
            If IsDisposed Then Return
            If payload IsNot Nothing Then
                Dim ok = reply IsNot Nothing AndAlso reply.Length >= 2 AndAlso reply(0) = 0
                Theme.DotText(wifiState, If(ok, Theme.eDot.Idle, Theme.eDot.Bad),
                    If(ok, "Сеть принята: мост перезапускается, проверка связи через 10–20 с", "Сеть не принята: мост занят, повторите через несколько секунд"))
                Notify("Сеть Wi-Fi", If(ok, "сеть записана в мост, мост перезапускается", "мост не принял сеть, повторите"), Not ok)
            Else
                WifiApplyReply(reply, True)
            End If
        Catch ex As Exception
            AgentLog.Failure("wifi", ex)
            If Not IsDisposed Then Theme.DotText(wifiState, Theme.eDot.Bad, "Ошибка обмена: " & ex.Message)
        Finally
            wifiBusy = False
            If Not IsDisposed Then WifiControls()
        End Try
    End Sub

    Private Function ResetWifiDefaults() As Boolean
        Dim reply = opros.WifiBridge(Nothing)
        If reply Is Nothing OrElse reply.Length < 2 Then Return False
        If reply.Length = 2 Then Return True ' старые прошивки не имеют локальной записи Wi-Fi
        If reply.Length < 6 OrElse reply(2) <> 1 OrElse Not opros.SetWifiModem(0, 0) Then Return False
        WifiApplyReply(opros.WifiBridge(Nothing), True)
        Return True
    End Function
End Class
