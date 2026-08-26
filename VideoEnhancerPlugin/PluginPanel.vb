Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Security.Cryptography
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

        ' 透明布局仍由 WinForms 负责尺寸计算，但在单个缓冲表面提交，
        ' 避免窗口缩放时逐层擦除后再请求 LakeUI 背景源重画。
        Private NotInheritable Class BufferedTableLayoutPanel
            Inherits TableLayoutPanel

            Public Sub New()
                SetStyle(ControlStyles.SupportsTransparentBackColor Or
                    ControlStyles.AllPaintingInWmPaint Or
                    ControlStyles.OptimizedDoubleBuffer, True)
                DoubleBuffered = True
                ResizeRedraw = False
                UpdateStyles()
            End Sub
        End Class

        ' 参照 3FUI 参数页的普通 Panel 布局：正数为固定宽度，负数为剩余空间权重。
        ' 这里只计算子控件边界，不额外创建透明绘图层。
        Private NotInheritable Class HorizontalLayoutPanel
            Inherits Panel

            Private ReadOnly _columns As Single()
            Private ReadOnly _columnByControl As New Dictionary(Of Control, Integer)()

            Public Sub New(ParamArray columns As Single())
                _columns = columns
                Margin = Padding.Empty
                Padding = Padding.Empty
                ResizeRedraw = False
            End Sub

            Public Sub AddColumn(control As Control, columnIndex As Integer)
                control.Dock = DockStyle.None
                _columnByControl(control) = columnIndex
                Controls.Add(control)
            End Sub

            Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
                MyBase.OnLayout(levent)
                If _columns.Length = 0 Then Return

                Dim fixedWidth As Single = 0
                Dim totalWeight As Single = 0
                For Each column In _columns
                    If column >= 0 Then
                        fixedWidth += column
                    Else
                        totalWeight += -column
                    End If
                Next

                Dim available = Math.Max(0.0F, CSng(ClientSize.Width) - fixedWidth)
                Dim left As Integer = 0
                Dim widths(_columns.Length - 1) As Integer
                For index = 0 To _columns.Length - 1
                    Dim width = If(_columns(index) >= 0,
                        _columns(index),
                        If(totalWeight > 0, available * (-_columns(index)) / totalWeight, 0.0F))
                    widths(index) = If(index = _columns.Length - 1,
                        Math.Max(0, ClientSize.Width - left),
                        Math.Max(0, CInt(Math.Round(width))))
                    left += widths(index)
                Next

                left = 0
                For index = 0 To widths.Length - 1
                    For Each pair In _columnByControl
                        If pair.Value <> index Then Continue For
                        Dim margin = pair.Key.Margin
                        If pair.Key.Anchor = AnchorStyles.None Then
                            ' BooleanSwitch 等定尺寸控件保持 DPI 尺寸并在列内居中，避免被拉成圆形。
                            Dim controlWidth = Math.Min(pair.Key.Width,
                                Math.Max(0, widths(index) - margin.Horizontal))
                            Dim controlHeight = Math.Min(pair.Key.Height,
                                Math.Max(0, ClientSize.Height - margin.Vertical))
                            pair.Key.SetBounds(
                                left + Math.Max(0, (widths(index) - controlWidth) \ 2),
                                Math.Max(0, (ClientSize.Height - controlHeight) \ 2),
                                controlWidth,
                                controlHeight)
                        Else
                            pair.Key.SetBounds(
                                left + margin.Left,
                                margin.Top,
                                Math.Max(0, widths(index) - margin.Horizontal),
                                Math.Max(0, ClientSize.Height - margin.Vertical))
                        End If
                    Next
                    left += widths(index)
                Next
            End Sub
        End Class

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
        Private ReadOnly _cmbInterpBackend As New ModernComboBox()
        Private ReadOnly _cmbFactor As New ModernComboBox()
        Private ReadOnly _cmbDynamicOpticalFlow As New ModernComboBox()
        Private ReadOnly _cmbSceneThreshold As New ModernComboBox()
        Private ReadOnly _cmbTileSize As New ModernComboBox()
        Private ReadOnly _lblFactor As New HtmlColorLabel()
        Private ReadOnly _cmbProcessOrder As New ModernComboBox()
        Private ReadOnly _lblProcessOrder As New HtmlColorLabel()
        Private _syncingMaster As Boolean = False
        Private _syncingBackend As Boolean = False
        Private _syncingInterpBackend As Boolean = False
        Private _syncingFactor As Boolean = False
        Private _syncingDynamicOpticalFlow As Boolean = False
        Private _syncingSceneThreshold As Boolean = False
        Private _syncingTileSize As Boolean = False
        Private _syncingProcessOrder As Boolean = False
        Private _syncingSwitch As Boolean = False
        Private _syncingInterpSwitch As Boolean = False
        Private _modelsLoaded As Boolean = False
        Private _loadingModels As Boolean = False
        Private _interpModelsLoaded As Boolean = False
        Private _loadingInterpModels As Boolean = False
        Private _syncingModelSelection As Boolean = False
        Private _syncingInterpModelSelection As Boolean = False
        Private _showModelMenuAfterLoad As Boolean = False
        Private _showInterpMenuAfterLoad As Boolean = False
        Private ReadOnly _modelCatalog As New List(Of ModelCatalogItem)()
        Private ReadOnly _interpModelCatalog As New List(Of ModelCatalogItem)()
        Private _modelMenu As ModernContextMenu
        Private _interpModelMenu As ModernContextMenu
        Private _uiReady As Boolean = False
        ' ── 选项卡分栏：超分主界面 / 实时预览 / 高级功能 / 模型转换器 ──
        Private ReadOnly _tabs As New ModernTabControl()
        ' 3FUI 通过字段名和控件名 ModernPanel1 绑定 LakeUI 背景穿透缓存。
        Private ReadOnly ModernPanel1 As New ModernPanel()
        Private ReadOnly _pageUpscale As New ModernPanel()
        Private ReadOnly _pagePreview As New Panel()
        Private ReadOnly _pageAdvanced As New Panel()
        Private ReadOnly _pageDownloader As New Panel()
        Private ReadOnly _pageConverter As New Panel()
        Private ReadOnly _pageImporter As New Panel()
        Private ReadOnly _pageTutorial As New Panel()
        Private ReadOnly _markdownSources As New Dictionary(Of Panel, String)()
        Private ReadOnly _markdownReady As New HashSet(Of Panel)()
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
        Private _convertIsInterpolation As Boolean = False
        Private _convertArchitecture As String = ""
        Private _conversionRunning As Boolean = False
        ' ── 用户模型导入页 ──
        Private ReadOnly _btnPickImportFile As New ModernButton()
        Private ReadOnly _btnPickImportFolder As New ModernButton()
        Private ReadOnly _btnImportModel As New ModernButton()
        Private ReadOnly _lblImportSource As New HtmlColorLabel()
        Private ReadOnly _lblImportStatus As New HtmlColorLabel()
        Private ReadOnly _importModelList As New UltraDetailListView()
        Private _importSourcePath As String = ""
        Private _modelImportBusy As Boolean = False
        Private _userModelsLoading As Boolean = False
        Private _importModelListConfigured As Boolean = False
        ' ── 模型下载页 ──
        Private Const DownloadActionColumn As Integer = 3
        Private Const MaxParallelDownloads As Integer = 3
        Private ReadOnly _downloadList As New UltraDetailListView()
        Private ReadOnly _btnRefreshDownloads As New ModernButton()
        Private ReadOnly _btnDownloadPluginUpdate As New ModernButton()
        Private ReadOnly _btnCleanArchives As New ModernButton()
        Private ReadOnly _btnCheckUpdates As New ModernButton()
        Private _downloadsLoaded As Boolean = False
        Private _downloadsLoading As Boolean = False
        Private _downloadOnline As Boolean = True
        Private _archiveCleanupBusy As Boolean = False
        Private _updateCheckBusy As Boolean = False
        Private _environmentCheckCompleted As Boolean = False
        Private ReadOnly _environmentCheckSync As New Object()
        Private _environmentCheckCancellation As System.Threading.CancellationTokenSource
        Private _environmentCheckTask As Task
        Private _downloadActiveCount As Integer = 0
        Private _downloadActionsEnabled As Boolean = True
        Private _downloadListConfigured As Boolean = False
        Private ReadOnly _activeDownloadPaths As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Private ReadOnly _activeDownloadGroups As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Private ReadOnly _downloadItemsByPath As New Dictionary(Of String, UltraDetailListView.ListItem)(StringComparer.OrdinalIgnoreCase)
        Private ReadOnly _downloadGroupItems As New Dictionary(Of String, UltraDetailListView.ListItem)(StringComparer.OrdinalIgnoreCase)
        Private NotInheritable Class DownloadModelEntry
            Public Property Name As String
            Public Property RelativePath As String
            Public Property Size As Long
            Public Property Installed As Boolean
            Public Property StatusText As String = ""
            Public Property ActionText As String = ""
            Public Property IsBackend As Boolean
            Public Property ForceBackendFull As Boolean
            Public Property BackendFullSize As Long
        End Class
        Private NotInheritable Class BackendDownloadStatus
            Public Property State As String = ""
            Public Property InstalledVersion As String = ""
            Public Property LatestVersion As String = ""
            Public Property Mode As String = ""
            Public Property DownloadSize As Long
            Public Property FullSize As Long
        End Class
        Private NotInheritable Class DownloadListRowTag
            Public Property Entry As DownloadModelEntry
            Public Property Category As String
            Public Property BatchPaths As List(Of String)
        End Class
        Private NotInheritable Class DownloadExecutionResult
            Public Property ExitCode As Integer = -1
            Public Property Errors As String = ""
        End Class
        Private NotInheritable Class ModelCatalogItem
            Public Property Id As String = ""
            Public Property DisplayName As String = ""
            Public Property Architecture As String = ""
            Public Property Purpose As String = ""
            Public Property Scale As Integer
            Public Property Source As String = ""
            Public Property Backends As String() = Array.Empty(Of String)()
        End Class
        Private NotInheritable Class ModelImportResponse
            Public Property Success As Boolean
            Public Property Source As String = ""
            Public Property Id As String = ""
            Public Property InstalledPath As String = ""
            Public Property Task As String = ""
            Public Property Architecture As String = ""
            Public Property Purpose As String = ""
            Public Property Scale As Integer
            Public Property Backends As String() = Array.Empty(Of String)()
            Public Property [Error] As String = ""
        End Class
        Private NotInheritable Class UserModelItem
            Public Property Id As String = ""
            Public Property DisplayName As String = ""
            Public Property RelativePath As String = ""
            Public Property Task As String = ""
            Public Property Architecture As String = ""
            Public Property Purpose As String = ""
            Public Property Format As String = ""
            Public Property Scale As Integer
            Public Property InputMultiple As Integer = 1
            Public Property MinimumSize As Integer
            Public Property Square As Boolean
            Public Property Tiling As String = ""
            Public Property Sha256 As String = ""
            Public Property Size As Long
            Public Property ImportedAtUtc As String = ""
            Public Property Backends As String() = Array.Empty(Of String)()
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
            Dim updateResult = PluginUpdater.ConsumeUpdateResult()
            If updateResult.StartsWith("OK|", StringComparison.Ordinal) Then
                ShowStatus("已更新到 v" & updateResult.Substring(3), False)
            ElseIf updateResult.StartsWith("ERROR|", StringComparison.Ordinal) Then
                ShowStatus("上次自动更新失败：" & updateResult.Substring(6), True)
            End If
            If _config.AutoCheckUpdates Then StartAutomaticUpdateCheck()
        End Sub

        Private Sub OnQueueMenuTick(sender As Object, e As EventArgs)
            QueueHook.AttachQueueMenu()
        End Sub

        Private Async Sub StartAutomaticUpdateCheck()
            Await Task.Delay(1500)
            If IsDisposed Then Return
            Await CheckForUpdatesAsync(True)
        End Sub

        Private Async Sub OnCheckUpdates(sender As Object, e As EventArgs)
            Await CheckForUpdatesAsync(False)
        End Sub

        ''' <summary>检查独立稳定版；自动检查失败时保持安静，发现新版本仍由用户确认。</summary>
        Private Async Function CheckForUpdatesAsync(silent As Boolean) As Task
            If _updateCheckBusy Then Return
            _updateCheckBusy = True
            Dim userAccepted = False
            _btnCheckUpdates.Enabled = False
            _btnDownloadPluginUpdate.Enabled = False
            If Not silent Then ShowStatus("正在从 GitHub 检查更新…", False)
            Try
                Dim manifest = Await PluginUpdater.FetchLatestManifestAsync()
                If Not PluginUpdater.HasUpdate(manifest) Then
                    If Not silent Then ShowStatus("当前已是最新稳定版 v" & PluginVersion.Current, False)
                    Return
                End If

                Dim message = "VideoEnhancer " & manifest.Version & " 可用" &
                    Environment.NewLine & "当前版本：" & PluginVersion.Current &
                    Environment.NewLine & "更新包：" & FormatDownloadSize(manifest.Package.Size)
                message &= Environment.NewLine & Environment.NewLine &
                    "下载完成后将自动关闭并重启 3FUI。" & Environment.NewLine &
                    "现在下载并安装吗？"
                If MessageBox.Show(Me, message, "发现新版本",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Information,
                        MessageBoxDefaultButton.Button1) <> DialogResult.Yes Then Return
                userAccepted = True

                Dim installedExe = PluginConfig.ResolveInstalledExePath(_config.ExePath)
                If String.IsNullOrWhiteSpace(installedExe) OrElse Not File.Exists(installedExe) Then
                    Throw New FileNotFoundException("找不到已安装的 videoenhancer.exe")
                End If
                Dim targetDirectory = PluginConfig.ResolvePluginRoot(installedExe)
                If String.IsNullOrWhiteSpace(targetDirectory) OrElse
                    Not File.Exists(Path.Combine(targetDirectory, "videoenhancer.3fui.dll")) Then
                    Throw New InvalidOperationException("自动更新无法确定承载插件 DLL 的 Plugin 目录")
                End If
                Dim hostExe = Environment.ProcessPath
                If String.IsNullOrWhiteSpace(hostExe) OrElse Not File.Exists(hostExe) Then
                    Throw New FileNotFoundException("无法确定 3FUI 主程序路径")
                End If

                ShowStatus("正在下载 VideoEnhancer v" & manifest.Version & "…", False)
                Dim packagePath = Await PluginUpdater.DownloadPackageAsync(manifest,
                    Sub(percent) ShowStatus("正在下载更新：" & percent & "%", False))
                ShowStatus("更新包校验通过，正在准备重启 3FUI…", False)
                If Not StopEnvironmentCheck(10000) Then
                    Throw New InvalidOperationException("启动环境检查未能及时停止，请稍后重试")
                End If
                PluginUpdater.StartUpdate(packagePath, targetDirectory,
                    Environment.ProcessId, hostExe)
                Application.Exit()
            Catch ex As Exception
                If Not silent OrElse userAccepted Then ShowStatus("检查或安装更新失败：" & ex.Message, True)
            Finally
                _updateCheckBusy = False
                If Not IsDisposed Then
                    _btnCheckUpdates.Enabled = True
                    _btnDownloadPluginUpdate.Enabled = True
                End If
            End Try
        End Function

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
            StopEnvironmentCheck(2000)
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
            _config.UpscaleEnabled = _switchUpscale.Checked
            ' 开启超分：CUDA 模式下放大模型列表切换为 models 下的 .pth 模型（空列表时自动回退 ncnn）
            If _switchUpscale.Checked AndAlso (_config.Backend = "cuda" OrElse _config.Backend = "tensorrt" OrElse _config.Backend = "onnx" OrElse _config.Backend = "flashvsr") Then
                RefreshUpscaleModels()
            End If
            _config.Save()
            UpdateModeStateLabels()
            UpdateProcessOrderState()
            UpdateAdvancedControlState()
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
            _config.InterpEnabled = _switchInterp.Checked
            If _switchInterp.Checked Then
                RefreshInterpModels()
            End If
            _config.Save()
            UpdateModeStateLabels()
            UpdateProcessOrderState()
            UpdateAdvancedControlState()
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
            _cmbModel.DroppedDown = False
            If _modelsLoaded Then
                ShowModelMenu(_cmbModel, _modelCatalog, False)
                Return
            End If
            _showModelMenuAfterLoad = True
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
            _cmbInterp.DroppedDown = False
            If _interpModelsLoaded Then
                ShowModelMenu(_cmbInterp, _interpModelCatalog, True)
                Return
            End If
            _showInterpMenuAfterLoad = True
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
                         Dim catalog = RunModelCatalog(exePath, "--list-model-catalog", "-backend", backend)
                         Dim models As List(Of String) = Nothing
                         If catalog.Count = 0 Then
                             models = RunListModels(exePath, "--search-models", "-backend", backend)
                         End If
                         Try
                             If Me.IsHandleCreated Then
                                 Me.BeginInvoke(New Action(Sub()
                                                               If catalog.Count > 0 Then
                                                                   ApplyModelCatalog(catalog, False)
                                                               Else
                                                                   ApplyModelList(If(models, New List(Of String)()))
                                                               End If
                                                               _loadingModels = False
                                                           End Sub))
                             Else
                                 If catalog.Count > 0 Then
                                     ApplyModelCatalog(catalog, False)
                                 Else
                                     ApplyModelList(If(models, New List(Of String)()))
                                 End If
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
            Dim backend = If(String.IsNullOrWhiteSpace(_config.InterpBackend), "ncnn", _config.InterpBackend)
            Task.Run(Sub()
                         Dim catalog = RunModelCatalog(exePath, "--list-interp-model-catalog", "-interp-backend", backend)
                         Dim models As List(Of String) = Nothing
                         If catalog.Count = 0 Then
                             models = RunListModels(exePath, "--list-interp-models", "-interp-backend", backend)
                         End If
                         Try
                             If Me.IsHandleCreated Then
                                 Me.BeginInvoke(New Action(Sub()
                                                               _loadingInterpModels = False
                                                               If catalog.Count > 0 Then
                                                                   ApplyModelCatalog(catalog, True)
                                                               Else
                                                                   ApplyInterpModelList(If(models, New List(Of String)()))
                                                               End If
                                                           End Sub))
                             Else
                                 _loadingInterpModels = False
                                 If catalog.Count > 0 Then
                                     ApplyModelCatalog(catalog, True)
                                 Else
                                     ApplyInterpModelList(If(models, New List(Of String)()))
                                 End If
                             End If
                         Catch
                             _loadingInterpModels = False
                         End Try
                     End Sub)
        End Sub

        Private Sub ApplyModelCatalog(catalog As List(Of ModelCatalogItem), interpolation As Boolean)
            Dim targetCatalog = If(interpolation, _interpModelCatalog, _modelCatalog)
            targetCatalog.Clear()
            targetCatalog.AddRange(catalog.Where(Function(item) item IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(item.Id)))
            Dim configured = If(interpolation, _config.InterpModel, _config.Model)
            Dim selected = targetCatalog.FirstOrDefault(
                Function(item) String.Equals(item.Id, configured, StringComparison.OrdinalIgnoreCase))
            Dim matchedConfigured = selected IsNot Nothing
            If selected Is Nothing AndAlso targetCatalog.Count > 0 Then selected = targetCatalog(0)
            If selected IsNot Nothing Then
                SetCatalogSelection(selected, interpolation, saveConfig:=Not matchedConfigured)
            End If
            If interpolation Then
                _interpModelsLoaded = targetCatalog.Count > 0
                _cmbInterp.WaterText = If(targetCatalog.Count > 0, "选择补帧模型…", "未找到补帧模型")
                If _showInterpMenuAfterLoad AndAlso targetCatalog.Count > 0 Then
                    _showInterpMenuAfterLoad = False
                    BeginInvoke(New Action(Sub() ShowModelMenu(_cmbInterp, _interpModelCatalog, True)))
                End If
            Else
                _modelsLoaded = targetCatalog.Count > 0
                _cmbModel.WaterText = If(targetCatalog.Count > 0, "选择放大模型…", "未找到放大模型")
                If _showModelMenuAfterLoad AndAlso targetCatalog.Count > 0 Then
                    _showModelMenuAfterLoad = False
                    BeginInvoke(New Action(Sub() ShowModelMenu(_cmbModel, _modelCatalog, False)))
                End If
            End If
            If targetCatalog.Count > 0 Then
                ShowStatus("已读取 " & targetCatalog.Count.ToString() & " 个" & If(interpolation, "补帧", "超分") & "模型，已按架构分组", False)
            End If
        End Sub

        Private Shared Function FallbackArchitecture(modelId As String) As String
            Dim normalized = If(modelId, "").Replace(Convert.ToChar(92), "/"c)
            For Each architecture In New String() {
                "RealESRGAN", "RealHatGAN", "ESRGAN", "SPANPlus", "SPAN", "SwinIR", "RealCUGAN",
                "AnimeSR", "CRAFT", "DITN", "MoSR", "RIFE", "GMFSS", "GIMM"
            }
                If normalized.IndexOf(architecture, StringComparison.OrdinalIgnoreCase) >= 0 Then Return architecture
            Next
            Dim segments = normalized.Split(New Char() {"/"c}, StringSplitOptions.RemoveEmptyEntries)
            Return If(segments.Length > 1, segments(0), "其他模型")
        End Function

        Private Shared Sub ConfigureModelMenu(menu As ModernContextMenu)
            menu.BackColor = Color.FromArgb(42, 42, 42)
            menu.BackColor1 = Color.FromArgb(42, 42, 42)
            menu.BorderColor = Color.FromArgb(72, 72, 72)
            menu.BorderSize = 1
            menu.MenuForeColor = UiText
            menu.HoverBackColor = UiSurfaceHover
            menu.PressedBackColor = UiAccentPressed
            menu.ArrowColor = UiTextSecondary
            menu.ItemHeight = 34
            menu.ItemPadding = New Padding(12, 0, 12, 0)
            menu.MenuPadding = New Padding(4)
            menu.SubMenuHorizontalOffset = 2
        End Sub

        Private Sub ShowModelMenu(anchor As ModernComboBox, catalog As List(Of ModelCatalogItem), interpolation As Boolean)
            If catalog.Count = 0 OrElse anchor.IsDisposed Then Return
            Dim root As New ModernContextMenu()
            ConfigureModelMenu(root)
            For Each group In catalog.GroupBy(Function(item) If(String.IsNullOrWhiteSpace(item.Architecture), "其他模型", item.Architecture)).
                    OrderBy(Function(item) item.Key, StringComparer.CurrentCultureIgnoreCase)
                Dim submenu As New ModernContextMenu()
                ConfigureModelMenu(submenu)
                For Each entry In group.OrderBy(Function(item) item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                    Dim selectedEntry = entry
                    Dim suffix = If(entry.Scale > 0 AndAlso Not interpolation, "  · " & entry.Scale.ToString() & "x", "")
                    If String.Equals(entry.Source, "user", StringComparison.OrdinalIgnoreCase) Then suffix &= "  [用户]"
                    Dim child As New ModernContextMenu.ModernMenuItem(entry.DisplayName & suffix) With {
                        .Checked = String.Equals(entry.Id, If(interpolation, _config.InterpModel, _config.Model), StringComparison.OrdinalIgnoreCase),
                        .CloseOnClick = True
                    }
                    AddHandler child.Click, Sub(sender, e) SetCatalogSelection(selectedEntry, interpolation, saveConfig:=True)
                    submenu.Items.Add(child)
                Next
                root.Items.Add(New ModernContextMenu.ModernMenuItem(group.Key) With {.SubMenu = submenu, .CloseOnClick = False})
            Next
            If interpolation Then
                _interpModelMenu = root
            Else
                _modelMenu = root
            End If
            root.Show(anchor, New Point(0, anchor.Height + 2))
        End Sub

        Private Sub SetCatalogSelection(entry As ModelCatalogItem, interpolation As Boolean, saveConfig As Boolean)
            Dim combo = If(interpolation, _cmbInterp, _cmbModel)
            If interpolation Then _syncingInterpModelSelection = True Else _syncingModelSelection = True
            Try
                combo.Items.Clear()
                combo.Items.Add(entry.DisplayName)
                combo.SelectedIndex = 0
            Finally
                If interpolation Then _syncingInterpModelSelection = False Else _syncingModelSelection = False
            End Try
            If Not saveConfig Then Return
            If interpolation Then
                SaveInterpModelSelection(entry.Id)
            Else
                _config.Model = entry.Id
                _config.Save()
            End If
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
                                If relative.StartsWith("Frame-Interpolation/", StringComparison.OrdinalIgnoreCase) OrElse
                                   relative.StartsWith("RIFE/", StringComparison.OrdinalIgnoreCase) Then Continue For
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
            _modelCatalog.Clear()
            For Each modelId In models
                _modelCatalog.Add(New ModelCatalogItem With {
                    .Id = modelId,
                    .DisplayName = Path.GetFileName(modelId.Replace("/"c, Convert.ToChar(92))),
                    .Architecture = FallbackArchitecture(modelId),
                    .Purpose = "SR",
                    .Source = "discovered"
                })
            Next
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
                Dim modeText = If(_config.Backend = "basicvsrpp",
                    "（BasicVSR++，官方 .pth 或 config.py/chkpts.pth 优化目录）",
                    If(_config.Backend = "tensorrt",
                    "（TensorRT，PTH 首次使用自动构建 Engine）",
                    If(_config.Backend = "onnx",
                    "（ONNX Runtime，models 下的 .onnx 文件）",
                    If(_config.Backend = "flashvsr",
                    "（FlashVSR，连续视频帧专用模型目录）",
                    If(_config.Backend = "cuda",
                    "（CUDA，models 下的 .pth/.pt/.pkl/.ckpt/.safetensors 文件）",
                    "（models 目录，.param/.bin 文件夹）")))))
                ShowStatus($"已从 videoenhancer.exe 读取 {models.Count} 个可用模型 " & modeText, False)
            Else
                If Not _environmentCheckCompleted Then
                    _cmbModel.WaterText = "正在读取模型列表…"
                    ShowStatus("正在检查环境并读取模型列表…", False)
                    Return
                End If
                If (_config.Backend = "cuda" OrElse _config.Backend = "tensorrt" OrElse _config.Backend = "onnx" OrElse _config.Backend = "flashvsr" OrElse _config.Backend = "basicvsrpp") AndAlso _config.UpscaleEnabled Then
                    Dim missingExt = If(_config.Backend = "basicvsrpp", "BasicVSR++ .pth 或优化目录", If(_config.Backend = "flashvsr", "FlashVSR 完整模型目录", If(_config.Backend = "tensorrt", "PTH 或 .engine", If(_config.Backend = "onnx", ".onnx", ".pth"))))
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
            _interpModelCatalog.Clear()
            For Each modelId In models
                _interpModelCatalog.Add(New ModelCatalogItem With {
                    .Id = modelId,
                    .DisplayName = Path.GetFileName(modelId.Replace("/"c, Convert.ToChar(92))),
                    .Architecture = FallbackArchitecture(modelId),
                    .Purpose = "Interpolation",
                    .Scale = 1,
                    .Source = "discovered"
                })
            Next
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
                Dim modeText = If(_config.InterpBackend = "tensorrt",
                    "（TensorRT，RIFE 权重首次使用自动构建 Engine）",
                    If(_config.InterpBackend = "cuda",
                    "（CUDA/PyTorch，Frame-Interpolation）",
                    "（NCNN，Frame-Interpolation 下的模型目录）"))
                ShowStatus($"已读取 {models.Count} 个补帧模型 " & modeText, False)
            Else
                If Not _environmentCheckCompleted Then
                    _cmbInterp.WaterText = "正在读取补帧模型…"
                    ShowStatus("正在检查环境并读取补帧模型…", False)
                    Return
                End If
                If _config.InterpBackend = "cuda" OrElse _config.InterpBackend = "tensorrt" Then
                    _cmbInterp.WaterText = "未找到兼容的补帧模型"
                    ShowStatus("未在 models" & Convert.ToChar(92) & "Frame-Interpolation 找到与 " & If(_config.InterpBackend = "tensorrt", "TensorRT", "CUDA/PyTorch") & " 兼容的补帧模型", _config.InterpEnabled)
                Else
                    _cmbInterp.WaterText = "未找到补帧模型"
                    ShowStatus("未在 models" & Convert.ToChar(92) & "Frame-Interpolation 找到含 .param/.bin 的补帧模型；旧 models" & Convert.ToChar(92) & "RIFE 仍可读取", True)
                End If
            End If
        End Sub

        Private Sub OnModelSelected(sender As Object, e As EventArgs)
            If _syncingModelSelection Then Return
            Dim model = _cmbModel.SelectedItem
            If String.IsNullOrWhiteSpace(model) Then
                Return
            End If
            _config.Model = model.Trim()
            _config.Save()
        End Sub

        Private Sub OnInterpModelSelected(sender As Object, e As EventArgs)
            If _syncingInterpModelSelection Then Return
            Dim model = _cmbInterp.SelectedItem
            If String.IsNullOrWhiteSpace(model) Then
                Return
            End If
            Dim selectedModel = model.Trim()
            SaveInterpModelSelection(selectedModel)
        End Sub

        Private Sub SaveInterpModelSelection(selectedModel As String)
            If (selectedModel.StartsWith("GIMM-VFI/", StringComparison.OrdinalIgnoreCase) OrElse
                selectedModel.StartsWith("GMFSS/", StringComparison.OrdinalIgnoreCase)) AndAlso
               Not String.Equals(_config.InterpBackend, "cuda", StringComparison.OrdinalIgnoreCase) Then
                _config.InterpBackend = "cuda"
                _syncingInterpBackend = True
                SyncInterpBackendCombo()
                _syncingInterpBackend = False
                UpdateAdvancedControlState()
                ShowStatus("该补帧模型仅支持 CUDA/PyTorch，已自动切换后端", False)
            End If
            _config.InterpModel = selectedModel
            _config.Save()
        End Sub

        ''' <summary>"选择推理方式"：ncnn（Vulkan，默认）或 cuda（PyTorch，超分/补帧均需 .pth 模型）。</summary>
        Private Sub OnBackendSelected(sender As Object, e As EventArgs)
            If _syncingBackend Then
                Return
            End If
            Dim backend = BackendValue(_cmbBackend.SelectedItem)
            If backend = _config.Backend Then
                SyncInterpSwitchFromConfig()
                Return
            End If
            _config.Backend = backend
            If backend = "basicvsrpp" Then
                _config.InterpEnabled = False
                _config.InterpModel = ""
            End If
            _config.Save()
            SyncInterpSwitchFromConfig()
            ' 切换后端后重新读取两个模型列表（CUDA 需要 .pth 模型；活动模式无 .pth 时由 Apply*List 自动回退）
            RefreshUpscaleModels()
            RefreshInterpModels()
            UpdateModeStateLabels()
            UpdateProcessOrderState()
            UpdateAdvancedControlState()
            Dim modeText = If(backend = "basicvsrpp",
                "BasicVSR++（NVIDIA）：官方 x4 权重或 1x 优化目录，不与补帧/图片模式混用",
                If(backend = "tensorrt",
                "TensorRT（NVIDIA）：超分与 RIFE 补帧均按实际输入尺寸自动构建 Engine",
                If(backend = "onnx",
                "ONNX Runtime：超分用 .onnx；补帧可独立选择 NCNN 或 CUDA",
                If(backend = "flashvsr",
                "FlashVSR（NVIDIA）：连续视频帧扩散超分；组合补帧会自动分两阶段",
                If(backend = "cuda",
                "CUDA（PyTorch）：超分用 models 下的权重，补帧用 Frame-Interpolation 下的权重",
                "NCNN（Vulkan）")))))
            ShowStatus("推理方式：" & modeText, False)
        End Sub

        ''' <summary>"补帧倍率"选择：保存倍率，后端会按该倍率直接生成目标帧率。</summary>
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
        End Sub

        Private Shared Function BackendValue(item As Object) As String
            Dim text = If(item Is Nothing, "", item.ToString())
            If text.Contains("BasicVSR++") Then
                Return "basicvsrpp"
            End If
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

        Private Shared Function InterpBackendValue(item As Object) As String
            Dim text = If(item Is Nothing, "", item.ToString())
            If text.Contains("TensorRT", StringComparison.OrdinalIgnoreCase) Then Return "tensorrt"
            If text.Contains("CUDA", StringComparison.OrdinalIgnoreCase) Then Return "cuda"
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

        Private Shared Function DynamicOpticalFlowValue(item As Object) As Boolean
            Return String.Equals(If(item Is Nothing, "", item.ToString()), "开启", StringComparison.OrdinalIgnoreCase)
        End Function

        Private Shared Function SceneThresholdValue(item As Object) As Double
            Dim text = If(item Is Nothing, "", item.ToString())
            Dim match = Regex.Match(text, "([0-9]+(?:\.[0-9]+)?)")
            Dim value As Double = 0
            If match.Success AndAlso Double.TryParse(match.Groups(1).Value, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, value) Then
                Return value
            End If
            Return 0
        End Function

        Private Shared Function TileSizeValue(item As Object) As Integer
            Dim text = If(item Is Nothing, "", item.ToString())
            Dim match = Regex.Match(text, "([0-9]+)")
            If match.Success Then
                Dim value As Integer
                If Integer.TryParse(match.Groups(1).Value, value) Then Return value
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

        Private Shared Function RunModelCatalog(exePath As String, ParamArray extraArgs As String()) As List(Of ModelCatalogItem)
            Dim models As New List(Of ModelCatalogItem)()
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
                psi.ArgumentList.Add("--json")
                For Each argument In extraArgs
                    If Not String.IsNullOrWhiteSpace(argument) Then psi.ArgumentList.Add(argument)
                Next
                Using child = Diagnostics.Process.Start(psi)
                    If child Is Nothing Then Return models
                    Dim stdout = child.StandardOutput.ReadToEnd()
                    child.WaitForExit(180000)
                    Dim jsonLine = stdout.Replace(Convert.ToChar(13).ToString(), "").
                        Split(New Char() {Convert.ToChar(10)}, StringSplitOptions.RemoveEmptyEntries).
                        LastOrDefault(Function(line) line.Trim().StartsWith("["c))
                    If String.IsNullOrWhiteSpace(jsonLine) Then Return models
                    Dim parsed = JsonSerializer.Deserialize(Of List(Of ModelCatalogItem))(jsonLine.Trim(),
                        New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True})
                    If parsed IsNot Nothing Then
                        models.AddRange(parsed.Where(Function(item) item IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(item.Id)))
                    End If
                End Using
            Catch
                models.Clear()
            End Try
            Return models.GroupBy(Function(item) item.Id, StringComparer.OrdinalIgnoreCase).
                Select(Function(group) group.First()).ToList()
        End Function

        Private Shared Function RunUserModelList(exePath As String) As List(Of UserModelItem)
            Dim models As New List(Of UserModelItem)()
            Dim psi As New ProcessStartInfo With {
                .FileName = exePath,
                .UseShellExecute = False,
                .RedirectStandardOutput = True,
                .RedirectStandardError = True,
                .CreateNoWindow = True,
                .StandardOutputEncoding = Encoding.UTF8,
                .StandardErrorEncoding = Encoding.UTF8
            }
            psi.ArgumentList.Add("--json")
            psi.ArgumentList.Add("--list-user-models")
            Using child = Diagnostics.Process.Start(psi)
                If child Is Nothing Then Throw New InvalidOperationException("无法启动用户模型清单进程")
                Dim stdout = child.StandardOutput.ReadToEnd()
                Dim stderr = child.StandardError.ReadToEnd()
                child.WaitForExit(30000)
                If child.ExitCode <> 0 Then Throw New InvalidOperationException(LastNonEmptyLine(stderr))
                Dim jsonLine = stdout.Replace(Convert.ToChar(13).ToString(), "").
                    Split(New Char() {Convert.ToChar(10)}, StringSplitOptions.RemoveEmptyEntries).
                    LastOrDefault(Function(line) line.Trim().StartsWith("["c))
                If String.IsNullOrWhiteSpace(jsonLine) Then Return models
                Dim parsed = JsonSerializer.Deserialize(Of List(Of UserModelItem))(jsonLine.Trim(),
                    New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True})
                If parsed IsNot Nothing Then models.AddRange(parsed.Where(Function(item) item IsNot Nothing))
            End Using
            Return models
        End Function

        ' ────────────────────────── 环境检查 ──────────────────────────

        Private Sub RunEnvironmentCheck(exePath As String)
            StopEnvironmentCheck(0)
            _environmentCheckCompleted = False
            ShowStatus("正在检查运行环境…", False)
            Dim cancellation As New System.Threading.CancellationTokenSource()
            SyncLock _environmentCheckSync
                _environmentCheckCancellation = cancellation
            End SyncLock
            Dim checkTask = Task.Run(Sub()
                         Try
                             cancellation.Token.ThrowIfCancellationRequested()
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
                             psi.ArgumentList.Add("-backend")
                             psi.ArgumentList.Add(_config.Backend)
                             Using p = Process.Start(psi)
                                 If p Is Nothing Then
                                     Return
                                 End If
                                 Using cancellation.Token.Register(
                                     Sub()
                                         Try
                                             If Not p.HasExited Then p.Kill(entireProcessTree:=True)
                                         Catch
                                         End Try
                                     End Sub)
                                     Dim stdoutTask = p.StandardOutput.ReadToEndAsync()
                                     Dim stderrTask = p.StandardError.ReadToEndAsync()
                                     Dim exited = p.WaitForExit(120000)
                                     If Not exited Then
                                         Try
                                             p.Kill(entireProcessTree:=True)
                                             p.WaitForExit()
                                         Catch
                                         End Try
                                         cancellation.Token.ThrowIfCancellationRequested()
                                         ShowStatus("环境检查耗时较长，模型列表仍在加载…", False)
                                         Return
                                     End If
                                     cancellation.Token.ThrowIfCancellationRequested()
                                     Dim stdout = stdoutTask.GetAwaiter().GetResult()
                                     Dim stderr = stderrTask.GetAwaiter().GetResult()
                                     Dim lines = (stdout & Environment.NewLine & stderr).Split(
                                         {Convert.ToChar(13), Convert.ToChar(10)}, StringSplitOptions.RemoveEmptyEntries)
                                     Dim ok = p.ExitCode = 0
                                     ' --check 的最终汇总行也会提到“[缺失]”，不能把它本身当作缺失项。
                                     ' 模型库、补帧库和设备专用 TensorRT Engine 属于可选运行资源，不阻断插件启动。
                                     Dim missingLines = lines.Where(Function(l) l.TrimStart().StartsWith("[缺失]", StringComparison.Ordinal)).ToList()
                                     Dim infrastructureMissing = missingLines.FirstOrDefault(
                                         Function(l)
                                             Dim normalized = l.Trim().ToLowerInvariant()
                                             Return Not normalized.Contains("模型库") AndAlso
                                                 Not normalized.Contains("补帧模型库") AndAlso
                                                 Not normalized.Contains("tensorrt engine") AndAlso
                                                 Not normalized.Contains("gpu") AndAlso
                                                 Not normalized.Contains("cuda")
                                         End Function)
                                     Dim text As String
                                     Dim isError As Boolean
                                     If ok Then
                                         text = "环境检测通过：基础组件与模型库就绪"
                                         isError = False
                                     ElseIf Not String.IsNullOrWhiteSpace(infrastructureMissing) Then
                                         text = "环境检测未通过：" & infrastructureMissing.Trim()
                                         isError = True
                                     Else
                                         ' 启动时模型目录可能仍由宿主/下载器准备中；这不是基础环境故障。
                                         text = "基础环境已就绪，模型列表仍在加载…"
                                         isError = False
                                     End If
                                     Try
                                         Me.BeginInvoke(New Action(Sub() ShowStatus(text, isError)))
                                     Catch
                                     End Try
                                 End Using
                              End Using
                          Catch ex As OperationCanceledException
                          Catch
                          End Try
                          SyncLock _environmentCheckSync
                              If Object.ReferenceEquals(_environmentCheckCancellation, cancellation) Then
                                  _environmentCheckCancellation = Nothing
                                  _environmentCheckTask = Nothing
                                  _environmentCheckCompleted = True
                              End If
                          End SyncLock
                          cancellation.Dispose()
                       End Sub)
            SyncLock _environmentCheckSync
                If Object.ReferenceEquals(_environmentCheckCancellation, cancellation) Then
                    _environmentCheckTask = checkTask
                End If
            End SyncLock
        End Sub

        ''' <summary>只停止插件自身的启动自检；真实视频任务仍由后端更新器单独拦截。</summary>
        Private Function StopEnvironmentCheck(timeoutMilliseconds As Integer) As Boolean
            Dim cancellation As System.Threading.CancellationTokenSource
            Dim checkTask As Task
            SyncLock _environmentCheckSync
                cancellation = _environmentCheckCancellation
                checkTask = _environmentCheckTask
            End SyncLock
            If cancellation Is Nothing Then Return True

            Try
                cancellation.Cancel()
            Catch ex As ObjectDisposedException
                Return True
            End Try
            If checkTask Is Nothing OrElse checkTask.IsCompleted Then Return True
            If timeoutMilliseconds <= 0 Then Return False
            Try
                Return checkTask.Wait(timeoutMilliseconds)
            Catch ex As AggregateException
                Return ex.InnerExceptions.All(Function(inner) TypeOf inner Is OperationCanceledException)
            End Try
        End Function

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
                .BackColor = Color.Transparent,
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
            Dim layout As New Panel With {
                .Margin = New Padding(0, 0, rightMargin, 0),
                .Padding = Padding.Empty
            }
            Dim label = CreateTextLabel(caption, 9.0F, FontStyle.Regular, UiTextMuted)
            label.Dock = DockStyle.None
            label.Margin = New Padding(2, 0, 2, 0)
            label.TextAlign = ContentAlignment.BottomLeft
            editor.Dock = DockStyle.None
            editor.AutoSize = False
            editor.MinimumSize = New Size(0, 32)
            editor.Margin = Padding.Empty
            layout.Controls.Add(label)
            layout.Controls.Add(editor)
            Dim arrange =
                Sub()
                    label.SetBounds(2, 0, Math.Max(0, layout.ClientSize.Width - 4), 28)
                    editor.SetBounds(0, 31, layout.ClientSize.Width,
                        Math.Max(32, layout.ClientSize.Height - 34))
                End Sub
            AddHandler layout.Layout, Sub(sender, e) arrange()
            arrange()
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

        ''' <summary>保存组合处理顺序；默认画质优先（先超分，再补帧）。</summary>
        Private Sub OnProcessOrderSelected(sender As Object, e As EventArgs)
            If _syncingProcessOrder Then Return
            Dim order = ProcessOrderValue(_cmbProcessOrder.SelectedItem)
            _config.ProcessOrder = order
            _config.Save()
            UpdateProcessOrderState()
            ShowStatus(If(order = "interp-first",
                "速度/算力优先：先补帧，再超分。",
                "画质优先：先超分，再补帧。"), False)
        End Sub

        Private Shared Function ProcessOrderValue(item As Object) As String
            Dim text = If(item Is Nothing, "", item.ToString())
            Return If(text.Contains("速度", StringComparison.Ordinal), "interp-first", "upscale-first")
        End Function

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
            ' AutoSize=False + 最小高度：下拉框高度完全由所在单元格决定且不小于箭头区域，
            ' 与宿主一致（宿主下拉框固定 30px 高、Dock=Fill、Overlay 下拉）。
            combo.AutoSize = False
            ' 3FUI 的选项型 ModernComboBox 只允许从列表选择；LakeUI 默认仍可能让内部文本框接收输入。
            combo.Editable = False
            combo.MinimumSize = New Size(0, 32)
            combo.Dock = DockStyle.Fill
            combo.DropDownMode = ModernComboBox.DropDownDisplayMode.Overlay
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

        Private Sub OnInterpBackendSelected(sender As Object, e As EventArgs)
            If _syncingInterpBackend Then Return
            Dim backend = InterpBackendValue(_cmbInterpBackend.SelectedItem)
            If backend = _config.InterpBackend Then Return
            _config.InterpBackend = backend
            _config.InterpModel = ""
            _config.Save()
            RefreshInterpModels()
            UpdateAdvancedControlState()
            ShowStatus("补帧后端：" & If(backend = "tensorrt", "TensorRT（RIFE 权重自动构建 Engine）", If(backend = "cuda", "CUDA（PyTorch 权重）", "NCNN（Vulkan）")), False)
        End Sub

        Private Sub OnDynamicOpticalFlowSelected(sender As Object, e As EventArgs)
            If _syncingDynamicOpticalFlow Then Return
            _config.InterpDynamicScaledOpticalFlow = DynamicOpticalFlowValue(_cmbDynamicOpticalFlow.SelectedItem)
            _config.Save()
            UpdateAdvancedControlState()
        End Sub

        Private Sub OnSceneThresholdSelected(sender As Object, e As EventArgs)
            If _syncingSceneThreshold Then Return
            Dim value = SceneThresholdValue(_cmbSceneThreshold.SelectedItem)
            If value <= 0 Then Return
            _config.SceneDetectThreshold = value
            _config.Save()
        End Sub

        Private Sub OnTileSizeSelected(sender As Object, e As EventArgs)
            If _syncingTileSize Then Return
            _config.UpscaleTileSize = TileSizeValue(_cmbTileSize.SelectedItem)
            _config.Save()
            UpdateAdvancedControlState()
        End Sub

        Private Sub InitializeUi()
            ' 不透明画布是背景映射尚未完成时的兜底，避免恢复窗口时短暂穿透到桌面/壁纸。
            BackColor = UiCanvas
            Dock = DockStyle.Fill
            MinimumSize = New Size(900, 680)
            Font = New Font("Microsoft YaHei UI", 10.0F)

            ' 保持宿主插件契约，由 3FUI 将主窗体设置为 BackgroundSource。
            ModernPanel1.Name = "ModernPanel1"
            ModernPanel1.Dock = DockStyle.Fill
            ModernPanel1.Margin = Padding.Empty
            ModernPanel1.Padding = New Padding(24, 20, 24, 18)
            ModernPanel1.BackColor = Color.Transparent
            ModernPanel1.BackColor1 = Color.Transparent
            ModernPanel1.BorderSize = 0
            ModernPanel1.BorderRadius = 0
            Dim root As New BufferedTableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 2,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty,
                .BackColor = Color.Transparent
            }
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            ' 状态栏给按钮保留稳定的下边距，避免矮窗口中按钮白底贴住宿主底边。
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 60.0F))

            _tabs.SuspendLayout()
            Try
                BuildTabs()
            Finally
                _tabs.ResumeLayout(False)
            End Try
            root.Controls.Add(_tabs, 0, 0)

            Dim sectionStatus As New BufferedTableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 3,
                .RowCount = 1,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = New Padding(0, 4, 0, 8)
            }
            sectionStatus.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
            sectionStatus.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 170.0F))
            sectionStatus.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 210.0F))
            sectionStatus.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            _lblStatus.AutoSize = False
            _lblStatus.Dock = DockStyle.Fill
            _lblStatus.Margin = Padding.Empty
            _lblStatus.ForeColor = UiTextMuted
            _lblStatus.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _lblStatus.Text = "<font color=#888888>就绪</font>"
            sectionStatus.Controls.Add(_lblStatus, 0, 0)
            _btnCheckUpdates.Text = "检查更新 v" & PluginVersion.Current
            _btnCheckUpdates.Dock = DockStyle.Fill
            _btnCheckUpdates.AutoSize = False
            _btnCheckUpdates.Margin = New Padding(12, 4, 0, 4)
            ConfigureSecondaryButton(_btnCheckUpdates)
            AddHandler _btnCheckUpdates.Click, AddressOf OnCheckUpdates
            sectionStatus.Controls.Add(_btnCheckUpdates, 2, 0)
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
            BuildOfficialModelDownloadPage()
            BuildOfficialConverterPage()
            BuildOfficialImporterPage()
            BuildMarkdownPage(_pageTutorial,
                "# 快速上手" & Environment.NewLine & Environment.NewLine &
                "## 1. 连接处理程序" & Environment.NewLine &
                "在 **超分主界面** 指定 `videoenhancer.exe`，然后开启插件。" & Environment.NewLine & Environment.NewLine &
                "## 2. 选择处理模式" & Environment.NewLine &
                "- 开启 **视频超分**，选择推理后端和放大模型。" & Environment.NewLine &
                "- 开启 **运动补帧**，选择 RIFE 模型与倍率；可与超分同时开启。" & Environment.NewLine &
                "- **组合处理顺序**只有在视频超分和运动补帧同时开启时才可选择；关闭任一功能后，该选项会自动变灰。" & Environment.NewLine &
                "- **画质优先：先超分，再补帧。** 默认使用该顺序，同一后端在单进程内按帧完成。" & Environment.NewLine &
                "- **速度/算力优先：先补帧，再超分。** 同后端时使用后端原生单程管线；先超分再补帧时使用内置帧传递包装器，均不会生成整段中间视频。" & Environment.NewLine &
                Environment.NewLine &
                "## 3. 同后端与跨后端" & Environment.NewLine &
                "- 当超分后端和补帧后端相同时，两阶段在同一个 RVE 进程内逐帧传递；最终只按一次用户选择的编码器输出，不会为改变顺序生成整段临时视频。" & Environment.NewLine &
                "- 当两者后端不同时，程序会分成两个阶段，并在输出目录生成隐藏的 `.videoenhancer-*.mkv` 临时文件。该文件使用 RGB FFV1 无损编码：SDR 为 `gbrp10le`，检测到 PQ/HLG HDR 时为 `gbrp16le`，音频和字幕直接复制。" & Environment.NewLine &
                "- 跨后端临时文件只在阶段切换期间存在，任务成功、失败或中止后都会自动清理；处理前仍需为它预留可用磁盘空间，4K/高帧率素材会明显增大。FFV1 是阶段间的无损传递，不是最终输出编码。" & Environment.NewLine &
                "- 当前 RVE 2.4.1 的 SDR 帧管线固定使用 8-bit `rgb24`；最终选择 `yuv420p10le` 只控制输出编码格式，不会把内部推理提升为原生 10-bit。跨后端的 `gbrp10le` FFV1 能无损保存第一阶段已经产生的帧，但不能恢复输入在读帧时丢失的 10/12-bit 精度。" & Environment.NewLine &
                "- PQ/HLG HDR 使用 16-bit `rgb48le` 帧管线，目前只允许 CUDA/PyTorch 或 TensorRT。NCNN、ONNX 和 FlashVSR 不具备完整 HDR 帧能力，程序会明确中止，避免静默降为 SDR。" & Environment.NewLine &
                Environment.NewLine &
                "## 4. 加入编码队列" & Environment.NewLine &
                "回到 3FUI 准备文件并加入队列，插件会自动通过 CLI 中转。" & Environment.NewLine & Environment.NewLine &
                "## 5. 查看输出" & Environment.NewLine &
                "在 **实时预览** 查看处理中或已完成的帧。")

            For Each page As Panel In New Panel() {
                _pageUpscale, _pagePreview, _pageDownloader,
                _pageConverter, _pageImporter, _pageTutorial
            }
                page.BackColor = Color.Transparent
                Dim modernPage = TryCast(page, ModernPanel)
                If modernPage IsNot Nothing Then
                    modernPage.BackColor1 = Color.Transparent
                End If
            Next

            Dim tabMain As New ModernTabControl.ModernTab("超分工作台") With {.BoundControl = _pageUpscale}
            Dim tabPreview As New ModernTabControl.ModernTab("实时预览") With {.BoundControl = _pagePreview}
            Dim tabDownloader As New ModernTabControl.ModernTab("模型下载") With {.BoundControl = _pageDownloader}
            Dim tabConverter As New ModernTabControl.ModernTab("模型转换") With {.BoundControl = _pageConverter}
            Dim tabImporter As New ModernTabControl.ModernTab("模型导入") With {.BoundControl = _pageImporter}
            Dim tabTutorial As New ModernTabControl.ModernTab("使用教程") With {.BoundControl = _pageTutorial}
            _tabs.Items.Add(tabMain)
            _tabs.Items.Add(tabPreview)
            _tabs.Items.Add(tabDownloader)
            _tabs.Items.Add(tabConverter)
            _tabs.Items.Add(tabImporter)
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
            Dim host As New Panel With {
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            Dim line As New Panel With {
                .BackColor = Color.FromArgb(58, 220, 220, 220)
            }
            line.Anchor = AnchorStyles.Left Or AnchorStyles.Right Or AnchorStyles.Top
            host.Controls.Add(line)
            AddHandler host.Layout,
                Sub(sender, e)
                    line.SetBounds(0, Math.Max(0, (host.ClientSize.Height - 1) \ 2),
                        host.ClientSize.Width, 1)
                End Sub
            Return host
        End Function

        Private Shared Function BuildOfficialModeHeader(title As String, description As String,
                                                        switchControl As LakeUI.BooleanSwitch,
                                                        stateLabel As HtmlColorLabel) As Control
            Dim titleLabel = CreateTextLabel(title, 12.0F, FontStyle.Regular, UiText)
            titleLabel.Margin = Padding.Empty
            titleLabel.TextAlign = ContentAlignment.MiddleLeft
            Dim titleWidth = Math.Max(84, TextRenderer.MeasureText(title, titleLabel.Font).Width + 4)
            Dim row As New HorizontalLayoutPanel(
                CSng(titleWidth), 10.0F, 42.0F, -1.0F, 112.0F)
            switchControl.Anchor = AnchorStyles.None
            switchControl.Margin = Padding.Empty
            Dim descriptionLabel = CreateOfficialCaption(description)
            descriptionLabel.TextAlign = ContentAlignment.MiddleLeft
            descriptionLabel.Margin = New Padding(14, 0, 0, 0)
            stateLabel.Dock = DockStyle.Fill
            stateLabel.Margin = Padding.Empty
            stateLabel.AutoSize = False
            stateLabel.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleRight
            row.AddColumn(titleLabel, 0)
            row.AddColumn(switchControl, 2)
            row.AddColumn(descriptionLabel, 3)
            row.AddColumn(stateLabel, 4)
            Return row
        End Function

        Private Shared Sub AddWorkbenchControl(root As ModernPanel, control As Control,
                                               top As Integer, height As Integer,
                                               leftRatio As Single, rightRatio As Single,
                                               Optional leftOffset As Integer = 0,
                                               Optional rightOffset As Integer = 0)
            control.Dock = DockStyle.None
            control.Anchor = AnchorStyles.Top Or AnchorStyles.Left
            Dim arrange =
                Sub()
                    Dim left = CInt(Math.Round(root.ClientSize.Width * leftRatio)) + leftOffset
                    Dim right = CInt(Math.Round(root.ClientSize.Width * rightRatio)) + rightOffset
                    control.SetBounds(left, top, Math.Max(0, right - left), height)
                End Sub
            root.Controls.Add(control)
            AddHandler root.Layout, Sub(sender, e) arrange()
            arrange()
        End Sub

        Private Shared Sub AddWorkbenchRow(root As ModernPanel, control As Control,
                                           top As Integer, height As Integer)
            AddWorkbenchControl(root, control, top, height, 0.0F, 1.0F)
        End Sub

        Private Sub BuildOfficialUpscalePage()
            _pageUpscale.Dock = DockStyle.Fill
            _pageUpscale.BackColor = Color.Transparent
            _pageUpscale.BackColor1 = Color.Transparent
            _pageUpscale.BorderSize = 0
            _pageUpscale.Padding = Padding.Empty
            ' 使用 LakeUI ModernPanel 原生滚动，避免 WinForms 白色非客户区滚动条。
            _pageUpscale.AutoScroll = False
            _pageUpscale.LayoutMode = ModernPanel.LayoutModeEnum.Absolute
            _pageUpscale.ScrollBarMode = ModernPanel.ScrollMode.Vertical
            _pageUpscale.ScrollBarWidth = 10
            _pageUpscale.ScrollBarTrackColor = Color.FromArgb(18, 18, 18)
            _pageUpscale.ScrollBarThumbColor = Color.FromArgb(72, 72, 72)
            _pageUpscale.ScrollBarThumbHoverColor = Color.FromArgb(104, 104, 104)
            _pageUpscale.VerticalScrollStep = 48
            _pageUpscale.AllowDrop = True
            AddHandler _pageUpscale.DragEnter, AddressOf OnImageDragEnter
            AddHandler _pageUpscale.DragDrop, AddressOf OnImageDragDrop

            ' 根容器保持固定内容高度并左右锚定；窗口较小时由页面滚动承载。
            ' 宽度交给标准 Anchor 布局事务，避免尺寸事件中强制重排整棵表格树。
            Dim root As New ModernPanel With {
                .Dock = DockStyle.None,
                .Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right,
                .AutoSize = False,
                .MinimumSize = New Size(0, 850),
                .Height = 850,
                .BackColor = Color.Transparent,
                .BackColor1 = Color.Transparent,
                .BackgroundSource = ModernPanel1,
                .LayoutMode = ModernPanel.LayoutModeEnum.Absolute,
                .ScrollBarMode = ModernPanel.ScrollMode.None,
                .BorderSize = 0,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty,
                .AllowDrop = True
            }
            AddHandler root.DragEnter, AddressOf OnImageDragEnter
            AddHandler root.DragDrop, AddressOf OnImageDragDrop
            root.SetBounds(0, 0, Math.Max(0,
                _pageUpscale.ClientSize.Width - _pageUpscale.ScrollBarWidth - 2), 850)

            ConfigureDpiSwitch(_switchMaster)
            _switchMaster.Checked = _config.Enabled
            AddHandler _switchMaster.CheckedChanged, AddressOf OnMasterSwitchChanged
            AddWorkbenchRow(root, BuildOfficialModeHeader(
                "插件总开关", "", _switchMaster, _lblMaster), 0, 40)

            Dim exeRow As New HorizontalLayoutPanel(150.0F, 12.0F, -1.0F)
            _btnPickExe.Text = "选择处理程序"
            _btnPickExe.Dock = DockStyle.Fill
            _btnPickExe.Margin = New Padding(0, 6, 0, 6)
            ConfigureSecondaryButton(_btnPickExe)
            AddHandler _btnPickExe.Click, AddressOf OnPickExeClick
            _lblExe.AutoSize = False
            _lblExe.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _lblExe.ForeColor = UiText
            exeRow.AddColumn(_btnPickExe, 0)
            exeRow.AddColumn(CreateOfficialValueBox(_lblExe), 2)
            AddWorkbenchRow(root, exeRow, 40, 48)
            AddWorkbenchRow(root, CreateOfficialSeparator(), 88, 25)

            AddWorkbenchRow(root, CreateOfficialSectionHeading(
                "视频处理", "超分与补帧可同时开启；默认按画质优先先超分、再补帧"), 113, 36)

            ConfigureDpiSwitch(_switchUpscale)
            _switchUpscale.Checked = _config.UpscaleEnabled
            _switchUpscale.Enabled = _config.Enabled
            AddHandler _switchUpscale.CheckedChanged, AddressOf OnUpscaleSwitchChanged
            Dim upscaleHeader = BuildOfficialModeHeader(
                "视频超分", "", _switchUpscale, _lblSwitch)
            _cmbBackend.WaterText = "选择推理方式…"
            ConfigureCombo(_cmbBackend)
            _cmbBackend.Items.Add("NCNN (Vulkan)")
            _cmbBackend.Items.Add("CUDA (PyTorch)")
            _cmbBackend.Items.Add("TensorRT (NVIDIA)")
            _cmbBackend.Items.Add("ONNX Runtime")
            _cmbBackend.Items.Add("FlashVSR (NVIDIA · 视频)")
            _cmbBackend.Items.Add("BasicVSR++ (NVIDIA · 视频)")
            AddHandler _cmbBackend.SelectedIndexChanged, AddressOf OnBackendSelected
            _cmbModel.WaterText = "选择放大模型…"
            ConfigureCombo(_cmbModel)
            AddHandler _cmbModel.DropDownOpened, AddressOf OnModelDropDownOpened
            AddHandler _cmbModel.Click, AddressOf OnModelComboClicked
            AddHandler _cmbModel.SelectedIndexChanged, AddressOf OnModelSelected
            Dim upscaleBackendField = CreateOfficialField("推理后端", _cmbBackend)
            Dim upscaleModelField = CreateOfficialField("放大模型", _cmbModel, 0)
            _cmbTileSize.WaterText = "RVE 默认（0）"
            ConfigureCombo(_cmbTileSize)
            _cmbTileSize.Items.Add("RVE 默认（0）")
            _cmbTileSize.Items.Add("128 px")
            _cmbTileSize.Items.Add("256 px")
            _cmbTileSize.Items.Add("384 px")
            _cmbTileSize.Items.Add("512 px")
            _cmbTileSize.Items.Add("768 px")
            _cmbTileSize.Items.Add("1024 px")
            AddHandler _cmbTileSize.SelectedIndexChanged, AddressOf OnTileSizeSelected
            Dim upscaleTileField = CreateOfficialField("超分分块尺寸", _cmbTileSize)
            Dim tileHint = CreateOfficialCaption("0=RVE默认；越小越省显存但更慢", UiTextMuted)
            tileHint.TextAlign = ContentAlignment.BottomLeft
            tileHint.Margin = Padding.Empty
            ConfigureDpiSwitch(_switchInterp)
            If String.Equals(_config.Backend, "basicvsrpp", StringComparison.OrdinalIgnoreCase) Then
                _config.InterpEnabled = False
                _config.InterpModel = ""
            End If
            SyncInterpSwitchFromConfig()
            AddHandler _switchInterp.CheckedChanged, AddressOf OnInterpSwitchChanged
            Dim interpHeader = BuildOfficialModeHeader(
                "运动补帧", "", _switchInterp, _lblSwitchInterp)
            _cmbInterpBackend.WaterText = "选择后端…"
            ConfigureCombo(_cmbInterpBackend)
            _cmbInterpBackend.Items.Add("NCNN (Vulkan)")
            _cmbInterpBackend.Items.Add("CUDA (PyTorch)")
            _cmbInterpBackend.Items.Add("TensorRT (NVIDIA)")
            AddHandler _cmbInterpBackend.SelectedIndexChanged, AddressOf OnInterpBackendSelected
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
            Dim interpBackendField = CreateOfficialField("补帧后端", _cmbInterpBackend)
            Dim interpModelField = CreateOfficialField("补帧模型", _cmbInterp)
            Dim interpFactorField = CreateOfficialField("补帧倍率", _cmbFactor, 0)
            _cmbSceneThreshold.WaterText = "标准 4.0"
            ConfigureCombo(_cmbSceneThreshold)
            _cmbSceneThreshold.Items.Add("敏感 1.0")
            _cmbSceneThreshold.Items.Add("较敏感 2.0")
            _cmbSceneThreshold.Items.Add("官方默认 3.5")
            _cmbSceneThreshold.Items.Add("标准 4.0")
            _cmbSceneThreshold.Items.Add("宽松 6.0")
            _cmbSceneThreshold.Items.Add("很宽松 8.0")
            _cmbSceneThreshold.Items.Add("极宽松 10.0")
            AddHandler _cmbSceneThreshold.SelectedIndexChanged, AddressOf OnSceneThresholdSelected
            _cmbDynamicOpticalFlow.WaterText = "关闭"
            ConfigureCombo(_cmbDynamicOpticalFlow)
            _cmbDynamicOpticalFlow.Items.Add("关闭")
            _cmbDynamicOpticalFlow.Items.Add("开启")
            AddHandler _cmbDynamicOpticalFlow.SelectedIndexChanged, AddressOf OnDynamicOpticalFlowSelected
            Dim interpThresholdField = CreateOfficialField("转场阈值", _cmbSceneThreshold)
            Dim interpFlowField = CreateOfficialField("动态光流尺度", _cmbDynamicOpticalFlow)

            AddWorkbenchRow(root, upscaleHeader, 149, 38)
            AddWorkbenchControl(root, upscaleBackendField, 187, 76, 0.0F, 0.46F, 0, -12)
            AddWorkbenchControl(root, upscaleModelField, 187, 76, 0.46F, 1.0F)
            AddWorkbenchControl(root, upscaleTileField, 263, 70, 0.0F, 0.46F, 0, -12)
            AddWorkbenchControl(root, tileHint, 263, 70, 0.46F, 1.0F)
            AddWorkbenchRow(root, interpHeader, 345, 38)
            AddWorkbenchControl(root, interpBackendField, 383, 76, 0.0F, 0.29F, 0, -12)
            AddWorkbenchControl(root, interpModelField, 383, 76, 0.29F, 0.76F, 0, -12)
            AddWorkbenchControl(root, interpFactorField, 383, 76, 0.76F, 1.0F)
            AddWorkbenchControl(root, interpThresholdField, 459, 70, 0.0F, 0.29F, 0, -12)
            AddWorkbenchControl(root, interpFlowField, 459, 70, 0.29F, 0.76F, 0, -12)

            Dim orderRow As New HorizontalLayoutPanel(150.0F, -54.0F, -46.0F) With {
                .Margin = New Padding(0, 8, 0, 0)
            }
            Dim orderCaption = CreateOfficialCaption("组合处理顺序")
            orderCaption.AutoSize = False
            orderCaption.Dock = DockStyle.Fill
            orderCaption.TextAlign = ContentAlignment.MiddleLeft
            _cmbProcessOrder.Items.Add("画质优先：先超分，再补帧")
            _cmbProcessOrder.Items.Add("速度/算力优先：先补帧，再超分")
            _cmbProcessOrder.SelectedIndex = If(String.Equals(_config.ProcessOrder, "interp-first", StringComparison.OrdinalIgnoreCase), 1, 0)
            _cmbProcessOrder.WaterText = "选择组合处理顺序…"
            ConfigureCombo(_cmbProcessOrder)
            _cmbProcessOrder.Editable = False
            Dim processOrderIndex = If(String.Equals(_config.ProcessOrder, "interp-first", StringComparison.OrdinalIgnoreCase), 1, 0)
            _cmbProcessOrder.SelectedIndex = -1
            _cmbProcessOrder.SelectedIndex = processOrderIndex
            _cmbProcessOrder.Margin = New Padding(0, 6, 12, 6)
            AddHandler _cmbProcessOrder.SelectedIndexChanged, AddressOf OnProcessOrderSelected
            _lblProcessOrder.AutoSize = False
            _lblProcessOrder.Dock = DockStyle.Fill
            _lblProcessOrder.Margin = Padding.Empty
            _lblProcessOrder.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            orderRow.AddColumn(orderCaption, 0)
            orderRow.AddColumn(_cmbProcessOrder, 1)
            orderRow.AddColumn(_lblProcessOrder, 2)
            AddWorkbenchRow(root, orderRow, 529, 56)
            AddWorkbenchRow(root, CreateOfficialSeparator(), 585, 25)

            AddWorkbenchRow(root, CreateOfficialSectionHeading(
                "图片增强", "沿用上方超分后端与模型，可选择文件、文件夹或直接拖入"), 610, 36)

            Dim imageInputRow As New HorizontalLayoutPanel(
                150.0F, 12.0F, 170.0F, 12.0F, -1.0F) With {
                .AllowDrop = True
            }
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
            imageInputRow.AddColumn(_btnImageFiles, 0)
            imageInputRow.AddColumn(_btnImageFolder, 2)
            imageInputRow.AddColumn(CreateOfficialValueBox(_lblImageInputs), 4)
            AddHandler imageInputRow.DragEnter, AddressOf OnImageDragEnter
            AddHandler imageInputRow.DragDrop, AddressOf OnImageDragDrop
            AddWorkbenchRow(root, imageInputRow, 646, 54)

            Dim imageOutputRow As New HorizontalLayoutPanel(170.0F, 12.0F, -1.0F)
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
            imageOutputRow.AddColumn(_btnImageOutput, 0)
            imageOutputRow.AddColumn(_txtImageOutput, 2)
            AddWorkbenchRow(root, imageOutputRow, 700, 54)

            Dim imageOptionsRow As New HorizontalLayoutPanel(
                82.0F, 220.0F, 20.0F, 82.0F, 220.0F, -1.0F, 16.0F, 170.0F)

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

            imageOptionsRow.AddColumn(suffixLabel, 0)
            imageOptionsRow.AddColumn(_cmbImageSuffix, 1)
            imageOptionsRow.AddColumn(formatLabel, 3)
            imageOptionsRow.AddColumn(_cmbImageFormat, 4)
            imageOptionsRow.AddColumn(_btnImageStart, 7)
            AddWorkbenchRow(root, imageOptionsRow, 754, 54)

            Dim progressRow As New HorizontalLayoutPanel(-1.0F, 16.0F, 300.0F)
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
            progressRow.AddColumn(_imageProgress, 0)
            progressRow.AddColumn(_lblImageProgress, 2)
            AddWorkbenchRow(root, progressRow, 808, 42)

            _pageUpscale.Controls.Add(root)
            ' 为 LakeUI 覆盖式滚动条保留绘制带，避免子窗口覆盖父面板的 GPU 滚动条。
            root.SetBounds(0, 0, Math.Max(0,
                _pageUpscale.ClientSize.Width - _pageUpscale.ScrollBarWidth - 2), 850)
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
            _cmbBackend.Items.Add("BasicVSR++ (NVIDIA · 视频)")
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
            SyncInterpSwitchFromConfig()
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
            SyncInterpSwitchFromConfig()
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
            _cmbBackend.Items.Add("BasicVSR++ (NVIDIA · 视频)")
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
                .ColumnCount = 3,
                .RowCount = 1,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            header.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
            header.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 174.0F))
            header.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 190.0F))
            header.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            header.Controls.Add(CreateOfficialSectionHeading(
                "模型资源库", "从 ModelScope 获取模型与后端组件"), 0, 0)
            _btnDownloadPluginUpdate.Text = "下载全部"
            _btnDownloadPluginUpdate.Dock = DockStyle.Fill
            _btnDownloadPluginUpdate.AutoSize = False
            _btnDownloadPluginUpdate.Margin = New Padding(12, 7, 0, 7)
            ConfigureSecondaryButton(_btnDownloadPluginUpdate)
            AddHandler _btnDownloadPluginUpdate.Click, AddressOf OnDownloadAllClick
            header.Controls.Add(_btnDownloadPluginUpdate, 2, 0)
            _btnRefreshDownloads.Text = "刷新资源"
            _btnRefreshDownloads.Dock = DockStyle.Fill
            _btnRefreshDownloads.Margin = New Padding(12, 7, 0, 7)
            ConfigureSecondaryButton(_btnRefreshDownloads)
            AddHandler _btnRefreshDownloads.Click, Sub(sender, e) LoadDownloadModels(True)
            header.Controls.Add(_btnRefreshDownloads, 1, 0)
            root.Controls.Add(header, 0, 0)

            ConfigureDownloadList()
            root.Controls.Add(_downloadList, 0, 1)
            _pageDownloader.Controls.Add(root)
        End Sub

        Private Sub ConfigureDownloadList()
            If _downloadListConfigured Then Return
            _downloadListConfigured = True
            _downloadList.Dock = DockStyle.Fill
            _downloadList.Margin = Padding.Empty
            _downloadList.AutoScroll = False
            _downloadList.Font = New Font("Microsoft YaHei UI", 9.2F)
            _downloadList.BackColor = Color.Transparent
            _downloadList.BackgroundColor = Color.Transparent
            _downloadList.BackgroundSource = ModernPanel1
            _downloadList.BorderColor = Color.Transparent
            _downloadList.BorderSize = 0
            _downloadList.BorderRadius = 0
            _downloadList.HeaderVisible = True
            _downloadList.HeaderHeight = 38
            _downloadList.HeaderBackColor = Color.FromArgb(36, 36, 36)
            _downloadList.HeaderForeColor = UiTextSecondary
            _downloadList.HeaderBorderColor = Color.FromArgb(52, 52, 52)
            _downloadList.HeaderBorderWidth = 1
            _downloadList.AllowColumnResize = True
            _downloadList.MultiSelect = False
            _downloadList.AllowDragReorder = False
            _downloadList.ItemForeColor = UiTextSecondary
            _downloadList.ItemHoverBackColor = Color.FromArgb(48, 255, 255, 255)
            _downloadList.ItemSelectedBackColor = Color.FromArgb(54, 71, 156, 255)
            _downloadList.ItemCornerRadius = 4
            _downloadList.ItemPadding = New Padding(12, 8, 10, 8)
            _downloadList.ItemSpacing = 2
            _downloadList.ContentPadding = New Padding(0, 4, 0, 4)
            _downloadList.GroupHeight = 38
            _downloadList.GroupBackColor = Color.FromArgb(31, 31, 31)
            _downloadList.GroupForeColor = UiText
            _downloadList.GroupBorderColor = Color.FromArgb(48, 48, 48)
            _downloadList.ScrollBarWidth = 10
            _downloadList.ScrollBarTrackColor = Color.FromArgb(18, 18, 18)
            _downloadList.ScrollBarThumbColor = Color.FromArgb(72, 72, 72)
            _downloadList.ScrollBarThumbHoverColor = Color.FromArgb(104, 104, 104)
            _downloadList.Columns.AddRange(New UltraDetailListView.ListColumn() {
                New UltraDetailListView.ListColumn("资源名称", 520),
                New UltraDetailListView.ListColumn("大小", 110),
                New UltraDetailListView.ListColumn("状态", 130),
                New UltraDetailListView.ListColumn("操作", 138)
            })
            AddHandler _downloadList.ItemClick, AddressOf OnDownloadListItemClick
            AddHandler _downloadList.ClientSizeChanged,
                Sub(sender, e)
                    If _downloadList.Columns.Count = 0 Then Return
                    Dim resourceWidth = Math.Max(260, _downloadList.ClientSize.Width - 10 - 110 - 130 - 138)
                    If _downloadList.Columns(0).Width <> resourceWidth Then
                        _downloadList.Columns(0).Width = resourceWidth
                        _downloadList.RefreshItems()
                    End If
                End Sub
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

        Private Sub BuildModelDownloadPage()
            _pageDownloader.Dock = DockStyle.Fill
            _pageDownloader.BackColor = Color.Transparent
            _pageDownloader.Padding = New Padding(8, 14, 8, 10)

            ConfigureDownloadList()
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
                        "<font color=#8A8A8A>模型下载到对应目录；Backend 单独检查版本并优先下载增量补丁，失败会自动回滚。</font>",
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

            ConfigureDownloadList()
            _pageDownloader.Controls.Add(_downloadList)
            ' Fill 先加入、Top 后加入，确保标题栏占位后列表填满其余区域。
            _pageDownloader.Controls.SetChildIndex(_downloadList, 0)
            _pageDownloader.Controls.SetChildIndex(header, 1)
        End Sub

        Private Function DownloadExecutablePath() As String
            Return PluginConfig.ResolveInstalledExePath(_config.ExePath)
        End Function

        Private Sub ResetDownloadList()
            _downloadList.Items.Clear()
            _downloadList.Groups.Clear()
            _downloadItemsByPath.Clear()
            _downloadGroupItems.Clear()
        End Sub

        Private Sub AddDownloadMessage(title As String, detail As String, color As Color)
            Dim item = New UltraDetailListView.ListItem(New UltraDetailListView.ListSubItem() {
                New UltraDetailListView.ListSubItem(title, New Font("Microsoft YaHei UI", 9.4F, FontStyle.Bold), color),
                New UltraDetailListView.ListSubItem(""),
                New UltraDetailListView.ListSubItem(detail, Nothing, UiTextMuted),
                New UltraDetailListView.ListSubItem("")
            })
            _downloadList.Items.Add(item)
        End Sub

        Private Sub LoadDownloadModels(force As Boolean)
            If _downloadsLoading OrElse _archiveCleanupBusy OrElse _downloadActiveCount > 0 OrElse
                (_downloadsLoaded AndAlso Not force) Then Return
            Dim exePath = DownloadExecutablePath()
            If String.IsNullOrWhiteSpace(exePath) Then
                ShowStatus("请先在超分主界面指定 videoenhancer.exe", True)
                Return
            End If
            _downloadsLoading = True
            _btnRefreshDownloads.Enabled = False
            _btnCleanArchives.Enabled = False
            _btnDownloadPluginUpdate.Enabled = False
            _downloadActionsEnabled = False
            _downloadList.BeginUpdate()
            Try
                ResetDownloadList()
                AddDownloadMessage("正在同步模型资源...", "请稍候", UiTextSecondary)
            Finally
                _downloadList.EndUpdate()
            End Try

            Task.Run(
                Sub()
                    Dim stdout = ""
                    Dim stderr = ""
                    Dim exitCode = -1
                    Dim backendStdout = ""
                    Dim backendExitCode = -1
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
                        Dim backendPsi As New ProcessStartInfo With {
                            .FileName = exePath, .WorkingDirectory = Path.GetDirectoryName(exePath),
                            .UseShellExecute = False, .RedirectStandardOutput = True,
                            .RedirectStandardError = True, .CreateNoWindow = True,
                            .StandardOutputEncoding = Encoding.UTF8, .StandardErrorEncoding = Encoding.UTF8
                        }
                        backendPsi.ArgumentList.Add("--backend-status")
                        backendPsi.ArgumentList.Add("--json")
                        Using backendProcess As Process = Diagnostics.Process.Start(backendPsi)
                            If backendProcess IsNot Nothing Then
                                Dim backendOutputTask = backendProcess.StandardOutput.ReadToEndAsync()
                                Dim backendErrorTask = backendProcess.StandardError.ReadToEndAsync()
                                If backendProcess.WaitForExit(45000) Then
                                    backendStdout = backendOutputTask.GetAwaiter().GetResult()
                                    backendExitCode = backendProcess.ExitCode
                                    If backendExitCode <> 0 Then stderr &= Environment.NewLine & backendErrorTask.GetAwaiter().GetResult()
                                Else
                                    Try
                                        backendProcess.Kill(True)
                                    Catch
                                    End Try
                                End If
                            End If
                        End Using
                    Catch ex As Exception
                        stderr = ex.Message
                    End Try
                    Try
                        BeginInvoke(New Action(Sub() RenderDownloadModels(stdout, stderr, exitCode, backendStdout, backendExitCode)))
                    Catch
                    End Try
                End Sub)
        End Sub

        Private Sub RenderDownloadModels(stdout As String, stderr As String, exitCode As Integer,
                                         backendStdout As String, backendExitCode As Integer)
            _downloadsLoading = False
            _btnRefreshDownloads.Enabled = True
            _downloadActionsEnabled = True
            _downloadList.BeginUpdate()
            Try
                ResetDownloadList()
                If exitCode <> 0 OrElse String.IsNullOrWhiteSpace(stdout) Then
                    _downloadsLoaded = False
                    If stderr.Contains("NO_NETWORK|", StringComparison.Ordinal) Then
                        _downloadOnline = False
                        ShowOfflineDownloadStatus()
                    ElseIf stderr.Contains("AUTH_REQUIRED|", StringComparison.Ordinal) Then
                        _downloadOnline = True
                        AddDownloadMessage("模型仓库需要认证", "设置 ModelScope 令牌后重启 3FUI", UiDanger)
                        ShowStatus("私有模型仓库需要有效令牌，请设置 VIDEOENHANCER_MODELSCOPE_TOKEN 或 MODELSCOPE_API_TOKEN 后重启 3FUI。", True)
                    Else
                        _downloadOnline = True
                        AddDownloadMessage("模型列表读取失败", "点击右上角刷新资源重试", UiDanger)
                        ShowStatus(CliErrorMessage(stderr, "模型列表读取失败"), True)
                    End If
                    Return
                End If

                Try
                    Dim entries As New List(Of DownloadModelEntry)()
                    Dim backendStatus As BackendDownloadStatus = Nothing
                    If backendExitCode = 0 AndAlso Not String.IsNullOrWhiteSpace(backendStdout) Then
                        Using backendDocument = JsonDocument.Parse(backendStdout.Trim())
                            Dim root = backendDocument.RootElement
                            backendStatus = New BackendDownloadStatus With {
                                .State = root.GetProperty("state").GetString(),
                                .InstalledVersion = root.GetProperty("installedVersion").GetString(),
                                .LatestVersion = root.GetProperty("latestVersion").GetString(),
                                .Mode = root.GetProperty("mode").GetString(),
                                .DownloadSize = root.GetProperty("downloadSize").GetInt64(),
                                .FullSize = root.GetProperty("fullSize").GetInt64()
                            }
                        End Using
                    End If
                    Using document = JsonDocument.Parse(stdout.Trim())
                        For Each item In document.RootElement.EnumerateArray()
                            Dim name = item.GetProperty("name").GetString()
                            Dim relativePath = item.GetProperty("path").GetString()
                            Dim size = item.GetProperty("size").GetInt64()
                            Dim entry = New DownloadModelEntry With {
                                .Name = If(name, relativePath), .RelativePath = If(relativePath, ""), .Size = size,
                                .Installed = IsDownloadInstalled(If(relativePath, ""))
                            }
                            entry.IsBackend = DownloadCategory(entry.RelativePath).Equals("Backend", StringComparison.OrdinalIgnoreCase)
                            If entry.IsBackend Then ApplyBackendDownloadStatus(entry, backendStatus)
                            entries.Add(entry)
                        Next
                    End Using
                    Dim categoryOrder = New String() {"Plugin", "Backend", "BasicVSR++", "Bin", "ONNX", "Param-Bin", "FlashVSR", "Frame-Interpolation", "RIFE", "PTH", "TensorRT-Default"}
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
                UpdateDownloadUtilityButtons()
            Finally
                _downloadList.EndUpdate()
            End Try
        End Sub

        Private Shared Sub ApplyBackendDownloadStatus(entry As DownloadModelEntry, status As BackendDownloadStatus)
            entry.Name = "Backend 后端"
            If status Is Nothing Then
                entry.Installed = True
                entry.StatusText = "更新信息不可用"
                entry.ActionText = "刷新后重试"
                Return
            End If
            entry.Size = status.DownloadSize
            entry.BackendFullSize = status.FullSize
            entry.ForceBackendFull = status.Mode.Equals("full", StringComparison.OrdinalIgnoreCase)
            Select Case status.State
                Case "current"
                    entry.Installed = True
                    entry.Name &= " " & status.LatestVersion
                    ' 状态列较窄，长文本会被列表控件按两行高度布局而显得上浮。
                    entry.StatusText = "已是最新"
                    entry.ActionText = "无需操作"
                Case "update-available", "legacy-update-available"
                    entry.Installed = False
                    entry.Name &= " " & status.InstalledVersion & " → " & status.LatestVersion
                    entry.StatusText = If(status.Mode = "patch", "可增量更新", "需要完整修复")
                    entry.ActionText = If(status.Mode = "patch", "增量更新", "完整修复")
                Case "not-installed"
                    entry.Installed = False
                    entry.Name &= " " & status.LatestVersion
                    entry.StatusText = "尚未安装"
                    entry.ActionText = "完整安装"
                Case Else
                    entry.Installed = False
                    entry.Name &= " → " & status.LatestVersion
                    entry.StatusText = "版本无法识别"
                    entry.ActionText = "完整修复"
            End Select
        End Sub

        Private Shared Function DownloadCategory(relativePath As String) As String
            If String.IsNullOrWhiteSpace(relativePath) Then Return "其他"
            Dim normalized = relativePath.Replace("\"c, "/"c)
            Dim slash = normalized.IndexOf("/"c)
            Return If(slash > 0, normalized.Substring(0, slash), normalized)
        End Function

        Private Shared Function DownloadCategoryTitle(category As String) As String
            Select Case category.ToUpperInvariant()
                Case "PLUGIN" : Return "插件文件"
                Case "ONNX" : Return "ONNX 模型"
                Case "PARAM-BIN" : Return "Param-Bin 模型"
                Case "FRAME-INTERPOLATION" : Return "Frame-Interpolation 补帧模型"
                Case "RIFE" : Return "旧版 RIFE 补帧模型"
                Case "PTH" : Return "PTH 模型"
                Case "BASICVSR++" : Return "BasicVSR++ 模型"
                Case "BACKEND" : Return "Backend 后端"
                Case Else : Return category
            End Select
        End Function

        Private Function IsDownloadInstalled(relativePath As String) As Boolean
            If String.IsNullOrWhiteSpace(relativePath) Then Return False
            Try
                Dim normalized = relativePath.Replace("\"c, "/"c).TrimStart("/"c)
                Dim slash = normalized.IndexOf("/"c)
                If slash <= 0 Then Return False
                Dim category = normalized.Substring(0, slash)
                Dim suffix = normalized.Substring(slash + 1).Replace("/"c, Path.DirectorySeparatorChar)
                Dim coreRoot = ResolveCoreRoot()
                Dim resolvedExe = PluginConfig.ResolveInstalledExePath(_config.ExePath)
                Dim destinationRoot = If(category.Equals("Plugin", StringComparison.OrdinalIgnoreCase),
                    If(String.IsNullOrWhiteSpace(resolvedExe), coreRoot, Path.GetDirectoryName(resolvedExe)),
                    If(category.Equals("Backend", StringComparison.OrdinalIgnoreCase),
                        Path.Combine(coreRoot, "python"),
                        If(category.Equals("Bin", StringComparison.OrdinalIgnoreCase),
                            Path.Combine(coreRoot, "bin"), Path.Combine(coreRoot, "models", category))))
                Dim downloaded = Path.Combine(destinationRoot, suffix)
                If File.Exists(downloaded) Then Return True

                ' 压缩包下载后会自动解压；刷新时用解压后的核心文件判断，清理压缩包后仍能保持“已存在”。
                If Not String.Equals(Path.GetExtension(suffix), ".7z", StringComparison.OrdinalIgnoreCase) AndAlso
                   Not String.Equals(Path.GetExtension(suffix), ".zip", StringComparison.OrdinalIgnoreCase) Then
                    Return False
                End If
                If category.Equals("Backend", StringComparison.OrdinalIgnoreCase) Then
                    Return File.Exists(Path.Combine(coreRoot, "python", "python", "python.exe"))
                End If
                If category.Equals("Bin", StringComparison.OrdinalIgnoreCase) Then
                    Dim archiveName = Path.GetFileNameWithoutExtension(suffix)
                    If archiveName.Equals("ffmpeg", StringComparison.OrdinalIgnoreCase) Then
                        Return File.Exists(Path.Combine(coreRoot, "bin", "ffmpeg", "ffmpeg.exe"))
                    End If
                    If archiveName.Equals("mkvtoolnix", StringComparison.OrdinalIgnoreCase) Then
                        Return Directory.Exists(Path.Combine(coreRoot, "bin", "mkvtoolnix"))
                    End If
                    If archiveName.Equals("PortableGit", StringComparison.OrdinalIgnoreCase) Then
                        Return Directory.Exists(Path.Combine(coreRoot, "bin", "PortableGit"))
                    End If
                End If
                If category.Equals("Frame-Interpolation", StringComparison.OrdinalIgnoreCase) Then
                    Return IsDownloadArchive(suffix) AndAlso
                        File.Exists(FrameInterpolationArchiveMarkerPath(coreRoot, normalized))
                End If
                If category.Equals("RIFE", StringComparison.OrdinalIgnoreCase) Then
                    Return Directory.Exists(Path.Combine(coreRoot, "models", "RIFE")) AndAlso
                        Directory.EnumerateFiles(Path.Combine(coreRoot, "models", "RIFE"), "*.param", SearchOption.AllDirectories).Any() AndAlso
                        Directory.EnumerateFiles(Path.Combine(coreRoot, "models", "RIFE"), "*.bin", SearchOption.AllDirectories).Any()
                End If
                If category.Equals("Param-Bin", StringComparison.OrdinalIgnoreCase) Then
                    Dim modelsRoot = Path.Combine(coreRoot, "models")
                    Return Directory.Exists(modelsRoot) AndAlso
                        Directory.EnumerateFiles(modelsRoot, "*.param", SearchOption.AllDirectories).Any() AndAlso
                        Directory.EnumerateFiles(modelsRoot, "*.bin", SearchOption.AllDirectories).Any()
                End If
                Return False
            Catch
                Return False
            End Try
        End Function

        Private Shared Function IsDownloadArchive(valuePath As String) As Boolean
            Select Case Path.GetExtension(valuePath).ToLowerInvariant()
                Case ".7z", ".zip", ".rar", ".gz", ".xz", ".zst", ".tar"
                    Return True
                Case Else
                    Return False
            End Select
        End Function

        Private Shared Function FrameInterpolationArchiveMarkerPath(coreRoot As String, relativePath As String) As String
            Dim normalized = relativePath.Replace("\"c, "/"c).ToUpperInvariant()
            Dim hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))
            Return Path.Combine(coreRoot, "models", "Frame-Interpolation", ".downloads", hash & ".installed")
        End Function

        Private Sub AddDownloadGroup(category As String, entries As List(Of DownloadModelEntry))
            Dim group = New UltraDetailListView.ListGroup(category,
                DownloadCategoryTitle(category) & "  ·  " & entries.Count & " 个文件") With {
                .ForeColor = If(category.Equals("Backend", StringComparison.OrdinalIgnoreCase), UiSuccess, UiText)
            }
            _downloadList.Groups.Add(group)

            Dim paths = entries.Select(Function(entry) entry.RelativePath).ToList()
            Dim installedCount = entries.Where(Function(entry) entry.Installed).Count()
            Dim isBackendGroup = category.Equals("Backend", StringComparison.OrdinalIgnoreCase)
            If Not isBackendGroup Then
            Dim batchItem = New UltraDetailListView.ListItem(New UltraDetailListView.ListSubItem() {
                New UltraDetailListView.ListSubItem("本组资源"),
                New UltraDetailListView.ListSubItem(entries.Count & " 个文件"),
                New UltraDetailListView.ListSubItem(installedCount & "/" & entries.Count & " 已存在"),
                New UltraDetailListView.ListSubItem(If(installedCount = entries.Count, "已全部存在", "下载本组"))
            }) With {
                .GroupName = category,
                .Tag = New DownloadListRowTag With {.Category = category, .BatchPaths = paths}
            }
            batchItem.SubItems(0).Font = New Font("Microsoft YaHei UI", 9.2F, FontStyle.Bold)
            batchItem.SubItems(DownloadActionColumn).ForeColor = If(installedCount = entries.Count, UiTextMuted, UiAccent)
            _downloadList.Items.Add(batchItem)
            _downloadGroupItems(category) = batchItem
            End If

            For Each entry In entries
                Dim item = New UltraDetailListView.ListItem(New UltraDetailListView.ListSubItem() {
                    New UltraDetailListView.ListSubItem(entry.Name),
                    New UltraDetailListView.ListSubItem(If(entry.Size > 0, FormatDownloadSize(entry.Size), "-")),
                    New UltraDetailListView.ListSubItem(If(String.IsNullOrWhiteSpace(entry.StatusText), If(entry.Installed, "本地已安装", "未安装"), entry.StatusText)),
                    New UltraDetailListView.ListSubItem(If(String.IsNullOrWhiteSpace(entry.ActionText), If(entry.Installed, "已存在", "下载"), entry.ActionText))
                }) With {
                    .GroupName = category,
                    .Tag = New DownloadListRowTag With {.Entry = entry, .Category = category}
                }
                item.SubItems(2).ForeColor = If(entry.Installed, UiSuccess, If(entry.IsBackend, UiAccent, UiTextMuted))
                item.SubItems(DownloadActionColumn).ForeColor = If(entry.Installed, UiTextMuted, UiAccent)
                _downloadList.Items.Add(item)
                _downloadItemsByPath(entry.RelativePath) = item
            Next
        End Sub

        Private Shared Function FormatDownloadSize(bytes As Long) As String
            If bytes >= 1024L * 1024L * 1024L Then Return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("0.00") & " GB"
            If bytes >= 1024L * 1024L Then Return (bytes / (1024.0 * 1024.0)).ToString("0.0") & " MB"
            If bytes >= 1024L Then Return (bytes / 1024.0).ToString("0.0") & " KB"
            Return bytes & " B"
        End Function

        Private Async Sub OnDownloadListItemClick(sender As Object, e As UltraDetailListView.ListItemEventArgs)
            If e.ColumnIndex <> DownloadActionColumn OrElse e.Item Is Nothing Then Return
            If Not _downloadActionsEnabled OrElse Not _downloadOnline OrElse _downloadsLoading OrElse _archiveCleanupBusy Then Return
            Dim row = TryCast(e.Item.Tag, DownloadListRowTag)
            If row Is Nothing Then Return
            If row.Entry IsNot Nothing Then
                Await DownloadSingleItemAsync(row.Entry)
            ElseIf row.BatchPaths IsNot Nothing Then
                Await DownloadGroupItemsAsync(row.Category, row.BatchPaths)
            End If
        End Sub

        Private Async Sub OnDownloadAllClick(sender As Object, e As EventArgs)
            If Not _downloadActionsEnabled OrElse Not _downloadOnline OrElse _downloadsLoading OrElse
                _archiveCleanupBusy OrElse _downloadActiveCount > 0 Then Return
            ' 插件 EXE 由自动更新流程管理；Backend 使用独立事务更新，均不进入三路并行资源下载。
            Dim paths = _downloadItemsByPath.Keys.
                Where(Function(path) Not path.Equals("Plugin/videoenhancer.exe", StringComparison.OrdinalIgnoreCase) AndAlso
                    Not DownloadCategory(path).Equals("Backend", StringComparison.OrdinalIgnoreCase)).
                ToList()
            If paths.Count = 0 Then
                ShowStatus("请先刷新资源列表。", True)
                Return
            End If
            Await DownloadGroupItemsAsync("全部资源", paths)
        End Sub

        Private Async Function DownloadSingleItemAsync(entry As DownloadModelEntry) As Task
            If entry Is Nothing OrElse entry.Installed Then Return
            If _downloadActiveCount >= MaxParallelDownloads Then
                ShowStatus("当前已有 3 个并行下载，请等待任一文件完成。", True)
                Return
            End If
            Dim exePath = DownloadExecutablePath()
            If String.IsNullOrWhiteSpace(exePath) Then Return
            If entry.IsBackend AndAlso entry.ForceBackendFull Then
                Dim sizeText = If(entry.BackendFullSize > 0, FormatDownloadSize(entry.BackendFullSize), "未知大小")
                Dim message = "完整修复包约 " & sizeText & "，将用干净后端整体替换现有 Backend。" &
                    Environment.NewLine & "旧后端会先移入事务备份；成功后清理，失败时自动恢复。" &
                    Environment.NewLine & Environment.NewLine & "现在下载完整修复包吗？"
                If MessageBox.Show(Me, message, "完整修复 Backend",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then Return
            End If
            Dim relativePath = entry.RelativePath
            If Not TryBeginDownload(relativePath) Then
                ShowStatus("该资源正在下载，请等待当前任务完成。", True)
                Return
            End If
            SetDownloadRowState(relativePath, "下载中", "准备中...", UiAccent, UiAccent)
            Dim result = Await ExecuteDownloadAsync(exePath, relativePath,
                Sub(text)
                    Try
                        BeginInvoke(New Action(Sub() SetDownloadRowState(relativePath, "下载中", text, UiAccent, UiAccent)))
                    Catch
                    End Try
                End Sub, entry.ForceBackendFull)
            If result.ExitCode = 0 Then
                entry.Installed = True
                SetDownloadRowState(relativePath, If(entry.IsBackend, "已更新", "本地已安装"), "已完成", UiSuccess, UiTextMuted)
                ShowStatus(If(entry.IsBackend, "后端更新完成", "模型下载完成：" & relativePath), False)
            ElseIf result.Errors.Contains("NO_NETWORK|") Then
                SetDownloadRowState(relativePath, "网络中断", "重试", UiDanger, UiAccent)
                _downloadOnline = False
                SetDownloadActionsEnabled(False)
                ShowOfflineDownloadStatus()
            ElseIf result.Errors.Contains("AUTH_REQUIRED|") Then
                SetDownloadRowState(relativePath, "需要认证", "重试", UiDanger, UiAccent)
                ShowStatus("私有模型仓库需要有效令牌，请设置 VIDEOENHANCER_MODELSCOPE_TOKEN 或 MODELSCOPE_API_TOKEN 后重启 3FUI。", True)
            ElseIf entry.IsBackend AndAlso result.Errors.Contains("BACKEND_FULL_REQUIRED|") Then
                entry.ForceBackendFull = True
                entry.Size = entry.BackendFullSize
                SetBackendFullRepairState(relativePath, entry.BackendFullSize)
                ShowStatus("增量补丁与本地后端文件不一致，已安全回滚。请点击““下载完整修复包””。", True)
            Else
                SetDownloadRowState(relativePath, "下载失败", "重试", UiDanger, UiAccent)
                ShowStatus(CliErrorMessage(result.Errors, "模型下载失败"), True)
            End If
            RefreshDownloadGroupSummary(DownloadCategory(relativePath))
        End Function

        Private Async Function DownloadGroupItemsAsync(category As String, allPaths As List(Of String)) As Task
            If allPaths Is Nothing OrElse allPaths.Count = 0 OrElse _activeDownloadGroups.Contains(category) Then Return
            If _downloadActiveCount >= MaxParallelDownloads Then
                ShowStatus("当前已有 3 个并行下载，请等待任一文件完成。", True)
                Return
            End If
            Dim paths = allPaths.Where(Function(path)
                Dim item As UltraDetailListView.ListItem = Nothing
                If Not _downloadItemsByPath.TryGetValue(path, item) Then Return False
                Dim row = TryCast(item.Tag, DownloadListRowTag)
                Return row IsNot Nothing AndAlso row.Entry IsNot Nothing AndAlso Not row.Entry.Installed AndAlso
                    Not _activeDownloadPaths.Contains(path)
            End Function).ToList()
            If paths.Count = 0 Then
                RefreshDownloadGroupSummary(category)
                Return
            End If
            Dim exePath = DownloadExecutablePath()
            If String.IsNullOrWhiteSpace(exePath) Then Return
            _activeDownloadGroups.Add(category)
            Dim completed = 0
            Dim nextIndex = 0
            Dim failed = False
            Dim failureMessage = ""
            ' 滑动窗口：始终保持最多 3 个活动下载，任一任务完成就立即补下一个。
            Dim running As New List(Of Task(Of DownloadExecutionResult))()
            Dim runningPaths As New Dictionary(Of Task(Of DownloadExecutionResult), String)()
            Try
                SetDownloadGroupState(category, "0/" & paths.Count & " 已完成", "下载中", UiAccent)
                While nextIndex < paths.Count OrElse running.Count > 0
                    While nextIndex < paths.Count AndAlso _downloadActiveCount < MaxParallelDownloads AndAlso Not failed
                        Dim relativePath = paths(nextIndex)
                        nextIndex += 1
                        If Not TryBeginDownload(relativePath) Then Continue While
                        Dim currentPath = relativePath
                        SetDownloadRowState(currentPath, "下载中", "准备中...", UiAccent, UiAccent)
                        Dim task = ExecuteDownloadAsync(exePath, currentPath,
                            Sub(text)
                                Try
                                    BeginInvoke(New Action(Sub()
                                        SetDownloadRowState(currentPath, "下载中", text, UiAccent, UiAccent)
                                    End Sub))
                                Catch
                                End Try
                            End Sub)
                        running.Add(task)
                        runningPaths(task) = currentPath
                    End While

                    If running.Count = 0 Then Exit While
                    Dim finished = Await Task.WhenAny(running)
                    running.Remove(finished)
                    Dim finishedPath = runningPaths(finished)
                    runningPaths.Remove(finished)
                    Dim result = Await finished
                    If result.ExitCode <> 0 Then
                        failed = True
                        failureMessage = CliErrorMessage(result.Errors, "模型下载失败")
                        SetDownloadRowState(finishedPath,
                            If(result.Errors.Contains("AUTH_REQUIRED|"), "需要认证", "下载失败"),
                            "重试", UiDanger, UiAccent)
                        If result.Errors.Contains("NO_NETWORK|") Then _downloadOnline = False
                    Else
                        completed += 1
                        MarkDownloadInstalled(finishedPath)
                    End If
                    SetDownloadGroupState(category, completed & "/" & paths.Count & " 已完成",
                        If(failed, "等待当前任务", "下载中"), If(failed, UiTextMuted, UiAccent))
                End While
            Finally
                _activeDownloadGroups.Remove(category)
            End Try

            RefreshDownloadGroupSummary(category)
            If Not _downloadOnline Then
                SetDownloadActionsEnabled(False)
                ShowOfflineDownloadStatus()
                Return
            End If
            If failed Then
                SetDownloadGroupState(category, completed & "/" & paths.Count & " 已完成", "继续下载", UiAccent)
                ShowStatus("批量下载过程中有文件失败：" & failureMessage, True)
            Else
                ShowStatus("该分类 " & completed & " 个文件已全部下载完成", False)
            End If
        End Function

        Private Async Function ExecuteDownloadAsync(exePath As String, relativePath As String,
                                                     progress As Action(Of String),
                                                     Optional forceBackendFull As Boolean = False) As Task(Of DownloadExecutionResult)
            Try
                Return Await Task.Run(Function() ExecuteModelDownload(exePath, relativePath, progress, forceBackendFull))
            Finally
                EndDownload(relativePath)
            End Try
        End Function

        Private Function ExecuteModelDownload(exePath As String, relativePath As String, progress As Action(Of String),
                                              Optional forceBackendFull As Boolean = False) As DownloadExecutionResult
            Dim result As New DownloadExecutionResult()
            Dim errors As New StringBuilder()
            Try
                Dim isBackendUpdate = DownloadCategory(relativePath).Equals("Backend", StringComparison.OrdinalIgnoreCase)
                If isBackendUpdate AndAlso Not StopEnvironmentCheck(10000) Then
                    errors.AppendLine("启动环境检查未能及时停止，请稍后重试")
                    result.Errors = errors.ToString()
                    Return result
                End If
                Dim psi As New ProcessStartInfo With {
                    .FileName = exePath, .WorkingDirectory = Path.GetDirectoryName(exePath),
                    .UseShellExecute = False, .RedirectStandardOutput = True,
                    .RedirectStandardError = True, .CreateNoWindow = True,
                    .StandardOutputEncoding = Encoding.UTF8, .StandardErrorEncoding = Encoding.UTF8
                }
                If isBackendUpdate Then
                    psi.ArgumentList.Add("--update-backend")
                    If forceBackendFull Then psi.ArgumentList.Add("--force-backend-full")
                Else
                    psi.ArgumentList.Add("--download-model")
                    psi.ArgumentList.Add(relativePath)
                End If
                Using process As New Process With {.StartInfo = psi}
                    AddHandler process.OutputDataReceived,
                        Sub(s, ev)
                            If ev.Data Is Nothing Then Return
                            If ev.Data.StartsWith("DOWNLOAD_PROGRESS|", StringComparison.Ordinal) Then
                                Dim parts = ev.Data.Split("|"c)
                                If parts.Length > 1 Then progress(parts(1) & "%")
                            ElseIf ev.Data.StartsWith("EXTRACT_COMPLETE|", StringComparison.Ordinal) Then
                                progress("解压完成")
                            ElseIf ev.Data.StartsWith("BACKEND_PATCH_START|", StringComparison.Ordinal) Then
                                progress("下载增量补丁")
                            ElseIf ev.Data.StartsWith("BACKEND_FULL_START|", StringComparison.Ordinal) Then
                                progress("下载完整修复包")
                            ElseIf ev.Data.StartsWith("BACKEND_PATCH_COMPLETE|", StringComparison.Ordinal) Then
                                progress("补丁已应用")
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

        Private Sub SetDownloadRowState(relativePath As String, status As String, action As String,
                                        statusColor As Color, actionColor As Color)
            Dim item As UltraDetailListView.ListItem = Nothing
            If Not _downloadItemsByPath.TryGetValue(relativePath, item) Then Return
            Dim changed = item.SubItems(2).Text <> status OrElse item.SubItems(DownloadActionColumn).Text <> action OrElse
                item.SubItems(2).ForeColor <> statusColor OrElse item.SubItems(DownloadActionColumn).ForeColor <> actionColor
            If Not changed Then Return
            item.SubItems(2).Text = status
            item.SubItems(2).ForeColor = statusColor
            item.SubItems(DownloadActionColumn).Text = action
            item.SubItems(DownloadActionColumn).ForeColor = actionColor
            _downloadList.RefreshItems()
        End Sub

        Private Sub SetBackendFullRepairState(relativePath As String, fullSize As Long)
            Dim item As UltraDetailListView.ListItem = Nothing
            If Not _downloadItemsByPath.TryGetValue(relativePath, item) Then Return
            item.SubItems(1).Text = If(fullSize > 0, FormatDownloadSize(fullSize), "-")
            item.SubItems(2).Text = "增量补丁不适用"
            item.SubItems(2).ForeColor = UiDanger
            item.SubItems(DownloadActionColumn).Text = "下载完整修复包"
            item.SubItems(DownloadActionColumn).ForeColor = UiAccent
            _downloadList.RefreshItems()
        End Sub

        Private Sub SetDownloadGroupState(category As String, status As String, action As String, actionColor As Color)
            Dim item As UltraDetailListView.ListItem = Nothing
            If Not _downloadGroupItems.TryGetValue(category, item) Then Return
            item.SubItems(2).Text = status
            item.SubItems(DownloadActionColumn).Text = action
            item.SubItems(DownloadActionColumn).ForeColor = actionColor
            _downloadList.RefreshItems()
        End Sub

        Private Sub MarkDownloadInstalled(relativePath As String)
            Dim item As UltraDetailListView.ListItem = Nothing
            If Not _downloadItemsByPath.TryGetValue(relativePath, item) Then Return
            Dim row = TryCast(item.Tag, DownloadListRowTag)
            If row IsNot Nothing AndAlso row.Entry IsNot Nothing Then row.Entry.Installed = True
            SetDownloadRowState(relativePath, "本地已安装", "已完成", UiSuccess, UiTextMuted)
        End Sub

        Private Sub RefreshDownloadGroupSummary(category As String)
            Dim item As UltraDetailListView.ListItem = Nothing
            If Not _downloadGroupItems.TryGetValue(category, item) Then Return
            Dim row = TryCast(item.Tag, DownloadListRowTag)
            If row Is Nothing OrElse row.BatchPaths Is Nothing Then Return
            Dim installed = 0
            For Each path In row.BatchPaths
                Dim resourceItem As UltraDetailListView.ListItem = Nothing
                If Not _downloadItemsByPath.TryGetValue(path, resourceItem) Then Continue For
                Dim resourceRow = TryCast(resourceItem.Tag, DownloadListRowTag)
                If resourceRow IsNot Nothing AndAlso resourceRow.Entry IsNot Nothing AndAlso resourceRow.Entry.Installed Then
                    installed += 1
                End If
            Next
            Dim allInstalled = installed = row.BatchPaths.Count
            SetDownloadGroupState(category, installed & "/" & row.BatchPaths.Count & " 已存在",
                If(allInstalled, "已全部存在", "下载本组"), If(allInstalled, UiTextMuted, UiAccent))
        End Sub

        Private Sub SetDownloadActionsEnabled(enabled As Boolean)
            _downloadActionsEnabled = enabled
            For Each item In _downloadList.Items
                Dim row = TryCast(item.Tag, DownloadListRowTag)
                If row Is Nothing Then Continue For
                Dim available = enabled AndAlso _downloadOnline
                If row.Entry IsNot Nothing Then
                    item.SubItems(DownloadActionColumn).ForeColor = If(available AndAlso Not row.Entry.Installed, UiAccent, UiTextMuted)
                ElseIf row.BatchPaths IsNot Nothing Then
                    Dim allInstalled = row.BatchPaths.All(Function(path)
                        Dim resourceItem As UltraDetailListView.ListItem = Nothing
                        If Not _downloadItemsByPath.TryGetValue(path, resourceItem) Then Return False
                        Dim resourceRow = TryCast(resourceItem.Tag, DownloadListRowTag)
                        Return resourceRow IsNot Nothing AndAlso resourceRow.Entry IsNot Nothing AndAlso resourceRow.Entry.Installed
                    End Function)
                    item.SubItems(DownloadActionColumn).ForeColor = If(available AndAlso Not allInstalled, UiAccent, UiTextMuted)
                End If
            Next
            _downloadList.RefreshItems()
            UpdateDownloadUtilityButtons()
        End Sub

        Private Function TryBeginDownload(relativePath As String) As Boolean
            If _downloadActiveCount >= MaxParallelDownloads OrElse _activeDownloadPaths.Contains(relativePath) Then Return False
            _activeDownloadPaths.Add(relativePath)
            _downloadActiveCount += 1
            UpdateDownloadUtilityButtons()
            Return True
        End Function

        Private Sub EndDownload(relativePath As String)
            If _activeDownloadPaths.Remove(relativePath) Then
                _downloadActiveCount = Math.Max(0, _downloadActiveCount - 1)
            End If
            UpdateDownloadUtilityButtons()
        End Sub

        Private Sub UpdateDownloadUtilityButtons()
            _btnRefreshDownloads.Enabled = Not _downloadsLoading AndAlso
                _downloadActiveCount = 0 AndAlso Not _archiveCleanupBusy
            _btnDownloadPluginUpdate.Enabled = _downloadsLoaded AndAlso _downloadActionsEnabled AndAlso
                _downloadOnline AndAlso _downloadActiveCount = 0 AndAlso Not _archiveCleanupBusy
            _btnCleanArchives.Enabled = _downloadActiveCount = 0 AndAlso Not _archiveCleanupBusy
        End Sub

        Private Sub ShowOfflineDownloadStatus()
            Try
                _statusClearTimer.Stop()
            Catch
            End Try
            If _downloadList.Items.Count = 0 Then
                AddDownloadMessage("暂时无法连接模型镜像", "检查网络后刷新资源", UiDanger)
            End If
            _lblStatus.Text = "<font color=#E07878>无法连接 ModelScope，请检查网络或代理设置</font>"
            SetDownloadActionsEnabled(False)
            UpdateDownloadUtilityButtons()
        End Sub

        Private Async Sub OnCleanDownloadArchives(sender As Object, e As EventArgs)
            If _archiveCleanupBusy OrElse _downloadActiveCount > 0 Then
                ShowStatus("请等待当前模型下载完成后再清理压缩包。", True)
                Return
            End If
            If Not File.Exists(_config.ExePath) Then
                ShowStatus("请先指定有效的 videoenhancer.exe", True)
                Return
            End If
            _archiveCleanupBusy = True
            SetDownloadActionsEnabled(False)
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
            _archiveCleanupBusy = False
            SetDownloadActionsEnabled(True)
            UpdateDownloadUtilityButtons()
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
                "模型转换", "超分权重与 RIFE 补帧权重使用各自的 TensorRT 构建流程"), 0, 0)

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
            _btnPickPth.Text = "选择或拖入权重"
            _btnPickPth.Dock = DockStyle.Fill
            _btnPickPth.Margin = New Padding(0, 8, 0, 8)
            ConfigureSecondaryButton(_btnPickPth)
            AddHandler _btnPickPth.Click, AddressOf OnPickPthClick
            _lblConvertInput.Text = "<font color=#888888>支持 .pth / .pt / .pkl</font>"
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
                .Text = "<font color=#DCDCDC><b>PyTorch 权重 → TensorRT Engine</b></font><br/>" &
                        "<font color=#888888>超分 .pth 归档到 TensorRT-Personalized；RIFE 按权重结构识别并构建 flow/encode 缓存。</font><br/>" &
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

        Private Sub BuildOfficialImporterPage()
            _pageImporter.Dock = DockStyle.Fill
            _pageImporter.BackColor = Color.Transparent
            _pageImporter.Padding = New Padding(0, 8, 0, 0)
            _pageImporter.AllowDrop = True
            AddHandler _pageImporter.DragEnter, AddressOf OnModelImportDragEnter
            AddHandler _pageImporter.DragDrop, AddressOf OnModelImportDragDrop

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 5,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty,
                .AllowDrop = True
            }
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 54.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 68.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 70.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 72.0F))
            AddHandler root.DragEnter, AddressOf OnModelImportDragEnter
            AddHandler root.DragDrop, AddressOf OnModelImportDragDrop
            root.Controls.Add(CreateOfficialSectionHeading(
                "模型导入", "安全预检架构、用途、倍率与后端能力，通过后安装到 models\User"), 0, 0)

            Dim sourceRow As New Panel With {
                .Dock = DockStyle.Fill,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = New Padding(0, 9, 0, 9)
            }
            _btnPickImportFile.Text = "选择模型或压缩包"
            _btnPickImportFile.Dock = DockStyle.Left
            _btnPickImportFile.Width = 180
            ConfigureOfficialImportButton(_btnPickImportFile)
            AddHandler _btnPickImportFile.Click, AddressOf OnPickImportFile
            _btnPickImportFolder.Text = "选择模型文件夹"
            _btnPickImportFolder.Dock = DockStyle.Left
            _btnPickImportFolder.Width = 180
            ConfigureOfficialImportButton(_btnPickImportFolder)
            AddHandler _btnPickImportFolder.Click, AddressOf OnPickImportFolder
            _lblImportSource.Text = "<font color=#888888>尚未选择；也可以拖入文件、文件夹或压缩包</font>"
            _lblImportSource.AutoSize = False
            _lblImportSource.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            Dim sourceValueBox = CreateOfficialValueBox(_lblImportSource)
            sourceValueBox.Dock = DockStyle.Fill
            Dim sourceGap1 As New JustEmptyControl With {.Dock = DockStyle.Left, .Width = 10, .BackColor = Color.Transparent}
            Dim sourceGap2 As New JustEmptyControl With {.Dock = DockStyle.Left, .Width = 12, .BackColor = Color.Transparent}
            ' 按 3FUI Designer 的 Dock 顺序：Fill 先加，其他控件从右向左加入。
            sourceRow.Controls.Add(sourceValueBox)
            sourceRow.Controls.Add(sourceGap2)
            sourceRow.Controls.Add(_btnPickImportFolder)
            sourceRow.Controls.Add(sourceGap1)
            sourceRow.Controls.Add(_btnPickImportFile)
            root.Controls.Add(sourceRow, 0, 1)

            Dim formats As New HtmlColorLabel With {
                .Dock = DockStyle.Fill,
                .Margin = New Padding(0, 8, 0, 4),
                .Padding = New Padding(14, 0, 14, 0),
                .BackColor1 = UiSurface,
                .BorderSize = 0,
                .BorderRadius = 10,
                .AutoSize = False,
                .LineSpacing = 5,
                .TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft,
                .Text = "<font color=#DCDCDC><b>支持格式</b></font>　" &
                        "<font color=#A8A8A8>PTH / PT / CKPT / SAFETENSORS / ONNX / NCNN PARAM+BIN / ZIP / 7Z / RAR</font><br/>" &
                        "<font color=#888888>补帧仅接受能识别为 RIFE、GMFSS 或 GIMM 的权重；TensorRT 能力按实际架构过滤。</font>"
            }
            root.Controls.Add(formats, 0, 2)

            ConfigureImportModelList()
            root.Controls.Add(_importModelList, 0, 3)

            Dim actionRow As New Panel With {
                .Dock = DockStyle.Fill,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = New Padding(0, 9, 0, 9)
            }
            _btnImportModel.Dock = DockStyle.Right
            _btnImportModel.Width = 210
            _btnImportModel.Text = "预检并导入模型"
            ConfigureOfficialImportButton(_btnImportModel, UiSuccess)
            AddHandler _btnImportModel.Click, AddressOf OnImportModelClick
            _lblImportStatus.Text = "<font color=#888888>等待选择模型…</font>"
            _lblImportStatus.AutoSize = False
            _lblImportStatus.Dock = DockStyle.Fill
            _lblImportStatus.Margin = Padding.Empty
            _lblImportStatus.Padding = New Padding(0, 0, 18, 0)
            _lblImportStatus.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            actionRow.Controls.Add(_lblImportStatus)
            actionRow.Controls.Add(_btnImportModel)
            root.Controls.Add(actionRow, 0, 4)
            _pageImporter.Controls.Add(root)
        End Sub

        Private Shared Sub ConfigureOfficialImportButton(button As ModernButton, Optional textColor As Color = Nothing)
            ' 严格对齐 3FUI 官方 Designer 的 ModernButton 用法，不叠加渐变或子控件。
            button.AnimationDuration = 0
            button.BackColor = Color.Transparent
            button.BackColor1 = Color.FromArgb(40, 220, 220, 220)
            button.BackColor2 = Color.Transparent
            button.HoverBackColor1 = Color.FromArgb(60, 220, 220, 220)
            button.HoverBackColor2 = Color.Transparent
            button.PressedBackColor1 = Color.FromArgb(80, 220, 220, 220)
            button.PressedBackColor2 = Color.Transparent
            button.BorderRadius = 10
            button.BorderSize = 0
            button.Margin = New Padding(2)
            button.Padding = Padding.Empty
            button.Icon = Nothing
            button.SubText = ""
            button.TextAlign = ModernButton.TextAlignEnum.Center
            button.ForeColor = If(textColor = Nothing, UiText, textColor)
        End Sub

        Private Sub ConfigureImportModelList()
            If _importModelListConfigured Then Return
            _importModelListConfigured = True
            _importModelList.Dock = DockStyle.Fill
            _importModelList.Margin = New Padding(0, 10, 0, 6)
            _importModelList.AutoScroll = False
            _importModelList.Font = New Font("Microsoft YaHei UI", 9.0F)
            _importModelList.BackColor = Color.Transparent
            _importModelList.BackgroundColor = Color.Transparent
            _importModelList.BackgroundSource = ModernPanel1
            _importModelList.BorderColor = UiStrokeSoft
            _importModelList.BorderSize = 1
            _importModelList.BorderRadius = 8
            _importModelList.HeaderVisible = True
            _importModelList.HeaderHeight = 38
            _importModelList.HeaderBackColor = Color.FromArgb(36, 36, 36)
            _importModelList.HeaderForeColor = UiTextSecondary
            _importModelList.HeaderBorderColor = Color.FromArgb(52, 52, 52)
            _importModelList.HeaderBorderWidth = 1
            _importModelList.AllowColumnResize = True
            _importModelList.MultiSelect = False
            _importModelList.AllowDragReorder = False
            _importModelList.ItemForeColor = UiTextSecondary
            _importModelList.ItemHoverBackColor = Color.FromArgb(48, 255, 255, 255)
            _importModelList.ItemSelectedBackColor = Color.FromArgb(54, 71, 156, 255)
            _importModelList.ItemCornerRadius = 4
            _importModelList.ItemPadding = New Padding(12, 8, 10, 8)
            _importModelList.ItemSpacing = 2
            _importModelList.ContentPadding = New Padding(0, 4, 0, 4)
            _importModelList.ScrollBarWidth = 10
            _importModelList.ScrollBarTrackColor = Color.FromArgb(18, 18, 18)
            _importModelList.ScrollBarThumbColor = Color.FromArgb(72, 72, 72)
            _importModelList.ScrollBarThumbHoverColor = Color.FromArgb(104, 104, 104)
            _importModelList.Columns.AddRange(New UltraDetailListView.ListColumn() {
                New UltraDetailListView.ListColumn("用户模型（双击修正能力）", 300),
                New UltraDetailListView.ListColumn("架构", 150),
                New UltraDetailListView.ListColumn("用途", 110),
                New UltraDetailListView.ListColumn("倍率", 70),
                New UltraDetailListView.ListColumn("后端", 210),
                New UltraDetailListView.ListColumn("格式", 100)
            })
            AddHandler _importModelList.ItemDoubleClick, AddressOf OnImportModelDoubleClick
            AddHandler _importModelList.ClientSizeChanged,
                Sub(sender, e)
                    If _importModelList.Columns.Count = 0 Then Return
                    Dim nameWidth = Math.Max(210, _importModelList.ClientSize.Width - 10 - 150 - 110 - 70 - 210 - 100)
                    If _importModelList.Columns(0).Width <> nameWidth Then
                        _importModelList.Columns(0).Width = nameWidth
                        _importModelList.RefreshItems()
                    End If
                End Sub
        End Sub

        Private Async Sub LoadUserModels()
            If _userModelsLoading Then Return
            Dim exePath = PluginConfig.ResolveInstalledExePath(_config.ExePath)
            _importModelList.Items.Clear()
            If String.IsNullOrWhiteSpace(exePath) OrElse Not File.Exists(exePath) Then
                AddImportModelMessage("找不到 videoenhancer.exe，请先在超分工作台指定处理程序")
                Return
            End If
            _userModelsLoading = True
            AddImportModelMessage("正在读取用户模型能力清单…")
            Try
                Dim models = Await Task.Run(Function() RunUserModelList(exePath))
                _importModelList.Items.Clear()
                If models.Count = 0 Then
                    AddImportModelMessage("尚未导入用户模型；可从上方选择文件、文件夹或压缩包")
                    Return
                End If
                For Each model In models
                    Dim item = New UltraDetailListView.ListItem(New UltraDetailListView.ListSubItem() {
                        New UltraDetailListView.ListSubItem(model.DisplayName),
                        New UltraDetailListView.ListSubItem(model.Architecture),
                        New UltraDetailListView.ListSubItem(DisplayUserModelPurpose(model)),
                        New UltraDetailListView.ListSubItem(If(model.Scale > 0, model.Scale.ToString() & "x", "-")),
                        New UltraDetailListView.ListSubItem(String.Join(" / ", model.Backends)),
                        New UltraDetailListView.ListSubItem(model.Format.ToUpperInvariant())
                    }) With {.Tag = model}
                    item.SubItems(0).ForeColor = UiText
                    item.SubItems(4).ForeColor = UiAccent
                    _importModelList.Items.Add(item)
                Next
            Catch ex As Exception
                _importModelList.Items.Clear()
                AddImportModelMessage("能力清单读取失败：" & ex.Message)
            Finally
                _userModelsLoading = False
            End Try
        End Sub

        Private Sub AddImportModelMessage(message As String)
            _importModelList.Items.Add(New UltraDetailListView.ListItem(New UltraDetailListView.ListSubItem() {
                New UltraDetailListView.ListSubItem(message, Nothing, UiTextMuted),
                New UltraDetailListView.ListSubItem(""), New UltraDetailListView.ListSubItem(""),
                New UltraDetailListView.ListSubItem(""), New UltraDetailListView.ListSubItem(""),
                New UltraDetailListView.ListSubItem("")
            }))
        End Sub

        Private Shared Function DisplayUserModelPurpose(model As UserModelItem) As String
            Select Case model.Task.ToLowerInvariant()
                Case "interpolation" : Return "补帧"
                Case "restoration" : Return "修复"
                Case Else : Return If(model.Purpose.Equals("Restoration", StringComparison.OrdinalIgnoreCase), "修复", "超分")
            End Select
        End Function

        Private Sub OnImportModelDoubleClick(sender As Object, e As UltraDetailListView.ListItemEventArgs)
            If e.Item Is Nothing Then Return
            Dim model = TryCast(e.Item.Tag, UserModelItem)
            If model Is Nothing Then Return
            ShowUserModelCapabilityEditor(model)
        End Sub

        Private Sub ShowUserModelCapabilityEditor(model As UserModelItem)
            Using dialog As New Form With {
                .Text = "修正模型能力 - " & model.DisplayName,
                .StartPosition = FormStartPosition.CenterParent,
                .FormBorderStyle = FormBorderStyle.FixedDialog,
                .MaximizeBox = False,
                .MinimizeBox = False,
                .ShowInTaskbar = False,
                .BackColor = Color.FromArgb(24, 24, 24),
                .ForeColor = UiText,
                .ClientSize = New Size(720, 570),
                .Font = New Font("Microsoft YaHei UI", 9.0F)
            }
                Dim grid As New TableLayoutPanel With {
                    .Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 11,
                    .Padding = New Padding(20, 16, 20, 16), .BackColor = Color.Transparent
                }
                grid.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 150.0F))
                grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
                For row = 0 To 8
                    grid.RowStyles.Add(New RowStyle(SizeType.Absolute, If(row = 8, 96.0F, 42.0F)))
                Next
                grid.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
                grid.RowStyles.Add(New RowStyle(SizeType.Absolute, 52.0F))

                Dim addCaption As Action(Of String, Integer) =
                    Sub(text, row)
                        grid.Controls.Add(New Label With {
                            .Text = text, .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft,
                            .ForeColor = UiTextSecondary, .BackColor = Color.Transparent
                        }, 0, row)
                    End Sub
                Dim readonlyValue As Func(Of String, Label) =
                    Function(text) New Label With {
                        .Text = text, .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft,
                        .ForeColor = UiTextMuted, .BackColor = Color.Transparent, .AutoEllipsis = True
                    }

                Dim architectureBox As New ModernTextBox With {.Text = model.Architecture, .Dock = DockStyle.Fill, .Margin = New Padding(0, 5, 0, 5)}
                Dim purposeBox As New ModernTextBox With {.Text = model.Purpose, .Dock = DockStyle.Fill, .Margin = New Padding(0, 5, 0, 5)}
                Dim scaleBox As New NumericUpDown With {
                    .Minimum = 1D, .Maximum = 16D, .Value = Math.Max(1, Math.Min(16, model.Scale)),
                    .Dock = DockStyle.Left, .Width = 150, .BackColor = Color.FromArgb(38, 38, 38), .ForeColor = UiText
                }
                Dim multipleBox As New NumericUpDown With {
                    .Minimum = 1D, .Maximum = 1024D, .Value = Math.Max(1, Math.Min(1024, model.InputMultiple)),
                    .Dock = DockStyle.Left, .Width = 150, .BackColor = Color.FromArgb(38, 38, 38), .ForeColor = UiText
                }
                If model.Task.Equals("interpolation", StringComparison.OrdinalIgnoreCase) OrElse
                    model.Task.Equals("restoration", StringComparison.OrdinalIgnoreCase) Then scaleBox.Enabled = False
                Dim backendList As New CheckedListBox With {
                    .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(31, 31, 31), .ForeColor = UiText,
                    .BorderStyle = BorderStyle.FixedSingle, .CheckOnClick = True, .MultiColumn = True,
                    .ColumnWidth = 150, .IntegralHeight = False
                }
                Dim backendNames = New String() {"ncnn", "cuda", "tensorrt", "onnx", "flashvsr", "basicvsrpp"}
                backendList.Items.AddRange(backendNames)
                For index = 0 To backendNames.Length - 1
                    backendList.SetItemChecked(index, model.Backends.Contains(backendNames(index), StringComparer.OrdinalIgnoreCase))
                Next

                addCaption("模型文件", 0) : grid.Controls.Add(readonlyValue(model.RelativePath), 1, 0)
                addCaption("格式 / SHA-256", 1) : grid.Controls.Add(readonlyValue(model.Format.ToUpperInvariant() & "  ·  " & model.Sha256), 1, 1)
                addCaption("任务类别（只读）", 2) : grid.Controls.Add(readonlyValue(DisplayUserModelPurpose(model) & "  [" & model.Task & "]"), 1, 2)
                addCaption("架构", 3) : grid.Controls.Add(architectureBox, 1, 3)
                addCaption("用途", 4) : grid.Controls.Add(purposeBox, 1, 4)
                addCaption("模型倍率", 5) : grid.Controls.Add(scaleBox, 1, 5)
                addCaption("输入尺寸倍数", 6) : grid.Controls.Add(multipleBox, 1, 6)
                Dim sizeRequirement = If(model.MinimumSize > 0, "最小 " & model.MinimumSize.ToString() & " px", "无额外最小值") &
                    If(model.Square, "；要求正方形", "") & If(String.IsNullOrWhiteSpace(model.Tiling), "", "；切片 " & model.Tiling)
                addCaption("其他尺寸要求（只读）", 7) : grid.Controls.Add(readonlyValue(sizeRequirement), 1, 7)
                addCaption("可用后端", 8) : grid.Controls.Add(backendList, 1, 8)

                Dim hint = readonlyValue("保存前会校验模型格式、架构和后端组合；错误组合不会写入能力清单。")
                hint.ForeColor = UiTextMuted
                grid.Controls.Add(hint, 0, 9)
                grid.SetColumnSpan(hint, 2)
                Dim buttons As New FlowLayoutPanel With {
                    .Dock = DockStyle.Fill, .FlowDirection = FlowDirection.RightToLeft,
                    .BackColor = Color.Transparent, .WrapContents = False
                }
                Dim saveButton As New ModernButton With {.Text = "保存修正", .Size = New Size(130, 40)}
                Dim cancelButton As New ModernButton With {.Text = "取消", .Size = New Size(100, 40)}
                ConfigurePrimaryButton(saveButton)
                ConfigureSecondaryButton(cancelButton)
                AddHandler cancelButton.Click, Sub(sender, args) dialog.Close()
                AddHandler saveButton.Click,
                    Sub(sender, args)
                        Dim selectedBackends = backendList.CheckedItems.Cast(Of Object)().Select(Function(value) value.ToString()).ToArray()
                        Dim errorText = UpdateUserModelCapabilities(model.Id, architectureBox.Text, purposeBox.Text,
                            Decimal.ToInt32(scaleBox.Value), Decimal.ToInt32(multipleBox.Value), selectedBackends)
                        If errorText.Length > 0 Then
                            MessageBox.Show(dialog, errorText, "能力修正失败", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                            Return
                        End If
                        dialog.DialogResult = DialogResult.OK
                        dialog.Close()
                    End Sub
                buttons.Controls.Add(saveButton)
                buttons.Controls.Add(cancelButton)
                grid.Controls.Add(buttons, 0, 10)
                grid.SetColumnSpan(buttons, 2)
                dialog.Controls.Add(grid)
                If dialog.ShowDialog(Me) = DialogResult.OK Then
                    LoadUserModels()
                    RefreshModels()
                    _lblImportStatus.Text = "<font color=#3FCD87>已保存能力修正，并刷新工作台模型列表</font>"
                End If
            End Using
        End Sub

        Private Function UpdateUserModelCapabilities(id As String, architecture As String, purpose As String,
                                                     scale As Integer, inputMultiple As Integer,
                                                     backends As String()) As String
            Dim exePath = PluginConfig.ResolveInstalledExePath(_config.ExePath)
            If String.IsNullOrWhiteSpace(exePath) OrElse Not File.Exists(exePath) Then Return "找不到 videoenhancer.exe"
            Try
                Dim psi As New ProcessStartInfo With {
                    .FileName = exePath, .UseShellExecute = False, .RedirectStandardOutput = True,
                    .RedirectStandardError = True, .CreateNoWindow = True,
                    .StandardOutputEncoding = Encoding.UTF8, .StandardErrorEncoding = Encoding.UTF8
                }
                Dim arguments = New String() {"--json", "--update-user-model", id, "--user-architecture", architecture,
                    "--user-purpose", purpose, "--user-scale", scale.ToString(), "--user-input-multiple",
                    inputMultiple.ToString(), "--user-backends", String.Join(",", backends)}
                For Each argument In arguments : psi.ArgumentList.Add(argument) : Next
                Using child = Diagnostics.Process.Start(psi)
                    If child Is Nothing Then Return "无法启动能力清单更新进程"
                    Dim stdout = child.StandardOutput.ReadToEnd()
                    Dim stderr = child.StandardError.ReadToEnd()
                    child.WaitForExit(30000)
                    If child.ExitCode <> 0 Then Return LastNonEmptyLine(If(String.IsNullOrWhiteSpace(stderr), stdout, stderr))
                End Using
                Return ""
            Catch ex As Exception
                Return ex.Message
            End Try
        End Function

        Private Sub OnPickImportFile(sender As Object, e As EventArgs)
            If _modelImportBusy Then Return
            Using dialog As New OpenFileDialog With {
                .Title = "选择要预检并导入的模型",
                .Filter = "支持的模型|*.pth;*.pt;*.pkl;*.ckpt;*.safetensors;*.onnx;*.param;*.bin;*.zip;*.7z;*.rar;*.tar;*.gz;*.xz;*.zst|所有文件|*.*",
                .CheckFileExists = True,
                .Multiselect = False
            }
                If dialog.ShowDialog(Me) = DialogResult.OK Then SetImportSource(dialog.FileName)
            End Using
        End Sub

        Private Sub OnPickImportFolder(sender As Object, e As EventArgs)
            If _modelImportBusy Then Return
            Using dialog As New FolderBrowserDialog With {.Description = "选择模型文件夹或 NCNN param/bin 目录"}
                If dialog.ShowDialog(Me) = DialogResult.OK Then SetImportSource(dialog.SelectedPath)
            End Using
        End Sub

        Private Sub OnModelImportDragEnter(sender As Object, e As DragEventArgs)
            If e.Data IsNot Nothing AndAlso e.Data.GetDataPresent(DataFormats.FileDrop) Then
                e.Effect = DragDropEffects.Copy
            Else
                e.Effect = DragDropEffects.None
            End If
        End Sub

        Private Sub OnModelImportDragDrop(sender As Object, e As DragEventArgs)
            If _modelImportBusy Then Return
            Dim paths = TryCast(If(e.Data Is Nothing, Nothing, e.Data.GetData(DataFormats.FileDrop)), String())
            If paths IsNot Nothing AndAlso paths.Length > 0 Then SetImportSource(paths(0))
        End Sub

        Private Sub SetImportSource(path As String)
            _importSourcePath = If(path, "").Trim()
            _lblImportSource.Text = "<font color=#D0D0D0>" & EscapeHtml(_importSourcePath) & "</font>"
            _lblImportStatus.Text = "<font color=#888888>准备进行元数据与兼容性预检</font>"
        End Sub

        Private Async Sub OnImportModelClick(sender As Object, e As EventArgs)
            If _modelImportBusy Then Return
            If String.IsNullOrWhiteSpace(_importSourcePath) Then
                _lblImportStatus.Text = "<font color=#E0A45C>请先选择要导入的模型、文件夹或压缩包</font>"
                Return
            End If
            If Not File.Exists(_config.ExePath) Then
                ShowStatus("请先指定 videoenhancer.exe", True)
                Return
            End If
            _modelImportBusy = True
            _btnImportModel.Text = "正在预检并导入…"
            _lblImportStatus.Text = "<font color=#479CFF>正在安全读取模型元数据并验证能力…</font>"
            Try
                Dim psi As New ProcessStartInfo With {
                    .FileName = _config.ExePath,
                    .UseShellExecute = False,
                    .RedirectStandardOutput = True,
                    .RedirectStandardError = True,
                    .CreateNoWindow = True,
                    .StandardOutputEncoding = Encoding.UTF8,
                    .StandardErrorEncoding = Encoding.UTF8
                }
                psi.ArgumentList.Add("--json")
                psi.ArgumentList.Add("--import-model")
                psi.ArgumentList.Add(_importSourcePath)
                Using child = Diagnostics.Process.Start(psi)
                    If child Is Nothing Then Throw New InvalidOperationException("无法启动模型导入进程")
                    Dim stdoutTask As Task(Of String) = child.StandardOutput.ReadToEndAsync()
                    Dim stderrTask As Task(Of String) = child.StandardError.ReadToEndAsync()
                    Await child.WaitForExitAsync()
                    Dim stdout = Await stdoutTask
                    Dim stderr = Await stderrTask
                    Dim jsonLine = stdout.Replace(Convert.ToChar(13).ToString(), "").
                        Split(New Char() {Convert.ToChar(10)}, StringSplitOptions.RemoveEmptyEntries).
                        LastOrDefault(Function(line) line.Trim().StartsWith("["c))
                    Dim results As List(Of ModelImportResponse) = Nothing
                    If Not String.IsNullOrWhiteSpace(jsonLine) Then
                        results = JsonSerializer.Deserialize(Of List(Of ModelImportResponse))(jsonLine.Trim(),
                            New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True})
                    End If
                    Dim resultItems = If(results, New List(Of ModelImportResponse)())
                    Dim succeeded = resultItems.Where(Function(item) item.Success).Count()
                    Dim failed = resultItems.Where(Function(item) Not item.Success).Count()
                    If succeeded > 0 Then
                        RefreshModels()
                        LoadUserModels()
                        Dim first = results.First(Function(item) item.Success)
                        _lblImportStatus.Text = "<font color=#3FCD87>已导入 " & succeeded.ToString() & " 个模型：" &
                            EscapeHtml(first.Architecture) & "，可用后端 " & EscapeHtml(String.Join(" / ", first.Backends)) & "</font>"
                        ShowStatus("模型已安装到 models\User，并刷新工作台模型列表", False)
                    End If
                    If failed > 0 OrElse succeeded = 0 Then
                        Dim failure = If(results, New List(Of ModelImportResponse)()).FirstOrDefault(Function(item) Not item.Success)
                        Dim message = If(failure IsNot Nothing, failure.Error, LastNonEmptyLine(stderr))
                        _lblImportStatus.Text = "<font color=#EB5D5D>预检未通过：" & EscapeHtml(message) & "</font>"
                        ShowStatus("模型导入失败：" & message, True)
                    End If
                End Using
            Catch ex As Exception
                _lblImportStatus.Text = "<font color=#EB5D5D>导入失败：" & EscapeHtml(ex.Message) & "</font>"
                ShowStatus("模型导入失败：" & ex.Message, True)
            Finally
                _modelImportBusy = False
                _btnImportModel.Text = "预检并导入模型"
            End Try
        End Sub

        Private Sub BuildMarkdownPage(page As Panel, markdown As String)
            page.Dock = DockStyle.Fill
            page.BackColor = Color.Transparent
            page.Padding = New Padding(0, 8, 0, 0)
            ' WebBrowser 初始化会加载系统浏览器引擎，延迟到用户首次打开对应选项卡，
            ' 避免两个教程页阻塞插件首屏布局。
            _markdownSources(page) = If(markdown, "")
        End Sub

        Private Sub EnsureMarkdownPage(page As Panel)
            If page Is Nothing OrElse _markdownReady.Contains(page) Then Return
            Dim markdown As String = ""
            If Not _markdownSources.TryGetValue(page, markdown) Then Return
            Dim browser As New WebBrowser With {
                .Dock = DockStyle.Fill, .AllowWebBrowserDrop = False,
                .IsWebBrowserContextMenuEnabled = False, .WebBrowserShortcutsEnabled = False,
                .ScriptErrorsSuppressed = True, .ScrollBarsEnabled = True
            }
            browser.DocumentText = MarkdownDocument(markdown)
            page.Controls.Add(browser)
            _markdownReady.Add(page)
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
                    IsPyTorchWeightExtension(paths(0)) Then
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
                .Title = "选择超分或补帧 PyTorch 权重",
                .Filter = "PyTorch 权重 (*.pth;*.pt;*.pkl)|*.pth;*.pt;*.pkl|所有文件 (*.*)|*.*",
                .CheckFileExists = True,
                .Multiselect = False
            }
                If dialog.ShowDialog(Me) = DialogResult.OK Then
                    SelectConverterInput(dialog.FileName)
                End If
            End Using
        End Sub

        Private Async Sub SelectConverterInput(modelPath As String)
            If Not File.Exists(modelPath) OrElse Not IsPyTorchWeightExtension(modelPath) Then
                SetConverterStatus("只支持有效的 .pth、.pt 或 .pkl 权重文件。", True)
                Return
            End If
            _convertInputPath = Path.GetFullPath(modelPath)
            _convertIsInterpolation = False
            _convertArchitecture = ""
            _btnConvert.Enabled = False
            SetConverterStatus("正在读取权重结构并识别模型架构…", False)

            Dim inspection = Await Task.Run(Function() InspectConverterModel(_convertInputPath))
            If Not String.Equals(_convertInputPath, Path.GetFullPath(modelPath), StringComparison.OrdinalIgnoreCase) Then Return
            If inspection.Item1 Then
                _convertIsInterpolation = True
                _convertArchitecture = inspection.Item2
            ElseIf Not String.IsNullOrWhiteSpace(inspection.Item3) AndAlso
                Not String.Equals(Path.GetExtension(_convertInputPath), ".pth", StringComparison.OrdinalIgnoreCase) Then
                SetConverterStatus(inspection.Item3, True)
                Return
            End If

            Dim outputDir = If(_convertIsInterpolation,
                               Path.GetDirectoryName(_convertInputPath),
                               GetPersonalizedTensorRtDirectory())
            _lblConvertInput.Text = "<font color=#DCDCDC>" & EscapeHtml(_convertInputPath) & "</font>"
            _lblConvertOutput.Text = "<font color=#DCDCDC>" & EscapeHtml(outputDir) & "</font>"
            _btnConvert.Enabled = Not _conversionRunning
            If _convertIsInterpolation Then
                _btnConvert.Text = "预构建 1080p RIFE Engine"
                SetConverterStatus("已识别 " & _convertArchitecture & "；将使用 RVE flow/encode 专用构建流程。", False)
            Else
                _btnConvert.Text = "开始转换  →"
                SetConverterStatus("超分模型已就绪，点击「开始转换」。", False)
            End If
        End Sub

        Private Shared Function IsPyTorchWeightExtension(filePath As String) As Boolean
            Dim extension = Path.GetExtension(filePath)
            Return String.Equals(extension, ".pth", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(extension, ".pt", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(extension, ".pkl", StringComparison.OrdinalIgnoreCase)
        End Function

        Private Async Sub OnConvertModelClick(sender As Object, e As EventArgs)
            If _conversionRunning OrElse Not File.Exists(_convertInputPath) Then Return
            Dim coreRoot = ResolveCoreRoot()
            Dim pythonExe = Path.Combine(coreRoot, "python", "python", "python.exe")
            Dim converter = Path.Combine(coreRoot, "python", "backend", "convert_tensorrt.py")
            Dim outputDir = GetPersonalizedTensorRtDirectory()
            Dim rifePrepare = Path.Combine(coreRoot, "python", "backend", "prepare_rife_tensorrt.py")
            If Not File.Exists(pythonExe) OrElse
               (Not _convertIsInterpolation AndAlso Not File.Exists(converter)) OrElse
               (_convertIsInterpolation AndAlso Not File.Exists(rifePrepare)) Then
                SetConverterStatus("找不到便携 Python 或所需的 TensorRT 构建脚本，请检查 videoenhancer.exe 的 core-path。", True)
                Return
            End If

            If Not _convertIsInterpolation Then Directory.CreateDirectory(outputDir)
            _conversionRunning = True
            _btnConvert.Enabled = False
            _btnPickPth.Enabled = False
            SetConverterStatus("正在离线编译 TensorRT Engine；复杂模型可能需要数分钟，请勿关闭程序…", False)
            Try
                Dim progress = Sub(line As String)
                                   Dim match = Regex.Match(line.Trim(), "^VIDEOENHANCER_TRT_PROGRESS\|[^|]+\|(\d+)\|?(.*)$", RegexOptions.IgnoreCase)
                                   If Not match.Success Then Return
                                   Dim percent As Integer
                                   If Not Integer.TryParse(match.Groups(1).Value, percent) Then Return
                                   percent = Math.Max(0, Math.Min(100, percent))
                                   Dim detail = match.Groups(2).Value.Trim()
                                   Dim statusText = "构建 TensorRT Engine " & percent.ToString() & "%" & If(String.IsNullOrWhiteSpace(detail), "", "：" & detail)
                                   Try
                                       If Not IsDisposed AndAlso IsHandleCreated Then
                                           BeginInvoke(New Action(Sub() SetConverterStatus(statusText, False)))
                                       End If
                                   Catch ex As InvalidOperationException
                                   End Try
                               End Sub
                Dim result = Await Task.Run(
                    Function()
                        If _convertIsInterpolation Then
                            Return RunRifeTensorRtPrepare(pythonExe, rifePrepare, _convertInputPath, 1920, 1080, progress)
                        End If
                        Return RunTensorRtConversion(pythonExe, converter, _convertInputPath, outputDir, progress)
                    End Function)
                If result.Item1 = 0 Then
                    Dim enginePath = LastEnginePath(result.Item2)
                    If _convertIsInterpolation Then
                        SetConverterStatus("RIFE Engine 已就绪；实际任务分辨率不同时会自动构建对应缓存。", False)
                        If _config.InterpBackend = "tensorrt" Then RefreshInterpModels()
                    Else
                        SetConverterStatus("转换完成：" & If(String.IsNullOrWhiteSpace(enginePath), outputDir, enginePath), False)
                        If _config.Backend = "tensorrt" Then RefreshUpscaleModels()
                    End If
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

        Private Shared Function RunTensorRtConversion(pythonExe As String, converter As String, inputPath As String, outputDir As String, progress As Action(Of String)) As Tuple(Of Integer, String)
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
                Dim output As New StringBuilder()
                Dim outputHandler As DataReceivedEventHandler = Sub(sender, e)
                                                                      If String.IsNullOrWhiteSpace(e.Data) Then Return
                                                                      SyncLock output
                                                                          output.AppendLine(e.Data)
                                                                      End SyncLock
                                                                      progress?.Invoke(e.Data)
                                                                  End Sub
                Dim errorHandler As DataReceivedEventHandler = Sub(sender, e)
                                                                     If String.IsNullOrWhiteSpace(e.Data) Then Return
                                                                     SyncLock output
                                                                         output.AppendLine(e.Data)
                                                                     End SyncLock
                                                                 End Sub
                AddHandler child.OutputDataReceived, outputHandler
                AddHandler child.ErrorDataReceived, errorHandler
                child.BeginOutputReadLine()
                child.BeginErrorReadLine()
                child.WaitForExit()
                ' 第二次等待确保异步事件已把尾部输出写入结果。
                child.WaitForExit()
                RemoveHandler child.OutputDataReceived, outputHandler
                RemoveHandler child.ErrorDataReceived, errorHandler
                Return New Tuple(Of Integer, String)(child.ExitCode, output.ToString())
            End Using
        End Function

        Private Function ResolveCoreRoot() As String
            Dim resolvedExe = PluginConfig.ResolveInstalledExePath(_config.ExePath)
            Dim exeDir = If(String.IsNullOrWhiteSpace(resolvedExe), AppDomain.CurrentDomain.BaseDirectory,
                Path.GetDirectoryName(resolvedExe))
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

        Private Shared Function LastEnginePath(text As String) As String
            If Not String.IsNullOrWhiteSpace(text) Then
                Dim lines = text.Replace(Convert.ToChar(13), Convert.ToChar(10)).Split(Convert.ToChar(10))
                For i As Integer = lines.Length - 1 To 0 Step -1
                    Dim line = lines(i).Trim()
                    If line.EndsWith(".engine", StringComparison.OrdinalIgnoreCase) AndAlso Not line.Contains("|"c) Then
                        Return line
                    End If
                Next
            End If
            Return LastNonEmptyLine(text)
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
            If _tabs.SelectedIndex = 5 Then
                EnsureMarkdownPage(_pageTutorial)
            End If
            ' 切换页面时清除底部状态提示
            ClearStatus()
            _btnCleanArchives.Visible = (_tabs.SelectedIndex = 2)
            If _tabs.SelectedIndex = 2 Then
                LoadDownloadModels(False)
            End If
            If _tabs.SelectedIndex = 4 Then
                LoadUserModels()
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
                StopEnvironmentCheck(5000)
                ' LakeUI 3.22.0 在 TabControl 隐藏时会重新显示当前绑定页。
                ' 先解除绑定，避免父窗体销毁期间访问已经 Dispose 的 ModernPanel。
                Try
                    For Each tab In _tabs.Items
                        tab.BoundControl = Nothing
                    Next
                Catch
                End Try
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

        ''' <summary>后端切换后同步补帧开关的可用状态；只有 BasicVSR++ 不支持组合补帧。</summary>
        Private Sub UpdateInterpSwitchState()
            SyncInterpSwitchFromConfig()
        End Sub

        Private Function InspectConverterModel(modelPath As String) As Tuple(Of Boolean, String, String)
            Dim coreRoot = ResolveCoreRoot()
            Dim pythonExe = Path.Combine(coreRoot, "python", "python", "python.exe")
            Dim inspector = Path.Combine(coreRoot, "python", "backend", "inspect_interpolation_models.py")
            If Not File.Exists(pythonExe) OrElse Not File.Exists(inspector) Then
                Return New Tuple(Of Boolean, String, String)(False, "", "补帧模型架构检查器未安装")
            End If
            Dim capture = RunProcessCaptureUtf8(pythonExe, Path.GetDirectoryName(inspector),
                                                New String() {inspector, modelPath})
            Dim jsonLine = capture.Item2.Replace(Convert.ToChar(13).ToString(), "").
                Split(Convert.ToChar(10)).
                Select(Function(line) line.Trim()).LastOrDefault(Function(line) line.StartsWith("["))
            If String.IsNullOrWhiteSpace(jsonLine) Then
                Return New Tuple(Of Boolean, String, String)(False, "", LastNonEmptyLine(capture.Item2))
            End If
            Try
                Using document = JsonDocument.Parse(jsonLine)
                    Dim item = document.RootElement(0)
                    Dim architecture = item.GetProperty("architecture").GetString()
                    Dim canTensorRt = item.GetProperty("tensorrt").GetBoolean()
                    Dim modelError = item.GetProperty("error").GetString()
                    If canTensorRt Then Return New Tuple(Of Boolean, String, String)(True, architecture, "")
                    If Not String.IsNullOrWhiteSpace(architecture) Then
                        Return New Tuple(Of Boolean, String, String)(False, architecture,
                            architecture & " 当前只支持 CUDA/PyTorch 补帧，不支持 TensorRT。")
                    End If
                    Return New Tuple(Of Boolean, String, String)(False, "", modelError)
                End Using
            Catch ex As Exception
                Return New Tuple(Of Boolean, String, String)(False, "", "模型架构检查失败：" & ex.Message)
            End Try
        End Function

        Private Shared Function RunProcessCaptureUtf8(fileName As String, workingDirectory As String,
                                                       arguments As IEnumerable(Of String)) As Tuple(Of Integer, String)
            Dim psi As New ProcessStartInfo With {
                .FileName = fileName, .WorkingDirectory = workingDirectory,
                .UseShellExecute = False, .CreateNoWindow = True,
                .RedirectStandardOutput = True, .RedirectStandardError = True,
                .StandardOutputEncoding = Encoding.UTF8, .StandardErrorEncoding = Encoding.UTF8
            }
            For Each argument In arguments
                psi.ArgumentList.Add(argument)
            Next
            Using child = Diagnostics.Process.Start(psi)
                If child Is Nothing Then Return New Tuple(Of Integer, String)(1, "无法启动模型检查进程")
                Dim stdout = child.StandardOutput.ReadToEnd()
                Dim stderr = child.StandardError.ReadToEnd()
                child.WaitForExit()
                Return New Tuple(Of Integer, String)(child.ExitCode, stdout & Environment.NewLine & stderr)
            End Using
        End Function

        Private Shared Function RunRifeTensorRtPrepare(pythonExe As String, prepareScript As String,
                                                       inputPath As String, width As Integer, height As Integer,
                                                       progress As Action(Of String)) As Tuple(Of Integer, String)
            Dim psi As New ProcessStartInfo With {
                .FileName = pythonExe,
                .WorkingDirectory = Path.GetDirectoryName(prepareScript),
                .UseShellExecute = False, .CreateNoWindow = True,
                .RedirectStandardOutput = True, .RedirectStandardError = True,
                .StandardOutputEncoding = Encoding.UTF8, .StandardErrorEncoding = Encoding.UTF8
            }
            For Each argument In New String() {prepareScript, inputPath, "--width", width.ToString(), "--height", height.ToString()}
                psi.ArgumentList.Add(argument)
            Next
            Using child = Diagnostics.Process.Start(psi)
                If child Is Nothing Then Return New Tuple(Of Integer, String)(1, "无法启动 RIFE TensorRT 构建进程")
                Dim output As New StringBuilder()
                Dim outputHandler As DataReceivedEventHandler =
                    Sub(sender, e)
                        If String.IsNullOrWhiteSpace(e.Data) Then Return
                        SyncLock output
                            output.AppendLine(e.Data)
                        End SyncLock
                        progress?.Invoke(e.Data)
                    End Sub
                Dim errorHandler As DataReceivedEventHandler =
                    Sub(sender, e)
                        If String.IsNullOrWhiteSpace(e.Data) Then Return
                        SyncLock output
                            output.AppendLine(e.Data)
                        End SyncLock
                    End Sub
                AddHandler child.OutputDataReceived, outputHandler
                AddHandler child.ErrorDataReceived, errorHandler
                child.BeginOutputReadLine()
                child.BeginErrorReadLine()
                child.WaitForExit()
                child.WaitForExit()
                RemoveHandler child.OutputDataReceived, outputHandler
                RemoveHandler child.ErrorDataReceived, errorHandler
                Return New Tuple(Of Integer, String)(child.ExitCode, output.ToString())
            End Using
        End Function

        ''' <summary>集中同步补帧配置、开关外观和状态标签，避免后端切换后出现状态分裂。</summary>
        Private Sub SyncInterpSwitchFromConfig()
            If _switchInterp Is Nothing OrElse _switchInterp.IsDisposed Then
                Return
            End If
            Dim previousSync = _syncingInterpSwitch
            _syncingInterpSwitch = True
            Try
                ' LakeUI 在 Enabled=False 时会停止动画但保留当前进度；同步后端时必须立即落到目标位置，
                ' 否则 Checked=False 可能仍绘制成右侧滑块。
                Dim animationDuration = _switchInterp.AnimationDuration
                _switchInterp.AnimationDuration = 0
                Try
                    _switchInterp.Checked = _config.InterpEnabled
                    _switchInterp.Enabled = _config.Enabled AndAlso
                        Not String.Equals(_config.Backend, "basicvsrpp", StringComparison.OrdinalIgnoreCase)
                Finally
                    _switchInterp.AnimationDuration = animationDuration
                End Try
            Finally
                _syncingInterpSwitch = previousSync
            End Try
            ' LakeUI 的开关由自绘渲染器负责外观；仅写 Checked 在后端切换时可能留下旧的 GPU 绘制帧。
            ' 强制刷新控件，确保视觉状态与配置同步。
            _switchInterp.Invalidate(True)
            _switchInterp.Refresh()
            _switchInterp.Update()
            UpdateModeStateLabels()
        End Sub

        Private Sub UpdateProcessOrderState()
            Dim combined = _config.UpscaleEnabled AndAlso _config.InterpEnabled
            _cmbProcessOrder.Enabled = _config.Enabled AndAlso combined
            Dim interpFirst = String.Equals(_config.ProcessOrder, "interp-first", StringComparison.OrdinalIgnoreCase)
            If interpFirst Then
                _lblProcessOrder.Text = "<font color=#B1BCCA>当前：先补帧，再超分。</font>"
            Else
                _config.ProcessOrder = "upscale-first"
                _lblProcessOrder.Text = "<font color=#B1BCCA>当前：先超分，再补帧。</font>"
            End If
            If _cmbProcessOrder.Items.Count >= 2 Then
                Dim index = If(interpFirst, 1, 0)
                Dim previousSync = _syncingProcessOrder
                _syncingProcessOrder = True
                ' LakeUI 通过 SelectedIndex 变化同步内部 SingleLineTextBoxRenderer；
                ' 同索引赋值会被短路，因此先清空再选中，而不是直接写 Text。
                _cmbProcessOrder.SelectedIndex = -1
                _cmbProcessOrder.SelectedIndex = index
                _syncingProcessOrder = previousSync
            End If
            _lblProcessOrder.Visible = combined
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
            If String.Equals(_config.Backend, "basicvsrpp", StringComparison.OrdinalIgnoreCase) Then
                _config.InterpEnabled = False
                _config.InterpModel = ""
            End If
            SyncInterpSwitchFromConfig()
            ' 推理方式 / 补帧倍率：仅主开关开启时可操作
            _syncingBackend = True
            SyncBackendCombo()
            _cmbBackend.Enabled = _config.Enabled
            _syncingBackend = False
            _syncingFactor = True
            SyncFactorCombo()
            _cmbFactor.Enabled = _config.Enabled
            _syncingFactor = False
            _syncingInterpBackend = True
            SyncInterpBackendCombo()
            _cmbInterpBackend.Enabled = _config.Enabled
            _syncingInterpBackend = False
            _syncingDynamicOpticalFlow = True
            SyncDynamicOpticalFlowCombo()
            _syncingDynamicOpticalFlow = False
            _syncingSceneThreshold = True
            SyncSceneThresholdCombo()
            _syncingSceneThreshold = False
            _syncingTileSize = True
            SyncTileSizeCombo()
            _syncingTileSize = False
            UpdateAdvancedControlState()
            _syncingProcessOrder = True
            If _cmbProcessOrder.Items.Count > 0 Then
                _cmbProcessOrder.SelectedIndex = If(String.Equals(_config.ProcessOrder, "interp-first", StringComparison.OrdinalIgnoreCase), 1, 0)
            End If
            _syncingProcessOrder = False
            UpdateModeStateLabels()
            UpdateProcessOrderState()
            If String.IsNullOrWhiteSpace(_config.ExePath) Then
                _lblExe.Text = "<font color=#888888>尚未指定 videoenhancer.exe</font>"
            Else
                _lblExe.Text = "<font color=#DCDCDC>" & EscapeHtml(_config.ExePath) & "</font>"
            End If
        End Sub

        ''' <summary>把配置的推理后端同步到下拉框（0=NCNN，1=CUDA，2=TensorRT，3=ONNX，4=FlashVSR，5=BasicVSR++）。</summary>
        Private Sub SyncBackendCombo()
            If _cmbBackend.Items.Count = 0 Then
                Return
            End If
            _cmbBackend.SelectedIndex = If(_config.Backend = "basicvsrpp", 5, If(_config.Backend = "flashvsr", 4, If(_config.Backend = "onnx", 3, If(_config.Backend = "tensorrt", 2, If(_config.Backend = "cuda", 1, 0)))))
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

        Private Sub SyncInterpBackendCombo()
            If _cmbInterpBackend.Items.Count = 0 Then Return
            _cmbInterpBackend.SelectedIndex = If(_config.InterpBackend = "tensorrt", 2, If(_config.InterpBackend = "cuda", 1, 0))
        End Sub

        Private Sub SyncDynamicOpticalFlowCombo()
            If _cmbDynamicOpticalFlow.Items.Count = 0 Then Return
            _cmbDynamicOpticalFlow.SelectedIndex = If(_config.InterpDynamicScaledOpticalFlow, 1, 0)
        End Sub

        Private Sub SyncSceneThresholdCombo()
            If _cmbSceneThreshold.Items.Count = 0 Then Return
            Dim value = If(_config.SceneDetectThreshold <= 0, 4.0, Math.Min(10.0, _config.SceneDetectThreshold))
            Dim best = 3
            For i As Integer = 0 To _cmbSceneThreshold.Items.Count - 1
                If Math.Abs(SceneThresholdValue(_cmbSceneThreshold.Items(i)) - value) < 0.001 Then
                    best = i
                    Exit For
                End If
            Next
            _cmbSceneThreshold.SelectedIndex = best
        End Sub

        Private Sub SyncTileSizeCombo()
            If _cmbTileSize.Items.Count = 0 Then Return
            Dim value = Math.Max(0, _config.UpscaleTileSize)
            Dim best = 0
            For i As Integer = 0 To _cmbTileSize.Items.Count - 1
                If TileSizeValue(_cmbTileSize.Items(i)) = value Then
                    best = i
                    Exit For
                End If
            Next
            _cmbTileSize.SelectedIndex = best
        End Sub

        Private Sub UpdateAdvancedControlState()
            _cmbDynamicOpticalFlow.Enabled = _config.Enabled AndAlso _config.InterpEnabled AndAlso String.Equals(_config.InterpBackend, "cuda", StringComparison.OrdinalIgnoreCase)
            _cmbSceneThreshold.Enabled = _config.Enabled AndAlso _config.InterpEnabled
            Dim tileBackend = String.Equals(_config.Backend, "ncnn", StringComparison.OrdinalIgnoreCase) OrElse
                String.Equals(_config.Backend, "cuda", StringComparison.OrdinalIgnoreCase) OrElse
                String.Equals(_config.Backend, "tensorrt", StringComparison.OrdinalIgnoreCase) OrElse
                String.Equals(_config.Backend, "onnx", StringComparison.OrdinalIgnoreCase)
            _cmbTileSize.Enabled = _config.Enabled AndAlso _config.UpscaleEnabled AndAlso tileBackend
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
