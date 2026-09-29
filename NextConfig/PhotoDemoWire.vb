Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Threading

' Модель очереди NEXT: подтверждённое смещение никогда не уменьшается.
' User/photo.c:495-593. Ни один экземпляр модели не открывает COM-порт.
Public Class PhotoDemoWire
    Implements IPhotoWire
    Private Class Item
        Public Number As UInteger
        Public Bytes As Byte()
        Public Confirmed As Integer
    End Class
    Private ReadOnly photos As New List(Of Item)
    Private ReadOnly incoming As New Queue(Of Byte())
    Private ReadOnly decoder As New SldnStream()
    Private ReadOnly mode As String
    Private lost As Boolean
    Private duplicated As Boolean
    Private nextNumber As UInteger = 481
    Public ReadOnly Commands As New List(Of Byte)
    Public ReadOnly Acknowledgements As New List(Of Integer)
    Public Shared ReadOnly Property Scenario As String
        Get
            Dim arg = Environment.GetCommandLineArgs().FirstOrDefault(Function(a) a.StartsWith("--demo-photo="))
            Return If(arg Is Nothing, "loss", arg.Substring(13))
        End Get
    End Property

    Public Sub New(Optional scenario As String = "loss")
        mode = scenario
        If mode <> "empty" Then
            AddPhoto() : AddPhoto()
        End If
    End Sub

    Public Shared Function Jpeg(number As UInteger) As Byte()
        Using bitmap As New Bitmap(640, 480), g = Graphics.FromImage(bitmap)
            g.Clear(Color.FromArgb(173, 201, 221))
            g.FillRectangle(Brushes.DarkSeaGreen, 0, 190, 640, 290)
            g.FillPolygon(Brushes.DimGray, {New Point(200, 480), New Point(280, 210), New Point(360, 210), New Point(510, 480)})
            g.FillRectangle(Brushes.LightGray, 30, 125, 180, 140)
            g.FillRectangle(Brushes.SlateGray, 70, 170, 75, 95)
            Using pen As New Pen(Color.WhiteSmoke, 8)
                g.DrawLine(pen, 190, 290, 510, 290)
            End Using
            Using font As New Font("Arial", 18, FontStyle.Bold)
                g.DrawString("DEMO / C328 / " & number, font, Brushes.Black, 20, 20)
                g.DrawString("NAVISET  29.09.2026  14:32", font, Brushes.White, 20, 430)
            End Using
            Using stream As New MemoryStream()
                bitmap.Save(stream, ImageFormat.Jpeg)
                Return stream.ToArray()
            End Using
        End Using
    End Function

    Private Sub AddPhoto()
        Dim bytes = Jpeg(nextNumber)
        If mode = "bad-jpeg" Then bytes(bytes.Length - 1) = 0
        photos.Add(New Item With {.Number = nextNumber, .Bytes = bytes,
            .Confirmed = If(mode = "missing-prefix", 512, 0)})
        nextNumber += 1UI
    End Sub

    Private Sub Queue(cmd As Byte, data As Byte(), id As Byte)
        Dim frame = SldnStream.Command(cmd, data, id)
        frame(6) = 60
        Dim crc = ClComPort.Crc16(frame, 4, frame.Length - 4)
        frame = frame.Concat({CByte(crc And 255), CByte(crc >> 8)}).ToArray()
        ' Произвольные границы USB, включая разрыв заголовка и CRC.
        For pos = 0 To frame.Length - 1 Step 37
            For Each decoded In decoder.Feed(frame.Skip(pos).Take(37).ToArray())
                incoming.Enqueue(decoded)
            Next
        Next
    End Sub

    Private Sub Answer(cmd As Byte, code As Byte, id As Byte)
        Queue(11, {cmd, code, 0, 0, 0, 0, 0, 0}, id)
    End Sub

    Public Sub Send(cmd As Byte, payload As Byte(), sequence As Byte) Implements IPhotoWire.Send
        Commands.Add(cmd)
        Select Case cmd
            Case 101
                Dim s(30) As Byte
                Array.Copy(BitConverter.GetBytes(CUInt((DateTime.UtcNow - New DateTime(1970, 1, 1)).TotalSeconds)), s, 4)
                If mode = "updating" Then s(5) = 8
                s(6) = 71 : Array.Copy(BitConverter.GetBytes(1789482488UI), 0, s, 7, 4)
                s(11) = 50 : s(12) = 139 : s(13) = 27
                s(16) = 1 : s(17) = 1 : s(18) = 1 : s(19) = 4 : s(20) = 1
                s(28) = 4 : s(30) = 3
                Queue(101, s, 0)
            Case 7
                If mode = "no-camera" Then
                    Answer(cmd, 1, sequence)
                Else
                    AddPhoto() : Answer(cmd, 0, sequence)
                End If
            Case 21
                Dim number = If(payload Is Nothing, 0UI, BitConverter.ToUInt32(payload, 0))
                Dim offset = If(payload Is Nothing, 0, CInt(BitConverter.ToUInt16(payload, 4)))
                Dim pending = photos.Where(Function(p) p.Confirmed < p.Bytes.Length).ToList()
                Dim target = If(number = 0, pending.FirstOrDefault(), pending.FirstOrDefault(Function(p) p.Number = number))
                If pending.Count > 0 AndAlso (target Is Nothing OrElse offset > target.Bytes.Length) Then
                    Answer(cmd, 1, sequence) : Return
                End If
                If target IsNot Nothing Then
                    For Each p In pending
                        If p Is target Then Exit For
                        p.Confirmed = p.Bytes.Length
                    Next
                    target.Confirmed = Math.Max(target.Confirmed, offset)
                    Acknowledgements.Add(offset)
                End If
                Answer(cmd, 0, sequence)
                pending = photos.Where(Function(p) p.Confirmed < p.Bytes.Length).ToList()
                For Each p In pending
                    For pos = p.Confirmed To p.Bytes.Length - 1 Step 512
                        Dim chunk = p.Bytes.Skip(pos).Take(512).ToArray()
                        Dim data = New Byte() {CByte(pending.Count)}.Concat(BitConverter.GetBytes(p.Number)).Concat(
                            BitConverter.GetBytes(CUInt(p.Bytes.Length))).Concat(BitConverter.GetBytes(CUShort(pos))).Concat(chunk).ToArray()
                        If mode = "loss" AndAlso Not lost AndAlso pos = 512 Then
                            lost = True : Continue For
                        End If
                        Queue(14, data, sequence)
                        If Not duplicated AndAlso pos = 0 Then
                            Queue(14, data, sequence) : duplicated = True
                        End If
                    Next
                Next
                Queue(14, {0}, sequence)
            Case Else
                Throw New InvalidOperationException("Неожиданная команда фото-демо: " & cmd)
        End Select
    End Sub

    Public Function Receive(timeout As Integer, cancel As CancellationToken) As Byte() Implements IPhotoWire.Receive
        cancel.ThrowIfCancellationRequested()
        If incoming.Count > 0 Then Return incoming.Dequeue()
        Thread.Sleep(Math.Min(timeout, 10))
        Return Nothing
    End Function
End Class
