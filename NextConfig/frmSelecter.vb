Public Class frmSelecter

    Public ID As Integer
    Public TextValue As String
    Public SelectedItems As Integer

    Private items As ArrayList
    Private clmn As Integer = 3
    Private fixed As Boolean = False

    Public Structure mItem
        Dim ID As Long
        Dim Name As String
        Dim ButtonBackColor As Color
        Dim ButtonForeColor As Color
    End Structure

    ' font_size остаётся в подписи ради прежних вызовов: размер шрифта теперь
    ' один на всю программу и берётся из оформления.
    Public Sub New(ByVal _title As String, ByVal _items As ArrayList, ByVal _clmn As Integer,
                   Optional ByVal font_size As Single = 28,
                   Optional ByVal _fixed As Boolean = False)

        InitializeComponent()

        Title.Text = _title
        items = _items
        clmn = Math.Max(1, _clmn)
        fixed = _fixed
        SelectedItems = 0
        ID = -1

        BuildItems()
    End Sub

    Private Sub BuildItems()
        Dim rows As Integer = CInt(Math.Ceiling(items.Count / clmn))

        items_grid.ColumnCount = clmn
        items_grid.RowCount = rows

        For i As Integer = 1 To clmn
            items_grid.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        Next

        For i As Integer = 0 To items.Count - 1
            Dim it As mItem = CType(items(i), mItem)

            ' «Отмена» в списке вариантов не нужна: у окна своя кнопка внизу
            If it.Name = "Отмена" Then Continue For

            Dim btn As New Button()
            btn.Text = it.Name
            btn.Tag = it.ID
            btn.Width = 200
            btn.Margin = New Padding(0, 0, Theme.GapRelated, Theme.GapRelated)
            Theme.Secondary(btn)

            AddHandler btn.Click, AddressOf Button_Click

            items_grid.Controls.Add(btn)
        Next
    End Sub

    Private Sub Button_Click(ByVal sender As System.Object, ByVal e As System.EventArgs)
        Dim btn As Button = CType(sender, Button)

        ID = CInt(btn.Tag)
        TextValue = btn.Text

        If Not fixed Then
            DialogResult = DialogResult.OK
            Me.Close()
            Exit Sub
        End If

        ' режим отметки нескольких значений: выбранное закрашено
        If (SelectedItems And (1 << ID)) = (1 << ID) Then
            SelectedItems = SelectedItems And Not (1 << ID)
            Theme.Secondary(btn)
        Else
            SelectedItems = SelectedItems Or (1 << ID)
            Theme.Primary(btn)
        End If
    End Sub

    ' Отмена - обычное действие, а не опасное: нейтральная кнопка внизу справа.
    Private Sub btnCancel_Click(sender As Object, e As EventArgs) Handles btnCancel.Click
        ID = CancelId()
        DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub

    ' Прежние вызовы различают отмену по своему номеру: 0 или -1.
    Private Function CancelId() As Integer
        For i As Integer = 0 To items.Count - 1
            Dim it As mItem = CType(items(i), mItem)
            If it.Name = "Отмена" Then Return CInt(it.ID)
        Next

        Return -1
    End Function

End Class
