Public Class frmInfo
    Public Sub New(ByVal title As String, ByVal txt As String,
                   Optional ByVal align As ContentAlignment = ContentAlignment.MiddleLeft,
                   Optional ByVal isDialog As Boolean = False)

        ' Этот вызов необходим конструктору форм Windows.
        InitializeComponent()

        ' Добавьте все инициализирующие действия после вызова InitializeComponent().
        lblTitle.Text = title
        info.Text = txt
        info.TextAlign = align
        AgentLog.Message(title, txt, isDialog)

        If isDialog Then
            Button2.Visible = True
        Else
            ' одна кнопка: Escape тоже должен закрывать окно
            Me.CancelButton = Button1
            Button1.Text = "Понятно"
        End If

    End Sub

    Private Sub Button1_Click_1(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button1.Click
        DialogResult = DialogResult.OK
        Me.Close()
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub
End Class