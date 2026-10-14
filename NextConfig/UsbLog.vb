Imports System.Globalization
Imports System.Text

' Тела 109: только проверенные кадры из общего декодера SLDN.
Public Class UsbLog
    Private Shared ReadOnly Ru As CultureInfo = CultureInfo.GetCultureInfo("ru-RU")
    Private Shared ReadOnly Events As New Dictionary(Of Byte, String) From {
        {6, "Обновление ID завершено"},
        {7, "Обновление настроек завершено"},
        {8, "Обновление групп завершено"},
        {9, "Обновление расписаний завершено"},
        {10, "Обновление праздничных дней завершено"},
        {11, "Нет регистрации в сети"},
        {12, "Нет подключения к интернету"},
        {13, "Неудачное подключение к серверу: нет трафика"},
        {14, "Подключен к серверу 1"},
        {15, "Подключен к серверу 2"},
        {16, "Ошибка соединения с сервером 1"},
        {17, "Ошибка соединения с сервером 2"},
        {18, "Потеря связи с сервером 1"},
        {19, "Потеря связи с сервером 2"},
        {20, "Вход 1 активен"},
        {21, "Вход 1 выключен"},
        {22, "Вход 2 активен"},
        {23, "Вход 2 выключен"},
        {24, "Авторегистрация включена"},
        {25, "Авторегистрация выключена"},
        {26, "«Открывать всем» включено"},
        {27, "«Открывать всем» выключено"},
        {28, "Разблокировка по расписанию включена"},
        {29, "Разблокировка по расписанию выключена"},
        {30, "Принудительная разблокировка включена"},
        {31, "Принудительная разблокировка выключена"},
        {32, "Перезагрузка по суточному таймеру"},
        {33, "Перезагрузка по команде пользователя"},
        {34, "Перезагрузка по расписанию"},
        {35, "Поступило входящее SMS"},
        {36, "Отправлено SMS"},
        {37, "Баланс ниже нормы"},
        {38, "Низкий уровень сигнала"},
        {39, "Перезагрузка по падению питания"},
        {40, "Перезагрузка: причина сброса процессора"},
        {65, "ID абонента отсутствует в базе"},
        {66, "ID абонента заблокирован"},
        {67, "Добавлен ID по SMS"},
        {68, "Добавлен ID по авторегистрации"},
        {69, "Выход включён по телефону"},
        {70, "Выход включён по RFID 485"},
        {71, "Выход включён по Wiegand"},
        {72, "Выход включён по брелоку"},
        {73, "Выход включён по автономеру"},
        {74, "Выход включён сервером: тест из админки"},
        {75, "Выход включён сервером: API"},
        {76, "Выход включён сервером: голосовой помощник"},
        {77, "Выход включён из мобильного приложения"},
        {78, "Получен ID со считывателя"},
        {131, "Значение счётчика проходов"},
        {132, "Начало загрузки новой прошивки"},
        {133, "Сбой загрузки прошивки, операция отменена"},
        {134, "Загрузка прошивки остановлена: прошло 10 минут"},
        {135, "Прошивка загружена, выполняется обновление"},
        {136, "Обновление прошивки завершено успешно"},
        {137, "Откат прошивки"},
        {138, "Истёк срок действия ID"},
        {139, "Запуск таймера ограничения по времени на объекте"}
    }

    Public Function Format(frame As Byte()) As String()
        If frame Is Nothing OrElse frame.Length < 18 OrElse frame(8) <> 109 OrElse frame(7) <> 0 Then Return {}
        Dim kind = frame(9) And 127
        Dim n = frame.Length - 18
        Dim text As String
        Select Case kind
            Case 1, 2, 3
                Dim chars = Encoding.GetEncoding(1251).GetString(frame, 16, n).ToCharArray()
                For i = 0 To chars.Length - 1
                    If Char.IsControl(chars(i)) Then chars(i) = "."c
                Next
                text = If(kind = 1, "→ ", If(kind = 2, "← ", "← уведомление: ")) & New String(chars)
                If (frame(9) And 128) <> 0 Then text &= " … [обрезано]"
            Case 4
                If n <> 3 Then Return {}
                Dim result = frame(16)
                Dim cme = BitConverter.ToUInt16(frame, 17)
                Select Case result
                    Case 0 : text = "← OK"
                    Case 1 : text = "← ERROR"
                    Case 2 : text = "← ошибка модема, CME " & If(cme = 65535, "текстовый код", cme.ToString(Ru))
                    Case 3 : text = "← приглашение передать данные"
                    Case 4 : text = "← модем не ответил"
                    Case Else : text = "← ошибка модема/потока, код " & result.ToString(Ru)
                End Select
            Case 5
                If n <> 3 OrElse frame(16) > 1 Then Return {}
                text = If(frame(16) = 1, "→ данные модему, ", "← данные от модема, ") & BitConverter.ToUInt16(frame, 17).ToString(Ru) & " байт"
            Case 16
                If n <> 7 OrElse frame(16) > 6 OrElse frame(17) > 15 OrElse frame(22) > 5 Then Return {}
                Dim nextPhase = CInt(frame(16))
                Dim duration = (BitConverter.ToUInt32(frame, 18) / 1000.0).ToString("0.0", Ru)
                Dim previous = LinkDiag.Stages(frame(22))
                If frame(17) <> 0 Then
                    text = "Связь: сбой на этапе «" & previous & "» — " & LinkDiag.ReasonText(frame(17))
                ElseIf nextPhase = 6 Then
                    text = "Связь: попытка закончена; «" & previous & "» за " & duration & " с"
                Else
                    text = "Связь: этап «" & LinkDiag.Stages(nextPhase) & "»; «" & previous & "» за " & duration & " с"
                End If
            Case 17
                If n <> 5 Then Return {}
                Select Case frame(16)
                    Case 0 : text = "Сервер: регистрация принята"
                    Case 1 : text = "Сервер: ошибка соединения/регистрации"
                    Case 2 : text = "Сервер: связь потеряна"
                    Case 3 : text = "Связь: ошибка регистрации в сети"
                    Case 4 : text = "Связь: нет интернета"
                    Case 5 : text = "Сервер: TCP подключён"
                    Case 6 : text = "Сервер: TCP разорван"
                    Case 7 : text = "Сервер: отказ в регистрации"
                    Case 8 : text = "Сервер: нет ответа на регистрацию"
                    Case 9 : text = "Сервер: надзор потерял ответы"
                    Case Else : Return {}
                End Select
                If frame(16) <> 3 AndAlso frame(16) <> 4 Then text &= ", сервер " & (BitConverter.ToUInt32(frame, 17) + 1UL).ToString(Ru)
            Case 18
                If n <> 5 OrElse frame(16) > 1 Then Return {}
                text = "Восстановление: " & If(frame(16) = 1, "перезапуск питания", "перезапуск с AT") & " через " &
                    (BitConverter.ToUInt32(frame, 17) / 1000.0).ToString("0.0", Ru) & " с"
            Case 32
                If n <> 32 Then Return {}
                Dim name As String = Nothing
                Dim eventId = frame(24)
                If Not Events.TryGetValue(eventId, name) Then name = "код " & eventId.ToString(Ru)
                Dim id = Encoding.ASCII.GetString(frame, 25, 10).Trim(ChrW(0), " "c)
                id = New String(id.Select(Function(c) If(Char.IsControl(c), "."c, c)).ToArray())
                ' До синхронизации часов плата пишет время от старта, его не показываем.
                Dim written = BitConverter.ToUInt32(frame, 20)
                Dim arg2 = BitConverter.ToUInt32(frame, 35)
                text = "Событие: " & name
                If written >= 1600000000UI Then text &= ", " & New DateTime(1970, 1, 1).AddSeconds(written).ToString("dd.MM.yyyy HH:mm:ss", Ru)
                If id <> "" Then text &= ", ID " & id
                If arg2 <> 0 Then text &= ", значение " & arg2.ToString(Ru)
                If frame(39) <> 0 Then text &= ", проходов " & frame(39).ToString(Ru)
            Case Else
                Return {}
        End Select
        Dim lines As New List(Of String)
        Dim lost = BitConverter.ToUInt16(frame, 14)
        If lost <> 0 Then lines.Add("— пропущено " & lost.ToString(Ru) & " записей журнала: USB был занят —")
        lines.Add((BitConverter.ToUInt32(frame, 10) / 1000.0).ToString("0.000", Ru).PadLeft(9) & "  " & text)
        Return lines.ToArray()
    End Function
End Class
