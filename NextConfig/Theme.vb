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

    Public ReadOnly FontProduct As New Font(Family, 18.0F, FontStyle.Regular)      ' название программы
    Public ReadOnly FontTitle As New Font(Family, 15.0F, FontStyle.Regular)        ' заголовок раздела, диалога
    Public ReadOnly FontGroup As New Font(Family, 11.0F, FontStyle.Bold)           ' заголовок группы
    Public ReadOnly FontBody As New Font(Family, 10.0F, FontStyle.Regular)         ' основной текст и поля
    Public ReadOnly FontBodyBold As New Font(Family, 10.0F, FontStyle.Bold)        ' важное в основном тексте
    Public ReadOnly FontCaption As New Font(Family, 9.0F, FontStyle.Regular)       ' подписи и пояснения
    Public ReadOnly FontValueBig As New Font(Family, 16.0F, FontStyle.Bold)        ' крупное значение состояния
    Public ReadOnly FontDot As New Font(Family, 12.0F, FontStyle.Regular)          ' точка индикатора
    Public ReadOnly FontMono As New Font(FamilyMono, 9.5F, FontStyle.Regular)      ' журнал

    ' ---- отступы (при базовом масштабе Windows) ----
    Public Const GapLabel As Integer = 6      ' подпись - поле
    Public Const GapRelated As Integer = 8    ' связанные элементы
    Public Const GapRow As Integer = 14       ' строки формы
    Public Const GapBlock As Integer = 18     ' внутри смыслового блока
    Public Const GapSection As Integer = 22   ' между блоками

    Public Const ButtonHeight As Integer = 40
    Public Const ButtonHeightSmall As Integer = 32
    Public Const ButtonWidth As Integer = 168
    Public Const ButtonWidthSmall As Integer = 120
    Public Const FieldHeight As Integer = 32

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
        f.AutoScaleMode = AutoScaleMode.Font
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
    Public Sub Dot(ByVal l As Label, ByVal state As eDot)
        l.Font = FontDot
        l.Text = "●"
        l.AutoSize = True
        l.BackColor = Color.Transparent
        l.ForeColor = DotColor(state)
    End Sub

    ' Текст состояния рядом с точкой. Цветом выделяем только успех и ошибку.
    Public Sub DotText(ByVal l As Label, ByVal state As eDot, ByVal text As String)
        l.Font = FontBody
        l.BackColor = Color.Transparent
        l.Text = text
        Select Case state
            Case eDot.Good : l.ForeColor = OkText
            Case eDot.Bad : l.ForeColor = Danger
            Case Else : l.ForeColor = TextMain
        End Select
    End Sub

    ' ---- поля ввода ----
    ' Поле кладётся в панель толщиной в одну точку: она и рисует границу,
    ' синюю при фокусе. У самого поля границы нет.

    Public Function FieldBox(ByVal t As TextBox, ByVal width As Integer) As Panel
        Dim p As New Panel()
        p.BackColor = Border
        p.Padding = New Padding(1)
        p.Size = New Size(width, FieldHeight)

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
            _value = Math.Max(0, Math.Min(v, _max))
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
