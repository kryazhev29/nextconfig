Imports System.Threading

Partial Public Class Form2
    Private ReadOnly tabLink As New TabPage With {.Text = "Связь", .Name = "tabLink", .AutoScroll = True}
    Private ReadOnly linkState As New Label With {.Name = "linkState", .Text = "Чтение состояния связи…"}
    Private ReadOnly linkDot As New Label
    Private ReadOnly linkCountdown As New Label With {.Name = "linkCountdown"}
    Private ReadOnly linkRefresh As New Button With {.Name = "linkRefresh"}
    Private ReadOnly linkSignal As New Meter
    Private ReadOnly linkValues As Label() = Enumerable.Range(0, 5).Select(Function(i) New Label With {.AutoSize = True, .Text = "—"}).ToArray()
    Private ReadOnly linkDurations As Label() = Enumerable.Range(0, 6).Select(Function(i) New Label With {.AutoSize = True, .Text = "—"}).ToArray()
    Private ReadOnly linkFailures As New DataGridView With {.Name = "linkFailures"}
    Private ReadOnly linkHistoryNote As New Label With {.AutoSize = True}
    Private linkTimer As System.Windows.Forms.Timer
    Private linkCancellation As CancellationTokenSource
    Private linkLoading As Boolean
    Private linkSnapshot As LinkSnapshot
    Private ReadOnly linkAge As New Diagnostics.Stopwatch

    Private Sub ConfigureLinkSection()
        tabLink.BackColor = Theme.WindowBack
        For Each value As Label In linkValues.Concat(linkDurations)
            Theme.Value(value)
        Next
        Theme.Caption(linkHistoryNote)
        Theme.LinkHistory(linkFailures)
        linkFailures.Columns.Add("stage", "Этап")
        linkFailures.Columns.Add("reason", "Причина")
        linkFailures.Columns.Add("seconds", "От старта попытки")
        linkFailures.Columns(0).FillWeight = 22
        linkFailures.Columns(1).FillWeight = 58
        linkFailures.Columns(2).FillWeight = 20
        Dim stageRows As New List(Of Control)
        For i = 0 To 5
            stageRows.Add(Theme.ValueRow(LinkDiag.Stages(i), linkDurations(i)))
        Next
        Dim details = Theme.Card("Параметры связи",
            Theme.ValueRow("Канал", linkValues(0)),
            Theme.ValueRow("Сигнал", Theme.LinkSignal(linkSignal, linkValues(1))),
            Theme.ValueRow("Тип сети", linkValues(2)),
            Theme.ValueRow("Оператор", linkValues(3)),
            Theme.ValueRow("Проверка ya.ru", linkValues(4)))
        tabLink.Controls.Add(Theme.CardPage(
            Theme.LinkState(linkDot, linkState, linkCountdown, linkRefresh),
            Theme.LinkColumns(details, Theme.Card("Этапы подключения", stageRows.ToArray())),
            Theme.Card("Последние 10 сбоев · новые сверху", linkHistoryNote, linkFailures)))
        linkTimer = New System.Windows.Forms.Timer(components) With {.Interval = 2000}
        AddHandler linkTimer.Tick, Sub() RefreshLink()
        AddHandler linkRefresh.Click, Sub() RefreshLink()
        AddHandler tabLink.VisibleChanged, AddressOf LinkVisibilityChanged
        AddHandler tmr1.Tick, AddressOf UpdateLinkCountdown
        AddHandler Disposed, Sub()
                                 If linkCancellation IsNot Nothing Then linkCancellation.Cancel()
                             End Sub
        Theme.LinkDot(linkDot, 0)
    End Sub

    Private Sub LinkVisibilityChanged(sender As Object, e As EventArgs)
        linkTimer.Enabled = tabLink.Visible AndAlso Not IsDisposed
        If linkCancellation IsNot Nothing Then linkCancellation.Cancel()
        If linkTimer.Enabled Then RefreshLink()
    End Sub

    Private Async Sub RefreshLink()
        If linkLoading OrElse Not tabLink.Visible OrElse busyWithBoard OrElse opros Is Nothing Then Return
        linkLoading = True
        linkRefresh.Enabled = False
        Dim cancellation As New CancellationTokenSource
        linkCancellation = cancellation
        Try
            Dim snapshot = Await Task.Run(Function() opros.GetLinkDiag(cancellation.Token))
            If Not cancellation.IsCancellationRequested AndAlso Not IsDisposed Then DisplayLink(snapshot)
        Catch ex As OperationCanceledException
        Catch ex As Exception
            If Not cancellation.IsCancellationRequested AndAlso Not IsDisposed Then
                AgentLog.Failure("link", ex)
                DisplayLink(Nothing)
            End If
        Finally
            linkLoading = False
            linkCancellation = Nothing
            cancellation.Dispose()
            If Not IsDisposed Then linkRefresh.Enabled = Not busyWithBoard
        End Try
    End Sub

    Private Sub DisplayLink(snapshot As LinkSnapshot)
        Dim historyChanged = snapshot Is Nothing OrElse linkSnapshot Is Nothing OrElse
            Not snapshot.Failures.SequenceEqual(linkSnapshot.Failures)
        linkSnapshot = snapshot
        linkAge.Restart()
        If historyChanged Then linkFailures.Rows.Clear()
        If snapshot Is Nothing Then
            Theme.SetText(linkState, "нет ответа от платы")
            Theme.LinkDot(linkDot, -1)
            For Each value As Label In linkValues.Concat(linkDurations)
                Theme.SetText(value, "—")
            Next
            linkSignal.Value = 0
            Theme.SetText(linkHistoryNote, "Нет ответа от платы. Проверьте USB-подключение и нажмите «Обновить».")
        Else
            Theme.SetText(linkState, LinkDiag.StateText(snapshot.State))
            Theme.LinkDot(linkDot, snapshot.State)
            Theme.SetText(linkValues(0), If(snapshot.Channel = 0, "GSM", "WiFi"))
            Theme.SetText(linkValues(1), LinkDiag.SignalText(snapshot))
            linkSignal.Visible = snapshot.Version <> 4 OrElse snapshot.Network <> 4
            linkSignal.Value = If(snapshot.Csq = 99 OrElse (snapshot.Version = 4 AndAlso snapshot.Network = 4), 0, CInt(snapshot.Csq))
            Theme.SetText(linkValues(2), LinkDiag.NetworkText(snapshot.Network))
            Theme.SetText(linkValues(3), LinkDiag.OperatorText(snapshot.Plmn))
            Theme.SetText(linkValues(4), LinkDiag.ProbeText(snapshot.Probe))
            For i = 0 To 5
                Theme.SetText(linkDurations(i), LinkDiag.DurationText(snapshot.StageMilliseconds(i)))
            Next
            If historyChanged Then
                For Each failure As LinkFailure In snapshot.Failures
                    linkFailures.Rows.Add(LinkDiag.Stages(failure.Stage), LinkDiag.ReasonText(failure.Reason), failure.Seconds & " с")
                Next
                linkFailures.ClearSelection()
            End If
            linkFailures.Visible = snapshot.Failures.Count > 0
            Theme.SetText(linkHistoryNote, If(snapshot.Failures.Count = 0, "Сбоев после включения контроллера нет.",
                "Время от начала своей попытки подключения; после перезапуска модема отсчёт начинается заново."))
        End If
        UpdateLinkCountdown()
    End Sub

    Private Sub UpdateLinkCountdown()
        Dim text = ""
        If linkSnapshot IsNot Nothing AndAlso linkSnapshot.State = 6 Then
            Dim remaining = Math.Max(0, CLng(linkSnapshot.RetryMilliseconds) - linkAge.ElapsedMilliseconds)
            text = "До перезапуска модема: " & Math.Ceiling(remaining / 1000.0).ToString("0") & " с"
        End If
        Theme.SetText(linkCountdown, text)
    End Sub
End Class
