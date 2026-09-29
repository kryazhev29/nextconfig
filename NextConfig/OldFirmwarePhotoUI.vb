Imports System.IO
Imports System.Threading.Tasks

Partial Public Class frmOldFirmware
    Private ReadOnly oldShoot As New Button With {.Name = "oldPhoto", .Text = "Сделать снимок"}
    Private ReadOnly headerShoot As New Button With {.Name = "oldPhotoHeader", .Text = "Сделать снимок"}
    Private ReadOnly photoSave As New Button With {.Name = "oldPhotoSaveCard", .Text = "Сохранить…"}
    Private ReadOnly photoOpen As New Button With {.Name = "oldPhotoOpen", .Text = "Открыть крупно"}
    Private ReadOnly reconnect As New Button With {.Name = "oldReconnect"}
    Private ReadOnly photoResolution As New ComboBox With {.Name = "oldResolution", .DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly photoFrame As New PhotoFrame With {.Name = "oldPhotoFrame"}
    Private ReadOnly photoMessage As New Label With {.AutoSize = True}
    ' последний снимок: в рамке, крупно - по кнопке или щелчку по рамке
    Private lastPhoto As Byte()
    Private lastPhotoTime As DateTime

    ' Коды разрешения камеры C328 (Modules/jpeg.h v1–v3): 1, 3, 5, 7.
    Private Shared ReadOnly ResolutionCodes As Byte() = {1, 3, 5, 7}

    ' Карточка как у старого конфигуратора, но аккуратнее: слева рамка 4:3
    ' со снимком, справа действия и сведения о снимке.
    Private Function BuildOldPhotos() As Control
        SetupButton(oldShoot, "Сделать снимок", 220) : Theme.Primary(oldShoot)
        SetupButton(photoSave, "Сохранить…", 220)
        SetupButton(photoOpen, "Открыть крупно", 220)
        SetupButton(reconnect, "Переподключиться к этой плате", 310)
        reconnect.Visible = False
        photoSave.Enabled = False : photoOpen.Enabled = False

        photoResolution.Items.AddRange({"80×64", "160×128", "320×240", "640×480"})
        photoResolution.Width = Theme.Px(220)
        photoResolution.SelectedIndex = OldPhotoPrefs.ResolutionIndex
        AddHandler photoResolution.SelectedIndexChanged, Sub() OldPhotoPrefs.ResolutionIndex = photoResolution.SelectedIndex

        photoFrame.Size = New Size(Theme.Px(480), Theme.Px(360))
        photoFrame.Margin = New Padding(0, 0, Theme.GapSection, 0)
        photoFrame.Placeholder = If(device.SupportsPhoto, "Фотографии нет" & vbLf & "Нажмите «Сделать снимок»", "Эта прошивка не снимает фото по USB")
        AddHandler photoFrame.Click, Sub() ShowLastPhoto()

        photoMessage.Text = device.PhotoSupportText
        photoMessage.Font = Theme.FontCaption : photoMessage.ForeColor = Theme.TextMuted
        photoMessage.MaximumSize = New Size(Theme.Px(320), 0)
        photoMessage.Margin = New Padding(0, Theme.GapSection, 0, 0)
        AgentLog.WatchText(photoMessage, "oldfw.photo")

        Dim resolutionCaption As New Label With {.Text = "Разрешение", .AutoSize = True, .Font = Theme.FontCaption,
            .ForeColor = Theme.TextMuted, .Margin = New Padding(0, Theme.GapRelated, 0, Theme.GapLabel)}
        Dim side As New FlowLayoutPanel With {.FlowDirection = FlowDirection.TopDown, .WrapContents = False, .AutoSize = True, .Margin = New Padding(0)}
        side.Controls.AddRange({oldShoot, resolutionCaption, photoResolution, photoSave, photoOpen, photoMessage, reconnect})
        For Each b In {photoSave, photoOpen}
            b.Margin = New Padding(0, Theme.GapRelated, 0, 0)
        Next

        Dim body As New TableLayoutPanel With {.ColumnCount = 2, .RowCount = 1, .AutoSize = True, .Dock = DockStyle.Fill, .Margin = New Padding(0)}
        body.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        body.Controls.Add(photoFrame, 0, 0)
        body.Controls.Add(side, 1, 0)

        oldShoot.Enabled = device.SupportsPhoto
        photoResolution.Enabled = device.SupportsPhoto
        SetupButton(headerShoot, "Сделать снимок", 190)
        Theme.Primary(headerShoot)
        headerShoot.Margin = New Padding(0, 0, Theme.GapSection, 0)
        headerShoot.Visible = device.SupportsPhoto
        AddHandler headerShoot.Click, Async Sub() Await TakeOldPhoto()
        AddHandler oldShoot.Click, Async Sub() Await TakeOldPhoto()
        AddHandler photoSave.Click, Sub() OldPhotoFiles.Save(Me, lastPhoto, device.Imei, lastPhotoTime, AddressOf ShowSaved)
        AddHandler photoOpen.Click, Sub() ShowLastPhoto()
        AddHandler reconnect.Click, Async Sub() Await ReconnectOld()
        Return Theme.Card("Фото с камеры", body)
    End Function

    Private Sub ShowSaved(path As String)
        photoMessage.Text = "Сохранено: " & path
    End Sub

    ' Снимок без пароля: 0x30 сразу, ход приёма - в рамке, итог - в рамке.
    ' В файл ничего не пишется, пока оператор не нажмёт «Сохранить».
    Private Async Function TakeOldPhoto() As Task
        If busy OrElse closePending OrElse Not device.SupportsPhoto Then Return
        Dim resolution = ResolutionCodes(Math.Max(0, photoResolution.SelectedIndex))
        SetBusy(True)
        photoSave.Enabled = False : photoOpen.Enabled = False
        photoFrame.ShowProgress("Съёмка… камера готовит кадр", -1)
        photoMessage.Text = "Идёт съёмка."
        Dim progress As Action(Of Long, Long) =
            Sub(got, total) BeginInvoke(New Action(Sub() photoFrame.ShowProgress(
                "Приём снимка: " & (got * 100 \ Math.Max(1, total)) & " % · " & got & " из " & total & " байт",
                CSng(got) / Math.Max(1, total))))
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
                photoFrame.SetImage(New Bitmap(source))
                photoMessage.Text = "Снимок " & source.Width & "×" & source.Height & " · " & (jpeg.Length \ 1024) & " КБ · " &
                                    lastPhotoTime.ToString("HH:mm:ss") & vbLf & "Щелчок по снимку открывает его крупно."
            End Using
        Catch ex As Exception
            photoFrame.ShowError(ex.Message)
            photoMessage.Text = ex.Message
            feedback.Text = "Снимок не получен: " & ex.Message
            reconnect.Visible = TypeOf ex Is IOException OrElse TypeOf ex Is TimeoutException
        Finally
            SetBusy(False)
            photoSave.Enabled = lastPhoto IsNot Nothing : photoOpen.Enabled = lastPhoto IsNot Nothing
            If closePending AndAlso Not polling Then Close()
        End Try
    End Function

    Private Sub ShowLastPhoto()
        If lastPhoto Is Nothing OrElse busy Then Return
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

' Рамка снимка: без снимка - подсказка, во время съёмки - ход приёма,
' после - снимок по размеру рамки с полями, при ошибке - её текст.
Public Class PhotoFrame
    Inherits Control
    Private image As Image
    Private status As String = ""
    Private fraction As Single = -1
    Private failed As Boolean
    Public Property Placeholder As String = "Фотографии нет"

    Public Sub New()
        SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.UserPaint Or ControlStyles.ResizeRedraw, True)
        BackColor = Theme.Surface
        AddHandler Disposed, Sub()
                                 If image IsNot Nothing Then image.Dispose()
                             End Sub
    End Sub

    Public Sub SetImage(value As Image)
        Dim previous = image
        image = value : status = "" : fraction = -1 : failed = False
        Cursor = Cursors.Hand
        If previous IsNot Nothing Then previous.Dispose()
        Invalidate()
    End Sub

    Public Sub ShowProgress(text As String, part As Single)
        status = text : fraction = part : failed = False
        Invalidate()
    End Sub

    Public Sub ShowError(text As String)
        status = text : fraction = -1 : failed = True
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
        g.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
        Dim inner = New Rectangle(0, 0, Width - 1, Height - 1)
        g.Clear(If(image Is Nothing, Theme.WindowBack, Color.Black))

        If image IsNot Nothing AndAlso status = "" Then
            Dim k = Math.Min(Width / image.Width, Height / image.Height)
            Dim w = CInt(image.Width * k), h = CInt(image.Height * k)
            g.DrawImage(image, (Width - w) \ 2, (Height - h) \ 2, w, h)
        Else
            Dim text = If(status <> "", status, Placeholder)
            Dim color = If(failed, Theme.Danger, If(status <> "", Theme.TextMain, Theme.TextMuted))
            Using f As New Font(Theme.FontBody.FontFamily, Theme.FontBody.Size * 1.15F, If(status = "", FontStyle.Regular, FontStyle.Bold))
                Dim area = New Rectangle(Theme.Px(24), 0, Width - Theme.Px(48), Height)
                TextRenderer.DrawText(g, text, f, area, color,
                    TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.WordBreak)
            End Using
            If status = "" Then
                ' значок фотоаппарата над подсказкой
                Dim cx = Width \ 2, cy = Height \ 2 - Theme.Px(56)
                Using pen As New Pen(Theme.TextMuted, Theme.Px(2))
                    g.DrawRectangle(pen, cx - Theme.Px(22), cy - Theme.Px(12), Theme.Px(44), Theme.Px(30))
                    g.DrawEllipse(pen, cx - Theme.Px(9), cy - Theme.Px(6), Theme.Px(18), Theme.Px(18))
                    g.DrawRectangle(pen, cx - Theme.Px(8), cy - Theme.Px(18), Theme.Px(16), Theme.Px(6))
                End Using
            End If
            If fraction >= 0 Then
                Dim bar = New Rectangle(Theme.Px(40), Height \ 2 + Theme.Px(30), Width - Theme.Px(80), Theme.Px(6))
                Using back As New SolidBrush(Theme.Border), fore As New SolidBrush(Theme.Accent)
                    g.FillRectangle(back, bar)
                    g.FillRectangle(fore, New Rectangle(bar.X, bar.Y, CInt(bar.Width * Math.Min(1.0F, fraction)), bar.Height))
                End Using
            End If
        End If

        Using border As New Pen(If(failed, Theme.Danger, Theme.Border), 1)
            g.DrawRectangle(border, inner)
        End Using
    End Sub
End Class

' Сохранение снимка по выбору оператора: имя IMEI_дата_время.jpg, папка запоминается.
Public Module OldPhotoFiles
    Public Sub Save(owner As IWin32Window, jpeg As Byte(), imei As String, taken As DateTime, done As Action(Of String))
        If jpeg Is Nothing Then Return
        Dim digits = New String(imei.Where(Function(c) Char.IsDigit(c)).ToArray())
        Using dialog As New SaveFileDialog With {
            .Filter = "Снимок JPEG (*.jpg)|*.jpg", .DefaultExt = "jpg",
            .FileName = digits & "_" & taken.ToString("yyyyMMdd_HHmmss") & ".jpg"}
            Dim folder = OldPhotoPrefs.Folder
            If folder <> "" AndAlso Directory.Exists(folder) Then dialog.InitialDirectory = folder
            If dialog.ShowDialog(owner) <> DialogResult.OK Then Return
            Try
                File.WriteAllBytes(dialog.FileName, jpeg)
                OldPhotoPrefs.Folder = Path.GetDirectoryName(dialog.FileName)
                AgentLog.Write("photo", "event", "saved", "file", dialog.FileName, "bytes", jpeg.Length)
                If done IsNot Nothing Then done(dialog.FileName)
            Catch ex As Exception
                Using f As New frmInfo("Сохранение снимка", "Не удалось сохранить: " & ex.Message)
                    f.ShowDialog(owner)
                End Using
            End Try
        End Using
    End Sub
End Module

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
        OldPhotoFiles.Save(Me, jpeg, imei, taken, Sub(path) info.Text = "Сохранено: " & path)
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
