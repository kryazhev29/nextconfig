Imports System.Drawing
Imports System.Windows.Forms

' Единое оформление всей программы: цвета, шрифты, отступы и виды кнопок.
' Формы своих цветов и размеров не держат - только зовут отсюда.
Public Module Theme

    ' ---- цвета ----
    Public ReadOnly Accent As Color = Color.FromArgb(&H25, &H63, &HEB)       ' основной синий
    Public ReadOnly AccentHover As Color = Color.FromArgb(&H16, &H78, &HD2)  ' синий при наведении
    Public ReadOnly AccentDown As Color = Color.FromArgb(&H12, &H62, &HAD)   ' синий при нажатии
    Public ReadOnly Ok As Color = Color.FromArgb(&H22, &HA0, &H60)           ' зелёный индикатора
    Public ReadOnly OkText As Color = Color.FromArgb(&H23, &H7A, &H32)       ' зелёный текста
    Public ReadOnly Danger As Color = Color.FromArgb(&HDC, &H35, &H45)       ' красный
    Public ReadOnly DangerHover As Color = Color.FromArgb(&HBD, &H2D, &H3B)
    Public ReadOnly Warning As Color = Color.FromArgb(&H8A, &H61, &H00)      ' тёмный янтарный
    Public ReadOnly WindowBack As Color = Color.FromArgb(&HF4, &HF7, &HFB)   ' фон окна
    Public ReadOnly Surface As Color = Color.White                           ' поверхность блоков
    Public ReadOnly SurfaceHover As Color = Color.FromArgb(&HEE, &HF3, &HFA)
    Public ReadOnly TextMain As Color = Color.FromArgb(&H20, &H2B, &H3A)     ' основной текст
    Public ReadOnly TextMuted As Color = Color.FromArgb(&H52, &H60, &H73)    ' второстепенный текст
    Public ReadOnly Border As Color = Color.FromArgb(&HDC, &HE4, &HEE)       ' границы
    Public ReadOnly Idle As Color = Color.FromArgb(&H9A, &HA6, &HB6)         ' серая точка
    Public ReadOnly Disabled As Color = Color.FromArgb(&HEB, &HEF, &HF5)     ' фон недоступного

    ' ---- шрифты ----
    Public Const Family As String = "Segoe UI"
    Public Const FamilyMono As String = "Consolas"

    Public ReadOnly FontProduct As Font = MakeFont(Family, 18.0F, FontStyle.Regular)      ' название программы
    Public ReadOnly FontTitle As Font = MakeFont(Family, 15.0F, FontStyle.Regular)        ' заголовок раздела, диалога
    Public ReadOnly FontGroup As Font = MakeFont(Family, 11.0F, FontStyle.Bold)           ' заголовок группы
    Public ReadOnly FontBody As Font = MakeFont(Family, 10.0F, FontStyle.Regular)         ' основной текст и поля
    Public ReadOnly FontBodyBold As Font = MakeFont(Family, 10.0F, FontStyle.Bold)        ' важное в основном тексте
    Public ReadOnly FontCaption As Font = MakeFont(Family, 9.0F, FontStyle.Regular)       ' подписи и пояснения
    Public ReadOnly FontValueBig As Font = MakeFont(Family, 16.0F, FontStyle.Bold)        ' крупное значение состояния
    Public ReadOnly FontDot As Font = MakeFont(Family, 12.0F, FontStyle.Regular)          ' точка индикатора
    Public ReadOnly FontMono As Font = MakeFont(FamilyMono, 9.5F, FontStyle.Regular)      ' журнал

    ' ---- масштаб Windows ----
    ' Размеры в оформлении и в формах записаны для обычного масштаба (100 %),
    ' а на экране считаются через Px. Автомасштаб форм выключен: два механизма
    ' сразу давали двойной пересчёт и окно не по размеру.
    Private ReadOnly uiScale As Single = ReadScale()

    Private Function DemoScale() As Single
        If Demo.IsOn AndAlso Environment.GetCommandLineArgs().Contains("--demo-dpi=120") Then Return 1.25F
        If Demo.IsOn AndAlso Environment.GetCommandLineArgs().Contains("--demo-dpi=96") Then Return 1.0F
        Return 0
    End Function

    Private Function MakeFont(family As String, size As Single, style As FontStyle) As Font
        If DemoScale() > 0 Then
            Using g = Graphics.FromHwnd(IntPtr.Zero)
                size *= DemoScale() * 96.0F / g.DpiX
            End Using
        End If
        Return New Font(family, size, style)
    End Function

    Private Function ReadScale() As Single
        If DemoScale() > 0 Then Return DemoScale()
        Try
            Using g As Graphics = Graphics.FromHwnd(IntPtr.Zero)
                Return g.DpiX / 96.0F
            End Using
        Catch ex As Exception
            Return 1.0F
        End Try
    End Function

    Public Function Px(ByVal value As Integer) As Integer
        Return CInt(Math.Round(value * uiScale))
    End Function

    ' ---- отступы (записаны для масштаба 100 %) ----
    Public ReadOnly GapLabel As Integer = Px(6)      ' подпись - поле
    Public ReadOnly GapRelated As Integer = Px(8)    ' связанные элементы
    Public ReadOnly GapRow As Integer = Px(14)       ' строки формы
    Public ReadOnly GapBlock As Integer = Px(18)     ' внутри смыслового блока
    Public ReadOnly GapSection As Integer = Px(22)   ' между блоками

    Public ReadOnly ButtonHeight As Integer = Px(40)
    Public ReadOnly ButtonHeightSmall As Integer = Px(36)
    Public ReadOnly ButtonWidth As Integer = Px(168)
    Public ReadOnly ButtonWidthSmall As Integer = Px(120)
    Public ReadOnly FieldHeight As Integer = Px(32)

    ' ---- состояния индикатора ----
    Public Enum eDot
        Good = 0     ' работает, подключено
        Idle = 1     ' неактивно
        Bad = 2      ' ошибка
        Waiting = 3  ' ожидание данных
    End Enum

    ' Логотип заказчика в исходниках вертикальный: надпись читается снизу вверх,
    ' он стоял полосой вдоль левого края окна. Для шапки разворачиваем в строку.
    Public Function LogoHorizontal(ByVal src As Image) As Image
        If src Is Nothing Then Return Nothing
        Dim b As New Bitmap(src)
        b.RotateFlip(RotateFlipType.Rotate90FlipNone)
        Return b
    End Function

    ' ---- форма ----

    Public Sub StyleForm(ByVal f As Form)
        f.BackColor = WindowBack
        f.ForeColor = TextMain
        f.Font = FontBody
        Using stream = GetType(Form1).Assembly.GetManifestResourceStream("NextConfig.naviset.ico")
            f.Icon = New Icon(stream)
        End Using
    End Sub

    Public Sub FitBoardWindow(form As Form)
        Dim area = Screen.FromControl(form).WorkingArea.Size
        If Demo.IsOn AndAlso Environment.GetCommandLineArgs().Contains("--demo-laptop") Then
            area = New Size(Math.Min(area.Width, 1366), Math.Min(area.Height, 728))
        End If
        Dim width = Math.Min(form.Width, area.Width)
        Dim height = Math.Min(form.Height, area.Height)
        form.MinimumSize = New Size(Math.Min(form.MinimumSize.Width, width), Math.Min(form.MinimumSize.Height, height))
        form.Size = New Size(width, height)
    End Sub

    Public Function BoardHeader(title As String, detail As String, Optional actions As Control = Nothing) As Control
        Dim header As New TableLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .ColumnCount = 3,
            .BackColor = Surface, .Padding = New Padding(GapSection, GapBlock, GapSection, GapBlock), .Margin = New Padding(0)}
        header.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, Px(112)))
        header.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        header.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, If(actions Is Nothing, 0, actions.Width)))
        Dim resource As New System.ComponentModel.ComponentResourceManager(GetType(Form1))
        Dim logo As New PictureBox With {.Image = LogoHorizontal(DirectCast(resource.GetObject("PictureBox1.Image"), Image)),
            .Size = New Size(Px(96), Px(30)), .SizeMode = PictureBoxSizeMode.Zoom, .Anchor = AnchorStyles.Left}
        AddHandler logo.Disposed, Sub() logo.Image.Dispose()
        header.Controls.Add(logo, 0, 0)
        Dim identity As New TableLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Fill, .ColumnCount = 1}
        identity.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        identity.Controls.Add(New Label With {.Text = title, .AutoSize = True, .Font = FontBodyBold, .Dock = DockStyle.Fill})
        identity.Controls.Add(New Label With {.Text = detail, .AutoSize = True, .Font = FontCaption, .ForeColor = TextMuted, .Dock = DockStyle.Fill})
        header.Controls.Add(identity, 1, 0)
        If actions IsNot Nothing Then
            actions.Anchor = AnchorStyles.Right
            header.Controls.Add(actions, 2, 0)
        End If
        AddHandler header.Paint, Sub(sender, e)
            Using pen As New Pen(Border)
                e.Graphics.DrawLine(pen, GapSection, header.Height - 1, header.Width - GapSection, header.Height - 1)
            End Using
        End Sub
        Return header
    End Function

    Public Sub ReadOnlyGrid(grid As DataGridView)
        grid.Dock = DockStyle.Fill
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.AllowUserToResizeRows = False
        grid.RowHeadersVisible = False
        grid.BackgroundColor = Surface
        grid.GridColor = Border
        grid.BorderStyle = BorderStyle.None
        grid.Font = FontBody
        grid.DefaultCellStyle.BackColor = Surface
        grid.DefaultCellStyle.ForeColor = TextMain
        grid.DefaultCellStyle.SelectionBackColor = SurfaceHover
        grid.DefaultCellStyle.SelectionForeColor = TextMain
        grid.ColumnHeadersDefaultCellStyle.BackColor = WindowBack
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextMain
        grid.ColumnHeadersDefaultCellStyle.Font = FontCaption
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = WindowBack
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextMain
        grid.EnableHeadersVisualStyles = False
        grid.RowTemplate.Height = Px(32)
        grid.RowTemplate.MinimumHeight = Px(32)
        grid.ColumnHeadersHeight = Px(34)
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.MultiSelect = False
    End Sub

    ' Блок с белой поверхностью и тонкой границей.
    Public Sub ReflowOverview(overview As TableLayoutPanel)
        Dim grid = DirectCast(overview.Controls(1), TableLayoutPanel)
        Dim cards = grid.Controls.Cast(Of Control)().ToArray()
        Dim narrow = overview.ClientSize.Width < Theme.Px(940)
        Dim wanted = If(narrow, 1, 2)
        If grid.ColumnCount = wanted AndAlso CInt(If(grid.Tag, -1)) = cards.Length Then Return
        grid.SuspendLayout()
        grid.Controls.Clear() : grid.ColumnStyles.Clear() : grid.RowStyles.Clear()
        grid.ColumnCount = wanted : grid.RowCount = If(narrow, cards.Length, cards.Length - 1)
        For i = 1 To wanted
            grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F / wanted))
        Next
        For i = 0 To grid.RowCount - 1
            grid.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Next
        For i = 0 To cards.Length - 1
            Dim column = If(narrow OrElse i >= 2, 0, i)
            Dim row = If(narrow, i, If(i < 2, 0, i - 1))
            grid.Controls.Add(cards(i), column, row)
            grid.SetColumnSpan(cards(i), If(Not narrow AndAlso i >= 2, 2, 1))
            cards(i).Dock = DockStyle.Fill
            cards(i).Margin = New Padding(If(Not narrow AndAlso i = 1, Theme.GapRelated, 0), 0,
                If(Not narrow AndAlso i = 0, Theme.GapRelated, 0), Theme.GapRelated)
        Next
        grid.Tag = cards.Length : grid.ResumeLayout(True)
    End Sub

    Public Function Card() As Panel
        Dim p As New Panel()
        p.BackColor = Surface
        p.BorderStyle = BorderStyle.FixedSingle
        p.Padding = New Padding(GapBlock)
        Return p
    End Function

    ' ---- кнопки ----

    ' Главное действие экрана или диалога. Такая кнопка обычно одна.
    Public Sub Primary(ByVal b As Button)
        Base(b)
        b.BackColor = Accent
        b.ForeColor = Color.White
        b.FlatAppearance.BorderSize = 0
        b.FlatAppearance.MouseOverBackColor = AccentHover
        b.FlatAppearance.MouseDownBackColor = AccentDown
        WatchEnabled(b)
    End Sub

    ' Второстепенное действие: отмена, закрыть, выбрать файл, экспорт.
    Public Sub Secondary(ByVal b As Button)
        Base(b)
        b.BackColor = Surface
        b.ForeColor = TextMain
        b.FlatAppearance.BorderSize = 1
        b.FlatAppearance.BorderColor = Border
        b.FlatAppearance.MouseOverBackColor = SurfaceHover
        b.FlatAppearance.MouseDownBackColor = Disabled
        WatchEnabled(b)
    End Sub

    ' Опасное действие: красный текст и граница. Заливка - только для последнего подтверждения.
    Public Sub DangerOutline(ByVal b As Button)
        Base(b)
        b.BackColor = Surface
        b.ForeColor = Danger
        b.FlatAppearance.BorderSize = 1
        b.FlatAppearance.BorderColor = Danger
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(&HFD, &HEC, &HEE)
        b.FlatAppearance.MouseDownBackColor = Color.FromArgb(&HF8, &HD7, &HDA)
        WatchEnabled(b)
    End Sub

    ' Последнее подтверждение необратимой операции.
    Public Sub DangerSolid(ByVal b As Button)
        Base(b)
        b.BackColor = Danger
        b.ForeColor = Color.White
        b.FlatAppearance.BorderSize = 0
        b.FlatAppearance.MouseOverBackColor = DangerHover
        b.FlatAppearance.MouseDownBackColor = Color.FromArgb(&HA0, &H26, &H32)
        WatchEnabled(b)
    End Sub

    ' Второстепенный размер: одна высота для всех кнопок внутри блоков.
    Public Sub Small(ByVal b As Button)
        b.Height = ButtonHeightSmall
        b.Font = FontCaption
        If b.Width < ButtonWidthSmall Then b.Width = ButtonWidthSmall
        FitWidth(b)
    End Sub

    Private Sub Base(ByVal b As Button)
        b.FlatStyle = FlatStyle.Flat
        b.Font = FontBody
        b.Height = ButtonHeight
        b.Padding = New Padding(GapRelated, 0, GapRelated, 0)
        b.UseVisualStyleBackColor = False
        b.Cursor = Cursors.Hand
        b.TextAlign = ContentAlignment.MiddleCenter
        b.UseCompatibleTextRendering = False

        If minWidths.Add(b) Then AddHandler b.TextChanged, AddressOf OnButtonTextChanged

        FitWidth(b)
    End Sub

    ' Надпись не должна обрезаться: кнопка не уже своего текста. Ширину,
    ' заданную в форме, считаем наименьшей - кнопки в ряду остаются ровными.
    Private ReadOnly minWidths As New HashSet(Of Button)

    Private Sub OnButtonTextChanged(ByVal sender As Object, ByVal e As EventArgs)
        FitWidth(DirectCast(sender, Button))
    End Sub

    Private Sub FitWidth(ByVal b As Button)
        If b.Text = "" Then Exit Sub

        Dim need As Integer = TextRenderer.MeasureText(b.Text, b.Font).Width + Px(28)

        If b.Width < need Then b.Width = need
    End Sub

    ' У плоской кнопки с заданным фоном Windows сама его не гасит: недоступная
    ' кнопка выглядит как рабочая. Запоминаем рабочие цвета и подменяем их.
    Private ReadOnly normalColors As New Dictionary(Of Button, Color())
    Private ReadOnly watched As New HashSet(Of Button)

    Private Sub WatchEnabled(ByVal b As Button)
        normalColors(b) = New Color() {b.BackColor, b.ForeColor, b.FlatAppearance.BorderColor}

        If watched.Add(b) Then
            AddHandler b.EnabledChanged, AddressOf OnEnabledChanged
            AddHandler b.Disposed, AddressOf OnButtonDisposed
        End If

        ApplyEnabled(b)
    End Sub

    Private Sub OnButtonDisposed(ByVal sender As Object, ByVal e As EventArgs)
        Dim b As Button = DirectCast(sender, Button)
        normalColors.Remove(b)
        watched.Remove(b)
    End Sub

    Private Sub OnEnabledChanged(ByVal sender As Object, ByVal e As EventArgs)
        ApplyEnabled(DirectCast(sender, Button))
    End Sub

    Private Sub ApplyEnabled(ByVal b As Button)
        Dim c As Color() = Nothing
        If Not normalColors.TryGetValue(b, c) Then Exit Sub

        If b.Enabled Then
            b.BackColor = c(0)
            b.ForeColor = c(1)
            b.FlatAppearance.BorderColor = c(2)
            b.Cursor = Cursors.Hand
        Else
            b.BackColor = Disabled
            b.ForeColor = TextMuted
            b.FlatAppearance.BorderColor = Border
            b.Cursor = Cursors.Default
        End If
    End Sub

    ' ---- подписи ----

    Public Sub Product(ByVal l As Label)
        l.Font = FontProduct
        l.ForeColor = TextMain
        l.BackColor = Color.Transparent
    End Sub

    Public Sub Title(ByVal l As Label)
        l.Font = FontTitle
        l.ForeColor = TextMain
        l.BackColor = Color.Transparent
    End Sub

    Public Sub Group(ByVal l As Label)
        l.Font = FontGroup
        l.ForeColor = TextMain
        l.BackColor = Color.Transparent
    End Sub

    Public Sub Value(ByVal l As Label)
        l.Font = FontBody
        l.ForeColor = TextMain
        l.BackColor = Color.Transparent
    End Sub

    Public Sub Caption(ByVal l As Label)
        l.Font = FontCaption
        l.ForeColor = TextMuted
        l.BackColor = Color.Transparent
    End Sub

    Public Sub BigValue(ByVal l As Label)
        l.Font = FontValueBig
        l.ForeColor = TextMain
        l.BackColor = Color.Transparent
    End Sub

    ' ---- индикатор состояния: точка плюс текст ----

    Public Function DotColor(ByVal state As eDot) As Color
        Select Case state
            Case eDot.Good : Return Ok
            Case eDot.Bad : Return Danger
            Case eDot.Waiting : Return Accent
            Case Else : Return Idle
        End Select
    End Function

    ' Точка. Текст состояния подписывается отдельной надписью рядом.
    ' Статус приходит раз в секунду, поэтому меняем только то, что изменилось:
    ' лишнее присваивание перерисовывает надпись и вся карточка мигает.
    Public Sub Dot(ByVal l As Label, ByVal state As eDot)
        If l.Text <> "●" Then
            l.Font = FontDot
            l.Text = "●"
            l.AutoSize = True
            l.BackColor = Color.Transparent
        End If

        SetColor(l, DotColor(state))
    End Sub

    ' Текст состояния рядом с точкой. Цветом выделяем только успех и ошибку.
    Public Sub DotText(ByVal l As Label, ByVal state As eDot, ByVal text As String)
        If l.Font IsNot FontBody Then
            l.Font = FontBody
            l.BackColor = Color.Transparent
        End If

        SetText(l, text)

        Select Case state
            Case eDot.Good : SetColor(l, OkText)
            Case eDot.Bad : SetColor(l, Danger)
            Case Else : SetColor(l, TextMain)
        End Select
    End Sub

    ' Присвоение только при изменении: иначе каждая надпись перерисовывается
    ' на каждый пакет статуса, а надписи с автоподбором ширины ещё и двигают соседей.
    Public Sub SetText(ByVal l As Label, ByVal text As String)
        If l.Text <> text Then l.Text = text
    End Sub

    Public Sub SetColor(ByVal l As Label, ByVal c As Color)
        If l.ForeColor <> c Then l.ForeColor = c
    End Sub

    ' Двойная буферизация у контейнеров: штатно её нет, и при перерисовке
    ' карточки видно моргание.
    Public Sub EnableDoubleBuffer(ByVal c As Control)
        Dim prop As Reflection.PropertyInfo = GetType(Control).GetProperty(
            "DoubleBuffered", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic)

        If prop IsNot Nothing Then prop.SetValue(c, True, Nothing)

        For Each child As Control In c.Controls
            EnableDoubleBuffer(child)
        Next
    End Sub

    ' ---- поля ввода ----
    ' Поле кладётся в панель толщиной в одну точку: она и рисует границу,
    ' синюю при фокусе. У самого поля границы нет.

    Public Function FieldBox(ByVal t As TextBox, ByVal width As Integer, Optional height As Integer = 32) As Panel
        Dim p As New Panel()
        p.BackColor = Border
        p.Padding = New Padding(1)
        p.Size = New Size(Px(width), Px(height))

        t.BorderStyle = BorderStyle.None
        t.Font = FontBody
        t.ForeColor = TextMain
        t.BackColor = Surface
        t.Dock = DockStyle.Fill
        ' поле ниже панели: так текст стоит по середине, а не под верхней границей
        t.Margin = New Padding(0)

        Dim inner As New Panel()
        inner.BackColor = Surface
        inner.Dock = DockStyle.Fill
        inner.Padding = New Padding(GapLabel, Math.Max(0, (Px(height) - 2 - t.PreferredHeight) \ 2), GapLabel, 0)
        inner.Controls.Add(t)

        p.Controls.Add(inner)

        AddHandler t.Enter, Sub() p.BackColor = Accent
        AddHandler t.Leave, Sub() p.BackColor = Border
        AddHandler t.EnabledChanged, Sub()
                                         inner.BackColor = If(t.Enabled, Surface, Disabled)
                                         t.BackColor = inner.BackColor
                                     End Sub

        Return p
    End Function

    ' Выпадающий список: выбор из нескольких значений вместо отдельного окна.
    ' Длинный список изменений прокручивается, кнопки остаются доступны.
    Public Sub ConfigChanges(f As frmInfo, text As String)
        Dim area As New TextBox With {.Multiline = True, .ReadOnly = True, .ScrollBars = ScrollBars.Vertical,
            .BorderStyle = BorderStyle.None, .BackColor = Surface, .ForeColor = TextMain, .Font = FontBody,
            .Width = Px(660), .Text = text, .Margin = New Padding(0, 0, 0, GapSection)}
        area.Height = Math.Min(Px(360), TextRenderer.MeasureText(text, FontBody,
            New Size(Px(620), Integer.MaxValue), TextFormatFlags.WordBreak).Height + Px(24))
        Dim contentRow = f.root.GetRow(f.info)
        f.root.Controls.Remove(f.info)
        f.info.Dispose()
        f.root.Controls.Add(area, 0, contentRow)
    End Sub

    ' Доработка: Кряжев А., 2026. Общие стили панели наладки.
    ' Доработка: Кряжев А., 2026. Разделы сохраняют свои поля при переключении.
    Public Function SettingsWorkspace(pages As Control(), save As Button, revert As Button, state As Label, Optional defaults As Button = Nothing) As Control
        Dim shell As New TableLayoutPanel With {.Dock = DockStyle.Fill, .Margin = New Padding(0), .ColumnCount = 1, .RowCount = 2, .BackColor = Surface}
        shell.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        shell.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        shell.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Dim body As New TableLayoutPanel With {.Dock = DockStyle.Fill, .Margin = New Padding(0), .ColumnCount = 1, .RowCount = 2}
        body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        body.RowStyles.Add(New RowStyle(SizeType.Absolute, Px(64)))
        body.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        Dim nav As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False,
            .Margin = New Padding(0), .Padding = New Padding(Px(12), Px(12), Px(12), 0), .BackColor = WindowBack}
        Dim hosts As New List(Of Panel)
        Dim links As New List(Of Button)
        Dim content As New Panel With {.Dock = DockStyle.Fill, .Margin = New Padding(0)}
        Dim titles = New String() {"Связь", "485 и Wiegand", "Доступ", "Сервис"}
        For i = 0 To pages.Length - 1
            Dim index = i
            Dim host As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True, .Visible = False}
            host.Controls.Add(pages(i))
            hosts.Add(host)
            content.Controls.Add(host)
            Dim link As New Button With {.Text = titles(i), .Name = "settingsSection" & i, .Size = New Size(Px(150), Px(42)),
                .FlatStyle = FlatStyle.Flat, .TextAlign = ContentAlignment.MiddleLeft, .Padding = New Padding(Px(10), 0, 0, 0),
                .Font = FontBody, .Margin = New Padding(0, 0, 0, Px(4)), .Cursor = Cursors.Hand, .UseVisualStyleBackColor = False}
            link.FlatAppearance.BorderSize = 0
            links.Add(link)
            nav.Controls.Add(link)
            AddHandler link.Click, Sub(sender, e)
                For j = 0 To hosts.Count - 1
                    hosts(j).Visible = j = index
                    links(j).BackColor = If(j = index, Color.FromArgb(222, 235, 252), WindowBack)
                    links(j).ForeColor = If(j = index, Accent, TextMain)
                    links(j).Font = If(j = index, FontBodyBold, FontBody)
                Next
                hosts(index).BringToFront()
            End Sub
        Next
        body.Controls.Add(nav, 0, 0)
        body.Controls.Add(content, 0, 1)
        Dim footer As New TableLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .ColumnCount = 2, .Margin = New Padding(0), .Padding = New Padding(GapSection, GapRelated, GapSection, GapRelated)}
        footer.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        footer.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        AddHandler footer.Paint, Sub(sender, e)
            Using pen As New Pen(Border)
                e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0)
            End Using
        End Sub
        Dim summary As New Label With {.AutoSize = True, .Anchor = AnchorStyles.Left, .Font = FontCaption, .MaximumSize = New Size(Px(350), 0), .Text = "Изменений нет", .ForeColor = TextMuted}
        AddHandler state.TextChanged, Sub(sender, e)
            summary.Text = If(state.Text = "", "Изменений нет", state.Text)
        End Sub
        AddHandler state.ForeColorChanged, Sub(sender, e)
            summary.ForeColor = state.ForeColor
        End Sub
        Dim actions As New FlowLayoutPanel With {.AutoSize = True, .WrapContents = False, .Margin = New Padding(0)}
        revert.Text = "Отменить"
        revert.Width = Px(120)
        save.Width = Px(210)
        save.Margin = New Padding(0)
        If defaults IsNot Nothing Then
            defaults.Text = "По умолчанию" : defaults.Width = Px(150)
            defaults.Margin = New Padding(0, 0, GapRelated, 0)
            actions.Controls.Add(defaults)
        End If
        actions.Controls.Add(revert)
        actions.Controls.Add(save)
        footer.Controls.Add(summary, 0, 0)
        footer.Controls.Add(actions, 1, 0)
        shell.Controls.Add(body, 0, 0)
        shell.Controls.Add(footer, 0, 1)
        ' Начальный раздел задаём до показа формы.
        hosts(1).Visible = True
        hosts(1).BringToFront()
        links(1).BackColor = Color.FromArgb(222, 235, 252)
        links(1).ForeColor = Accent
        links(1).Font = FontBodyBold
        Return shell
    End Function

    Public Function SettingsSection(title As String, description As String, ParamArray controls As Control()) As Control
        Dim section As New TableLayoutPanel With {.AutoSize = True, .ColumnCount = 1, .Anchor = AnchorStyles.Left Or AnchorStyles.Right, .Margin = New Padding(0), .BackColor = Surface}
        section.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        section.Controls.Add(New Label With {.Text = title, .AutoSize = True, .Font = FontTitle, .ForeColor = TextMain, .Margin = New Padding(0, 0, 0, GapLabel)})
        If description <> "" Then
        section.Controls.Add(New Label With {.Text = description, .AutoSize = True, .Font = FontCaption, .ForeColor = TextMuted, .Margin = New Padding(0, 0, 0, GapBlock)})
        End If
        For Each control In controls
            section.Controls.Add(control)
        Next
        Return section
    End Function

    ' Доработка: Кряжев А., 2026. Общая композиция всех экранов и диалогов.
    Public Sub PasswordField(box As Panel, field As TextBox)
        field.UseSystemPasswordChar = True
        Dim eye As New Button With {.Name = "showPin", .Dock = DockStyle.Right, .Width = Px(34), .FlatStyle = FlatStyle.Flat,
            .BackColor = Surface, .Cursor = Cursors.Hand, .AccessibleName = "Показать ПИН-код", .TabStop = True}
        eye.FlatAppearance.BorderSize = 0
        Dim hint As New ToolTip()
        hint.SetToolTip(eye, "Показать ПИН-код")
        AddHandler eye.Click, Sub(sender, e)
            field.UseSystemPasswordChar = Not field.UseSystemPasswordChar
            eye.AccessibleName = If(field.UseSystemPasswordChar, "Показать ПИН-код", "Скрыть ПИН-код")
            hint.SetToolTip(eye, eye.AccessibleName)
            eye.Invalidate()
        End Sub
        AddHandler eye.Paint, Sub(sender, e)
            e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
            Dim x = (eye.Width - Px(18)) \ 2, y = (eye.Height - Px(10)) \ 2
            Using pen As New Pen(TextMuted, Math.Max(1, Px(1)))
                e.Graphics.DrawEllipse(pen, x, y, Px(18), Px(10))
                e.Graphics.DrawEllipse(pen, x + Px(7), y + Px(3), Px(4), Px(4))
                If Not field.UseSystemPasswordChar Then e.Graphics.DrawLine(pen, x, y + Px(12), x + Px(18), y - Px(2))
            End Using
        End Sub
        AddHandler eye.Disposed, Sub(sender, e) hint.Dispose()
        box.Controls.Add(eye)
        eye.SendToBack()
    End Sub

    Public Sub PeripheryCombo(c As ComboBox)
        ' Нативный DropDownList игнорирует ForeColor в закрытой части на некоторых темах Windows.
        c.DrawMode = DrawMode.OwnerDrawFixed
        AddHandler c.DrawItem, Sub(sender, e)
            e.DrawBackground()
            If e.Index >= 0 Then
                Dim selected = (e.State And DrawItemState.Selected) <> 0
                Dim color = If(Not c.Enabled, SystemColors.GrayText, If(selected, SystemColors.HighlightText, c.ForeColor))
                Dim bounds = e.Bounds
                bounds.Inflate(-Px(3), 0)
                TextRenderer.DrawText(e.Graphics, c.GetItemText(c.Items(e.Index)), c.Font, bounds, color,
                    TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPadding)
            End If
            e.DrawFocusRectangle()
        End Sub
    End Sub

    Public Sub BusRowAppearance(address As TextBox, kind As ComboBox, number As Label, configured As Boolean)
        Dim color = If(configured, TextMain, Idle)
        address.ForeColor = color
        kind.ForeColor = color
        number.ForeColor = color
    End Sub

    Public Function BrandLine() As Control
        Dim line As New TableLayoutPanel With {.AutoSize = True, .ColumnCount = 2, .Anchor = AnchorStyles.Left Or AnchorStyles.Right,
            .Margin = New Padding(0, 0, 0, Px(24)), .Padding = New Padding(0, 0, 0, Px(16))}
        line.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        line.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        Dim resource As New System.ComponentModel.ComponentResourceManager(GetType(Form1))
        Dim logo As New PictureBox With {.Image = LogoHorizontal(DirectCast(resource.GetObject("PictureBox1.Image"), Image)),
            .Size = New Size(Px(104), Px(30)), .SizeMode = PictureBoxSizeMode.Zoom, .Margin = New Padding(0)}
        AddHandler logo.Disposed, Sub(sender, e)
            If logo.Image IsNot Nothing Then logo.Image.Dispose()
        End Sub
        line.Controls.Add(logo, 0, 0)
        line.Controls.Add(New Label With {.Text = "ДОМОВОЙ NEXT", .Font = FontCaption, .ForeColor = TextMuted,
            .AutoSize = True, .Anchor = AnchorStyles.Right, .Margin = New Padding(Px(24), 0, 0, 0)}, 1, 0)
        AddHandler line.Paint, Sub(sender, e)
            Using pen As New Pen(Border)
                e.Graphics.DrawLine(pen, 0, line.Height - 1, line.Width, line.Height - 1)
            End Using
        End Sub
        Return line
    End Function

    Public Sub DialogLayout(form As Form, content As TableLayoutPanel, actions As FlowLayoutPanel)
        form.BackColor = Surface
        form.AutoSize = True
        form.AutoSizeMode = AutoSizeMode.GrowAndShrink
        content.BackColor = Surface
        content.Padding = New Padding(Px(28))
        Dim children = content.Controls.Cast(Of Control)().ToArray()
        content.Controls.Clear()
        content.RowCount = children.Length + 1
        content.Controls.Add(BrandLine(), 0, 0)
        For i = 0 To children.Length - 1
            content.Controls.Add(children(i), 0, i + 1)
        Next
        actions.Padding = New Padding(0, Px(18), 0, 0)
        actions.Anchor = AnchorStyles.Left Or AnchorStyles.Right
        AddHandler actions.Paint, Sub(sender, e)
            Using pen As New Pen(Border)
                e.Graphics.DrawLine(pen, 0, 0, actions.Width, 0)
            End Using
        End Sub
        ' Карточки остаются прежними; длинный текст прокручивается, кнопки видны всегда.
        content.Controls.Remove(actions)
        content.RowCount = Math.Max(1, content.RowCount - 1)
        content.Dock = DockStyle.Top
        actions.Dock = DockStyle.Fill : actions.Margin = New Padding(Px(28), 0, Px(28), Px(20))
        Dim shell As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2, .Margin = New Padding(0)}
        shell.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        shell.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        shell.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Dim viewport As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True, .Margin = New Padding(0)}
        form.Controls.Remove(content)
        viewport.Controls.Add(content)
        shell.Controls.Add(viewport, 0, 0) : shell.Controls.Add(actions, 0, 1)
        form.AutoSize = False : form.Controls.Add(shell)
        AddHandler form.Shown, Sub()
            Dim area = Screen.FromControl(form).WorkingArea.Size
            If Demo.IsOn AndAlso Environment.GetCommandLineArgs().Contains("--demo-laptop") Then
                area = New Size(Math.Min(area.Width, 1366), Math.Min(area.Height, 728))
            End If
            Dim chrome = form.Size - form.ClientSize
            Dim width = Math.Min(area.Width - chrome.Width, Math.Max(form.ClientSize.Width,
                Math.Max(content.PreferredSize.Width, actions.PreferredSize.Width + Px(56))))
            form.ClientSize = New Size(width, form.ClientSize.Height)
            content.PerformLayout()
            Dim height = content.GetPreferredSize(New Size(width, 0)).Height + actions.PreferredSize.Height + Px(20)
            form.MinimumSize = New Size(Math.Min(form.MinimumSize.Width, area.Width), Math.Min(form.MinimumSize.Height, area.Height))
            form.ClientSize = New Size(width, Math.Min(area.Height - chrome.Height, height))
            Dim bounds = Screen.FromControl(form).WorkingArea
            form.Location = New Point(bounds.Left + (bounds.Width - form.Width) \ 2, bounds.Top + (bounds.Height - form.Height) \ 2)
            form.BeginInvoke(New Action(Sub() viewport.AutoScrollPosition = Point.Empty))
        End Sub
    End Sub

    Public Sub SurfaceCard(panel As TableLayoutPanel)
        panel.BorderStyle = BorderStyle.None
        panel.BackColor = Surface
        panel.Padding = New Padding(Px(22))
        AddHandler panel.Paint, Sub(sender, e)
            Using pen As New Pen(Border)
                e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1)
            End Using
        End Sub
    End Sub

    Public Function Card(title As String, ParamArray rows As Control()) As TableLayoutPanel
        Dim block As New TableLayoutPanel With {.ColumnCount = 1, .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink, .Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right}
        block.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        SurfaceCard(block) : block.Padding = New Padding(GapSection)
        block.Margin = New Padding(0, 0, 0, GapSection)
        If title <> "" Then
            Dim heading As New Label With {.Text = title, .AutoSize = True, .Margin = New Padding(0, 0, 0, GapBlock)}
            Group(heading) : block.Controls.Add(heading)
        End If
        For Each row In rows
            block.Controls.Add(row)
        Next
        Return block
    End Function

    Public Function CardPage(ParamArray cards As Control()) As TableLayoutPanel
        Dim page As New TableLayoutPanel With {.ColumnCount = 1, .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink, .Dock = DockStyle.Top, .Padding = New Padding(GapSection)}
        page.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        For Each block As Control In cards
            page.Controls.Add(block)
        Next
        Return page
    End Function

    Public Function ValueRow(title As String, value As Control) As Control
        Dim row As New TableLayoutPanel With {.ColumnCount = 2, .AutoSize = True,
            .Anchor = AnchorStyles.Left Or AnchorStyles.Right, .Margin = New Padding(0, 0, 0, GapRow)}
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 42))
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 58))
        row.Controls.Add(New Label With {.Text = title, .Font = FontCaption, .ForeColor = TextMuted,
            .AutoSize = True, .Dock = DockStyle.Fill, .Margin = New Padding(0, 0, GapRelated, 0)}, 0, 0)
        value.Anchor = AnchorStyles.Left Or AnchorStyles.Right
        row.Controls.Add(value, 1, 0)
        Return row
    End Function

    Public Function PageHeading(title As String, subtitle As String) As Control
        Dim panel As New TableLayoutPanel With {.AutoSize = True, .ColumnCount = 1, .Margin = New Padding(0, 0, 0, Px(22)), .Anchor = AnchorStyles.Left Or AnchorStyles.Right}
        panel.Controls.Add(New Label With {.Text = title, .AutoSize = True, .Font = FontTitle, .ForeColor = TextMain, .Margin = New Padding(0, 0, 0, Px(6))})
        panel.Controls.Add(New Label With {.Text = subtitle, .AutoSize = True, .Font = FontCaption, .ForeColor = TextMuted, .Margin = New Padding(0)})
        Return panel
    End Function

    Public Function Overview(cards As Control()) As Control
        Dim page As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2, .Padding = New Padding(GapSection), .AutoScroll = True, .AutoScrollMinSize = New Size(0, Px(600))}
        page.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        page.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        page.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        page.Controls.Add(PageHeading("Состояние контроллера", "Связь, параметры устройства и сигналы в реальном времени"))
        Dim grid As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 2, .Margin = New Padding(0)}
        grid.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        grid.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        For i = 0 To cards.Length - 1
            If i < 2 Then cards(i).Dock = DockStyle.Fill
            cards(i).Margin = New Padding(If(i = 1, GapRelated, 0), 0, If(i = 0, GapRelated, 0), GapRelated)
            grid.Controls.Add(cards(i), If(i < 2, i, 0), If(i < 2, 0, i - 1))
            If i >= 2 Then grid.SetColumnSpan(cards(i), 2)
        Next
        page.Controls.Add(grid)
        Return page
    End Function

    Public Function SignalGrid(states As Control(), actions As Button()) As Control
        Dim grid As New TableLayoutPanel With {.AutoSize = True, .ColumnCount = 4, .Anchor = AnchorStyles.Left Or AnchorStyles.Right, .Margin = New Padding(0)}
        Dim captions = New String() {"Вход 1", "Вход 2", "Выход 1", "Выход 2"}
        For i = 0 To states.Length - 1
            grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F / states.Length))
            Dim tile As New TableLayoutPanel With {.AutoSize = True, .ColumnCount = 1, .Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right,
                .Margin = New Padding(0, 0, GapRelated, 0)}
            tile.Controls.Add(New Label With {.Text = captions(i), .AutoSize = True, .Font = FontCaption, .ForeColor = TextMuted, .Margin = New Padding(0, 0, 0, GapRelated)})
            states(i).Margin = New Padding(0, 0, 0, GapRelated)
            tile.Controls.Add(states(i))
            If actions(i) IsNot Nothing Then tile.Controls.Add(actions(i))
            grid.Controls.Add(tile, i, 0)
        Next
        Return grid
    End Function

    ' Одно меню для NEXT, старой прошивки и загрузчика. Карточки остаются на своих TabPage.
    Public Function MainNavigation(tabs As TabControl) As Control
        Dim shell As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1, .Margin = New Padding(0)}
        shell.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, Px(144)))
        shell.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        shell.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        Dim nav As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.TopDown,
            .WrapContents = False, .BackColor = Surface, .AutoScroll = True,
            .Padding = New Padding(Px(8), GapSection, Px(8), 0), .Margin = New Padding(0)}
        Dim links As New List(Of Button)
        For i = 0 To tabs.TabPages.Count - 1
            Dim index = i
            Dim link As New Button With {.Text = tabs.TabPages(i).Text, .Name = "mainSection" & i, .FlatStyle = FlatStyle.Flat,
                .Font = FontBody, .Height = Px(48), .Width = Px(128), .TextAlign = ContentAlignment.MiddleLeft,
                .Padding = New Padding(Px(12), 0, 0, 0), .Margin = New Padding(0, 0, 0, Px(4)),
                .BackColor = Surface, .ForeColor = TextMuted, .Cursor = Cursors.Hand}
            link.FlatAppearance.BorderSize = 0
            link.FlatAppearance.MouseOverBackColor = SurfaceHover
            AddHandler link.Click, Sub(sender, e) tabs.SelectedIndex = index
            AddHandler link.Paint, Sub(sender, e)
                If tabs.SelectedIndex = index Then
                    Using brush As New SolidBrush(Accent)
                        e.Graphics.FillRectangle(brush, 0, Px(8), Px(3), link.Height - Px(16))
                    End Using
                End If
            End Sub
            links.Add(link) : nav.Controls.Add(link)
        Next
        AddHandler tabs.SelectedIndexChanged, Sub(sender, e)
            For i = 0 To links.Count - 1
                links(i).ForeColor = If(i = tabs.SelectedIndex, Accent, TextMuted)
                links(i).BackColor = If(i = tabs.SelectedIndex, WindowBack, Surface)
                links(i).Invalidate()
            Next
        End Sub
        If links.Count > 0 Then links(0).ForeColor = Accent : links(0).BackColor = WindowBack
        tabs.Appearance = TabAppearance.FlatButtons
        tabs.SizeMode = TabSizeMode.Fixed : tabs.ItemSize = New Size(1, 1)
        tabs.Padding = New Point(0, 0) : tabs.Dock = DockStyle.None
        Dim host As New Panel With {.Dock = DockStyle.Fill, .Margin = New Padding(0)}
        host.Controls.Add(tabs)
        AddHandler host.Resize, Sub(sender, e)
            tabs.SetBounds(-Px(4), -Px(8), host.Width + Px(8), host.Height + Px(12))
        End Sub
        shell.Controls.Add(nav, 0, 0) : shell.Controls.Add(host, 1, 0)
        Return shell
    End Function

    Public Sub ConnectionLayout(form As Form, root As TableLayoutPanel, card As TableLayoutPanel)
        form.ClientSize = New Size(Px(830), Px(520))
        root.ColumnCount = 2
        root.ColumnStyles.Clear()
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, Px(335)))
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        root.Controls.Remove(card)
        Dim intro As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .Padding = New Padding(Px(30), Px(44), Px(26), Px(30)), .Margin = New Padding(0), .BackColor = Color.FromArgb(232, 240, 252)}
        intro.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        intro.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        intro.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        intro.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        intro.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        intro.Controls.Add(New Label With {.Text = "ДОМОВОЙ NEXT", .AutoSize = True, .Font = FontProduct, .ForeColor = TextMain, .Margin = New Padding(0, 0, 0, Px(24))})
        intro.Controls.Add(New Label With {.Text = "Настройка и обслуживание контроллеров доступа", .AutoSize = True, .MaximumSize = New Size(Px(220), 0), .Font = FontBody, .ForeColor = TextMuted, .Margin = New Padding(0)})
        intro.Controls.Add(New Panel With {.Dock = DockStyle.Fill})
        intro.Controls.Add(New Label With {.Text = "USB-подключение" & vbCrLf & "Параметры · диагностика · прошивка", .AutoSize = True, .MaximumSize = New Size(Px(225), 0), .Font = FontCaption, .ForeColor = TextMuted, .Margin = New Padding(0)})
        card.BorderStyle = BorderStyle.None
        card.Padding = New Padding(Px(30))
        root.Controls.Add(intro, 0, 0)
        root.Controls.Add(card, 1, 0)
        root.BackColor = Surface
    End Sub

    Public Sub ServiceActions(panel As FlowLayoutPanel, first As Button)
        panel.Controls.SetChildIndex(first, 0)
        panel.FlowDirection = FlowDirection.TopDown
        panel.WrapContents = False
        For Each button As Button In panel.Controls
            button.Width = Px(310)
            button.Height = ButtonHeight
            button.Font = FontBody
            button.TextAlign = ContentAlignment.MiddleLeft
            button.Margin = New Padding(0, 0, 0, GapRelated)
        Next
    End Sub

    Public Sub BusOptions(panel As TableLayoutPanel)
        panel.ColumnCount = 3
        panel.RowCount = 1
        panel.Height = Px(54)
        panel.AutoSize = False
        panel.Width = Px(700)
        panel.Anchor = AnchorStyles.Left Or AnchorStyles.Right
        panel.Margin = New Padding(0, 0, 0, GapBlock)
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 28))
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 24))
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 48))
    End Sub

    Public Function BusField(title As String, editor As Control) As Control
        Dim field As New TableLayoutPanel With {.AutoSize = False, .ColumnCount = 1, .Width = Px(160), .Height = Px(54),
            .Anchor = AnchorStyles.Left Or AnchorStyles.Right, .Margin = New Padding(0, 0, GapBlock, 0)}
        field.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        Dim caption As New Label With {.AutoSize = True, .Text = title, .Margin = New Padding(0, 0, 0, GapLabel)}
        Theme.Caption(caption)
        editor.Dock = DockStyle.Fill
        editor.Margin = New Padding(0)
        field.Controls.Add(caption, 0, 0)
        field.Controls.Add(editor, 0, 1)
        Return field
    End Function

    Public Sub BusTable(panel As TableLayoutPanel)
        panel.ColumnCount = 4
        panel.RowCount = 9
        panel.Height = Px(34 * 9)
        panel.AutoSize = False
        panel.Width = Px(700)
        panel.Anchor = AnchorStyles.Left Or AnchorStyles.Right
        panel.Margin = New Padding(0, 0, 0, GapRelated)
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, Px(40)))
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, Px(90)))
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, Px(150)))
        For i = 0 To 8
            panel.RowStyles.Add(New RowStyle(SizeType.Absolute, Px(34)))
        Next
        AddHandler panel.CellPaint, Sub(sender, e)
            If e.Row = 0 Then
                Using brush As New SolidBrush(WindowBack)
                    e.Graphics.FillRectangle(brush, e.CellBounds)
                End Using
            End If
        End Sub
    End Sub

    Public Function BusCell(text As String, heading As Boolean) As Label
        Return New Label With {.Text = text, .AutoSize = False, .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft, .Font = If(heading, FontCaption, FontBody),
            .ForeColor = TextMuted, .BackColor = Color.Transparent, .Margin = New Padding(Px(8), 0, Px(8), 0)}
    End Function

    Public Sub BusEditor(editor As Control)
        editor.Anchor = AnchorStyles.Left Or AnchorStyles.Right
        editor.Margin = New Padding(Px(8), Px(4), Px(12), Px(4))
    End Sub

    Public Function BusNotice(text As String, warning As Boolean) As Label
        Return New Label With {.Text = If(warning, "!  ", "ℹ  ") & text, .AutoSize = True,
            .Font = FontCaption, .ForeColor = If(warning, Theme.Warning, TextMuted),
            .BackColor = If(warning, Color.FromArgb(255, 248, 231), Color.FromArgb(239, 245, 253)), .Padding = New Padding(Px(10)),
            .Margin = New Padding(0, 0, 0, GapRelated), .MaximumSize = New Size(0, 0),
            .Anchor = AnchorStyles.Left Or AnchorStyles.Right}
    End Function

    Public Sub BusStatus(label As Label, responds As Boolean, configured As Boolean)
        SetText(label, If(Not configured, "—", If(responds, "● Отвечает", "○ Не отвечает")))
        SetColor(label, If(responds, OkText, If(configured, Warning, TextMuted)))
    End Sub

    Public Sub Address485(n As AddressEditor)
        n.Font = FontBody
        n.Margin = New Padding(0, 0, GapRelated, 0)
        AddHandler n.MouseWheel, AddressOf SwallowWheel
    End Sub

    Public Sub Combo(ByVal c As ComboBox, ByVal width As Integer)
        c.DropDownStyle = ComboBoxStyle.DropDownList
        c.FlatStyle = FlatStyle.Standard
        c.Font = FontBody
        c.ForeColor = TextMain
        c.BackColor = Surface
        c.Width = Px(width)

        ' Колесо мыши над списком меняет значение настройки, а человек всего лишь
        ' прокручивал страницу. Для настроек контроллера это недопустимо.
        AddHandler c.MouseWheel, AddressOf SwallowWheel
    End Sub

    Private Sub SwallowWheel(ByVal sender As Object, ByVal e As MouseEventArgs)
        Dim h As HandledMouseEventArgs = TryCast(e, HandledMouseEventArgs)
        If h IsNot Nothing Then h.Handled = True
    End Sub

    ' Пояснение или ошибка под полем.
    Public Sub Note(ByVal l As Label, ByVal isError As Boolean)
        l.Font = FontCaption
        l.ForeColor = If(isError, Danger, TextMuted)
        l.BackColor = Color.Transparent
    End Sub

    ' Значение только для чтения: тот же вид, но серым фоном.
    Public Sub ReadOnlyField(ByVal t As TextBox)
        t.[ReadOnly] = True
        t.BackColor = Disabled
        If t.Parent IsNot Nothing Then t.Parent.BackColor = Disabled
    End Sub

End Module

' Шкала уровня. Своя, а не стандартная полоса Windows: та всегда зелёная,
' цвет у неё при включённых темах оформления не меняется.
Public Class Meter
    Inherits Control

    Private _value As Integer = 0
    Private _max As Integer = 31

    Public Sub New()
        SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or ControlStyles.SupportsTransparentBackColor, True)
        BackColor = Color.Transparent
        Size = New Size(120, 8)
    End Sub

    Public Property Maximum As Integer
        Get
            Return _max
        End Get
        Set(ByVal v As Integer)
            _max = Math.Max(1, v)
            Invalidate()
        End Set
    End Property

    Public Property Value As Integer
        Get
            Return _value
        End Get
        Set(ByVal v As Integer)
            Dim n As Integer = Math.Max(0, Math.Min(v, _max))
            If n = _value Then Exit Property

            _value = n
            Invalidate()
        End Set
    End Property

    Protected Overrides Sub OnPaint(ByVal e As PaintEventArgs)
        Using b As New SolidBrush(Theme.Border)
            e.Graphics.FillRectangle(b, 0, 0, Width, Height)
        End Using

        Dim w As Integer = CInt(Width * (_value / _max))
        If w > 0 Then
            Using b As New SolidBrush(Theme.Accent)
                e.Graphics.FillRectangle(b, 0, 0, w, Height)
            End Using
        End If
    End Sub

End Class

' Доработка: Кряжев А., 2026. Поле байтового адреса без стрелок и изменения колесом.
Public Class AddressEditor
    Inherits TextBox
    Private previousText As String = "0"
    Private currentValue As Byte
    Private restoring As Boolean
    Public Event ValueChanged As EventHandler

    Public Sub New()
        MaxLength = 3
        Text = "0"
        AccessibleDescription = "Адрес устройства: целое число от 0 до 255"
    End Sub

    Public Property Value As Byte
        Get
            Return currentValue
        End Get
        Set(value As Byte)
            Text = value.ToString()
        End Set
    End Property

    Protected Overrides Sub OnKeyPress(e As KeyPressEventArgs)
        If Not Char.IsControl(e.KeyChar) AndAlso (e.KeyChar < "0"c OrElse e.KeyChar > "9"c) Then e.Handled = True
        MyBase.OnKeyPress(e)
    End Sub

    Protected Overrides Sub OnTextChanged(e As EventArgs)
        If restoring Then Return
        If Text = "" Then
            MyBase.OnTextChanged(e)
            Return
        End If
        Dim parsed As Byte
        If Not Text.All(Function(c) c >= "0"c AndAlso c <= "9"c) OrElse Not Byte.TryParse(Text, parsed) Then
            Dim position = SelectionStart
            restoring = True
            Text = previousText
            SelectionStart = Math.Min(position, TextLength)
            restoring = False
            Return
        End If
        previousText = Text
        Dim changed = currentValue <> parsed
        currentValue = parsed
        MyBase.OnTextChanged(e)
        If changed Then RaiseEvent ValueChanged(Me, EventArgs.Empty)
    End Sub

    Protected Overrides Sub OnLostFocus(e As EventArgs)
        Text = currentValue.ToString()
        MyBase.OnLostFocus(e)
    End Sub
End Class
