Imports System.Globalization

Public Class frmPIN

    Private pin As Integer
    Public nPin As Integer

    Public Sub New(ByVal _pin As Integer)

        ' Этот вызов является обязательным для конструктора.
        InitializeComponent()

        ' Добавить код инициализации после вызова InitializeComponent().
        pin = _pin
        nPin = pin

    End Sub

    Private Sub Form3_Load(sender As Object, e As EventArgs) Handles MyBase.Load

    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        Dim bt() As Byte = System.Text.Encoding.UTF8.GetBytes(txtOldPIN.Text)
        Dim oldPin As Integer = BitConverter.ToUInt32(bt, 0)

        If oldPin <> pin Then
            Using f As New frmInfo("Смена ПИН кода", "Проверьте правильность ввода текущего ПИН кода!")
                f.ShowDialog()
            End Using
            Exit Sub
        End If

        If txtNewPIN.Text <> txtNewPIN2.Text Then
            Using f As New frmInfo("Смена ПИН кода", "Проверьте правильность ввода нового ПИН кода!")
                f.ShowDialog()
            End Using
            Exit Sub
        End If

        If txtNewPIN.Text = String.Empty Then
            Using f As New frmInfo("Смена ПИН кода", "Вы не ввели новый ПИН код!")
                f.ShowDialog()
            End Using
            Exit Sub
        End If

        bt = System.Text.Encoding.UTF8.GetBytes(txtNewPIN.Text)
        nPin = BitConverter.ToUInt32(bt, 0)
        DialogResult = DialogResult.OK
        Me.Close()
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub

    Private Sub txtPIN_KeyDown(sender As Object, e As KeyEventArgs) Handles txtOldPIN.KeyDown, txtNewPIN.KeyDown, txtNewPIN2.KeyDown
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