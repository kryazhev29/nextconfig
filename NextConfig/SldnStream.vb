Imports System.IO

' NEXT User/photo.c:564-585: CRC по длине и телу, произвольные границы USB.
' Общий CRC берётся из ClComPort; поток фото нельзя читать через SendRecCmd:
' тот возвращает только первый пакет и очищает входной буфер перед запросом.
Public Class SldnStream
    Private ReadOnly pending As New List(Of Byte)
    Public Property BadFrames As Integer

    ' Эти три старые команды возвращают номер 0, остальные отражают номер запроса.
    ' Текущий ответ: код/время/буфер (18 Б); прежний добавляет номер команды (19 Б).
    Public Shared Function IsAnswer(reply As Byte(), command As Byte) As Boolean
        If reply Is Nothing OrElse reply.Length < 9 OrElse reply(8) <> 11 Then Return False
        Return reply.Length = 18 OrElse (reply.Length = 19 AndAlso reply(9) = command)
    End Function

    Public Shared Function AnswerCode(reply As Byte()) As Byte
        If reply Is Nothing OrElse (reply.Length <> 18 AndAlso reply.Length <> 19) OrElse reply(8) <> 11 Then Throw New InvalidDataException("Длина ответа 11")
        Return reply(reply.Length - 9)
    End Function

    Public Shared Function MatchesReply(request As Byte(), reply As Byte()) As Boolean
        If request Is Nothing OrElse request.Length < 9 OrElse reply Is Nothing OrElse reply.Length < 11 Then Return False
        Dim cmd = request(8)
        Dim sequence = If(reply(8) <> 11 AndAlso (cmd = 100 OrElse cmd = 101 OrElse cmd = 106), CByte(0), request(7))
        If reply(7) <> sequence Then Return False
        If reply(8) = 11 Then Return IsAnswer(reply, cmd)
        If cmd = 20 Then Return reply(8) = 4 AndAlso reply.Length >= 13
        If reply(8) <> cmd Then Return False
        Select Case cmd
            Case 2, 3, 8, 19, 23
                Return False ' только подтверждение 11
            Case 12, 102
                Return reply.Length >= 12
            Case 106
                Return reply.Length >= 523
            Case Else
                Return True
        End Select
    End Function

    Public Shared Function Command(cmd As Byte, payload As Byte(), Optional sequence As Byte = 0) As Byte()
        Dim n = If(payload Is Nothing, 0, payload.Length)
        Dim b(8 + n) As Byte
        Array.Copy(Text.Encoding.ASCII.GetBytes("SLDN"), b, 4)
        b(4) = CByte((n + 3) And 255) : b(5) = CByte((n + 3) >> 8)
        b(6) = 255 : b(7) = sequence : b(8) = cmd
        If n > 0 Then Array.Copy(payload, 0, b, 9, n)
        Return b
    End Function

    Public Shared Function Frame(cmd As Byte, payload As Byte(), Optional sequence As Byte = 0) As Byte()
        Dim b = Command(cmd, payload, sequence)
        Dim crc = ClComPort.Crc16(b, 4, b.Length - 4)
        Return b.Concat(New Byte() {CByte(crc And 255), CByte(crc >> 8)}).ToArray()
    End Function

    Public Shared Function ReadReply(stream As Stream, request As Byte(), timeout As Integer) As Byte()
        Dim decoder As New SldnStream()
        Dim watch = Diagnostics.Stopwatch.StartNew()
        Dim buffer(1023) As Byte
        While watch.ElapsedMilliseconds < timeout
            If stream.CanTimeout Then stream.ReadTimeout = Math.Max(1, timeout - CInt(watch.ElapsedMilliseconds))
            Dim count = stream.Read(buffer, 0, buffer.Length)
            If count = 0 Then Throw New EndOfStreamException("Сервер закрыл соединение до получения ответа.")
            For Each reply In decoder.Feed(buffer.Take(count).ToArray())
                If MatchesReply(request, reply) AndAlso reply(8) = request(8) AndAlso reply.Length >= 12 Then
                    If request(8) = 17 AndAlso reply(9) = 3 AndAlso reply.Length < 292 Then Continue For
                    Return reply
                End If
            Next
        End While
        Throw New TimeoutException("Нет полного ответа от сервера.")
    End Function

    Public Shared Function QueryServer(host As String, port As Integer, cmd As Byte, imei As String) As Byte()
        Dim payload = Text.Encoding.ASCII.GetBytes(imei)
        If payload.Length <> 15 OrElse imei.Any(Function(c) c < "0"c OrElse c > "9"c) Then Throw New ArgumentException("Некорректный IMEI")
        Using client As New Net.Sockets.TcpClient()
            Dim connect = client.ConnectAsync(host, port)
            If Not connect.Wait(2000) Then Throw New TimeoutException("Сервер не ответил на подключение.")
            Dim request = Frame(cmd, payload)
            Using stream = client.GetStream()
                stream.WriteTimeout = 2000
                stream.Write(request, 0, request.Length)
                Return ReadReply(stream, request, 2000)
            End Using
        End Using
    End Function

    Public Function Feed(bytes As Byte()) As List(Of Byte())
        pending.AddRange(bytes)
        Dim frames As New List(Of Byte())
        While pending.Count >= 6
            If pending(0) <> 83 OrElse pending(1) <> 76 OrElse pending(2) <> 68 OrElse pending(3) <> 78 Then
                pending.RemoveAt(0) : Continue While
            End If
            Dim n = CInt(pending(4)) Or (CInt(pending(5)) << 8)
            If n < 3 OrElse n > 2040 Then
                BadFrames += 1 : pending.RemoveAt(0) : Continue While
            End If
            If pending.Count < n + 8 Then Exit While
            Dim b = pending.Take(n + 8).ToArray()
            If ClComPort.Crc16(b, 4, n + 2) <> BitConverter.ToUInt16(b, n + 6) Then
                BadFrames += 1 : pending.RemoveAt(0) : Continue While
            End If
            pending.RemoveRange(0, b.Length)
            frames.Add(b)
        End While
        Return frames
    End Function
End Class
