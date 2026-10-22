' Доработка: Кряжев А., 2026. Окно «Настройки»: общие кирпичи раскладки и вкладка «Основные»
' (серверы, сотовая связь, ПИН-код). Вкладки «Wi-Fi-модем» (Form2Wifi.vb) и «RS-485 и Wiegand» (Rs485.vb)
' собраны теми же кирпичами: карточки на сером фоне, подпись над полем, поле во всю колонку.
Imports System.Drawing
Imports System.Windows.Forms

Partial Public Class Form2

    ' Поле настроек: подпись над полем, поле во всю ширину колонки. Справа от подписи — галочка,
    ' под полем — пояснение или ошибка (невидимое места не занимает).
    ' Высота по содержимому: фиксированная высота обрезала нижнюю рамку поля.
    Private Function SettingsField(title As String, editor As Control, Optional toggle As Control = Nothing, Optional note As Label = Nothing) As Control
        Dim field As New TableLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .ColumnCount = 2, .RowCount = 3, .Anchor = AnchorStyles.Left Or AnchorStyles.Right, .Margin = New Padding(0, 0, 0, Theme.GapRow)}
        field.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        field.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        For rowIndex = 0 To 2 ' «row» — это функция Row из Form2.Designer.vb (VB не различает регистр)
            field.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Next
        Dim caption As New Label With {.Text = title, .AutoSize = True, .Font = Theme.FontBody, .ForeColor = Theme.TextMain,
            .Anchor = AnchorStyles.Left, .Margin = New Padding(0, 0, 0, Theme.GapLabel)}
        field.Controls.Add(caption, 0, 0)
        If toggle IsNot Nothing Then
            toggle.Anchor = AnchorStyles.Right
            toggle.Margin = New Padding(Theme.GapRelated, 0, 0, Theme.GapLabel)
            field.Controls.Add(toggle, 1, 0)
        Else
            field.SetColumnSpan(caption, 2)
        End If
        editor.Anchor = AnchorStyles.Left Or AnchorStyles.Right
        editor.Margin = New Padding(0)
        field.Controls.Add(editor, 0, 1)
        field.SetColumnSpan(editor, 2)
        If note IsNot Nothing Then
            note.AutoSize = True
            note.Anchor = AnchorStyles.Left Or AnchorStyles.Right
            note.Margin = New Padding(0, Theme.GapLabel, 0, 0)
            field.Controls.Add(note, 0, 2)
            field.SetColumnSpan(note, 2)
        End If
        Return field
    End Function

    ' Пояснение серым под полем или в конце карточки.
    Private Function SettingsNote(text As String) As Label
        Return New Label With {.Text = text, .AutoSize = True, .Font = Theme.FontCaption,
            .ForeColor = Theme.TextMuted, .Anchor = AnchorStyles.Left Or AnchorStyles.Right,
            .Margin = New Padding(0, 0, 0, Theme.GapRelated)}
    End Function

    ' Две колонки поровну; уже breakpoint точек (при 100 %) — друг под другом.
    Private Function SettingsColumns(left As Control, right As Control, Optional breakpoint As Integer = 760) As Control
        Dim grid As New TableLayoutPanel With {.Name = "settingsColumns", .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink, .ColumnCount = 2, .RowCount = 1,
            .Anchor = AnchorStyles.Left Or AnchorStyles.Right, .Margin = New Padding(0)}
        grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        grid.Controls.Add(left, 0, 0) : grid.Controls.Add(right, 1, 0)
        Dim arrange As EventHandler = Sub()
            Dim stacked = grid.Width < Theme.ControlPx(grid, breakpoint)
            Dim column = If(stacked, 0, 1), row = If(stacked, 1, 0)
            If grid.GetColumn(right) = column AndAlso grid.GetRow(right) = row Then Return
            grid.SuspendLayout()
            grid.ColumnStyles(0).Width = If(stacked, 100, 50)
            grid.ColumnStyles(1).Width = If(stacked, 0, 50)
            grid.SetCellPosition(right, New TableLayoutPanelCellPosition(column, row))
            left.Margin = New Padding(0, 0, If(stacked, 0, Theme.GapRelated), Theme.GapRow)
            right.Margin = New Padding(If(stacked, 0, Theme.GapRelated), 0, 0, Theme.GapRow)
            grid.ResumeLayout(True)
        End Sub
        left.Margin = New Padding(0, 0, Theme.GapRelated, Theme.GapRow)
        right.Margin = New Padding(Theme.GapRelated, 0, 0, Theme.GapRow)
        AddHandler grid.SizeChanged, arrange
        Return grid
    End Function

    ' Значение и кнопка рядом: ПИН-код и «Сменить ПИН».
    Private Function SettingsValue(value As Control, action As Control) As Control
        Dim line As New TableLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .ColumnCount = 2, .RowCount = 1, .Anchor = AnchorStyles.Left, .Margin = New Padding(0, 0, 0, Theme.GapRelated)}
        line.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        line.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        value.Anchor = AnchorStyles.Left
        value.Margin = New Padding(0, 0, Theme.GapBlock, 0)
        action.Anchor = AnchorStyles.Left
        action.Margin = New Padding(0)
        line.Controls.Add(value, 0, 0)
        line.Controls.Add(action, 1, 0)
        Return line
    End Function

    ' Вкладка «Основные»: серверы и сотовая связь рядом, ПИН-код под ними.
    Private Function BuildBasics() As Control
        lblPIN.Font = Theme.FontBodyBold
        Dim servers = Theme.Card("Серверы",
            SettingsField("Адрес сервера 1", boxServer1, chServer1, lblServer1Err),
            SettingsField("Адрес сервера 2", boxServer2, chServer2, lblServer2Err),
            SettingsNote("Адрес — узел или узел:порт, например 87.244.4.92:15010. Проверка открывает соединение с сервером 1 с этого компьютера."),
            Label21)
        Dim cellular = Theme.Card("Сотовая связь",
            SettingsField("APN", boxOperator, Nothing, SettingsNote("Пусто — APN подбирается по SIM-карте. Иначе: APN, логин, пароль через запятую.")),
            SettingsColumns(SettingsField("Тип сети", cmbNetType), SettingsField("VoLTE", cmbVoLTE), 400))
        Dim pin = Theme.Card("ПИН-код контроллера", SettingsValue(lblPIN, btnChangePIN),
            SettingsNote("Запрашивается при подключении к плате по USB. Заводской ПИН-код — 4321."))
        servers.Dock = DockStyle.Fill : cellular.Dock = DockStyle.Fill
        Return Page(SettingsColumns(servers, cellular), pin)
    End Function
End Class
