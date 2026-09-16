Imports System.IO

' Замена загрузчика контроллера (краткое ТЗ, разделы 8 и 9). Файл загрузчика
' выбирается пользователем по фотографии платы: контроллер под старым загрузчиком
' свою версию платы не знает.
Public Class frmBoot

    Private ReadOnly fName As String
    Private ReadOnly opros As clOpros
    Private ReadOnly boardHw As Byte

    Private image As Byte()
    Private fileHw As Byte
    Private running As Boolean = False
    Private applyRun As Boolean = False
    Private thread_replace As Threading.Thread

    Private Delegate Sub delegate_onProgress(ByVal percent As Integer)
    Private Delegate Sub delegate_onDone(ByVal ok As Boolean, ByVal text As String)

    ' boardHw — номер платы из статуса контроллера, 0 если он его не знает
    Public Sub New(ByVal _file As String, ByVal _opros As clOpros, ByVal _boardHw As Byte)

        ' Этот вызов является обязательным для конструктора.
        InitializeComponent()

        fName = _file
        opros = _opros
        boardHw = _boardHw
        lblFile.Text = fName
    End Sub

    Private Sub frmBoot_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Dim errText As String = ""

        btnApply.Enabled = False
        btnCheck.Enabled = False

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

    ' Замена. Двойное подтверждение по п. 9.6 краткого ТЗ.
    Private Sub btnApply_Click(sender As Object, e As EventArgs) Handles btnApply.Click

        Using f As New frmInfo("Внимание", "Заменить загрузчик контроллера на файл" & vbCrLf &
                               IO.Path.GetFileName(fName) & "?" & vbCrLf &
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
        lblState.Text = If(apply, "Идёт замена загрузчика...", "Идёт проверка передачей...")

        thread_replace = New Threading.Thread(AddressOf tReplace)
        thread_replace.Priority = Threading.ThreadPriority.Normal
        thread_replace.Name = "replaceBoot"
        thread_replace.Start()
    End Sub

    Private Sub tReplace()
        Dim errText As String = ""
        Dim ok As Boolean

        Try
            ok = opros.ReplaceBootloader(image, applyRun, AddressOf onProgress, errText)
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
            btnCheck.Enabled = True
            btnApply.Enabled = True
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
