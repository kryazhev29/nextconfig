Imports System.IO

' NEXT User/photo.c:564-585: CRC по длине и телу, произвольные границы USB.
' Общий CRC берётся из ClComPort; поток фото нельзя читать через SendRecCmd:
' тот возвращает только первый пакет и очищает входной буфер перед запросом.
Public Class SldnStream
    Private ReadOnly pending As New List(Of Byte)
    Public Property BadFrames As Integer

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
