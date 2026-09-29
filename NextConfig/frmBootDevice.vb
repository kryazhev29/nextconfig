Public Class frmBootDevice
    Inherits Form
    Private ReadOnly port As ClComPort
    Private ReadOnly version As Byte
    Private ReadOnly hardware As Byte
    Public Sub New(comport As ClComPort, portName As String, bootVersion As Byte, board As Byte)
        port = comport : version = bootVersion : hardware = board
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
        Dim upload As New Button With {.Text = "Обновить прошивку", .Width = Theme.Px(250)}
        Theme.Primary(upload)
        AddHandler upload.Click, AddressOf UploadFirmware
        Dim close As New Button With {.Text = "Закрыть", .Width = Theme.Px(120)}
        Theme.Secondary(close) : AddHandler close.Click, Sub() Me.Close()
        Dim old = version < clBoot.OLD_BOOT_BELOW
        Dim description As New Label With {.AutoSize = True, .Dock = DockStyle.Fill, .Font = Theme.FontBody,
            .Text = If(old, "Шаг 1 из 2: записать новую прошивку через старый загрузчик. " &
                "После перезапуска войдите по ПИН-коду: NextConfig предложит шаг 2 — замену загрузчика на 0x32.",
                "Плата находится в загрузчике. Выберите файл прошивки для этой платы.")}
        device.Controls.Add(Theme.CardPage(Theme.PageHeading("Устройство", If(old, "Перевод на новую систему", "Восстановление прошивки")),
            Theme.Card("Прошивка и загрузчик", description,
                New Label With {.Text = "Загрузчик 0x" & version.ToString("X2") & " · плата " & board &
                    ". Версия приложения в режиме загрузчика недоступна.", .AutoSize = True, .Font = Theme.FontCaption,
                    .ForeColor = Theme.TextMuted, .Dock = DockStyle.Fill, .Margin = New Padding(0, Theme.GapRelated, 0, Theme.GapSection)}, upload), close))
        root.Controls.Add(Theme.MainNavigation(pages), 0, 1) : Controls.Add(root)
    End Sub

    Private Sub UploadFirmware(sender As Object, e As EventArgs)
        Using dialog As New OpenFileDialog With {.Filter = "Файлы прошивки (*.bin)|*.bin|Все файлы (*.*)|*.*",
            .FileName = hardware & ".FW.БИЛД.bin", .InitialDirectory = AppDomain.CurrentDomain.BaseDirectory}
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            ' frmFW оставляет существующие проверки файла и подтверждение записи.
            Using transfer As New frmFW(dialog.FileName, port, version)
                If transfer.ShowDialog(Me) <> DialogResult.OK Then Return
            End Using
        End Using
        If Demo.IsOn AndAlso version < clBoot.OLD_BOOT_BELOW Then Demo.LegacyFwStarted = True
        DialogResult = DialogResult.OK : Close()
    End Sub
End Class
