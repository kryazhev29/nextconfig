Imports System.Text.RegularExpressions

' Только представление: порядок и байты OldSettings остаются прежними.
Public Module OldSettingsPresentation
    Public Function DisplayValue(f As OldSettingField, block As Byte(), index As Integer) As String
        Dim raw = OldSettings.RawValue(block, index)
        If f.Secret Then Return "••••"
        If f.Offset = 159 AndAlso raw = "0" Then Return "160×120"
        Dim options = Choices(f), value As Integer
        If Integer.TryParse(raw, value) AndAlso options.ContainsKey(value) Then Return options(value)
        If f.Mask <> 255 AndAlso (f.Mask >> f.Shift) = 1 Then Return If(raw = "1", "Включено", "Выключено")
        If f.Offset = 167 OrElse (f.Offset >= 138 AndAlso f.Offset <= 158 AndAlso (f.Offset - 138) Mod 3 = 0) Then
            Dim days = {"Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"}
            Return If(value = 0, If(f.Offset = 167, "Дни не выбраны", "Все дни"), String.Join(", ", days.Where(Function(d, i) (value And (1 << i)) <> 0)))
        End If
        Return raw
    End Function
    Public Function ServiceOnly(f As OldSettingField) As Boolean
        Return f.Offset < 9 OrElse (f.Offset = 60 AndAlso f.Mask <> 1) OrElse
            (f.Offset >= 61 AndAlso f.Offset <= 64) OrElse
            (f.Offset = 164 AndAlso f.Mask = 224) OrElse f.Offset = 174 OrElse f.Offset >= 195
    End Function

    Public Function Group(f As OldSettingField, original As String) As String
        If ServiceOnly(f) Then Return "Служебные"
        If f.Offset = 172 OrElse f.Offset = 176 OrElse (f.Offset = 164 AndAlso f.Mask = 8) Then Return "Баланс SIM-карты"
        If f.Offset = 13 Then
            Return New String() {"Вызовы и SMS", "Время", "Входы", "Входы", "Фото", "Доступ", "Время", "Выход по времени"}(f.Shift)
        End If
        If f.Offset = 164 Then Return If(f.Mask = 1, "Время", If(f.Mask = 2, "Выход по времени", "Вызовы и SMS"))
        If original = "Вызовы" Then Return "Вызовы и SMS"
        Return original
    End Function

    Public Function Title(f As OldSettingField, original As String) As String
        Select Case f.Offset
            Case 135 : Return "Длительность импульса, секунды"
            Case 136 : Return "Пауза между импульсами, секунды"
            Case 160 : Return "Часовой пояс, часы от UTC"
            Case 171 : Return "Задержка фото, секунды"
            Case 172 : Return "Порог уведомления, рубли"
            Case 176 : Return "USSD-запрос баланса"
            Case 192 : Return "Расписание вызовов от всех"
        End Select
        If f.Offset = 164 AndAlso f.Mask = 2 Then Return "Действие на границах периода"
        If f.Offset = 164 AndAlso f.Mask = 1 Then Return "Время от спутникового приёмника"
        Return original.Replace(" (маска)", "").Replace("Синхронизация NTP", "Сверять время через интернет")
    End Function

    Public Function Hint(f As OldSettingField) As String
        If f.Offset >= 138 AndAlso f.Offset <= 158 Then
            Return If((f.Offset - 138) Mod 3 = 0, "Выберите дни доступа. Если ничего не выбрано — доступны все дни.", "Местные часы платы. Одинаковые начало и конец — доступ круглосуточно.")
        End If
        Select Case f.Offset
            Case 9 : Return "Пароль для изменения настроек: от 0 до 65535. Храните его у владельца."
            Case 11 : Return "Пароль управляющего и команд по SMS: от 0 до 65535."
            Case 14 : Return "Выберите оператора SIM-карты. Для своего APN выберите «Другой»."
            Case 15 : Return "Настройки интернета от оператора: APN,логин,пароль через запятые."
            Case 55 : Return "PIN SIM-карты: до четырёх цифр; пустое поле — без PIN."
            Case 60 : Return "Разрешает плате соединяться с сервером."
            Case 66 : Return "IP-адрес или имя сервера, выданное обслуживающей организацией."
            Case 90 : Return "Порт соединения с сервером."
            Case 92 : Return "Адрес сервера точного времени."
            Case 116 : Return "Порт сервера точного времени; обычно 123."
            Case 118 : Return "Телефон SIM-карты, установленной в контроллере."
            Case 134 : Return "Переключение меняет состояние выхода; импульс включает его на заданное время."
            Case 135, 136 : Return "От 0 до 255 секунд. Используется в режиме «Импульс»."
            Case 137 : Return "Количество импульсов при одном открытии."
            Case 159 : Return "Размер фотографий, сохраняемый в настройках платы."
            Case 160 : Return "Например, для Барнаула UTC+7 — значение 7."
            Case 161 : Return "Местный час ежедневного перезапуска; действует при включённом перезапуске по времени."
            Case 162, 163, 165, 166 : Return "Местное время начала или окончания автоматического управления выходом."
            Case 167 : Return "Дни автоматического управления выходом."
            Case 168 : Return "После скольких гудков плата обрабатывает входящий вызов."
            Case 169, 170 : Return "Назначение входа; действует, когда этот вход включён."
            Case 171 : Return "Задержка автоматического фото в v1–v3. В исходниках v5 применение не подтверждено."
            Case 172 : Return "SMS администратору при балансе ниже порога. Целые рубли, копейки отбрасываются. 0 отключает ежедневный запрос."
            Case 176 : Return "Команда вашего оператора, например *100#. До 15 символов. Пустая строка отключает ежедневный запрос."
            Case 192 : Return "Ограничивает вызовы от незарегистрированных номеров выбранным расписанием."
            Case 193 : Return "Количество подключённых RFID-антенн."
            Case 194 : Return "Модель установленного считывателя."
        End Select
        If f.Offset = 164 Then
            Select Case f.Mask
                Case 1 : Return "Использовать часы спутникового приёмника, если он установлен. В v1–v3 при этом сверка через интернет не применяется."
                Case 8 : Return "Раз в сутки при наличии сети запрашивать баланс и предупреждать администратора по SMS. Нужны строка запроса и ненулевой порог."
                Case 2 : Return "Удерживать выход весь период либо подавать импульс 1 секунду в начале и конце."
                Case 4 : Return "Отправлять ответ по SMS незарегистрированным пользователям."
                Case 16 : Return "Пересылать входящие SMS администратору."
            End Select
        End If
        If f.Offset = 13 Then
            Return New String() {"Разрешить открытие по звонку также номерам, которых нет в базе.",
                "Автоматическая сверка часов платы с сервером точного времени.",
                "Обрабатывать сигнал первого входа.", "Обрабатывать сигнал второго входа.",
                "Автоматически фотографировать события доступа.", "Требовать пароль для доступа к управлению.",
                "Ежедневно перезапускать плату в выбранный час.", "Автоматически управлять выходом по дням и времени."}(f.Shift)
        End If
        Return "Изменение остаётся в черновике до записи настроек."
    End Function

    Public Function Choices(f As OldSettingField) As Dictionary(Of Integer, String)
        Dim result As New Dictionary(Of Integer, String)
        If f.Offset >= 138 AndAlso f.Offset <= 158 AndAlso (f.Offset - 138) Mod 3 <> 0 Then
            For n = 0 To 23 : result(n) = n.ToString("00") & ":00" : Next
            Return result
        End If
        Dim key = "", count = 0
        Select Case f.Offset
            Case 14 : key = "operator" : count = 6
            Case 134 : key = "output" : count = 2
            Case 169 : key = "input1" : count = 6
            Case 170 : key = "input2" : count = 6
            Case 194 : key = "reader" : count = 4
            Case 159
                For Each n In {3, 5, 7}
                    result(n) = Regex.Replace(OldFirmware.SettingCode("photo", CByte(n)), " \(код .*\)", "")
                Next
            Case 192
                result(0) = "Без ограничений по времени"
                For n = 1 To 7 : result(n) = "Расписание " & n : Next
            Case 161, 162, 163
                For n = 0 To 23 : result(n) = n.ToString("00") & ":00" : Next
            Case 165, 166
                For n = 0 To 59 : result(n) = n.ToString("00") & " мин" : Next
            Case 164
                If f.Mask = 2 Then
                    result(0) = "Удерживать выход включённым"
                    result(1) = "Импульс в начале и конце"
                End If
        End Select
        For n = 0 To count - 1
            result(n) = Regex.Replace(OldFirmware.SettingCode(key, CByte(n)), " \(код .*\)", "").Replace("---", "Не используется").Replace("Инвертировать", "Переключать состояние")
        Next
        Return result
    End Function

    Public Function Enabled(f As OldSettingField, block As Byte()) As Boolean
        Select Case f.Offset
            Case 15 : Return block(14) = 5
            Case 172, 176 : Return (block(164) And 8) <> 0
            Case 66, 90 : Return (block(60) And 1) <> 0
            Case 92, 116 : Return (block(13) And 2) <> 0
            Case 135, 136, 137 : Return block(134) = 1
            Case 161 : Return (block(13) And 64) <> 0
            Case 162, 163, 165, 166, 167 : Return (block(13) And 128) <> 0
            Case 169 : Return (block(13) And 4) <> 0
            Case 170 : Return (block(13) And 8) <> 0
            Case 192 : Return (block(13) And 1) <> 0
        End Select
        If f.Offset = 164 AndAlso f.Mask = 2 Then Return (block(13) And 128) <> 0
        Return True
    End Function
End Module
