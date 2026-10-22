Imports System.Drawing.Drawing2D

Public Structure LinkPoint
    Public Seconds As Double
    Public EndSeconds As Double
    Public Level As Byte             ' дБм без знака: RSRP в 4G, RSSI в 2G/3G
End Structure

Public Class LinkHistory
    Private ReadOnly samples As New List(Of LinkPoint)
    Public LatestSeconds As Double
    Public Gsm As Boolean            ' точки — RSSI 2G/3G; смена сети начинает историю заново
    Public Sub Clear()
        samples.Clear() : LatestSeconds = 0
    End Sub
    Public ReadOnly Property Points As IReadOnlyList(Of LinkPoint)
        Get
            Return samples
        End Get
    End Property

    Public Sub Observe(seconds As Double, rsrp As Byte)
        Observe(seconds, rsrp, False)
    End Sub

    Public Sub Observe(seconds As Double, level As Byte, gsm As Boolean)
        If gsm <> Me.Gsm Then Clear() : Me.Gsm = gsm
        Dim old = samples.TakeWhile(Function(p) p.EndSeconds < seconds - 600).Count()
        If old > 0 Then samples.RemoveRange(0, old)
        If samples.Count > 0 AndAlso samples(0).Seconds < seconds - 600 Then
            Dim first = samples(0)
            first.Seconds = seconds - 600 : samples(0) = first
        End If
        If level = 255 Then Return
        LatestSeconds = seconds
        If samples.Count = 0 OrElse samples.Last().Level <> level Then
            samples.Add(New LinkPoint With {.Seconds = seconds, .EndSeconds = seconds, .Level = level})
        Else
            Dim last = samples.Last()
            last.EndSeconds = seconds : samples(samples.Count - 1) = last
        End If
    End Sub
End Class

Public Module LinkDrawing
    Public ReadOnly Amber As Color = Color.FromArgb(&HF5, &H9E, &HB)
    Public ReadOnly AmberText As Color = Color.FromArgb(&H92, &H40, &HE)
    Public ReadOnly AmberBack As Color = Color.FromArgb(&HFE, &HF3, &HC7)
    Public ReadOnly GoodBack As Color = Color.FromArgb(&HDC, &HFC, &HE7)
    Public ReadOnly PoorBack As Color = Color.FromArgb(&HFE, &HEE, &HE2)
    Public ReadOnly StageColors As Color() = {Color.FromArgb(&HE5, &HE7, &HEB), Color.FromArgb(&HDB, &HEA, &HFE),
        Color.FromArgb(&HBF, &HDB, &HFE), Color.FromArgb(&H93, &HC5, &HFD), Color.FromArgb(&HBB, &HF7, &HD0), Color.FromArgb(&H86, &HEF, &HAC)}
    Public Function GradeColor(grade As Integer) As Color
        Return If(grade = LinkDiag.Good, Theme.Ok, If(grade = LinkDiag.Medium, Amber, If(grade = LinkDiag.Poor, Theme.Danger, Theme.Idle)))
    End Function

    Public Function GradeTextColor(grade As Integer) As Color
        Return If(grade = LinkDiag.Medium, AmberText, GradeColor(grade))
    End Function

    Public Function GradeBackColor(grade As Integer) As Color
        Return If(grade = LinkDiag.Good, GoodBack, If(grade = LinkDiag.Medium, AmberBack, If(grade = LinkDiag.Poor, PoorBack, Theme.Disabled)))
    End Function

    Public Sub Bars(g As Graphics, area As Rectangle, count As Integer, grade As Integer)
        For i = 0 To 4
            Dim h = (i + 1) * area.Height \ 5
            Using brush As New SolidBrush(If(i < count, GradeColor(grade), Theme.Border))
                g.FillRectangle(brush, area.Left + i * area.Width \ 5, area.Bottom - h, Math.Max(2, area.Width \ 7), h)
            End Using
        Next
    End Sub

    ' Крест линиями: символ «×» в Montserrat слишком мелкий для кружка состояния.
    Public Sub Cross(g As Graphics, x As Integer, y As Integer, half As Single, color As Color)
        Using pen As New Pen(color, Math.Max(1.5F, half / 2.6F)) With {.StartCap = LineCap.Round, .EndCap = LineCap.Round}
            g.DrawLine(pen, x - half, y - half, x + half, y + half)
            g.DrawLine(pen, x - half, y + half, x + half, y - half)
        End Using
    End Sub

    ' Веер Wi-Fi: точка и три дуги, закрашено по оценке моста (LinkDiag.WifiGrade: хорошо — три, средне — две, слабо — одна); без данных — пустой.
    ' Неиспользуемый канал — серым: зелёный веер читается как «Wi-Fi работает».
    Public Sub WifiFan(g As Graphics, area As Rectangle, level As Integer, Optional muted As Boolean = False)
        Dim grade = LinkDiag.WifiGrade(level), filled = grade + 1
        Dim color = If(muted, Theme.Idle, GradeColor(grade))
        Dim cx = area.Left + area.Width / 2.0F, cy = area.Bottom - 1.0F, ring = area.Height / 3.6F
        Using dot As New SolidBrush(If(filled > 0, color, Theme.Border))
            g.FillEllipse(dot, cx - ring * 0.4F, cy - ring * 0.8F, ring * 0.8F, ring * 0.8F)
        End Using
        For i = 1 To 3
            Dim r = ring * (i + 0.2F)
            Using pen As New Pen(If(i <= filled, color, Theme.Border), Math.Max(1.5F, ring * 0.42F)) With {.StartCap = LineCap.Round, .EndCap = LineCap.Round}
                g.DrawArc(pen, cx - r, cy - r, r * 2, r * 2, 225, 90)
            End Using
        Next
    End Sub

    Public Sub DrawLabel(g As Graphics, value As String, font As Font, color As Color, bounds As Rectangle, Optional align As ContentAlignment = ContentAlignment.MiddleLeft)
        Dim flags = TextFormatFlags.WordBreak Or TextFormatFlags.NoPrefix
        If align = ContentAlignment.MiddleRight Then flags = flags Or TextFormatFlags.Right
        If align = ContentAlignment.MiddleCenter Then flags = flags Or TextFormatFlags.HorizontalCenter
        TextRenderer.DrawText(g, value, font, bounds, color, flags Or TextFormatFlags.VerticalCenter)
    End Sub

    ' Пока истории меньше 10 минут, ось времени идёт от первой точки: линия видна сразу во всю ширину.
    Public Function PlotWindow(history As LinkHistory, now As Double) As Double
        If history Is Nothing OrElse history.Points.Count = 0 Then Return 600
        Return Math.Max(60, Math.Min(600, now - history.Points(0).Seconds))
    End Function

    Public Function AgoText(seconds As Double) As String
        Return If(seconds >= 120, "−" & Math.Round(seconds / 60).ToString("0") & " мин", "−" & Math.Round(seconds).ToString("0") & " с")
    End Function

    Public Sub Plot(g As Graphics, bounds As Rectangle, history As LinkHistory, now As Double, zones As Boolean)
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim limits = LinkDiag.LevelScale(history.Gsm)
        Dim ymin = limits(0), ymax = limits(3), window = PlotWindow(history, now)
        If zones Then
            For grade = 0 To 2
                Dim y = bounds.Bottom - CSng((limits(grade + 1) - ymin) / CDbl(ymax - ymin) * bounds.Height)
                Dim h = CSng((limits(grade + 1) - limits(grade)) / CDbl(ymax - ymin) * bounds.Height)
                Using brush As New SolidBrush(GradeBackColor(grade))
                    g.FillRectangle(brush, bounds.Left, y, bounds.Width, h)
                End Using
            Next
        End If
        Dim points As New List(Of PointF)
        Dim x = Function(seconds As Double) CSng(bounds.Left + Math.Max(0, seconds - now + window) / window * bounds.Width)
        For Each sample In history.Points
            If sample.EndSeconds < now - window Then Continue For
            Dim y = CSng(bounds.Bottom - Math.Max(0, Math.Min(ymax - ymin, -CInt(sample.Level) - ymin)) / CDbl(ymax - ymin) * bounds.Height)
            points.Add(New PointF(x(sample.Seconds), y))
            If sample.EndSeconds > sample.Seconds Then points.Add(New PointF(x(sample.EndSeconds), y))
        Next
        If points.Count = 0 Then Return
        Dim latestX = x(history.LatestSeconds)
        If latestX > points.Last().X Then points.Add(New PointF(latestX, points.Last().Y))
        Using pen As New Pen(Theme.Accent, Theme.DrawingPx(g, 10) / 4.0F), brush As New SolidBrush(Theme.Accent)
            If points.Count > 1 Then g.DrawLines(pen, points.ToArray())
            Dim last = points.Last(), radius = Theme.DrawingPx(g, If(zones, 4, 3))
            g.FillEllipse(brush, last.X - radius, last.Y - radius, radius * 2, radius * 2)
            ' На большом графике у последней точки — текущий уровень, чтобы не сверять со шкалой.
            If zones AndAlso history.Points.Count > 0 Then
                Dim label = "−" & history.Points.Last().Level & " дБм", w = Theme.DrawingPx(g, 80)
                DrawLabel(g, label, Theme.FontBodyBold, Theme.TextMain,
                    New Rectangle(CInt(last.X) - w - Theme.DrawingPx(g, 6), CInt(last.Y) - Theme.DrawingPx(g, 26), w, Theme.DrawingPx(g, 22)), ContentAlignment.MiddleRight)
            End If
        End Using
    End Sub
End Module

Public Class SignalGauge
    Inherits Control
    Public Quality As Boolean
    Public Gsm As Boolean            ' 2G/3G: уровень — RSSI по CSQ, качество модем не сообщает
    Public Network As String = "2G"
    Public Raw As Byte = 255

    Public Sub New()
        SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Height = Theme.ControlPx(Me, 168) : Dock = DockStyle.Top : BackColor = Theme.Surface
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics, pad = Theme.ControlPx(Me, 6), barY = Theme.ControlPx(Me, 93), barHeight = Theme.ControlPx(Me, 10)
        Dim measured = Not (Quality AndAlso Gsm)
        ' Монтажнику — что значит шкала, без терминов.
        LinkDrawing.DrawLabel(g, If(Not measured, "В " & Network & " качество сигнала не измеряется", If(Quality, "Насколько чистый сигнал: мешают соседние вышки и загрузка", "Насколько сильно слышна вышка")),
            Theme.FontCaption, Theme.TextMuted, New Rectangle(pad, barY + Theme.ControlPx(Me, 44), Width - 2 * pad, Theme.ControlPx(Me, 24)))
        Dim limits = If(Quality, New Integer() {LinkDiag.RsrqMinimum, LinkDiag.RsrqPoorThreshold, LinkDiag.RsrqGoodThreshold, LinkDiag.RsrqMaximum}, LinkDiag.LevelScale(Gsm))
        Dim minimum = limits(0), maximum = limits(3)
        Dim grade = If(Not measured, -1, If(Quality, LinkDiag.RsrqGrade(Raw), If(Gsm, LinkDiag.GsmGrade(Raw), LinkDiag.RsrpGrade(Raw))))
        LinkDrawing.DrawLabel(g, If(Quality, If(Gsm, "Качество", "Качество (RSRQ)"), If(Gsm, "Уровень (RSSI)", "Уровень (RSRP)")), Theme.FontBody, Theme.TextMuted, New Rectangle(pad, 0, Width - 2 * pad, Theme.ControlPx(Me, 24)))
        LinkDrawing.DrawLabel(g, If(Not measured, "—", If(Raw = 255, "—", "−" & Raw) & If(Quality, " дБ", " дБм")), Theme.FontValueBig, Theme.TextMain, New Rectangle(pad, Theme.ControlPx(Me, 29), Width - Theme.ControlPx(Me, 136), Theme.ControlPx(Me, 48)))
        Dim span = Math.Max(1, Width - 2 * pad)
        ' Оценки нет, если величину не измеряют: серая полоса и пояснение вместо «Нет данных».
        If measured Then
            Dim badge As New Rectangle(Width - Theme.ControlPx(Me, 126), Theme.ControlPx(Me, 35), Theme.ControlPx(Me, 120), Theme.ControlPx(Me, 30))
            Using brush As New SolidBrush(LinkDrawing.GradeBackColor(grade))
                g.FillRectangle(brush, badge)
            End Using
            LinkDrawing.DrawLabel(g, LinkDiag.GradeText(grade), Theme.FontBody, LinkDrawing.GradeTextColor(grade), badge, ContentAlignment.MiddleCenter)
        Else
            Using brush As New SolidBrush(Theme.Border)
                g.FillRectangle(brush, pad, barY, span, barHeight)
            End Using
            Return
        End If
        For gradeIndex = 0 To 2
            Dim x = pad + CSng((limits(gradeIndex) - minimum) / CDbl(maximum - minimum) * span)
            Dim w = CSng((limits(gradeIndex + 1) - limits(gradeIndex)) / CDbl(maximum - minimum) * span)
            Using brush As New SolidBrush(LinkDrawing.GradeColor(gradeIndex))
                g.FillRectangle(brush, x, barY, w, barHeight)
            End Using
        Next
        If Raw <> 255 Then
            Dim x = pad + CInt(Math.Max(0, Math.Min(1, (-CInt(Raw) - minimum) / CDbl(maximum - minimum))) * span)
            Using pen As New Pen(Theme.TextMain, Theme.ControlPx(Me, 3))
                g.DrawLine(pen, x, barY - Theme.ControlPx(Me, 5), x, barY + barHeight + Theme.ControlPx(Me, 5))
            End Using
        End If
        Dim marks = limits
        For i = 0 To marks.Length - 1
            Dim x = pad + CInt((marks(i) - minimum) / CDbl(maximum - minimum) * span)
            Dim w = Theme.ControlPx(Me, 50)
            x = Math.Max(pad, Math.Min(Width - w, x - w \ 2))
            LinkDrawing.DrawLabel(g, "−" & Math.Abs(marks(i)), Theme.FontCaption, Theme.TextMuted, New Rectangle(x, barY + Theme.ControlPx(Me, 15), w, Theme.ControlPx(Me, 25)), ContentAlignment.MiddleCenter)
        Next
    End Sub
End Class

Public Class LinkBannerPanel
    Inherits Control
    Private ReadOnly refresh As Button
    Private ReadOnly titleFont As Font = AppFonts.Create(Theme.FontBody.Size * 1.2F, FontStyle.Bold)
    Private ReadOnly detailFont As Font = AppFonts.Create(Theme.FontBody.Size * 0.975F, FontStyle.Regular)
    Public Heading As String = "Чтение состояния связи…"
    Public Detail As String = ""
    Public Grade As Integer = -1
    Public SignalBars As Integer

    Public Sub New(button As Button)
        SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Height = Theme.ControlPx(Me, 104) : Dock = DockStyle.Top : Margin = New Padding(0, 0, 0, Theme.GapRelated)
        refresh = button : Theme.Secondary(refresh) : refresh.Text = "Обновить" : refresh.Width = Theme.ControlPx(Me, 120)
        Controls.Add(refresh)
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        If refresh IsNot Nothing Then refresh.Location = New Point(Width - refresh.Width - Theme.ControlPx(Me, 24), (Height - refresh.Height) \ 2)
    End Sub

    Public Sub SetState(grade As Integer, bars As Integer, heading As String, detail As String)
        Me.Grade = grade : SignalBars = bars : Me.Heading = heading : Me.Detail = detail
        ForeColor = LinkDrawing.GradeTextColor(grade) : BackColor = LinkDrawing.GradeBackColor(grade)
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim left = Theme.ControlPx(Me, 67), available = Math.Max(1, Width - left - refresh.Width - Theme.ControlPx(Me, 48))
        LinkDrawing.Bars(e.Graphics, New Rectangle(Theme.ControlPx(Me, 23), Theme.ControlPx(Me, 35), Theme.ControlPx(Me, 28), Theme.ControlPx(Me, 29)), SignalBars, Grade)
        LinkDrawing.DrawLabel(e.Graphics, Heading, titleFont, ForeColor, New Rectangle(left, Theme.ControlPx(Me, 17), available, Theme.ControlPx(Me, 28)))
        LinkDrawing.DrawLabel(e.Graphics, Detail, detailFont, ForeColor, New Rectangle(left, Theme.ControlPx(Me, 45), available, Theme.ControlPx(Me, 45)))
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            titleFont.Dispose() : detailFont.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class

Public Class CellChips
    Inherits Control
    Private ReadOnly errors As LinkLabel
    Public Chips As String() = {}

    Public Sub New(failures As LinkLabel)
        SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        errors = failures : Controls.Add(errors)
        Height = Theme.ControlPx(Me, 100) : Dock = DockStyle.Top : BackColor = Theme.Surface
    End Sub

    Private Function ChipBounds(g As Graphics) As List(Of Rectangle)
        Dim bounds As New List(Of Rectangle), x = 0, y = 0
        For Each chip In Chips
            Dim span = Math.Min(Math.Max(1, Width), TextRenderer.MeasureText(g, chip, Theme.FontBody).Width + Theme.ControlPx(Me, 20))
            If x > 0 AndAlso x + span > Width Then x = 0 : y += Theme.ControlPx(Me, 33)
            bounds.Add(New Rectangle(x, y, span, Theme.ControlPx(Me, 27)))
            x += span + Theme.ControlPx(Me, 6)
        Next
        Return bounds
    End Function

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim bounds = ChipBounds(e.Graphics)
        For i = 0 To Chips.Length - 1
            Using brush As New SolidBrush(Theme.WindowBack)
                e.Graphics.FillRectangle(brush, bounds(i))
            End Using
            LinkDrawing.DrawLabel(e.Graphics, Chips(i), Theme.FontBody, Theme.TextMain, bounds(i), ContentAlignment.MiddleCenter)
        Next
        errors.Location = New Point(0, If(bounds.Count = 0, 0, bounds.Last().Bottom + Theme.ControlPx(Me, 12)))
        Dim wanted = errors.Top + errors.Height + Theme.ControlPx(Me, 6)
        If Height <> wanted Then Height = wanted
    End Sub
End Class

Public Class SignalPlot
    Inherits Control
    Public History As LinkHistory
    Public Seconds As Double
    Public Version As Byte
    Public ReadOnly Property Collecting As Boolean
        Get
            Return History.Points.Count = 0 OrElse History.LatestSeconds - History.Points(0).Seconds < 30
        End Get
    End Property

    Public Sub New()
        SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Height = Theme.ControlPx(Me, 190) : Dock = DockStyle.Top : BackColor = Theme.Surface
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics, area As New Rectangle(Theme.ControlPx(Me, 50), Theme.ControlPx(Me, 32), Math.Max(1, Width - Theme.ControlPx(Me, 65)), Height - Theme.ControlPx(Me, 58))
        ' CSQ прошивка опрашивает раз в 30 с (comm_info.c), частый опрос антенны — только QENG для 4G.
        LinkDrawing.DrawLabel(g, If(Version >= 5 AndAlso Not History.Gsm, "точка раз в 2 с", "точка раз в 30 с"), Theme.FontCaption, Theme.TextMuted, New Rectangle(0, 0, Width, Theme.ControlPx(Me, 25)), ContentAlignment.MiddleRight)
        LinkDrawing.Plot(g, area, History, Seconds, True)
        If History.Points.Count = 0 Then LinkDrawing.DrawLabel(g, "собираю данные…", Theme.FontCaption, Theme.TextMuted, area, ContentAlignment.MiddleCenter)
        Dim scale = LinkDiag.LevelScale(History.Gsm)
        For Each mark In New Integer() {scale(3), scale(2), scale(1), scale(0)}
            Dim y = area.Bottom - CInt((mark - scale(0)) / CDbl(scale(3) - scale(0)) * area.Height)
            LinkDrawing.DrawLabel(g, "−" & Math.Abs(mark), Theme.FontCaption, Theme.TextMuted, New Rectangle(0, y - Theme.ControlPx(Me, 10), Theme.ControlPx(Me, 44), Theme.ControlPx(Me, 20)))
        Next
        LinkDrawing.DrawLabel(g, LinkDrawing.AgoText(LinkDrawing.PlotWindow(History, Seconds)), Theme.FontCaption, Theme.TextMuted, New Rectangle(area.Left, area.Bottom, Theme.ControlPx(Me, 70), Theme.ControlPx(Me, 24)))
        LinkDrawing.DrawLabel(g, "сейчас", Theme.FontCaption, Theme.TextMuted, New Rectangle(area.Right - Theme.ControlPx(Me, 70), area.Bottom, Theme.ControlPx(Me, 70), Theme.ControlPx(Me, 24)), ContentAlignment.MiddleRight)
    End Sub
End Class

Public Class ConnectionPhases
    Inherits Control
    Private ReadOnly hint As New ToolTip
    Public Durations As UInteger() = New UInteger(5) {}
    Private hover As Integer = -1

    Public Sub New()
        SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Height = Theme.ControlPx(Me, 145) : Dock = DockStyle.Top : BackColor = Theme.Surface
    End Sub

    Public Function PhaseAt(x As Integer) As Integer
        Dim total = Durations.Sum(Function(d) CDbl(d))
        If total = 0 OrElse x < 0 OrElse x >= Width Then Return -1
        Dim endX As Double = 0
        For i = 0 To 5
            endX += Durations(i) / total * Width
            If x < endX Then Return i
        Next
        Return -1
    End Function

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        Dim index = If(e.Y >= Theme.ControlPx(Me, 32) AndAlso e.Y < Theme.ControlPx(Me, 61), PhaseAt(e.X), -1)
        If index = hover Then Return
        hover = index
        hint.SetToolTip(Me, If(index < 0, "", LinkDiag.Stages(Math.Max(0, index)) & ": " & LinkDiag.DurationText(Durations(Math.Max(0, index)))))
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim total = Durations.Sum(Function(d) CDbl(d)), g = e.Graphics
        LinkDrawing.DrawLabel(g, "Подключение " & (total / 1000).ToString("0.0", Globalization.CultureInfo.GetCultureInfo("ru-RU")) & " с", Theme.FontGroup, Theme.TextMain, New Rectangle(0, 0, Width, Theme.ControlPx(Me, 28)))
        Dim start As Double = 0
        For i = 0 To 5
            Dim span = If(total = 0, 0, Durations(i) / total * Width)
            Using brush As New SolidBrush(LinkDrawing.StageColors(i))
                g.FillRectangle(brush, CSng(start), Theme.ControlPx(Me, 34), CSng(span), Theme.ControlPx(Me, 28))
            End Using
            Dim caption = LinkDiag.Stages(i) & " " & (Durations(i) / 1000).ToString("0.0", Globalization.CultureInfo.GetCultureInfo("ru-RU"))
            If TextRenderer.MeasureText(g, caption, Theme.FontCaption).Width <= span - Theme.ControlPx(Me, 8) Then LinkDrawing.DrawLabel(g, caption, Theme.FontCaption, Theme.TextMain, New Rectangle(CInt(start) + Theme.ControlPx(Me, 4), Theme.ControlPx(Me, 34), CInt(span) - Theme.ControlPx(Me, 8), Theme.ControlPx(Me, 28)))
            start += span
        Next
        For i = 0 To 5
            Dim x = (i Mod 2) * Width \ 2, y = Theme.ControlPx(Me, 70 + (i \ 2) * 23)
            Using brush As New SolidBrush(LinkDrawing.StageColors(i))
                g.FillRectangle(brush, x, y + Theme.ControlPx(Me, 6), Theme.ControlPx(Me, 9), Theme.ControlPx(Me, 9))
            End Using
            LinkDrawing.DrawLabel(g, LinkDiag.Stages(i) & " · " & LinkDiag.DurationText(Durations(i)), Theme.FontCaption, Theme.TextMuted, New Rectangle(x + Theme.ControlPx(Me, 15), y, Width \ 2 - Theme.ControlPx(Me, 17), Theme.ControlPx(Me, 23)))
        Next
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then hint.Dispose()
        MyBase.Dispose(disposing)
    End Sub
End Class

' Карточка «Связь» на странице устройства (22.10.2026): итог крупно; при выбранном Wi-Fi-модеме — строки каналов
' со значком, ролью и оценкой сигнала словом справа; путь до сервера по активному каналу. Каждое сведение — один раз:
' «на связи» — в итоге и цепочке; цифры сотовой — второй строкой её канала, отдельная шкала сигнала — только при одном
' канале. «Логика: …» с кнопкой «Изменить» открывает выбор Wi-Fi-модема; память идентификаторов — в карточке
' «Устройство». Размеры в точках при 100 %.
Public Class LinkOverview
    Inherits Control
    Private Const RowsTop As Integer = 82, RowHeight As Integer = 50, RowPitch As Integer = 54
    Private Const ChainHeight As Integer = 106, ProblemHeight As Integer = 44, PanelHeight As Integer = 90
    Private ReadOnly nameFont As Font = AppFonts.Create(Theme.FontBody.Size * 1.1F, FontStyle.Regular)
    Private ReadOnly nameBold As Font = AppFonts.Create(Theme.FontBody.Size * 1.1F, FontStyle.Bold)
    Private ReadOnly policy As New Button With {.Name = "linkPolicy", .Text = "Изменить", .Visible = False}
    Private _channels As LinkChannelView = LinkChannels.Stale()
    Public Signal As LinkSignalView
    Public Cell As String = ""
    Public Details As String = "Нет свежих данных"
    Public Event More As EventHandler
    Public Event PolicyClick As EventHandler

    Public Sub New()
        SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Height = Theme.ControlPx(Me, DesignHeight) : Dock = DockStyle.Top : BackColor = Theme.Surface
        Signal = LinkDiag.SignalView(Nothing, 99)
        Theme.Secondary(policy) : Theme.Small(policy)
        policy.Height = Theme.ControlPx(Me, 28) ' в строку заголовка карточки
        AddHandler policy.Click, AddressOf PolicyClicked
        Controls.Add(policy)
    End Sub

    Public Property Channels As LinkChannelView
        Get
            Return _channels
        End Get
        Set(value As LinkChannelView)
            _channels = value
            policy.Visible = PolicyText <> ""
            PlacePolicy()
        End Set
    End Property

    Public ReadOnly Property Steps As Integer
        Get
            Return Channels.Steps
        End Get
    End Property

    Public ReadOnly Property Problem As String
        Get
            Return Channels.Problem
        End Get
    End Property

    ' Крупная шкала сигнала — только при одном канале. При двух те же цифры стоят в строке сотовой: второй раз не повторяем.
    Public ReadOnly Property PanelVisible As Boolean
        Get
            Return Channels.Rows.Length = 0
        End Get
    End Property

    ' Заголовок 30, итог 52, строки каналов по 54 и зазор 6, путь до сервера 106 (с подсказкой 150), шкала 90 (один канал), низ 4.
    Private ReadOnly Property ChainDesign As Integer
        Get
            Return RowsTop + If(Channels.Rows.Length = 0, 0, RowPitch * Channels.Rows.Length + 6)
        End Get
    End Property

    Private ReadOnly Property SignalDesign As Integer
        Get
            Return ChainDesign + ChainHeight + If(Problem = "", 0, ProblemHeight)
        End Get
    End Property

    Public ReadOnly Property DesignHeight As Integer
        Get
            Return SignalDesign + If(PanelVisible, PanelHeight, 0) + 4
        End Get
    End Property

    Public ReadOnly Property SignalTop As Integer
        Get
            Return Theme.ControlPx(Me, SignalDesign)
        End Get
    End Property

    ' Куда нажимать за подробностями сотовой: крупная шкала или, при Wi-Fi, строка сотовой.
    Public ReadOnly Property MoreArea As Rectangle
        Get
            If PanelVisible Then Return New Rectangle(0, SignalTop, Width, Theme.ControlPx(Me, PanelHeight))
            For i = 0 To Channels.Rows.Length - 1
                If Channels.Rows(i).Channel = eLinkChannel.Cellular Then Return New Rectangle(0, Theme.ControlPx(Me, RowsTop + RowPitch * i), Width, Theme.ControlPx(Me, RowHeight))
            Next
            Return Rectangle.Empty
        End Get
    End Property

    ' «Логика: только GSM» в углу и кнопка «Изменить» — выбор Wi-Fi-модема в настройках; при одном канале — оператор и сеть.
    Private ReadOnly Property PolicyText As String
        Get
            Return If(Channels.Rows.Length = 0, "", Channels.Policy)
        End Get
    End Property

    Private Sub PlacePolicy()
        policy.Location = New Point(Math.Max(0, Width - policy.Width - Theme.ControlPx(Me, 4)), 0)
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        PlacePolicy()
    End Sub

    Private Sub PolicyClicked(sender As Object, e As EventArgs)
        RaiseEvent PolicyClick(Me, EventArgs.Empty)
    End Sub

    Private Shared Function ToneText(tone As eLinkTone) As Color
        Return If(tone = eLinkTone.Good, Theme.OkText, If(tone = eLinkTone.Warn, LinkDrawing.AmberText, If(tone = eLinkTone.Bad, Theme.Danger, Theme.TextMuted)))
    End Function

    Private Shared Function ToneDot(tone As eLinkTone) As Color
        Return If(tone = eLinkTone.Good, Theme.Ok, If(tone = eLinkTone.Warn, LinkDrawing.Amber, If(tone = eLinkTone.Bad, Theme.Danger, Theme.Idle)))
    End Function

    ' Одна строка с многоточием: длинное имя оператора не переносится внутрь соседней колонки.
    Private Shared Sub OneLine(g As Graphics, value As String, font As Font, color As Color, bounds As Rectangle, Optional center As Boolean = False)
        TextRenderer.DrawText(g, value, font, bounds, color, TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix Or
            TextFormatFlags.VerticalCenter Or If(center, TextFormatFlags.HorizontalCenter, TextFormatFlags.Left))
    End Sub

    Private Shared Function TextWidth(g As Graphics, value As String, font As Font) As Integer
        Return TextRenderer.MeasureText(g, value, font, New Size(Integer.MaxValue, Integer.MaxValue), TextFormatFlags.SingleLine Or TextFormatFlags.NoPrefix).Width
    End Function

    ' Оценка канала одними словами для Wi-Fi и сотовой: «сигнал хороший»; проценты и дБм рядом не сравнить. -1 — нет данных.
    Private Function RowGrade(row As LinkChannelRow) As Integer
        Return If(row.Channel = eLinkChannel.Wifi, LinkDiag.WifiGrade(row.Level), Signal.Grade)
    End Function

    Private Shared Function Unused(row As LinkChannelRow) As Boolean
        Return row.Role = "Не используется"
    End Function

    ' Роль: активный — заливка, работающие роли — белая плашка с рамкой, «Не используется» — серая.
    Private Sub DrawRole(g As Graphics, pill As Rectangle, row As LinkChannelRow)
        Dim used = Not Unused(row)
        Using path = Theme.RoundedPath(New RectangleF(pill.X + 0.5F, pill.Y + 0.5F, pill.Width - 1.0F, pill.Height - 1.0F), pill.Height / 2.0F),
              fill As New SolidBrush(If(row.Active, Theme.Accent, If(used, Theme.Surface, Theme.Disabled))), pen As New Pen(If(used, Theme.Accent, Theme.Disabled))
            g.FillPath(fill, path)
            g.DrawPath(pen, path)
        End Using
        OneLine(g, row.Role, Theme.FontCaption, If(row.Active, Color.White, If(used, Theme.Accent, Theme.TextMuted)), pill, True)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics, p = Theme.ControlPx(Me, 4), span = Math.Max(1, Width - 2 * p)
        g.SmoothingMode = SmoothingMode.AntiAlias
        LinkDrawing.DrawLabel(g, "Связь", Theme.FontGroup, Theme.TextMain, New Rectangle(p, 0, Theme.ControlPx(Me, 70), Theme.ControlPx(Me, 28)))
        Dim cornerRight = If(policy.Visible, policy.Left - Theme.ControlPx(Me, 10), Width - Theme.ControlPx(Me, 5))
        LinkDrawing.DrawLabel(g, If(Channels.Rows.Length = 0, Cell, PolicyText), Theme.FontCaption, Theme.TextMuted,
            New Rectangle(Theme.ControlPx(Me, 80), 0, Math.Max(1, cornerRight - Theme.ControlPx(Me, 80)), Theme.ControlPx(Me, 28)), ContentAlignment.MiddleRight)

        ' Итог: на связи ли сервер и через какой канал.
        Dim mark = Theme.ControlPx(Me, 15), markY = Theme.ControlPx(Me, 54), headColor = ToneDot(Channels.HeadTone)
        Using brush As New SolidBrush(Color.FromArgb(30, headColor))
            g.FillEllipse(brush, p, markY - mark, mark * 2, mark * 2)
        End Using
        If Channels.HeadTone = eLinkTone.Bad Then
            LinkDrawing.Cross(g, p + mark, markY, mark * 0.38F, headColor)
        Else
            LinkDrawing.DrawLabel(g, If(Channels.HeadTone = eLinkTone.Good, "✓", "·"), Theme.FontValueBig, headColor, New Rectangle(p, markY - mark, mark * 2, mark * 2), ContentAlignment.MiddleCenter)
        End If
        Dim headLeft = p + mark * 2 + Theme.ControlPx(Me, 10)
        Dim headWidth = Math.Min(Math.Max(1, p + span - headLeft), TextWidth(g, Channels.Headline, Theme.FontValueBig) + Theme.ControlPx(Me, 4))
        OneLine(g, Channels.Headline, Theme.FontValueBig, ToneText(Channels.HeadTone), New Rectangle(headLeft, markY - Theme.ControlPx(Me, 21), headWidth, Theme.ControlPx(Me, 42)))
        ' Пояснение только целиком: обрезанное «попытка через сот…» хуже, чем никакого (канал виден и по строкам ниже).
        Dim noteLeft = headLeft + headWidth + Theme.ControlPx(Me, 8), noteWidth = p + span - noteLeft
        If Channels.Note <> "" AndAlso TextWidth(g, Channels.Note, Theme.FontCaption) <= noteWidth Then
            OneLine(g, Channels.Note, Theme.FontCaption, Theme.TextMuted, New Rectangle(noteLeft, markY - Theme.ControlPx(Me, 21), noteWidth, Theme.ControlPx(Me, 42)))
        End If

        ' Каналы: значок с уровнем, имя, роль сразу за именем, оценка сигнала справа; состояние второй строкой.
        ' Неиспользуемый канал — серым целиком, причина — в строке состояния. Без состояния (активный канал
        ' без неполадок) строка одна и стоит посередине.
        Dim top = Theme.ControlPx(Me, RowsTop)
        For Each row In Channels.Rows
            Dim area As New Rectangle(p, top, span, Theme.ControlPx(Me, RowHeight))
            If row.Active Then
                Using back As New SolidBrush(Theme.SurfaceHover), accent As New SolidBrush(Theme.Accent)
                    g.FillRectangle(back, area)
                    g.FillRectangle(accent, area.Left, area.Top, Theme.ControlPx(Me, 3), area.Height)
                End Using
            End If
            Dim muted = Unused(row)
            Dim icon As New Rectangle(area.Left + Theme.ControlPx(Me, 12), area.Top + Theme.ControlPx(Me, 8), Theme.ControlPx(Me, 24), Theme.ControlPx(Me, 19))
            If row.Channel = eLinkChannel.Wifi Then
                LinkDrawing.WifiFan(g, icon, row.Level, muted)
            Else
                LinkDrawing.Bars(g, icon, Signal.Bars, Signal.Grade)
            End If
            ' Сотовая на связи — второй строкой её цифры («уровень −94 дБм · качество −9 дБ»), а не отдельной шкалой ниже.
            Dim state = row.State, stateColor = ToneText(row.Tone)
            If state = "" AndAlso row.Active AndAlso row.Channel = eLinkChannel.Cellular Then state = Details : stateColor = Theme.TextMuted
            Dim grade = RowGrade(row), noState = state = "", nameTop = area.Top + Theme.ControlPx(Me, If(noState, 12, 3))
            Dim value = If(grade < 0, "", LinkDiag.SignalTitle(grade).ToLowerInvariant())
            Dim rowLeft = area.Left + Theme.ControlPx(Me, 48), rowRight = area.Right - Theme.ControlPx(Me, 10)
            ' Строка сотовой открывает страницу «Связь»: стрелка — цветом ссылки, оценка — цветом оценки, как у Wi-Fi.
            Dim arrowWidth = If(row.Channel = eLinkChannel.Cellular, TextWidth(g, "→", Theme.FontBody), 0)
            If arrowWidth > 0 Then OneLine(g, "→", Theme.FontBody, Theme.Accent, New Rectangle(rowRight - arrowWidth, nameTop, arrowWidth, Theme.ControlPx(Me, 26)))
            Dim valueRight = rowRight - If(arrowWidth > 0, arrowWidth + Theme.ControlPx(Me, 6), 0)
            Dim valueWidth = If(value = "", 0, TextWidth(g, value, Theme.FontBody))
            If value <> "" Then OneLine(g, value, Theme.FontBody, If(muted, Theme.TextMuted, LinkDrawing.GradeTextColor(grade)), New Rectangle(valueRight - valueWidth, nameTop, valueWidth, Theme.ControlPx(Me, 26)))
            Dim rowFont = If(row.Active, nameBold, nameFont), pillWidth = TextWidth(g, row.Role, Theme.FontCaption) + Theme.ControlPx(Me, 18)
            Dim nameWidth = Math.Max(1, Math.Min(TextWidth(g, row.Name, rowFont),
                valueRight - valueWidth - Theme.ControlPx(Me, If(value = "", 0, 12)) - pillWidth - Theme.ControlPx(Me, 10) - rowLeft))
            OneLine(g, row.Name, rowFont, If(muted, Theme.TextMuted, Theme.TextMain), New Rectangle(rowLeft, nameTop, nameWidth, Theme.ControlPx(Me, 26)))
            DrawRole(g, New Rectangle(rowLeft + nameWidth + Theme.ControlPx(Me, 10), nameTop + Theme.ControlPx(Me, 3), pillWidth, Theme.ControlPx(Me, 20)), row)
            If Not noState Then OneLine(g, state, Theme.FontBody, stateColor, New Rectangle(rowLeft, area.Top + Theme.ControlPx(Me, 28), Math.Max(1, rowRight - rowLeft), Theme.ControlPx(Me, 20)))
            top += Theme.ControlPx(Me, RowPitch)
        Next

        ' Путь до сервера по активному каналу: крест — место обрыва. Шаг не шире 170 точек и от левого края, как строки выше.
        Dim chainTop = Theme.ControlPx(Me, ChainDesign), labels = Channels.Labels, count = labels.Length
        OneLine(g, "Путь до сервера" & If(Channels.Via = "", "", " через " & Channels.Via), Theme.FontCaption, Theme.TextMuted, New Rectangle(p, chainTop, span, Theme.ControlPx(Me, 20)))
        Dim chainSpan = Math.Min(span, Theme.ControlPx(Me, 170) * count), chainLeft = p
        For i = 0 To count - 1
            Dim x = chainLeft + chainSpan * (2 * i + 1) \ (2 * count), y = chainTop + Theme.ControlPx(Me, 50), radius = Theme.ControlPx(Me, 16)
            Dim color = If(Steps < 0 OrElse i > Steps, Theme.Idle, If(i < Steps, Theme.Ok, Theme.Danger))
            If i > 0 Then
                Using pen As New Pen(If(i < Steps, Theme.Ok, If(i = Steps, Theme.Danger, Theme.Border)), Theme.ControlPx(Me, 2))
                    g.DrawLine(pen, x - chainSpan \ count + radius + Theme.ControlPx(Me, 8), y, x - radius - Theme.ControlPx(Me, 8), y)
                End Using
                If i = Steps Then LinkDrawing.Cross(g, x - chainSpan \ (2 * count), y, Theme.ControlPx(Me, 4), Theme.Danger)
            End If
            Using brush As New SolidBrush(Color.FromArgb(30, color))
                g.FillEllipse(brush, x - radius, y - radius, radius * 2, radius * 2)
            End Using
            If i = Steps Then
                LinkDrawing.Cross(g, x, y, radius * 0.38F, color)
            Else
                LinkDrawing.DrawLabel(g, If(Steps < 0 OrElse i > Steps, "·", "✓"), Theme.FontGroup, color, New Rectangle(x - radius, y - radius, radius * 2, radius * 2), ContentAlignment.MiddleCenter)
            End If
            LinkDrawing.DrawLabel(g, labels(i), Theme.FontBody, Theme.TextMain, New Rectangle(x - chainSpan \ (2 * count), chainTop + Theme.ControlPx(Me, 72), chainSpan \ count, Theme.ControlPx(Me, 24)), ContentAlignment.MiddleCenter)
        Next
        LinkDrawing.DrawLabel(g, Problem, Theme.FontBody, If(Steps < 0, Theme.TextMuted, Theme.Danger), New Rectangle(p, chainTop + Theme.ControlPx(Me, 100), span, Theme.ControlPx(Me, 44)))

        If PanelVisible Then
            Dim panel As New Rectangle(p, SignalTop, span, Theme.ControlPx(Me, PanelHeight))
            Using brush As New SolidBrush(Theme.WindowBack)
                g.FillRectangle(brush, panel)
            End Using
            LinkDrawing.Bars(g, New Rectangle(p + Theme.ControlPx(Me, 12), panel.Top + Theme.ControlPx(Me, 27), Theme.ControlPx(Me, 40), Theme.ControlPx(Me, 38)), Signal.Bars, Signal.Grade)
            ' Мини-графика нет: без шкалы и времени он ничего не говорит, полный график — на странице «Связь».
            Dim infoWidth = Math.Max(Theme.ControlPx(Me, 100), span - Theme.ControlPx(Me, 205))
            OneLine(g, LinkDiag.SignalTitle(Signal.Grade), nameBold, Theme.TextMain, New Rectangle(p + Theme.ControlPx(Me, 66), panel.Top + Theme.ControlPx(Me, 9), infoWidth, Theme.ControlPx(Me, 27)))
            LinkDrawing.DrawLabel(g, Details, Theme.FontBody, Theme.TextMuted, New Rectangle(p + Theme.ControlPx(Me, 66), panel.Top + Theme.ControlPx(Me, 37), infoWidth, Theme.ControlPx(Me, 46)))
            LinkDrawing.DrawLabel(g, "Подробнее →", Theme.FontBody, Theme.Accent, New Rectangle(Width - Theme.ControlPx(Me, 136), panel.Top + Theme.ControlPx(Me, 29), Theme.ControlPx(Me, 130), Theme.ControlPx(Me, 32)), ContentAlignment.MiddleRight)
        End If
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        Cursor = If(MoreArea.Contains(e.Location), Cursors.Hand, Cursors.Default)
    End Sub

    Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
        MyBase.OnMouseClick(e)
        If e.Button = MouseButtons.Left AndAlso MoreArea.Contains(e.Location) Then RaiseEvent More(Me, EventArgs.Empty)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then nameFont.Dispose() : nameBold.Dispose()
        MyBase.Dispose(disposing)
    End Sub
End Class
