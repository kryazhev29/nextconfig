Imports System.IO
Imports System.Drawing
Imports System.Globalization

' NEXT User/photo.c:495-593, User/user_fun.c:3470-3475.
' Сопоставлено с Tools/Photo/photo_server_gui.py:70-196; реализация независимая.
' Никаких портов в этом классе: одинаковый разбор для USB и демо.
Public Class PhotoTransfer
    Public Enum StepKind
        More
        RequestAgain
        Done
    End Enum
    Public ReadOnly Saved As New List(Of String)
    Public Property Message As String = "Ожидание фотографий…"
    Public Property Received As Integer
    Public Property Size As Integer
    Public Property Number As UInteger
    Public Property Retries As Integer
    Public Property Duplicates As Integer
    Private ReadOnly folder As String
    Private ReadOnly imei As String
    Private data As Byte() = New Byte() {}
    Private hasPhoto As Boolean
    Private completed As Boolean
    Private missingPrefix As String = ""
    Private lastRequestedNumber As UInteger
    Private lastRequestedOffset As Integer

    Public Sub New(deviceImei As String, directory As String)
        If Not System.Text.RegularExpressions.Regex.IsMatch(deviceImei, "\A[0-9A-Za-z_-]{1,64}\z") Then
            Throw New InvalidDataException("Некорректный IMEI: нельзя сформировать имя фото.")
        End If
        imei = deviceImei
        folder = Path.GetFullPath(directory)
        IO.Directory.CreateDirectory(folder)
    End Sub

    Private Function Filename(number As UInteger, extension As String) As String
        Return Path.Combine(folder, imei & "_" & number.ToString(CultureInfo.InvariantCulture) & extension)
    End Function

    Public Function Cursor(Optional reset As Boolean = False) As Byte()
        Dim number As UInteger = If(reset OrElse Not hasPhoto, 0UI, Me.Number)
        Dim offset As Integer = If(reset OrElse Not hasPhoto, 0, Received)
        lastRequestedNumber = number : lastRequestedOffset = offset
        Return BitConverter.GetBytes(number).Concat(BitConverter.GetBytes(CUShort(offset))).ToArray()
    End Function

    Public Function Receive(payload As Byte()) As StepKind
        If payload.Length = 1 AndAlso payload(0) = 0 Then
            If hasPhoto AndAlso Not completed Then
                Message = "Передача прервалась. Запрашиваем фото с байта " & Received & "."
                Return StepKind.RequestAgain
            End If
            Message = If(Saved.Count = 0, "Очередь фотографий пуста.", "Готово. Сохранено фото: " & Saved.Count & ". Очередь пуста.")
            Return StepKind.Done
        End If
        If payload.Length < 12 Then Throw New InvalidDataException("Короткий пакет фотографии.")
        Dim n = BitConverter.ToUInt32(payload, 1)
        Dim length = BitConverter.ToUInt32(payload, 5)
        Dim offset = CInt(BitConverter.ToUInt16(payload, 9))
        Dim count = payload.Length - 11
        If payload(0) = 0 OrElse length = 0 OrElse length > 65535 OrElse count > 1024 OrElse offset + count > length Then
            Throw New InvalidDataException("Некорректный размер или смещение фото.")
        End If
        If Not hasPhoto OrElse n <> Number Then
            If hasPhoto AndAlso Not completed Then
                Message = "Пропущена часть фото " & Number & ". Запрашиваем повтор."
                Return StepKind.RequestAgain
            End If
            Dim prefix As Byte() = New Byte() {}
            Dim partialPath = Filename(n, ".partial")
            If offset > 0 Then
                If File.Exists(partialPath) Then
                    If New FileInfo(partialPath).Length > length Then Throw New InvalidDataException("Сохранённая часть больше заявленного фото.")
                    prefix = File.ReadAllBytes(partialPath)
                ElseIf File.Exists(Filename(n, ".jpg")) Then
                    prefix = File.ReadAllBytes(Filename(n, ".jpg"))
                    ValidateJpeg(prefix)
                End If
                If prefix.Length < offset Then
                    Dim gap = n & ":" & offset
                    If missingPrefix = gap AndAlso lastRequestedNumber = n AndAlso lastRequestedOffset = prefix.Length Then
                        Throw New IOException("Начало фото уже подтверждено платой, но отсутствует в выбранной папке (нужно " & offset & " байт). Выберите прежнюю папку с .partial или JPEG.")
                    End If
                    missingPrefix = gap
                    Number = n : Size = CInt(length) : Received = prefix.Length
                    data = prefix : hasPhoto = True : completed = False
                    Message = "Ищем неподтверждённое начало фото " & n & "."
                    Return StepKind.RequestAgain
                End If
            End If
            Number = n : Size = CInt(length) : Received = offset
            data = prefix.Take(offset).ToArray() : hasPhoto = True : completed = False
            Persist(partialPath, data)
        End If
        If length <> Size Then Throw New InvalidDataException("Размер того же фото изменился во время передачи.")
        If offset > Received Then
            If missingPrefix = n & ":" & offset AndAlso lastRequestedNumber = n AndAlso lastRequestedOffset = Received Then
                Throw New IOException("Подтверждённое начало фото отсутствует в этой папке. Выберите прежнюю папку выгрузки.")
            End If
            Message = "Пакет потерян. Повтор с байта " & Received & "."
            Return StepKind.RequestAgain
        End If
        If offset < Received Then
            If offset + count > Received OrElse Not data.Skip(offset).Take(count).SequenceEqual(payload.Skip(11)) Then
                Throw New InvalidDataException("Повторный пакет отличается от сохранённого. Фото не подтверждено.")
            End If
            Duplicates += 1
            Message = "Повтор пакета проверен, повторная запись не нужна."
            Return StepKind.More
        End If
        Dim nextData = data.Concat(payload.Skip(11)).ToArray()
        Persist(Filename(n, ".partial"), nextData) ' fsync ДО любого подтверждения
        data = nextData : Received = data.Length
        Message = "Фото " & n & ": " & Received & " из " & Size & " байт."
        If Received = Size Then
            ValidateJpeg(data)
            Dim finalPath = Filename(n, ".jpg")
            If File.Exists(finalPath) Then
                If Not File.ReadAllBytes(finalPath).SequenceEqual(data) Then Throw New IOException("Файл " & Path.GetFileName(finalPath) & " уже существует с другим содержимым. Выберите другую папку.")
            Else
                Dim temp = finalPath & ".part"
                Persist(temp, data)
                File.Move(temp, finalPath)
            End If
            File.Delete(Filename(n, ".partial"))
            completed = True
            If Not Saved.Contains(finalPath) Then Saved.Add(finalPath)
            Message = "Сохранено " & Path.GetFileName(finalPath) & ". Подтверждаем получение…"
            Return StepKind.RequestAgain
        End If
        Return StepKind.More
    End Function

    Private Shared Sub Persist(path As String, bytes As Byte())
        ' Старый подтверждённый префикс не уничтожается при записи следующего блока:
        ' временный файл + замена только после Flush(true).
        Dim temp = path & ".writing"
        Using f As New FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None)
            f.Write(bytes, 0, bytes.Length)
            f.Flush(True)
        End Using
        If File.Exists(path) Then File.Replace(temp, path, Nothing) Else File.Move(temp, path)
    End Sub

    Public Shared Sub ValidateJpeg(bytes As Byte())
        If bytes.Length < 4 OrElse bytes(0) <> 255 OrElse bytes(1) <> &HD8 OrElse
            bytes(bytes.Length - 2) <> 255 OrElse bytes(bytes.Length - 1) <> &HD9 Then
            Throw New InvalidDataException("JPEG не целый: отсутствует FFD8/FFD9. Подтверждение не отправлено.")
        End If
        Using stream As New MemoryStream(bytes), picture = Image.FromStream(stream, True, True)
            If picture.RawFormat.Guid <> Imaging.ImageFormat.Jpeg.Guid Then Throw New InvalidDataException("Получен файл другого формата.")
            Using decoded As New Bitmap(picture)
                If decoded.Width = 0 OrElse decoded.Height = 0 Then Throw New InvalidDataException("Пустое изображение JPEG.")
            End Using
        End Using
    End Sub
End Class

