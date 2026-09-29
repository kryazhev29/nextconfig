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
    Private settings As DataTable
    Private numbers As DataTable
    Private busy As Boolean
    Private closePending As Boolean

    Public Sub New(reader As OldFirmware, portName As String)
        device = reader
        Text = "Домовой · старая прошивка · только чтение" & If(Demo.IsOn, " · ДЕМО", "")
        AutoScaleMode = AutoScaleMode.None
        Theme.StyleForm(Me)
        StartPosition = FormStartPosition.CenterScreen
        ClientSize = New Size(Theme.Px(1280), Theme.Px(840))
        MinimumSize = New Size(Theme.Px(960), Theme.Px(680))
        Dim area = Screen.FromControl(Me).WorkingArea
        Size = New Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height))

        Dim root As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 6,
            .Padding = New Padding(Theme.Px(14))}
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, Theme.Px(112)))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, Theme.Px(76)))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, Theme.Px(190)))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, Theme.Px(51)))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, Theme.Px(30)))
        Controls.Add(root)
        Dim identity As New Label With {.Dock = DockStyle.Fill, .Font = Theme.FontCaption,
            .Text = portName & " · " & device.IdentityText(), .AutoEllipsis = True}
        Dim header As New TableLayoutPanel With {.Dock = DockStyle.Fill, .RowCount = 2, .ColumnCount = 1}
        header.RowStyles.Add(New RowStyle(SizeType.Absolute, Theme.Px(46)))
        header.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        Dim brand = Theme.BrandLine()
        brand.Margin = New Padding(0)
        brand.Dock = DockStyle.Fill
        header.Controls.Add(brand, 0, 0)
        header.Controls.Add(identity, 0, 1)
        root.Controls.Add(header, 0, 0)

        Dim actions As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .WrapContents = True}
        actions.Controls.Add(New Label With {.Text = "Пароль (0–65535):", .AutoSize = True,
            .Margin = New Padding(Theme.Px(3), Theme.Px(12), Theme.Px(5), 0)})
        password.Name = "oldPassword"
        password.UseSystemPasswordChar = True
        password.MaxLength = 5
        password.Width = Theme.Px(85)
        password.Margin = New Padding(0, Theme.Px(8), Theme.Px(12), 0)
        actions.Controls.Add(password)
        SetupButton(readConfig, "Прочитать настройки", 190)
        Theme.Primary(readConfig)
        SetupButton(readBase, "Прочитать базу", 165)
        SetupButton(export, "Сохранить базу CSV", 185)
        SetupButton(refreshButton, "Обновить состояние", 190)
        SetupButton(closeButton, "Закрыть", 100)
        export.Enabled = False
        actions.Controls.AddRange({readConfig, readBase, export, refreshButton, closeButton})
        root.Controls.Add(actions, 0, 1)

        Dim columns As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1}
        columns.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 54))
        columns.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 46))
        root.Controls.Add(columns, 0, 2)
        Dim left As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 4}
        left.RowStyles.Add(New RowStyle(SizeType.Absolute, Theme.Px(30)))
        left.RowStyles.Add(New RowStyle(SizeType.Absolute, Theme.Px(34)))
        left.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        left.RowStyles.Add(New RowStyle(SizeType.Absolute, Theme.Px(28)))
        left.Controls.Add(New Label With {.Text = "Все настройки · только просмотр", .Font = Theme.FontGroup, .Dock = DockStyle.Fill}, 0, 0)
        Dim findPanel As New FlowLayoutPanel With {.Dock = DockStyle.Fill}
        findPanel.Controls.Add(New Label With {.Text = "Поиск:", .AutoSize = True})
        search.Width = Theme.Px(280)
        search.Name = "oldSettingsSearch"
        findPanel.Controls.Add(search)
        left.Controls.Add(findPanel, 0, 1)
        Theme.ReadOnlyGrid(settingsGrid)
        left.Controls.Add(settingsGrid, 0, 2)
        showSecrets.Text = "Показать пароли и PIN в настройках"
        showSecrets.AutoSize = True
        left.Controls.Add(showSecrets, 0, 3)
        columns.Controls.Add(left, 0, 0)

        Dim right As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 4}
        right.RowStyles.Add(New RowStyle(SizeType.Absolute, Theme.Px(30)))
        right.RowStyles.Add(New RowStyle(SizeType.Percent, 52))
        right.RowStyles.Add(New RowStyle(SizeType.Absolute, Theme.Px(34)))
        right.RowStyles.Add(New RowStyle(SizeType.Percent, 48))
        monitorTitle.Text = "Состояние · ожидание данных"
        monitorTitle.Font = Theme.FontGroup
        monitorTitle.Dock = DockStyle.Fill
        right.Controls.Add(monitorTitle, 0, 0)
        Theme.ReadOnlyGrid(statusGrid)
        right.Controls.Add(statusGrid, 0, 1)
        numbersTitle.Text = "База номеров / ID · ещё не прочитана"
        numbersTitle.Font = Theme.FontGroup
        numbersTitle.Dock = DockStyle.Fill
        numbersTitle.Padding = New Padding(0, Theme.Px(6), 0, 0)
        right.Controls.Add(numbersTitle, 0, 2)
        Theme.ReadOnlyGrid(numbersGrid)
        right.Controls.Add(numbersGrid, 0, 3)
        columns.Controls.Add(right, 1, 0)

        ' FW/User/user_fun.c:741-742: 0x2e пуст; :764-821: 0x40 — SMS/модем;
        ' :824-826: 0x41 подтверждает серверный лог. Unit1.cpp:3854-3865,
        ' :3911-3918 использует 0x2e; :3901-3907 и :5396-5414 — журнал сервера.
        Dim journal As New Label With {.Dock = DockStyle.Fill, .Font = Theme.FontCaption,
            .Padding = New Padding(0, Theme.Px(7), 0, 0),
            .Text = "Журнал: чтение журнала не поддерживается этой прошивкой." & vbCrLf &
                "Для настроек нужен администратор, для базы — управляющий или администратор. " &
                If(Demo.IsOn, "ДЕМО: доступ разрешён, пароль не требуется.", "Пароль вводится только владельцем.")}
        root.Controls.Add(New PhotoPanel(Nothing, device.Imei, True), 0, 3)
        root.Controls.Add(journal, 0, 4)
        feedback.Dock = DockStyle.Fill
        feedback.AutoEllipsis = True
        feedback.Font = Theme.FontCaption
        feedback.Text = "Порт занят только этим сеансом чтения."
        root.Controls.Add(feedback, 0, 5)
        AddHandler Shown, Async Sub(s, e) Await LoadInitial()
        AddHandler readConfig.Click, Async Sub(s, e) Await ReadConfiguration()
        AddHandler readBase.Click, Async Sub(s, e) Await ReadDatabase()
        AddHandler refreshButton.Click, Async Sub(s, e) Await RefreshStatus()
        AddHandler poll.Tick, Async Sub(s, e) Await RefreshStatus()
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

    Private Async Function RunRead(action As Func(Of DataTable), apply As Action(Of DataTable),
                                   message As String, Optional authenticate As Boolean = False,
                                   Optional requiredAccess As Byte = 0) As Task
        If busy OrElse IsDisposed OrElse closePending Then Return
        Dim pin As UShort? = Nothing
        Try
            If authenticate Then pin = GetPassword()
            SetBusy(True)
            If authenticate Then feedback.Text = "Чтение…"
            Dim result = Await Task.Run(Function()
                                            If pin.HasValue Then
                                                Dim access = device.Authenticate(pin.Value)
                                                If access <> &H20 AndAlso (requiredAccess = &H20 OrElse access <> &H10) Then
                                                    Throw New InvalidDataException("Недостаточно прав для этого чтения.")
                                                End If
                                            End If
                                            Return action()
                                        End Function)
            If Not closePending Then
                apply(result)
                If message <> "" Then feedback.Text = message
            End If
        Catch ex As Exception
            feedback.Text = ex.Message
            If Not authenticate Then
                monitorTitle.Text = "Состояние · нет свежих данных"
                poll.Stop()
            End If
        Finally
            pin = Nothing
            SetBusy(False)
            If closePending Then Close()
        End Try
    End Function

    Private Async Function RefreshStatus() As Task
        Await RunRead(Function() device.ReadStatus(),
            Sub(t)
                Dim top = If(statusGrid.Rows.Count > 0, Math.Max(0, statusGrid.FirstDisplayedScrollingRowIndex), 0)
                BindFields(statusGrid, t)
                If top < statusGrid.Rows.Count Then statusGrid.FirstDisplayedScrollingRowIndex = top
                monitorTitle.Text = "Состояние · " & DateTime.Now.ToString("HH:mm:ss")
                poll.Start()
            End Sub, "")
    End Function

    Private Async Function ReadConfiguration() As Task
        Await RunRead(Function() device.ReadSettings(),
            Sub(t)
                settings = t
                BindFields(settingsGrid, settings)
                FilterSettings()
            End Sub, "Настройки прочитаны. Поля доступны только для просмотра.", True, &H20)
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
                For Each c As DataGridViewColumn In numbersGrid.Columns
                    c.Width = Theme.Px(130)
                Next
                numbersGrid.Columns(0).Width = Theme.Px(45)
                numbersGrid.Columns(2).Width = Theme.Px(170)
                numbersGrid.Columns(12).Visible = False
                numbersTitle.Text = "База номеров / ID · записей: " & t.Rows.Count
            End Sub, "База прочитана полностью. Можно сохранить CSV.", True, &H10)
        If numbers Is Nothing Then numbersTitle.Text = "База номеров / ID · не прочитана"
    End Function

    Private Sub BindFields(grid As DataGridView, table As DataTable)
        grid.DataSource = table
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
        If busy Then
            e.Cancel = True
            feedback.Text = "Завершение текущего чтения перед закрытием…"
        End If
    End Sub
End Class
