Public Class frmBootDevice
    Inherits Form
    Private ReadOnly port As ClComPort
    Private ReadOnly version As Byte
    Private ReadOnly hardware As Byte
    ' номер платы в новой системе; 0 - неизвестен (старый загрузчик v5 и MODEM сообщает 139)
    Private target As Byte
    Public Sub New(comport As ClComPort, portName As String, bootVersion As Byte, board As Byte, Optional targetHw As Byte = 0)
        port = comport : version = bootVersion : hardware = board
        target = targetHw
        AutoScaleMode = AutoScaleMode.None : Theme.StyleForm(Me)
        Text = "Домовой · загрузчик" & If(Demo.IsOn, " · ДЕМО", "")
        StartPosition = FormStartPosition.CenterScreen
        Dim area = Screen.FromControl(Me).WorkingArea
        Size = New Size(Math.Min(Theme.Px(1280), area.Width), Math.Min(Theme.Px(800), area.Height))
        Dim root As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2}
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        root.Controls.Add(Theme.BoardHeader("Плата " & board, portName & " · загрузчик 0x" & version.ToString("X2")), 0, 0)
        AddHandler Shown, Sub() Theme.FitBoardWindow(Me)
        Dim pages As New TabControl()
        Dim device As New TabPage("Устройство") With {.BackColor = Theme.WindowBack, .AutoScroll = True}
        pages.TabPages.Add(device)
        Dim upload As New Button With {.Text = "Записать прошивку", .Width = Theme.Px(250)}
        Theme.Primary(upload)
        AddHandler upload.Click, AddressOf UploadFirmware
        Dim close As New Button With {.Text = "Закрыть", .Width = Theme.Px(120)}
        Theme.Secondary(close) : AddHandler close.Click, Sub() Me.Close()
        Dim old = version < clBoot.OLD_BOOT_BELOW
        Dim description As New Label With {.AutoSize = True, .Dock = DockStyle.Fill, .Font = Theme.FontBody,
            .Text = If(old, "Шаг 1 из 2: записать на плату новую прошивку. Нажмите «Записать прошивку» — программа сама возьмёт файл и проведёт по шагам. " &
                "Потом нужно будет выключить и включить питание платы и подключиться к ней — программа предложит шаг 2.",
                "Плата в режиме загрузки: прошивки на ней нет или она не запустилась. Нажмите «Записать прошивку» — программа сама возьмёт файл и проведёт по шагам.")}
        device.Controls.Add(Theme.CardPage(Theme.PageHeading("Устройство", If(old, "Перевод на новую систему", "Восстановление прошивки")),
            Theme.Card("Прошивка и загрузчик", description,
                New Label With {.Text = "Загрузчик 0x" & version.ToString("X2") & " · плата " & board &
                    ". Версия приложения в режиме загрузчика недоступна.", .AutoSize = True, .Font = Theme.FontCaption,
                    .ForeColor = Theme.TextMuted, .Dock = DockStyle.Fill, .Margin = New Padding(0, Theme.GapRelated, 0, Theme.GapSection)}, upload), close))
        root.Controls.Add(Theme.MainNavigation(pages), 0, 1) : Controls.Add(root)
    End Sub

    ' Файл берём сами (свежая сборка для этой платы рядом с программой),
    ' в окне записи его можно поменять кнопкой «Выбрать другой файл».
    Private Sub UploadFirmware(sender As Object, e As EventArgs)
        If target = 0 AndAlso hardware = &H8B Then
            ' старый загрузчик сообщает 139 и на v5, и на старой плате MODEM
            Using ask As New frmInfo("Какая это плата?",
                "Загрузчик не говорит, какая это плата." & vbCrLf &
                "Если на плате написано NEXT v5 — нажмите «Плата v5»." & vbCrLf &
                "Если это старая плата (ДОМОВОЙ IP, v1–v3) — нажмите «Старая плата».", , True)
                ask.Button1.Text = "Плата v5"
                ask.Button2.Text = "Старая плата"
                target = If(ask.ShowDialog(Me) = DialogResult.OK, CByte(139), CByte(140))
            End Using
        End If
        ' 140 - признак старой платы MODEM: номер платы у неё тоже 139, сборка - из её папки
        Dim oldModem = target = 140
        Dim firmwareHw = If(target = 0 OrElse target = 140, hardware, target)
        Dim file = FirmwareFiles.FindFirmware(firmwareHw, oldModem)
        If file Is Nothing Then
            Using dialog As New OpenFileDialog With {.Filter = "Файлы прошивки (*.bin)|*.bin|Все файлы (*.*)|*.*",
                .InitialDirectory = AppDomain.CurrentDomain.BaseDirectory}
                If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
                file = dialog.FileName
                FirmwareFiles.LastFolder = IO.Path.GetDirectoryName(file)
            End Using
        End If
        ' frmFW оставляет существующие проверки файла и подтверждение записи.
        Using transfer As New frmFW(file, port, version, firmwareHw)
            If transfer.ShowDialog(Me) <> DialogResult.OK Then Return
        End Using
        If Demo.IsOn AndAlso version < clBoot.OLD_BOOT_BELOW Then Demo.LegacyFwStarted = True
        DialogResult = DialogResult.OK : Close()
    End Sub
End Class
