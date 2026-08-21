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
        Private ReadOnly _cmbImageSuffix As New ModernComboBox()
        Private ReadOnly _lblImageInputs As New HtmlColorLabel()
        Private ReadOnly _lblImageOutput As New HtmlColorLabel()
        Private ReadOnly _lblImageProgress As New HtmlColorLabel()
        Private ReadOnly _imageProgress As New ProgressBar()
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

        Public Sub New(config As PluginConfig)
            _config = config
            Current = Me
            InitializeUi()
            AddHandler Load, AddressOf OnPanelLoad
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
            ' CLI 版本不一致或旧进程缓存时，直接从 exe 同级 models 目录补扫 TensorRT engine。
            If models.Count = 0 AndAlso String.Equals(_config.Backend, "tensorrt", StringComparison.OrdinalIgnoreCase) Then
                Try
                    Dim dirs = New List(Of String) From {
                        Path.Combine(Path.GetDirectoryName(_config.ExePath), "models"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models"),
                        "C:\PortableSoft\VideoEnhancer-CLI\models"
                    }
                    For Each modelDir In dirs.Distinct(StringComparer.OrdinalIgnoreCase)
                        If Not Directory.Exists(modelDir) Then Continue For
                        For Each p In Directory.GetFiles(modelDir, "*.engine", SearchOption.TopDirectoryOnly)
                            Dim n = Path.GetFileNameWithoutExtension(p)
                            If Not String.IsNullOrWhiteSpace(n) AndAlso Not models.Contains(n, StringComparer.OrdinalIgnoreCase) Then models.Add(n)
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
                    "（TensorRT，models 下的 .engine 文件）",
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
                    Dim missingExt = If(_config.Backend = "flashvsr", "FlashVSR 完整模型目录", If(_config.Backend = "tensorrt", ".engine", If(_config.Backend = "onnx", ".onnx", ".pth")))
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
                "TensorRT（NVIDIA）：超分用 models 下的 .engine 模型（仅 N 卡）",
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

        Private Sub InitializeUi()
            BackColor = Color.Transparent
            Dock = DockStyle.Fill

            Dim root As New Panel() With {.Dock = DockStyle.Fill, .BackColor = Color.Transparent, .Padding = New Padding(24)}
            Controls.Add(root)

            ' ── ModernTabControl 分栏：超分主界面 / 实时预览 / 高级功能 ──
            ' WinForms 按控件集合的逆序执行 Dock 布局：Fill 必须先加入、Bottom 最后加入，
            ' 这样状态栏先占底部，选项卡内容区自动让出底部空间，避免被状态栏覆盖/裁剪。
            BuildTabs()
            root.Controls.Add(_tabs)

            ' ── 底部状态栏（Dock=Bottom；最后加入 → 最先布局占底部）──
            Dim sectionStatus As New Panel() With {.Dock = DockStyle.Bottom, .Height = 44, .BackColor = Color.Transparent, .Padding = New Padding(0, 12, 0, 0)}
            _lblStatus.AutoSize = True
            _lblStatus.Dock = DockStyle.Fill
            _lblStatus.ForeColor = Color.FromArgb(190, 190, 190)
            _lblStatus.Text = "就绪"
            sectionStatus.Controls.Add(_lblStatus)
            _btnCleanArchives.Text = "清理临时文件"
            _btnCleanArchives.Size = New Size(132, 32)
            _btnCleanArchives.Dock = DockStyle.Right
            _btnCleanArchives.BorderRadius = 8
            _btnCleanArchives.BorderSize = 1
            _btnCleanArchives.BorderColor = Color.FromArgb(68, 68, 68)
            _btnCleanArchives.BackColor1 = Color.FromArgb(46, 46, 46)
            _btnCleanArchives.HoverBackColor1 = Color.FromArgb(58, 58, 58)
            _btnCleanArchives.Visible = False
            AddHandler _btnCleanArchives.Click, AddressOf OnCleanDownloadArchives
            sectionStatus.Controls.Add(_btnCleanArchives)
            _btnCleanArchives.BringToFront()
            root.Controls.Add(sectionStatus)
        End Sub

        ' ────────────────────────── 选项卡分栏 ──────────────────────────

        Private Sub BuildTabs()
            _tabs.Dock = DockStyle.Fill
            _tabs.ContentBackColor = Color.Transparent
            _tabs.TabStripHeight = 44
            _tabs.TabStripPadding = New Padding(6, 4, 6, 0)
            _tabs.TabItemTextPadding = 16
            _tabs.IndicatorHeight = 2
            _tabs.IndicatorPadding = 8
            _tabs.TabAlignment = ModernTabControl.TabAlignmentEnum.Left
            _tabs.Font = New Font("Microsoft YaHei UI", 9.5F)
            _tabs.AnimationDuration = 0
            _tabs.AnimationFPS = 30

            BuildUpscalePage()
            BuildPreviewPage()
            BuildAdvancedPage()
            BuildModelDownloadPage()
            BuildConverterPage()
            BuildMarkdownPage(_pageModelInfo, "# Markdown 渲染测试" & Environment.NewLine & Environment.NewLine & "这是模型简介页面的 **Markdown 测试文字**。")
            BuildMarkdownPage(_pageTutorial, "")

            Dim tabMain As New ModernTabControl.ModernTab("超分主界面") With {.BoundControl = _pageUpscale}
            Dim tabPreview As New ModernTabControl.ModernTab("实时预览") With {.BoundControl = _pagePreview}
            Dim tabAdvanced As New ModernTabControl.ModernTab("高级功能") With {.BoundControl = _pageAdvanced}
            Dim tabDownloader As New ModernTabControl.ModernTab("模型下载") With {.BoundControl = _pageDownloader}
            Dim tabConverter As New ModernTabControl.ModernTab("模型转换器") With {.BoundControl = _pageConverter}
            Dim tabModelInfo As New ModernTabControl.ModernTab("模型简介") With {.BoundControl = _pageModelInfo}
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

        Private Sub BuildUpscalePage()
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
            Dim imageSection = BuildImageUpscaleSection()

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

        Private Function BuildImageUpscaleSection() As Panel
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

        ''' <summary>BooleanSwitch 按宿主窗口的实际 DPI 重新计算尺寸（96 DPI 基准为 44×23）。</summary>
        Private Shared Sub ConfigureDpiSwitch(switchControl As LakeUI.BooleanSwitch)
            Dim applySize As Action =
                Sub()
                    Dim dpi = 96
                    If switchControl.FindForm() IsNot Nothing Then
                        dpi = switchControl.FindForm().DeviceDpi
                    ElseIf switchControl.IsHandleCreated Then
                        dpi = switchControl.DeviceDpi
                    End If
                    Dim scale = Math.Max(1.0F, CSng(dpi) / 96.0F)
                    switchControl.Size = New Size(CInt(Math.Round(44 * scale)), CInt(Math.Round(23 * scale)))
                End Sub
            AddHandler switchControl.HandleCreated, Sub(sender, e) applySize()
            AddHandler switchControl.DpiChangedAfterParent, Sub(sender, e) applySize()
            AddHandler switchControl.ParentChanged, Sub(sender, e) applySize()
            applySize()
        End Sub

        Private Shared Sub ConfigureImageButton(button As ModernButton, text As String, width As Integer)
            button.Text = text
            button.Size = New Size(width, 32)
            button.BorderRadius = 7
            button.BorderSize = 0
            button.BackColor1 = Color.FromArgb(42, 220, 220, 220)
            button.HoverBackColor1 = Color.FromArgb(62, 220, 220, 220)
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
                If Directory.Exists(_config.ImageOutput) Then dialog.SelectedPath = _config.ImageOutput
                If dialog.ShowDialog() = DialogResult.OK Then
                    _config.ImageOutput = dialog.SelectedPath
                    _config.Save()
                    RefreshImageOutputLabel()
                End If
            End Using
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
            _lblImageInputs.Text = "<font color=#D8D8D8>已选择 " & _imageFiles.Count & " 个文件、" & _imageFolders.Count & " 个递归文件夹</font>"
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

        Private Sub RefreshImageOutputLabel()
            _btnImageOutput.Enabled = Not _switchImageOriginal.Checked
            Dim text = If(_switchImageOriginal.Checked, "原图片所在目录", If(String.IsNullOrWhiteSpace(_config.ImageOutput), "尚未指定输出文件夹", _config.ImageOutput))
            _lblImageOutput.Text = "<font color=#A8A8A8>输出：</font><font color=#E0E0E0>" & EscapeHtml(text) & "</font>"
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
            If Not _switchImageOriginal.Checked AndAlso String.IsNullOrWhiteSpace(_config.ImageOutput) Then
                ShowStatus("请指定图片输出文件夹，或开启输出到原目录", True) : Return
            End If

            Dim args As New List(Of String)()
            For Each path In _imageFiles : args.Add("--image-input") : args.Add(path) : Next
            For Each path In _imageFolders : args.Add("--image-folder") : args.Add(path) : Next
            If _switchImageOriginal.Checked Then
                args.Add("--image-output-original")
            Else
                args.Add("--image-output") : args.Add(_config.ImageOutput)
            End If
            args.Add("--image-suffix") : args.Add(If(_cmbImageSuffix.SelectedIndex = 1, "model", "timestamp"))
            args.Add(If(_switchImagePng.Checked, "--image-png", "--image-source-format"))
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

        Private Sub BuildPreviewPage()
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

        Private Sub BuildModelDownloadPage()
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
            Dim loading As New Label() With {
                .Text = "正在读取在线模型列表…", .ForeColor = Color.FromArgb(170, 170, 170),
                .Size = New Size(520, 40), .TextAlign = ContentAlignment.MiddleLeft
            }
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
                                runningProcess.WaitForExit(45000)
                                stdout = outputTask.GetAwaiter().GetResult()
                                stderr = errorTask.GetAwaiter().GetResult()
                                exitCode = runningProcess.ExitCode
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
                _downloadOnline = False
                ShowOfflineDownloadStatus()
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
                Dim categoryOrder = New String() {"ONNX", "Param-Bin", "RIFE", "PTH", "Backend"}
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
            Dim groupPanel As New Panel() With {
                .Width = Math.Max(360, _downloadList.ClientSize.Width - 24), .Height = 54,
                .Margin = New Padding(0, 0, 0, 8), .BackColor = Color.Transparent
            }
            Dim header As New Panel() With {
                .Location = New Point(0, 0), .Height = 52, .Width = groupPanel.Width,
                .Anchor = AnchorStyles.Left Or AnchorStyles.Top Or AnchorStyles.Right,
                .BackColor = Color.FromArgb(34, 34, 38)
            }
            Dim title As New Label() With {
                .Text = DownloadCategoryTitle(category) & "  ·  " & entries.Count & " 个文件",
                .Location = New Point(16, 0), .Height = 52, .ForeColor = Color.FromArgb(235, 235, 238),
                .Font = New Font("Microsoft YaHei UI", 10.0F, FontStyle.Bold),
                .TextAlign = ContentAlignment.MiddleLeft, .AutoEllipsis = True
            }
            Dim expandButton As New ModernButton() With {
                .Text = "展开  ▾", .Size = New Size(92, 34), .BorderRadius = 7, .BorderSize = 0,
                .BackColor1 = Color.FromArgb(38, 255, 255, 255),
                .HoverBackColor1 = Color.FromArgb(58, 255, 255, 255)
            }
            Dim allButton As New ModernButton() With {
                .Text = "一键全部下载", .Size = New Size(142, 34),
                .Tag = entries.Select(Function(entry) entry.RelativePath).ToList(),
                .BorderRadius = 7, .BorderSize = 0,
                .BackColor1 = Color.FromArgb(52, 0, 120, 212),
                .HoverBackColor1 = Color.FromArgb(78, 0, 120, 212)
            }
            Dim content As New FlowLayoutPanel() With {
                .Location = New Point(0, 56), .Width = groupPanel.Width,
                .Height = Math.Max(1, entries.Count * 52), .Visible = False,
                .WrapContents = False, .FlowDirection = FlowDirection.TopDown,
                .AutoScroll = False, .BackColor = Color.Transparent, .Margin = New Padding(0)
            }
            For Each entry In entries
                content.Controls.Add(CreateDownloadRow(entry, content.Width))
            Next
            expandButton.Tag = New Object() {groupPanel, content}
            AddHandler expandButton.Click, AddressOf OnToggleDownloadGroup
            AddHandler allButton.Click, AddressOf OnDownloadAllClick
            Dim arrangeHeader As Action =
                Sub()
                    allButton.Left = Math.Max(250, header.ClientSize.Width - allButton.Width - 10)
                    allButton.Top = 9
                    expandButton.Left = allButton.Left - expandButton.Width - 8
                    expandButton.Top = 9
                    title.Width = Math.Max(120, expandButton.Left - title.Left - 10)
                    content.Width = groupPanel.ClientSize.Width
                    For Each row As Panel In content.Controls.OfType(Of Panel)()
                        row.Width = Math.Max(320, content.ClientSize.Width)
                    Next
                End Sub
            AddHandler header.Resize, Sub(sender, e) arrangeHeader()
            AddHandler groupPanel.Resize, Sub(sender, e) arrangeHeader()
            header.Controls.AddRange(New Control() {title, expandButton, allButton})
            groupPanel.Controls.AddRange(New Control() {header, content})
            _downloadList.Controls.Add(groupPanel)
            arrangeHeader()
        End Sub

        Private Function CreateDownloadRow(entry As DownloadModelEntry, rowWidth As Integer) As Panel
            Dim row As New Panel() With {
                .Width = Math.Max(320, rowWidth), .Height = 48,
                .Margin = New Padding(0, 0, 0, 4), .BackColor = Color.FromArgb(16, 255, 255, 255)
            }
            Dim sizeText = If(entry.Size > 0, "  ·  " & FormatDownloadSize(entry.Size), "")
            Dim label As New Label() With {
                .Text = entry.Name & sizeText, .ForeColor = Color.FromArgb(215, 215, 215),
                .Dock = DockStyle.Fill, .Padding = New Padding(12, 0, 8, 0),
                .TextAlign = ContentAlignment.MiddleLeft, .AutoEllipsis = True
            }
            Dim button As New ModernButton() With {
                .Text = "下载", .Dock = DockStyle.Right, .Width = 108, .Tag = entry.RelativePath,
                .BorderRadius = 7, .BorderSize = 0,
                .BackColor1 = Color.FromArgb(42, 110, 190, 255),
                .HoverBackColor1 = Color.FromArgb(68, 110, 190, 255)
            }
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
            groupPanel.Height = If(content.Visible, 56 + content.Height, 54)
            button.Text = If(content.Visible, "收起  ▴", "展开  ▾")
            _downloadList.PerformLayout()
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
                ShowStatus("模型下载失败", True)
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
                ShowStatus("批量下载在第 " & (completed + 1) & " 个文件处失败", True)
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
            _lblStatus.Text = "<font color=#E07878>当前无网络</font>"
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

        Private Sub BuildMarkdownPage(page As Panel, markdown As String)
            page.Dock = DockStyle.Fill
            page.BackColor = Color.Transparent
            page.Padding = New Padding(10, 12, 10, 10)
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
                "html,body{background:#17171b;color:#d8d8dc;font-family:'Microsoft YaHei UI',sans-serif;margin:0;padding:12px 16px;}" &
                "h1{font-size:24px;color:#fff;margin:4px 0 18px;border-bottom:1px solid #3b3b42;padding-bottom:10px;}" &
                "h2{font-size:20px;color:#f2f2f2}h3{font-size:17px}p,li{font-size:14px;line-height:1.8}strong{color:#fff}" &
                "code{background:#303038;padding:2px 5px;border-radius:4px;color:#e8c98d}a{color:#76b7ff}</style></head><body>" & body.ToString() & "</body></html>"
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
            _lblConvertInput.Text = "<font color=#A8A8A8>输入模型：</font><font color=#E0E0E0>" & EscapeHtml(_convertInputPath) & "</font>"
            _lblConvertOutput.Text = "<font color=#A8A8A8>输出目录：</font><font color=#E0E0E0>" & EscapeHtml(outputDir) & "</font>"
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
            Dim color = If(isError, "#E58A8A", "#9AA79A")
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
            _lblExe.Text = "<font color=#B8B8B8>videoenhancer.exe：</font><font color=#E6E6E6>" & EscapeHtml(If(String.IsNullOrWhiteSpace(_config.ExePath), "（未指定）", _config.ExePath)) & "</font>"
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
