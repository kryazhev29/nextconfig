Imports System.IO
Imports System.Threading.Tasks

Partial Public Class frmOldFirmware
    Private ReadOnly testOutput As New Button With {.Name = "oldTestOutputDevice"}
    Private ReadOnly serviceGrid As New DataGridView()
    Private serviceCard As TableLayoutPanel
    Private Function BuildServiceActions() As Control
        Dim actions As New FlowLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Fill}
        Dim names = {"oldTestOutput", "oldAutoregistration", "oldFactoryReset", "oldChronology", "oldWorkLog"}
        Dim titles = {"Проверить выход", "Авторегистрация", "Заводские настройки", "Прочитать хронологию", "Прочитать лог работы"}
        For i = 1 To 2
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
                Dim observed As DataTable = Nothing
                Await RunWrite("Проверка выхода", "Выход 1 сработает по настройкам платы. Ворота или замок могут открыться. Убедитесь, что движение безопасно. Проверить выход?",
                    Sub() observed = device.TestOutputAndReadStatus(),
                    Sub()
                        UpdateStatusCards(observed)
                        BindFields(statusGrid, observed)
                        feedback.Text = If(Field(observed, "Выход активен") = "Да",
                            "Выход включён — это видно по состоянию платы после проверки.",
                            "Выход не включился к моменту чтения состояния. Проверьте режим выхода, длительность импульса и подключение; короткий импульс мог уже закончиться.")
                    End Sub, successMessage:=Nothing, requiredAccess:=&H10)
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
                                   For Each groupCard As Control In settingGroups.Values : groupCard.Dispose() : Next
                                   settingGroups.Clear() : settingEditors.Clear() : settingRows.Clear() : invalidSettings.Clear()
                                   settingsGroup.Items.Clear() : scheduleGroup.Items.Clear()
                                   diagnosticSettings.DataSource = Nothing
                                   draftLabel.Text = "Сначала прочитайте настройки платы."
                                   numbers = Nothing : numbersGrid.DataSource = Nothing
                               End Sub, "Сброс подтверждён платой, настройки и пустая база прочитаны. Перед дальнейшей записью введите пароль заново.")
            Case 3
                Await RunRead(Function() device.ReadChronology(), AddressOf ShowServiceResult, "Получен ответ запроса хронологии.", True, &H10)
            Case 4
                Await RunRead(Function() device.ReadWorkLog(), AddressOf ShowServiceResult, "Получен ответ лога работы.")
        End Select
    End Function

    Private Function BuildMonitoringDiagnostics() As Control
        Dim toggle As New Button With {.Name = "oldMonitorDiagnostics"}
        SetupButton(toggle, "Показать данные состояния", 245)
        Dim identity As New Label With {.AutoSize = True, .Visible = False, .Text = "Загрузчик: 0x" & device.Identification(3).ToString("X2") & " · Тип платы: " & device.Identification(4)}
        statusGrid.Visible = False : monitorTitle.Visible = False
        AddHandler toggle.Click, Sub()
                                     statusGrid.Visible = Not statusGrid.Visible
                                     monitorTitle.Visible = statusGrid.Visible
                                     identity.Visible = statusGrid.Visible
                                     toggle.Text = If(statusGrid.Visible, "Скрыть данные состояния", "Показать данные состояния")
                                 End Sub
        Return Theme.Card("Диагностика состояния", toggle, identity, monitorTitle, statusGrid)
    End Function

    Private Shared Function OldOperatorError(ex As Exception) As String
        If TypeOf ex Is OperationCanceledException Then Return "Действие отменено. При необходимости повторите его."
        Dim message = ex.Message
        If message.Contains("Команда проверки отправлена") Then Return message
        If message.IndexOf("парол", StringComparison.OrdinalIgnoreCase) >= 0 OrElse message.Contains("прав") OrElse message.Contains("доступ") Then
            Return "Не удалось подтвердить доступ. Проверьте пароль и используйте учётные данные с нужными правами."
        End If
        If TypeOf ex Is IOException OrElse TypeOf ex Is TimeoutException OrElse TypeOf ex Is InvalidDataException Then
            Return "Не удалось получить достоверные данные платы. Проверьте питание и USB-подключение, затем повторите чтение. После записи сначала перечитайте данные."
        End If
        Return "Не удалось выполнить действие. Проверьте введённые значения и подключение платы, затем повторите."
    End Function
End Class
