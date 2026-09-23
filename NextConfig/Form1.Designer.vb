<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class Form1
    Inherits System.Windows.Forms.Form

    'Форма переопределяет dispose для очистки списка компонентов.
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

    'Является обязательной для конструктора форм Windows Forms
    Private components As System.ComponentModel.IContainer

    ' Экран подключения. Собран на контейнерах, не на координатах:
    ' карточка по центру, поля и главная кнопка одной ширины.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(Form1))

        Me.root = New System.Windows.Forms.TableLayoutPanel()
        Me.card = New System.Windows.Forms.TableLayoutPanel()
        Me.head = New System.Windows.Forms.TableLayoutPanel()
        Me.picLogo = New System.Windows.Forms.PictureBox()
        Me.lblProduct = New System.Windows.Forms.Label()
        Me.lblSubtitle = New System.Windows.Forms.Label()
        Me.lblSerialCap = New System.Windows.Forms.Label()
        Me.txtIMEI = New System.Windows.Forms.TextBox()
        Me.lblPinCap = New System.Windows.Forms.Label()
        Me.txtPIN = New System.Windows.Forms.TextBox()
        Me.lnkDefault = New System.Windows.Forms.LinkLabel()
        Me.btnConnect = New System.Windows.Forms.Button()
        Me.statusRow = New System.Windows.Forms.FlowLayoutPanel()
        Me.lblDot = New System.Windows.Forms.Label()
        Me.lblStatus = New System.Windows.Forms.Label()
        Me.lblReason = New System.Windows.Forms.Label()

        Me.boxIMEI = Theme.FieldBox(Me.txtIMEI, FieldWidth)
        Me.boxPIN = Theme.FieldBox(Me.txtPIN, FieldWidth)

        CType(Me.picLogo, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.root.SuspendLayout()
        Me.card.SuspendLayout()
        Me.head.SuspendLayout()
        Me.statusRow.SuspendLayout()
        Me.SuspendLayout()
        '
        'picLogo
        '
        Me.picLogo.Image = Theme.LogoHorizontal(CType(resources.GetObject("PictureBox1.Image"), System.Drawing.Image))
        Me.picLogo.Name = "picLogo"
        Me.picLogo.Size = New System.Drawing.Size(Theme.Px(100), Theme.Px(32))
        Me.picLogo.Anchor = System.Windows.Forms.AnchorStyles.Left
        Me.picLogo.Margin = New System.Windows.Forms.Padding(0, 0, Theme.GapRow, 0)
        Me.picLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom
        Me.picLogo.TabStop = False
        '
        'lblProduct
        '
        Me.lblProduct.AutoSize = True
        Me.lblProduct.Name = "lblProduct"
        Me.lblProduct.Text = "Подключение"
        Me.lblProduct.Anchor = System.Windows.Forms.AnchorStyles.Left
        Me.lblProduct.Margin = New System.Windows.Forms.Padding(0)
        Theme.Product(Me.lblProduct)
        '
        'head
        '
        Me.head.AutoSize = True
        Me.head.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.head.ColumnCount = 2
        Me.head.RowCount = 1
        Me.head.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize))
        Me.head.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize))
        Me.head.Controls.Add(Me.picLogo, 0, 0)
        Me.head.Controls.Add(Me.lblProduct, 1, 0)
        Me.head.Margin = New System.Windows.Forms.Padding(0)
        Me.head.Name = "head"
        '
        'lblSubtitle
        '
        Me.lblSubtitle.AutoSize = True
        Me.lblSubtitle.Name = "lblSubtitle"
        Me.lblSubtitle.Text = "Укажите устройство и ПИН-код доступа"
        Me.lblSubtitle.Margin = New System.Windows.Forms.Padding(0, 2, 0, Theme.GapSection)
        Theme.Caption(Me.lblSubtitle)
        '
        'lblSerialCap
        '
        Me.lblSerialCap.AutoSize = True
        Me.lblSerialCap.Name = "lblSerialCap"
        Me.lblSerialCap.Text = "Серийный номер"
        Me.lblSerialCap.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapLabel)
        Theme.Caption(Me.lblSerialCap)
        '
        'boxIMEI
        '
        Me.boxIMEI.Name = "boxIMEI"
        Me.boxIMEI.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapRow)
        Me.txtIMEI.Name = "txtIMEI"
        Me.txtIMEI.MaxLength = 15
        '
        'lblPinCap
        '
        Me.lblPinCap.AutoSize = True
        Me.lblPinCap.Name = "lblPinCap"
        Me.lblPinCap.Text = "ПИН-код"
        Me.lblPinCap.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapLabel)
        Theme.Caption(Me.lblPinCap)
        '
        'boxPIN
        '
        Me.boxPIN.Name = "boxPIN"
        Me.boxPIN.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapRelated)
        Me.txtPIN.Name = "txtPIN"
        Me.txtPIN.MaxLength = 4
        '
        'lnkDefault
        '
        Me.lnkDefault.AutoSize = True
        Me.lnkDefault.Name = "lnkDefault"
        Me.lnkDefault.Text = "Заполнить по умолчанию"
        Me.lnkDefault.Font = Theme.FontCaption
        Me.lnkDefault.LinkColor = Theme.Accent
        Me.lnkDefault.ActiveLinkColor = Theme.AccentDown
        Me.lnkDefault.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline
        Me.lnkDefault.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapSection)
        '
        'btnConnect
        '
        Me.btnConnect.Name = "btnConnect"
        Me.btnConnect.Text = "Подключиться"
        Me.btnConnect.Enabled = False
        Me.btnConnect.Size = New System.Drawing.Size(Theme.Px(FieldWidth), Theme.ButtonHeight)
        Me.btnConnect.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapBlock)
        Theme.Primary(Me.btnConnect)
        '
        'lblDot
        '
        Me.lblDot.Name = "lblDot"
        Me.lblDot.Margin = New System.Windows.Forms.Padding(0, 0, Theme.GapLabel, 0)
        Theme.Dot(Me.lblDot, Theme.eDot.Waiting)
        '
        'lblStatus
        '
        Me.lblStatus.AutoSize = True
        Me.lblStatus.Name = "lblStatus"
        Me.lblStatus.Margin = New System.Windows.Forms.Padding(0, 3, 0, 0)
        Theme.DotText(Me.lblStatus, Theme.eDot.Waiting, "Поиск устройств…")
        '
        'statusRow
        '
        Me.statusRow.AutoSize = True
        Me.statusRow.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.statusRow.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight
        Me.statusRow.WrapContents = False
        Me.statusRow.Controls.Add(Me.lblDot)
        Me.statusRow.Controls.Add(Me.lblStatus)
        Me.statusRow.Margin = New System.Windows.Forms.Padding(0)
        Me.statusRow.Name = "statusRow"
        '
        'lblReason
        '
        Me.lblReason.AutoSize = True
        Me.lblReason.MaximumSize = New System.Drawing.Size(Theme.Px(FieldWidth), 0)
        Me.lblReason.Name = "lblReason"
        Me.lblReason.Text = ""
        Me.lblReason.Margin = New System.Windows.Forms.Padding(0, Theme.GapLabel, 0, 0)
        Theme.Caption(Me.lblReason)
        '
        'card
        '
        Me.card.AutoSize = True
        Me.card.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.card.BackColor = Theme.Surface
        Me.card.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.card.ColumnCount = 1
        Me.card.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize))
        Me.card.Padding = New System.Windows.Forms.Padding(Theme.GapSection)
        Me.card.Anchor = System.Windows.Forms.AnchorStyles.None
        Me.card.Name = "card"
        Me.card.Controls.Add(Me.head)
        Me.card.Controls.Add(Me.lblSubtitle)
        Me.card.Controls.Add(Me.lblSerialCap)
        Me.card.Controls.Add(Me.boxIMEI)
        Me.card.Controls.Add(Me.lblPinCap)
        Me.card.Controls.Add(Me.boxPIN)
        Me.card.Controls.Add(Me.lnkDefault)
        Me.card.Controls.Add(Me.btnConnect)
        Me.card.Controls.Add(Me.statusRow)
        Me.card.Controls.Add(Me.lblReason)
        '
        'root
        '
        Me.root.ColumnCount = 1
        Me.root.RowCount = 1
        Me.root.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100.0!))
        Me.root.RowStyles.Add(New System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100.0!))
        Me.root.Controls.Add(Me.card, 0, 0)
        Me.root.Dock = System.Windows.Forms.DockStyle.Fill
        Me.root.Name = "root"
        '
        'Form1
        '
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None
        Me.ClientSize = New System.Drawing.Size(Theme.Px(440), Theme.Px(492))
        Me.Controls.Add(Me.root)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle
        Me.MaximizeBox = False
        Me.Name = "Form1"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "Домовой NEXT"
        Me.AcceptButton = Me.btnConnect
        Theme.StyleForm(Me)
        Theme.ConnectionLayout(Me, Me.root, Me.card)

        CType(Me.picLogo, System.ComponentModel.ISupportInitialize).EndInit()
        Me.head.ResumeLayout(False)
        Me.head.PerformLayout()
        Me.statusRow.ResumeLayout(False)
        Me.statusRow.PerformLayout()
        Me.card.ResumeLayout(False)
        Me.card.PerformLayout()
        Me.root.ResumeLayout(False)
        Me.root.PerformLayout()
        Me.ResumeLayout(False)

    End Sub

    ' ширина полей и главной кнопки - одна на весь экран подключения
    Private Const FieldWidth As Integer = 320

    Friend WithEvents root As TableLayoutPanel
    Friend WithEvents card As TableLayoutPanel
    Friend WithEvents head As TableLayoutPanel
    Friend WithEvents picLogo As PictureBox
    Friend WithEvents lblProduct As Label
    Friend WithEvents lblSubtitle As Label
    Friend WithEvents lblSerialCap As Label
    Friend WithEvents boxIMEI As Panel
    Friend WithEvents txtIMEI As TextBox
    Friend WithEvents lblPinCap As Label
    Friend WithEvents boxPIN As Panel
    Friend WithEvents txtPIN As TextBox
    Friend WithEvents lnkDefault As LinkLabel
    Friend WithEvents btnConnect As Button
    Friend WithEvents statusRow As FlowLayoutPanel
    Friend WithEvents lblDot As Label
    Friend WithEvents lblStatus As Label
    Friend WithEvents lblReason As Label
End Class
