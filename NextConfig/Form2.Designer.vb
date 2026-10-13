<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class Form2
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

    ' Главное окно. Три части сверху вниз: постоянная шапка с платой,
    ' вкладки с содержимым и строка итога последнего действия.
    ' Собрано на контейнерах: окно тянется, содержимое вкладок прокручивается.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(Form2))
        Me.components = New System.ComponentModel.Container()

        ' ---- создание ----
        Me.root = New System.Windows.Forms.TableLayoutPanel()
        Me.pnlHead = New System.Windows.Forms.Panel()
        Me.headRow = New System.Windows.Forms.TableLayoutPanel()
        Me.headLine = New System.Windows.Forms.Panel()
        Me.picLogo = New System.Windows.Forms.PictureBox()
        Me.headDevice = New System.Windows.Forms.TableLayoutPanel()
        Me.lblIMEICap = New System.Windows.Forms.Label()
        Me.lblIMEI = New System.Windows.Forms.Label()
        Me.headVersions = New System.Windows.Forms.FlowLayoutPanel()
        Me.lblHeadModel = New System.Windows.Forms.Label()
        Me.lblHeadHW = New System.Windows.Forms.Label()
        Me.lblHeadFW = New System.Windows.Forms.Label()
        Me.lblHeadBoot = New System.Windows.Forms.Label()
        Me.headState = New System.Windows.Forms.FlowLayoutPanel()
        Me.dotConn = New System.Windows.Forms.Label()
        Me.lblConnState = New System.Windows.Forms.Label()
        Me.lblPortInfo = New System.Windows.Forms.Label()

        Me.tabs = New System.Windows.Forms.TabControl()
        Me.tabState = New System.Windows.Forms.TabPage()
        Me.tabSettings = New System.Windows.Forms.TabPage()
        Me.tabIds = New System.Windows.Forms.TabPage()
        Me.tabObject = New System.Windows.Forms.TabPage()
        Me.tabFW = New System.Windows.Forms.TabPage()
        Me.tabLog = New System.Windows.Forms.TabPage()

        Me.pnlStatusBar = New System.Windows.Forms.Panel()
        Me.statusRow = New System.Windows.Forms.TableLayoutPanel()
        Me.lblLastUpdate = New System.Windows.Forms.Label()
        Me.lblSaveState = New System.Windows.Forms.Label()

        ' состояние: связь
        Me.dotGSM = New System.Windows.Forms.Label()
        Me.lblGSM = New System.Windows.Forms.Label()
        Me.dotInt = New System.Windows.Forms.Label()
        Me.lblInt = New System.Windows.Forms.Label()
        Me.dotServer = New System.Windows.Forms.Label()
        Me.lblServerConnect = New System.Windows.Forms.Label()
        Me.lblNet = New System.Windows.Forms.Label()
        Me.pbSig = New Meter()
        Me.lblSig = New System.Windows.Forms.Label()
        Me.sigRow = New System.Windows.Forms.FlowLayoutPanel()

        ' состояние: устройство
        Me.lblModuleType = New System.Windows.Forms.Label()
        Me.Label19 = New System.Windows.Forms.Button()
        Me.lblHW = New System.Windows.Forms.Label()
        Me.lblFW = New System.Windows.Forms.Label()
        Me.lblBoot = New System.Windows.Forms.Label()
        Me.lblTime = New System.Windows.Forms.Label()
        Me.Label16 = New System.Windows.Forms.Button()

        ' состояние: входы и выходы
        Me.dotIN1 = New System.Windows.Forms.Label()
        Me.lbIN1 = New System.Windows.Forms.Label()
        Me.dotIN2 = New System.Windows.Forms.Label()
        Me.lblIN2 = New System.Windows.Forms.Label()
        Me.dotOUT1 = New System.Windows.Forms.Label()
        Me.lblOUT1 = New System.Windows.Forms.Label()
        Me.btnTestOUT1 = New System.Windows.Forms.Button()
        Me.dotOUT2 = New System.Windows.Forms.Label()
        Me.lbOUT2 = New System.Windows.Forms.Label()
        Me.btnTestOUT2 = New System.Windows.Forms.Button()

        ' настройки
        Me.txtServer1 = New System.Windows.Forms.TextBox()
        Me.txtServer2 = New System.Windows.Forms.TextBox()
        Me.txtOperator = New System.Windows.Forms.TextBox()
        Me.lblServer1Err = New System.Windows.Forms.Label()
        Me.lblServer2Err = New System.Windows.Forms.Label()
        Me.chServer1 = New System.Windows.Forms.CheckBox()
        Me.Label21 = New System.Windows.Forms.Button()
        Me.chServer2 = New System.Windows.Forms.CheckBox()
        Me.cmbVoLTE = New System.Windows.Forms.ComboBox()
        Me.cmbNetType = New System.Windows.Forms.ComboBox()
        Me.btnRevert = New System.Windows.Forms.Button()
        Me.lblPIN = New System.Windows.Forms.Label()
        Me.btnChangePIN = New System.Windows.Forms.Button()
        Me.Button1 = New System.Windows.Forms.Button()
        Me.btnSave = New System.Windows.Forms.Button()
        Me.btnDefault = New System.Windows.Forms.Button()
        Me.btnFactory = New System.Windows.Forms.Button()
        Me.btnTranspMode = New System.Windows.Forms.Button()
        Me.Label20 = New System.Windows.Forms.Button()
        Me.saveRow = New System.Windows.Forms.FlowLayoutPanel()
        Me.serviceRow = New System.Windows.Forms.FlowLayoutPanel()

        ' идентификаторы
        Me.lblUsedMem = New System.Windows.Forms.Label()
        Me.meterMem = New Meter()
        Me.btnImport = New System.Windows.Forms.Button()
        Me.btnExport = New System.Windows.Forms.Button()
        Me.btnClear = New System.Windows.Forms.Button()
        Me.idsRow = New System.Windows.Forms.FlowLayoutPanel()
        Me.idsClearRow = New System.Windows.Forms.FlowLayoutPanel()

        ' объект
        Me.lblObjNum = New System.Windows.Forms.Label()
        Me.lblObjName = New System.Windows.Forms.Label()
        Me.lblObjController = New System.Windows.Forms.Label()
        Me.lblAdmin = New System.Windows.Forms.Label()
        Me.btnUpdateObj = New System.Windows.Forms.Button()
        Me.btnUnConnect = New System.Windows.Forms.Button()
        Me.objRow = New System.Windows.Forms.FlowLayoutPanel()

        ' прошивка
        Me.btnUpdateFW = New System.Windows.Forms.Button()
        Me.lblFwHint = New System.Windows.Forms.Label()
        Me.btnUpdateBoot = New System.Windows.Forms.Button()
        Me.lblBootHint = New System.Windows.Forms.Label()

        ' журнал
        Me.CheckBox1 = New System.Windows.Forms.CheckBox()
        Me.CheckBox2 = New System.Windows.Forms.CheckBox()
        Me.CheckBox3 = New System.Windows.Forms.CheckBox()
        Me.CheckBox4 = New System.Windows.Forms.CheckBox()
        Me.chAutoScroll = New System.Windows.Forms.CheckBox()
        Me.RichTextBox1 = New System.Windows.Forms.RichTextBox()
        Me.btnClearLog = New System.Windows.Forms.Button()
        Me.Button2 = New System.Windows.Forms.Button()
        Me.logFilters = New System.Windows.Forms.FlowLayoutPanel()
        Me.logActions = New System.Windows.Forms.FlowLayoutPanel()
        Me.logRoot = New System.Windows.Forms.TableLayoutPanel()

        Me.OpenFileDialog1 = New System.Windows.Forms.OpenFileDialog()

        Me.boxServer1 = Theme.FieldBox(Me.txtServer1, 260)
        Me.boxServer2 = Theme.FieldBox(Me.txtServer2, 260)
        Me.boxOperator = Theme.FieldBox(Me.txtOperator, 320)

        CType(Me.picLogo, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()

        ' ---- шапка ----
        Me.picLogo.Image = Theme.LogoHorizontal(CType(resources.GetObject("PictureBox1.Image"), System.Drawing.Image))
        Me.picLogo.Name = "picLogo"
        Me.picLogo.Size = New System.Drawing.Size(Theme.Px(88), Theme.Px(28))
        Me.picLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom
        Me.picLogo.Anchor = System.Windows.Forms.AnchorStyles.Left
        Me.picLogo.Margin = New System.Windows.Forms.Padding(0, 0, Theme.GapSection, 0)
        Me.picLogo.TabStop = False

        Me.lblIMEICap.AutoSize = True
        Me.lblIMEICap.Text = "Устройство"
        Me.lblIMEICap.Margin = New System.Windows.Forms.Padding(0, 0, Theme.GapRelated, 0)
        Me.lblIMEICap.Anchor = System.Windows.Forms.AnchorStyles.Left
        Theme.Caption(Me.lblIMEICap)

        Me.lblIMEI.AutoSize = True
        Me.lblIMEI.Name = "lblIMEI"
        Me.lblIMEI.Text = "---"
        Me.lblIMEI.Margin = New System.Windows.Forms.Padding(0)
        Me.lblIMEI.Anchor = System.Windows.Forms.AnchorStyles.Left
        Theme.Value(Me.lblIMEI)
        Me.lblIMEI.Font = Theme.FontBodyBold

        Me.lblHeadModel.AutoSize = True
        Me.lblHeadModel.Name = "lblHeadModel"
        Me.lblHeadModel.Text = "Модель ---"
        Me.lblHeadModel.Margin = New System.Windows.Forms.Padding(0, 0, Theme.GapSection, 0)
        Theme.Caption(Me.lblHeadModel)

        Me.lblHeadHW.AutoSize = True
        Me.lblHeadHW.Name = "lblHeadHW"
        Me.lblHeadHW.Text = "Плата ---"
        Me.lblHeadHW.Margin = New System.Windows.Forms.Padding(0, 0, Theme.GapSection, 0)
        Theme.Caption(Me.lblHeadHW)

        Me.lblHeadFW.AutoSize = True
        Me.lblHeadFW.Name = "lblHeadFW"
        Me.lblHeadFW.Text = "Прошивка ---"
        Me.lblHeadFW.Margin = New System.Windows.Forms.Padding(0, 0, Theme.GapSection, 0)
        Theme.Caption(Me.lblHeadFW)

        Me.lblHeadBoot.AutoSize = True
        Me.lblHeadBoot.Name = "lblHeadBoot"
        Me.lblHeadBoot.Text = "Загрузчик ---"
        Me.lblHeadBoot.Margin = New System.Windows.Forms.Padding(0)
        Theme.Caption(Me.lblHeadBoot)

        Me.headVersions.AutoSize = True
        Me.headVersions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.headVersions.WrapContents = False
        Me.headVersions.Margin = New System.Windows.Forms.Padding(0, Theme.GapLabel, 0, 0)
        Me.headVersions.Controls.Add(Me.lblHeadModel)
        Me.headVersions.Controls.Add(Me.lblHeadHW)
        Me.headVersions.Controls.Add(Me.lblHeadFW)
        Me.headVersions.Controls.Add(Me.lblHeadBoot)

        Dim imeiRow As New System.Windows.Forms.FlowLayoutPanel()
        imeiRow.AutoSize = True
        imeiRow.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        imeiRow.WrapContents = False
        imeiRow.Margin = New System.Windows.Forms.Padding(0)
        imeiRow.Controls.Add(Me.lblIMEICap)
        imeiRow.Controls.Add(Me.lblIMEI)

        Me.headDevice.AutoSize = True
        Me.headDevice.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.headDevice.ColumnCount = 1
        Me.headDevice.Margin = New System.Windows.Forms.Padding(0)
        Me.headDevice.Anchor = System.Windows.Forms.AnchorStyles.Left
        Me.headDevice.Controls.Add(imeiRow)
        Me.headDevice.Controls.Add(Me.headVersions)

        Theme.Dot(Me.dotConn, Theme.eDot.Waiting)
        Me.dotConn.Name = "dotConn"
        Me.dotConn.Margin = New System.Windows.Forms.Padding(0, 0, Theme.GapLabel, 0)

        Me.lblConnState.AutoSize = True
        Me.lblConnState.Name = "lblConnState"
        Me.lblConnState.Margin = New System.Windows.Forms.Padding(0, 3, Theme.GapSection, 0)
        Theme.DotText(Me.lblConnState, Theme.eDot.Waiting, "Ожидание данных")

        Me.lblPortInfo.AutoSize = True
        Me.lblPortInfo.Name = "lblPortInfo"
        Me.lblPortInfo.Margin = New System.Windows.Forms.Padding(0, 3, 0, 0)
        Theme.Caption(Me.lblPortInfo)

        Me.headState.AutoSize = True
        Me.headState.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.headState.WrapContents = False
        Me.headState.Anchor = System.Windows.Forms.AnchorStyles.Right
        Me.headState.Margin = New System.Windows.Forms.Padding(0)
        Me.headState.Controls.Add(Me.dotConn)
        Me.headState.Controls.Add(Me.lblConnState)
        Me.headState.Controls.Add(Me.lblPortInfo)

        Me.headRow.AutoSize = True
        Me.headRow.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.headRow.ColumnCount = 3
        Me.headRow.RowCount = 1
        Me.headRow.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize))
        Me.headRow.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100.0!))
        Me.headRow.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize))
        Me.headRow.Dock = System.Windows.Forms.DockStyle.Top
        Me.headRow.Margin = New System.Windows.Forms.Padding(0)
        Me.headRow.Controls.Add(Me.picLogo, 0, 0)
        Me.headRow.Controls.Add(Me.headDevice, 1, 0)
        Me.headRow.Controls.Add(Me.headState, 2, 0)

        Me.headLine.BackColor = Theme.Border
        Me.headLine.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.headLine.Height = 1

        Me.pnlHead.AutoSize = True
        Me.pnlHead.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.pnlHead.BackColor = Theme.Surface
        Me.pnlHead.Dock = System.Windows.Forms.DockStyle.Fill
        Me.pnlHead.Padding = New System.Windows.Forms.Padding(Theme.GapSection, Theme.GapBlock, Theme.GapSection, Theme.GapBlock)
        Me.pnlHead.Margin = New System.Windows.Forms.Padding(0)
        Me.pnlHead.Name = "pnlHead"
        Me.pnlHead.Controls.Add(Me.headRow)
        Me.pnlHead.Controls.Add(Me.headLine)

        ' ---- вкладка «Состояние» ----
        Theme.Dot(Me.dotGSM, Theme.eDot.Idle)
        Theme.Dot(Me.dotInt, Theme.eDot.Idle)
        Theme.Dot(Me.dotServer, Theme.eDot.Idle)
        Theme.DotText(Me.lblGSM, Theme.eDot.Idle, "Ожидание данных")
        Theme.DotText(Me.lblInt, Theme.eDot.Idle, "Ожидание данных")
        Theme.DotText(Me.lblServerConnect, Theme.eDot.Idle, "Ожидание данных")
        Me.lblGSM.Name = "lblGSM"
        Me.lblInt.Name = "lblInt"
        Me.lblServerConnect.Name = "lblServerConnect"
        Me.lblGSM.AutoSize = True
        Me.lblInt.AutoSize = True
        Me.lblServerConnect.AutoSize = True

        Me.lblNet.AutoSize = True
        Me.lblNet.Name = "lblNet"
        Me.lblNet.Text = "---"
        Theme.Value(Me.lblNet)

        Me.pbSig.Name = "pbSig"
        Me.pbSig.Maximum = 31
        Me.pbSig.Value = 0
        Me.pbSig.Size = New System.Drawing.Size(Theme.Px(140), Theme.Px(8))
        Me.pbSig.Margin = New System.Windows.Forms.Padding(0, 8, Theme.GapRelated, 0)

        Me.lblSig.AutoSize = True
        Me.lblSig.Name = "lblSig"
        Me.lblSig.Text = "---"
        Me.lblSig.Margin = New System.Windows.Forms.Padding(0)
        Theme.Value(Me.lblSig)

        Me.sigRow.AutoSize = True
        Me.sigRow.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.sigRow.WrapContents = False
        Me.sigRow.Margin = New System.Windows.Forms.Padding(0)
        Me.sigRow.Controls.Add(Me.pbSig)
        Me.sigRow.Controls.Add(Me.lblSig)

        Me.lblModuleType.AutoSize = True
        Me.lblModuleType.Name = "lblModuleType"
        Me.lblModuleType.Text = "---"
        Theme.Value(Me.lblModuleType)

        Me.Label19.Name = "Label19"
        Me.Label19.Text = "Изменить"
        Theme.Secondary(Me.Label19)
        Theme.Small(Me.Label19)

        Me.lblHW.AutoSize = True
        Me.lblHW.Name = "lblHW"
        Me.lblHW.Text = "---"
        Theme.Value(Me.lblHW)

        Me.lblFW.AutoSize = True
        Me.lblFW.Name = "lblFW"
        Me.lblFW.Text = "---"
        Theme.Value(Me.lblFW)

        Me.lblBoot.AutoSize = True
        Me.lblBoot.Name = "lblBoot"
        Me.lblBoot.Text = "---"
        Theme.Value(Me.lblBoot)

        Me.lblTime.AutoSize = True
        Me.lblTime.Name = "lblTime"
        Me.lblTime.Text = "---"
        Theme.Value(Me.lblTime)

        Me.Label16.Name = "Label16"
        Me.Label16.Text = "Синхронизировать"
        Theme.Secondary(Me.Label16)
        Theme.Small(Me.Label16)

        Theme.Dot(Me.dotIN1, Theme.eDot.Idle)
        Theme.Dot(Me.dotIN2, Theme.eDot.Idle)
        Theme.Dot(Me.dotOUT1, Theme.eDot.Idle)
        Theme.Dot(Me.dotOUT2, Theme.eDot.Idle)
        Theme.DotText(Me.lbIN1, Theme.eDot.Idle, "---")
        Theme.DotText(Me.lblIN2, Theme.eDot.Idle, "---")
        Theme.DotText(Me.lblOUT1, Theme.eDot.Idle, "---")
        Theme.DotText(Me.lbOUT2, Theme.eDot.Idle, "---")
        Me.lbIN1.Name = "lbIN1"
        Me.lblIN2.Name = "lblIN2"
        Me.lblOUT1.Name = "lblOUT1"
        Me.lbOUT2.Name = "lbOUT2"
        Me.lbIN1.AutoSize = True
        Me.lblIN2.AutoSize = True
        Me.lblOUT1.AutoSize = True
        Me.lbOUT2.AutoSize = True

        Me.btnTestOUT1.Name = "btnTestOUT1"
        Me.btnTestOUT1.Text = "Проверить выход"
        Me.btnTestOUT1.Tag = 0
        Theme.Secondary(Me.btnTestOUT1)
        Theme.Small(Me.btnTestOUT1)

        Me.btnTestOUT2.Name = "btnTestOUT2"
        Me.btnTestOUT2.Text = "Проверить выход"
        Me.btnTestOUT2.Tag = 1
        Theme.Secondary(Me.btnTestOUT2)
        Theme.Small(Me.btnTestOUT2)

        Me.tabState.Text = "Состояние"
        Me.tabState.BackColor = Theme.WindowBack
        Me.tabState.AutoScroll = True
        Me.tabState.Controls.Add(Theme.Overview(New Control() {
            CardBlock("", Me.deviceConnection),
            CardBlock("Устройство",
                      CompactRow("Модуль связи", Me.lblModuleType, Me.Label19),
                      CompactRow("Версия платы", Me.lblHW),
                      CompactRow("Версия прошивки", Me.lblFW),
                      CompactRow("Версия загрузчика", Me.lblBoot),
                      CompactRow("Время устройства", Me.lblTime, Me.Label16)),
            CardBlock("Входы и выходы", IoBlock())}))

        ' ---- вкладка «Настройки» ----
        Me.txtServer1.Name = "txtServer1"
        Me.txtServer2.Name = "txtServer2"
        Me.txtOperator.Name = "txtOperator"

        Me.lblServer1Err.AutoSize = True
        Me.lblServer1Err.Name = "lblServer1Err"
        Me.lblServer1Err.Text = ""
        Me.lblServer1Err.Visible = False
        Me.lblServer1Err.Margin = New System.Windows.Forms.Padding(0)
        Theme.Note(Me.lblServer1Err, True)

        Me.lblServer2Err.AutoSize = True
        Me.lblServer2Err.Name = "lblServer2Err"
        Me.lblServer2Err.Text = ""
        Me.lblServer2Err.Visible = False
        Me.lblServer2Err.Margin = New System.Windows.Forms.Padding(0)
        Theme.Note(Me.lblServer2Err, True)

        Me.chServer1.AutoSize = True
        Me.chServer1.Name = "chServer1"
        Me.chServer1.Text = "Использовать"
        Me.chServer1.Font = Theme.FontCaption
        Me.chServer1.ForeColor = Theme.TextMuted

        Me.chServer2.AutoSize = True
        Me.chServer2.Name = "chServer2"
        Me.chServer2.Text = "Использовать"
        Me.chServer2.Font = Theme.FontCaption
        Me.chServer2.ForeColor = Theme.TextMuted

        Me.Label21.Name = "Label21"
        Me.Label21.Text = "Проверить"
        Theme.Secondary(Me.Label21)
        Theme.Small(Me.Label21)

        Me.cmbVoLTE.Name = "cmbVoLTE"
        Me.cmbVoLTE.Items.AddRange(New Object() {"Выключено", "Включено"})
        Theme.Combo(Me.cmbVoLTE, 260)

        Me.cmbNetType.Name = "cmbNetType"
        Me.cmbNetType.Items.AddRange(New Object() {"Авто", "GPRS/EDGE", "3G/4G", "3G", "4G"})
        Theme.Combo(Me.cmbNetType, 260)

        Me.lblPIN.AutoSize = True
        Me.lblPIN.Name = "lblPIN"
        Me.lblPIN.Text = "----"
        Theme.Value(Me.lblPIN)

        Me.btnChangePIN.Name = "btnChangePIN"
        Me.btnChangePIN.Text = "Сменить ПИН"
        Theme.Secondary(Me.btnChangePIN)
        Theme.Small(Me.btnChangePIN)

        Me.btnSave.Name = "btnSave"
        Me.btnSave.Text = "Сохранить изменения"
        Me.btnSave.Width = Theme.Px(200)
        Me.btnSave.Margin = New System.Windows.Forms.Padding(0, 0, Theme.GapRelated, 0)
        Theme.Primary(Me.btnSave)

        Me.btnRevert.Name = "btnRevert"
        Me.btnRevert.Text = "Отменить изменения"
        Me.btnRevert.Width = Theme.Px(200)
        Me.btnRevert.Margin = New System.Windows.Forms.Padding(0, 0, Theme.GapRelated, 0)
        Theme.Secondary(Me.btnRevert)

        Me.Button1.Name = "Button1"
        Me.Button1.Text = "Считать с платы"
        Me.Button1.Width = Theme.Px(176)
        Me.Button1.Margin = New System.Windows.Forms.Padding(0, 0, Theme.GapRelated, 0)
        Theme.Secondary(Me.Button1)

        Me.btnDefault.Name = "btnDefault"
        Me.btnDefault.Text = "Записать настройки по умолчанию"
        Me.btnDefault.Width = Theme.Px(280)
        Me.btnDefault.Margin = New System.Windows.Forms.Padding(0, 0, Theme.GapRelated, Theme.GapRelated)
        Theme.DangerOutline(Me.btnDefault)
        Theme.Small(Me.btnDefault)

        Me.btnFactory.Name = "btnFactory"
        Me.btnFactory.Text = "Сброс на заводские настройки"
        Me.btnFactory.Width = Theme.Px(256)
        Me.btnFactory.Margin = New System.Windows.Forms.Padding(0, 0, Theme.GapRelated, Theme.GapRelated)
        Theme.DangerOutline(Me.btnFactory)
        Theme.Small(Me.btnFactory)

        Me.Label20.Name = "Label20"
        Me.Label20.Text = "Перезагрузить контроллер"
        Me.Label20.Width = Theme.Px(224)
        Me.Label20.Margin = New System.Windows.Forms.Padding(0, 0, Theme.GapRelated, Theme.GapRelated)
        Theme.Secondary(Me.Label20)
        Theme.Small(Me.Label20)

        Me.btnTranspMode.Name = "btnTranspMode"
        Me.btnTranspMode.Text = "Прозрачный режим с модемом"
        Me.btnTranspMode.Width = Theme.Px(256)
        Me.btnTranspMode.Margin = New System.Windows.Forms.Padding(0, 0, Theme.GapRelated, Theme.GapRelated)
        Theme.Secondary(Me.btnTranspMode)
        Theme.Small(Me.btnTranspMode)

        Me.saveRow.AutoSize = True
        Me.saveRow.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.saveRow.WrapContents = False
        Me.saveRow.Margin = New System.Windows.Forms.Padding(0, Theme.GapBlock, 0, 0)
        Me.saveRow.Controls.Add(Me.btnSave)
        Me.saveRow.Controls.Add(Me.btnRevert)
        Me.saveRow.Controls.Add(Me.Button1)

        Me.serviceRow.AutoSize = True
        Me.serviceRow.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.serviceRow.Margin = New System.Windows.Forms.Padding(0)
        Me.serviceRow.Controls.Add(Me.Label20)
        Me.serviceRow.Controls.Add(Me.btnTranspMode)
        Me.serviceRow.Controls.Add(Me.btnDefault)
        Me.serviceRow.Controls.Add(Me.btnFactory)

        Me.tabSettings.Text = "Настройки"
        Me.tabSettings.BackColor = Theme.WindowBack
        Me.tabSettings.AutoScroll = False
        Dim connectionPage = Page(
            Theme.SettingsSection("Серверы", "Адреса подключения контроллера",
                      Row("Адрес сервера 1", Me.boxServer1, Me.Label21),
                      Note(Me.lblServer1Err), Row("", Me.chServer1),
                      Row("Адрес сервера 2", Me.boxServer2),
                      Note(Me.lblServer2Err), Row("", Me.chServer2)),
            Theme.SettingsSection("Мобильная связь", "",
                      Row("Точка доступа", Me.boxOperator),
                      Note(Hint("Пусто — параметры оператора. Иначе: точка доступа, логин, пароль.")),
                      Row("Разрешённые сети", Me.cmbNetType), Row("VoLTE", Me.cmbVoLTE)))
        Dim busPage = Page(Build485())
        Dim accessPage = Page(Theme.SettingsSection("Доступ", "Управление ПИН-кодом контроллера", Row("ПИН-код", Me.lblPIN, Me.btnChangePIN)))
        Me.serviceRow.Controls.Add(Me.Button1)
        Theme.ServiceActions(Me.serviceRow, Me.Button1)
        Dim servicePage = Page(Theme.SettingsSection("Сервис", "Чтение настроек и обслуживание контроллера", Me.serviceRow))
        Me.tabSettings.Controls.Add(Theme.SettingsWorkspace(
            New Control() {connectionPage, busPage, accessPage},
            Me.btnSave, Me.btnRevert, Me.lblSaveState, Me.btnDefault))

        ' ---- вкладка «Идентификаторы» ----
        Me.lblUsedMem.AutoSize = True
        Me.lblUsedMem.Name = "lblUsedMem"
        Me.lblUsedMem.Text = "---"
        Me.lblUsedMem.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapRelated)
        Theme.BigValue(Me.lblUsedMem)

        Me.meterMem.Name = "meterMem"
        Me.meterMem.Maximum = 50000
        Me.meterMem.Value = 0
        Me.meterMem.Size = New System.Drawing.Size(Theme.Px(240), Theme.Px(8))
        Me.meterMem.Margin = New System.Windows.Forms.Padding(0)

        Me.btnImport.Name = "btnImport"
        Me.btnImport.Text = "Загрузить из файла"
        Me.btnImport.Width = Theme.Px(188)
        Me.btnImport.Margin = New System.Windows.Forms.Padding(0, 0, Theme.GapRelated, 0)
        Theme.Secondary(Me.btnImport)
        Theme.Small(Me.btnImport)

        Me.btnExport.Name = "btnExport"
        Me.btnExport.Text = "Выгрузить в файл"
        Me.btnExport.Width = Theme.Px(188)
        Me.btnExport.Margin = New System.Windows.Forms.Padding(0, 0, Theme.GapRelated, 0)
        Theme.Secondary(Me.btnExport)
        Theme.Small(Me.btnExport)

        Me.btnClear.Name = "btnClear"
        Me.btnClear.Text = "Очистить память"
        Me.btnClear.Width = Theme.Px(188)
        Theme.DangerOutline(Me.btnClear)
        Theme.Small(Me.btnClear)

        Me.idsRow.AutoSize = True
        Me.idsRow.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.idsRow.WrapContents = False
        Me.idsRow.Margin = New System.Windows.Forms.Padding(0)
        Me.idsRow.Controls.Add(Me.btnImport)
        Me.idsRow.Controls.Add(Me.btnExport)

        Me.idsClearRow.AutoSize = True
        Me.idsClearRow.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.idsClearRow.WrapContents = False
        Me.idsClearRow.Margin = New System.Windows.Forms.Padding(0)
        Me.idsClearRow.Controls.Add(Me.btnClear)

        Me.tabIds.Text = "Идентификаторы"
        Me.tabIds.BackColor = Theme.WindowBack
        Me.tabIds.AutoScroll = True
        Me.tabIds.Controls.Add(Page(
            Theme.PageHeading("Идентификаторы", "Память контроллера и обслуживание базы доступа"),
            CardBlock("Память идентификаторов",
                      Row("Занято", Me.lblUsedMem),
                      Note(Me.meterMem)),
            CardBlock("База идентификаторов",
                      Me.idsRow,
                      Hint("Импорт и экспорт файлов пока недоступны.")),
            CardBlock("Очистка",
                      Me.idsClearRow,
                      Hint("Удаляет из памяти контроллера все идентификаторы. Вернуть их можно только повторной загрузкой с сервера."))))

        ' ---- вкладка «Объект» ----
        Me.lblObjNum.AutoSize = True
        Me.lblObjNum.Name = "lblObjNum"
        Me.lblObjNum.Text = "---"
        Theme.Value(Me.lblObjNum)

        Me.lblObjName.AutoSize = True
        Me.lblObjName.Name = "lblObjName"
        Me.lblObjName.Text = "---"
        Theme.Value(Me.lblObjName)

        Me.lblObjController.AutoSize = True
        Me.lblObjController.Name = "lblObjController"
        Me.lblObjController.Text = "---"
        Theme.Value(Me.lblObjController)

        Me.lblAdmin.AutoSize = True
        Me.lblAdmin.Name = "lblAdmin"
        Me.lblAdmin.Text = "---"
        Theme.Value(Me.lblAdmin)

        Me.btnUpdateObj.Name = "btnUpdateObj"
        Me.btnUpdateObj.Text = "Обновить с сервера"
        Me.btnUpdateObj.Width = Theme.Px(200)
        Me.btnUpdateObj.Margin = New System.Windows.Forms.Padding(0, 0, Theme.GapRelated, 0)
        Theme.Secondary(Me.btnUpdateObj)
        Theme.Small(Me.btnUpdateObj)

        Me.btnUnConnect.Name = "btnUnConnect"
        Me.btnUnConnect.Text = "Отвязать от объекта"
        Me.btnUnConnect.Width = Theme.Px(200)
        Theme.DangerOutline(Me.btnUnConnect)
        Theme.Small(Me.btnUnConnect)

        Me.objRow.AutoSize = True
        Me.objRow.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.objRow.WrapContents = False
        Me.objRow.Margin = New System.Windows.Forms.Padding(0)
        Me.objRow.Controls.Add(Me.btnUpdateObj)
        Me.objRow.Controls.Add(Me.btnUnConnect)

        Me.tabObject.Text = "Объект"
        Me.tabObject.BackColor = Theme.WindowBack
        Me.tabObject.AutoScroll = True
        Me.tabObject.Controls.Add(Page(
            Theme.PageHeading("Объект", "Привязка контроллера и сведения с сервера"),
            CardBlock("Объект",
                      Row("Уникальный номер", Me.lblObjNum),
                      Row("Наименование", Me.lblObjName),
                      Row("Контроллер на объекте", Me.lblObjController),
                      Row("Администратор", Me.lblAdmin),
                      Hint("Сведения приходят с сервера и здесь только показываются.")),
            CardBlock("Действия",
                      Me.objRow,
                      Hint("«Обновить с сервера» запрашивает привязку заново. «Отвязать от объекта» снимает её - это делается на сервере и назад само не вернётся."))))

        ' ---- вкладка «Прошивка» ----
        Me.btnUpdateFW.Name = "btnUpdateFW"
        Me.btnUpdateFW.Text = "Выбрать файл и обновить"
        Me.btnUpdateFW.Width = Theme.Px(240)
        Me.btnUpdateFW.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapRelated)
        Theme.Primary(Me.btnUpdateFW)

        Me.lblFwHint.AutoSize = True
        Me.lblFwHint.Name = "lblFwHint"
        Me.lblFwHint.MaximumSize = New System.Drawing.Size(Theme.Px(560), 0)
        Me.lblFwHint.Text = "Программа сама найдёт свежий файл прошивки и проведёт по шагам. Около 1 минуты, потом сама подключится к плате и покажет итог."
        Me.lblFwHint.Margin = New System.Windows.Forms.Padding(0)
        Theme.Caption(Me.lblFwHint)

        Me.btnUpdateBoot.Name = "btnUpdateBoot"
        Me.btnUpdateBoot.Text = "Заменить загрузчик"
        Me.btnUpdateBoot.Width = Theme.Px(300)
        Me.btnUpdateBoot.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapRelated)
        Theme.DangerOutline(Me.btnUpdateBoot)

        Me.lblBootHint.AutoSize = True
        Me.lblBootHint.Name = "lblBootHint"
        Me.lblBootHint.MaximumSize = New System.Drawing.Size(Theme.Px(560), 0)
        Me.lblBootHint.Text = "Нужно один раз, если на плате старый загрузчик. Вернуть прежний нельзя. Программа сама найдёт файл, сначала проверит его на плате и проведёт по шагам."
        Me.lblBootHint.Margin = New System.Windows.Forms.Padding(0)
        Theme.Caption(Me.lblBootHint)

        Me.tabFW.Text = "Прошивка"
        Me.tabFW.BackColor = Theme.WindowBack
        Me.tabFW.AutoScroll = True
        Me.tabFW.Controls.Add(Page(
            Theme.PageHeading("Прошивка и загрузчик", "Обслуживание программного обеспечения контроллера"),
            CardBlock("Прошивка контроллера", Me.btnUpdateFW, Me.lblFwHint),
            CardBlock("Загрузчик", Me.btnUpdateBoot, Me.lblBootHint)))

        ' ---- вкладка «Журнал» ----
        Me.CheckBox1.AutoSize = True
        Me.CheckBox1.Name = "CheckBox1"
        Me.CheckBox1.Text = "AT-команды"
        Me.CheckBox1.Margin = New System.Windows.Forms.Padding(0, 0, Theme.GapSection, 0)

        Me.CheckBox2.AutoSize = True
        Me.CheckBox2.Name = "CheckBox2"
        Me.CheckBox2.Text = "События GSM"
        Me.CheckBox2.Margin = New System.Windows.Forms.Padding(0, 0, Theme.GapSection, 0)

        Me.CheckBox3.AutoSize = True
        Me.CheckBox3.Name = "CheckBox3"
        Me.CheckBox3.Text = "Общие события"
        Me.CheckBox3.Margin = New System.Windows.Forms.Padding(0, 0, Theme.GapSection, 0)

        Me.CheckBox4.AutoSize = True
        Me.CheckBox4.Name = "CheckBox4"
        Me.CheckBox4.Text = "Остановить опрос статуса"
        Me.CheckBox4.Margin = New System.Windows.Forms.Padding(0, 0, Theme.GapSection, 0)

        Me.chAutoScroll.AutoSize = True
        Me.chAutoScroll.Name = "chAutoScroll"
        Me.chAutoScroll.Text = "Прокручивать к последней записи"
        Me.chAutoScroll.Checked = True
        Me.chAutoScroll.Margin = New System.Windows.Forms.Padding(0)

        Me.logFilters.AutoSize = True
        Me.logFilters.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.logFilters.Dock = System.Windows.Forms.DockStyle.Fill
        Me.logFilters.Margin = New System.Windows.Forms.Padding(0, 0, 0, Theme.GapRelated)
        Me.logFilters.Controls.Add(Me.CheckBox1)
        Me.logFilters.Controls.Add(Me.CheckBox2)
        Me.logFilters.Controls.Add(Me.CheckBox3)
        Me.logFilters.Controls.Add(Me.CheckBox4)
        Me.logFilters.Controls.Add(Me.chAutoScroll)

        Me.RichTextBox1.Name = "RichTextBox1"
        Me.RichTextBox1.Dock = System.Windows.Forms.DockStyle.Fill
        Me.RichTextBox1.Font = Theme.FontMono
        Me.RichTextBox1.ForeColor = Theme.TextMain
        Me.RichTextBox1.BackColor = Theme.Surface
        Me.RichTextBox1.BorderStyle = System.Windows.Forms.BorderStyle.None
        Me.RichTextBox1.[ReadOnly] = True
        Me.RichTextBox1.WordWrap = True
        Me.RichTextBox1.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical
        Me.RichTextBox1.Margin = New System.Windows.Forms.Padding(0)

        Me.Button2.Name = "Button2"
        Me.Button2.Text = "Копировать"
        Me.Button2.Width = Theme.Px(148)
        Me.Button2.Margin = New System.Windows.Forms.Padding(0, Theme.GapRelated, Theme.GapRelated, 0)
        Theme.Secondary(Me.Button2)
        Theme.Small(Me.Button2)

        Me.btnClearLog.Name = "btnClearLog"
        Me.btnClearLog.Text = "Очистить"
        Me.btnClearLog.Width = Theme.Px(148)
        Me.btnClearLog.Margin = New System.Windows.Forms.Padding(0, Theme.GapRelated, 0, 0)
        Theme.Secondary(Me.btnClearLog)
        Theme.Small(Me.btnClearLog)

        Me.logActions.AutoSize = True
        Me.logActions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.logActions.Dock = System.Windows.Forms.DockStyle.Fill
        Me.logActions.WrapContents = False
        Me.logActions.Margin = New System.Windows.Forms.Padding(0)
        Me.logActions.Controls.Add(Me.Button2)
        Me.logActions.Controls.Add(Me.btnClearLog)

        Me.logRoot.ColumnCount = 1
        Me.logRoot.RowCount = 4
        Me.logRoot.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100.0!))
        Me.logRoot.RowStyles.Add(New System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize))
        Me.logRoot.RowStyles.Add(New System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100.0!))
        Me.logRoot.RowStyles.Add(New System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize))
        Me.logRoot.Dock = System.Windows.Forms.DockStyle.Fill
        Me.logRoot.Padding = New System.Windows.Forms.Padding(Theme.GapSection)
        Me.logRoot.RowStyles.Insert(0, New System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize))
        Me.logRoot.Controls.Add(Theme.PageHeading("Журнал событий", "Диагностика обмена и сообщений контроллера"), 0, 0)
        Me.logRoot.Controls.Add(Me.logFilters, 0, 1)
        Me.logRoot.Controls.Add(Me.RichTextBox1, 0, 2)
        Me.logRoot.Controls.Add(Me.logActions, 0, 3)

        Me.tabLog.Text = "Журнал"
        Me.tabLog.BackColor = Theme.WindowBack
        Me.tabLog.Controls.Add(Me.logRoot)

        ' ---- вкладки ----
        Me.tabs.Name = "tabs"
        Me.tabs.Dock = System.Windows.Forms.DockStyle.Fill
        Me.tabs.Font = Theme.FontBody
        Me.tabs.Margin = New System.Windows.Forms.Padding(0)
        Me.tabs.Padding = New System.Drawing.Point(Theme.GapBlock, Theme.GapRelated)
        Me.tabs.SelectedIndex = 0
        Me.tabs.Controls.Add(Me.tabState)
        Me.tabs.Controls.Add(Me.tabSettings)
        Me.tabs.Controls.Add(Me.tabIds)
        Me.tabs.Controls.Add(Me.tabObject)
        Me.tabs.Controls.Add(Me.tabFW)
        Me.tabs.Controls.Add(Me.tabLog)
        ConfigureSections()

        ' ---- строка итога ----
        Me.lblLastUpdate.AutoSize = True
        Me.lblLastUpdate.Name = "lblLastUpdate"
        Me.lblLastUpdate.Text = "Последнее обновление: —"
        Me.lblLastUpdate.Anchor = System.Windows.Forms.AnchorStyles.Left
        Me.lblLastUpdate.Margin = New System.Windows.Forms.Padding(0)
        Theme.Caption(Me.lblLastUpdate)

        Me.lblSaveState.AutoSize = True
        Me.lblSaveState.Name = "lblSaveState"
        Me.lblSaveState.Text = ""
        Me.lblSaveState.Anchor = System.Windows.Forms.AnchorStyles.Right
        Me.lblSaveState.Margin = New System.Windows.Forms.Padding(0)
        Theme.Caption(Me.lblSaveState)

        Me.statusRow.AutoSize = True
        Me.statusRow.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.statusRow.ColumnCount = 2
        Me.statusRow.RowCount = 1
        Me.statusRow.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100.0!))
        Me.statusRow.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize))
        Me.statusRow.Dock = System.Windows.Forms.DockStyle.Top
        Me.statusRow.Margin = New System.Windows.Forms.Padding(0)
        Me.statusRow.Controls.Add(Me.lblLastUpdate, 0, 0)
        Me.statusRow.Controls.Add(Me.lblSaveState, 1, 0)

        Me.pnlStatusBar.AutoSize = True
        Me.pnlStatusBar.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.pnlStatusBar.BackColor = Theme.Surface
        Me.pnlStatusBar.Dock = System.Windows.Forms.DockStyle.Fill
        Me.pnlStatusBar.Margin = New System.Windows.Forms.Padding(0)
        Me.pnlStatusBar.Padding = New System.Windows.Forms.Padding(Theme.GapSection, Theme.GapRelated, Theme.GapSection, Theme.GapRelated)
        Me.pnlStatusBar.Controls.Add(Me.statusRow)

        ' ---- окно ----
        Me.root.ColumnCount = 1
        Me.root.RowCount = 3
        Me.root.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100.0!))
        Me.root.RowStyles.Add(New System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize))
        Me.root.RowStyles.Add(New System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100.0!))
        Me.root.RowStyles.Add(New System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize))
        Me.root.Dock = System.Windows.Forms.DockStyle.Fill
        Me.root.Controls.Add(Me.pnlHead, 0, 0)
        Me.root.Controls.Add(Theme.MainNavigation(Me.tabs), 0, 1)
        Me.root.Controls.Add(Me.pnlStatusBar, 0, 2)

        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None
        Me.ClientSize = New System.Drawing.Size(Theme.Px(1280), Theme.Px(800))
        Me.MinimumSize = New System.Drawing.Size(Theme.Px(1000), Theme.Px(700))
        Me.Controls.Add(Me.root)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable
        Me.Name = "Form2"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "Домовой NEXT"
        Theme.StyleForm(Me)
        AddHandler Me.tabs.SelectedIndexChanged, Sub(sender, e)
            Me.pnlStatusBar.Visible = Me.tabs.SelectedTab IsNot Me.tabSettings
        End Sub

        CType(Me.picLogo, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    ' ---- сборка однотипных кусков ----

    ' Содержимое вкладки: блоки сверху вниз, поля по краям, прокрутка при нехватке места.
    Private Function Page(ByVal ParamArray blocks As Control()) As TableLayoutPanel
        Dim p As New TableLayoutPanel()
        p.ColumnCount = 1
        p.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0!))
        p.AutoSize = True
        p.AutoSizeMode = AutoSizeMode.GrowAndShrink
        p.Dock = DockStyle.Top
        p.Padding = New Padding(Theme.GapSection)

        For Each b As Control In blocks
            p.Controls.Add(b)
        Next

        Return p
    End Function

    ' Смысловой блок: белая поверхность, заголовок группы, дальше строки.
    Private Function CardBlock(ByVal title As String, ByVal ParamArray rows As Control()) As TableLayoutPanel
        Return Theme.Card(title, rows)
    End Function

    ' Блок действий без заголовка и без рамки: кнопки сохранения под настройками.
    Private Function SaveBlock(ByVal row As Control) As TableLayoutPanel
        Dim c As New TableLayoutPanel()
        c.ColumnCount = 1
        c.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0!))
        c.AutoSize = True
        c.AutoSizeMode = AutoSizeMode.GrowAndShrink
        c.Margin = New Padding(0, 0, 0, Theme.GapSection)
        c.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        c.Controls.Add(row)
        Return c
    End Function

    ' Строка блока: название слева, значение, справа - действие, если оно есть.
    Private Function CompactRow(caption As String, value As Control, Optional action As Control = Nothing) As TableLayoutPanel
        Dim line = Row(caption, value, action)
        line.ColumnStyles(0).Width = Theme.Px(If(New String() {"Мобильная сеть", "Интернет", "Соединение с сервером", "Технология сейчас", "Уровень сигнала"}.Contains(caption), 190, 146))
        Return line
    End Function

    Private Function Row(ByVal caption As String, ByVal value As Control, Optional ByVal action As Control = Nothing) As TableLayoutPanel
        Dim r As New TableLayoutPanel()
        r.ColumnCount = 3
        r.RowCount = 1
        r.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, CSng(Theme.Px(220))))
        r.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0!))
        r.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        r.AutoSize = True
        r.AutoSizeMode = AutoSizeMode.GrowAndShrink
        r.Margin = New Padding(0, 0, 0, Theme.GapRow)
        r.Anchor = AnchorStyles.Left Or AnchorStyles.Right

        Dim cap As New Label()
        cap.AutoSize = True
        cap.Text = caption
        cap.Anchor = AnchorStyles.Left
        cap.Margin = New Padding(0, 0, Theme.GapRelated, 0)
        Theme.Caption(cap)

        value.Anchor = AnchorStyles.Left
        If TypeOf value Is Label Then value.Margin = New Padding(0)

        r.Controls.Add(cap, 0, 0)
        r.Controls.Add(value, 1, 0)

        If action IsNot Nothing Then
            action.Anchor = AnchorStyles.Right
            action.Margin = New Padding(Theme.GapRelated, 0, 0, 0)
            r.Controls.Add(action, 2, 0)
        End If

        Return r
    End Function

    ' Пояснение или ошибка под полем: стоит под колонкой значений, а не под подписью.
    Private Function Note(ByVal text As Control) As TableLayoutPanel
        Dim r As New TableLayoutPanel()
        r.ColumnCount = 2
        r.RowCount = 1
        r.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, CSng(Theme.Px(220))))
        r.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0!))
        r.AutoSize = True
        r.AutoSizeMode = AutoSizeMode.GrowAndShrink
        r.Margin = New Padding(0)
        r.Anchor = AnchorStyles.Left Or AnchorStyles.Right

        text.Anchor = AnchorStyles.Left
        text.Margin = New Padding(0, 0, 0, Theme.GapRow)

        Dim spacer As New Label()
        spacer.Text = ""
        spacer.AutoSize = True
        spacer.Margin = New Padding(0)

        r.Controls.Add(spacer, 0, 0)
        r.Controls.Add(text, 1, 0)

        Return r
    End Function

    ' Неизменяемое пояснение под полем или под строкой кнопок.
    Private Function Hint(ByVal text As String) As Label
        Dim l As New Label()
        l.AutoSize = True
        l.MaximumSize = New Size(Theme.Px(640), 0)
        l.Text = text
        l.Margin = New Padding(0, Theme.GapRelated, 0, 0)
        Theme.Note(l, False)
        Return l
    End Function

    ' Точка состояния и подпись рядом с ней.
    Private Function DotPair(ByVal dot As Label, ByVal text As Label) As FlowLayoutPanel
        Dim f As New FlowLayoutPanel()
        f.AutoSize = True
        f.AutoSizeMode = AutoSizeMode.GrowAndShrink
        f.WrapContents = False
        f.Margin = New Padding(0)
        dot.Margin = New Padding(0, 0, Theme.GapLabel, 0)
        text.Margin = New Padding(0, 3, 0, 0)
        f.Controls.Add(dot)
        f.Controls.Add(text)
        Return f
    End Function

    Friend WithEvents root As TableLayoutPanel
    Friend WithEvents pnlHead As Panel
    Friend WithEvents headRow As TableLayoutPanel
    Friend WithEvents headLine As Panel
    Friend WithEvents picLogo As PictureBox
    Friend WithEvents headDevice As TableLayoutPanel
    Friend WithEvents lblIMEICap As Label
    Friend WithEvents lblIMEI As Label
    Friend WithEvents headVersions As FlowLayoutPanel
    Friend WithEvents lblHeadModel As Label
    Friend WithEvents lblHeadHW As Label
    Friend WithEvents lblHeadFW As Label
    Friend WithEvents lblHeadBoot As Label
    Friend WithEvents headState As FlowLayoutPanel
    Friend WithEvents dotConn As Label
    Friend WithEvents lblConnState As Label
    Friend WithEvents lblPortInfo As Label

    Friend WithEvents tabs As TabControl
    Friend WithEvents tabState As TabPage
    Friend WithEvents tabSettings As TabPage
    Friend WithEvents tabIds As TabPage
    Friend WithEvents tabObject As TabPage
    Friend WithEvents tabFW As TabPage
    Friend WithEvents tabLog As TabPage

    Friend WithEvents pnlStatusBar As Panel
    Friend WithEvents statusRow As TableLayoutPanel
    Friend WithEvents lblLastUpdate As Label
    Friend WithEvents lblSaveState As Label

    Friend WithEvents dotGSM As Label
    Friend WithEvents lblGSM As Label
    Friend WithEvents dotInt As Label
    Friend WithEvents lblInt As Label
    Friend WithEvents dotServer As Label
    Friend WithEvents lblServerConnect As Label
    Friend WithEvents lblNet As Label
    Friend WithEvents pbSig As Meter
    Friend WithEvents lblSig As Label
    Friend WithEvents sigRow As FlowLayoutPanel

    Friend WithEvents lblModuleType As Label
    Friend WithEvents Label19 As Button
    Friend WithEvents lblHW As Label
    Friend WithEvents lblFW As Label
    Friend WithEvents lblBoot As Label
    Friend WithEvents lblTime As Label
    Friend WithEvents Label16 As Button

    Friend WithEvents dotIN1 As Label
    Friend WithEvents lbIN1 As Label
    Friend WithEvents dotIN2 As Label
    Friend WithEvents lblIN2 As Label
    Friend WithEvents dotOUT1 As Label
    Friend WithEvents lblOUT1 As Label
    Friend WithEvents btnTestOUT1 As Button
    Friend WithEvents dotOUT2 As Label
    Friend WithEvents lbOUT2 As Label
    Friend WithEvents btnTestOUT2 As Button

    Friend WithEvents boxServer1 As Panel
    Friend WithEvents txtServer1 As TextBox
    Friend WithEvents lblServer1Err As Label
    Friend WithEvents chServer1 As CheckBox
    Friend WithEvents Label21 As Button
    Friend WithEvents boxServer2 As Panel
    Friend WithEvents txtServer2 As TextBox
    Friend WithEvents lblServer2Err As Label
    Friend WithEvents chServer2 As CheckBox
    Friend WithEvents boxOperator As Panel
    Friend WithEvents txtOperator As TextBox
    Friend WithEvents cmbNetType As ComboBox
    Friend WithEvents cmbVoLTE As ComboBox
    Friend WithEvents btnRevert As Button
    Friend WithEvents lblPIN As Label
    Friend WithEvents btnChangePIN As Button
    Friend WithEvents Button1 As Button
    Friend WithEvents btnSave As Button
    Friend WithEvents btnDefault As Button
    Friend WithEvents btnFactory As Button
    Friend WithEvents btnTranspMode As Button
    Friend WithEvents Label20 As Button
    Friend WithEvents saveRow As FlowLayoutPanel
    Friend WithEvents serviceRow As FlowLayoutPanel

    Friend WithEvents lblUsedMem As Label
    Friend WithEvents meterMem As Meter
    Friend WithEvents btnImport As Button
    Friend WithEvents btnExport As Button
    Friend WithEvents btnClear As Button
    Friend WithEvents idsRow As FlowLayoutPanel
    Friend WithEvents idsClearRow As FlowLayoutPanel

    Friend WithEvents lblObjNum As Label
    Friend WithEvents lblObjName As Label
    Friend WithEvents lblObjController As Label
    Friend WithEvents lblAdmin As Label
    Friend WithEvents btnUpdateObj As Button
    Friend WithEvents btnUnConnect As Button
    Friend WithEvents objRow As FlowLayoutPanel

    Friend WithEvents btnUpdateFW As Button
    Friend WithEvents lblFwHint As Label
    Friend WithEvents btnUpdateBoot As Button
    Friend WithEvents lblBootHint As Label

    Friend WithEvents CheckBox1 As CheckBox
    Friend WithEvents CheckBox2 As CheckBox
    Friend WithEvents CheckBox3 As CheckBox
    Friend WithEvents CheckBox4 As CheckBox
    Friend WithEvents chAutoScroll As CheckBox
    Friend WithEvents RichTextBox1 As RichTextBox
    Friend WithEvents btnClearLog As Button
    Friend WithEvents Button2 As Button
    Friend WithEvents logFilters As FlowLayoutPanel
    Friend WithEvents logActions As FlowLayoutPanel
    Friend WithEvents logRoot As TableLayoutPanel

    Friend WithEvents OpenFileDialog1 As OpenFileDialog
End Class
