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
    Private isFinded As Boolean = False
    Private curPort As String = ""

    Public Sub New()
        InitializeComponent()
    End Sub

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load

        If Demo.IsOn Then Me.Text = "Домовой NEXT — демонстрационный режим"

        AgentLog.WatchText(lblStatus, "form1.status")
        AgentLog.WatchText(lblReason, "form1.reason")
        opros = New clOpros(Me)

        AddHandler opros.isFinded, AddressOf opros_isFinded
        AddHandler opros.onFind, AddressOf opros_onFind
        AddHandler opros.onBootFound, AddressOf opros_onBootFound
        AddHandler opros.onOldFirmwareFound, AddressOf opros_onOldFirmwareFound

        UpdateConnectState()

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

    ' Кнопка «Подключиться» доступна только когда есть всё нужное.
    ' Рядом пишем, чего именно не хватает: цвет сам по себе этого не объясняет.
    Private Sub UpdateConnectState()
        Dim reason As String = ""

        If Not isFinded Then
            reason = "Устройство не найдено. Проверьте кабель и питание платы."
        ElseIf txtIMEI.Text.Trim() = "" Then
            reason = "Не заполнен серийный номер."
        ElseIf txtPIN.Text.Length <> 4 Then
            reason = "ПИН-код состоит из четырёх цифр."
        End If

        btnConnect.Enabled = (reason = "")
        lblReason.Text = reason
    End Sub

    Private Sub btnConnect_Click(sender As Object, e As EventArgs) Handles btnConnect.Click
        Dim bt() As Byte = System.Text.Encoding.UTF8.GetBytes(txtPIN.Text)
        Dim pin As Integer = BitConverter.ToUInt32(bt, 0)

        SetStatus(Theme.eDot.Waiting, "Подключение…")
        btnConnect.Enabled = False
        statusRow.Refresh()

        If opros.Autorize(pin) Then
            Me.Hide()
            opros.SetRegim(clOpros.eRejim.Work)
            Using f2 As New Form2(opros, curPort, txtIMEI.Text, pin)
                If f2.ShowDialog() = DialogResult.Abort Then
                    Me.Close()
                    Exit Sub
                End If
            End Using
            isFinded = False
            txtIMEI.Text = ""
            SetStatus(Theme.eDot.Waiting, "Поиск устройств…")
            UpdateConnectState()
            opros.SetRegim(clOpros.eRejim.Search)
            Me.Show()
        Else
            SetStatus(Theme.eDot.Bad, "ПИН-код не принят")
            btnConnect.Enabled = True
            Using f As New frmInfo("Подключение", "Проверьте правильность ПИН кода!")
                f.ShowDialog()
            End Using
        End If
    End Sub

    Private Delegate Sub delegate_isFinded(ByVal port As String, ByVal imei As String, ByVal isDefPIN As Byte)

    Private Sub opros_isFinded(ByVal port As String, ByVal imei As String, ByVal isDefPIN As Byte)
        Dim UIUpdater As New delegate_isFinded(AddressOf refIsFinded)
        Me.BeginInvoke(UIUpdater, port, imei, isDefPIN)
    End Sub

    Private Sub refIsFinded(ByVal port As String, ByVal imei As String, ByVal isDefPIN As Byte)
        curPort = port
        isFinded = True
        AgentLog.Write("device", "kind", "next", "port", port, "imei", imei, "defaultPin", isDefPIN > 0)
        SetStatus(Theme.eDot.Good, "Устройство найдено · " & port)
        txtIMEI.Text = imei
        If isDefPIN > 0 Then
            txtPIN.Text = "4321"
        End If
        UpdateConnectState()
    End Sub

    Private Sub lnkDefault_LinkClicked(sender As Object, e As LinkLabelLinkClickedEventArgs) Handles lnkDefault.LinkClicked
        txtPIN.Text = "4321"
        txtPIN.Focus()
        txtPIN.SelectionStart = txtPIN.Text.Length
    End Sub

    Private Delegate Sub delegate_onFind(ByVal port As String)

    Private Sub opros_onFind(ByVal port As String)
        Dim UIUpdater As New delegate_onFind(AddressOf refOnFind)
        Me.BeginInvoke(UIUpdater, port)
    End Sub

    Private Sub refOnFind(ByVal port As String)
        txtIMEI.Text = ""
        isFinded = False
        curPort = port

        If port = "" Then
            SetStatus(Theme.eDot.Bad, "Нет доступных портов")
        Else
            SetStatus(Theme.eDot.Waiting, "Поиск устройств… · " & port)
        End If

        UpdateConnectState()
    End Sub

    Private Delegate Sub delegate_onBootFound(ByVal port As String, ByVal bootVer As Byte, ByVal hw As Byte)

    Private Sub opros_onOldFirmwareFound(port As String, identity As Byte())
        If IsDisposed OrElse Disposing Then Return
        AgentLog.Write("device", "kind", "oldfw", "port", port, "fw", CInt(identity(5)), "boot", "0x" & identity(3).ToString("X2"), "hw", CInt(identity(4)))
        BeginInvoke(New Action(Sub()
                                   isFinded = False
                                   UpdateConnectState()
                                   Hide()
                                   Using f As New frmOldFirmware(New OldFirmware(opros.GetComPort, identity, Demo.IsOn), port)
                                       f.ShowDialog(Me)
                                       If f.EnteredBootloader Then
                                           Show()
                                           SetStatus(Theme.eDot.Waiting, "Поиск загрузчика после перезапуска…")
                                           opros.FindAfterLegacyReset()
                                           Return
                                       End If
                                   End Using
                                   ' Закрытие просмотра завершает сеанс, без повторного сканирования портов.
                                   If opros.GetComPort.Port.IsOpen Then opros.GetComPort.Port.Close()
                                   Close()
                               End Sub))
    End Sub

    ' На порту ответил загрузчик: приложения на плате нет или его держат кнопкой.
    Private Sub opros_onBootFound(ByVal port As String, ByVal bootVer As Byte, ByVal hw As Byte)
        Dim UIUpdater As New delegate_onBootFound(AddressOf refOnBootFound)
        Me.BeginInvoke(UIUpdater, port, bootVer, hw)
    End Sub

    Private Sub refOnBootFound(ByVal port As String, ByVal bootVer As Byte, ByVal hw As Byte)
        txtIMEI.Text = ""
        isFinded = False
        curPort = port
        AgentLog.Write("device", "kind", "bootloader", "port", port, "boot", "0x" & bootVer.ToString("X2"), "hw", CInt(hw))
        SetStatus(Theme.eDot.Waiting, "Загрузчик 0x" & bootVer.ToString("X2") & " · плата " & hw & " · " & port)
        UpdateConnectState()

        Hide()
        Using device As New frmBootDevice(opros.GetComPort, port, bootVer, hw)
            If device.ShowDialog(Me) <> DialogResult.OK Then
                Close() : Return
            End If
        End Using
        Show()
        opros.SetRegim(clOpros.eRejim.Search)
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
