Imports System.IO

' WinForms прокручивает страницу к фокусу при Enable/Layout. Оператор управляет
' прокруткой сам; опрос и повторное включение кнопок не перемещают страницу.
Public Class OldStableTabPage
    Inherits TabPage
    Public Sub New(caption As String)
        MyBase.New(caption)
    End Sub
    Protected Overrides Function ScrollToControl(activeControl As Control) As Point
        Return DisplayRectangle.Location
    End Function
End Class

Public Class OldStablePanel
    Inherits Panel
    Protected Overrides Function ScrollToControl(activeControl As Control) As Point
        Return DisplayRectangle.Location
    End Function
End Class

Partial Public Class frmOldFirmware
    Private ReadOnly settingsGroup As New ComboBox With {.Name = "oldSettingsGroup", .DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly settingCards As New OldStablePanel With {.Dock = DockStyle.Fill, .AutoScroll = True}
    Private ReadOnly scheduleCards As New OldStablePanel With {.Dock = DockStyle.Fill, .AutoScroll = True}
    Private ReadOnly scheduleGroup As New ComboBox With {.Name = "oldScheduleGroup", .DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly settingEditors As New Dictionary(Of Integer, Control)
    Private ReadOnly settingRows As New Dictionary(Of Integer, Control)
    Private ReadOnly settingGroups As New Dictionary(Of String, Control)
    Private ReadOnly invalidSettings As New HashSet(Of Integer)
    Private ReadOnly draftLabel As New Label With {.AutoSize = True, .Text = "Сначала прочитайте настройки платы."}
    Private ReadOnly diagnosticSettings As New DataGridView()
    Private renderingSettings As Boolean

    Private Function BuildSettingsPage(actions As FlowLayoutPanel) As Control
        Dim page As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 5, .Padding = New Padding(Theme.GapSection)}
        page.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        For Each kind In {SizeType.AutoSize, SizeType.AutoSize, SizeType.AutoSize, SizeType.Percent, SizeType.AutoSize}
            page.RowStyles.Add(New RowStyle(kind, If(kind = SizeType.Percent, 100, 0)))
        Next
        page.Controls.Add(Theme.PageHeading("Настройки", "Измените черновик · для отправки в плату нажмите «Записать настройки»"), 0, 0)
        page.Controls.Add(actions, 0, 1)
        settingsGroup.Width = Theme.Px(250)
        Dim selectGroup As New FlowLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Fill}
        selectGroup.Controls.AddRange({New Label With {.Text = "Раздел", .AutoSize = True, .Margin = New Padding(0, Theme.GapRelated, Theme.GapRelated, 0)}, settingsGroup})
        page.Controls.Add(selectGroup, 0, 2)
        page.Controls.Add(settingCards, 0, 3)
        Dim footer As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True}
        footer.Controls.AddRange({showSecrets, draftLabel})
        page.Controls.Add(footer, 0, 4)
        AddHandler settingsGroup.SelectedIndexChanged, Sub() FilterSettingCards()
        Return page
    End Function

    Private Function BuildSchedulesPage(read As Button) As Control
        Dim write As New Button With {.Name = "oldWriteSchedules"}
        SetupButton(write, "Записать настройки", 245) : writeButtons.Add(write)
        write.Enabled = device.CanWrite
        AddHandler write.Click, Async Sub() Await SaveOldSettings()
        Dim actions As New FlowLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Fill}
        scheduleGroup.Width = Theme.Px(245)
        actions.Controls.AddRange({read, write, scheduleGroup})
        Dim page As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3, .Padding = New Padding(Theme.GapSection)}
        page.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        page.RowStyles.Add(New RowStyle(SizeType.AutoSize)) : page.RowStyles.Add(New RowStyle(SizeType.AutoSize)) : page.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        page.Controls.Add(Theme.PageHeading("Расписания доступа", "Выберите расписание · запись сохраняет также черновик из раздела «Настройки»"), 0, 0)
        page.Controls.Add(actions, 0, 1) : page.Controls.Add(scheduleCards, 0, 2)
        AddHandler scheduleGroup.SelectedIndexChanged, Sub() FilterSettingCards()
        Return page
    End Function

    Private Function BuildSettingsDiagnostics() As Control
        Theme.ReadOnlyGrid(diagnosticSettings)
        diagnosticSettings.Height = Theme.Px(320) : diagnosticSettings.Dock = DockStyle.Top
        diagnosticSettings.Visible = False
        Dim toggle As New Button With {.Name = "oldSettingsDiagnostics"}
        SetupButton(toggle, "Показать служебные поля", 245)
        AddHandler toggle.Click, Sub()
                                     diagnosticSettings.Visible = Not diagnosticSettings.Visible
                                     toggle.Text = If(diagnosticSettings.Visible, "Скрыть служебные поля", "Показать служебные поля")
                                 End Sub
        Return Theme.Card("Диагностика настроек", toggle, diagnosticSettings)
    End Function

    Private Sub RenderSettingCards()
        renderingSettings = True
        Dim selected = settingsGroup.Text
        settingCards.SuspendLayout()
        Try
            For Each groupCard As Control In settingGroups.Values : groupCard.Dispose() : Next
            settingCards.Controls.Clear() : scheduleCards.Controls.Clear() : settingGroups.Clear() : settingEditors.Clear() : settingRows.Clear() : invalidSettings.Clear()
            Dim diagnostic = settings.Clone()
            For i = 0 To OldSettings.Fields.Count - 1
                Dim f = OldSettings.Fields(i), row = settings.Rows(i)
                Dim group = OldSettingsPresentation.Group(f, CStr(row(0)))
                If group = "Служебные" Then
                    diagnostic.ImportRow(row) : Continue For
                End If
                If Not settingGroups.ContainsKey(group) Then
                    Dim content As New TableLayoutPanel With {.ColumnCount = 1, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .Dock = DockStyle.Top, .Name = "oldGroup" & group}
                    content.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
                    content.Controls.Add(Theme.PageHeading(group, If(group = "Баланс SIM-карты",
                        "Старый числовой USSD-номер больше не используется. Для запроса нужна полная строка, например *100#.", "")))
                    settingGroups.Add(group, content)
                    If group.StartsWith("Расписание ") Then scheduleCards.Controls.Add(content) Else settingCards.Controls.Add(content)
                End If
                Dim editor = SettingEditor(i)
                settingEditors.Add(i, editor)
                Dim fieldRow As New TableLayoutPanel With {.ColumnCount = 2, .RowCount = 2, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .Dock = DockStyle.Top,
                    .BackColor = Theme.Surface, .Padding = New Padding(Theme.GapRelated), .Margin = New Padding(0, 0, 0, Theme.GapRelated)}
                fieldRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 45))
                fieldRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 55))
                Dim caption As New Label With {.Text = OldSettingsPresentation.Title(f, CStr(row(1))), .AutoSize = True, .Dock = DockStyle.Fill, .Font = Theme.FontBody}
                editor.Dock = DockStyle.Top
                fieldRow.Controls.Add(caption, 0, 0) : fieldRow.Controls.Add(editor, 1, 0)
                Dim hint As New Label With {.Text = OldSettingsPresentation.Hint(f), .AutoSize = True, .Dock = DockStyle.Fill, .Font = Theme.FontCaption, .ForeColor = Theme.TextMuted}
                fieldRow.Controls.Add(hint, 0, 1) : fieldRow.SetColumnSpan(hint, 2)
                settingRows.Add(i, fieldRow)
                settingGroups(group).Controls.Add(fieldRow)
            Next
            BindFields(diagnosticSettings, diagnostic)
            settingsGroup.Items.Clear()
            settingsGroup.Items.AddRange(settingGroups.Keys.Where(Function(g) Not g.StartsWith("Расписание ")).Cast(Of Object)().ToArray())
            settingsGroup.SelectedItem = If(settingGroups.ContainsKey(selected), selected, "Связь")
            Dim previousSchedule = scheduleGroup.Text
            scheduleGroup.Items.Clear()
            scheduleGroup.Items.AddRange(settingGroups.Keys.Where(Function(g) g.StartsWith("Расписание ")).Cast(Of Object)().ToArray())
            scheduleGroup.SelectedItem = If(settingGroups.ContainsKey(previousSchedule), previousSchedule, "Расписание 1")
            draftLabel.Text = If(OldSettings.Changes(originalSettings, editedSettings) = "", "Настройки прочитаны. Изменений нет.", "Есть изменения в черновике.")
        Finally
            renderingSettings = False : settingCards.ResumeLayout()
        End Try
        UpdateSettingDependencies() : FilterSettingCards()
    End Sub

    Private Function SettingEditor(index As Integer) As Control
        Dim f = OldSettings.Fields(index), raw = OldSettings.RawValue(editedSettings, index)
        Dim name = "oldSetting" & f.Offset & "_" & f.Mask
        Dim options = OldSettingsPresentation.Choices(f)
        If options.Count > 0 Then
            Dim value = Integer.Parse(raw)
            ' 0 и 3 означают одинаковый размер; сохраняем полученный вариант.
            If f.Offset = 159 AndAlso value = 0 Then options.Remove(3) : options(0) = "160×120"
            If Not options.ContainsKey(value) Then options(value) = "Неизвестное значение (" & value & ") — сохранено"
            Dim combo As New ComboBox With {.Name = name, .DropDownStyle = ComboBoxStyle.DropDownList, .DisplayMember = "Value", .ValueMember = "Key", .DataSource = options.ToList(), .SelectedValue = value}
            AddHandler combo.SelectedValueChanged, Sub()
                                                      If combo.SelectedValue IsNot Nothing Then ChangeSettingDraft(index, CInt(combo.SelectedValue).ToString())
                                                  End Sub
            Return combo
        End If
        If f.Mask <> 255 AndAlso (f.Mask >> f.Shift) = 1 Then
            Dim check As New CheckBox With {.Name = name, .AutoSize = True, .Text = "Включено", .Checked = raw = "1"}
            AddHandler check.CheckedChanged, Sub() ChangeSettingDraft(index, If(check.Checked, "1", "0"))
            Return check
        End If
        If f.Offset = 167 OrElse (f.Offset >= 138 AndAlso f.Offset <= 158 AndAlso (f.Offset - 138) Mod 3 = 0) Then Return DayEditor(index)
        If f.Kind = "text" OrElse f.Secret Then
            Dim text As New TextBox With {.Name = name, .Text = raw, .UseSystemPasswordChar = f.Secret AndAlso Not showSecrets.Checked, .MaxLength = If(f.Kind = "text", f.Width - 1, 5)}
            AddHandler text.TextChanged, Sub() ChangeSettingDraft(index, text.Text)
            Return text
        End If
        Dim number As New NumericUpDown With {.Name = name, .Minimum = If(f.Kind = "signed", -128, 0), .Maximum = If(f.Kind = "signed", 127, If(f.Width = 2, 65535, 255)), .Value = Decimal.Parse(raw)}
        AddHandler number.ValueChanged, Sub() ChangeSettingDraft(index, number.Value.ToString())
        Return number
    End Function

    Private Function DayEditor(index As Integer) As Control
        Dim panel As New FlowLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .Dock = DockStyle.Top}
        Dim value = Integer.Parse(OldSettings.RawValue(editedSettings, index))
        For day = 0 To 6
            Dim bit = 1 << day
            Dim check As New CheckBox With {.Text = New String() {"Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"}(day), .AutoSize = True, .Checked = (value And bit) <> 0}
            AddHandler check.CheckedChanged, Sub()
                                                Dim current = Integer.Parse(OldSettings.RawValue(editedSettings, index))
                                                ChangeSettingDraft(index, If(check.Checked, current Or bit, current And (255 Xor bit)).ToString())
                                            End Sub
            panel.Controls.Add(check)
        Next
        Return panel
    End Function

    Private Sub ChangeSettingDraft(index As Integer, value As String)
        If renderingSettings OrElse busy Then Return
        Try
            editedSettings = OldSettings.Change(editedSettings, index, value)
            invalidSettings.Remove(index)
            draftLabel.Text = If(invalidSettings.Count > 0, "Исправьте незаполненные или неверные поля.",
                If(OldSettings.Changes(originalSettings, editedSettings) = "", "Изменений нет.", "Изменения в черновике. Для отправки нажмите «Записать настройки»."))
            UpdateSettingDependencies()
        Catch ex As Exception
            invalidSettings.Add(index)
            draftLabel.Text = "Проверьте поле «" & OldSettingsPresentation.Title(OldSettings.Fields(index), CStr(settings.Rows(index)(1))) & "»: " & If(OldSettings.Fields(index).Secret, "введите допустимые цифры.", "проверьте длину и допустимые символы.")
        End Try
    End Sub

    Private Sub UpdateSettingDependencies()
        If editedSettings Is Nothing Then Return
        For Each entry In settingEditors
            entry.Value.Enabled = Not busy AndAlso OldSettingsPresentation.Enabled(OldSettings.Fields(entry.Key), editedSettings)
        Next
    End Sub

    Private Sub FilterSettingCards()
        If renderingSettings Then Return
        Dim query = search.Text.Trim()
        For Each entry In settingRows
            Dim f = OldSettings.Fields(entry.Key), row = settings.Rows(entry.Key)
            entry.Value.Visible = CStr(row(0)).StartsWith("Расписание ") OrElse query = "" OrElse (OldSettingsPresentation.Title(f, CStr(row(1))) & " " & OldSettingsPresentation.Hint(f)).IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0
        Next
        For Each entry In settingGroups
            entry.Value.Visible = entry.Key = settingsGroup.Text OrElse entry.Key = scheduleGroup.Text
        Next
    End Sub
End Class
