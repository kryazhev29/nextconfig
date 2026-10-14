Imports System.Drawing.Drawing2D

' Контурные значки меню: сетка 24x24, перерисовка в текущем DPI.
Public Module NavIcons
    Public Sub Draw(g As Graphics, bounds As Rectangle, section As String, color As Color)
        Dim saved = g.Save()
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.TranslateTransform(bounds.Left, bounds.Top)
        g.ScaleTransform(bounds.Width / 24.0F, bounds.Height / 24.0F)
        Using pen As New Pen(color, 1.65F) With {.StartCap = LineCap.Round, .EndCap = LineCap.Round, .LineJoin = LineJoin.Round}
            Select Case section
                Case "Устройство"
                    g.DrawRectangle(pen, 3, 4, 17, 12)
                    g.DrawLine(pen, 9, 20, 15, 20)
                    g.DrawLine(pen, 12, 16, 12, 20)
                    g.DrawRectangle(pen, 16, 12, 5, 9)
                Case "Связь"
                    g.DrawEllipse(pen, 10, 10, 4, 4)
                    g.DrawArc(pen, 6, 6, 12, 12, 140, 80)
                    g.DrawArc(pen, 6, 6, 12, 12, -40, 80)
                    g.DrawArc(pen, 2, 2, 20, 20, 140, 80)
                    g.DrawArc(pen, 2, 2, 20, 20, -40, 80)
                    g.DrawLine(pen, 12, 14, 12, 22)
                    g.DrawLine(pen, 9, 22, 15, 22)
                Case "Пользователи"
                    g.DrawEllipse(pen, 5, 3, 8, 8)
                    g.DrawArc(pen, 2, 12, 14, 16, 180, 180)
                    g.DrawArc(pen, 11, 3, 8, 8, -75, 150)
                    g.DrawArc(pen, 10, 12, 12, 16, -90, 90)
                Case "Настройки"
                    Dim points(31) As PointF
                    For i = 0 To points.Length - 1
                        Dim angle = (i * 11.25 - 5.625) * Math.PI / 180
                        Dim radius = If(i Mod 4 < 2, 10.0F, 7.8F)
                        points(i) = New PointF(12 + CSng(Math.Cos(angle)) * radius, 12 + CSng(Math.Sin(angle)) * radius)
                    Next
                    g.DrawPolygon(pen, points)
                    g.DrawEllipse(pen, 8.5F, 8.5F, 7, 7)
                Case "Сервис"
                    g.DrawLines(pen, New PointF() {New PointF(14, 3), New PointF(11, 6), New PointF(12, 10),
                        New PointF(3, 19), New PointF(5, 21), New PointF(14, 12), New PointF(18, 13),
                        New PointF(21, 10), New PointF(21, 6), New PointF(17, 9), New PointF(15, 7), New PointF(18, 3), New PointF(14, 3)})
                Case "Расписания"
                    g.DrawRectangle(pen, 3, 5, 18, 16)
                    g.DrawLine(pen, 3, 10, 21, 10)
                    g.DrawLine(pen, 8, 2, 8, 7) : g.DrawLine(pen, 16, 2, 16, 7)
                    g.DrawLine(pen, 8, 14, 10, 14) : g.DrawLine(pen, 14, 14, 16, 14)
                Case Else ' Журнал
                    g.DrawRectangle(pen, 6, 3, 14, 18)
                    For Each y In New Integer() {7, 12, 17}
                        g.DrawLine(pen, 3, y, 7, y)
                    Next
                    g.DrawLine(pen, 10, 8, 16, 8)
                    g.DrawLine(pen, 10, 12, 16, 12)
                    g.DrawLine(pen, 10, 16, 14, 16)
            End Select
        End Using
        g.Restore(saved)
    End Sub
End Module
