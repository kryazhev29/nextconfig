Imports System.Globalization
Imports System.IO

Public Class Form2

    Private photoCard As PhotoPanel
    Private photoClosePending As Boolean
    Private ReadOnly photoDisabled As New List(Of Control)
    Private WithEvents opros As clOpros
    Private WithEvents tmr1 As New System.Windows.Forms.Timer
    Private status As clOpros.s_status
    Private config As clOpros.s_config
    Public ReadOnly Property ConfirmedPin As String
        Get
            Return PinFormat.Digits(CUInt(pinCode))
        End Get
    End Property
    Private pinCode As Integer
    Private updatingLog As Boolean

    ' пока настройки раскладываются по полям, их события не считаем правкой
    Private updatingFields As Boolean = False

    ' настройки ровно в том виде, в каком они прочитаны с платы:
    ' по ним видно, что именно меняет запись
    Private onBoard As clOpros.s_config

    ' пока идёт долгая работа с платой, окно операции само всё рассказывает
    Private busyWithBoard As Boolean = False

    ' незаконченная строка журнала ждёт продолжения
    Private logTail As String = ""
    Private Const LogLimit As Integer = 500000

    ' когда последний раз приходил статус: по нему видно, свежие данные на экране или нет
    Private lastStatus As DateTime = DateTime.MinValue
    Private Const StaleSeconds As Integer = 6


    Public Sub New(ByVal _opros As clOpros, ByVal _info As String, ByVal _imei As String, ByVal _pin As Integer)

        ' Этот вызов является обязательным для конструктора.
        InitializeComponent()
        UpdateWriteAvailability()

        ' Добавить код инициализации после вызова InitializeComponent().
        opros = _opros
        lblPortInfo.Text = _info
        lblIMEI.Text = _imei
        lblPIN.Text = PinFormat.Digits(CUInt(_pin))
        pinCode = _pin
        photoCard = New PhotoPanel(opros, _imei)
        Dim overview = DirectCast(tabState.Controls(0), TableLayoutPanel)
        Dim grid = DirectCast(overview.Controls(1), TableLayoutPanel)
        grid.RowCount = 4
        grid.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        grid.Controls.Add(photoCard, 0, 3) : grid.SetColumnSpan(photoCard, 2)
        grid.AutoSize = True : grid.Dock = DockStyle.Top
        grid.RowStyles(0).SizeType = SizeType.AutoSize
        overview.RowStyles(1).SizeType = SizeType.AutoSize
        overview.AutoScrollMinSize = Size.Empty
        AddHandler photoCard.BusyChanged, AddressOf PhotoBusyChanged
        AddHandler overview.SizeChanged, Sub() ReflowOverview()
        AddHandler Shown, Sub() ReflowOverview()

        messages = New StatusMessage(pnlStatusBar, lblMessage, lblLastUpdate)
        AddHandler messages.FlashChanged, Sub() UpdateStatusBar()
        AddHandler tabs.SelectedIndexChanged, Sub() UpdateStatusBar()
        AgentLog.WatchText(lblMessage, "form2.message")
        UpdateStatusBar()

        If Demo.IsOn Then Me.Text = "Домовой NEXT — демонстрационный режим"
        UpdateFirmwareAvailability()
    End Sub

    ' Нижняя строка видна на всех страницах. Состояние сохранения на «Настройках»
    ' уже написано у кнопки «Сохранить», а во время вспышки оно бы не читалось на тёмном.
    Private messages As StatusMessage

    Private Sub UpdateStatusBar()
        lblSaveState.Visible = tabs.SelectedTab IsNot tabSettings AndAlso Not messages.Flashing
    End Sub

    ' Сообщение оператору в нижней строке вместо окна. Вопросы остаются окнами frmInfo.
    Private Sub Notify(title As String, text As String, Optional isError As Boolean = False)
        messages.Show(title, text, isError)
    End Sub

    Private Sub Form2_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        FitToScreen()
        Theme.EnableDoubleBuffer(Me)

        AgentLog.WatchText(lblSaveState, "form2.save")
        AgentLog.WatchText(lblConnState, "form2.connection")
        AgentLog.WatchText(lblFwHint, "form2.update")
        AddHandler opros.onStatus, AddressOf opros_onStatus
        AddHandler tmr1.Tick, AddressOf TmrTick
        tmr1.Interval = 1000
        tmr1.Enabled = True

        AddHandler opros.GetComPort.ReceiveComand, AddressOf OnRecStr
        opros.StopGetStatus(False)

        RestoreLogSelection()
        ReadConfig(True)
        If names485.Notice <> "" Then RichTextBox1.AppendText(names485.Notice & vbCrLf)
        AddHandler opros.GetComPort.ErrorPort, AddressOf OnErrorPort
        ' опрос сам заметил, что плата не отвечает
        AddHandler opros.onLost, AddressOf OnErrorPort
        AddHandler opros.GetComPort.PinRequired, AddressOf OnPinRequired
    End Sub

    Private Sub Form2_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        If photoCard.Busy Then
            photoClosePending = True : photoCard.Cancel() : e.Cancel = True : Return
        End If
        RemoveHandler opros.onStatus, AddressOf opros_onStatus
        RemoveHandler opros.onLost, AddressOf OnErrorPort

        RemoveHandler opros.GetComPort.ReceiveComand, AddressOf OnRecStr
        RemoveHandler opros.GetComPort.ErrorPort, AddressOf OnErrorPort
        RemoveHandler opros.GetComPort.PinRequired, AddressOf OnPinRequired
    End Sub

    ' На маленьком экране или при крупном масштабе Windows окно нужного размера
    ' может не поместиться: ужимаем его до рабочей области, содержимое прокручивается.
    Private Sub FitToScreen()
        Theme.FitBoardWindow(Me)
    End Sub

    ' Раз в секунду смотрим, давно ли приходил статус. Если связь пропала,
    ' в шапке видно, что на экране последнее полученное, а не текущее состояние.
    Private Sub PhotoBusyChanged(value As Boolean)
        busyWithBoard = value
        UpdateFirmwareAvailability()
        If value Then
            DisableOtherActions(Me)
        Else
            For Each c In photoDisabled
                If Not c.IsDisposed Then c.Enabled = True
            Next
            photoDisabled.Clear()
            UpdateWriteAvailability()
            TmrTick()
            If photoClosePending Then BeginInvoke(New Action(AddressOf Close))
        End If
    End Sub

    Private Sub DisableOtherActions(parent As Control)
        For Each c As Control In parent.Controls
            If c Is photoCard Then Continue For
            If (TypeOf c Is Button OrElse TypeOf c Is CheckBox OrElse TypeOf c Is ComboBox OrElse TypeOf c Is TextBox) AndAlso c.Enabled Then
                photoDisabled.Add(c) : c.Enabled = False
            Else
                DisableOtherActions(c)
            End If
        Next
    End Sub

    Private Sub TmrTick()
        Dim fresh = StatusIsFresh()
        btnTestOUT1.Enabled = fresh AndAlso Not busyWithBoard AndAlso HasConfigSnapshot()
        btnTestOUT2.Enabled = btnTestOUT1.Enabled
        UpdateIoButtons()
        UpdateFirmwareAvailability()
        photoCard.SetState(onBoard, status.readyToUpdate, DateTime.Now.Subtract(lastStatus).TotalSeconds <= StaleSeconds, Camera232, wifiPortBusy)
        If lastStatus = DateTime.MinValue Then
            SetHeadState(Theme.eDot.Waiting, "Ожидание данных")
        ElseIf DateTime.Now.Subtract(lastStatus).TotalSeconds > StaleSeconds Then
            SetHeadState(Theme.eDot.Bad, "Данные не обновляются")
        End If
        If Not fresh Then
            For Each indicator As Label In {dotGSM, dotInt, dotServer, dotIN1, dotIN2, dotOUT1, dotOUT2}
                Theme.Dot(indicator, Theme.eDot.Idle)
            Next
            For Each label In {lblGSM, lblInt, lblServerConnect, lbIN1, lblIN2, lblOUT1, lbOUT2}
                Theme.DotText(label, Theme.eDot.Idle, "Нет свежих данных")
            Next
            lblSig.Text = "Нет свежих данных" : pbSig.Value = 0
            If linkSnapshot IsNot Nothing Then DisplayLink(Nothing)
            deviceConnection.Signal = LinkDiag.SignalView(Nothing, 99)
            deviceConnection.Cell = "" : deviceConnection.Details = "Нет свежих данных"
            ResetLinkChannels()
            lblNet.Text = "—"
            Status485()
        End If
    End Sub

    Private Function StatusIsFresh() As Boolean
        Return lastStatus <> DateTime.MinValue AndAlso DateTime.Now.Subtract(lastStatus).TotalSeconds <= StaleSeconds
    End Function

    Private Function FirmwareBlockReason() As String
        If busyWithBoard Then Return "Плата занята другой операцией. Дождитесь её завершения."
        If Not StatusIsFresh() Then Return "Нет свежего статуса платы по USB. Проверьте подключение и дождитесь обновления данных."
        Return ""
    End Function

    Private Sub UpdateFirmwareAvailability()
        Dim reason = FirmwareBlockReason()
        btnUpdateFW.Enabled = reason = ""
        btnUpdateBoot.Enabled = btnUpdateFW.Enabled
        Theme.SetText(lblFwHint, If(reason = "", FirmwareHint, reason))
    End Sub

    Private Function CanUpdateFirmware() As Boolean
        Dim reason = FirmwareBlockReason()
        If reason = "" Then Return True
        AgentLog.Write("update.blocked", "reason", reason, "busy", busyWithBoard, "lastStatus", lastStatus)
        Notify("Обновление недоступно", "Обновление недоступно: " & reason, True)
        Return False
    End Function

    Private Const FirmwareHint As String = "Программа сама найдёт свежий файл прошивки и проведёт по шагам. Около 1 минуты, потом сама подключится к плате и покажет итог."

    Private Sub SetHeadState(ByVal state As Theme.eDot, ByVal text As String)
        Theme.Dot(dotConn, state)
        Theme.DotText(lblConnState, state, text)
    End Sub

    ' Состояние строкой и цветом сразу: точка плюс подпись.
    Private Sub SetDot(ByVal dot As Label, ByVal text As Label, ByVal good As Boolean,
                       ByVal onText As String, ByVal offText As String,
                       Optional ByVal offIsError As Boolean = False)
        Dim state As Theme.eDot

        If good Then
            state = Theme.eDot.Good
        ElseIf offIsError Then
            state = Theme.eDot.Bad
        Else
            state = Theme.eDot.Idle
        End If

        Theme.Dot(dot, state)
        Theme.DotText(text, state, If(good, onText, offText))
    End Sub

    ' Контроллер держит время в UTC; показываем его по местному поясу компьютера
    ' и пишем расхождение, когда оно заметное.
    Private Function DeviceTimeText(ByVal unix As UInt32) As String
        If Not DeviceTimeIsSet(unix) Then Return "Время не задано"
        Dim t As DateTime = DateTime.SpecifyKind(UnixTimestampToDateTime(unix), DateTimeKind.Utc).ToLocalTime()
        Dim text As String = t.ToString("dd.MM.yyyy HH:mm:ss")
        Dim diff As TimeSpan = DateTime.Now.Subtract(t)

        If Math.Abs(diff.TotalMinutes) < 2 Then Return text

        Return text & " · расходится с компьютером на " & FormatDiff(diff)
    End Function

    Private Function DeviceTimeIsSet(unix As UInt32) As Boolean
        Return unix >= 946684800UI ' До 01.01.2000 — часы ещё не установлены.
    End Function

    Private Function FormatDiff(ByVal diff As TimeSpan) As String
        Dim d As TimeSpan = diff.Duration()

        If d.TotalHours >= 1 Then
            Return CInt(Math.Floor(d.TotalHours)) & " ч " & d.Minutes & " мин"
        End If

        Return CInt(Math.Floor(d.TotalMinutes)) & " мин"
    End Function

    ' В статусе приходит не настройка, а технология, которую модем сейчас
    ' использует (device.netType = gsm.net_type в прошивке): 0 неизвестно,
    ' 1 GSM/EDGE, 3 3G, 4 LTE. Словарь настроек сюда не подходит: 0 у него
    ' называется «Авто», а на деле это «модем ещё не сообщил».
    Private Function CurrentNetName(ByVal v As clOpros.eNetType) As String
        Select Case CInt(v)
            Case 0 : Return "не сообщается"
            Case 1 : Return "2G (GSM/EDGE)"
            Case 2 : Return "3G/4G"
            Case 3 : Return "3G"
            Case 4 : Return "4G (LTE)"
            Case Else : Return "код " & CInt(v)
        End Select
    End Function

    Private Function ModuleName(ByVal m As clOpros.eModuleType) As String
        Select Case m
            Case clOpros.eModuleType.M_UG95 : Return "UG95/UG915"
            Case clOpros.eModuleType.M_SLM336 : Return "SLM336"
            Case clOpros.eModuleType.M_M66 : Return "M66"
            Case clOpros.eModuleType.M_OTHER : Return "другой"
            Case Else : Return "не определён"
        End Select
    End Function

    ' Строка итога внизу окна: сохранены ли настройки.
    Private Sub SetSaveState(ByVal saved As Boolean)
        If saved Then
            lblSaveState.Text = "Настройки сохранены"
            lblSaveState.ForeColor = Theme.OkText
        Else
            lblSaveState.Text = "Есть несохранённые изменения"
            lblSaveState.ForeColor = Theme.Warning
        End If
    End Sub

    Private statusPosted As Integer

    Private Sub opros_onStatus(ByVal status As clOpros.s_status)
        ' Пока окно рисует статус, не накапливаем очередь, задерживающую кнопки и опрос связи.
        If Threading.Interlocked.Exchange(statusPosted, 1) <> 0 Then Return
        Me.BeginInvoke(New Action(Sub()
                                      Try
                                          refOnStatus(status)
                                      Finally
                                          Threading.Interlocked.Exchange(statusPosted, 0)
                                      End Try
                                  End Sub))
    End Sub

    Private Sub refOnStatus(ByVal _status As clOpros.s_status)

        status = _status
        lastStatus = DateTime.Now
        TmrTick()
        photoCard.SetState(onBoard, status.readyToUpdate, True, Camera232, wifiPortBusy)
        Status485()

        Theme.SetText(lblTime, DeviceTimeText(status.time))
        Theme.SetColor(lblTime, If(DeviceTimeIsSet(status.time), Theme.TextMain, Theme.Danger))
        Theme.SetText(lblFW, status.fw_version & ", сборка " & BuildText(CUInt(status.fw_build)))
        Theme.SetText(lblBoot, "0x" & status.boot_version.ToString("X2"))
        Theme.SetText(lblHW, status.hw_version.ToString())
        Theme.SetText(lblModuleType, ModuleName(status.moduleType))

        ' то же самое коротко в шапке: она видна на любой вкладке
        Theme.SetText(lblHeadModel, "Модуль " & ModuleName(status.moduleType))
        Theme.SetText(lblHeadHW, "Плата " & status.hw_version)
        Theme.SetText(lblHeadFW, "Прошивка " & status.fw_version)
        Theme.SetText(lblHeadBoot, "Загрузчик 0x" & status.boot_version.ToString("X2"))

        ' Решение владельца 13.10.2026 (отступление от ТЗ): выключенное серым, красный — только неисправность.
        UpdateIo()

        SetDot(dotGSM, lblGSM, status.gsm = 1, "Подключено", "Нет сети", True)
        SetDot(dotInt, lblInt, status.gprs = 1, "Подключено", "Нет интернета", True)
        SetDot(dotServer, lblServerConnect, status.connectToServer <> 0, "Подключено", "Нет связи с сервером", True)

        UpdateDeviceSignal()
        Theme.SetText(lblNet, CurrentNetName(status.netType))

        Theme.SetText(lblUsedMem, status.usedMem & " из 50 000")
        meterMem.Value = status.usedMem
        Theme.SetText(deviceMemory, CUInt(status.usedMem).ToString("N0", Globalization.CultureInfo.GetCultureInfo("ru-RU")) & " из 50 000 · " & Math.Min(100, status.usedMem * 100 \ 50000) & " %")
        deviceMemoryMeter.Value = status.usedMem

        SetHeadState(Theme.eDot.Good, "На связи")
        Theme.SetText(lblLastUpdate, "Последнее обновление: " & lastStatus.ToString("HH:mm:ss"))

        If CheckOnConnect IsNot Nothing AndAlso status.fw_version <> 0 Then
            Dim check = CheckOnConnect
            CheckOnConnect = Nothing
            ' итог показываем вместо предложения замены: оно появится при следующем подключении
            bootOffered = True
            Me.BeginInvoke(New Action(Sub() ShowUpdateResult(check)))
        End If

        If Not bootOffered AndAlso status.fw_version <> 0 AndAlso status.boot_version < clBoot.OLD_BOOT_BELOW Then
            bootOffered = True
            Me.BeginInvoke(New MethodInvoker(AddressOf OfferBootReplace))
        End If
    End Sub

    ' После удачной передачи плата перезапускается: экран подключения по этому
    ' ожиданию сам дождётся её, подключится и покажет итог.
    Public Property PendingCheck As UpdateCheck

    ' Итог прошлого обновления: проверяется по первому статусу после подключения.
    Public Property CheckOnConnect As UpdateCheck

    ' Номер платы для выбора файлов. Под старым загрузчиком прошивка сообщает 0 -
    ' тогда номер, запомненный при переводе этой платы (139 v5, 140 старая MODEM).
    Private ReadOnly Property BoardHw As Byte
        Get
            If status.hw_version <> 0 Then Return status.hw_version
            Return If(FirmwareFiles.SavedTarget(lblIMEI.Text.Trim()) <> 0, CByte(139), CByte(0))
        End Get
    End Property

    ' Старая плата MODEM (IP v3): номер тот же 139, сборки - из своей папки. Запомнено
    ' при переводе; иначе по модулю связи: на v5 стоит SLM336, на старых - Quectel.
    Private ReadOnly Property OldModem As Boolean
        Get
            Dim saved = FirmwareFiles.SavedTarget(lblIMEI.Text.Trim())
            If saved <> 0 Then Return saved = 140
            Return status.moduleType = clOpros.eModuleType.M_M66 OrElse status.moduleType = clOpros.eModuleType.M_OTHER
        End Get
    End Property

    ' Камера на порту 232 - только у старой MODEM (IP v3). Модуль Quectel проверяется и отдельно:
    ' запомненная цель 139 после перевода делает OldModem ложным.
    Private ReadOnly Property Camera232 As Boolean
        Get
            Return OldModem OrElse status.moduleType = clOpros.eModuleType.M_M66 OrElse status.moduleType = clOpros.eModuleType.M_OTHER
        End Get
    End Property

    ' Файл берём сами (самая свежая сборка для этой платы рядом с программой);
    ' нет такого - оператор выбирает, папка запоминается.
    Private Sub btnUpdateFW_Click(sender As Object, e As EventArgs) Handles btnUpdateFW.Click
        If Not CanUpdateFirmware() Then Return
        Dim file As String = FirmwareFiles.FindFirmware(BoardHw, OldModem)
        If file Is Nothing Then
            ' Окно выбора файла закрывает главное, поэтому подсказка ещё и в его заголовке.
            Notify("Обновление прошивки", "Рядом с программой нет файла прошивки для этой платы. " &
                   "Укажите файл вида 139.71.1790667089.bin — его присылает производитель.")
            OpenFileDialog1.Title = "Укажите файл прошивки вида 139.71.1790667089.bin"
            OpenFileDialog1.Filter = "Файлы прошивки (*.bin)|*.bin|Все файлы (*.*)|*.*"
            OpenFileDialog1.FileName = ""
            If OpenFileDialog1.ShowDialog(Me) <> DialogResult.OK Then Exit Sub
            file = OpenFileDialog1.FileName
            FirmwareFiles.LastFolder = Path.GetDirectoryName(file)
        End If

        ' опрос статуса на время записи не нужен: порт занимают блоки прошивки
        busyWithBoard = True
        UpdateFirmwareAvailability()
        tmr1.Enabled = False
        opros.StopGetStatus(True)
        Dim installed = False
        Try
            Using f As New frmFW(file, opros) With {.BoardBuild = CUInt(status.fw_build)}
                If f.ShowDialog(Me) = DialogResult.OK Then
                    installed = True
                    ' контроллер устанавливает прошивку и перезагружается, связь потеряна
                    PendingCheck = New UpdateCheck With {.BeforeBuild = CUInt(status.fw_build), .ExpectedBuild = f.FileBuild,
                                                         .BeforeBoot = status.boot_version}
                    Me.Close()
                End If
            End Using
        Finally
            ' После успешной записи плата перезапускается, окно закрыто: опрос возобновит новое подключение.
            If Not installed Then
                busyWithBoard = False
                opros.StopGetStatus(False)
                tmr1.Enabled = Not IsDisposed
                If Not IsDisposed Then UpdateFirmwareAvailability()
            End If
        End Try
    End Sub

    Private Sub btnUpdateBoot_Click(sender As Object, e As EventArgs) Handles btnUpdateBoot.Click
        If Not CanUpdateFirmware() Then Return
        Dim file As String = AskBootFile()
        If file <> "" Then ReplaceBoot(file)
    End Sub

    ' Загрузчик для этой платы рядом с программой. У v5 и MODEM плата одна (139),
    ' поэтому файлов может быть несколько - тогда оператор выбирает по плате.
    Private Function AskBootFile() As String
        Dim found = FirmwareFiles.FindBootloaders(BoardHw, OldModem)
        If found.Count = 1 Then Return found(0)

        OpenFileDialog1.Filter = "Файлы загрузчика (DomovoiBoot*.bin)|DomovoiBoot*.bin|Все файлы (*.*)|*.*"
        OpenFileDialog1.FileName = ""
        If found.Count > 1 Then
            Dim names = String.Join(", ", found.Select(Function(f) FirmwareFiles.ProfileName(f)))
            Notify("Выбор загрузчика", "Для этой платы подходят несколько загрузчиков: " & names & ". " &
                   "Откройте папку с названием вашей платы (например, v5 — для платы v5) и выберите DomovoiBoot.bin.")
            OpenFileDialog1.Title = "Откройте папку вашей платы (" & names & ") и выберите DomovoiBoot.bin"
            OpenFileDialog1.InitialDirectory = Path.GetDirectoryName(Path.GetDirectoryName(found(0)))
        Else
            Notify("Выбор загрузчика", "Рядом с программой нет файла загрузчика. " &
                   "Укажите файл DomovoiBoot.bin из комплекта для вашей платы.")
            OpenFileDialog1.Title = "Укажите DomovoiBoot.bin из комплекта для вашей платы"
            OpenFileDialog1.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory
        End If
        If OpenFileDialog1.ShowDialog(Me) <> DialogResult.OK Then Return ""
        FirmwareFiles.LastFolder = Path.GetDirectoryName(Path.GetDirectoryName(OpenFileDialog1.FileName))
        Return OpenFileDialog1.FileName
    End Function

    Private Sub ReplaceBoot(ByVal file As String)
        ' опрос статуса на время замены не нужен: порт занимает команда 110
        busyWithBoard = True
        UpdateFirmwareAvailability()
        tmr1.Enabled = False
        opros.StopGetStatus(True)
        Dim installed = False
        Try
            Using f As New frmFW(file, opros, BoardHw)
                If f.ShowDialog(Me) = DialogResult.OK Then
                    installed = True
                    ' контроллер перезагружается с новым загрузчиком, связь потеряна
                    PendingCheck = New UpdateCheck With {.Bootloader = True, .BeforeBuild = CUInt(status.fw_build),
                                                         .BeforeBoot = status.boot_version}
                    Me.Close()
                End If
            End Using
        Finally
            ' После успешной записи плата перезапускается, окно закрыто: опрос возобновит новое подключение.
            If Not installed Then
                busyWithBoard = False
                opros.StopGetStatus(False)
                tmr1.Enabled = Not IsDisposed
                If Not IsDisposed Then UpdateFirmwareAvailability()
            End If
        End Try
    End Sub

    ' Шаг 2 перевода на новую систему. Прошивка работает, а загрузчик старый: она
    ' сообщает серверу плату 0 и не берёт обновления с сервера. Предлагаем один раз
    ' за подключение. Файл загрузчика берём рядом с программой, если он там один.
    Private bootOffered As Boolean = False

    Private Sub OfferBootReplace()
        If busyWithBoard Then Exit Sub

        Using f As New frmInfo("Перевод на новую систему",
                               "Новая прошивка уже стоит. Остался шаг 2 из 2 — заменить загрузчик." & vbCrLf &
                               "Без этого плата не сможет получать обновления с сервера." & vbCrLf &
                               "Около 1 минуты, программа проведёт по шагам.", , True)
            f.Button1.Text = "Заменить загрузчик"
            f.Button2.Text = "Позже"
            If f.ShowDialog(Me) <> DialogResult.OK Then Exit Sub
        End Using

        Dim file As String = AskBootFile()
        If file <> "" Then ReplaceBoot(file)
    End Sub

    ' Плата вернулась после обновления: что было и что стало, словами.
    Private Sub ShowUpdateResult(ByVal check As UpdateCheck)
        Dim ok As Boolean
        Dim text As String
        If check.Bootloader Then
            ok = status.boot_version >= clBoot.OLD_BOOT_BELOW
            text = If(ok, "Загрузчик заменён, плата работает." & vbCrLf &
                          "Было: 0x" & check.BeforeBoot.ToString("X2") & ". Стало: 0x" & status.boot_version.ToString("X2") & "." & vbCrLf &
                          "Теперь плата может получать обновления с сервера.",
                      "Загрузчик не заменился: на плате по-прежнему 0x" & status.boot_version.ToString("X2") & "." & vbCrLf &
                          "Плата работает. Попробуйте заменить загрузчик ещё раз.")
        ElseIf CUInt(status.fw_build) = check.ExpectedBuild Then
            ok = True
            text = "Прошивка обновлена, плата работает." & vbCrLf &
                   "Было: сборка от " & BuildText(check.BeforeBuild) & "." & vbCrLf &
                   "Стало: сборка от " & BuildText(check.ExpectedBuild) & "."
        ElseIf CUInt(status.fw_build) = check.BeforeBuild Then
            ok = False
            text = "Плата работает на прежней прошивке (сборка от " & BuildText(check.BeforeBuild) & ")." & vbCrLf &
                   "Новая не установилась. Причина записана в журнал событий платы (раздел «Журнал»)."
        Else
            ok = False
            text = "На плате сборка от " & BuildText(CUInt(status.fw_build)) & "," & vbCrLf &
                   "а ожидалась сборка от " & BuildText(check.ExpectedBuild) & ". Посмотрите журнал событий платы."
        End If
        AgentLog.Write("updatecheck", "ok", ok, "boot", check.Bootloader, "fw", status.fw_build, "bootver", status.boot_version)
        Notify(If(ok, "Готово", "Внимание"), text, Not ok)
    End Sub

    ' Кнопка ТЕСТ по ТЗ: запускает алгоритм выхода по его настройкам (команда 8, значение 1)
    ' и сразу запрашивает статус. В режиме УРОВЕНЬ включённый выход выключает (значение 0).
    ' Отказ контроллера (пауза реле, запрет в настройках) штатный: без окна, причина пишется под выходом.
    ' Второму выходу передаётся его текущее состояние: 0 выключил бы его.
    Private Sub btnTestOUT_Click(sender As Object, e As EventArgs) Handles btnTestOUT2.Click, btnTestOUT1.Click
        If Not StatusIsFresh() OrElse Not HasConfigSnapshot() OrElse busyWithBoard Then Return
        Dim n As Integer = CInt(CType(sender, Control).Tag)
        If Date.Now < ioTestUntil(n) Then Return
        Dim level As Boolean = If(n = 0, onBoard.out1.outMode, onBoard.out2.outMode) = 1
        Dim want As Boolean = Not (level AndAlso If(n = 0, status.out1, status.out2))
        Dim code As Integer

        If n = 0 Then
            code = opros.SetOut(want, status.out2)
        Else
            code = opros.SetOut(status.out1, want)
        End If

        If code = -1 Then
            Notify("Выходы", "Выход " & (n + 1) & ": контроллер не ответил на команду.", True)
        Else
            ' Отказ платы раньше молча проглатывался — теперь под выходом написано почему.
            ShowOutTestResult(n, code)
        End If
    End Sub
    Private Sub btnTranspMode_Click(sender As Object, e As EventArgs) Handles btnTranspMode.Click
        Using f As New frmInfo("Прозрачный режим", "Перевести контроллер в прозрачный режим с GSM модулем?" & vbCrLf & "Конфигуратор будет закрыт!",, True)
            f.Button1.Text = "Перевести"
            f.Button2.Text = "Отмена"
            If f.ShowDialog(Me) = DialogResult.OK Then
                If opros.SetTransparentMode() Then
                    ' Программа сейчас закроется - строку состояния никто не увидит, поэтому окно.
                    Using f2 As New frmInfo("Прозрачный режим", "Контроллер переведен в прозрачный режим с GSM модулем!" & vbCrLf &
                                            "Программа будет закрыта")
                        f2.ShowDialog(Me)
                    End Using
                    Me.DialogResult = DialogResult.Abort
                    Me.Close()
                Else
                    Notify("Прозрачный режим", "Прозрачный режим: команда не поддерживается или нет ответа контроллера.", True)
                End If
            End If
        End Using
    End Sub

    Private Sub btnFactory_Click(sender As Object, e As EventArgs) Handles btnFactory.Click
        Using f As New frmInfo("Заводские настройки", "Вы уверены что хотите сбросить контроллер на заводские настройки?",, True)
            f.Button1.Text = "Сбросить"
            f.Button2.Text = "Отмена"
            If f.ShowDialog(Me) = DialogResult.OK Then
                If opros.SetDefault() Then
                    Notify("Заводские настройки", "Контроллер сброшен на заводские настройки.")
                Else
                    Notify("Заводские настройки", "Не удалось сбросить на заводские настройки. Повторите операцию.", True)
                End If
            End If
        End Using
    End Sub

    Private Sub btnChangePIN_Click(sender As Object, e As EventArgs) Handles btnChangePIN.Click
        Using f As New frmPIN(pinCode)
            If f.ShowDialog(Me) = DialogResult.OK Then
                config.pin = CUInt(f.nPin)
                UpdateConfigFields()
            End If
        End Using
    End Sub

    Private Sub btnSave_Click(sender As Object, e As EventArgs) Handles btnSave.Click

        If Not HasConfigSnapshot() Then Return
        If Not ValidateFields() Then
            lblSaveState.Text = "Проверьте отмеченные значения"
            lblSaveState.ForeColor = Theme.Danger
            Exit Sub
        End If

        ApplyFields()

        Dim changes As List(Of String) = ChangeList()
        Dim configChanged = changes.Count > 0
        Dim question As String

        WifiChanges(changes)
        If changes.Count = 0 Then
            lblSaveState.Text = "Изменений нет"
            Return
        Else
            question = "В контроллере изменится:" & vbCrLf & vbCrLf & String.Join(vbCrLf, changes)
        End If

        Using f As New frmInfo("Сохранение настроек", question, ContentAlignment.MiddleLeft, True)
            Theme.ConfigChanges(f, question)
            f.Button1.Text = "Сохранить"
            f.Button2.Text = "Отмена"
            If f.ShowDialog(Me) = DialogResult.OK Then
                ' Настройки сервера (команда 12) и выбор Wi-Fi-модема (команда 114) хранятся в плате раздельно.
                If configChanged Then
                    If Not opros.SetConfig(config) Then
                        Notify("Сохранение настроек", "Не удалось сохранить настройки. Повторите операцию.", True)
                        Return
                    End If
                    ConfirmConfigSnapshot()
                End If
                If WifiDiffers() AndAlso Not SaveWifiChoice() Then
                    SetSaveState(False)
                    Notify("Сохранение настроек", If(configChanged, "Настройки сохранены, но выбор Wi-Fi-модема не записан. Повторите сохранение.",
                        "Не удалось записать выбор Wi-Fi-модема. Повторите операцию."), True)
                    Return
                End If
                SetSaveState(True)
                Notify("Сохранение настроек", "Настройки сохранены в контроллер.")
            End If
        End Using
    End Sub

    Private Sub btnDefault_Click(sender As Object, e As EventArgs) Handles btnDefault.Click
        If Not HasConfigSnapshot() Then Return
        Using f As New frmInfo("Настройки по умолчанию", "Вы уверены что хотите установить настройки по умолчанию?",, True)
            f.Button1.Text = "Записать"
            f.Button2.Text = "Отмена"
            If f.ShowDialog(Me) = DialogResult.OK Then
                If Not ResetWifiDefaults() Then
                    Notify("Настройки по умолчанию", "Не удалось сбросить выбор Wi-Fi-модема. Настройки не записаны.", True)
                    Return
                End If
                SetDefaultConfig()
                If opros.SetConfig(config) Then
                    ConfirmConfigSnapshot()
                    Notify("Настройки по умолчанию", "Настройки по умолчанию записаны в контроллер.")
                Else
                    Notify("Настройки по умолчанию", "Не удалось записать настройки по умолчанию. Повторите операцию.", True)
                End If
            End If
        End Using
    End Sub

    Private Sub SetDefaultConfig()
        With config
            .time = DateTimeToUnixTimestamp(DateTime.UtcNow)
            .pin = BitConverter.ToUInt32(System.Text.Encoding.UTF8.GetBytes("4321"), 0)
            .server1 = "93.189.229.197:15010"
            .useServer1 = 1
            .server2 = "93.189.229.197:15020"
            .useserver2 = 0
            .ntpServer = ""
            .opType = ""
            .netType = clOpros.eNetType._Авто
            .countryCode = "+7"
            .simNumber = ""
            .ussdBalance = ""
            .resetTime = 0
            .fullLog = 0
            .VoLTE = 0
            .rs485Mode = 1
            .enableNTPsync = 0
            .wiegandType = 0
            .periph1.rs485Adr = 0
            .periph1.rs485Type = 0
            .periph2.rs485Adr = 0
            .periph2.rs485Type = 0
            .periph3.rs485Adr = 0
            .periph3.rs485Type = 0
            .periph4.rs485Adr = 0
            .periph4.rs485Type = 0
            .periph5.rs485Adr = 0
            .periph5.rs485Type = 0
            .periph6.rs485Adr = 0
            .periph6.rs485Type = 0
            .periph7.rs485Adr = 0
            .periph7.rs485Type = 0
            .periph8.rs485Adr = 0
            .periph8.rs485Type = 0
            With .out1
                .unBlockUse = 0
                .outMode = 0
                .onServerCmd = 1
                .onCam = 0
                .onOther485 = 0
                .onRF433 = 0
                .onRFID = 0
                .onWiegand = 0
                .impulse = 1
                .pause = 3
                .repeate = 1
            End With
            With .out2
                .unBlockUse = 0
                .outMode = 0
                .onServerCmd = 1
                .onCam = 0
                .onOther485 = 0
                .onRF433 = 0
                .onRFID = 0
                .onWiegand = 0
                .impulse = 1
                .pause = 3
                .repeate = 1
            End With
            .in1Func = 0
            .in2Func = 0

        End With

        UpdateConfigFields()
    End Sub

    Private Sub UpdateConfigFields()
        SetSaveState(False)

        ' пока раскладываем значения по полям, их события не считаем правкой
        updatingFields = True

        With config
            ' Прошивка хранит ПИН числом, а не четырьмя буквами — иначе на экране мусор.
            lblPIN.Text = PinFormat.Digits(.pin)
            txtServer1.Text = .server1
            txtServer2.Text = .server2
            chServer1.Checked = .useServer1
            chServer2.Checked = .useserver2
            txtOperator.Text = .opType
            cmbVoLTE.SelectedIndex = If(.VoLTE <= 1, CInt(.VoLTE), -1)
            cmbNetType.SelectedIndex = If(CInt(.netType) < cmbNetType.Items.Count, CInt(.netType), -1)
        End With

        Load485()
        updatingFields = False

        UpdateDirty()

    End Sub

    ' Проверка введённого. Ошибку пишем возле того значения, из-за которого
    ' она возникла, и ничего на плату не отправляем.
    Private Function ValidateFields() As Boolean
        If System.Text.Encoding.UTF8.GetByteCount(txtOperator.Text.Trim()) > 31 Then
            ShowSettingsSection(0)
            Notify("Точка доступа", "Точка доступа, логин и пароль вместе должны занимать не более 31 байта UTF-8.", True)
            txtOperator.Focus()
            Return False
        End If
        lblServer1Err.Text = ServerError(txtServer1.Text, True)
        lblServer2Err.Text = ServerError(txtServer2.Text, False)

        ' пустое пояснение не должно занимать строку
        lblServer1Err.Visible = (lblServer1Err.Text <> "")
        lblServer2Err.Visible = (lblServer2Err.Text <> "")

        Dim valid = lblServer1Err.Text = "" AndAlso lblServer2Err.Text = ""
        If Not valid Then ShowSettingsSection(0)
        Return valid
    End Function

    ' Раздел настроек: 0 «Связь», 1 «485 и Wiegand», 2 «Доступ», 3 «Сервис».
    Private Sub ShowSettingsSection(section As Integer)
        tabs.SelectedTab = tabSettings
        Dim link = TryCast(tabSettings.Controls.Find("settingsSection" & section, True).FirstOrDefault(), Button)
        If link IsNot Nothing Then link.PerformClick()
    End Sub

    ' Перенести введённое в настройки. Список, который почему-то не выбран,
    ' оставляем как есть: молча записать вместо него первый пункт нельзя.
    Private Sub ApplyFields()
        Apply485()
        With config
            .server1 = txtServer1.Text.Trim()
            .server2 = txtServer2.Text.Trim()
            .useServer1 = If(chServer1.Checked, 1, 0)
            .useserver2 = If(chServer2.Checked, 1, 0)
            .opType = txtOperator.Text.Trim()

            If cmbVoLTE.SelectedIndex >= 0 Then .VoLTE = CByte(cmbVoLTE.SelectedIndex)
            If cmbNetType.SelectedIndex >= 0 Then .netType = CType(cmbNetType.SelectedIndex, clOpros.eNetType)
        End With
    End Sub

    ' Что именно изменится на плате. Сравниваем с тем, что прочитано, а не с
    ' тем, что сейчас в памяти окна.
    Private Function ChangeList() As List(Of String)
        Dim list As New List(Of String)

        AddChange(list, "Адрес сервера 1", onBoard.server1, config.server1)
        AddChange(list, "Использовать сервер 1", YesNo(onBoard.useServer1), YesNo(config.useServer1))
        AddChange(list, "Адрес сервера 2", onBoard.server2, config.server2)
        AddChange(list, "Использовать сервер 2", YesNo(onBoard.useserver2), YesNo(config.useserver2))
        If onBoard.opType <> config.opType Then list.Add("• Точка доступа: параметры изменены (скрыто)")
        AddChange(list, "Тип сети", opros.NetTypeToString(onBoard.netType), opros.NetTypeToString(config.netType))
        AddChange(list, "VoLTE", OnOff(onBoard.VoLTE), OnOff(config.VoLTE))
        If onBoard.pin <> config.pin Then list.Add("• ПИН-код: изменён (скрыто)")

        Changes485(list)
        Return list
    End Function

    Private Sub AddChange(ByVal list As List(Of String), ByVal name As String, ByVal was As String, ByVal now As String)
        If was <> now Then list.Add("• " & name & ": " & was & " → " & now)
    End Sub

    Private Function YesNo(ByVal v As Byte) As String
        Return If(v = 0, "нет", "да")
    End Function

    Private Function OnOff(ByVal v As Byte) As String
        Return If(v = 0, "выключено", "включено")
    End Function

    Private Function ApnText(ByVal v As String) As String
        Return If(v = "", "у оператора", "скрыто")
    End Function

    Private Function PinText(ByVal v As UInt32) As String
        Return "скрыто"
    End Function

    ' Адрес сервера пишется как узел:порт, например 87.244.4.92:15010. Узел без порта прошивка
    ' дополняет заводским портом поля (15010 или 15020): так адрес записывает сервер СофтЭксперт.
    Private Function ServerError(ByVal text As String, ByVal required As Boolean) As String
        Dim v As String = text.Trim()
        If System.Text.Encoding.UTF8.GetByteCount(v) > 31 Then Return "Адрес с портом должен занимать не более 31 байта UTF-8"

        If v = "" Then
            Return If(required, "Укажите адрес в виде 87.244.4.92:15010", "")
        End If

        Dim parts As String() = v.Split(":"c)

        If parts.Length > 2 Then Return "Адрес пишется как узел:порт, например 87.244.4.92:15010"
        If parts(0).Trim() = "" Then Return "Не указан адрес узла"
        If parts.Length = 1 Then Return ""

        Dim port As Integer

        If Not Integer.TryParse(parts(1).Trim(), port) Then Return "Порт - это число"
        If port < 1 OrElse port > 65535 Then Return "Порт от 1 до 65535"

        Return ""
    End Function

    Private Sub Settings_Changed(sender As Object, e As EventArgs) _
        Handles txtServer1.TextChanged, txtServer2.TextChanged, txtOperator.TextChanged,
                cmbVoLTE.SelectedIndexChanged, cmbNetType.SelectedIndexChanged,
                chServer1.CheckedChanged, chServer2.CheckedChanged
        UpdateDirty()
    End Sub

    ' Сравниваем поля с тем, что прочитано с платы. По флагу «идёт заполнение»
    ' полагаться нельзя: список отдаёт своё событие позже, когда вкладку
    ' открывают в первый раз, и окно врало про несохранённые изменения.
    Private Sub ConfirmConfigSnapshot()
        onBoard = config
        pinCode = BitConverter.ToInt32(BitConverter.GetBytes(config.pin), 0)
        UpdateWriteAvailability()
    End Sub

    Private Function HasConfigSnapshot() As Boolean
        Return onBoard.rawConfig IsNot Nothing AndAlso onBoard.rawConfig.Length = 512
    End Function

    Private Sub UpdateWriteAvailability()
        btnSave.Enabled = HasConfigSnapshot() AndAlso Not (photoCard IsNot Nothing AndAlso photoCard.Busy)
        btnDefault.Enabled = btnSave.Enabled
    End Sub

    Private Sub UpdateDirty()
        If updatingFields Then Exit Sub

        If FieldsDiffer() Then
            SetSaveState(False)
        Else
            lblSaveState.Text = ""
        End If
    End Sub

    Private Function FieldsDiffer() As Boolean
        If FieldsDiffer485() OrElse WifiDiffers() Then Return True
        With config
            If txtServer1.Text.Trim() <> .server1 Then Return True
            If txtServer2.Text.Trim() <> .server2 Then Return True
            If chServer1.Checked <> (.useServer1 <> 0) Then Return True
            If chServer2.Checked <> (.useserver2 <> 0) Then Return True
            If txtOperator.Text.Trim() <> .opType Then Return True
            If cmbVoLTE.SelectedIndex <> If(.VoLTE = 0, 0, 1) Then Return True
            If cmbNetType.SelectedIndex <> CInt(.netType) Then Return True
        End With

        Return False
    End Function

    ' Отменить изменения - это вернуть то, что стоит в контроллере.
    Private Sub btnRevert_Click(sender As Object, e As EventArgs) Handles btnRevert.Click
        ReadConfig(False)
    End Sub

    ' Выгрузки и загрузки базы идентификаторов в программе нет: в протоколе с
    ' контроллером она не заведена. Кнопка должна об этом сказать, а не молчать.
    Private Sub btnExport_Click(sender As Object, e As EventArgs) Handles btnExport.Click
        Notify("База идентификаторов", "Выгрузка базы идентификаторов пока не сделана.")
    End Sub

    Private Sub btnImport_Click(sender As Object, e As EventArgs) Handles btnImport.Click
        Notify("База идентификаторов", "Загрузка базы идентификаторов пока не сделана.")
    End Sub

    Private Sub btnClear_Click(sender As Object, e As EventArgs) Handles btnClear.Click
        Using f As New frmInfo("Очистка памяти", "Вы уверены что хотите удалить из памяти все идентификаторы?",, True)
            f.Button1.Text = "Очистить"
            f.Button2.Text = "Отмена"
            If f.ShowDialog(Me) = DialogResult.OK Then
                If opros.ClearMem() Then
                    Notify("Очистка памяти", "Память идентификаторов очищена.")
                Else
                    Notify("Очистка памяти", "Не удалось очистить память идентификаторов. Повторите операцию.", True)
                End If
            End If
        End Using
    End Sub

    Private Async Sub btnUpdateObj_Click(sender As Object, e As EventArgs) Handles btnUpdateObj.Click

        Await QueryObject(17)

    End Sub

    Private Async Sub btnUnConnect_Click(sender As Object, e As EventArgs) Handles btnUnConnect.Click
        Using f As New frmInfo("Отвязка от объекта", "Вы уверены что хотите отвязать контроллер от объекта?",, True)
            f.Button1.Text = "Отвязать"
            f.Button2.Text = "Отмена"
            If f.ShowDialog(Me) = DialogResult.OK Then
                Await QueryObject(18)
                'Dim str As String
                'If opros.UnConnectObject() Then
                '    str = "Команда выполнена!"
                'Else
                '    str = "Не удалось выполнить!" & vbCrLf & "Повторите операцию!"
                'End If
                'Using f2 As New frmInfo("Информация", str)
                '    f2.ShowDialog()
                'End Using
            End If
        End Using
    End Sub

    Private Sub btnClearLog_Click(sender As Object, e As EventArgs) Handles btnClearLog.Click
        RichTextBox1.Clear()
        logTail = ""
    End Sub

    ' Журнал приходит кусками, строка может прийти не целиком: красим только
    ' дописанные до конца строки, хвост ждёт продолжения.
    Private Sub onLogTextReceive(ByVal _str As String)
        logTail &= _str

        Dim lines As String() = logTail.Split(New String() {vbCrLf, vbLf}, StringSplitOptions.None)

        For i As Integer = 0 To lines.Length - 2
            AppendLogLine(lines(i))
        Next

        logTail = lines(lines.Length - 1)

        If chAutoScroll.Checked Then
            RichTextBox1.SelectionStart = RichTextBox1.TextLength
            RichTextBox1.ScrollToCaret()
        End If
    End Sub

    Private Sub AppendLogLine(ByVal line As String)
        ' журнал за долгий прогон вырастает на сотни тысяч знаков и начинает тормозить
        If RichTextBox1.TextLength > LogLimit Then
            RichTextBox1.Clear()
            RichTextBox1.SelectionColor = Theme.TextMuted
            RichTextBox1.AppendText("--- журнал очищен: он вырос больше " & LogLimit & " знаков ---" & vbCrLf)
        End If

        RichTextBox1.SelectionStart = RichTextBox1.TextLength
        RichTextBox1.SelectionLength = 0
        RichTextBox1.SelectionColor = LogColor(line)
        RichTextBox1.AppendText(line & vbCrLf)
    End Sub

    ' Цвет по заметным признакам в строке. Красным - только ошибки.
    Private Function LogColor(ByVal line As String) As Color
        Dim t As String = line.Trim()
        Dim l As String = t.ToUpperInvariant()

        If l.Contains("ERROR") OrElse l.Contains("FAIL") OrElse l.Contains("ОШИБ") OrElse l.Contains("СБОЙ") OrElse l.Contains("НЕ ОТВЕТ") OrElse l.Contains("ПОТЕРЯ") OrElse l.Contains("ОТКАЗ") Then Return Theme.Danger
        If l.Contains("WARN") OrElse l.Contains("ПРЕДУПРЕ") Then Return Theme.Warning

        ' переписка с модемом - подробность, а не событие: команды, ответы,
        ' незапрошенные сообщения модуля и разделители
        If t.Contains("→") OrElse t.Contains("←") Then Return Theme.TextMuted
        If t.StartsWith("+") OrElse t.StartsWith(">") OrElse t.StartsWith("<") OrElse
           t.StartsWith("AT") OrElse t.StartsWith("-----") OrElse l = "OK" Then Return Theme.TextMuted

        Return Theme.TextMain
    End Function

    Private Sub Label16_Click(sender As Object, e As EventArgs) Handles Label16.Click
        ' Контроллер и сервер считают время в UTC; местное время уводило часы на пояс вперёд.
        Dim ok = opros.SetTime(DateTimeToUnixTimestamp(DateTime.UtcNow))
        Notify("Время контроллера", If(ok, "Время контроллера установлено.", "Не удалось установить время контроллера."), Not ok)
    End Sub

    Private Sub Label19_Click(sender As Object, e As EventArgs) Handles Label19.Click
        Dim arr As New ArrayList

        arr.Add(New frmSelecter.mItem With {.ID = 1, .Name = "UG95/UG915"})
        arr.Add(New frmSelecter.mItem With {.ID = 2, .Name = "SLM336"})
        arr.Add(New frmSelecter.mItem With {.ID = 3, .Name = "M66"})
        arr.Add(New frmSelecter.mItem With {.ID = 0, .Name = "Отмена"})

        Using f As New frmSelecter("Тип модуля", arr, 2, 14)
            f.ShowDialog(Me)

            If f.ID = 0 Then
                Exit Sub
            End If
            Dim str As String
            If opros.SetModuleType(f.ID) Then
                str = "Команда выполнена!" & vbCrLf & "Контроллер будет перезагружен!"
            Else
                str = "Не удалось выполнить!" & vbCrLf & "Повторите операцию!"
            End If
            ' Окно сейчас закроется - строку состояния никто не увидит, поэтому окно.
            Using f2 As New frmInfo("Тип модуля", str)
                f2.ShowDialog(Me)
            End Using
            Me.Close()
        End Using
    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        ReadConfig(False)
    End Sub

    ' Чтение настроек с платы. При открытии окна - итог подключения в строке состояния, по кнопке - ответ чтения.
    Private Sub ReadConfig(ByVal silent As Boolean)
        If opros.GetConfig(config) Then
            ConfirmConfigSnapshot()
            snapshotConfig = config
            RefreshSnapshot()
            UpdateConfigFields()
            RevertWifi()
            lblSaveState.Text = "Настройки считаны в " & DateTime.Now.ToString("HH:mm:ss")
            lblSaveState.ForeColor = Theme.TextMuted
            If silent Then
                Notify("Подключение", "плата " & lblPortInfo.Text & " подключена, настройки считаны")
            Else
                Notify("Чтение настроек", "Настройки считаны с контроллера.")
            End If
            Exit Sub
        End If

        lblSaveState.Text = "Не удалось считать настройки"
        lblSaveState.ForeColor = Theme.Danger

        If silent Then
            Notify("Подключение", "плата " & lblPortInfo.Text & " подключена, настройки не считаны", True)
            Exit Sub
        End If

        Notify("Чтение настроек", "Не удалось считать настройки. Повторите операцию.", True)
    End Sub





    ' После ПИН/переподключения плата снова получает текущий выбор.
    Private Sub RestoreLogSelection()
        ShowLogSelection(opros.LogBits)
        If Not opros.EnableLog(opros.LogBits) Then AppendLogLine("Журнал: плата не подтвердила выбор событий. Проверьте USB-подключение и повторите выбор.")
    End Sub

    Private Sub ShowLogSelection(bits As Byte)
        updatingLog = True
        CheckBox1.Checked = (bits And 1) <> 0
        CheckBox2.Checked = (bits And 2) <> 0
        CheckBox3.Checked = (bits And 4) <> 0
        updatingLog = False
    End Sub

    Private Sub LogSelectionChanged(sender As Object, e As EventArgs) Handles CheckBox1.CheckedChanged, CheckBox2.CheckedChanged, CheckBox3.CheckedChanged
        If updatingLog Then Return
        Dim bits = CByte(If(CheckBox1.Checked, 1, 0) Or If(CheckBox2.Checked, 2, 0) Or If(CheckBox3.Checked, 4, 0))
        If Not opros.EnableLog(bits) Then AppendLogLine("Журнал: плата не подтвердила выбор событий. Проверьте USB-подключение и повторите выбор.")
    End Sub

    Private Sub Label20_Click(sender As Object, e As EventArgs) Handles Label20.Click
        If opros.ResetController() Then
            Me.Close()
        End If
    End Sub

    Private Delegate Sub delegate_onRecStr(ByVal str As String)

    Private Sub OnRecStr(ByVal str As String)
        Dim UIUpdater As New delegate_onRecStr(AddressOf onLogTextReceive)
        Me.BeginInvoke(UIUpdater, str)
    End Sub

    Private Sub CheckBox4_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBox4.CheckedChanged
        If CheckBox4.Checked Then
            'RemoveHandler opros.onStatus, AddressOf opros_onStatus
            opros.StopGetStatus(True)
        Else
            'AddHandler opros.onStatus, AddressOf opros_onStatus
            opros.StopGetStatus(False)
        End If
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        Clipboard.Clear()
        Clipboard.SetText(RichTextBox1.Text)
    End Sub

    Private Async Function QueryObject(cmd As Byte) As Threading.Tasks.Task
        Dim imei = lblIMEI.Text.Trim()
        btnUpdateObj.Enabled = False : btnUnConnect.Enabled = False
        Try
            Dim rec = Await Threading.Tasks.Task.Run(Function() SldnStream.QueryServer("93.189.229.197", 15010, cmd, imei))
            If IsDisposed OrElse Disposing Then Return
            Dim message As String
            Dim failed = True
            Select Case rec(9)
                Case 1
                    message = "Контроллер отсутствует в базе."
                Case 2
                    message = If(cmd = 17, "Контроллер не привязан к объекту.", "Контроллер отвязан от всех объектов.")
                    failed = False
                    lblObjNum.Text = "" : lblObjName.Text = "" : lblObjController.Text = "" : lblAdmin.Text = ""
                Case 3
                    If cmd = 17 Then
                        lblObjNum.Text = System.Text.Encoding.UTF8.GetString(rec, 10, 8).TrimEnd(ChrW(0))
                        lblObjName.Text = System.Text.Encoding.UTF8.GetString(rec, 18, 128).TrimEnd(ChrW(0))
                        lblObjController.Text = System.Text.Encoding.UTF8.GetString(rec, 146, 128).TrimEnd(ChrW(0))
                        lblAdmin.Text = System.Text.Encoding.UTF8.GetString(rec, 274, 16).TrimEnd(ChrW(0))
                        message = "Данные о привязке получены."
                        failed = False
                    Else
                        message = "Не удалось отвязать контроллер."
                    End If
                Case Else
                    message = "Сервер не подтвердил выполнение запроса."
            End Select
            Notify("Объект", message, failed)
        Catch ex As Exception
            If Not IsDisposed AndAlso Not Disposing Then
                Notify("Объект", "Запрос к серверу не выполнен: " & ex.GetBaseException().Message, True)
            End If
        Finally
            If Not IsDisposed AndAlso Not Disposing Then
                btnUpdateObj.Enabled = Not busyWithBoard : btnUnConnect.Enabled = Not busyWithBoard
            End If
        End Try
    End Function
    Private Sub Label21_Click(sender As Object, e As EventArgs) Handles Label21.Click
        Dim socket As New Net.Sockets.TcpClient
        Dim sList As String() = txtServer1.Text.Trim().Split(":"c)
        Dim port As Integer = 15010

        If sList.Count > 2 OrElse (sList.Count = 2 AndAlso Not Integer.TryParse(sList(1), port)) Then
            Notify("Проверка сервера", "Адрес сервера записан неправильно. Нужно адрес:порт.", True)
            Exit Sub
        End If

        Try
            socket.Connect(sList(0), port)
        Catch ex As Exception
            Notify("Проверка сервера", "Нет соединения с сервером " & txtServer1.Text.Trim() & ": " & ex.Message, True)
            Exit Sub
        End Try

        If socket.Connected Then
            Notify("Проверка сервера", "Соединение с сервером " & txtServer1.Text.Trim() & " установлено.")

            socket.Close()
        End If
    End Sub

    Private Delegate Sub delegate_onErrorPort()
    Private Sub OnErrorPort()
        Dim UIUpdater As New delegate_onErrorPort(AddressOf UIonErrorPort)
        Me.BeginInvoke(UIUpdater)
    End Sub

    Private Sub UIonErrorPort()

        ' Во время записи прошивки или замены загрузчика контроллер перезагружается
        ' сам, и порт пропадает по делу. Про это рассказывает экран операции,
        ' а не окно с ошибкой поверх него.
        If busyWithBoard Then Exit Sub

        RemoveHandler opros.GetComPort.ErrorPort, AddressOf OnErrorPort

        ' Окно сейчас закроется - строку состояния никто не увидит, поэтому окно.
        Using f As New frmInfo("Подключение", "Потеряно соединение с контроллером!")
            f.ShowDialog(Me)
        End Using

        Me.Close()

    End Sub
    Private Sub OnPinRequired()
        Me.BeginInvoke(New delegate_onErrorPort(AddressOf UIonPinRequired))
    End Sub

    ' Контроллер больше не считает программу вошедшей по ПИН-коду - вернуться на экран входа.
    Private Sub UIonPinRequired()
        If busyWithBoard Then Exit Sub
        RemoveHandler opros.GetComPort.PinRequired, AddressOf OnPinRequired
        RemoveHandler opros.GetComPort.ErrorPort, AddressOf OnErrorPort
        Using f As New frmInfo("Подключение", "Контроллер просит ПИН-код. Подключитесь заново.")
            f.ShowDialog(Me)
        End Using
        Me.Close()
    End Sub
End Class
