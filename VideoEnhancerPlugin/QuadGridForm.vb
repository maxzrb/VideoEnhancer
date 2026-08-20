Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Globalization
Imports System.IO
Imports System.Reflection
Imports System.Text
Imports System.Text.Json
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports LakeUI

Namespace videoenhancer

    ''' <summary>
    ''' 「制作四宫格比对视频」独立二级窗口（不影响 3FUI 主界面）：
    ''' 拖入/浏览 1-4 个视频，选择输出大小、缩放算法、分割线宽度与颜色，
    ''' 预览区实时渲染四宫格布局与分割线效果；输出时生成 xstack 滤镜 ffmpeg 命令并执行。
    ''' 1 路直接显示；2 路按上下/左右裁切；3 路按可选方向组成 1+2；4 路固定 2×2。
    ''' </summary>
    Friend Class QuadGridForm
        Inherits Form

        Private Enum GridKind
            SingleVideo
            Grid4
            TwoCol
            TwoRow
            TwoRight
            TwoLeft
            TwoTop
            TwoBottom
        End Enum

        ' ── 控件 ──
        Private ReadOnly _videos(3) As String
        Private ReadOnly _slotLabels(3) As VideoSlotCard
        Private ReadOnly _preview As New PictureBox()
        Private ReadOnly _cmbSize As New ComboBox()
        Private ReadOnly _cmbScale As New ComboBox()
        Private ReadOnly _numLine As New NumericUpDown()
        Private ReadOnly _btnColor As New ModernButton()
        Private ReadOnly _cmbLayout As New ComboBox()
        Private ReadOnly _cmbEncoder As New ComboBox()
        Private ReadOnly _numQuality As New NumericUpDown()
        Private ReadOnly _chkBurnFileName As New CheckBox()
        Private ReadOnly _playerBar As New Panel()
        Private ReadOnly _btnPlay As New ModernButton()
        Private ReadOnly _lblTime As New HtmlColorLabel()
        Private ReadOnly _timeline As New SmoothTimeline()
        Private ReadOnly _lblPreviewNote As New HtmlColorLabel()
        Private ReadOnly _nameOverlays(3) As HtmlColorLabel
        Private ReadOnly _dividerPanels As New List(Of Panel)()
        Private ReadOnly _playbackTimer As New Timer() With {.Interval = 33}
        Private ReadOnly _previewDebounceTimer As New Timer() With {.Interval = 90}
        Private ReadOnly _previewClock As New Stopwatch()
        Private _previewPlaying As Boolean
        Private _timelineInternal As Boolean
        Private _pendingSeek As TimeSpan
        Private _hasPendingSeek As Boolean
        Private _timelineDragging As Boolean
        Private _resumeAfterTimelineSeek As Boolean
        Private _previewPosition As TimeSpan
        Private _playBasePosition As TimeSpan
        Private _previewDuration As TimeSpan
        Private ReadOnly _videoDurations(3) As TimeSpan
        Private ReadOnly _previewProcessLock As New Object()
        Private _previewStreamProcess As Process
        Private _stillFrameProcess As Process
        Private _streamGeneration As Integer
        Private _visualGeneration As Integer
        Private _stillRequestVersion As Integer
        Private _stillFrameBusy As Boolean
        Private _pendingStillTarget As TimeSpan
        Private ReadOnly _displayLock As New Object()
        Private _pendingDisplayImage As Image
        Private _pendingDisplayGeneration As Integer
        Private _displayUpdatePosted As Boolean
        Private _compositeFrame As Image
        Private _closing As Boolean
        Private ReadOnly _btnOutput As New ModernButton()
        Private ReadOnly _lblStatus As New HtmlColorLabel()
        Private ReadOnly _frameCache As New Dictionary(Of Integer, Image)()
        Private ReadOnly _framePath As New Dictionary(Of Integer, String)()
        Private ReadOnly _config As PluginConfig

        ' JSON 布局中的设计坐标以 1200×720 为基准。
        Private Const LayoutDesignWidth As Integer = 1200
        Private Const LayoutDesignHeight As Integer = 720
        Private Const RightPaneDesignLeft As Integer = 700
        Private Const RightPaneDesignRight As Integer = 1180
        Private Const RightPaneHorizontalScale As Double = 0.6
        Private ReadOnly _layoutControls As New Dictionary(Of String, Control)(StringComparer.OrdinalIgnoreCase)
        Private _layoutDocument As LayoutDocument
        Private ReadOnly _lblEncoderSection As New HtmlColorLabel()
        Private ReadOnly _lblLayoutSection As New HtmlColorLabel()
        Private ReadOnly _btnEncoderCaption As New ModernButton()
        Private ReadOnly _btnScaleCaption As New ModernButton()
        Private ReadOnly _btnSizeCaption As New ModernButton()
        Private ReadOnly _btnQualityCaption As New ModernButton()
        Private ReadOnly _btnLayoutCaption As New ModernButton()
        Private ReadOnly _btnLineCaption As New ModernButton()
        Private ReadOnly _timelineHost As New Panel()
        Private ReadOnly _encoderHost As New Panel()
        Private ReadOnly _scaleHost As New Panel()
        Private ReadOnly _sizeHost As New Panel()
        Private ReadOnly _layoutHost As New Panel()
        Private ReadOnly _qualityHost As New Panel()
        Private ReadOnly _lineHost As New Panel()
        Private ReadOnly _titleText As New HtmlColorLabel()
        Private ReadOnly _titleIcon As New HtmlColorLabel()
        Private ReadOnly _btnMinimize As New ModernButton()
        Private ReadOnly _btnMaximize As New ModernButton()
        Private ReadOnly _btnClose As New ModernButton()
        Private _windowDragStart As Point
        Private _windowDragBounds As Rectangle
        Private _windowDragging As Boolean
        Private Shared ReadOnly ThumbnailGate As New System.Threading.SemaphoreSlim(1, 1)

        Private NotInheritable Class LayoutDocument
            Public Property CanvasWidth As Integer
            Public Property CanvasHeight As Integer
            Public Property Controls As List(Of LayoutItem)
        End Class

        Private NotInheritable Class LayoutItem
            Public Property Name As String
            Public Property Type As String
            Public Property Text As String
            Public Property CenterX As Integer
            Public Property CenterY As Integer
            Public Property Width As Integer
            Public Property Height As Integer
        End Class

        Private _lineColor As Color = Color.White
        Private _ffmpeg As String = ""
        Private _ffprobe As String = ""
        Private _running As Boolean = False
        Private _process As Process

        Public Sub New(config As PluginConfig)
            _config = config
            Text = "生成对比视频"
            ClientSize = New Size(LayoutDesignWidth, LayoutDesignHeight)
            MinimumSize = New Size(980, 600)
            AutoScaleMode = AutoScaleMode.None
            FormBorderStyle = FormBorderStyle.None
            DoubleBuffered = True
            StartPosition = FormStartPosition.CenterParent
            BackColor = Color.FromArgb(24, 24, 28)
            ForeColor = Color.FromArgb(220, 220, 220)
            Font = New Font("Microsoft YaHei UI", 9.0F)
            ResolveFfmpeg()
            BuildUi()
            AddHandler MouseDown, AddressOf TitleMouseDown
            AddHandler MouseMove, AddressOf TitleMouseMove
            AddHandler MouseUp, AddressOf TitleMouseUp
            AddHandler Resize, AddressOf LayoutFormResize
            AddHandler _playbackTimer.Tick, AddressOf PlaybackTimerTick
            AddHandler _previewDebounceTimer.Tick, AddressOf PreviewDebounceTick
            _playbackTimer.Start()
        End Sub

        Protected Overrides Sub OnShown(e As EventArgs)
            MyBase.OnShown(e)
            For Each f As Form In Application.OpenForms
                If f IsNot Me AndAlso String.Equals(f.Text, "FFmpegFreeUI", StringComparison.OrdinalIgnoreCase) AndAlso f.WindowState = FormWindowState.Normal Then
                    Bounds = f.Bounds
                    Exit For
                End If
            Next
            ApplyJsonLayout()
        End Sub
        ' ────────────────────────── UI 构建 ──────────────────────────

        Private Sub BuildUi()
            SuspendLayout()

            ConfigureTitleBar()

            For i As Integer = 0 To 3
                Dim card As New VideoSlotCard() With {.SlotIndex = i, .Tag = i}
                AddHandler card.DragEnter, AddressOf SlotDragEnter
                AddHandler card.DragDrop, AddressOf SlotDragDrop
                AddHandler card.Click, AddressOf SlotBrowseClick
                _slotLabels(i) = card
                Controls.Add(card)
            Next

            _preview.BackColor = Color.FromArgb(16, 16, 18)
            _preview.AllowDrop = True
            _preview.SizeMode = PictureBoxSizeMode.Normal
            AddHandler _preview.Paint, AddressOf PreviewPaint
            AddHandler _preview.DragEnter, AddressOf SlotDragEnter
            AddHandler _preview.DragDrop, AddressOf PreviewDragDrop
            Controls.Add(_preview)

            ' 1.1 的流畅方案只使用一个合成画面。这里不再创建 4 个原生播放器 HWND，
            ' 避免多路 2160p 解码争抢以及子窗口覆盖 LakeUI 文字时的重影。
            For i As Integer = 0 To 3
                Dim overlay As New HtmlColorLabel() With {
                    .AutoSize = False,
                    .BackColor1 = Color.FromArgb(210, 12, 15, 19),
                    .BorderRadius = 5,
                    .BorderSize = 0,
                    .ForeColor = Color.White,
                    .Font = New Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
                    .Padding = New Padding(8, 2, 8, 2),
                    .TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft,
                    .Visible = False}
                _preview.Controls.Add(overlay)
                _nameOverlays(i) = overlay
            Next

            _btnPlay.Text = "▶"
            _btnPlay.ForeColor = Color.White
            _btnPlay.BackColor1 = Color.FromArgb(40, 45, 53)
            _btnPlay.BackColor2 = Color.FromArgb(40, 45, 53)
            _btnPlay.HoverBackColor1 = Color.FromArgb(50, 57, 66)
            _btnPlay.HoverBackColor2 = Color.FromArgb(50, 57, 66)
            _btnPlay.BorderColor = Color.FromArgb(72, 81, 92)
            _btnPlay.BorderSize = 1
            _btnPlay.BorderRadius = 7
            AddHandler _btnPlay.Click, AddressOf PlayClick
            _lblTime.Text = "00:00:00/00:00:00"
            _lblTime.TextAlign = HtmlColorLabel.TextAlignEnum.Center
            _lblTime.ForeColor = Color.White
            _lblTime.BackColor1 = Color.Transparent
            _lblTime.BorderSize = 0
            _timeline.Minimum = 0
            _timeline.Maximum = 1
            AddHandler _timeline.ValueChanged, AddressOf TimelineValueChanged
            AddHandler _timeline.MouseDown, AddressOf TimelineMouseDown
            AddHandler _timeline.MouseUp, AddressOf TimelineMouseUp
            _lblPreviewNote.Text = "同步预览"
            _lblPreviewNote.TextAlign = HtmlColorLabel.TextAlignEnum.Center
            _lblPreviewNote.ForeColor = Color.Gainsboro
            _lblPreviewNote.BackColor1 = Color.Transparent
            _lblPreviewNote.BorderSize = 0
            Controls.Add(_timeline)
            Controls.Add(_lblTime)
            Controls.Add(_btnPlay)
            Controls.Add(_lblPreviewNote)

            ConfigureSectionLabel(_lblEncoderSection, "编码器选项")
            ConfigureSectionLabel(_lblLayoutSection, "布局与画面")
            ConfigureCaptionButton(_btnEncoderCaption, "编码器")
            ConfigureCaptionButton(_btnScaleCaption, "缩放算法")
            ConfigureCaptionButton(_btnSizeCaption, "分辨率")
            ConfigureCaptionButton(_btnQualityCaption, "质量(CQ)")
            ConfigureCaptionButton(_btnLayoutCaption, "排版方式")
            ConfigureCaptionButton(_btnLineCaption, "线宽")

            _cmbSize.DropDownStyle = ComboBoxStyle.DropDownList
            _cmbSize.Items.AddRange(New Object() {"3840x2160", "2560x1440", "1920x1080", "1280x720", "960x540"})
            _cmbSize.SelectedIndex = 0
            AddHandler _cmbSize.SelectedIndexChanged, AddressOf OptionsChanged
            _cmbScale.DropDownStyle = ComboBoxStyle.DropDownList
            _cmbScale.Items.AddRange(New Object() {"lanczos", "bicubic", "bilinear", "spline"})
            _cmbScale.SelectedIndex = 0
            AddHandler _cmbScale.SelectedIndexChanged, AddressOf OptionsChanged
            _cmbLayout.DropDownStyle = ComboBoxStyle.DropDownList
            AddHandler _cmbLayout.SelectedIndexChanged, AddressOf OptionsChanged
            _cmbEncoder.DropDownStyle = ComboBoxStyle.DropDownList
            _cmbEncoder.Items.AddRange(New Object() {"HEVC · NVIDIA NVENC", "AV1 · NVIDIA NVENC", "HEVC · Intel QSV", "AV1 · Intel QSV", "HEVC · AMD AMF", "AV1 · AMD AMF", "HEVC · CPU (x265)", "AV1 · CPU (SVT-AV1)"})
            _cmbEncoder.SelectedIndex = 0
            AddHandler _cmbEncoder.SelectedIndexChanged, Sub() _btnQualityCaption.Text = If(_cmbEncoder.SelectedIndex >= 6, "质量(CRF)", "质量(CQ)")

            For Each combo As ComboBox In New ComboBox() {_cmbEncoder, _cmbScale, _cmbSize, _cmbLayout}
                combo.FlatStyle = FlatStyle.Flat
                combo.BackColor = Color.FromArgb(36, 41, 49)
                combo.ForeColor = Color.FromArgb(232, 236, 241)
                combo.Font = New Font("Microsoft YaHei UI", 10.0F)
            Next

            _numLine.Minimum = 1
            _numLine.Maximum = 32
            _numLine.Value = 4
            _numLine.AutoSize = False
            _numLine.BackColor = Color.FromArgb(45, 48, 56)
            _numLine.ForeColor = Color.FromArgb(220, 220, 220)
            _numLine.BorderStyle = BorderStyle.FixedSingle
            AddHandler _numLine.ValueChanged, AddressOf OptionsChanged
            _numQuality.Minimum = 0
            _numQuality.Maximum = 51
            _numQuality.Value = 28
            _numQuality.AutoSize = False
            _numQuality.BackColor = Color.FromArgb(36, 41, 49)
            _numQuality.ForeColor = Color.FromArgb(232, 236, 241)
            _numQuality.BorderStyle = BorderStyle.FixedSingle

            ConfigureControlHost(_timelineHost, _timeline, Color.Transparent)
            ConfigureControlHost(_encoderHost, _cmbEncoder, Color.FromArgb(36, 41, 49))
            ConfigureControlHost(_scaleHost, _cmbScale, Color.FromArgb(36, 41, 49))
            ConfigureControlHost(_sizeHost, _cmbSize, Color.FromArgb(36, 41, 49))
            ConfigureControlHost(_layoutHost, _cmbLayout, Color.FromArgb(36, 41, 49))
            ConfigureControlHost(_qualityHost, _numQuality, Color.FromArgb(36, 41, 49))
            ConfigureControlHost(_lineHost, _numLine, Color.FromArgb(36, 41, 49))

            _btnColor.TextAlign = ModernButton.TextAlignEnum.Center
            _btnColor.BackColor1 = Color.White
            _btnColor.BackColor2 = Color.White
            _btnColor.HoverBackColor1 = Color.FromArgb(235, 238, 242)
            _btnColor.HoverBackColor2 = Color.FromArgb(235, 238, 242)
            _btnColor.ForeColor = Color.FromArgb(30, 30, 30)
            _btnColor.BorderRadius = 7
            _btnColor.BorderSize = 0
            AddHandler _btnColor.Click, AddressOf ColorClick
            _chkBurnFileName.Text = "将文件名烧录到画面"
            _chkBurnFileName.AutoSize = False
            _chkBurnFileName.ForeColor = Color.Gainsboro
            _btnOutput.Text = "开始生成"
            _btnOutput.ForeColor = Color.White
            _btnOutput.BackColor1 = Color.FromArgb(26, 166, 100)
            _btnOutput.BackColor2 = Color.FromArgb(26, 166, 100)
            _btnOutput.HoverBackColor1 = Color.FromArgb(33, 187, 116)
            _btnOutput.HoverBackColor2 = Color.FromArgb(33, 187, 116)
            _btnOutput.PressedBackColor1 = Color.FromArgb(21, 142, 85)
            _btnOutput.PressedBackColor2 = Color.FromArgb(21, 142, 85)
            _btnOutput.BorderSize = 0
            _btnOutput.BorderRadius = 7
            _btnOutput.Font = New Font("Microsoft YaHei UI", 13.0F, FontStyle.Bold)
            AddHandler _btnOutput.Click, AddressOf OutputClick

            _lblStatus.AutoSize = False
            _lblStatus.ForeColor = Color.FromArgb(150, 200, 160)
            _lblStatus.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _lblStatus.BackColor1 = Color.Transparent
            _lblStatus.Text = "就绪"
            _lblStatus.Visible = False

            For Each c As Control In New Control() {_lblEncoderSection, _lblLayoutSection, _btnEncoderCaption, _btnScaleCaption,
                                                    _btnSizeCaption, _btnQualityCaption, _btnLayoutCaption, _btnLineCaption,
                                                    _encoderHost, _scaleHost, _sizeHost, _qualityHost, _layoutHost, _lineHost, _timelineHost,
                                                    _btnColor, _chkBurnFileName, _btnOutput, _lblStatus}
                Controls.Add(c)
            Next

            For Each c As Control In New Control() {_titleIcon, _titleText, _btnMinimize, _btnMaximize, _btnClose}
                Controls.Add(c)
                c.BringToFront()
            Next

            RegisterJsonControls()
            _layoutDocument = LoadLayoutDocument()
            UpdateLayoutCombo()
            UpdateColorButton()
            ApplyJsonLayout()
            ResumeLayout(False)
        End Sub

        Private Shared Sub ConfigureSectionLabel(label As HtmlColorLabel, text As String)
            label.Text = text
            label.AutoSize = False
            label.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            label.ForeColor = Color.White
            label.BackColor1 = Color.Transparent
            label.BorderSize = 0
            label.Font = New Font("Microsoft YaHei UI", 11.0F, FontStyle.Bold)
        End Sub

        Private Shared Sub ConfigureCaptionButton(button As ModernButton, text As String)
            button.Text = text
            button.ForeColor = Color.White
            button.TabStop = False
            button.BackColor1 = Color.Transparent
            button.BackColor2 = Color.Transparent
            button.HoverBackColor1 = Color.FromArgb(42, 47, 55)
            button.HoverBackColor2 = Color.FromArgb(42, 47, 55)
            button.PressedBackColor1 = Color.FromArgb(34, 39, 46)
            button.PressedBackColor2 = Color.FromArgb(34, 39, 46)
            button.BorderSize = 0
            button.BorderRadius = 5
            button.TextAlign = ModernButton.TextAlignEnum.Left
        End Sub

        Private Shared Sub ConfigureControlHost(host As Panel, child As Control, backColor As Color)
            host.BackColor = backColor
            host.BorderStyle = BorderStyle.None
            host.Controls.Add(child)
        End Sub

        Private Sub ConfigureTitleBar()
            _titleIcon.Text = "⊞"
            _titleIcon.ForeColor = Color.FromArgb(35, 190, 120)
            _titleIcon.Font = New Font("Segoe UI Symbol", 18.0F, FontStyle.Bold)
            _titleIcon.TextAlign = HtmlColorLabel.TextAlignEnum.Center
            _titleIcon.BackColor1 = Color.Transparent
            _titleIcon.BorderSize = 0
            _titleText.Text = "生成对比视频"
            _titleText.ForeColor = Color.FromArgb(239, 242, 247)
            _titleText.Font = New Font("Microsoft YaHei UI", 13.0F, FontStyle.Bold)
            _titleText.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _titleText.BackColor1 = Color.Transparent
            _titleText.BorderSize = 0
            For Each button As ModernButton In New ModernButton() {_btnMinimize, _btnMaximize, _btnClose}
                button.BackColor1 = Color.Transparent
                button.BackColor2 = Color.Transparent
                button.HoverBackColor1 = Color.FromArgb(50, 55, 63)
                button.HoverBackColor2 = Color.FromArgb(50, 55, 63)
                button.PressedBackColor1 = Color.FromArgb(38, 43, 50)
                button.PressedBackColor2 = Color.FromArgb(38, 43, 50)
                button.BorderSize = 0
                button.BorderRadius = 0
                button.ForeColor = Color.FromArgb(220, 225, 232)
                button.Font = New Font("Segoe UI Symbol", 11.0F)
            Next
            _btnMinimize.Text = "—"
            _btnMaximize.Text = "□"
            _btnClose.Text = "×"
            _btnClose.HoverBackColor1 = Color.FromArgb(196, 43, 54)
            _btnClose.HoverBackColor2 = Color.FromArgb(196, 43, 54)
            AddHandler _btnMinimize.Click, Sub() WindowState = FormWindowState.Minimized
            AddHandler _btnMaximize.Click, AddressOf ToggleMaximize
            AddHandler _btnClose.Click, Sub() Close()
            AddHandler _titleIcon.MouseDown, AddressOf TitleMouseDown
            AddHandler _titleIcon.MouseMove, AddressOf TitleMouseMove
            AddHandler _titleIcon.MouseUp, AddressOf TitleMouseUp
            AddHandler _titleText.MouseDown, AddressOf TitleMouseDown
            AddHandler _titleText.MouseMove, AddressOf TitleMouseMove
            AddHandler _titleText.MouseUp, AddressOf TitleMouseUp
            AddHandler _titleText.DoubleClick, AddressOf ToggleMaximize
        End Sub

        Private Sub RegisterJsonControls()
            _layoutControls.Clear()
            _layoutControls("label1") = _lblLayoutSection
            _layoutControls("button2") = _btnOutput
            _layoutControls("combobox3") = _encoderHost
            _layoutControls("checkbox4") = _chkBurnFileName
            _layoutControls("label5") = _preview
            _layoutControls("label6") = _timelineHost
            _layoutControls("label7") = _lblPreviewNote
            _layoutControls("button8") = _btnPlay
            For i As Integer = 0 To 3
                _layoutControls("label" & (9 + i).ToString(CultureInfo.InvariantCulture)) = _slotLabels(i)
            Next
            _layoutControls("label13") = _lblTime
            _layoutControls("button14") = _btnEncoderCaption
            _layoutControls("button15") = _btnScaleCaption
            _layoutControls("combobox16") = _scaleHost
            _layoutControls("combobox17") = _sizeHost
            _layoutControls("button18") = _btnSizeCaption
            _layoutControls("button19") = _qualityHost
            _layoutControls("button20") = _btnLayoutCaption
            _layoutControls("button21") = _btnQualityCaption
            _layoutControls("label22") = _lblEncoderSection
            _layoutControls("combobox23") = _layoutHost
            _layoutControls("button24") = _btnLineCaption
            _layoutControls("button25") = _lineHost
            _layoutControls("label26") = _btnColor
        End Sub

        Private Shared Function LoadLayoutDocument() As LayoutDocument
            Dim options As New JsonSerializerOptions() With {.PropertyNameCaseInsensitive = True}
            For Each candidate In LayoutCandidates()
                Try
                    If File.Exists(candidate) Then
                        Dim value = JsonSerializer.Deserialize(Of LayoutDocument)(File.ReadAllText(candidate), options)
                        If value IsNot Nothing AndAlso value.Controls IsNot Nothing AndAlso value.Controls.Count > 0 Then Return value
                    End If
                Catch
                End Try
            Next
            Try
                Using stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("videoenhancer-layout.json")
                    If stream IsNot Nothing Then
                        Dim value = JsonSerializer.Deserialize(Of LayoutDocument)(stream, options)
                        If value IsNot Nothing AndAlso value.Controls IsNot Nothing AndAlso value.Controls.Count > 0 Then Return value
                    End If
                End Using
            Catch
            End Try
            Return Nothing
        End Function

        Private Shared Function LayoutCandidates() As IEnumerable(Of String)
            Dim result As New List(Of String)()
            Dim baseDir = AppContext.BaseDirectory
            result.Add(Path.Combine(baseDir, "videoenhancer-layout.json"))
            Dim assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            If Not String.IsNullOrWhiteSpace(assemblyDir) Then result.Add(Path.Combine(assemblyDir, "videoenhancer-layout.json"))
            Dim cursor = New DirectoryInfo(If(String.IsNullOrWhiteSpace(assemblyDir), baseDir, assemblyDir))
            For depth As Integer = 0 To 7
                If cursor Is Nothing Then Exit For
                result.Add(Path.Combine(cursor.FullName, "PluginDesigner", "bin", "Release", "net10.0-windows", "videoenhancer-layout.json"))
                cursor = cursor.Parent
            Next
            Return result
        End Function

        Private Sub LayoutFormResize(sender As Object, e As EventArgs)
            ApplyJsonLayout()
            UpdateWindowRegion()
        End Sub

        Private Sub ApplyJsonLayout()
            If _layoutDocument Is Nothing OrElse _layoutDocument.Controls Is Nothing OrElse ClientSize.Width <= 0 OrElse ClientSize.Height <= 0 Then Return
            Dim scaleX = ClientSize.Width / CDbl(LayoutDesignWidth)
            Dim scaleY = ClientSize.Height / CDbl(LayoutDesignHeight)
            SuspendLayout()
            For Each item In _layoutDocument.Controls
                If item Is Nothing OrElse item.Width <= 0 OrElse item.Height <= 0 OrElse String.IsNullOrWhiteSpace(item.Name) Then Continue For
                Dim control As Control = Nothing
                If Not _layoutControls.TryGetValue(item.Name, control) Then Continue For
                Dim logical = New Rectangle(item.CenterX - item.Width \ 2, item.CenterY - item.Height \ 2, item.Width, item.Height)
                If IsRightPaneItem(item.Name) Then logical = ScaleRightPaneBounds(logical)
                control.Bounds = ScaleBounds(logical, scaleX, scaleY)
                Dim combo = TryCast(control, ComboBox)
                If combo IsNot Nothing Then
                    combo.IntegralHeight = False
                    combo.ItemHeight = Math.Max(12, control.Height - 8)
                End If
            Next
            LayoutHostedControl(_timelineHost, _timeline)
            LayoutHostedControl(_encoderHost, _cmbEncoder)
            LayoutHostedControl(_scaleHost, _cmbScale)
            LayoutHostedControl(_sizeHost, _cmbSize)
            LayoutHostedControl(_layoutHost, _cmbLayout)
            LayoutHostedControl(_qualityHost, _numQuality)
            LayoutHostedControl(_lineHost, _numLine)
            _lblStatus.Bounds = ScaleBounds(New Rectangle(20, 680, 500, 28), scaleX, scaleY)
            _titleIcon.Bounds = ScaleBounds(New Rectangle(17, 7, 32, 35), scaleX, scaleY)
            _titleText.Bounds = ScaleBounds(New Rectangle(50, 7, 240, 35), scaleX, scaleY)
            _btnMinimize.Bounds = New Rectangle(Math.Max(0, ClientSize.Width - 135), 0, 45, Math.Max(36, CInt(Math.Round(45 * scaleY))))
            _btnMaximize.Bounds = New Rectangle(Math.Max(0, ClientSize.Width - 90), 0, 45, Math.Max(36, CInt(Math.Round(45 * scaleY))))
            _btnClose.Bounds = New Rectangle(Math.Max(0, ClientSize.Width - 45), 0, 45, Math.Max(36, CInt(Math.Round(45 * scaleY))))
            ResumeLayout(False)
            UpdatePreviewSurfaces()
            UpdateWindowRegion()
            Invalidate()
        End Sub

        Private Shared Function ScaleBounds(value As Rectangle, scaleX As Double, scaleY As Double) As Rectangle
            Return New Rectangle(CInt(Math.Round(value.X * scaleX)), CInt(Math.Round(value.Y * scaleY)),
                                 Math.Max(1, CInt(Math.Round(value.Width * scaleX))), Math.Max(1, CInt(Math.Round(value.Height * scaleY))))
        End Function

        Private Shared Function ScaleRightPaneBounds(value As Rectangle) As Rectangle
            Dim scaledPaneWidth = CInt(Math.Round((RightPaneDesignRight - RightPaneDesignLeft) * RightPaneHorizontalScale))
            Dim targetLeft = RightPaneDesignRight - scaledPaneWidth
            Return New Rectangle(targetLeft + CInt(Math.Round((value.X - RightPaneDesignLeft) * RightPaneHorizontalScale)),
                                 value.Y,
                                 Math.Max(1, CInt(Math.Round(value.Width * RightPaneHorizontalScale))),
                                 value.Height)
        End Function

        Private Shared Function IsRightPaneItem(name As String) As Boolean
            Select Case If(name, "").ToLowerInvariant()
                Case "label1", "button2", "combobox3", "checkbox4", "button14", "button15", "combobox16",
                     "combobox17", "button18", "button19", "button20", "button21", "label22", "combobox23",
                     "button24", "button25", "label26"
                    Return True
                Case Else
                    Return False
            End Select
        End Function

        Private Shared Sub LayoutHostedControl(host As Panel, child As Control)
            If host.ClientSize.Width <= 0 OrElse host.ClientSize.Height <= 0 Then Return
            If TypeOf child Is SmoothTimeline Then
                child.Bounds = host.ClientRectangle
                Return
            End If
            child.Width = host.ClientSize.Width
            child.Left = 0
            child.Top = (host.ClientSize.Height - child.Height) \ 2
        End Sub

        Private Sub ToggleMaximize(sender As Object, e As EventArgs)
            WindowState = If(WindowState = FormWindowState.Maximized, FormWindowState.Normal, FormWindowState.Maximized)
        End Sub

        Private Sub TitleMouseDown(sender As Object, e As MouseEventArgs)
            If e.Button <> MouseButtons.Left OrElse WindowState = FormWindowState.Maximized Then Return
            If sender Is Me AndAlso e.Y > Math.Max(42, CInt(Math.Round(48 * ClientSize.Height / CDbl(LayoutDesignHeight)))) Then Return
            _windowDragging = True
            _windowDragStart = Cursor.Position
            _windowDragBounds = Bounds
        End Sub

        Private Sub TitleMouseMove(sender As Object, e As MouseEventArgs)
            If Not _windowDragging OrElse e.Button <> MouseButtons.Left Then Return
            Dim p = Cursor.Position
            Location = New Point(_windowDragBounds.X + p.X - _windowDragStart.X, _windowDragBounds.Y + p.Y - _windowDragStart.Y)
        End Sub

        Private Sub TitleMouseUp(sender As Object, e As MouseEventArgs)
            _windowDragging = False
        End Sub

        Private Sub UpdateWindowRegion()
            If WindowState = FormWindowState.Maximized Then
                Region = Nothing
                Return
            End If
            Using path = QuadGridDrawing.RoundedPath(New RectangleF(0, 0, Math.Max(1, Width), Math.Max(1, Height)), 11)
                Dim old = Region
                Region = New Region(path)
                If old IsNot Nothing Then old.Dispose()
            End Using
        End Sub

        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            If ClientRectangle.Width <= 0 OrElse ClientRectangle.Height <= 0 Then
                MyBase.OnPaintBackground(e)
                Return
            End If
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
            Using brush As New LinearGradientBrush(ClientRectangle, Color.FromArgb(18, 22, 27), Color.FromArgb(25, 30, 36), 90.0F)
                e.Graphics.FillRectangle(brush, ClientRectangle)
            End Using
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e)
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
            Dim scaleX = ClientSize.Width / CDbl(LayoutDesignWidth)
            Dim scaleY = ClientSize.Height / CDbl(LayoutDesignHeight)
            For Each logical In New Rectangle() {New Rectangle(18, 160, 865, 498), New Rectangle(892, 58, 288, 570)}
                Dim rect = ScaleBounds(logical, scaleX, scaleY)
                If rect.Width <= 1 OrElse rect.Height <= 1 Then Continue For
                Using path = QuadGridDrawing.RoundedPath(New RectangleF(rect.X + 0.5F, rect.Y + 0.5F, rect.Width - 1, rect.Height - 1), 8)
                    Using brush As New SolidBrush(Color.FromArgb(22, 27, 33))
                        e.Graphics.FillPath(brush, path)
                    End Using
                    Using pen As New Pen(Color.FromArgb(55, 64, 74), 1.0F)
                        e.Graphics.DrawPath(pen, path)
                    End Using
                End Using
            Next
            Using pen As New Pen(Color.FromArgb(46, 54, 63), 1.0F)
                e.Graphics.DrawLine(pen, 18, CInt(Math.Round(665 * scaleY)), ClientSize.Width - 18, CInt(Math.Round(665 * scaleY)))
            End Using
        End Sub

        ' ────────────────────────── 拖放 / 浏览 ──────────────────────────

        Private Shared Function IsVideoFile(path As String) As Boolean
            Dim ext = System.IO.Path.GetExtension(path).ToLowerInvariant()
            Select Case ext
                Case ".mp4", ".mkv", ".mov", ".avi", ".wmv", ".flv", ".ts", ".m2ts", ".webm", ".mpg", ".mpeg", ".m4v", ".3gp", ".vob"
                    Return True
                Case Else
                    Return False
            End Select
        End Function

        Private Sub SlotDragEnter(sender As Object, e As DragEventArgs)
            If e.Data.GetDataPresent(DataFormats.FileDrop) Then
                e.Effect = DragDropEffects.Copy
            Else
                e.Effect = DragDropEffects.None
            End If
        End Sub

        Private Sub SlotDragDrop(sender As Object, e As DragEventArgs)
            Dim files = TryCast(e.Data.GetData(DataFormats.FileDrop), String())
            If files Is Nothing OrElse files.Length = 0 Then
                Return
            End If
            Dim target = -1
            Dim ctl = TryCast(sender, Control)
            If ctl IsNot Nothing AndAlso ctl.Tag IsNot Nothing Then
                Integer.TryParse(ctl.Tag.ToString(), target)
            End If
            If target < 0 OrElse target > 3 Then
                target = FirstEmptySlot()
            End If
            If target < 0 Then
                SetStatusText("四个槽位已满", True)
                Return
            End If
            SetVideo(target, files(0))
        End Sub

        Private Sub PreviewDragDrop(sender As Object, e As DragEventArgs)
            Dim files = TryCast(e.Data.GetData(DataFormats.FileDrop), String())
            If files Is Nothing OrElse files.Length = 0 Then
                Return
            End If
            Dim target = FirstEmptySlot()
            If target < 0 Then
                SetStatusText("四个槽位已满", True)
                Return
            End If
            SetVideo(target, files(0))
        End Sub

        Private Sub SlotBrowseClick(sender As Object, e As EventArgs)
            Dim idx = -1
            Dim ctl = TryCast(sender, Control)
            If ctl IsNot Nothing AndAlso ctl.Tag IsNot Nothing Then
                Integer.TryParse(ctl.Tag.ToString(), idx)
            End If
            If idx < 0 OrElse idx > 3 Then
                Return
            End If
            Using dlg As New OpenFileDialog()
                dlg.Title = "选择视频 " & (idx + 1).ToString()
                dlg.Filter = "视频文件|*.mp4;*.mkv;*.mov;*.avi;*.wmv;*.flv;*.ts;*.m2ts;*.webm;*.mpg;*.mpeg;*.m4v;*.3gp|所有文件|*.*"
                dlg.CheckFileExists = True
                If dlg.ShowDialog(Me) = DialogResult.OK Then
                    SetVideo(idx, dlg.FileName)
                End If
            End Using
        End Sub

        Private Function FirstEmptySlot() As Integer
            For i As Integer = 0 To 3
                If String.IsNullOrWhiteSpace(_videos(i)) Then
                    Return i
                End If
            Next
            Return -1
        End Function

        Private Sub SetVideo(idx As Integer, path As String)
            If String.IsNullOrWhiteSpace(path) OrElse Not File.Exists(path) Then
                SetStatusText("文件不存在：" & If(path, ""), True)
                Return
            End If
            If Not IsVideoFile(path) Then
                SetStatusText("不是支持的视频格式：" & System.IO.Path.GetFileName(path), True)
                Return
            End If
            _videos(idx) = path
            _slotLabels(idx).SetVideo(path)
            _slotLabels(idx).SetPreviewImage(Nothing)
            _videoDurations(idx) = TimeSpan.Zero
            Dim old As Image = Nothing
            If _frameCache.TryGetValue(idx, old) Then
                Try : old.Dispose() : Catch : End Try
                _frameCache.Remove(idx)
            End If
            UpdateLayoutCombo()
            _preview.Invalidate()
            ProbeDurationAsync(idx, path)
            LoadFirstFrameAsync(idx, path)
            SchedulePreviewRefresh()
        End Sub

        Private Sub UpdatePreviewSurfaces()
            If _preview Is Nothing OrElse _preview.ClientSize.Width <= 0 OrElse _preview.ClientSize.Height <= 0 Then Return
            Dim inputs = CollectVideos()
            Dim logicalWidth As Integer = 0
            Dim logicalHeight As Integer = 0
            ParseSize(logicalWidth, logicalHeight)
            If logicalWidth <= 0 OrElse logicalHeight <= 0 Then Return
            Dim rects = LayoutRects(CurrentKind(), logicalWidth, logicalHeight)
            Dim scale = Math.Min(_preview.ClientSize.Width / CDbl(logicalWidth), _preview.ClientSize.Height / CDbl(logicalHeight))
            Dim canvasWidth = Math.Max(1, CInt(Math.Round(logicalWidth * scale)))
            Dim canvasHeight = Math.Max(1, CInt(Math.Round(logicalHeight * scale)))
            Dim originX = (_preview.ClientSize.Width - canvasWidth) \ 2
            Dim originY = (_preview.ClientSize.Height - canvasHeight) \ 2

            RebuildDividerPanels(CurrentKind(), logicalWidth, logicalHeight, scale, originX, originY)
            UpdateNameOverlays(inputs, canvasWidth, canvasHeight, originX, originY, scale)
        End Sub

        Private Sub RebuildDividerPanels(kind As GridKind, logicalWidth As Integer, logicalHeight As Integer, scale As Double, originX As Integer, originY As Integer)
            For Each panel In _dividerPanels
                _preview.Controls.Remove(panel)
                panel.Dispose()
            Next
            _dividerPanels.Clear()
            For Each r In LineRects(kind, logicalWidth, logicalHeight, CInt(_numLine.Value))
                Dim panel As New Panel() With {
                    .BackColor = _lineColor,
                    .Bounds = New Rectangle(originX + CInt(Math.Round(r.X * scale)), originY + CInt(Math.Round(r.Y * scale)),
                                            Math.Max(1, CInt(Math.Round(r.Width * scale))), Math.Max(1, CInt(Math.Round(r.Height * scale))))}
                _preview.Controls.Add(panel)
                _dividerPanels.Add(panel)
            Next
            BringDividersToFront()
        End Sub

        Private Sub BringDividersToFront()
            For Each panel In _dividerPanels
                panel.BringToFront()
            Next
            For Each overlay In _nameOverlays
                If overlay IsNot Nothing AndAlso overlay.Visible Then overlay.BringToFront()
            Next
        End Sub

        Private Enum NameAnchor
            TopLeft
            TopRight
            BottomLeft
            BottomRight
        End Enum

        Private Structure NamePlacement
            Public Anchor As NameAnchor
            Public StackIndex As Integer
        End Structure

        Private Shared Function NamePlacements(count As Integer) As List(Of NamePlacement)
            Dim result As New List(Of NamePlacement)()
            Select Case count
                Case 1
                    result.Add(New NamePlacement With {.Anchor = NameAnchor.TopLeft})
                Case 2
                    result.Add(New NamePlacement With {.Anchor = NameAnchor.TopLeft})
                    result.Add(New NamePlacement With {.Anchor = NameAnchor.BottomRight})
                Case 3
                    ' 三路都使用“各自小块的左上角”，具体坐标由布局矩形决定。
                    For i As Integer = 0 To 2
                        result.Add(New NamePlacement With {.Anchor = NameAnchor.TopLeft})
                    Next
                Case Else
                    result.Add(New NamePlacement With {.Anchor = NameAnchor.TopLeft})
                    result.Add(New NamePlacement With {.Anchor = NameAnchor.TopRight})
                    result.Add(New NamePlacement With {.Anchor = NameAnchor.BottomLeft})
                    result.Add(New NamePlacement With {.Anchor = NameAnchor.BottomRight})
            End Select
            Return result
        End Function

        Private Sub UpdateNameOverlays(inputs As List(Of String), canvasWidth As Integer, canvasHeight As Integer,
                                       originX As Integer, originY As Integer, scale As Double)
            Dim placements = NamePlacements(inputs.Count)
            Dim cellRects = If(inputs.Count = 3, LayoutRects(CurrentKind(), canvasWidth, canvasHeight), Nothing)
            Dim margin = Math.Max(7, CInt(Math.Round(12 * scale)))
            Dim labelHeight = Math.Max(24, CInt(Math.Round(32 * Math.Min(1.0, scale + 0.25))))
            For i As Integer = 0 To 3
                Dim label = _nameOverlays(i)
                If label Is Nothing Then Continue For
                If i >= inputs.Count OrElse i >= placements.Count Then
                    label.Visible = False
                    Continue For
                End If
                label.Text = Path.GetFileName(inputs(i))
                Dim measured = TextRenderer.MeasureText(label.Text, label.Font).Width + 20
                Dim availableWidth = canvasWidth - margin * 2
                If inputs.Count = 3 AndAlso cellRects IsNot Nothing AndAlso i < cellRects.Count Then
                    availableWidth = cellRects(i).Width - margin * 2
                End If
                Dim labelWidth = Math.Min(Math.Max(70, measured), Math.Max(70, availableWidth))
                Dim placement = placements(i)
                Dim x = originX + margin
                Dim y = originY + margin + placement.StackIndex * (labelHeight + 4)
                If inputs.Count = 3 AndAlso cellRects IsNot Nothing AndAlso i < cellRects.Count Then
                    x = originX + cellRects(i).X + margin
                    y = originY + cellRects(i).Y + margin
                End If
                Select Case placement.Anchor
                    Case NameAnchor.TopRight
                        x = originX + canvasWidth - margin - labelWidth
                    Case NameAnchor.BottomLeft
                        y = originY + canvasHeight - margin - labelHeight
                    Case NameAnchor.BottomRight
                        x = originX + canvasWidth - margin - labelWidth
                        y = originY + canvasHeight - margin - labelHeight
                End Select
                label.Bounds = New Rectangle(x, y, labelWidth, labelHeight)
                label.Visible = True
            Next
            BringDividersToFront()
        End Sub

        Private Sub PlaybackTimerTick(sender As Object, e As EventArgs)
            If _previewPlaying Then
                _previewPosition = _playBasePosition + _previewClock.Elapsed
                If _previewDuration > TimeSpan.Zero AndAlso _previewPosition >= _previewDuration Then
                    _previewPosition = _previewDuration
                    _previewPlaying = False
                    _previewClock.Stop()
                    StopStreamingPreview()
                    _btnPlay.Text = "▶"
                    RequestCompositeFrame(_previewPosition)
                End If
            End If
            Dim shownPosition = If(_timelineDragging OrElse _hasPendingSeek, _pendingSeek, _previewPosition)
            UpdateTimelineDisplay(shownPosition)
        End Sub

        Private Sub PlayClick(sender As Object, e As EventArgs)
            If CollectVideos().Count = 0 OrElse _ffmpeg = "" Then Return
            If Not _previewPlaying Then
                _previewPlaying = True
                _playBasePosition = _previewPosition
                _previewClock.Restart()
                _btnPlay.Text = "⏸"
                StartStreamingPreview(_previewPosition)
            Else
                _previewPosition = _playBasePosition + _previewClock.Elapsed
                _previewPlaying = False
                _previewClock.Stop()
                StopStreamingPreview()
                _btnPlay.Text = "▶"
                RequestCompositeFrame(_previewPosition)
            End If
        End Sub

        Private Sub TimelineValueChanged(sender As Object, e As EventArgs)
            If _timelineInternal Then Return
            _pendingSeek = TimeSpan.FromSeconds(Math.Max(0, _timeline.Value))
            _hasPendingSeek = True
            _lblTime.Text = FormatTime(_pendingSeek) & "/" & FormatTime(_previewDuration)
            If Not _timelineDragging AndAlso Not _timeline.IsDragging Then CommitTimelineSeek()
        End Sub

        Private Sub TimelineMouseDown(sender As Object, e As MouseEventArgs)
            If e.Button <> MouseButtons.Left Then Return
            _timelineDragging = True
            _resumeAfterTimelineSeek = _previewPlaying
            If _previewPlaying Then _previewPosition = _playBasePosition + _previewClock.Elapsed
            _previewPlaying = False
            _previewClock.Stop()
            _btnPlay.Text = "▶"
            StopStreamingPreview()
        End Sub

        Private Sub TimelineMouseUp(sender As Object, e As MouseEventArgs)
            If Not _timelineDragging OrElse e.Button <> MouseButtons.Left Then Return
            _timelineDragging = False
            CommitTimelineSeek(Not _resumeAfterTimelineSeek)
            If _resumeAfterTimelineSeek Then
                _previewPlaying = True
                _playBasePosition = _previewPosition
                _previewClock.Restart()
                _btnPlay.Text = "⏸"
                StartStreamingPreview(_previewPosition)
            Else
                _btnPlay.Text = "▶"
            End If
        End Sub

        Private Sub CommitTimelineSeek(Optional requestFrame As Boolean = True)
            Dim target = _pendingSeek
            If _previewDuration > TimeSpan.Zero AndAlso target > _previewDuration Then target = _previewDuration
            _previewPosition = target
            _pendingSeek = target
            _hasPendingSeek = False
            If requestFrame Then RequestCompositeFrame(target)
        End Sub

        Private Sub UpdateTimelineDisplay(position As TimeSpan)
            If _timelineInternal Then Return
            _timelineInternal = True
            _timeline.Maximum = Math.Max(1, CInt(Math.Ceiling(Math.Max(1.0, _previewDuration.TotalSeconds))))
            If Not _timelineDragging Then
                _timeline.Value = Math.Min(_timeline.Maximum, Math.Max(0, CInt(Math.Round(position.TotalSeconds))))
            End If
            _timelineInternal = False
            _lblTime.Text = FormatTime(position) & "/" & FormatTime(_previewDuration)
        End Sub

        Private Sub SchedulePreviewRefresh()
            If _closing Then Return
            _previewDebounceTimer.Stop()
            _previewDebounceTimer.Start()
        End Sub

        Private Sub PreviewDebounceTick(sender As Object, e As EventArgs)
            _previewDebounceTimer.Stop()
            UpdatePreviewSurfaces()
            If _previewPlaying Then
                _previewPosition = _playBasePosition + _previewClock.Elapsed
                _playBasePosition = _previewPosition
                _previewClock.Restart()
                StartStreamingPreview(_previewPosition)
            Else
                RequestCompositeFrame(_previewPosition)
            End If
        End Sub

        Private Sub ProbeDurationAsync(idx As Integer, path As String)
            If String.IsNullOrWhiteSpace(_ffprobe) OrElse Not File.Exists(_ffprobe) Then Return
            Task.Run(New Action(Sub()
                Dim duration = TimeSpan.Zero
                Try
                    Dim psi As New ProcessStartInfo() With {
                        .FileName = _ffprobe,
                        .UseShellExecute = False,
                        .CreateNoWindow = True,
                        .RedirectStandardOutput = True,
                        .RedirectStandardError = True}
                    psi.ArgumentList.Add("-v")
                    psi.ArgumentList.Add("error")
                    psi.ArgumentList.Add("-show_entries")
                    psi.ArgumentList.Add("format=duration")
                    psi.ArgumentList.Add("-of")
                    psi.ArgumentList.Add("default=noprint_wrappers=1:nokey=1")
                    psi.ArgumentList.Add(path)
                    Using p As New Process()
                        p.StartInfo = psi
                        If p.Start() Then
                            Dim errorRead = p.StandardError.ReadToEndAsync()
                            Dim value = p.StandardOutput.ReadToEnd().Trim()
                            p.WaitForExit(10000)
                            Dim seconds As Double
                            If Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, seconds) AndAlso seconds > 0 Then
                                duration = TimeSpan.FromSeconds(seconds)
                            End If
                        End If
                    End Using
                Catch
                End Try
                Try
                    If IsHandleCreated Then BeginInvoke(New Action(Sub()
                        If idx >= 0 AndAlso idx <= 3 AndAlso String.Equals(_videos(idx), path, StringComparison.OrdinalIgnoreCase) Then
                            _videoDurations(idx) = duration
                            UpdatePreviewDuration()
                        End If
                    End Sub))
                Catch
                End Try
            End Sub))
        End Sub

        Private Sub UpdatePreviewDuration()
            Dim duration = TimeSpan.Zero
            For i As Integer = 0 To 3
                If String.IsNullOrWhiteSpace(_videos(i)) OrElse _videoDurations(i) <= TimeSpan.Zero Then Continue For
                If duration = TimeSpan.Zero OrElse _videoDurations(i) < duration Then duration = _videoDurations(i)
            Next
            _previewDuration = duration
            If duration > TimeSpan.Zero AndAlso _previewPosition > duration Then _previewPosition = duration
            UpdateTimelineDisplay(_previewPosition)
        End Sub

        Private Sub GetPreviewOutputSize(ByRef width As Integer, ByRef height As Integer)
            Dim logicalWidth As Integer = 0
            Dim logicalHeight As Integer = 0
            ParseSize(logicalWidth, logicalHeight)
            If logicalWidth <= 0 OrElse logicalHeight <= 0 Then
                logicalWidth = 1920
                logicalHeight = 1080
            End If
            Dim availableWidth = Math.Max(320, Math.Min(1280, _preview.ClientSize.Width))
            Dim availableHeight = Math.Max(180, Math.Min(720, _preview.ClientSize.Height))
            Dim scale = Math.Min(availableWidth / CDbl(logicalWidth), availableHeight / CDbl(logicalHeight))
            width = Math.Max(2, CInt(Math.Floor(logicalWidth * scale / 2.0)) * 2)
            height = Math.Max(2, CInt(Math.Floor(logicalHeight * scale / 2.0)) * 2)
        End Sub

        Private Function CreatePreviewProcessInfo(inputs As List(Of String), target As TimeSpan, continuous As Boolean) As ProcessStartInfo
            Dim previewWidth As Integer = 0
            Dim previewHeight As Integer = 0
            GetPreviewOutputSize(previewWidth, previewHeight)
            Dim logicalWidth As Integer = 0
            Dim logicalHeight As Integer = 0
            ParseSize(logicalWidth, logicalHeight)
            Dim previewLine = 0
            If logicalWidth > 0 AndAlso _numLine.Value > 0 Then
                previewLine = Math.Max(1, CInt(Math.Round(CDbl(_numLine.Value) * previewWidth / logicalWidth)))
            End If
            Dim filter = BuildFilter(inputs, CurrentKind(), previewWidth, previewHeight, "bilinear", previewLine, "")
            Dim psi As New ProcessStartInfo() With {
                .FileName = _ffmpeg,
                .UseShellExecute = False,
                .CreateNoWindow = True,
                .RedirectStandardOutput = True,
                .RedirectStandardError = True}
            psi.ArgumentList.Add("-v")
            psi.ArgumentList.Add("error")
            psi.ArgumentList.Add("-nostdin")
            For Each input In inputs
                psi.ArgumentList.Add("-ss")
                psi.ArgumentList.Add(Math.Max(0, target.TotalSeconds).ToString("0.###", CultureInfo.InvariantCulture))
                If continuous Then psi.ArgumentList.Add("-re")
                psi.ArgumentList.Add("-i")
                psi.ArgumentList.Add(input)
            Next
            psi.ArgumentList.Add("-filter_complex")
            psi.ArgumentList.Add(filter)
            psi.ArgumentList.Add("-map")
            psi.ArgumentList.Add("[final]")
            psi.ArgumentList.Add("-an")
            If continuous Then
                psi.ArgumentList.Add("-r")
                psi.ArgumentList.Add("15")
            Else
                psi.ArgumentList.Add("-frames:v")
                psi.ArgumentList.Add("1")
            End If
            psi.ArgumentList.Add("-f")
            psi.ArgumentList.Add("image2pipe")
            psi.ArgumentList.Add("-c:v")
            psi.ArgumentList.Add("mjpeg")
            psi.ArgumentList.Add("-q:v")
            psi.ArgumentList.Add(If(continuous, "5", "3"))
            psi.ArgumentList.Add("-")
            Return psi
        End Function

        Private Sub RequestCompositeFrame(target As TimeSpan)
            If _closing OrElse _ffmpeg = "" OrElse CollectVideos().Count = 0 Then Return
            _pendingStillTarget = target
            _stillRequestVersion += 1
            System.Threading.Interlocked.Increment(_visualGeneration)
            If _stillFrameBusy Then Return
            StartStillFrameRequest()
        End Sub

        Private Sub StartStillFrameRequest()
            If _closing OrElse _previewPlaying Then Return
            Dim inputs = CollectVideos()
            If inputs.Count = 0 Then Return
            _stillFrameBusy = True
            Dim requestVersion = _stillRequestVersion
            Dim visualVersion = _visualGeneration
            Dim target = _pendingStillTarget
            Dim psi = CreatePreviewProcessInfo(inputs, target, False)
            Task.Run(New Action(Sub()
                Dim image As Image = Nothing
                Try
                    Using p As New Process()
                        p.StartInfo = psi
                        If p.Start() Then
                            SyncLock _previewProcessLock
                                _stillFrameProcess = p
                            End SyncLock
                            Dim errorRead = p.StandardError.ReadToEndAsync()
                            Using data As New MemoryStream()
                                Dim copy = p.StandardOutput.BaseStream.CopyToAsync(data)
                                If Not p.WaitForExit(15000) Then
                                    Try : p.Kill() : Catch : End Try
                                End If
                                Try : copy.Wait(1500) : Catch : End Try
                                If data.Length > 0 Then
                                    data.Position = 0
                                    Using source = Image.FromStream(data)
                                        image = New Bitmap(source)
                                    End Using
                                End If
                            End Using
                        End If
                        SyncLock _previewProcessLock
                            If Object.ReferenceEquals(_stillFrameProcess, p) Then _stillFrameProcess = Nothing
                        End SyncLock
                    End Using
                Catch
                    If image IsNot Nothing Then Try : image.Dispose() : Catch : End Try
                    image = Nothing
                End Try
                If image IsNot Nothing Then
                    If requestVersion = _stillRequestVersion AndAlso visualVersion = _visualGeneration AndAlso Not _previewPlaying Then
                        QueueCompositeImage(image, visualVersion)
                    Else
                        Try : image.Dispose() : Catch : End Try
                    End If
                End If
                Try
                    If IsHandleCreated Then BeginInvoke(New Action(Sub()
                        _stillFrameBusy = False
                        If Not _previewPlaying AndAlso requestVersion <> _stillRequestVersion Then StartStillFrameRequest()
                    End Sub))
                Catch
                    _stillFrameBusy = False
                End Try
            End Sub))
        End Sub

        Private Sub StartStreamingPreview(target As TimeSpan)
            StopStreamingPreview()
            If _closing OrElse _ffmpeg = "" Then Return
            Dim inputs = CollectVideos()
            If inputs.Count = 0 Then Return
            _stillRequestVersion += 1
            Dim stillProcess As Process = Nothing
            SyncLock _previewProcessLock
                stillProcess = _stillFrameProcess
                _stillFrameProcess = Nothing
            End SyncLock
            If stillProcess IsNot Nothing Then
                Try
                    If Not stillProcess.HasExited Then stillProcess.Kill()
                Catch
                End Try
            End If
            Dim visualVersion = System.Threading.Interlocked.Increment(_visualGeneration)
            Dim generation = System.Threading.Interlocked.Increment(_streamGeneration)
            Dim psi = CreatePreviewProcessInfo(inputs, target, True)
            Task.Run(New Action(Sub()
                Dim p As Process = Nothing
                Try
                    p = New Process() With {.StartInfo = psi}
                    If Not p.Start() Then Return
                    Dim errorRead = p.StandardError.ReadToEndAsync()
                    SyncLock _previewProcessLock
                        If generation <> _streamGeneration OrElse _closing Then
                            Try : p.Kill() : Catch : End Try
                            Return
                        End If
                        _previewStreamProcess = p
                    End SyncLock
                    ReadMjpegStream(p.StandardOutput.BaseStream, generation, visualVersion)
                    Try : p.WaitForExit(1500) : Catch : End Try
                Catch
                Finally
                    SyncLock _previewProcessLock
                        If Object.ReferenceEquals(_previewStreamProcess, p) Then _previewStreamProcess = Nothing
                    End SyncLock
                    If p IsNot Nothing Then Try : p.Dispose() : Catch : End Try
                End Try
            End Sub))
        End Sub

        Private Sub StopStreamingPreview()
            System.Threading.Interlocked.Increment(_streamGeneration)
            Dim p As Process = Nothing
            SyncLock _previewProcessLock
                p = _previewStreamProcess
                _previewStreamProcess = Nothing
            End SyncLock
            If p IsNot Nothing Then
                Try
                    If Not p.HasExited Then p.Kill()
                Catch
                End Try
            End If
        End Sub

        Private Sub ReadMjpegStream(stream As Stream, generation As Integer, visualVersion As Integer)
            Dim buffer(32767) As Byte
            Dim frame As MemoryStream = Nothing
            Dim previous As Integer = -1
            Do While generation = _streamGeneration AndAlso Not _closing
                Dim count As Integer
                Try
                    count = stream.Read(buffer, 0, buffer.Length)
                Catch
                    Exit Do
                End Try
                If count <= 0 Then Exit Do
                For i As Integer = 0 To count - 1
                    Dim current = CInt(buffer(i))
                    If frame Is Nothing Then
                        If previous = &HFF AndAlso current = &HD8 Then
                            frame = New MemoryStream()
                            frame.WriteByte(&HFF)
                            frame.WriteByte(&HD8)
                        End If
                    Else
                        frame.WriteByte(CByte(current))
                        If previous = &HFF AndAlso current = &HD9 Then
                            Try
                                frame.Position = 0
                                Using source = Image.FromStream(frame)
                                    Dim image As Image = New Bitmap(source)
                                    If generation = _streamGeneration AndAlso visualVersion = _visualGeneration AndAlso _previewPlaying Then
                                        QueueCompositeImage(image, visualVersion)
                                    Else
                                        image.Dispose()
                                    End If
                                End Using
                            Catch
                            End Try
                            frame.Dispose()
                            frame = Nothing
                            previous = -1
                            Continue For
                        ElseIf frame.Length > 32L * 1024L * 1024L Then
                            frame.Dispose()
                            frame = Nothing
                        End If
                    End If
                    previous = current
                Next
            Loop
            If frame IsNot Nothing Then frame.Dispose()
        End Sub

        Private Sub QueueCompositeImage(image As Image, generation As Integer)
            Dim shouldPost = False
            SyncLock _displayLock
                If _closing OrElse generation <> _visualGeneration Then
                    image.Dispose()
                    Return
                End If
                If _pendingDisplayImage IsNot Nothing Then _pendingDisplayImage.Dispose()
                _pendingDisplayImage = image
                _pendingDisplayGeneration = generation
                If Not _displayUpdatePosted Then
                    _displayUpdatePosted = True
                    shouldPost = True
                End If
            End SyncLock
            If Not shouldPost Then Return
            Try
                BeginInvoke(New Action(AddressOf ApplyPendingCompositeImage))
            Catch
                SyncLock _displayLock
                    _displayUpdatePosted = False
                    If _pendingDisplayImage IsNot Nothing Then _pendingDisplayImage.Dispose()
                    _pendingDisplayImage = Nothing
                End SyncLock
            End Try
        End Sub

        Private Sub ApplyPendingCompositeImage()
            Dim image As Image = Nothing
            Dim generation As Integer
            SyncLock _displayLock
                image = _pendingDisplayImage
                generation = _pendingDisplayGeneration
                _pendingDisplayImage = Nothing
                _displayUpdatePosted = False
            End SyncLock
            If image Is Nothing Then Return
            If generation <> _visualGeneration OrElse _closing Then
                image.Dispose()
                Return
            End If
            Dim old = _compositeFrame
            _compositeFrame = image
            If old IsNot Nothing Then Try : old.Dispose() : Catch : End Try
            _preview.Invalidate()
        End Sub

        Private Shared Function FormatTime(value As TimeSpan) As String
            Return Math.Floor(Math.Max(0, value.TotalHours)).ToString("00", CultureInfo.InvariantCulture) & ":" & value.Minutes.ToString("00", CultureInfo.InvariantCulture) & ":" & value.Seconds.ToString("00", CultureInfo.InvariantCulture)
        End Function

        ' ────────────────────────── 首帧预览 ──────────────────────────

        Private Sub LoadFirstFrameAsync(idx As Integer, path As String)
            Try
                System.Threading.Tasks.Task.Run(New Action(Sub()
                    ThumbnailGate.Wait()
                    Dim img As Image = Nothing
                    Try
                        img = ExtractFirstFrame(path)
                    Finally
                        ThumbnailGate.Release()
                    End Try
                    If img IsNot Nothing Then
                        SyncLock _frameCache
                            Dim old As Image = Nothing
                            If _frameCache.TryGetValue(idx, old) Then
                                Try : old.Dispose() : Catch : End Try
                            End If
                            _frameCache(idx) = img
                            _framePath(idx) = path
                        End SyncLock
                        Try
                            If _preview.IsHandleCreated Then
                                _preview.BeginInvoke(New Action(Sub()
                                    If idx >= 0 AndAlso idx <= 3 AndAlso String.Equals(_videos(idx), path, StringComparison.OrdinalIgnoreCase) Then
                                        _slotLabels(idx).SetPreviewImage(img)
                                    End If
                                    _preview.Invalidate()
                                End Sub))
                            End If
                        Catch
                        End Try
                    End If
                End Sub))
            Catch
            End Try
        End Sub

        Private Function ExtractFirstFrame(path As String) As Image
            If _ffmpeg = "" Then
                Return Nothing
            End If
            Dim positions As Double() = {1.0, 0.0}
            For Each startPos In positions
                Try
                    Dim psi As New ProcessStartInfo()
                    psi.FileName = _ffmpeg
                    psi.UseShellExecute = False
                    psi.CreateNoWindow = True
                    psi.RedirectStandardOutput = True
                    psi.ArgumentList.Add("-v")
                    psi.ArgumentList.Add("error")
                    psi.ArgumentList.Add("-nostdin")
                    psi.ArgumentList.Add("-ss")
                    psi.ArgumentList.Add(startPos.ToString("0.###", CultureInfo.InvariantCulture))
                    psi.ArgumentList.Add("-i")
                    psi.ArgumentList.Add(path)
                    psi.ArgumentList.Add("-frames:v")
                    psi.ArgumentList.Add("1")
                    psi.ArgumentList.Add("-vf")
                    psi.ArgumentList.Add("scale=480:-2:force_original_aspect_ratio=decrease")
                    psi.ArgumentList.Add("-f")
                    psi.ArgumentList.Add("image2pipe")
                    psi.ArgumentList.Add("-c:v")
                    psi.ArgumentList.Add("mjpeg")
                    psi.ArgumentList.Add("-q:v")
                    psi.ArgumentList.Add("3")
                    psi.ArgumentList.Add("-")
                    Using p As New Process()
                        p.StartInfo = psi
                        If Not p.Start() Then
                            Continue For
                        End If
                        Dim ms As New MemoryStream()
                        Try
                            Dim copy = p.StandardOutput.BaseStream.CopyToAsync(ms)
                            Dim exited = p.WaitForExit(10000)
                            If Not exited Then
                                Try : p.Kill() : Catch : End Try
                                Try : copy.Wait(1500) : Catch : End Try
                                Continue For
                            End If
                            Try : copy.Wait(1500) : Catch : End Try
                            If ms.Length <= 0 Then
                                Continue For
                            End If
                            ms.Position = 0
                            Using src = Image.FromStream(ms)
                                Return New Bitmap(src)
                            End Using
                        Finally
                            ms.Dispose()
                        End Try
                    End Using
                Catch
                    Continue For
                End Try
            Next
            Return Nothing
        End Function
        ' ────────────────────────── 预览绘制 ──────────────────────────

        Private Sub OptionsChanged(sender As Object, e As EventArgs)
            UpdatePreviewSurfaces()
            _preview.Invalidate()
            SchedulePreviewRefresh()
        End Sub

        Private Sub PreviewPaint(sender As Object, e As PaintEventArgs)
            Dim g = e.Graphics
            g.Clear(Color.FromArgb(16, 16, 18))
            g.InterpolationMode = InterpolationMode.HighQualityBicubic
            g.PixelOffsetMode = PixelOffsetMode.HighQuality
            Dim inputs = CollectVideos()
            If inputs.Count = 0 Then
                DrawCenteredText(g, "拖入或浏览 1-4 个视频，实时预览四宫格布局", Color.FromArgb(150, 157, 168), 18)
                Return
            End If
            Dim w As Integer = 0
            Dim h As Integer = 0
            ParseSize(w, h)
            If w <= 0 OrElse h <= 0 Then
                Return
            End If
            Dim kind = CurrentKind()
            Dim rects = LayoutRects(kind, w, h)
            Dim pw = _preview.ClientSize.Width
            Dim ph = _preview.ClientSize.Height
            If pw <= 0 OrElse ph <= 0 Then
                Return
            End If
            Dim scale = Math.Min(pw / CDbl(w), ph / CDbl(h))
            Dim ox = (pw - w * scale) / 2.0
            Dim oy = (ph - h * scale) / 2.0

            If _compositeFrame IsNot Nothing Then
                Try
                    Dim destination = New Rectangle(CInt(Math.Round(ox)), CInt(Math.Round(oy)),
                                                    Math.Max(1, CInt(Math.Round(w * scale))), Math.Max(1, CInt(Math.Round(h * scale))))
                    g.DrawImage(_compositeFrame, destination)
                    Using pen = New Pen(Color.FromArgb(70, 70, 80))
                        g.DrawRectangle(pen, destination)
                    End Using
                    Return
                Catch
                End Try
            End If

            For i As Integer = 0 To rects.Count - 1
                Dim r = rects(i)
                Dim sx = ox + r.X * scale
                Dim sy = oy + r.Y * scale
                Dim sw = r.Width * scale
                Dim sh = r.Height * scale
                Using brush = New SolidBrush(If(i Mod 2 = 0, Color.FromArgb(30, 32, 38), Color.FromArgb(24, 26, 32)))
                    g.FillRectangle(brush, CSng(sx), CSng(sy), CSng(sw), CSng(sh))
                End Using
                Dim frame As Image = Nothing
                If i < inputs.Count AndAlso _frameCache.TryGetValue(i, frame) AndAlso frame IsNot Nothing Then
                    If String.Equals(_framePath(i), inputs(i), StringComparison.OrdinalIgnoreCase) Then
                        Try
                            ' 与输出滤镜完全相同：先把源映射为完整输出画布，再取本格对应区域。
                            ' 例如四路时 1/2/3/4 分别取左上、右上、左下、右下，不缩放挤压整帧。
                            Dim srcX = CSng(r.X / CDbl(w) * frame.Width)
                            Dim srcY = CSng(r.Y / CDbl(h) * frame.Height)
                            Dim srcW = CSng(r.Width / CDbl(w) * frame.Width)
                            Dim srcH = CSng(r.Height / CDbl(h) * frame.Height)
                            Dim destination = New Rectangle(CInt(Math.Round(sx)), CInt(Math.Round(sy)), Math.Max(1, CInt(Math.Round(sw))), Math.Max(1, CInt(Math.Round(sh))))
                            g.DrawImage(frame, destination, srcX, srcY, srcW, srcH, GraphicsUnit.Pixel)
                        Catch
                        End Try
                    End If
                End If
            Next

            ' 分割线：实时渲染，宽度随预览缩放等比缩放
            Dim dividerRects = LineRects(kind, w, h, CInt(_numLine.Value))
            Using brush = New SolidBrush(_lineColor)
                For Each r In dividerRects
                    g.FillRectangle(brush, CSng(ox + r.X * scale), CSng(oy + r.Y * scale), CSng(r.Width * scale), CSng(r.Height * scale))
                Next
            End Using
            Using pen = New Pen(Color.FromArgb(70, 70, 80))
                g.DrawRectangle(pen, CSng(ox), CSng(oy), CSng(w * scale), CSng(h * scale))
            End Using
        End Sub

        Private Shared Sub DrawCenteredText(g As Graphics, text As String, color As Color, size As Integer, Optional x As Single = 0, Optional y As Single = 0, Optional w As Single = 0, Optional h As Single = 0)
            If String.IsNullOrWhiteSpace(text) Then
                Return
            End If
            Using font = New Font("Microsoft YaHei UI", size, FontStyle.Regular, GraphicsUnit.Pixel)
                Using brush = New SolidBrush(color)
                    Dim format As New StringFormat()
                    format.Alignment = StringAlignment.Center
                    format.LineAlignment = StringAlignment.Center
                    format.Trimming = StringTrimming.EllipsisCharacter
                    If w > 0 Then
                        Dim rect As New RectangleF(x, y, w, h)
                        g.DrawString(text, font, brush, rect, format)
                    Else
                        Dim sizeF = g.MeasureString(text, font)
                        Dim cx = (g.ClipBounds.Width - sizeF.Width) / 2.0F
                        Dim cy = (g.ClipBounds.Height - sizeF.Height) / 2.0F
                        g.DrawString(text, font, brush, cx, cy, format)
                    End If
                    format.Dispose()
                End Using
            End Using
        End Sub

        ' ────────────────────────── 布局计算 ──────────────────────────

        Private Function CollectVideos() As List(Of String)
            Dim result As New List(Of String)()
            For i As Integer = 0 To 3
                If Not String.IsNullOrWhiteSpace(_videos(i)) Then
                    result.Add(_videos(i))
                End If
            Next
            Return result
        End Function

        Private Sub ParseSize(ByRef w As Integer, ByRef h As Integer)
            Dim text = If(_cmbSize.SelectedItem, "").ToString()
            Dim idx = text.IndexOf("x"c)
            If idx <= 0 Then
                w = 3840
                h = 2160
                Return
            End If
            If Not Integer.TryParse(text.Substring(0, idx), w) Then
                w = 3840
            End If
            If Not Integer.TryParse(text.Substring(idx + 1), h) Then
                h = 2160
            End If
            If w Mod 2 <> 0 Then
                w -= 1
            End If
            If h Mod 2 <> 0 Then
                h -= 1
            End If
            w = Math.Max(320, w)
            h = Math.Max(180, h)
        End Sub

        Private Sub UpdateLayoutCombo()
            Dim inputs = CollectVideos()
            _cmbLayout.Items.Clear()
            Select Case inputs.Count
                Case 0
                    _cmbLayout.Enabled = False
                    _cmbLayout.Items.Add("等待导入视频")
                    _cmbLayout.SelectedIndex = 0
                Case 1
                    _cmbLayout.Enabled = False
                    _cmbLayout.Items.Add("单路直接输出")
                    _cmbLayout.SelectedIndex = 0
                Case 2
                    _cmbLayout.Enabled = True
                    _cmbLayout.Items.Add("左右排版")
                    _cmbLayout.Items.Add("上下排版")
                    _cmbLayout.SelectedIndex = 0
                Case 3
                    _cmbLayout.Enabled = True
                    _cmbLayout.Items.Add("2 个在右侧")
                    _cmbLayout.Items.Add("2 个在左侧")
                    _cmbLayout.Items.Add("2 个在上方")
                    _cmbLayout.Items.Add("2 个在下方")
                    _cmbLayout.SelectedIndex = 0
                Case Else
                    _cmbLayout.Enabled = False
                    _cmbLayout.Items.Add("2×2 四宫格")
                    _cmbLayout.SelectedIndex = 0
            End Select
        End Sub

        Private Function CurrentKind() As GridKind
            Dim inputs = CollectVideos()
            Select Case inputs.Count
                Case 0, 1
                    Return GridKind.SingleVideo
                Case 2
                    If _cmbLayout.SelectedIndex = 1 Then
                        Return GridKind.TwoRow
                    End If
                    Return GridKind.TwoCol
                Case 3
                    Select Case _cmbLayout.SelectedIndex
                        Case 1 : Return GridKind.TwoLeft
                        Case 2 : Return GridKind.TwoTop
                        Case 3 : Return GridKind.TwoBottom
                        Case Else : Return GridKind.TwoRight
                    End Select
                Case Else
                    Return GridKind.Grid4
            End Select
        End Function

        Private Shared Function LayoutRects(kind As GridKind, w As Integer, h As Integer) As List(Of Rectangle)
            Dim hw = w \ 2
            Dim hh = h \ 2
            Dim result As New List(Of Rectangle)()
            Select Case kind
                Case GridKind.SingleVideo
                    result.Add(New Rectangle(0, 0, w, h))
                Case GridKind.TwoCol
                    result.Add(New Rectangle(0, 0, hw, h))
                    result.Add(New Rectangle(hw, 0, hw, h))
                Case GridKind.TwoRow
                    result.Add(New Rectangle(0, 0, w, hh))
                    result.Add(New Rectangle(0, hh, w, hh))
                Case GridKind.TwoRight
                    result.Add(New Rectangle(0, 0, hw, h))
                    result.Add(New Rectangle(hw, 0, hw, hh))
                    result.Add(New Rectangle(hw, hh, hw, hh))
                Case GridKind.TwoLeft
                    result.Add(New Rectangle(0, 0, hw, hh))
                    result.Add(New Rectangle(0, hh, hw, hh))
                    result.Add(New Rectangle(hw, 0, hw, h))
                Case GridKind.TwoTop
                    result.Add(New Rectangle(0, 0, hw, hh))
                    result.Add(New Rectangle(hw, 0, hw, hh))
                    result.Add(New Rectangle(0, hh, w, hh))
                Case GridKind.TwoBottom
                    result.Add(New Rectangle(0, 0, w, hh))
                    result.Add(New Rectangle(0, hh, hw, hh))
                    result.Add(New Rectangle(hw, hh, hw, hh))
                Case Else
                    result.Add(New Rectangle(0, 0, hw, hh))
                    result.Add(New Rectangle(hw, 0, hw, hh))
                    result.Add(New Rectangle(0, hh, hw, hh))
                    result.Add(New Rectangle(hw, hh, hw, hh))
            End Select
            Return result
        End Function

        Private Shared Function XstackLayout(kind As GridKind) As String
            Select Case kind
                Case GridKind.SingleVideo : Return "0_0"
                Case GridKind.TwoCol : Return "0_0|w0_0"
                Case GridKind.TwoRow : Return "0_0|0_h0"
                Case GridKind.TwoRight : Return "0_0|w0_0|w0_h1"
                Case GridKind.TwoLeft : Return "0_0|0_h0|w0_0"
                Case GridKind.TwoTop : Return "0_0|w0_0|0_h0"
                Case GridKind.TwoBottom : Return "0_0|0_h0|w1_h0"
                Case Else : Return "0_0|w0_0|0_h0|w0_h0"
            End Select
        End Function

        Private Shared Function LineRects(kind As GridKind, w As Integer, h As Integer, lw As Integer) As List(Of Rectangle)
            Dim hw = w \ 2
            Dim hh = h \ 2
            Dim half = lw \ 2
            Dim result As New List(Of Rectangle)()
            Select Case kind
                Case GridKind.SingleVideo
                    Return result
                Case GridKind.TwoCol
                    result.Add(New Rectangle(hw - half, 0, lw, h))
                Case GridKind.TwoRow
                    result.Add(New Rectangle(0, hh - half, w, lw))
                Case GridKind.TwoRight
                    result.Add(New Rectangle(hw - half, 0, lw, h))
                    result.Add(New Rectangle(hw, hh - half, hw, lw))
                Case GridKind.TwoLeft
                    result.Add(New Rectangle(hw - half, 0, lw, h))
                    result.Add(New Rectangle(0, hh - half, hw, lw))
                Case GridKind.TwoTop
                    result.Add(New Rectangle(0, hh - half, w, lw))
                    result.Add(New Rectangle(hw - half, 0, lw, hh))
                Case GridKind.TwoBottom
                    result.Add(New Rectangle(0, hh - half, w, lw))
                    result.Add(New Rectangle(hw - half, hh, lw, hh))
                Case Else
                    result.Add(New Rectangle(hw - half, 0, lw, h))
                    result.Add(New Rectangle(0, hh - half, w, lw))
            End Select
            Return result
        End Function
        ' ────────────────────────── 颜色 ──────────────────────────

        Private Sub ColorClick(sender As Object, e As EventArgs)
            Try
                Dim dlg As New ModernColorDialog()
                dlg.SelectedColor = _lineColor
                If dlg.ShowDialog(Me) = DialogResult.OK Then
                    _lineColor = dlg.SelectedColor
                    UpdateColorButton()
                    _preview.Invalidate()
                End If
            Catch
                Using dlg As New ColorDialog()
                    dlg.Color = _lineColor
                    If dlg.ShowDialog(Me) = DialogResult.OK Then
                        _lineColor = dlg.Color
                        UpdateColorButton()
                        _preview.Invalidate()
                    End If
                End Using
            End Try
        End Sub

        Private Sub UpdateColorButton()
            _btnColor.BackColor1 = _lineColor
            _btnColor.BackColor2 = _lineColor
            _btnColor.Text = "#" & _lineColor.R.ToString("X2") & _lineColor.G.ToString("X2") & _lineColor.B.ToString("X2")
            For Each panel In _dividerPanels
                panel.BackColor = _lineColor
            Next
        End Sub

        ' ────────────────────────── 输出 ──────────────────────────

        Private Sub OutputClick(sender As Object, e As EventArgs)
            If _running Then
                Return
            End If
            Dim inputs = CollectVideos()
            If inputs.Count < 1 Then
                MessageBox.Show(Me, "请先导入至少 1 个视频。", "对比视频", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If
            Using dlg As New SaveFileDialog()
                dlg.Title = "输出四宫格比对视频"
                dlg.Filter = "MKV 视频 (*.mkv)|*.mkv|MP4 视频 (*.mp4)|*.mp4|所有文件 (*.*)|*.*"
                dlg.FileName = "四宫格比对_" & DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) & ".mkv"
                If dlg.ShowDialog(Me) <> DialogResult.OK Then
                    Return
                End If
                StartEncode(inputs, dlg.FileName)
            End Using
        End Sub

        Private Sub StartEncode(inputs As List(Of String), outputPath As String)
            If _ffmpeg = "" Then
                SetStatusText("未找到 ffmpeg（请先启用 videoenhancer.exe 并配置 core-path）", True)
                Return
            End If
            Dim w As Integer = 0
            Dim h As Integer = 0
            ParseSize(w, h)
            Dim kind = CurrentKind()
            Dim lw = CInt(_numLine.Value)
            Dim algo = If(_cmbScale.SelectedItem, "lanczos").ToString()

            Dim assPath = ""
            If _chkBurnFileName.Checked Then
                Try
                    assPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(outputPath), System.IO.Path.GetFileNameWithoutExtension(outputPath) & "_labels.ass")
                    System.IO.File.WriteAllText(assPath, BuildAss(inputs, kind, w, h), New UTF8Encoding(False))
                Catch
                    assPath = ""
                End Try
            End If
            Dim filter = BuildFilter(inputs, kind, w, h, algo, lw, assPath)

            Dim psi As New ProcessStartInfo()
            psi.FileName = _ffmpeg
            psi.UseShellExecute = False
            psi.CreateNoWindow = True
            psi.RedirectStandardOutput = True
            psi.RedirectStandardError = True
            psi.StandardErrorEncoding = Encoding.UTF8
            For Each input In inputs
                psi.ArgumentList.Add("-i")
                psi.ArgumentList.Add(input)
            Next
            psi.ArgumentList.Add("-filter_complex")
            psi.ArgumentList.Add(filter)
            psi.ArgumentList.Add("-map")
            psi.ArgumentList.Add("[final]")
            psi.ArgumentList.Add("-map")
            psi.ArgumentList.Add("0:a?")
            AddEncoderArguments(psi)
            psi.ArgumentList.Add("-pix_fmt")
            psi.ArgumentList.Add("yuv420p10le")
            psi.ArgumentList.Add("-c:a")
            psi.ArgumentList.Add("copy")
            If String.Equals(System.IO.Path.GetExtension(outputPath), ".mkv", StringComparison.OrdinalIgnoreCase) Then
                psi.ArgumentList.Add("-f") : psi.ArgumentList.Add("matroska")
            End If
            psi.ArgumentList.Add(outputPath)

            _running = True
            _btnOutput.Text = "编码中…"
            _btnOutput.Enabled = False
            SetStatusText("正在编码：xstack 四宫格 → " & System.IO.Path.GetFileName(outputPath), False)
            Try
                _process = New Process()
                _process.StartInfo = psi
                If Not _process.Start() Then
                    _running = False
                    _btnOutput.Enabled = True
                    _btnOutput.Text = "输出"
                    SetStatusText("启动 ffmpeg 失败", True)
                    Return
                End If
                MonitorEncode(_process, outputPath)
            Catch ex As Exception
                _running = False
                _btnOutput.Enabled = True
                _btnOutput.Text = "输出"
                SetStatusText("启动失败：" & ex.Message, True)
            End Try
        End Sub

        Private Sub AddEncoderArguments(psi As ProcessStartInfo)
            Dim idx = _cmbEncoder.SelectedIndex
            Dim encoder = If(idx = 1, "av1_nvenc", If(idx = 2, "hevc_qsv", If(idx = 3, "av1_qsv", If(idx = 4, "hevc_amf", If(idx = 5, "av1_amf", If(idx = 6, "libx265", If(idx = 7, "libsvtav1", "hevc_nvenc")))))))
            Dim quality = CInt(_numQuality.Value).ToString(CultureInfo.InvariantCulture)
            psi.ArgumentList.Add("-c:v") : psi.ArgumentList.Add(encoder)
            If encoder.StartsWith("lib", StringComparison.OrdinalIgnoreCase) Then
                psi.ArgumentList.Add("-crf") : psi.ArgumentList.Add(quality)
            ElseIf encoder.EndsWith("_qsv", StringComparison.OrdinalIgnoreCase) Then
                psi.ArgumentList.Add("-global_quality") : psi.ArgumentList.Add(quality)
            ElseIf encoder.EndsWith("_amf", StringComparison.OrdinalIgnoreCase) Then
                psi.ArgumentList.Add("-rc") : psi.ArgumentList.Add("qvbr")
                psi.ArgumentList.Add("-qvbr_quality_level") : psi.ArgumentList.Add(quality)
            Else
                psi.ArgumentList.Add("-cq") : psi.ArgumentList.Add(quality)
                psi.ArgumentList.Add("-b:v") : psi.ArgumentList.Add("0")
            End If
        End Sub

        Private Sub MonitorEncode(p As Process, outputPath As String)
            Dim captured = p
            Dim capturedOutput = outputPath
            System.Threading.Tasks.Task.Run(New Action(Sub()
                Dim lastLine As String = ""
                Try
                    Dim line As String = Nothing
                    Do
                        line = captured.StandardError.ReadLine()
                        If line Is Nothing Then
                            Exit Do
                        End If
                        If line.Contains("time=") OrElse line.Contains("frame=") OrElse line.Contains("Error") OrElse line.Contains("error") Then
                            lastLine = line
                            UpdateStatusSafe(line)
                        End If
                    Loop
                Catch
                End Try
                Try
                    captured.WaitForExit()
                Catch
                End Try
                Dim code As Integer = -1
                Try
                    code = captured.ExitCode
                Catch
                End Try
                If code = 0 Then
                    UpdateStatusSafe("完成：已输出 " & capturedOutput)
                Else
                    UpdateStatusSafe("失败：ffmpeg 退出码 " & code.ToString() & If(String.IsNullOrWhiteSpace(lastLine), "", "（" & lastLine & "）"), True)
                End If
                Try
                    If _preview.IsHandleCreated Then
                        _preview.BeginInvoke(New Action(Sub()
                            _running = False
                            _process = Nothing
                            _btnOutput.Enabled = True
                            _btnOutput.Text = "输出"
                        End Sub))
                    Else
                        _running = False
                    End If
                Catch
                    _running = False
                End Try
            End Sub))
        End Sub

        Private Sub UpdateStatusSafe(text As String, Optional error_ As Boolean = False)
            Try
                If IsHandleCreated Then
                    BeginInvoke(New Action(Sub() SetStatusText(text, error_)))
                Else
                    SetStatusText(text, error_)
                End If
            Catch
            End Try
        End Sub

        Private Sub SetStatusText(text As String, error_ As Boolean)
            _lblStatus.Text = text
            _lblStatus.ForeColor = If(error_, Color.FromArgb(224, 120, 120), Color.FromArgb(150, 200, 160))
            _lblStatus.Visible = Not String.Equals(text, "就绪", StringComparison.Ordinal)
            If _lblStatus.Visible Then _lblStatus.BringToFront()
        End Sub
        ' ────────────────────────── ffmpeg 滤镜构建 ──────────────────────────

        Private Function BuildFilter(inputs As List(Of String), kind As GridKind, w As Integer, h As Integer, algo As String, lw As Integer, assPath As String) As String
            Dim sb As New StringBuilder()
            Dim rects = LayoutRects(kind, w, h)
            For i As Integer = 0 To rects.Count - 1
                Dim r = rects(i)
                If i > 0 Then
                    sb.Append(" ")
                End If
                sb.Append("[").Append(i.ToString()).Append(":v] ")
                ' 先按比例铺满完整画布，再从中心裁成画布大小，最后取该视频负责的格子。
                ' 这样四路分别取得左上/右上/左下/右下，不会把整帧挤压进小格。
                sb.Append("scale=").Append(w.ToString()).Append(":").Append(h.ToString())
                sb.Append(":force_original_aspect_ratio=increase:flags=").Append(algo)
                sb.Append(", crop=").Append(w.ToString()).Append(":").Append(h.ToString())
                sb.Append(":(iw-").Append(w.ToString()).Append(")/2:(ih-").Append(h.ToString()).Append(")/2")
                sb.Append(", setsar=1, crop=").Append(r.Width.ToString()).Append(":").Append(r.Height.ToString())
                sb.Append(":").Append(r.X.ToString()).Append(":").Append(r.Y.ToString())
                sb.Append(", setpts=PTS-STARTPTS [v").Append(i.ToString()).Append("]; ")
            Next
            If rects.Count = 1 Then
                sb.Append("[v0] null [out]; ")
            Else
                sb.Append("[v0]")
                For i As Integer = 1 To rects.Count - 1
                    sb.Append("[v").Append(i.ToString()).Append("]")
                Next
                sb.Append(" xstack=inputs=").Append(rects.Count.ToString()).Append(":layout=").Append(XstackLayout(kind)).Append(" [out]; ")
            End If

            Dim dividerRects = LineRects(kind, w, h, lw)
            Dim colorHex = "0x" & _lineColor.R.ToString("X2") & _lineColor.G.ToString("X2") & _lineColor.B.ToString("X2")
            Dim first As Boolean = True
            For Each r In dividerRects
                If Not first Then
                    sb.Append(", ")
                Else
                    sb.Append("[out] ")
                    first = False
                End If
                sb.Append("drawbox=x=").Append(r.X.ToString()).Append(":y=").Append(r.Y.ToString())
                sb.Append(":w=").Append(r.Width.ToString()).Append(":h=").Append(r.Height.ToString())
                sb.Append(":color=").Append(colorHex).Append(":t=fill")
            Next
            If first Then
                sb.Append("[out] null [lined]; ")
            Else
                sb.Append(" [lined]; ")
            End If

            If Not String.IsNullOrWhiteSpace(assPath) AndAlso File.Exists(assPath) Then
                sb.Append("[lined] subtitles=filename=").Append(EscapeFilterPath(assPath)).Append(" [final]")
            Else
                sb.Append("[lined] null [final]")
            End If
            Return sb.ToString()
        End Function

        Private Shared Function EscapeFilterPath(path As String) As String
            Dim sb As New StringBuilder()
            sb.Append("'")
            For Each c As Char In path
                Select Case c
                    Case "\"c
                        sb.Append("\\")
                    Case ":"c
                        sb.Append("\:")
                    Case "'"c
                        sb.Append("\'")
                    Case Else
                        sb.Append(c)
                End Select
            Next
            sb.Append("'")
            Return sb.ToString()
        End Function

        Private Shared Function BuildAss(inputs As List(Of String), kind As GridKind, w As Integer, h As Integer) As String
            Dim sb As New StringBuilder()
            sb.AppendLine("[Script Info]")
            sb.AppendLine("ScriptType: v4.00+")
            sb.AppendLine("PlayResX: " & w.ToString())
            sb.AppendLine("PlayResY: " & h.ToString())
            sb.AppendLine("ScaledBorderAndShadow: yes")
            sb.AppendLine()
            sb.AppendLine("[V4+ Styles]")
            sb.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding")
            Dim fontSize = Math.Max(22, w \ 60)
            sb.AppendLine("Style: Default,Microsoft YaHei," & fontSize.ToString() & ",&H00FFFFFF,&H000000FF,&H00101010,&H80000000,-1,0,0,0,100,100,0,0,1,2,1,5,20,20,20,1")
            sb.AppendLine()
            sb.AppendLine("[Events]")
            sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text")
            Dim placements = NamePlacements(inputs.Count)
            Dim cellRects = If(inputs.Count = 3, LayoutRects(kind, w, h), Nothing)
            Dim margin = Math.Max(20, fontSize)
            Dim lineStep = fontSize + Math.Max(8, fontSize \ 3)
            For i As Integer = 0 To inputs.Count - 1
                Dim placement = placements(i)
                Dim x = margin
                Dim y = margin + placement.StackIndex * lineStep
                Dim alignment = 7
                If inputs.Count = 3 AndAlso cellRects IsNot Nothing AndAlso i < cellRects.Count Then
                    x = cellRects(i).X + margin
                    y = cellRects(i).Y + margin
                End If
                Select Case placement.Anchor
                    Case NameAnchor.TopRight
                        x = w - margin
                        alignment = 9
                    Case NameAnchor.BottomLeft
                        y = h - margin
                        alignment = 1
                    Case NameAnchor.BottomRight
                        x = w - margin
                        y = h - margin
                        alignment = 3
                End Select
                Dim name = System.IO.Path.GetFileNameWithoutExtension(inputs(i))
                name = SanitizeAssText(name)
                sb.AppendLine("Dialogue: 0,0:00:00:00,99:00:00:00,Default,,0,0,0,,{\an" & alignment.ToString(CultureInfo.InvariantCulture) &
                              "\pos(" & x.ToString(CultureInfo.InvariantCulture) & "," & y.ToString(CultureInfo.InvariantCulture) & ")}" & name)
            Next
            Return sb.ToString()
        End Function

        Private Shared Function SanitizeAssText(text As String) As String
            If String.IsNullOrEmpty(text) Then
                Return text
            End If
            Dim sb As New StringBuilder()
            For Each c As Char In text
                Select Case c
                    Case "{"c, "}"c, "\"c
                        sb.Append(" ")
                    Case ","c
                        sb.Append("，")
                    Case Else
                        sb.Append(c)
                End Select
            Next
            Return sb.ToString()
        End Function

        ' ────────────────────────── ffmpeg 定位 / 关闭 ──────────────────────────

        Private Sub ResolveFfmpeg()
            Try
                Dim exePath = If(_config Is Nothing, "", _config.ExePath)
                Dim exeDir = ""
                If Not String.IsNullOrWhiteSpace(exePath) Then
                    exeDir = System.IO.Path.GetDirectoryName(exePath)
                End If
                If exeDir = "" Then
                    exeDir = Environment.CurrentDirectory
                End If
                Dim core As String = ""
                Try
                    Dim ini = System.IO.Path.Combine(exeDir, "videoenhancer.ini")
                    If File.Exists(ini) Then
                        For Each line In File.ReadAllLines(ini)
                            If line.TrimStart().StartsWith("core-path=", StringComparison.OrdinalIgnoreCase) Then
                                Dim idx = line.IndexOf("="c)
                                If idx >= 0 Then
                                    Dim v = line.Substring(idx + 1).Trim()
                                    v = v.Trim(Convert.ToChar(34))
                                    If v.Length > 0 Then
                                        core = v
                                    End If
                                End If
                            End If
                        Next
                    End If
                Catch
                End Try
                If core = "" Then
                    core = exeDir
                End If
                Dim ff1 = System.IO.Path.Combine(core, "bin", "ffmpeg", "ffmpeg.exe")
                Dim ff2 = System.IO.Path.Combine(core, "bin", "ffmpeg.exe")
                If File.Exists(ff1) Then
                    _ffmpeg = ff1
                ElseIf File.Exists(ff2) Then
                    _ffmpeg = ff2
                ElseIf File.Exists(System.IO.Path.Combine(core, "ffmpeg.exe")) Then
                    _ffmpeg = System.IO.Path.Combine(core, "ffmpeg.exe")
                End If
                If _ffmpeg <> "" Then
                    Dim probe = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_ffmpeg), "ffprobe.exe")
                    If File.Exists(probe) Then _ffprobe = probe
                End If
            Catch
            End Try
        End Sub

        Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
            If _running AndAlso _process IsNot Nothing Then
                Dim answer = MessageBox.Show(Me, "编码正在进行中，关闭窗口将停止编码（已写出的部分可能不完整）。确定关闭？", "制作四宫格比对视频", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                If answer <> DialogResult.Yes Then
                    e.Cancel = True
                    Return
                End If
                Try
                    If _process IsNot Nothing AndAlso Not _process.HasExited Then
                        _process.Kill()
                    End If
                Catch
                End Try
            End If
            _closing = True
            _playbackTimer.Stop()
            _previewDebounceTimer.Stop()
            _previewClock.Stop()
            StopStreamingPreview()
            Dim stillProcess As Process = Nothing
            SyncLock _previewProcessLock
                stillProcess = _stillFrameProcess
                _stillFrameProcess = Nothing
            End SyncLock
            If stillProcess IsNot Nothing Then
                Try
                    If Not stillProcess.HasExited Then stillProcess.Kill()
                Catch
                End Try
            End If
            System.Threading.Interlocked.Increment(_visualGeneration)
            SyncLock _displayLock
                If _pendingDisplayImage IsNot Nothing Then Try : _pendingDisplayImage.Dispose() : Catch : End Try
                _pendingDisplayImage = Nothing
            End SyncLock
            If _compositeFrame IsNot Nothing Then Try : _compositeFrame.Dispose() : Catch : End Try
            _compositeFrame = Nothing
            For Each img As Image In _frameCache.Values
                Try : img.Dispose() : Catch : End Try
            Next
            _frameCache.Clear()
            MyBase.OnFormClosing(e)
        End Sub

    End Class

End Namespace
