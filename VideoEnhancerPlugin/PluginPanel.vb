Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Text.Json
Imports System.Text.RegularExpressions
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports FFmpegFreeUI
Imports LakeUI

Namespace videoenhancer

    ''' <summary>"视频超分"插件页面：插件总开关 + 超分/补帧两行开关与模型选择 + 状态信息。</summary>
    Public Class PluginPanel
        Inherits UserControl

        ' 与官方 API 示例插件保持一致：#181818 背景、半透明灰控件、低饱和文字和单一蓝色强调。
        Private Shared ReadOnly UiCanvas As Color = Color.FromArgb(24, 24, 24)
        Private Shared ReadOnly UiSurface As Color = Color.FromArgb(40, 220, 220, 220)
        Private Shared ReadOnly UiSurfaceRaised As Color = Color.FromArgb(40, 220, 220, 220)
        Private Shared ReadOnly UiSurfaceHover As Color = Color.FromArgb(60, 220, 220, 220)
        Private Shared ReadOnly UiStroke As Color = Color.Transparent
        Private Shared ReadOnly UiStrokeSoft As Color = Color.Transparent
        Private Shared ReadOnly UiAccent As Color = Color.FromArgb(71, 156, 255)
        Private Shared ReadOnly UiAccentHover As Color = Color.FromArgb(110, 71, 156, 255)
        Private Shared ReadOnly UiAccentPressed As Color = Color.FromArgb(140, 71, 156, 255)
        Private Shared ReadOnly UiSuccess As Color = Color.FromArgb(63, 205, 135)
        Private Shared ReadOnly UiDanger As Color = Color.FromArgb(235, 93, 93)
        Private Shared ReadOnly UiText As Color = Color.FromArgb(220, 220, 220)
        Private Shared ReadOnly UiTextSecondary As Color = Color.FromArgb(176, 220, 220, 220)
        Private Shared ReadOnly UiTextMuted As Color = Color.FromArgb(120, 255, 255, 255)

        Private ReadOnly _config As PluginConfig
        Private ReadOnly _btnPickExe As New ModernButton()
        Private ReadOnly _switchMaster As New LakeUI.BooleanSwitch()
        Private ReadOnly _lblMaster As New HtmlColorLabel()
        Private ReadOnly _cmbModel As New ModernComboBox()
        Private ReadOnly _cmbInterp As New ModernComboBox()
        Private ReadOnly _lblExe As New HtmlColorLabel()
        Private ReadOnly _lblStatus As New HtmlColorLabel()
        Private ReadOnly _switchUpscale As New LakeUI.BooleanSwitch()
        Private ReadOnly _lblSwitch As New HtmlColorLabel()
        Private ReadOnly _switchInterp As New LakeUI.BooleanSwitch()
        Private ReadOnly _lblSwitchInterp As New HtmlColorLabel()
        Private ReadOnly _cmbBackend As New ModernComboBox()
        Private ReadOnly _lblBackend As New HtmlColorLabel()
        Private ReadOnly _cmbFactor As New ModernComboBox()
        Private ReadOnly _lblFactor As New HtmlColorLabel()
        Private _syncingMaster As Boolean = False
        Private _syncingBackend As Boolean = False
        Private _syncingFactor As Boolean = False
        Private _syncingSwitch As Boolean = False
        Private _syncingInterpSwitch As Boolean = False
        Private _modelsLoaded As Boolean = False
        Private _loadingModels As Boolean = False
        Private _interpModelsLoaded As Boolean = False
        Private _loadingInterpModels As Boolean = False
        Private _uiReady As Boolean = False
        ' ── 选项卡分栏：超分主界面 / 实时预览 / 高级功能 / 模型转换器 ──
        Private ReadOnly _tabs As New ModernTabControl()
        Private ReadOnly _pageUpscale As New Panel()
        Private ReadOnly _pagePreview As New Panel()
        Private ReadOnly _pageAdvanced As New Panel()
        Private ReadOnly _pageDownloader As New Panel()
        Private ReadOnly _pageConverter As New Panel()
        Private ReadOnly _pageModelInfo As New Panel()
        Private ReadOnly _pageTutorial As New Panel()
        ' ── 独立图片超分页（位于超分主界面内）──
        Private ReadOnly _btnImageFiles As New ModernButton()
        Private ReadOnly _btnImageFolder As New ModernButton()
        Private ReadOnly _btnImageOutput As New ModernButton()
        Private ReadOnly _btnImageStart As New ModernButton()
        Private ReadOnly _switchImageOriginal As New LakeUI.BooleanSwitch()
        Private ReadOnly _switchImagePng As New LakeUI.BooleanSwitch()
        Private ReadOnly _txtImageOutput As New ModernTextBox()
        Private ReadOnly _cmbImageSuffix As New ModernComboBox()
        Private ReadOnly _cmbImageFormat As New ModernComboBox()
        Private ReadOnly _lblImageInputs As New HtmlColorLabel()
        Private ReadOnly _lblImageOutput As New HtmlColorLabel()
        Private ReadOnly _lblImageProgress As New HtmlColorLabel()
        Private ReadOnly _imageProgress As New FluentProgressBar()
        Private ReadOnly _imageFiles As New List(Of String)()
        Private ReadOnly _imageFolders As New List(Of String)()
        Private _imageProcess As Process
        Private _imageRunning As Boolean
        Private _imageCompleteReceived As Boolean
        ' ── 实时预览页 ──
        Private ReadOnly _picPreview As New PictureBox()          ' 原生 .NET 图片控件（修复预览不切换）
        Private ReadOnly _cmbTask As New ModernComboBox()         ' 多任务选择
        Private ReadOnly _lblTask As New HtmlColorLabel()
        Private ReadOnly _cmbRate As New ModernComboBox()
        Private ReadOnly _lblPreviewTitle As New HtmlColorLabel()
        Private ReadOnly _lblPreviewStatus As New HtmlColorLabel()
        Private ReadOnly _lblPreviewNote As New HtmlColorLabel()
        Private ReadOnly _lblRate As New HtmlColorLabel()
        Private ReadOnly _lblAdvancedHint As New HtmlColorLabel()
        Private ReadOnly _btnQuad As New ModernButton()
        ' ── 模型转换器页 ──
        Private ReadOnly _lblConvertInput As New HtmlColorLabel()
        Private ReadOnly _lblConvertOutput As New HtmlColorLabel()
        Private ReadOnly _lblConvertStatus As New HtmlColorLabel()
        Private ReadOnly _btnPickPth As New ModernButton()
        Private ReadOnly _btnConvert As New ModernButton()
        Private _convertInputPath As String = ""
        Private _conversionRunning As Boolean = False
        ' ── 模型下载页 ──
        Private ReadOnly _downloadList As New FlowLayoutPanel()
        Private ReadOnly _btnRefreshDownloads As New ModernButton()
        Private ReadOnly _btnCleanArchives As New ModernButton()
        Private _downloadsLoaded As Boolean = False
        Private _downloadsLoading As Boolean = False
        Private _downloadOnline As Boolean = True
        Private _downloadBusy As Boolean = False
        Private _downloadScrollResetPending As Boolean = False
        Private NotInheritable Class DownloadModelEntry
            Public Property Name As String
            Public Property RelativePath As String
            Public Property Size As Long
        End Class
        Private NotInheritable Class DownloadExecutionResult
            Public Property ExitCode As Integer = -1
            Public Property Errors As String = ""
        End Class
        Private ReadOnly _statusClearTimer As New Timer() With {.Interval = 5000}
        ' 定期把「预览输出」右键菜单项挂到编码队列窗体（窗体实例重建后自动恢复）
        Private ReadOnly _queueMenuTimer As New Timer() With {.Interval = 2000}
        Private ReadOnly _taskIds As New List(Of String)()
        Private _pendingPreviewTaskId As String = ""
        Private _quadForm As QuadGridForm
        Private _engine As PreviewEngine
        Private _lastPreviewImage As Image

        ''' <summary>插件面板实例（编码队列右键「预览输出」等外部入口使用）。</summary>
        Friend Shared Current As PluginPanel

        Public Sub New(config As PluginConfig, Optional previewOnly As Boolean = False)
            _config = config
            Current = Me
            InitializeUi()
            If previewOnly Then
                _uiReady = True
                RefreshUi()
            Else
                AddHandler Load, AddressOf OnPanelLoad
            End If
        End Sub

        Public ReadOnly Property IsEnabled As Boolean
            Get
                Return _config.Enabled
            End Get
        End Property

        Private Sub OnPanelLoad(sender As Object, e As EventArgs)
            _uiReady = True
            RefreshUi()
            ' 状态提示定时清除（红色错误 5 秒后自动消失）
            AddHandler _statusClearTimer.Tick, AddressOf OnStatusClearTick
            AddHandler _tabs.SelectedIndexChanged, AddressOf OnTabChanged
            ' 实时预览引擎：与插件总开关无关，任何编码队列任务都可用
            If _engine Is Nothing Then
                _engine = New PreviewEngine(_config, Me)
                AddHandler _engine.FrameReady, AddressOf OnPreviewFrameReady
                AddHandler _engine.StatusChanged, AddressOf OnPreviewStatusChanged
                AddHandler _engine.TasksChanged, AddressOf OnPreviewTasksChanged
                _engine.PreviewVisible = (_tabs.SelectedIndex = 1)
                _engine.Start()
            End If
            ' 上次退出时已启用且 exe 存在 → 自动恢复启用状态
            If _config.Enabled AndAlso File.Exists(_config.ExePath) Then
                TryEnable(_config.ExePath, True)
            End If
            ' 「预览输出」右键菜单与插件总开关无关：启动即挂，并定期同步
            QueueHook.AttachQueueMenu()
            AddHandler _queueMenuTimer.Tick, AddressOf OnQueueMenuTick
            _queueMenuTimer.Start()
        End Sub

        Private Sub OnQueueMenuTick(sender As Object, e As EventArgs)
            QueueHook.AttachQueueMenu()
        End Sub

        ' ────────────────────────── 插件总开关 ──────────────────────────

        ''' <summary>尝试启用（供主开关与测试共用）。silent 时不在失败时弹窗。</summary>
        Public Function TryEnable(exePath As String, Optional silent As Boolean = False) As Boolean
            Try
                If Not File.Exists(exePath) Then
                    If Not silent Then
                        ShowStatus("videoenhancer.exe 不存在：" & exePath, True)
                    End If
                    Return False
                End If
                _config.ExePath = exePath
                _config.Enabled = True
                _config.Save()
                RefreshUi()
                UpdateHookState()
                RunEnvironmentCheck(exePath)
                RefreshModels()
                Return True
            Catch ex As Exception
                If Not silent Then
                    ShowStatus("启用失败：" & ex.Message, True)
                End If
                Return False
            End Try
        End Function

        Public Sub Disable()
            Try
                QueueHook.Uninstall()
                设置_v6.实例对象.替代进程文件名 = ""
            Catch
            End Try
            _config.Enabled = False
            _config.Save()
            RefreshUi()
            ShowStatus("已停用：编码队列恢复为直接执行 ffmpeg", False)
        End Sub

        ''' <summary>"插件总开关"切换：开 → 未指定路径时弹出选择；关 → 停止对参数面板的 hook。</summary>
        Private Sub OnMasterSwitchChanged(sender As Object, e As EventArgs)
            If _syncingMaster Then
                Return
            End If
            If _switchMaster.Checked Then
                Dim exePath = _config.ExePath
                If Not File.Exists(exePath) Then
                    Using dialog As New OpenFileDialog With {
                        .Title = "请选择 videoenhancer.exe",
                        .Filter = "videoenhancer.exe|videoenhancer.exe|可执行文件 (*.exe)|*.exe",
                        .CheckFileExists = True
                    }
                        If dialog.ShowDialog(Me) <> DialogResult.OK Then
                            _syncingMaster = True
                            _switchMaster.Checked = False
                            _syncingMaster = False
                            Return
                        End If
                        exePath = dialog.FileName
                    End Using
                End If
                If Not TryEnable(exePath) Then
                    _syncingMaster = True
                    _switchMaster.Checked = False
                    _syncingMaster = False
                End If
            Else
                Disable()
            End If
        End Sub

        ' ────────────────────────── 超分 / 补帧开关 ──────────────────────────

        ''' <summary>"超分开关"切换：开 → 需主开关开启；随后按状态挂载/卸载 hook。</summary>
        Private Sub OnUpscaleSwitchChanged(sender As Object, e As EventArgs)
            If _syncingSwitch Then
                Return
            End If
            If _switchUpscale.Checked AndAlso Not _config.Enabled Then
                _syncingSwitch = True
                _switchUpscale.Checked = False
                _syncingSwitch = False
                ShowStatus("请先开启「插件总开关」", True)
                Return
            End If
            ' 与补帧互斥：不能同时开启
            If _switchUpscale.Checked AndAlso _config.InterpEnabled Then
                _syncingSwitch = True
                _switchUpscale.Checked = False
                _syncingSwitch = False
                ShowStatus("超分与补帧不能同时开启，请先关闭「补帧开关」", True)
                Return
            End If
            _config.UpscaleEnabled = _switchUpscale.Checked
            ' 开启超分：CUDA 模式下放大模型列表切换为 models 下的 .pth 模型（空列表时自动回退 ncnn）
            If _switchUpscale.Checked AndAlso (_config.Backend = "cuda" OrElse _config.Backend = "tensorrt" OrElse _config.Backend = "onnx" OrElse _config.Backend = "flashvsr") Then
                RefreshUpscaleModels()
            End If
            _config.Save()
            UpdateModeStateLabels()
            UpdateHookState()
        End Sub

        ''' <summary>"补帧开关"切换：开 → 需主开关开启；随后按状态挂载/卸载 hook。</summary>
        Private Sub OnInterpSwitchChanged(sender As Object, e As EventArgs)
            If _syncingInterpSwitch Then
                Return
            End If
            If _switchInterp.Checked AndAlso Not _config.Enabled Then
                _syncingInterpSwitch = True
                _switchInterp.Checked = False
                _syncingInterpSwitch = False
                ShowStatus("请先开启「插件总开关」", True)
                Return
            End If
            ' 与超分互斥：不能同时开启
            If _switchInterp.Checked AndAlso _config.UpscaleEnabled Then
                _syncingInterpSwitch = True
                _switchInterp.Checked = False
                _syncingInterpSwitch = False
                ShowStatus("超分与补帧不能同时开启，请先关闭「超分开关」", True)
                Return
            End If
            _config.InterpEnabled = _switchInterp.Checked
            If _switchInterp.Checked AndAlso _config.Backend = "cuda" Then
                RefreshInterpModels()
            End If
            _config.Save()
            UpdateModeStateLabels()
            UpdateHookState()
        End Sub

        ''' <summary>按主开关 + 超分/补帧开关状态统一挂载/卸载"加入编码队列"hook。</summary>
        Private Sub UpdateHookState()
            Dim wantHook As Boolean = _config.Enabled AndAlso File.Exists(_config.ExePath) AndAlso
                (_config.UpscaleEnabled OrElse _config.InterpEnabled)
            If wantHook Then
                If Not QueueHook.Install() Then
                    ShowStatus("未能挂载""加入编码队列""按钮，请确认 3FUI 版本兼容", True)
                    Return
                End If
                设置_v6.实例对象.替代进程文件名 = _config.ExePath
                ShowStatus("已启用：编码队列将通过 videoenhancer.exe 中转执行", False)
            Else
                Try
                    QueueHook.Uninstall()
                    设置_v6.实例对象.替代进程文件名 = ""
                Catch
                End Try
                ShowStatus("已停用：编码队列恢复为直接执行 ffmpeg", False)
            End If
        End Sub

        Private Sub OnPickExeClick(sender As Object, e As EventArgs)
            Using dialog As New OpenFileDialog With {
                .Title = "请选择 videoenhancer.exe",
                .Filter = "videoenhancer.exe|videoenhancer.exe|可执行文件 (*.exe)|*.exe",
                .CheckFileExists = True,
                .InitialDirectory = If(Path.GetDirectoryName(_config.ExePath), Environment.CurrentDirectory)
            }
                If dialog.ShowDialog(Me) <> DialogResult.OK Then
                    Return
                End If
                _config.ExePath = dialog.FileName
                _config.Save()
                RefreshUi()
                RefreshModels()
            End Using
        End Sub

        ' ────────────────────────── 模型下拉框 ──────────────────────────

        Private Sub OnModelDropDownOpened(sender As Object, e As EventArgs)
            If _modelsLoaded Then
                Return
            End If
            StartModelLoad()
        End Sub

        ''' <summary>下拉框点击兜底：空列表时 ModernComboBox 不触发 DropDownOpened，用 Click 补一次加载。</summary>
        Private Sub OnModelComboClicked(sender As Object, e As EventArgs)
            If _modelsLoaded OrElse _cmbModel.Items.Count > 0 Then
                Return
            End If
            StartModelLoad()
        End Sub

        Private Sub OnInterpDropDownOpened(sender As Object, e As EventArgs)
            If _interpModelsLoaded Then
                Return
            End If
            StartInterpModelLoad()
        End Sub

        Private Sub OnInterpComboClicked(sender As Object, e As EventArgs)
            If _interpModelsLoaded OrElse _cmbInterp.Items.Count > 0 Then
                Return
            End If
            StartInterpModelLoad()
        End Sub

        ''' <summary>重新读取模型列表（启用 / 更换 exe / 下拉重试共用）。</summary>
        Public Sub RefreshModels()
            _modelsLoaded = False
            _interpModelsLoaded = False
            StartModelLoad()
            StartInterpModelLoad()
        End Sub

        Private Sub StartModelLoad()
            If _loadingModels Then
                Return
            End If
            If Not File.Exists(_config.ExePath) Then
                ShowStatus("请先启用并指定 videoenhancer.exe", True)
                Return
            End If
            _loadingModels = True
            _cmbModel.WaterText = "正在读取模型列表…"
            Dim exePath = _config.ExePath
            Dim backend = If(String.IsNullOrWhiteSpace(_config.Backend), "ncnn", _config.Backend)
            Task.Run(Sub()
                         Dim models = RunListModels(exePath, "--search-models", "-backend", backend)
                         Try
                             If Me.IsHandleCreated Then
                                 Me.BeginInvoke(New Action(Sub()
                                                               ApplyModelList(models)
                                                               _loadingModels = False
                                                           End Sub))
                             Else
                                 ApplyModelList(models)
                                 _loadingModels = False
                             End If
                         Catch
                             _loadingModels = False
                         End Try
                     End Sub)
        End Sub

        Private Sub StartInterpModelLoad()
            If _loadingInterpModels Then
                Return
            End If
            If Not File.Exists(_config.ExePath) Then
                Return
            End If
            _loadingInterpModels = True
            _cmbInterp.WaterText = "正在读取补帧模型…"
            Dim exePath = _config.ExePath
            Dim backend = If(String.IsNullOrWhiteSpace(_config.Backend), "ncnn", _config.Backend)
            Task.Run(Sub()
                         Dim models = RunListModels(exePath, "--list-interp-models", "-backend", backend)
                         Try
                             If Me.IsHandleCreated Then
                                 Me.BeginInvoke(New Action(Sub()
                                                               _loadingInterpModels = False
                                                               ApplyInterpModelList(models)
                                                           End Sub))
                             Else
                                 _loadingInterpModels = False
                                 ApplyInterpModelList(models)
                             End If
                         Catch
                             _loadingInterpModels = False
                         End Try
                     End Sub)
        End Sub

        Private Sub ApplyModelList(models As List(Of String))
            ' CLI 版本不一致或旧进程缓存时，从候选 models 目录补扫 TensorRT PTH/Engine。
            If models.Count = 0 AndAlso String.Equals(_config.Backend, "tensorrt", StringComparison.OrdinalIgnoreCase) Then
                Try
                    Dim dirs = New List(Of String) From {
                        Path.Combine(Path.GetDirectoryName(_config.ExePath), "models"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models"),
                        "C:\PortableSoft\VideoEnhancer-CLI\models"
                    }
                    For Each modelDir In dirs.Distinct(StringComparer.OrdinalIgnoreCase)
                        If Not Directory.Exists(modelDir) Then Continue For
                        For Each pattern In New String() {"*.engine", "*.pth", "*.pt", "*.pkl"}
                            For Each p In Directory.GetFiles(modelDir, pattern, SearchOption.AllDirectories)
                                Dim relative = Path.GetRelativePath(modelDir, p).Replace(Convert.ToChar(92), "/"c)
                                If relative.StartsWith("RIFE/", StringComparison.OrdinalIgnoreCase) Then Continue For
                                If relative.StartsWith("TensorRT-Cache/", StringComparison.OrdinalIgnoreCase) Then Continue For
                                Dim n = Path.ChangeExtension(relative, Nothing)
                                If Not String.IsNullOrWhiteSpace(n) AndAlso Not models.Contains(n, StringComparer.OrdinalIgnoreCase) Then models.Add(n)
                            Next
                        Next
                    Next
                Catch
                End Try
            End If
            _cmbModel.Items.Clear()
            If models.Count > 0 Then
                _cmbModel.Items.AddRange(models)
                _modelsLoaded = True
                Dim selected As String = Nothing
                If Not String.IsNullOrEmpty(_config.Model) Then
                    selected = models.FirstOrDefault(Function(m) String.Equals(m, _config.Model, StringComparison.OrdinalIgnoreCase))
                End If
                If selected IsNot Nothing Then
                    _cmbModel.SelectedIndex = Math.Max(0, models.IndexOf(selected))
                Else
                    _cmbModel.SelectedIndex = 0
                End If
                Dim modeText = If(_config.Backend = "tensorrt",
                    "（TensorRT，PTH 首次使用自动构建 Engine）",
                    If(_config.Backend = "onnx",
                    "（ONNX Runtime，models 下的 .onnx 文件）",
                    If(_config.Backend = "flashvsr",
                    "（FlashVSR，连续视频帧专用模型目录）",
                    If(_config.Backend = "cuda",
                    "（CUDA，models 下的 .pth/.pt/.pkl 文件）",
                    "（models 目录，.param/.bin 文件夹）"))))
                ShowStatus($"已从 videoenhancer.exe 读取 {models.Count} 个可用模型 " & modeText, False)
            Else
                If (_config.Backend = "cuda" OrElse _config.Backend = "tensorrt" OrElse _config.Backend = "onnx" OrElse _config.Backend = "flashvsr") AndAlso _config.UpscaleEnabled Then
                    Dim missingExt = If(_config.Backend = "flashvsr", "FlashVSR 完整模型目录", If(_config.Backend = "tensorrt", "PTH 或 .engine", If(_config.Backend = "onnx", ".onnx", ".pth")))
                    _cmbModel.WaterText = "未找到 " & missingExt & " 放大模型"
                    ShowStatus("未找到 " & missingExt & " 放大模型，请确认 models 目录", True)
                    ' 保留用户选择的 TensorRT，不因一次扫描失败自动改回 NCNN。
                    _loadingModels = False
                Else
                    _cmbModel.WaterText = "未找到可用模型"
                    ShowStatus("未在 models 目录找到含 .param/.bin 的模型", True)
                End If
            End If
        End Sub

        Private Sub ApplyInterpModelList(models As List(Of String))
            _cmbInterp.Items.Clear()
            If models.Count > 0 Then
                _cmbInterp.Items.AddRange(models)
                _interpModelsLoaded = True
                Dim selected As String = Nothing
                If Not String.IsNullOrEmpty(_config.InterpModel) Then
                    selected = models.FirstOrDefault(Function(m) String.Equals(m, _config.InterpModel, StringComparison.OrdinalIgnoreCase))
                End If
                If selected IsNot Nothing Then
                    _cmbInterp.SelectedIndex = Math.Max(0, models.IndexOf(selected))
                Else
                    _cmbInterp.SelectedIndex = 0
                End If
                Dim modeText = If(_config.Backend = "cuda",
                    "（CUDA，" & Convert.ToChar(92) & "RIFE 下的 .pth 文件）",
                    "（models" & Convert.ToChar(92) & "RIFE）")
                ShowStatus($"已读取 {models.Count} 个补帧模型 " & modeText, False)
            Else
                If _config.Backend = "cuda" Then
                    If _config.InterpEnabled Then
                        _cmbInterp.WaterText = "未找到 .pth 补帧模型"
                        ShowStatus("未在 models" & Convert.ToChar(92) & "RIFE 找到 .pth 补帧模型，已回退到 NCNN 推理", True)
                        _config.Backend = "ncnn"
                        _config.Save()
                        _syncingBackend = True
                        SyncBackendCombo()
                        _syncingBackend = False
                        StartInterpModelLoad()
                    Else
                        _cmbInterp.WaterText = "未找到 .pth 补帧模型"
                        ShowStatus("CUDA 补帧需要 models" & Convert.ToChar(92) & "RIFE 下的 .pth 模型（当前为空，仅超分时忽略）", False)
                    End If
                Else
                    _cmbInterp.WaterText = "未找到补帧模型"
                    ShowStatus("未在 models" & Convert.ToChar(92) & "RIFE 目录找到含 .param/.bin 的补帧模型", True)
                End If
            End If
        End Sub

        Private Sub OnModelSelected(sender As Object, e As EventArgs)
            Dim model = _cmbModel.SelectedItem
            If String.IsNullOrWhiteSpace(model) Then
                Return
            End If
            _config.Model = model.Trim()
            _config.Save()
        End Sub

        Private Sub OnInterpModelSelected(sender As Object, e As EventArgs)
            Dim model = _cmbInterp.SelectedItem
            If String.IsNullOrWhiteSpace(model) Then
                Return
            End If
            _config.InterpModel = model.Trim()
            _config.Save()
        End Sub

        ''' <summary>"选择推理方式"：ncnn（Vulkan，默认）或 cuda（PyTorch，超分/补帧均需 .pth 模型）。</summary>
        Private Sub OnBackendSelected(sender As Object, e As EventArgs)
            If _syncingBackend Then
                Return
            End If
            Dim backend = BackendValue(_cmbBackend.SelectedItem)
            If backend = _config.Backend Then
                Return
            End If
            If (backend = "onnx" OrElse backend = "flashvsr") AndAlso _config.InterpEnabled Then
                ShowStatus(If(backend = "flashvsr", "FlashVSR 不能与补帧同时运行；请先关闭补帧开关。", "ONNX Runtime 当前只用于超分；请先关闭补帧开关。"), True)
                _syncingBackend = True
                SyncBackendCombo()
                _syncingBackend = False
                Return
            End If
            _config.Backend = backend
            _config.Save()
            ' 切换后端后重新读取两个模型列表（CUDA 需要 .pth 模型；活动模式无 .pth 时由 Apply*List 自动回退）
            RefreshUpscaleModels()
            RefreshInterpModels()
            Dim modeText = If(backend = "tensorrt",
                "TensorRT（NVIDIA）：可选 PTH 或预制 Engine；缓存缺失时按 GPU、TensorRT 版本和输入尺寸自动构建",
                If(backend = "onnx",
                "ONNX Runtime：超分用 models 下的 .onnx 模型，自动优先 CUDA",
                If(backend = "flashvsr",
                "FlashVSR（NVIDIA）：连续视频帧专用扩散超分，不用于图片或补帧",
                If(backend = "cuda",
                "CUDA（PyTorch）：超分用 models 下的 .pth 模型，补帧用 models" & Convert.ToChar(92) & "RIFE 下的 .pth 模型",
                "NCNN（Vulkan）"))))
            ShowStatus("推理方式：" & modeText, False)
        End Sub

        ''' <summary>"补帧倍率"选择：保存倍率并提示去"视频参数-画面帧"设置帧率。</summary>
        Private Sub OnFactorSelected(sender As Object, e As EventArgs)
            If _syncingFactor Then
                Return
            End If
            Dim factor = FactorValue(_cmbFactor.SelectedItem)
            If factor <= 1 Then
                Return
            End If
            _config.InterpFactor = factor
            _config.Save()
            Try
                MessageBox.Show(Me,
                    "请前往「视频参数-画面帧」页面指定帧率为原视频的 " & factor.ToString("0") & " 倍。",
                    "补帧倍率", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Catch
            End Try
        End Sub

        Private Shared Function BackendValue(item As Object) As String
            Dim text = If(item Is Nothing, "", item.ToString())
            If text.Contains("FlashVSR") Then
                Return "flashvsr"
            End If
            If text.Contains("TensorRT") Then
                Return "tensorrt"
            End If
            If text.Contains("ONNX") Then
                Return "onnx"
            End If
            If text.Contains("CUDA") Then
                Return "cuda"
            End If
            Return "ncnn"
        End Function

        Private Shared Function FactorValue(item As Object) As Double
            Dim text = If(item Is Nothing, "", item.ToString())
            Dim digits = New String(text.TakeWhile(Function(c) Char.IsDigit(c)).ToArray())
            Dim v As Double = 0
            If Double.TryParse(digits, v) Then
                Return v
            End If
            Return 0
        End Function

        ''' <summary>按当前推理后端重新读取补帧模型列表（cuda → .pth，ncnn → 文件夹）。</summary>
        Private Sub RefreshInterpModels()
            _interpModelsLoaded = False
            StartInterpModelLoad()
        End Sub

        ''' <summary>按当前推理后端重新读取放大模型列表（cuda → models 下 .pth，ncnn → 文件夹）。</summary>
        Private Sub RefreshUpscaleModels()
            _modelsLoaded = False
            StartModelLoad()
        End Sub

        Private Shared Function RunListModels(exePath As String, ParamArray extraArgs As String()) As List(Of String)
            Dim models As New List(Of String)
            Try
                Dim psi As New ProcessStartInfo With {
                    .FileName = exePath,
                    .UseShellExecute = False,
                    .RedirectStandardOutput = True,
                    .RedirectStandardError = True,
                    .CreateNoWindow = True,
                    .StandardOutputEncoding = Encoding.UTF8
                }
                psi.ArgumentList.Add("--json")
                For Each a In extraArgs
                    If Not String.IsNullOrWhiteSpace(a) Then
                        psi.ArgumentList.Add(a)
                    End If
                Next
                Using p = Process.Start(psi)
                    If p Is Nothing Then
                        Return models
                    End If
                    Dim stdout = p.StandardOutput.ReadToEnd()
                    p.WaitForExit(60000)
                    Dim firstLine = stdout.Split(Convert.ToChar(10)).FirstOrDefault(Function(l) l.Trim().StartsWith("["c))
                    If Not String.IsNullOrWhiteSpace(firstLine) Then
                        Try
                            Dim parsed = JsonSerializer.Deserialize(Of List(Of String))(firstLine.Trim())
                            If parsed IsNot Nothing Then
                                For Each modelName In parsed
                                    If Not String.IsNullOrWhiteSpace(modelName) Then
                                        models.Add(modelName.Trim())
                                    End If
                                Next
                            End If
                        Catch
                            models.Clear()
                        End Try
                    End If
                    If models.Count = 0 Then
                        For Each line As String In stdout.Split(Convert.ToChar(10))
                            Dim trimmed = line.Trim()
                            If trimmed = "" OrElse trimmed.StartsWith("("c) OrElse trimmed.Contains("：") Then
                                Continue For
                            End If
                            Dim modelName = trimmed
                            Dim paren = trimmed.IndexOf("  (", StringComparison.Ordinal)
                            If paren > 0 Then
                                modelName = trimmed.Substring(0, paren).Trim()
                            End If
                            If modelName.Length > 0 AndAlso Not modelName.Contains(" "c) Then
                                models.Add(modelName)
                            End If
                        Next
                    End If
                End Using
            Catch
            End Try
            Return models.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        End Function

        ' ────────────────────────── 环境检查 ──────────────────────────

        Private Sub RunEnvironmentCheck(exePath As String)
            Task.Run(Sub()
                         Try
                             Dim psi As New ProcessStartInfo With {
                                 .FileName = exePath,
                                 .UseShellExecute = False,
                                 .RedirectStandardOutput = True,
                                 .RedirectStandardError = True,
                                 .CreateNoWindow = True,
                                 .StandardOutputEncoding = Encoding.UTF8,
                                 .StandardErrorEncoding = Encoding.UTF8
                             }
                             psi.ArgumentList.Add("--check")
                             Using p = Process.Start(psi)
                                 If p Is Nothing Then
                                     Return
                                 End If
                                 Dim stdout = p.StandardOutput.ReadToEnd()
                                 p.WaitForExit(120000)
                                 Dim summary = stdout.Split(Convert.ToChar(10)).FirstOrDefault(Function(l) l.Contains("[环境检查]"))
                                 Dim ok = p.ExitCode = 0
                                 Dim text = If(ok, "环境检测通过：" & If(summary, "ffmpeg / python / 模型库就绪"),
                                                  "环境检测未通过：" & If(summary, "请查看 videoenhancer.exe --check 输出"))
                                 Try
                                     Me.BeginInvoke(New Action(Sub() ShowStatus(text, Not ok)))
                                 Catch
                                 End Try
                             End Using
                         Catch
                         End Try
                     End Sub)
        End Sub

        ' ────────────────────────── UI ──────────────────────────

        Private Shared Function CreateTextLabel(text As String, fontSize As Single, style As FontStyle,
                                                color As Color) As Label
            Return New Label() With {
                .Text = text, .ForeColor = color, .BackColor = Color.Transparent,
                .Font = New Font("Microsoft YaHei UI", fontSize, style),
                .TextAlign = ContentAlignment.MiddleLeft, .AutoEllipsis = True
            }
        End Function

        Private Shared Function CreateHtmlTextLabel(text As String, fontSize As Single, style As FontStyle,
                                                    color As Color) As HtmlColorLabel
            Return New HtmlColorLabel() With {
                .Text = text, .ForeColor = color, .BackColor = Color.Transparent,
                .BackColor1 = Color.Transparent, .BorderSize = 0,
                .Font = New Font("Microsoft YaHei UI", fontSize, style),
                .TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft, .AutoSize = False
            }
        End Function

        Private Shared Function CreateOfficialSectionHeading(title As String, description As String) As HtmlColorLabel
            Dim headingText = $"<span style=""font-size:13; color:Silver"">{EscapeHtml(title)}</span>"
            If Not String.IsNullOrWhiteSpace(description) Then
                headingText &= "   " & EscapeHtml(description)
            End If
            Return New HtmlColorLabel With {
                .Dock = DockStyle.Fill,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty,
                .BackColor1 = Color.Transparent,
                .BorderSize = 0,
                .ForeColor = UiTextMuted,
                .Text = headingText,
                .TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft,
                .AutoSize = False
            }
        End Function

        Private Shared Function CreateOfficialField(caption As String, editor As Control,
                                                     Optional rightMargin As Integer = 12) As Control
            Dim layout As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 2,
                .BackColor = Color.Transparent,
                .Margin = New Padding(0, 0, rightMargin, 0),
                .Padding = Padding.Empty
            }
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 28))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            Dim label = CreateTextLabel(caption, 9.0F, FontStyle.Regular, UiTextMuted)
            label.Dock = DockStyle.Fill
            label.Margin = New Padding(2, 0, 2, 0)
            label.TextAlign = ContentAlignment.BottomLeft
            editor.Dock = DockStyle.Fill
            editor.Margin = New Padding(0, 5, 0, 5)
            layout.Controls.Add(label, 0, 0)
            layout.Controls.Add(editor, 0, 1)
            Return layout
        End Function

        Private Shared Function CreateOfficialCaption(text As String, Optional color As Color = Nothing) As Label
            Dim actualColor = If(color = Nothing, UiTextMuted, color)
            Dim label = CreateTextLabel(text, 9.0F, FontStyle.Regular, actualColor)
            label.Dock = DockStyle.Fill
            label.Margin = Padding.Empty
            Return label
        End Function

        Private Shared Function CreatePageHeader(symbol As String, title As String, subtitle As String) As FluentCardPanel
            Dim header As New FluentCardPanel() With {
                .Dock = DockStyle.Top, .Height = 82,
                .FillColor = UiSurface, .StrokeColor = UiStrokeSoft, .CornerRadius = 12
            }
            Dim iconBack As New FluentCardPanel() With {
                .Location = New Point(18, 17), .Size = New Size(48, 48),
                .FillColor = Color.FromArgb(34, UiAccent), .StrokeColor = Color.FromArgb(88, UiAccent),
                .CornerRadius = 12
            }
            Dim icon As Label = CreateTextLabel(symbol, 15.0F, FontStyle.Bold, UiAccent)
            icon.Dock = DockStyle.Fill
            icon.TextAlign = ContentAlignment.MiddleCenter
            iconBack.Controls.Add(icon)
            Dim titleLabel As HtmlColorLabel = CreateHtmlTextLabel(title, 13.0F, FontStyle.Bold, UiText)
            titleLabel.Location = New Point(82, 14)
            titleLabel.Size = New Size(660, 30)
            titleLabel.Anchor = AnchorStyles.Left Or AnchorStyles.Top Or AnchorStyles.Right
            Dim subtitleLabel As Label = CreateTextLabel(subtitle, 9.0F, FontStyle.Regular, UiTextSecondary)
            subtitleLabel.Location = New Point(82, 43)
            subtitleLabel.Size = New Size(900, 24)
            subtitleLabel.Anchor = AnchorStyles.Left Or AnchorStyles.Top Or AnchorStyles.Right
            header.Controls.AddRange(New Control() {iconBack, titleLabel, subtitleLabel})
            AddHandler header.Resize,
                Sub(sender, e)
                    titleLabel.Width = Math.Max(220, header.ClientSize.Width - titleLabel.Left - 20)
                    subtitleLabel.Width = Math.Max(220, header.ClientSize.Width - subtitleLabel.Left - 20)
                End Sub
            Return header
        End Function

        Private Shared Sub ConfigurePrimaryButton(button As ModernButton)
            button.Font = New Font("Microsoft YaHei UI", 10.0F, FontStyle.Regular)
            button.ForeColor = UiText
            button.BorderRadius = 10
            button.BorderSize = 0
            button.BorderColor = Color.Transparent
            button.HoverBorderColor = Color.Transparent
            button.PressedBorderColor = Color.Transparent
            button.BackColor1 = Color.FromArgb(80, UiAccent)
            button.BackColor2 = Color.FromArgb(80, UiAccent)
            button.HoverBackColor1 = UiAccentHover
            button.HoverBackColor2 = UiAccentHover
            button.PressedBackColor1 = UiAccentPressed
            button.PressedBackColor2 = UiAccentPressed
        End Sub

        Private Shared Sub ConfigureSecondaryButton(button As ModernButton)
            button.Font = New Font("Microsoft YaHei UI", 10.0F, FontStyle.Regular)
            button.ForeColor = UiText
            button.BorderRadius = 10
            button.BorderSize = 0
            button.BorderColor = Color.Transparent
            button.HoverBorderColor = Color.Transparent
            button.PressedBorderColor = Color.Transparent
            button.BackColor1 = UiSurfaceRaised
            button.BackColor2 = UiSurfaceRaised
            button.HoverBackColor1 = UiSurfaceHover
            button.HoverBackColor2 = UiSurfaceHover
            button.PressedBackColor1 = Color.FromArgb(80, 220, 220, 220)
            button.PressedBackColor2 = Color.FromArgb(80, 220, 220, 220)
        End Sub

        Private Shared Sub ConfigureCombo(combo As ModernComboBox)
            combo.Font = New Font("Microsoft YaHei UI", 10.0F)
            combo.ForeColor = UiText
            combo.WaterTextForeColor = UiTextMuted
            combo.Padding = New Padding(10, 0, 10, 0)
            combo.BackColor1 = UiSurfaceRaised
            combo.BackColor2 = UiSurfaceRaised
            combo.HoverBackColor1 = UiSurfaceHover
            combo.HoverBackColor2 = UiSurfaceHover
            combo.PressedBackColor1 = Color.FromArgb(80, 220, 220, 220)
            combo.PressedBackColor2 = Color.FromArgb(80, 220, 220, 220)
            combo.BorderColor = Color.Transparent
            combo.BorderColorFocus = Color.FromArgb(80, 220, 220, 220)
            combo.HoverBorderColor = Color.Transparent
            combo.ArrowColor = UiTextMuted
            combo.HoverArrowColor = UiText
            combo.BorderRadius = 10
            combo.BorderSize = 0
            combo.Editable = True
            combo.MaxDropDownItems = 12
            combo.DropDownBackColor = Color.FromArgb(48, 48, 48)
            combo.DropDownBorderColor = Color.Transparent
            combo.DropDownHoverColor = UiSurfaceHover
            combo.DropDownSelectedColor = Color.FromArgb(80, UiAccent)
            combo.DropDownSelectedForeColor = UiText
            combo.DropDownScrollBarColor = UiAccent
            combo.DropDownScrollBarTrackColor = Color.Transparent
        End Sub

        Private Sub InitializeUi()
            BackColor = Color.Transparent
            Dock = DockStyle.Fill
            MinimumSize = New Size(900, 680)
            Font = New Font("Microsoft YaHei UI", 10.0F)

            ' 官方 API 插件约定：宿主通过名为 ModernPanel1 的 Fill 面板接入个性化背景。
            Dim ModernPanel1 As New ModernPanel With {
                .Name = "ModernPanel1",
                .Dock = DockStyle.Fill,
                .Margin = Padding.Empty,
                .Padding = New Padding(24, 20, 24, 18),
                .BackColor = Color.Transparent,
                .BackColor1 = UiCanvas,
                .BorderSize = 0,
                .BorderRadius = 0
            }
            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 2,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty,
                .BackColor = Color.Transparent
            }
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))

            BuildTabs()
            root.Controls.Add(_tabs, 0, 0)

            Dim sectionStatus As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 1,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = New Padding(0, 4, 0, 0)
            }
            sectionStatus.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
            sectionStatus.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 210.0F))
            sectionStatus.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            _lblStatus.AutoSize = False
            _lblStatus.Dock = DockStyle.Fill
            _lblStatus.Margin = Padding.Empty
            _lblStatus.ForeColor = UiTextMuted
            _lblStatus.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _lblStatus.Text = "<font color=#888888>就绪</font>"
            sectionStatus.Controls.Add(_lblStatus, 0, 0)
            _btnCleanArchives.Text = "清理临时文件"
            _btnCleanArchives.Dock = DockStyle.Fill
            _btnCleanArchives.Margin = New Padding(12, 4, 0, 4)
            ConfigureSecondaryButton(_btnCleanArchives)
            _btnCleanArchives.ForeColor = Color.White
            _btnCleanArchives.BackColor1 = Color.FromArgb(150, 190, 48, 48)
            _btnCleanArchives.BackColor2 = Color.FromArgb(150, 190, 48, 48)
            _btnCleanArchives.HoverBackColor1 = Color.FromArgb(190, 220, 64, 64)
            _btnCleanArchives.HoverBackColor2 = Color.FromArgb(190, 220, 64, 64)
            _btnCleanArchives.PressedBackColor1 = Color.FromArgb(220, 160, 36, 36)
            _btnCleanArchives.PressedBackColor2 = Color.FromArgb(220, 160, 36, 36)
            _btnCleanArchives.Visible = False
            AddHandler _btnCleanArchives.Click, AddressOf OnCleanDownloadArchives
            sectionStatus.Controls.Add(_btnCleanArchives, 1, 0)
            root.Controls.Add(sectionStatus, 0, 1)
            ModernPanel1.Controls.Add(root)
            Controls.Add(ModernPanel1)
        End Sub

        ' ────────────────────────── 选项卡分栏 ──────────────────────────

        Private Sub BuildTabs()
            _tabs.Dock = DockStyle.Fill
            _tabs.ContentBackColor = Color.Transparent
            _tabs.BackColor = Color.Transparent
            _tabs.TabStripBackColor = Color.Transparent
            _tabs.TabStripOverlayColor = Color.Transparent
            _tabs.TabStripHeight = 44
            _tabs.TabStripPadding = New Padding(0, 2, 0, 3)
            _tabs.TabItemTextPadding = 7
            _tabs.TabItemSpacing = 4
            _tabs.TabItemBorderRadius = 8
            _tabs.TabItemForeColor = UiTextMuted
            _tabs.TabItemSelectedForeColor = UiText
            _tabs.TabItemSelectedBackColor = UiSurface
            _tabs.TabItemHoverBackColor = UiSurfaceHover
            _tabs.IndicatorColor = UiAccent
            _tabs.IndicatorHeight = 2
            _tabs.IndicatorBorderRadius = 1
            _tabs.IndicatorPadding = 12
            _tabs.SeparatorWidth = 0
            _tabs.ContentBorderWidth = 0
            _tabs.TabAlignment = ModernTabControl.TabAlignmentEnum.Left
            _tabs.Font = New Font("Microsoft YaHei UI", 10.0F)
            _tabs.AnimationDuration = 0
            _tabs.AnimationFPS = 30

            BuildOfficialUpscalePage()
            BuildOfficialPreviewPage()
            BuildOfficialAdvancedPage()
            BuildOfficialModelDownloadPage()
            BuildOfficialConverterPage()
            BuildMarkdownPage(_pageModelInfo,
                "# 模型选择指南" & Environment.NewLine & Environment.NewLine &
                "## 放大模型" & Environment.NewLine &
                "- **NCNN / Param-Bin**：兼容性最好，适合 Vulkan 显卡和日常使用。" & Environment.NewLine &
                "- **PTH / CUDA**：适合 NVIDIA 显卡，模型选择丰富。" & Environment.NewLine &
                "- **TensorRT Engine**：吞吐更高，但需要与当前显卡和 CUDA 环境匹配。" & Environment.NewLine &
                "- **ONNX Runtime**：便于跨后端部署，性能取决于执行提供程序。" & Environment.NewLine & Environment.NewLine &
                "## 补帧模型" & Environment.NewLine &
                "- RIFE 模型用于生成中间帧；2 倍适合大多数素材，4 倍以上建议先短片测试。" & Environment.NewLine & Environment.NewLine &
                "## 建议" & Environment.NewLine &
                "优先从较短片段开始，确认画质、显存占用和速度后再处理完整视频。")
            BuildMarkdownPage(_pageTutorial,
                "# 快速上手" & Environment.NewLine & Environment.NewLine &
                "## 1. 连接处理程序" & Environment.NewLine &
                "在 **超分主界面** 指定 `videoenhancer.exe`，然后开启插件。" & Environment.NewLine & Environment.NewLine &
                "## 2. 选择一种处理模式" & Environment.NewLine &
                "- 开启 **视频超分**，选择推理后端和放大模型。" & Environment.NewLine &
                "- 或开启 **运动补帧**，选择 RIFE 模型与倍率。" & Environment.NewLine &
                "- 两种模式互斥，避免重复处理同一任务。" & Environment.NewLine & Environment.NewLine &
                "## 3. 加入编码队列" & Environment.NewLine &
                "回到 3FUI 准备文件并加入队列，插件会自动通过 CLI 中转。" & Environment.NewLine & Environment.NewLine &
                "## 4. 查看输出" & Environment.NewLine &
                "在 **实时预览** 查看处理中或已完成的帧；需要多视频比较时打开 **对比工作室**。")

            Dim tabMain As New ModernTabControl.ModernTab("超分工作台") With {.BoundControl = _pageUpscale}
            Dim tabPreview As New ModernTabControl.ModernTab("实时预览") With {.BoundControl = _pagePreview}
            Dim tabAdvanced As New ModernTabControl.ModernTab("对比工具") With {.BoundControl = _pageAdvanced}
            Dim tabDownloader As New ModernTabControl.ModernTab("模型下载") With {.BoundControl = _pageDownloader}
            Dim tabConverter As New ModernTabControl.ModernTab("模型转换") With {.BoundControl = _pageConverter}
            Dim tabModelInfo As New ModernTabControl.ModernTab("模型指南") With {.BoundControl = _pageModelInfo}
            Dim tabTutorial As New ModernTabControl.ModernTab("使用教程") With {.BoundControl = _pageTutorial}
            _tabs.Items.Add(tabMain)
            _tabs.Items.Add(tabPreview)
            _tabs.Items.Add(tabAdvanced)
            _tabs.Items.Add(tabDownloader)
            _tabs.Items.Add(tabConverter)
            _tabs.Items.Add(tabModelInfo)
            _tabs.Items.Add(tabTutorial)
            ' 每次打开插件都从超分主界面开始，避免保留上次停留在实时预览/高级功能页的状态。
            _tabs.SelectedIndex = 0
        End Sub

        ' ────────────────────────── 超分主界面页 ──────────────────────────

        Private Shared Function CreateOfficialValueBox(valueControl As Control) As ModernPanel
            Dim box As New ModernPanel With {
                .Dock = DockStyle.Fill,
                .Margin = New Padding(0, 5, 0, 5),
                .Padding = New Padding(10, 0, 10, 0),
                .BackColor = Color.Transparent,
                .BackColor1 = UiSurface,
                .BorderColor = Color.Transparent,
                .BorderSize = 0,
                .BorderRadius = 10
            }
            valueControl.Dock = DockStyle.Fill
            valueControl.Margin = Padding.Empty
            box.Controls.Add(valueControl)
            Return box
        End Function

        Private Shared Sub ConfigureOfficialTextBox(textBox As ModernTextBox, waterText As String)
            textBox.Dock = DockStyle.Fill
            textBox.Margin = New Padding(0, 6, 0, 6)
            textBox.Padding = New Padding(12, 0, 12, 0)
            textBox.Font = New Font("Microsoft YaHei UI", 10.0F)
            textBox.BackColor1 = UiSurfaceRaised
            textBox.ForeColor = UiText
            textBox.WaterText = waterText
            textBox.WaterTextForeColor = UiTextMuted
            textBox.CaretColor = UiText
            textBox.SelectionColor = UiSurfaceHover
            textBox.BorderColor = Color.Transparent
            textBox.BorderColorFocus = Color.FromArgb(80, 220, 220, 220)
            textBox.BorderSize = 0
            textBox.BorderRadius = 10
            textBox.MultiLine = False
        End Sub

        Private Shared Function CreateOfficialSeparator() As Control
            Dim host As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 3,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            host.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
            host.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))
            host.RowStyles.Add(New RowStyle(SizeType.Absolute, 1.0F))
            host.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))
            host.Controls.Add(New Panel With {
                .Dock = DockStyle.Fill,
                .Margin = Padding.Empty,
                .BackColor = Color.FromArgb(58, 220, 220, 220)
            }, 0, 1)
            Return host
        End Function

        Private Shared Function BuildOfficialModeHeader(title As String, description As String,
                                                        switchControl As LakeUI.BooleanSwitch,
                                                        stateLabel As HtmlColorLabel) As Control
            Dim row As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 5,
                .RowCount = 1,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            Dim titleLabel = CreateTextLabel(title, 12.0F, FontStyle.Regular, UiText)
            titleLabel.Dock = DockStyle.Fill
            titleLabel.Margin = Padding.Empty
            titleLabel.TextAlign = ContentAlignment.MiddleLeft
            Dim titleWidth = Math.Max(84, TextRenderer.MeasureText(title, titleLabel.Font).Width + 4)
            row.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, CSng(titleWidth)))
            row.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 10.0F))
            row.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 42.0F))
            row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
            row.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 112.0F))
            row.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            switchControl.Anchor = AnchorStyles.None
            switchControl.Margin = Padding.Empty
            Dim descriptionLabel = CreateOfficialCaption(description)
            descriptionLabel.TextAlign = ContentAlignment.MiddleLeft
            descriptionLabel.Margin = New Padding(14, 0, 0, 0)
            stateLabel.Dock = DockStyle.Fill
            stateLabel.Margin = Padding.Empty
            stateLabel.AutoSize = False
            stateLabel.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleRight
            row.Controls.Add(titleLabel, 0, 0)
            row.Controls.Add(switchControl, 2, 0)
            row.Controls.Add(descriptionLabel, 3, 0)
            row.Controls.Add(stateLabel, 4, 0)
            Return row
        End Function

        Private Sub BuildOfficialUpscalePage()
            _pageUpscale.Dock = DockStyle.Fill
            _pageUpscale.BackColor = Color.Transparent
            _pageUpscale.Padding = Padding.Empty
            _pageUpscale.AutoScroll = False
            _pageUpscale.AllowDrop = True
            AddHandler _pageUpscale.DragEnter, AddressOf OnImageDragEnter
            AddHandler _pageUpscale.DragDrop, AddressOf OnImageDragDrop

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .Height = 526,
                .MinimumSize = New Size(820, 526),
                .ColumnCount = 1,
                .RowCount = 11,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty,
                .AllowDrop = True
            }
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 25.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 112.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 25.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 54.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 54.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 54.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
            AddHandler root.DragEnter, AddressOf OnImageDragEnter
            AddHandler root.DragDrop, AddressOf OnImageDragDrop

            ConfigureDpiSwitch(_switchMaster)
            _switchMaster.Checked = _config.Enabled
            AddHandler _switchMaster.CheckedChanged, AddressOf OnMasterSwitchChanged
            root.Controls.Add(BuildOfficialModeHeader(
                "插件总开关", "", _switchMaster, _lblMaster), 0, 0)

            Dim exeRow As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 3,
                .RowCount = 1,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            exeRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 150.0F))
            exeRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 12.0F))
            exeRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
            exeRow.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            _btnPickExe.Text = "选择处理程序"
            _btnPickExe.Dock = DockStyle.Fill
            _btnPickExe.Margin = New Padding(0, 6, 0, 6)
            ConfigureSecondaryButton(_btnPickExe)
            AddHandler _btnPickExe.Click, AddressOf OnPickExeClick
            _lblExe.AutoSize = False
            _lblExe.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _lblExe.ForeColor = UiText
            exeRow.Controls.Add(_btnPickExe, 0, 0)
            exeRow.Controls.Add(CreateOfficialValueBox(_lblExe), 2, 0)
            root.Controls.Add(exeRow, 0, 1)
            root.Controls.Add(CreateOfficialSeparator(), 0, 2)

            root.Controls.Add(CreateOfficialSectionHeading(
                "视频处理", "超分与补帧互斥；模型列表随推理后端自动切换"), 0, 3)

            Dim modes As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 1,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            modes.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
            modes.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
            modes.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

            Dim upscalePane As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 2,
                .BackColor = Color.Transparent,
                .Margin = New Padding(0, 0, 12, 0),
                .Padding = Padding.Empty
            }
            upscalePane.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
            upscalePane.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            ConfigureDpiSwitch(_switchUpscale)
            _switchUpscale.Checked = _config.UpscaleEnabled
            _switchUpscale.Enabled = _config.Enabled
            AddHandler _switchUpscale.CheckedChanged, AddressOf OnUpscaleSwitchChanged
            upscalePane.Controls.Add(BuildOfficialModeHeader(
                "视频超分", "", _switchUpscale, _lblSwitch), 0, 0)
            Dim upscaleFields As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 1,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            upscaleFields.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 46.0F))
            upscaleFields.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 54.0F))
            upscaleFields.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            _cmbBackend.WaterText = "选择推理方式…"
            ConfigureCombo(_cmbBackend)
            _cmbBackend.Items.Add("NCNN (Vulkan)")
            _cmbBackend.Items.Add("CUDA (PyTorch)")
            _cmbBackend.Items.Add("TensorRT (NVIDIA)")
            _cmbBackend.Items.Add("ONNX Runtime")
            _cmbBackend.Items.Add("FlashVSR (NVIDIA · 视频)")
            AddHandler _cmbBackend.SelectedIndexChanged, AddressOf OnBackendSelected
            _cmbModel.WaterText = "选择放大模型…"
            ConfigureCombo(_cmbModel)
            AddHandler _cmbModel.DropDownOpened, AddressOf OnModelDropDownOpened
            AddHandler _cmbModel.Click, AddressOf OnModelComboClicked
            AddHandler _cmbModel.SelectedIndexChanged, AddressOf OnModelSelected
            upscaleFields.Controls.Add(CreateOfficialField("推理后端", _cmbBackend), 0, 0)
            upscaleFields.Controls.Add(CreateOfficialField("放大模型", _cmbModel, 0), 1, 0)
            upscalePane.Controls.Add(upscaleFields, 0, 1)
            modes.Controls.Add(upscalePane, 0, 0)

            Dim interpPane As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 2,
                .BackColor = Color.Transparent,
                .Margin = New Padding(12, 0, 0, 0),
                .Padding = Padding.Empty
            }
            interpPane.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
            interpPane.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            ConfigureDpiSwitch(_switchInterp)
            _switchInterp.Checked = _config.InterpEnabled
            _switchInterp.Enabled = _config.Enabled
            AddHandler _switchInterp.CheckedChanged, AddressOf OnInterpSwitchChanged
            interpPane.Controls.Add(BuildOfficialModeHeader(
                "运动补帧", "", _switchInterp, _lblSwitchInterp), 0, 0)
            Dim interpFields As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 1,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            interpFields.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 68.0F))
            interpFields.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 32.0F))
            interpFields.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            _cmbInterp.WaterText = "选择补帧模型…"
            ConfigureCombo(_cmbInterp)
            AddHandler _cmbInterp.DropDownOpened, AddressOf OnInterpDropDownOpened
            AddHandler _cmbInterp.Click, AddressOf OnInterpComboClicked
            AddHandler _cmbInterp.SelectedIndexChanged, AddressOf OnInterpModelSelected
            _cmbFactor.WaterText = "选择倍率…"
            ConfigureCombo(_cmbFactor)
            _cmbFactor.Items.Add("2 倍")
            _cmbFactor.Items.Add("3 倍")
            _cmbFactor.Items.Add("4 倍")
            _cmbFactor.Items.Add("8 倍")
            AddHandler _cmbFactor.SelectedIndexChanged, AddressOf OnFactorSelected
            interpFields.Controls.Add(CreateOfficialField("补帧模型", _cmbInterp), 0, 0)
            interpFields.Controls.Add(CreateOfficialField("补帧倍率", _cmbFactor, 0), 1, 0)
            interpPane.Controls.Add(interpFields, 0, 1)
            modes.Controls.Add(interpPane, 1, 0)
            root.Controls.Add(modes, 0, 4)
            root.Controls.Add(CreateOfficialSeparator(), 0, 5)

            root.Controls.Add(CreateOfficialSectionHeading(
                "图片增强", "沿用上方超分后端与模型，可选择文件、文件夹或直接拖入"), 0, 6)

            Dim imageInputRow As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 5,
                .RowCount = 1,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty,
                .AllowDrop = True
            }
            imageInputRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 150.0F))
            imageInputRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 12.0F))
            imageInputRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 170.0F))
            imageInputRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 12.0F))
            imageInputRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
            imageInputRow.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            ConfigureImageButton(_btnImageFiles, "选择图片", 150)
            ConfigureImageButton(_btnImageFolder, "选择文件夹", 170)
            _btnImageFiles.Dock = DockStyle.Fill
            _btnImageFolder.Dock = DockStyle.Fill
            _btnImageFiles.Margin = New Padding(0, 6, 0, 6)
            _btnImageFolder.Margin = New Padding(0, 6, 0, 6)
            AddHandler _btnImageFiles.Click, AddressOf OnPickImageFiles
            AddHandler _btnImageFolder.Click, AddressOf OnPickImageFolder
            _lblImageInputs.AutoSize = False
            _lblImageInputs.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _lblImageInputs.Text = "<font color=#888888>尚未选择图片</font>"
            imageInputRow.Controls.Add(_btnImageFiles, 0, 0)
            imageInputRow.Controls.Add(_btnImageFolder, 2, 0)
            imageInputRow.Controls.Add(CreateOfficialValueBox(_lblImageInputs), 4, 0)
            AddHandler imageInputRow.DragEnter, AddressOf OnImageDragEnter
            AddHandler imageInputRow.DragDrop, AddressOf OnImageDragDrop
            root.Controls.Add(imageInputRow, 0, 7)

            Dim imageOutputRow As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 3,
                .RowCount = 1,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            imageOutputRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 170.0F))
            imageOutputRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 12.0F))
            imageOutputRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
            imageOutputRow.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            ConfigureImageButton(_btnImageOutput, "选择输出目录", 170)
            _btnImageOutput.Dock = DockStyle.Fill
            _btnImageOutput.Margin = New Padding(0, 6, 0, 6)
            AddHandler _btnImageOutput.Click, AddressOf OnPickImageOutput
            ConfigureOfficialTextBox(_txtImageOutput, "留空即输出到源目录")
            Dim initialOutput = If(_config.ImageOutputOriginal, "", _config.ImageOutput)
            _txtImageOutput.Text = initialOutput
            _config.ImageOutput = initialOutput
            _config.ImageOutputOriginal = String.IsNullOrWhiteSpace(initialOutput)
            AddHandler _txtImageOutput.TextChanged, AddressOf OnImageOutputTextChanged
            imageOutputRow.Controls.Add(_btnImageOutput, 0, 0)
            imageOutputRow.Controls.Add(_txtImageOutput, 2, 0)
            root.Controls.Add(imageOutputRow, 0, 8)

            Dim imageOptionsRow As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 8,
                .RowCount = 1,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            imageOptionsRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 82.0F))
            imageOptionsRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 220.0F))
            imageOptionsRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 20.0F))
            imageOptionsRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 82.0F))
            imageOptionsRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 220.0F))
            imageOptionsRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
            imageOptionsRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 16.0F))
            imageOptionsRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 170.0F))
            imageOptionsRow.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

            Dim suffixLabel = CreateOfficialCaption("命名方式")
            suffixLabel.TextAlign = ContentAlignment.MiddleLeft
            _cmbImageSuffix.Items.Add("处理时间戳")
            _cmbImageSuffix.Items.Add("模型名称")
            _cmbImageSuffix.SelectedIndex = If(String.Equals(_config.ImageSuffix, "model", StringComparison.OrdinalIgnoreCase), 1, 0)
            _cmbImageSuffix.WaterText = "选择命名方式…"
            ConfigureCombo(_cmbImageSuffix)
            _cmbImageSuffix.Editable = False
            _cmbImageSuffix.Dock = DockStyle.Fill
            _cmbImageSuffix.Margin = New Padding(0, 6, 0, 6)
            AddHandler _cmbImageSuffix.SelectedIndexChanged, AddressOf OnImageSuffixChanged

            Dim formatLabel = CreateOfficialCaption("输出格式")
            formatLabel.TextAlign = ContentAlignment.MiddleLeft
            _cmbImageFormat.Items.Add("无损 PNG")
            _cmbImageFormat.Items.Add("保留源格式")
            _cmbImageFormat.SelectedIndex = If(_config.ImagePng, 0, 1)
            _cmbImageFormat.WaterText = "选择输出格式…"
            ConfigureCombo(_cmbImageFormat)
            _cmbImageFormat.Editable = False
            _cmbImageFormat.Dock = DockStyle.Fill
            _cmbImageFormat.Margin = New Padding(0, 6, 0, 6)
            AddHandler _cmbImageFormat.SelectedIndexChanged, AddressOf OnImageFormatChanged

            _btnImageStart.Text = "开始增强"
            _btnImageStart.Dock = DockStyle.Fill
            _btnImageStart.Margin = New Padding(0, 6, 0, 6)
            ConfigurePrimaryButton(_btnImageStart)
            AddHandler _btnImageStart.Click, AddressOf OnStartImageProcessing

            imageOptionsRow.Controls.Add(suffixLabel, 0, 0)
            imageOptionsRow.Controls.Add(_cmbImageSuffix, 1, 0)
            imageOptionsRow.Controls.Add(formatLabel, 3, 0)
            imageOptionsRow.Controls.Add(_cmbImageFormat, 4, 0)
            imageOptionsRow.Controls.Add(_btnImageStart, 7, 0)
            root.Controls.Add(imageOptionsRow, 0, 9)

            Dim progressRow As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 3,
                .RowCount = 1,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            progressRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
            progressRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 16.0F))
            progressRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 300.0F))
            progressRow.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            _imageProgress.Minimum = 0
            _imageProgress.Maximum = 1000
            _imageProgress.Dock = DockStyle.Fill
            _imageProgress.Margin = New Padding(0, 15, 0, 15)
            _imageProgress.TrackColor = Color.FromArgb(40, 220, 220, 220)
            _imageProgress.ProgressColor = UiAccent
            _imageProgress.GlowColor = Color.FromArgb(120, 204, 255)
            _lblImageProgress.AutoSize = False
            _lblImageProgress.Dock = DockStyle.Fill
            _lblImageProgress.Margin = Padding.Empty
            _lblImageProgress.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _lblImageProgress.Text = "<font color=#888888>等待开始</font>"
            progressRow.Controls.Add(_imageProgress, 0, 0)
            progressRow.Controls.Add(_lblImageProgress, 2, 0)
            root.Controls.Add(progressRow, 0, 10)

            ' 最小窗口保留紧凑布局；宿主窗口较高时主动拉开分区和操作行，
            ' 避免所有控件挤在页面顶部，同时不让按钮本身变得过高。
            Dim applyDensity As Action =
                Sub()
                    Dim spacious = _pageUpscale.ClientSize.Height >= 650
                    Dim rowHeights = If(
                        spacious,
                        New Single() {46.0F, 56.0F, 33.0F, 42.0F, 132.0F, 33.0F, 42.0F, 60.0F, 60.0F, 60.0F, 46.0F},
                        New Single() {40.0F, 48.0F, 25.0F, 36.0F, 112.0F, 25.0F, 36.0F, 54.0F, 54.0F, 54.0F, 42.0F})

                    root.SuspendLayout()
                    Dim totalHeight As Integer = 0
                    For index As Integer = 0 To rowHeights.Length - 1
                        root.RowStyles(index).Height = rowHeights(index)
                        totalHeight += CInt(rowHeights(index))
                    Next
                    upscalePane.RowStyles(0).Height = If(spacious, 44.0F, 38.0F)
                    interpPane.RowStyles(0).Height = If(spacious, 44.0F, 38.0F)
                    upscalePane.Margin = If(spacious, New Padding(0, 0, 16, 0), New Padding(0, 0, 12, 0))
                    interpPane.Margin = If(spacious, New Padding(16, 0, 0, 0), New Padding(12, 0, 0, 0))
                    root.Height = totalHeight
                    root.ResumeLayout(True)
                End Sub

            AddHandler _pageUpscale.ClientSizeChanged,
                Sub(sender, e)
                    applyDensity()
                End Sub

            _pageUpscale.Controls.Add(root)
            applyDensity()
            UpdateModeStateLabels()
        End Sub

        Private Sub BuildUpscalePage()
            _pageUpscale.Dock = DockStyle.Fill
            _pageUpscale.BackColor = Color.Transparent
            _pageUpscale.Padding = New Padding(8, 14, 8, 10)

            ' 固定底部处理程序条；其余区域按「状态头 → 双处理卡 → 图片工作区」排列。
            Dim contentHost As New Panel() With {.Dock = DockStyle.Fill, .BackColor = Color.Transparent}
            _pageUpscale.Controls.Add(contentHost)

            Dim exeStrip As New FluentCardPanel() With {
                .Dock = DockStyle.Bottom, .Height = 50,
                .FillColor = UiSurface, .StrokeColor = UiStrokeSoft, .CornerRadius = 10,
                .Padding = New Padding(14, 8, 8, 8)
            }
            Dim exeIcon As Label = CreateTextLabel("⌘", 13.0F, FontStyle.Bold, UiAccent)
            exeIcon.Dock = DockStyle.Left
            exeIcon.Width = 28
            exeIcon.TextAlign = ContentAlignment.MiddleLeft
            _btnPickExe.Text = "选择程序"
            _btnPickExe.Size = New Size(112, 34)
            _btnPickExe.Dock = DockStyle.Right
            ConfigureSecondaryButton(_btnPickExe)
            AddHandler _btnPickExe.Click, AddressOf OnPickExeClick
            _lblExe.AutoSize = False
            _lblExe.Dock = DockStyle.Fill
            _lblExe.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _lblExe.ForeColor = UiTextSecondary
            exeStrip.Controls.Add(_lblExe)
            exeStrip.Controls.Add(_btnPickExe)
            exeStrip.Controls.Add(exeIcon)
            _pageUpscale.Controls.Add(exeStrip)

            Dim imageSection = BuildImageUpscaleSection()
            imageSection.Dock = DockStyle.Fill
            contentHost.Controls.Add(imageSection)

            Dim settingsHost As New Panel() With {
                .Dock = DockStyle.Top, .Height = 218, .BackColor = Color.Transparent
            }
            Dim upscaleCard As New FluentCardPanel() With {
                .FillColor = UiSurface, .StrokeColor = UiStrokeSoft, .CornerRadius = 12
            }
            Dim interpCard As New FluentCardPanel() With {
                .FillColor = UiSurface, .StrokeColor = UiStrokeSoft, .CornerRadius = 12
            }
            settingsHost.Controls.AddRange(New Control() {upscaleCard, interpCard})

            ' 视频超分卡片
            Dim upscaleAccent As New Panel() With {
                .BackColor = UiAccent, .Location = New Point(0, 18), .Size = New Size(4, 42)
            }
            Dim upscaleTitle As HtmlColorLabel = CreateHtmlTextLabel("视频超分", 12.0F, FontStyle.Bold, UiText)
            upscaleTitle.Location = New Point(20, 7)
            upscaleTitle.Size = New Size(250, 28)
            Dim upscaleDesc As Label = CreateTextLabel("提升视频分辨率与细节，模型随推理后端联动。", 8.7F, FontStyle.Regular, UiTextMuted)
            upscaleDesc.Location = New Point(20, 32)
            upscaleDesc.Size = New Size(430, 24)
            _lblSwitch.Text = "<font color=#7E8C9D>关闭</font>"
            _lblSwitch.AutoSize = False
            _lblSwitch.Size = New Size(68, 30)
            _lblSwitch.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleRight
            _switchUpscale.Dock = DockStyle.None
            ConfigureDpiSwitch(_switchUpscale)
            _switchUpscale.Checked = _config.UpscaleEnabled
            _switchUpscale.Enabled = _config.Enabled
            AddHandler _switchUpscale.CheckedChanged, AddressOf OnUpscaleSwitchChanged

            _lblBackend.Text = "<font color=#B1BCCA>推理后端</font>"
            _lblBackend.AutoSize = False
            _lblBackend.Location = New Point(20, 58)
            _lblBackend.Size = New Size(110, 24)
            _lblBackend.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _cmbBackend.Location = New Point(20, 80)
            _cmbBackend.Size = New Size(250, 36)
            _cmbBackend.WaterText = "选择推理方式…"
            ConfigureCombo(_cmbBackend)
            _cmbBackend.Items.Add("NCNN (Vulkan)")
            _cmbBackend.Items.Add("CUDA (PyTorch)")
            _cmbBackend.Items.Add("TensorRT (NVIDIA)")
            _cmbBackend.Items.Add("ONNX Runtime")
            _cmbBackend.Items.Add("FlashVSR (NVIDIA · 视频)")
            AddHandler _cmbBackend.SelectedIndexChanged, AddressOf OnBackendSelected

            Dim upscaleModelLabel As Label = CreateTextLabel("放大模型", 8.7F, FontStyle.Regular, UiTextSecondary)
            upscaleModelLabel.Location = New Point(20, 121)
            upscaleModelLabel.Size = New Size(120, 24)
            _cmbModel.Location = New Point(20, 144)
            _cmbModel.Size = New Size(420, 36)
            _cmbModel.WaterText = "点击选择放大模型…"
            ConfigureCombo(_cmbModel)
            AddHandler _cmbModel.DropDownOpened, AddressOf OnModelDropDownOpened
            AddHandler _cmbModel.Click, AddressOf OnModelComboClicked
            AddHandler _cmbModel.SelectedIndexChanged, AddressOf OnModelSelected
            upscaleCard.Controls.AddRange(New Control() {
                upscaleAccent, upscaleTitle, upscaleDesc, _lblSwitch, _switchUpscale,
                _lblBackend, _cmbBackend, upscaleModelLabel, _cmbModel
            })

            ' 运动补帧卡片
            Dim interpAccent As New Panel() With {
                .BackColor = UiSuccess, .Location = New Point(0, 18), .Size = New Size(4, 42)
            }
            Dim interpTitle As HtmlColorLabel = CreateHtmlTextLabel("运动补帧", 12.0F, FontStyle.Bold, UiText)
            interpTitle.Location = New Point(20, 7)
            interpTitle.Size = New Size(250, 28)
            Dim interpDesc As Label = CreateTextLabel("通过 RIFE 生成中间帧，让运动画面更流畅。", 8.7F, FontStyle.Regular, UiTextMuted)
            interpDesc.Location = New Point(20, 32)
            interpDesc.Size = New Size(430, 24)
            _lblSwitchInterp.Text = "<font color=#7E8C9D>关闭</font>"
            _lblSwitchInterp.AutoSize = False
            _lblSwitchInterp.Size = New Size(68, 30)
            _lblSwitchInterp.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleRight
            _switchInterp.Dock = DockStyle.None
            ConfigureDpiSwitch(_switchInterp)
            _switchInterp.Checked = _config.InterpEnabled
            _switchInterp.Enabled = _config.Enabled
            AddHandler _switchInterp.CheckedChanged, AddressOf OnInterpSwitchChanged

            Dim interpModelLabel As Label = CreateTextLabel("补帧模型", 8.7F, FontStyle.Regular, UiTextSecondary)
            interpModelLabel.Location = New Point(20, 58)
            interpModelLabel.Size = New Size(120, 24)
            _cmbInterp.Location = New Point(20, 80)
            _cmbInterp.Size = New Size(420, 36)
            _cmbInterp.WaterText = "点击选择补帧模型…"
            ConfigureCombo(_cmbInterp)
            AddHandler _cmbInterp.DropDownOpened, AddressOf OnInterpDropDownOpened
            AddHandler _cmbInterp.Click, AddressOf OnInterpComboClicked
            AddHandler _cmbInterp.SelectedIndexChanged, AddressOf OnInterpModelSelected

            _lblFactor.Text = "<font color=#B1BCCA>补帧倍率</font>"
            _lblFactor.AutoSize = False
            _lblFactor.Location = New Point(20, 121)
            _lblFactor.Size = New Size(120, 24)
            _lblFactor.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _cmbFactor.Location = New Point(20, 144)
            _cmbFactor.Size = New Size(140, 36)
            _cmbFactor.WaterText = "倍率…"
            ConfigureCombo(_cmbFactor)
            _cmbFactor.Items.Add("2 倍")
            _cmbFactor.Items.Add("3 倍")
            _cmbFactor.Items.Add("4 倍")
            _cmbFactor.Items.Add("8 倍")
            AddHandler _cmbFactor.SelectedIndexChanged, AddressOf OnFactorSelected
            Dim factorHint As Label = CreateTextLabel("更高倍率会增加处理时间与显存占用", 8.5F, FontStyle.Regular, UiTextMuted)
            factorHint.Location = New Point(176, 143)
            factorHint.Size = New Size(300, 38)
            interpCard.Controls.AddRange(New Control() {
                interpAccent, interpTitle, interpDesc, _lblSwitchInterp, _switchInterp,
                interpModelLabel, _cmbInterp, _lblFactor, _cmbFactor, factorHint
            })

            Dim arrangeUpscaleCard As Action =
                Sub()
                    Dim right = upscaleCard.ClientSize.Width - 20
                    _switchUpscale.Location = New Point(Math.Max(260, right - _switchUpscale.Width), 15)
                    _lblSwitch.Location = New Point(_switchUpscale.Left - _lblSwitch.Width - 8, 11)
                    upscaleTitle.Width = Math.Max(120, _lblSwitch.Left - upscaleTitle.Left - 12)
                    upscaleDesc.Width = Math.Max(180, right - upscaleDesc.Left)
                    _cmbBackend.Width = Math.Max(180, right - _cmbBackend.Left)
                    _cmbModel.Width = Math.Max(180, right - _cmbModel.Left)
                End Sub
            Dim arrangeInterpCard As Action =
                Sub()
                    Dim right = interpCard.ClientSize.Width - 20
                    _switchInterp.Location = New Point(Math.Max(260, right - _switchInterp.Width), 15)
                    _lblSwitchInterp.Location = New Point(_switchInterp.Left - _lblSwitchInterp.Width - 8, 11)
                    interpTitle.Width = Math.Max(120, _lblSwitchInterp.Left - interpTitle.Left - 12)
                    interpDesc.Width = Math.Max(180, right - interpDesc.Left)
                    _cmbInterp.Width = Math.Max(180, right - _cmbInterp.Left)
                    factorHint.Width = Math.Max(120, right - factorHint.Left)
                End Sub
            AddHandler upscaleCard.Resize, Sub(sender, e) arrangeUpscaleCard()
            AddHandler interpCard.Resize, Sub(sender, e) arrangeInterpCard()

            Dim arrangeSettings As Action =
                Sub()
                    Dim width = settingsHost.ClientSize.Width
                    If width < 900 Then
                        If settingsHost.Height <> 420 Then settingsHost.Height = 420
                        upscaleCard.SetBounds(0, 10, Math.Max(420, width), 196)
                        interpCard.SetBounds(0, 214, Math.Max(420, width), 196)
                    Else
                        If settingsHost.Height <> 218 Then settingsHost.Height = 218
                        Dim cardWidth = Math.Max(420, (width - 12) \ 2)
                        upscaleCard.SetBounds(0, 10, cardWidth, 198)
                        interpCard.SetBounds(cardWidth + 12, 10, Math.Max(420, width - cardWidth - 12), 198)
                    End If
                    arrangeUpscaleCard()
                    arrangeInterpCard()
                End Sub
            AddHandler settingsHost.Resize, Sub(sender, e) arrangeSettings()
            contentHost.Controls.Add(settingsHost)

            ' 顶部总状态卡片
            Dim masterCard As New FluentCardPanel() With {
                .Dock = DockStyle.Top, .Height = 74,
                .FillColor = UiSurface, .StrokeColor = UiStrokeSoft, .CornerRadius = 12
            }
            Dim masterAccent As New Panel() With {
                .BackColor = UiAccent, .Location = New Point(0, 15), .Size = New Size(4, 44)
            }
            Dim masterIcon As New FluentCardPanel() With {
                .Location = New Point(18, 17), .Size = New Size(40, 40),
                .FillColor = Color.FromArgb(34, UiAccent), .StrokeColor = Color.FromArgb(88, UiAccent), .CornerRadius = 12
            }
            Dim masterGlyph As Label = CreateTextLabel("VE", 11.0F, FontStyle.Bold, UiAccent)
            masterGlyph.Dock = DockStyle.Fill
            masterGlyph.TextAlign = ContentAlignment.MiddleCenter
            masterIcon.Controls.Add(masterGlyph)
            Dim masterTitle As Label = CreateTextLabel("Video Enhancer", 13.0F, FontStyle.Bold, UiText)
            masterTitle.Location = New Point(72, 8)
            masterTitle.Size = New Size(480, 30)
            Dim masterSubtitle As Label = CreateTextLabel("接管编码队列，为视频任务启用 AI 超分或运动补帧。", 9.0F, FontStyle.Regular, UiTextSecondary)
            masterSubtitle.Location = New Point(72, 35)
            masterSubtitle.Size = New Size(700, 25)
            _lblMaster.AutoSize = False
            _lblMaster.Size = New Size(126, 36)
            _lblMaster.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleRight
            _switchMaster.Dock = DockStyle.None
            ConfigureDpiSwitch(_switchMaster)
            _switchMaster.Checked = _config.Enabled
            AddHandler _switchMaster.CheckedChanged, AddressOf OnMasterSwitchChanged
            masterCard.Controls.AddRange(New Control() {
                masterAccent, masterIcon, masterTitle, masterSubtitle, _lblMaster, _switchMaster
            })
            Dim arrangeMaster As Action =
                Sub()
                    Dim right = masterCard.ClientSize.Width - 22
                    _switchMaster.Location = New Point(Math.Max(420, right - _switchMaster.Width), 25)
                    _lblMaster.Location = New Point(_switchMaster.Left - _lblMaster.Width - 10, 19)
                    masterTitle.Width = Math.Max(180, _lblMaster.Left - masterTitle.Left - 16)
                    masterSubtitle.Width = Math.Max(220, _lblMaster.Left - masterSubtitle.Left - 16)
                End Sub
            AddHandler masterCard.Resize, Sub(sender, e) arrangeMaster()
            contentHost.Controls.Add(masterCard)

            arrangeMaster()
            arrangeSettings()
            UpdateModeStateLabels()
        End Sub

        Private Function BuildImageUpscaleSection() As Panel
            Dim section As New FluentCardPanel() With {
                .FillColor = UiSurface, .StrokeColor = UiStrokeSoft, .CornerRadius = 12,
                .AllowDrop = True
            }
            AddHandler section.DragEnter, AddressOf OnImageDragEnter
            AddHandler section.DragDrop, AddressOf OnImageDragDrop

            Dim title As HtmlColorLabel = CreateHtmlTextLabel("图片增强", 12.0F, FontStyle.Bold, UiText)
            title.Location = New Point(18, 10)
            title.Size = New Size(300, 30)
            Dim subtitle As Label = CreateTextLabel("沿用上方超分后端与模型，支持文件、文件夹和拖放。", 8.8F, FontStyle.Regular, UiTextMuted)
            subtitle.Location = New Point(18, 36)
            subtitle.Size = New Size(720, 24)
            section.Controls.AddRange(New Control() {title, subtitle})

            Dim inputRow As New FluentCardPanel() With {
                .FillColor = UiSurfaceRaised, .StrokeColor = UiStrokeSoft, .CornerRadius = 9
            }
            Dim inputTag As Label = CreateTextLabel("输入", 8.7F, FontStyle.Bold, UiAccent)
            inputTag.Location = New Point(14, 0)
            inputTag.Size = New Size(58, 66)
            ConfigureImageButton(_btnImageFiles, "选择图片", 148)
            ConfigureImageButton(_btnImageFolder, "选择文件夹", 172)
            _btnImageFiles.Location = New Point(82, 15)
            _btnImageFolder.Location = New Point(238, 15)
            AddHandler _btnImageFiles.Click, AddressOf OnPickImageFiles
            AddHandler _btnImageFolder.Click, AddressOf OnPickImageFolder
            _lblImageInputs.Location = New Point(422, 10)
            _lblImageInputs.Size = New Size(420, 46)
            _lblImageInputs.AutoSize = False
            _lblImageInputs.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _lblImageInputs.Text = "<font color=#7E8C9D>尚未选择图片，可直接拖放到此区域</font>"
            inputRow.Controls.AddRange(New Control() {inputTag, _btnImageFiles, _btnImageFolder, _lblImageInputs})
            section.Controls.Add(inputRow)

            Dim outputRow As New FluentCardPanel() With {
                .FillColor = UiSurfaceRaised, .StrokeColor = UiStrokeSoft, .CornerRadius = 9
            }
            Dim outputTag As Label = CreateTextLabel("输出", 8.7F, FontStyle.Bold, UiSuccess)
            outputTag.Location = New Point(14, 0)
            outputTag.Size = New Size(58, 72)
            ConfigureImageButton(_btnImageOutput, "输出文件夹", 148)
            _btnImageOutput.Location = New Point(82, 18)
            AddHandler _btnImageOutput.Click, AddressOf OnPickImageOutput
            _lblImageOutput.Location = New Point(238, 12)
            _lblImageOutput.Size = New Size(290, 48)
            _lblImageOutput.AutoSize = False
            _lblImageOutput.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _switchImageOriginal.Dock = DockStyle.None
            ConfigureDpiSwitch(_switchImageOriginal)
            _switchImageOriginal.Checked = _config.ImageOutputOriginal
            AddHandler _switchImageOriginal.CheckedChanged, AddressOf OnImageOriginalChanged
            Dim originalLabel As Label = CreateTextLabel("原目录输出", 8.7F, FontStyle.Regular, UiTextSecondary)
            originalLabel.Size = New Size(104, 36)
            _cmbImageSuffix.Size = New Size(150, 36)
            _cmbImageSuffix.Items.Add("处理时间戳")
            _cmbImageSuffix.Items.Add("模型名称")
            _cmbImageSuffix.SelectedIndex = If(String.Equals(_config.ImageSuffix, "model", StringComparison.OrdinalIgnoreCase), 1, 0)
            _cmbImageSuffix.WaterText = "文件名后缀"
            ConfigureCombo(_cmbImageSuffix)
            AddHandler _cmbImageSuffix.SelectedIndexChanged, AddressOf OnImageSuffixChanged
            outputRow.Controls.AddRange(New Control() {
                outputTag, _btnImageOutput, _lblImageOutput, _switchImageOriginal, originalLabel, _cmbImageSuffix
            })
            section.Controls.Add(outputRow)

            Dim actionRow As New FluentCardPanel() With {
                .FillColor = Color.FromArgb(238, 29, 36, 46), .StrokeColor = UiStroke, .CornerRadius = 9
            }
            _btnImageStart.Text = "开始增强  →"
            _btnImageStart.Size = New Size(170, 38)
            ConfigurePrimaryButton(_btnImageStart)
            AddHandler _btnImageStart.Click, AddressOf OnStartImageProcessing
            _switchImagePng.Dock = DockStyle.None
            ConfigureDpiSwitch(_switchImagePng)
            _switchImagePng.Checked = _config.ImagePng
            AddHandler _switchImagePng.CheckedChanged, AddressOf OnImagePngChanged
            Dim pngLabel As Label = CreateTextLabel("无损 PNG", 8.8F, FontStyle.Bold, UiTextSecondary)
            pngLabel.Size = New Size(92, 38)
            Dim pngHint As Label = CreateTextLabel("关闭时保留源格式", 8.4F, FontStyle.Regular, UiTextMuted)
            pngHint.Size = New Size(170, 38)
            _imageProgress.Minimum = 0
            _imageProgress.Maximum = 1000
            _imageProgress.TrackColor = Color.FromArgb(42, 50, 61)
            _imageProgress.ProgressColor = UiAccent
            _imageProgress.GlowColor = Color.FromArgb(120, 204, 255)
            _lblImageProgress.AutoSize = False
            _lblImageProgress.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _lblImageProgress.Text = "<font color=#7E8C9D>等待开始</font>"
            actionRow.Controls.AddRange(New Control() {
                _btnImageStart, _switchImagePng, pngLabel, pngHint, _imageProgress, _lblImageProgress
            })
            section.Controls.Add(actionRow)

            Dim arrange As Action =
                Sub()
                    Dim rowWidth = Math.Max(760, section.ClientSize.Width - 32)
                    Dim gap = Math.Max(8, Math.Min(16, (section.ClientSize.Height - 290) \ 2))
                    inputRow.SetBounds(16, 66, rowWidth, 66)
                    outputRow.SetBounds(16, inputRow.Bottom + gap, rowWidth, 72)
                    actionRow.SetBounds(16, outputRow.Bottom + gap, rowWidth, 72)
                    title.Width = Math.Max(220, rowWidth - 20)
                    subtitle.Width = Math.Max(220, rowWidth - 20)

                    _lblImageInputs.Width = Math.Max(160, rowWidth - _lblImageInputs.Left - 14)

                    Dim suffixWidth = Math.Max(138, Math.Min(170, CInt(rowWidth * 0.14)))
                    _cmbImageSuffix.SetBounds(rowWidth - suffixWidth - 12, 18, suffixWidth, 36)
                    originalLabel.Location = New Point(_cmbImageSuffix.Left - originalLabel.Width - 8, 18)
                    _switchImageOriginal.Location = New Point(originalLabel.Left - _switchImageOriginal.Width - 8, 24)
                    _lblImageOutput.Width = Math.Max(130, _switchImageOriginal.Left - _lblImageOutput.Left - 12)

                    _btnImageStart.Location = New Point(14, 17)
                    _switchImagePng.Location = New Point(202, 24)
                    pngLabel.Location = New Point(_switchImagePng.Right + 10, 17)
                    pngHint.Location = New Point(pngLabel.Right, 17)
                    Dim progressLeft = Math.Max(470, CInt(rowWidth * 0.58))
                    pngHint.Width = Math.Max(0, progressLeft - pngHint.Left - 12)
                    pngHint.Visible = pngHint.Width >= 90
                    Dim progressWidth = Math.Max(145, Math.Min(280, CInt(rowWidth * 0.2)))
                    _imageProgress.SetBounds(progressLeft, 31, progressWidth, 10)
                    _lblImageProgress.SetBounds(_imageProgress.Right + 14, 10,
                                                Math.Max(110, rowWidth - _imageProgress.Right - 26), 52)
                End Sub
            AddHandler section.Resize, Sub(sender, e) arrange()
            RefreshImageOutputLabel()
            arrange()
            Return section
        End Function

        Private Sub BuildUpscalePageLegacy()
            _pageUpscale.Dock = DockStyle.Fill
            _pageUpscale.BackColor = Color.Transparent
            _pageUpscale.AutoScroll = True
            ' 给页签标题与插件总开关之间留出明确的呼吸空间；其余控件相对间距保持不变。
            _pageUpscale.Padding = New Padding(0, 22, 0, 0)

            ' 行内 Dock.Left 从右往左排列：先添加右侧标签，最后添加开关（最左）。
            ' 整页 Dock.Top 反序添加：最后添加的行排在最上。

            ' ── 说明 + exe 路径（放回超分主界面；先添加 → 排在最下）──
            Dim sectionHint As New Panel() With {.Dock = DockStyle.Fill, .BackColor = Color.Transparent, .Padding = New Padding(2, 2, 0, 0)}
            _lblAdvancedHint.AutoSize = False
            _lblAdvancedHint.Dock = DockStyle.Fill
            _lblAdvancedHint.TextAlign = HtmlColorLabel.TextAlignEnum.TopLeft
            _lblAdvancedHint.LineSpacing = 4
            _lblAdvancedHint.Text = "<font color=#9A9A9A><b>说明</b></font><br/>" &
                "<font color=#8A8A8A>「插件总开关」仅作用于「超分主界面」页：开启后，加入编码队列的命令会被 videoenhancer.exe 中转执行 AI 超分/补帧。</font><br/>" &
                "<font color=#8A8A8A>「实时预览」与队列监控即使关闭插件总开关也能使用。超分开关右边选择图片超分模型，开关关闭也可以使用。</font><br/>" &
                "<font color=#8A8A8A>CLI 程序启动时读取本目录 videoenhancer.ini 的 core-path，并校验 bin\ffmpeg、python 库与模型库。</font>"
            sectionHint.Controls.Add(_lblAdvancedHint)

            Dim sectionExe As New Panel() With {.Dock = DockStyle.Top, .Height = 44, .BackColor = Color.Transparent, .Padding = New Padding(0, 8, 0, 0)}
            _btnPickExe.Text = "更改路径"
            _btnPickExe.Size = New Size(110, 32)
            _btnPickExe.Dock = DockStyle.Right
            _btnPickExe.BorderRadius = 8
            _btnPickExe.BorderSize = 0
            _btnPickExe.BackColor1 = Color.FromArgb(40, 220, 220, 220)
            _btnPickExe.HoverBackColor1 = Color.FromArgb(60, 220, 220, 220)
            AddHandler _btnPickExe.Click, AddressOf OnPickExeClick
            sectionExe.Controls.Add(_btnPickExe)
            _lblExe.AutoSize = True
            _lblExe.Dock = DockStyle.Fill
            _lblExe.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _lblExe.ForeColor = Color.Gainsboro
            sectionExe.Controls.Add(_lblExe)
            Dim footer As New Panel() With {.Dock = DockStyle.Top, .Height = 130, .BackColor = Color.Transparent}
            footer.Controls.Add(sectionHint)
            footer.Controls.Add(sectionExe)
            _pageUpscale.Controls.Add(footer)

            ' 图片超分位于补帧倍率下方；模型与推理方式直接借用上方选择。
            Dim imageSection = BuildImageUpscaleSectionLegacy()

            ' ── 因子行（补帧倍率）：先添加 → 排在最下 ──
            Dim sectionFactor As New Panel() With {.Dock = DockStyle.Top, .Height = 56, .BackColor = Color.Transparent, .Padding = New Padding(0, 10, 0, 0)}
            Dim rowFactor As New Panel() With {.Dock = DockStyle.Top, .Height = 40, .BackColor = Color.Transparent}
            sectionFactor.Controls.Add(rowFactor)
            _lblFactor.Text = "<font color=#D8D8D8>补帧倍率</font>"
            _lblFactor.AutoSize = False
            _lblFactor.Size = New Size(110, 40)
            _lblFactor.Location = New Point(199, 0)
            _lblFactor.Anchor = AnchorStyles.Left Or AnchorStyles.Top
            _lblFactor.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            rowFactor.Controls.Add(_lblFactor)
            _cmbFactor.Location = New Point(337, 0)
            _cmbFactor.Size = New Size(90, 40)
            _cmbFactor.Anchor = AnchorStyles.Left Or AnchorStyles.Top
            _cmbFactor.WaterText = "补帧倍率…"
            _cmbFactor.BorderRadius = 8
            _cmbFactor.BorderSize = 1
            _cmbFactor.Items.Add("2 倍")
            _cmbFactor.Items.Add("3 倍")
            _cmbFactor.Items.Add("4 倍")
            _cmbFactor.Items.Add("8 倍")
            AddHandler _cmbFactor.SelectedIndexChanged, AddressOf OnFactorSelected
            rowFactor.Controls.Add(_cmbFactor)
            _pageUpscale.Controls.Add(sectionFactor)

            ' ── 补帧行：补帧开关 + 补帧模型 ──
            Dim sectionInterp As New Panel() With {.Dock = DockStyle.Top, .Height = 56, .BackColor = Color.Transparent, .Padding = New Padding(0, 10, 0, 0)}
            Dim rowInterp As New Panel() With {.Dock = DockStyle.Top, .Height = 40, .BackColor = Color.Transparent}
            sectionInterp.Controls.Add(rowInterp)
            Dim lblInterpModel As New HtmlColorLabel() With {
                .Text = "<font color=#D8D8D8>补帧模型</font>",
                .AutoSize = False,
                .Size = New Size(110, 40),
                .Location = New Point(201, 0),
                .Anchor = AnchorStyles.Left Or AnchorStyles.Top,
                .TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            }
            rowInterp.Controls.Add(lblInterpModel)
            _lblSwitchInterp.Text = "<font color=#E8E8E8><b>补帧开关</b></font>"
            _lblSwitchInterp.AutoSize = False
            _lblSwitchInterp.Size = New Size(120, 40)
            _lblSwitchInterp.Padding = New Padding(14, 0, 0, 0)
            _lblSwitchInterp.Dock = DockStyle.Left
            _lblSwitchInterp.ForeColor = Color.Gainsboro
            _lblSwitchInterp.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            rowInterp.Controls.Add(_lblSwitchInterp)
            _switchInterp.Dock = DockStyle.Left
            ConfigureDpiSwitch(_switchInterp)
            _switchInterp.TrackColorOn = Color.FromArgb(80, 200, 120)
            _switchInterp.TrackColorOff = Color.FromArgb(90, 90, 100)
            _switchInterp.KnobColor = Color.FromArgb(235, 235, 235)
            _switchInterp.Checked = _config.InterpEnabled
            _switchInterp.Enabled = _config.Enabled
            AddHandler _switchInterp.CheckedChanged, AddressOf OnInterpSwitchChanged
            rowInterp.Controls.Add(_switchInterp)
            _cmbInterp.Dock = DockStyle.None
            _cmbInterp.Location = New Point(337, 0)
            _cmbInterp.Size = New Size(300, 40)
            _cmbInterp.Anchor = AnchorStyles.Left Or AnchorStyles.Top
            _cmbInterp.WaterText = "点击选择补帧模型…"
            _cmbInterp.BorderRadius = 8
            _cmbInterp.BorderSize = 1
            AddHandler _cmbInterp.DropDownOpened, AddressOf OnInterpDropDownOpened
            AddHandler _cmbInterp.Click, AddressOf OnInterpComboClicked
            AddHandler _cmbInterp.SelectedIndexChanged, AddressOf OnInterpModelSelected
            rowInterp.Controls.Add(_cmbInterp)
            _pageUpscale.Controls.Add(sectionInterp)

            ' ── 模型行（放大模型，设计器 pnlBackend 高 50）──
            Dim sectionModel As New Panel() With {.Dock = DockStyle.Top, .Height = 50, .BackColor = Color.Transparent, .Padding = New Padding(0, 8, 0, 0)}
            Dim rowModel As New Panel() With {.Dock = DockStyle.Top, .Height = 40, .BackColor = Color.Transparent}
            sectionModel.Controls.Add(rowModel)
            Dim lblUpscaleModel As New HtmlColorLabel() With {
                .Text = "<font color=#D8D8D8>放大模型</font>",
                .AutoSize = False,
                .Size = New Size(110, 40),
                .Location = New Point(201, 0),
                .Anchor = AnchorStyles.Left Or AnchorStyles.Top,
                .TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            }
            rowModel.Controls.Add(lblUpscaleModel)
            _cmbModel.Dock = DockStyle.None
            _cmbModel.Location = New Point(337, 0)
            _cmbModel.Size = New Size(456, 40)
            _cmbModel.Anchor = AnchorStyles.Left Or AnchorStyles.Top
            _cmbModel.WaterText = "点击选择放大模型…"
            _cmbModel.BorderRadius = 8
            _cmbModel.BorderSize = 1
            AddHandler _cmbModel.DropDownOpened, AddressOf OnModelDropDownOpened
            AddHandler _cmbModel.Click, AddressOf OnModelComboClicked
            AddHandler _cmbModel.SelectedIndexChanged, AddressOf OnModelSelected
            rowModel.Controls.Add(_cmbModel)
            _pageUpscale.Controls.Add(sectionModel)

            ' ── 超分行：超分开关 + 选择推理方式 ──
            Dim sectionUpscale As New Panel() With {.Dock = DockStyle.Top, .Height = 56, .BackColor = Color.Transparent, .Padding = New Padding(0, 10, 0, 0)}
            Dim rowUpscale As New Panel() With {.Dock = DockStyle.Top, .Height = 40, .BackColor = Color.Transparent}
            sectionUpscale.Controls.Add(rowUpscale)
            _lblBackend.Text = "<font color=#D8D8D8>选择推理方式</font>"
            _lblBackend.AutoSize = False
            _lblBackend.Size = New Size(130, 40)
            _lblBackend.Location = New Point(201, 0)
            _lblBackend.Anchor = AnchorStyles.Left Or AnchorStyles.Top
            _lblBackend.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            rowUpscale.Controls.Add(_lblBackend)
            _cmbBackend.Dock = DockStyle.None
            _cmbBackend.Location = New Point(337, 0)
            _cmbBackend.Size = New Size(220, 36)
            _cmbBackend.Anchor = AnchorStyles.Left Or AnchorStyles.Top
            _cmbBackend.WaterText = "选择推理方式…"
            _cmbBackend.BorderRadius = 8
            _cmbBackend.BorderSize = 1
            _cmbBackend.Items.Add("NCNN (Vulkan)")
            _cmbBackend.Items.Add("CUDA (PyTorch)")
            _cmbBackend.Items.Add("TensorRT (NVIDIA)")
            _cmbBackend.Items.Add("ONNX Runtime")
            _cmbBackend.Items.Add("FlashVSR (NVIDIA · 视频)")
            AddHandler _cmbBackend.SelectedIndexChanged, AddressOf OnBackendSelected
            rowUpscale.Controls.Add(_cmbBackend)
            _lblSwitch.Text = "<font color=#E8E8E8><b>超分开关</b></font>"
            _lblSwitch.AutoSize = False
            _lblSwitch.Size = New Size(120, 40)
            _lblSwitch.Padding = New Padding(14, 0, 0, 0)
            _lblSwitch.Dock = DockStyle.Left
            _lblSwitch.ForeColor = Color.Gainsboro
            _lblSwitch.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            rowUpscale.Controls.Add(_lblSwitch)
            _switchUpscale.Dock = DockStyle.Left
            ConfigureDpiSwitch(_switchUpscale)
            _switchUpscale.TrackColorOn = Color.FromArgb(80, 200, 120)
            _switchUpscale.TrackColorOff = Color.FromArgb(90, 90, 100)
            _switchUpscale.KnobColor = Color.FromArgb(235, 235, 235)
            _switchUpscale.Checked = _config.UpscaleEnabled
            _switchUpscale.Enabled = _config.Enabled
            AddHandler _switchUpscale.CheckedChanged, AddressOf OnUpscaleSwitchChanged
            rowUpscale.Controls.Add(_switchUpscale)
            _pageUpscale.Controls.Add(sectionUpscale)

            ' ── 插件总开关（最后添加 → 排在最上）──
            Dim sectionMaster As New Panel() With {.Dock = DockStyle.Top, .Height = 50, .BackColor = Color.Transparent, .Padding = New Padding(0, 10, 0, 0)}
            _lblMaster.Text = "<font color=#F2F2F2><b>插件总开关</b></font>  <font color=#B8B8B8>关闭此开关时，超分主页面功能不生效</font>"
            _lblMaster.AutoSize = False
            _lblMaster.Size = New Size(589, 40)
            _lblMaster.Padding = New Padding(14, 0, 0, 0)
            _lblMaster.Dock = DockStyle.Left
            _lblMaster.ForeColor = Color.White
            _lblMaster.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            sectionMaster.Controls.Add(_lblMaster)
            _switchMaster.Dock = DockStyle.Left
            ConfigureDpiSwitch(_switchMaster)
            _switchMaster.TrackColorOn = Color.FromArgb(80, 200, 120)
            _switchMaster.TrackColorOff = Color.FromArgb(90, 90, 100)
            _switchMaster.KnobColor = Color.FromArgb(235, 235, 235)
            _switchMaster.Checked = _config.Enabled
            AddHandler _switchMaster.CheckedChanged, AddressOf OnMasterSwitchChanged
            sectionMaster.Controls.Add(_switchMaster)
            _pageUpscale.Controls.Add(sectionMaster)

            ' 图片区占用固定设置区与底部说明之间的全部剩余高度；窗口较小时保留滚动能力。
            Dim resizeImageSection As Action =
                Sub()
                    Dim fixedHeight = 50 + 56 + 50 + 56 + 56
                    Dim available = _pageUpscale.ClientSize.Height - _pageUpscale.Padding.Vertical - fixedHeight - footer.Height
                    imageSection.Height = Math.Max(330, available)
                End Sub
            AddHandler _pageUpscale.Resize, Sub(sender, e) resizeImageSection()
            resizeImageSection()
        End Sub

        Private Function BuildImageUpscaleSectionLegacy() As Panel
            Dim section As New FluentCardPanel() With {
                .Dock = DockStyle.Top, .Height = 330, .FillColor = Color.FromArgb(43, 43, 43),
                .StrokeColor = Color.FromArgb(62, 62, 62), .CornerRadius = 12,
                .Padding = New Padding(18), .AllowDrop = True
            }
            AddHandler section.DragEnter, AddressOf OnImageDragEnter
            AddHandler section.DragDrop, AddressOf OnImageDragDrop

            Dim title As New Label() With {
                .Text = "图片超分", .Location = New Point(20, 14), .Size = New Size(880, 30),
                .ForeColor = Color.White, .BackColor = Color.Transparent,
                .Font = New Font("Microsoft YaHei UI", 12.0F, FontStyle.Bold),
                .TextAlign = ContentAlignment.MiddleLeft
            }
            section.Controls.Add(title)

            Dim subtitle As New Label() With {
                .Text = "借用上方超分模型和推理方式，可处理单张图片或递归文件夹。",
                .Location = New Point(20, 44), .Size = New Size(880, 24),
                .ForeColor = Color.FromArgb(170, 170, 170), .BackColor = Color.Transparent,
                .Font = New Font("Microsoft YaHei UI", 9.0F), .TextAlign = ContentAlignment.MiddleLeft
            }
            section.Controls.Add(subtitle)

            Dim inputRow As New FluentCardPanel() With {
                .Location = New Point(20, 76), .Size = New Size(900, 50),
                .Anchor = AnchorStyles.Left Or AnchorStyles.Top Or AnchorStyles.Right,
                .FillColor = Color.FromArgb(51, 51, 51), .StrokeColor = Color.FromArgb(68, 68, 68), .CornerRadius = 8
            }
            ConfigureImageButton(_btnImageFiles, "选择或拖入文件", 185)
            ConfigureImageButton(_btnImageFolder, "选择文件夹及其子目录", 232)
            _btnImageFiles.Location = New Point(8, 9)
            _btnImageFolder.Location = New Point(205, 9)
            AddHandler _btnImageFiles.Click, AddressOf OnPickImageFiles
            AddHandler _btnImageFolder.Click, AddressOf OnPickImageFolder
            _lblImageInputs.Location = New Point(449, 9)
            _lblImageInputs.Size = New Size(435, 32)
            _lblImageInputs.AutoSize = False
            _lblImageInputs.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _lblImageInputs.Text = "<font color=#999999>尚未选择图片</font>"
            inputRow.Controls.AddRange(New Control() {_btnImageFiles, _btnImageFolder, _lblImageInputs})
            section.Controls.Add(inputRow)

            Dim outputRow As New FluentCardPanel() With {
                .Location = New Point(20, 136), .Size = New Size(900, 50),
                .Anchor = AnchorStyles.Left Or AnchorStyles.Top Or AnchorStyles.Right,
                .FillColor = Color.FromArgb(51, 51, 51), .StrokeColor = Color.FromArgb(68, 68, 68), .CornerRadius = 8
            }
            ConfigureImageButton(_btnImageOutput, "指定输出文件夹", 185)
            _btnImageOutput.Location = New Point(8, 9)
            AddHandler _btnImageOutput.Click, AddressOf OnPickImageOutput
            _lblImageOutput.Location = New Point(205, 9)
            _lblImageOutput.Size = New Size(300, 32)
            _lblImageOutput.AutoSize = False
            _lblImageOutput.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _switchImageOriginal.Location = New Point(513, 13)
            ConfigureDpiSwitch(_switchImageOriginal)
            _switchImageOriginal.Checked = _config.ImageOutputOriginal
            AddHandler _switchImageOriginal.CheckedChanged, AddressOf OnImageOriginalChanged
            Dim originalLabel As New Label() With {.Text = "输出到原目录，附加", .ForeColor = Color.Gainsboro, .BackColor = Color.Transparent, .Location = New Point(568, 10), .Size = New Size(170, 30), .TextAlign = ContentAlignment.MiddleLeft}
            _cmbImageSuffix.Location = New Point(738, 9)
            _cmbImageSuffix.Size = New Size(160, 32)
            _cmbImageSuffix.Items.Add("处理时间戳")
            _cmbImageSuffix.Items.Add("模型名称")
            _cmbImageSuffix.SelectedIndex = If(String.Equals(_config.ImageSuffix, "model", StringComparison.OrdinalIgnoreCase), 1, 0)
            AddHandler _cmbImageSuffix.SelectedIndexChanged, AddressOf OnImageSuffixChanged
            outputRow.Controls.AddRange(New Control() {_btnImageOutput, _lblImageOutput, _switchImageOriginal, originalLabel, _cmbImageSuffix})
            section.Controls.Add(outputRow)

            Dim actionRow As New FluentCardPanel() With {
                .Location = New Point(20, 196), .Size = New Size(900, 50),
                .Anchor = AnchorStyles.Left Or AnchorStyles.Top Or AnchorStyles.Right,
                .FillColor = Color.FromArgb(51, 51, 51), .StrokeColor = Color.FromArgb(68, 68, 68), .CornerRadius = 8
            }
            ConfigureImageButton(_btnImageStart, "开始处理", 185)
            _btnImageStart.Location = New Point(8, 9)
            _btnImageStart.BackColor1 = Color.FromArgb(0, 120, 212)
            _btnImageStart.HoverBackColor1 = Color.FromArgb(17, 94, 163)
            _btnImageStart.ForeColor = Color.White
            AddHandler _btnImageStart.Click, AddressOf OnStartImageProcessing
            _switchImagePng.Location = New Point(208, 13)
            ConfigureDpiSwitch(_switchImagePng)
            _switchImagePng.Checked = _config.ImagePng
            AddHandler _switchImagePng.CheckedChanged, AddressOf OnImagePngChanged
            Dim pngLabel As New Label() With {.Text = "处理为 PNG 格式", .ForeColor = Color.Gainsboro, .BackColor = Color.Transparent, .Location = New Point(263, 10), .Size = New Size(150, 30), .TextAlign = ContentAlignment.MiddleLeft}
            Dim pngHint As New Label() With {.Text = "开启后统一输出为无损 PNG；关闭时输出源格式", .ForeColor = Color.FromArgb(160, 160, 160), .BackColor = Color.Transparent, .Location = New Point(413, 10), .Size = New Size(477, 30), .TextAlign = ContentAlignment.MiddleLeft}
            actionRow.Controls.AddRange(New Control() {_btnImageStart, _switchImagePng, pngLabel, pngHint})
            section.Controls.Add(actionRow)

            Dim progressRow As New FluentCardPanel() With {
                .Location = New Point(20, 256), .Size = New Size(900, 50),
                .Anchor = AnchorStyles.Left Or AnchorStyles.Top Or AnchorStyles.Right,
                .FillColor = Color.FromArgb(47, 47, 47), .StrokeColor = Color.FromArgb(68, 68, 68), .CornerRadius = 8
            }
            _imageProgress.Location = New Point(8, 16)
            _imageProgress.Size = New Size(420, 18)
            _imageProgress.Minimum = 0
            _imageProgress.Maximum = 1000
            _lblImageProgress.Location = New Point(443, 7)
            _lblImageProgress.Size = New Size(445, 38)
            _lblImageProgress.AutoSize = False
            _lblImageProgress.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _lblImageProgress.Text = "<font color=#999999>处理进度 / ETA：等待开始</font>"
            progressRow.Controls.AddRange(New Control() {_imageProgress, _lblImageProgress})
            section.Controls.Add(progressRow)

            ' 水平方向利用完整可用宽度；左边界保持不动。各按钮基准宽度较旧布局增加约 30%。
            ' 垂直方向把四行平均铺开，避免集中挤在模块顶部。
            Dim arrange As Action =
                Sub()
                    Dim rowWidth = Math.Max(900, section.ClientSize.Width - 40)
                    inputRow.Width = rowWidth
                    outputRow.Width = rowWidth
                    actionRow.Width = rowWidth
                    progressRow.Width = rowWidth

                    _lblImageInputs.Width = Math.Max(220, rowWidth - _lblImageInputs.Left - 8)

                    Dim suffixWidth = Math.Max(150, Math.Min(190, CInt(rowWidth * 0.17)))
                    _cmbImageSuffix.Width = suffixWidth
                    _cmbImageSuffix.Left = rowWidth - suffixWidth
                    originalLabel.Width = 170
                    originalLabel.Left = _cmbImageSuffix.Left - originalLabel.Width
                    _switchImageOriginal.Left = originalLabel.Left - _switchImageOriginal.Width - 10
                    _lblImageOutput.Width = Math.Max(180, _switchImageOriginal.Left - _lblImageOutput.Left - 10)

                    pngHint.Width = Math.Max(220, rowWidth - pngHint.Left - 8)
                    _imageProgress.Width = Math.Max(360, CInt(rowWidth * 0.55))
                    _lblImageProgress.Left = _imageProgress.Right + 15
                    _lblImageProgress.Width = Math.Max(220, rowWidth - _lblImageProgress.Left)

                    Dim usable = Math.Max(230, section.ClientSize.Height - 86)
                    Dim stepY = Math.Max(58, usable \ 4)
                    inputRow.Top = 76
                    outputRow.Top = inputRow.Top + stepY
                    actionRow.Top = outputRow.Top + stepY
                    progressRow.Top = Math.Min(section.ClientSize.Height - progressRow.Height - 14, actionRow.Top + stepY)
                End Sub
            AddHandler section.Resize, Sub(sender, e) arrange()

            _pageUpscale.Controls.Add(section)
            RefreshImageOutputLabel()
            arrange()
            Return section
        End Function

        ''' <summary>BooleanSwitch 按宿主窗口的实际 DPI 重新计算尺寸（96 DPI 基准为 38×20）。</summary>
        Private Shared Sub ConfigureDpiSwitch(switchControl As LakeUI.BooleanSwitch)
            switchControl.TrackColorOn = UiAccent
            switchControl.HoverTrackColorOn = UiAccentHover
            switchControl.PressedTrackColorOn = UiAccentPressed
            switchControl.TrackColorOff = Color.FromArgb(63, 73, 86)
            switchControl.HoverTrackColorOff = Color.FromArgb(76, 88, 103)
            switchControl.PressedTrackColorOff = Color.FromArgb(52, 62, 74)
            switchControl.KnobColor = Color.FromArgb(245, 248, 251)
            switchControl.HoverKnobColor = Color.White
            switchControl.PressedKnobColor = Color.FromArgb(225, 232, 240)
            switchControl.BorderColor = Color.Transparent
            switchControl.BorderSize = 0
            Dim applySize As Action =
                Sub()
                    Dim dpi = 96
                    If switchControl.FindForm() IsNot Nothing Then
                        dpi = switchControl.FindForm().DeviceDpi
                    ElseIf switchControl.IsHandleCreated Then
                        dpi = switchControl.DeviceDpi
                    End If
                    Dim scale = Math.Max(1.0F, CSng(dpi) / 96.0F)
                    switchControl.Size = New Size(CInt(Math.Round(38 * scale)), CInt(Math.Round(20 * scale)))
                End Sub
            AddHandler switchControl.HandleCreated, Sub(sender, e) applySize()
            AddHandler switchControl.DpiChangedAfterParent, Sub(sender, e) applySize()
            AddHandler switchControl.ParentChanged, Sub(sender, e) applySize()
            applySize()
        End Sub

        Private Shared Sub ConfigureImageButton(button As ModernButton, text As String, width As Integer)
            button.Text = text
            button.Size = New Size(width, 36)
            ConfigureSecondaryButton(button)
        End Sub

        Private Sub OnPickImageFiles(sender As Object, e As EventArgs)
            Using dialog As New OpenFileDialog With {
                .Title = "选择要超分的图片", .Multiselect = True,
                .Filter = "图片文件|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.tif;*.tiff;*.avif|所有文件|*.*"
            }
                If dialog.ShowDialog() = DialogResult.OK Then AddImagePaths(dialog.FileNames)
            End Using
        End Sub

        Private Sub OnPickImageFolder(sender As Object, e As EventArgs)
            Using dialog As New FolderBrowserDialog With {.Description = "选择图片文件夹（将递归处理子目录）", .ShowNewFolderButton = False}
                If dialog.ShowDialog() = DialogResult.OK Then AddImagePaths(New String() {dialog.SelectedPath})
            End Using
        End Sub

        Private Sub OnPickImageOutput(sender As Object, e As EventArgs)
            Using dialog As New FolderBrowserDialog With {.Description = "选择图片输出文件夹", .ShowNewFolderButton = True}
                Dim currentOutput = _txtImageOutput.Text.Trim()
                If Directory.Exists(currentOutput) Then dialog.SelectedPath = currentOutput
                If dialog.ShowDialog() = DialogResult.OK Then
                    _txtImageOutput.Text = dialog.SelectedPath
                End If
            End Using
        End Sub

        Private Sub OnImageOutputTextChanged(sender As Object, e As EventArgs)
            Dim outputPath = _txtImageOutput.Text.Trim()
            _config.ImageOutput = outputPath
            _config.ImageOutputOriginal = String.IsNullOrWhiteSpace(outputPath)
            _config.Save()
        End Sub

        Private Sub OnImageDragEnter(sender As Object, e As DragEventArgs)
            If e.Data.GetDataPresent(DataFormats.FileDrop) Then e.Effect = DragDropEffects.Copy
        End Sub

        Private Sub OnImageDragDrop(sender As Object, e As DragEventArgs)
            Dim paths = TryCast(e.Data.GetData(DataFormats.FileDrop), String())
            If paths IsNot Nothing Then AddImagePaths(paths)
        End Sub

        Private Sub AddImagePaths(paths As IEnumerable(Of String))
            Dim supported = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tif", ".tiff", ".avif"}
            For Each path In paths
                If Directory.Exists(path) Then
                    If Not _imageFolders.Contains(path, StringComparer.OrdinalIgnoreCase) Then _imageFolders.Add(path)
                ElseIf File.Exists(path) AndAlso supported.Contains(IO.Path.GetExtension(path)) Then
                    If Not _imageFiles.Contains(path, StringComparer.OrdinalIgnoreCase) Then _imageFiles.Add(path)
                End If
            Next
            _lblImageInputs.Text = "<font color=#DCDCDC>已选择 " & _imageFiles.Count & " 个文件、" & _imageFolders.Count & " 个递归文件夹</font>"
        End Sub

        Private Sub OnImageOriginalChanged(sender As Object, e As EventArgs)
            _config.ImageOutputOriginal = _switchImageOriginal.Checked
            _config.Save()
            RefreshImageOutputLabel()
        End Sub

        Private Sub OnImagePngChanged(sender As Object, e As EventArgs)
            _config.ImagePng = _switchImagePng.Checked
            _config.Save()
        End Sub

        Private Sub OnImageSuffixChanged(sender As Object, e As EventArgs)
            _config.ImageSuffix = If(_cmbImageSuffix.SelectedIndex = 1, "model", "timestamp")
            _config.Save()
        End Sub

        Private Sub OnImageFormatChanged(sender As Object, e As EventArgs)
            _config.ImagePng = _cmbImageFormat.SelectedIndex <> 1
            _config.Save()
        End Sub

        Private Sub RefreshImageOutputLabel()
            _btnImageOutput.Enabled = Not _switchImageOriginal.Checked
            Dim text = If(_switchImageOriginal.Checked, "原图片所在目录", If(String.IsNullOrWhiteSpace(_config.ImageOutput), "尚未指定输出文件夹", _config.ImageOutput))
            _lblImageOutput.Text = If(String.IsNullOrWhiteSpace(_config.ImageOutput) AndAlso Not _switchImageOriginal.Checked,
                "<font color=#888888>" & EscapeHtml(text) & "</font>",
                "<font color=#DCDCDC>" & EscapeHtml(text) & "</font>")
        End Sub

        Private Sub OnStartImageProcessing(sender As Object, e As EventArgs)
            If _imageRunning Then Return
            If _config.Backend = "flashvsr" Then
                ShowStatus("FlashVSR 是连续视频帧模型，图片超分请选择 NCNN、CUDA、TensorRT 或 ONNX。", True)
                Return
            End If
            If _imageFiles.Count = 0 AndAlso _imageFolders.Count = 0 Then
                ShowStatus("请先选择或拖入图片/文件夹", True) : Return
            End If
            If Not File.Exists(_config.ExePath) Then
                ShowStatus("请先指定有效的 videoenhancer.exe", True) : Return
            End If
            If String.IsNullOrWhiteSpace(_config.Model) Then
                ShowStatus("请先在上方选择放大模型", True) : Return
            End If
            Dim outputPath = _txtImageOutput.Text.Trim()
            _config.ImageOutput = outputPath
            _config.ImageOutputOriginal = String.IsNullOrWhiteSpace(outputPath)
            _config.ImageSuffix = If(_cmbImageSuffix.SelectedIndex = 1, "model", "timestamp")
            _config.ImagePng = _cmbImageFormat.SelectedIndex <> 1
            _config.Save()

            Dim args As New List(Of String)()
            For Each path In _imageFiles : args.Add("--image-input") : args.Add(path) : Next
            For Each path In _imageFolders : args.Add("--image-folder") : args.Add(path) : Next
            If String.IsNullOrWhiteSpace(outputPath) Then
                args.Add("--image-output-original")
            Else
                args.Add("--image-output") : args.Add(outputPath)
            End If
            args.Add("--image-suffix") : args.Add(_config.ImageSuffix)
            args.Add(If(_config.ImagePng, "--image-png", "--image-source-format"))
            args.Add("-backend") : args.Add(_config.Backend)
            args.Add("-modelpath") : args.Add(_config.Model)

            Dim psi As New ProcessStartInfo With {
                .FileName = _config.ExePath, .WorkingDirectory = Path.GetDirectoryName(_config.ExePath),
                .UseShellExecute = False, .CreateNoWindow = True,
                .RedirectStandardOutput = True, .RedirectStandardError = True,
                .StandardOutputEncoding = Encoding.UTF8, .StandardErrorEncoding = Encoding.UTF8,
                .Arguments = String.Join(" ", args.Select(Function(value) QuoteCommandArgument(value)))
            }
            _imageProcess = New Process With {.StartInfo = psi, .EnableRaisingEvents = True}
            Dim errors As New StringBuilder()
            AddHandler _imageProcess.OutputDataReceived, Sub(s, ev) If ev.Data IsNot Nothing Then HandleImageProgressLine(ev.Data)
            AddHandler _imageProcess.ErrorDataReceived, Sub(s, ev) If ev.Data IsNot Nothing Then SyncLock errors : errors.AppendLine(ev.Data) : End SyncLock
            _imageRunning = True
            _imageCompleteReceived = False
            _btnImageStart.Enabled = False
            _imageProgress.Value = 0
            _lblImageProgress.Text = "<font color=#D8D8D8>正在加载模型…</font>"
            Try
                _imageProcess.Start()
                _imageProcess.BeginOutputReadLine()
                _imageProcess.BeginErrorReadLine()
                Task.Run(Sub()
                    _imageProcess.WaitForExit()
                    Dim code = _imageProcess.ExitCode
                    Dim errorText As String
                    SyncLock errors : errorText = errors.ToString() : End SyncLock
                    If IsHandleCreated Then BeginInvoke(New Action(Sub()
                        _imageRunning = False
                        _btnImageStart.Enabled = True
                        If code = 0 OrElse _imageCompleteReceived Then
                            _imageProgress.Value = 1000
                            _lblImageProgress.Text = "<font color=#96D2A0>处理完成</font>"
                        Else
                            _lblImageProgress.Text = "<font color=#E07878>处理失败：" & EscapeHtml(LastNonEmptyLine(errorText)) & "</font>"
                        End If
                    End Sub))
                End Sub)
            Catch ex As Exception
                _imageRunning = False
                _btnImageStart.Enabled = True
                _lblImageProgress.Text = "<font color=#E07878>启动失败：" & EscapeHtml(ex.Message) & "</font>"
            End Try
        End Sub

        Private Sub HandleImageProgressLine(line As String)
            If line.StartsWith("IMAGE_COMPLETE|", StringComparison.Ordinal) Then
                _imageCompleteReceived = True
                Return
            End If
            If Not line.StartsWith("IMAGE_PROGRESS|", StringComparison.Ordinal) Then Return
            Dim parts = line.Split("|"c)
            If parts.Length < 6 Then Return
            Dim current, total As Integer
            Dim elapsed, eta As Double
            If Not Integer.TryParse(parts(1), current) OrElse Not Integer.TryParse(parts(2), total) OrElse total <= 0 Then Return
            Double.TryParse(parts(3), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, elapsed)
            Double.TryParse(parts(4), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, eta)
            If IsHandleCreated Then BeginInvoke(New Action(Sub()
                _imageProgress.Value = Math.Max(0, Math.Min(1000, CInt(current * 1000.0 / total)))
                _lblImageProgress.Text = "<font color=#D8D8D8>" & current & "/" & total & "　已用 " & FormatDuration(elapsed) & "　ETA " & FormatDuration(eta) & "</font>"
            End Sub))
        End Sub

        Private Shared Function FormatDuration(seconds As Double) As String
            Dim value = TimeSpan.FromSeconds(Math.Max(0, seconds))
            Return value.ToString(If(value.TotalHours >= 1, "hh\:mm\:ss", "mm\:ss"))
        End Function

        Private Shared Function QuoteCommandArgument(value As String) As String
            If value Is Nothing Then value = ""
            Return """" & value.Replace(""""c, "\""") & """"
        End Function

        ' ────────────────────────── 实时预览页 ──────────────────────────

        Private Sub BuildOfficialPreviewPage()
            _pagePreview.Dock = DockStyle.Fill
            _pagePreview.BackColor = Color.Transparent
            _pagePreview.Padding = New Padding(0, 8, 0, 0)

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 5,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 44.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))

            _lblPreviewTitle.Text = "<span style=""font-size:13; color:Silver"">实时预览</span>   队列画面监看"
            _lblPreviewTitle.AutoSize = False
            _lblPreviewTitle.Dock = DockStyle.Fill
            _lblPreviewTitle.Margin = Padding.Empty
            _lblPreviewTitle.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            root.Controls.Add(_lblPreviewTitle, 0, 0)

            _lblPreviewStatus.Text = "<font color=#888888>等待编码队列任务…</font>"
            _lblPreviewStatus.AutoSize = False
            _lblPreviewStatus.Dock = DockStyle.Fill
            _lblPreviewStatus.Margin = New Padding(0, 4, 0, 4)
            _lblPreviewStatus.Padding = New Padding(0, 2, 0, 2)
            _lblPreviewStatus.LineSpacing = 2
            _lblPreviewStatus.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft

            Dim taskRow As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 3,
                .RowCount = 1,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            taskRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 112.0F))
            taskRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
            taskRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 360.0F))
            taskRow.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            _lblTask.Text = "<font color=#C0C0C0>预览任务</font>"
            _lblTask.AutoSize = False
            _lblTask.Dock = DockStyle.Fill
            _lblTask.Margin = Padding.Empty
            _lblTask.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _cmbTask.WaterText = "选择要预览的任务…"
            ConfigureCombo(_cmbTask)
            _cmbTask.Dock = DockStyle.Fill
            _cmbTask.Margin = New Padding(0, 5, 0, 5)
            AddHandler _cmbTask.SelectedIndexChanged, AddressOf OnTaskSelected
            Dim taskHint = CreateOfficialCaption("可查看处理中或已经完成的帧")
            taskHint.TextAlign = ContentAlignment.MiddleLeft
            taskHint.Margin = New Padding(16, 0, 0, 0)
            taskRow.Controls.Add(_lblTask, 0, 0)
            taskRow.Controls.Add(_cmbTask, 1, 0)
            taskRow.Controls.Add(taskHint, 2, 0)
            root.Controls.Add(taskRow, 0, 1)
            root.Controls.Add(_lblPreviewStatus, 0, 2)

            Dim previewSurface As New ModernPanel With {
                .Dock = DockStyle.Fill,
                .Margin = New Padding(0, 4, 0, 4),
                .Padding = New Padding(1),
                .BackColor = Color.Transparent,
                .BackColor1 = Color.FromArgb(16, 16, 18),
                .BorderColor = Color.FromArgb(55, 55, 55),
                .BorderSize = 1,
                .BorderRadius = 0
            }
            _picPreview.Dock = DockStyle.Fill
            _picPreview.BackColor = Color.FromArgb(16, 16, 18)
            _picPreview.SizeMode = PictureBoxSizeMode.Zoom
            previewSurface.Controls.Add(_picPreview)
            root.Controls.Add(previewSurface, 0, 3)

            Dim footer As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 3,
                .RowCount = 1,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            footer.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
            footer.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 96.0F))
            footer.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 170.0F))
            footer.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            _lblPreviewNote.Text = "<font color=#888888>预览会跟随任务进度；慢速处理时短暂停顿属于正常现象。</font>"
            _lblPreviewNote.AutoSize = False
            _lblPreviewNote.Dock = DockStyle.Fill
            _lblPreviewNote.Margin = Padding.Empty
            _lblPreviewNote.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _lblRate.Text = "<font color=#C0C0C0>刷新频率</font>"
            _lblRate.AutoSize = False
            _lblRate.Dock = DockStyle.Fill
            _lblRate.Margin = Padding.Empty
            _lblRate.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleRight
            _cmbRate.WaterText = "切换频率…"
            ConfigureCombo(_cmbRate)
            _cmbRate.Items.Add("0.5 秒")
            _cmbRate.Items.Add("1 秒")
            _cmbRate.Items.Add("2 秒")
            _cmbRate.Items.Add("3 秒")
            _cmbRate.Items.Add("关键帧模式")
            _cmbRate.SelectedIndex = 1
            _cmbRate.Dock = DockStyle.Fill
            _cmbRate.Margin = New Padding(12, 5, 0, 5)
            AddHandler _cmbRate.SelectedIndexChanged, AddressOf OnRateSelected
            footer.Controls.Add(_lblPreviewNote, 0, 0)
            footer.Controls.Add(_lblRate, 1, 0)
            footer.Controls.Add(_cmbRate, 2, 0)
            root.Controls.Add(footer, 0, 4)
            _pagePreview.Controls.Add(root)
        End Sub

        Private Sub BuildOfficialAdvancedPage()
            _pageAdvanced.Dock = DockStyle.Fill
            _pageAdvanced.BackColor = Color.Transparent
            _pageAdvanced.Padding = New Padding(0, 8, 0, 0)

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 4,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 44.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 82.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 70.0F))
            root.Controls.Add(CreateOfficialSectionHeading(
                "视频对比工作室", "并排检查原片、超分、补帧和不同参数版本；全部在本机处理"), 0, 0)

            Dim steps As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 3,
                .RowCount = 1,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            For index As Integer = 0 To 2
                steps.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33.3333F))
            Next
            steps.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            Dim stepTexts = New String() {
                "01   添加 2–4 个视频",
                "02   选择对比布局",
                "03   预览并导出结果"
            }
            For index As Integer = 0 To stepTexts.Length - 1
                Dim stepPanel As New ModernPanel With {
                    .Dock = DockStyle.Fill,
                    .Margin = If(index = 0, New Padding(0, 9, 8, 9),
                                 If(index = 2, New Padding(8, 9, 0, 9), New Padding(8, 9, 8, 9))),
                    .Padding = New Padding(14, 0, 14, 0),
                    .BackColor = Color.Transparent,
                    .BackColor1 = UiSurface,
                    .BorderColor = Color.Transparent,
                    .BorderSize = 0,
                    .BorderRadius = 10
                }
                Dim stepLabel = CreateTextLabel(stepTexts(index), 9.5F, FontStyle.Regular,
                                                If(index = 0, UiAccent, UiText))
                stepLabel.Dock = DockStyle.Fill
                stepLabel.Margin = Padding.Empty
                stepPanel.Controls.Add(stepLabel)
                steps.Controls.Add(stepPanel, index, 0)
            Next
            root.Controls.Add(steps, 0, 1)

            Dim previewGrid As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 2,
                .BackColor = Color.Transparent,
                .Margin = New Padding(0, 8, 0, 8),
                .Padding = Padding.Empty
            }
            previewGrid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
            previewGrid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
            previewGrid.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))
            previewGrid.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))
            Dim previewNames = New String() {"原始画面", "方案 A", "方案 B", "方案 C"}
            For index As Integer = 0 To previewNames.Length - 1
                Dim cell As New ModernPanel With {
                    .Dock = DockStyle.Fill,
                    .Margin = New Padding(If(index Mod 2 = 0, 0, 4), If(index < 2, 0, 4),
                                          If(index Mod 2 = 0, 4, 0), If(index < 2, 4, 0)),
                    .BackColor = Color.Transparent,
                    .BackColor1 = Color.FromArgb(If(index = 0, 28, 34), If(index = 0, 28, 34), If(index = 0, 28, 34)),
                    .BorderColor = Color.Transparent,
                    .BorderSize = 0,
                    .BorderRadius = 0
                }
                Dim caption = CreateTextLabel(previewNames(index), 10.0F, FontStyle.Regular,
                                              If(index = 0, UiTextMuted, UiTextSecondary))
                caption.Dock = DockStyle.Fill
                caption.TextAlign = ContentAlignment.MiddleCenter
                cell.Controls.Add(caption)
                previewGrid.Controls.Add(cell, index Mod 2, index \ 2)
            Next
            root.Controls.Add(previewGrid, 0, 2)

            Dim footer As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 1,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            footer.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
            footer.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 230.0F))
            footer.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            Dim hint = CreateOfficialCaption("支持上下、左右、1+2 和四宫格布局，并可自定义编码器、分辨率与分割线")
            hint.TextAlign = ContentAlignment.MiddleLeft
            _btnQuad.Text = "打开对比工作室"
            _btnQuad.Dock = DockStyle.Fill
            _btnQuad.Margin = New Padding(12, 8, 0, 8)
            ConfigurePrimaryButton(_btnQuad)
            AddHandler _btnQuad.Click, AddressOf OnQuadClick
            footer.Controls.Add(hint, 0, 0)
            footer.Controls.Add(_btnQuad, 1, 0)
            root.Controls.Add(footer, 0, 3)
            _pageAdvanced.Controls.Add(root)
        End Sub

        Private Sub BuildOfficialModelDownloadPage()
            _pageDownloader.Dock = DockStyle.Fill
            _pageDownloader.BackColor = Color.Transparent
            _pageDownloader.Padding = New Padding(0, 8, 0, 0)

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 2,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            Dim header As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 1,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            header.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
            header.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 174.0F))
            header.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            header.Controls.Add(CreateOfficialSectionHeading(
                "模型资源库", "从 ModelScope 获取模型与后端组件"), 0, 0)
            _btnRefreshDownloads.Text = "刷新资源"
            _btnRefreshDownloads.Dock = DockStyle.Fill
            _btnRefreshDownloads.Margin = New Padding(12, 7, 0, 7)
            ConfigureSecondaryButton(_btnRefreshDownloads)
            AddHandler _btnRefreshDownloads.Click, Sub(sender, e) LoadDownloadModels(True)
            header.Controls.Add(_btnRefreshDownloads, 1, 0)
            root.Controls.Add(header, 0, 0)

            _downloadList.Dock = DockStyle.Fill
            _downloadList.AutoScroll = True
            _downloadList.WrapContents = False
            _downloadList.FlowDirection = FlowDirection.TopDown
            _downloadList.BackColor = Color.Transparent
            _downloadList.Margin = Padding.Empty
            _downloadList.Padding = New Padding(0, 8, 4, 4)
            AddHandler _downloadList.ClientSizeChanged,
                Sub(sender, e)
                    For Each row As Panel In _downloadList.Controls.OfType(Of Panel)()
                        row.Width = Math.Max(360, _downloadList.ClientSize.Width - 24)
                    Next
                    QueueDownloadScrollWidthReset()
                End Sub
            root.Controls.Add(_downloadList, 0, 1)
            _pageDownloader.Controls.Add(root)
        End Sub

        Private Sub BuildPreviewPage()
            _pagePreview.Dock = DockStyle.Fill
            _pagePreview.BackColor = Color.Transparent
            _pagePreview.Padding = New Padding(8, 14, 8, 10)

            ' 画面区域先加入并填满剩余空间，上下工具条随后占位。
            Dim previewFrame As New FluentCardPanel() With {
                .Dock = DockStyle.Fill, .FillColor = UiCanvas,
                .StrokeColor = UiStrokeSoft, .CornerRadius = 12,
                .Padding = New Padding(8)
            }
            _picPreview.Dock = DockStyle.Fill
            _picPreview.BackColor = Color.FromArgb(10, 13, 17)
            _picPreview.SizeMode = PictureBoxSizeMode.Zoom
            previewFrame.Controls.Add(_picPreview)
            _pagePreview.Controls.Add(previewFrame)

            Dim bottomHost As New Panel() With {
                .Dock = DockStyle.Bottom, .Height = 70, .BackColor = Color.Transparent,
                .Padding = New Padding(0, 12, 0, 0)
            }
            Dim bottomBar As New FluentCardPanel() With {
                .Dock = DockStyle.Fill, .FillColor = UiSurface,
                .StrokeColor = UiStrokeSoft, .CornerRadius = 10,
                .Padding = New Padding(14, 8, 10, 8)
            }
            _lblPreviewNote.Text = "<font color=#7E8C9D>预览会自动跟随任务进度；慢速处理时短暂停顿属于正常现象。</font>"
            _lblPreviewNote.AutoSize = False
            _lblPreviewNote.Dock = DockStyle.Fill
            _lblPreviewNote.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            bottomBar.Controls.Add(_lblPreviewNote)
            _cmbRate.Dock = DockStyle.Right
            _cmbRate.Width = 158
            _cmbRate.WaterText = "切换频率…"
            ConfigureCombo(_cmbRate)
            _cmbRate.Items.Add("0.5 秒")
            _cmbRate.Items.Add("1 秒")
            _cmbRate.Items.Add("2 秒")
            _cmbRate.Items.Add("3 秒")
            _cmbRate.Items.Add("关键帧模式")
            _cmbRate.SelectedIndex = 1
            AddHandler _cmbRate.SelectedIndexChanged, AddressOf OnRateSelected
            bottomBar.Controls.Add(_cmbRate)
            _lblRate.Text = "<font color=#B1BCCA>刷新频率</font>"
            _lblRate.AutoSize = False
            _lblRate.Dock = DockStyle.Right
            _lblRate.Width = 86
            _lblRate.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            bottomBar.Controls.Add(_lblRate)
            ' Dock.Right 依 Z 顺序布局：下拉框置于最右，标签紧邻其左侧。
            bottomBar.Controls.SetChildIndex(_cmbRate, bottomBar.Controls.Count - 1)
            bottomHost.Controls.Add(bottomBar)
            _pagePreview.Controls.Add(bottomHost)

            Dim headerHost As New Panel() With {
                .Dock = DockStyle.Top, .Height = 108, .BackColor = Color.Transparent,
                .Padding = New Padding(0, 0, 0, 12)
            }
            Dim header As New FluentCardPanel() With {
                .Dock = DockStyle.Fill, .FillColor = UiSurface,
                .StrokeColor = UiStrokeSoft, .CornerRadius = 12
            }
            Dim liveDot As Label = CreateTextLabel("●", 9.0F, FontStyle.Regular, UiSuccess)
            liveDot.Location = New Point(18, 16)
            liveDot.Size = New Size(22, 28)
            liveDot.TextAlign = ContentAlignment.MiddleCenter
            _lblPreviewTitle.Text = "<font color=#F2F6FA><b>实时预览</b></font>　<font color=#7E8C9D>队列画面监看</font>"
            _lblPreviewTitle.AutoSize = False
            _lblPreviewTitle.Location = New Point(44, 13)
            _lblPreviewTitle.Size = New Size(460, 34)
            _lblPreviewTitle.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _lblPreviewStatus.Text = "<font color=#7E8C9D>等待编码队列任务…</font>"
            _lblPreviewStatus.AutoSize = False
            _lblPreviewStatus.Location = New Point(20, 49)
            _lblPreviewStatus.Size = New Size(700, 30)
            _lblPreviewStatus.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _lblTask.Text = "<font color=#B1BCCA>预览任务</font>"
            _lblTask.AutoSize = False
            _lblTask.Size = New Size(84, 38)
            _lblTask.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _cmbTask.Size = New Size(360, 38)
            _cmbTask.WaterText = "选择要预览的任务…"
            ConfigureCombo(_cmbTask)
            AddHandler _cmbTask.SelectedIndexChanged, AddressOf OnTaskSelected
            header.Controls.AddRange(New Control() {liveDot, _lblPreviewTitle, _lblPreviewStatus, _lblTask, _cmbTask})
            Dim arrangeHeader As Action =
                Sub()
                    _cmbTask.Location = New Point(Math.Max(520, header.ClientSize.Width - _cmbTask.Width - 18), 28)
                    _lblTask.Location = New Point(_cmbTask.Left - _lblTask.Width - 8, 28)
                    _lblPreviewTitle.Width = Math.Max(220, _lblTask.Left - _lblPreviewTitle.Left - 20)
                    _lblPreviewStatus.Width = Math.Max(300, _lblTask.Left - _lblPreviewStatus.Left - 20)
                End Sub
            AddHandler header.Resize, Sub(sender, e) arrangeHeader()
            headerHost.Controls.Add(header)
            _pagePreview.Controls.Add(headerHost)
            arrangeHeader()
        End Sub

        Private Sub BuildPreviewPageLegacy()
            _pagePreview.Dock = DockStyle.Fill
            _pagePreview.BackColor = Color.Transparent
            ' 设计器坐标：标题/任务/状态/预览区左侧留 30px 边距，底栏左侧留 27px
            _pagePreview.Padding = New Padding(30, 4, 0, 0)

            ' 中央预览区：原生 PictureBox（Fill 先添加 → 最后布局 → 填充剩余空间）
            Dim previewBorder As New Panel() With {.Dock = DockStyle.Fill, .BackColor = Color.FromArgb(64, 64, 74), .Padding = New Padding(1)}
            _picPreview.Dock = DockStyle.Fill
            _picPreview.BackColor = Color.FromArgb(16, 16, 18)
            _picPreview.SizeMode = PictureBoxSizeMode.Zoom
            previewBorder.Controls.Add(_picPreview)
            _pagePreview.Controls.Add(previewBorder)

            ' 状态行
            _lblPreviewStatus.Text = "<font color=#9AA79A>等待编码队列任务…</font>"
            _lblPreviewStatus.AutoSize = False
            _lblPreviewStatus.Dock = DockStyle.Top
            _lblPreviewStatus.Height = 26
            _lblPreviewStatus.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _pagePreview.Controls.Add(_lblPreviewStatus)

            ' 任务选择行：预览任务 [下拉框]
            Dim taskBar As New Panel() With {.Dock = DockStyle.Top, .Height = 36, .BackColor = Color.Transparent, .Padding = New Padding(0, 4, 0, 0)}
            _cmbTask.Dock = DockStyle.Left
            _cmbTask.Width = 300
            _cmbTask.BorderRadius = 8
            _cmbTask.BorderSize = 1
            _cmbTask.WaterText = "选择要预览的任务…"
            AddHandler _cmbTask.SelectedIndexChanged, AddressOf OnTaskSelected
            taskBar.Controls.Add(_cmbTask)
            _lblTask.Text = "<font color=#C8C8C8>预览任务</font>"
            _lblTask.AutoSize = False
            _lblTask.Dock = DockStyle.Left
            _lblTask.Width = 96
            _lblTask.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            taskBar.Controls.Add(_lblTask)
            _pagePreview.Controls.Add(taskBar)

            ' 标题行
            _lblPreviewTitle.Text = "<font color=#E8E8E8><b>实时预览</b></font>  <font color=#8A8A8A>预览超分/编码完成的帧</font>"
            _lblPreviewTitle.AutoSize = False
            _lblPreviewTitle.Dock = DockStyle.Top
            _lblPreviewTitle.Height = 36
            _lblPreviewTitle.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _pagePreview.Controls.Add(_lblPreviewTitle)

            ' 底部栏：说明（左）+ 切换频率（右）
            Dim bottomBar As New Panel() With {.Dock = DockStyle.Bottom, .Height = 46, .BackColor = Color.Transparent, .Padding = New Padding(0, 10, 0, 0)}
            _lblPreviewNote.Text = "<font color=#8A8A8A>处理速度较慢时，可能存在预览停顿</font>"
            _lblPreviewNote.AutoSize = False
            _lblPreviewNote.Dock = DockStyle.Fill
            _lblPreviewNote.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            bottomBar.Controls.Add(_lblPreviewNote)
            _lblRate.Text = "<font color=#C8C8C8>切换频率</font>"
            _lblRate.AutoSize = False
            _lblRate.Dock = DockStyle.Right
            _lblRate.Width = 90
            _lblRate.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            bottomBar.Controls.Add(_lblRate)
            _cmbRate.Dock = DockStyle.Right
            _cmbRate.Width = 150
            _cmbRate.BorderRadius = 8
            _cmbRate.BorderSize = 1
            _cmbRate.WaterText = "切换频率…"
            _cmbRate.Items.Add("0.5 秒")
            _cmbRate.Items.Add("1 秒")
            _cmbRate.Items.Add("2 秒")
            _cmbRate.Items.Add("3 秒")
            _cmbRate.Items.Add("关键帧模式")
            _cmbRate.SelectedIndex = 1
            AddHandler _cmbRate.SelectedIndexChanged, AddressOf OnRateSelected
            bottomBar.Controls.Add(_cmbRate)
            _pagePreview.Controls.Add(bottomBar)
        End Sub

        ' ────────────────────────── 高级功能页 ──────────────────────────

        Private Sub BuildAdvancedPage()
            _pageAdvanced.Dock = DockStyle.Fill
            _pageAdvanced.BackColor = Color.Transparent
            _pageAdvanced.Padding = New Padding(8, 14, 8, 10)
            _pageAdvanced.AutoScroll = True

            Dim workflowHost As New Panel() With {
                .Dock = DockStyle.Top, .Height = 104, .BackColor = Color.Transparent,
                .Padding = New Padding(0, 12, 0, 0)
            }
            Dim workflow As New FluentCardPanel() With {
                .Dock = DockStyle.Fill, .FillColor = Color.FromArgb(212, 26, 32, 40),
                .StrokeColor = UiStrokeSoft, .CornerRadius = 10
            }
            Dim workflowTitle As Label = CreateTextLabel("三步完成对比", 9.0F, FontStyle.Bold, UiTextSecondary)
            workflowTitle.Location = New Point(18, 12)
            workflowTitle.Size = New Size(150, 26)
            Dim stepOne As Label = CreateTextLabel("01  添加 2–4 个视频", 9.0F, FontStyle.Regular, UiText)
            Dim stepTwo As Label = CreateTextLabel("02  选择画面布局", 9.0F, FontStyle.Regular, UiText)
            Dim stepThree As Label = CreateTextLabel("03  预览并导出", 9.0F, FontStyle.Regular, UiText)
            Dim arrowOne As Label = CreateTextLabel("→", 11.0F, FontStyle.Regular, UiTextMuted)
            Dim arrowTwo As Label = CreateTextLabel("→", 11.0F, FontStyle.Regular, UiTextMuted)
            workflow.Controls.AddRange(New Control() {workflowTitle, stepOne, arrowOne, stepTwo, arrowTwo, stepThree})
            Dim arrangeWorkflow As Action =
                Sub()
                    Dim available = Math.Max(720, workflow.ClientSize.Width - 36)
                    Dim stepWidth = Math.Max(180, (available - 80) \ 3)
                    stepOne.SetBounds(18, 42, stepWidth, 30)
                    arrowOne.SetBounds(stepOne.Right + 10, 42, 30, 30)
                    stepTwo.SetBounds(arrowOne.Right + 10, 42, stepWidth, 30)
                    arrowTwo.SetBounds(stepTwo.Right + 10, 42, 30, 30)
                    stepThree.SetBounds(arrowTwo.Right + 10, 42, stepWidth, 30)
                End Sub
            AddHandler workflow.Resize, Sub(sender, e) arrangeWorkflow()
            workflowHost.Controls.Add(workflow)
            _pageAdvanced.Controls.Add(workflowHost)

            Dim heroHost As New Panel() With {
                .Dock = DockStyle.Top, .Height = 266, .BackColor = Color.Transparent,
                .Padding = New Padding(0, 12, 0, 0)
            }
            Dim hero As New FluentCardPanel() With {
                .Dock = DockStyle.Fill, .FillColor = UiSurface,
                .StrokeColor = UiStrokeSoft, .CornerRadius = 12
            }
            Dim heroAccent As New Panel() With {
                .BackColor = UiAccent, .Location = New Point(0, 20), .Size = New Size(4, 62)
            }
            Dim heroKicker As Label = CreateTextLabel("LOCAL VIDEO LAB", 8.0F, FontStyle.Bold, UiAccent)
            heroKicker.Location = New Point(24, 18)
            heroKicker.Size = New Size(260, 24)
            Dim heroTitle As HtmlColorLabel = CreateHtmlTextLabel("把差异放在同一画面里", 15.0F, FontStyle.Bold, UiText)
            heroTitle.Location = New Point(24, 42)
            heroTitle.Size = New Size(560, 38)
            Dim heroDesc As Label = CreateTextLabel("实时组合原片、超分、补帧等版本，支持上下、左右、1+2 与四宫格布局。", 9.2F, FontStyle.Regular, UiTextSecondary)
            heroDesc.Location = New Point(24, 82)
            heroDesc.Size = New Size(650, 48)
            Dim privacy As Label = CreateTextLabel("✓ 本机处理    ✓ 自定义分辨率    ✓ 自定义编码器与分割线", 8.7F, FontStyle.Regular, UiSuccess)
            privacy.Location = New Point(24, 132)
            privacy.Size = New Size(640, 30)
            _btnQuad.Text = "打开对比工作室  →"
            _btnQuad.Size = New Size(188, 42)
            ConfigurePrimaryButton(_btnQuad)
            AddHandler _btnQuad.Click, AddressOf OnQuadClick

            Dim preview As New FluentCardPanel() With {
                .FillColor = Color.FromArgb(14, 18, 24), .StrokeColor = UiStroke, .CornerRadius = 10
            }
            Dim previewCaption As Label = CreateTextLabel("4-UP PREVIEW", 7.5F, FontStyle.Bold, UiTextMuted)
            previewCaption.Location = New Point(12, 8)
            previewCaption.Size = New Size(150, 20)
            preview.Controls.Add(previewCaption)
            Dim cells As New List(Of FluentCardPanel)()
            For i As Integer = 0 To 3
                Dim cell As New FluentCardPanel() With {
                    .FillColor = If(i Mod 2 = 0, Color.FromArgb(38, 50, 65), Color.FromArgb(47, 42, 58)),
                    .StrokeColor = Color.FromArgb(61, 74, 90), .CornerRadius = 6
                }
                Dim badge As Label = CreateTextLabel((i + 1).ToString(), 8.0F, FontStyle.Bold,
                                                     If(i = 0, UiAccent, UiTextMuted))
                badge.Dock = DockStyle.Fill
                badge.TextAlign = ContentAlignment.MiddleCenter
                cell.Controls.Add(badge)
                cells.Add(cell)
                preview.Controls.Add(cell)
            Next
            Dim arrangePreview As Action =
                Sub()
                    Dim gap = 8
                    Dim cellWidth = Math.Max(70, (preview.ClientSize.Width - 32 - gap) \ 2)
                    Dim cellHeight = Math.Max(45, (preview.ClientSize.Height - 48 - gap) \ 2)
                    cells(0).SetBounds(12, 32, cellWidth, cellHeight)
                    cells(1).SetBounds(cells(0).Right + gap, 32, cellWidth, cellHeight)
                    cells(2).SetBounds(12, cells(0).Bottom + gap, cellWidth, cellHeight)
                    cells(3).SetBounds(cells(2).Right + gap, cells(0).Bottom + gap, cellWidth, cellHeight)
                End Sub
            AddHandler preview.Resize, Sub(sender, e) arrangePreview()
            hero.Controls.AddRange(New Control() {
                heroAccent, heroKicker, heroTitle, heroDesc, privacy, _btnQuad, preview
            })
            Dim arrangeHero As Action =
                Sub()
                    Dim previewWidth = Math.Max(300, Math.Min(420, CInt(hero.ClientSize.Width * 0.32)))
                    preview.SetBounds(hero.ClientSize.Width - previewWidth - 20, 18, previewWidth, hero.ClientSize.Height - 36)
                    Dim textRight = preview.Left - 24
                    heroTitle.Width = Math.Max(260, textRight - heroTitle.Left)
                    heroDesc.Width = Math.Max(260, textRight - heroDesc.Left)
                    privacy.Width = Math.Max(260, textRight - privacy.Left)
                    _btnQuad.Location = New Point(24, hero.ClientSize.Height - _btnQuad.Height - 22)
                    preview.Visible = hero.ClientSize.Width >= 820
                    arrangePreview()
                End Sub
            AddHandler hero.Resize, Sub(sender, e) arrangeHero()
            heroHost.Controls.Add(hero)
            _pageAdvanced.Controls.Add(heroHost)

            Dim headerHost As New Panel() With {
                .Dock = DockStyle.Top, .Height = 94, .BackColor = Color.Transparent,
                .Padding = New Padding(0, 0, 0, 12)
            }
            headerHost.Controls.Add(CreatePageHeader("▦", "对比工具", "在同一画面中检查不同模型、参数和处理版本，快速找到最合适的方案。"))
            _pageAdvanced.Controls.Add(headerHost)
            arrangeHero()
            arrangeWorkflow()
        End Sub

        Private Sub BuildAdvancedPageLegacy()
            _pageAdvanced.Dock = DockStyle.Fill
            _pageAdvanced.BackColor = Color.Transparent
            _pageAdvanced.Padding = New Padding(8, 22, 8, 8)

            ' Fluent Design 功能卡片；只调整呈现，仍打开原有 QuadGridForm 后端。
            Dim card As New FluentCardPanel() With {
                .Dock = DockStyle.Top, .Height = 166, .Padding = New Padding(24, 20, 24, 20),
                .FillColor = Color.FromArgb(43, 43, 43), .StrokeColor = Color.FromArgb(63, 63, 63), .CornerRadius = 12
            }
            Dim accent As New Panel() With {
                .BackColor = Color.FromArgb(96, 205, 255), .Location = New Point(0, 22),
                .Size = New Size(4, 122), .Anchor = AnchorStyles.Left Or AnchorStyles.Top Or AnchorStyles.Bottom
            }
            Dim icon As New Label() With {
                .Text = "▦", .Location = New Point(24, 22), .Size = New Size(48, 48),
                .ForeColor = Color.FromArgb(96, 205, 255), .Font = New Font("Segoe UI Symbol", 24.0F),
                .TextAlign = ContentAlignment.MiddleCenter
            }
            Dim title As New Label() With {
                .Text = "视频对比工作室", .Location = New Point(84, 22), .Size = New Size(420, 32),
                .ForeColor = Color.FromArgb(250, 250, 250), .Font = New Font("Microsoft YaHei UI", 13.0F, FontStyle.Bold),
                .TextAlign = ContentAlignment.MiddleLeft
            }
            Dim description As New Label() With {
                .Text = "拖入 1–4 个视频，实时预览上下、左右、1+2 或四宫格布局，并自定义编码器、分辨率和分割线。",
                .Location = New Point(84, 58), .Size = New Size(690, 52),
                .ForeColor = Color.FromArgb(190, 190, 190), .Font = New Font("Microsoft YaHei UI", 9.5F),
                .TextAlign = ContentAlignment.MiddleLeft, .AutoEllipsis = True
            }
            Dim footnote As New Label() With {
                .Text = "至少需要两个视频 · 处理过程完全在本机完成", .Location = New Point(84, 112), .Size = New Size(520, 28),
                .ForeColor = Color.FromArgb(145, 145, 145), .Font = New Font("Microsoft YaHei UI", 8.5F),
                .TextAlign = ContentAlignment.MiddleLeft
            }
            _btnQuad.Text = "打开工作室  →"
            _btnQuad.Size = New Size(168, 40)
            _btnQuad.Location = New Point(card.Width - 192, 102)
            _btnQuad.Anchor = AnchorStyles.Right Or AnchorStyles.Bottom
            _btnQuad.BorderRadius = 8
            _btnQuad.BorderSize = 0
            _btnQuad.BackColor1 = Color.FromArgb(0, 120, 212)
            _btnQuad.HoverBackColor1 = Color.FromArgb(17, 94, 163)
            _btnQuad.PressedBackColor1 = Color.FromArgb(0, 91, 158)
            AddHandler _btnQuad.Click, AddressOf OnQuadClick
            card.Controls.AddRange(New Control() {accent, icon, title, description, footnote, _btnQuad})
            _pageAdvanced.Controls.Add(card)
        End Sub

        ' ────────────────────────── 模型下载页 ──────────────────────────

        Private Sub QueueDownloadScrollWidthReset()
            If _downloadScrollResetPending OrElse _downloadList.IsDisposed OrElse Not _downloadList.IsHandleCreated Then Return
            _downloadScrollResetPending = True
            Try
                _downloadList.BeginInvoke(New Action(
                    Sub()
                        _downloadScrollResetPending = False
                        If _downloadList.IsDisposed Then Return
                        ' FlowLayoutPanel 会缓存窗口放大前的虚拟宽度；在本轮布局结束后
                        ' 清空缓存，避免只需要纵向滚动时仍出现白色横向滚动条。
                        _downloadList.AutoScrollMinSize = Size.Empty
                        _downloadList.PerformLayout()
                    End Sub))
            Catch
                _downloadScrollResetPending = False
            End Try
        End Sub

        Private Sub BuildModelDownloadPage()
            _pageDownloader.Dock = DockStyle.Fill
            _pageDownloader.BackColor = Color.Transparent
            _pageDownloader.Padding = New Padding(8, 14, 8, 10)

            _downloadList.Dock = DockStyle.Fill
            _downloadList.AutoScroll = True
            _downloadList.WrapContents = False
            _downloadList.FlowDirection = FlowDirection.TopDown
            _downloadList.BackColor = Color.Transparent
            _downloadList.Padding = New Padding(0, 4, 4, 4)
            AddHandler _downloadList.ClientSizeChanged,
                Sub(sender, e)
                    For Each row As Panel In _downloadList.Controls.OfType(Of Panel)()
                        row.Width = Math.Max(360, _downloadList.ClientSize.Width - 24)
                    Next
                End Sub
            _pageDownloader.Controls.Add(_downloadList)

            Dim headerHost As New Panel() With {
                .Dock = DockStyle.Top, .Height = 96, .BackColor = Color.Transparent,
                .Padding = New Padding(0, 0, 0, 12)
            }
            Dim header = CreatePageHeader("↓", "模型资源库", "从 ModelScope 镜像获取模型与后端组件；压缩包下载后会自动解压到正确目录。")
            _btnRefreshDownloads.Text = "刷新资源"
            _btnRefreshDownloads.Size = New Size(124, 38)
            ' 位置由 Resize 处理；同时使用 Right 锚点会在首次布局时重复偏移。
            _btnRefreshDownloads.Anchor = AnchorStyles.Top
            ConfigurePrimaryButton(_btnRefreshDownloads)
            AddHandler _btnRefreshDownloads.Click, Sub(sender, e) LoadDownloadModels(True)
            header.Controls.Add(_btnRefreshDownloads)
            Dim arrangeHeader As Action =
                Sub()
                    _btnRefreshDownloads.Location = New Point(Math.Max(300, header.ClientSize.Width - _btnRefreshDownloads.Width - 16), 22)
                    For Each child As Control In header.Controls
                        If child IsNot _btnRefreshDownloads AndAlso child.Left >= 80 Then
                            child.Width = Math.Max(160, _btnRefreshDownloads.Left - child.Left - 16)
                        End If
                    Next
                    _btnRefreshDownloads.BringToFront()
                End Sub
            AddHandler header.Resize, Sub(sender, e) arrangeHeader()
            headerHost.Controls.Add(header)
            _pageDownloader.Controls.Add(headerHost)
            arrangeHeader()
        End Sub

        Private Sub BuildModelDownloadPageLegacy()
            _pageDownloader.Dock = DockStyle.Fill
            _pageDownloader.BackColor = Color.Transparent
            _pageDownloader.Padding = New Padding(0, 12, 0, 0)

            Dim header As New Panel() With {.Dock = DockStyle.Top, .Height = 76, .BackColor = Color.Transparent}
            Dim description As New HtmlColorLabel() With {
                .Text = "<font color=#D8D8D8><b>ModelScope 模型镜像</b></font><br/>" &
                        "<font color=#8A8A8A>文件下载到 models 对应分类；Backend 文件下载到 python。压缩包会自动解压。</font>",
                .AutoSize = False, .Dock = DockStyle.Fill,
                .TextAlign = HtmlColorLabel.TextAlignEnum.TopLeft, .LineSpacing = 4
            }
            _btnRefreshDownloads.Text = "刷新列表"
            _btnRefreshDownloads.Size = New Size(118, 34)
            _btnRefreshDownloads.Dock = DockStyle.Right
            _btnRefreshDownloads.BorderRadius = 8
            _btnRefreshDownloads.BorderSize = 0
            _btnRefreshDownloads.BackColor1 = Color.FromArgb(40, 110, 190, 255)
            _btnRefreshDownloads.HoverBackColor1 = Color.FromArgb(60, 110, 190, 255)
            AddHandler _btnRefreshDownloads.Click, Sub(sender, e) LoadDownloadModels(True)
            header.Controls.Add(description)
            header.Controls.Add(_btnRefreshDownloads)
            _pageDownloader.Controls.Add(header)

            _downloadList.Dock = DockStyle.Fill
            _downloadList.AutoScroll = True
            _downloadList.WrapContents = False
            _downloadList.FlowDirection = FlowDirection.TopDown
            _downloadList.BackColor = Color.Transparent
            _downloadList.Padding = New Padding(0, 4, 4, 4)
            AddHandler _downloadList.ClientSizeChanged,
                Sub(sender, e)
                    For Each row As Panel In _downloadList.Controls.OfType(Of Panel)()
                        row.Width = Math.Max(320, _downloadList.ClientSize.Width - 24)
                    Next
                End Sub
            _pageDownloader.Controls.Add(_downloadList)
            ' Fill 先加入、Top 后加入，确保标题栏占位后列表填满其余区域。
            _pageDownloader.Controls.SetChildIndex(_downloadList, 0)
            _pageDownloader.Controls.SetChildIndex(header, 1)
        End Sub

        Private Function DownloadExecutablePath() As String
            If File.Exists(_config.ExePath) Then Return _config.ExePath
            Dim besideHost = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "videoenhancer.exe")
            Return If(File.Exists(besideHost), besideHost, "")
        End Function

        Private Sub LoadDownloadModels(force As Boolean)
            If _downloadsLoading OrElse (_downloadsLoaded AndAlso Not force) Then Return
            Dim exePath = DownloadExecutablePath()
            If String.IsNullOrWhiteSpace(exePath) Then
                ShowStatus("请先在超分主界面指定 videoenhancer.exe", True)
                Return
            End If
            _downloadsLoading = True
            _btnRefreshDownloads.Enabled = False
            _downloadList.Controls.Clear()
            Dim loading As New ModernPanel() With {
                .Width = Math.Max(360, _downloadList.ClientSize.Width - 24), .Height = 72,
                .BackColor = Color.Transparent,
                .BackColor1 = Color.FromArgb(48, 220, 220, 220),
                .BorderColor = Color.Transparent,
                .BorderSize = 0,
                .BorderRadius = 12,
                .Margin = New Padding(0, 0, 0, 10)
            }
            Dim loadingText As Label = CreateTextLabel("正在同步模型资源…", 9.2F, FontStyle.Regular, UiTextSecondary)
            loadingText.Dock = DockStyle.Fill
            loadingText.Padding = New Padding(18, 0, 0, 0)
            loading.Controls.Add(loadingText)
            _downloadList.Controls.Add(loading)

            Task.Run(
                Sub()
                    Dim stdout = ""
                    Dim stderr = ""
                    Dim exitCode = -1
                    Try
                        Dim psi As New ProcessStartInfo With {
                            .FileName = exePath, .WorkingDirectory = Path.GetDirectoryName(exePath),
                            .UseShellExecute = False, .RedirectStandardOutput = True,
                            .RedirectStandardError = True, .CreateNoWindow = True,
                            .StandardOutputEncoding = Encoding.UTF8, .StandardErrorEncoding = Encoding.UTF8
                        }
                        psi.ArgumentList.Add("--list-download-models")
                        psi.ArgumentList.Add("--json")
                        Using runningProcess As Process = Diagnostics.Process.Start(psi)
                            If runningProcess IsNot Nothing Then
                                Dim outputTask = runningProcess.StandardOutput.ReadToEndAsync()
                                Dim errorTask = runningProcess.StandardError.ReadToEndAsync()
                                If runningProcess.WaitForExit(45000) Then
                                    stdout = outputTask.GetAwaiter().GetResult()
                                    stderr = errorTask.GetAwaiter().GetResult()
                                    exitCode = runningProcess.ExitCode
                                Else
                                    Try
                                        runningProcess.Kill(True)
                                    Catch
                                    End Try
                                    stderr = "[错误] 读取 ModelScope 模型列表超时"
                                    exitCode = -2
                                End If
                            End If
                        End Using
                    Catch ex As Exception
                        stderr = ex.Message
                    End Try
                    Try
                        BeginInvoke(New Action(Sub() RenderDownloadModels(stdout, stderr, exitCode)))
                    Catch
                    End Try
                End Sub)
        End Sub

        Private Sub RenderDownloadModels(stdout As String, stderr As String, exitCode As Integer)
            _downloadsLoading = False
            _btnRefreshDownloads.Enabled = True
            _downloadList.Controls.Clear()
            If exitCode <> 0 OrElse String.IsNullOrWhiteSpace(stdout) Then
                _downloadsLoaded = False
                If stderr.Contains("NO_NETWORK|", StringComparison.Ordinal) Then
                    _downloadOnline = False
                    ShowOfflineDownloadStatus()
                Else
                    _downloadOnline = True
                    ShowStatus(CliErrorMessage(stderr, "模型列表读取失败"), True)
                End If
                Return
            End If

            Try
                Dim entries As New List(Of DownloadModelEntry)()
                Using document = JsonDocument.Parse(stdout.Trim())
                    For Each item In document.RootElement.EnumerateArray()
                        Dim name = item.GetProperty("name").GetString()
                        Dim relativePath = item.GetProperty("path").GetString()
                        Dim size = item.GetProperty("size").GetInt64()
                        entries.Add(New DownloadModelEntry With {
                            .Name = If(name, relativePath), .RelativePath = If(relativePath, ""), .Size = size
                        })
                    Next
                End Using
                Dim categoryOrder = New String() {"Backend", "Bin", "ONNX", "Param-Bin", "FlashVSR", "RIFE", "PTH", "TensorRT-Default"}
                For Each group In entries.GroupBy(Function(entry) DownloadCategory(entry.RelativePath)).
                        OrderBy(Function(value)
                                    Dim index = Array.FindIndex(categoryOrder, Function(name) name.Equals(value.Key, StringComparison.OrdinalIgnoreCase))
                                    Return If(index < 0, Integer.MaxValue, index)
                                End Function)
                    AddDownloadGroup(group.Key, group.ToList())
                Next
                _downloadsLoaded = True
                _downloadOnline = True
                ShowStatus("模型列表已更新，共 " & entries.Count & " 个文件", False)
            Catch ex As Exception
                _downloadsLoaded = False
                _downloadOnline = True
                ShowStatus("模型列表格式错误：" & ex.Message, True)
            End Try
        End Sub

        Private Shared Function DownloadCategory(relativePath As String) As String
            If String.IsNullOrWhiteSpace(relativePath) Then Return "其他"
            Dim normalized = relativePath.Replace("\"c, "/"c)
            Dim slash = normalized.IndexOf("/"c)
            Return If(slash > 0, normalized.Substring(0, slash), normalized)
        End Function

        Private Shared Function DownloadCategoryTitle(category As String) As String
            Select Case category.ToUpperInvariant()
                Case "ONNX" : Return "ONNX 模型"
                Case "PARAM-BIN" : Return "Param-Bin 模型"
                Case "RIFE" : Return "RIFE 模型"
                Case "PTH" : Return "PTH 模型"
                Case "BACKEND" : Return "Backend 后端"
                Case Else : Return category
            End Select
        End Function

        Private Sub AddDownloadGroup(category As String, entries As List(Of DownloadModelEntry))
            Const headerHeight As Integer = 64
            Const rowHeightWithGap As Integer = 52
            Dim groupPanel As New ModernPanel() With {
                .Width = Math.Max(360, _downloadList.ClientSize.Width - 24), .Height = headerHeight,
                .Margin = New Padding(0, 0, 0, 10),
                .Padding = Padding.Empty,
                .BackColor = Color.Transparent,
                .BackColor1 = Color.FromArgb(48, 220, 220, 220),
                .BorderColor = Color.Transparent,
                .BorderSize = 0,
                .BorderRadius = 12
            }
            Dim header As New Panel() With {
                .Location = New Point(0, 0), .Height = headerHeight, .Width = groupPanel.Width,
                .Anchor = AnchorStyles.Left Or AnchorStyles.Top Or AnchorStyles.Right,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty
            }
            Dim categoryMark As New Label() With {
                .Text = "●", .Location = New Point(16, 0), .Size = New Size(20, headerHeight),
                .ForeColor = If(category.Equals("Backend", StringComparison.OrdinalIgnoreCase), UiSuccess, UiAccent),
                .BackColor = Color.Transparent, .Font = New Font("Segoe UI Symbol", 7.0F),
                .TextAlign = ContentAlignment.MiddleCenter
            }
            Dim title As New Label() With {
                .Text = DownloadCategoryTitle(category) & "  ·  " & entries.Count & " 个文件",
                .Location = New Point(42, 0), .Height = headerHeight, .ForeColor = UiText,
                .Font = New Font("Microsoft YaHei UI", 10.0F, FontStyle.Bold),
                .TextAlign = ContentAlignment.MiddleLeft, .AutoEllipsis = True
            }
            Dim expandButton As New ModernButton() With {
                .Text = "展开　▼", .Size = New Size(120, 40)
            }
            ConfigureSecondaryButton(expandButton)
            Dim allButton As New ModernButton() With {
                .Text = "下载全部", .Size = New Size(160, 40),
                .Tag = entries.Select(Function(entry) entry.RelativePath).ToList()
            }
            ConfigurePrimaryButton(allButton)
            Dim content As New FlowLayoutPanel() With {
                .Location = New Point(0, headerHeight), .Width = groupPanel.Width,
                .Height = Math.Max(1, entries.Count * rowHeightWithGap + 14), .Visible = False,
                .WrapContents = False, .FlowDirection = FlowDirection.TopDown,
                .AutoScroll = False, .BackColor = Color.Transparent, .Margin = Padding.Empty,
                .Padding = New Padding(8, 6, 8, 8)
            }
            For Each entry In entries
                content.Controls.Add(CreateDownloadRow(entry, content.Width))
            Next
            expandButton.Tag = New Object() {groupPanel, content}
            AddHandler expandButton.Click, AddressOf OnToggleDownloadGroup
            AddHandler allButton.Click, AddressOf OnDownloadAllClick
            Dim arrangeHeader As Action =
                Sub()
                    allButton.Left = Math.Max(250, header.ClientSize.Width - allButton.Width - 12)
                    allButton.Top = 12
                    expandButton.Left = allButton.Left - expandButton.Width - 8
                    expandButton.Top = 12
                    title.Width = Math.Max(120, expandButton.Left - title.Left - 10)
                    content.Width = groupPanel.ClientSize.Width
                    For Each row As Panel In content.Controls.OfType(Of Panel)()
                        row.Width = Math.Max(320, content.ClientSize.Width - content.Padding.Horizontal)
                    Next
                End Sub
            AddHandler header.Resize, Sub(sender, e) arrangeHeader()
            AddHandler groupPanel.Resize, Sub(sender, e) arrangeHeader()
            header.Controls.AddRange(New Control() {categoryMark, title, expandButton, allButton})
            groupPanel.Controls.AddRange(New Control() {header, content})
            _downloadList.Controls.Add(groupPanel)
            arrangeHeader()
        End Sub

        Private Function CreateDownloadRow(entry As DownloadModelEntry, rowWidth As Integer) As Panel
            Dim row As New ModernPanel() With {
                .Width = Math.Max(320, rowWidth), .Height = 48,
                .Margin = New Padding(0, 0, 0, 4),
                .Padding = Padding.Empty,
                .BackColor = Color.Transparent,
                .BackColor1 = Color.FromArgb(68, 8, 10, 14),
                .BorderColor = Color.Transparent,
                .BorderSize = 0,
                .BorderRadius = 9
            }
            Dim sizeText = If(entry.Size > 0, "  ·  " & FormatDownloadSize(entry.Size), "")
            Dim label As New Label() With {
                .Text = entry.Name & sizeText, .ForeColor = UiTextSecondary,
                .Dock = DockStyle.Fill, .Padding = New Padding(14, 0, 8, 0),
                .TextAlign = ContentAlignment.MiddleLeft, .AutoEllipsis = True
            }
            Dim button As New ModernButton() With {
                .Text = "下载", .Dock = DockStyle.Right, .Width = 132,
                .Margin = Padding.Empty, .Tag = entry.RelativePath
            }
            ConfigureSecondaryButton(button)
            AddHandler button.Click, AddressOf OnDownloadModelClick
            row.Controls.Add(label)
            row.Controls.Add(button)
            Return row
        End Function

        Private Sub OnToggleDownloadGroup(sender As Object, e As EventArgs)
            Dim button = TryCast(sender, ModernButton)
            Dim state = If(button Is Nothing, Nothing, TryCast(button.Tag, Object()))
            If state Is Nothing OrElse state.Length < 2 Then Return
            Dim groupPanel = TryCast(state(0), Panel)
            Dim content = TryCast(state(1), FlowLayoutPanel)
            If groupPanel Is Nothing OrElse content Is Nothing Then Return
            content.Visible = Not content.Visible
            groupPanel.Height = If(content.Visible, 64 + content.Height, 64)
            button.Text = If(content.Visible, "收起　▲", "展开　▼")
            _downloadList.PerformLayout()
            QueueDownloadScrollWidthReset()
        End Sub

        Private Shared Function FormatDownloadSize(bytes As Long) As String
            If bytes >= 1024L * 1024L * 1024L Then Return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("0.00") & " GB"
            If bytes >= 1024L * 1024L Then Return (bytes / (1024.0 * 1024.0)).ToString("0.0") & " MB"
            If bytes >= 1024L Then Return (bytes / 1024.0).ToString("0.0") & " KB"
            Return bytes & " B"
        End Function

        Private Async Sub OnDownloadModelClick(sender As Object, e As EventArgs)
            If Not _downloadOnline OrElse _downloadBusy Then Return
            Dim button = TryCast(sender, ModernButton)
            If button Is Nothing OrElse button.Tag Is Nothing Then Return
            Dim exePath = DownloadExecutablePath()
            If String.IsNullOrWhiteSpace(exePath) Then Return
            Dim relativePath = button.Tag.ToString()
            _downloadBusy = True
            SetDownloadActionsEnabled(False)
            button.Text = "准备中…"
            Dim result = Await Task.Run(Function() ExecuteModelDownload(exePath, relativePath,
                Sub(text)
                    Try
                        BeginInvoke(New Action(Sub() button.Text = text))
                    Catch
                    End Try
                End Sub))
            _downloadBusy = False
            If result.ExitCode = 0 Then
                button.Text = "已完成"
                ShowStatus("模型下载完成：" & relativePath, False)
                SetDownloadActionsEnabled(True)
            ElseIf result.Errors.Contains("NO_NETWORK|") Then
                button.Text = "下载"
                _downloadOnline = False
                ShowOfflineDownloadStatus()
            Else
                button.Text = "重试"
                SetDownloadActionsEnabled(True)
                ShowStatus(CliErrorMessage(result.Errors, "模型下载失败"), True)
            End If
        End Sub

        Private Async Sub OnDownloadAllClick(sender As Object, e As EventArgs)
            If Not _downloadOnline OrElse _downloadBusy Then Return
            Dim button = TryCast(sender, ModernButton)
            Dim paths = If(button Is Nothing, Nothing, TryCast(button.Tag, List(Of String)))
            If paths Is Nothing OrElse paths.Count = 0 Then Return
            Dim exePath = DownloadExecutablePath()
            If String.IsNullOrWhiteSpace(exePath) Then Return
            _downloadBusy = True
            SetDownloadActionsEnabled(False)
            Dim completed = 0
            Dim failed = False
            Dim failureMessage = ""
            For Each relativePath In paths
                Dim current = completed + 1
                button.Text = current & "/" & paths.Count
                Dim result = Await Task.Run(Function() ExecuteModelDownload(exePath, relativePath,
                    Sub(text)
                        If text.EndsWith("%", StringComparison.Ordinal) Then
                            Try : BeginInvoke(New Action(Sub() button.Text = current & "/" & paths.Count & "  " & text)) : Catch : End Try
                        End If
                    End Sub))
                If result.ExitCode <> 0 Then
                    failed = True
                    failureMessage = CliErrorMessage(result.Errors, "模型下载失败")
                    If result.Errors.Contains("NO_NETWORK|") Then
                        _downloadOnline = False
                        ShowOfflineDownloadStatus()
                    End If
                    Exit For
                End If
                completed += 1
            Next
            _downloadBusy = False
            If Not _downloadOnline Then
                button.Text = "一键全部下载"
                Return
            End If
            SetDownloadActionsEnabled(True)
            If failed Then
                button.Text = "继续下载"
                ShowStatus("批量下载在第 " & (completed + 1) & " 个文件处失败：" & failureMessage, True)
            Else
                button.Text = "全部完成"
                ShowStatus("该分类 " & completed & " 个文件已全部下载完成", False)
            End If
        End Sub

        Private Function ExecuteModelDownload(exePath As String, relativePath As String, progress As Action(Of String)) As DownloadExecutionResult
            Dim result As New DownloadExecutionResult()
            Dim errors As New StringBuilder()
            Try
                Dim psi As New ProcessStartInfo With {
                    .FileName = exePath, .WorkingDirectory = Path.GetDirectoryName(exePath),
                    .UseShellExecute = False, .RedirectStandardOutput = True,
                    .RedirectStandardError = True, .CreateNoWindow = True,
                    .StandardOutputEncoding = Encoding.UTF8, .StandardErrorEncoding = Encoding.UTF8
                }
                psi.ArgumentList.Add("--download-model")
                psi.ArgumentList.Add(relativePath)
                Using process As New Process With {.StartInfo = psi}
                    AddHandler process.OutputDataReceived,
                        Sub(s, ev)
                            If ev.Data Is Nothing Then Return
                            If ev.Data.StartsWith("DOWNLOAD_PROGRESS|", StringComparison.Ordinal) Then
                                Dim parts = ev.Data.Split("|"c)
                                If parts.Length > 1 Then progress(parts(1) & "%")
                            ElseIf ev.Data.StartsWith("EXTRACT_COMPLETE|", StringComparison.Ordinal) Then
                                progress("解压完成")
                            End If
                        End Sub
                    AddHandler process.ErrorDataReceived, Sub(s, ev) If ev.Data IsNot Nothing Then errors.AppendLine(ev.Data)
                    process.Start()
                    process.BeginOutputReadLine()
                    process.BeginErrorReadLine()
                    process.WaitForExit()
                    result.ExitCode = process.ExitCode
                End Using
            Catch ex As Exception
                errors.AppendLine(ex.Message)
            End Try
            result.Errors = errors.ToString()
            Return result
        End Function

        Private Iterator Function AllDownloadButtons(parent As Control) As IEnumerable(Of ModernButton)
            For Each child As Control In parent.Controls
                Dim button = TryCast(child, ModernButton)
                If button IsNot Nothing AndAlso (TypeOf button.Tag Is String OrElse TypeOf button.Tag Is List(Of String)) Then
                    Yield button
                End If
                If child.HasChildren Then
                    For Each nested In AllDownloadButtons(child)
                        Yield nested
                    Next
                End If
            Next
        End Function

        Private Sub SetDownloadActionsEnabled(enabled As Boolean)
            For Each button In AllDownloadButtons(_downloadList)
                button.Enabled = enabled AndAlso _downloadOnline
            Next
        End Sub

        Private Sub ShowOfflineDownloadStatus()
            Try
                _statusClearTimer.Stop()
            Catch
            End Try
            If _downloadList.Controls.Count = 0 Then
                Dim emptyCard As New ModernPanel() With {
                    .Width = Math.Max(360, _downloadList.ClientSize.Width - 24), .Height = 96,
                    .BackColor = Color.Transparent,
                    .BackColor1 = Color.FromArgb(48, 220, 220, 220),
                    .BorderColor = Color.Transparent,
                    .BorderSize = 0,
                    .BorderRadius = 12,
                    .Margin = New Padding(0, 0, 0, 10)
                }
                Dim emptyText As Label = CreateTextLabel("暂时无法连接模型镜像", 10.0F, FontStyle.Bold, UiText)
                emptyText.Location = New Point(18, 14)
                emptyText.Size = New Size(520, 30)
                Dim emptyHint As Label = CreateTextLabel("检查网络连接后点击右上角的刷新资源按钮重试。", 8.8F, FontStyle.Regular, UiTextMuted)
                emptyHint.Location = New Point(18, 46)
                emptyHint.Size = New Size(620, 28)
                emptyCard.Controls.AddRange(New Control() {emptyText, emptyHint})
                _downloadList.Controls.Add(emptyCard)
            End If
            _lblStatus.Text = "<font color=#E07878>无法连接 ModelScope，请检查网络或代理设置</font>"
            SetDownloadActionsEnabled(False)
        End Sub

        Private Async Sub OnCleanDownloadArchives(sender As Object, e As EventArgs)
            If _downloadBusy Then Return
            If Not File.Exists(_config.ExePath) Then
                ShowStatus("请先指定有效的 videoenhancer.exe", True)
                Return
            End If
            _downloadBusy = True
            _btnCleanArchives.Enabled = False
            ShowStatus("正在清理下载压缩包…", False)
            Dim output = New StringBuilder()
            Dim errors = New StringBuilder()
            Dim exitCode = Await Task.Run(
                Function()
                    Try
                        Dim psi As New ProcessStartInfo With {
                            .FileName = _config.ExePath,
                            .WorkingDirectory = Path.GetDirectoryName(_config.ExePath),
                            .UseShellExecute = False, .CreateNoWindow = True,
                            .RedirectStandardOutput = True, .RedirectStandardError = True,
                            .StandardOutputEncoding = Encoding.UTF8, .StandardErrorEncoding = Encoding.UTF8
                        }
                        psi.ArgumentList.Add("--clean-download-archives")
                        Using process As New Process With {.StartInfo = psi}
                            process.Start()
                            output.Append(process.StandardOutput.ReadToEnd())
                            errors.Append(process.StandardError.ReadToEnd())
                            process.WaitForExit()
                            Return process.ExitCode
                        End Using
                    Catch ex As Exception
                        errors.Append(ex.Message)
                        Return -1
                    End Try
                End Function)
            _downloadBusy = False
            _btnCleanArchives.Enabled = True
            Dim complete = output.ToString().Split(New Char() {Convert.ToChar(13), Convert.ToChar(10)}, StringSplitOptions.RemoveEmptyEntries).
                FirstOrDefault(Function(line) line.StartsWith("CLEAN_COMPLETE|", StringComparison.Ordinal))
            If exitCode = 0 AndAlso complete IsNot Nothing Then
                Dim parts = complete.Split("|"c)
                Dim count = If(parts.Length > 1, parts(1), "0")
                ShowStatus("已清理 " & count & " 个下载压缩包", False)
            Else
                ShowStatus("清理失败：" & LastNonEmptyLine(errors.ToString()), True)
            End If
        End Sub

        ' ────────────────────────── 模型转换器页 ──────────────────────────

        Private Sub BuildOfficialConverterPage()
            _pageConverter.Dock = DockStyle.Fill
            _pageConverter.BackColor = Color.Transparent
            _pageConverter.Padding = New Padding(0, 8, 0, 0)
            _pageConverter.AllowDrop = True
            AddHandler _pageConverter.DragEnter, AddressOf OnConverterDragEnter
            AddHandler _pageConverter.DragDrop, AddressOf OnConverterDragDrop

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 5,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty,
                .AllowDrop = True
            }
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 64.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 64.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 70.0F))
            AddHandler root.DragEnter, AddressOf OnConverterDragEnter
            AddHandler root.DragDrop, AddressOf OnConverterDragDrop
            root.Controls.Add(CreateOfficialSectionHeading(
                "模型转换", "将 PyTorch PTH 模型离线编译为当前设备专用的 TensorRT Engine"), 0, 0)

            Dim inputRow As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 3,
                .RowCount = 1,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty,
                .AllowDrop = True
            }
            inputRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 180.0F))
            inputRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 12.0F))
            inputRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
            inputRow.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            _btnPickPth.Text = "选择或拖入 PTH"
            _btnPickPth.Dock = DockStyle.Fill
            _btnPickPth.Margin = New Padding(0, 8, 0, 8)
            ConfigureSecondaryButton(_btnPickPth)
            AddHandler _btnPickPth.Click, AddressOf OnPickPthClick
            _lblConvertInput.Text = "<font color=#888888>尚未选择 .pth 文件</font>"
            _lblConvertInput.AutoSize = False
            _lblConvertInput.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            inputRow.Controls.Add(_btnPickPth, 0, 0)
            inputRow.Controls.Add(CreateOfficialValueBox(_lblConvertInput), 2, 0)
            AddHandler inputRow.DragEnter, AddressOf OnConverterDragEnter
            AddHandler inputRow.DragDrop, AddressOf OnConverterDragDrop
            root.Controls.Add(inputRow, 0, 1)

            Dim outputRow As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 3,
                .RowCount = 1,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            outputRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 180.0F))
            outputRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 12.0F))
            outputRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
            outputRow.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            Dim outputCaption = CreateOfficialCaption("输出目录")
            outputCaption.TextAlign = ContentAlignment.MiddleLeft
            outputCaption.Padding = New Padding(12, 0, 0, 0)
            _lblConvertOutput.Text = "<font color=#888888>选择模型后自动确定</font>"
            _lblConvertOutput.AutoSize = False
            _lblConvertOutput.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            outputRow.Controls.Add(outputCaption, 0, 0)
            outputRow.Controls.Add(CreateOfficialValueBox(_lblConvertOutput), 2, 0)
            root.Controls.Add(outputRow, 0, 2)

            Dim information As New HtmlColorLabel With {
                .Dock = DockStyle.Fill,
                .Margin = New Padding(0, 18, 0, 8),
                .Padding = Padding.Empty,
                .BackColor1 = Color.Transparent,
                .BorderSize = 0,
                .ForeColor = UiTextMuted,
                .AutoSize = False,
                .LineSpacing = 7,
                .TextAlign = HtmlColorLabel.TextAlignEnum.TopLeft,
                .Text = "<font color=#DCDCDC><b>PTH → TensorRT Engine</b></font><br/>" &
                        "<font color=#888888>输出会归档到 models\TensorRT-Personalized，与预置引擎分开管理。</font><br/>" &
                        "<font color=#888888>转换完全在本机进行，不会上传模型；复杂模型可能需要数分钟。</font><br/>" &
                        "<font color=#888888>Engine 与显卡、TensorRT 和 CUDA 版本绑定，换设备后建议重新转换。</font>"
            }
            root.Controls.Add(information, 0, 3)

            Dim actionRow As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 1,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            actionRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 190.0F))
            actionRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
            actionRow.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            _btnConvert.Text = "开始离线转换"
            _btnConvert.Dock = DockStyle.Fill
            _btnConvert.Margin = New Padding(0, 9, 0, 9)
            _btnConvert.Enabled = False
            ConfigurePrimaryButton(_btnConvert)
            AddHandler _btnConvert.Click, AddressOf OnConvertModelClick
            _lblConvertStatus.Text = "<font color=#888888>等待选择模型…</font>"
            _lblConvertStatus.AutoSize = False
            _lblConvertStatus.Dock = DockStyle.Fill
            _lblConvertStatus.Margin = New Padding(16, 0, 0, 0)
            _lblConvertStatus.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            actionRow.Controls.Add(_btnConvert, 0, 0)
            actionRow.Controls.Add(_lblConvertStatus, 1, 0)
            root.Controls.Add(actionRow, 0, 4)
            _pageConverter.Controls.Add(root)
        End Sub

        Private Sub BuildMarkdownPage(page As Panel, markdown As String)
            page.Dock = DockStyle.Fill
            page.BackColor = Color.Transparent
            page.Padding = New Padding(0, 8, 0, 0)
            Dim browser As New WebBrowser With {
                .Dock = DockStyle.Fill, .AllowWebBrowserDrop = False,
                .IsWebBrowserContextMenuEnabled = False, .WebBrowserShortcutsEnabled = False,
                .ScriptErrorsSuppressed = True, .ScrollBarsEnabled = True
            }
            browser.DocumentText = MarkdownDocument(markdown)
            page.Controls.Add(browser)
        End Sub

        Private Shared Function MarkdownDocument(markdown As String) As String
            Dim body As New StringBuilder()
            Dim inList = False
            Dim lineFeed As Char = Convert.ToChar(10)
            For Each raw As String In If(markdown, "").Replace(Environment.NewLine, lineFeed.ToString()).Split(New Char() {lineFeed})
                Dim line = raw.TrimEnd()
                If line.StartsWith("### ") Then
                    If inList Then body.Append("</ul>") : inList = False
                    body.Append("<h3>").Append(InlineMarkdown(line.Substring(4))).Append("</h3>")
                ElseIf line.StartsWith("## ") Then
                    If inList Then body.Append("</ul>") : inList = False
                    body.Append("<h2>").Append(InlineMarkdown(line.Substring(3))).Append("</h2>")
                ElseIf line.StartsWith("# ") Then
                    If inList Then body.Append("</ul>") : inList = False
                    body.Append("<h1>").Append(InlineMarkdown(line.Substring(2))).Append("</h1>")
                ElseIf line.StartsWith("- ") OrElse line.StartsWith("* ") Then
                    If Not inList Then body.Append("<ul>") : inList = True
                    body.Append("<li>").Append(InlineMarkdown(line.Substring(2))).Append("</li>")
                ElseIf String.IsNullOrWhiteSpace(line) Then
                    If inList Then body.Append("</ul>") : inList = False
                Else
                    If inList Then body.Append("</ul>") : inList = False
                    body.Append("<p>").Append(InlineMarkdown(line)).Append("</p>")
                End If
            Next
            If inList Then body.Append("</ul>")
            Return "<!doctype html><html><head><meta charset='utf-8'><style>" &
                "html{background:#181818;scrollbar-face-color:#454545;scrollbar-track-color:#181818;scrollbar-arrow-color:#888;}" &
                "body{box-sizing:border-box;max-width:1080px;background:#181818;color:#989898;font-family:'Microsoft YaHei UI','Segoe UI',sans-serif;margin:0;padding:14px 10px 38px;}" &
                "h1{font-size:21px;font-weight:400;color:#dcdcdc;margin:0 0 16px;padding:0;}" &
                "h2{font-size:16px;font-weight:400;color:#d0d0d0;margin:18px 0 8px;}h3{font-size:15px;color:#c8c8c8;}" &
                "p,li{font-size:13px;line-height:1.65;}p{margin:4px 0 10px;}ul{padding:0 0 0 24px;margin:4px 0 12px;}" &
                "li{padding:2px 0;}strong{color:#dcdcdc}code{background:#383838;padding:3px 6px;border-radius:5px;color:#9bc8ff}a{color:#479cff;}" &
                "::-webkit-scrollbar{width:8px}::-webkit-scrollbar-track{background:#181818}::-webkit-scrollbar-thumb{background:#484848;border-radius:4px}</style></head><body>" &
                body.ToString() & "</body></html>"
        End Function

        Private Shared Function InlineMarkdown(text As String) As String
            Dim value = System.Net.WebUtility.HtmlEncode(If(text, ""))
            value = Regex.Replace(value, "\*\*(.+?)\*\*", "<strong>$1</strong>")
            value = Regex.Replace(value, "`(.+?)`", "<code>$1</code>")
            value = Regex.Replace(value, "\[(.+?)\]\((https?://[^\s)]+)\)", "<a href='$2'>$1</a>")
            Return value
        End Function

        Private Sub BuildConverterPage()
            _pageConverter.Dock = DockStyle.Fill
            _pageConverter.BackColor = Color.Transparent
            _pageConverter.Padding = New Padding(8, 14, 8, 10)
            _pageConverter.AllowDrop = True
            AddHandler _pageConverter.DragEnter, AddressOf OnConverterDragEnter
            AddHandler _pageConverter.DragDrop, AddressOf OnConverterDragDrop

            Dim workspace As New FluentCardPanel() With {
                .Dock = DockStyle.Fill, .FillColor = UiSurface,
                .StrokeColor = UiStrokeSoft, .CornerRadius = 12
            }
            Dim dropZone As New FluentCardPanel() With {
                .FillColor = UiSurfaceRaised, .StrokeColor = Color.FromArgb(104, UiAccent), .CornerRadius = 12,
                .AllowDrop = True
            }
            AddHandler dropZone.DragEnter, AddressOf OnConverterDragEnter
            AddHandler dropZone.DragDrop, AddressOf OnConverterDragDrop
            Dim dropIcon As HtmlColorLabel = CreateHtmlTextLabel("PTH", 22.0F, FontStyle.Bold, UiAccent)
            dropIcon.TextAlign = HtmlColorLabel.TextAlignEnum.Center
            Dim dropTitle As Label = CreateTextLabel("拖入 PTH 模型", 12.0F, FontStyle.Bold, UiText)
            dropTitle.TextAlign = ContentAlignment.MiddleCenter
            Dim dropHint As Label = CreateTextLabel("或从磁盘选择一个 .pth 文件", 8.8F, FontStyle.Regular, UiTextMuted)
            dropHint.TextAlign = ContentAlignment.MiddleCenter
            _btnPickPth.Text = "选择模型"
            _btnPickPth.Size = New Size(148, 40)
            ConfigureSecondaryButton(_btnPickPth)
            AddHandler _btnPickPth.Click, AddressOf OnPickPthClick
            dropZone.Controls.AddRange(New Control() {dropIcon, dropTitle, dropHint, _btnPickPth})

            Dim detailKicker As Label = CreateTextLabel("OFFLINE CONVERSION", 8.0F, FontStyle.Bold, UiAccent)
            Dim detailTitle As Label = CreateTextLabel("PTH → TensorRT Engine", 15.0F, FontStyle.Bold, UiText)
            Dim detailDesc As Label = CreateTextLabel("为当前 NVIDIA 显卡生成专用 Engine，提升吞吐并降低推理开销。整个过程只在本机进行。", 9.0F, FontStyle.Regular, UiTextSecondary)
            Dim compatibility As Label = CreateTextLabel("注意：Engine 与显卡、TensorRT 和 CUDA 版本绑定，换设备后建议重新转换。", 8.7F, FontStyle.Regular, UiTextMuted)

            _lblConvertInput.Text = "<font color=#7E8C9D>输入模型</font><br/><font color=#B1BCCA>尚未选择 .pth 文件</font>"
            _lblConvertInput.AutoSize = False
            _lblConvertInput.TextAlign = HtmlColorLabel.TextAlignEnum.TopLeft
            _lblConvertOutput.Text = "<font color=#7E8C9D>输出目录</font><br/><font color=#B1BCCA>选择模型后自动确定</font>"
            _lblConvertOutput.AutoSize = False
            _lblConvertOutput.TextAlign = HtmlColorLabel.TextAlignEnum.TopLeft
            _lblConvertStatus.Text = "<font color=#7E8C9D>等待选择模型…</font>"
            _lblConvertStatus.AutoSize = False
            _lblConvertStatus.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _btnConvert.Text = "开始转换  →"
            _btnConvert.Size = New Size(164, 42)
            _btnConvert.Enabled = False
            ConfigurePrimaryButton(_btnConvert)
            AddHandler _btnConvert.Click, AddressOf OnConvertModelClick
            workspace.Controls.AddRange(New Control() {
                dropZone, detailKicker, detailTitle, detailDesc, compatibility,
                _lblConvertInput, _lblConvertOutput, _btnConvert, _lblConvertStatus
            })
            _pageConverter.Controls.Add(workspace)

            Dim headerHost As New Panel() With {
                .Dock = DockStyle.Top, .Height = 96, .BackColor = Color.Transparent,
                .Padding = New Padding(0, 0, 0, 12)
            }
            headerHost.Controls.Add(CreatePageHeader("◇", "模型转换", "将 PyTorch 模型离线编译为 TensorRT Engine，并自动归档到个性化模型目录。"))
            _pageConverter.Controls.Add(headerHost)

            Dim arranging As Boolean = False
            Dim arrange As Action =
                Sub()
                    If arranging Then Return
                    arranging = True
                    Try
                        Dim pad = 22
                        Dim gap = 28
                        Dim leftWidth = Math.Max(300, Math.Min(470, CInt(workspace.ClientSize.Width * 0.36)))
                        dropZone.SetBounds(pad, pad, leftWidth, Math.Max(300, workspace.ClientSize.Height - pad * 2))
                        dropIcon.SetBounds(20, Math.Max(38, dropZone.ClientSize.Height \ 2 - 110), dropZone.ClientSize.Width - 40, 64)
                        dropTitle.SetBounds(20, dropIcon.Bottom, dropZone.ClientSize.Width - 40, 34)
                        dropHint.SetBounds(20, dropTitle.Bottom + 2, dropZone.ClientSize.Width - 40, 28)
                        _btnPickPth.Location = New Point((dropZone.ClientSize.Width - _btnPickPth.Width) \ 2, dropHint.Bottom + 18)

                        Dim left = dropZone.Right + gap
                        Dim width = Math.Max(320, workspace.ClientSize.Width - left - pad)
                        detailKicker.SetBounds(left, 30, width, 24)
                        detailTitle.SetBounds(left, 54, width, 42)
                        detailDesc.SetBounds(left, 99, width, 52)
                        compatibility.SetBounds(left, 151, width, 42)
                        Dim fieldTop = Math.Max(206, CInt(workspace.ClientSize.Height * 0.43))
                        _lblConvertInput.SetBounds(left, fieldTop, width, 58)
                        _lblConvertOutput.SetBounds(left, fieldTop + 68, width, 58)
                        _btnConvert.Location = New Point(left, Math.Min(workspace.ClientSize.Height - 64, fieldTop + 148))
                        _lblConvertStatus.SetBounds(_btnConvert.Right + 16, _btnConvert.Top - 4,
                                                    Math.Max(140, width - _btnConvert.Width - 16), 50)
                    Finally
                        arranging = False
                    End Try
                End Sub
            AddHandler workspace.Resize, Sub(sender, e) arrange()
            arrange()
        End Sub

        Private Sub BuildConverterPageLegacy()
            _pageConverter.Dock = DockStyle.Fill
            _pageConverter.BackColor = Color.Transparent
            _pageConverter.Padding = New Padding(0, 18, 0, 0)
            _pageConverter.AllowDrop = True
            AddHandler _pageConverter.DragEnter, AddressOf OnConverterDragEnter
            AddHandler _pageConverter.DragDrop, AddressOf OnConverterDragDrop

            Dim actionRow As New Panel() With {.Dock = DockStyle.Top, .Height = 58, .BackColor = Color.Transparent, .Padding = New Padding(0, 10, 0, 0)}
            _btnPickPth.Text = "选择或拖入 PTH 模型"
            _btnPickPth.Size = New Size(200, 38)
            _btnPickPth.Dock = DockStyle.Left
            _btnPickPth.BorderRadius = 8
            _btnPickPth.BorderSize = 0
            _btnPickPth.BackColor1 = Color.FromArgb(40, 110, 190, 255)
            _btnPickPth.HoverBackColor1 = Color.FromArgb(60, 110, 190, 255)
            AddHandler _btnPickPth.Click, AddressOf OnPickPthClick
            actionRow.Controls.Add(_btnPickPth)

            _btnConvert.Text = "开始离线转换"
            _btnConvert.Size = New Size(160, 38)
            _btnConvert.Dock = DockStyle.Left
            _btnConvert.Margin = New Padding(12, 0, 0, 0)
            _btnConvert.BorderRadius = 8
            _btnConvert.BorderSize = 0
            _btnConvert.Enabled = False
            AddHandler _btnConvert.Click, AddressOf OnConvertModelClick
            actionRow.Controls.Add(_btnConvert)
            _pageConverter.Controls.Add(actionRow)

            Dim info As New HtmlColorLabel() With {
                .Text = "<font color=#D8D8D8><b>PTH → TensorRT Engine</b></font><br/>" &
                        "<font color=#8A8A8A>拖入一个 .pth 模型后，输出目录会自动设为 models\TensorRT-Personalized，和预置引擎分开管理。</font><br/>" &
                        "<font color=#8A8A8A>TensorRT 通常能获得更高吞吐与更低推理开销；转换完全离线进行，不会上传模型。</font><br/>" &
                        "<font color=#8A8A8A>Engine 与显卡、TensorRT/CUDA 版本相关，建议在实际使用的设备上重新转换。</font>",
                .AutoSize = False,
                .Dock = DockStyle.Top,
                .Height = 112,
                .TextAlign = HtmlColorLabel.TextAlignEnum.TopLeft,
                .LineSpacing = 4
            }
            _pageConverter.Controls.Add(info)

            _lblConvertInput.Text = "<font color=#A8A8A8>输入模型：</font><font color=#7F7F7F>请拖入或选择 .pth 文件</font>"
            _lblConvertInput.AutoSize = False
            _lblConvertInput.Dock = DockStyle.Top
            _lblConvertInput.Height = 38
            _lblConvertInput.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _pageConverter.Controls.Add(_lblConvertInput)

            _lblConvertOutput.Text = "<font color=#A8A8A8>输出目录：</font><font color=#7F7F7F>选择模型后自动确定</font>"
            _lblConvertOutput.AutoSize = False
            _lblConvertOutput.Dock = DockStyle.Top
            _lblConvertOutput.Height = 38
            _lblConvertOutput.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _pageConverter.Controls.Add(_lblConvertOutput)

            _lblConvertStatus.Text = "<font color=#8A8A8A>等待选择模型…</font>"
            _lblConvertStatus.AutoSize = False
            _lblConvertStatus.Dock = DockStyle.Top
            _lblConvertStatus.Height = 52
            _lblConvertStatus.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _pageConverter.Controls.Add(_lblConvertStatus)
            ' DockStyle.Top 按 Z 序布局，固定为：说明 → 输入 → 输出 → 操作 → 状态。
            _pageConverter.Controls.SetChildIndex(info, 0)
            _pageConverter.Controls.SetChildIndex(_lblConvertInput, 1)
            _pageConverter.Controls.SetChildIndex(_lblConvertOutput, 2)
            _pageConverter.Controls.SetChildIndex(actionRow, 3)
            _pageConverter.Controls.SetChildIndex(_lblConvertStatus, 4)
        End Sub

        Private Sub OnConverterDragEnter(sender As Object, e As DragEventArgs)
            If e.Data IsNot Nothing AndAlso e.Data.GetDataPresent(DataFormats.FileDrop) Then
                Dim paths = TryCast(e.Data.GetData(DataFormats.FileDrop), String())
                If paths IsNot Nothing AndAlso paths.Length > 0 AndAlso
                    String.Equals(Path.GetExtension(paths(0)), ".pth", StringComparison.OrdinalIgnoreCase) Then
                    e.Effect = DragDropEffects.Copy
                    Return
                End If
            End If
            e.Effect = DragDropEffects.None
        End Sub

        Private Sub OnConverterDragDrop(sender As Object, e As DragEventArgs)
            Dim paths = TryCast(e.Data.GetData(DataFormats.FileDrop), String())
            If paths IsNot Nothing AndAlso paths.Length > 0 Then
                SelectConverterInput(paths(0))
            End If
        End Sub

        Private Sub OnPickPthClick(sender As Object, e As EventArgs)
            Using dialog As New OpenFileDialog With {
                .Title = "选择要转换的 PTH 模型",
                .Filter = "PyTorch 模型 (*.pth)|*.pth",
                .CheckFileExists = True,
                .Multiselect = False
            }
                If dialog.ShowDialog(Me) = DialogResult.OK Then
                    SelectConverterInput(dialog.FileName)
                End If
            End Using
        End Sub

        Private Sub SelectConverterInput(modelPath As String)
            If Not File.Exists(modelPath) OrElse Not String.Equals(Path.GetExtension(modelPath), ".pth", StringComparison.OrdinalIgnoreCase) Then
                SetConverterStatus("只支持拖入有效的 .pth 模型文件。", True)
                Return
            End If
            If _switchInterp.Checked AndAlso _config.Backend = "onnx" Then
                _syncingInterpSwitch = True
                _switchInterp.Checked = False
                _syncingInterpSwitch = False
                ShowStatus("ONNX Runtime 当前用于超分模型；补帧请切换到 NCNN 或 CUDA。", True)
                Return
            End If
            _convertInputPath = Path.GetFullPath(modelPath)
            Dim outputDir = GetPersonalizedTensorRtDirectory()
            _lblConvertInput.Text = "<font color=#DCDCDC>" & EscapeHtml(_convertInputPath) & "</font>"
            _lblConvertOutput.Text = "<font color=#DCDCDC>" & EscapeHtml(outputDir) & "</font>"
            _btnConvert.Enabled = Not _conversionRunning
            SetConverterStatus("模型已就绪，点击「开始离线转换」。", False)
        End Sub

        Private Async Sub OnConvertModelClick(sender As Object, e As EventArgs)
            If _conversionRunning OrElse Not File.Exists(_convertInputPath) Then Return
            Dim coreRoot = ResolveCoreRoot()
            Dim pythonExe = Path.Combine(coreRoot, "python", "python", "python.exe")
            Dim converter = Path.Combine(coreRoot, "python", "backend", "convert_tensorrt.py")
            Dim outputDir = GetPersonalizedTensorRtDirectory()
            If Not File.Exists(pythonExe) OrElse Not File.Exists(converter) Then
                SetConverterStatus("找不到便携 Python 或 convert_tensorrt.py，请检查 videoenhancer.exe 的 core-path。", True)
                Return
            End If

            Directory.CreateDirectory(outputDir)
            _conversionRunning = True
            _btnConvert.Enabled = False
            _btnPickPth.Enabled = False
            SetConverterStatus("正在离线编译 TensorRT Engine；复杂模型可能需要数分钟，请勿关闭程序…", False)
            Try
                Dim result = Await Task.Run(Function() RunTensorRtConversion(pythonExe, converter, _convertInputPath, outputDir))
                If result.Item1 = 0 Then
                    Dim enginePath = LastNonEmptyLine(result.Item2)
                    SetConverterStatus("转换完成：" & If(String.IsNullOrWhiteSpace(enginePath), outputDir, enginePath), False)
                    If _config.Backend = "tensorrt" Then RefreshUpscaleModels()
                Else
                    SetConverterStatus("转换失败：" & LastNonEmptyLine(result.Item2), True)
                End If
            Catch ex As Exception
                SetConverterStatus("转换失败：" & ex.Message, True)
            Finally
                _conversionRunning = False
                _btnPickPth.Enabled = True
                _btnConvert.Enabled = File.Exists(_convertInputPath)
            End Try
        End Sub

        Private Shared Function RunTensorRtConversion(pythonExe As String, converter As String, inputPath As String, outputDir As String) As Tuple(Of Integer, String)
            Dim psi As New ProcessStartInfo With {
                .FileName = pythonExe,
                .WorkingDirectory = Path.GetDirectoryName(converter),
                .UseShellExecute = False,
                .CreateNoWindow = True,
                .RedirectStandardOutput = True,
                .RedirectStandardError = True,
                .StandardOutputEncoding = Encoding.UTF8,
                .StandardErrorEncoding = Encoding.UTF8
            }
            psi.ArgumentList.Add(converter)
            psi.ArgumentList.Add(inputPath)
            psi.ArgumentList.Add("--output-dir")
            psi.ArgumentList.Add(outputDir)
            Using child As Diagnostics.Process = Diagnostics.Process.Start(psi)
                If child Is Nothing Then Return New Tuple(Of Integer, String)(1, "无法启动模型转换进程")
                Dim stdoutTask = child.StandardOutput.ReadToEndAsync()
                Dim stderrTask = child.StandardError.ReadToEndAsync()
                child.WaitForExit()
                Task.WaitAll(stdoutTask, stderrTask)
                Return New Tuple(Of Integer, String)(child.ExitCode, stdoutTask.Result & Environment.NewLine & stderrTask.Result)
            End Using
        End Function

        Private Function ResolveCoreRoot() As String
            Dim exeDir = If(File.Exists(_config.ExePath), Path.GetDirectoryName(_config.ExePath), AppDomain.CurrentDomain.BaseDirectory)
            Dim iniPath = Path.Combine(exeDir, "videoenhancer.ini")
            Try
                If File.Exists(iniPath) Then
                    For Each rawLine In File.ReadLines(iniPath)
                        Dim line = rawLine.Trim()
                        If line.StartsWith("core-path", StringComparison.OrdinalIgnoreCase) Then
                            Dim equalsAt = line.IndexOf("="c)
                            If equalsAt >= 0 Then
                                Dim value = line.Substring(equalsAt + 1).Trim().Trim(""""c)
                                If Not Path.IsPathRooted(value) Then value = Path.GetFullPath(Path.Combine(exeDir, value))
                                If Directory.Exists(value) Then Return value
                            End If
                        End If
                    Next
                End If
            Catch
            End Try
            Return exeDir
        End Function

        Private Function GetPersonalizedTensorRtDirectory() As String
            Return Path.Combine(ResolveCoreRoot(), "models", "TensorRT-Personalized")
        End Function

        Private Sub SetConverterStatus(text As String, isError As Boolean)
            Dim color = If(isError, "#F4707A", "#53D2A2")
            _lblConvertStatus.Text = "<font color=" & color & ">" & EscapeHtml(If(text, "")) & "</font>"
        End Sub

        Private Shared Function LastNonEmptyLine(text As String) As String
            If String.IsNullOrWhiteSpace(text) Then Return "未返回详细信息"
            Dim lines = text.Replace(Convert.ToChar(13), Convert.ToChar(10)).Split(Convert.ToChar(10))
            For i As Integer = lines.Length - 1 To 0 Step -1
                If Not String.IsNullOrWhiteSpace(lines(i)) Then Return lines(i).Trim()
            Next
            Return "未返回详细信息"
        End Function

        ''' <summary>从 CLI 标准错误中提取可直接展示给用户的错误正文。</summary>
        Private Shared Function CliErrorMessage(text As String, fallback As String) As String
            If String.IsNullOrWhiteSpace(text) Then Return fallback
            Dim lines = text.Replace(Convert.ToChar(13), Convert.ToChar(10)).Split(Convert.ToChar(10))
            For Each rawLine In lines
                Dim line = rawLine.Trim()
                If line.StartsWith("[错误]", StringComparison.Ordinal) Then
                    Return line.Substring(4).Trim()
                End If
            Next
            For Each rawLine In lines
                Dim line = rawLine.Trim()
                If line.Length > 0 AndAlso Not line.Contains("|") Then Return line
            Next
            Return fallback
        End Function

        ' ────────────────────────── 预览事件 / 工具 ──────────────────────────

        ''' <summary>
        ''' 编码队列右键「预览输出」入口：切换到「实时预览」页并选中对应任务。
        ''' 任务不在执行中时仍记录为待选，等它开始执行后自动选中。
        ''' </summary>
        Public Sub ShowPreviewForTask(taskId As String)
            Try
                _pendingPreviewTaskId = If(taskId, "")
                If _engine IsNot Nothing Then
                    _engine.SelectedTaskId = _pendingPreviewTaskId
                End If
                ' 先切到 3FUI 主界面的「视频超分」页（左侧导航），再切到内部「实时预览」选项卡
                ActivatePluginPage()
                If _tabs IsNot Nothing Then
                    _tabs.SelectedIndex = 1
                End If
                TrySelectPendingTask()
            Catch
            End Try
        End Sub

        ''' <summary>把 3FUI 主窗体左侧导航切换到「视频超分」插件页（插件面板在 FormMain_v6 的 ModernTabListControl1 中）。</summary>
        Private Shared Sub ActivatePluginPage()
            Try
                Dim mainForm = HostAccess.GetDefaultInstance("FormMain_v6")
                If mainForm Is Nothing Then
                    Return
                End If
                Dim tabList = HostAccess.GetField(mainForm, "_ModernTabListControl1", "ModernTabListControl1")
                If tabList Is Nothing Then
                    Return
                End If
                Dim itemsProp = tabList.GetType().GetProperty("Items")
                Dim selProp = tabList.GetType().GetProperty("SelectedIndex")
                If itemsProp Is Nothing OrElse selProp Is Nothing Then
                    Return
                End If
                Dim items = TryCast(itemsProp.GetValue(tabList), System.Collections.IEnumerable)
                If items Is Nothing Then
                    Return
                End If
                Dim idx = 0
                For Each item As Object In items
                    If item IsNot Nothing Then
                        Dim textProp = item.GetType().GetProperty("Text")
                        If textProp IsNot Nothing Then
                            Dim text = TryCast(textProp.GetValue(item), String)
                            If String.Equals(text, "视频超分", StringComparison.OrdinalIgnoreCase) Then
                                selProp.SetValue(tabList, idx)
                                Return
                            End If
                        End If
                    End If
                    idx += 1
                Next
            Catch
            End Try
        End Sub

        ''' <summary>如果待选任务在当前任务列表里，选中它并清除待选标记。</summary>
        Private Sub TrySelectPendingTask()
            If String.IsNullOrWhiteSpace(_pendingPreviewTaskId) Then
                Return
            End If
            For i As Integer = 0 To _taskIds.Count - 1
                If String.Equals(_taskIds(i), _pendingPreviewTaskId, StringComparison.Ordinal) Then
                    _cmbTask.SelectedIndex = i
                    _pendingPreviewTaskId = ""
                    Return
                End If
            Next
        End Sub

        Private Sub OnRateSelected(sender As Object, e As EventArgs)
            If _engine Is Nothing Then
                Return
            End If
            Select Case _cmbRate.SelectedIndex
                Case 0 : _engine.IntervalSeconds = 0.5
                Case 1 : _engine.IntervalSeconds = 1.0
                Case 2 : _engine.IntervalSeconds = 2.0
                Case 3 : _engine.IntervalSeconds = 3.0
                Case 4 : _engine.SetKeyframeMode(True)
            End Select
        End Sub

        Private Sub OnTaskSelected(sender As Object, e As EventArgs)
            If _engine Is Nothing Then
                Return
            End If
            Dim idx = _cmbTask.SelectedIndex
            If idx >= 0 AndAlso idx < _taskIds.Count Then
                _engine.SelectedTaskId = _taskIds(idx)
            End If
        End Sub

        Private Sub OnPreviewTasksChanged(sender As Object, tasks As List(Of PreviewTaskInfo))
            Try
                Dim selectedId As String = ""
                If _cmbTask.SelectedIndex >= 0 AndAlso _cmbTask.SelectedIndex < _taskIds.Count Then
                    selectedId = _taskIds(_cmbTask.SelectedIndex)
                End If
                _cmbTask.Items.Clear()
                _taskIds.Clear()
                If tasks.Count = 0 Then
                    _cmbTask.WaterText = "暂无执行中的任务"
                    Return
                End If
                Dim index = 0
                For i As Integer = 0 To tasks.Count - 1
                    _cmbTask.Items.Add(tasks(i).ToString())
                    _taskIds.Add(tasks(i).Id)
                    If String.Equals(tasks(i).Id, selectedId, StringComparison.Ordinal) Then
                        index = i
                    End If
                Next
                ' 待选任务优先（右键「预览输出」）；否则保持原选择，默认最上面一个
                Dim pendingIndex = -1
                If Not String.IsNullOrWhiteSpace(_pendingPreviewTaskId) Then
                    For i As Integer = 0 To _taskIds.Count - 1
                        If String.Equals(_taskIds(i), _pendingPreviewTaskId, StringComparison.Ordinal) Then
                            pendingIndex = i
                            Exit For
                        End If
                    Next
                End If
                If pendingIndex >= 0 Then
                    _cmbTask.SelectedIndex = pendingIndex
                    _pendingPreviewTaskId = ""
                Else
                    _cmbTask.SelectedIndex = index
                End If
            Catch
            End Try
        End Sub

        Private Sub OnPreviewFrameReady(sender As Object, image As Image)
            If image Is Nothing Then
                Return
            End If
            If _lastPreviewImage IsNot Nothing AndAlso Not ReferenceEquals(_lastPreviewImage, image) Then
                _lastPreviewImage.Dispose()
            End If
            _lastPreviewImage = image
            Try
                _picPreview.Image = image
            Catch
            End Try
        End Sub

        Private Sub OnPreviewStatusChanged(sender As Object, text As String, isError As Boolean)
            Dim color = If(isError, "#E07878", "#A8B8A8")
            _lblPreviewStatus.Text = "<font color=" & color & ">" & EscapeHtml(text) & "</font>"
        End Sub

        Private Sub OnTabChanged(sender As Object, e As EventArgs)
            If _engine IsNot Nothing Then
                _engine.PreviewVisible = (_tabs.SelectedIndex = 1)
            End If
            ' 切换页面时清除底部状态提示
            ClearStatus()
            _btnCleanArchives.Visible = (_tabs.SelectedIndex = 3)
            If _tabs.SelectedIndex = 3 Then
                LoadDownloadModels(False)
            End If
        End Sub

        Private Sub OnStatusClearTick(sender As Object, e As EventArgs)
            ClearStatus()
        End Sub

        Private Sub ClearStatus()
            Try
                _statusClearTimer.Stop()
            Catch
            End Try
            If _uiReady Then
                _lblStatus.Text = "<font color=#B8B8B8>就绪</font>"
            End If
        End Sub

        Private Sub OnQuadClick(sender As Object, e As EventArgs)
            If _quadForm Is Nothing OrElse _quadForm.IsDisposed Then
                _quadForm = New QuadGridForm(_config)
            End If
            Try
                If Not _quadForm.Visible Then
                    _quadForm.Show(Me)
                Else
                    _quadForm.Activate()
                End If
            Catch
            End Try
        End Sub

        Private Shared Function EscapeHtml(text As String) As String
            If String.IsNullOrEmpty(text) Then
                Return text
            End If
            Return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
        End Function

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                If Current Is Me Then
                    Current = Nothing
                End If
                Try
                    _statusClearTimer.Stop()
                    _statusClearTimer.Dispose()
                Catch
                End Try
                Try
                    _queueMenuTimer.Stop()
                    _queueMenuTimer.Dispose()
                Catch
                End Try
                If _quadForm IsNot Nothing Then
                    Try
                        _quadForm.Dispose()
                    Catch
                    End Try
                    _quadForm = Nothing
                End If
                If _engine IsNot Nothing Then
                    Try
                        _engine.Dispose()
                    Catch
                    End Try
                    _engine = Nothing
                End If
                If _lastPreviewImage IsNot Nothing Then
                    Try
                        _lastPreviewImage.Dispose()
                    Catch
                    End Try
                    _lastPreviewImage = Nothing
                End If
            End If
            MyBase.Dispose(disposing)
        End Sub

        Private Sub UpdateModeStateLabels()
            _lblMaster.Text = If(_config.Enabled,
                "<font color=#3FCD87><b>插件已启用</b></font>",
                "<font color=#888888><b>插件已关闭</b></font>")
            _lblSwitch.Text = If(_config.UpscaleEnabled,
                "<font color=#479CFF><b>已开启</b></font>",
                "<font color=#888888>关闭</font>")
            _lblSwitchInterp.Text = If(_config.InterpEnabled,
                "<font color=#3FCD87><b>已开启</b></font>",
                "<font color=#888888>关闭</font>")
        End Sub

        Private Sub RefreshUi()
            If Not _uiReady Then
                Return
            End If
            ' 插件总开关：同步配置状态
            _syncingMaster = True
            _switchMaster.Checked = _config.Enabled
            _syncingMaster = False
            ' 超分开关：仅主开关开启时可操作
            _syncingSwitch = True
            _switchUpscale.Checked = _config.UpscaleEnabled
            _switchUpscale.Enabled = _config.Enabled
            _syncingSwitch = False
            ' 补帧开关：仅主开关开启时可操作
            _syncingInterpSwitch = True
            _switchInterp.Checked = _config.InterpEnabled
            _switchInterp.Enabled = _config.Enabled
            _syncingInterpSwitch = False
            ' 推理方式 / 补帧倍率：仅主开关开启时可操作
            _syncingBackend = True
            SyncBackendCombo()
            _cmbBackend.Enabled = _config.Enabled
            _syncingBackend = False
            _syncingFactor = True
            SyncFactorCombo()
            _cmbFactor.Enabled = _config.Enabled
            _syncingFactor = False
            UpdateModeStateLabels()
            If String.IsNullOrWhiteSpace(_config.ExePath) Then
                _lblExe.Text = "<font color=#888888>尚未指定 videoenhancer.exe</font>"
            Else
                _lblExe.Text = "<font color=#DCDCDC>" & EscapeHtml(_config.ExePath) & "</font>"
            End If
        End Sub

        ''' <summary>把配置的推理后端同步到下拉框（0=NCNN，1=CUDA，2=TensorRT，3=ONNX，4=FlashVSR）。</summary>
        Private Sub SyncBackendCombo()
            If _cmbBackend.Items.Count = 0 Then
                Return
            End If
            _cmbBackend.SelectedIndex = If(_config.Backend = "flashvsr", 4, If(_config.Backend = "onnx", 3, If(_config.Backend = "tensorrt", 2, If(_config.Backend = "cuda", 1, 0))))
        End Sub

        ''' <summary>把配置的补帧倍率同步到下拉框（2/3/4/8）。</summary>
        Private Sub SyncFactorCombo()
            If _cmbFactor.Items.Count = 0 Then
                Return
            End If
            Dim factor = If(_config.InterpFactor <= 1, 2.0, _config.InterpFactor)
            Dim idx = 0
            For i As Integer = 0 To _cmbFactor.Items.Count - 1
                If FactorValue(_cmbFactor.Items(i)) = factor Then
                    idx = i
                    Exit For
                End If
            Next
            _cmbFactor.SelectedIndex = idx
        End Sub

        Private Sub ShowStatus(text As String, error_ As Boolean)
            If Not _uiReady Then
                Return
            End If
            Try
                If IsHandleCreated Then
                    BeginInvoke(New Action(Sub() SetStatus(text, error_)))
                Else
                    SetStatus(text, error_)
                End If
            Catch
            End Try
        End Sub

        Private Sub SetStatus(text As String, error_ As Boolean)
            If error_ Then
                _lblStatus.Text = "<font color=#E07878>" & EscapeHtml(text) & "</font>"
            Else
                _lblStatus.Text = "<font color=#96D2A0>" & EscapeHtml(text) & "</font>"
            End If
            ' 错误提示（如"超分和补帧不能同时开启"）5 秒后自动消失
            If error_ Then
                Try
                    _statusClearTimer.Stop()
                    _statusClearTimer.Start()
                Catch
                End Try
            End If
        End Sub

    End Class

End Namespace
