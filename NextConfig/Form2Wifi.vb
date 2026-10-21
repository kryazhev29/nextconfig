' Доработка: Кряжев А., 2026. Локальный выбор Wi-Fi-модема хранится в device платы.
Imports System.Linq
Imports System.Text
Imports System.Threading.Tasks
Imports System.Windows.Forms

Partial Public Class Form2
    Private wifiSsid As TextBox, wifiKey As TextBox
    Private wifiModel As ComboBox, wifiPolicy As ComboBox
    Private wifiState As Label, wifiRule As Label
    Private wifiWrite As Button, wifiCheck As Button, wifiSave As Button
    Private wifiBusy As Boolean, wifiLoaded As Boolean, wifiSelected As Boolean
    Private wifiLoading As Boolean, wifiPortBusy As Boolean
    Private wifiOptions As Control
    Private ReadOnly wifiChannel As New Label With {.Name = "wifiChannel", .AutoSize = True, .Text = "Связь: нет данных"}

    Private Function BuildWifi() As Control
        wifiModel = New ComboBox With {.Name = "wifiModel"}
        wifiModel.Items.AddRange({"Не выбран", "USR-DR164"})
        wifiPolicy = New ComboBox With {.Name = "wifiPolicy"}
        wifiPolicy.Items.AddRange({"Только GSM", "GSM, если нет Wi-Fi", "Только Wi-Fi"})
        Theme.Combo(wifiModel, 240) : Theme.Combo(wifiPolicy, 300)
        wifiModel.SelectedIndex = 0 : wifiPolicy.SelectedIndex = 0
        wifiSsid = New TextBox With {.Name = "wifiSsid", .MaxLength = 32}
        wifiKey = New TextBox With {.Name = "wifiKey", .MaxLength = 63}
        Dim ssidBox = Theme.FieldBox(wifiSsid, 240), keyBox = Theme.FieldBox(wifiKey, 240)
        Theme.PasswordField(keyBox, wifiKey)
        wifiSave = New Button With {.Name = "wifiSave", .Text = "Сохранить выбор"}
        wifiWrite = New Button With {.Name = "wifiWrite", .Text = "Записать сеть"}
        wifiCheck = New Button With {.Name = "wifiCheck", .Text = "Прочитать"}
        For Each button In {wifiSave, wifiWrite, wifiCheck}
            Theme.Secondary(button)
            button.Margin = New Padding(0, 0, Theme.GapRelated, 0)
        Next
        AddHandler wifiModel.SelectedIndexChanged, Sub() WifiControls()
        AddHandler wifiPolicy.SelectedIndexChanged, Sub() WifiControls()
        AddHandler wifiSave.Click, Sub() WifiExchange(2)
        AddHandler wifiWrite.Click, Sub() WifiExchange(1)
        AddHandler wifiCheck.Click, Sub() WifiExchange(0)
        Dim choice As New TableLayoutPanel
        Theme.BusOptions(choice)
        choice.Controls.Add(Theme.BusField("Wi-Fi-модем", wifiModel), 0, 0)
        choice.Controls.Add(Theme.BusField("Логика работы", wifiPolicy), 1, 0)
        Dim network As New TableLayoutPanel
        Theme.BusOptions(network)
        network.Controls.Add(Theme.BusField("Сеть Wi-Fi (SSID)", ssidBox), 0, 0)
        network.Controls.Add(Theme.BusField("Пароль сети", keyBox), 1, 0)
        wifiOptions = network
        Dim actions As New FlowLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .WrapContents = False, .Margin = New Padding(0, 0, 0, Theme.GapRow)}
        actions.Controls.AddRange({wifiSave, wifiWrite, wifiCheck})
        wifiState = Theme.BusNotice("Состояние моста не запрашивалось", False)
        wifiRule = Theme.BusNotice("При выбранном Wi-Fi-модеме камера отключена, VoLTE выключен. В режиме «Только Wi-Fi» GSM остаётся для звонков.", False)
        WifiControls()
        Dim section = Theme.SettingsSection("Wi-Fi-модем", "Настройка только на месте, через NextConfig", choice, network, actions, wifiState, wifiRule,
            Hint("Выбор модема и логика сохраняются в плате отдельно от настроек сервера. Сеть и пароль записываются в мост; после записи мост перезапускается."))
        section.Margin = New Padding(0, 0, 0, Theme.GapSection)
        Return section
    End Function

    Private Sub WifiControls()
        If wifiLoading OrElse wifiSave Is Nothing Then Return
        If wifiModel.SelectedIndex = 0 Then wifiPolicy.SelectedIndex = 0
        Dim selected = wifiModel.SelectedIndex = 1
        wifiModel.Enabled = wifiLoaded AndAlso Not wifiBusy
        wifiPolicy.Enabled = wifiLoaded AndAlso selected AndAlso Not wifiBusy
        wifiSave.Enabled = wifiLoaded AndAlso Not wifiBusy
        wifiWrite.Enabled = wifiLoaded AndAlso wifiSelected AndAlso selected AndAlso Not wifiBusy
        wifiCheck.Enabled = Not wifiBusy
        wifiOptions.Visible = selected
        wifiRule.Visible = selected
        wifiSsid.Enabled = Not wifiBusy : wifiKey.Enabled = Not wifiBusy
        If cmbVoLTE IsNot Nothing Then cmbVoLTE.Enabled = Not wifiSelected
    End Sub

    ' Автоопрос обновляет факт работы, но не перетирает несохранённый выбор пользователя.
    Private Sub WifiApplyReply(reply As Byte(), Optional reload As Boolean = False)
        If reply Is Nothing OrElse reply.Length < 2 Then
            Theme.DotText(wifiChannel, Theme.eDot.Idle, "Связь: нет свежих данных")
            Theme.DotText(wifiState, Theme.eDot.Bad, "Плата не ответила на запрос состояния Wi-Fi")
            Return
        End If
        If reply.Length < 6 OrElse reply(2) <> 1 OrElse reply(3) > 1 OrElse reply(4) > 2 Then
            wifiLoaded = False : WifiControls()
            Theme.DotText(wifiChannel, Theme.eDot.Idle, If(reply(0) <> 0, "Связь: через Wi-Fi", "Связь: через GSM"))
            Theme.DotText(wifiState, Theme.eDot.Idle, "Прошивка не поддерживает локальный выбор Wi-Fi-модема")
            Return
        End If
        wifiSelected = reply(3) = 1
        wifiPortBusy = wifiSelected OrElse reply(1) = 5
        If Not wifiLoaded OrElse reload Then
            wifiLoading = True
            wifiModel.SelectedIndex = reply(3) : wifiPolicy.SelectedIndex = reply(4)
            wifiLoading = False
        End If
        wifiLoaded = True
        Dim channel = If(reply(5) = 1, "Wi-Fi", If(reply(5) = 2, "2G", If(reply(5) = 4, "4G", "нет данных")))
        Theme.DotText(wifiChannel, If(reply(5) = 0, Theme.eDot.Idle, Theme.eDot.Good), "Связь: " & channel)
        Dim text = "Wi-Fi-модем не выбран", dot = Theme.eDot.Idle
        If reply(1) = 4 Then
            text = "Мост не подтвердил выключение" : dot = Theme.eDot.Bad
        ElseIf reply(1) = 5 Then
            text = "Выключение моста…"
        ElseIf wifiSelected Then
            Select Case reply(1)
                Case 2 : text = "Мост: TCP сервера открыт" : dot = Theme.eDot.Good
                Case 3 : text = If(reply(4) = 0, "TCP моста выключен: только GSM", "TCP моста выключен; ожидание повтора")
                Case 1 : text = "Мост отвечает, TCP закрыт" : dot = Theme.eDot.Good
                Case Else : text = "Мост: нет подтверждённой связи" : dot = Theme.eDot.Bad
            End Select
        End If
        Theme.DotText(wifiState, dot, text)
        WifiControls()
        If warning485 IsNot Nothing Then Update485State()
    End Sub

    ' 0 — чтение, 1 — точка доступа, 2 — устойчивый локальный выбор.
    Private Async Sub WifiExchange(action As Integer)
        If wifiBusy OrElse busyWithBoard OrElse opros Is Nothing Then Return
        Dim payload As Byte() = {}
        Dim model = CByte(Math.Max(0, wifiModel.SelectedIndex)), policy = CByte(Math.Max(0, wifiPolicy.SelectedIndex))
        If action = 1 Then
            Dim ssid = Encoding.UTF8.GetBytes(wifiSsid.Text.Trim()), key = Encoding.UTF8.GetBytes(wifiKey.Text)
            If ssid.Length = 0 OrElse ssid.Length > 32 OrElse key.Length > 63 OrElse ssid.Contains(CByte(0)) OrElse key.Contains(CByte(0)) Then
                Theme.DotText(wifiState, Theme.eDot.Bad, "Сеть 1–32 байта, пароль до 63; пустой пароль — открытая сеть")
                Return
            End If
            payload = ssid.Concat({CByte(0)}).Concat(key).Concat({CByte(0)}).ToArray()
        End If
        wifiBusy = True : WifiControls()
        Try
            If action = 2 Then
                If Not Await Task.Run(Function() opros.SetWifiModem(model, policy)) Then
                    Theme.DotText(wifiState, Theme.eDot.Bad, "Выбор не сохранён. Повторите запись.")
                    Return
                End If
            End If
            Dim reply = Await Task.Run(Function() opros.WifiBridge(payload))
            If IsDisposed Then Return
            If action = 1 Then
                Dim ok = reply IsNot Nothing AndAlso reply.Length >= 2 AndAlso reply(0) = 0
                Theme.DotText(wifiState, If(ok, Theme.eDot.Idle, Theme.eDot.Bad),
                    If(ok, "Принято: мост перезапускается, проверка связи через 10–20 с", "Сеть не принята: проверьте поля и повторите запись"))
                If ok Then wifiKey.Clear()
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
