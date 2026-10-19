' Доработка: Кряжев А., 2026
' Wi-Fi-мост DR164 на линии 485: точка доступа и состояние связи через команду 113.
' Сеть и пароль хранятся в самом мосте; плата передаёт их по линии и перезапускает мост.
' Раздел стоит под «485 и Wiegand»; в настройки платы эти поля не входят.
Imports System.Linq
Imports System.Text
Imports System.Threading.Tasks
Imports System.Windows.Forms

Partial Public Class Form2
    Private wifiSsid As TextBox, wifiKey As TextBox
    Private wifiState As Label
    Private wifiWrite As Button, wifiCheck As Button
    Private wifiBusy As Boolean

    ' Раздел «Wi-Fi-мост»: поля сети и пароля, запись в мост, запрос состояния.
    Private Function BuildWifi() As Control
        wifiSsid = New TextBox With {.Name = "wifiSsid", .MaxLength = 32}
        wifiKey = New TextBox With {.Name = "wifiKey", .MaxLength = 63}
        Dim ssidBox = Theme.FieldBox(wifiSsid, 240)
        Dim keyBox = Theme.FieldBox(wifiKey, 240)
        Theme.PasswordField(keyBox, wifiKey)
        wifiWrite = New Button With {.Name = "wifiWrite", .Text = "Записать в мост"}
        Theme.Secondary(wifiWrite)
        wifiCheck = New Button With {.Name = "wifiCheck", .Text = "Проверить связь"}
        Theme.Secondary(wifiCheck)
        wifiCheck.Margin = New Padding(Theme.GapRelated, 0, 0, 0)
        AddHandler wifiWrite.Click, Sub() WifiExchange(True)
        AddHandler wifiCheck.Click, Sub() WifiExchange(False)
        Dim options As New TableLayoutPanel
        Theme.BusOptions(options)
        options.Controls.Add(Theme.BusField("Сеть Wi-Fi (SSID)", ssidBox), 0, 0)
        options.Controls.Add(Theme.BusField("Пароль сети", keyBox), 1, 0)
        Dim actions As New FlowLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .WrapContents = False, .Margin = New Padding(0, 0, 0, Theme.GapRow)}
        actions.Controls.Add(wifiWrite)
        actions.Controls.Add(wifiCheck)
        wifiState = Theme.BusNotice("Состояние моста не запрашивалось", False)
        Return Theme.SettingsSection("Wi-Fi-мост DR164", "Точка доступа для моста на линии 485", options, actions, wifiState,
            Hint("Сеть и пароль хранятся в самом мосте, на плате их нет. Сервер идёт через мост, когда линия 485 в активном режиме " &
                 "и у одного из устройств тип «Wi-Fi модем». После записи мост перезапускается, связь появляется через 10–20 с."))
    End Function

    ' Запись точки доступа (write = True) или запрос состояния; ответ показывается строкой под кнопками.
    Private Async Sub WifiExchange(write As Boolean)
        If wifiBusy OrElse busyWithBoard OrElse opros Is Nothing Then Return
        Dim payload As Byte() = {}
        If write Then
            Dim ssid = Encoding.UTF8.GetBytes(wifiSsid.Text.Trim())
            Dim key = Encoding.UTF8.GetBytes(wifiKey.Text)
            If ssid.Length = 0 OrElse ssid.Length > 32 OrElse key.Length > 63 Then
                Theme.SetText(wifiState, "Сеть 1–32 байта, пароль до 63; пустой пароль — открытая сеть")
                Return
            End If
            payload = ssid.Concat({CByte(0)}).Concat(key).Concat({CByte(0)}).ToArray()
        End If
        wifiBusy = True
        wifiWrite.Enabled = False : wifiCheck.Enabled = False
        Theme.SetText(wifiState, If(write, "Передача мосту…", "Запрос состояния…"))
        Try
            Dim reply = Await Task.Run(Function() opros.WifiBridge(payload))
            If IsDisposed Then Return
            Theme.SetText(wifiState, If(reply Is Nothing, "Плата не ответила на команду 113: прошивка без Wi-Fi или порт занят", WifiText(write, reply)))
        Catch ex As Exception
            AgentLog.Failure("wifi", ex)
            If Not IsDisposed Then Theme.SetText(wifiState, "Ошибка обмена: " & ex.Message)
        Finally
            wifiBusy = False
            If Not IsDisposed Then
                wifiWrite.Enabled = True : wifiCheck.Enabled = True
            End If
        End Try
    End Sub

    ' Текст ответа 113: после записи — код приёма, при запросе — канал сервера и связь моста.
    Private Shared Function WifiText(write As Boolean, reply As Byte()) As String
        If reply.Length < 2 Then Return "Ответ платы не разобран"
        If write Then
            Select Case reply(0)
                Case 0 : Return "Принято: мост перезапускается, связь появится через 10–20 с"
                Case 1 : Return "Занято: идёт съёмка или передача, повторите через минуту"
                Case Else : Return "Плата отвергла данные: проверьте сеть и пароль"
            End Select
        End If
        Dim link = If(reply(1) = 2, "TCP сервера через мост открыт",
                   If(reply(1) = 1, "точка доступа есть, TCP сервера нет", "точки доступа нет"))
        Return If(reply(0) <> 0, "Сервер через мост; ", "Сервер через GSM; ") & link
    End Function
End Class
