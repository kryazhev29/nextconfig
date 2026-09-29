Imports System.IO
Imports System.Threading.Tasks

' Общее подтверждение: данные остаются в окне, не попадают в AgentLog.
Public Class OldChangeDialog
    Inherits Form
    Public Sub New(title As String, changes As String)
        Me.Text = title : Theme.StyleForm(Me)
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(Theme.Px(760), Theme.Px(520))
        Dim root As New TableLayoutPanel With {.Dock = DockStyle.Fill, .RowCount = 2, .ColumnCount = 1, .Padding = New Padding(Theme.GapSection)}
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Dim text As New TextBox With {.Multiline = True, .ReadOnly = True, .ScrollBars = ScrollBars.Vertical, .Dock = DockStyle.Fill, .Text = changes, .Font = Theme.FontBody}
        Dim buttons As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .FlowDirection = FlowDirection.RightToLeft}
        Dim cancel As New Button With {.Text = "Отмена", .DialogResult = DialogResult.Cancel, .AutoSize = True}
        Dim confirm As New Button With {.Name = "oldConfirmWrite", .Text = "Подтвердить запись", .DialogResult = DialogResult.OK, .AutoSize = True}
        Theme.Secondary(cancel) : Theme.Primary(confirm)
        buttons.Controls.AddRange({cancel, confirm})
        root.Controls.Add(text, 0, 0) : root.Controls.Add(buttons, 0, 1) : Controls.Add(root)
        CancelButton = cancel
        AddHandler Shown, Sub() Theme.FitBoardWindow(Me)
    End Sub
End Class

Partial Public Class frmOldFirmware
    Private ReadOnly writeSettings As New Button With {.Name = "oldWriteSettings"}
    Private ReadOnly editSetting As New Button With {.Name = "oldEditSetting"}
    Private ReadOnly writeButtons As New List(Of Button)
    Private originalSettings As Byte()
    Private editedSettings As Byte()

    Private Sub SetupSettingsWrite(actions As FlowLayoutPanel)
        SetupButton(editSetting, "Изменить параметр", 195)
        SetupButton(writeSettings, "Записать настройки", 205)
        actions.Controls.AddRange({editSetting, writeSettings})
        writeButtons.Add(writeSettings)
        writeSettings.Enabled = device.CanWrite
        AddHandler editSetting.Click, Sub() EditOldSetting(settingsGrid)
        AddHandler settingsGrid.CellDoubleClick, Sub() EditOldSetting(settingsGrid)
        AddHandler schedulesGrid.CellDoubleClick, Sub() EditOldSetting(schedulesGrid)
        AddHandler writeSettings.Click, Async Sub() Await SaveOldSettings()
    End Sub

    Private Sub ShowSettings(block As Byte())
        settings = OldSettings.Table(block)
        BindFields(settingsGrid, settings) : FilterSettings()
        Dim schedules = settings.Clone()
        For Each row As DataRow In settings.Rows
            If CStr(row(0)).StartsWith("Расписание ") Then schedules.ImportRow(row)
        Next
        BindFields(schedulesGrid, schedules)
    End Sub

    Private Sub EditOldSetting(grid As DataGridView)
        If busy OrElse editedSettings Is Nothing Then Return
        Dim row = TryCast(grid.CurrentRow?.DataBoundItem, DataRowView)
        If row Is Nothing Then Return
        Dim index = settings.Rows.Cast(Of DataRow)().ToList().FindIndex(Function(r) CStr(r(1)) = CStr(row(1)))
        If index < 0 Then Return
        Dim field = OldSettings.Fields(index)
        If Not field.Editable Then
            feedback.Text = "Резерв сохраняется без изменений, CRC пересчитывается автоматически." : Return
        End If
        Using dialog As New Form With {.Text = CStr(row(1)), .StartPosition = FormStartPosition.CenterParent, .Size = New Size(Theme.Px(650), Theme.Px(300))}
            Theme.StyleForm(dialog)
            dialog.AutoScroll = True
            AddHandler dialog.Shown, Sub() Theme.FitBoardWindow(dialog)
            Dim input As New TextBox With {.Text = OldSettings.RawValue(editedSettings, index), .UseSystemPasswordChar = field.Secret, .Dock = DockStyle.Top}
            Dim hint As New Label With {.Text = field.Hint, .AutoSize = True, .MaximumSize = New Size(Theme.Px(570), 0)}
            Dim apply As New Button With {.Text = "Применить к черновику", .AutoSize = True}
            Theme.Secondary(apply)
            dialog.Controls.Add(Theme.CardPage(hint, input, apply))
            AddHandler apply.Click, Sub()
                                        Try
                                            editedSettings = OldSettings.Change(editedSettings, index, input.Text)
                                            dialog.DialogResult = DialogResult.OK
                                        Catch ex As Exception
                                            hint.Text = ex.Message
                                        End Try
                                    End Sub
            If dialog.ShowDialog(Me) = DialogResult.OK Then
                ShowSettings(editedSettings)
                feedback.Text = "Изменён черновик. Для записи нажмите «Записать настройки»."
            End If
        End Using
    End Sub

    Private Async Function RunWrite(title As String, changes As String, action As Action, Optional done As Action = Nothing, Optional successMessage As String = "Операция выполнена и проверена.", Optional requiredAccess As Byte = &H20) As Task
        If busy OrElse closePending Then Return
        If Not device.CanWrite Then feedback.Text = device.WriteSupportText : Return
        If String.IsNullOrWhiteSpace(changes) Then feedback.Text = "Изменений нет." : Return
        Using confirmation As New OldChangeDialog(title, changes)
            If confirmation.ShowDialog(Me) <> DialogResult.OK Then Return
        End Using
        Dim pin As UShort? = Nothing
        SetBusy(True) : poll.Stop()
        Try
            pin = GetPassword()
            Await portGate.WaitAsync()
            Try
                Await Task.Run(Sub()
                                   If pin.HasValue Then device.Authenticate(pin.Value)
                                   device.RequireAccess(requiredAccess)
                                   action()
                               End Sub)
            Finally
                portGate.Release()
            End Try
            If done IsNot Nothing Then done()
            feedback.Text = successMessage
        Catch ex As Exception
            feedback.Text = ex.Message
            reconnect.Visible = True
        Finally
            pin = Nothing : SetBusy(False)
            If closePending AndAlso Not polling Then Close() Else poll.Start()
        End Try
    End Function

    Private Async Function SaveOldSettings() As Task
        If originalSettings Is Nothing OrElse editedSettings Is Nothing Then
            feedback.Text = "Сначала прочитайте настройки." : Return
        End If
        Dim changes = OldSettings.Changes(originalSettings, editedSettings)
        If changes = "" Then feedback.Text = "Изменений нет." : Return
        Dim backup As String
        Using dialog As New SaveFileDialog With {.Title = "Резерв настроек перед записью (содержит пароли)", .Filter = "Блок настроек|*.bin", .FileName = "Domovoi_" & device.Imei & "_" & DateTime.Now.ToString("yyyyMMdd_HHmmss") & ".bin"}
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            backup = dialog.FileName
        End Using
        Dim before = CType(originalSettings.Clone(), Byte()), after = CType(editedSettings.Clone(), Byte())
        Await RunWrite("Запись настроек", changes,
                       Sub() device.WriteSettings(before, after, backup),
                       Sub()
                           originalSettings = device.SettingsSnapshot()
                           editedSettings = CType(originalSettings.Clone(), Byte())
                           ShowSettings(editedSettings)
                       End Sub)
    End Function
End Class
