Imports System.Globalization
Imports System.Threading
Imports System.IO
Imports System.Drawing.Drawing2D

Module Module1

    Public ReadOnly provider_pset As New NumberFormatInfo() With {.NumberDecimalSeparator = "."}
    Public ReadOnly provider_comma As New NumberFormatInfo() With {.NumberDecimalSeparator = ","}

    Public Function UnixTimestampToDateTime(ByVal _UnixTimeStamp As Long) As DateTime
        Return (New DateTime(1970, 1, 1, 0, 0, 0)).AddSeconds(_UnixTimeStamp)
    End Function

    ' Билд прошивки - время сборки в секундах от 1970 (UTC), как в имени файла HW.FW.БИЛД.bin.
    ' Показываем дату и время по часам компьютера, число - в скобках для сверки с именем файла.
    Public Function BuildText(ByVal build As Long) As String
        Dim t As DateTime = DateTime.SpecifyKind(UnixTimestampToDateTime(build), DateTimeKind.Utc).ToLocalTime()
        Return t.ToString("dd.MM.yyyy HH:mm") & " (" & build & ")"
    End Function

    Public Function DateTimeToUnixTimestamp(ByVal _DateTime As DateTime) As Long
        Dim _UnixTimeSpan As TimeSpan = (_DateTime.Subtract(New DateTime(1970, 1, 1, 0, 0, 0)))
        Return CLng(Fix(_UnixTimeSpan.TotalSeconds))
    End Function

End Module

Public Class Form1

    Private WithEvents opros As clOpros

    ' Все платы последнего прохода по портам. Плату выбирает оператор,
    ' программа сама ни одну не открывает.
    Private ReadOnly lstDevices As New ListBox()
    Private ReadOnly lblDevicesCap As New Label()
    Private devices As New List(Of clOpros.FoundDevice)
    Private lastScanText As String = ""

    Public Sub New()
        InitializeComponent()
    End Sub

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load

        If Demo.IsOn Then Me.Text = "Домовой NEXT — демонстрационный режим"

        BuildDeviceList()
        AgentLog.WatchText(lblStatus, "form1.status")
        AgentLog.WatchText(lblReason, "form1.reason")
        opros = New clOpros(Me)

        AddHandler opros.onScan, AddressOf opros_onScan

        UpdateConnectState()

    End Sub

    ' Список плат встаёт над полем серийного номера.
    Private Sub BuildDeviceList()
        lblDevicesCap.AutoSize = True
        lblDevicesCap.Name = "lblDevicesCap"
        lblDevicesCap.Text = "Найденные платы"
        lblDevicesCap.Margin = New Padding(0, 0, 0, Theme.GapLabel)
        Theme.Caption(lblDevicesCap)

        lstDevices.Name = "lstDevices"
        lstDevices.Font = Theme.FontBody
        lstDevices.BorderStyle = BorderStyle.FixedSingle
        lstDevices.IntegralHeight = False
        lstDevices.Size = New Size(Theme.Px(FieldWidth), Theme.Px(104))
        lstDevices.Margin = New Padding(0, 0, 0, Theme.GapRow)
        AddHandler lstDevices.SelectedIndexChanged, Sub() SelectionChanged()
        AddHandler lstDevices.DoubleClick, Sub()
                                               If btnConnect.Enabled Then btnConnect.PerformClick()
                                           End Sub

        card.Controls.Add(lblDevicesCap)
        card.Controls.Add(lstDevices)
        Dim at = card.Controls.GetChildIndex(lblSerialCap)
        card.Controls.SetChildIndex(lblDevicesCap, at)
        card.Controls.SetChildIndex(lstDevices, at + 1)
        lblSubtitle.Text = "Выберите плату и подключитесь"
        ClientSize = New Size(ClientSize.Width, ClientSize.Height + Theme.Px(150))
    End Sub

    Private Sub Form1_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        If opros IsNot Nothing Then
            opros.Ended()

            While opros.IsExit = False
                Thread.Sleep(500)
            End While
        End If
        GC.Collect()
    End Sub

    ' Состояние поиска устройства: точка и понятный текст, без технической строки в скобках.
    Private Sub SetStatus(ByVal state As Theme.eDot, ByVal text As String)
        Theme.Dot(lblDot, state)
        Theme.DotText(lblStatus, state, text)
    End Sub

    Private ReadOnly Property Selected As clOpros.FoundDevice
        Get
            Return TryCast(lstDevices.SelectedItem, clOpros.FoundDevice)
        End Get
    End Property

    ' Кнопка доступна только когда есть всё нужное.
    ' Рядом пишем, чего именно не хватает: цвет сам по себе этого не объясняет.
    Private Sub UpdateConnectState()
        Dim reason As String = ""
        Dim dev = Selected

        If devices.Count = 0 Then
            reason = "Плата не найдена. Проверьте кабель и питание платы."
        ElseIf dev Is Nothing Then
            reason = "Выберите плату в списке."
        ElseIf dev.Kind = clOpros.eDeviceKind.NextFw Then
            If txtIMEI.Text.Trim() = "" Then
                reason = "Не заполнен серийный номер."
            ElseIf txtPIN.Text.Length <> 4 Then
                reason = "ПИН-код состоит из четырёх цифр."
            End If
        End If

        btnConnect.Enabled = (reason = "")
        lblReason.Text = reason
    End Sub

    ' Для NEXT нужен вход по ПИН-коду; старая плата и загрузчик открываются без него.
    Private Sub SelectionChanged()
        Dim dev = Selected
        Dim isNext = dev IsNot Nothing AndAlso dev.Kind = clOpros.eDeviceKind.NextFw

        txtIMEI.Text = If(dev Is Nothing, "", dev.Imei)
        txtPIN.Enabled = isNext
        lnkDefault.Enabled = isNext
        btnConnect.Text = If(dev Is Nothing OrElse isNext, "Подключиться", "Открыть")
        If isNext AndAlso dev.DefaultPin AndAlso txtPIN.Text = "" Then txtPIN.Text = "4321"
        UpdateConnectState()
    End Sub

    Private Sub opros_onScan(found As List(Of clOpros.FoundDevice), portCount As Integer)
        If IsDisposed OrElse Disposing OrElse Not IsHandleCreated Then Return
        BeginInvoke(New Action(Sub() ShowScan(found, portCount)))
    End Sub

    ' Новый проход: список обновляется, выбор оператора сохраняется,
    ' если его плата никуда не делась.
    Private Sub ShowScan(found As List(Of clOpros.FoundDevice), portCount As Integer)
        Dim text = String.Join("; ", found.Select(Function(d) d.Title))
        If text <> lastScanText Then
            lastScanText = text
            AgentLog.Write("scan", "ports", portCount, "devices", found.Select(Function(d) d.Title).ToList())
        End If

        Dim keep = If(Selected Is Nothing, "", Selected.Key)
        Dim same = devices.Select(Function(d) d.Key).SequenceEqual(found.Select(Function(d) d.Key))
        devices = found

        If Not same Then
            lstDevices.BeginUpdate()
            lstDevices.Items.Clear()
            For Each d In found
                lstDevices.Items.Add(d)
            Next
            Dim idx = found.FindIndex(Function(d) d.Key = keep)
            ' одна плата - выбрана сразу: выбор ничего не открывает
            If idx < 0 AndAlso found.Count = 1 Then idx = 0
            lstDevices.SelectedIndex = idx
            lstDevices.EndUpdate()
            SelectionChanged()
        End If

        If portCount = 0 Then
            SetStatus(Theme.eDot.Bad, "Нет доступных портов")
        ElseIf found.Count = 0 Then
            SetStatus(Theme.eDot.Waiting, "Поиск плат… · портов: " & portCount)
        Else
            SetStatus(Theme.eDot.Good, "Найдено плат: " & found.Count)
        End If

        UpdateConnectState()
    End Sub

    Private Sub btnConnect_Click(sender As Object, e As EventArgs) Handles btnConnect.Click
        Dim dev = Selected
        If dev Is Nothing Then Return

        btnConnect.Enabled = False
        SetStatus(Theme.eDot.Waiting, "Подключение к " & dev.Port & "…")
        statusRow.Refresh()

        Dim errText As String = ""
        If Not opros.Open(dev, errText) Then
            SetStatus(Theme.eDot.Bad, "Порт не открылся")
            Using f As New frmInfo("Подключение", errText)
                f.ShowDialog()
            End Using
            UpdateConnectState()
            Return
        End If

        Select Case dev.Kind
            Case clOpros.eDeviceKind.NextFw : ConnectNext(dev)
            Case clOpros.eDeviceKind.OldFirmware : OpenOldFirmware(dev)
            Case Else : OpenBootloader(dev)
        End Select
    End Sub

    Private Sub ConnectNext(dev As clOpros.FoundDevice)
        Dim bt() As Byte = System.Text.Encoding.UTF8.GetBytes(txtPIN.Text)
        Dim pin As Integer = BitConverter.ToUInt32(bt, 0)

        If Not opros.Autorize(pin) Then
            opros.Release()
            SetStatus(Theme.eDot.Bad, "ПИН-код не принят")
            UpdateConnectState()
            Using f As New frmInfo("Подключение", "Проверьте правильность ПИН кода!")
                f.ShowDialog()
            End Using
            Return
        End If

        Me.Hide()
        opros.SetRegim(clOpros.eRejim.Work)
        Using f2 As New Form2(opros, dev.Port, txtIMEI.Text, pin)
            If f2.ShowDialog() = DialogResult.Abort Then
                Me.Close()
                Exit Sub
            End If
        End Using
        BackToList()
    End Sub

    Private Sub OpenOldFirmware(dev As clOpros.FoundDevice)
        Hide()
        Dim toBoot As Boolean
        Using f As New frmOldFirmware(New OldFirmware(opros.GetComPort, dev.Identity, Demo.IsOn), dev.Port)
            f.ShowDialog(Me)
            toBoot = f.EnteredBootloader
        End Using

        If toBoot Then
            ' плата перезапускается и появится в списке уже загрузчиком
            opros.FindAfterLegacyReset()
            ClearList()
            Show()
            SetStatus(Theme.eDot.Waiting, "Плата перезапускается в загрузчик. Выберите её в списке, когда появится")
            Return
        End If
        BackToList()
    End Sub

    Private Sub OpenBootloader(dev As clOpros.FoundDevice)
        Hide()
        Using device As New frmBootDevice(opros.GetComPort, dev.Port, dev.BootVer, dev.Hw)
            device.ShowDialog(Me)
        End Using
        BackToList()
    End Sub

    ' Окно платы закрыто: порт отпущен, снова ищем все платы.
    Private Sub BackToList()
        opros.Release()
        ClearList()
        Show()
        SetStatus(Theme.eDot.Waiting, "Поиск плат…")
    End Sub

    Private Sub ClearList()
        devices = New List(Of clOpros.FoundDevice)
        lastScanText = ""
        lstDevices.Items.Clear()
        txtPIN.Text = ""
        SelectionChanged()
    End Sub

    Private Sub lnkDefault_LinkClicked(sender As Object, e As LinkLabelLinkClickedEventArgs) Handles lnkDefault.LinkClicked
        txtPIN.Text = "4321"
        txtPIN.Focus()
        txtPIN.SelectionStart = txtPIN.Text.Length
    End Sub

    Private Sub txtIMEI_TextChanged(sender As Object, e As EventArgs) Handles txtIMEI.TextChanged
        UpdateConnectState()
    End Sub

    Private Sub txtPIN_TextChanged(sender As Object, e As EventArgs) Handles txtPIN.TextChanged
        UpdateConnectState()
    End Sub

    ' В поля номера и ПИН-кода пускаем только цифры - но и с основного ряда клавиш,
    ' а не только с цифровой части, как было раньше.
    Private Sub DigitsOnly_KeyPress(sender As Object, e As KeyPressEventArgs) Handles txtPIN.KeyPress, txtIMEI.KeyPress
        If Char.IsControl(e.KeyChar) Then Exit Sub
        If Not Char.IsDigit(e.KeyChar) Then e.Handled = True
    End Sub

End Class
