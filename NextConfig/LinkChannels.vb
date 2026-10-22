' Карточка «Связь» на два канала (22.10.2026): какой канал везёт сервер, роль второго и цепочка этапов активного.
' Источник — USB 113 (прошивка task/wifi-one, User/c/user_commands.c): 0 сервер через мост; 1 связь моста
' (0 нет, 1 отвечает, 2 TCP открыт, 3 TCP выключен, 4 выключение без подтверждения, 5 выключение); 2 признак формата;
' 3 Wi-Fi-модем; 4 логика (0 только GSM, 1 GSM, если нет Wi-Fi, 2 только Wi-Fi); 5 канал. Два байта — прошивка до локального выбора.
' Состояние 112 описывает только модем, поэтому в цепочку Wi-Fi оно не входит.
Public Enum eLinkChannel
    Unknown = 0
    Wifi = 1
    Cellular = 2
End Enum

Public Enum eLinkTone
    Muted = 0
    Good = 1
    Warn = 2
    Bad = 3
End Enum

Public Structure LinkChannelRow
    Public Name As String
    Public Role As String
    Public State As String
    Public Tone As eLinkTone
    Public Active As Boolean
End Structure

Public Class LinkChannelView
    Public Active As eLinkChannel
    Public Known As Boolean          ' был ответ 113: только тогда смена канала — событие
    Public Online As Boolean
    Public Via As String = ""        ' «Wi-Fi», «сотовую сеть 4G»
    Public Headline As String = "Нет свежих данных"
    Public Note As String = ""       ' мелко после итога: «попытка через Wi-Fi»
    Public HeadTone As eLinkTone = eLinkTone.Muted
    Public Header As String = "Сервер: нет данных"
    Public Policy As String = ""
    Public Rows As LinkChannelRow() = {}   ' пусто, когда Wi-Fi-модем не выбран: один канал, как раньше
    Public Labels As String() = {"SIM", "Сеть", "Интернет", "Сервер"}
    Public Steps As Integer = -1
    Public Problem As String = "Нет свежих данных"
End Class

Public Module LinkChannels
    Public ReadOnly Policies As String() = {"только GSM", "GSM, если нет Wi-Fi", "только Wi-Fi"}
    Private ReadOnly WifiProblems As String() = {
        "Мост не отвечает — проверьте питание моста и линию RS-485",
        "Мост отвечает, соединения нет — проверьте сеть Wi-Fi, пароль и доступ в интернет",
        "Соединение открыто, сервер не отвечает — проверьте адрес сервера в настройках"}
    Private ReadOnly CellularBreaks As String() = {"нет SIM-карты", "нет сети", "нет интернета", "сервер не отвечает"}

    Public Function Stale() As LinkChannelView
        Return New LinkChannelView
    End Function

    ' network — технология модема из статуса 101 (1 — 2G, 3 — 3G, 4 — 4G, прочее — не сообщается).
    Public Function View(wifi As Byte(), snapshot As LinkSnapshot, gsm As Byte, internet As Byte, server As Byte, network As Byte) As LinkChannelView
        Dim known = wifi IsNot Nothing AndAlso wifi.Length >= 2
        Dim local = known AndAlso wifi.Length >= 6 AndAlso wifi(2) = 1 AndAlso wifi(3) <= 1 AndAlso wifi(4) <= 2
        Dim bridge = known AndAlso wifi(0) <> 0
        Dim generation = If(network = 1 OrElse network = 3 OrElse network = 4, LinkDiag.NetworkText(network), "")
        Dim result As New LinkChannelView With {.Known = known, .Online = server <> 0,
            .Active = If(bridge, eLinkChannel.Wifi, eLinkChannel.Cellular),
            .Via = If(bridge, "Wi-Fi", "сотовую сеть" & If(generation = "", "", " " & generation))}
        Dim selected = If(local, wifi(3) = 1, bridge)
        ' Коротко «через 4G»: крупный итог помещается в карточку в половину страницы; полностью — в событии переключения.
        Dim channel = If(bridge, "Wi-Fi", If(generation = "", "сотовую сеть", generation))
        If result.Online Then
            result.Headline = "На связи через " & channel : result.HeadTone = eLinkTone.Good
            result.Header = "Сервер через " & channel
        Else
            result.Headline = "Нет связи с сервером" : result.HeadTone = eLinkTone.Bad
            If selected Then result.Note = "попытка через " & channel
            result.Header = "Сервер не на связи"
        End If
        If bridge Then
            result.Labels = {"Мост", "Сеть Wi-Fi", "Сервер"}
            result.Steps = If(result.Online, 3, If(wifi(1) = 0, 0, If(wifi(1) <> 2, 1, 2)))
            result.Problem = If(result.Steps = 3, "", WifiProblems(result.Steps))
        Else
            result.Labels = {"SIM", If(generation = "", "Сеть", "Сеть " & generation), "Интернет", "Сервер"}
            result.Steps = LinkDiag.ConnectionSteps(snapshot, gsm, internet, server)
            result.Problem = LinkDiag.ConnectionProblem(snapshot, result.Steps)
        End If
        If Not selected Then Return result

        Dim policy = If(local, CInt(wifi(4)), -1)
        If policy >= 0 Then result.Policy = "Логика: " & Policies(policy)
        Dim cell = LinkDiag.CellText(snapshot, network, False) ' диапазон — в подробностях: в строке канала не помещается
        Dim wifiRow As New LinkChannelRow With {.Name = If(local, "Wi-Fi · USR-DR164", "Wi-Fi-мост"), .Active = bridge,
            .Role = If(bridge, "Активный", If(policy = 0, "Не используется", "Основной"))}
        Dim used = bridge OrElse policy <> 0 ' основной канал, который сейчас не везёт сервер, — повод для внимания
        Select Case wifi(1)
            Case 2 : wifiRow.State = "соединение с сервером открыто" : wifiRow.Tone = eLinkTone.Good
            Case 1 : wifiRow.State = "мост отвечает, соединения нет" : wifiRow.Tone = If(used, eLinkTone.Warn, eLinkTone.Muted)
            Case 3 : wifiRow.State = If(policy = 0, "соединение выключено", "соединение выключено, ожидание повтора") : wifiRow.Tone = If(used, eLinkTone.Warn, eLinkTone.Muted)
            Case 4 : wifiRow.State = "мост не подтвердил выключение" : wifiRow.Tone = eLinkTone.Bad
            Case 5 : wifiRow.State = "выключение моста…" : wifiRow.Tone = eLinkTone.Muted
            Case Else : wifiRow.State = "мост не отвечает" : wifiRow.Tone = If(used, eLinkTone.Bad, eLinkTone.Muted)
        End Select
        Dim cellRow As New LinkChannelRow With {.Name = "Сотовая" & If(cell = "", "", " · " & cell), .Active = Not bridge,
            .Role = If(Not bridge, "Активный", If(policy = 2, "Звонки и SMS", "Резерв"))}
        If Not bridge Then
            cellRow.State = If(result.Online, "сервер на связи", CellularBreaks(Math.Max(0, Math.Min(3, result.Steps))))
            cellRow.Tone = If(result.Online, eLinkTone.Good, eLinkTone.Bad)
        ElseIf gsm = 1 Then
            cellRow.State = "сеть есть" : cellRow.Tone = eLinkTone.Good
        ElseIf policy = 2 Then
            cellRow.State = "нет сети — звонки и SMS недоступны" : cellRow.Tone = eLinkTone.Bad
        Else
            cellRow.State = "нет сети — резерв недоступен" : cellRow.Tone = eLinkTone.Warn
        End If
        result.Rows = {wifiRow, cellRow}
        Return result
    End Function

    ' Текст для строки состояния: «переключение с Wi-Fi на сотовую сеть 4G»; пусто, если канал не сменился.
    Public Function SwitchText(previous As LinkChannelView, current As LinkChannelView) As String
        If previous Is Nothing OrElse current Is Nothing OrElse Not previous.Known OrElse Not current.Known OrElse previous.Active = current.Active Then Return ""
        Return "переключение с " & If(previous.Active = eLinkChannel.Wifi, "Wi-Fi", "сотовой сети") & " на " & current.Via
    End Function
End Module
