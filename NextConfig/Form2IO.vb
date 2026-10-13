' Блок «Входы и выходы» на главной: одна строка на канал — номер, назначение, состояние, проверка.
' Решение владельца 13.10.2026: выключенное серым (норма), включённое зелёным, красный — только неисправность.
Partial Public Class Form2
    ' Порядок строк: выход 1, выход 2, вход 1, вход 2.
    Private ReadOnly ioPurpose As Label() = {New Label, New Label, New Label, New Label}
    ' Под выходом — почему проверка не сработала или выключена в настройках.
    Private ReadOnly ioNote As Label() = {New Label With {.Name = "ioNote1"}, New Label With {.Name = "ioNote2"}}
    Private ReadOnly ioTestUntil As Date() = {Date.MinValue, Date.MinValue}
    Private ReadOnly ioRefusal As String() = {"", ""}
    Private ReadOnly ioRefusalUntil As Date() = {Date.MinValue, Date.MinValue}

    Private Function IoBlock() As Control
        Dim grid As New TableLayoutPanel With {.AutoSize = True, .ColumnCount = 4, .Anchor = AnchorStyles.Left Or AnchorStyles.Right,
            .Margin = New Padding(0), .Name = "ioGrid"}
        grid.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, Theme.Px(96)))
        grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        grid.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        Dim names = {"Выход 1", "Выход 2", "Вход 1", "Вход 2"}
        Dim states As Control() = {DotPair(dotOUT1, lblOUT1), DotPair(dotOUT2, lbOUT2), DotPair(dotIN1, lbIN1), DotPair(dotIN2, lblIN2)}
        Dim buttons As Button() = {btnTestOUT1, btnTestOUT2, Nothing, Nothing}
        Dim row = 0
        For i = 0 To 3
            Dim rowMargin = New Padding(0, If(i = 0, 0, Theme.Px(10)), Theme.GapRelated, Theme.Px(10))
            Dim name As New Label With {.Text = names(i), .AutoSize = True, .Font = Theme.FontBodyBold, .ForeColor = Theme.TextMain, .Margin = rowMargin,
                .Anchor = AnchorStyles.Left}
            ioPurpose(i).AutoSize = True : ioPurpose(i).Font = Theme.FontBody : ioPurpose(i).ForeColor = Theme.TextMuted
            ioPurpose(i).Margin = rowMargin : ioPurpose(i).Anchor = AnchorStyles.Left
            states(i).Margin = rowMargin : states(i).Anchor = AnchorStyles.Left
            grid.Controls.Add(name, 0, row) : grid.Controls.Add(ioPurpose(i), 1, row) : grid.Controls.Add(states(i), 2, row)
            If buttons(i) IsNot Nothing Then
                buttons(i).Text = "Проверить" : buttons(i).Anchor = AnchorStyles.Right : buttons(i).Margin = New Padding(0, rowMargin.Top - Theme.Px(4), 0, 0)
                grid.Controls.Add(buttons(i), 3, row)
                row += 1
                ioNote(i).AutoSize = True : ioNote(i).Font = Theme.FontCaption : ioNote(i).ForeColor = LinkDrawing.AmberText
                ioNote(i).Margin = New Padding(0, 0, 0, Theme.Px(6)) : ioNote(i).Visible = False
                grid.Controls.Add(ioNote(i), 1, row) : grid.SetColumnSpan(ioNote(i), 3)
            End If
            row += 1
        Next
        Return grid
    End Function

    Private Function OutParam(n As Integer) As clOpros.s_outparam
        Return If(n = 0, onBoard.out1, onBoard.out2)
    End Function

    Private Function OutPurpose(n As Integer) As String
        If Not HasConfigSnapshot() Then Return ""
        Dim p = OutParam(n)
        If p.unBlockUse <> 0 Then Return "Разблокировка"
        If p.outMode = 1 Then Return "Открытие · уровень"
        Return "Открытие · " & If(p.impulse = 0, "импульс не задан", "импульс " & p.impulse & " с")
    End Function

    ' Функции входов в прошивке пока только «датчик ворот» (8); остальное — номер функции.
    Private Function InFunction(n As Integer) As Byte
        Return If(n = 0, onBoard.in1Func, onBoard.in2Func)
    End Function

    Private Function InPurpose(n As Integer) As String
        If Not HasConfigSnapshot() Then Return ""
        Dim f = InFunction(n)
        Return If(f = 0, "Функция не задана", If(f = 8, "Датчик ворот", "Функция " & f))
    End Function

    ' Причина, по которой плата не выполнит проверку, — видна до нажатия.
    Private Function OutBlocked(n As Integer) As String
        If Not HasConfigSnapshot() Then Return ""
        Dim p = OutParam(n)
        If p.onServerCmd = 0 Then Return "Проверка выключена: в настройках выхода не включено «Команда сервера»"
        If p.outMode <> 1 AndAlso p.impulse = 0 Then Return "Проверка не сработает: в настройках выхода не задано время импульса"
        Return ""
    End Function

    Private Sub UpdateIo()
        Dim outs = {status.out1, status.out2}
        Dim ins = {status.in1, status.in2}
        Dim outLabels = {lblOUT1, lbOUT2}, outDots = {dotOUT1, dotOUT2}
        Dim inLabels = {lbIN1, lblIN2}, inDots = {dotIN1, dotIN2}
        For n = 0 To 1
            Theme.SetText(ioPurpose(n), OutPurpose(n))
            IoState(outDots(n), outLabels(n), outs(n), "Включён", "Выключен")
            Theme.SetText(ioPurpose(2 + n), InPurpose(n))
            Dim gate = HasConfigSnapshot() AndAlso InFunction(n) = 8
            IoState(inDots(n), inLabels(n), ins(n), If(gate, "Ворота закрыты", "Активен"), If(gate, "Ворота открыты", "Не активен"))
        Next
        UpdateIoButtons()
    End Sub

    ' Выключенное — серая точка и приглушённый текст: это обычное состояние, не авария.
    Private Sub IoState(dot As Label, text As Label, isOn As Boolean, onText As String, offText As String)
        SetDot(dot, text, isOn, onText, offText)
        If Not isOn Then Theme.SetColor(text, Theme.TextMuted)
    End Sub

    ' Раз в секунду: отсчёт проверки на кнопке и подсказки под выходами.
    Private Sub UpdateIoButtons()
        Dim buttons = {btnTestOUT1, btnTestOUT2}
        Dim outs = {status.out1, status.out2}
        For n = 0 To 1
            Dim left = (ioTestUntil(n) - Date.Now).TotalSeconds
            Dim level = HasConfigSnapshot() AndAlso OutParam(n).outMode = 1
            Dim text = If(left > 0, "Идёт… " & Math.Ceiling(left).ToString("0") & " с",
                If(level, If(outs(n), "Выключить", "Включить"), "Проверить"))
            If buttons(n).Text <> text Then buttons(n).Text = text
            Dim note = If(Date.Now < ioRefusalUntil(n), ioRefusal(n), OutBlocked(n))
            Theme.SetText(ioNote(n), note)
            ioNote(n).Visible = note <> ""
        Next
    End Sub

    ' Ответ платы на проверку: 0 — выполнено, 1 — ошибка параметра, 2 — не выполнено; -1 — нет ответа.
    Private Sub ShowOutTestResult(n As Integer, code As Integer)
        If code = 0 Then
            Dim p = OutParam(n)
            If p.outMode <> 1 Then ioTestUntil(n) = Date.Now.AddSeconds(Math.Max(1, CInt(p.impulse)))
            ioRefusalUntil(n) = Date.MinValue
        ElseIf code > 0 Then
            Dim blocked = OutBlocked(n)
            ioRefusal(n) = If(blocked <> "", blocked.Replace("Проверка выключена", "Не сработал").Replace("Проверка не сработает", "Не сработал"),
                "Не сработал: выход занят — пауза между срабатываниями, разблокировка или обновление прошивки")
            ioRefusalUntil(n) = Date.Now.AddSeconds(10)
        End If
        UpdateIoButtons()
    End Sub
End Class
