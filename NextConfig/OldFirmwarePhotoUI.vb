Imports System.IO
Imports System.Threading.Tasks

Partial Public Class frmOldFirmware
    Private ReadOnly oldShoot As New Button With {.Name = "oldPhoto", .Text = "Снимок старой камеры"}
    Private ReadOnly reconnect As New Button With {.Name = "oldReconnect"}
    Private ReadOnly photoResolution As New ComboBox With {.Name = "oldResolution", .DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly photoPreview As New PictureBox With {.SizeMode = PictureBoxSizeMode.Zoom}
    Private ReadOnly photoMessage As New Label With {.AutoSize = True}

    Private Function BuildOldPhotos() As Control
        SetupButton(oldShoot, "Снимок старой камеры", 225)
        SetupButton(reconnect, "Переподключиться к этой плате", 310)
        reconnect.Visible = False
        photoResolution.Items.AddRange({"80×64", "160×128", "320×240", "640×480"})
        photoResolution.Width = Theme.Px(130)
        photoPreview.Height = Theme.Px(200) : photoPreview.Dock = DockStyle.Top
        photoPreview.Visible = False
        photoMessage.Text = device.PhotoSupportText
        Dim actions As New FlowLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Fill}
        actions.Controls.AddRange({oldShoot, photoResolution, reconnect})
        oldShoot.Enabled = device.SupportsPhoto
        photoResolution.Visible = device.SupportsPhoto
        AddHandler oldShoot.Click, Async Sub() Await TakeOldPhoto()
        AddHandler reconnect.Click, Async Sub() Await ReconnectOld()
        AddHandler FormClosed, Sub()
                                  If photoPreview.Image IsNot Nothing Then photoPreview.Image.Dispose()
                              End Sub
        Return Theme.Card("Фото с камеры", photoMessage, actions, photoPreview)
    End Function

    Private Async Function TakeOldPhoto() As Task
        If busy OrElse closePending OrElse Not device.SupportsPhoto Then Return
        If photoResolution.SelectedIndex < 0 Then
            feedback.Text = "Выберите разрешение снимка." : Return
        End If
        Dim folder As String
        Using dialog As New FolderBrowserDialog With {.Description = "Папка для JPEG с камеры"}
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            folder = dialog.SelectedPath
        End Using
        Dim resolution = New Byte() {1, 3, 5, 7}(photoResolution.SelectedIndex)
        SetBusy(True)
        photoMessage.Text = "Приём фото…"
        Try
            Await portGate.WaitAsync()
            Dim path As String
            Try
                path = Await Task.Run(Function() device.CapturePhoto(folder, resolution))
            Finally
                portGate.Release()
            End Try
            Using stream = File.OpenRead(path), source = Image.FromStream(stream)
                Dim previous = photoPreview.Image
                photoPreview.Image = New Bitmap(source)
                If previous IsNot Nothing Then previous.Dispose()
            End Using
            photoPreview.Visible = True : photoMessage.Text = "Сохранено: " & path
        Catch ex As Exception
            photoMessage.Text = ex.Message
            reconnect.Visible = True
        Finally
            SetBusy(False)
            If closePending AndAlso Not polling Then Close()
        End Try
    End Function

    Private Async Function ReconnectOld() As Task
        Await RunRead(Function()
                          device.Reconnect()
                          Return device.ReadStatus()
                      End Function,
                      Sub(t)
                          UpdateStatusCards(t)
                          reconnect.Visible = False
                          poll.Start()
                      End Sub, "Связь с той же платой восстановлена. При необходимости введите пароль заново.")
    End Function
End Class
