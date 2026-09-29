Imports System.Threading.Tasks

Partial Public Class frmOldFirmware
    Private ReadOnly rfidAddress As New NumericUpDown With {.Name = "oldRfidAddress", .Minimum = 0, .Maximum = 254}
    Private ReadOnly rfidParameter As New ComboBox With {.Name = "oldRfidParameter", .DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly rfidValue As New ComboBox With {.Name = "oldRfidValue"}
    Private rfidOptions As New Dictionary(Of Integer, String)
    Private ReadOnly rfidHint As New Label With {.AutoSize = True}
    Private lastRfid As OldRfid
    Private Function BuildRfidCard() As Control
        Dim read As New Button With {.Name = "oldReadRfid"}
        Dim write As New Button With {.Name = "oldWriteRfid"}
        SetupButton(read, "Прочитать параметр RFID", 245)
        SetupButton(write, "Записать параметр RFID", 245)
        writeButtons.Add(write) : write.Enabled = device.CanWrite
        rfidParameter.Items.AddRange({"Адрес", "Мощность", "Скорость", "Диапазон RU5300", "Область PRIME"})
        rfidParameter.Width = Theme.Px(245) : rfidAddress.Width = Theme.Px(80) : rfidValue.Width = Theme.Px(165)
        Dim fields As New FlowLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Fill}
        fields.Controls.AddRange({New Label With {.Text = "Адрес считывателя", .AutoSize = True}, rfidAddress, rfidParameter,
                                 New Label With {.Text = "Значение", .AutoSize = True}, rfidValue})
        Dim actions As New FlowLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Fill}
        actions.Controls.AddRange({read, write})
        rfidHint.Text = "Выберите параметр и прочитайте его. Тип считывателя берётся из настроек платы."
        AddHandler rfidAddress.ValueChanged, Sub() lastRfid = Nothing
        AddHandler rfidParameter.SelectedIndexChanged, Sub()
                                                          lastRfid = Nothing
                                                          rfidValue.DataSource = Nothing : rfidOptions.Clear()
                                                          Select Case rfidParameter.SelectedIndex
                                                              Case 2
                                                                  Dim speeds = {9600, 19200, 38400, 57600, 115200}
                                                                  For i = 0 To speeds.Length - 1 : rfidOptions(i) = speeds(i) & " бод" : Next
                                                              Case 3
                                                                  rfidOptions(0) = "US" : rfidOptions(1) = "EU"
                                                              Case 4
                                                                  rfidOptions(1) = "EPC" : rfidOptions(2) = "TID" : rfidOptions(3) = "User"
                                                          End Select
                                                          rfidValue.DropDownStyle = If(rfidOptions.Count > 0, ComboBoxStyle.DropDownList, ComboBoxStyle.DropDown)
                                                          If rfidOptions.Count > 0 Then
                                                              rfidValue.DisplayMember = "Value" : rfidValue.ValueMember = "Key"
                                                              rfidValue.DataSource = rfidOptions.ToList() : rfidValue.SelectedIndex = -1
                                                          Else
                                                              rfidValue.Text = ""
                                                          End If
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
                          lastRfid = result
                          If rfidOptions.Count > 0 Then
                              If Not rfidOptions.ContainsKey(result.Value) Then
                                  rfidOptions(result.Value) = "Неизвестное значение (" & result.Value & ")"
                                  rfidValue.DataSource = rfidOptions.ToList()
                              End If
                              rfidValue.SelectedValue = CInt(result.Value)
                          Else
                              rfidValue.Text = result.Value.ToString()
                          End If
                          rfidHint.Text = "Прочитано: " & RfidDisplay(result.Value) & ". " & If(rfidOptions.Count = 0, OldRfid.Hint(result.ReaderType, parameter), "Выберите значение из списка; изменение отправляется только после подтверждения.")
                      End Sub, "Параметр RFID прочитан.", True, &H20)
    End Function
    Private Async Function WriteRfidParameter() As Task
        If busy OrElse lastRfid Is Nothing Then feedback.Text = "Сначала прочитайте выбранный параметр RFID." : Return
        Dim value As Byte
        If rfidOptions.Count > 0 Then
            If rfidValue.SelectedValue Is Nothing Then feedback.Text = "Выберите значение из списка." : Return
            value = CByte(rfidValue.SelectedValue)
        ElseIf Not Byte.TryParse(rfidValue.Text, value) Then
            feedback.Text = "Введите целое значение RFID 0–255." : Return
        End If
        Dim before = lastRfid
        Try
            OldRfid.ValidateValue(before.ReaderType, before.Parameter, value)
        Catch ex As Exception
            feedback.Text = ex.Message : Return
        End Try
        If before.Value = value Then feedback.Text = "Изменений нет." : Return
        Dim after As OldRfid = Nothing
        Await RunWrite("Параметр RFID", "Считыватель " & before.Address & " · " & rfidParameter.Text & ": " & RfidDisplay(before.Value) & " → " & RfidDisplay(value) & vbCrLf & "Изменение адреса или скорости может прервать связь считывателя с платой.",
                       Sub() after = device.WriteRfid(before, value),
                       Sub()
                           rfidAddress.Value = after.Address
                           lastRfid = after
                           rfidHint.Text = "Запись проверена чтением: " & RfidDisplay(after.Value) & "."
                       End Sub)
    End Function
    Private Function RfidDisplay(value As Byte) As String
        Return If(rfidOptions.ContainsKey(value), rfidOptions(CInt(value)), value.ToString())
    End Function
End Class
