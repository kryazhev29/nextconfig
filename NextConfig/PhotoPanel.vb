Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks

Public Class PhotoPanel
    Inherits TableLayoutPanel
    Private ReadOnly shoot As New Button With {.Text = "Сделать снимок"}
    Private ReadOnly fetch As New Button With {.Text = "Забрать фото"}
    Private ReadOnly stopButton As New Button With {.Text = "Отмена", .Visible = False}
    Private ReadOnly addresses As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly hint As New Label With {.AutoSize = True}
    Private ReadOnly progress As New Label With {.AutoSize = True}
    Private ReadOnly preview As New PictureBox With {.SizeMode = PictureBoxSizeMode.Zoom}
    Private ReadOnly meter As New ProgressBar()
    Private ReadOnly opros As clOpros
    Private ReadOnly imei As String
    Private ReadOnly legacy As Boolean
    Private cancellation As CancellationTokenSource
    Private blocked As Boolean = True
    Private configured As Boolean
    Private lastImage As String
    Private lastFolder As String = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
    Public Event BusyChanged(busy As Boolean)
    Public ReadOnly Property Busy As Boolean
        Get
            Return cancellation IsNot Nothing
        End Get
    End Property

    Public Sub New(reader As clOpros, serial As String, Optional oldFirmware As Boolean = False)
        opros = reader : imei = serial : legacy = oldFirmware
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
        Dim actions As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .Margin = New Padding(0, Theme.GapRelated, 0, Theme.GapRelated)}
        Theme.Primary(shoot) : Theme.Secondary(fetch) : Theme.Secondary(stopButton)
        For Each button In {shoot, fetch, stopButton}
            button.Size = New Size(Theme.Px(155), Theme.ButtonHeightSmall)
            button.Margin = New Padding(0, 0, Theme.GapRelated, Theme.GapRelated)
        Next
        addresses.Font = Theme.FontBody : addresses.Width = Theme.Px(105)
        actions.Controls.AddRange({shoot, fetch, addresses, stopButton})
        Controls.Add(actions)
        For Each label In {hint, progress}
            Theme.Note(label, False) : label.Dock = DockStyle.Fill
            label.Margin = New Padding(0, 0, 0, Theme.GapRelated)
            Controls.Add(label)
        Next
        meter.Dock = DockStyle.Top : meter.Height = Theme.Px(5) : meter.Visible = False
        Controls.Add(meter)
        preview.Dock = DockStyle.Top : preview.Height = Theme.Px(200) : preview.Visible = False
        Controls.Add(preview)
        AddHandler shoot.Click, Async Sub() Await Run(True)
        AddHandler fetch.Click, Async Sub() Await Run(False)
        AddHandler stopButton.Click, Sub() Cancel()
        AddHandler preview.Click, AddressOf ShowPreview
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

    Public Sub SetState(config As clOpros.s_config, updating As Boolean, fresh As Boolean)
        If legacy Then Return
        blocked = updating OrElse Not fresh
        Dim available = {config.periph1, config.periph2, config.periph3, config.periph4,
            config.periph5, config.periph6, config.periph7, config.periph8}.
            Where(Function(p) p.rs485Type = 3).Select(Function(p) p.rs485Adr).Distinct().ToArray()
        configured = config.rs485Mode = 1 AndAlso available.Length > 0
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
            If(Not fresh, "Ожидание свежего состояния платы…", If(configured,
            "Камера C328 · адрес RS-485. Нажмите на фото, чтобы открыть крупнее.",
            "Для съёмки нужны активный RS-485 и периферия C328 (тип 3). Проверьте настройки; они не изменены.")))
        UpdateButtons()
    End Sub

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
        Dim folder As String = Nothing
        If Not capture Then
            Using dialog As New FolderBrowserDialog With {.Description = "Папка для фото " & imei, .ShowNewFolderButton = True, .SelectedPath = lastFolder}
                If dialog.ShowDialog(FindForm()) <> DialogResult.OK Then Return
                folder = dialog.SelectedPath : lastFolder = folder
            End Using
        End If
        Dim address = If(addresses.SelectedItem Is Nothing, CByte(0), CByte(addresses.SelectedItem))
        cancellation = New CancellationTokenSource()
        Dim token = cancellation.Token
        UpdateButtons() : RaiseEvent BusyChanged(True)
        Try
            Dim report As Action(Of PhotoProgress) = Sub(p)
                If Not IsDisposed AndAlso IsHandleCreated Then BeginInvoke(New Action(Sub() Display(p)))
            End Sub
            Await Task.Run(Sub() opros.Photo(capture, address, imei, folder, report, token))
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
        Using stream = File.OpenRead(value.SavedPath), source = Image.FromStream(stream)
            Dim old = preview.Image
            preview.Image = New Bitmap(source)
            If old IsNot Nothing Then old.Dispose()
        End Using
        lastImage = value.SavedPath : preview.Visible = True
    End Sub

    Private Sub ShowPreview(sender As Object, e As EventArgs)
        If preview.Image Is Nothing Then Return
        Using window As New Form With {.Text = Path.GetFileName(lastImage), .StartPosition = FormStartPosition.CenterParent,
            .Size = New Size(Theme.Px(720), Theme.Px(560)), .BackColor = Theme.WindowBack, .Font = Theme.FontBody}
            window.Controls.Add(New PictureBox With {.Dock = DockStyle.Fill, .SizeMode = PictureBoxSizeMode.Zoom, .Image = preview.Image})
            window.ShowDialog(FindForm())
        End Using
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            Cancel()
            If preview.Image IsNot Nothing Then preview.Image.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
