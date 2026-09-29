Imports System.IO
Imports System.Threading.Tasks

Partial Public Class frmOldFirmware
    Private ReadOnly serviceGrid As New DataGridView()
    Private serviceCard As TableLayoutPanel
    Private Function BuildServiceActions() As Control
        Dim actions As New FlowLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Fill}
        Dim names = {"oldTestOutput", "oldAutoregistration", "oldFactoryReset", "oldChronology", "oldWorkLog"}
        Dim titles = {"Проверить выход", "Авторегистрация", "Заводские настройки", "Прочитать хронологию", "Прочитать лог работы"}
        For i = 0 To names.Length - 1
            Dim button As New Button With {.Name = names(i)}
            SetupButton(button, titles(i), 225) : actions.Controls.Add(button)
            If i < 3 Then writeButtons.Add(button) : button.Enabled = device.CanWrite
            Dim operation = i
            AddHandler button.Click, Async Sub() Await ServiceAction(operation)
        Next
        Theme.ReadOnlyGrid(serviceGrid) : serviceGrid.Visible = False
        serviceGrid.MaximumSize = New Size(0, Theme.Px(180))
        serviceGrid.Height = Theme.Px(180) : serviceGrid.Dock = DockStyle.Top
        serviceCard = Theme.Card("Обслуживание старой платы", actions,
                                 New Label With {.AutoSize = True, .Text = device.WriteSupportText})
        Return serviceCard
    End Function
    Private Sub ShowServiceResult(table As DataTable)
        BindFields(serviceGrid, table)
        serviceGrid.Visible = True
        If serviceGrid.Parent Is Nothing Then serviceCard.Controls.Add(serviceGrid)
    End Sub

    Private Async Function ServiceAction(operation As Integer) As Task
        If busy OrElse closePending Then Return
        Select Case operation
            Case 0
                Await RunWrite("Проверка выхода", "Выход: текущее состояние → срабатывание по настройкам импульса/инверсии платы.",
                               Sub() device.TestOutput(), successMessage:="Команда выхода отправлена без ответа. Фактическое срабатывание проверьте на выходе; состояние видно в мониторинге.", requiredAccess:=&H10)
            Case 1
                Dim current As Boolean
                Dim read As Boolean
                Await RunRead(Function()
                                  current = device.AutoregistrationState()
                                  Return New DataTable()
                              End Function, Sub(t) read = True, "")
                If Not read Then Return
                Await RunWrite("Авторегистрация", "Авторегистрация: " & If(current, "включена → выключена", "выключена → включена") & vbCrLf &
                               "При включении плата автоматически добавляет новые номера.", Sub() device.SetAutoregistration(current, Not current))
            Case 2
                If originalSettings Is Nothing OrElse numbers Is Nothing Then
                    feedback.Text = "Перед сбросом прочитайте настройки и базу." : Return
                End If
                Dim before = CType(originalSettings.Clone(), Byte()), baseBefore = numbers.Copy()
                Dim expected = OldFirmware.FactorySettings(device.Identification(5), device.Identification(6))
                Dim changes As String
                If expected IsNot Nothing Then
                    changes = OldSettings.Changes(before, expected)
                Else
                    changes = String.Join(vbCrLf, OldSettings.Table(before).Rows.Cast(Of DataRow)().Where(Function(r) CStr(r(1)) <> "CRC конфигурации").
                                          Select(Function(r) CStr(r(1)) & ": " & If(CStr(r(0)) = "Доступ", "••••", CStr(r(2))) & " → заводское значение прошивки 47 (не подтверждено исходниками)"))
                End If
                changes &= vbCrLf & "База: " & baseBefore.Rows.Count & " записей → 0." & vbCrLf & "Хронология: имеющиеся записи → удалены." & vbCrLf &
                    "Резерв настроек → заводской; пароли → заводские. Будут сохранены BIN (с паролями) и CSV. Восстановление хронологии недоступно."
                Dim folder As String
                Using dialog As New FolderBrowserDialog With {.Description = "Папка для резерва настроек и базы перед сбросом"}
                    If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
                    folder = dialog.SelectedPath
                End Using
                Await RunWrite("Заводские настройки: данные будут удалены", changes, Sub() device.FactoryReset(before, baseBefore, folder),
                               Sub()
                                   originalSettings = Nothing : editedSettings = Nothing : settings = Nothing
                                   numbers = Nothing : numbersGrid.DataSource = Nothing : settingsGrid.DataSource = Nothing : schedulesGrid.DataSource = Nothing
                               End Sub, "Сброс подтверждён платой, настройки и пустая база прочитаны. Перед дальнейшей записью введите пароль заново.")
            Case 3
                Await RunRead(Function() device.ReadChronology(), AddressOf ShowServiceResult, "Получен ответ запроса хронологии.", True, &H10)
            Case 4
                Await RunRead(Function() device.ReadWorkLog(), AddressOf ShowServiceResult, "Получен ответ лога работы.")
        End Select
    End Function
End Class
