Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Text.Json
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
        ' ── 选项卡分栏：超分主界面 / 实时预览 / 高级功能 ──
        Private ReadOnly _tabs As New ModernTabControl()
        Private ReadOnly _pageUpscale As New Panel()
        Private ReadOnly _pagePreview As New Panel()
        Private ReadOnly _pageAdvanced As New Panel()
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
            If _switchUpscale.Checked AndAlso (_config.Backend = "cuda" OrElse _config.Backend = "tensorrt") Then
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
                    If(_config.Backend = "cuda",
                    "（CUDA，models 下的 .pth/.pt/.pkl 文件）",
                    "（models 目录，.param/.bin 文件夹）"))
                ShowStatus($"已从 videoenhancer.exe 读取 {models.Count} 个可用模型 " & modeText, False)
            Else
                If (_config.Backend = "cuda" OrElse _config.Backend = "tensorrt") AndAlso _config.UpscaleEnabled Then
                    _cmbModel.WaterText = If(_config.Backend = "tensorrt", "未找到 .engine 放大模型", "未找到 .pth 放大模型")
                    ShowStatus(If(_config.Backend = "tensorrt", "未找到 .engine 放大模型，请确认 models 目录", "未找到 .pth 放大模型"), True)
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
            _config.Backend = backend
            _config.Save()
            ' 切换后端后重新读取两个模型列表（CUDA 需要 .pth 模型；活动模式无 .pth 时由 Apply*List 自动回退）
            RefreshUpscaleModels()
            RefreshInterpModels()
            Dim modeText = If(backend = "tensorrt",
                "TensorRT（NVIDIA）：超分用 models 下的 .engine 模型（仅 N 卡）",
                If(backend = "cuda",
                "CUDA（PyTorch）：超分用 models 下的 .pth 模型，补帧用 models" & Convert.ToChar(92) & "RIFE 下的 .pth 模型",
                "NCNN（Vulkan）"))
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
            If text.Contains("TensorRT") Then
                Return "tensorrt"
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

            Dim tabMain As New ModernTabControl.ModernTab("超分主界面") With {.BoundControl = _pageUpscale}
            Dim tabPreview As New ModernTabControl.ModernTab("实时预览") With {.BoundControl = _pagePreview}
            Dim tabAdvanced As New ModernTabControl.ModernTab("高级功能") With {.BoundControl = _pageAdvanced}
            _tabs.Items.Add(tabMain)
            _tabs.Items.Add(tabPreview)
            _tabs.Items.Add(tabAdvanced)
            ' 每次打开插件都从超分主界面开始，避免保留上次停留在实时预览/高级功能页的状态。
            _tabs.SelectedIndex = 0
        End Sub

        ' ────────────────────────── 超分主界面页 ──────────────────────────

        Private Sub BuildUpscalePage()
            _pageUpscale.Dock = DockStyle.Fill
            _pageUpscale.BackColor = Color.Transparent
            ' 给页签标题与插件总开关之间留出明确的呼吸空间；其余控件相对间距保持不变。
            _pageUpscale.Padding = New Padding(0, 22, 0, 0)

            ' 行内 Dock.Left 从右往左排列：先添加右侧标签，最后添加开关（最左）。
            ' 整页 Dock.Top 反序添加：最后添加的行排在最上。

            ' ── 说明 + exe 路径（放回超分主界面；先添加 → 排在最下）──
            Dim sectionHint As New Panel() With {.Dock = DockStyle.Top, .Height = 96, .BackColor = Color.Transparent, .Padding = New Padding(2, 12, 0, 0)}
            _lblAdvancedHint.AutoSize = False
            _lblAdvancedHint.Dock = DockStyle.Fill
            _lblAdvancedHint.TextAlign = HtmlColorLabel.TextAlignEnum.TopLeft
            _lblAdvancedHint.LineSpacing = 4
            _lblAdvancedHint.Text = "<font color=#9A9A9A><b>说明</b></font><br/>" &
                "<font color=#8A8A8A>「插件总开关」仅作用于「超分主界面」页：开启后，加入编码队列的命令会被 videoenhancer.exe 中转执行 AI 超分/补帧。</font><br/>" &
                "<font color=#8A8A8A>「实时预览」与队列监控即使关闭插件总开关也能使用。</font><br/>" &
                "<font color=#8A8A8A>CLI 程序启动时读取本目录 videoenhancer.ini 的 core-path，并校验 bin\ffmpeg、python 库与模型库。</font>"
            sectionHint.Controls.Add(_lblAdvancedHint)
            _pageUpscale.Controls.Add(sectionHint)

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
            _pageUpscale.Controls.Add(sectionExe)

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
            _switchInterp.Size = New Size(66, 34)
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
            _switchUpscale.Size = New Size(66, 34)
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
            _switchMaster.Size = New Size(66, 34)
            _switchMaster.TrackColorOn = Color.FromArgb(80, 200, 120)
            _switchMaster.TrackColorOff = Color.FromArgb(90, 90, 100)
            _switchMaster.KnobColor = Color.FromArgb(235, 235, 235)
            _switchMaster.Checked = _config.Enabled
            AddHandler _switchMaster.CheckedChanged, AddressOf OnMasterSwitchChanged
            sectionMaster.Controls.Add(_switchMaster)
            _pageUpscale.Controls.Add(sectionMaster)
        End Sub

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
            _pageAdvanced.Padding = New Padding(0, 4, 0, 0)

            ' 工具行：「制作四宫格比对视频」打开独立二级窗口（不影响主界面）
            Dim sectionQuad As New Panel() With {.Dock = DockStyle.Top, .Height = 78, .BackColor = Color.Transparent, .Padding = New Padding(0, 26, 0, 0)}
            _btnQuad.Text = "制作四宫格比对视频"
            _btnQuad.Size = New Size(210, 36)
            _btnQuad.Dock = DockStyle.Left
            _btnQuad.BorderRadius = 8
            _btnQuad.BorderSize = 0
            _btnQuad.BackColor1 = Color.FromArgb(40, 110, 190, 255)
            _btnQuad.HoverBackColor1 = Color.FromArgb(60, 110, 190, 255)
            AddHandler _btnQuad.Click, AddressOf OnQuadClick
            sectionQuad.Controls.Add(_btnQuad)
            _pageAdvanced.Controls.Add(sectionQuad)

            ' 说明文字
            Dim sectionDesc As New Panel() With {.Dock = DockStyle.Top, .Height = 108, .BackColor = Color.Transparent, .Padding = New Padding(2, 16, 0, 0)}
            Dim lblDesc As New HtmlColorLabel() With {
                .Text = "<font color=#9A9A9A><b>四宫格比对</b></font><br/>" &
                        "<font color=#8A8A8A>拖入 1-4 个视频，选择输出大小 / 缩放算法 / 分割线，生成 2×2（或 1+1+2 / 上下 / 左右）比对视频。</font><br/>" &
                        "<font color=#8A8A8A>至少需要 2 个视频；少于 4 个时自动调整布局。</font>",
                .AutoSize = False,
                .Dock = DockStyle.Fill,
                .TextAlign = HtmlColorLabel.TextAlignEnum.TopLeft,
                .LineSpacing = 4
            }
            sectionDesc.Controls.Add(lblDesc)
            _pageAdvanced.Controls.Add(sectionDesc)
        End Sub

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

        ''' <summary>把配置的推理后端同步到下拉框（0=NCNN，1=CUDA）。</summary>
        Private Sub SyncBackendCombo()
            If _cmbBackend.Items.Count = 0 Then
                Return
            End If
            _cmbBackend.SelectedIndex = If(_config.Backend = "tensorrt", 2, If(_config.Backend = "cuda", 1, 0))
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
