<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class frmInfo
    Inherits System.Windows.Forms.Form

    <System.Diagnostics.DebuggerNonUserCode()>
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

    ' Сообщение или вопрос. Один образец на все диалоги программы:
    ' заголовок, текст, кнопки внизу справа - главное действие последним.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Me.root = New System.Windows.Forms.TableLayoutPanel()
        Me.lblTitle = New System.Windows.Forms.Label()
        Me.info = New System.Windows.Forms.Label()
        Me.buttons = New System.Windows.Forms.FlowLayoutPanel()
        Me.Button1 = New System.Windows.Forms.Button()
        Me.Button2 = New System.Windows.Forms.Button()
        Me.root.SuspendLayout()
        Me.buttons.SuspendLayout()
        Me.SuspendLayout()

        Me.lblTitle.AutoSize = True
        Me.lblTitle.Name = "lblTitle"
        Me.lblTitle.Text = "Информация"
        Me.lblTitle.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapBlock)
        Theme.Title(Me.lblTitle)

        Me.info.AutoSize = True
        Me.info.Name = "info"
        Me.info.Text = ""
        Me.info.MaximumSize = New System.Drawing.Size(460, 0)
        Me.info.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapSection)
        Theme.Value(Me.info)

        Me.Button1.Name = "Button1"
        Me.Button1.Text = "Продолжить"
        Me.Button1.Width = 168
        Me.Button1.Margin = New System.Windows.Forms.Padding(Theme.GapRelated, 0, 0, 0)
        Theme.Primary(Me.Button1)

        Me.Button2.Name = "Button2"
        Me.Button2.Text = "Отмена"
        Me.Button2.Width = 148
        Me.Button2.Visible = False
        Me.Button2.Margin = New System.Windows.Forms.Padding(0)
        Theme.Secondary(Me.Button2)

        Me.buttons.AutoSize = True
        Me.buttons.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.buttons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft
        Me.buttons.WrapContents = False
        Me.buttons.Margin = New System.Windows.Forms.Padding(0)
        Me.buttons.Anchor = System.Windows.Forms.AnchorStyles.Right
        Me.buttons.Controls.Add(Me.Button1)
        Me.buttons.Controls.Add(Me.Button2)

        Me.root.ColumnCount = 1
        Me.root.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100.0!))
        Me.root.AutoSize = True
        Me.root.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.root.Dock = System.Windows.Forms.DockStyle.Fill
        Me.root.Padding = New System.Windows.Forms.Padding(Theme.GapSection)
        Me.root.Controls.Add(Me.lblTitle)
        Me.root.Controls.Add(Me.info)
        Me.root.Controls.Add(Me.buttons)

        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.AutoSize = True
        Me.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.ClientSize = New System.Drawing.Size(520, 200)
        Me.MinimumSize = New System.Drawing.Size(420, 180)
        Me.Controls.Add(Me.root)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "frmInfo"
        Me.ShowInTaskbar = False
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "Домовой NEXT"
        Me.AcceptButton = Me.Button1
        Me.CancelButton = Me.Button2
        Theme.StyleForm(Me)

        Me.buttons.ResumeLayout(False)
        Me.root.ResumeLayout(False)
        Me.root.PerformLayout()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents root As TableLayoutPanel
    Friend WithEvents lblTitle As Label
    Friend WithEvents info As Label
    Friend WithEvents buttons As FlowLayoutPanel
    Friend WithEvents Button1 As Button
    Friend WithEvents Button2 As Button
End Class
