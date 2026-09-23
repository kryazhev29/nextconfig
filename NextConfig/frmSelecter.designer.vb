<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class frmSelecter
    Inherits System.Windows.Forms.Form

    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    Private components As System.ComponentModel.IContainer

    ' Выбор из нескольких значений. Тот же образец, что у остальных диалогов:
    ' заголовок, варианты, «Отмена» внизу справа.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Me.root = New System.Windows.Forms.TableLayoutPanel()
        Me.Title = New System.Windows.Forms.Label()
        Me.items_grid = New System.Windows.Forms.TableLayoutPanel()
        Me.buttons = New System.Windows.Forms.FlowLayoutPanel()
        Me.btnCancel = New System.Windows.Forms.Button()
        Me.root.SuspendLayout()
        Me.buttons.SuspendLayout()
        Me.SuspendLayout()

        Me.Title.AutoSize = True
        Me.Title.Name = "Title"
        Me.Title.Text = "Выбор"
        Me.Title.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapBlock)
        Theme.Title(Me.Title)

        Me.items_grid.AutoSize = True
        Me.items_grid.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.items_grid.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapSection)
        Me.items_grid.Name = "items_grid"

        Me.btnCancel.Name = "btnCancel"
        Me.btnCancel.Text = "Отмена"
        Me.btnCancel.Width = Theme.Px(148)
        Me.btnCancel.Margin = New System.Windows.Forms.Padding(0)
        Theme.Secondary(Me.btnCancel)

        Me.buttons.AutoSize = True
        Me.buttons.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.buttons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft
        Me.buttons.WrapContents = False
        Me.buttons.Margin = New System.Windows.Forms.Padding(0)
        Me.buttons.Anchor = System.Windows.Forms.AnchorStyles.Right
        Me.buttons.Controls.Add(Me.btnCancel)

        Me.root.ColumnCount = 1
        Me.root.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100.0!))
        Me.root.AutoSize = True
        Me.root.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.root.Dock = System.Windows.Forms.DockStyle.Fill
        Me.root.Padding = New System.Windows.Forms.Padding(Theme.GapSection)
        Me.root.Controls.Add(Me.Title)
        Me.root.Controls.Add(Me.items_grid)
        Me.root.Controls.Add(Me.buttons)

        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None
        Me.AutoSize = True
        Me.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.ClientSize = New System.Drawing.Size(Theme.Px(460), Theme.Px(260))
        Me.Controls.Add(Me.root)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "frmSelecter"
        Me.ShowInTaskbar = False
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "Домовой NEXT"
        Me.CancelButton = Me.btnCancel
        Theme.StyleForm(Me)
        Theme.DialogLayout(Me, Me.root, Me.buttons)

        Me.buttons.ResumeLayout(False)
        Me.root.ResumeLayout(False)
        Me.root.PerformLayout()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents root As TableLayoutPanel
    Friend WithEvents Title As Label
    Friend WithEvents items_grid As TableLayoutPanel
    Friend WithEvents buttons As FlowLayoutPanel
    Friend WithEvents btnCancel As Button
End Class
