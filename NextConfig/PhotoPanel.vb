Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks

Public Class PhotoPanel
    Inherits TableLayoutPanel
    Private ReadOnly shoot As New Button With {.Text = "Сделать снимок"}
    ' Снимок забирается сам после «Сделать снимок»; отдельная кнопка скрыта.
    Private ReadOnly fetch As New Button With {.Text = "Забрать фото", .Visible = False}
    Private ReadOnly stopButton As New Button With {.Text = "Отмена", .Visible = False}
    Private ReadOnly save As New Button With {.Text = "Сохранить…", .Enabled = False}
    Private ReadOnly open As New Button With {.Text = "Открыть крупно", .Enabled = False}
    Private ReadOnly addresses As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly addressCaption As Label
    Private ReadOnly hint As New Label With {.AutoSize = True}
    Private ReadOnly progress As New Label With {.AutoSize = True}
    Private ReadOnly preview As New PhotoFrame With {.Name = "photoFrame"}
    Private ReadOnly meter As New Meter With {.Maximum = 100}
    Private ReadOnly opros As clOpros
    Private ReadOnly imei As String
    Private ReadOnly legacy As Boolean
    Private cancellation As CancellationTokenSource
    Private blocked As Boolean = True
    Private configured As Boolean
    Private plateAddresses As Byte() = {}
    Private lastImage As String
    Private lastPhoto As Byte()
    Private lastPhotoTime As DateTime
    Private lastFolder As String = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
    Public Event BusyChanged(busy As Boolean)
    Public ReadOnly Property Busy As Boolean
        Get
            Return cancellation IsNot Nothing
        End Get
    End Property

    Public Sub New(reader As clOpros, serial As String, Optional oldFirmware As Boolean = False)
        opros = reader : imei = serial : legacy = oldFirmware
        AgentLog.WatchText(progress, "photo")
        If Demo.IsOn Then
            Dim arg = Environment.GetCommandLineArgs().FirstOrDefault(Function(a) a.StartsWith("--demo-photo-folder="))
            If arg IsNot Nothing AndAlso Directory.Exists(arg.Substring(20)) Then lastFolder = arg.Substring(20)
        End If
        Theme.SurfaceCard(Me)
        BackColor = Theme.Surface : Dock = DockStyle.Fill : AutoSize = True
        ColumnCount = 1 : Padding = New Padding(Theme.GapSection)
        Margin = New Padding(0, 0, 0, Theme.GapSection)
        Dim heading As New Label With {.Text = "Фото с камеры", .AutoSize = True}
        Theme.Group(heading)
        Controls.Add(heading)
        Dim actions As New FlowLayoutPanel With {.FlowDirection = FlowDirection.TopDown, .WrapContents = False,
            .AutoSize = True, .Margin = New Padding(0)}
        Theme.Primary(shoot) : Theme.Secondary(fetch) : Theme.Secondary(stopButton)
        Theme.Secondary(save) : Theme.Secondary(open)
        For Each button In {shoot, fetch, stopButton, save, open}
            button.Size = New Size(Theme.Px(220), Theme.ButtonHeightSmall)
            button.Margin = New Padding(0, 0, Theme.GapRelated, Theme.GapRelated)
        Next
        addresses.Font = Theme.FontBody : addresses.Width = Theme.Px(220)
        addressCaption = New Label With {.Text = "Камера · адрес RS-485", .AutoSize = True,
            .Font = Theme.FontCaption, .ForeColor = Theme.TextMuted,
            .Margin = New Padding(0, Theme.GapRelated, 0, Theme.GapLabel), .Visible = Not legacy}
        actions.Controls.AddRange({shoot, fetch, addressCaption, addresses, save, open, stopButton})
        For Each label In {hint, progress}
            Theme.Note(label, False)
            label.MaximumSize = New Size(Theme.Px(320), 0)
            label.Margin = New Padding(0, Theme.GapRelated, 0, Theme.GapRelated)
            actions.Controls.Add(label)
        Next
        meter.Size = New Size(Theme.Px(220), Theme.Px(5)) : meter.Visible = False
        actions.Controls.Add(meter)
        preview.Size = New Size(Theme.Px(480), Theme.Px(360))
        preview.Margin = New Padding(0, 0, Theme.GapSection, 0)
        preview.Placeholder = "Фотографии нет" & vbLf & "Сделайте снимок или заберите фото с платы"
        Dim body As New TableLayoutPanel With {.ColumnCount = 2, .RowCount = 1, .AutoSize = True,
            .Dock = DockStyle.Fill, .Margin = New Padding(0, Theme.GapRelated, 0, 0)}
        body.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        body.Controls.Add(preview, 0, 0)
        body.Controls.Add(actions, 1, 0)
        Controls.Add(body)
        AddHandler shoot.Click, Async Sub() Await Run(True)
        AddHandler fetch.Click, Async Sub() Await Run(False)
        AddHandler stopButton.Click, Sub() Cancel()
        AddHandler preview.Click, AddressOf ShowPreview
        AddHandler open.Click, AddressOf ShowPreview
        AddHandler save.Click, Sub() OldPhotoFiles.Save(FindForm(), lastPhoto, imei, lastPhotoTime,
            Sub(path) progress.Text = "Сохранено: " & path)
        If legacy Then
            hint.Text = "Фото по USB не поддерживается этой прошивкой v5."
            addresses.Visible = False : blocked = False
        Else
            hint.Text = "Ожидание состояния и настроек камеры…"
            shoot.Enabled = False : fetch.Enabled = False
        End If
    End Sub

    Public Sub Cancel()
        If cancellation IsNot Nothing Then cancellation.Cancel()
    End Sub

    ' camera232 - старая плата MODEM (IP v3): камера одна, на порту 232, как в старой прошивке;
    ' настройки RS-485 к ней не относятся (прошивка ветки fix/ipv3-photo-rs232).
    ' wifiPort - выбран Wi-Fi-модем (USB 113): мост занимает весь порт RS-485, прошивка камеру запрещает.
    Public Sub SetState(config As clOpros.s_config, updating As Boolean, fresh As Boolean, Optional camera232 As Boolean = False,
                        Optional wifiPort As Boolean = False)
        If legacy Then Return
        blocked = updating OrElse Not fresh
        Dim cameras = {config.periph1, config.periph2, config.periph3, config.periph4,
            config.periph5, config.periph6, config.periph7, config.periph8}.Where(Function(p) PeripheryNames.IsCamera(p.rs485Type))
        Dim available = cameras.Select(Function(p) p.rs485Adr).Distinct().ToArray()
        plateAddresses = cameras.Where(Function(p) p.rs485Type = PeripheryNames.PlateCameraType).Select(Function(p) p.rs485Adr).ToArray()
        configured = config.rs485Mode = 1 AndAlso available.Length > 0 AndAlso Not wifiPort
        addressCaption.Visible = Not camera232 : addresses.Visible = Not camera232
        If camera232 Then
            configured = True
            If Not Busy Then addresses.Items.Clear()
            hint.Text = If(updating, "Идёт обновление прошивки. Съёмка и выгрузка недоступны.",
                If(Not fresh, "Ожидание свежего состояния платы…", "Камера C328 · порт 232. Нажмите на фото, чтобы открыть крупнее."))
            UpdateButtons()
            Return
        End If
        If Not Busy Then
            Dim selected = addresses.SelectedItem
            addresses.Items.Clear()
            For Each address In available
                addresses.Items.Add(address)
            Next
            If selected IsNot Nothing AndAlso addresses.Items.Contains(selected) Then addresses.SelectedItem = selected
            If addresses.SelectedIndex < 0 AndAlso addresses.Items.Count > 0 Then addresses.SelectedIndex = 0
        End If
        hint.Text = If(updating, "Идёт обновление прошивки. Съёмка и выгрузка недоступны.",
            If(Not fresh, "Ожидание свежего состояния платы…", If(wifiPort,
            "Камера недоступна: порт RS-485 занят Wi-Fi-мостом.", If(Not configured,
            "Для съёмки нужны активный RS-485 и камера в таблице RS-485: JPEG (тип 9) или камера номеров (тип 8). Проверьте настройки; они не изменены.",
            If(PlateSelected(), "Камера номеров · адрес RS-485: снимок только пока номер в кадре. Нажмите на фото, чтобы открыть крупнее.",
            "Камера C328 · адрес RS-485. Нажмите на фото, чтобы открыть крупнее.")))))
        UpdateButtons()
    End Sub

    Private Function PlateSelected() As Boolean
        Return addresses.SelectedItem IsNot Nothing AndAlso plateAddresses.Contains(CByte(addresses.SelectedItem))
    End Function

    Private Sub UpdateButtons()
        shoot.Enabled = Not Busy AndAlso Not blocked AndAlso (configured OrElse legacy)
        fetch.Enabled = Not Busy AndAlso Not blocked
        addresses.Enabled = Not Busy : stopButton.Visible = Busy
    End Sub

    Private Async Function Run(capture As Boolean) As Task
        If legacy Then
            ' v5 User/user_fun.c:752-759: 0x30 => 250, 0x31/0x32 пусты.
            progress.Text = "Фото не поддерживается этой прошивкой: 0x30 возвращает код 250; выгрузки по USB нет."
            Return
        End If
        If Busy OrElse blocked Then Return
        ' Снимок сразу забирается во временную папку и показывается в рамке; на диск к
        ' оператору - только кнопкой «Сохранить…».
        Dim folder As String = If(capture, Path.Combine(Path.GetTempPath(), "NextConfig_photo", imei), Nothing)
        If folder IsNot Nothing Then Directory.CreateDirectory(folder)
        If Not capture Then
            Using dialog As New FolderBrowserDialog With {.Description = "Папка для фото " & imei, .ShowNewFolderButton = True, .SelectedPath = lastFolder}
                If dialog.ShowDialog(FindForm()) <> DialogResult.OK Then Return
                folder = dialog.SelectedPath : lastFolder = folder
            End Using
        End If
        Dim address = If(addresses.SelectedItem Is Nothing, CByte(0), CByte(addresses.SelectedItem))
        Dim plateCamera = PlateSelected()
        cancellation = New CancellationTokenSource()
        Dim token = cancellation.Token
        UpdateButtons() : RaiseEvent BusyChanged(True)
        Try
            Dim report As Action(Of PhotoProgress) = Sub(p)
                If Not IsDisposed AndAlso IsHandleCreated Then BeginInvoke(New Action(Sub() Display(p)))
            End Sub
            Await Task.Run(Sub() opros.Photo(capture, address, imei, folder, report, token, plateCamera))
        Catch ex As OperationCanceledException
            progress.Text = "Операция отменена. Принятая часть сохранена; для продолжения выберите ту же папку."
        Catch ex As Exception
            progress.Text = ex.Message
        Finally
            cancellation.Dispose() : cancellation = Nothing
            meter.Visible = False
            UpdateButtons() : RaiseEvent BusyChanged(False)
        End Try
    End Function

    Private Sub Display(value As PhotoProgress)
        If IsDisposed Then Return
        progress.Text = value.Text
        meter.Visible = Busy AndAlso value.Total > 0
        meter.Value = If(value.Total = 0, 0, Math.Min(100, CInt(100.0 * value.Received / value.Total)))
        If value.SavedPath Is Nothing OrElse value.SavedPath = lastImage Then Return
        Dim jpeg = File.ReadAllBytes(value.SavedPath)
        Using stream As New MemoryStream(jpeg), source = Image.FromStream(stream)
            preview.SetImage(New Bitmap(source))
        End Using
        lastPhoto = jpeg : lastPhotoTime = File.GetLastWriteTime(value.SavedPath)
        lastImage = value.SavedPath
        save.Enabled = True : open.Enabled = True
    End Sub

    Private Sub ShowPreview(sender As Object, e As EventArgs)
        If lastPhoto Is Nothing Then Return
        Using window As New frmOldPhotoView(lastPhoto, imei, lastPhotoTime)
            window.ShowDialog(FindForm())
        End Using
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            Cancel()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
