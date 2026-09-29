Imports System.Threading.Tasks

Partial Public Class frmOldFirmware
    Private Function StateRow(title As String, key As String, Optional dot As Boolean = False) As Control
        If dot Then Return Theme.ValueRow(title, StateIndicator(key))
        Dim value As New Label With {.Text = "—", .AutoSize = True, .Dock = DockStyle.Fill}
        Theme.Value(value) : stateValues.Add(key, value)
        Return Theme.ValueRow(title, value)
    End Function

    Private Function StateIndicator(key As String) As Control
        Dim value As New Label With {.Text = "—", .AutoSize = True}
        Theme.Value(value) : stateValues.Add(key, value)
        Dim indicator As New Label With {.AutoSize = True}
        Theme.Dot(indicator, Theme.eDot.Waiting) : stateDots.Add(key, indicator)
        Dim pair As New FlowLayoutPanel With {.AutoSize = True, .WrapContents = False, .Dock = DockStyle.Fill}
        pair.Controls.AddRange({indicator, value})
        Return pair
    End Function

    Private Function GridPage(title As String, subtitle As String, grid As DataGridView,
                              Optional actions As Control = Nothing, Optional footer As Control = Nothing) As Control
        Dim page As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 4,
            .Padding = New Padding(Theme.GapSection), .BackColor = Theme.WindowBack}
        page.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        page.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        page.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        page.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        page.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        page.Controls.Add(Theme.PageHeading(title, subtitle), 0, 0)
        If actions IsNot Nothing Then page.Controls.Add(actions, 0, 1)
        grid.Dock = DockStyle.Fill : page.Controls.Add(grid, 0, 2)
        If footer IsNot Nothing Then page.Controls.Add(footer, 0, 3)
        Return page
    End Function

    Private Shared Function Field(table As DataTable, name As String) As String
        For Each row As DataRow In table.Rows
            If CStr(row(1)) = name Then Return CStr(row(2))
        Next
        Return "—"
    End Function

    Private Sub StatusValue(key As String, text As String, Optional good As Boolean? = Nothing)
        stateValues(key).Text = text
        If Not good.HasValue Then Return
        Dim state = If(good.Value, Theme.eDot.Good, Theme.eDot.Bad)
        Theme.Dot(stateDots(key), state) : Theme.DotText(stateValues(key), state, text)
    End Sub

    Private Sub UpdateStatusCards(table As DataTable)
        Dim registration = Field(table, "Регистрация GSM / GPRS").Split("/"c)
        Dim gsm = registration.Length = 2 AndAlso registration(0).Trim() = "1"
        Dim gprs = registration.Length = 2 AndAlso registration(1).Trim() = "1"
        StatusValue("gsm", If(gsm, "Подключено", "Нет сети"), gsm)
        StatusValue("gprs", If(gprs, "Подключено", "Нет интернета"), gprs)
        Dim server = Field(table, "Подключение к серверу") = "Да"
        StatusValue("server", If(server, "Подключено", "Нет связи"), server)
        StatusValue("net", Field(table, "Тип сети"))
        Dim signal As Integer
        Integer.TryParse(Field(table, "Уровень сигнала CSQ (0–31, 99 — неизвестен)"), signal)
        StatusValue("signal", If(signal <= 31, signal & " из 31", "Не определён"))
        signalBar.Value = If(signal <= 31, Math.Max(0, signal), 0)
        StatusValue("time", System.Text.RegularExpressions.Regex.Replace(Field(table, "Местное время платы"), " \(\d+\)$", ""))
        StatusValue("memory", Field(table, "Номеров в базе") & " из " & device.NumberLimit)
        Dim states = Field(table, "Вход 1 / вход 2 / выход").Split("/"c)
        Dim keys = {"in1", "in2", "out"}
        For i = 0 To Math.Min(2, states.Length - 1)
            Dim active = states(i).Trim() = "Да"
            StatusValue(keys(i), If(active, "Включён", "Выключен"), active)
        Next
    End Sub

    Private Sub ShowNumberDetails(sender As Object, e As EventArgs)
        Dim row = TryCast(numbersGrid.CurrentRow?.DataBoundItem, DataRowView)
        If row Is Nothing Then numberDetails.Clear() : Return
        Dim expiry = CStr(row(5))
        expiry = If(expiry.Contains("не задано"), "Бессрочно", System.Text.RegularExpressions.Regex.Replace(expiry, " \(\d+\)$", ""))
        numberDetails.Text = "Действует до (время платы): " & expiry & vbCrLf &
            "Лимит проходов: " & If(CStr(row(6)) = "0", "Без ограничений", CStr(row(6))) & vbCrLf &
            "Расписание: " & If(CStr(row(7)) = "0", "Без ограничений по времени", CStr(row(7))) & vbCrLf &
            "Использовано проходов: " & CStr(row(8))
    End Sub

    Private Async Sub EnterBootloader(sender As Object, e As EventArgs)
        If busy Then Return
        poll.Stop()
        Using confirmation As New frmInfo("Перевод на новую систему",
            "Плата перезапустится в режим загрузки прошивки. " &
            "Затем потребуется выбрать файл новой прошивки, а после её запуска — заменить загрузчик. " &
            "Сохраните базу номеров перед переводом. Перейти в загрузчик?", , True)
            confirmation.Button1.Text = "Перейти в загрузчик"
            confirmation.Button2.Text = "Отмена"
            If confirmation.ShowDialog(Me) <> DialogResult.OK Then
                poll.Start() : Return
            End If
        End Using
        poll.Stop() : SetBusy(True)
        Try
            ' опрос мог уже начаться до остановки таймера: ждём, пока он отдаст порт
            Await portGate.WaitAsync()
            Try
                Await Task.Run(Sub() device.EnterBootloader())
            Finally
                portGate.Release()
            End Try
            EnteredBootloader = True
        Catch ex As Exception
            feedback.Text = OldOperatorError(ex)
        Finally
            SetBusy(False)
        End Try
        If EnteredBootloader Then Close() Else poll.Start()
    End Sub
End Class
