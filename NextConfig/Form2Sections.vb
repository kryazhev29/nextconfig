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
        serviceRow.WrapContents = True : serviceRow.Dock = DockStyle.Top
        serviceRow.FlowDirection = FlowDirection.LeftToRight
        For Each action As Control In serviceRow.Controls
            action.Margin = New Padding(0, 0, Theme.GapRelated, Theme.GapRelated)
        Next
        Dim snapshotCard = CardBlock("Все настройки платы · только просмотр", readActions,
            Hint("Последний пакет, прочитанный с платы. Изменения в редакторе здесь не отображаются."), snapshotSecrets, snapshotGrid)
        Dim maintenanceCard = CardBlock("Обслуживание", serviceRow)
        grid.Controls.Add(maintenanceCard, 0, 2)
        grid.SetColumnSpan(maintenanceCard, 2)
        Dim shortcuts As New FlowLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Fill,
            .Margin = New Padding(0, 0, 0, Theme.GapSection)}
        Dim captions = {"Объект", "Загрузчик", "Настройки платы"}
        Dim targets As Control() = {objectCard, bootCard, snapshotCard}
        For i = 0 To captions.Length - 1
            Dim target = targets(i)
            Dim link As New Button With {.Text = captions(i), .Width = Theme.Px(155), .Margin = New Padding(0, 0, Theme.GapRelated, Theme.GapRelated)}
            Theme.Secondary(link) : Theme.Small(link)
            AddHandler link.Click, Sub() tabObject.ScrollControlIntoView(target)
            shortcuts.Controls.Add(link)
        Next
        tabObject.Controls.Add(Page(Theme.PageHeading("Сервис", "Объект, загрузчик и просмотр настроек платы"),
            shortcuts, objectCard, objectActions, bootCard, snapshotCard))
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

    ' В широком окне «Устройству» — 580 точек: строке времени с «Синхронизировать» хватает, «Связь» берёт остальное.
    ' Обе карточки одной высоты: Fill по строке сетки, низ на одной линии.
    Private Sub ReflowOverview()
        Theme.ReflowOverview(DirectCast(tabState.Controls(0), TableLayoutPanel), 580)
    End Sub
End Class
