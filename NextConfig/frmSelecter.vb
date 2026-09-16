Public Class frmSelecter

    Public ID As Integer
    Public TextValue As String
    Public SelectedItems As Integer

    Private items As ArrayList
    Private clmn = 3, row As Integer = 3
    Private fixed As Boolean = False

    Public Structure mItem
        Dim ID As Long
        Dim Name As String
        Dim ButtonBackColor As Color
        Dim ButtonForeColor As Color
    End Structure

    Public Sub New(ByVal _title As String, ByVal _items As ArrayList, ByVal _clmn As Integer,
                   Optional ByVal font_size As Single = 28,
                   Optional ByVal _fixed As Boolean = False)

        ' Этот вызов необходим конструктору форм Windows.
        InitializeComponent()

        ' Добавьте все инициализирующие действия после вызова InitializeComponent().
        Title.Text = _title
        items = _items
        clmn = _clmn
        row = Math.Ceiling(items.Count / clmn)

        Dim dx As Integer = 20
        Dim dy As Integer = 20
        Dim width, height As Integer
        Dim start_pos_y As Integer = 20 + Title.Height
        Dim start_pos_x As Integer = 20
        Dim pos_x = start_pos_x, pos_y As Integer = start_pos_y
        Dim fnt As Font = New Font(Me.Font.Name, font_size, FontStyle.Bold)

        width = (Me.Width - dx * (clmn + 1)) / clmn
        height = (Me.Height - dy * (row + 1) - start_pos_y) / row

        For i As Integer = 0 To items.Count - 1
            'строки
            Dim btn As New Button With {.Width = width, .Height = height,
                                                   .Text = CType(items(i), mItem).Name,
                                                   .Tag = CType(items(i), mItem).ID,
                                                   .Location = New Point(pos_x, pos_y),
                                                   .Font = fnt}
            Dim obj As Object = CType(items(i), mItem).ButtonBackColor

            If obj IsNot Nothing Then
                btn.BackColor = obj
            End If

            obj = CType(items(i), mItem).ButtonForeColor

            If obj IsNot Nothing Then
                btn.ForeColor = obj
            End If

            AddHandler btn.Click, AddressOf Button_Click

            Me.Controls.Add(btn)

            pos_x += btn.Width + dx

            If pos_x > Me.Width - btn.Width Then
                pos_x = dx
                pos_y += btn.Height + dy
            End If
        Next

        fixed = _fixed
        SelectedItems = 0

    End Sub

    Private Sub Button_Click(ByVal sender As System.Object, ByVal e As System.EventArgs)
        ID = CType(sender, Button).Tag
        TextValue = CType(sender, Button).Text

        If fixed = False Then
            Me.Close()
        Else
            If ID = -1 Then
                Me.Close()
            Else
                Dim btn As Button = sender

                If (SelectedItems And (1 << ID)) = (1 << ID) Then
                    Dim obj As Object = CType(items(ID), mItem).ButtonBackColor

                    If obj IsNot Nothing Then
                        btn.BackColor = obj
                    End If

                    SelectedItems = SelectedItems And Not (1 << ID)
                Else
                    SelectedItems = SelectedItems Or (1 << ID)

                    btn.BackColor = Color.Green
                End If
            End If
        End If
    End Sub

End Class