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
        txtOldPIN.Focus()
    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click

        ' Длину проверяем до перевода в число: на коротком коде BitConverter
        ' читал за концом массива и ронял программу.
        If Not PinFormat.IsValid(txtOldPIN.Text) Then
            ShowError("Текущий ПИН-код — 4 или 5 цифр.", txtOldPIN)
            Exit Sub
        End If

        If Not PinFormat.Matches(txtOldPIN.Text, CUInt(pin)) Then
            ShowError("Текущий ПИН-код не совпадает.", txtOldPIN)
            Exit Sub
        End If

        If Not PinFormat.IsValid(txtNewPIN.Text) Then
            ShowError("Новый ПИН-код — 4 или 5 цифр.", txtNewPIN)
            Exit Sub
        End If

        If txtNewPIN.Text <> txtNewPIN2.Text Then
            ShowError("Второй раз ПИН-код введён иначе.", txtNewPIN2)
            Exit Sub
        End If

        nPin = CInt(PinFormat.ToStored(txtNewPIN.Text))
        DialogResult = DialogResult.OK
        Me.Close()
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub

    ' Ошибка стоит в самом окне, рядом с полем, а не отдельным сообщением поверх.
    Private Sub ShowError(ByVal text As String, ByVal focusOn As TextBox)
        lblError.Text = text
        focusOn.Focus()
        focusOn.SelectAll()
    End Sub

    ' Только цифры - но и с основного ряда клавиш, а не с цифровой части одной.
    Private Sub txtPIN_KeyPress(sender As Object, e As KeyPressEventArgs) Handles txtOldPIN.KeyPress, txtNewPIN.KeyPress, txtNewPIN2.KeyPress
        If Char.IsControl(e.KeyChar) Then Exit Sub
        If Not Char.IsDigit(e.KeyChar) Then e.Handled = True
    End Sub

End Class