Imports System.Threading

Partial Public Class Form2
    Private ReadOnly tabLink As New TabPage With {.Text = "Связь", .Name = "tabLink", .AutoScroll = True}
    Private ReadOnly deviceConnection As New LinkOverview
    Private ReadOnly deviceMemory As New Label With {.Name = "deviceMemory", .Text = "---", .AutoSize = True}
    Private ReadOnly deviceMemoryMeter As New Meter With {.Maximum = 50000}
    Private ReadOnly signalHistory As New LinkHistory
    Private ReadOnly linkClock As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
    Private ReadOnly linkState As New Label With {.Name = "linkState", .Text = "Чтение состояния связи…"}
    Private ReadOnly linkCountdown As New Label With {.Name = "linkCountdown"}
    Private ReadOnly linkRefresh As New Button With {.Name = "linkRefresh"}
    Private ReadOnly linkSignal As New Meter
    Private ReadOnly linkCsq As New Label With {.AutoSize = True}
    Private ReadOnly linkRsrp As New SignalGauge
    Private ReadOnly linkRsrq As New SignalGauge With {.Quality = True}
    Private ReadOnly linkPlot As New SignalPlot
    Private ReadOnly linkPhases As New ConnectionPhases
    Private linkCell As CellChips
    Private ReadOnly linkFailures As New DataGridView With {.Name = "linkFailures"}
    Private ReadOnly linkHistoryNote As New Label With {.AutoSize = True}
    Private ReadOnly linkErrors As New LinkLabel With {.AutoSize = True}
    Private linkBanner As LinkBannerPanel
    Private linkGauges As Control
    Private linkCsqRow As Control
    Private linkPlotCard As Control
    Private linkTimer As System.Windows.Forms.Timer
    Private linkCancellation As CancellationTokenSource
    Private linkLoading As Boolean
    Private linkTried As Boolean ' был ответ или отказ 112: до него в 4G сигнал «Измеряется»
    Private linkSnapshot As LinkSnapshot
    Private ReadOnly linkAge As New Diagnostics.Stopwatch

    ' Нижний ряд главной: фото с камеры и справа память идентификаторов с выгрузкой базы
    ' (владелец 26.10: на пустом месте справа от фото). В тесном окне память уходит под фото.
    Private ReadOnly mediaGrid As New TableLayoutPanel With {.Name = "mediaGrid", .AutoSize = True, .Margin = New Padding(0)}
    Private memoryCard As Control

    Private Function MediaRow() As Control
        memoryCard = Theme.Card("Память идентификаторов", MemoryBlock())
        ArrangeMediaRow()
        Return mediaGrid
    End Function

    Private Function MemoryBlock() As Control
        Dim line As New TableLayoutPanel With {.AutoSize = True, .ColumnCount = 2, .Anchor = AnchorStyles.Left Or AnchorStyles.Right, .Margin = New Padding(0)}
        line.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        line.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        deviceMemory.Anchor = AnchorStyles.Left : deviceMemory.Margin = New Padding(0)
        btnExportIds.Anchor = AnchorStyles.Right : btnExportIds.Margin = New Padding(Theme.GapRelated, 0, 0, 0)
        line.Controls.Add(deviceMemory, 0, 0) : line.Controls.Add(btnExportIds, 1, 0)
        deviceMemoryMeter.Anchor = AnchorStyles.Left Or AnchorStyles.Right
        deviceMemoryMeter.Height = Theme.Px(6) : deviceMemoryMeter.Margin = New Padding(0, Theme.GapBlock, 0, 0)
        Dim hint As New Label With {.AutoSize = True, .Margin = New Padding(0, Theme.GapBlock, 0, 0),
            .Text = "«Выгрузить базу» сохраняет идентификаторы с платы в файл CSV в формате сервера — резервная копия."}
        Theme.Note(hint, False)
        Dim block As New TableLayoutPanel With {.AutoSize = True, .ColumnCount = 1, .Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right, .Margin = New Padding(0)}
        block.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        block.Controls.Add(line) : block.Controls.Add(deviceMemoryMeter) : block.Controls.Add(hint)
        AddHandler block.SizeChanged, Sub() hint.MaximumSize = New Size(Math.Max(1, block.ClientSize.Width), 0)
        Return block
    End Function

    ' Рядом, пока справа от фото остаётся не меньше 380 точек; иначе друг под другом.
    Private Sub ArrangeMediaRow()
        If memoryCard Is Nothing Then Return
        Dim overview = tabState.Controls(0)
        Dim narrow = overview.ClientSize.Width < photoCard.PreferredSize.Width + Theme.ControlPx(overview, 380 + 2 * 24)
        Dim columns = If(narrow, 1, 2)
        If mediaGrid.ColumnCount = columns AndAlso mediaGrid.Controls.Count = 2 Then Return
        Dim gap = Theme.ControlPx(mediaGrid, 8)
        mediaGrid.SuspendLayout()
        mediaGrid.Controls.Clear() : mediaGrid.ColumnStyles.Clear() : mediaGrid.RowStyles.Clear()
        mediaGrid.ColumnCount = columns : mediaGrid.RowCount = 3 - columns
        mediaGrid.ColumnStyles.Add(New ColumnStyle(If(narrow, SizeType.Percent, SizeType.AutoSize), 100))
        If Not narrow Then mediaGrid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        For i = 1 To mediaGrid.RowCount
            mediaGrid.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Next
        photoCard.Dock = DockStyle.Fill : memoryCard.Dock = DockStyle.Fill
        photoCard.Margin = New Padding(0, 0, If(narrow, 0, gap), If(narrow, gap, 0))
        memoryCard.Margin = New Padding(If(narrow, 0, gap), 0, 0, 0)
        mediaGrid.Controls.Add(photoCard, 0, 0)
        mediaGrid.Controls.Add(memoryCard, If(narrow, 0, 1), If(narrow, 1, 0))
        mediaGrid.ResumeLayout(True)
    End Sub

    Private Sub ConfigureLinkSection()
        tabLink.BackColor = Theme.WindowBack
        linkPlot.History = signalHistory
        AddHandler deviceConnection.More, Sub() tabs.SelectedTab = tabLink
        AddHandler deviceConnection.PolicyClick, Sub() ShowWifiSettings()
        Theme.Value(linkCsq) : Theme.Caption(linkHistoryNote) : Theme.Value(deviceMemory)
        Theme.LinkHistory(linkFailures)
        linkFailures.Columns.Add("stage", "Этап")
        linkFailures.Columns.Add("reason", "Причина")
        linkFailures.Columns.Add("seconds", "От старта попытки")
        linkFailures.Columns(0).FillWeight = 22
        linkFailures.Columns(1).FillWeight = 58
        linkFailures.Columns(2).FillWeight = 20
        linkFailures.Visible = False : linkHistoryNote.Visible = False
        Theme.Caption(linkErrors) : linkErrors.ForeColor = Theme.TextMuted : linkErrors.LinkColor = Theme.Accent
        linkErrors.Text = "Сбоев нет" : linkErrors.LinkArea = New LinkArea(0, 0)
        AddHandler linkErrors.LinkClicked, Sub() ToggleLinkFailures()
        linkCell = New CellChips(linkErrors)
        linkBanner = New LinkBannerPanel(linkRefresh)
        linkGauges = Theme.LinkColumns(Theme.Card("", linkRsrp), Theme.Card("", linkRsrq))
        linkCsqRow = Theme.Card("Уровень сигнала", Theme.LinkSignal(linkSignal, linkCsq))
        linkPlotCard = Theme.Card("Уровень сигнала", linkPlot)
        linkGauges.Visible = False : linkPlotCard.Visible = False
        tabLink.Controls.Add(Theme.CardPage(linkBanner, linkGauges, linkCsqRow, linkPlotCard,
            Theme.LinkColumns(Theme.Card("", linkPhases), Theme.Card("Сота", linkCell)),
            linkHistoryNote, linkFailures))
        linkTimer = New System.Windows.Forms.Timer(components) With {.Interval = 5000}
        AddHandler linkTimer.Tick, Sub() RefreshLink()
        AddHandler linkRefresh.Click, Sub() RefreshLink()
        AddHandler tabLink.VisibleChanged, AddressOf LinkVisibilityChanged
        AddHandler tabState.VisibleChanged, AddressOf LinkVisibilityChanged
        AddHandler tabSettings.VisibleChanged, AddressOf LinkVisibilityChanged
        AddHandler tmr1.Tick, AddressOf UpdateLinkCountdown
        AddHandler Disposed, Sub()
                                 If linkCancellation IsNot Nothing Then linkCancellation.Cancel()
                             End Sub
        If Demo.IsOn Then
            Dim now = linkClock.Elapsed.TotalSeconds
            For sampleSecond As Integer = 0 To 598 Step 2
                signalHistory.Observe(now - 600 + sampleSecond, Demo.SignalRsrp(sampleSecond))
            Next
        End If
    End Sub

    Private Sub ToggleLinkFailures()
        If linkSnapshot Is Nothing OrElse linkSnapshot.Failures.Count = 0 Then Return
        linkFailures.Visible = Not linkFailures.Visible
        linkHistoryNote.Visible = linkFailures.Visible
    End Sub

    Private Sub LinkVisibilityChanged(sender As Object, e As EventArgs)
        linkTimer.Interval = If(tabLink.Visible, 2000, 5000)
        linkTimer.Enabled = (tabLink.Visible OrElse tabState.Visible OrElse tabSettings.Visible) AndAlso Not IsDisposed
        If linkCancellation IsNot Nothing Then linkCancellation.Cancel()
        If linkTimer.Enabled Then RefreshLink()
    End Sub

    Private Async Sub RefreshLink()
        If linkLoading OrElse Not (tabLink.Visible OrElse tabState.Visible OrElse tabSettings.Visible) OrElse busyWithBoard OrElse opros Is Nothing Then Return
        linkLoading = True : linkRefresh.Enabled = False
        Dim cancellation As New CancellationTokenSource
        linkCancellation = cancellation
        Try
            Dim snapshot = Await Task.Run(Function() opros.GetLinkDiag(cancellation.Token))
            If Not cancellation.IsCancellationRequested AndAlso Not IsDisposed Then
                DisplayLink(snapshot)
                If Not wifiBusy Then
                    Dim wifiReply = Await Task.Run(Function() opros.WifiBridge(Nothing))
                    If Not cancellation.IsCancellationRequested AndAlso Not IsDisposed Then WifiApplyReply(wifiReply)
                End If
            End If
        Catch ex As OperationCanceledException
        Catch ex As Exception
            If Not cancellation.IsCancellationRequested AndAlso Not IsDisposed Then
                AgentLog.Failure("link", ex)
                ' Один сорвавшийся ответ не сбрасывает экран: прошлый снимок держится до 10 с.
                If linkSnapshot Is Nothing OrElse linkAge.Elapsed.TotalSeconds >= 10 Then DisplayLink(Nothing)
            End If
        Finally
            linkLoading = False : linkCancellation = Nothing
            cancellation.Dispose()
            If Not IsDisposed Then linkRefresh.Enabled = Not busyWithBoard
        End Try
    End Sub

    ' Модем изредка отвечает на QENG ошибкой (плата №1: 2 из 40 опросов) — последний замер держится до 10 с.
    Private lastLte As LinkSnapshot
    Private ReadOnly lastLteAge As New Diagnostics.Stopwatch

    Private Sub HoldLastLte(snapshot As LinkSnapshot)
        If snapshot Is Nothing OrElse snapshot.Version < 4 OrElse snapshot.Network <> 4 Then lastLte = Nothing : Return
        If snapshot.Rsrp <> 255 Then
            lastLte = snapshot : lastLteAge.Restart()
        ElseIf lastLte IsNot Nothing AndAlso lastLteAge.Elapsed.TotalSeconds < 10 Then
            snapshot.Rsrp = lastLte.Rsrp : snapshot.Rsrq = lastLte.Rsrq
            snapshot.Band = lastLte.Band : snapshot.Bandwidth = lastLte.Bandwidth
        End If
    End Sub

    Private Sub DisplayLink(snapshot As LinkSnapshot)
        HoldLastLte(snapshot)
        linkTried = True
        Dim historyChanged = snapshot Is Nothing OrElse linkSnapshot Is Nothing OrElse Not snapshot.Failures.SequenceEqual(linkSnapshot.Failures)
        linkSnapshot = snapshot : linkAge.Restart()
        If historyChanged Then linkFailures.Rows.Clear()
        Theme.SetText(linkState, LinkDiag.BannerHeading(snapshot))
        Dim grade = If(snapshot IsNot Nothing AndAlso snapshot.State = 7, LinkDiag.SignalView(snapshot, snapshot.Csq).Grade, LinkDiag.Poor)
        linkBanner.SetState(grade, If(snapshot Is Nothing, 0, LinkDiag.SignalView(snapshot, snapshot.Csq).Bars), LinkDiag.BannerHeading(snapshot), LinkDiag.BannerDetail(snapshot))
        If snapshot Is Nothing Then
            linkGauges.Visible = False : linkPlotCard.Visible = False
            linkCsqRow.Visible = True : linkSignal.Value = 0
            Theme.SetText(linkCsq, "Нет данных")
            linkPhases.Durations = New UInteger(5) {}
            linkErrors.Text = "Нет данных о сбоях" : linkErrors.LinkArea = New LinkArea(0, 0)
            linkFailures.Visible = False : linkHistoryNote.Visible = False
        Else
            Dim signal = LinkDiag.SignalView(snapshot, snapshot.Csq)
            Theme.SetText(linkCsq, signal.LevelText)
            linkSignal.Maximum = signal.Maximum : linkSignal.Value = signal.Value
            ' 2G и 3G — те же шкалы и график, что в 4G: уровень RSSI в дБм из CSQ, качество не измеряется.
            Dim graph = signal.Lte OrElse signal.Gsm
            Dim level = If(signal.Gsm, LinkDiag.GsmLevel(snapshot.Csq), snapshot.Rsrp)
            linkGauges.Visible = graph : linkPlotCard.Visible = graph : linkCsqRow.Visible = Not graph
            linkRsrp.Gsm = signal.Gsm : linkRsrq.Gsm = signal.Gsm : linkRsrq.Network = LinkDiag.NetworkText(snapshot.Network)
            linkRsrp.Raw = level : linkRsrq.Raw = If(signal.Gsm, CByte(255), snapshot.Rsrq)
            linkRsrp.Invalidate() : linkRsrq.Invalidate()
            If (tabLink.Visible OrElse tabState.Visible OrElse tabSettings.Visible) AndAlso graph Then signalHistory.Observe(linkClock.Elapsed.TotalSeconds, level, signal.Gsm)
            linkPlot.Seconds = linkClock.Elapsed.TotalSeconds : linkPlot.Version = snapshot.Version : linkPlot.Invalidate()
            linkPhases.Durations = snapshot.StageMilliseconds
            If historyChanged Then
                For Each failure As LinkFailure In snapshot.Failures
                    linkFailures.Rows.Add(LinkDiag.Stages(failure.Stage), LinkDiag.ReasonText(failure.Reason), failure.Seconds & " с")
                Next
                linkFailures.ClearSelection()
            End If
            linkErrors.Text = If(snapshot.Failures.Count = 0, "Сбоев нет", "Сбои: " & snapshot.Failures.Count)
            linkErrors.LinkArea = New LinkArea(0, If(snapshot.Failures.Count = 0, 0, linkErrors.Text.Length))
            If snapshot.Failures.Count = 0 Then linkFailures.Visible = False
            linkHistoryNote.Visible = linkFailures.Visible
            Theme.SetText(linkHistoryNote, "Новые сверху. Время от начала своей попытки; после перезапуска модема отсчёт начинается заново.")
        End If
        linkCell.Chips = LinkDiag.CellChips(snapshot)
        linkCell.Text = String.Join(" · ", linkCell.Chips)
        linkCell.Invalidate()
        linkPhases.Invalidate()
        If StatusIsFresh() Then UpdateDeviceSignal()
        UpdateLinkCountdown()
    End Sub

    ' Канал сервера (Wi-Fi или сотовая) из последнего ответа 113: один сорвавшийся опрос не переключает карточку.
    Private linkWifi As Byte()
    Private lastChannels As LinkChannelView

    Private Sub UpdateDeviceSignal()
        Dim snapshot = If(status.netType = clOpros.eNetType._4G, linkSnapshot, Nothing)
        Dim measuring = status.netType = clOpros.eNetType._4G AndAlso Not linkTried
        Dim signal = If(measuring, LinkDiag.MeasuringView(), LinkDiag.SignalView(snapshot, status.signal, CByte(status.netType)))
        Theme.SetText(lblSig, signal.LevelText)
        pbSig.Maximum = signal.Maximum : pbSig.Value = signal.Value
        deviceConnection.Signal = signal
        Dim channels = LinkChannels.View(linkWifi, linkSnapshot, status.gsm, status.gprs, status.connectToServer, CByte(status.netType))
        Dim switched = LinkChannels.SwitchText(lastChannels, channels)
        lastChannels = channels
        If switched <> "" Then Notify("Связь", switched)
        ShowLinkChannels(channels)
        deviceConnection.Cell = LinkDiag.CellText(linkSnapshot, CByte(status.netType))
        ' Две строки: уровень и качество не переносятся посередине числа в узкой карточке.
        ' Одна строка цифр: качество в 2G не измеряется — об этом говорит страница «Связь».
        Dim rssi = LinkDiag.GsmLevel(status.signal)
        deviceConnection.Details = If(measuring, "уровень измеряется", If(signal.Lte,
            "уровень " & If(snapshot.Rsrp = 255, "нет данных", "−" & snapshot.Rsrp & " дБм") & " · " &
            "качество " & If(snapshot.Rsrq = 255, "нет данных", "−" & snapshot.Rsrq & " дБ"), If(signal.Gsm,
            "уровень " & If(rssi = 255, "нет данных", "−" & rssi & " дБм"), "CSQ " & signal.LevelText)))
        deviceConnection.Invalidate()
    End Sub

    ' «Канал связи: …» на карточке «Связь»: выбор Wi-Fi-модема — вкладка «Wi-Fi-модем» в настройках.
    Private Sub ShowWifiSettings()
        ShowSettingsSection(1)
        Dim host = wifiModel.Parent
        While host IsNot Nothing AndAlso Not (TypeOf host Is ScrollableControl AndAlso DirectCast(host, ScrollableControl).AutoScroll)
            host = host.Parent
        End While
        If host IsNot Nothing Then DirectCast(host, ScrollableControl).ScrollControlIntoView(wifiModel)
    End Sub

    Private Sub ShowLinkChannels(channels As LinkChannelView)
        deviceConnection.Channels = channels
        deviceConnection.Height = Theme.ControlPx(deviceConnection, deviceConnection.DesignHeight)
        Theme.SetText(lblHeadLink, channels.Header)
    End Sub

    ' Статус устарел: канал неизвестен, после возврата данных смена канала не объявляется.
    Private Sub ResetLinkChannels()
        linkWifi = Nothing : lastChannels = Nothing
        ShowLinkChannels(LinkChannels.Stale())
        deviceConnection.Invalidate()
    End Sub

    Private Sub UpdateLinkCountdown()
        Dim text = ""
        If linkSnapshot IsNot Nothing AndAlso linkSnapshot.State = 6 Then
            Dim remaining = Math.Max(0, CLng(linkSnapshot.RetryMilliseconds) - linkAge.ElapsedMilliseconds)
            text = "До перезапуска модема: " & Math.Ceiling(remaining / 1000.0).ToString("0") & " с"
        End If
        Theme.SetText(linkCountdown, text)
        If linkBanner IsNot Nothing Then
            linkBanner.Detail = LinkDiag.BannerDetail(linkSnapshot) & If(text = "", "", " " & text)
            linkBanner.Invalidate()
        End If
    End Sub
End Class
