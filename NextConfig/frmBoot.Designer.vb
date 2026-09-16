<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class frmBoot
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

    'Примечание: следующая процедура является обязательной для конструктора форм Windows Forms
    'Для ее изменения используйте конструктор форм Windows Form.
    'Не изменяйте ее в редакторе исходного кода.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Me.lblTitle = New System.Windows.Forms.Label()
        Me.lblFile = New System.Windows.Forms.Label()
        Me.lblInfo = New System.Windows.Forms.Label()
        Me.ProgressBar1 = New System.Windows.Forms.ProgressBar()
        Me.lblState = New System.Windows.Forms.Label()
        Me.btnCheck = New System.Windows.Forms.Button()
        Me.btnApply = New System.Windows.Forms.Button()
        Me.btnClose = New System.Windows.Forms.Button()
        Me.SuspendLayout()
        '
        'lblTitle
        '
        Me.lblTitle.BackColor = System.Drawing.Color.LimeGreen
        Me.lblTitle.Dock = System.Windows.Forms.DockStyle.Top
        Me.lblTitle.Font = New System.Drawing.Font("Microsoft Sans Serif", 14.25!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(204, Byte))
        Me.lblTitle.ForeColor = System.Drawing.Color.White
        Me.lblTitle.Location = New System.Drawing.Point(0, 0)
        Me.lblTitle.Name = "lblTitle"
        Me.lblTitle.Size = New System.Drawing.Size(453, 29)
        Me.lblTitle.TabIndex = 0
        Me.lblTitle.Text = "Замена загрузчика"
        Me.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        '
        'lblFile
        '
        Me.lblFile.AutoSize = True
        Me.lblFile.Font = New System.Drawing.Font("Microsoft Sans Serif", 8.25!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(204, Byte))
        Me.lblFile.ForeColor = System.Drawing.Color.Gray
        Me.lblFile.Location = New System.Drawing.Point(11, 40)
        Me.lblFile.Name = "lblFile"
        Me.lblFile.Size = New System.Drawing.Size(40, 13)
        Me.lblFile.TabIndex = 1
        Me.lblFile.Text = "Файл"
        '
        'lblInfo
        '
        Me.lblInfo.AutoSize = True
        Me.lblInfo.Font = New System.Drawing.Font("Microsoft Sans Serif", 8.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(204, Byte))
        Me.lblInfo.Location = New System.Drawing.Point(11, 60)
        Me.lblInfo.Name = "lblInfo"
        Me.lblInfo.Size = New System.Drawing.Size(13, 13)
        Me.lblInfo.TabIndex = 2
        Me.lblInfo.Text = "-"
        '
        'ProgressBar1
        '
        Me.ProgressBar1.Location = New System.Drawing.Point(14, 82)
        Me.ProgressBar1.Name = "ProgressBar1"
        Me.ProgressBar1.Size = New System.Drawing.Size(427, 24)
        Me.ProgressBar1.Step = 1
        Me.ProgressBar1.TabIndex = 3
        '
        'lblState
        '
        Me.lblState.AutoSize = True
        Me.lblState.Font = New System.Drawing.Font("Microsoft Sans Serif", 8.25!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(204, Byte))
        Me.lblState.ForeColor = System.Drawing.Color.Gray
        Me.lblState.Location = New System.Drawing.Point(12, 109)
        Me.lblState.Name = "lblState"
        Me.lblState.Size = New System.Drawing.Size(13, 13)
        Me.lblState.TabIndex = 4
        Me.lblState.Text = "-"
        '
        'btnCheck
        '
        Me.btnCheck.BackColor = System.Drawing.Color.Gainsboro
        Me.btnCheck.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(204, Byte))
        Me.btnCheck.Location = New System.Drawing.Point(14, 133)
        Me.btnCheck.Name = "btnCheck"
        Me.btnCheck.Size = New System.Drawing.Size(140, 48)
        Me.btnCheck.TabIndex = 5
        Me.btnCheck.Text = "Проверить"
        Me.btnCheck.UseVisualStyleBackColor = False
        '
        'btnApply
        '
        Me.btnApply.BackColor = System.Drawing.Color.LimeGreen
        Me.btnApply.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(204, Byte))
        Me.btnApply.ForeColor = System.Drawing.Color.White
        Me.btnApply.Location = New System.Drawing.Point(160, 133)
        Me.btnApply.Name = "btnApply"
        Me.btnApply.Size = New System.Drawing.Size(140, 48)
        Me.btnApply.TabIndex = 6
        Me.btnApply.Text = "Заменить"
        Me.btnApply.UseVisualStyleBackColor = False
        '
        'btnClose
        '
        Me.btnClose.BackColor = System.Drawing.Color.Red
        Me.btnClose.Font = New System.Drawing.Font("Microsoft Sans Serif", 11.25!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(204, Byte))
        Me.btnClose.ForeColor = System.Drawing.Color.White
        Me.btnClose.Location = New System.Drawing.Point(306, 133)
        Me.btnClose.Name = "btnClose"
        Me.btnClose.Size = New System.Drawing.Size(135, 48)
        Me.btnClose.TabIndex = 7
        Me.btnClose.Text = "Закрыть"
        Me.btnClose.UseVisualStyleBackColor = False
        '
        'frmBoot
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(453, 194)
        Me.ControlBox = False
        Me.Controls.Add(Me.btnClose)
        Me.Controls.Add(Me.btnApply)
        Me.Controls.Add(Me.btnCheck)
        Me.Controls.Add(Me.lblState)
        Me.Controls.Add(Me.ProgressBar1)
        Me.Controls.Add(Me.lblInfo)
        Me.Controls.Add(Me.lblFile)
        Me.Controls.Add(Me.lblTitle)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle
        Me.Name = "frmBoot"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents lblTitle As Label
    Friend WithEvents lblFile As Label
    Friend WithEvents lblInfo As Label
    Friend WithEvents ProgressBar1 As ProgressBar
    Friend WithEvents lblState As Label
    Friend WithEvents btnCheck As Button
    Friend WithEvents btnApply As Button
    Friend WithEvents btnClose As Button
End Class
