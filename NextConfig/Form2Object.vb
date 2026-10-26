' Привязка контроллера к объекту: карточка «Объект» в разделе «Сервис».
' Алексей 26.10.2026: отображение привязки нужно — запрашивать командой по кнопке «Обновить»,
' отвязывать кнопкой «Отвязать». Протокол2 стр. 106–107: конфигуратор шлёт серверу 17 (запрос)
' и 18 (отвязка) без регистрации, ответ 25: код 1 — нет в базе, 2 — не привязан, 3 — привязан
' (+ номер объекта 8 Б, наименование 128, контроллер 128, телефон администратора 16).
' Адрес — сервер 1 из настроек платы (порт по умолчанию 15010), а не зашитый адрес старой системы.
Partial Public Class Form2
    Private ReadOnly lblObjNum As New Label With {.Name = "lblObjNum", .Text = "---", .AutoSize = True}
    Private ReadOnly lblObjName As New Label With {.Name = "lblObjName", .Text = "---", .AutoSize = True}
    Private ReadOnly lblObjController As New Label With {.Name = "lblObjController", .Text = "---", .AutoSize = True}
    Private ReadOnly lblAdmin As New Label With {.Name = "lblAdmin", .Text = "---", .AutoSize = True}
    Private ReadOnly objectState As New Label With {.Name = "objectState", .AutoSize = True}
    Private WithEvents btnUpdateObj As New Button With {.Name = "btnUpdateObj", .Text = "Обновить с сервера"}
    Private WithEvents btnUnConnect As New Button With {.Name = "btnUnConnect", .Text = "Отвязать от объекта"}

    Private Function ObjectCard() As Control
        For Each l In {lblObjNum, lblObjName, lblObjController, lblAdmin}
            Theme.Value(l)
        Next
        Theme.DotText(objectState, Theme.eDot.Idle, "Нажмите «Обновить с сервера» — сведения запрашиваются у сервера 1 из настроек платы.")
        btnUpdateObj.Width = Theme.Px(200) : btnUpdateObj.Margin = New Padding(0, 0, Theme.GapRelated, 0)
        Theme.Secondary(btnUpdateObj) : Theme.Small(btnUpdateObj)
        btnUnConnect.Width = Theme.Px(200)
        Theme.DangerOutline(btnUnConnect) : Theme.Small(btnUnConnect)
        Dim actions As New FlowLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .WrapContents = False, .Margin = New Padding(0, Theme.GapBlock, 0, 0)}
        actions.Controls.Add(btnUpdateObj) : actions.Controls.Add(btnUnConnect)
        Return CardBlock("Объект",
                         Row("Уникальный номер", lblObjNum),
                         Row("Наименование", lblObjName),
                         Row("Контроллер на объекте", lblObjController),
                         Row("Администратор", lblAdmin),
                         objectState, actions,
                         Hint("Сведения приходят с сервера. «Отвязать от объекта» снимает привязку на сервере — назад само не вернётся."))
    End Function

    ' Сервер 1 из настроек платы: «узел» или «узел:порт»; без порта — 15010, как в прошивке.
    Private Function ObjectServer() As Tuple(Of String, Integer)
        Dim address = If(HasConfigSnapshot(), onBoard.server1, "").Trim()
        If address = "" Then Return Nothing
        Dim parts = address.Split(":"c)
        Dim port = 15010
        If parts.Length > 2 OrElse (parts.Length = 2 AndAlso Not Integer.TryParse(parts(1), port)) Then Return Nothing
        Return Tuple.Create(parts(0), port)
    End Function

    Private Async Sub btnUpdateObj_Click(sender As Object, e As EventArgs) Handles btnUpdateObj.Click
        Await QueryObject(17)
    End Sub

    Private Async Sub btnUnConnect_Click(sender As Object, e As EventArgs) Handles btnUnConnect.Click
        Using f As New frmInfo("Отвязка от объекта", "Вы уверены, что хотите отвязать контроллер от объекта?",, True)
            f.Button1.Text = "Отвязать"
            f.Button2.Text = "Отмена"
            If f.ShowDialog(Me) <> DialogResult.OK Then Return
        End Using
        Await QueryObject(18)
    End Sub

    Private Async Function QueryObject(cmd As Byte) As Threading.Tasks.Task
        Dim imei = lblIMEI.Text.Trim()
        Dim server = ObjectServer()
        If server Is Nothing Then
            Theme.DotText(objectState, Theme.eDot.Bad, "Адрес сервера 1 в настройках платы не прочитан или записан неверно.")
            Return
        End If
        btnUpdateObj.Enabled = False : btnUnConnect.Enabled = False
        Theme.DotText(objectState, Theme.eDot.Waiting, If(cmd = 17, "Запрос привязки у ", "Отвязка через ") & server.Item1 & ":" & server.Item2 & "…")
        Try
            Dim rec = Await Threading.Tasks.Task.Run(Function() SldnStream.QueryServer(server.Item1, server.Item2, cmd, imei))
            If IsDisposed OrElse Disposing Then Return
            Dim text As String
            Dim dot = Theme.eDot.Bad
            Select Case rec(9)
                Case 1
                    text = "Контроллер отсутствует в базе сервера."
                    ShowObject(Nothing)
                Case 2
                    text = If(cmd = 17, "Контроллер не привязан к объекту.", "Контроллер отвязан от объекта.")
                    dot = Theme.eDot.Idle
                    ShowObject(Nothing)
                Case 3
                    If cmd = 17 Then
                        ShowObject(rec)
                        text = "Привязка получена с сервера."
                        dot = Theme.eDot.Good
                    Else
                        text = "Сервер не отвязал контроллер — повторите."
                    End If
                Case Else
                    text = "Сервер не подтвердил запрос (код " & rec(9) & ")."
            End Select
            Theme.DotText(objectState, dot, text)
            Notify("Объект", text, dot = Theme.eDot.Bad)
        Catch ex As Exception
            If IsDisposed OrElse Disposing Then Return
            AgentLog.Failure("object", ex)
            Theme.DotText(objectState, Theme.eDot.Bad, "Сервер не ответил: " & ex.GetBaseException().Message)
            Notify("Объект", "Запрос к серверу не выполнен: " & ex.GetBaseException().Message, True)
        Finally
            If Not IsDisposed AndAlso Not Disposing Then
                btnUpdateObj.Enabled = Not busyWithBoard : btnUnConnect.Enabled = Not busyWithBoard
            End If
        End Try
    End Function

    ' Поля ответа 25: смещения от начала кадра (9 — код), строки с нулём в конце.
    Private Sub ShowObject(rec As Byte())
        If rec Is Nothing Then
            For Each l In {lblObjNum, lblObjName, lblObjController, lblAdmin}
                Theme.SetText(l, "---")
            Next
            Return
        End If
        Theme.SetText(lblObjNum, System.Text.Encoding.UTF8.GetString(rec, 10, 8).TrimEnd(ChrW(0)))
        Theme.SetText(lblObjName, System.Text.Encoding.UTF8.GetString(rec, 18, 128).TrimEnd(ChrW(0)))
        Theme.SetText(lblObjController, System.Text.Encoding.UTF8.GetString(rec, 146, 128).TrimEnd(ChrW(0)))
        Theme.SetText(lblAdmin, System.Text.Encoding.UTF8.GetString(rec, 274, 16).TrimEnd(ChrW(0)))
    End Sub
End Class
