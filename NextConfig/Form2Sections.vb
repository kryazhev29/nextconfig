Partial Public Class Form2
    Private snapshotConfig As clOpros.s_config
    Private ReadOnly snapshotGrid As New DataGridView()
    Private ReadOnly snapshotSecrets As New CheckBox With {.Text = "Показать ПИН и параметры оператора", .AutoSize = True}

    ' Переносим существующие контролы вместе с обработчиками, не создаём копии карточек.
    Private Sub ConfigureSections()
        tabState.Text = "Устройство" : tabIds.Text = "Пользователи"
        Dim overview = DirectCast(tabState.Controls(0), TableLayoutPanel)
        Dim grid = DirectCast(overview.Controls(1), TableLayoutPanel)
        Dim deviceCard = DirectCast(grid.GetControlFromPosition(1, 0), TableLayoutPanel)
        Dim memory As New Label With {.AutoSize = True}
        Theme.Value(memory)
        AddHandler lblUsedMem.TextChanged, Sub() memory.Text = lblUsedMem.Text
        DirectCast(grid.GetControlFromPosition(0, 0), TableLayoutPanel).Controls.Add(CompactRow("Память идентификаторов", memory))
        btnUpdateFW.Text = "Обновить прошивку"
        deviceCard.Controls.Add(btnUpdateFW)
        deviceCard.Controls.Add(lblFwHint)
        lblFwHint.MaximumSize = Size.Empty : lblFwHint.Anchor = AnchorStyles.Left Or AnchorStyles.Right

        Dim firmwarePage = DirectCast(tabFW.Controls(0), TableLayoutPanel)
        Dim bootCard = firmwarePage.Controls(2)
        Dim objectPage = DirectCast(tabObject.Controls(0), TableLayoutPanel)
        Dim objectCard = objectPage.Controls(1)
        Dim objectActions = objectPage.Controls(2)
        tabObject.Controls.Clear() : tabObject.Text = "Сервис"

        ' Тот же журнал, редкие переключатели диагностики теперь в сервисе.
        Dim filters As New FlowLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Fill, .Margin = New Padding(0)}
        filters.Controls.AddRange({CheckBox1, CheckBox2, CheckBox3, CheckBox4})
        Dim openLog As New Button With {.Text = "Открыть журнал обмена", .Width = Theme.Px(240)}
        Theme.Secondary(openLog)
        AddHandler openLog.Click, Sub() tabs.SelectedTab = tabLog

        Theme.ReadOnlyGrid(snapshotGrid)
        snapshotGrid.Height = Theme.Px(420) : snapshotGrid.Dock = DockStyle.Top
        snapshotGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        snapshotGrid.ScrollBars = ScrollBars.Vertical
        snapshotSecrets.Font = Theme.FontBody
        AddHandler snapshotSecrets.CheckedChanged, Sub() RefreshSnapshot()
        Button1.Text = "Перечитать настройки платы"
        Button1.Width = Theme.Px(260)
        Dim readActions As New FlowLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Fill}
        readActions.Controls.Add(Button1)
        serviceRow.Controls.Remove(btnDefault)
        serviceRow.WrapContents = True : serviceRow.Dock = DockStyle.Fill
        Dim snapshotCard = CardBlock("Все настройки платы · только просмотр", readActions,
            Hint("Последний пакет, прочитанный с платы. Изменения в редакторе здесь не отображаются."), snapshotSecrets, snapshotGrid)
        Dim diagnosticsCard = CardBlock("Диагностика обмена", filters, openLog)
        Dim maintenanceCard = CardBlock("Обслуживание", serviceRow)
        Dim shortcuts As New FlowLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Fill,
            .Margin = New Padding(0, 0, 0, Theme.GapSection)}
        Dim captions = {"Объект", "Загрузчик", "Настройки платы", "Диагностика", "Обслуживание"}
        Dim targets As Control() = {objectCard, bootCard, snapshotCard, diagnosticsCard, maintenanceCard}
        For i = 0 To captions.Length - 1
            Dim target = targets(i)
            Dim link As New Button With {.Text = captions(i), .Width = Theme.Px(155), .Margin = New Padding(0, 0, Theme.GapRelated, Theme.GapRelated)}
            Theme.Secondary(link) : Theme.Small(link)
            AddHandler link.Click, Sub() tabObject.ScrollControlIntoView(target)
            shortcuts.Controls.Add(link)
        Next
        tabObject.Controls.Add(Page(Theme.PageHeading("Сервис", "Объект, диагностика и обслуживание контроллера"),
            shortcuts, objectCard, objectActions, bootCard, snapshotCard, diagnosticsCard, maintenanceCard))
        tabs.TabPages.Clear()
        ConfigureLinkSection()
        tabs.TabPages.AddRange({tabState, tabLink, tabIds, tabLog, tabSettings, tabObject})
    End Sub

    Private Sub RefreshSnapshot()
        snapshotGrid.DataSource = ConfigSnapshot.Parse(snapshotConfig, snapshotSecrets.Checked)
        If snapshotGrid.Columns.Count = 0 Then Return
        snapshotGrid.Columns(0).FillWeight = 12
        snapshotGrid.Columns(1).FillWeight = 45
        snapshotGrid.Columns(2).FillWeight = 43
        snapshotGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.True
        snapshotGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells
    End Sub

    Private Sub ReflowOverview()
        Dim overview = DirectCast(tabState.Controls(0), TableLayoutPanel)
        Theme.ReflowOverview(overview)
    End Sub
End Class
