' Доработка: Кряжев А., 2026
' Камера номеров Interceptor (тип RS-485 8): связь из статуса 101 и считывание номера командой 6
' (источник 3 — RS-485, адрес камеры). Номер приходит пакетом 8 в канал запроса: в USB — у прошивки
' с 29.10.2026, прежняя отдаёт его только серверу. Карточка видна, когда камера есть в таблице RS-485.
Public Class PlatePanel
    Inherits TableLayoutPanel
    Public Const Seconds As Byte = 15         ' окно захвата на плате
    Private Const GraceSeconds As Integer = 3 ' «время вышло» плата шлёт на своём секундном такте
    Private ReadOnly read As New Button With {.Text = "Считать номер"}
    Private ReadOnly cancelButton As New Button With {.Text = "Отмена", .Visible = False}
    Private ReadOnly cameras As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly linkDot As New Label
    Private ReadOnly link As New Label With {.AutoSize = True}
    Private ReadOnly plate As New Label With {.Name = "plateText", .Text = "—", .TextAlign = ContentAlignment.MiddleCenter}
    Private ReadOnly note As New Label With {.AutoSize = True}
    Private ReadOnly countdown As New System.Windows.Forms.Timer With {.Interval = 500}
    Private ReadOnly opros As clOpros
    Private slots As New Dictionary(Of Byte, Integer) ' адрес камеры -> строка таблицы RS-485 (её «Связь» в статусе)
    Private states As Byte()
    Private polled, fresh, found As Boolean
    Private reason As String = "Ожидание состояния платы…"
    Private outcome As String = "Поставьте машину перед камерой и нажмите «Считать номер»."
    Private outcomeError, requesting As Boolean
    Private waitingAddress As Byte
    Private waitingUntil As DateTime = DateTime.MinValue

    Public ReadOnly Property HasCamera As Boolean
        Get
            Return found
        End Get
    End Property

    Public Sub New(reader As clOpros)
        opros = reader
        AgentLog.WatchText(note, "plate")
        AgentLog.WatchText(plate, "plate.number")
        Theme.SurfaceCard(Me)
        BackColor = Theme.Surface : Dock = DockStyle.Fill : AutoSize = True
        ColumnCount = 1 : Padding = New Padding(Theme.GapSection)
        Dim heading As New Label With {.Text = "Камера номеров", .AutoSize = True, .Margin = New Padding(0, 0, 0, Theme.GapBlock)}
        Theme.Group(heading)
        Dim caption As New Label With {.Text = "Камера · адрес RS-485", .AutoSize = True,
            .Font = Theme.FontCaption, .ForeColor = Theme.TextMuted, .Margin = New Padding(0, 0, 0, Theme.GapLabel)}
        cameras.Font = Theme.FontBody : cameras.Width = Theme.Px(110)
        cameras.Margin = New Padding(0, 0, Theme.GapRelated, 0)
        Theme.Dot(linkDot, Theme.eDot.Idle)
        For Each label In {linkDot, link}
            label.Anchor = AnchorStyles.Left : label.Margin = New Padding(0, Theme.Px(4), 0, 0)
        Next
        Dim line As New FlowLayoutPanel With {.AutoSize = True, .WrapContents = False, .Margin = New Padding(0, 0, 0, Theme.GapRelated)}
        line.Controls.AddRange({cameras, linkDot, link})
        plate.Font = Theme.FontPlate : plate.ForeColor = Theme.TextMain : plate.BackColor = Theme.WindowBack
        plate.Size = New Size(Theme.Px(300), Theme.Px(64)) : plate.Margin = New Padding(0, 0, 0, Theme.GapRelated)
        Theme.Note(note, False)
        note.MaximumSize = New Size(Theme.Px(380), 0) : note.Margin = New Padding(0, 0, 0, Theme.GapRelated)
        Theme.Primary(read) : Theme.Secondary(cancelButton)
        For Each button In {read, cancelButton}
            button.Size = New Size(Theme.Px(180), Theme.ButtonHeightSmall)
            button.Margin = New Padding(0, 0, Theme.GapRelated, 0)
        Next
        Dim actions As New FlowLayoutPanel With {.AutoSize = True, .WrapContents = False, .Margin = New Padding(0)}
        actions.Controls.AddRange({read, cancelButton})
        Controls.AddRange({heading, caption, line, plate, note, actions})
        AddHandler read.Click, AddressOf ReadPlate
        AddHandler cancelButton.Click, Sub() Finish("Ожидание отменено. Пока плата не закончит захват (до " & Seconds & " с), проезд по номерам не проверяется.", False)
        AddHandler countdown.Tick, AddressOf Tick
        AddHandler cameras.SelectedIndexChanged, Sub() Apply()
        Apply()
    End Sub

    ' blocked - плата занята обновлением или выгрузкой фото; wifiPort - порт RS-485 отдан Wi-Fi-мосту (USB 113).
    Public Sub SetState(config As clOpros.s_config, status As clOpros.s_status, blocked As Boolean, isFresh As Boolean, wifiPort As Boolean)
        Dim table = {config.periph1, config.periph2, config.periph3, config.periph4,
            config.periph5, config.periph6, config.periph7, config.periph8}
        Dim list As New Dictionary(Of Byte, Integer)
        For i = 0 To 7
            If table(i).rs485Type = PeripheryNames.PlateCameraType AndAlso Not list.ContainsKey(table(i).rs485Adr) Then list.Add(table(i).rs485Adr, i)
        Next
        found = list.Count > 0
        If Not Waiting AndAlso Not list.Keys.SequenceEqual(slots.Keys) Then
            Dim selected = cameras.SelectedItem
            cameras.Items.Clear()
            For Each address In list.Keys
                cameras.Items.Add(address)
            Next
            If selected IsNot Nothing AndAlso cameras.Items.Contains(selected) Then cameras.SelectedItem = selected
            If cameras.SelectedIndex < 0 AndAlso cameras.Items.Count > 0 Then cameras.SelectedIndex = 0
        End If
        slots = list : states = status.peripheryState
        polled = config.rs485Mode = 1 AndAlso Not wifiPort : fresh = isFresh
        reason = If(blocked, "Плата занята: обновление прошивки или выгрузка фото.",
            If(Not isFresh, "Ожидание свежего состояния платы…",
            If(wifiPort, "Камера недоступна: порт RS-485 занят Wi-Fi-мостом.",
            If(config.rs485Mode <> 1, "Камера номеров работает в активном режиме RS-485: выберите «Активный» в настройках RS-485.", ""))))
        Apply()
    End Sub

    Private Function Selected() As Byte?
        If cameras.SelectedItem Is Nothing Then Return Nothing
        Return CByte(cameras.SelectedItem)
    End Function

    Private ReadOnly Property Waiting As Boolean
        Get
            Return requesting OrElse waitingUntil > DateTime.MinValue
        End Get
    End Property

    Private Sub Apply()
        Dim address = Selected()
        Dim dot = Theme.eDot.Idle, state = "Не опрашивается"
        If address.HasValue AndAlso slots.ContainsKey(address.Value) AndAlso polled Then
            If Not fresh OrElse states Is Nothing OrElse states.Length <= slots(address.Value) Then
                state = "Нет свежих данных"
            ElseIf states(slots(address.Value)) <> 0 Then
                dot = Theme.eDot.Good : state = "Отвечает"
            Else
                dot = Theme.eDot.Bad : state = "Не отвечает"
            End If
        End If
        Theme.Dot(linkDot, dot) : Theme.DotText(link, dot, state)
        Dim message = outcome, isError = outcomeError
        If requesting Then
            message = "Запрос захвата…" : isError = False
        ElseIf waitingUntil > DateTime.MinValue Then
            Dim left = Math.Max(0, CInt(Math.Ceiling((waitingUntil.AddSeconds(-GraceSeconds) - DateTime.Now).TotalSeconds)))
            message = "Ждём номер от камеры " & waitingAddress & "… " & left & " с. Пока ждём, проезд по номерам не проверяется." : isError = False
        ElseIf reason <> "" Then
            message = reason : isError = False
        End If
        Theme.SetText(note, message) : Theme.SetColor(note, If(isError, Theme.Danger, Theme.TextMuted))
        read.Enabled = reason = "" AndAlso address.HasValue AndAlso Not Waiting
        cancelButton.Visible = waitingUntil > DateTime.MinValue
        cameras.Enabled = Not Waiting
    End Sub

    Private Async Sub ReadPlate(sender As Object, e As EventArgs)
        Dim address = Selected()
        If reason <> "" OrElse Waiting OrElse Not address.HasValue Then Return
        requesting = True : Apply()
        Dim code As Integer
        Try
            code = Await Task.Run(Function() opros.CaptureId(Seconds, address.Value))
        Catch ex As Exception
            code = -1
        Finally
            requesting = False
        End Try
        If IsDisposed Then Return
        Select Case code
            Case 0
                waitingAddress = address.Value
                waitingUntil = DateTime.Now.AddSeconds(Seconds + GraceSeconds)
                countdown.Start() : Apply()
            Case 4 : Finish("Требуется ПИН-код: переподключитесь к плате.", True)
            Case -1 : Finish("Плата не ответила на запрос. Проверьте подключение и повторите.", True)
            Case Else : Finish("Плата не приняла запрос захвата: код " & code & ".", True)
        End Select
    End Sub

    Private Sub Tick(sender As Object, e As EventArgs)
        If waitingUntil = DateTime.MinValue Then
            countdown.Stop()
        ElseIf DateTime.Now >= waitingUntil Then
            Finish("Ответ с номером не пришёл. Если камера отвечает, проверьте прошивку платы: до 29.10.2026 номер уходил только серверу.", True)
        Else
            Apply()
        End If
    End Sub

    Private Sub Finish(message As String, isError As Boolean)
        countdown.Stop() : waitingUntil = DateTime.MinValue
        outcome = message : outcomeError = isError
        Apply()
    End Sub

    ' Кадры, не ставшие ответом на запрос (ClComPort.ReceiveBytes), приходят из потока порта.
    Public Sub OnFrame(frame As Byte())
        Dim reply = PlateRead.Parse(frame)
        If reply Is Nothing OrElse IsDisposed OrElse Not IsHandleCreated Then Return
        BeginInvoke(New Action(Sub() Accept(reply)))
    End Sub

    Private Sub Accept(reply As PlateRead)
        If waitingUntil = DateTime.MinValue Then Return ' ответ на отменённый захват
        If reply.Read Then
            Theme.SetText(plate, reply.Plate)
            Finish("Считан в " & DateTime.Now.ToString("HH:mm:ss") & ", камера " & waitingAddress &
                ". Номер латиницей, как его отдаёт камера: так же он должен быть в базе ID.", False)
        Else
            Finish("Камера " & waitingAddress & " не прислала номер за " & Seconds & " с: в кадре нет номера или камера не отвечает.", True)
        End If
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then countdown.Dispose()
        MyBase.Dispose(disposing)
    End Sub
End Class

' Пакет 8 платы (Протокол2): [9] 1 - ID считан, 255 - время вышло; [10..19] ID как от считывателя,
' у камеры номеров - номер латиницей, остаток нулями.
Public Class PlateRead
    Public Property Read As Boolean
    Public Property Plate As String

    Public Shared Function Parse(frame As Byte()) As PlateRead
        If frame Is Nothing OrElse frame.Length < 22 OrElse frame(8) <> clOpros.eCmd.REC_READEDID Then Return Nothing
        Dim value = System.Text.Encoding.ASCII.GetString(frame, 10, 10).Split(ChrW(0))(0).Trim()
        Return New PlateRead With {.Read = frame(9) = 1 AndAlso value <> "", .Plate = value}
    End Function
End Class
