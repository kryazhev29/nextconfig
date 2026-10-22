Imports System.Drawing
Imports System.Windows.Forms

' Сообщения программы в нижней строке окна вместо всплывающих окон
' (просьба Алексея 21.10.2026: окно, ушедшее на задний план, запирало программу).
' Новое сообщение 2 секунды показывается белым по тёмно-синему, потом строка
' возвращается к обычному виду, а текст остаётся до следующего сообщения.
' Вопросы и ошибки, где нужно решение оператора, остаются диалогами frmInfo.
Public Class StatusMessage
    Public Const FlashMs As Integer = 2000

    Private ReadOnly bar As Control
    Private ReadOnly message As Label
    Private ReadOnly neighbours As Label()
    Private ReadOnly neighbourColors As Color()
    Private ReadOnly normalBack As Color
    Private ReadOnly timer As New Timer With {.Interval = FlashMs}
    Private ReadOnly tip As New ToolTip
    Private failed As Boolean

    ' Во время вспышки соседние подписи строки тоже белые. Подписи, которые
    ' сами меняют цвет (состояние сохранения), сюда не передавать - форма прячет их по FlashChanged.
    Public Event FlashChanged As EventHandler
    Public ReadOnly Property Flashing As Boolean
    ' Высота одной строки сообщения: подпись с Dock = Fill свою высоту теряет.
    Public ReadOnly Property LineHeight As Integer

    Public Sub New(bar As Control, message As Label, ParamArray neighbours As Label())
        Me.bar = bar
        Me.message = message
        Me.neighbours = neighbours
        Me.neighbourColors = neighbours.Select(Function(l) l.ForeColor).ToArray()
        Me.normalBack = bar.BackColor
        message.AutoSize = False
        message.AutoEllipsis = True
        message.TextAlign = ContentAlignment.MiddleLeft
        LineHeight = TextRenderer.MeasureText("Ag", message.Font).Height + Theme.Px(4)
        message.Height = LineHeight
        AddHandler timer.Tick, Sub() EndFlash()
        AddHandler bar.Disposed, Sub()
                                     timer.Dispose()
                                     tip.Dispose()
                                 End Sub
    End Sub

    ' Строка как в образце Алексея (почтовая программа, 22.10.2026): «дата, время: операция - итог».
    ' Многострочный текст собирается в одну строку; полный текст - в подсказке при наведении.
    Public Shared Function Line(title As String, text As String, time As DateTime) As String
        Dim body = String.Join(" ", text.Split({vbCr, vbLf}, StringSplitOptions.RemoveEmptyEntries).Select(Function(s) s.Trim()))
        Return time.ToString("dd.MM.yyyy, HH:mm:ss", Globalization.CultureInfo.InvariantCulture) & ": " &
            If(String.IsNullOrEmpty(title), "", title & " - ") & body
    End Function

    Public Sub Show(title As String, text As String, Optional isError As Boolean = False)
        If bar.IsDisposed Then Return
        If bar.InvokeRequired Then
            bar.BeginInvoke(New Action(Sub() Show(title, text, isError)))
            Return
        End If
        AgentLog.Message(title, text, False)
        message.Text = Line(title, text, DateTime.Now)
        tip.SetToolTip(message, text)
        failed = isError
        timer.Stop()
        bar.BackColor = Theme.BrandEnd
        message.ForeColor = Color.White
        For Each l In neighbours
            l.ForeColor = Color.White
        Next
        _Flashing = True
        RaiseEvent FlashChanged(Me, EventArgs.Empty)
        timer.Start()
    End Sub

    Private Sub EndFlash()
        timer.Stop()
        If bar.IsDisposed Then Return
        bar.BackColor = normalBack
        message.ForeColor = If(failed, Theme.Danger, Theme.TextMain)
        For i = 0 To neighbours.Length - 1
            neighbours(i).ForeColor = neighbourColors(i)
        Next
        _Flashing = False
        RaiseEvent FlashChanged(Me, EventArgs.Empty)
    End Sub
End Class
