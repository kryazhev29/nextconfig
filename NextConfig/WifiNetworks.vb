' Доработка: Кряжев А., 2026. Список сетей Wi-Fi, найденных мостом DR164 (USB 115, прошивка с 22.10.2026).
' Запрос [строка]: 255 — забыть прежний список и искать заново. Ответ [статус][следующая строка][уровень %, длина, SSID]…:
' статус 0 — страница списка (следующая строка 255 — конец), 1 — поиск идёт, 2 — Wi-Fi-модем не выбран, 3 — мост не ответил.
' Мост отдаёт сети по убыванию уровня; имена без повторов; подключённая сеть — из ответа 113.
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text

Public Structure WifiNetwork
    Public Name As String
    Public Level As Integer        ' 0..100, -1 — нет данных
    Public Connected As Boolean
End Structure

Public Enum eWifiScan
    Ready = 0          ' список получен
    Searching = 1      ' поиск идёт, спросить снова
    NoModem = 2        ' Wi-Fi-модем не выбран
    NoAnswer = 3       ' мост не ответил
    NoReply = 4        ' плата не ответила: прошивка без команды 115 или нет связи
    Timeout = 5        ' поиск не закончился за отведённое время
End Enum

Public Class WifiScanResult
    Public Status As eWifiScan = eWifiScan.NoReply
    Public Networks As New List(Of WifiNetwork)
End Class

Public Module WifiNetworks
    Public Const SearchLimitMs As Integer = 20000
    Public Const PollMs As Integer = 400

    ' Страница ответа 115 → строки в list; nextRow — следующая строка (255 — конец). Статус по первому байту.
    Public Function ParsePage(reply As Byte(), list As List(Of WifiNetwork), ByRef nextRow As Integer) As eWifiScan
        nextRow = 255
        If reply Is Nothing OrElse reply.Length < 1 Then Return eWifiScan.NoReply
        If reply(0) <> 0 Then Return If(reply(0) <= 3, CType(reply(0), eWifiScan), eWifiScan.NoReply)
        If reply.Length < 2 Then Return eWifiScan.NoReply
        nextRow = reply(1)
        Dim at = 2
        While at + 2 <= reply.Length
            Dim len = CInt(reply(at + 1))
            If len = 0 OrElse at + 2 + len > reply.Length Then Exit While
            list.Add(New WifiNetwork With {.Name = Encoding.UTF8.GetString(reply, at + 2, len), .Level = Math.Min(100, CInt(reply(at)))})
            at += 2 + len
        End While
        Return eWifiScan.Ready
    End Function

    Public Function StatusText(status As eWifiScan) As String
        Select Case status
            Case eWifiScan.Ready : Return ""
            Case eWifiScan.Searching : Return "Поиск сетей…"
            Case eWifiScan.NoModem : Return "Wi-Fi-модем не выбран: сначала выберите модем и сохраните изменения"
            Case eWifiScan.NoAnswer : Return "Мост не ответил на запрос списка сетей — проверьте питание моста и линию RS-485"
            Case eWifiScan.Timeout : Return "Поиск сетей не закончился — повторите"
            Case Else : Return "Плата не ответила: нужна прошивка от 22.10.2026 или новее"
        End Select
    End Function

    ' Порядок «как в Windows»: подключённая сеть первой с пометкой, остальные по убыванию уровня.
    ' Подключённая сеть, которой нет в списке моста (скрытая), тоже показывается первой с уровнем из 113.
    Public Function Order(found As IEnumerable(Of WifiNetwork), current As String, currentLevel As Integer) As List(Of WifiNetwork)
        Dim rows = found.Where(Function(n) n.Name <> "").GroupBy(Function(n) n.Name).Select(Function(g) g.First()).ToList()
        Dim result = rows.Where(Function(n) n.Name <> current).OrderByDescending(Function(n) n.Level).ThenBy(Function(n) n.Name).ToList()
        If current <> "" Then
            Dim known = rows.Where(Function(n) n.Name = current).ToArray()
            Dim level = If(known.Length > 0 AndAlso known(0).Level >= 0, known(0).Level, currentLevel)
            result.Insert(0, New WifiNetwork With {.Name = current, .Level = level, .Connected = True})
        End If
        Return result
    End Function

    ' Текст второй строки сети: «Подключено» у текущей, оценка сигнала у остальных.
    Public Function Detail(n As WifiNetwork) As String
        If n.Connected Then Return "Подключено"
        Select Case LinkDiag.WifiGrade(n.Level)
            Case LinkDiag.Good : Return "Сигнал хороший"
            Case LinkDiag.Medium : Return "Сигнал средний"
            Case LinkDiag.Poor : Return "Сигнал слабый"
            Case Else : Return "Уровень не измерен"
        End Select
    End Function
End Module
