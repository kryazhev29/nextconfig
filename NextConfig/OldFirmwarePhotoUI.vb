Imports System.IO
Imports System.Threading.Tasks

Partial Public Class frmOldFirmware
    Private ReadOnly oldShoot As New Button With {.Name = "oldPhoto", .Text = "Сделать снимок"}
    Private ReadOnly reconnect As New Button With {.Name = "oldReconnect"}
    Private ReadOnly photoResolution As New ComboBox With {.Name = "oldResolution", .DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly photoPreview As New PictureBox With {.SizeMode = PictureBoxSizeMode.Zoom, .Cursor = Cursors.Hand}
    Private ReadOnly photoMessage As New Label With {.AutoSize = True}
    ' последний снимок: окно просмотра открывается повторно щелчком по превью
    Private lastPhoto As Byte()
    Private lastPhotoTime As DateTime

    ' Коды разрешения камеры C328 (Modules/jpeg.h v1–v3): 1, 3, 5, 7.
    Private Shared ReadOnly ResolutionCodes As Byte() = {1, 3, 5, 7}

    Private Function BuildOldPhotos() As Control
        SetupButton(oldShoot, "Сделать снимок", 200)
        Theme.Primary(oldShoot)
        SetupButton(reconnect, "Переподключиться к этой плате", 310)
        reconnect.Visible = False
        photoResolution.Items.AddRange({"80×64", "160×128", "320×240", "640×480"})
        photoResolution.Width = Theme.Px(130)
        photoResolution.SelectedIndex = OldPhotoPrefs.ResolutionIndex
        AddHandler photoResolution.SelectedIndexChanged, Sub() OldPhotoPrefs.ResolutionIndex = photoResolution.SelectedIndex
        photoPreview.Height = Theme.Px(200) : photoPreview.Dock = DockStyle.Top
        photoPreview.Visible = False
        AddHandler photoPreview.Click, Sub() ShowLastPhoto()
        photoMessage.Text = device.PhotoSupportText
        AgentLog.WatchText(photoMessage, "oldfw.photo")
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

    ' Снимок без пароля: 0x30 сразу, ход приёма в байтах, итог - в окне просмотра.
    ' В файл ничего не пишется, пока оператор не нажмёт «Сохранить».
    Private Async Function TakeOldPhoto() As Task
        If busy OrElse closePending OrElse Not device.SupportsPhoto Then Return
        Dim resolution = ResolutionCodes(Math.Max(0, photoResolution.SelectedIndex))
        SetBusy(True)
        photoMessage.Text = "Съёмка… камера готовит кадр"
        Dim progress As Action(Of Long, Long) =
            Sub(got, total) BeginInvoke(New Action(Sub() photoMessage.Text = "Приём фото: " & got & " из " & total & " байт"))
        Try
            Await portGate.WaitAsync()
            Dim jpeg As Byte()
            Try
                jpeg = Await Task.Run(Function() device.CapturePhoto(resolution, progress))
            Finally
                portGate.Release()
            End Try
            lastPhoto = jpeg
            lastPhotoTime = DateTime.Now
            Using stream As New MemoryStream(jpeg), source = Image.FromStream(stream)
                Dim previous = photoPreview.Image
                photoPreview.Image = New Bitmap(source)
                If previous IsNot Nothing Then previous.Dispose()
                photoMessage.Text = "Снимок " & source.Width & "×" & source.Height & ", " & jpeg.Length & " байт · " &
                                    lastPhotoTime.ToString("HH:mm:ss") & ". Щёлкните по снимку, чтобы открыть его снова."
            End Using
            photoPreview.Visible = True
            ShowLastPhoto()
        Catch ex As Exception
            photoMessage.Text = ex.Message
            feedback.Text = "Снимок не получен: " & ex.Message
            reconnect.Visible = TypeOf ex Is IOException OrElse TypeOf ex Is TimeoutException
            Using f As New frmInfo("Фото с камеры", ex.Message)
                f.ShowDialog(Me)
            End Using
        Finally
            SetBusy(False)
            If closePending AndAlso Not polling Then Close()
        End Try
    End Function

    Private Sub ShowLastPhoto()
        If lastPhoto Is Nothing Then Return
        Using viewer As New frmOldPhotoView(lastPhoto, device.Imei, lastPhotoTime)
            viewer.ShowDialog(Me)
        End Using
    End Sub

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

' Окно снимка: крупно, по размеру окна; сохранить - по желанию оператора.
Public Class frmOldPhotoView
    Inherits Form

    Private ReadOnly jpeg As Byte()
    Private ReadOnly imei As String
    Private ReadOnly taken As DateTime
    Private ReadOnly picture As New PictureBox With {.Dock = DockStyle.Fill, .SizeMode = PictureBoxSizeMode.Zoom}
    Private ReadOnly info As New Label With {.AutoSize = True}
    Private ReadOnly save As New Button With {.Name = "oldPhotoSave", .Text = "Сохранить"}
    Private ReadOnly closeButton As New Button With {.Name = "oldPhotoClose", .Text = "Закрыть"}

    Public Sub New(data As Byte(), serial As String, time As DateTime)
        jpeg = data : imei = serial : taken = time
        AutoScaleMode = AutoScaleMode.None : Theme.StyleForm(Me)
        Text = "Снимок с камеры · " & serial
        StartPosition = FormStartPosition.CenterParent
        Dim area = Screen.FromPoint(Cursor.Position).WorkingArea
        Size = New Size(Math.Min(Theme.Px(900), area.Width), Math.Min(Theme.Px(760), area.Height))
        MinimumSize = New Size(Theme.Px(480), Theme.Px(400))

        Using stream As New MemoryStream(jpeg), source = Image.FromStream(stream)
            picture.Image = New Bitmap(source)
            info.Text = source.Width & "×" & source.Height & " · " & jpeg.Length & " байт · " & taken.ToString("dd.MM.yyyy HH:mm:ss")
        End Using
        picture.BackColor = Color.Black

        Theme.Primary(save) : Theme.Secondary(closeButton)
        info.Font = Theme.FontCaption : info.ForeColor = Theme.TextMuted
        info.Margin = New Padding(0, Theme.GapRelated, Theme.GapSection, 0)
        Dim bar As New FlowLayoutPanel With {.Dock = DockStyle.Bottom, .AutoSize = True, .FlowDirection = FlowDirection.RightToLeft,
            .Padding = New Padding(Theme.GapSection), .BackColor = Theme.Surface}
        bar.Controls.AddRange({closeButton, save, info})
        Controls.Add(picture)
        Controls.Add(bar)
        AcceptButton = save : CancelButton = closeButton
        AddHandler save.Click, Sub() SaveJpeg()
        AddHandler closeButton.Click, Sub() Close()
        AddHandler FormClosed, Sub() picture.Image.Dispose()
    End Sub

    Private Sub SaveJpeg()
        Dim digits = New String(imei.Where(Function(c) Char.IsDigit(c)).ToArray())
        Using dialog As New SaveFileDialog With {
            .Filter = "Снимок JPEG (*.jpg)|*.jpg", .DefaultExt = "jpg",
            .FileName = digits & "_" & taken.ToString("yyyyMMdd_HHmmss") & ".jpg"}
            Dim folder = OldPhotoPrefs.Folder
            If folder <> "" AndAlso Directory.Exists(folder) Then dialog.InitialDirectory = folder
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                File.WriteAllBytes(dialog.FileName, jpeg)
                OldPhotoPrefs.Folder = Path.GetDirectoryName(dialog.FileName)
                AgentLog.Write("photo", "event", "saved", "file", dialog.FileName, "bytes", jpeg.Length)
                info.Text = "Сохранено: " & dialog.FileName
            Catch ex As Exception
                Using f As New frmInfo("Сохранение снимка", "Не удалось сохранить: " & ex.Message)
                    f.ShowDialog(Me)
                End Using
            End Try
        End Using
    End Sub
End Class

' Разрешение и папка снимков старой платы между запусками программы.
Public Module OldPhotoPrefs
    Private ReadOnly Property FilePath As String
        Get
            Return Path.Combine(AgentLog.Folder, "oldphoto.txt")
        End Get
    End Property

    Private Function Read(index As Integer) As String
        Try
            Dim lines = File.ReadAllLines(FilePath)
            If index < lines.Length Then Return lines(index)
        Catch
        End Try
        Return ""
    End Function

    Private Sub Store(index As Integer, value As String)
        Try
            Dim lines = {Read(0), Read(1)}
            lines(index) = value
            Directory.CreateDirectory(AgentLog.Folder)
            File.WriteAllLines(FilePath, lines)
        Catch
        End Try
    End Sub

    ' по умолчанию 640×480
    Public Property ResolutionIndex As Integer
        Get
            Dim n As Integer
            If Integer.TryParse(Read(0), n) AndAlso n >= 0 AndAlso n <= 3 Then Return n
            Return 3
        End Get
        Set(value As Integer)
            If value >= 0 Then Store(0, value.ToString())
        End Set
    End Property

    Public Property Folder As String
        Get
            Return Read(1)
        End Get
        Set(value As String)
            Store(1, value)
        End Set
    End Property
End Module
