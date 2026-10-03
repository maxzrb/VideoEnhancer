Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Text.Json
Imports System.Text.RegularExpressions
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports LakeUI

Namespace videoenhancer

    Public Partial Class PluginPanel

        Private ReadOnly _pageSegmented As New ModernPanel()
        Private _segmentRoot As DpiLayoutPanel
        Private ReadOnly _cmbSegmentVideo As New WheelLockedComboBox()
        Private ReadOnly _cmbSegmentMode As New WheelLockedComboBox()
        Private ReadOnly _switchSegmented As New LakeUI.BooleanSwitch()
        Private ReadOnly _lblSegmentedSwitch As New HtmlColorLabel()
        Private ReadOnly _switchMixedSegmentBackends As New LakeUI.BooleanSwitch()
        Private ReadOnly _lblMixedSegmentBackends As New HtmlColorLabel()
        Private ReadOnly _btnSegmentRefresh As New ModernButton()
        Private ReadOnly _btnSegmentAdd As New ModernButton()
        Private ReadOnly _segmentRowsPanel As New DpiLayoutPanel()
        Private ReadOnly _lblSegmentStatus As New HtmlColorLabel()
        Private ReadOnly _segmentVideoPaths As New List(Of String)()
        Private ReadOnly _segmentModelChoices As New List(Of SegmentModelChoice)()
        Private ReadOnly _segmentVideoProbes As New Dictionary(Of String, SegmentVideoProbe)(StringComparer.OrdinalIgnoreCase)
        Private _segmentModelsLoaded As Boolean
        Private _segmentModelsLoading As Boolean
        Private _segmentVideosLoading As Boolean
        Private _segmentSync As Boolean

        Private NotInheritable Class SegmentModelChoice
            Public Property Backend As String = ""
            Public Property Model As String = ""
            Public Property DisplayName As String = ""
            Public Property Scale As Integer

            Public Overrides Function ToString() As String
                If Scale > 0 Then
                    Return SegmentBackendDisplayName(Backend) & " · " & DisplayName & " · " & Scale & "x"
                End If
                Return SegmentBackendDisplayName(Backend) & " · " & DisplayName & " · 自定义分辨率"
            End Function
        End Class

        Private NotInheritable Class SegmentRowControls
            Public Property Index As Integer
            Public Property StartBox As ModernTextBox
            Public Property EndBox As ModernTextBox
            Public Property ModelBox As WheelLockedComboBox
            Public Property WidthBox As ModernTextBox
            Public Property HeightBox As ModernTextBox
            Public Property Choices As List(Of SegmentModelChoice)
        End Class

        Private Shared Function SegmentBackendDisplayName(backend As String) As String
            Select Case If(backend, "").ToLowerInvariant()
                Case "cuda" : Return "CUDA"
                Case "tensorrt" : Return "TensorRT"
                Case "onnx" : Return "ONNX"
                Case "ffmpeg" : Return "FFmpeg 硬拉"
                Case "anime4k" : Return "Anime4K"
                Case Else : Return "NCNN"
            End Select
        End Function

        Private Shared Function IsSegmentModelBackend(backend As String) As Boolean
            Dim value = If(backend, "").Trim().ToLowerInvariant()
            Return value = "ncnn" OrElse value = "cuda" OrElse value = "tensorrt" OrElse value = "onnx"
        End Function

        Private Shared Function IsSegmentCustomBackend(backend As String) As Boolean
            Dim value = If(backend, "").Trim().ToLowerInvariant()
            Return value = "ffmpeg" OrElse value = "anime4k"
        End Function

        ' 分段表头与数据行共用同一组宽度，避免高 DPI 下标题换行或输入值被裁切。
        Private Const SegmentContentHeight As Integer = 656
        Private Const SegmentBoundaryColumnWidth As Single = 128.0F
        Private Const SegmentTargetSizeColumnWidth As Single = 96.0F
        Private Const SegmentActionColumnWidth As Single = 72.0F
        Private Const SegmentColumnGap As Single = UiColumnGap
        Private Const SegmentRowHeight As Integer = UiRowHeight
        Private Const SegmentRowPitch As Integer = 40

        Private Shared Function CreateSegmentGridPanel() As ModernHorizontalPanel
            Return New ModernHorizontalPanel(
                SegmentBoundaryColumnWidth, SegmentColumnGap,
                SegmentBoundaryColumnWidth, SegmentColumnGap,
                -1.0F, SegmentColumnGap,
                SegmentTargetSizeColumnWidth, SegmentColumnGap,
                SegmentTargetSizeColumnWidth, SegmentColumnGap,
                SegmentActionColumnWidth)
        End Function

        Private Sub BuildOfficialSegmentedPage()
            _pageSegmented.Dock = DockStyle.Fill
            _pageSegmented.LayoutMode = ModernPanel.LayoutModeEnum.Absolute
            Dim root As New DpiLayoutPanel With {
                .Dock = DockStyle.None,
                .Anchor = AnchorStyles.Top Or AnchorStyles.Left,
                .AutoSize = False,
                .MinimumSize = New Size(0, SegmentContentHeight),
                .Height = SegmentContentHeight,
                .BackColor = Color.Transparent,
                .BackColor1 = Color.Transparent,
                .LayoutMode = ModernPanel.LayoutModeEnum.Absolute,
                .BorderSize = 0
            }
            _segmentRoot = root
            AddHandler _pageSegmented.ClientSizeChanged, Sub(sender, e) SyncSegmentedRootBounds()
            AddHandler _pageSegmented.SizeChanged, Sub(sender, e) SyncSegmentedRootBounds()
            AddWorkbenchRow(root, CreateOfficialSectionHeading(
                "分段超分设置",
                "按秒分段并吸附关键帧；固定倍率模型优先决定尺寸，FFmpeg / Anime4K 自动跟随；自定义处理可设置宽高。"), 8, 40)

            _cmbSegmentVideo.WaterText = "切换到本页后读取 3FUI 添加文件列表…"
            ConfigureCombo(_cmbSegmentVideo)
            AddHandler _cmbSegmentVideo.SelectedIndexChanged, AddressOf OnSegmentVideoSelected
            Dim videoField = CreateOfficialField("视频", _cmbSegmentVideo, 0)
            ConfigureSecondaryButton(_btnSegmentRefresh)
            _btnSegmentRefresh.Text = "刷新视频列表"
            _btnSegmentRefresh.Dock = DockStyle.None
            _btnSegmentRefresh.Anchor = AnchorStyles.Left Or AnchorStyles.Right Or AnchorStyles.Bottom
            _btnSegmentRefresh.Margin = Padding.Empty
            AddHandler _btnSegmentRefresh.Click, Sub(sender, e) RefreshSegmentedVideos()
            Dim refreshField As New DpiLayoutPanel With {
                .Margin = Padding.Empty,
                .Padding = Padding.Empty,
                .BackColor = Color.Transparent,
                .BackColor1 = Color.Transparent,
                .BorderSize = 0
            }
            refreshField.Controls.Add(_btnSegmentRefresh)
            Dim arrangeRefresh =
                Sub()
                    ' 共用字段标题占位和控件高度，避免刷新按钮与视频框之间叠加额外边距。
                    _btnSegmentRefresh.SetBounds(0, refreshField.ScaleY(UiFieldEditorTop), refreshField.ClientSize.Width,
                        refreshField.ScaleY(UiControlHeight))
                End Sub
            AddHandler refreshField.Layout, Sub(sender, e) arrangeRefresh()
            arrangeRefresh()
            Dim videoRow As New ModernHorizontalPanel(-1.0F, CSng(UiColumnGap), 132.0F)
            videoRow.AddColumn(videoField, 0)
            videoRow.AddColumn(refreshField, 2)
            AddWorkbenchRow(root, videoRow, 52, UiFieldHeight)

            ConfigureCombo(_cmbSegmentMode)
            _cmbSegmentMode.Items.Add("按秒（默认，断点自动吸附关键帧）")
            _cmbSegmentMode.Items.Add("精确帧（兼容旧模式）")
            AddHandler _cmbSegmentMode.SelectedIndexChanged, AddressOf OnSegmentModeChanged
            AddWorkbenchControl(root, CreateOfficialField("分段计数模式", _cmbSegmentMode), 112, UiFieldHeight, 0.0F, 1.0F)

            ConfigureDpiSwitch(_switchSegmented)
            AddHandler _switchSegmented.CheckedChanged, AddressOf OnSegmentedSwitchChanged
            _lblSegmentedSwitch.AutoSize = False
            _lblSegmentedSwitch.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            Dim switchCaption = CreateOfficialCaption("分段总开关")
            switchCaption.Dock = DockStyle.Fill
            switchCaption.TextAlign = ContentAlignment.MiddleLeft
            Dim switchCaptionWidth = Math.Max(80,
                MeasureTextWidth96(switchCaption.Text, switchCaption.Font) + UiColumnGap)
            Dim switchRow As New ModernHorizontalPanel(
                CSng(switchCaptionWidth), CSng(UiColumnGap), 40.0F, CSng(UiColumnGap), -1.0F, CSng(UiColumnGap), 132.0F)
            _switchSegmented.Dock = DockStyle.None
            _switchSegmented.Anchor = AnchorStyles.None
            _switchSegmented.Margin = Padding.Empty
            _lblSegmentedSwitch.Dock = DockStyle.Fill
            _lblSegmentedSwitch.Margin = Padding.Empty
            ConfigurePrimaryButton(_btnSegmentAdd)
            _btnSegmentAdd.Text = "＋ 添加断点"
            _btnSegmentAdd.Dock = DockStyle.None
            _btnSegmentAdd.Anchor = AnchorStyles.None
            _btnSegmentAdd.Size = New Size(120, UiControlHeight)
            _btnSegmentAdd.Margin = Padding.Empty
            AddHandler _btnSegmentAdd.Click, AddressOf OnAddSegment
            switchRow.AddColumn(switchCaption, 0)
            switchRow.AddColumn(_switchSegmented, 2)
            switchRow.AddColumn(_lblSegmentedSwitch, 4)
            switchRow.AddColumn(_btnSegmentAdd, 6)
            AddWorkbenchRow(root, switchRow, 176, 32)

            ConfigureDpiSwitch(_switchMixedSegmentBackends)
            AddHandler _switchMixedSegmentBackends.CheckedChanged, AddressOf OnMixedSegmentBackendsChanged
            _lblMixedSegmentBackends.AutoSize = False
            _lblMixedSegmentBackends.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            Dim mixedBackendCaption = CreateOfficialCaption("测试功能：跨模型后端混用")
            mixedBackendCaption.Dock = DockStyle.Fill
            mixedBackendCaption.TextAlign = ContentAlignment.MiddleLeft
            Dim mixedBackendCaptionWidth = Math.Max(156,
                MeasureTextWidth96(mixedBackendCaption.Text, mixedBackendCaption.Font) + UiColumnGap)
            Dim mixedBackendRow As New ModernHorizontalPanel(
                CSng(mixedBackendCaptionWidth), CSng(UiColumnGap), 40.0F, CSng(UiColumnGap), -1.0F)
            _switchMixedSegmentBackends.Dock = DockStyle.None
            _switchMixedSegmentBackends.Anchor = AnchorStyles.None
            _switchMixedSegmentBackends.Margin = Padding.Empty
            _lblMixedSegmentBackends.Dock = DockStyle.Fill
            _lblMixedSegmentBackends.Margin = Padding.Empty
            mixedBackendRow.AddColumn(mixedBackendCaption, 0)
            mixedBackendRow.AddColumn(_switchMixedSegmentBackends, 2)
            mixedBackendRow.AddColumn(_lblMixedSegmentBackends, 4)
            AddWorkbenchRow(root, mixedBackendRow, 212, 32)

            Dim header = CreateSegmentGridPanel()
            For Each caption In New String() {"入点（秒/帧）", "出点（秒/帧）", "处理方式", "目标宽", "目标高", "操作"}
                Dim label = CreateOfficialCaption(caption)
                label.Dock = DockStyle.Fill
                label.TextAlign = ContentAlignment.MiddleLeft
                header.AddColumn(label, header.Controls.Count * 2)
            Next
            AddWorkbenchRow(root, header, 252, 28)

            _segmentRowsPanel.BackColor = Color.Transparent
            _segmentRowsPanel.BackColor1 = Color.Transparent
            _segmentRowsPanel.BorderColor = UiSurface
            _segmentRowsPanel.BorderSize = 1
            _segmentRowsPanel.BorderRadius = 6
            _segmentRowsPanel.LayoutMode = ModernPanel.LayoutModeEnum.Absolute
            _segmentRowsPanel.AutoScroll = True
            AddWorkbenchRow(root, _segmentRowsPanel, 280, 300)
            AddHandler _segmentRowsPanel.ClientSizeChanged, AddressOf OnSegmentRowsPanelResized

            _lblSegmentStatus.AutoSize = False
            _lblSegmentStatus.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _lblSegmentStatus.Text = "<font color=#888888>切换到本页后会读取视频时长、尺寸和关键帧。</font>"
            AddWorkbenchRow(root, CreateOfficialValueBox(_lblSegmentStatus), 588, 56)
            _pageSegmented.Controls.Add(root)
            EnsureBuiltinSegmentChoices()
            SyncSegmentedRootBounds()
        End Sub

        Private Sub SyncSegmentedRootBounds()
            Dim root = _segmentRoot
            If root Is Nothing OrElse root.IsDisposed OrElse
               _pageSegmented Is Nothing OrElse _pageSegmented.IsDisposed Then Return
            Dim width = Math.Max(0, _pageSegmented.ClientSize.Width - root.ScaleX(_pageSegmented.ScrollBarWidth + 2))
            Dim contentHeight = root.ScaleY(SegmentContentHeight)
            Dim rootLeft = If(_pageSegmented.HorizontalScrollOffset > 0, root.Left, 0)
            Dim rootTop = If(_pageSegmented.VerticalScrollOffset > 0, root.Top, 0)
            If root.Left <> rootLeft OrElse root.Top <> rootTop OrElse root.Width <> width OrElse root.Height <> contentHeight Then
                root.SetBounds(rootLeft, rootTop, width, contentHeight)
            End If
        End Sub

        Private Sub ActivateSegmentedPage()
            LoadSegmentModelCatalogs()
            RefreshSegmentedVideos()
        End Sub

        Private Sub EnsureBuiltinSegmentChoices()
            If Not _segmentModelChoices.Any(Function(choice) choice.Backend = "ffmpeg") Then
                For Each pair In New (String, String)() {
                    ("lanczos", "Lanczos"),
                    ("bicubic", "Bicubic"),
                    ("bilinear", "Bilinear"),
                    ("neighbor", "Nearest Neighbour"),
                    ("area", "Area"),
                    ("spline", "Spline"),
                    ("fast_bilinear", "Fast Bilinear"),
                    ("bicublin", "Bicubic Luma / Bilinear Chroma")
                }
                    _segmentModelChoices.Add(New SegmentModelChoice With {
                        .Backend = "ffmpeg", .Model = pair.Item1, .DisplayName = pair.Item2, .Scale = 0
                    })
                Next
            End If
            If Not _segmentModelChoices.Any(Function(choice) choice.Backend = "anime4k") Then
                For Each pair In New (String, String)() {
                    ("anime4k-v4-a.glsl", "Anime4K v4 Mode A"),
                    ("anime4k-v4-a+a.glsl", "Anime4K v4 Mode A+A"),
                    ("anime4k-v4-b.glsl", "Anime4K v4 Mode B"),
                    ("anime4k-v4-b+b.glsl", "Anime4K v4 Mode B+B"),
                    ("anime4k-v4-c.glsl", "Anime4K v4 Mode C"),
                    ("anime4k-v4-c+a.glsl", "Anime4K v4 Mode C+A"),
                    ("anime4k-v4.1-gan.glsl", "Anime4K v4.1 GAN")
                }
                    _segmentModelChoices.Add(New SegmentModelChoice With {
                        .Backend = "anime4k", .Model = pair.Item1, .DisplayName = pair.Item2, .Scale = 0
                    })
                Next
            End If
        End Sub

        Private Async Sub LoadSegmentModelCatalogs()
            EnsureBuiltinSegmentChoices()
            If _segmentModelsLoaded OrElse _segmentModelsLoading Then Return
            Dim exePath = PluginConfig.ResolveInstalledExePath()
            If String.IsNullOrWhiteSpace(exePath) OrElse Not File.Exists(exePath) Then
                RenderSegmentRows()
                Return
            End If
            _segmentModelsLoading = True
            Try
                Dim choices = Await Task.Run(Function()
                    Dim result As New List(Of SegmentModelChoice)()
                    For Each backend In New String() {"ncnn", "cuda", "tensorrt", "onnx"}
                        For Each item In ModelCatalogClient.RunModelCatalog(exePath, "--list-model-catalog", "-backend", backend)
                            Dim scale = If(item.Scale > 0, item.Scale, InferSegmentScale(item.Id))
                            If scale <= 0 Then Continue For
                            result.Add(New SegmentModelChoice With {
                                .Backend = backend,
                                .Model = item.Id,
                                .DisplayName = If(String.IsNullOrWhiteSpace(item.DisplayName), item.Id, item.DisplayName),
                                .Scale = scale
                            })
                        Next
                    Next
                    Return result
                End Function)
                _segmentModelChoices.RemoveAll(Function(choice) IsSegmentModelBackend(choice.Backend))
                _segmentModelChoices.AddRange(choices)
                _segmentModelsLoaded = True
                RenderSegmentRows()
            Finally
                _segmentModelsLoading = False
            End Try
        End Sub

        Private Shared Function InferSegmentScale(model As String) As Integer
            Dim match = Regex.Match(If(model, ""), "(?:^|[-_])(\d+)x(?:[-_]|$)|(?:^|[-_])x(\d+)(?:[-_]|$)", RegexOptions.IgnoreCase)
            If Not match.Success Then Return 0
            Dim text = If(match.Groups(1).Success, match.Groups(1).Value, match.Groups(2).Value)
            Dim value As Integer
            Return If(Integer.TryParse(text, value), value, 0)
        End Function

        Private Async Sub RefreshSegmentedVideos()
            If _segmentVideosLoading Then Return
            _segmentVideosLoading = True
            _btnSegmentRefresh.Enabled = False
            _lblSegmentStatus.Text = "<font color=#B8B8B8>正在读取视频时长、尺寸和关键帧…</font>"
            Try
                Dim paths = QueueHook.GetCurrentPrepareFilePaths().
                    Where(Function(path) Not String.IsNullOrWhiteSpace(path) AndAlso File.Exists(path)).
                    Select(Function(filePath) IO.Path.GetFullPath(filePath)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                Dim ffprobe = FfmpegToolResolver.Resolve("ffprobe.exe")
                Dim probed = Await Task.Run(Function()
                    Dim result As New List(Of SegmentVideoProbe)()
                    For Each filePath As String In paths
                        Dim probe = SegmentVideoProbeService.ProbeSegmentVideo(ffprobe, filePath)
                        If probe IsNot Nothing Then result.Add(probe)
                    Next
                    Return result
                End Function)

                Dim previousPath = SelectedSegmentVideoPath()
                _segmentSync = True
                _cmbSegmentVideo.Items.Clear()
                _segmentVideoPaths.Clear()
                _segmentVideoProbes.Clear()
                For Each probe In probed
                    _segmentVideoPaths.Add(probe.Path)
                    _segmentVideoProbes(probe.Path) = probe
                    _cmbSegmentVideo.Items.Add(
                        Path.GetFileName(probe.Path) & "　（" &
                        FormatSegmentSeconds(probe.DurationSeconds) & " 秒 · " &
                        probe.Width & "×" & probe.Height & " · 关键帧 " & probe.Keyframes.Count & "）")
                    EnsureSegmentVideoConfig(probe)
                Next
                Dim selectedIndex = If(previousPath.Length = 0, -1, _segmentVideoPaths.FindIndex(
                    Function(path) String.Equals(path, previousPath, StringComparison.OrdinalIgnoreCase)))
                If selectedIndex < 0 AndAlso _segmentVideoPaths.Count > 0 Then selectedIndex = 0
                _cmbSegmentVideo.SelectedIndex = selectedIndex
                _segmentSync = False
                _config.Save()
                RefreshSegmentSelection()
                If probed.Count = 0 Then
                    _lblSegmentStatus.Text = If(String.IsNullOrWhiteSpace(ffprobe),
                        "<font color=#E07878>未找到 ffprobe，无法读取分段视频信息。</font>",
                        "<font color=#E07878>添加文件列表中没有可读取的视频。</font>")
                End If
            Catch ex As Exception
                _segmentSync = False
                _lblSegmentStatus.Text = "<font color=#E07878>视频检测失败：" & EscapeHtml(ex.Message) & "</font>"
            Finally
                _segmentVideosLoading = False
                _btnSegmentRefresh.Enabled = True
            End Try
        End Sub

        Private Function EnsureSegmentVideoConfig(probe As SegmentVideoProbe) As SegmentedVideoConfig
            If _config.SegmentedVideos Is Nothing Then _config.SegmentedVideos = New List(Of SegmentedVideoConfig)()
            Dim config = _config.SegmentedVideos.FirstOrDefault(
                Function(item) String.Equals(item.Path, probe.Path, StringComparison.OrdinalIgnoreCase))
            If config Is Nothing Then
                config = New SegmentedVideoConfig With {
                    .Path = probe.Path,
                    .FrameCount = probe.FrameCount,
                    .DurationSeconds = probe.DurationSeconds,
                    .SourceWidth = probe.Width,
                    .SourceHeight = probe.Height,
                    .BoundaryMode = "seconds"
                }
                config.Segments.Add(New SegmentedUpscaleRange With {
                    .StartSeconds = 0,
                    .EndSeconds = probe.DurationSeconds,
                    .TargetWidth = probe.Width * 2,
                    .TargetHeight = probe.Height * 2
                })
                _config.SegmentedVideos.Add(config)
                Return config
            End If

            config.Path = probe.Path
            config.DurationSeconds = probe.DurationSeconds
            config.SourceWidth = probe.Width
            config.SourceHeight = probe.Height
            If config.FrameCount <= 0 Then config.FrameCount = probe.FrameCount
            If config.Segments Is Nothing Then config.Segments = New List(Of SegmentedUpscaleRange)()
            If String.IsNullOrWhiteSpace(config.BoundaryMode) Then
                config.BoundaryMode = If(config.Segments.Any(
                    Function(segment) segment.Start > 0 OrElse segment.[End] > 0), "frames", "seconds")
            End If
            If config.Segments.Count = 0 Then
                If String.Equals(config.BoundaryMode, "frames", StringComparison.OrdinalIgnoreCase) Then
                    config.Segments.Add(New SegmentedUpscaleRange With {.Start = 1, .End = config.FrameCount})
                Else
                    config.Segments.Add(New SegmentedUpscaleRange With {
                        .StartSeconds = 0, .EndSeconds = probe.DurationSeconds,
                        .TargetWidth = probe.Width * 2, .TargetHeight = probe.Height * 2
                    })
                End If
            End If
            If String.Equals(config.BoundaryMode, "seconds", StringComparison.OrdinalIgnoreCase) Then
                config.Segments(0).StartSeconds = 0
                config.Segments(config.Segments.Count - 1).EndSeconds = probe.DurationSeconds
                SegmentEditingRules.SnapAllSegmentBoundaries(config, probe)
                SegmentEditingRules.ApplySegmentResolutionRule(config)
            Else
                config.Segments(0).Start = 1
                config.Segments(config.Segments.Count - 1).[End] = config.FrameCount
            End If
            Return config
        End Function

        Private Function SelectedSegmentVideoPath() As String
            If _cmbSegmentVideo.SelectedIndex < 0 OrElse _cmbSegmentVideo.SelectedIndex >= _segmentVideoPaths.Count Then Return ""
            Return _segmentVideoPaths(_cmbSegmentVideo.SelectedIndex)
        End Function

        Private Function SelectedSegmentProbe() As SegmentVideoProbe
            Dim path = SelectedSegmentVideoPath()
            Dim probe As SegmentVideoProbe = Nothing
            If path.Length > 0 Then _segmentVideoProbes.TryGetValue(path, probe)
            Return probe
        End Function

        Private Function SelectedSegmentConfig() As SegmentedVideoConfig
            Dim path = SelectedSegmentVideoPath()
            If path.Length = 0 OrElse _config.SegmentedVideos Is Nothing Then Return Nothing
            Return _config.SegmentedVideos.FirstOrDefault(
                Function(item) String.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase))
        End Function

        Private Sub OnSegmentVideoSelected(sender As Object, e As EventArgs)
            If _segmentSync Then Return
            RefreshSegmentSelection()
        End Sub

        Private Sub RefreshSegmentSelection()
            Dim config = SelectedSegmentConfig()
            _segmentSync = True
            _switchSegmented.Enabled = config IsNot Nothing AndAlso _config.Enabled
            _switchSegmented.Checked = config IsNot Nothing AndAlso config.Enabled
            _switchMixedSegmentBackends.Enabled = config IsNot Nothing
            _switchMixedSegmentBackends.Checked = config IsNot Nothing AndAlso config.AllowMixedModelBackends
            _btnSegmentAdd.Enabled = config IsNot Nothing AndAlso
                (config.DurationSeconds > 0 OrElse config.FrameCount > 1)
            If config Is Nothing Then
                _cmbSegmentMode.SelectedIndex = -1
            Else
                _cmbSegmentMode.SelectedIndex = If(String.Equals(config.BoundaryMode, "frames", StringComparison.OrdinalIgnoreCase), 1, 0)
            End If
            _cmbSegmentMode.Enabled = config IsNot Nothing
            _segmentSync = False
            RefreshSegmentSwitchText()
            RefreshMixedSegmentBackendText()
            RenderSegmentRows()
            ValidateAndShowSegmentConfig()
        End Sub

        Private Async Sub OnSegmentModeChanged(sender As Object, e As EventArgs)
            If _segmentSync Then Return
            Dim config = SelectedSegmentConfig()
            Dim probe = SelectedSegmentProbe()
            If config Is Nothing OrElse probe Is Nothing OrElse _cmbSegmentMode.SelectedIndex < 0 Then Return
            Dim requestedMode = If(_cmbSegmentMode.SelectedIndex = 1, "frames", "seconds")
            If String.Equals(config.BoundaryMode, requestedMode, StringComparison.OrdinalIgnoreCase) Then Return

            If requestedMode = "frames" Then
                _cmbSegmentMode.Enabled = False
                _lblSegmentStatus.Text = "<font color=#B8B8B8>正在读取精确帧数…</font>"
                Dim ffprobe = FfmpegToolResolver.Resolve("ffprobe.exe")
                Dim exactFrames = Await Task.Run(Function() SegmentVideoProbeService.ProbeSegmentFrameCount(ffprobe, config.Path))
                _cmbSegmentMode.Enabled = True
                If exactFrames <= 0 Then
                    _segmentSync = True
                    _cmbSegmentMode.SelectedIndex = 0
                    _segmentSync = False
                    _lblSegmentStatus.Text = "<font color=#E07878>无法读取精确帧数，已保留按秒模式。</font>"
                    Return
                End If
                config.FrameCount = exactFrames
                SegmentEditingRules.ConvertSegmentSecondsToFrames(config)
                config.BoundaryMode = "frames"
            Else
                config.BoundaryMode = "seconds"
                SegmentEditingRules.ConvertSegmentFramesToSeconds(config, probe)
            End If
            _config.Save()
            RenderSegmentRows()
            ValidateAndShowSegmentConfig()
        End Sub

        Private Sub OnSegmentedSwitchChanged(sender As Object, e As EventArgs)
            If _segmentSync Then Return
            Dim config = SelectedSegmentConfig()
            If config Is Nothing Then Return
            If _switchSegmented.Checked AndAlso Not _config.Enabled Then
                _segmentSync = True
                _switchSegmented.Checked = False
                _segmentSync = False
                ShowStatus("请先开启「插件总开关」", True)
                Return
            End If
            config.Enabled = _switchSegmented.Checked
            _config.Save()
            RefreshSegmentSwitchText()
            ValidateAndShowSegmentConfig()
            UpdateHookState()
        End Sub

        Private Function HasEnabledSegmentedVideo() As Boolean
            Return _config.SegmentedVideos IsNot Nothing AndAlso
                _config.SegmentedVideos.Any(Function(item) item IsNot Nothing AndAlso item.Enabled)
        End Function

        Private Sub RefreshSegmentSwitchText()
            _lblSegmentedSwitch.Text = If(_switchSegmented.Checked,
                "<font color=#479CFF><b>已开启</b></font>", "<font color=#888888>关闭</font>")
        End Sub

        Private Sub OnMixedSegmentBackendsChanged(sender As Object, e As EventArgs)
            If _segmentSync Then Return
            Dim config = SelectedSegmentConfig()
            If config Is Nothing Then Return
            config.AllowMixedModelBackends = _switchMixedSegmentBackends.Checked
            _config.Save()
            RefreshMixedSegmentBackendText()
            RenderSegmentRows()
            ValidateAndShowSegmentConfig()
        End Sub

        Private Sub RefreshMixedSegmentBackendText()
            _lblMixedSegmentBackends.Text = If(_switchMixedSegmentBackends.Checked,
                "<font color=#E8B45A><b>已开启实验功能</b>：仅建议熟悉各后端依赖的用户使用</font>",
                "<font color=#888888>默认关闭；NCNN / CUDA / TensorRT / ONNX 之间不允许跨后端混用</font>")
        End Sub

        Private Sub OnAddSegment(sender As Object, e As EventArgs)
            Dim config = SelectedSegmentConfig()
            Dim probe = SelectedSegmentProbe()
            If config Is Nothing OrElse config.Segments.Count = 0 Then Return
            Dim last = config.Segments(config.Segments.Count - 1)
            Dim newSegment As New SegmentedUpscaleRange With {
                .Backend = last.Backend,
                .Model = last.Model,
                .DisplayName = last.DisplayName,
                .Scale = last.Scale,
                .TargetWidth = last.TargetWidth,
                .TargetHeight = last.TargetHeight
            }
            If String.Equals(config.BoundaryMode, "seconds", StringComparison.OrdinalIgnoreCase) Then
                If probe Is Nothing OrElse last.EndSeconds - last.StartSeconds <= 0.002 Then
                    ShowStatus("最后一段没有可用的秒级区间，不能继续添加断点", True)
                    Return
                End If
                Dim oldEnd = last.EndSeconds
                Dim desired = last.StartSeconds + (oldEnd - last.StartSeconds) / 2
                Dim split = SegmentEditingRules.SnapSegmentBoundary(probe, desired, last.StartSeconds, oldEnd)
                If split < 0 Then
                    ShowStatus("最后一段附近没有可用的内部关键帧，不能继续拆分", True)
                    Return
                End If
                last.EndSeconds = split
                newSegment.StartSeconds = split
                newSegment.EndSeconds = oldEnd
            Else
                If last.[End] <= last.Start Then
                    ShowStatus("最后一段只有一帧，不能继续拆分", True)
                    Return
                End If
                Dim oldEnd = last.[End]
                Dim split = last.Start + (oldEnd - last.Start) \ 2
                last.[End] = split
                newSegment.Start = split + 1
                newSegment.[End] = oldEnd
            End If
            config.Segments.Add(newSegment)
            SegmentEditingRules.ApplySegmentResolutionRule(config)
            _config.Save()
            RenderSegmentRows()
            ValidateAndShowSegmentConfig()
        End Sub

        Private Sub RenderSegmentRows()
            _segmentRowsPanel.Controls.Clear()
            Dim config = SelectedSegmentConfig()
            If config Is Nothing Then Return
            Dim secondsMode = String.Equals(config.BoundaryMode, "seconds", StringComparison.OrdinalIgnoreCase)
            Dim fixedScale = SegmentEditingRules.SegmentFixedScale(config)
            _segmentSync = True
            Try
                For index = 0 To config.Segments.Count - 1
                    Dim segment = config.Segments(index)
                    Dim row As New SegmentRowControls With {.Index = index}
                    row.StartBox = New ModernTextBox()
                    row.EndBox = New ModernTextBox()
                    row.ModelBox = New WheelLockedComboBox()
                    row.WidthBox = New ModernTextBox()
                    row.HeightBox = New ModernTextBox()
                    ConfigureOfficialTextBox(row.StartBox, "入点")
                    ConfigureOfficialTextBox(row.EndBox, "出点")
                    ConfigureOfficialTextBox(row.WidthBox, "宽")
                    ConfigureOfficialTextBox(row.HeightBox, "高")
                    If secondsMode Then
                        row.StartBox.Text = FormatSegmentSeconds(segment.StartSeconds)
                        row.EndBox.Text = FormatSegmentSeconds(segment.EndSeconds)
                    Else
                        row.StartBox.Text = segment.Start.ToString(CultureInfo.InvariantCulture)
                        row.EndBox.Text = segment.[End].ToString(CultureInfo.InvariantCulture)
                    End If
                    row.StartBox.Enabled = index > 0
                    row.EndBox.Enabled = index < config.Segments.Count - 1
                    row.StartBox.Tag = row
                    row.EndBox.Tag = row
                    AddHandler row.StartBox.Leave, AddressOf OnSegmentStartLeave
                    AddHandler row.EndBox.Leave, AddressOf OnSegmentEndLeave

                    ConfigureCombo(row.ModelBox)
                    row.ModelBox.WaterText = "选择模型 / FFmpeg / Anime4K…"
                    row.Choices = SegmentChoicesForRow(config, index)
                    For Each choice In row.Choices
                        row.ModelBox.Items.Add(choice.ToString())
                    Next
                    Dim choiceIndex = row.Choices.FindIndex(Function(choice)
                        Return String.Equals(choice.Backend, segment.Backend, StringComparison.OrdinalIgnoreCase) AndAlso
                            String.Equals(choice.Model, segment.Model, StringComparison.OrdinalIgnoreCase)
                    End Function)
                    row.ModelBox.SelectedIndex = choiceIndex
                    row.ModelBox.Tag = row
                    AddHandler row.ModelBox.SelectedIndexChanged, AddressOf OnSegmentModelSelected

                    row.WidthBox.Text = If(segment.TargetWidth > 0, segment.TargetWidth.ToString(CultureInfo.InvariantCulture), "")
                    row.HeightBox.Text = If(segment.TargetHeight > 0, segment.TargetHeight.ToString(CultureInfo.InvariantCulture), "")
                    Dim customSizeEnabled = fixedScale = 0 AndAlso IsSegmentCustomBackend(segment.Backend)
                    row.WidthBox.Enabled = customSizeEnabled
                    row.HeightBox.Enabled = customSizeEnabled
                    row.WidthBox.Tag = row
                    row.HeightBox.Tag = row
                    AddHandler row.WidthBox.Leave, AddressOf OnSegmentTargetSizeLeave
                    AddHandler row.HeightBox.Leave, AddressOf OnSegmentTargetSizeLeave

                    Dim deleteButton As New ModernButton()
                    ConfigureSecondaryButton(deleteButton)
                    deleteButton.Text = "删除"
                    deleteButton.Dock = DockStyle.Fill
                    deleteButton.Margin = New Padding(0, 4, 0, 4)
                    deleteButton.Enabled = config.Segments.Count > 1
                    deleteButton.Tag = index
                    AddHandler deleteButton.Click, AddressOf OnDeleteSegment

                    Dim rowPanel = CreateSegmentGridPanel()
                    rowPanel.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
                    For Each textBox In New ModernTextBox() {row.StartBox, row.EndBox, row.WidthBox, row.HeightBox}
                        textBox.Dock = DockStyle.Fill
                        textBox.Margin = New Padding(0, 4, 0, 4)
                    Next
                    row.ModelBox.Dock = DockStyle.Fill
                    row.ModelBox.Margin = New Padding(0, 4, 0, 4)
                    rowPanel.AddColumn(row.StartBox, 0)
                    rowPanel.AddColumn(row.EndBox, 2)
                    rowPanel.AddColumn(row.ModelBox, 4)
                    rowPanel.AddColumn(row.WidthBox, 6)
                    rowPanel.AddColumn(row.HeightBox, 8)
                    rowPanel.AddColumn(deleteButton, 10)
                    ' 懒加载创建的行不经过初始自动缩放，加入页面前按当前比例缩放一次。
                    rowPanel.Scale(_segmentRowsPanel.LayoutScale)
                    rowPanel.SetBounds(
                        _segmentRowsPanel.ScaleX(8), _segmentRowsPanel.ScaleY(8 + index * SegmentRowPitch),
                        Math.Max(_segmentRowsPanel.ScaleX(720), _segmentRowsPanel.ClientSize.Width - _segmentRowsPanel.ScaleX(24)),
                        _segmentRowsPanel.ScaleY(SegmentRowHeight))
                    _segmentRowsPanel.Controls.Add(rowPanel)
                Next
                _segmentRowsPanel.AutoScrollMinSize = New Size(0, _segmentRowsPanel.ScaleY(16 + config.Segments.Count * SegmentRowPitch))
            Finally
                _segmentSync = False
            End Try
        End Sub

        Private Function SegmentChoicesForRow(config As SegmentedVideoConfig, index As Integer) As List(Of SegmentModelChoice)
            Dim current = config.Segments(index)
            Dim otherModels = config.Segments.
                Where(Function(segment, currentIndex) currentIndex <> index AndAlso IsSegmentModelBackend(segment.Backend) AndAlso segment.Scale > 0).
                ToList()
            If otherModels.Count = 0 Then Return _segmentModelChoices.ToList()
            Dim lockedScale = otherModels(0).Scale
            Dim lockedBackend = otherModels(0).Backend
            Return _segmentModelChoices.Where(
                Function(choice)
                    If choice.Scale = 0 Then Return True
                    If choice.Scale <> lockedScale Then Return False
                    If config.AllowMixedModelBackends Then Return True
                    If String.Equals(choice.Backend, lockedBackend, StringComparison.OrdinalIgnoreCase) Then Return True
                    Return String.Equals(choice.Backend, current.Backend, StringComparison.OrdinalIgnoreCase) AndAlso
                        String.Equals(choice.Model, current.Model, StringComparison.OrdinalIgnoreCase)
                End Function).ToList()
        End Function

        Private Sub OnSegmentModelSelected(sender As Object, e As EventArgs)
            If _segmentSync Then Return
            Dim combo = TryCast(sender, WheelLockedComboBox)
            Dim row = TryCast(If(combo Is Nothing, Nothing, combo.Tag), SegmentRowControls)
            Dim config = SelectedSegmentConfig()
            If combo Is Nothing OrElse row Is Nothing OrElse config Is Nothing OrElse
                combo.SelectedIndex < 0 OrElse combo.SelectedIndex >= row.Choices.Count Then Return
            Dim choice = row.Choices(combo.SelectedIndex)
            Dim segment = config.Segments(row.Index)
            segment.Backend = choice.Backend
            segment.Model = choice.Model
            segment.DisplayName = choice.DisplayName
            segment.Scale = choice.Scale
            SegmentEditingRules.ApplySegmentResolutionRule(config)
            _config.Save()
            RenderSegmentRows()
            ValidateAndShowSegmentConfig()
        End Sub

        Private Sub OnSegmentTargetSizeLeave(sender As Object, e As EventArgs)
            If _segmentSync Then Return
            Dim box = TryCast(sender, ModernTextBox)
            Dim row = TryCast(If(box Is Nothing, Nothing, box.Tag), SegmentRowControls)
            Dim config = SelectedSegmentConfig()
            If box Is Nothing OrElse row Is Nothing OrElse config Is Nothing OrElse SegmentEditingRules.SegmentFixedScale(config) > 0 Then Return
            Dim segment = config.Segments(row.Index)
            If Not IsSegmentCustomBackend(segment.Backend) Then Return
            Dim value As Integer
            If Not Integer.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, value) OrElse value <= 0 Then
                ShowStatus("目标宽高必须是正整数", True)
                RenderSegmentRows()
                Return
            End If
            Dim width = If(ReferenceEquals(box, row.WidthBox), value, segment.TargetWidth)
            Dim height = If(ReferenceEquals(box, row.HeightBox), value, segment.TargetHeight)
            If width <= 0 Then width = Math.Max(1, config.SourceWidth * 2)
            If height <= 0 Then height = Math.Max(1, config.SourceHeight * 2)
            For Each item In config.Segments
                item.TargetWidth = width
                item.TargetHeight = height
            Next
            _config.Save()
            RenderSegmentRows()
            ValidateAndShowSegmentConfig()
        End Sub

        Private Sub OnSegmentStartLeave(sender As Object, e As EventArgs)
            If _segmentSync Then Return
            Dim box = TryCast(sender, ModernTextBox)
            Dim row = TryCast(If(box Is Nothing, Nothing, box.Tag), SegmentRowControls)
            Dim config = SelectedSegmentConfig()
            If row Is Nothing OrElse config Is Nothing OrElse row.Index <= 0 Then Return
            If String.Equals(config.BoundaryMode, "seconds", StringComparison.OrdinalIgnoreCase) Then
                Dim probe = SelectedSegmentProbe()
                Dim value As Double
                If probe Is Nothing OrElse Not TryParseSegmentSeconds(box.Text, value) Then
                    ShowStatus("入点秒数无效", True) : RenderSegmentRows() : Return
                End If
                Dim previous = config.Segments(row.Index - 1)
                Dim current = config.Segments(row.Index)
                Dim snapped = SegmentEditingRules.SnapSegmentBoundary(probe, value, previous.StartSeconds, current.EndSeconds)
                If snapped < 0 Then
                    ShowStatus("附近没有可用的内部关键帧", True) : RenderSegmentRows() : Return
                End If
                previous.EndSeconds = snapped
                current.StartSeconds = snapped
            Else
                Dim value As Long
                If Not Long.TryParse(box.Text, value) OrElse value <= config.Segments(row.Index - 1).Start OrElse value > config.Segments(row.Index).[End] Then
                    ShowStatus("入点必须是上一段入点之后、当前出点之前的唯一帧号", True)
                    RenderSegmentRows() : Return
                End If
                config.Segments(row.Index).Start = value
                config.Segments(row.Index - 1).[End] = value - 1
            End If
            _config.Save()
            RenderSegmentRows()
            ValidateAndShowSegmentConfig()
        End Sub

        Private Sub OnSegmentEndLeave(sender As Object, e As EventArgs)
            If _segmentSync Then Return
            Dim box = TryCast(sender, ModernTextBox)
            Dim row = TryCast(If(box Is Nothing, Nothing, box.Tag), SegmentRowControls)
            Dim config = SelectedSegmentConfig()
            If row Is Nothing OrElse config Is Nothing OrElse row.Index >= config.Segments.Count - 1 Then Return
            If String.Equals(config.BoundaryMode, "seconds", StringComparison.OrdinalIgnoreCase) Then
                Dim probe = SelectedSegmentProbe()
                Dim value As Double
                If probe Is Nothing OrElse Not TryParseSegmentSeconds(box.Text, value) Then
                    ShowStatus("出点秒数无效", True) : RenderSegmentRows() : Return
                End If
                Dim current = config.Segments(row.Index)
                Dim following = config.Segments(row.Index + 1)
                Dim snapped = SegmentEditingRules.SnapSegmentBoundary(probe, value, current.StartSeconds, following.EndSeconds)
                If snapped < 0 Then
                    ShowStatus("附近没有可用的内部关键帧", True) : RenderSegmentRows() : Return
                End If
                current.EndSeconds = snapped
                following.StartSeconds = snapped
            Else
                Dim value As Long
                If Not Long.TryParse(box.Text, value) OrElse value < config.Segments(row.Index).Start OrElse value >= config.Segments(row.Index + 1).[End] Then
                    ShowStatus("出点必须位于当前入点之后、下一段出点之前，且不能与其他边界重复", True)
                    RenderSegmentRows() : Return
                End If
                config.Segments(row.Index).[End] = value
                config.Segments(row.Index + 1).Start = value + 1
            End If
            _config.Save()
            RenderSegmentRows()
            ValidateAndShowSegmentConfig()
        End Sub

        Private Sub OnDeleteSegment(sender As Object, e As EventArgs)
            Dim button = TryCast(sender, ModernButton)
            Dim config = SelectedSegmentConfig()
            If button Is Nothing OrElse config Is Nothing OrElse config.Segments.Count <= 1 Then Return
            Dim index = CInt(button.Tag)
            Dim removed = config.Segments(index)
            config.Segments.RemoveAt(index)
            If String.Equals(config.BoundaryMode, "seconds", StringComparison.OrdinalIgnoreCase) Then
                If index = 0 Then
                    config.Segments(0).StartSeconds = 0
                Else
                    config.Segments(index - 1).EndSeconds = removed.EndSeconds
                End If
                config.Segments(config.Segments.Count - 1).EndSeconds = config.DurationSeconds
            Else
                If index = 0 Then
                    config.Segments(0).Start = 1
                Else
                    config.Segments(index - 1).[End] = removed.[End]
                End If
                config.Segments(config.Segments.Count - 1).[End] = config.FrameCount
            End If
            SegmentEditingRules.ApplySegmentResolutionRule(config)
            _config.Save()
            RenderSegmentRows()
            ValidateAndShowSegmentConfig()
        End Sub

        Private Sub OnSegmentRowsPanelResized(sender As Object, e As EventArgs)
            For Each control As Control In _segmentRowsPanel.Controls
                control.Width = Math.Max(_segmentRowsPanel.ScaleX(720), _segmentRowsPanel.ClientSize.Width - _segmentRowsPanel.ScaleX(24))
            Next
        End Sub

        Private Shared Function FormatSegmentSeconds(value As Double) As String
            Return value.ToString("0.###", CultureInfo.InvariantCulture)
        End Function

        Private Shared Function TryParseSegmentSeconds(text As String, ByRef value As Double) As Boolean
            If Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, value) Then Return True
            Return Double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, value)
        End Function

        Private Sub ValidateAndShowSegmentConfig()
            Dim config = SelectedSegmentConfig()
            If config Is Nothing Then Return
            Dim errorText = SegmentEditingRules.ValidateSegmentRanges(config)
            If errorText.Length > 0 Then
                _lblSegmentStatus.Text = "<font color=#E07878>配置未通过：" & EscapeHtml(errorText) & "</font>"
                Return
            End If
            Dim fixedScale = SegmentEditingRules.SegmentFixedScale(config)
            Dim sizeText As String
            If fixedScale > 0 Then
                sizeText = "模型倍率优先 " & fixedScale & "x → " &
                    config.Segments(0).TargetWidth & "×" & config.Segments(0).TargetHeight &
                    "；FFmpeg / Anime4K 自动跟随"
            Else
                sizeText = "自定义统一输出 " & config.Segments(0).TargetWidth & "×" & config.Segments(0).TargetHeight
            End If
            Dim coverage = If(String.Equals(config.BoundaryMode, "seconds", StringComparison.OrdinalIgnoreCase),
                "已自动覆盖 0-" & FormatSegmentSeconds(config.DurationSeconds) & " 秒；内部断点吸附关键帧",
                "已连续覆盖 1-" & config.FrameCount & " 帧")
            _lblSegmentStatus.Text = "<font color=#96D2A0>" & EscapeHtml(coverage & "；" & sizeText & "；配置已自动保存。") & "</font>"
        End Sub

    End Class

End Namespace
