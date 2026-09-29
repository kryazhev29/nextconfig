Imports System.Threading.Tasks

Partial Public Class frmOldFirmware
    Private ReadOnly rfidAddress As New NumericUpDown With {.Name = "oldRfidAddress", .Minimum = 0, .Maximum = 254}
    Private ReadOnly rfidParameter As New ComboBox With {.Name = "oldRfidParameter", .DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly rfidValue As New TextBox With {.Name = "oldRfidValue"}
    Private ReadOnly rfidHint As New Label With {.AutoSize = True}
    Private lastRfid As OldRfid
    Private Function BuildRfidCard() As Control
        Dim read As New Button With {.Name = "oldReadRfid"}
        Dim write As New Button With {.Name = "oldWriteRfid"}
        SetupButton(read, "Прочитать параметр RFID", 245)
        SetupButton(write, "Записать параметр RFID", 245)
        writeButtons.Add(write) : write.Enabled = device.CanWrite
        rfidParameter.Items.AddRange({"Адрес · 0x03", "Мощность · 0x05", "Скорость · 0x07", "Диапазон RU5300 · 0x3F", "Область PRIME · 0x0A"})
        rfidParameter.Width = Theme.Px(245) : rfidAddress.Width = Theme.Px(80) : rfidValue.Width = Theme.Px(90)
        Dim fields As New FlowLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Fill}
        fields.Controls.AddRange({New Label With {.Text = "Адрес считывателя", .AutoSize = True}, rfidAddress, rfidParameter,
                                 New Label With {.Text = "Значение", .AutoSize = True}, rfidValue})
        Dim actions As New FlowLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Fill}
        actions.Controls.AddRange({read, write})
        rfidHint.Text = "Выберите параметр и прочитайте его. Тип считывателя берётся из настроек платы."
        AddHandler rfidAddress.ValueChanged, Sub() lastRfid = Nothing
        AddHandler rfidParameter.SelectedIndexChanged, Sub()
                                                          lastRfid = Nothing
                                                          rfidValue.Clear()
                                                      End Sub
        AddHandler read.Click, Async Sub() Await ReadRfidParameter()
        AddHandler write.Click, Async Sub() Await WriteRfidParameter()
        Return Theme.Card("RFID-считыватель", fields, actions, rfidHint)
    End Function
    Private Async Function ReadRfidParameter() As Task
        If busy OrElse rfidParameter.SelectedIndex < 0 Then
            feedback.Text = "Выберите параметр RFID." : Return
        End If
        Dim address = CByte(rfidAddress.Value)
        Dim parameter = New Byte() {3, 5, 7, &H3F, &HA}(rfidParameter.SelectedIndex)
        Dim result As OldRfid = Nothing
        lastRfid = Nothing
        Await RunRead(Function()
                          result = device.ReadRfid(address, parameter)
                          Return New DataTable()
                      End Function,
                      Sub(t)
                          lastRfid = result : rfidValue.Text = result.Value.ToString()
                          rfidHint.Text = "Прочитано: " & result.Value & ". " & OldRfid.Hint(result.ReaderType, parameter)
                      End Sub, "Параметр RFID прочитан.", True, &H20)
    End Function
    Private Async Function WriteRfidParameter() As Task
        If busy OrElse lastRfid Is Nothing Then feedback.Text = "Сначала прочитайте выбранный параметр RFID." : Return
        Dim value As Byte
        If Not Byte.TryParse(rfidValue.Text, value) Then feedback.Text = "Введите целое значение RFID 0–255." : Return
        Dim before = lastRfid
        Try
            OldRfid.ValidateValue(before.ReaderType, before.Parameter, value)
        Catch ex As Exception
            feedback.Text = ex.Message : Return
        End Try
        If before.Value = value Then feedback.Text = "Изменений нет." : Return
        Dim after As OldRfid = Nothing
        Await RunWrite("Параметр RFID", "Считыватель " & before.Address & " · " & rfidParameter.Text & ": " & before.Value & " → " & value & vbCrLf & OldRfid.Hint(before.ReaderType, before.Parameter),
                       Sub() after = device.WriteRfid(before, value),
                       Sub()
                           rfidAddress.Value = after.Address
                           lastRfid = after
                           rfidHint.Text = "Запись проверена чтением: " & after.Value & "."
                       End Sub)
    End Function
End Class
