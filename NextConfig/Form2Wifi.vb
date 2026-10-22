' Доработка: Кряжев А., 2026. Локальный выбор Wi-Fi-модема хранится в device платы.
Imports System.Linq
Imports System.Text
Imports System.Threading.Tasks
Imports System.Windows.Forms

Partial Public Class Form2
    Private wifiSsid As TextBox, wifiKey As TextBox
    Private wifiModel As ComboBox, wifiPolicy As ComboBox
    Private wifiState As Label, wifiRule As Label
    Private wifiWrite As Button, wifiCheck As Button
    Private wifiBusy As Boolean, wifiLoaded As Boolean, wifiSelected As Boolean
    Private wifiLoading As Boolean, wifiPortBusy As Boolean, wifiStateKnown As Boolean, wifiMisses As Integer
    Private wifiBoardModel As Byte, wifiBoardPolicy As Byte
    Private wifiOptions As Control
    Private wifiSetupHint As Label

    ' Поля измеряются по содержимому: фиксированная высота обрезала нижнюю рамку.
    Private Function OperatorField(title As String, editor As Control) As Control
        Dim field As New TableLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .ColumnCount = 1, .Anchor = AnchorStyles.Left Or AnchorStyles.Right, .Margin = New Padding(0, 0, 0, Theme.GapRow)}
        field.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        field.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        field.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        field.Controls.Add(New Label With {.Text = title, .AutoSize = True, .Font = Theme.FontBody,
            .ForeColor = Theme.TextMain, .Margin = New Padding(0, 0, 0, Theme.GapLabel)}, 0, 0)
        editor.Anchor = AnchorStyles.Left Or AnchorStyles.Right
        editor.Margin = New Padding(0)
        field.Controls.Add(editor, 0, 1)
        Return field
    End Function

    Private Function OperatorNote(text As String) As Label
        Return New Label With {.Text = text, .AutoSize = True, .Font = Theme.FontCaption,
            .ForeColor = Theme.TextMuted, .Anchor = AnchorStyles.Left Or AnchorStyles.Right,
            .Margin = New Padding(0, 0, 0, Theme.GapRelated)}
    End Function

    Private Function OperatorColumns(left As Control, right As Control, Optional breakpoint As Integer = 760) As Control
        Dim grid As New TableLayoutPanel With {.Name = "operatorColumns", .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink, .ColumnCount = 2, .RowCount = 1,
            .Anchor = AnchorStyles.Left Or AnchorStyles.Right, .Margin = New Padding(0)}
        grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        grid.Controls.Add(left, 0, 0) : grid.Controls.Add(right, 1, 0)
        Dim arrange As EventHandler = Sub()
            Dim stacked = grid.Width < Theme.ControlPx(grid, breakpoint)
            Dim column = If(stacked, 0, 1), row = If(stacked, 1, 0)
            If grid.GetColumn(right) = column AndAlso grid.GetRow(right) = row Then Return
            grid.SuspendLayout()
            grid.ColumnStyles(0).Width = If(stacked, 100, 50)
            grid.ColumnStyles(1).Width = If(stacked, 0, 50)
            grid.SetCellPosition(right, New TableLayoutPanelCellPosition(column, row))
            left.Margin = New Padding(0, 0, If(stacked, 0, Theme.GapRelated), Theme.GapRow)
            right.Margin = New Padding(If(stacked, 0, Theme.GapRelated), 0, 0, Theme.GapRow)
            grid.ResumeLayout(True)
        End Sub
        left.Margin = New Padding(0, 0, Theme.GapRelated, Theme.GapRow)
        right.Margin = New Padding(Theme.GapRelated, 0, 0, Theme.GapRow)
        AddHandler grid.SizeChanged, arrange
        Return grid
    End Function

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
        wifiWrite = New Button With {.Name = "wifiWrite", .Text = "Записать сеть"}
        wifiCheck = New Button With {.Name = "wifiCheck", .Text = "Обновить статус"}
        For Each button In {wifiWrite, wifiCheck}
            Theme.Secondary(button)
            button.Margin = New Padding(0, 0, Theme.GapRelated, 0)
        Next
        AddHandler wifiModel.SelectedIndexChanged, Sub() WifiEdited()
        AddHandler wifiPolicy.SelectedIndexChanged, Sub() WifiEdited()
        AddHandler wifiWrite.Click, Sub() WifiExchange(1)
        AddHandler wifiCheck.Click, Sub() WifiExchange(0)
        Dim choice = Theme.Card("Подключение к серверу",
            OperatorColumns(OperatorField("Wi-Fi-модем", wifiModel), OperatorField("Канал связи с сервером", wifiPolicy), 600),
            OperatorNote("Примените выбор кнопкой «Сохранить изменения» внизу окна. Сервер этот выбор не меняет."))
        Dim network = OperatorColumns(OperatorField("Название сети (SSID)", ssidBox), OperatorField("Пароль сети", keyBox), 600)
        wifiOptions = network
        Dim actions As New FlowLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .WrapContents = True, .Anchor = AnchorStyles.Left Or AnchorStyles.Right,
            .Margin = New Padding(0, 0, 0, Theme.GapRelated)}
        actions.Controls.AddRange({wifiWrite, wifiCheck})
        wifiState = Theme.BusNotice("Состояние моста не запрашивалось", False)
        wifiState.Name = "wifiState"
        wifiState.Font = Theme.FontBody
        wifiState.ForeColor = Theme.TextMain
        wifiState.BackColor = Theme.SurfaceHover
        wifiSetupHint = OperatorNote("")
        Dim networkCard = Theme.Card("Сеть Wi-Fi", network, actions, wifiSetupHint)
        choice.Dock = DockStyle.Fill : networkCard.Dock = DockStyle.Fill
        wifiRule = OperatorNote("Wi-Fi-модем занимает RS-485: камера недоступна, VoLTE выключен. В режиме «Только Wi-Fi» сотовая связь остаётся для звонков.")
        WifiControls()
        Dim heading As New TableLayoutPanel With {.AutoSize = True, .ColumnCount = 2,
            .Anchor = AnchorStyles.Left Or AnchorStyles.Right, .Margin = New Padding(0, 0, 0, Theme.GapRow)}
        heading.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, Theme.Px(210)))
        heading.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        heading.Controls.Add(New Label With {.Text = "Wi-Fi-модем", .AutoSize = True, .Font = Theme.FontTitle,
            .ForeColor = Theme.TextMain, .Anchor = AnchorStyles.Left, .Margin = New Padding(0)}, 0, 0)
        wifiState.Margin = New Padding(0)
        heading.Controls.Add(wifiState, 1, 0)
        Dim section As New TableLayoutPanel With {.AutoSize = True, .ColumnCount = 1, .BackColor = Theme.WindowBack,
            .Anchor = AnchorStyles.Left Or AnchorStyles.Right, .Margin = New Padding(0, 0, 0, Theme.GapRow)}
        section.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        section.Controls.Add(heading)
        section.Controls.Add(OperatorColumns(choice, networkCard))
        section.Controls.Add(wifiRule)
        Return section
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
        Theme.SetText(wifiSetupHint, If(Not selected, "Выберите Wi-Fi-модем и сохраните изменения, чтобы настроить сеть.",
            If(Not wifiSelected, "Сначала сохраните выбор модема кнопкой «Сохранить изменения» внизу окна.",
                "«Записать сеть» сразу передаёт сеть и пароль в модем и перезапускает его. Пустой пароль — открытая сеть.")))
        wifiSsid.Enabled = Not wifiBusy : wifiKey.Enabled = Not wifiBusy
        If cmbVoLTE IsNot Nothing Then cmbVoLTE.Enabled = Not wifiSelected
    End Sub

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
        AddChange(list, "Логика работы", CStr(wifiPolicy.Items(wifiBoardPolicy)), CStr(wifiPolicy.SelectedItem))
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

    ' 0 — чтение, 1 — точка доступа.
    Private Async Sub WifiExchange(action As Integer)
        If wifiBusy OrElse busyWithBoard OrElse opros Is Nothing Then Return
        Dim payload As Byte() = {}
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
