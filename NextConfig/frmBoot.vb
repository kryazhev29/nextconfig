Imports System.IO
Imports System.IO.Ports

' Два действия сервисного ПО с загрузчиком (краткое ТЗ, разделы 8 и 9):
'  - замена самого загрузчика через работающую прошивку, команда 110;
'  - запись прошивки в плату, которая стоит в загрузчике и приложения не имеет.
Public Class frmBoot

    Public Enum eMode
        REPLACE_BOOT = 0 ' замена загрузчика командой 110
        FLASH_APP = 1    ' запись прошивки через загрузчик
    End Enum

    Private ReadOnly fName As String
    Private ReadOnly mode As eMode
    Private ReadOnly opros As clOpros
    Private ReadOnly bootPort As ClComPort
    Private ReadOnly boardHw As Byte

    Private image As Byte()
    Private app As clBoot.s_appfile
    Private fileHw As Byte
    Private running As Boolean = False
    Private applyRun As Boolean = False
    Private thread_work As Threading.Thread

    Private Delegate Sub delegate_onProgress(ByVal percent As Integer)
    Private Delegate Sub delegate_onDone(ByVal ok As Boolean, ByVal text As String)

    ' Замена загрузчика. boardHw — номер платы из статуса контроллера, 0 если он его не знает.
    Public Sub New(ByVal _file As String, ByVal _opros As clOpros, ByVal _boardHw As Byte)

        ' Этот вызов является обязательным для конструктора.
        InitializeComponent()

        fName = _file
        mode = eMode.REPLACE_BOOT
        opros = _opros
        boardHw = _boardHw
        lblFile.Text = fName
    End Sub

    ' Запись прошивки в плату, стоящую в загрузчике.
    Public Sub New(ByVal _file As String, ByVal _port As ClComPort)

        InitializeComponent()

        fName = _file
        mode = eMode.FLASH_APP
        bootPort = _port
        lblFile.Text = fName
        lblTitle.Text = "Запись прошивки через загрузчик"
    End Sub

    Private Sub frmBoot_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Dim errText As String = ""

        btnApply.Enabled = False
        btnCheck.Enabled = False

        If mode = eMode.FLASH_APP Then
            btnCheck.Visible = False
            btnApply.Text = "Прошить"

            If Not clBoot.CheckAppFile(fName, app, errText) Then
                lblInfo.Text = errText
                Exit Sub
            End If

            lblInfo.Text = "Прошивка " & app.version & " для платы " & app.hard & ", " &
                           app.image.Length & " байт, контрольная сумма 0x" &
                           clOpros.Crc32Stm(app.image).ToString("X8")
            lblState.Text = "Плата стоит в загрузчике. Не отключайте питание и USB."
            btnApply.Enabled = True
            Exit Sub
        End If

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

        lblInfo.Text = "Загрузчик 0x32 для платы " & fileHw & ", " & image.Length &
                       " байт, контрольная сумма 0x" & clOpros.Crc32Stm(image).ToString("X8")

        ' Контроллер под загрузчиком 2.1 сообщает версию платы 0 — сверять не с чем,
        ' пользователь выбирает файл по плате.
        If boardHw <> 0 AndAlso boardHw <> fileHw Then
            lblInfo.Text = "Файл для платы " & fileHw & ", а контроллер сообщает плату " & boardHw & "!"
            Exit Sub
        End If

        btnApply.Enabled = True
        btnCheck.Enabled = True
        lblState.Text = "Готов. Сначала проверка, потом замена."
    End Sub

    ' Передать файл и отменить: контроллер принимает и проверяет образ,
    ' загрузчик на плате при этом не трогается.
    Private Sub btnCheck_Click(sender As Object, e As EventArgs) Handles btnCheck.Click
        StartWork(False)
    End Sub

    Private Sub btnApply_Click(sender As Object, e As EventArgs) Handles btnApply.Click

        If mode = eMode.FLASH_APP Then
            Using f As New frmInfo("Внимание", "Записать в контроллер прошивку" & vbCrLf &
                                   Path.GetFileName(fName) & "?", , True)
                f.Button1.Text = "Да"
                f.Button2.Text = "Нет"
                If f.ShowDialog() <> DialogResult.OK Then
                    Exit Sub
                End If
            End Using

            StartWork(True)
            Exit Sub
        End If

        ' Замена загрузчика. Двойное подтверждение по п. 9.6 краткого ТЗ.
        Using f As New frmInfo("Внимание", "Заменить загрузчик контроллера на файл" & vbCrLf &
                               Path.GetFileName(fName) & "?" & vbCrLf &
                               "Плата " & fileHw & ".", , True)
            f.Button1.Text = "Да"
            f.Button2.Text = "Нет"
            If f.ShowDialog() <> DialogResult.OK Then
                Exit Sub
            End If
        End Using

        Using f As New frmInfo("Внимание", "Замена необратима: вернуть прежний загрузчик" & vbCrLf &
                               "без программатора нельзя." & vbCrLf &
                               "Не отключайте питание и USB до конца замены!", , True)
            f.Button1.Text = "Заменить"
            f.Button2.Text = "Отмена"
            If f.ShowDialog() <> DialogResult.OK Then
                Exit Sub
            End If
        End Using

        StartWork(True)
    End Sub

    Private Sub btnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
        If running Then
            Exit Sub
        End If

        Me.DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub

    Private Sub StartWork(ByVal apply As Boolean)
        applyRun = apply
        running = True
        btnCheck.Enabled = False
        btnApply.Enabled = False
        btnClose.Enabled = False
        ProgressBar1.Value = 0

        If mode = eMode.FLASH_APP Then
            lblState.Text = "Идёт запись прошивки..."
        Else
            lblState.Text = If(apply, "Идёт замена загрузчика...", "Идёт проверка передачей...")
        End If

        thread_work = New Threading.Thread(AddressOf tWork)
        thread_work.Priority = Threading.ThreadPriority.Normal
        thread_work.Name = "bootWork"
        thread_work.Start()
    End Sub

    Private Sub tWork()
        Dim errText As String = ""
        Dim ok As Boolean

        Try
            If mode = eMode.FLASH_APP Then
                ok = clBoot.Upload(bootPort, fName, app, AddressOf onProgress, errText)
            Else
                ok = opros.ReplaceBootloader(image, applyRun, AddressOf onProgress, errText)
            End If
        Catch ex As Exception
            ok = False
            errText = ex.Message
        End Try

        onDone(ok, errText)
    End Sub

    Private Sub onProgress(ByVal percent As Integer)
        Me.BeginInvoke(New delegate_onProgress(AddressOf refProgress), percent)
    End Sub

    Private Sub refProgress(ByVal percent As Integer)
        If percent >= 0 AndAlso percent <= 100 Then
            ProgressBar1.Value = percent
        End If
    End Sub

    Private Sub onDone(ByVal ok As Boolean, ByVal text As String)
        Me.BeginInvoke(New delegate_onDone(AddressOf refDone), ok, text)
    End Sub

    Private Sub refDone(ByVal ok As Boolean, ByVal text As String)
        running = False
        btnClose.Enabled = True

        If Not ok Then
            lblState.Text = "Не выполнено."
            Using f As New frmInfo("Ошибка!", text)
                f.ShowDialog()
            End Using
            btnCheck.Enabled = (mode = eMode.REPLACE_BOOT)
            btnApply.Enabled = True
            Exit Sub
        End If

        If mode = eMode.FLASH_APP Then
            lblState.Text = "Прошивка записана, контроллер её устанавливает."
            Using f As New frmInfo("Готово", "Прошивка записана." & vbCrLf &
                                   "Загрузчик устанавливает её и запускает." & vbCrLf &
                                   "Подключитесь заново примерно через 20 секунд.")
                f.ShowDialog()
            End Using

            Me.DialogResult = DialogResult.OK
            Me.Close()
            Exit Sub
        End If

        If Not applyRun Then
            lblState.Text = "Файл принят и проверен контроллером, замена отменена."
            Using f As New frmInfo("Проверка", "Контроллер принял и проверил файл загрузчика." & vbCrLf &
                                   "На плате ничего не изменилось.")
                f.ShowDialog()
            End Using
            btnCheck.Enabled = True
            btnApply.Enabled = True
            Exit Sub
        End If

        lblState.Text = "Загрузчик заменён, контроллер перезагружается."
        Using f As New frmInfo("Готово", "Контроллер перезаписывает загрузчик и перезагружается." & vbCrLf &
                               "Через 15 секунд подключитесь заново и проверьте" & vbCrLf &
                               "версию загрузчика: она должна стать 50.")
            f.ShowDialog()
        End Using

        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub

End Class
