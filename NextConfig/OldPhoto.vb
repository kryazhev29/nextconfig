Imports System.IO
Imports System.Threading
Imports System.Diagnostics

' SLGT поток: несколько кадров за одно чтение и фрагменты одного кадра.
Public Class OldPhotoWire
    Implements IPhotoWire
    Private ReadOnly port As ClComPort
    Private ReadOnly pending As New List(Of Byte)
    Private ReadOnly frames As New Queue(Of Byte())

    Public Sub New(value As ClComPort)
        port = value
        port.EnableEventReceived = False
        port.Port.DiscardInBuffer()
    End Sub

    Public Sub Send(cmd As Byte, payload As Byte(), sequence As Byte) Implements IPhotoWire.Send
        If cmd <> &H30 Then Throw New InvalidOperationException("Канал предназначен только для фото.")
        Dim frame = clBoot.Frame(cmd, payload)
        port.Port.Write(frame, 0, frame.Length)
        AgentLog.OldPhotoFrame(port.Port.PortName, True, frame)
    End Sub

    Public Shared Function Decode(pending As List(Of Byte), bytes As Byte()) As List(Of Byte())
        pending.AddRange(bytes)
        Dim result As New List(Of Byte())
        While pending.Count >= 8
            If pending(0) <> 83 OrElse pending(1) <> 76 OrElse pending(2) <> 71 OrElse pending(3) <> 84 Then
                pending.RemoveAt(0) : Continue While
            End If
            Dim size = CInt(pending(4)) + 256 * CInt(pending(5))
            If size < 2 OrElse size > 4096 Then
                pending.RemoveAt(0) : Continue While
            End If
            If pending.Count < size + 8 Then Exit While
            Dim body = pending.Skip(6).Take(size).ToArray()
            Dim crc = CInt(pending(size + 6)) + 256 * CInt(pending(size + 7))
            pending.RemoveRange(0, size + 8)
            If crc <> clBoot.Crc16(body) Then Throw New InvalidDataException("Повреждён кадр фото (CRC). Повторите снимок.")
            If body(0) = &H55 AndAlso body(1) = &H30 Then result.Add(body.Skip(2).ToArray())
        End While
        Return result
    End Function

    Public Function Receive(timeout As Integer, cancel As CancellationToken) As Byte() Implements IPhotoWire.Receive
        Dim clock = Stopwatch.StartNew()
        Do
            cancel.ThrowIfCancellationRequested()
            If frames.Count > 0 Then Return frames.Dequeue()
            Dim count = port.Port.BytesToRead
            If count > 0 Then
                Dim bytes(count - 1) As Byte
                Dim got = port.Port.Read(bytes, 0, bytes.Length)
                For Each frame In Decode(pending, bytes.Take(got).ToArray())
                    AgentLog.OldPhotoFrame(port.Port.PortName, False, frame)
                    frames.Enqueue(frame)
                Next
            Else
                Thread.Sleep(5)
            End If
        Loop While clock.ElapsedMilliseconds < timeout
        Return Nothing
    End Function
End Class

Public Class OldPhotoSession
    ' Код 250: прошивка v1–v3 так отвечает при выключенной фотофиксации,
    ' прошивка v5 - всегда (фото по USB в ней нет).
    Public Shared Function Refused250(enabled As Boolean?) As String
        If Not enabled.HasValue Then Return "Плата отказала в съёмке (код 250): фотофиксация выключена в настройках платы или фото по USB нет в этой прошивке."
        If enabled.Value Then Return "Плата отказала в съёмке (код 250), хотя фотофиксация включена: фото по USB нет в этой прошивке."
        Return "Фотофиксация выключена в настройках платы (код 250). Включите её и повторите снимок."
    End Function

    Public Shared Function ErrorText(code As Byte, started As Boolean) As String
        Select Case code
            Case 250 : Return "Фотофиксация выключена в настройках платы."
            Case 2 : Return "Камера занята. Повторите снимок позже."
            Case 3 : Return If(started, "Камера передала повреждённые данные.", "Камера не подключена или передала повреждённые данные.")
            Case 4 : Return If(started, "Ошибка синхронизации камеры.", "Ошибка параметров или синхронизации камеры.")
            Case 5 : Return "Камера не отвечает или отсутствует подключение."
            Case Else : Return "Ошибка камеры, код " & code & "."
        End Select
    End Function

    ' Снимок целиком в памяти: сохраняет его оператор, если снимок нужен.
    ' enabled - бит фотофиксации из прочитанных настроек, Nothing - настройки не читались
    ' (для съёмки пароль не нужен, и читать их ради неё нельзя).
    ' progress - принято байт, всего байт.
    Public Shared Function Capture(wire As IPhotoWire, camera As Byte, resolution As Byte, cancel As CancellationToken,
                                   Optional enabled As Boolean? = Nothing,
                                   Optional progress As Action(Of Long, Long) = Nothing) As Byte()
        If camera <> 0 OrElse Not New Byte() {1, 3, 5, 7}.Contains(resolution) Then Throw New InvalidDataException("Выберите камеру и разрешение.")
        wire.Send(&H30, New Byte() {0, camera, resolution}, 0)
        Using data As New MemoryStream()
            Dim total As Long = -1
            Dim clock = Stopwatch.StartNew()
            Do
                cancel.ThrowIfCancellationRequested()
                If clock.ElapsedMilliseconds > 120000 Then Throw New TimeoutException("Время приёма фото истекло. Повторите снимок.")
                Dim frame = wire.Receive(15000, cancel)
                If frame Is Nothing Then Throw New TimeoutException("Плата перестала передавать фото. Проверьте подключение и повторите снимок.")
                If frame.Length = 1 Then
                    If frame(0) = 250 Then Throw New IOException(Refused250(enabled))
                    If frame(0) <> 10 Then Throw New IOException(ErrorText(frame(0), total >= 0))
                    If total < 0 OrElse data.Length <> total Then Throw New InvalidDataException("Фото получено не полностью. Файл не сохранён.")
                    Dim jpeg = data.ToArray()
                    If jpeg.Length < 4 OrElse jpeg(0) <> &HFF OrElse jpeg(1) <> &HD8 OrElse jpeg(jpeg.Length - 2) <> &HFF OrElse jpeg.Last() <> &HD9 Then Throw New InvalidDataException("Неверные границы JPEG. Файл не сохранён.")
                    data.Position = 0
                    Using decoded = Image.FromStream(data, False, True)
                        If decoded.Width < 1 Then Throw New InvalidDataException("Неверное изображение JPEG.")
                    End Using
                    Return jpeg
                ElseIf frame.Length = 5 AndAlso frame(0) = 0 Then
                    If total >= 0 Then Throw New InvalidDataException("Повторный заголовок фото.")
                    total = BitConverter.ToUInt32(frame, 1)
                    If total < 4 OrElse total > 8 * 1024 * 1024 Then Throw New InvalidDataException("Недопустимый размер фото.")
                    If progress IsNot Nothing Then progress(0, total)
                ElseIf frame.Length >= 3 AndAlso frame(0) = 1 Then
                    Dim count = CInt(BitConverter.ToUInt16(frame, 1))
                    If total < 0 OrElse count = 0 OrElse count <> frame.Length - 3 OrElse data.Length + count > total Then Throw New InvalidDataException("Нарушена длина или последовательность фото.")
                    data.Write(frame, 3, count)
                    If progress IsNot Nothing Then progress(data.Length, total)
                Else
                    Throw New InvalidDataException("Неизвестный ответ камеры.")
                End If
            Loop
        End Using
    End Function
End Class

Public Class OldPhotoDemoWire
    Implements IPhotoWire
    Private ReadOnly replies As New Queue(Of Byte())
    Private ReadOnly supported As Boolean
    Public Sub New(Optional canCapture As Boolean = True)
        supported = canCapture
    End Sub
    Public Sub Send(cmd As Byte, payload As Byte(), sequence As Byte) Implements IPhotoWire.Send
        If Not supported Then
            replies.Enqueue(New Byte() {250}) : Return
        End If
        Using bitmap As New Bitmap(320, 240), graphics = Drawing.Graphics.FromImage(bitmap), stream As New MemoryStream()
            graphics.Clear(Theme.Surface)
            graphics.DrawString("ДЕМО · Домовой v3", Theme.FontBody, Brushes.Black, 16, 80)
            bitmap.Save(stream, Imaging.ImageFormat.Jpeg)
            Dim jpeg = stream.ToArray()
            replies.Enqueue(New Byte() {0}.Concat(BitConverter.GetBytes(CUInt(jpeg.Length))).ToArray())
            For offset = 0 To jpeg.Length - 1 Step 512
                Dim count = Math.Min(512, jpeg.Length - offset)
                replies.Enqueue(New Byte() {1}.Concat(BitConverter.GetBytes(CUShort(count))).Concat(jpeg.Skip(offset).Take(count)).ToArray())
            Next
            replies.Enqueue(New Byte() {10})
        End Using
    End Sub
    Public Function Receive(timeout As Integer, cancel As CancellationToken) As Byte() Implements IPhotoWire.Receive
        cancel.ThrowIfCancellationRequested()
        Return If(replies.Count = 0, Nothing, replies.Dequeue())
    End Function
End Class
