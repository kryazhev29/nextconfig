<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class frmPIN
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

    ' Смена ПИН-кода. Тот же образец, что у остальных диалогов.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Me.root = New System.Windows.Forms.TableLayoutPanel()
        Me.lblTitle = New System.Windows.Forms.Label()
        Me.Label5 = New System.Windows.Forms.Label()
        Me.txtOldPIN = New System.Windows.Forms.TextBox()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.txtNewPIN = New System.Windows.Forms.TextBox()
        Me.Label2 = New System.Windows.Forms.Label()
        Me.txtNewPIN2 = New System.Windows.Forms.TextBox()
        Me.lblError = New System.Windows.Forms.Label()
        Me.buttons = New System.Windows.Forms.FlowLayoutPanel()
        Me.Button1 = New System.Windows.Forms.Button()
        Me.Button2 = New System.Windows.Forms.Button()

        Me.boxOld = Theme.FieldBox(Me.txtOldPIN, 200)
        Me.boxNew = Theme.FieldBox(Me.txtNewPIN, 200)
        Me.boxNew2 = Theme.FieldBox(Me.txtNewPIN2, 200)

        Me.root.SuspendLayout()
        Me.buttons.SuspendLayout()
        Me.SuspendLayout()

        Me.lblTitle.AutoSize = True
        Me.lblTitle.Name = "lblTitle"
        Me.lblTitle.Text = "Смена ПИН-кода"
        Me.lblTitle.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapBlock)
        Theme.Title(Me.lblTitle)

        Me.Label5.AutoSize = True
        Me.Label5.Name = "Label5"
        Me.Label5.Text = "Текущий ПИН-код"
        Me.Label5.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapLabel)
        Theme.Caption(Me.Label5)

        Me.Label1.AutoSize = True
        Me.Label1.Name = "Label1"
        Me.Label1.Text = "Новый ПИН-код"
        Me.Label1.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapLabel)
        Theme.Caption(Me.Label1)

        Me.Label2.AutoSize = True
        Me.Label2.Name = "Label2"
        Me.Label2.Text = "Новый ПИН-код ещё раз"
        Me.Label2.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapLabel)
        Theme.Caption(Me.Label2)

        Me.txtOldPIN.Name = "txtOldPIN"
        Me.txtOldPIN.MaxLength = 4
        Me.txtNewPIN.Name = "txtNewPIN"
        Me.txtNewPIN.MaxLength = 4
        Me.txtNewPIN2.Name = "txtNewPIN2"
        Me.txtNewPIN2.MaxLength = 4

        Me.boxOld.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapRow)
        Me.boxNew.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapRow)
        Me.boxNew2.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapRelated)

        Me.lblError.AutoSize = True
        Me.lblError.Name = "lblError"
        Me.lblError.Text = ""
        Me.lblError.MaximumSize = New System.Drawing.Size(Theme.Px(360), 0)
        Me.lblError.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapBlock)
        Theme.Note(Me.lblError, True)

        Me.Button1.Name = "Button1"
        Me.Button1.Text = "Сменить ПИН"
        Me.Button1.Width = Theme.Px(176)
        Me.Button1.Margin = New System.Windows.Forms.Padding(Theme.GapRelated, 0, 0, 0)
        Theme.Primary(Me.Button1)

        Me.Button2.Name = "Button2"
        Me.Button2.Text = "Отмена"
        Me.Button2.Width = Theme.Px(148)
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
        Me.root.Controls.Add(Me.Label5)
        Me.root.Controls.Add(Me.boxOld)
        Me.root.Controls.Add(Me.Label1)
        Me.root.Controls.Add(Me.boxNew)
        Me.root.Controls.Add(Me.Label2)
        Me.root.Controls.Add(Me.boxNew2)
        Me.root.Controls.Add(Me.lblError)
        Me.root.Controls.Add(Me.buttons)

        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None
        Me.ClientSize = New System.Drawing.Size(Theme.Px(420), Theme.Px(400))
        Me.Controls.Add(Me.root)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "frmPIN"
        Me.ShowInTaskbar = False
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "Домовой NEXT"
        Me.AcceptButton = Me.Button1
        Me.CancelButton = Me.Button2
        Theme.StyleForm(Me)
        Theme.DialogLayout(Me, Me.root, Me.buttons)

        Me.buttons.ResumeLayout(False)
        Me.root.ResumeLayout(False)
        Me.root.PerformLayout()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents root As TableLayoutPanel
    Friend WithEvents lblTitle As Label
    Friend WithEvents buttons As FlowLayoutPanel
    Friend WithEvents Button2 As Button
    Friend WithEvents Button1 As Button
    Friend WithEvents Label5 As Label
    Friend WithEvents boxOld As Panel
    Friend WithEvents txtOldPIN As TextBox
    Friend WithEvents Label1 As Label
    Friend WithEvents boxNew As Panel
    Friend WithEvents txtNewPIN As TextBox
    Friend WithEvents Label2 As Label
    Friend WithEvents boxNew2 As Panel
    Friend WithEvents txtNewPIN2 As TextBox
    Friend WithEvents lblError As Label
End Class
