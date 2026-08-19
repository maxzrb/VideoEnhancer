Imports System
Imports System.Drawing
Imports System.Windows.Forms

Namespace PluginLayoutDesigner

    Partial Public Class PluginLayoutForm
        Inherits Form

        ''' <summary>必需的设计器变量。</summary>
        Private components As System.ComponentModel.IContainer

        ' ── 总开关行（对应 sectionMaster）──
        Friend WithEvents pnlMaster As Panel
        Friend WithEvents chkMaster As CheckBox
        Friend WithEvents lblCapMaster As Label

        ' ── 超分行（对应 sectionUpscale）──
        Friend WithEvents pnlUpscale As Panel
        Friend WithEvents chkUpscale As CheckBox
        Friend WithEvents lblUpscale As Label
        Friend WithEvents lblUpscaleModel As Label
        Friend WithEvents cmbModel As ComboBox
        Friend WithEvents lblCapUpscale As Label

        ' ── 推理方式行（对应 sectionBackend）──
        Friend WithEvents pnlBackend As Panel
        Friend WithEvents lblBackend As Label
        Friend WithEvents cmbBackend As ComboBox
        Friend WithEvents lblCapBackend As Label

        ' ── 补帧行（对应 sectionInterp）──
        Friend WithEvents pnlInterp As Panel
        Friend WithEvents chkInterp As CheckBox
        Friend WithEvents lblInterp As Label
        Friend WithEvents lblInterpModel As Label
        Friend WithEvents cmbInterp As ComboBox
        Friend WithEvents lblFactor As Label
        Friend WithEvents lblCapInterp As Label

        ' ── exe 路径行（对应 sectionExe）──
        Friend WithEvents pnlExe As Panel
        Friend WithEvents lblExe As Label
        Friend WithEvents btnExe As Button

        ' ── 状态区（对应 sectionStatus）──
        Friend WithEvents pnlStatus As Panel
        Friend WithEvents lblStatus As Label
        Friend WithEvents lblLegend As Label

        ''' <summary>清理所有正在使用的资源。</summary>
        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing AndAlso (components IsNot Nothing) Then
                components.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub

        ''' <summary>设计器支持所需的方法 - 不要使用代码编辑器修改此方法的内容。</summary>
        <System.Diagnostics.DebuggerStepThrough()>
        Private Sub InitializeComponent()
            Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(PluginLayoutForm))
            pnlMaster = New Panel()
            lblCapMaster = New Label()
            chkMaster = New CheckBox()
            pnlUpscale = New Panel()
            lblCapUpscale = New Label()
            lblUpscale = New Label()
            cmbBackend = New ComboBox()
            lblBackend = New Label()
            chkUpscale = New CheckBox()
            cmbModel = New ComboBox()
            lblUpscaleModel = New Label()
            pnlBackend = New Panel()
            lblCapBackend = New Label()
            pnlInterp = New Panel()
            lblCapInterp = New Label()
            cmbInterp = New ComboBox()
            lblInterpModel = New Label()
            lblInterp = New Label()
            chkInterp = New CheckBox()
            lblFactor = New Label()
            pnlExe = New Panel()
            btnExe = New Button()
            lblExe = New Label()
            pnlStatus = New Panel()
            lblLegend = New Label()
            lblStatus = New Label()
            Panel1 = New Panel()
            Label1 = New Label()
            ComboBox1 = New ComboBox()
            Panel2 = New Panel()
            Label5 = New Label()
            Label4 = New Label()
            Label2 = New Label()
            Label3 = New Label()
            lblMaster = New Label()
            pnlMaster.SuspendLayout()
            pnlUpscale.SuspendLayout()
            pnlBackend.SuspendLayout()
            pnlInterp.SuspendLayout()
            pnlExe.SuspendLayout()
            pnlStatus.SuspendLayout()
            Panel1.SuspendLayout()
            Panel2.SuspendLayout()
            SuspendLayout()
            ' 
            ' pnlMaster
            ' 
            pnlMaster.BackColor = Color.FromArgb(CByte(250), CByte(250), CByte(252))
            pnlMaster.BorderStyle = BorderStyle.FixedSingle
            pnlMaster.Controls.Add(lblCapMaster)
            pnlMaster.Controls.Add(lblMaster)
            pnlMaster.Controls.Add(chkMaster)
            pnlMaster.Location = New Point(54, 34)
            pnlMaster.Name = "pnlMaster"
            pnlMaster.Size = New Size(900, 50)
            pnlMaster.TabIndex = 0
            ' 
            ' lblCapMaster
            ' 
            lblCapMaster.ForeColor = Color.FromArgb(CByte(150), CByte(150), CByte(150))
            lblCapMaster.Location = New Point(800, 15)
            lblCapMaster.Name = "lblCapMaster"
            lblCapMaster.Size = New Size(100, 20)
            lblCapMaster.TabIndex = 3
            lblCapMaster.Text = "master"
            lblCapMaster.TextAlign = ContentAlignment.MiddleRight
            ' 
            ' chkMaster
            ' 
            chkMaster.Location = New Point(11, 8)
            chkMaster.Name = "chkMaster"
            chkMaster.Size = New Size(66, 34)
            chkMaster.TabIndex = 1
            chkMaster.TextAlign = ContentAlignment.MiddleCenter
            ' 
            ' pnlUpscale
            ' 
            pnlUpscale.BackColor = Color.FromArgb(CByte(244), CByte(246), CByte(248))
            pnlUpscale.BorderStyle = BorderStyle.FixedSingle
            pnlUpscale.Controls.Add(lblCapUpscale)
            pnlUpscale.Controls.Add(lblUpscale)
            pnlUpscale.Controls.Add(cmbBackend)
            pnlUpscale.Controls.Add(lblBackend)
            pnlUpscale.Controls.Add(chkUpscale)
            pnlUpscale.Location = New Point(54, 173)
            pnlUpscale.Name = "pnlUpscale"
            pnlUpscale.Size = New Size(900, 56)
            pnlUpscale.TabIndex = 1
            ' 
            ' lblCapUpscale
            ' 
            lblCapUpscale.ForeColor = Color.FromArgb(CByte(150), CByte(150), CByte(150))
            lblCapUpscale.Location = New Point(800, 18)
            lblCapUpscale.Name = "lblCapUpscale"
            lblCapUpscale.Size = New Size(100, 20)
            lblCapUpscale.TabIndex = 5
            lblCapUpscale.Text = "upscale"
            lblCapUpscale.TextAlign = ContentAlignment.MiddleRight
            ' 
            ' lblUpscale
            ' 
            lblUpscale.Location = New Point(41, 12)
            lblUpscale.Name = "lblUpscale"
            lblUpscale.Padding = New Padding(14, 0, 0, 0)
            lblUpscale.Size = New Size(120, 34)
            lblUpscale.TabIndex = 2
            lblUpscale.Text = "超分开关"
            lblUpscale.TextAlign = ContentAlignment.MiddleLeft
            ' 
            ' cmbBackend
            ' 
            cmbBackend.DropDownStyle = ComboBoxStyle.DropDownList
            cmbBackend.Items.AddRange(New Object() {"NCNN (Vulkan)", "CUDA (PyTorch)"})
            cmbBackend.Location = New Point(337, 13)
            cmbBackend.Name = "cmbBackend"
            cmbBackend.Size = New Size(220, 32)
            cmbBackend.TabIndex = 2
            ' 
            ' lblBackend
            ' 
            lblBackend.Location = New Point(201, 11)
            lblBackend.Name = "lblBackend"
            lblBackend.Size = New Size(130, 36)
            lblBackend.TabIndex = 1
            lblBackend.Text = "选择推理方式"
            lblBackend.TextAlign = ContentAlignment.MiddleLeft
            ' 
            ' chkUpscale
            ' 
            chkUpscale.Location = New Point(11, 12)
            chkUpscale.Name = "chkUpscale"
            chkUpscale.Size = New Size(66, 34)
            chkUpscale.TabIndex = 1
            chkUpscale.TextAlign = ContentAlignment.MiddleCenter
            ' 
            ' cmbModel
            ' 
            cmbModel.DropDownStyle = ComboBoxStyle.DropDownList
            cmbModel.Items.AddRange(New Object() {"AnimeJaNai-V2-2x-Compact-36K", "RealESRGAN-AnimeVideoV3-2x", "RealESRGAN-AnimeVideoV3-4x"})
            cmbModel.Location = New Point(337, 10)
            cmbModel.Name = "cmbModel"
            cmbModel.Size = New Size(456, 32)
            cmbModel.TabIndex = 4
            ' 
            ' lblUpscaleModel
            ' 
            lblUpscaleModel.Location = New Point(201, 8)
            lblUpscaleModel.Name = "lblUpscaleModel"
            lblUpscaleModel.Size = New Size(110, 34)
            lblUpscaleModel.TabIndex = 3
            lblUpscaleModel.Text = "放大模型"
            lblUpscaleModel.TextAlign = ContentAlignment.MiddleLeft
            ' 
            ' pnlBackend
            ' 
            pnlBackend.BackColor = Color.FromArgb(CByte(238), CByte(240), CByte(244))
            pnlBackend.BorderStyle = BorderStyle.FixedSingle
            pnlBackend.Controls.Add(lblCapBackend)
            pnlBackend.Controls.Add(cmbModel)
            pnlBackend.Controls.Add(lblUpscaleModel)
            pnlBackend.Location = New Point(54, 229)
            pnlBackend.Name = "pnlBackend"
            pnlBackend.Size = New Size(900, 50)
            pnlBackend.TabIndex = 2
            ' 
            ' lblCapBackend
            ' 
            lblCapBackend.ForeColor = Color.FromArgb(CByte(150), CByte(150), CByte(150))
            lblCapBackend.Location = New Point(800, 15)
            lblCapBackend.Name = "lblCapBackend"
            lblCapBackend.Size = New Size(100, 20)
            lblCapBackend.TabIndex = 3
            lblCapBackend.Text = "backend"
            lblCapBackend.TextAlign = ContentAlignment.MiddleRight
            ' 
            ' pnlInterp
            ' 
            pnlInterp.BackColor = Color.FromArgb(CByte(232), CByte(235), CByte(240))
            pnlInterp.BorderStyle = BorderStyle.FixedSingle
            pnlInterp.Controls.Add(lblCapInterp)
            pnlInterp.Controls.Add(cmbInterp)
            pnlInterp.Controls.Add(lblInterpModel)
            pnlInterp.Controls.Add(lblInterp)
            pnlInterp.Controls.Add(chkInterp)
            pnlInterp.Location = New Point(54, 279)
            pnlInterp.Name = "pnlInterp"
            pnlInterp.Size = New Size(900, 56)
            pnlInterp.TabIndex = 3
            ' 
            ' lblCapInterp
            ' 
            lblCapInterp.ForeColor = Color.FromArgb(CByte(150), CByte(150), CByte(150))
            lblCapInterp.Location = New Point(800, 18)
            lblCapInterp.Name = "lblCapInterp"
            lblCapInterp.Size = New Size(100, 20)
            lblCapInterp.TabIndex = 7
            lblCapInterp.Text = "interp"
            lblCapInterp.TextAlign = ContentAlignment.MiddleRight
            ' 
            ' cmbInterp
            ' 
            cmbInterp.DropDownStyle = ComboBoxStyle.DropDownList
            cmbInterp.Items.AddRange(New Object() {"rife-v4.25", "rife-v4.26", "rife-v4.26-heavy"})
            cmbInterp.Location = New Point(337, 13)
            cmbInterp.Name = "cmbInterp"
            cmbInterp.Size = New Size(300, 32)
            cmbInterp.TabIndex = 4
            ' 
            ' lblInterpModel
            ' 
            lblInterpModel.Location = New Point(201, 10)
            lblInterpModel.Name = "lblInterpModel"
            lblInterpModel.Size = New Size(110, 34)
            lblInterpModel.TabIndex = 3
            lblInterpModel.Text = "补帧模型"
            lblInterpModel.TextAlign = ContentAlignment.MiddleLeft
            ' 
            ' lblInterp
            ' 
            lblInterp.Location = New Point(36, 6)
            lblInterp.Name = "lblInterp"
            lblInterp.Padding = New Padding(14, 0, 0, 0)
            lblInterp.Size = New Size(125, 34)
            lblInterp.TabIndex = 2
            lblInterp.Text = "补帧开关"
            lblInterp.TextAlign = ContentAlignment.MiddleLeft
            ' 
            ' chkInterp
            ' 
            chkInterp.Location = New Point(11, 7)
            chkInterp.Name = "chkInterp"
            chkInterp.Size = New Size(66, 34)
            chkInterp.TabIndex = 1
            chkInterp.TextAlign = ContentAlignment.MiddleCenter
            ' 
            ' lblFactor
            ' 
            lblFactor.Location = New Point(199, 9)
            lblFactor.Name = "lblFactor"
            lblFactor.Size = New Size(110, 34)
            lblFactor.TabIndex = 5
            lblFactor.Text = "补帧倍率"
            lblFactor.TextAlign = ContentAlignment.MiddleLeft
            ' 
            ' pnlExe
            ' 
            pnlExe.BackColor = Color.FromArgb(CByte(244), CByte(246), CByte(248))
            pnlExe.BorderStyle = BorderStyle.FixedSingle
            pnlExe.Controls.Add(btnExe)
            pnlExe.Controls.Add(lblExe)
            pnlExe.Location = New Point(54, 417)
            pnlExe.Name = "pnlExe"
            pnlExe.Size = New Size(900, 44)
            pnlExe.TabIndex = 4
            ' 
            ' btnExe
            ' 
            btnExe.Location = New Point(781, 4)
            btnExe.Name = "btnExe"
            btnExe.Size = New Size(110, 32)
            btnExe.TabIndex = 2
            btnExe.Text = "更改路径"
            ' 
            ' lblExe
            ' 
            lblExe.ForeColor = Color.FromArgb(CByte(70), CByte(70), CByte(70))
            lblExe.Location = New Point(3, 4)
            lblExe.Name = "lblExe"
            lblExe.Size = New Size(780, 32)
            lblExe.TabIndex = 1
            lblExe.Text = "videoenhancer.exe：C:\Path\videoenhancer.exe"
            lblExe.TextAlign = ContentAlignment.MiddleLeft
            ' 
            ' pnlStatus
            ' 
            pnlStatus.BackColor = Color.FromArgb(CByte(250), CByte(250), CByte(252))
            pnlStatus.BorderStyle = BorderStyle.FixedSingle
            pnlStatus.Controls.Add(lblLegend)
            pnlStatus.Controls.Add(lblStatus)
            pnlStatus.Location = New Point(54, 505)
            pnlStatus.Name = "pnlStatus"
            pnlStatus.Size = New Size(900, 214)
            pnlStatus.TabIndex = 5
            ' 
            ' lblLegend
            ' 
            lblLegend.ForeColor = Color.FromArgb(CByte(100), CByte(100), CByte(110))
            lblLegend.Location = New Point(3, 62)
            lblLegend.Name = "lblLegend"
            lblLegend.Size = New Size(900, 150)
            lblLegend.TabIndex = 2
            lblLegend.Text = resources.GetString("lblLegend.Text")
            ' 
            ' lblStatus
            ' 
            lblStatus.ForeColor = Color.FromArgb(CByte(120), CByte(120), CByte(120))
            lblStatus.Location = New Point(-1, 0)
            lblStatus.Name = "lblStatus"
            lblStatus.Size = New Size(900, 40)
            lblStatus.TabIndex = 1
            lblStatus.Text = "状态信息区（对应 _lblStatus）— 运行后鼠标悬停控件显示坐标，单击复制"
            lblStatus.TextAlign = ContentAlignment.MiddleLeft
            ' 
            ' Panel1
            ' 
            Panel1.BackColor = Color.FromArgb(CByte(232), CByte(235), CByte(240))
            Panel1.BorderStyle = BorderStyle.FixedSingle
            Panel1.Controls.Add(Label1)
            Panel1.Controls.Add(ComboBox1)
            Panel1.Controls.Add(lblFactor)
            Panel1.Location = New Point(54, 331)
            Panel1.Name = "Panel1"
            Panel1.Size = New Size(900, 56)
            Panel1.TabIndex = 8
            ' 
            ' Label1
            ' 
            Label1.ForeColor = Color.FromArgb(CByte(150), CByte(150), CByte(150))
            Label1.Location = New Point(800, 18)
            Label1.Name = "Label1"
            Label1.Size = New Size(100, 20)
            Label1.TabIndex = 7
            Label1.Text = "interp"
            Label1.TextAlign = ContentAlignment.MiddleRight
            ' 
            ' ComboBox1
            ' 
            ComboBox1.DropDownStyle = ComboBoxStyle.DropDownList
            ComboBox1.Items.AddRange(New Object() {"2 倍", "3 倍", "4 倍", "8 倍"})
            ComboBox1.Location = New Point(337, 11)
            ComboBox1.Name = "ComboBox1"
            ComboBox1.Size = New Size(90, 32)
            ComboBox1.TabIndex = 6
            ' 
            ' Panel2
            ' 
            Panel2.BackColor = Color.FromArgb(CByte(250), CByte(250), CByte(252))
            Panel2.BorderStyle = BorderStyle.FixedSingle
            Panel2.Controls.Add(Label5)
            Panel2.Controls.Add(Label4)
            Panel2.Controls.Add(Label2)
            Panel2.Controls.Add(Label3)
            Panel2.Location = New Point(54, 103)
            Panel2.Name = "Panel2"
            Panel2.Size = New Size(900, 50)
            Panel2.TabIndex = 4
            ' 
            ' Label5
            ' 
            Label5.Location = New Point(648, 8)
            Label5.Name = "Label5"
            Label5.Padding = New Padding(14, 0, 0, 0)
            Label5.Size = New Size(155, 34)
            Label5.TabIndex = 5
            Label5.Text = "高级功能"
            Label5.TextAlign = ContentAlignment.MiddleLeft
            ' 
            ' Label4
            ' 
            Label4.Location = New Point(324, 8)
            Label4.Name = "Label4"
            Label4.Padding = New Padding(14, 0, 0, 0)
            Label4.Size = New Size(155, 34)
            Label4.TabIndex = 4
            Label4.Text = "实时预览页面"
            Label4.TextAlign = ContentAlignment.MiddleLeft
            ' 
            ' Label2
            ' 
            Label2.ForeColor = Color.FromArgb(CByte(150), CByte(150), CByte(150))
            Label2.Location = New Point(800, 15)
            Label2.Name = "Label2"
            Label2.Size = New Size(100, 20)
            Label2.TabIndex = 3
            Label2.Text = "master"
            Label2.TextAlign = ContentAlignment.MiddleRight
            ' 
            ' Label3
            ' 
            Label3.Location = New Point(11, 8)
            Label3.Name = "Label3"
            Label3.Padding = New Padding(14, 0, 0, 0)
            Label3.Size = New Size(130, 34)
            Label3.TabIndex = 2
            Label3.Text = "超分主页面"
            Label3.TextAlign = ContentAlignment.MiddleLeft
            ' 
            ' lblMaster
            ' 
            lblMaster.Location = New Point(41, 7)
            lblMaster.Name = "lblMaster"
            lblMaster.Padding = New Padding(14, 0, 0, 0)
            lblMaster.Size = New Size(589, 34)
            lblMaster.TabIndex = 2
            lblMaster.Text = "插件总开关  关闭此开关时，超分主页面功能不生效"
            lblMaster.TextAlign = ContentAlignment.MiddleLeft
            ' 
            ' PluginLayoutForm
            ' 
            AutoScaleMode = AutoScaleMode.None
            ClientSize = New Size(1221, 750)
            Controls.Add(Panel2)
            Controls.Add(Panel1)
            Controls.Add(pnlStatus)
            Controls.Add(pnlExe)
            Controls.Add(pnlInterp)
            Controls.Add(pnlBackend)
            Controls.Add(pnlUpscale)
            Controls.Add(pnlMaster)
            FormBorderStyle = FormBorderStyle.FixedSingle
            MaximizeBox = False
            Name = "PluginLayoutForm"
            StartPosition = FormStartPosition.CenterScreen
            Text = "插件页面布局设计器（videoenhancer.3fui）"
            pnlMaster.ResumeLayout(False)
            pnlUpscale.ResumeLayout(False)
            pnlBackend.ResumeLayout(False)
            pnlInterp.ResumeLayout(False)
            pnlExe.ResumeLayout(False)
            pnlStatus.ResumeLayout(False)
            Panel1.ResumeLayout(False)
            Panel2.ResumeLayout(False)
            ResumeLayout(False)
        End Sub

        Friend WithEvents Panel1 As Panel
        Friend WithEvents Label1 As Label
        Friend WithEvents ComboBox1 As ComboBox
        Friend WithEvents Panel2 As Panel
        Friend WithEvents Label2 As Label
        Friend WithEvents Label3 As Label
        Friend WithEvents Label4 As Label
        Friend WithEvents Label5 As Label
        Friend WithEvents lblMaster As Label

    End Class

End Namespace
