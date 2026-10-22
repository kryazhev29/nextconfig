' Доработка: Кряжев А., 2026. Выбор сети Wi-Fi для моста «как в Windows»: список найденных сетей с уровнем,
' подключённая первой с пометкой, выбор → пароль → «Сохранить сеть»; «Обновить» ищет заново; «Другая сеть…» — скрытое имя.
' Окно само опрашивает плату (USB 115) и отдаёт выбранное имя и пароль; запись в мост (USB 113) делает вызывающая форма.
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Text
Imports System.Threading.Tasks
Imports System.Windows.Forms

Public Class frmWifiPicker
    Inherits Form

    Public Property Ssid As String = ""
    Public Property Key As String = ""

    Private ReadOnly opros As clOpros
    Private ReadOnly current As String, currentLevel As Integer
    Private ReadOnly root As New TableLayoutPanel
    Private ReadOnly buttons As New FlowLayoutPanel
    Private ReadOnly list As New TableLayoutPanel
    Private ReadOnly viewport As New Panel
    Private ReadOnly state As Label
    Private ReadOnly btnRefresh As New Button With {.Name = "wifiRefresh", .Text = "Обновить"}
    Private ReadOnly btnSave As New Button With {.Name = "wifiSave", .Text = "Сохранить сеть"}
    Private ReadOnly btnCancel As New Button With {.Name = "wifiCancel", .Text = "Отмена"}
    Private ReadOnly editor As New TableLayoutPanel With {.Name = "wifiEditor"}
    Private ReadOnly nameBox As TextBox, keyBox As TextBox
    Private ReadOnly nameField As Control, keyField As Control
    Private namePanel As Control
    Private rows As New List(Of WifiRow)
    Private selected As WifiRow
    Private searching As Boolean
    Private shownNetworks As List(Of WifiNetwork)

    Public Sub New(poller As clOpros, currentSsid As String, level As Integer)
        opros = poller : current = If(currentSsid, "") : currentLevel = level
        nameBox = New TextBox With {.Name = "wifiHiddenName", .MaxLength = 32}
        keyBox = New TextBox With {.Name = "wifiNetworkKey", .MaxLength = 63}
        nameField = Theme.FieldBox(nameBox, 320)
        keyField = Theme.FieldBox(keyBox, 320)
        Theme.PasswordField(DirectCast(keyField, Panel), keyBox, "пароль")
        state = Theme.BusNotice("", False)
        state.Name = "wifiScanState" : state.Font = Theme.FontBody : state.ForeColor = Theme.TextMain : state.BackColor = Theme.SurfaceHover
        Build()
        AddHandler Shown, Sub() StartScan()
    End Sub

    Private Sub Build()
        SuspendLayout()
        Dim title As New Label With {.Text = "Сеть Wi-Fi", .AutoSize = True, .Margin = New Padding(0, 0, 0, Theme.GapLabel)}
        Theme.Title(title)
        Dim subtitle As New Label With {.Text = "Выберите сеть, к которой подключится Wi-Fi-мост, и введите её пароль.",
            .AutoSize = True, .MaximumSize = New Size(Theme.Px(460), 0), .Margin = New Padding(0, 0, 0, Theme.GapBlock)}
        Theme.Caption(subtitle)

        Dim head As New TableLayoutPanel With {.AutoSize = True, .ColumnCount = 2, .Anchor = AnchorStyles.Left Or AnchorStyles.Right,
            .Margin = New Padding(0, 0, 0, Theme.GapRelated)}
        head.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        head.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        Dim caption As New Label With {.Text = "Доступные сети", .AutoSize = True, .Anchor = AnchorStyles.Left, .Margin = New Padding(0)}
        Theme.Group(caption)
        Theme.Secondary(btnRefresh) : Theme.Small(btnRefresh) : btnRefresh.Margin = New Padding(0) : btnRefresh.Anchor = AnchorStyles.Right
        AddHandler btnRefresh.Click, Sub() StartScan()
        head.Controls.Add(caption, 0, 0) : head.Controls.Add(btnRefresh, 1, 0)

        state.Margin = New Padding(0, 0, 0, Theme.GapRelated)
        state.Anchor = AnchorStyles.Left Or AnchorStyles.Right

        list.ColumnCount = 1 : list.AutoSize = True : list.AutoSizeMode = AutoSizeMode.GrowAndShrink
        list.Dock = DockStyle.Top : list.Margin = New Padding(0) : list.BackColor = Theme.Surface
        list.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        viewport.AutoScroll = True : viewport.BackColor = Theme.Surface
        viewport.Size = New Size(Theme.Px(470), Theme.Px(360))
        viewport.Anchor = AnchorStyles.Left Or AnchorStyles.Right
        viewport.Margin = New Padding(0, 0, 0, Theme.GapBlock)
        viewport.Controls.Add(list)
        AddHandler viewport.Paint, Sub(sender, e)
            Using pen As New Pen(Theme.Border)
                e.Graphics.DrawRectangle(pen, 0, 0, viewport.Width - 1, viewport.Height - 1)
            End Using
        End Sub

        BuildEditor()

        Theme.Primary(btnSave) : btnSave.Width = Theme.Px(190) : btnSave.Margin = New Padding(Theme.GapRelated, 0, 0, 0) : btnSave.Enabled = False
        Theme.Secondary(btnCancel) : btnCancel.Width = Theme.Px(140) : btnCancel.Margin = New Padding(0)
        AddHandler btnSave.Click, Sub() Save()
        AddHandler btnCancel.Click, Sub()
            DialogResult = DialogResult.Cancel
            Close()
        End Sub
        buttons.AutoSize = True : buttons.AutoSizeMode = AutoSizeMode.GrowAndShrink
        buttons.FlowDirection = FlowDirection.RightToLeft : buttons.WrapContents = False
        buttons.Margin = New Padding(0) : buttons.Anchor = AnchorStyles.Right
        buttons.Controls.Add(btnSave) : buttons.Controls.Add(btnCancel)

        root.ColumnCount = 1 : root.AutoSize = True : root.AutoSizeMode = AutoSizeMode.GrowAndShrink
        root.Dock = DockStyle.Fill : root.Padding = New Padding(Theme.GapSection)
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        root.Controls.Add(title) : root.Controls.Add(subtitle) : root.Controls.Add(head) : root.Controls.Add(state)
        root.Controls.Add(viewport) : root.Controls.Add(buttons)

        AutoScaleMode = AutoScaleMode.None
        ClientSize = New Size(Theme.Px(520), Theme.Px(600))
        Controls.Add(root)
        FormBorderStyle = FormBorderStyle.FixedDialog : MaximizeBox = False : MinimizeBox = False
        Name = "frmWifiPicker" : ShowInTaskbar = False : StartPosition = FormStartPosition.CenterParent
        Text = "Домовой NEXT" : AcceptButton = btnSave : CancelButton = btnCancel
        Theme.StyleForm(Me)
        Theme.DialogLayout(Me, root, buttons)
        ResumeLayout(False)
    End Sub

    ' Поля под выбранной строкой: имя — только для «Другой сети», пароль — всегда (пустой — открытая сеть).
    Private Sub BuildEditor()
        editor.ColumnCount = 1 : editor.AutoSize = True : editor.AutoSizeMode = AutoSizeMode.GrowAndShrink
        editor.Anchor = AnchorStyles.Left Or AnchorStyles.Right : editor.BackColor = Theme.SurfaceHover
        editor.Padding = New Padding(Theme.Px(52), Theme.GapRelated, Theme.GapBlock, Theme.GapBlock) : editor.Margin = New Padding(0)
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        namePanel = EditorField("Название сети (SSID)", nameField)
        editor.Controls.Add(namePanel)
        editor.Controls.Add(EditorField("Пароль сети", keyField))
        Dim note As New Label With {.Text = "Пустой пароль — открытая сеть. Мост перезапустится и подключится к выбранной сети.",
            .AutoSize = True, .MaximumSize = New Size(Theme.Px(380), 0), .Margin = New Padding(0)}
        Theme.Note(note, False) : note.BackColor = Color.Transparent
        editor.Controls.Add(note)
        AddHandler nameBox.TextChanged, Sub() UpdateSave()
    End Sub

    Private Function EditorField(title As String, field As Control) As Control
        Dim block As New TableLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .ColumnCount = 1,
            .Margin = New Padding(0, 0, 0, Theme.GapRelated), .BackColor = Color.Transparent}
        block.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        Dim label As New Label With {.Text = title, .AutoSize = True, .Margin = New Padding(0, 0, 0, Theme.GapLabel)}
        Theme.Caption(label) : label.BackColor = Color.Transparent
        field.Margin = New Padding(0)
        block.Controls.Add(label) : block.Controls.Add(field)
        Return block
    End Function

    ' Поиск: плата спрашивает мост, окно опрашивает её, список приходит страницами.
    Private Async Sub StartScan()
        If searching Then Return
        searching = True : btnRefresh.Enabled = False
        ShowNetworks(Nothing)
        Theme.DotText(state, Theme.eDot.Idle, "Поиск сетей…")
        Try
            Dim result = If(opros Is Nothing, New WifiScanResult, Await Task.Run(Function() opros.WifiScan()))
            If IsDisposed Then Return
            If result.Status = eWifiScan.Ready Then
                ShowNetworks(WifiNetworks.Order(result.Networks, current, currentLevel))
                Dim count = rows.Count - 1 ' без строки «Другая сеть…»
                Theme.DotText(state, If(count > 0, Theme.eDot.Good, Theme.eDot.Idle),
                    If(count > 0, "Найдено сетей: " & count & ". Выберите сеть и введите пароль.", "Сетей не найдено — проверьте антенну моста и повторите."))
            Else
                ShowNetworks(WifiNetworks.Order(Enumerable.Empty(Of WifiNetwork), current, currentLevel))
                Theme.DotText(state, Theme.eDot.Bad, WifiNetworks.StatusText(result.Status))
            End If
        Catch ex As Exception
            AgentLog.Failure("wifi-scan", ex)
            If Not IsDisposed Then Theme.DotText(state, Theme.eDot.Bad, "Ошибка обмена: " & ex.Message)
        Finally
            searching = False
            If Not IsDisposed Then btnRefresh.Enabled = True
        End Try
    End Sub

    ' Перестроить список: Nothing — пусто (идёт поиск). В конце всегда «Другая сеть…» для скрытого имени.
    Public Sub ShowNetworks(networks As List(Of WifiNetwork))
        shownNetworks = networks
        list.SuspendLayout()
        list.Controls.Clear()
        rows.Clear() : selected = Nothing
        If networks IsNot Nothing Then
            For Each n In networks
                AddRow(New WifiRow(n, False))
            Next
            AddRow(New WifiRow(New WifiNetwork With {.Name = "Другая сеть…", .Level = -1}, True))
        End If
        list.ResumeLayout(True)
        UpdateSave()
    End Sub

    Private Sub AddRow(row As WifiRow)
        row.Width = Math.Max(1, viewport.ClientSize.Width)
        row.Anchor = AnchorStyles.Left Or AnchorStyles.Right
        AddHandler row.Click, Sub() SelectRow(row)
        rows.Add(row)
        list.Controls.Add(row)
    End Sub

    Public Sub SelectRow(row As WifiRow)
        If selected Is row Then Return
        list.SuspendLayout()
        If editor.Parent Is list Then list.Controls.Remove(editor)
        For Each r In rows
            r.Selected = r Is row
        Next
        selected = row
        namePanel.Visible = row.Hidden
        nameBox.Text = "" : keyBox.Text = ""
        Dim at = list.Controls.IndexOf(row)
        list.Controls.Add(editor)
        list.Controls.SetChildIndex(editor, at + 1)
        editor.Width = row.Width
        list.ResumeLayout(True)
        viewport.ScrollControlIntoView(editor)
        UpdateSave()
        If row.Hidden Then nameBox.Focus() Else keyBox.Focus()
    End Sub

    Private Sub UpdateSave()
        btnSave.Enabled = selected IsNot Nothing AndAlso (Not selected.Hidden OrElse nameBox.Text.Trim() <> "")
    End Sub

    Public ReadOnly Property SelectedName As String
        Get
            If selected Is Nothing Then Return ""
            Return If(selected.Hidden, nameBox.Text.Trim(), selected.Network.Name)
        End Get
    End Property

    Public ReadOnly Property SelectedHidden As Boolean
        Get
            Return selected IsNot Nothing AndAlso selected.Hidden
        End Get
    End Property

    Public ReadOnly Property RowCount As Integer
        Get
            Return rows.Count
        End Get
    End Property

    Public ReadOnly Property Row(index As Integer) As WifiRow
        Get
            Return rows(index)
        End Get
    End Property

    Public ReadOnly Property EditorVisible As Boolean
        Get
            Return editor.Parent Is list
        End Get
    End Property

    ' Проверка как у прошивки (113): имя 1–32 байта, пароль до 63, без нулей.
    Public Function Validate() As String
        Dim ssid = Encoding.UTF8.GetBytes(SelectedName), key = Encoding.UTF8.GetBytes(keyBox.Text)
        If ssid.Length = 0 Then Return "Выберите сеть"
        If ssid.Length > 32 Then Return "Название сети длиннее 32 байт"
        If key.Length > 63 Then Return "Пароль длиннее 63 байт"
        If ssid.Contains(CByte(0)) OrElse key.Contains(CByte(0)) Then Return "Недопустимые символы"
        Return ""
    End Function

    Private Sub Save()
        Dim problem = Validate()
        If problem <> "" Then
            Theme.DotText(state, Theme.eDot.Bad, problem)
            Return
        End If
        Ssid = SelectedName : Key = keyBox.Text
        DialogResult = DialogResult.OK
        Close()
    End Sub
End Class

' Строка сети: веер уровня, имя, вторая строка «Подключено» или оценка сигнала; выбранная — с полосой слева.
Public Class WifiRow
    Inherits Panel

    Private _network As WifiNetwork
    Public ReadOnly Hidden As Boolean
    Private _selected As Boolean

    Public Sub New(n As WifiNetwork, hidden As Boolean)
        _network = n : Me.Hidden = hidden
        SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Height = Theme.Px(56) : Margin = New Padding(0) : Cursor = Cursors.Hand
        Name = "wifiRow" : Text = n.Name : AccessibleName = n.Name ' Text не рисуется: по нему строку находит канал --agent
        AddHandler MouseEnter, Sub() Invalidate()
        AddHandler MouseLeave, Sub() Invalidate()
    End Sub

    Public Property Network As WifiNetwork
        Get
            Return _network
        End Get
        Set(value As WifiNetwork)
            _network = value : Text = value.Name : AccessibleName = value.Name : Invalidate()
        End Set
    End Property

    Public Property Selected As Boolean
        Get
            Return _selected
        End Get
        Set(value As Boolean)
            _selected = value : Invalidate()
        End Set
    End Property

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        Dim hover = ClientRectangle.Contains(PointToClient(MousePosition))
        g.Clear(If(_selected, Theme.SurfaceHover, If(hover, Theme.WindowBack, Theme.Surface)))
        g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
        If _selected Then
            Using brush As New SolidBrush(Theme.Accent)
                g.FillRectangle(brush, 0, Theme.Px(12), Theme.Px(3), Height - Theme.Px(24))
            End Using
        End If
        Dim icon = New Rectangle(Theme.Px(16), (Height - Theme.Px(22)) \ 2, Theme.Px(26), Theme.Px(22))
        If Hidden Then
            Using pen As New Pen(Theme.TextMuted, Math.Max(1.5F, Theme.Px(2)))
                Dim cx = icon.Left + icon.Width \ 2, cy = icon.Top + icon.Height \ 2
                g.DrawLine(pen, cx - Theme.Px(7), cy, cx + Theme.Px(7), cy)
                g.DrawLine(pen, cx, cy - Theme.Px(7), cx, cy + Theme.Px(7))
            End Using
        Else
            LinkDrawing.WifiFan(g, icon, Network.Level, Network.Level < 0)
        End If
        Dim textLeft = Theme.Px(52), textWidth = Math.Max(10, Width - textLeft - Theme.Px(16))
        Dim detail = If(Hidden, "Ввести название скрытой сети", WifiNetworks.Detail(Network))
        TextRenderer.DrawText(g, Network.Name, If(Network.Connected, Theme.FontBodyBold, Theme.FontBody), New Rectangle(textLeft, Theme.Px(9), textWidth, Theme.Px(20)),
            Theme.TextMain, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix)
        TextRenderer.DrawText(g, detail, Theme.FontCaption, New Rectangle(textLeft, Theme.Px(30), textWidth, Theme.Px(18)),
            If(Network.Connected, Theme.OkText, Theme.TextMuted), TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix)
        Using pen As New Pen(Theme.Border)
            g.DrawLine(pen, textLeft, Height - 1, Width, Height - 1)
        End Using
    End Sub
End Class
