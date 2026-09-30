Imports System.IO
Imports System.Threading
Imports System.Diagnostics

Public Interface IPhotoWire
    Sub Send(cmd As Byte, payload As Byte(), sequence As Byte)
    Function Receive(timeout As Integer, cancel As CancellationToken) As Byte()
End Interface

' Порт уже открыт и эксклюзивно занят clOpros. Здесь нет SerialPort.Open.
Public Class PhotoSerialWire
    Implements IPhotoWire
    Private ReadOnly comport As ClComPort
    Private ReadOnly decoder As New SldnStream()
    Private ReadOnly ready As New Queue(Of Byte())
    Public Sub New(port As ClComPort)
        comport = port
        comport.EnableEventReceived = False
        comport.Port.DiscardInBuffer()
    End Sub
    Public Sub Send(cmd As Byte, payload As Byte(), sequence As Byte) Implements IPhotoWire.Send
        Dim b = SldnStream.Frame(cmd, payload, sequence)
        comport.Port.Write(b, 0, b.Length)
        AgentLog.PhotoFrame(comport.Port.PortName, True, b)
    End Sub
    Public Function Receive(timeout As Integer, cancel As CancellationToken) As Byte() Implements IPhotoWire.Receive
        Dim watch = Stopwatch.StartNew()
        Do
            cancel.ThrowIfCancellationRequested()
            If ready.Count > 0 Then Return ready.Dequeue()
            Dim count = comport.Port.BytesToRead
            If count > 0 Then
                Dim b(count - 1) As Byte
                Dim got = comport.Port.Read(b, 0, b.Length)
                For Each frame In decoder.Feed(b.Take(got).ToArray())
                    AgentLog.PhotoFrame(comport.Port.PortName, False, frame)
                    ready.Enqueue(frame)
                Next
            Else
                Thread.Sleep(5)
            End If
        Loop While watch.ElapsedMilliseconds < timeout
        Return Nothing
    End Function
End Class

' Ответ 2 на команду 21: плата ещё снимает или готовит память - выгрузку можно повторить.
Public Class PhotoBusyException
    Inherits IOException
    Public Sub New(message As String)
        MyBase.New(message)
    End Sub
End Class

Public Class PhotoProgress
    Public Property Text As String
    Public Property Received As Integer
    Public Property Total As Integer
    Public Property SavedPath As String
End Class

Public NotInheritable Class PhotoSession
    Private ReadOnly wire As IPhotoWire
    Private ReadOnly report As Action(Of PhotoProgress)
    Private ReadOnly statusFrame As Action(Of Byte())
    Private Shared sequenceCounter As Integer
    Private lastStatus As Long
    Private lastPoll As Long
    Private ReadOnly clock As Stopwatch = Stopwatch.StartNew()

    Public Sub New(channel As IPhotoWire, progress As Action(Of PhotoProgress), Optional onStatus As Action(Of Byte()) = Nothing)
        wire = channel : report = progress : statusFrame = onStatus
    End Sub

    Private Sub Tell(message As String, Optional transfer As PhotoTransfer = Nothing)
        If report Is Nothing Then Return
        report(New PhotoProgress With {.Text = message,
            .Received = If(transfer Is Nothing, 0, transfer.Received),
            .Total = If(transfer Is Nothing, 0, transfer.Size),
            .SavedPath = If(transfer Is Nothing OrElse transfer.Saved.Count = 0, Nothing, transfer.Saved.Last())})
    End Sub

    Private Function Send(cmd As Byte, data As Byte()) As Byte
        Dim sequence = CByte((Interlocked.Increment(sequenceCounter) And &H7FFFFFFF) Mod 255 + 1)
        wire.Send(cmd, data, sequence)
        Return sequence
    End Function

    ' NEXT User/user_fun.c:2041-2078: flags UInt16 по смещению 13 полного кадра.
    ' structures.h:213: readyToUpdate — бит 11. Проверяем перед любым фото-действием.
    Private Function ProcessStatus(frame As Byte()) As Boolean
        If frame(8) <> 101 Then Return False
        If frame.Length < 42 Then Throw New InvalidDataException("Короткий статус: операции с фото остановлены.")
        If statusFrame IsNot Nothing Then statusFrame(frame)
        If (BitConverter.ToUInt16(frame, 13) And &H800) <> 0 Then
            Throw New InvalidOperationException("Идёт обновление прошивки. Съёмка и выгрузка фото запрещены.")
        End If
        lastStatus = clock.ElapsedMilliseconds
        Return True
    End Function

    Private Sub CheckStatus(cancel As CancellationToken)
        wire.Send(101, Nothing, 0)
        Dim untilTime = clock.ElapsedMilliseconds + 3000
        Do
            Dim frame = wire.Receive(100, cancel)
            If frame IsNot Nothing AndAlso ProcessStatus(frame) Then Return
        Loop While clock.ElapsedMilliseconds < untilTime
        Throw New IOException("Нет свежего состояния платы. Операции с фото не начаты.")
    End Sub

    Public Sub Capture(address As Byte, cancel As CancellationToken)
        CheckStatus(cancel)
        Dim id = Send(7, New Byte() {address})
        Tell("Запрос снимка: камера " & address & "…")
        Dim untilTime = clock.ElapsedMilliseconds + 4000
        Do
            Dim frame = wire.Receive(100, cancel)
            If frame Is Nothing Then Continue Do
            If ProcessStatus(frame) Then Continue Do
            If frame(7) <> id OrElse frame(8) <> 11 Then Continue Do
            If frame.Length < 13 OrElse frame(9) <> 7 Then Continue Do
            CheckAnswer(frame(10), False)
            Tell("Камера снимает…")
            Return
        Loop While clock.ElapsedMilliseconds < untilTime
        Throw New IOException("Плата не подтвердила съёмку. Повторите запрос после проверки связи.")
    End Sub

    Public Function Download(imei As String, folder As String, cancel As CancellationToken) As PhotoTransfer
        Dim transfer As New PhotoTransfer(imei, folder)
        CheckStatus(cancel)
        Dim id = Send(21, transfer.Cursor(True))
        Dim lastData = clock.ElapsedMilliseconds
        Dim retriesWithoutData As Integer = 0
        Dim rejected As Integer = 0
        Dim lastProgress = clock.ElapsedMilliseconds
        Tell("Запрашиваем очередь фотографий…")
        Do
            cancel.ThrowIfCancellationRequested()
            Dim now = clock.ElapsedMilliseconds
            If now - lastProgress > 15000 Then Throw New IOException("Нет продвижения выгрузки. Принятая часть сохранена; повторите операцию.")
            If now - lastPoll >= 1000 Then
                wire.Send(101, Nothing, 0) : lastPoll = now
            End If
            If now - lastStatus > 5000 Then Throw New IOException("Состояние платы не обновляется. Выгрузка остановлена; принятая часть сохранена.")
            Dim frame = wire.Receive(100, cancel)
            If frame IsNot Nothing Then
                If ProcessStatus(frame) Then Continue Do
                If frame(7) <> id Then Continue Do ' хвост предыдущего потока
                If frame(8) = 11 AndAlso frame.Length >= 13 AndAlso frame(9) = 21 Then
                    If frame(10) = 1 Then
                        rejected += 1
                        If rejected > 2 Then Throw New IOException("Плата повторно отвергла номер/смещение фото.")
                        Tell("Курсор фото устарел. Запрашиваем первое неподтверждённое место (0/0).")
                        id = Send(21, transfer.Cursor(True)) : lastData = now
                    Else
                        CheckAnswer(frame(10), True)
                    End If
                ElseIf frame(8) = 14 Then
                    Dim before = transfer.Received
                    Dim action = transfer.Receive(frame.Skip(9).Take(frame.Length - 11).ToArray())
                    Tell(transfer.Message, transfer)
                    If action = PhotoTransfer.StepKind.Done Then Return transfer
                    If transfer.Received > before Then
                        retriesWithoutData = 0 : lastProgress = now
                    End If
                    If action = PhotoTransfer.StepKind.RequestAgain Then
                        If transfer.Received < transfer.Size Then transfer.Retries += 1
                        retriesWithoutData += 1
                        If retriesWithoutData > 6 Then Throw New IOException("Не удаётся получить пропущенную часть фото. Принятые байты сохранены.")
                        id = Send(21, transfer.Cursor())
                    End If
                    lastData = now
                End If
            End If
            If now - lastData > 2500 Then
                retriesWithoutData += 1 : transfer.Retries += 1
                If retriesWithoutData > 3 Then Throw New IOException("Нет пакетов фото. Повторите снимок.")
                Tell("Нет пакета. Повтор " & retriesWithoutData & " с байта " & transfer.Received & ".", transfer)
                id = Send(21, transfer.Cursor()) : lastData = now
            End If
        Loop
    End Function

    Private Shared Sub CheckAnswer(code As Byte, downloading As Boolean)
        Select Case code
            Case 0 : Return
            Case 1 : Throw New IOException("Камера не настроена: нужен активный RS-485 и периферия C328 (тип 3) с выбранным адресом.")
            Case 2
                If downloading Then Throw New PhotoBusyException("Плата занята съёмкой, обновлением или подготовкой памяти. Повторите выгрузку позже.")
                Throw New IOException("Плата занята или не может начать съёмку. Проверьте камеру и свободную память.")
            Case 4 : Throw New IOException("Требуется ПИН-код. Переподключитесь к плате и введите ПИН.")
            Case Else : Throw New IOException("Плата отказала в операции с фото: код " & code & ".")
        End Select
    End Sub
End Class
