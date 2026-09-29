Imports System.IO
Imports System.Threading.Tasks

Partial Public Class frmOldFirmware
    Private Sub SetupNumberWrites(actions As FlowLayoutPanel)
        Dim titles = {"Добавить номер", "Изменить номер", "Удалить номер", "Загрузить базу CSV", "Сжать базу"}
        Dim names = {"oldAddNumber", "oldEditNumber", "oldDeleteNumber", "oldImportNumbers", "oldCompressNumbers"}
        For i = 0 To titles.Length - 1
            Dim button As New Button With {.Name = names(i)}
            SetupButton(button, titles(i), 190)
            actions.Controls.Add(button) : writeButtons.Add(button)
            button.Enabled = device.CanWrite
            Dim operation = i
            AddHandler button.Click, Async Sub() Await NumberAction(operation)
        Next
    End Sub

    Private Function EditNumberRecord(source As Byte()) As Byte()
        Dim result As Byte() = Nothing
        Using dialog As New Form With {.Text = If(source Is Nothing, "Добавление номера", "Изменение номера"), .StartPosition = FormStartPosition.CenterParent, .Size = New Size(Theme.Px(670), Theme.Px(620))}
            Theme.StyleForm(dialog)
            Dim fields As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 2, .Padding = New Padding(Theme.GapSection)}
            fields.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 48)) : fields.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 52))
            Dim idBox As New TextBox With {.MaxLength = 10}
            Dim nameBox As New TextBox With {.MaxLength = 19}
            Dim kind As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
            Dim access As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
            kind.Items.AddRange({"Телефон", "1-Wire", "RFID"})
            access.Items.AddRange({"Пользователь", "Управляющий", "Администратор", "IP-пользователь"})
            Dim expires As New TextBox With {.Text = "0"}
            Dim passages As New TextBox With {.Text = "0"}
            Dim schedule As New TextBox With {.Text = "0"}
            If source IsNot Nothing Then
                idBox.Text = OldNumbers.Id(source)
                nameBox.Text = System.Text.Encoding.GetEncoding(1251).GetString(source, 12, 20).Split(ChrW(0))(0)
                Dim code = (source(32) >> 4) And 3
                kind.SelectedIndex = If(code < 3, code, -1)
                access.SelectedIndex = source(32) And 3
                expires.Text = BitConverter.ToUInt32(source, 33).ToString()
                passages.Text = source(37).ToString() : schedule.Text = source(38).ToString()
            End If
            Dim captions = {"Номер / ID (10 символов)", "Имя", "Тип", "Уровень доступа", "Срок: Unix, 0 — без срока", "Лимит проходов (0–255)", "Расписание (0–7)"}
            Dim controls As Control() = {idBox, nameBox, kind, access, expires, passages, schedule}
            For i = 0 To controls.Length - 1
                fields.Controls.Add(New Label With {.Text = captions(i), .AutoSize = True}, 0, i)
                controls(i).Dock = DockStyle.Fill : fields.Controls.Add(controls(i), 1, i)
            Next
            Dim hint As New Label With {.AutoSize = True, .MaximumSize = New Size(Theme.Px(570), 0), .Text = "Телефон без +7. ID — шестнадцатеричный. Счётчик использованных проходов будет сброшен."}
            Dim apply As New Button With {.Text = "Просмотреть изменения", .AutoSize = True}
            Theme.Secondary(apply)
            fields.Controls.Add(hint, 0, controls.Length) : fields.SetColumnSpan(hint, 2)
            fields.Controls.Add(apply, 1, controls.Length + 1)
            dialog.Controls.Add(fields)
            AddHandler apply.Click, Sub()
                                        Try
                                            result = OldNumbers.Edit(source, idBox.Text, nameBox.Text, kind.SelectedIndex, access.SelectedIndex,
                                                                     UInteger.Parse(expires.Text), Byte.Parse(passages.Text), Byte.Parse(schedule.Text))
                                            dialog.DialogResult = DialogResult.OK
                                        Catch ex As Exception
                                            hint.Text = "Проверьте поля: " & ex.Message
                                        End Try
                                    End Sub
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return Nothing
        End Using
        Return result
    End Function

    Private Async Function NumberAction(operation As Integer) As Task
        If busy OrElse closePending Then Return
        If numbers Is Nothing Then feedback.Text = "Сначала прочитайте базу номеров." : Return
        Dim before = numbers.Copy()
        Dim original As Byte() = Nothing, replacement As Byte() = Nothing
        Dim after As DataTable = Nothing
        Dim changes As String
        Try
            If operation = 1 OrElse operation = 2 Then
                Dim selected = TryCast(numbersGrid.CurrentRow?.DataBoundItem, DataRowView)
                If selected Is Nothing Then feedback.Text = "Выберите запись." : Return
                original = OldNumbers.Record(selected.Row)
            End If
            If operation <= 1 Then
                replacement = EditNumberRecord(original)
                If replacement Is Nothing Then Return
            End If
            If operation = 3 Then
                Using dialog As New OpenFileDialog With {.Title = "CSV, сохранённый NextConfig", .Filter = "База номеров CSV|*.csv"}
                    If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
                    after = OldNumbers.LoadCsv(dialog.FileName, device.NumberLimit)
                End Using
            Else
                Dim records = OldNumbers.Records(before)
                If original IsNot Nothing Then records.RemoveAll(Function(r) OldNumbers.Id(r) = OldNumbers.Id(original))
                If replacement IsNot Nothing Then records.Add(replacement)
                after = OldNumbers.ToTable(records)
            End If
            changes = OldNumbers.Changes(before, after)
            If operation = 4 Then
                changes = "Сжатие: разрозненные ячейки → уплотнённая база." & vbCrLf & "Записей: " & before.Rows.Count & " → " & before.Rows.Count
            ElseIf operation = 3 Then
                changes &= vbCrLf & "Полная замена базы. Использованные проходы всех записей → 0. При обрыве возможна частичная загрузка."
            ElseIf replacement IsNot Nothing Then
                changes &= vbCrLf & "Использованные проходы этой записи → 0."
                If original IsNot Nothing Then changes &= vbCrLf & "Изменение выполняется удалением и добавлением; при обрыве между командами запись может отсутствовать."
            End If
        Catch ex As Exception
            feedback.Text = ex.Message : Return
        End Try
        Dim backup As String
        Using dialog As New SaveFileDialog With {.Title = "Резерв базы перед изменением", .Filter = "База номеров CSV|*.csv", .FileName = "Domovoi_base_" & DateTime.Now.ToString("yyyyMMdd_HHmmss") & ".csv"}
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            backup = dialog.FileName
        End Using
        Await RunWrite("Изменение базы номеров", changes,
                       Sub()
                           Select Case operation
                               Case 3 : device.ImportNumbers(before, after, backup)
                               Case 4
                                   If File.Exists(backup) Then Throw New IOException("Резерв уже существует. Выберите новое имя.")
                                   OldFirmware.SaveCsv(before, backup)
                                   device.CompressNumbers(before)
                               Case Else : device.ChangeNumber(before, original, replacement, backup)
                           End Select
                       End Sub)
        ' Даже при ошибке перечитываем частично изменённую базу; старый экспорт очищается.
        If Not closePending Then
            Dim operationResult = feedback.Text
            Await ReadDatabase()
            feedback.Text = operationResult & " " & feedback.Text
        End If
    End Function
End Class
