Imports System.Globalization
Imports System.IO
Imports System.IO.Ports

' Один экран для всех долгих операций с платой:
'  - обновление прошивки через работающую прошивку (команды 19 и 20);
'  - замена загрузчика через работающую прошивку (команда 110);
'  - запись прошивки в плату, которая стоит в загрузчике (протокол загрузчика).
' Оператор видит шаги, что нужно до начала, ход в процентах и что будет дальше.
Public Class frmFW

    Public Enum eMode
        FW_UPDATE = 0    ' прошивка по командам 19 и 20
        BOOT_REPLACE = 1 ' замена загрузчика командой 110
        FW_VIA_BOOT = 2  ' прошивка через загрузчик
    End Enum

    ' метка платы в образе: вектор 8, 0x4857 и номер HW в младшем байте
    Private Const HW_MARKER_OFFSET As Integer = &H20
    Private Const HW_MARKER_TAG As UInt32 = &H48570000UI
    Private Const MAX_BLOCK_TRIES As Integer = 5

    Private fName As String
    Private ReadOnly mode As eMode
    Private ReadOnly opros As clOpros
    Private ReadOnly bootPort As ClComPort
    Private ReadOnly boardHw As Byte

    ' сборка, которая сейчас на плате (0 - неизвестна): для «было / станет»
    Public Property BoardBuild As UInteger

    ' сборка из выбранного файла - её ждём на плате после перезапуска
    Public ReadOnly Property FileBuild As UInteger
        Get
            Return app.build
        End Get
    End Property

    Private fSize As Integer
    Private fReaded As Integer
    Private image As Byte()
    Private app As clBoot.s_appfile
    Private fileHw As Byte
    Private running As Boolean = False
    Private doneOk As Boolean = False
    Private nBlock As Integer
    Private thread_work As Threading.Thread
    Private ReadOnly clock As New Stopwatch()
    Private ReadOnly lblSteps As New Label With {.Name = "lblSteps", .AutoSize = True}
    Private steps As String()
    Private stepNow As Integer

    Private Delegate Sub delegate_onReaded(ByVal readed As Integer, ByVal percent As Byte)
    Private Delegate Sub delegate_onDone(ByVal ok As Boolean, ByVal text As String)

    ' Обновление прошивки.
    Public Sub New(ByVal _file As String, ByVal _opros As clOpros)

        ' Этот вызов является обязательным для конструктора.
        InitializeComponent()

        fName = _file
        mode = eMode.FW_UPDATE
        opros = _opros
    End Sub

    ' Замена загрузчика. boardHw — плата из статуса контроллера, 0 если он её не знает.
    Public Sub New(ByVal _file As String, ByVal _opros As clOpros, ByVal _boardHw As Byte)

        InitializeComponent()

        fName = _file
        mode = eMode.BOOT_REPLACE
        opros = _opros
        boardHw = _boardHw
    End Sub

    ' Запись прошивки в плату, стоящую в загрузчике. bootVer - версия из опроса:
    ' ниже 0x30 - старый загрузчик, первый шаг перевода платы на новую систему.
    ' targetHw - номер платы в новой системе (0 - неизвестен): файл другой платы не пишется.
    Public Sub New(ByVal _file As String, ByVal _port As ClComPort, ByVal _bootVer As Byte, Optional ByVal targetHw As Byte = 0)

        InitializeComponent()

        fName = _file
        mode = eMode.FW_VIA_BOOT
        bootPort = _port
        oldBoot = _bootVer < clBoot.OLD_BOOT_BELOW
        boardHw = targetHw
    End Sub

    Private ReadOnly oldBoot As Boolean

    Private Sub frmFW_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        AgentLog.WatchText(lblState, "frmFW.state")

        ' шаги - сразу под заголовком: где оператор сейчас и что осталось
        Theme.Caption(lblSteps)
        lblSteps.ForeColor = Theme.TextMain
        lblSteps.Margin = New Padding(0, 0, 0, Theme.GapSection)
        root.Controls.Add(lblSteps)
        root.Controls.SetChildIndex(lblSteps, 1)

        Select Case mode
            Case eMode.FW_UPDATE
                lblTitle.Text = "Обновление прошивки"
                steps = {"Подготовка", "Передача файла", "Установка на плате", "Проверка после перезапуска"}
                Button1.Text = "Начать обновление"
            Case eMode.FW_VIA_BOOT
                lblTitle.Text = If(oldBoot, "Перевод на новую систему: шаг 1 из 2", "Запись прошивки")
                steps = If(oldBoot, {"Подготовка", "Запись новой прошивки", "Перезапуск питания", "Шаг 2: замена загрузчика"},
                           {"Подготовка", "Запись прошивки", "Запуск прошивки"})
                Button1.Text = "Начать запись"
            Case eMode.BOOT_REPLACE
                lblTitle.Text = If(boardHw = 0, "Перевод на новую систему: шаг 2 из 2", "Замена загрузчика")
                steps = {"Подготовка", "Проверка файла на плате", "Замена загрузчика", "Проверка после перезапуска"}
                Button1.Text = "Заменить загрузчик"
        End Select

        btnCheck.Text = "Выбрать другой файл"
        Theme.Secondary(btnCheck)
        lblStageCap.Text = "Что сейчас"
        lblFileCap.Text = "Что будет записано"
        ShowStep(0)
        LoadFile()
    End Sub

    ' Шаги списком: ✓ пройден, ● сейчас, ○ впереди.
    Private Sub ShowStep(ByVal n As Integer)
        stepNow = n
        Dim lines As New List(Of String)
        For i = 0 To steps.Length - 1
            Dim mark = If(i < n, "✓", If(i = n, "●", "○"))
            lines.Add(mark & "  " & (i + 1) & ". " & steps(i))
        Next
        lblSteps.Text = String.Join(vbCrLf, lines)
    End Sub

    ' Проверка файла и то, что оператору важно знать до начала.
    Private Sub LoadFile()
        Dim errText As String = ""
        AgentLog.Write("fwfile", "mode", mode.ToString(), "file", fName)
        Label5.Text = Path.GetFileName(fName)
        Label1.Text = ""
        ProgressBar1.Value = 0
        lblState.ForeColor = Theme.TextMain
        Theme.Caption(lblInfo)
        Button1.Visible = True
        Button1.Enabled = False
        btnCheck.Visible = True
        btnCheck.Enabled = True

        Select Case mode
            Case eMode.FW_UPDATE, eMode.FW_VIA_BOOT
                If Not clBoot.CheckAppFile(fName, app, errText) Then
                    FileRejected(errText)
                    Exit Sub
                End If
                If boardHw <> 0 AndAlso app.hard <> boardHw Then
                    FileRejected("Этот файл для платы " & app.hard & ", а для этой платы нужен файл " & boardHw &
                                 ".X.X.bin" & If(boardHw = 140, " (старая плата ДОМОВОЙ IP).", "."))
                    Exit Sub
                End If
                image = app.image
                fSize = image.Length
                lblInfo.Text = FirmwareText()
                lblState.Text = PreparationText()
                If mode = eMode.FW_UPDATE AndAlso BoardBuild <> 0 AndAlso app.build = BoardBuild Then
                    lblState.Text = "Эта прошивка уже стоит на плате — обновлять нечего." & vbCrLf &
                                    "Если нужен другой файл, нажмите «Выбрать другой файл»."
                    Exit Sub
                End If

            Case eMode.BOOT_REPLACE
                Try
                    image = File.ReadAllBytes(fName)
                Catch ex As Exception
                    FileRejected("Файл не читается: " & ex.Message)
                    Exit Sub
                End Try

                If Not clOpros.CheckBootImage(image, fileHw, errText) Then
                    FileRejected(errText)
                    Exit Sub
                End If

                ' Плата под старым загрузчиком свою версию не знает и сообщает 0 —
                ' тогда сверять не с чем, файл выбирается по плате.
                If boardHw <> 0 AndAlso boardHw <> fileHw Then
                    FileRejected("Этот загрузчик для платы " & fileHw & ", а подключена плата " & boardHw & ". Выберите файл для своей платы.")
                    Exit Sub
                End If

                fSize = image.Length
                Dim profile = FirmwareFiles.ProfileName(fName)
                lblInfo.Text = "Новый загрузчик для платы " & fileHw &
                               If(profile.Length <= 6, " (комплект " & profile & ")", "") & "." & vbCrLf &
                               "Файл проверен и подходит к этой плате."
                AgentLog.Write("fwfile", "crc", clOpros.Crc32Stm(image).ToString("X8"), "hw", fileHw)
                lblState.Text = PreparationText()
                ' здесь необратима сама операция, а не выход из окна
                Theme.DangerOutline(Button1)
        End Select

        Button1.Enabled = True
        Me.AcceptButton = Button1
    End Sub

    Private Function PreparationText() As String
        Select Case mode
            Case eMode.BOOT_REPLACE
                Return "Загрузчик — служебная программа платы, через которую ставятся прошивки. Меняется один раз." & vbCrLf & vbCrLf &
                       "Перед началом проверьте:" & vbCrLf &
                       "•  плата подключена к 12 В и к компьютеру по USB;" & vbCrLf &
                       "•  до конца нельзя отключать питание и кабель — прерванная замена выводит плату из строя." & vbCrLf & vbCrLf &
                       "Сначала плата проверит файл, ничего не меняя. Если файл подходит, загрузчик заменится, " &
                       "плата перезапустится, программа подключится к ней сама. Около 1 минуты."
            Case eMode.FW_VIA_BOOT
                Return "Перед началом проверьте:" & vbCrLf &
                       "•  плата подключена к 12 В и к компьютеру по USB;" & vbCrLf &
                       "•  до конца не отключайте питание и кабель." & vbCrLf & vbCrLf &
                       "Запись займёт около 1 минуты."
            Case Else
                Return "Перед началом проверьте:" & vbCrLf &
                       "•  плата подключена к 12 В и к компьютеру по USB;" & vbCrLf &
                       "•  до конца не отключайте питание и кабель." & vbCrLf & vbCrLf &
                       "Около 1 минуты. После передачи плата сама установит прошивку и перезапустится, " &
                       "программа дождётся её и подключится сама."
        End Select
    End Function

    ' Кнопка «Выбрать другой файл»: оператор берёт файл сам, папка запоминается.
    Private Sub btnCheck_Click(sender As Object, e As EventArgs) Handles btnCheck.Click
        Using dialog As New OpenFileDialog With {.Filter = "Файлы прошивки (*.bin)|*.bin|Все файлы (*.*)|*.*"}
            Dim folder = Path.GetDirectoryName(fName)
            If folder <> "" AndAlso Directory.Exists(folder) Then dialog.InitialDirectory = folder
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Exit Sub
            fName = dialog.FileName
            FirmwareFiles.LastFolder = Path.GetDirectoryName(fName)
        End Using
        LoadFile()
    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click

        If mode = eMode.BOOT_REPLACE Then
            ' Двойное подтверждение по п. 9.6 краткого ТЗ.
            Using f As New frmInfo("Замена загрузчика", "Заменить загрузчик этой платы?" & vbCrLf &
                                   "Сначала плата проверит файл, потом загрузчик заменится.", , True)
                f.Button1.Text = "Да"
                f.Button2.Text = "Нет"
                If f.ShowDialog(Me) <> DialogResult.OK Then
                    Exit Sub
                End If
            End Using

            Using f As New frmInfo("Внимание", "Вернуть прежний загрузчик потом будет нельзя." & vbCrLf &
                                   "До конца не отключайте питание и USB!", , True)
                f.Button1.Text = "Заменить"
                f.Button2.Text = "Отмена"
                If f.ShowDialog(Me) <> DialogResult.OK Then
                    Exit Sub
                End If
            End Using
        End If

        StartWork()
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        Me.DialogResult = If(doneOk, DialogResult.OK, DialogResult.Cancel)
        Me.Close()
    End Sub

    ' Пока идёт запись, окно не закрывается: прерванная на середине операция
    ' оставляет плату в неизвестном состоянии. Каждая операция кончается сама,
    ' по ответу платы или по тайм-ауту.
    Private Sub frmFW_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        If running Then e.Cancel = True
    End Sub

    Private Sub StartWork()
        running = True
        Button1.Enabled = False
        btnCheck.Enabled = False
        Button2.Enabled = False
        ProgressBar1.Value = 0
        lblState.ForeColor = Theme.TextMain
        ShowStep(1)

        Select Case mode
            Case eMode.BOOT_REPLACE
                lblState.Text = "Плата проверяет файл. На плате пока ничего не меняется."
            Case eMode.FW_VIA_BOOT
                lblState.Text = "Идёт запись прошивки. Не отключайте питание и USB."
            Case Else
                lblState.Text = "Идёт передача прошивки в плату. Не отключайте питание и USB."
        End Select

        clock.Restart()
        thread_work = New Threading.Thread(AddressOf tWork)
        thread_work.Priority = Threading.ThreadPriority.Normal
        thread_work.Name = "deviceWork"
        thread_work.Start()
    End Sub

    Private Sub tWork()
        Dim errText As String = ""
        Dim ok As Boolean = False

        Try
            Select Case mode
                Case eMode.FW_UPDATE
                    ok = WriteFirmware(errText)
                Case eMode.FW_VIA_BOOT
                    ok = clBoot.Upload(bootPort, fName, app, AddressOf onProgress, errText)
                Case eMode.BOOT_REPLACE
                    ' сначала пробная передача: плата принимает и проверяет файл, ничего не меняя
                    ok = opros.ReplaceBootloader(image, False, AddressOf onProgress, errText)
                    If ok Then
                        Me.Invoke(New Action(Sub()
                                                 ShowStep(2)
                                                 lblState.Text = "Файл подошёл. Идёт замена загрузчика. Не отключайте питание и USB!"
                                                 ProgressBar1.Value = 0
                                                 clock.Restart()
                                             End Sub))
                        ok = opros.ReplaceBootloader(image, True, AddressOf onProgress, errText)
                    ElseIf errText <> "" Then
                        errText = "плата не приняла файл, на плате ничего не изменилось. " & errText
                    End If
            End Select
        Catch ex As Exception
            ok = False
            errText = ex.Message
        End Try

        onDone(ok, errText)
    End Sub

    ' Обновление прошивки командами 19 и 20 через работающую прошивку.
    Private Function WriteFirmware(ByRef errText As String) As Boolean
        Dim fwInfo As New clOpros.s_fwinfo

        fwInfo.hard = app.hard
        fwInfo.version = app.version
        fwInfo.build = app.build
        fwInfo.size = CUInt(image.Length)
        fwInfo.crc = clOpros.Crc32Stm(image)
        fwInfo.nBlocks = CUShort((image.Length + 1023) \ 1024)

        fReaded = 0

        Dim rez As clOpros.eCmdAnswer = opros.StartUpdate(fwInfo)

        ' 0 отвечает контроллер по USB, 5 — по каналу сервера; и то и другое значит
        ' «готов принять прошивку».
        If rez <> clOpros.eCmdAnswer.CMD_SUCCESS AndAlso rez <> clOpros.eCmdAnswer.CMD_READYTOUPDATE Then
            errText = StartAnswerText(rez)
            Return False
        End If

        Dim percent As Byte
        Dim tries As Integer = 0

        nBlock = 0

        While nBlock < fwInfo.nBlocks

            ' Блок читается по своему смещению: после неудачи повторяется тот же блок.
            Dim code As clOpros.eCmdAnswer = opros.WriteFWBlock(CUShort(nBlock), BlockOf(image, nBlock), percent)

            If percent > 100 Then
                ' Прошивка до «Протокола2» (FW 71 от 16.09) сообщала итог в поле хода.
                Select Case percent
                    Case 248
                        Return True
                    Case 255
                        tries += 1
                    Case Else
                        errText = ProgressText(percent)
                        Return False
                End Select
            Else
                ' «Протокол2», команда 4: ход 0…100 и код. 0 — блок принят,
                ' 254 и 255 — тот же блок ещё раз, 253 — контроллер прекратил загрузку.
                Select Case CInt(code)
                    Case 0
                        nBlock += 1
                        fReaded = Math.Min(nBlock * 1024, fSize)
                        tries = 0
                        fw_onRead(fReaded, percent)
                    Case 253
                        errText = "Плата прекратила загрузку. Причина записана в её журнал событий."
                        Return False
                    Case Else
                        tries += 1
                End Select
            End If

            If tries >= MAX_BLOCK_TRIES Then
                errText = "часть файла не прошла " & MAX_BLOCK_TRIES & " раз подряд. Проверьте кабель USB и начните заново."
                Return False
            End If

            Threading.Thread.Sleep(10)
        End While

        ' Все блоки приняты: контроллер сам проверит контрольную сумму файла
        ' и перезагрузится на установку.
        Return True
    End Function

    ' Блок команды 20 всегда ровно 1024 байта, хвост последнего добивается 0xFF.
    Private Shared Function BlockOf(ByVal image As Byte(), ByVal index As Integer) As Byte()
        Dim block(1023) As Byte
        Dim offset As Integer = index * 1024

        For i As Integer = 0 To 1023
            block(i) = If(offset + i < image.Length, image(offset + i), CByte(&HFF))
        Next

        Return block
    End Function

    ' Ответ контроллера на команду 19 (лист «Протокол», D532).
    Private Shared Function StartAnswerText(ByVal rez As clOpros.eCmdAnswer) As String
        Select Case rez
            Case clOpros.eCmdAnswer.CMD_PARAMERROR
                Return "плата отклонила файл (ошибка в параметрах)."
            Case clOpros.eCmdAnswer.CMD_RUNERROR
                Return "плата сейчас занята: идёт другая загрузка, пробный запуск новой прошивки или замена загрузчика. Подождите несколько минут и начните заново."
            Case clOpros.eCmdAnswer.CMD_UNDEFRESULT
                Return "плата не ответила. Проверьте кабель USB и питание и начните заново."
            Case clOpros.eCmdAnswer.CMD_GETPASSWORD
                Return "плата просит ПИН-код. Переподключитесь к плате с ПИН-кодом."
            Case clOpros.eCmdAnswer.CMD_ROLLBACKRUN
                Return "плата сейчас возвращает предыдущую прошивку. Подождите пару минут и подключитесь заново."
            Case clOpros.eCmdAnswer.CMD_CRCERROR
                Return "файл повреждён (не сходится контрольная сумма). Возьмите файл заново."
            Case clOpros.eCmdAnswer.CMD_UNDEFINE
                Return "эта плата не умеет обновляться по USB."
            Case Else
                Return "плата отказала, код " & CInt(rez) & "."
        End Select
    End Function

    ' Итог в поле хода команды 4 у прошивки до «Протокола2» (лист «Протокол», D473).
    Private Shared Function ProgressText(ByVal percent As Byte) As String
        Select Case percent
            Case 249
                Return "обновление не уложилось в отведённый час."
            Case 250
                Return "файл собран для другой платы. Выберите файл для своей платы."
            Case 251
                Return "передача прервалась. Проверьте кабель USB и начните заново."
            Case 252
                Return "файл повреждён (не сходится контрольная сумма)."
            Case 253
                Return "на плате нет резервной копии прошивки."
            Case 254
                Return "эта прошивка уже стоит на плате."
            Case Else
                Return "сбой обновления, код " & percent & "."
        End Select
    End Function

    Private Sub onProgress(ByVal percent As Integer)
        fw_onRead(CInt(CLng(fSize) * percent \ 100), CByte(Math.Max(0, Math.Min(100, percent))))
    End Sub

    Private Sub fw_onRead(ByVal readed As Integer, ByVal percent As Byte)
        Dim UIUpdater As New delegate_onReaded(AddressOf refOnReaded)
        Me.BeginInvoke(UIUpdater, readed, percent)
    End Sub

    ' Проценты и сколько примерно осталось - по скорости с начала этапа.
    Private Sub refOnReaded(ByVal readed As Integer, ByVal percent As Byte)
        If percent > 100 Then Exit Sub
        ProgressBar1.Value = percent

        Dim left As String = ""
        If percent >= 3 AndAlso percent < 100 Then
            Dim seconds = CInt(clock.Elapsed.TotalSeconds * (100 - percent) / percent)
            left = " · осталось около " & If(seconds < 60, Math.Max(5, (seconds + 4) \ 5 * 5) & " с", ((seconds + 30) \ 60) & " мин")
        End If
        Label1.Text = percent & " %" & left

        ' 100 % - это «всё отдано», дальше контроллер работает сам
        If percent >= 100 AndAlso mode = eMode.FW_UPDATE Then
            ShowStep(2)
            lblState.Text = "Файл передан. Плата проверяет его и начинает установку."
        End If
    End Sub

    Private Sub onDone(ByVal ok As Boolean, ByVal text As String)
        Me.BeginInvoke(New delegate_onDone(AddressOf refDone), ok, text)
    End Sub

    Private Sub refDone(ByVal ok As Boolean, ByVal text As String)
        running = False
        clock.Stop()
        Button2.Enabled = True

        If Not ok Then
            ShowStep(0)
            lblState.Text = "Не получилось: " & text & vbCrLf & vbCrLf &
                            "Можно нажать «" & Button1.Text & "» ещё раз или закрыть окно."
            lblState.ForeColor = Theme.Danger
            Button1.Enabled = True
            btnCheck.Enabled = True
            Exit Sub
        End If

        Select Case mode
            Case eMode.FW_UPDATE
                ShowStep(3)
                Finish("Файл передан, плата устанавливает прошивку и перезапускается." & vbCrLf &
                       "Нажмите «Дальше» — программа дождётся платы, подключится сама и покажет итог." & vbCrLf &
                       "Питание и USB не отключайте.")

            Case eMode.FW_VIA_BOOT
                If oldBoot Then
                    ShowStep(2)
                    Finish("Новая прошивка записана." & vbCrLf & vbCrLf &
                           "Теперь:" & vbCrLf &
                           "1.  Отключите от платы 12 В и USB." & vbCrLf &
                           "2.  Подождите 5 секунд и включите снова (кнопку на плате не нажимать)." & vbCrLf &
                           "3.  Подключитесь к плате — программа предложит шаг 2, замену загрузчика.")
                Else
                    ShowStep(2)
                    Finish("Прошивка записана. Плата запускает её." & vbCrLf &
                           "Через 20 секунд плата появится в списке — подключитесь к ней.")
                End If

            Case eMode.BOOT_REPLACE
                ShowStep(3)
                Finish("Загрузчик передан, плата записывает его и перезапускается." & vbCrLf &
                       "Нажмите «Дальше» — программа дождётся платы, подключится сама и проверит загрузчик." & vbCrLf &
                       "Питание и USB не отключайте.")
        End Select
    End Sub

    ' Работа кончилась удачно: итог в окне, дальше только одна кнопка.
    Private Sub Finish(ByVal text As String)
        doneOk = True

        lblState.Text = text
        lblState.ForeColor = Theme.OkText
        ProgressBar1.Value = ProgressBar1.Maximum
        Label1.Text = "100 %"

        Button1.Visible = False
        btnCheck.Visible = False
        Button2.Text = If(mode = eMode.FW_VIA_BOOT, "Готово", "Дальше")
        Theme.Primary(Button2)
        Button2.Enabled = True
        Me.AcceptButton = Button2
    End Sub

    ' Что за прошивка, простыми словами: что стоит сейчас и что будет.
    Private Function FirmwareText() As String
        Dim marked As Boolean = False

        If image.Length >= HW_MARKER_OFFSET + 4 Then
            Dim mark As UInt32 = BitConverter.ToUInt32(image, HW_MARKER_OFFSET)
            marked = (mark And &HFFFFFF00UI) = HW_MARKER_TAG
        End If
        AgentLog.Write("fwfile", "crc", clOpros.Crc32Stm(image).ToString("X8"), "hw", app.hard, "marked", marked)

        Dim t As String = "Новая прошивка: сборка от " & BuildText(app.build) & "."
        If BoardBuild <> 0 Then
            t &= vbCrLf & "Сейчас на плате: сборка от " & BuildText(BoardBuild) & "."
            If app.build < BoardBuild Then t &= vbCrLf & "Внимание: файл старее той прошивки, что стоит на плате."
        End If
        Return t & vbCrLf & "Файл для платы " & app.hard & ", проверен."
    End Function

    ' Файл не подходит: сказать почему и предложить выбрать другой.
    Private Sub FileRejected(ByVal why As String)
        lblInfo.Text = why
        Theme.Note(lblInfo, True)
        lblState.Text = "Этот файл не подходит. Нажмите «Выбрать другой файл»."
        lblState.ForeColor = Theme.Danger
        Button1.Visible = False
        Me.AcceptButton = btnCheck
    End Sub

End Class
