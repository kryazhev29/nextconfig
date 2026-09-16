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

    Private Sub tWriteFile()
        Dim fwInfo As New clOpros.s_fwinfo
        Dim slist As String() = fName.Substring(fName.LastIndexOf("\") + 1).Split(".")

        '[версия HW].[Версия FW].[Билд таймштамп в секундах от 01.01.1970].bin

        If slist.Length <> 4 Then
            running = False
            Exit Sub
        End If

        fwInfo.hard = UInt16.Parse(slist(0))
        fwInfo.version = UInt16.Parse(slist(1))
        fwInfo.build = UInt32.Parse(slist(2))
        fwInfo.size = fSize
        fwInfo.crc = 0

        fwInfo.nBlocks = fSize / 1024
        If (fwInfo.nBlocks * 1024) < fSize Then
            fwInfo.nBlocks += 1
        End If

        fReaded = 0
        running = True

        Try
            'создаем объект BinaryReader
            Using reader As New BinaryReader(File.Open(fName, FileMode.Open))

                ' рассчитываем контрольную сумму
                Dim crc As UInt32 = UInt32.MaxValue
                For i As Integer = 0 To fSize - 1 Step 4
                    crc = Crc32(crc, reader.ReadUInt32())
                Next

                fwInfo.crc = crc

                Dim buf As Byte()
                Dim rez As clOpros.eCmdAnswer = opros.StartUpdate(fwInfo)
                Dim percent As Byte

                'AddHandler Comport.ReceiveBytes, AddressOf onBytesReceive

                ' если без ошибок запустили процесс
                If rez = clOpros.eCmdAnswer.CMD_SUCCESS Then
                    nBlock = 0
                    reader.BaseStream.Seek(0, SeekOrigin.Begin)
                    While Not break
                        buf = reader.ReadBytes(1024)
                        fReaded += buf.Length
                        'fw_onRead(fReaded)
                        If opros.WriteFWBlock(nBlock, buf, percent) = clOpros.eCmdAnswer.CMD_SUCCESS Then
                            nBlock += 1
                        End If

                        Select Case percent
                            Case 248
                                Using f As New frmInfo("Ошибка!", "Обновление завершено!")
                                    f.ShowDialog()
                                End Using
                                Me.DialogResult = DialogResult.OK
                                Me.Close()
                            Case 252
                                Using f As New frmInfo("Ошибка!", "Не верная контрольная сумма!")
                                    f.ShowDialog()
                                End Using
                            Case Else
                                fw_onRead(fReaded, percent)
                        End Select

                        If buf.Length <> 1024 Then
                            Exit While
                        End If

                        'If getBlock Then
                        '    reader.BaseStream.Seek(nBlock * 1024, SeekOrigin.Begin)
                        '    buf = reader.ReadBytes(1024)
                        '    fReaded += buf.Length
                        '    fw_onRead(fReaded)
                        '    opros.WriteFWBlock(nBlock, buf)

                        '    If buf.Length <> 1024 Then
                        '        Exit While
                        '    End If
                        '    getBlock = False
                        'End If

                        Threading.Thread.Sleep(10)
                    End While
                Else
                    Dim str As String = ""

                    Select Case rez
                        Case clOpros.eCmdAnswer.CMD_PARAMERROR
                            str = "Ошибка в параметре!"
                        Case clOpros.eCmdAnswer.CMD_RUNERROR
                            str = "Ошибка выполнения!"
                        Case clOpros.eCmdAnswer.CMD_UNDEFRESULT
                            str = "Не верный результат!"
                        Case clOpros.eCmdAnswer.CMD_GETPASSWORD
                            str = "Требуется ввод пароля!"
                        Case clOpros.eCmdAnswer.CMD_RUNNING
                            str = "Обновление уже выполняется!"
                        Case clOpros.eCmdAnswer.CMD_CRCERROR
                            str = "Ошибка в контрольной сумме!"
                        Case clOpros.eCmdAnswer.CMD_UNDEFINE
                            str = "Команда не поддерживается!"
                        Case clOpros.eCmdAnswer.CMD_GOTOBOOLOADER
                            str = "Обновление завершено!"
                    End Select

                    Using f As New frmInfo("Ошибка!", str)
                        f.ShowDialog()
                    End Using

                    If rez = clOpros.eCmdAnswer.CMD_GOTOBOOLOADER Then
                        Me.DialogResult = DialogResult.OK
                        Me.Close()
                    End If
                End If
            End Using
        Catch ex As Exception
            Using f As New frmInfo("Ошибка!", ex.Message)
                f.ShowDialog()
            End Using
        End Try

        'RemoveHandler Comport.ReceiveBytes, AddressOf onBytesReceive
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
        ProgressBar1.Value = percent
    End Sub

    Private Function CRC32(ByVal crc As UInt32, ByVal data As UInt32) As UInt32
        crc = crc Xor data

        For i As Integer = 0 To 31
            If crc And &H80000000 Then
                crc = (crc << 1) Xor &H4C11DB7
            Else
                crc <<= 1
            End If
        Next

        Return crc
    End Function

End Class