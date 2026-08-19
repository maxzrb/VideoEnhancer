Imports System
Imports System.Drawing
Imports System.Windows.Forms

Namespace PluginLayoutDesigner

    Partial Public Class PreviewLayoutForm
        Inherits Form

        ''' <summary>必需的设计器变量。</summary>
        Private components As System.ComponentModel.IContainer

        ' ── 标题行（对应 _lblPreviewTitle）──
        Friend WithEvents pnlTitle As Panel
        Friend WithEvents lblTitle As Label

        ' ── 任务选择行（对应 taskBar / _lblTask / _cmbTask）──
        Friend WithEvents pnlTask As Panel
        Friend WithEvents lblTask As Label
        Friend WithEvents cmbTask As ComboBox

        ' ── 状态行（对应 _lblPreviewStatus）──
        Friend WithEvents lblStatus As Label

        ' ── 中央预览区（对应 _picPreview，原生 PictureBox）──
        Friend WithEvents picPreview As PictureBox

        ' ── 底部栏（对应 bottomBar / _lblPreviewNote / _lblRate / _cmbRate）──
        Friend WithEvents pnlBottom As Panel
        Friend WithEvents lblNote As Label
        Friend WithEvents lblRate As Label
        Friend WithEvents cmbRate As ComboBox

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
            pnlTitle = New Panel()
            lblTitle = New Label()
            pnlTask = New Panel()
            cmbTask = New ComboBox()
            lblTask = New Label()
            lblStatus = New Label()
            picPreview = New PictureBox()
            pnlBottom = New Panel()
            cmbRate = New ComboBox()
            lblRate = New Label()
            lblNote = New Label()
            pnlTitle.SuspendLayout()
            pnlTask.SuspendLayout()
            CType(picPreview, ComponentModel.ISupportInitialize).BeginInit()
            pnlBottom.SuspendLayout()
            SuspendLayout()
            ' 
            ' pnlTitle
            ' 
            pnlTitle.Controls.Add(lblTitle)
            pnlTitle.Location = New Point(0, 0)
            pnlTitle.Name = "pnlTitle"
            pnlTitle.Size = New Size(1203, 36)
            pnlTitle.TabIndex = 0
            ' 
            ' lblTitle
            ' 
            lblTitle.ForeColor = Color.FromArgb(CByte(232), CByte(232), CByte(232))
            lblTitle.Location = New Point(30, 9)
            lblTitle.Name = "lblTitle"
            lblTitle.Size = New Size(1203, 36)
            lblTitle.TabIndex = 0
            lblTitle.Text = "实时预览    预览超分/编码完成的帧"
            lblTitle.TextAlign = ContentAlignment.MiddleLeft
            ' 
            ' pnlTask
            ' 
            pnlTask.Controls.Add(cmbTask)
            pnlTask.Controls.Add(lblTask)
            pnlTask.Location = New Point(30, 57)
            pnlTask.Name = "pnlTask"
            pnlTask.Padding = New Padding(0, 4, 0, 0)
            pnlTask.Size = New Size(1203, 36)
            pnlTask.TabIndex = 1
            ' 
            ' cmbTask
            ' 
            cmbTask.DropDownStyle = ComboBoxStyle.DropDownList
            cmbTask.Items.AddRange(New Object() {"任务 1（最上面）", "任务 2", "任务 3"})
            cmbTask.Location = New Point(96, 4)
            cmbTask.Name = "cmbTask"
            cmbTask.Size = New Size(300, 32)
            cmbTask.TabIndex = 1
            ' 
            ' lblTask
            ' 
            lblTask.ForeColor = Color.FromArgb(CByte(200), CByte(200), CByte(200))
            lblTask.Location = New Point(0, 4)
            lblTask.Name = "lblTask"
            lblTask.Size = New Size(96, 32)
            lblTask.TabIndex = 0
            lblTask.Text = "预览任务"
            lblTask.TextAlign = ContentAlignment.MiddleLeft
            ' 
            ' lblStatus
            ' 
            lblStatus.ForeColor = Color.FromArgb(CByte(154), CByte(167), CByte(154))
            lblStatus.Location = New Point(30, 114)
            lblStatus.Name = "lblStatus"
            lblStatus.Size = New Size(1203, 26)
            lblStatus.TabIndex = 2
            lblStatus.Text = "等待编码队列任务…"
            lblStatus.TextAlign = ContentAlignment.MiddleLeft
            ' 
            ' picPreview
            ' 
            picPreview.BackColor = Color.FromArgb(CByte(16), CByte(16), CByte(18))
            picPreview.Location = New Point(30, 162)
            picPreview.Name = "picPreview"
            picPreview.Size = New Size(1200, 492)
            picPreview.SizeMode = PictureBoxSizeMode.Zoom
            picPreview.TabIndex = 3
            picPreview.TabStop = False
            ' 
            ' pnlBottom
            ' 
            pnlBottom.Controls.Add(cmbRate)
            pnlBottom.Controls.Add(lblRate)
            pnlBottom.Controls.Add(lblNote)
            pnlBottom.Location = New Point(27, 677)
            pnlBottom.Name = "pnlBottom"
            pnlBottom.Padding = New Padding(0, 10, 0, 0)
            pnlBottom.Size = New Size(1203, 46)
            pnlBottom.TabIndex = 4
            ' 
            ' cmbRate
            ' 
            cmbRate.DropDownStyle = ComboBoxStyle.DropDownList
            cmbRate.Items.AddRange(New Object() {"0.5 秒", "1 秒", "2 秒", "3 秒", "关键帧模式"})
            cmbRate.Location = New Point(1041, 6)
            cmbRate.Name = "cmbRate"
            cmbRate.Size = New Size(150, 32)
            cmbRate.TabIndex = 2
            ' 
            ' lblRate
            ' 
            lblRate.ForeColor = Color.FromArgb(CByte(200), CByte(200), CByte(200))
            lblRate.Location = New Point(945, 6)
            lblRate.Name = "lblRate"
            lblRate.Size = New Size(90, 36)
            lblRate.TabIndex = 1
            lblRate.Text = "切换频率"
            lblRate.TextAlign = ContentAlignment.MiddleLeft
            ' 
            ' lblNote
            ' 
            lblNote.ForeColor = Color.FromArgb(CByte(138), CByte(138), CByte(138))
            lblNote.Location = New Point(3, 7)
            lblNote.Name = "lblNote"
            lblNote.Size = New Size(447, 36)
            lblNote.TabIndex = 0
            lblNote.Text = "处理速度较慢时，可能存在预览停顿"
            lblNote.TextAlign = ContentAlignment.MiddleLeft
            ' 
            ' PreviewLayoutForm
            ' 
            AutoScaleMode = AutoScaleMode.None
            BackColor = Color.FromArgb(CByte(28), CByte(28), CByte(34))
            ClientSize = New Size(1294, 772)
            Controls.Add(pnlBottom)
            Controls.Add(picPreview)
            Controls.Add(lblStatus)
            Controls.Add(pnlTask)
            Controls.Add(pnlTitle)
            ForeColor = Color.FromArgb(CByte(220), CByte(220), CByte(220))
            Name = "PreviewLayoutForm"
            Text = "实时预览页布局设计器（Video Enhancer 插件）"
            pnlTitle.ResumeLayout(False)
            pnlTask.ResumeLayout(False)
            CType(picPreview, ComponentModel.ISupportInitialize).EndInit()
            pnlBottom.ResumeLayout(False)
            ResumeLayout(False)

        End Sub

    End Class

End Namespace
