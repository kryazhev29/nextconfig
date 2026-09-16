Imports System.Globalization
Imports System.IO
Imports System.IO.Ports

' Один экран хода для всех долгих операций с платой:
'  - обновление прошивки через работающую прошивку (команды 19 и 20);
'  - замена загрузчика через работающую прошивку (команда 110);
'  - запись прошивки в плату, которая стоит в загрузчике (протокол загрузчика).
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

    Private ReadOnly fName As String
    Private ReadOnly mode As eMode
    Private ReadOnly opros As clOpros
    Private ReadOnly bootPort As ClComPort
    Private ReadOnly boardHw As Byte

    Private fSize As Integer
    Private fReaded As Integer
    Private image As Byte()
    Private app As clBoot.s_appfile
    Private fileHw As Byte
    Private break As Boolean = False
    Private running As Boolean = False
    Private applyRun As Boolean = False
    Private nBlock As Integer
    Private thread_work As Threading.Thread

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

    ' Запись прошивки в плату, стоящую в загрузчике.
    Public Sub New(ByVal _file As String, ByVal _port As ClComPort)

        InitializeComponent()

        fName = _file
        mode = eMode.FW_VIA_BOOT
        bootPort = _port
    End Sub

    Private Sub frmFW_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Dim errText As String = ""

        Label5.Text = fName
        Label1.Text = "0/0"
        btnCheck.Visible = (mode = eMode.BOOT_REPLACE)
        Button1.Enabled = False

        Select Case mode
            Case eMode.FW_UPDATE
                lblTitle.Text = "Обновление прошивки"
                Button1.Text = "Обновить"

                If Not clBoot.CheckAppFile(fName, app, errText) Then
                    lblInfo.Text = errText
                    Exit Sub
                End If

                image = app.image
                fSize = image.Length
                lblInfo.Text = "Прошивка " & app.version & " для платы " & app.hard & ", " & fSize &
                               " байт, контрольная сумма 0x" & clOpros.Crc32Stm(image).ToString("X8")
                Label1.Text = "0/" & fSize

            Case eMode.FW_VIA_BOOT
                lblTitle.Text = "Запись прошивки через загрузчик"
                Button1.Text = "Прошить"

                If Not clBoot.CheckAppFile(fName, app, errText) Then
                    lblInfo.Text = errText
                    Exit Sub
                End If

                image = app.image
                fSize = image.Length
                lblInfo.Text = "Прошивка " & app.version & " для платы " & app.hard & ", " & fSize &
                               " байт, контрольная сумма 0x" & clOpros.Crc32Stm(image).ToString("X8")
                lblState.Text = "Плата стоит в загрузчике. Не отключайте питание и USB."

            Case eMode.BOOT_REPLACE
                lblTitle.Text = "Замена загрузчика"
                Button1.Text = "Заменить загрузчик"
                btnCheck.Text = "Пробная передача"

                Try
                    image = File.ReadAllBytes(fName)
                Catch ex As Exception
                    lblInfo.Text = ex.Message
                    Exit Sub
                End Try

                If Not clOpros.CheckBootImage(image, fileHw, errText) Then
                    lblInfo.Text = errText
                    Exit Sub
                End If

                fSize = image.Length
                lblInfo.Text = "Загрузчик 0x32 для платы " & fileHw & ", " & fSize &
                               " байт, контрольная сумма 0x" & clOpros.Crc32Stm(image).ToString("X8")

                ' Плата под старым загрузчиком свою версию не знает и сообщает 0 —
                ' тогда сверять не с чем, файл выбирается по плате.
                If boardHw <> 0 AndAlso boardHw <> fileHw Then
                    lblInfo.Text = "Файл для платы " & fileHw & ", а контроллер сообщает плату " & boardHw & "!"
                    Exit Sub
                End If

                lblState.Text = "Сначала пробная передача, она ничего не меняет. Потом замена."
                Label1.Text = "0/" & fSize

                ' здесь необратима сама операция, а не выход из окна
                Button1.BackColor = Color.Red
                Button2.BackColor = Color.Gainsboro
                Button2.ForeColor = Color.Black
        End Select

        Button1.Enabled = True
        btnCheck.Enabled = (mode = eMode.BOOT_REPLACE)
    End Sub

    ' Пробная передача загрузчика: контроллер принимает и проверяет файл, затем отмена.
    Private Sub btnCheck_Click(sender As Object, e As EventArgs) Handles btnCheck.Click
        StartWork(False)
    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click

        If mode = eMode.BOOT_REPLACE Then
            ' Двойное подтверждение по п. 9.6 краткого ТЗ.
            Using f As New frmInfo("Внимание", "Заменить загрузчик контроллера на файл" & vbCrLf &
                                   Path.GetFileName(fName) & "?" & vbCrLf &
                                   "Плата " & fileHw & ".", , True)
                f.Button1.Text = "Да"
                f.Button2.Text = "Нет"
                If f.ShowDialog(Me) <> DialogResult.OK Then
                    Exit Sub
                End If
            End Using

            Using f As New frmInfo("Внимание", "Замена необратима: вернуть прежний загрузчик" & vbCrLf &
                                   "без программатора нельзя." & vbCrLf &
                                   "Не отключайте питание и USB до конца замены!", , True)
                f.Button1.Text = "Заменить"
                f.Button2.Text = "Отмена"
                If f.ShowDialog(Me) <> DialogResult.OK Then
                    Exit Sub
                End If
            End Using
        End If

        StartWork(True)
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        break = True

        While running
            Threading.Thread.Sleep(100)
        End While

        Me.DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub

    Private Sub StartWork(ByVal apply As Boolean)
        applyRun = apply
        running = True
        break = False
        Button1.Enabled = False
        btnCheck.Enabled = False
        ProgressBar1.Value = 0

        Select Case mode
            Case eMode.BOOT_REPLACE
                lblState.Text = If(apply, "Идёт замена загрузчика...", "Идёт проверка передачей...")
            Case Else
                lblState.Text = "Идёт запись прошивки..."
        End Select

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
                    ok = opros.ReplaceBootloader(image, applyRun, AddressOf onProgress, errText)
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

        While Not break AndAlso nBlock < fwInfo.nBlocks

            ' Блок читается по своему смещению: после неудачи повторяется тот же блок.
            If opros.WriteFWBlock(CUShort(nBlock), BlockOf(image, nBlock), percent) = clOpros.eCmdAnswer.CMD_SUCCESS Then
                nBlock += 1
                fReaded = Math.Min(nBlock * 1024, fSize)
                tries = 0
            Else
                tries += 1
            End If

            Select Case percent
                Case 248
                    Return True
                Case 249, 250, 251, 252, 253, 254
                    errText = ProgressText(percent)
                    Return False
                Case 255
                    ' блок отклонён, обновление продолжается — повторяем тот же блок
                Case Else
                    fw_onRead(fReaded, percent)
            End Select

            If tries >= MAX_BLOCK_TRIES Then
                errText = "Блок " & nBlock & " не принят " & MAX_BLOCK_TRIES & " раз подряд. Обновление прервано."
                Return False
            End If

            Threading.Thread.Sleep(10)
        End While

        If break Then
            errText = "Прервано."
            Return False
        End If

        errText = "Контроллер не сообщил о завершении обновления."
        Return False
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
                Return "Ошибка в параметрах команды!"
            Case clOpros.eCmdAnswer.CMD_RUNERROR
                Return "Контроллер занят: идёт обновление, пробный запуск новой прошивки или замена загрузчика."
            Case clOpros.eCmdAnswer.CMD_UNDEFRESULT
                Return "Контроллер не ответил на команду обновления!"
            Case clOpros.eCmdAnswer.CMD_GETPASSWORD
                Return "Требуется ввод пароля!"
            Case clOpros.eCmdAnswer.CMD_ROLLBACKRUN
                Return "Контроллер запустил откат на предыдущую прошивку."
            Case clOpros.eCmdAnswer.CMD_CRCERROR
                Return "Ошибка в контрольной сумме!"
            Case clOpros.eCmdAnswer.CMD_UNDEFINE
                Return "Команда не поддерживается!"
            Case Else
                Return "Контроллер отказал, код " & CInt(rez) & "."
        End Select
    End Function

    ' Код хода обновления в команде 4 (лист «Протокол», D473).
    Private Shared Function ProgressText(ByVal percent As Byte) As String
        Select Case percent
            Case 249
                Return "Обновление не уложилось в отведённый час."
            Case 250
                Return "Прошивка собрана для другой платы!"
            Case 251
                Return "Блоки перестали приходить, обновление прекращено."
            Case 252
                Return "Не верная контрольная сумма!"
            Case 253
                Return "В контроллере нет резервной копии прошивки."
            Case 254
                Return "Прошивка с таким номером уже установлена."
            Case Else
                Return "Сбой обновления, код " & percent & "."
        End Select
    End Function

    Private Sub onProgress(ByVal percent As Integer)
        fw_onRead(CInt(CLng(fSize) * percent \ 100), CByte(Math.Max(0, Math.Min(100, percent))))
    End Sub

    Private Sub fw_onRead(ByVal readed As Integer, ByVal percent As Byte)
        Dim UIUpdater As New delegate_onReaded(AddressOf refOnReaded)
        Me.BeginInvoke(UIUpdater, readed, percent)
    End Sub

    Private Sub refOnReaded(ByVal readed As Integer, ByVal percent As Byte)
        Label1.Text = readed & "/" & fSize

        If percent <= 100 Then
            ProgressBar1.Value = percent
        End If
    End Sub

    Private Sub onDone(ByVal ok As Boolean, ByVal text As String)
        Me.BeginInvoke(New delegate_onDone(AddressOf refDone), ok, text)
    End Sub

    Private Sub refDone(ByVal ok As Boolean, ByVal text As String)
        running = False

        If Not ok Then
            lblState.Text = "Не выполнено."
            ShowMsg("Ошибка!", text)
            Button1.Enabled = True
            btnCheck.Enabled = (mode = eMode.BOOT_REPLACE)
            Exit Sub
        End If

        Select Case mode
            Case eMode.FW_UPDATE
                lblState.Text = "Прошивка записана, контроллер её устанавливает."
                ShowMsg("Обновление", "Обновление завершено!" & vbCrLf &
                        "Контроллер устанавливает прошивку и перезагружается.")

            Case eMode.FW_VIA_BOOT
                lblState.Text = "Прошивка записана, загрузчик её устанавливает."
                ShowMsg("Готово", "Прошивка записана." & vbCrLf &
                        "Загрузчик устанавливает её и запускает." & vbCrLf &
                        "Подключитесь заново примерно через 20 секунд.")

            Case eMode.BOOT_REPLACE
                If Not applyRun Then
                    lblState.Text = "Файл принят и проверен контроллером, замена отменена."
                    ShowMsg("Проверка", "Контроллер принял и проверил файл загрузчика." & vbCrLf &
                            "На плате ничего не изменилось.")
                    Button1.Enabled = True
                    btnCheck.Enabled = True
                    Exit Sub
                End If

                lblState.Text = "Загрузчик заменён, контроллер перезагружается."
                ShowMsg("Готово", "Контроллер перезаписывает загрузчик и перезагружается." & vbCrLf &
                        "Через 15 секунд подключитесь заново и проверьте" & vbCrLf &
                        "версию загрузчика: она должна стать 50.")
        End Select

        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub

    ' Окно показывает поток окна, а не поток записи: иначе форма живёт в двух потоках.
    Private Sub ShowMsg(ByVal caption As String, ByVal text As String)
        If Me.InvokeRequired Then
            Me.Invoke(New Action(Of String, String)(AddressOf ShowMsg), caption, text)
            Exit Sub
        End If

        Using f As New frmInfo(caption, text)
            f.ShowDialog(Me)
        End Using
    End Sub

End Class
