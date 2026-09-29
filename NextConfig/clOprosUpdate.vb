Imports System.Globalization
Imports System.Threading

' Часть класса clOpros: обновление прошивки и замена загрузчика
Partial Public Class clOpros

    ' ******************** Замена загрузчика, команда 110 ********************
    ' Протокол: Bootloader/boot_manual.h. Операции: 0 BEGIN (размер 4, CRC 4, HW 1),
    ' 1 DATA (смещение 4, до 256 байт), 2 COMMIT, 3 CANCEL, 4 STATUS.
    ' Ответ команды 110: 8 байт [операция, статус, принято 4, HW, взведено].

    Public Enum eBootOp
        OP_BEGIN = 0
        OP_DATA = 1
        OP_COMMIT = 2
        OP_CANCEL = 3
        OP_STATUS = 4
    End Enum

    Public Enum eBootStatus
        BOOT_OK = 0
        BOOT_BAD = 1
        BOOT_IO = 2
        BOOT_BUSY = 3
        BOOT_DENIED = 4
        BOOT_CRC = 5
        ' прошивка отказала ответом 11, до обработчика замены дело не дошло
        BOOT_OLD_FW = 100   ' код 255: команды 110 нет, на плате старая прошивка
        BOOT_NEED_PIN = 101 ' код 4: ПИН не введён
        BOOT_REFUSED = 102  ' другой код, он в _bootRefuseCode
        BOOT_NOANSWER = 255
    End Enum

    Private _bootRefuseCode As Byte

    Public Const BOOT_IMAGE_SIZE As Integer = 32768
    Public Const BOOT_CHUNK As Integer = 256

    ' Контрольная сумма как у аппаратного CRC контроллера: полином 0x04C11DB7,
    ' начальное значение 0xFFFFFFFF, по 4-байтным словам, без инверсии.
    Public Shared Function Crc32Stm(ByVal image As Byte()) As UInt32
        Dim crc As UInt32 = UInt32.MaxValue

        For i As Integer = 0 To image.Length - 1 Step 4
            crc = crc Xor BitConverter.ToUInt32(image, i)

            For b As Integer = 0 To 31
                If (crc And &H80000000UI) <> 0 Then
                    crc = (crc << 1) Xor &H4C11DB7UI
                Else
                    crc <<= 1
                End If
            Next
        Next

        Return crc
    End Function

    Public Function BootStatusText(ByVal status As eBootStatus) As String
        Select Case status
            Case eBootStatus.BOOT_OK
                Return "выполнено"
            Case eBootStatus.BOOT_BAD
                Return "загрузчик не прошёл проверку"
            Case eBootStatus.BOOT_IO
                Return "ошибка записи во внешнюю память"
            Case eBootStatus.BOOT_BUSY
                Return "контроллер занят: идёт обновление или пробный запуск"
            Case eBootStatus.BOOT_DENIED
                Return "контроллер отказал: не тот объём памяти микроконтроллера"
            Case eBootStatus.BOOT_CRC
                Return "не сошлась контрольная сумма"
            Case eBootStatus.BOOT_OLD_FW
                Return "прошивка на плате не умеет менять загрузчик, сначала залейте новую прошивку"
            Case eBootStatus.BOOT_NEED_PIN
                Return "контроллер ждёт ПИН-код, переподключитесь к плате"
            Case eBootStatus.BOOT_REFUSED
                Return "контроллер отказал, код ответа " & _bootRefuseCode
            Case Else
                Return "контроллер не ответил"
        End Select
    End Function

    ' Одна операция команды 110. received, hw и armed берутся из ответа контроллера.
    Public Function BootManual(ByVal op As eBootOp,
                               ByVal payload As Byte(),
                               ByRef received As UInt32,
                               ByRef hw As Byte,
                               ByRef armed As Byte,
                               ByVal timeout As Integer) As eBootStatus

        Dim rez As eBootStatus = eBootStatus.BOOT_NOANSWER

        SyncLock Me
            _isPause = True

            While _isSending
                Thread.Sleep(1)
            End While

            Dim rec As Byte()
            Dim send As Byte()

            If payload Is Nothing Then
                ReDim send(0)
            Else
                ReDim send(payload.Length)
                Array.Copy(payload, 0, send, 1, payload.Length)
            End If

            send(0) = CByte(op)

            If Comport.SendRecCmd(rec, SetCmd(eCmd.CMD_MANUALBOOT, send), timeout) Then
                If rec(cmdIdx) = eCmd.CMD_MANUALBOOT AndAlso rec.Length >= payloadIdx + 8 Then
                    If rec(payloadIdx) = CByte(op) Then
                        rez = CType(rec(payloadIdx + 1), eBootStatus)
                        received = BitConverter.ToUInt32(rec, payloadIdx + 2)
                        hw = rec(payloadIdx + 6)
                        armed = rec(payloadIdx + 7)
                    End If
                ElseIf rec(cmdIdx) = eCmd.REC_ANSWER AndAlso rec.Length > payloadIdx + 1 Then
                    ' прошивка отказала целиком, причина в коде ответа 11
                    _bootRefuseCode = rec(payloadIdx + 1)
                    Select Case _bootRefuseCode
                        Case 5 : rez = eBootStatus.BOOT_BUSY ' идёт обновление или пробный запуск
                        Case 4 : rez = eBootStatus.BOOT_NEED_PIN
                        Case 255 : rez = eBootStatus.BOOT_OLD_FW
                        Case Else : rez = eBootStatus.BOOT_REFUSED
                    End Select
                End If
            End If

            _isPause = False
        End SyncLock

        Return rez
    End Function

    ' Проверка файла загрузчика до отправки: ровно 32 КБ, версия 0x32 в хвосте,
    ' номер платы и вектора в начале области 0x08000000.
    Public Shared Function CheckBootImage(ByVal image As Byte(), ByRef hw As Byte, ByRef errText As String) As Boolean
        errText = ""
        hw = 0

        If image.Length <> BOOT_IMAGE_SIZE Then
            errText = "Файл загрузчика должен быть ровно " & BOOT_IMAGE_SIZE & " байт, а он " & image.Length & "."
            Return False
        End If

        If image(BOOT_IMAGE_SIZE - 4) <> &H32 Then
            errText = "Это не загрузчик версии 0x32."
            Return False
        End If

        hw = image(BOOT_IMAGE_SIZE - 3)

        If hw = 0 Then
            errText = "В файле загрузчика не указан номер платы."
            Return False
        End If

        Dim sp As UInt32 = BitConverter.ToUInt32(image, 0)
        Dim pc As UInt32 = BitConverter.ToUInt32(image, 4)

        If sp < &H20000008UI OrElse sp > &H20005000UI OrElse (sp And 7UI) <> 0UI Then
            errText = "В файле загрузчика испорчен вектор начального стека."
            Return False
        End If

        If (pc And 1UI) = 0UI OrElse (pc And Not 1UI) < &H8000000UI OrElse (pc And Not 1UI) >= &H8007FFCUI Then
            errText = "В файле загрузчика испорчен вектор запуска."
            Return False
        End If

        Return True
    End Function

    ' Полная замена загрузчика. При apply = False файл только передаётся и
    ' проверяется контроллером, затем отменяется: на плате ничего не меняется.
    Public Function ReplaceBootloader(ByVal image As Byte(),
                                      ByVal apply As Boolean,
                                      ByVal progress As Action(Of Integer),
                                      ByRef errText As String) As Boolean

        Dim received As UInt32 = 0
        Dim hw As Byte = 0
        Dim armed As Byte = 0
        Dim fileHw As Byte = 0
        Dim status As eBootStatus

        If Not CheckBootImage(image, fileHw, errText) Then
            Return False
        End If

        ' прежняя незавершённая передача, если она была
        BootManual(eBootOp.OP_CANCEL, Nothing, received, hw, armed, 3000)

        Dim head(8) As Byte
        Array.Copy(BitConverter.GetBytes(CUInt(image.Length)), 0, head, 0, 4)
        Array.Copy(BitConverter.GetBytes(Crc32Stm(image)), 0, head, 4, 4)
        head(8) = fileHw

        status = BootManual(eBootOp.OP_BEGIN, head, received, hw, armed, 30000)

        If status <> eBootStatus.BOOT_OK Then
            errText = "Начало замены: " & BootStatusText(status) & "."
            Return False
        End If

        Dim offset As Integer = 0

        While offset < image.Length
            Dim chunk As Integer = Math.Min(BOOT_CHUNK, image.Length - offset)
            Dim data(chunk + 3) As Byte

            Array.Copy(BitConverter.GetBytes(CUInt(offset)), 0, data, 0, 4)
            Array.Copy(image, offset, data, 4, chunk)

            status = BootManual(eBootOp.OP_DATA, data, received, hw, armed, 5000)

            If status <> eBootStatus.BOOT_OK Then
                errText = "Передача на смещении " & offset & ": " & BootStatusText(status) & "."
                BootManual(eBootOp.OP_CANCEL, Nothing, received, hw, armed, 3000)
                Return False
            End If

            offset += chunk

            If received <> CUInt(offset) Then
                errText = "Контроллер принял " & received & " байт вместо " & offset & "."
                BootManual(eBootOp.OP_CANCEL, Nothing, received, hw, armed, 3000)
                Return False
            End If

            If progress IsNot Nothing Then
                progress((offset * 100) \ image.Length)
            End If
        End While

        If Not apply Then
            status = BootManual(eBootOp.OP_CANCEL, Nothing, received, hw, armed, 5000)

            If status <> eBootStatus.BOOT_OK Then
                errText = "Отмена передачи: " & BootStatusText(status) & "."
                Return False
            End If

            Return True
        End If

        status = BootManual(eBootOp.OP_COMMIT, Nothing, received, hw, armed, 10000)

        If status <> eBootStatus.BOOT_OK Then
            errText = "Замена загрузчика: " & BootStatusText(status) & "."
            Return False
        End If

        If armed = 0 Then
            errText = "Контроллер не взвёл замену загрузчика."
            Return False
        End If

        Return True
    End Function

End Class
