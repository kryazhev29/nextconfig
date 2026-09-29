Imports System.IO
Imports System.Text

' Эталон v5: Финальный релиз только v5/Naviset Domovoi(Mini)_v3.0_IAR93/
' Naviset Domovoi(Mini)_v3.0_IAR93/ (далее FW).
' Старый конфигуратор: Configurator Domovoi Mini/Configurator Domovoi Mini/Unit1.cpp.
' Смещения ниже отсчитываются от тела [55, команда, ...], либо от начала структуры.
Public NotInheritable Class OldFirmware
    Private ReadOnly port As ClComPort
    Private ReadOnly demoMode As Boolean
    Private photoEnabled As Boolean?
    Private accessLevel As Byte
    Private lastSettings As Byte()
    Private ReadOnly stopRead As New Threading.CancellationTokenSource()
    Public ReadOnly Identification As Byte()
    Private Shared ReadOnly Ansi As Encoding = Encoding.GetEncoding(1251)

    Public Sub New(comport As ClComPort, identity As Byte(), isDemo As Boolean)
        If Not IsFirmware(identity) Then Throw New InvalidDataException("Неизвестный ответ идентификации старой прошивки.")
        port = comport
        demoMode = isDemo
        Identification = CType(identity.Clone(), Byte())
    End Sub

    ' FW/User/user_fun.c:4806-4819; FW/Bootloader/bootloader.c:341-350;
    ' Unit1.cpp:837-881. Признак 1 + 24 байта данных = приложение,
    ' признак 0 + 8 байт данных = загрузчик, независимо от версии загрузчика.
    Public Shared Function IsFirmware(b As Byte()) As Boolean
        ' FW/board.h:9: DEVICETYPE эталона v5 = 0x8B. Это тип, не ревизия PCB:
        ' старые DIN также могли иметь 0x8B; их совместимость отдельно не заявляется.
        Return b IsNot Nothing AndAlso b.Length = 26 AndAlso b(0) = &H55 AndAlso b(1) = 0 AndAlso b(2) = 1 AndAlso b(6) = &H8B AndAlso b(5) <> 49
    End Function

    Public Shared Function IsBootloader(b As Byte()) As Boolean
        Return b IsNot Nothing AndAlso b.Length = 10 AndAlso b(0) = &H55 AndAlso b(1) = 0 AndAlso b(2) = 0
    End Function

    Public ReadOnly Property SupportsPhoto As Boolean
        Get
            Return Identification(5) <> 49
        End Get
    End Property

    Public ReadOnly Property PhotoSupportText As String
        Get
            Return "Камера 0 · выберите разрешение и папку для JPEG. Поддержка проверяется ответом платы."
        End Get
    End Property

    Public Function CapturePhoto(folder As String, resolution As Byte) As String
        If Not SupportsPhoto Then Throw New InvalidOperationException(PhotoSupportText)
        If Not photoEnabled.HasValue Then ReadSettings()
        Dim wire As IPhotoWire = If(demoMode, DirectCast(New OldPhotoDemoWire(Identification(5) = 45), IPhotoWire), New OldPhotoWire(port))
        Return OldPhotoSession.Capture(wire, folder, Imei, 0, resolution, stopRead.Token, photoEnabled.Value)
    End Function

    Public Sub Reconnect()
        stopRead.Token.ThrowIfCancellationRequested()
        If demoMode Then Return
        accessLevel = 0
        lastSettings = Nothing
        photoEnabled = Nothing
        port.EnableEventReceived = False
        port.Port.Close()
        Try
            port.Port.Open()
            Dim identity = clBoot.IdentifyLegacy(port)
            If Not IsFirmware(identity) OrElse Not identity.SequenceEqual(Identification) Then
                Throw New IOException("На порту другая плата или другая прошивка. Закройте окно и выполните новый поиск.")
            End If
        Catch
            port.Port.Close()
            Throw
        End Try
    End Sub

    Public Function IdentityText() As String
        Dim b = Identification
        Return "Старая прошивка Domovoi · версия " & b(5) & " · плата загрузчика " & b(4) &
            " · тип устройства " & b(6) & " · загрузчик 0x" & b(3).ToString("X2") & vbCrLf &
            "IMEI: " & Str(b, 10, 16) & " · база до " & NumberLimit & " номеров"
    End Function

    Public ReadOnly Property Imei As String
        Get
            Return Str(Identification, 10, 16)
        End Get
    End Property

    ' CFG/Unit1.cpp:844-847: именно BOOT_RESTRICT, не версия платы.
    Public ReadOnly Property NumberLimit As Integer
        Get
            Return If(Identification(7) = &H10, 650, 15000)
        End Get
    End Property

    ' CFG/Unit1.dfm:1499-1505,2310-2316,2331-2337,1898-1900,2402-2405,2661-2665;
    ' привязка ItemIndex: Unit1.cpp:2059,2069,2116-2117,2137-2138,2148.
    Public Shared Function SettingCode(field As String, code As Byte) As String
        Dim names As String() = Nothing
        Select Case field
            Case "operator"
                names = {"Автоматически, данные с SIM-карты", "Билайн", "Мегафон", "МТС", "Теле2", "Другой"}
            Case "input1"
                names = {"SMS: ошибка контроллера", "Состояние ворот", "Сделать снимок с камеры", "Контроль питающей сети", "Датчик присутствия транспорта", "Кнопка открытия"}
            Case "input2"
                names = {"SMS: вскрытие корпуса", "Сделать снимок с камеры", "---", "Контроль питающей сети", "Датчик присутствия транспорта", "Кнопка открытия"}
            Case "output"
                names = {"Инвертировать", "Импульс"}
            Case "reader"
                names = {"UHF RFID 868-5300", "UHF RFID 868-1610", "UHF RFID 868-PRIME", "Универсальный WG"}
            Case "photo"
                Select Case code
                    Case 0, 3 : Return "160×120 (код " & code & ")"
                    Case 5 : Return "320×240 (код 5)"
                    Case 7 : Return "640×480 (код 7)"
                End Select
        End Select
        If names IsNot Nothing AndAlso code < names.Length Then Return names(code) & " (код " & code & ")"
        Return "Код " & code
    End Function

    Private Function Request(cmd As Byte, data As Byte()) As Byte()
        stopRead.Token.ThrowIfCancellationRequested()
        Dim b As Byte()
        If demoMode Then
            b = Demo.OldFirmwareReply(cmd, data)
        Else
            Dim errorText As String = ""
            b = clBoot.ReadOldFirmware(port, cmd, data, errorText)
            If b Is Nothing Then Throw New IOException(errorText)
        End If
        Header(b, cmd, 2)
        Return b
    End Function

    Public Sub EnterBootloader()
        stopRead.Token.ThrowIfCancellationRequested()
        If demoMode Then Return
        clBoot.EnterOldBootloader(port)
    End Sub

    Public Sub CancelRead()
        stopRead.Cancel()
    End Sub

    ' FW/User/user_fun.c:495-515; structures.h:18-20; Unit1.cpp:1060-1069.
    ' Пароль UInt16 LE, не строка ASCII. Никаких встроенных паролей.
    Public Function Authenticate(password As UShort) As Byte
        accessLevel = 0
        accessLevel = ParseAuthentication(Request(&H25, BitConverter.GetBytes(password)))
        Return accessLevel
    End Function

    Public Shared Function ParseAuthentication(b As Byte()) As Byte
        Header(b, &H25, 4)
        If b.Length <> 4 OrElse b(2) <> 0 Then Throw New InvalidDataException("Пароль не принят.")
        Return b(3)
    End Function

    Public Function ReadSettings() As DataTable
        Dim reply = Request(&H20, New Byte() {0})
        Dim table = ParseSettings(reply)
        lastSettings = reply.Skip(6).ToArray()
        photoEnabled = (reply(19) And 16) <> 0
        Return table
    End Function

    Public ReadOnly Property CanWrite As Boolean
        Get
            Return New Byte() {45, 47, 48}.Contains(Identification(5))
        End Get
    End Property

    Public ReadOnly Property WriteSupportText As String
        Get
            Return If(CanWrite, "Запись — только после подтверждения изменений.", "Протокол этой прошивки не подтверждён: запись недоступна.")
        End Get
    End Property

    Public Sub RequireAdministrator()
        If Not CanWrite Then Throw New InvalidOperationException(WriteSupportText)
        If Not demoMode AndAlso accessLevel <> &H20 Then Throw New UnauthorizedAccessException("Введите пароль администратора и повторите операцию.")
    End Sub

    Public Function SettingsSnapshot() As Byte()
        Return If(lastSettings Is Nothing, Nothing, CType(lastSettings.Clone(), Byte()))
    End Function

    Public Sub WriteSettings(before As Byte(), after As Byte(), backup As String)
        RequireAdministrator()
        OldSettings.Table(before) : OldSettings.Table(after)
        ReadSettings()
        If Not lastSettings.SequenceEqual(before) Then Throw New IOException("Настройки платы изменились после чтения. Прочитайте их заново и повторите изменения.")
        If Not before.Skip(195).Take(315).SequenceEqual(after.Skip(195).Take(315)) Then Throw New InvalidDataException("Нельзя изменять резерв конфигурации.")
        OldSettings.UpdateCrc(after)
        Using file As New FileStream(backup, FileMode.CreateNew, FileAccess.Write)
            file.Write(before, 0, before.Length) : file.Flush(True)
        End Using
        Dim reply As Byte()
        If demoMode Then
            reply = Demo.OldFirmwareReply(&H21, New Byte() {0, 0, 2}.Concat(after).ToArray())
        Else
            reply = clBoot.WriteOldSettings(port, after)
        End If
        If reply Is Nothing OrElse Not reply.SequenceEqual(New Byte() {&H55, &H21, 0, 0}) Then Throw New IOException("Плата не подтвердила запись настроек. Прочитайте настройки перед повтором.")
        ReadSettings()
        If Not lastSettings.SequenceEqual(after) Then Throw New IOException("Прочитанные настройки не совпали с записанными. Резерв сохранён.")
    End Sub

    Public Function ReadStatus() As DataTable
        Return ParseStatus(Request(&H22, New Byte() {0}))
    End Function

    ' FW/User/user_fun.c:626-669; structures.h:266-287 (64 байта);
    ' Unit1.cpp:1154-1198,3739-3747; Unit1.h:945-972.
    ' Читаем последовательно: firmware хранит numbersoffset.
    Public Function ReadNumbers() As DataTable
        ' v1-v3 отвечает старым numberscount, затем пересчитывает его.
        Request(&H2C, New Byte() {0})
        Dim b = Request(&H2C, New Byte() {0})
        Header(b, &H2C, 5)
        If b.Length <> 5 OrElse b(2) <> 0 Then Throw New InvalidDataException("Неверный ответ количества номеров.")
        Dim total = U16(b, 3)
        If total > NumberLimit Then Throw New InvalidDataException("Количество номеров превышает объём платы.")
        Dim table = NumbersTable()
        Dim index As Integer = 0
        While index < total
            b = Request(&H2C, New Byte() {1, CByte(index And 255), CByte(index >> 8)})
            Dim block = ParseNumbers(b, index, total)
            For Each row As DataRow In block.Rows
                table.ImportRow(row)
            Next
            index += block.Rows.Count
        End While
        Return table
    End Function

    Public Shared Function ParseNumbers(b As Byte(), index As Integer, total As Integer) As DataTable
        Header(b, &H2C, 7)
        Dim count = U16(b, 5)
        If b(2) <> 1 OrElse U16(b, 3) <> index OrElse count = 0 OrElse
            index + count > total OrElse b.Length <> 7 + count * 64 Then
            Throw New InvalidDataException("Нарушена последовательность или длина блока базы. Экспорт отменён.")
        End If
        Dim table = NumbersTable()
        For n = 0 To count - 1
            Dim p = 7 + n * 64
            If b(p) <> &HAA Then Throw New InvalidDataException("Неактивная ячейка в ответе базы. Повторите чтение; экспорт отменён.")
            Dim flags = b(p + 32)
            Dim kind = (flags >> 4) And 3
            Dim id = Str(b, p + 1, 11)
            table.Rows.Add((index + n + 1).ToString(), id, Str(b, p + 12, 20),
                New String() {"Телефон", "1-Wire", "RFID", "Неизвестный (3)"}(kind),
                New String() {"Пользователь", "Управляющий", "Администратор", "IP-пользователь"}(flags And 3),
                Timestamp(U32(b, p + 33)), b(p + 37).ToString(), b(p + 38).ToString(),
                b(p + 39).ToString(), "0x" & b(p).ToString("X2"), ((flags >> 2) And 1).ToString(),
                ((flags >> 3) And 1).ToString(), Hex(b, p, 64))
        Next
        Return table
    End Function

    Public Shared Function NumbersTable() As DataTable
        Dim t As New DataTable()
        For Each columnTitle As String In {"№", "Номер / ID", "Имя", "Тип", "Доступ", "Срок действия (часы платы)",
            "Лимит проходов", "Расписание", "Использовано проходов", "Метка ячейки", "Изменён", "Новый", "Исходные 64 байта"}
            t.Columns.Add(columnTitle)
        Next
        Return t
    End Function

    ' FW/User/user_fun.c:421-438; structures.h:336-407, connectstr:302-313,
    ' shedulestr:317-321; Unit1.cpp:966-981; Unit1.h:995-1057.
    ' Полная packed ConfStr = 512 байт.
    Public Shared Function ParseSettings(b As Byte()) As DataTable
        Header(b, &H20, 6)
        If b(2) <> 0 Then Throw New InvalidDataException("Ошибка чтения настроек: " & b(2))
        If b(3) <> 0 OrElse U16(b, 4) <> 512 OrElse b.Length <> 518 Then
            Throw New InvalidDataException("Неизвестная раскладка настроек: требуется блок 0 размером 512 байт.")
        End If
        Dim d = b.Skip(6).ToArray()
        Dim t = Fields()
        Add(t, "Служебные", "Идентификатор конфигурации", "0x" & d(0).ToString("X2"))
        Add(t, "Служебные", "Версия конфигурации", d(1))
        Add(t, "Служебные", "Номер конфигурации", d(2))
        Add(t, "Служебные", "Количество конфигураций", d(3))
        Add(t, "Служебные", "Тип оборудования", d(4))
        Add(t, "Служебные", "Время изменения (часы платы)", Timestamp(U32(d, 5)))
        Add(t, "Доступ", "Пароль администратора", U16(d, 9))
        Add(t, "Доступ", "Пароль управляющего / SMS", U16(d, 11))
        Bits(t, "Основные", d(13), {"Принимать все вызовы", "Синхронизация NTP", "Использовать вход 1", "Использовать вход 2", "Фотофиксация", "Запрашивать пароль", "Перезапуск по времени", "Выход по периоду"})
        Add(t, "Связь", "Оператор GSM", SettingCode("operator", d(14)))
        Add(t, "Связь", "Точка доступа APN", Str(d, 15, 40))
        Add(t, "Доступ", "PIN SIM-карты", Str(d, 55, 5))
        Add(t, "Сервер", "Подключение включено", YesNo(d(60) And 1))
        Add(t, "Сервер", "Резерв флагов подключения", d(60) >> 1)
        Add(t, "Сервер", "Код протокола", d(61))
        Add(t, "Сервер", "Время активности (исходное)", U16(d, 62))
        Add(t, "Сервер", "Таймаут (исходное)", U16(d, 64))
        Add(t, "Сервер", "Адрес", Str(d, 66, 24))
        Add(t, "Сервер", "Порт", U16(d, 90))
        Add(t, "Время", "Сервер NTP", Str(d, 92, 24))
        Add(t, "Время", "Порт NTP", U16(d, 116))
        Add(t, "Связь", "Номер SIM-карты", Str(d, 118, 16))
        Dim names = {"Тип срабатывания выхода", "Длительность импульса (исходное)", "Пауза (исходное)", "Количество повторов"}
        For i = 0 To 3
            Add(t, "Выход", names(i), If(i = 0, SettingCode("output", d(134)), d(134 + i).ToString()))
        Next
        For i = 0 To 6
            Dim p = 138 + i * 3
            Add(t, "Расписание " & (i + 1), "Расписание " & (i + 1) & " · дни недели (маска)", "0x" & d(p).ToString("X2"))
            Add(t, "Расписание " & (i + 1), "Расписание " & (i + 1) & " · начало (час)", d(p + 1))
            Add(t, "Расписание " & (i + 1), "Расписание " & (i + 1) & " · конец (час)", d(p + 2))
        Next
        Add(t, "Фото", "Разрешение фото", SettingCode("photo", d(159)))
        Add(t, "Время", "Часовой пояс", If(d(160) < 128, CInt(d(160)), CInt(d(160)) - 256))
        Add(t, "Время", "Час перезапуска", d(161))
        Add(t, "Выход по времени", "Начало (час)", d(162))
        Add(t, "Выход по времени", "Конец (час)", d(163))
        Bits(t, "Дополнительно", d(164), {"Время GNSS", "Тип выхода по времени", "SMS незарегистрированным", "Проверять баланс", "Пересылать SMS администратору"})
        Add(t, "Дополнительно", "Резерв флагов", d(164) >> 5)
        Add(t, "Выход по времени", "Начало (минута)", d(165))
        Add(t, "Выход по времени", "Конец (минута)", d(166))
        Add(t, "Выход по времени", "Дни недели (маска)", "0x" & d(167).ToString("X2"))
        Add(t, "Вызовы", "Количество гудков", d(168))
        Add(t, "Входы", "Функция входа 1", SettingCode("input1", d(169)))
        Add(t, "Входы", "Функция входа 2", SettingCode("input2", d(170)))
        Add(t, "Фото", "Задержка фото (исходное)", d(171))
        Add(t, "Баланс", "Порог (исходное)", U16(d, 172))
        Add(t, "Баланс", "Числовой код запроса", U16(d, 174))
        Add(t, "Баланс", "Строка запроса", Str(d, 176, 16))
        Add(t, "Вызовы", "Расписание приёма от всех", d(192))
        Add(t, "RFID", "Количество антенн", d(193))
        Add(t, "RFID", "Тип считывателя", SettingCode("reader", d(194)))
        Add(t, "Служебные", "Резерв, байты 195–509", Hex(d, 195, 315))
        Add(t, "Служебные", "CRC конфигурации", "0x" & U16(d, 510).ToString("X4"))
        Return t
    End Function

    ' FW/User/user_fun.c:476-483; structures.h:79-145 (стабильный префикс);
    ' Unit1.cpp:1038-1048; Unit1.h:768-919. DEVICE не ConfStr:
    ' поля packed, указатели = 32 бит; фактический размер эталона 513, не 512.
    Public Shared Function ParseStatus(b As Byte()) As DataTable
        Header(b, &H22, 5)
        Dim length = U16(b, 3)
        If b(2) <> 0 OrElse length < 282 OrElse b.Length <> length + 5 Then
            Throw New InvalidDataException("Неполный или неизвестный блок мониторинга.")
        End If
        Dim d = b.Skip(5).ToArray()
        Dim t = Fields()
        Add(t, "Состояние", "Вход 1 / вход 2 / выход", YesNo(d(70) And 1) & " / " & YesNo(d(70) And 2) & " / " & YesNo(d(70) And 4))
        Add(t, "Связь", "Уровень сигнала CSQ (0–31, 99 — неизвестен)", d(24))
        Add(t, "Связь", "Подключение к серверу", YesNo(d(32) And 64))
        Add(t, "Связь", "Регистрация GSM / GPRS", (d(23) And 15) & " / " & (d(23) >> 4))
        Add(t, "Время", "Местное время платы", Timestamp(U32(d, 66)))
        Add(t, "Состояние", "Наработка, с", U32(d, 49))
        Add(t, "Состояние", "Номеров в базе", U16(d, 54))
        Add(t, "Состояние", "Температура, °C", If(d(61) < 128, CInt(d(61)), CInt(d(61)) - 256))
        ' structures.h:196-258; Unit1.cpp:2405,2438-2451.
        Add(t, "Питание", "Внешнее питание, мВ", U16(d, 270))
        Add(t, "Связь", "Тип сети", If(d(269) <= 4, New String() {"Не определён", "GSM", "2G", "3G", "4G"}(Math.Min(CInt(d(269)), 4)), "Код " & d(269)))
        Add(t, "Баланс", "Остаток средств (исходное)", U16(d, 166))
        Add(t, "RFID", "Количество считывателей", d(237))
        Add(t, "RFID", "Состояние антенн (маска)", "0x" & d(238).ToString("X2"))
        Add(t, "Доступ", "Лимит номеров", U16(d, 160))
        Add(t, "Плата", "IMEI", Str(d, 0, 16))
        Add(t, "Плата", "Версия прошивки / тип", d(16) & " / " & d(17))
        Add(t, "Плата", "Дата / время сборки (коды)", U16(d, 18) & " / " & U16(d, 20))
        Add(t, "Плата", "Код заказчика", d(22))
        Add(t, "Связь", "Номер текущего вызова", Str(d, 33, 16))
        Add(t, "Связь", "Код оператора", d(53))
        Add(t, "Доступ", "Уровень доступа", "0x" & d(60).ToString("X2"))
        Add(t, "Входы", "Вход 1 / вход 2 (АЦП)", U16(d, 62) & " / " & U16(d, 64))
        Bits(t, "Состояние", d(70), {"Вход 1 активен", "Вход 2 активен", "Выход активен", "Сервер авторизован", "Доступ заблокирован", "Логический вход 1", "Логический вход 2", "Время достоверно"})
        Add(t, "Состояние", "Авторегистрация", YesNo(d(32) And 128))
        Add(t, "Журнал", "Счётчик записей (служебный)", U16(d, 79))
        Add(t, "Служебные", "Размер DEVICE, байт", length)
        Add(t, "Служебные", "Исходный блок DEVICE", Hex(d, 0, d.Length))
        Return t
    End Function

    Public Shared Sub SaveCsv(table As DataTable, path As String)
        ' UTF-8 BOM, ;, RFC4180 quoting. Excel не должен выполнять строки как формулы.
        Using w As New StreamWriter(path, False, New UTF8Encoding(True))
            w.WriteLine(String.Join(";", table.Columns.Cast(Of DataColumn)().Select(Function(c) CsvCell(c.ColumnName))))
            For Each row As DataRow In table.Rows
                w.WriteLine(String.Join(";", row.ItemArray.Select(Function(v) CsvCell(Convert.ToString(v)))))
            Next
        End Using
    End Sub

    Private Shared Function CsvCell(value As String) As String
        If value.Length > 0 AndAlso ("=+-@".Contains(value.TrimStart().FirstOrDefault()) OrElse
            value(0) = ChrW(9) OrElse value(0) = ChrW(13) OrElse value(0) = ChrW(10)) Then value = "'" & value
        Return """" & value.Replace("""", """""") & """"
    End Function

    Private Shared Sub Header(b As Byte(), cmd As Byte, minLength As Integer)
        If b Is Nothing OrElse b.Length < minLength OrElse b(0) <> &H55 OrElse b(1) <> cmd Then
            Throw New InvalidDataException("Некорректный ответ 0x" & cmd.ToString("X2"))
        End If
    End Sub
    Private Shared Function Fields() As DataTable
        Dim t As New DataTable()
        t.Columns.Add("Раздел") : t.Columns.Add("Параметр") : t.Columns.Add("Значение")
        Return t
    End Function
    Private Shared Sub Add(t As DataTable, group As String, title As String, value As Object)
        t.Rows.Add(group, title, Convert.ToString(value, Globalization.CultureInfo.InvariantCulture))
    End Sub
    Private Shared Sub Bits(t As DataTable, group As String, value As Byte, titles As String())
        For i = 0 To titles.Length - 1
            Add(t, group, titles(i), YesNo(value And (1 << i)))
        Next
    End Sub
    Private Shared Function YesNo(value As Integer) As String
        Return If(value <> 0, "Да", "Нет")
    End Function
    Private Shared Function U16(b As Byte(), offset As Integer) As UShort
        Return BitConverter.ToUInt16(b, offset)
    End Function
    Private Shared Function U32(b As Byte(), offset As Integer) As UInteger
        Return BitConverter.ToUInt32(b, offset)
    End Function
    Private Shared Function Str(b As Byte(), offset As Integer, count As Integer) As String
        Return Ansi.GetString(b, offset, count).Split(ChrW(0))(0)
    End Function
    Private Shared Function Hex(b As Byte(), offset As Integer, count As Integer) As String
        Return BitConverter.ToString(b, offset, count).Replace("-", " ")
    End Function
    Private Shared Function Timestamp(value As UInteger) As String
        If value = 0 OrElse value = UInteger.MaxValue Then Return value.ToString() & " (не задано)"
        Return New DateTime(1970, 1, 1).AddSeconds(value).ToString("dd.MM.yyyy HH:mm:ss") & " (" & value & ")"
    End Function
End Class
