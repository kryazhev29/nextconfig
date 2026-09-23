Imports System.Drawing
Imports System.Windows.Forms

' Единое оформление всей программы: цвета, шрифты, отступы и виды кнопок.
' Формы своих цветов и размеров не держат - только зовут отсюда.
Public Module Theme

    ' ---- цвета ----
    Public ReadOnly Accent As Color = Color.FromArgb(&H1E, &H90, &HFF)       ' основной синий
    Public ReadOnly AccentHover As Color = Color.FromArgb(&H16, &H78, &HD2)  ' синий при наведении
    Public ReadOnly AccentDown As Color = Color.FromArgb(&H12, &H62, &HAD)   ' синий при нажатии
    Public ReadOnly Ok As Color = Color.FromArgb(&H32, &HCD, &H32)           ' зелёный индикатора
    Public ReadOnly OkText As Color = Color.FromArgb(&H23, &H7A, &H32)       ' зелёный текста
    Public ReadOnly Danger As Color = Color.FromArgb(&HDC, &H35, &H45)       ' красный
    Public ReadOnly DangerHover As Color = Color.FromArgb(&HBD, &H2D, &H3B)
    Public ReadOnly Warning As Color = Color.FromArgb(&H8A, &H61, &H00)      ' тёмный янтарный
    Public ReadOnly WindowBack As Color = Color.FromArgb(&HF4, &HF7, &HFB)   ' фон окна
    Public ReadOnly Surface As Color = Color.White                           ' поверхность блоков
    Public ReadOnly SurfaceHover As Color = Color.FromArgb(&HEE, &HF3, &HFA)
    Public ReadOnly TextMain As Color = Color.FromArgb(&H20, &H2B, &H3A)     ' основной текст
    Public ReadOnly TextMuted As Color = Color.FromArgb(&H65, &H73, &H86)    ' второстепенный текст
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
    Public ReadOnly ButtonHeightSmall As Integer = Px(32)
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
        f.Font = FontBody
    End Sub

    ' Блок с белой поверхностью и тонкой границей.
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

    Public Function FieldBox(ByVal t As TextBox, ByVal width As Integer) As Panel
        Dim p As New Panel()
        p.BackColor = Border
        p.Padding = New Padding(1)
        p.Size = New Size(Px(width), FieldHeight)

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
        inner.Padding = New Padding(GapLabel, Math.Max(0, (FieldHeight - 2 - t.PreferredHeight) \ 2), GapLabel, 0)
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
        f.root.Controls.Remove(f.info)
        f.info.Dispose()
        f.root.Controls.Add(area, 0, 1)
    End Sub

    ' Доработка: Кряжев А., 2026. Общие стили панели наладки.
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
        panel.Height = Px(32 * 9)
        panel.AutoSize = False
        panel.Width = Px(700)
        panel.Anchor = AnchorStyles.Left Or AnchorStyles.Right
        panel.Margin = New Padding(0, 0, 0, GapRelated)
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, Px(40)))
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, Px(90)))
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, Px(150)))
        For i = 0 To 8
            panel.RowStyles.Add(New RowStyle(SizeType.Absolute, Px(32)))
        Next
        AddHandler panel.CellPaint, Sub(sender, e)
            If e.Row = 0 OrElse e.Row Mod 2 = 0 Then
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
            .BackColor = WindowBack, .Padding = New Padding(Px(10)),
            .Margin = New Padding(0, 0, 0, GapRelated), .MaximumSize = New Size(0, 0),
            .Anchor = AnchorStyles.Left Or AnchorStyles.Right}
    End Function

    Public Sub BusStatus(label As Label, responds As Boolean, configured As Boolean)
        SetText(label, If(responds, "● Отвечает", "○ Не отвечает"))
        SetColor(label, If(responds, OkText, If(configured, Warning, TextMuted)))
    End Sub

    Public Sub Address485(n As NumericUpDown)
        n.Font = FontBody
        n.Margin = New Padding(0, 0, GapRelated, 0)
        AddHandler n.MouseWheel, AddressOf SwallowWheel
    End Sub

    Public Sub Combo(ByVal c As ComboBox, ByVal width As Integer)
        c.DropDownStyle = ComboBoxStyle.DropDownList
        c.FlatStyle = FlatStyle.Flat
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
