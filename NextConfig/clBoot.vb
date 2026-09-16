Imports System.IO
Imports System.IO.Ports
Imports System.Threading

' Работа с платой, стоящей в загрузчике 0x32 (краткое ТЗ, раздел 8): загрузчик
' отвечает по USB своим идентификатором, программа предлагает выбрать файл
' прошивки и записывает его. Протокол загрузчика (Bootloader/boot_core.c):
' кадр "SLGT" + длина 2 + тело + CRC16 тела; тело запроса [0xAA, команда, данные],
' тело ответа [0x55, команда, статус, версия, HW, ...] — всего 10 байт.
' Команды: 0x00 идентификация, 0x20 начать приём, 0x21 данные, 0x22 установить.
Public Class clBoot

    Public Const CMD_INFO As Byte = &H0
    Public Const CMD_BEGIN As Byte = &H20
    Public Const CMD_DATA As Byte = &H21
    Public Const CMD_COMMIT As Byte = &H22

    Public Const CHUNK As Integer = 256
    Public Const MAX_CODE As Integer = &H17E00     ' размер области прошивки
    Public Const APP_BASE As UInt32 = &H8008200UI  ' вектора приложения
    Public Const HW_MARKER_OFFSET As Integer = &H20
    Public Const HW_MARKER_TAG As UInt32 = &H48570000UI

    ' сведения о прошивке из имени файла HW.FW.БИЛД.bin
    Public Structure s_appfile
        Dim hard As Byte
        Dim version As UInt16
        Dim build As UInt32
        Dim image As Byte()
    End Structure

    Private Shared Function Crc16(ByVal data As Byte()) As Integer
        Dim crc As Integer = &HFFFF

        For i As Integer = 0 To data.GetUpperBound(0)
            crc = crc Xor data(i)

            For j As Integer = 0 To 7
                If (crc And 1) <> 0 Then
                    crc = (crc >> 1) Xor &HA001
                Else
                    crc >>= 1
                End If
            Next
        Next

        Return crc
    End Function

    Private Shared Function Frame(ByVal cmd As Byte, ByVal data As Byte()) As Byte()
        Dim bodyLen As Integer = 2 + If(data Is Nothing, 0, data.Length)
        Dim body(bodyLen - 1) As Byte

        body(0) = &HAA
        body(1) = cmd

        If data IsNot Nothing Then
            Array.Copy(data, 0, body, 2, data.Length)
        End If

        Dim crc As Integer = Crc16(body)
        Dim frm(bodyLen + 7) As Byte

        frm(0) = Asc("S")
        frm(1) = Asc("L")
        frm(2) = Asc("G")
        frm(3) = Asc("T")
        frm(4) = bodyLen And &HFF
        frm(5) = (bodyLen >> 8) And &HFF

        Array.Copy(body, 0, frm, 6, bodyLen)

        frm(bodyLen + 6) = crc And &HFF
        frm(bodyLen + 7) = (crc >> 8) And &HFF

        Return frm
    End Function

    ' Одна команда загрузчику. Возвращает тело ответа или Nothing.
    Private Shared Function Exchange(ByVal comport As ClComPort,
                                     ByVal cmd As Byte,
                                     ByVal data As Byte(),
                                     ByVal timeout As Integer,
                                     ByRef errText As String) As Byte()

        ' иначе обработчик события приёма заберёт ответ загрузчика себе
        comport.EnableEventReceived = False

        Dim port As SerialPort = comport.Port
        Dim frm As Byte() = Frame(cmd, data)
        Dim pending As New List(Of Byte)
        Dim tick As Integer = Environment.TickCount

        errText = ""

        Try
            port.DiscardInBuffer()
            port.DiscardOutBuffer()
            port.Write(frm, 0, frm.Length)
        Catch ex As Exception
            errText = ex.Message
            Return Nothing
        End Try

        While (Environment.TickCount - tick) < timeout

            Try
                Dim ready As Integer = port.BytesToRead

                If ready > 0 Then
                    Dim buf(ready - 1) As Byte
                    port.Read(buf, 0, ready)
                    pending.AddRange(buf)
                End If
            Catch ex As Exception
                errText = ex.Message
                Return Nothing
            End Try

            While pending.Count >= 8
                ' ищем начало кадра
                If pending(0) <> Asc("S") OrElse pending(1) <> Asc("L") OrElse
                   pending(2) <> Asc("G") OrElse pending(3) <> Asc("T") Then
                    pending.RemoveAt(0)
                    Continue While
                End If

                Dim size As Integer = pending(4) + (CInt(pending(5)) << 8)

                If size < 2 OrElse size > 592 Then
                    pending.RemoveAt(0)
                    Continue While
                End If

                If pending.Count < size + 8 Then
                    Exit While
                End If

                Dim body(size - 1) As Byte
                pending.CopyTo(6, body, 0, size)

                Dim got As Integer = pending(6 + size) + (CInt(pending(7 + size)) << 8)
                pending.RemoveRange(0, size + 8)

                If got <> Crc16(body) OrElse body(0) <> &H55 OrElse body(1) <> cmd Then
                    Continue While
                End If

                If body.Length <> 10 Then
                    errText = "Загрузчик ответил кадром неизвестного вида."
                    Return Nothing
                End If

                If body(2) = 7 Then
                    errText = "Микроконтроллер платы сообщает другой объём памяти: этот загрузчик ему не подходит."
                    Return Nothing
                End If

                If body(2) <> 0 Then
                    errText = "Загрузчик отказал, код " & body(2) & "."
                    Return Nothing
                End If

                Return body
            End While

            Thread.Sleep(5)
        End While

        errText = "Загрузчик не ответил на команду 0x" & cmd.ToString("X2") & "."
        Return Nothing
    End Function

    ' Есть ли на порту загрузчик. Версия и номер платы берутся из ответа.
    Public Shared Function Info(ByVal comport As ClComPort, ByRef bootVer As Byte, ByRef hw As Byte) As Boolean
        Dim errText As String = ""
        Dim body As Byte() = Exchange(comport, CMD_INFO, Nothing, 700, errText)

        If body Is Nothing Then
            Return False
        End If

        bootVer = body(3)
        hw = body(4)

        Return True
    End Function

    ' Разбор файла прошивки: имя, размер, вектора и метка платы в образе.
    Public Shared Function CheckAppFile(ByVal fileName As String, ByRef app As s_appfile, ByRef errText As String) As Boolean
        Dim slist As String() = Path.GetFileName(fileName).Split(".")

        errText = ""

        If slist.Length <> 4 Then
            errText = "Имя файла должно быть вида HW.FW.БИЛД.bin"
            Return False
        End If

        Try
            app.hard = Byte.Parse(slist(0))
            app.version = UInt16.Parse(slist(1))
            app.build = UInt32.Parse(slist(2))
        Catch ex As Exception
            errText = "Имя файла должно быть вида HW.FW.БИЛД.bin"
            Return False
        End Try

        Dim data As Byte()

        Try
            data = File.ReadAllBytes(fileName)
        Catch ex As Exception
            errText = ex.Message
            Return False
        End Try

        ' образ дополняется 0xFF до кратного 4: контрольная сумма считается словами
        If (data.Length Mod 4) <> 0 Then
            Dim padded(data.Length + (4 - (data.Length Mod 4)) - 1) As Byte

            For i As Integer = 0 To padded.Length - 1
                padded(i) = If(i < data.Length, data(i), CByte(&HFF))
            Next

            data = padded
        End If

        If data.Length < 8 OrElse data.Length > MAX_CODE Then
            errText = "Прошивка не помещается в отведённую область: " & data.Length & " байт."
            Return False
        End If

        Dim sp As UInt32 = BitConverter.ToUInt32(data, 0)
        Dim pc As UInt32 = BitConverter.ToUInt32(data, 4)

        If sp < &H20000008UI OrElse sp > &H20005000UI OrElse (sp And 7UI) <> 0UI Then
            errText = "В файле испорчен вектор начального стека."
            Return False
        End If

        If (pc And 1UI) = 0UI OrElse (pc And Not 1UI) < APP_BASE OrElse
           (pc And Not 1UI) >= APP_BASE + CUInt(data.Length) Then
            errText = "Это не прошивка контроллера: она собрана не на адрес 0x08008200."
            Return False
        End If

        ' метка платы в зарезервированном векторе 8
        If data.Length >= HW_MARKER_OFFSET + 4 Then
            Dim mark As UInt32 = BitConverter.ToUInt32(data, HW_MARKER_OFFSET)

            If (mark And &HFFFFFF00UI) = HW_MARKER_TAG AndAlso CByte(mark And &HFFUI) <> app.hard Then
                errText = "Образ собран для платы " & CInt(mark And &HFFUI) & ", а в имени файла указана " & app.hard & "."
                Return False
            End If
        End If

        app.image = data

        Return True
    End Function

    ' Запись прошивки платой, стоящей в загрузчике. Установку и пробный запуск
    ' загрузчик делает сам после команды 0x22.
    Public Shared Function Upload(ByVal comport As ClComPort,
                                  ByVal fileName As String,
                                  ByVal app As s_appfile,
                                  ByVal progress As Action(Of Integer),
                                  ByRef errText As String) As Boolean

        Dim body As Byte() = Exchange(comport, CMD_INFO, Nothing, 2000, errText)

        If body Is Nothing Then
            Return False
        End If

        If body(3) < &H31 Then
            errText = "На плате старый загрузчик, он этот протокол не понимает."
            Return False
        End If

        If body(4) <> app.hard Then
            errText = "Загрузчик сообщает плату " & body(4) & ", а файл для платы " & app.hard & "."
            Return False
        End If

        Dim name As Byte() = System.Text.Encoding.ASCII.GetBytes(Path.GetFileName(fileName))
        Dim head(8 + name.Length) As Byte

        Array.Copy(BitConverter.GetBytes(CUInt(app.image.Length)), 0, head, 0, 4)
        Array.Copy(BitConverter.GetBytes(clOpros.Crc32Stm(app.image)), 0, head, 4, 4)
        Array.Copy(name, 0, head, 8, name.Length)
        head(head.Length - 1) = 0

        If Exchange(comport, CMD_BEGIN, head, 30000, errText) Is Nothing Then
            Return False
        End If

        Dim offset As Integer = 0

        While offset < app.image.Length
            Dim chunk As Integer = Math.Min(CHUNK, app.image.Length - offset)
            Dim data(chunk + 3) As Byte

            Array.Copy(BitConverter.GetBytes(CUInt(offset)), 0, data, 0, 4)
            Array.Copy(app.image, offset, data, 4, chunk)

            If Exchange(comport, CMD_DATA, data, 5000, errText) Is Nothing Then
                Return False
            End If

            offset += chunk

            If progress IsNot Nothing Then
                progress((offset * 100) \ app.image.Length)
            End If
        End While

        If Exchange(comport, CMD_COMMIT, Nothing, 30000, errText) Is Nothing Then
            Return False
        End If

        Return True
    End Function

End Class
