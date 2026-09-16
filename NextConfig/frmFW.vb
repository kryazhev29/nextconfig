Imports System.Globalization
Imports System.IO

Public Class frmFW

    Private fName As String
    Private fSize As Integer
    Private fReaded As Integer
    Private opros As clOpros
    Private break As Boolean = False
    Private running As Boolean = False
    Private thread_wrFile As Threading.Thread

    Private WithEvents Comport As ClComPort
    Private getBlock As Boolean = False
    Private nBlock As Integer

    ' метка платы в образе: вектор 8, 0x4857 и номер HW в младшем байте
    Private Const HW_MARKER_OFFSET As Integer = &H20
    Private Const HW_MARKER_TAG As UInt32 = &H48570000UI
    Private Const MAX_BLOCK_TRIES As Integer = 5

    Private Delegate Sub delegate_onReaded(ByVal readed As Integer, ByVal percent As Byte)

    Public Sub New(ByVal _file As String, ByVal _opros As clOpros)

        ' Этот вызов является обязательным для конструктора.
        InitializeComponent()

        ' Добавить код инициализации после вызова InitializeComponent()
        fName = _file
        opros = _opros
        Label5.Text = fName
        Label1.Text = "0/0"
        Comport = opros.GetComPort
    End Sub

    Private Sub Form3_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Dim fi As New FileInfo(fName)

        fSize = fi.Length
        Label1.Text = "0/" & fSize
    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click

        thread_wrFile = New Threading.Thread(AddressOf tWriteFile)
        thread_wrFile.Priority = Threading.ThreadPriority.Normal
        thread_wrFile.Name = "writeFile"
        thread_wrFile.Start()

    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        break = True
        While running
            Threading.Thread.Sleep(100)
        End While

        Me.DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub

    ' Образ дополняется байтами 0xFF до кратного 4: контроллер считает контрольную
    ' сумму по 4-байтным словам и отвергает размер, не кратный 4.
    Private Shared Function PadImage(ByVal data As Byte()) As Byte()
        Dim tail As Integer = data.Length Mod 4

        If tail = 0 Then
            Return data
        End If

        Dim padded(data.Length + (4 - tail) - 1) As Byte

        For i As Integer = 0 To padded.Length - 1
            padded(i) = If(i < data.Length, data(i), CByte(&HFF))
        Next

        Return padded
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

    ' Контрольная сумма как у аппаратного CRC контроллера: по 4-байтным словам.
    Private Shared Function ImageCrc(ByVal image As Byte()) As UInt32
        Return clOpros.Crc32Stm(image)
    End Function

    ' Метка платы в зарезервированном векторе 8 образа: 0x4857 и номер HW в младшем байте.
    ' Образы, собранные до появления метки, её не имеют — возвращаем -1.
    Private Shared Function BoardMarker(ByVal image As Byte()) As Integer
        If image.Length < HW_MARKER_OFFSET + 4 Then
            Return -1
        End If

        Dim mark As UInt32 = BitConverter.ToUInt32(image, HW_MARKER_OFFSET)

        If (mark And &HFFFFFF00UI) <> HW_MARKER_TAG Then
            Return -1
        End If

        Return CInt(mark And &HFFUI)
    End Function

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

    Private Sub CloseOk()
        Me.BeginInvoke(New Action(Sub()
                                      Me.DialogResult = DialogResult.OK
                                      Me.Close()
                                  End Sub))
    End Sub

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

    Private Sub tWriteFile()
        Dim fwInfo As New clOpros.s_fwinfo
        Dim slist As String() = fName.Substring(fName.LastIndexOf("\") + 1).Split(".")

        '[версия HW].[Версия FW].[Билд таймштамп в секундах от 01.01.1970].bin

        If slist.Length <> 4 Then
            ShowMsg("Ошибка!", "Имя файла должно быть вида HW.FW.БИЛД.bin")
            running = False
            Exit Sub
        End If

        running = True

        Try
            fwInfo.hard = UInt16.Parse(slist(0))
            fwInfo.version = UInt16.Parse(slist(1))
            fwInfo.build = UInt32.Parse(slist(2))

            Dim image As Byte() = PadImage(File.ReadAllBytes(fName))
            Dim board As Integer = BoardMarker(image)

            ' Контроллер проверит метку сам и ответит кодом 250, но лучше не начинать.
            If board >= 0 AndAlso board <> fwInfo.hard Then
                ShowMsg("Ошибка!", "Образ собран для платы " & board & ", а в имени файла указана " & fwInfo.hard & "!")
                running = False
                Exit Sub
            End If

            fSize = image.Length
            fwInfo.size = CUInt(image.Length)
            fwInfo.crc = ImageCrc(image)
            fwInfo.nBlocks = CUShort((image.Length + 1023) \ 1024)

            fReaded = 0

            Dim rez As clOpros.eCmdAnswer = opros.StartUpdate(fwInfo)

            ' 0 отвечает контроллер по USB, 5 — по каналу сервера; и то и другое значит
            ' «готов принять прошивку».
            If rez <> clOpros.eCmdAnswer.CMD_SUCCESS AndAlso rez <> clOpros.eCmdAnswer.CMD_READYTOUPDATE Then
                ShowMsg("Ошибка!", StartAnswerText(rez))
                running = False
                Exit Sub
            End If

            Dim percent As Byte
            Dim tries As Integer = 0
            Dim done As Boolean = False

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
                        ShowMsg("Обновление", "Обновление завершено!")
                        done = True
                        Exit While
                    Case 249, 250, 251, 252, 253, 254
                        ShowMsg("Ошибка!", ProgressText(percent))
                        Exit While
                    Case 255
                        ' блок отклонён, обновление продолжается — повторяем тот же блок
                    Case Else
                        fw_onRead(fReaded, percent)
                End Select

                If tries >= MAX_BLOCK_TRIES Then
                    ShowMsg("Ошибка!", "Блок " & nBlock & " не принят " & MAX_BLOCK_TRIES & " раз подряд. Обновление прервано.")
                    Exit While
                End If

                Threading.Thread.Sleep(10)
            End While

            If done Then
                CloseOk()
            End If

        Catch ex As Exception
            ShowMsg("Ошибка!", ex.Message)
        End Try

        running = False
    End Sub

    Private Sub onBytesReceive(ByVal buffer As Byte())
        If buffer(8) = clOpros.eCmd.REC_GETUPDBLOCK Then
            nBlock = BitConverter.ToUInt16(buffer, 9)
            getBlock = True
        End If
    End Sub

    Private Sub fw_onRead(ByVal readed As Integer, ByVal percent As Byte)
        Dim UIUpdater As New delegate_onReaded(AddressOf refOnReaded)
        Me.BeginInvoke(UIUpdater, readed, percent)
    End Sub

    Private Sub refOnReaded(ByVal readed As Integer, ByVal percent As Byte)
        Label1.Text = readed & "/" & fSize
        'ProgressBar1.Value = (readed * 100) / fSize
        If percent <= 100 Then
            ProgressBar1.Value = percent
        End If
    End Sub

End Class