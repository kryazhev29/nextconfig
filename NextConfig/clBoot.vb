Imports System.IO
Imports System.IO.Ports
Imports System.Threading

' Работа с платой, стоящей в загрузчике 0x32 (краткое ТЗ, раздел 8): загрузчик
' отвечает по USB своим идентификатором, программа предлагает выбрать файл
' прошивки и записывает его. Протокол загрузчика (Bootloader/boot_core.c):
' кадр "SLGT" + длина 2 + тело + CRC16 тела; тело запроса [0xAA, команда, данные],
' тело ответа [0x55, команда, статус, версия, HW, ...] — всего 10 байт.
' Команды: 0x00 идентификация, 0x20 начать приём, 0x21 данные, 0x22 установить.
'
' Старый загрузчик 2.x (платы до перевода на новую систему) говорит тем же кадром
' и так же отвечает на 0x00, но пишет прошивку своими командами и принимает её
' только зашифрованной (как программа CodingFW): 0x10 открыть флеш, 0x12 стереть,
' 0x13 заголовок 512 байт, 0x14 блоки по 1024, 0x15 проверить CRC, 0x11 закрыть.
' Ответ на них - [0x55, команда, статус]. Шифрование делает сама программа.
Public Class clBoot

    Public Const CMD_INFO As Byte = &H0
    Public Const CMD_BEGIN As Byte = &H20
    Public Const CMD_DATA As Byte = &H21
    Public Const CMD_COMMIT As Byte = &H22

    ' старый загрузчик 2.x
    Public Const OLD_BOOT_BELOW As Byte = &H30     ' версии ниже - старый загрузчик
    Private Const OLD_UNLOCK As Byte = &H10
    Private Const OLD_LOCK As Byte = &H11
    Private Const OLD_ERASE As Byte = &H12
    Private Const OLD_HEADER As Byte = &H13
    Private Const OLD_BLOCK As Byte = &H14
    Private Const OLD_CHECK As Byte = &H15
    Private Const OLD_HEADER_SIZE As Integer = 512
    Private Const OLD_BLOCK_SIZE As Integer = 1024

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

    Friend Shared Function Crc16(ByVal data As Byte()) As Integer
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

    Friend Shared Function Frame(ByVal cmd As Byte, ByVal data As Byte()) As Byte()
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
    ' legacy: старый загрузчик, ответ короче 10 байт и без кода объёма памяти.
    Private Shared Function Exchange(ByVal comport As ClComPort,
                                     ByVal cmd As Byte,
                                     ByVal data As Byte(),
                                     ByVal timeout As Integer,
                                     ByRef errText As String,
                                     Optional ByVal legacy As Boolean = False,
                                     Optional ByVal oldFirmware As Boolean = False) As Byte()
        Dim started = Environment.TickCount
        Dim body = ExchangeCore(comport, cmd, data, timeout, errText, legacy, oldFirmware)
        AgentLog.SlgtExchange(comport.Port.PortName, cmd, Frame(cmd, data), body, Environment.TickCount - started, errText)
        Return body
    End Function

    Private Shared Function ExchangeCore(ByVal comport As ClComPort,
                                         ByVal cmd As Byte,
                                         ByVal data As Byte(),
                                         ByVal timeout As Integer,
                                         ByRef errText As String,
                                         ByVal legacy As Boolean,
                                         ByVal oldFirmware As Boolean) As Byte()

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
                    Dim received As Integer = port.Read(buf, 0, ready)
                    pending.AddRange(buf.Take(received))
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

                If size < 2 OrElse size > If(oldFirmware, 4096, 592) Then
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

                If got <> Crc16(body) OrElse body(0) <> &H55 Then
                    Continue While
                End If

                ' v5 User/user_fun.c:346-374: на защищённое чтение приходит 0x25,
                ' а не исходная команда. Unit1.cpp:1060-1069 подтверждает раскладку.
                If oldFirmware AndAlso body(1) = &H25 AndAlso cmd <> &H25 AndAlso
                    body.Length >= 5 AndAlso body(2) = 4 Then
                    errText = "Требуется пароль " & If(body(4) = 2, "администратора.", "управляющего или администратора.")
                    Return Nothing
                End If
                If body(1) <> cmd Then Continue While
                If oldFirmware Then Return body

                If body.Length < 3 OrElse (Not legacy AndAlso body.Length <> 10) Then
                    errText = "Загрузчик ответил кадром неизвестного вида."
                    Return Nothing
                End If

                If Not legacy AndAlso body(2) = 7 Then
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

        errText = If(oldFirmware, "Плата", "Загрузчик") & " не ответил на команду 0x" & cmd.ToString("X2") & "."
        Return Nothing
    End Function

    ' Единственный вход для этапа чтения v5. Сборка кадра и CRC остаются общими.
    Friend Shared Function ReadOldFirmware(comport As ClComPort, cmd As Byte, data As Byte(), ByRef errorText As String) As Byte()
        Select Case cmd
            Case &H0, &H20, &H22, &H25, &H2C, &H30, &H31, &H32
                Return Exchange(comport, cmd, data, 3000, errorText, True, True)
            Case Else
                Throw New InvalidOperationException("Команда не разрешена в режиме чтения старой прошивки.")
        End Select
    End Function

    ' v5 User/user_fun.c:409-418; Unit1.cpp:2699 RebootinBootClick -> ResetDevice(1).
    ' Отдельно от допуска чтения: единственный вызов — после подтверждения в окне платы.
    Private Shared Function OldWriteExchange(comport As ClComPort, cmd As Byte, payload As Byte(), Optional timeout As Integer = 10000) As Byte()
        Dim errorText As String = ""
        Dim reply = Exchange(comport, cmd, payload, timeout, errorText, True, True)
        If reply Is Nothing Then Throw New IOException(errorText & " Результат операции неизвестен; повторять автоматически нельзя.")
        Return reply
    End Function

    Private Shared Function ReadOptionalOldService(comport As ClComPort, cmd As Byte, payload As Byte()) As Byte()
        Dim errorText As String = ""
        Dim reply = Exchange(comport, cmd, payload, 3000, errorText, True, True)
        If reply Is Nothing AndAlso Not errorText.Contains("не ответил") Then Throw New IOException(errorText)
        Return reply
    End Function
    Friend Shared Function ReadOldRfid(comport As ClComPort, address As Byte, parameter As Byte) As Byte()
        Dim reply = ReadOptionalOldService(comport, &H50, New Byte() {address, parameter})
        If reply Is Nothing Then Throw New IOException("RFID-считыватель не ответил. Проверьте тип, адрес и подключение.")
        Return reply
    End Function
    Friend Shared Function WriteOldRfid(comport As ClComPort, address As Byte, parameter As Byte, value As Byte) As Byte()
        Return OldWriteExchange(comport, &H51, New Byte() {address, parameter, value})
    End Function

    Friend Shared Function ReadOldChronology(comport As ClComPort, payload As Byte()) As Byte()
        If payload Is Nothing OrElse payload.Length <> 20 OrElse payload(0) <> 0 Then Throw New InvalidDataException("Разрешён только запрос количества хронологии.")
        Return ReadOptionalOldService(comport, &H2E, payload)
    End Function
    Friend Shared Function ReadOldWorkLog(comport As ClComPort) As Byte()
        Return ReadOptionalOldService(comport, &H29, Nothing)
    End Function
    Friend Shared Sub TestOldOutput(comport As ClComPort)
        SendOldWithoutReply(comport, &H26, Nothing)
    End Sub
    Friend Shared Sub SetOldAutoregistration(comport As ClComPort, enabled As Boolean)
        SendOldWithoutReply(comport, &H27, New Byte() {If(enabled, CByte(1), CByte(0))})
    End Sub
    Friend Shared Function ResetOldFactory(comport As ClComPort) As Byte()
        Return OldWriteExchange(comport, &H2F, Nothing, 60000)
    End Function

    Friend Shared Function AddOldNumber(comport As ClComPort, record As Byte()) As Byte()
        OldNumbers.Validate(record)
        Return OldWriteExchange(comport, &H2A, record)
    End Function
    Friend Shared Function DeleteOldNumber(comport As ClComPort, record As Byte()) As Byte()
        OldNumbers.Validate(record)
        Return OldWriteExchange(comport, &H2B, record)
    End Function
    Friend Shared Function WriteOldNumbersBlock(comport As ClComPort, payload As Byte()) As Byte()
        If payload.Length < 69 OrElse payload(0) <> 0 OrElse payload.Length <> 5 + BitConverter.ToUInt16(payload, 3) * 64 Then Throw New InvalidDataException("Неверный блок базы.")
        Return OldWriteExchange(comport, &H2D, payload, 30000)
    End Function
    Friend Shared Function ClearOldNumbers(comport As ClComPort) As Byte()
        Return OldWriteExchange(comport, &H2D, New Byte() {1}, 30000)
    End Function
    Friend Shared Sub FinishOldNumbers(comport As ClComPort)
        SendOldWithoutReply(comport, &H2D, New Byte() {2})
    End Sub
    Friend Shared Function CompressOldNumbers(comport As ClComPort) As Byte()
        Return OldWriteExchange(comport, &H28, Nothing, 60000)
    End Function
    Private Shared Sub SendOldWithoutReply(comport As ClComPort, cmd As Byte, payload As Byte())
        comport.EnableEventReceived = False
        Dim frame = clBoot.Frame(cmd, payload)
        comport.Port.Write(frame, 0, frame.Length)
        AgentLog.SlgtExchange(comport.Port.PortName, cmd, frame, Nothing, 0, "Команда без ответа; результат проверяется чтением")
        Thread.Sleep(100)
    End Sub

    Friend Shared Function WriteOldSettings(comport As ClComPort, block As Byte()) As Byte()
        If block Is Nothing OrElse block.Length <> 512 Then Throw New InvalidDataException("Требуется блок 512 байт.")
        Dim errorText As String = ""
        Dim reply = Exchange(comport, &H21, New Byte() {0, 0, 2}.Concat(block).ToArray(), 5000, errorText, True, True)
        If reply Is Nothing Then Throw New IOException(errorText & " Результат записи неизвестен; сначала прочитайте настройки.")
        Return reply
    End Function

    Friend Shared Sub EnterOldBootloader(comport As ClComPort)
        Dim errorText As String = ""
        Dim reply = Exchange(comport, &H4, New Byte() {1}, 3000, errorText, True, True)
        If reply Is Nothing Then Throw New IO.IOException(errorText & " Если плата уже перезапустилась, подключитесь заново.")
        If reply.Length <> 2 Then Throw New IO.InvalidDataException("Неожиданный ответ на переход в загрузчик.")
    End Sub

    Friend Shared Function IdentifyLegacy(comport As ClComPort) As Byte()
        Dim errorText As String = ""
        Return Exchange(comport, CMD_INFO, Nothing, 700, errorText, True, True)
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

        If Demo.IsOn Then Return DemoUpload(progress)

        Dim body As Byte() = Exchange(comport, CMD_INFO, Nothing, 2000, errText)

        If body Is Nothing Then
            Return False
        End If

        If body(4) <> app.hard Then
            errText = "Загрузчик сообщает плату " & body(4) & ", а файл для платы " & app.hard & "."
            Return False
        End If

        If body(3) < OLD_BOOT_BELOW Then
            Return UploadLegacy(comport, app, progress, errText)
        End If

        If body(3) < &H31 Then
            errText = "Загрузчик 0x" & body(3).ToString("X2") & " этот протокол не понимает."
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

    ' Пакет для старого загрузчика, как у CodingFW: заголовок 512 байт (SLFW, плата,
    ' номер прошивки, размер, CRC по открытому образу, билд) и тело, зашифрованное
    ' цепочкой от 0x5A. Повторно такой пакет не шифруется - берётся обычный BIN.
    Public Shared Function LegacyPackage(ByVal app As s_appfile) As Byte()
        Dim image As Byte() = app.image
        Dim fw As Byte = CByte(app.version And &HFF)
        Dim pkg(OLD_HEADER_SIZE + image.Length - 1) As Byte

        For i As Integer = 0 To OLD_HEADER_SIZE - 1
            pkg(i) = &HFF
        Next

        Array.Copy(BitConverter.GetBytes(&H57464C53UI), 0, pkg, 0, 4) ' "SLFW"
        pkg(4) = app.hard
        Array.Copy(BitConverter.GetBytes(app.version), 0, pkg, 5, 2)
        pkg(7) = 0
        Array.Copy(BitConverter.GetBytes(CUInt(image.Length)), 0, pkg, 8, 4)
        Array.Copy(BitConverter.GetBytes(clOpros.Crc32Stm(image)), 0, pkg, 12, 4)
        Array.Copy(BitConverter.GetBytes(app.build), 0, pkg, 20, 4)

        Dim crypt As Integer = &H5A

        For i As Integer = 0 To image.Length - 1
            pkg(OLD_HEADER_SIZE + i) = CByte((((CInt(image(i)) - fw) And &HFF) Xor crypt) And &HFF)
            crypt = (crypt + image(i)) And &HFF
        Next

        Return pkg
    End Function

    ' Запись через старый загрузчик. Перезапуск (0x04) не шлётся: плату включают
    ' заново без кнопки, и загрузчик запускает записанную прошивку.
    Private Shared Function UploadLegacy(ByVal comport As ClComPort,
                                         ByVal app As s_appfile,
                                         ByVal progress As Action(Of Integer),
                                         ByRef errText As String) As Boolean

        If app.version > 255 Then
            errText = "Старый загрузчик принимает номер прошивки не больше 255."
            Return False
        End If

        Dim pkg As Byte() = LegacyPackage(app)
        Dim size As Integer = app.image.Length

        If Exchange(comport, OLD_UNLOCK, Nothing, 3000, errText, True) Is Nothing Then Return False

        If Exchange(comport, OLD_ERASE, BitConverter.GetBytes(CUInt(size + OLD_HEADER_SIZE)), 20000, errText, True) Is Nothing Then
            errText = "Стирание: " & errText
            Return False
        End If

        Dim head(OLD_HEADER_SIZE + 1) As Byte
        Array.Copy(BitConverter.GetBytes(CUShort(OLD_HEADER_SIZE)), 0, head, 0, 2)
        Array.Copy(pkg, 0, head, 2, OLD_HEADER_SIZE)

        If Exchange(comport, OLD_HEADER, head, 5000, errText, True) Is Nothing Then
            errText = "Заголовок: " & errText
            Return False
        End If

        Dim offset As Integer = 0

        While offset < size
            Dim chunk As Integer = Math.Min(OLD_BLOCK_SIZE, size - offset)
            Dim data(chunk + 5) As Byte

            Array.Copy(BitConverter.GetBytes(CUInt(offset)), 0, data, 0, 4)
            Array.Copy(BitConverter.GetBytes(CUShort(chunk)), 0, data, 4, 2)
            Array.Copy(pkg, OLD_HEADER_SIZE + offset, data, 6, chunk)

            If Exchange(comport, OLD_BLOCK, data, 5000, errText, True) Is Nothing Then
                errText = "Блок на смещении " & offset & ": " & errText
                Return False
            End If

            offset += chunk

            If progress IsNot Nothing Then
                progress((offset * 100) \ size)
            End If
        End While

        If Exchange(comport, OLD_CHECK, Nothing, 5000, errText, True) Is Nothing Then
            errText = "Контрольная сумма на плате не сошлась: " & errText
            Return False
        End If

        If Exchange(comport, OLD_LOCK, Nothing, 3000, errText, True) Is Nothing Then Return False

        Return True
    End Function

    ' Демонстрационный режим: ход записи без платы.
    Private Shared Function DemoUpload(ByVal progress As Action(Of Integer)) As Boolean
        For p As Integer = 5 To 100 Step 5
            Thread.Sleep(80)
            If progress IsNot Nothing Then progress(p)
        Next

        Return True
    End Function

End Class
