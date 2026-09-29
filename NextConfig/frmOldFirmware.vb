Imports System.IO
Imports System.Threading.Tasks

' Один экран чтения. Все размеры, цвета и шрифты задаются через Theme.
Public Class frmOldFirmware
    Inherits Form
    Private ReadOnly device As OldFirmware
    Private ReadOnly settingsGrid As New DataGridView()
    Private ReadOnly statusGrid As New DataGridView()
    Private ReadOnly numbersGrid As New DataGridView()
    Private ReadOnly password As New TextBox()
    Private ReadOnly search As New TextBox()
    Private ReadOnly showSecrets As New CheckBox()
    Private ReadOnly feedback As New Label()
    Private ReadOnly monitorTitle As New Label()
    Private ReadOnly numbersTitle As New Label()
    Private ReadOnly readConfig As New Button()
    Private ReadOnly readBase As New Button()
    Private ReadOnly export As New Button()
    Private ReadOnly refreshButton As New Button()
    Private ReadOnly closeButton As New Button()
    Private ReadOnly poll As New Timer() With {.Interval = 2000}
    Private ReadOnly tabs As New TabControl()
    Private ReadOnly tabDevice As New TabPage("Устройство")
    Private ReadOnly tabUsers As New TabPage("Пользователи")
    Private ReadOnly tabSettings As New TabPage("Настройки")
    Private ReadOnly tabSchedules As New TabPage("Расписания")
    Private ReadOnly tabService As New TabPage("Сервис")
    Private ReadOnly schedulesGrid As New DataGridView()
    Private ReadOnly numberDetails As New TextBox With {.Multiline = True, .ReadOnly = True}
    Private ReadOnly stateValues As New Dictionary(Of String, Label)
    Private ReadOnly stateDots As New Dictionary(Of String, Label)
    Private ReadOnly signalBar As New Meter With {.Maximum = 31}
    Private ReadOnly migrate As New Button With {.Text = "Перевести на новую систему"}
    Public Property EnteredBootloader As Boolean
    Private settings As DataTable
    Private numbers As DataTable
    Private busy As Boolean
    ' фоновый опрос состояния: кнопки при нём не гаснут, но порт он занимает
    Private polling As Boolean
    ' порт один: опрос и действие оператора обращаются к плате по очереди
    Private ReadOnly portGate As New Threading.SemaphoreSlim(1, 1)
    Private closePending As Boolean

    Public Sub New(reader As OldFirmware, portName As String)
        device = reader
        Text = "Домовой · старая прошивка" & If(Demo.IsOn, " · ДЕМО", "")
        AutoScaleMode = AutoScaleMode.None : Theme.StyleForm(Me)
        StartPosition = FormStartPosition.CenterScreen
        ClientSize = New Size(Theme.Px(1280), Theme.Px(800))
        Dim area = Screen.FromControl(Me).WorkingArea
        MinimumSize = New Size(Math.Min(Theme.Px(1000), area.Width), Math.Min(Theme.Px(650), area.Height))
        Size = New Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height))
        Dim root As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3, .Margin = New Padding(0)}
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Controls.Add(root)
        Dim actions As New FlowLayoutPanel With {.AutoSize = False, .WrapContents = False, .Size = New Size(Theme.Px(370), Theme.Px(40)), .Margin = New Padding(0)}
        actions.Controls.Add(New Label With {.Text = "Пароль (0–65535):", .Font = Theme.FontCaption, .AutoSize = True,
            .Margin = New Padding(0, Theme.GapRelated, Theme.GapRelated, 0)})
        password.Name = "oldPassword" : password.UseSystemPasswordChar = True : password.MaxLength = 5
        actions.Controls.Add(Theme.FieldBox(password, 100))
        SetupButton(closeButton, "Закрыть", 100) : actions.Controls.Add(closeButton)
        root.Controls.Add(Theme.BoardHeader("Устройство " & device.Imei,
            portName & " · старая прошивка " & device.Identification(5) & " · плата " & device.Identification(4), actions), 0, 0)
        SetupButton(readConfig, "Прочитать настройки", 200) : Theme.Primary(readConfig)
        SetupButton(readBase, "Прочитать базу", 170)
        SetupButton(export, "Сохранить базу CSV", 190)
        SetupButton(refreshButton, "Обновить состояние", 190)
        SetupButton(migrate, "Перевести на новую систему", 280) : Theme.Primary(migrate)
        export.Enabled = False
        For Each page In {tabDevice, tabUsers, tabSettings, tabSchedules, tabService}
            page.BackColor = Theme.WindowBack : page.AutoScroll = True
        Next
        tabs.TabPages.AddRange({tabDevice, tabUsers, tabSettings, tabSchedules, tabService})
        root.Controls.Add(Theme.MainNavigation(tabs), 0, 1)
        feedback.Dock = DockStyle.Fill : feedback.AutoSize = True : feedback.Font = Theme.FontCaption
        feedback.Margin = New Padding(Theme.GapSection, Theme.GapRelated, Theme.GapSection, Theme.GapRelated)
        feedback.Text = device.WriteSupportText
        AgentLog.WatchText(feedback, "oldfw.feedback")
        root.Controls.Add(feedback, 0, 2)

        Dim connection = Theme.Card("Связь",
            StateRow("Мобильная сеть", "gsm", True), StateRow("Интернет", "gprs", True),
            StateRow("Соединение с сервером", "server", True), StateRow("Технология сейчас", "net"),
            StateRow("Уровень сигнала", "signal"))
        signalBar.Height = Theme.Px(8) : signalBar.Dock = DockStyle.Top : connection.Controls.Add(signalBar)
        Dim version As New Label With {.Text = device.Identification(5).ToString(), .AutoSize = True}
        Theme.Value(version)
        Dim boot As New Label With {.Text = "0x" & device.Identification(3).ToString("X2"), .AutoSize = True}
        Theme.Value(boot)
        Dim fwCard = Theme.Card("Устройство",
            Theme.ValueRow("Версия прошивки", version), Theme.ValueRow("Версия загрузчика", boot),
            StateRow("Сборка (коды платы)", "build"), StateRow("Время устройства", "time"),
            StateRow("Память номеров", "memory"), migrate,
            New Label With {.Text = "Перевод: новая прошивка, затем замена загрузчика. Настройки старой системы автоматически не переносятся.",
                .AutoSize = True, .Dock = DockStyle.Fill, .Font = Theme.FontCaption, .ForeColor = Theme.TextMuted})
        Dim inputs = Theme.Card("Входы и выходы", Theme.SignalGrid(
            {StateIndicator("in1"), StateIndicator("in2"), StateIndicator("out")}, {Nothing, Nothing, Nothing}))
        Dim overview = DirectCast(Theme.Overview({connection, fwCard, inputs}), TableLayoutPanel)
        Dim grid = DirectCast(overview.Controls(1), TableLayoutPanel)
        grid.RowCount = 5 : grid.RowStyles(0).SizeType = SizeType.AutoSize
        For i = 2 To 4
            grid.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Next
        Dim photos = BuildOldPhotos()
        grid.Controls.Add(photos, 0, 2) : grid.SetColumnSpan(photos, 2)
        Dim quickExport As New Button()
        SetupButton(quickExport, "Сохранить базу CSV", 190)
        AddHandler quickExport.Click, Async Sub()
            If numbers Is Nothing Then Await ReadDatabase()
            If numbers IsNot Nothing Then ExportBase(Nothing, EventArgs.Empty)
        End Sub
        Dim quick As New FlowLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Fill}
        quick.Controls.AddRange({refreshButton, quickExport})
        grid.Controls.Add(quick, 0, 3) : grid.SetColumnSpan(quick, 2)
        Dim journal As New Label With {.Text = "Чтение журнала не поддерживается этой прошивкой.",
            .AutoSize = True, .Font = Theme.FontCaption, .ForeColor = Theme.TextMuted, .Dock = DockStyle.Fill}
        grid.Controls.Add(journal, 0, 4) : grid.SetColumnSpan(journal, 2)
        grid.AutoSize = True : grid.Dock = DockStyle.Top
        overview.RowStyles(1).SizeType = SizeType.AutoSize : overview.AutoScrollMinSize = Size.Empty
        tabDevice.Controls.Add(overview)
        AddHandler overview.SizeChanged, Sub() Theme.ReflowOverview(overview)

        Theme.ReadOnlyGrid(settingsGrid) : Theme.ReadOnlyGrid(statusGrid)
        Theme.ReadOnlyGrid(numbersGrid) : Theme.ReadOnlyGrid(schedulesGrid)
        showSecrets.Text = "Показать пароли и PIN в настройках" : showSecrets.AutoSize = True : showSecrets.Font = Theme.FontCaption
        search.Width = Theme.Px(260)
        Dim settingsActions As New FlowLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Fill}
        settingsActions.Controls.Add(readConfig)
        settingsActions.Controls.Add(New Label With {.Text = "Поиск:", .Font = Theme.FontCaption, .AutoSize = True, .Padding = New Padding(0, Theme.GapRelated, 0, 0)})
        settingsActions.Controls.Add(Theme.FieldBox(search, 260))
        SetupSettingsWrite(settingsActions)
        tabSettings.Controls.Add(GridPage("Настройки", "Выберите параметр и измените черновик · запись после подтверждения", settingsGrid, settingsActions, showSecrets))
        numbersTitle.Text = "База номеров / ID · ещё не прочитана" : numbersTitle.AutoSize = True : Theme.Group(numbersTitle)
        Dim numberActions As New FlowLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Fill}
        numberActions.Controls.AddRange({readBase, export})
        numberDetails.Dock = DockStyle.Fill : numberDetails.Height = Theme.Px(112)
        numberDetails.ScrollBars = ScrollBars.Vertical : numberDetails.BorderStyle = BorderStyle.None
        numberDetails.Font = Theme.FontCaption : numberDetails.BackColor = Theme.Surface
        tabUsers.Controls.Add(GridPage("Пользователи", "База номеров / ID · чтение и сохранение CSV", numbersGrid, numberActions, numberDetails))
        AddHandler numbersGrid.SelectionChanged, AddressOf ShowNumberDetails
        Dim readSchedules As New Button()
        SetupButton(readSchedules, "Прочитать расписания", 220)
        AddHandler readSchedules.Click, Async Sub() Await ReadConfiguration()
        tabSchedules.Controls.Add(GridPage("Расписания", "Двойной щелчок — изменить · запись в разделе Настройки", schedulesGrid, readSchedules))
        monitorTitle.AutoSize = True : Theme.Group(monitorTitle)
        statusGrid.Height = Theme.Px(420) : statusGrid.Dock = DockStyle.Top
        Dim allSettings As New Button()
        SetupButton(allSettings, "Все настройки платы", 230)
        AddHandler allSettings.Click, Sub() tabs.SelectedTab = tabSettings
        tabService.Controls.Add(Theme.CardPage(Theme.PageHeading("Сервис", "Данные платы для проверки и диагностики"),
            Theme.Card("Мониторинг · все поля", monitorTitle, statusGrid),
            Theme.Card("Настройки платы", allSettings,
                New Label With {.Text = "Для настроек нужен администратор, для базы — управляющий или администратор. " &
                    If(Demo.IsOn, "ДЕМО: пароль не требуется.", "Пароль вводится только владельцем."),
                    .AutoSize = True, .Dock = DockStyle.Fill, .Font = Theme.FontCaption, .ForeColor = Theme.TextMuted})))
        AddHandler migrate.Click, AddressOf EnterBootloader
        AddHandler Shown, Async Sub(s, e)
            Theme.FitBoardWindow(Me)
            Await LoadInitial()
        End Sub
        AddHandler readConfig.Click, Async Sub(s, e) Await ReadConfiguration()
        AddHandler readBase.Click, Async Sub(s, e) Await ReadDatabase()
        AddHandler refreshButton.Click, Async Sub(s, e) Await RefreshStatus()
        AddHandler poll.Tick, Async Sub(s, e) Await RefreshStatus(True)
        AddHandler export.Click, AddressOf ExportBase
        AddHandler closeButton.Click, Sub(s, e) Close()
        AddHandler search.TextChanged, Sub(s, e) FilterSettings()
        AddHandler showSecrets.CheckedChanged, Sub(s, e) settingsGrid.Invalidate()
        AddHandler settingsGrid.CellFormatting, AddressOf FormatSetting
        AddHandler FormClosing, AddressOf ClosingReader
        AddHandler FormClosed, Sub(s, e) poll.Dispose()
    End Sub

    Private Sub SetupButton(button As Button, title As String, width As Integer)
        Theme.Secondary(button)
        button.Text = title
        button.Size = New Size(Theme.Px(width), Theme.ButtonHeightSmall)
    End Sub

    Private Async Function LoadInitial() As Task
        Await RefreshStatus()
        If IsDisposed OrElse closePending Then Return
        If Demo.IsOn Then
            Await ReadConfiguration()
            If IsDisposed OrElse closePending Then Return
            Await ReadDatabase()
        End If
    End Function

    Private Sub SetBusy(value As Boolean)
        busy = value
        oldShoot.Enabled = Not value AndAlso device.SupportsPhoto
        reconnect.Enabled = Not value
        For Each button In writeButtons
            button.Enabled = Not value AndAlso device.CanWrite
        Next
        migrate.Enabled = Not value
        readConfig.Enabled = Not value
        readBase.Enabled = Not value
        refreshButton.Enabled = Not value
        export.Enabled = Not value AndAlso numbers IsNot Nothing
        password.Enabled = Not value
    End Sub

    Private Function GetPassword() As UShort?
        If password.Text.Length = 0 Then Return Nothing
        Dim n As UShort
        If Not UShort.TryParse(password.Text, Globalization.NumberStyles.None,
                              Globalization.CultureInfo.InvariantCulture, n) Then
            Throw New InvalidDataException("Пароль должен быть целым числом от 0 до 65535.")
        End If
        password.Clear()
        Return n
    End Function

    ' background = фоновый опрос по таймеру: кнопки не трогает и пропускает ход,
    ' если плата занята. Действие оператора дожидается конца опроса, а не теряется.
    Private Async Function RunRead(action As Func(Of DataTable), apply As Action(Of DataTable),
                                   message As String, Optional authenticate As Boolean = False,
                                   Optional requiredAccess As Byte = 0,
                                   Optional background As Boolean = False) As Task
        If IsDisposed OrElse closePending Then Return
        If background AndAlso (busy OrElse polling) Then Return
        If Not background AndAlso busy Then Return
        Dim pin As UShort? = Nothing
        Try
            If authenticate Then pin = GetPassword()
            If background Then polling = True Else SetBusy(True)
            If authenticate Then feedback.Text = "Чтение…"
            Await portGate.WaitAsync()
            Dim result As DataTable
            Try
                result = Await Task.Run(Function()
                                            If pin.HasValue Then
                                                Dim access = device.Authenticate(pin.Value)
                                                If access <> &H20 AndAlso (requiredAccess = &H20 OrElse access <> &H10) Then
                                                    Throw New InvalidDataException("Недостаточно прав для этого чтения.")
                                                End If
                                            End If
                                            Return action()
                                        End Function)
            Finally
                portGate.Release()
            End Try
            If Not closePending Then
                apply(result)
                If message <> "" Then feedback.Text = message
            End If
        Catch ex As Exception
            feedback.Text = ex.Message & " Можно переподключиться к этой плате в разделе Устройство."
            reconnect.Visible = True
            If Not authenticate Then
                monitorTitle.Text = "Состояние · нет свежих данных"
                poll.Stop()
            End If
        Finally
            pin = Nothing
            If background Then polling = False Else SetBusy(False)
            If closePending AndAlso Not (busy OrElse polling) Then Close()
        End Try
    End Function

    Private Async Function RefreshStatus(Optional background As Boolean = False) As Task
        Await RunRead(Function() device.ReadStatus(),
            Sub(t)
                Dim top = If(statusGrid.Rows.Count > 0, Math.Max(0, statusGrid.FirstDisplayedScrollingRowIndex), 0)
                BindFields(statusGrid, t)
                UpdateStatusCards(t)
                If top < statusGrid.Rows.Count Then statusGrid.FirstDisplayedScrollingRowIndex = top
                monitorTitle.Text = "Состояние · " & DateTime.Now.ToString("HH:mm:ss")
                poll.Start()
            End Sub, "", background:=background)
    End Function

    Private Async Function ReadConfiguration() As Task
        Await RunRead(Function() device.ReadSettings(),
            Sub(t)
                originalSettings = device.SettingsSnapshot()
                editedSettings = CType(originalSettings.Clone(), Byte())
                ShowSettings(editedSettings)
            End Sub, "Настройки прочитаны. Изменения сохраняются в черновике до подтверждения записи.", True, &H20)
    End Function

    Private Async Function ReadDatabase() As Task
        ' Старый снимок нельзя экспортировать после ошибки повторного чтения.
        If busy OrElse IsDisposed OrElse closePending Then Return
        numbers = Nothing
        numbersGrid.DataSource = Nothing
        numbersTitle.Text = "База номеров / ID · чтение…"
        Await RunRead(Function() device.ReadNumbers(),
            Sub(t)
                numbers = t
                numbersGrid.DataSource = t
                numbersGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
                numbersGrid.ScrollBars = ScrollBars.Vertical
                For Each column As DataGridViewColumn In numbersGrid.Columns
                    column.Visible = column.Index <= 4 OrElse column.Index = 7
                    column.FillWeight = If(column.Index = 0, 8, If(column.Index = 2, 28, 16))
                Next
                ShowNumberDetails(Nothing, EventArgs.Empty)
                numbersTitle.Text = "База номеров / ID · записей: " & t.Rows.Count
            End Sub, "База прочитана полностью. Можно сохранить CSV.", True, &H10)
        If numbers Is Nothing Then numbersTitle.Text = "База номеров / ID · не прочитана"
    End Function

    Private Sub BindFields(grid As DataGridView, table As DataTable)
        grid.DataSource = table
        grid.ScrollBars = ScrollBars.Vertical
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells
        grid.Columns(0).Visible = False
        grid.Columns(1).AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        grid.Columns(1).FillWeight = 62
        grid.Columns(2).AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        grid.Columns(2).FillWeight = 38
    End Sub

    Private Sub FilterSettings()
        If settings Is Nothing Then Return
        ' LINQ по строкам вместо RowFilter: ввод %, [, ', * не становится выражением.
        Dim q = search.Text.Trim()
        Dim view = settings.Clone()
        For Each row As DataRow In settings.Rows
            If (CStr(row(0)) & " " & CStr(row(1))).IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0 Then view.ImportRow(row)
        Next
        settingsGrid.DataSource = view
    End Sub

    Private Sub FormatSetting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 OrElse e.ColumnIndex <> 2 OrElse showSecrets.Checked Then Return
        Dim row = TryCast(settingsGrid.Rows(e.RowIndex).DataBoundItem, DataRowView)
        If row IsNot Nothing AndAlso CStr(row(0)) = "Доступ" Then
            e.Value = "••••" : e.FormattingApplied = True
        End If
    End Sub

    Private Sub ExportBase(sender As Object, e As EventArgs)
        If busy OrElse numbers Is Nothing Then Return
        Using dialog As New SaveFileDialog With {.Filter = "База номеров CSV (*.csv)|*.csv", .DefaultExt = "csv",
            .FileName = "Domovoi_номера_" & DateTime.Now.ToString("yyyyMMdd_HHmmss") & ".csv"}
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                OldFirmware.SaveCsv(numbers, dialog.FileName)
                feedback.Text = "Сохранено записей: " & numbers.Rows.Count & " · " & dialog.FileName
            Catch ex As Exception
                feedback.Text = "Не удалось сохранить CSV: " & ex.Message
            End Try
        End Using
    End Sub

    Private Sub ClosingReader(sender As Object, e As FormClosingEventArgs)
        poll.Stop()
        closePending = True
        device.CancelRead()
        If busy OrElse polling Then
            e.Cancel = True
            feedback.Text = "Завершение текущего чтения перед закрытием…"
        End If
    End Sub
End Class
