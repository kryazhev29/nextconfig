<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class frmFW
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

    ' Один экран хода для всех долгих операций с платой. Сверху вниз:
    ' что за операция, что за файл и что в нём проверено, какой сейчас этап,
    ' полоса хода и действия.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Me.root = New System.Windows.Forms.TableLayoutPanel()
        Me.lblTitle = New System.Windows.Forms.Label()
        Me.fileCard = New System.Windows.Forms.TableLayoutPanel()
        Me.lblFileCap = New System.Windows.Forms.Label()
        Me.Label5 = New System.Windows.Forms.Label()
        Me.lblInfo = New System.Windows.Forms.Label()
        Me.workCard = New System.Windows.Forms.TableLayoutPanel()
        Me.lblStageCap = New System.Windows.Forms.Label()
        Me.lblState = New System.Windows.Forms.Label()
        Me.ProgressBar1 = New Meter()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.buttons = New System.Windows.Forms.FlowLayoutPanel()
        Me.Button2 = New System.Windows.Forms.Button()
        Me.Button1 = New System.Windows.Forms.Button()
        Me.btnCheck = New System.Windows.Forms.Button()
        Me.root.SuspendLayout()
        Me.fileCard.SuspendLayout()
        Me.workCard.SuspendLayout()
        Me.buttons.SuspendLayout()
        Me.SuspendLayout()
        '
        'lblTitle
        '
        Me.lblTitle.AutoSize = True
        Me.lblTitle.Name = "lblTitle"
        Me.lblTitle.Text = "Загрузка файла"
        Me.lblTitle.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapBlock)
        Theme.Title(Me.lblTitle)
        '
        ' карточка файла: имя и то, что в нём проверено
        '
        Me.lblFileCap.AutoSize = True
        Me.lblFileCap.Text = "Файл"
        Me.lblFileCap.Margin = New System.Windows.Forms.Padding(0, 0, 0, 2)
        Theme.Caption(Me.lblFileCap)

        Me.Label5.AutoSize = True
        Me.Label5.Name = "Label5"
        Me.Label5.Text = "-"
        Me.Label5.MaximumSize = New System.Drawing.Size(Theme.Px(520), 0)
        Me.Label5.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapRelated)
        Theme.Value(Me.Label5)

        Me.lblInfo.AutoSize = True
        Me.lblInfo.Name = "lblInfo"
        Me.lblInfo.Text = "-"
        Me.lblInfo.MaximumSize = New System.Drawing.Size(Theme.Px(520), 0)
        Me.lblInfo.Margin = New System.Windows.Forms.Padding(0)
        Theme.Caption(Me.lblInfo)

        Me.fileCard.ColumnCount = 1
        Me.fileCard.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100.0!))
        Me.fileCard.AutoSize = True
        Me.fileCard.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.fileCard.BackColor = Theme.Surface
        Me.fileCard.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.fileCard.Padding = New System.Windows.Forms.Padding(Theme.GapBlock)
        Me.fileCard.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapSection)
        Me.fileCard.Anchor = System.Windows.Forms.AnchorStyles.Left Or System.Windows.Forms.AnchorStyles.Right
        Me.fileCard.Controls.Add(Me.lblFileCap)
        Me.fileCard.Controls.Add(Me.Label5)
        Me.fileCard.Controls.Add(Me.lblInfo)
        '
        ' карточка хода: этап, полоса, счётчик байт
        '
        Me.lblStageCap.AutoSize = True
        Me.lblStageCap.Text = "Этап"
        Me.lblStageCap.Margin = New System.Windows.Forms.Padding(0, 0, 0, 2)
        Theme.Caption(Me.lblStageCap)

        Me.lblState.AutoSize = True
        Me.lblState.Name = "lblState"
        Me.lblState.Text = "Готово к началу"
        Me.lblState.MaximumSize = New System.Drawing.Size(Theme.Px(520), 0)
        Me.lblState.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapBlock)
        Theme.Value(Me.lblState)

        Me.ProgressBar1.Name = "ProgressBar1"
        Me.ProgressBar1.Maximum = 100
        Me.ProgressBar1.Value = 0
        Me.ProgressBar1.Size = New System.Drawing.Size(Theme.Px(520), Theme.Px(8))
        Me.ProgressBar1.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapRelated)
        Me.ProgressBar1.Anchor = System.Windows.Forms.AnchorStyles.Left Or System.Windows.Forms.AnchorStyles.Right

        Me.Label1.AutoSize = True
        Me.Label1.Name = "Label1"
        Me.Label1.Text = "0/0"
        Me.Label1.Margin = New System.Windows.Forms.Padding(0)
        Theme.Caption(Me.Label1)

        Me.workCard.ColumnCount = 1
        Me.workCard.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100.0!))
        Me.workCard.AutoSize = True
        Me.workCard.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.workCard.BackColor = Theme.Surface
        Me.workCard.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.workCard.Padding = New System.Windows.Forms.Padding(Theme.GapBlock)
        Me.workCard.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapSection)
        Me.workCard.Anchor = System.Windows.Forms.AnchorStyles.Left Or System.Windows.Forms.AnchorStyles.Right
        Me.workCard.Controls.Add(Me.lblStageCap)
        Me.workCard.Controls.Add(Me.lblState)
        Me.workCard.Controls.Add(Me.ProgressBar1)
        Me.workCard.Controls.Add(Me.Label1)
        '
        ' действия: главное справа, отмена рядом
        '
        Me.Button1.Name = "Button1"
        Me.Button1.Text = "Загрузить"
        Me.Button1.Width = Theme.Px(200)
        Me.Button1.Margin = New System.Windows.Forms.Padding(Theme.GapRelated, 0, 0, 0)
        Theme.Primary(Me.Button1)

        Me.btnCheck.Name = "btnCheck"
        Me.btnCheck.Text = "Пробная передача"
        Me.btnCheck.Width = Theme.Px(200)
        Me.btnCheck.Margin = New System.Windows.Forms.Padding(Theme.GapRelated, 0, 0, 0)
        Theme.Secondary(Me.btnCheck)

        Me.Button2.Name = "Button2"
        Me.Button2.Text = "Закрыть"
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
        Me.buttons.Controls.Add(Me.btnCheck)
        Me.buttons.Controls.Add(Me.Button2)
        '
        'root
        '
        Me.root.ColumnCount = 1
        Me.root.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100.0!))
        Me.root.Dock = System.Windows.Forms.DockStyle.Fill
        Me.root.AutoSize = True
        Me.root.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.root.Padding = New System.Windows.Forms.Padding(Theme.GapSection)
        Me.root.Controls.Add(Me.lblTitle)
        Me.root.Controls.Add(Me.fileCard)
        Me.root.Controls.Add(Me.workCard)
        Me.root.Controls.Add(Me.buttons)
        '
        'frmFW
        '
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None
        Me.ClientSize = New System.Drawing.Size(Theme.Px(600), Theme.Px(400))
        Me.MinimumSize = New System.Drawing.Size(Theme.Px(560), Theme.Px(380))
        ' итог операции бывает в две-три строки: окно должно вырасти за ним,
        ' иначе кнопки уходят за нижний край
        Me.AutoSize = True
        Me.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.Controls.Add(Me.root)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "frmFW"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "Домовой NEXT"
        Theme.StyleForm(Me)

        Me.buttons.ResumeLayout(False)
        Me.workCard.ResumeLayout(False)
        Me.workCard.PerformLayout()
        Me.fileCard.ResumeLayout(False)
        Me.fileCard.PerformLayout()
        Me.root.ResumeLayout(False)
        Me.root.PerformLayout()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents root As TableLayoutPanel
    Friend WithEvents lblTitle As Label
    Friend WithEvents fileCard As TableLayoutPanel
    Friend WithEvents lblFileCap As Label
    Friend WithEvents Label5 As Label
    Friend WithEvents lblInfo As Label
    Friend WithEvents workCard As TableLayoutPanel
    Friend WithEvents lblStageCap As Label
    Friend WithEvents lblState As Label
    Friend WithEvents ProgressBar1 As Meter
    Friend WithEvents Label1 As Label
    Friend WithEvents buttons As FlowLayoutPanel
    Friend WithEvents btnCheck As Button
    Friend WithEvents Button2 As Button
    Friend WithEvents Button1 As Button
End Class
