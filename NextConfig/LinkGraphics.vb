Imports System.Drawing.Drawing2D

Public Structure LinkPoint
    Public Seconds As Double
    Public EndSeconds As Double
    Public Rsrp As Byte
End Structure

Public Class LinkHistory
    Private ReadOnly samples As New List(Of LinkPoint)
    Public LatestSeconds As Double
    Public Sub Clear()
        samples.Clear() : LatestSeconds = 0
    End Sub
    Public ReadOnly Property Points As IReadOnlyList(Of LinkPoint)
        Get
            Return samples
        End Get
    End Property

    Public Sub Observe(seconds As Double, rsrp As Byte)
        Dim old = samples.TakeWhile(Function(p) p.EndSeconds < seconds - 600).Count()
        If old > 0 Then samples.RemoveRange(0, old)
        If samples.Count > 0 AndAlso samples(0).Seconds < seconds - 600 Then
            Dim first = samples(0)
            first.Seconds = seconds - 600 : samples(0) = first
        End If
        If rsrp = 255 Then Return
        LatestSeconds = seconds
        If samples.Count = 0 OrElse samples.Last().Rsrp <> rsrp Then
            samples.Add(New LinkPoint With {.Seconds = seconds, .EndSeconds = seconds, .Rsrp = rsrp})
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
        Dim ymin = LinkDiag.RsrpMinimum, ymax = LinkDiag.RsrpMaximum, window = PlotWindow(history, now)
        If zones Then
            Dim limits = New Integer() {ymin, LinkDiag.RsrpPoorThreshold, LinkDiag.RsrpGoodThreshold, ymax}
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
            Dim y = CSng(bounds.Bottom - Math.Max(0, Math.Min(ymax - ymin, -CInt(sample.Rsrp) - ymin)) / CDbl(ymax - ymin) * bounds.Height)
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
                Dim label = "−" & history.Points.Last().Rsrp & " дБм", w = Theme.DrawingPx(g, 80)
                DrawLabel(g, label, Theme.FontBodyBold, Theme.TextMain,
                    New Rectangle(CInt(last.X) - w - Theme.DrawingPx(g, 6), CInt(last.Y) - Theme.DrawingPx(g, 26), w, Theme.DrawingPx(g, 22)), ContentAlignment.MiddleRight)
            End If
        End Using
    End Sub
End Module

Public Class SignalGauge
    Inherits Control
    Public Quality As Boolean
    Public Raw As Byte = 255

    Public Sub New()
        SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Height = Theme.ControlPx(Me, 168) : Dock = DockStyle.Top : BackColor = Theme.Surface
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics, pad = Theme.ControlPx(Me, 6), barY = Theme.ControlPx(Me, 93), barHeight = Theme.ControlPx(Me, 10)
        ' Монтажнику — что значит шкала, без терминов.
        LinkDrawing.DrawLabel(g, If(Quality, "Насколько чистый сигнал: мешают соседние вышки и загрузка", "Насколько сильно слышна вышка"),
            Theme.FontCaption, Theme.TextMuted, New Rectangle(pad, barY + Theme.ControlPx(Me, 44), Width - 2 * pad, Theme.ControlPx(Me, 24)))
        Dim minimum = If(Quality, LinkDiag.RsrqMinimum, LinkDiag.RsrpMinimum), maximum = If(Quality, LinkDiag.RsrqMaximum, LinkDiag.RsrpMaximum)
        Dim grade = If(Quality, LinkDiag.RsrqGrade(Raw), LinkDiag.RsrpGrade(Raw))
        LinkDrawing.DrawLabel(g, If(Quality, "Качество (RSRQ)", "Уровень (RSRP)"), Theme.FontBody, Theme.TextMuted, New Rectangle(pad, 0, Width - 2 * pad, Theme.ControlPx(Me, 24)))
        LinkDrawing.DrawLabel(g, If(Raw = 255, "—", "−" & Raw) & If(Quality, " дБ", " дБм"), Theme.FontValueBig, Theme.TextMain, New Rectangle(pad, Theme.ControlPx(Me, 29), Width - Theme.ControlPx(Me, 110), Theme.ControlPx(Me, 48)))
        Dim badge As New Rectangle(Width - Theme.ControlPx(Me, 100), Theme.ControlPx(Me, 35), Theme.ControlPx(Me, 94), Theme.ControlPx(Me, 30))
        Using brush As New SolidBrush(LinkDrawing.GradeBackColor(grade))
            g.FillRectangle(brush, badge)
        End Using
        LinkDrawing.DrawLabel(g, LinkDiag.GradeText(grade), Theme.FontBody, LinkDrawing.GradeTextColor(grade), badge, ContentAlignment.MiddleCenter)
        Dim span = Math.Max(1, Width - 2 * pad)
        Dim limits = If(Quality, New Integer() {minimum, LinkDiag.RsrqPoorThreshold, LinkDiag.RsrqGoodThreshold, maximum}, New Integer() {minimum, LinkDiag.RsrpPoorThreshold, LinkDiag.RsrpGoodThreshold, maximum})
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
    Private ReadOnly titleFont As New Font(Theme.FontBody.FontFamily, Theme.FontBody.Size * 1.2F, FontStyle.Bold)
    Private ReadOnly detailFont As New Font(Theme.FontBody.FontFamily, Theme.FontBody.Size * 0.975F, FontStyle.Regular)
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
        LinkDrawing.DrawLabel(g, If(Version >= 5, "точка раз в 2 с", "точка раз в 30 с"), Theme.FontCaption, Theme.TextMuted, New Rectangle(0, 0, Width, Theme.ControlPx(Me, 25)), ContentAlignment.MiddleRight)
        LinkDrawing.Plot(g, area, History, Seconds, True)
        If History.Points.Count = 0 Then LinkDrawing.DrawLabel(g, "собираю данные…", Theme.FontCaption, Theme.TextMuted, area, ContentAlignment.MiddleCenter)
        For Each mark In New Integer() {LinkDiag.RsrpMaximum, LinkDiag.RsrpGoodThreshold, LinkDiag.RsrpPoorThreshold, LinkDiag.RsrpMinimum}
            Dim y = area.Bottom - CInt((mark - LinkDiag.RsrpMinimum) / CDbl(LinkDiag.RsrpMaximum - LinkDiag.RsrpMinimum) * area.Height)
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

Public Class LinkOverview
    Inherits Control
    Public Signal As LinkSignalView
    Public Steps As Integer = -1
    Public Problem As String = "Ожидание данных"
    Public Cell As String = ""
    Public NetworkLabel As String = "Сеть"
    Public Details As String = "Нет свежих данных"
    Public MemoryUsed As UInteger
    Public History As LinkHistory
    Public Seconds As Double
    Public Event More As EventHandler

    Public Sub New()
        SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Height = Theme.ControlPx(Me, 255) : Dock = DockStyle.Top : BackColor = Theme.Surface
        Signal = LinkDiag.SignalView(Nothing, 99)
    End Sub

    Private ReadOnly Property SignalTop As Integer
        Get
            Return Theme.ControlPx(Me, If(Problem = "", 120, 164))
        End Get
    End Property

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics, p = Theme.ControlPx(Me, 4), span = Math.Max(1, Width - 2 * p)
        g.SmoothingMode = SmoothingMode.AntiAlias
        LinkDrawing.DrawLabel(g, "Связь", Theme.FontGroup, Theme.TextMain, New Rectangle(p, 0, Theme.ControlPx(Me, 70), Theme.ControlPx(Me, 28)))
        LinkDrawing.DrawLabel(g, Cell, Theme.FontCaption, Theme.TextMuted, New Rectangle(Theme.ControlPx(Me, 80), 0, Math.Max(1, Width - Theme.ControlPx(Me, 85)), Theme.ControlPx(Me, 28)), ContentAlignment.MiddleRight)
        Dim labels = New String() {"SIM", NetworkLabel, "Интернет", "Сервер"}
        For i = 0 To 3
            Dim x = p + span * (2 * i + 1) \ 8, y = Theme.ControlPx(Me, 62), radius = Theme.ControlPx(Me, 17)
            Dim color = If(Steps < 0 OrElse i > Steps, Theme.Idle, If(i < Steps, Theme.Ok, Theme.Danger))
            If i > 0 Then
                Using pen As New Pen(If(i < Steps, Theme.Ok, If(i = Steps, Theme.Danger, Theme.Border)), Theme.ControlPx(Me, 2))
                    g.DrawLine(pen, x - span \ 4 + radius + Theme.ControlPx(Me, 9), y, x - radius - Theme.ControlPx(Me, 9), y)
                End Using
                If i = Steps Then LinkDrawing.DrawLabel(g, "×", Theme.FontGroup, Theme.Danger, New Rectangle(x - span \ 8 - Theme.ControlPx(Me, 8), y - Theme.ControlPx(Me, 12), Theme.ControlPx(Me, 16), Theme.ControlPx(Me, 24)), ContentAlignment.MiddleCenter)
            End If
            Using brush As New SolidBrush(Color.FromArgb(30, color))
                g.FillEllipse(brush, x - radius, y - radius, radius * 2, radius * 2)
            End Using
            LinkDrawing.DrawLabel(g, If(Steps < 0 OrElse i > Steps, "·", If(i < Steps, "✓", "×")), Theme.FontValueBig, color, New Rectangle(x - radius, y - radius, radius * 2, radius * 2), ContentAlignment.MiddleCenter)
            LinkDrawing.DrawLabel(g, labels(i), Theme.FontCaption, Theme.TextMain, New Rectangle(x - span \ 8, Theme.ControlPx(Me, 86), span \ 4, Theme.ControlPx(Me, 24)), ContentAlignment.MiddleCenter)
        Next
        LinkDrawing.DrawLabel(g, Problem, Theme.FontCaption, If(Steps < 0, Theme.TextMuted, Theme.Danger), New Rectangle(p, Theme.ControlPx(Me, 116), span, Theme.ControlPx(Me, 42)))
        Dim panel As New Rectangle(p, SignalTop, span, Theme.ControlPx(Me, 80))
        Using brush As New SolidBrush(Theme.WindowBack)
            g.FillRectangle(brush, panel)
        End Using
        LinkDrawing.Bars(g, New Rectangle(p + Theme.ControlPx(Me, 10), panel.Top + Theme.ControlPx(Me, 25), Theme.ControlPx(Me, 36), Theme.ControlPx(Me, 35)), Signal.Bars, Signal.Grade)
        Dim hasGraph = Signal.Lte AndAlso History IsNot Nothing AndAlso History.Points.Count > 0
        Dim textWidth = Math.Max(Theme.ControlPx(Me, 100), span - Theme.ControlPx(Me, If(hasGraph, 270, 170)))
        LinkDrawing.DrawLabel(g, LinkDiag.SignalTitle(Signal.Grade), Theme.FontBodyBold, Theme.TextMain, New Rectangle(p + Theme.ControlPx(Me, 58), panel.Top + Theme.ControlPx(Me, 8), textWidth, Theme.ControlPx(Me, 25)))
        LinkDrawing.DrawLabel(g, Details, Theme.FontCaption, Theme.TextMuted, New Rectangle(p + Theme.ControlPx(Me, 58), panel.Top + Theme.ControlPx(Me, 33), textWidth, Theme.ControlPx(Me, 42)))
        If hasGraph Then LinkDrawing.Plot(g, New Rectangle(Width - Theme.ControlPx(Me, 205), panel.Top + Theme.ControlPx(Me, 20), Theme.ControlPx(Me, 95), Theme.ControlPx(Me, 40)), History, Seconds, False)
        LinkDrawing.DrawLabel(g, "Подробнее →", Theme.FontCaption, Theme.Accent, New Rectangle(Width - Theme.ControlPx(Me, 100), panel.Top + Theme.ControlPx(Me, 25), Theme.ControlPx(Me, 95), Theme.ControlPx(Me, 30)), ContentAlignment.MiddleRight)
        Dim memoryY = panel.Bottom + Theme.ControlPx(Me, 10)
        LinkDrawing.DrawLabel(g, "Память идентификаторов", Theme.FontCaption, Theme.TextMuted, New Rectangle(p, memoryY, span \ 2, Theme.ControlPx(Me, 24)))
        LinkDrawing.DrawLabel(g, MemoryUsed.ToString("N0", Globalization.CultureInfo.GetCultureInfo("ru-RU")) & " из 50 000 · " & Math.Min(100, MemoryUsed * 100UL \ 50000).ToString() & " %", Theme.FontCaption, Theme.TextMain, New Rectangle(p + span \ 2, memoryY, span \ 2, Theme.ControlPx(Me, 24)), ContentAlignment.MiddleRight)
        Using back As New SolidBrush(Theme.Border), fill As New SolidBrush(Theme.Accent)
            g.FillRectangle(back, p, memoryY + Theme.ControlPx(Me, 30), span, Theme.ControlPx(Me, 5))
            g.FillRectangle(fill, p, memoryY + Theme.ControlPx(Me, 30), CSng(span * Math.Min(1, MemoryUsed / 50000.0)), Theme.ControlPx(Me, 5))
        End Using
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        Cursor = If(e.Y >= SignalTop AndAlso e.Y < SignalTop + Theme.ControlPx(Me, 80), Cursors.Hand, Cursors.Default)
    End Sub

    Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
        MyBase.OnMouseClick(e)
        If e.Button = MouseButtons.Left AndAlso e.Y >= SignalTop AndAlso e.Y < SignalTop + Theme.ControlPx(Me, 80) Then RaiseEvent More(Me, EventArgs.Empty)
    End Sub
End Class
