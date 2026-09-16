Imports System.Globalization

Public Class frmText

    Public vText As String

    Public Sub New(ByVal _title As String, ByVal _lbl As String, ByVal _txt As String)

        ' Этот вызов является обязательным для конструктора.
        InitializeComponent()

        ' Добавить код инициализации после вызова InitializeComponent()
        lblTitle.Text = _title
        Label5.Text = _lbl
        vText = _txt
        txtOldPIN.Text = vText
    End Sub

    Private Sub Form3_Load(sender As Object, e As EventArgs) Handles MyBase.Load

    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        vText = txtOldPIN.Text
        DialogResult = DialogResult.OK
        Me.Close()
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub

End Class