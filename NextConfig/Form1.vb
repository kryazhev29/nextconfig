Imports System.Globalization
Imports System.Threading
Imports System.IO
Imports System.Drawing.Drawing2D

Module Module1

    Public ReadOnly provider_pset As New NumberFormatInfo() With {.NumberDecimalSeparator = "."}
    Public ReadOnly provider_comma As New NumberFormatInfo() With {.NumberDecimalSeparator = ","}

    'Public Class RoundButton
    '    Inherits Button

    '    Public Sub New()
    '        MyBase.New
    '    End Sub

    '    Protected Overrides Sub OnPaint(ByVal e As System.Windows.Forms.PaintEventArgs)
    '        Dim grPath As GraphicsPath = New GraphicsPath()

    '        grPath.AddEllipse(0, 0, ClientSize.Width, ClientSize.Height)
    '        Me.Region = New System.Drawing.Region(grPath)
    '        MyBase.OnPaint(e)
    '    End Sub

    'End Class

    Public Function UnixTimestampToDateTime(ByVal _UnixTimeStamp As Long) As DateTime
        Return (New DateTime(1970, 1, 1, 0, 0, 0)).AddSeconds(_UnixTimeStamp)
    End Function

    Public Function DateTimeToUnixTimestamp(ByVal _DateTime As DateTime) As Long
        Dim _UnixTimeSpan As TimeSpan = (_DateTime.Subtract(New DateTime(1970, 1, 1, 0, 0, 0)))
        Return CLng(Fix(_UnixTimeSpan.TotalSeconds))
    End Function

End Module

Public Class Form1

    Private WithEvents opros As clOpros
    Private isFinded As Boolean = False

    Public Sub New()
        InitializeComponent()
    End Sub

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load

        opros = New clOpros(Me)

        AddHandler opros.isFinded, AddressOf opros_isFinded
        AddHandler opros.onFind, AddressOf opros_onFind
        AddHandler opros.onBootFound, AddressOf opros_onBootFound

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

    Private Sub btnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
        Me.Close()
    End Sub

    Private Sub btnConnect_Click(sender As Object, e As EventArgs) Handles btnConnect.Click
        Dim bt() As Byte = System.Text.Encoding.UTF8.GetBytes(txtPIN.Text)
        Dim pin As Integer = BitConverter.ToUInt32(bt, 0)

        If opros.Autorize(pin) Then
            Me.Hide()
            opros.SetRegim(clOpros.eRejim.Work)
            Using f2 As New Form2(opros, lblPortInfo.Text, txtIMEI.Text, pin)
                If f2.ShowDialog() = DialogResult.Abort Then
                    Me.Close()
                    Exit Sub
                End If
            End Using
            lblPortInfo.Text = ""
            btnConnect.Enabled = False
            opros.SetRegim(clOpros.eRejim.Search)
            Me.Show()
        Else
            Using f As New frmInfo("Информация", "Проверьте правильность ПИН кода!")
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
        lblPortInfo.Text = String.Format("({0} - Соединен)", port)
        txtIMEI.Text = imei
        If isDefPIN > 0 Then
            txtPIN.Text = "4321"
            btnConnect.Enabled = True
        End If
        isFinded = True
    End Sub

    Private Sub lblDefault_Click(sender As Object, e As EventArgs) Handles lblDefault.Click
        txtPIN.Text = "4321"
    End Sub

    Private Delegate Sub delegate_onFind(ByVal port As String)

    Private Sub opros_onFind(ByVal port As String)
        Dim UIUpdater As New delegate_onFind(AddressOf refOnFind)
        Me.BeginInvoke(UIUpdater, port)
    End Sub

    Private Sub refOnFind(ByVal port As String)
        txtIMEI.Text = ""
        If port = "" Then
            lblPortInfo.Text = "(Нет доступных портов)"
        Else
            lblPortInfo.Text = String.Format("({0} - Поиск устройств)", port)
        End If
    End Sub

    Private Delegate Sub delegate_onBootFound(ByVal port As String, ByVal bootVer As Byte, ByVal hw As Byte)

    ' На порту ответил загрузчик: приложения на плате нет или его держат кнопкой.
    Private Sub opros_onBootFound(ByVal port As String, ByVal bootVer As Byte, ByVal hw As Byte)
        Dim UIUpdater As New delegate_onBootFound(AddressOf refOnBootFound)
        Me.BeginInvoke(UIUpdater, port, bootVer, hw)
    End Sub

    Private Sub refOnBootFound(ByVal port As String, ByVal bootVer As Byte, ByVal hw As Byte)
        txtIMEI.Text = ""
        lblPortInfo.Text = String.Format("({0} - загрузчик 0x{1}, плата {2})", port, bootVer.ToString("X2"), hw)

        Using f As New frmInfo("Загрузчик", "На порту " & port & " отвечает загрузчик 0x" & bootVer.ToString("X2") &
                               ", плата " & hw & "." & vbCrLf &
                               "Прошивки на плате нет или её держат кнопкой." & vbCrLf &
                               "Записать прошивку?", , True)
            f.Button1.Text = "Да"
            f.Button2.Text = "Нет"

            If f.ShowDialog() <> DialogResult.OK Then
                opros.SetRegim(clOpros.eRejim.Search)
                Exit Sub
            End If
        End Using

        Using ofd As New OpenFileDialog()
            ofd.Filter = "Файлы прошивки (*.bin)|*.bin|Все файлы (*.*)|*.*"
            ofd.FileName = hw & ".FW.БИЛД.bin"

            If ofd.ShowDialog() = DialogResult.OK Then
                Using f As New frmBoot(ofd.FileName, opros.GetComPort)
                    f.ShowDialog()
                End Using
            End If
        End Using

        opros.SetRegim(clOpros.eRejim.Search)
    End Sub

    Private Sub txtPIN_TextChanged(sender As Object, e As EventArgs) Handles txtPIN.TextChanged
        If txtPIN.Text.Length = 4 Then
            btnConnect.Enabled = True
        Else
            btnConnect.Enabled = False
        End If
    End Sub

    Private Sub txtPIN_KeyDown(sender As Object, e As KeyEventArgs) Handles txtPIN.KeyDown
        If e.KeyCode = Keys.NumPad1 Or e.KeyCode = Keys.NumPad2 Or
               e.KeyCode = Keys.NumPad3 Or e.KeyCode = Keys.NumPad4 Or
               e.KeyCode = Keys.NumPad5 Or e.KeyCode = Keys.NumPad6 Or
               e.KeyCode = Keys.NumPad7 Or e.KeyCode = Keys.NumPad8 Or
               e.KeyCode = Keys.NumPad9 Or e.KeyCode = Keys.NumPad0 Or
               e.KeyCode = Keys.Back Or e.KeyCode = Keys.Delete Then
            e.SuppressKeyPress = False
        Else
            e.SuppressKeyPress = True
        End If
    End Sub

End Class