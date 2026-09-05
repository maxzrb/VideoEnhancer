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
Imports System.Reflection
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports FFmpegFreeUI
Imports LakeUI

Namespace videoenhancer

    ''' <summary>"视频超分"插件页面：插件总开关 + 超分/补帧两行开关与模型选择 + 状态信息。</summary>
    Public Class PluginPanel
        Inherits UserControl

        ' 关闭状态的选项框不应因鼠标滚轮经过显示区域而悄悄改变配置。
        ' LakeUI 的下拉列表使用独立窗口，拦截这里的消息不会影响打开列表后的滚动。
        Private NotInheritable Class WheelLockedComboBox
            Inherits LakeComboBox

            Private Const WmMouseWheel As Integer = &H20A
            Private Const WmMouseHWheel As Integer = &H20E

            Protected Overrides Sub WndProc(ByRef m As Message)
                If m.Msg = WmMouseWheel OrElse m.Msg = WmMouseHWheel Then
                    Return
                End If
                MyBase.WndProc(m)
            End Sub
        End Class

        ' 与官方 API 示例插件保持一致：#181818 背景、半透明灰控件、低饱和文字和单一蓝色强调。
        Private Shared ReadOnly UiCanvas As Color = Color.FromArgb(24, 24, 24)
        Private Shared ReadOnly UiSurface As Color = Color.FromArgb(40, 220, 220, 220)
        Private Shared ReadOnly UiSurfaceRaised As Color = Color.FromArgb(40, 220, 220, 220)
        Private Shared ReadOnly UiSurfaceHover As Color = Color.FromArgb(60, 220, 220, 220)
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
        Private ReadOnly _cmbModel As New WheelLockedComboBox()
        Private ReadOnly _cmbInterp As New WheelLockedComboBox()
        Private ReadOnly _lblExe As New HtmlColorLabel()
        Private ReadOnly _lblStatus As New HtmlColorLabel()
        Private ReadOnly _switchUpscale As New LakeUI.BooleanSwitch()
        Private ReadOnly _switchUpscaleHalf As New LakeUI.BooleanSwitch()
        Private ReadOnly _lblSwitch As New HtmlColorLabel()
        Private ReadOnly _switchInterp As New LakeUI.BooleanSwitch()
        Private ReadOnly _switchInterpHalf As New LakeUI.BooleanSwitch()
        Private ReadOnly _lblSwitchInterp As New HtmlColorLabel()
        Private ReadOnly _cmbBackend As New WheelLockedComboBox()
        Private ReadOnly _lblBackend As New HtmlColorLabel()
        Private ReadOnly _cmbInterpBackend As New WheelLockedComboBox()
        Private ReadOnly _cmbFactor As New WheelLockedComboBox()
        Private ReadOnly _cmbDynamicOpticalFlow As New WheelLockedComboBox()
        Private ReadOnly _cmbSceneThreshold As New WheelLockedComboBox()
        Private ReadOnly _cmbTileSize As New WheelLockedComboBox()
        Private ReadOnly _lblFactor As New HtmlColorLabel()
        Private ReadOnly _cmbProcessOrder As New WheelLockedComboBox()
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
        Private _syncingUpscaleHalfSwitch As Boolean = False
        Private _syncingInterpHalfSwitch As Boolean = False
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
        Private _modelMenuToolTipController As ModelMenuToolTipController
        Private _uiReady As Boolean = False
        ' ── 选项卡分栏：超分主界面 / 实时预览 / 高级功能 / 模型转换器 ──
        Private ReadOnly _tabs As New ModernTabControl()
        Private _upscaleRoot As ModernPanel
        Private _upscaleRootSyncPending As Boolean
        ' 3FUI 通过字段名和控件名 ModernPanel1 绑定 LakeUI 背景穿透缓存。
        Private ReadOnly ModernPanel1 As New ModernPanel()
        Private ReadOnly _pageUpscale As New ModernPanel()
        Private ReadOnly _pagePreview As New ModernPanel()
        Private ReadOnly _pageDownloader As New ModernPanel()
        Private ReadOnly _pageConverter As New ModernPanel()
        Private ReadOnly _pageImporter As New ModernPanel()
        Private ReadOnly _pageTutorial As New ModernPanel()
        Private ReadOnly _markdownSources As New Dictionary(Of ModernPanel, String)()
        Private ReadOnly _markdownReady As New HashSet(Of ModernPanel)()
        ' ── 独立图片超分页（位于超分主界面内）──
        Private ReadOnly _btnImageFiles As New ModernButton()
        Private ReadOnly _btnImageFolder As New ModernButton()
        Private ReadOnly _btnImageOutput As New ModernButton()
        Private ReadOnly _btnImageStart As New ModernButton()
        Private ReadOnly _switchImageOriginal As New LakeUI.BooleanSwitch()
        Private ReadOnly _switchImagePng As New LakeUI.BooleanSwitch()
        Private ReadOnly _txtImageOutput As New ModernTextBox()
        Private ReadOnly _cmbImageSuffix As New WheelLockedComboBox()
        Private ReadOnly _cmbImageFormat As New WheelLockedComboBox()
        Private ReadOnly _lblImageInputs As New HtmlColorLabel()
        Private ReadOnly _lblImageOutput As New HtmlColorLabel()
        Private ReadOnly _lblImageProgress As New HtmlColorLabel()
        Private ReadOnly _imageProgress As New ExcellentProgressBar()
        Private ReadOnly _imageFiles As New List(Of String)()
        Private ReadOnly _imageFolders As New List(Of String)()
        Private _imageProcess As Process
        Private _imageRunning As Boolean
        Private _imageCompleteReceived As Boolean
        ' ── 实时预览页 ──
        Private ReadOnly _picPreview As New PixelPictureBox()
        Private ReadOnly _cmbTask As New WheelLockedComboBox()         ' 多任务选择
        Private ReadOnly _lblTask As New HtmlColorLabel()
        Private ReadOnly _cmbRate As New WheelLockedComboBox()
        Private ReadOnly _lblPreviewTitle As New HtmlColorLabel()
        Private ReadOnly _lblPreviewStatus As New HtmlColorLabel()
        Private ReadOnly _lblPreviewNote As New HtmlColorLabel()
        Private ReadOnly _lblRate As New HtmlColorLabel()
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
        Private _userModelContextMenu As ModernContextMenu
        Private _contextUserModel As UserModelItem
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

        ' ModernContextMenu 的菜单项不是 WinForms 控件，使用 LakeUI 的浮动提示窗显示当前悬停模型说明。
        Private NotInheritable Class ModelMenuToolTipController
            Private ReadOnly _menus As New HashSet(Of ModernContextMenu)()
            Private ReadOnly _tooltips As Dictionary(Of ModernContextMenu.ModernMenuItem, String)
            Private ReadOnly _timer As New Timer() With {.Interval = 100}
            Private ReadOnly _tipForm As FloatingToolTipForm
            Private ReadOnly _tipStyle As FloatingToolTipStyle
            Private _hoveredItem As ModernContextMenu.ModernMenuItem
            Private _shownItem As ModernContextMenu.ModernMenuItem
            Private _hoverSinceUtc As DateTime
            Private _closed As Boolean

            Public Sub New(rootMenu As ModernContextMenu,
                           owner As Control,
                           tooltips As Dictionary(Of ModernContextMenu.ModernMenuItem, String))
                _tooltips = If(tooltips,
                    New Dictionary(Of ModernContextMenu.ModernMenuItem, String)())
                _tipForm = New FloatingToolTipForm(owner)
                RegisterMenu(rootMenu)
                _tipStyle = New FloatingToolTipStyle() With {
                    .Font = New Font("Microsoft YaHei UI", 9.0F, FontStyle.Regular),
                    .BackColor = Color.FromArgb(245, 42, 42, 42),
                    .ForeColor = UiText,
                    .BorderColor = Color.FromArgb(96, 96, 96),
                    .BorderSize = 1,
                    .BorderRadius = 8,
                    .Padding = New Padding(10, 8, 10, 8),
                    .MaxWidth = 360
                }
                AddHandler _timer.Tick, AddressOf OnTimerTick
            End Sub

            Public Sub Start()
                If _closed Then Return
                _timer.Start()
            End Sub

            Public Sub Close()
                If _closed Then Return
                _closed = True
                Try
                    _timer.Stop()
                    RemoveHandler _timer.Tick, AddressOf OnTimerTick
                    _timer.Dispose()
                Catch
                End Try
                HideTip()
                Try
                    _tipForm.Dispose()
                Catch
                End Try
                Try
                    If _tipStyle.Font IsNot Nothing Then _tipStyle.Font.Dispose()
                Catch
                End Try
            End Sub

            Private Sub RegisterMenu(menu As ModernContextMenu)
                If menu Is Nothing OrElse Not _menus.Add(menu) Then Return
                For Each item As ModernContextMenu.ModernMenuItem In menu.Items
                    If item IsNot Nothing AndAlso item.SubMenu IsNot Nothing Then
                        RegisterMenu(item.SubMenu)
                    End If
                Next
            End Sub

            Private Sub OnTimerTick(sender As Object, e As EventArgs)
                If _closed Then Return
                Try
                    Dim popup As Form = Nothing
                    Dim item As ModernContextMenu.ModernMenuItem = Nothing
                    Dim itemBounds As Rectangle
                    If Not TryGetHoveredItem(popup, item, itemBounds) Then
                        ResetHover()
                        Return
                    End If

                    Dim tooltipText As String = Nothing
                    If Not _tooltips.TryGetValue(item, tooltipText) OrElse
                       String.IsNullOrWhiteSpace(tooltipText) Then
                        ResetHover()
                        Return
                    End If

                    If Not Object.ReferenceEquals(_hoveredItem, item) Then
                        _hoveredItem = item
                        _shownItem = Nothing
                        _hoverSinceUtc = DateTime.UtcNow
                        HideTip()
                        Return
                    End If
                    If Object.ReferenceEquals(_shownItem, item) Then Return
                    If (DateTime.UtcNow - _hoverSinceUtc).TotalMilliseconds < 350 Then Return

                    ShowTip(popup, itemBounds, item, tooltipText)
                Catch
                    ResetHover()
                End Try
            End Sub

            Private Function TryGetHoveredItem(ByRef popup As Form,
                                               ByRef item As ModernContextMenu.ModernMenuItem,
                                               ByRef itemBounds As Rectangle) As Boolean
                Dim cursorPoint As Point = Cursor.Position
                Try
                    For index = Application.OpenForms.Count - 1 To 0 Step -1
                        Dim candidate = Application.OpenForms(index)
                        If candidate Is Nothing OrElse candidate.IsDisposed OrElse Not candidate.Visible OrElse
                           Not candidate.Bounds.Contains(cursorPoint) OrElse
                           Not String.Equals(candidate.GetType().FullName,
                               "LakeUI.ModernContextMenu+MenuPopupForm", StringComparison.Ordinal) Then
                            Continue For
                        End If

                        Dim menu = GetPopupMenu(candidate)
                        If menu Is Nothing OrElse Not _menus.Contains(menu) Then Continue For
                        Dim location = candidate.PointToClient(cursorPoint)
                        Dim itemIndex = GetPopupItemIndex(candidate, location)
                        If itemIndex < 0 OrElse itemIndex >= menu.Items.Count Then Continue For
                        Dim candidateItem = menu.Items(itemIndex)
                        If candidateItem Is Nothing OrElse candidateItem.IsSeparator OrElse candidateItem.IsDescription Then
                            Continue For
                        End If

                        popup = candidate
                        item = candidateItem
                        itemBounds = GetPopupItemBounds(candidate, itemIndex, location)
                        Return True
                    Next
                Catch
                End Try
                Return False
            End Function

            Private Shared Function GetPopupMenu(popup As Form) As ModernContextMenu
                Try
                    Dim field = popup.GetType().GetField("菜单",
                        BindingFlags.Instance Or BindingFlags.NonPublic)
                    If field Is Nothing Then
                        field = popup.GetType().GetFields(
                            BindingFlags.Instance Or BindingFlags.NonPublic).
                            FirstOrDefault(Function(candidate) GetType(ModernContextMenu).IsAssignableFrom(candidate.FieldType))
                    End If
                    If field Is Nothing Then Return Nothing
                    Return TryCast(field.GetValue(popup), ModernContextMenu)
                Catch
                    Return Nothing
                End Try
            End Function

            Private Shared Function GetPopupItemIndex(popup As Form, location As Point) As Integer
                Try
                    Dim method = popup.GetType().GetMethod("获取项目索引",
                        BindingFlags.Instance Or BindingFlags.NonPublic)
                    If method Is Nothing Then Return -1
                    Dim result = method.Invoke(popup, New Object() {location, True})
                    Return If(result Is Nothing, -1, CInt(result))
                Catch
                    Return -1
                End Try
            End Function

            Private Shared Function GetPopupItemBounds(popup As Form,
                                                       itemIndex As Integer,
                                                       location As Point) As Rectangle
                Try
                    Dim field = popup.GetType().GetField("项目区域列表",
                        BindingFlags.Instance Or BindingFlags.NonPublic)
                    Dim areas = If(field Is Nothing, Nothing,
                        TryCast(field.GetValue(popup), System.Collections.IList))
                    If areas IsNot Nothing AndAlso itemIndex >= 0 AndAlso itemIndex < areas.Count AndAlso
                       TypeOf areas(itemIndex) Is Rectangle Then
                        Return DirectCast(areas(itemIndex), Rectangle)
                    End If
                Catch
                End Try
                Return New Rectangle(location, New Size(1, 1))
            End Function

            Private Sub ShowTip(popup As Form,
                                itemBounds As Rectangle,
                                item As ModernContextMenu.ModernMenuItem,
                                text As String)
                Dim screenBounds = popup.RectangleToScreen(itemBounds)
                Dim workingArea = Screen.FromRectangle(screenBounds).WorkingArea
                Dim side As FloatingToolTipSide
                Dim anchor As Point
                If workingArea.Right - screenBounds.Right >= 380 Then
                    side = FloatingToolTipSide.Right
                    anchor = New Point(screenBounds.Right,
                                       screenBounds.Top + Math.Max(1, screenBounds.Height \ 2))
                Else
                    side = FloatingToolTipSide.Left
                    anchor = New Point(screenBounds.Left,
                                       screenBounds.Top + Math.Max(1, screenBounds.Height \ 2))
                End If
                _tipForm.ShowTip(text, anchor, _tipStyle, 8, side)
                _shownItem = item
            End Sub

            Private Sub ResetHover()
                _hoveredItem = Nothing
                _shownItem = Nothing
                _hoverSinceUtc = DateTime.MinValue
                HideTip()
            End Sub

            Private Sub HideTip()
                Try
                    If Not _tipForm.IsDisposed Then _tipForm.Hide()
                Catch
                End Try
            End Sub
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
            If Not LakeUiV51Available() Then
                InitializeCompatibilityErrorUi()
                Return
            End If
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
                    "下载完成并校验后会再次询问是否关闭并重启 3FUI。" & Environment.NewLine &
                    "现在下载更新包吗？"
                If Not ShowLakeConfirm(Me, message, "发现新版本", defaultYes:=True) Then Return
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
                ShowStatus("更新包已下载并校验，等待确认安装…", False)
                Dim restartMessage = "VideoEnhancer " & manifest.Version & " 已下载并通过校验。" &
                    Environment.NewLine & Environment.NewLine &
                    "现在安装会关闭并重新启动 3FUI。" & Environment.NewLine &
                    "请先停止编码与视频处理任务，并保存尚未完成的操作。" & Environment.NewLine & Environment.NewLine &
                    "确定现在重启并安装吗？"
                If Not ShowLakeConfirm(Me, restartMessage, "确认重启安装", defaultYes:=False) Then
                    ShowStatus("更新包已下载；已取消本次重启安装", False)
                    Return
                End If
                ShowStatus("用户已确认，正在准备重启 3FUI…", False)
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

        ''' <summary>超分精度开关：开启时优先半精度，关闭时强制 FP32。</summary>
        Private Sub OnUpscaleHalfSwitchChanged(sender As Object, e As EventArgs)
            If _syncingUpscaleHalfSwitch Then Return
            _config.UpscaleHalfPrecision = _switchUpscaleHalf.Checked
            _config.Save()
            ShowStatus(If(_switchUpscaleHalf.Checked,
                "超分将优先使用 FP16，不兼容时自动回退 FP32",
                "超分已强制使用 FP32"), False)
        End Sub

        ''' <summary>补帧精度开关：开启时优先半精度，关闭时强制 FP32。</summary>
        Private Sub OnInterpHalfSwitchChanged(sender As Object, e As EventArgs)
            If _syncingInterpHalfSwitch Then Return
            _config.InterpHalfPrecision = _switchInterpHalf.Checked
            _config.Save()
            ShowStatus(If(_switchInterpHalf.Checked,
                "补帧将优先使用 FP16，不兼容时自动回退 FP32",
                "补帧已强制使用 FP32"), False)
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

        ''' <summary>LakeUI 5.1 是本插件的最低 GPU 控件基线，允许后续 5.x 宿主版本。</summary>
        Private Shared Function LakeUiV51Available() As Boolean
            Try
                Dim version = GetType(ModernPanel).Assembly.GetName().Version
                Return version IsNot Nothing AndAlso version.Major = 5 AndAlso version.Minor >= 1
            Catch
                Return False
            End Try
        End Function

        Private Sub InitializeCompatibilityErrorUi()
            BackColor = UiCanvas
            Dock = DockStyle.Fill
            MinimumSize = New Size(640, 220)
            ModernPanel1.Name = "ModernPanel1"
            ModernPanel1.Dock = DockStyle.Fill
            ModernPanel1.BackColor = Color.Transparent
            ModernPanel1.BackColor1 = Color.Transparent
            ModernPanel1.BorderSize = 0
            Dim message As New HtmlColorLabel With {
                .Dock = DockStyle.Fill,
                .BackColor = Color.Transparent,
                .BackColor1 = Color.Transparent,
                .BorderSize = 0,
                .Padding = New Padding(24),
                .Font = New Font("Microsoft YaHei UI", 12.0F, FontStyle.Regular),
                .ForeColor = UiDanger,
                .TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft,
                .Text = "<font color=#EB5D5D><b>无法加载视频超分插件</b></font><br/>" &
                        "<font color=#C0C0C0>需要升级 3FUI/LakeUI 5.1 或更高版本后才能继续使用。</font>"
            }
            ModernPanel1.Controls.Add(message)
            Controls.Add(ModernPanel1)
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

        Private Shared Function ModelIntroduction(entry As ModelCatalogItem,
                                                   interpolation As Boolean) As String
            If entry Is Nothing Then Return ""
            If interpolation Then Return InterpolationModelIntroduction(entry)

            Dim modelId = If(entry.Id, "").Replace(Convert.ToChar(92), "/").Trim()
            Dim displayName = If(entry.DisplayName, "").Trim()
            Dim key = (modelId & " " & displayName).ToLowerInvariant()
            Dim architecture = If(entry.Architecture, "").Trim().ToUpperInvariant()

            If key.Contains("basicvsr") Then
                Return "BasicVSR++ REDS4：利用相邻视频帧做时序复原，适合画面连续的低清视频；它不是普通单帧放大，当前不能再叠加运动补帧。"
            End If
            If key.Contains("flashvsr") Then
                Return "FlashVSR：面向连续视频的时序超分模型，适合希望一次处理运动连续性与分辨率的 NVIDIA 用户；它不是普通图片模型，当前不参与通用补帧组合。"
            End If

            If key.Contains("animejanai-hd-v3.1") Then
                Dim preset = If(key.Contains("sharp1") AndAlso key.Contains("performance"),
                    "Sharp1 Performance：在较轻量的 Performance 配置上进一步强调边缘清晰度。",
                    If(key.Contains("sharp1"),
                        "Sharp1 Balanced：在 Balanced 配置上额外强调线稿和边缘。",
                        If(key.Contains("performance"),
                            "Performance：优先考虑处理速度和显存占用。",
                            "Balanced：在清晰度、稳定性和资源占用之间取平衡。")))
                Return "AnimeJaNai HD V3.1 " & preset & " 这是 2x 动漫/插画模型；干净的高清原片可优先用 Sharp1，普通素材先用 Balanced，显存紧张时选 Performance。"
            End If
            If key.Contains("animejanai-sd-v1beta34") Then
                Return "AnimeJaNai SD V1 beta34 Compact strong：针对较低清晰度动漫素材做 2x 强增强；低清噪点和压缩块也可能被放大，更适合噪声较少的动漫素材。"
            End If
            If key.Contains("animejanai-v3") Then
                Return "AnimeJaNai V3 HD Sharp1 Compact：2x 动漫/插画模型，Compact 版本较易运行，Sharp1 会更强调线稿边缘；原片已有噪点时先比较是否过锐。"
            End If
            If key.Contains("animejanai-v2") Then
                Return "AnimeJaNai V2 Compact：2x 动漫/插画轻量模型，适合第一次测试、预览或显存较紧张的设备；想要更强边缘强调可改试 V3 Sharp1。"
            End If

            If key.Contains("anisd") Then
                Return AniSdModelIntroduction(key, architecture)
            End If

            If key.Contains("realhatgan") Then
                If key.Contains("x1") OrElse key.Contains("fix-only") Then
                    Return "RealHatGAN JP Illustration x1 修复版：只修复插画纹理和边缘，不改变分辨率；适合尺寸已经够大、只想减少瑕疵的素材，输入边长需按 16 的倍数处理。"
                End If
                If key.Contains("universal") Then
                    Return "RealHatGAN Universal Illustration 2x：面向不同风格插画的 2x 放大，适合不确定具体画风的二次元素材；输入边长需按 16 的倍数处理。"
                End If
                If key.Contains("4x") Then
                    Return "RealHatGAN JP Illustration 4x：针对日式插画做 4x 放大，适合需要大幅放大的线稿和绘画素材；输入边长需按 16 的倍数处理，显存压力也更高。"
                End If
                Return "RealHatGAN JP Illustration 2x：针对日式插画做中等幅度放大，适合先保留线稿结构再增加纹理；输入边长需按 16 的倍数处理。"
            End If

            If key.Contains("animevideov3") Then
                If key.Contains("-2x") Then
                    Return "Real-ESRGAN AnimeVideoV3 2x：为动漫视频准备的 2x 模型，适合原片尚清楚、只需要温和放大的情况，速度和细节风险都较易控制。"
                End If
                If key.Contains("-3x") Then
                    Return "Real-ESRGAN AnimeVideoV3 3x：为动漫视频准备的 3x 模型，适合 2x 不够、4x 又过大的中间需求；适合需要中等放大幅度的动漫视频。"
                End If
                Return "Real-ESRGAN AnimeVideoV3 4x：为动漫视频准备的 4x 模型，适合低分辨率动画需要明显放大的情况；输出像素量约为 2x 的四倍，处理更慢。"
            End If
            If key.Contains("general-x4v3") Then
                Return "Real-ESRGAN General x4v3：面向真人、风景和普通网络视频的通用 4x 方案；内容类型不特殊时可优先选择，适合做通用画面放大。"
            End If
            If key.Contains("x4plus-anime") Then
                Return "Real-ESRGAN x4plus Anime：动漫/插画 4x 模型，适合线稿、平涂和角色画面；如果原片压缩严重，先用较低倍率或去噪模型比较。"
            End If
            If key.Contains("x4-jp-illustration-fix1") Then
                Return "Real-ESRGAN JP Illustration fix1：日式插画专用 4x ONNX 导出修正版 1，题材和倍率固定；它与 fix2 是不同导出版本，优先保留能在当前环境预检和运行的一份。"
            End If
            If key.Contains("x4-jp-illustration-fix2") Then
                Return "Real-ESRGAN JP Illustration fix2：日式插画专用 4x ONNX 导出修正版 2，题材和倍率固定；若 fix1 在你的 ONNX 环境异常，可用它做替代测试。"
            End If

            If key.Contains("waifu2x") Then
                If key.Contains("photo") Then
                    Return "Waifu2x Photo 2x：为照片类素材准备的 2x 模型；人物、实拍和纹理照片可先选它，纯动漫线稿优先考虑普通或 Noise 版本。"
                End If
                If key.Contains("noise3") Then
                    Return "Waifu2x Noise3 2x：2x 放大并使用最强一级去噪，适合噪声很重的动漫截图；细线和小字可能被抹掉，建议与 Noise2 对比。"
                End If
                If key.Contains("noise2") Then
                    Return "Waifu2x Noise2 2x：2x 放大并使用中等去噪，适合有明显压缩噪点但仍要保留线稿的动漫素材。"
                End If
                If key.Contains("noise1") Then
                    Return "Waifu2x Noise1 2x：2x 放大并使用轻度去噪，适合轻微噪点的动漫画面；比 Noise2 更容易保留细节。"
                End If
                If key.Contains("noise0") Then
                    Return "Waifu2x Noise0 2x：2x 放大但不主动加强去噪，适合原片干净、希望尽量保留原有纹理的动漫素材。"
                End If
                Return "Waifu2x 2x：动漫和插画的基础 2x 放大方案；素材有噪点时再按噪声强度选择 Noise1/2/3，避免一开始就过度去噪。"
            End If
            If key.Contains("cugan-conservative") Then
                Return "Real-CUGAN Conservative 2x：动漫画面的保守型 2x 放大，倾向少改动画面；适合不想出现过度锐化或新纹理的干净素材。"
            End If
            If key.Contains("denoiseh264") Then
                Return "DenoiseH264 SuperUltraCompact：针对 H.264 压缩噪声的 1x 处理，不改变分辨率；适合先清理块状噪声，再交给后续放大模型。"
            End If
            If key.Contains("dncnn") Then
                Return "DnCNN ColorBlind：盲去噪 1x 模型，会根据画面估计噪声强度，不改变分辨率；适合噪声来源不明的素材，但要留意细节是否被过度抹平。"
            End If
            If key.Contains("animesr") Then
                Return "AnimeSR V2：动漫视频时序超分 4x 模型，利用相邻帧帮助保持动画细节连续；当前清单仅支持 CUDA，适合 NVIDIA 用户处理动漫视频。"
            End If
            If key.Contains("apisr-dat") Then
                Return "APISR DAT GAN 4x：面向动漫/插画纹理恢复的 4x GAN 模型，适合希望补回细节的素材；GAN 可能生成看似合理的新纹理，建议先看脸部和文字。"
            End If
            If key.Contains("apisr-grl") Then
                Return "APISR GRL GAN 4x：APISR 的 GRL 4x 纹理恢复版本，适合细节丰富的动漫/插画；更适合需要明显补回纹理的画面。"
            End If
            If key.Contains("apisr-rrdb") Then
                If key.Contains("-2x") Then
                    Return "APISR RRDB GAN 2x：温和的 2x 纹理恢复，适合原片分辨率尚可、只想补一点细节的动漫/插画。"
                End If
                Return "APISR RRDB GAN 4x：需要明显放大的 4x 纹理恢复版本；比 2x 更吃资源，也更容易增强细线、文字和重复纹理。"
            End If
            If key.Contains("aniscale2-refiner") Then
                Return "AniScale2 Refiner 1x：只做细节修复、不改变分辨率；适合先清理或整理画面，再决定是否另做 2x 放大。"
            End If
            If key.Contains("aniscale2-esrgan-lite") Then
                Return "AniScale2 ESRGAN-Lite 2x：偏轻量的动漫/插画 2x 放大，适合速度优先或显存较紧张的设备。"
            End If
            If key.Contains("aniscale2-esrgan") Then
                Return "AniScale2 ESRGAN 2x：动漫/插画的常规 2x 纹理增强，适合想比轻量版获得更强细节、又不需要 4x 的素材。"
            End If
            If key.Contains("aniscale2-ditn") Then
                Return "AniScale2 DITN 2x：动漫/插画 2x 细节恢复模型，适合想保留结构、减少过度锐化的素材。"
            End If
            If key.Contains("aniscale2-omni") Then
                Return "AniScale2 Omni 2x：面向多种动漫/插画内容的均衡 2x 方案，适合不知道该选哪种专门风格时先做基准测试。"
            End If
            If key.Contains("anitoon-rplksrl") Then
                Return "AniToon RPLKSR-L 2x：AniToon 的大模型版本，偏向保留更多动漫纹理；画质优先时使用，显存和时间开销会高于 S 版。"
            End If
            If key.Contains("anitoon-rplksrs") Then
                Return "AniToon RPLKSR-S 2x：AniToon 的小模型版本，偏向速度和较低资源占用；适合预览、批量处理或显存较紧张的设备。"
            End If
            If key.Contains("anitoon-rplksr") Then
                Return "AniToon RPLKSR 2x：AniToon 的标准 2x 动漫放大方案；想在速度与细节之间取中间位置时先用它。"
            End If
            If key.Contains("nomos8k") Then
                If key.Contains("strong") Then
                    Return "Nomos8k SPAN OTF strong 4x：高强度 4x 纹理恢复，适合细节缺失明显的素材；也最容易把噪声或错误纹理一起放大。"
                End If
                If key.Contains("weak") Then
                    Return "Nomos8k SPAN OTF weak 4x：较温和的 4x 纹理恢复，适合画面本身较干净、希望少改动原貌的素材。"
                End If
                Return "Nomos8k SPAN OTF medium 4x：中等强度 4x 纹理恢复，适合在 weak 和 strong 之间取平衡；第一次使用可先从它开始。"
            End If
            If key.Contains("modernspanimation-v3") Then
                Return "ModernSpanimation V3 2x：面向动漫画面和线稿的 SPAN 2x 版本；它与 V2 是不同训练版本，适合希望使用较新训练配置的动漫素材。"
            End If
            If key.Contains("modernspanimation-v2") Then
                Return "ModernSpanimation V2 2x：面向动漫画面和线稿的 SPAN 2x 版本，适合希望使用 V2 训练配置的动漫素材。"
            End If
            If key.Contains("bhi-spanplusdynamic") Then
                Return "BHI SpanPlus Dynamic Light 2x：轻量动态输入的 SPANPlus 2x 模型，适合希望兼顾速度与线稿细节的动漫素材。"
            End If
            If key.Contains("sudo-shuffle-span") Then
                Return "Sudo-Shuffle SPAN 2x：针对插画和动漫纹理的 2x SPAN 方案，适合想保留线稿、避免过度 GAN 纹理的素材。"
            End If
            If key.Contains("openproteus") Then
                Return "OpenProteus Compact i2 2x：轻量 2x 细节恢复模型，适合普通动漫/插画素材；处理速度和较低资源占用优先时可选它。"
            End If
            If key.Contains("ani4k-compact") Then
                Return "Ani4K Compact 2x：面向动漫画面的轻量 2x 放大，适合先快速查看模型方向；如果细节不足，再与 AnimeJaNai 或 SPAN 版本比较。"
            End If

            If key.Contains("realplksr") OrElse key.Contains("rplksr") Then
                If key.Contains("-l") Then
                    Return "RealPLKSR-L 2x：较大容量的 2x 细节恢复模型，适合画面质量优先；资源紧张时改用 S 版或 Compact 版。"
                End If
                If key.Contains("-s") Then
                    Return "RealPLKSR-S 2x：较小容量的 2x 细节恢复模型，适合预览和速度优先；细节要求高时可与标准版对比。"
                End If
                If key.Contains("dynamic") Then
                    Return "RealPLKSR 动态输入 2x：适合尺寸不固定的视频帧，按输入内容动态处理；它偏向自然的边缘与纹理恢复。"
                End If
                Return "RealPLKSR 2x：动漫/插画的均衡细节恢复模型，适合想要清晰边缘但不希望使用强 GAN 风格的素材。"
            End If

            Select Case architecture
                Case "COMPACT"
                    Return "「" & displayName & "」是 Compact 轻量 2x 模型，适合预览、批量处理或显存有限的设备；先观察清晰度，再决定是否换更大模型。"
                Case "CRAFT"
                    Return "「" & displayName & "」是 CRAFT 2x 纹理恢复模型，适合动漫/插画细节；请重点检查线稿、文字和高对比边缘。"
                Case "DAT", "DAT2"
                    Return "「" & displayName & "」是 DAT 纹理恢复模型，适合细节丰富的动漫/插画；纹理恢复取向较积极。"
                Case "DITN"
                    Return "「" & displayName & "」是 DITN 2x 细节恢复模型，适合希望增强纹理但保留原结构的动漫/插画。"
                Case "ESRGAN", "ESRGAN-LITE"
                    Return "「" & displayName & "」是动漫/插画 2x 纹理增强模型；Lite 侧重轻量，普通版侧重更充分的细节恢复。"
                Case "ESRGAN-REFINER"
                    Return "「" & displayName & "」是 1x 细节修复模型，只修画面不改分辨率；适合把去噪/修复作为独立第一步。"
                Case "GRL"
                    Return "「" & displayName & "」是 GRL 纹理恢复模型，适合细节丰富、需要补回纹理的动漫/插画。"
                Case "OMNISR"
                    Return "「" & displayName & "」是均衡型 2x 细节恢复模型，适合不同内容混合的视频，第一次选择可用它做基准。"
                Case "REAL-CUGAN"
                    Return "「" & displayName & "」是偏保守的动漫 2x 模型，适合希望少改动原画、降低过度锐化风险的素材。"
                Case "RRDBNET"
                    Return "「" & displayName & "」是 RRDB 纹理恢复模型，适合普通动漫/插画放大；高倍率更适合确实需要大幅放大的素材。"
                Case "SPAN", "SPANF3", "SPANPLUS"
                    Return "「" & displayName & "」是 SPAN 结构与纹理恢复模型，适合线稿、平涂和动漫画面；它更强调边缘，压缩噪声严重时先做去噪测试。"
                Case "SWINIR"
                    Return "「" & displayName & "」是 SwinIR 细节恢复模型，适合希望结果较稳、不过分制造纹理的动漫/插画；ONNX 固定窗口版本会自动按窗口处理。"
                Case Else
                    Return "「" & displayName & "」当前标记为 " & If(String.IsNullOrWhiteSpace(architecture), "未知架构", architecture) & "；请根据素材题材、目标倍率和可用后端选择，适合先从默认参数开始。"
            End Select
        End Function

        Private Shared Function InterpolationModelIntroduction(entry As ModelCatalogItem) As String
            Dim modelId = If(entry.Id, "").Replace(Convert.ToChar(92), "/").Trim()
            Dim displayName = If(entry.DisplayName, "").Trim()
            Dim key = (modelId & " " & displayName).ToLowerInvariant()
            If key.Contains("rife") Then
                If key.Contains("heavy") Then
                    Return "RIFE heavy：更重的通用光流补帧模型，复杂运动时可获得更充分的运动估计；速度和显存开销较高，适合显存充足且运动复杂的素材。"
                End If
                If key.Contains("lite") Then
                    Return "RIFE lite：偏轻量的通用光流补帧模型，适合预览、批量处理或显存紧张的设备；复杂运动的余量小于 heavy。"
                End If
                If key.Contains("4.26") Then
                    Return "RIFE v4.26：通用光流补帧模型，适合真人、动画和普通镜头；它是一次稳妥的默认起点，倍率先从 2 倍开始。"
                End If
                If key.Contains("4.25") Then
                    Return "RIFE v4.25：通用光流补帧模型，适合大多数连续运动画面；快速运动或复杂遮挡的素材也可优先考虑。"
                End If
                Return "RIFE：通用光流补帧模型，给连续视频生成中间帧；适合真人、动漫和普通镜头，倍率通常从 2 倍开始。"
            End If
            If key.Contains("gmfss") Then
                If key.Contains("anime") OrElse key.Contains("animerun") Then
                    Return "GMFSS AnimeRun：针对动漫运动和线稿连续性的补帧模型，适合动画素材；真人视频请优先用 RIFE 或 GMFSS Base 做比较。"
                End If
                If key.Contains("union") Then
                    Return "GMFSS Union：通用时序补帧模型，利用更多帧信息处理复杂运动；适合想在快速镜头中提升稳定性的 NVIDIA 用户。"
                End If
                Return "GMFSS Base：通用时序补帧模型，适合真人和普通连续运动；倍率从 2 倍开始更易控制计算量。"
            End If
            If key.Contains("gimm") Then
                If key.Contains("lpips") Then
                    Return "GIMM LPIPS：强调感知相似度的时序补帧模型，适合更在意运动观感的连续视频。"
                End If
                If key.Contains("-r") Then
                    Return "GIMM R：时序补帧模型的 R 配置，适合希望保持运动结构连续的素材；适合连续性要求较高的画面。"
                End If
                If key.Contains("-f") Then
                    Return "GIMM F：时序补帧模型的 F 配置，适合希望改善运动流畅度的素材；倍率可从 2 倍、转场阈值 4.0 开始。"
                End If
                Return "GIMM：时序补帧模型，适合连续运动视频；它需要 CUDA/PyTorch，适合 NVIDIA 用户处理连续运动画面。"
            End If
            Return "「" & displayName & "」是补帧模型，用于根据相邻帧生成中间帧；倍率通常从 2 倍开始，素材运动复杂时再提高倍率。"
        End Function

        Private Shared Function AniSdModelIntroduction(key As String, architecture As String) As String
            Dim variantName As String
            If key.Contains("ac-g6i2a") Then
                variantName = "AC-G6i2a"
            ElseIf key.Contains("ac-g6i2b") Then
                variantName = "AC-G6i2b"
            ElseIf key.Contains("dc") Then
                variantName = "DC"
            ElseIf key.Contains("db-i2") Then
                variantName = "DB-i2"
            ElseIf key.Contains("g6i1b") Then
                variantName = "G6i1b"
            ElseIf key.Contains("g6i1") Then
                variantName = "G6i1"
            ElseIf key.Contains("ps-g6i2") Then
                variantName = "PS-G6i2"
            ElseIf key.Contains("ac-") Then
                variantName = "AC"
            Else
                variantName = "AniSD"
            End If

            Dim role As String
            Select Case architecture
                Case "COMPACT"
                    role = "Compact 轻量版，适合预览和显存有限的设备"
                Case "SPAN"
                    role = "SPAN 版本，适合线稿、平涂和边缘细节"
                Case "SWINIR"
                    role = "SwinIR 版本，倾向稳定恢复纹理；ONNX 固定窗口版本会按窗口处理"
                Case "CRAFT"
                    role = "CRAFT 版本，适合细节丰富的动漫/插画"
                Case "DAT2"
                    role = "DAT2 版本，适合纹理复杂的动漫/插画"
                Case "REALPLKSR"
                    role = "RealPLKSR 版本，适合在边缘清晰与纹理自然之间取平衡"
                Case Else
                    role = If(String.IsNullOrWhiteSpace(architecture), "具体架构未标注", architecture & " 版本")
            End Select

            If key.Contains("-1x") Then
                Return "AniSD " & variantName & " " & role & "，这是 1x 修复而不是放大；适合先修画面、再另选 2x/4x 模型。"
            End If
            If key.Contains("dynamic") Then
                Return "AniSD " & variantName & " " & role & "，这是 2x 动态输入版本，适合尺寸不固定的视频帧；适合动漫和插画的常规放大。"
            End If
            If key.Contains("240x320") OrElse key.Contains("320x448") OrElse key.Contains("480x320") Then
                Dim windowSize = If(key.Contains("240x320"), "240x320", If(key.Contains("320x448"), "320x448", "480x320"))
                Return "AniSD " & variantName & " " & role & "，这是 2x ONNX 固定窗口 " & windowSize & " 版本；适合与对应窗口布局配合，程序会自动按窗口处理。"
            End If
            Return "AniSD " & variantName & " " & role & "，这是 2x 动漫/插画模型；AC、DC、DB、PS 和 G6i 代表不同训练配置，不是简单的高低档位，应按具体架构和素材特点选择。"
        End Function

        Private Shared Function ModelTooltipText(entry As ModelCatalogItem,
                                                  interpolation As Boolean) As String
            If entry Is Nothing Then Return ""
            Dim lines As New List(Of String)()
            If String.Equals(entry.Source, "builtin", StringComparison.OrdinalIgnoreCase) Then
                lines.Add("内置模型")
            ElseIf String.Equals(entry.Source, "user", StringComparison.OrdinalIgnoreCase) Then
                lines.Add("用户导入模型")
            End If
            If Not String.IsNullOrWhiteSpace(entry.DisplayName) Then
                lines.Add("模型：" & entry.DisplayName)
            End If
            lines.Add(ModelIntroduction(entry, interpolation))
            If Not interpolation AndAlso entry.Scale > 0 Then
                lines.Add("倍率：" & entry.Scale.ToString() & "x")
            End If
            If entry.Backends IsNot Nothing AndAlso entry.Backends.Length > 0 Then
                lines.Add("支持后端：" & String.Join(" / ", entry.Backends.Select(Function(value) BackendDisplayName(value))))
            End If
            Return String.Join(Environment.NewLine, lines.Where(Function(line) Not String.IsNullOrWhiteSpace(line)))
        End Function

        Private Shared Function BackendDisplayName(value As String) As String
            Select Case If(value, "").Trim().ToLowerInvariant()
                Case "ncnn"
                    Return "NCNN"
                Case "cuda"
                    Return "CUDA"
                Case "tensorrt"
                    Return "TensorRT"
                Case "onnx"
                    Return "ONNX"
                Case "flashvsr"
                    Return "FlashVSR"
                Case "basicvsrpp"
                    Return "BasicVSR++"
                Case Else
                    Return If(value, "").Trim()
            End Select
        End Function

        Private Shared Sub ConfigureModelMenu(menu As ModernContextMenu,
                                              Optional reserveIconColumn As Boolean = True)
            menu.BackColor = Color.FromArgb(42, 42, 42)
            menu.BackColor1 = Color.FromArgb(42, 42, 42)
            menu.BorderColor = Color.FromArgb(72, 72, 72)
            menu.BorderSize = 1
            menu.MenuForeColor = UiText
            menu.HoverBackColor = UiSurfaceHover
            menu.PressedBackColor = UiAccentPressed
            menu.ArrowColor = UiTextSecondary
            menu.ItemHeight = 34
            ' 一级分类没有勾选框或图标，关闭图标列；二级模型项保留图标列承载勾选标记。
            menu.IconSize = If(reserveIconColumn, 24, 0)
            menu.ItemPadding = New Padding(12, 0, 12, 0)
            menu.MenuPadding = New Padding(4)
            menu.SubMenuHorizontalOffset = 2
        End Sub

        ''' <summary>按锚点下方的实际可用空间压缩根菜单，避免 LakeUI 因菜单过高而翻到屏幕顶端。</summary>
        Private Shared Sub FitModelMenuBelowAnchor(menu As ModernContextMenu, anchor As Control)
            If menu Is Nothing OrElse anchor Is Nothing OrElse menu.Items.Count = 0 Then Return
            Dim popupPoint = anchor.PointToScreen(New Point(0, anchor.Height + 2))
            Dim workingArea = Screen.FromPoint(popupPoint).WorkingArea
            Dim dpiScale = Math.Max(1.0R, anchor.DeviceDpi / 96.0R)
            Dim availableLogicalHeight = Math.Max(0.0R,
                (workingArea.Bottom - popupPoint.Y - 12) / dpiScale)
            Dim fixedLogicalHeight = menu.MenuPadding.Vertical + 4
            Dim fittingItemHeight = CInt(Math.Floor(
                (availableLogicalHeight - fixedLogicalHeight) / menu.Items.Count))
            menu.ItemHeight = Math.Max(22, Math.Min(menu.ItemHeight, fittingItemHeight))
        End Sub

        Private Sub CloseModelMenuToolTip()
            Dim controller = _modelMenuToolTipController
            _modelMenuToolTipController = Nothing
            If controller IsNot Nothing Then controller.Close()
        End Sub

        Private Sub ShowModelMenu(anchor As ModernComboBox, catalog As List(Of ModelCatalogItem), interpolation As Boolean)
            If catalog.Count = 0 OrElse anchor.IsDisposed Then Return
            CloseModelMenuToolTip()
            Dim root As New ModernContextMenu()
            ConfigureModelMenu(root, reserveIconColumn:=False)
            Dim tooltipEntries As New Dictionary(Of ModernContextMenu.ModernMenuItem, String)()
            For Each group In catalog.GroupBy(Function(item) If(String.IsNullOrWhiteSpace(item.Architecture), "其他模型", item.Architecture)).
                    OrderBy(Function(item) item.Key, StringComparer.CurrentCultureIgnoreCase)
                Dim submenu As New ModernContextMenu()
                ConfigureModelMenu(submenu, reserveIconColumn:=True)
                For Each entry In group.OrderBy(Function(item) item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                    Dim selectedEntry = entry
                    Dim suffix = If(entry.Scale > 0 AndAlso Not interpolation, "  · " & entry.Scale.ToString() & "x", "")
                    If String.Equals(entry.Source, "user", StringComparison.OrdinalIgnoreCase) Then suffix &= "  [用户]"
                    Dim child As New ModernContextMenu.ModernMenuItem(entry.DisplayName & suffix) With {
                        .Checked = String.Equals(entry.Id, If(interpolation, _config.InterpModel, _config.Model), StringComparison.OrdinalIgnoreCase),
                        .CloseOnClick = True
                    }
                    AddHandler child.Click, Sub(sender, e) SetCatalogSelection(selectedEntry, interpolation, saveConfig:=True)
                    tooltipEntries(child) = ModelTooltipText(selectedEntry, interpolation)
                    submenu.Items.Add(child)
                Next
                root.Items.Add(New ModernContextMenu.ModernMenuItem(group.Key) With {.SubMenu = submenu, .CloseOnClick = False})
            Next
            FitModelMenuBelowAnchor(root, anchor)
            If interpolation Then
                _interpModelMenu = root
            Else
                _modelMenu = root
            End If
            Dim tooltipController = New ModelMenuToolTipController(root, anchor, tooltipEntries)
            _modelMenuToolTipController = tooltipController
            AddHandler root.MenuClosed,
                Sub(sender As Object, e As EventArgs)
                    If Object.ReferenceEquals(_modelMenuToolTipController, tooltipController) Then
                        _modelMenuToolTipController = Nothing
                    End If
                    tooltipController.Close()
                End Sub
            tooltipController.Start()
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
                                                 color As Color) As LakeTextLabel
            Return New LakeTextLabel() With {
                .Text = text, .ForeColor = color, .BackColor = Color.Transparent,
                .Font = New Font("Microsoft YaHei UI", fontSize, style),
                .TextAlign = ContentAlignment.MiddleLeft, .AutoSize = False
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
            Dim layout As New ModernPanel With {
                .Margin = New Padding(0, 0, rightMargin, 0),
                .Padding = Padding.Empty,
                .BackColor = Color.Transparent,
                .BackColor1 = Color.Transparent,
                .BorderSize = 0
            }
            Dim label As LakeTextLabel = CreateTextLabel(caption, 9.0F, FontStyle.Regular, UiTextMuted)
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

        Private Shared Function CreateOfficialCaption(text As String, Optional color As Color = Nothing) As LakeTextLabel
            Dim actualColor = If(color = Nothing, UiTextMuted, color)
            Dim label = CreateTextLabel(text, 9.0F, FontStyle.Regular, actualColor)
            label.Dock = DockStyle.Fill
            label.Margin = Padding.Empty
            Return label
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

        ''' <summary>插件确认提示统一使用 LakeUI 5.1 消息框；系统文件选择器仍保留原生对话框。</summary>
        Friend Shared Function ShowLakeConfirm(owner As IWin32Window, prompt As String, title As String,
                                                Optional defaultYes As Boolean = False) As Boolean
            Dim buttons As New List(Of ExMsgBoxModule.ExMsgBoxButton)()
            If defaultYes Then
                buttons.Add(New ExMsgBoxModule.ExMsgBoxButton("是", True))
                buttons.Add(New ExMsgBoxModule.ExMsgBoxButton("否", False))
            Else
                buttons.Add(New ExMsgBoxModule.ExMsgBoxButton("否", False))
                buttons.Add(New ExMsgBoxModule.ExMsgBoxButton("是", True))
            End If
            Dim result = ExMsgBoxModule.ExMsgBox(prompt, buttons, title, 0, owner)
            Return result = If(defaultYes, 0, 1)
        End Function

        Friend Shared Sub ShowLakeInfo(owner As IWin32Window, prompt As String, title As String)
            Dim buttons As New List(Of ExMsgBoxModule.ExMsgBoxButton) From {
                New ExMsgBoxModule.ExMsgBoxButton("确定", True)
            }
            ExMsgBoxModule.ExMsgBox(prompt, buttons, title, 0, owner)
        End Sub

        Private Shared Sub ConfigureCombo(combo As ModernComboBox)
            ' AutoSize=False + 最小高度：下拉框高度完全由所在单元格决定且不小于箭头区域，
            ' 与宿主一致（宿主下拉框固定 30px 高、Dock=Fill、Overlay 下拉）。
            combo.AutoSize = False
            combo.MinimumSize = New Size(0, 32)
            combo.Dock = DockStyle.Fill
            combo.DropDownMode = ModernComboBox.DropDownDisplayMode.Overlay
            combo.Font = New Font("Microsoft YaHei UI", 10.0F)
            combo.ForeColor = UiText
            combo.WaterTextForeColor = UiTextMuted
            ' 组合框右侧由 LakeUI 固定保留箭头区域；缩小左右内边距，避免窄列中
            ' 选项文本在箭头前被截断，同时仍保留足够的视觉留白。
            combo.Padding = New Padding(6, 0, 6, 0)
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
            ' 与 3FUI 的选项型下拉框一致：只能选择既有项目，不能自由修改文本。
            combo.Editable = False
            combo.MaxDropDownItems = 12
            combo.DropDownBackColor = Color.FromArgb(48, 48, 48)
            combo.DropDownBorderColor = Color.Transparent
            combo.DropDownHoverColor = UiSurfaceHover
            combo.DropDownSelectedColor = Color.FromArgb(80, UiAccent)
            combo.DropDownSelectedForeColor = UiText
            combo.DropDownScrollBarColor = UiAccent
            combo.DropDownScrollBarTrackColor = Color.Transparent
        End Sub

        Private Shared Sub ConfigureModelSelector(combo As ModernComboBox)
            ConfigureCombo(combo)
            ' 模型框的 DropDownOpened 会立即关闭 LakeUI 原生列表并打开自定义模型菜单。
            ' LakeUI 默认的 300ms Overlay 关闭动画会把当前模型项短暂绘制在锚点上，
            ' 因此这里关闭原生动画，避免自定义菜单出现前闪出一层浅灰蓝色模型框。
            combo.DropDownAnimationDuration = 0
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
            Dim root As New ModernGridPanel With {
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
            root.AddAt(_tabs, 0, 0)

            Dim sectionStatus As New ModernGridPanel With {
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
            sectionStatus.AddAt(_lblStatus, 0, 0)
            _btnCheckUpdates.Text = "检查更新 v" & PluginVersion.Current
            _btnCheckUpdates.Dock = DockStyle.Fill
            _btnCheckUpdates.AutoSize = False
            _btnCheckUpdates.Margin = New Padding(12, 4, 0, 4)
            ConfigureSecondaryButton(_btnCheckUpdates)
            AddHandler _btnCheckUpdates.Click, AddressOf OnCheckUpdates
            sectionStatus.AddAt(_btnCheckUpdates, 2, 0)
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
            sectionStatus.AddAt(_btnCleanArchives, 1, 0)
            root.AddAt(sectionStatus, 0, 1)
            ModernPanel1.Controls.Add(root)
            Controls.Add(ModernPanel1)
            AddHandler ClientSizeChanged, Sub(sender, e) SyncUpscaleRootBounds()
            AddHandler Layout, Sub(sender, e) SyncUpscaleRootBounds()
            QueueUpscaleRootBounds()
        End Sub

        ' ────────────────────────── 选项卡分栏 ──────────────────────────

        Private Shared Function BeginnerTutorialMarkdown() As String
            Return String.Join(Environment.NewLine, New String() {
                "# 使用教程",
                "",
                "如果你第一次使用视频超分或补帧，请按下面的顺序一步一步来。第一次不要同时打开所有功能，先把程序路径、模型和后端对应关系设置正确，再处理完整视频。",
                "",
                "## 先认识这几个概念",
                "- **视频超分**：把每一帧的宽和高放大，同时尝试补回纹理。例如 2x 是宽度和高度各变成 2 倍，最终像素数量约变成 4 倍；4x 的像素数量约是原来的 16 倍，所以更慢、更吃显存。",
                "- **运动补帧**：在原视频帧之间生成新帧，让运动看起来更顺滑。2 倍不是增加 2 帧，而是让输出帧率约变成原来的 2 倍。",
                "- **推理后端**：决定模型由哪套运行引擎执行，不代表画质等级。模型文件格式必须与后端匹配。",
                "- **模型**：决定画面适合什么内容。真人、动漫、插画、去噪和时序视频模型不是一回事，模型名右侧的悬浮提示会告诉你用途、倍率和后端。",
                "",
                "## 第 1 步：连接处理程序",
                "1. 在 3FUI 打开本插件的 **超分工作台**。",
                "2. 点击 **选择处理程序**，选择插件目录下的 `videoenhancer\\videoenhancer.exe`。新布局通常是 `3FUI\\Plugin\\videoenhancer\\videoenhancer.exe`；不要选择 `videoenhancer.3fui.dll`、`FFmpegFreeUI.exe` 或模型文件。",
                "3. 开启 **插件总开关**。如果路径正确，状态区会开始检查运行环境；请等它结束，不要在检查过程中反复切换后端。",
                "4. 看到环境检查通过后，再开启 **视频超分** 或 **运动补帧**。如果检查失败，先看状态区的具体文字，不要直接换模型，因为程序可能连 Python、显卡驱动或后端都还没有找到。",
                "",
                "## 第 2 步：准备模型",
                "### 方法 A：下载内置模型",
                "1. 切换到 **模型下载** 页，点击 **刷新资源**。列表中的资源按用途分组，模型通常会标明 NCNN、CUDA/PyTorch、TensorRT 或 ONNX 所需格式。",
                "2. 第一次只下载一个模型，不要点击 **下载全部**。下载并安装完成后，回到 **超分工作台**，再打开对应模型下拉框；如果列表还没有刷新，重新开启该功能或再次刷新模型。",
                "3. 当前后端只会列出它能使用的模型。比如选了 ONNX，就应选择 ONNX 模型；选了 NCNN，就应选择带 `.param/.bin` 的模型目录；不能拿 PTH 文件硬套到 ONNX 或 NCNN。",
                "",
                "### 方法 B：导入自己的模型",
                "1. 切换到 **模型导入** 页，选择模型文件或直接拖入文件、文件夹或压缩包。",
                "2. 填写或确认任务类型、架构和倍率，然后点击 **预检并导入模型**。预检未通过时不要强行使用，先按错误文字修正格式、架构或倍率。",
                "3. 导入成功后回到工作台，选择与导入结果显示的后端相同的后端。用户模型会和内置模型一起出现在对应的架构菜单中，并标注 `[用户]`。",
                "",
                "## 第 3 步：只做视频超分",
                "### 3.1 先选推理后端",
                "- **NCNN (Vulkan)**：不依赖 CUDA，使用显卡驱动提供的 Vulkan；适合没有 NVIDIA/CUDA 环境、想先跑通流程的人。它要使用 Param-Bin 模型目录。",
                "- **CUDA (PyTorch)**：需要 NVIDIA 显卡和可用驱动，使用 PTH/PT/PKL 权重；模型覆盖较广。电脑有 NVIDIA 显卡时，第一次建议从它开始。",
                "- **TensorRT (NVIDIA)**：也需要 NVIDIA 显卡；通常适合已经确认 CUDA 能正常运行、希望进一步提高速度的人。第一次使用某个模型和输入尺寸时可能要构建 Engine，请耐心等待；Engine 与显卡和输入设置有关，不能随便从别的电脑复制。",
                "- **ONNX Runtime**：只能选择 ONNX 模型；适合已经下载或导入 ONNX 文件的情况。看到模型列表为空时，先检查文件格式和模型目录，不要把空列表当成模型损坏。",
                "- **FlashVSR / BasicVSR++**：这是利用连续视频帧的时序超分模型，不是普通单帧放大模型。它们更适合视频素材；BasicVSR++ 当前不能再叠加运动补帧。",
                "",
                "### 3.2 再选放大模型",
                "1. 点击 **放大模型**，先进入一级架构分类，再在第二级点击具体模型。鼠标停在具体模型上会显示简短说明；说明中的倍率是模型固定输出倍率，不需要另填。",
                "2. **真人、风景、普通网络视频**：先找 `RealESRGAN-General-x4v3` 做基准。它是通用 4x，不代表任何视频都必须放大 4 倍；如果最终只需要 2x，应优先选明确标注 2x 的模型或后续调整输出尺寸。",
                "3. **动漫视频**：先看 `RealESRGAN-AnimeVideoV3` 的 2x/3x/4x 版本；原片还清楚时从 2x 开始，低分辨率且确实需要大画面时再试 4x。",
                "4. **动漫截图、插画、线稿**：可从 AnimeJaNai 的 Balanced、Waifu2x 或 SPAN 类模型开始。Balanced 适合普通情况，Sharp1 更强调边缘，Noise 版本按噪声强弱选择。",
                "5. **只想去掉压缩噪声、不想放大**：选择 `DenoiseH264` 或 `DnCNN` 这类 1x 模型。1x 只修画面，不改变宽高；不要因为名称里有模型家族名就把它当成 2x/4x 放大模型。",
                "6. 如果不确定，先看悬浮提示，再根据素材的脸部、字幕、线稿、快速运动和重复纹理选择模型；不要只按某一帧是否更锐来判断。",
                "",
                "### 3.3 半精度和分块怎么选",
                "- **半精度推理**默认开启时，CUDA/TensorRT 会优先尝试 FP16，不兼容时自动回退 FP32。第一次使用保持开启即可；如果出现黑帧、花屏或模型报不支持，再关闭它强制 FP32。超分和补帧的半精度开关彼此独立。",
                "- **超分分块尺寸**先保持 `RVE 默认（0）`。如果任务报显存不足，按 `512 px → 384 px → 256 px → 128 px` 逐级尝试；数值越小越省显存，但需要更多块，速度会变慢。不要为了追求更大的数字而忽略显存。",
                "- FlashVSR 等不使用分块的后端会自动禁用这个选项；选项变灰是能力限制，不是故障。",
                "",
                "## 第 4 步：只做运动补帧",
                "### 4.1 选择补帧后端和模型",
                "- **NCNN**：适合不使用 CUDA 的 RIFE 模型目录。",
                "- **CUDA**：适合 RIFE、GMFSS 和 GIMM 的 PyTorch 权重；GMFSS/GIMM 在当前程序中会使用 CUDA。",
                "- **TensorRT**：当前主要用于 RIFE 权重自动构建 Engine；GIMM 和 GMFSS 不要强行选 TensorRT。",
                "- 第一次使用先选通用 RIFE 和 2 倍。RIFE heavy 会消耗更多显存和时间，适合普通版本不够稳定时再试；GMFSS AnimeRun 更适合动漫运动，GMFSS Base 适合先做通用基准。",
                "",
                "### 4.2 补帧倍率怎么选",
                "- `2 倍`：最稳妥的起点。例如输入 24 fps，输出约 48 fps；建议第一次使用。",
                "- `3 倍`：适合想比 2 倍更顺滑、又不想承担 4 倍开销的情况。",
                "- `4 倍`：适合高刷新率播放或慢动作需求，但计算量和运动错误风险都会增加。",
                "- `8 倍`：只建议在已经确认模型、素材和显存都稳定后使用；快速运动、遮挡和镜头切换更容易出现不自然的中间帧。",
                "选择倍率后不需要再去 3FUI 的视频参数里手动填写输出帧率；插件会把倍率交给处理程序。",
                "",
                "### 4.3 转场阈值怎么选",
                "- 先使用 **标准 4.0**。阈值越低，程序越敏感，越容易在镜头切换处跳过补帧；阈值越高，程序越不敏感，可能把切换前后的画面误认为连续运动。",
                "- 如果视频剪辑很多、转场处出现鬼影或两幅画面混在一起，改用 `2.0` 或 `3.5`；如果镜头很连续但不希望轻微变化被当成转场，可试 `6.0`。",
                "- `1.0` 很敏感，`8.0/10.0` 很宽松。它们不是画质档位，而是转场判断灵敏度；不要为了让画面更锐而调高阈值。",
                "",
                "### 4.4 动态光流尺度",
                "- 默认关闭即可。普通素材、固定机位和缓慢运动先不要改。",
                "- CUDA 下遇到大幅运动、镜头速度变化或运动尺度差异明显时，可以开启；它会增加计算量，不保证所有素材都更好。",
                "",
                "## 第 5 步：同时超分和补帧时怎么选",
                "1. 同时开启 **视频超分** 和 **运动补帧** 后，才会出现 **组合处理顺序**。第一次建议保持 **画质优先：先超分，再补帧**；先把画面放大，再在更大的画面上计算运动，便于观察细节。",
                "2. 如果显存或速度压力较大，可以试 **速度/算力优先：先补帧，再超分**。先在较小画面上补帧，再统一放大，通常更省算力，但要留意快速运动和细线。",
                "3. 两个阶段使用同一个后端时，程序会在一个 RVE 进程内逐帧传递，不会因为换顺序生成整段临时视频。小白优先使用同后端，例如 CUDA + CUDA。",
                "4. 两个阶段使用不同后端时，程序会生成隐藏的 `.videoenhancer-*.mkv` 无损中间文件。它需要额外磁盘空间，4K 或高帧率视频可能很大；任务结束后会自动清理，FFV1 只是阶段间传递格式，不是最终输出格式。",
                "5. BasicVSR++ 是时序超分特殊后端，当前不能与运动补帧组合；如果补帧开关变灰，这是设计上的能力限制。",
                "",
                "## 第 6 步：确认设置并加入队列",
                "1. 确认输入视频包含你关心的内容，例如人物、字幕、细线、快速运动或镜头转场。不同内容会影响模型和参数的选择。",
                "2. 确认插件总开关、需要的功能开关、处理程序路径、后端和模型都已选好。下拉框请用鼠标左键打开和选择；鼠标滚轮经过显示区域不会再悄悄改变单选值，打开后的列表仍可在列表区域滚动。",
                "3. 回到 3FUI 的文件列表，点击 **加入编码队列**。插件会接管这次任务并通过 `videoenhancer.exe` 执行；处理期间不要移动或删除模型、Python 后端和输入文件。",
                "4. 在 **实时预览** 查看处理中或已完成的画面。重点留意原片与输出的脸部、字幕、线稿、运动边缘、转场和颜色。",
                "",
                "## 常见问题",
                "### 模型列表是空的",
                "先确认 `videoenhancer.exe` 路径存在，再确认当前后端和模型格式匹配；下载或导入后回到工作台重新打开模型菜单。如果选了 BasicVSR++、FlashVSR、ONNX 等特殊后端，不要期待它显示其他后端的模型。",
                "",
                "### 任务报显存不足",
                "先把超分分块调小，关闭不必要的超分/补帧阶段，补帧倍率退回 2 倍；CUDA/TensorRT 还可以暂时关闭对应的半精度开关做稳定性对比。不要一边保留 4x/8x，一边把分块调到最大。",
                "",
                "### 画面过锐、噪声变多或细线消失",
                "这是模型与素材不匹配的常见表现。动漫线稿可换 Balanced、Noise0/1 或较温和的 2x 模型；噪声很重时才逐步使用 Noise2/3 或强模型。每次只改一个选项，方便判断变化原因。",
                "",
                "### 补帧出现鬼影或转场撕裂",
                "先把倍率降到 2 倍，转场阈值改为 2.0/3.5，并确认素材不是大量快速剪辑。再尝试另一个补帧模型；不要只把阈值调到最大，因为过高可能让程序跨越真正的镜头切换。",
                "",
                "### 第一次 TensorRT 很慢",
                "正常。TensorRT 可能正在为当前显卡、输入尺寸、倍率、分块和精度构建 Engine；后续相同设置会复用缓存。换显卡、分块、倍率或精度后，出现新的构建过程也是正常的。",
                "",
                "### 10-bit 输出是不是 10-bit 推理",
                "不是。当前 RVE 的 SDR 内部帧仍是 8-bit RGB；最终选择 10-bit 输出只影响编码格式，不等于模型以 10-bit 精度推理。PQ/HLG HDR 目前只允许 CUDA/PyTorch 或 TensorRT，其他后端会明确拒绝。"
            })
        End Function

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
            BuildMarkdownPage(_pageTutorial, BeginnerTutorialMarkdown())

            For Each page As ModernPanel In New ModernPanel() {
                _pageUpscale, _pagePreview, _pageDownloader,
                _pageConverter, _pageImporter, _pageTutorial
            }
                page.BackColor = Color.Transparent
                page.BackColor1 = Color.Transparent
                ' ModernPanel 默认带 1px 灰色边框；页面根节点属于 TabControl 内容面，必须显式关闭，
                ' 否则会在插件外沿绘制一圈亮线并遮住背景映射。
                page.BorderColor = Color.Transparent
                page.BorderSize = 0
                page.BorderRadius = 0
                page.BackgroundSource = ModernPanel1
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
            Dim host As New ModernPanel With {
                .Margin = Padding.Empty,
                .Padding = Padding.Empty,
                .BackColor = Color.Transparent,
                .BackColor1 = Color.Transparent,
                .BorderSize = 0
            }
            Dim line As New ModernPanel With {
                .BackColor = Color.Transparent,
                .BackColor1 = Color.FromArgb(58, 220, 220, 220),
                .BorderSize = 0
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
                                                        stateLabel As HtmlColorLabel,
                                                        Optional halfSwitch As LakeUI.BooleanSwitch = Nothing) As Control
            Dim titleLabel = CreateTextLabel(title, 12.0F, FontStyle.Regular, UiText)
            titleLabel.Margin = Padding.Empty
            titleLabel.TextAlign = ContentAlignment.MiddleLeft
            Dim titleWidth = Math.Max(84, TextRenderer.MeasureText(title, titleLabel.Font).Width + 4)
            Dim row As ModernHorizontalPanel
            Dim halfLabel As LakeTextLabel = Nothing
            If halfSwitch Is Nothing Then
                row = New ModernHorizontalPanel(
                    CSng(titleWidth), 10.0F, 42.0F, -1.0F, 112.0F)
            Else
                halfLabel = CreateTextLabel("半精度推理", 11.0F, FontStyle.Regular, UiTextSecondary)
                halfLabel.AutoSize = False
                halfLabel.Dock = DockStyle.Fill
                halfLabel.TextAlign = ContentAlignment.MiddleCenter
                halfLabel.Margin = Padding.Empty
                Dim halfLabelWidth = Math.Max(108,
                    TextRenderer.MeasureText(halfLabel.Text, halfLabel.Font).Width + 14)
                row = New ModernHorizontalPanel(
                    CSng(titleWidth), 10.0F, 42.0F, 18.0F, CSng(halfLabelWidth), 8.0F, 42.0F, -1.0F, 112.0F)
            End If
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
            If halfSwitch Is Nothing Then
                row.AddColumn(descriptionLabel, 3)
                row.AddColumn(stateLabel, 4)
            Else
                halfSwitch.Anchor = AnchorStyles.None
                halfSwitch.Margin = Padding.Empty
                row.AddColumn(halfLabel, 4)
                row.AddColumn(halfSwitch, 6)
                row.AddColumn(descriptionLabel, 7)
                row.AddColumn(stateLabel, 8)
            End If
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

        ''' <summary>
        ''' 按 LakeUI V5 的显式 BackgroundSource 语义，为滚动页内的每个 GPU 控件
        ''' 注册同一个稳定背景源。LakeUI 的自动祖先取景不会注册坐标依赖，父级滚动
        ''' 改变控件屏幕坐标后，子表面可能继续显示滚动前的背景采样。
        ''' </summary>
        Private Shared Sub BindScrollableGpuBackgroundSources(root As Control, source As Control)
            If root Is Nothing OrElse source Is Nothing Then Return

            Dim provider = TryCast(root, D3D_IBackgroundSourceProvider)
            If provider IsNot Nothing Then
                Dim currentSource As Control = Nothing
                If Not provider.TryGetBackgroundSource(currentSource) OrElse currentSource Is Nothing Then
                    Dim sourceProperty = root.GetType().GetProperty(
                        "BackgroundSource", BindingFlags.Instance Or BindingFlags.Public)
                    If sourceProperty IsNot Nothing AndAlso sourceProperty.CanWrite AndAlso
                       sourceProperty.PropertyType.IsAssignableFrom(source.GetType()) Then
                        sourceProperty.SetValue(root, source)
                    End If
                End If
            End If

            For Each child As Control In root.Controls
                BindScrollableGpuBackgroundSources(child, source)
            Next
        End Sub

        ''' <summary>让超分页的固定内容根节点跟随宿主实际宽度变化。</summary>
        Private Sub SyncUpscaleRootBounds()
            Dim root = _upscaleRoot
            If root Is Nothing OrElse root.IsDisposed OrElse
               _pageUpscale Is Nothing OrElse _pageUpscale.IsDisposed Then Return
            ' LakeUI 5.x 的 ModernTabControl 在宿主完成 Dock 布局前可能暂时保留
            ' 页面旧 ClientSize；同时取页面、TabControl 和插件背景根的可用宽度，
            ' 让后续测量能够跨过这个中间状态并覆盖到宿主真实视口。
            Dim availableWidth = Math.Max(_pageUpscale.Width, _pageUpscale.ClientSize.Width)
            availableWidth = Math.Max(availableWidth, Math.Max(_tabs.Width, _tabs.ClientSize.Width))
            If ModernPanel1 IsNot Nothing AndAlso Not ModernPanel1.IsDisposed Then
                availableWidth = Math.Max(availableWidth,
                    ModernPanel1.ClientSize.Width - ModernPanel1.Padding.Left - ModernPanel1.Padding.Right)
            End If
            Dim width = Math.Max(0, availableWidth - _pageUpscale.ScrollBarWidth - 2)
            If root.Left <> 0 OrElse root.Top <> 0 OrElse root.Width <> width OrElse root.Height <> 850 Then
                root.SetBounds(0, 0, width, 850)
            End If
        End Sub

        ''' <summary>在宿主完成 TabControl/插件面板布局后补一次宽度同步。</summary>
        Private Sub QueueUpscaleRootBounds()
            If _upscaleRoot Is Nothing OrElse _upscaleRootSyncPending OrElse IsDisposed Then Return
            If Not IsHandleCreated Then Return
            _upscaleRootSyncPending = True
            Try
                BeginInvoke(New Action(
                    Sub()
                        _upscaleRootSyncPending = False
                        SyncUpscaleRootBounds()
                    End Sub))
            Catch
                _upscaleRootSyncPending = False
            End Try
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

            ' 根容器保持固定内容高度；窗口较小时由页面滚动承载。
            ' 横向由一次性的宿主布局同步，避免 LakeUI 自定义 Dock/Anchor 布局重入。
            ' LakeUI 的自动祖先背景路径明确使用 registerDependency:=False；滚动改变
            ' 父级坐标时，自动取景不会让子级 GPU 表面失效。滚动根及其所有 V5 子控件
            ' 在页面构建完成后统一显式映射到 ModernPanel1，交给 LakeUI 注册坐标依赖。
            ' 宽度由 SyncUpscaleRootBounds 明确提交；不使用 Anchor.Right，
            ' 避免 WinForms 默认布局恢复创建时的窄尺寸。
            Dim root As New ModernPanel With {
                .Dock = DockStyle.None,
                .Anchor = AnchorStyles.Top Or AnchorStyles.Left,
                .AutoSize = False,
                .MinimumSize = New Size(0, 850),
                .Height = 850,
                .BackColor = Color.Transparent,
                .BackColor1 = Color.Transparent,
                .LayoutMode = ModernPanel.LayoutModeEnum.Absolute,
                .ScrollBarMode = ModernPanel.ScrollMode.None,
                .BorderSize = 0,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty,
                .AllowDrop = True
            }
            _upscaleRoot = root
            AddHandler _pageUpscale.ClientSizeChanged, Sub(sender, e) SyncUpscaleRootBounds()
            AddHandler _pageUpscale.SizeChanged, Sub(sender, e) SyncUpscaleRootBounds()
            AddHandler _pageUpscale.Layout, Sub(sender, e) SyncUpscaleRootBounds()
            AddHandler _pageUpscale.VisibleChanged, Sub(sender, e) SyncUpscaleRootBounds()
            AddHandler _tabs.ClientSizeChanged, Sub(sender, e) SyncUpscaleRootBounds()
            AddHandler _tabs.Layout, Sub(sender, e) SyncUpscaleRootBounds()
            AddHandler root.DragEnter, AddressOf OnImageDragEnter
            AddHandler root.DragDrop, AddressOf OnImageDragDrop
            ' 页面第一次构建时 ClientSize 可能还是宿主的初始窄尺寸；等 TabControl
            ' 完成布局后必须同步根面板宽度，否则所有内容会永久停留在左半边。
            SyncUpscaleRootBounds()

            ConfigureDpiSwitch(_switchMaster)
            _switchMaster.Checked = _config.Enabled
            AddHandler _switchMaster.CheckedChanged, AddressOf OnMasterSwitchChanged
            AddWorkbenchRow(root, BuildOfficialModeHeader(
                "插件总开关", "", _switchMaster, _lblMaster), 0, 40)

            Dim exeRow As New ModernHorizontalPanel(150.0F, 12.0F, -1.0F)
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
            ConfigureDpiSwitch(_switchUpscaleHalf)
            _switchUpscale.Checked = _config.UpscaleEnabled
            _switchUpscaleHalf.Checked = _config.UpscaleHalfPrecision
            _switchUpscale.Enabled = _config.Enabled
            AddHandler _switchUpscale.CheckedChanged, AddressOf OnUpscaleSwitchChanged
            AddHandler _switchUpscaleHalf.CheckedChanged, AddressOf OnUpscaleHalfSwitchChanged
            Dim upscaleHeader = BuildOfficialModeHeader(
                "视频超分", "", _switchUpscale, _lblSwitch, _switchUpscaleHalf)
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
            ConfigureModelSelector(_cmbModel)
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
            ConfigureDpiSwitch(_switchInterpHalf)
            _switchInterpHalf.Checked = _config.InterpHalfPrecision
            If String.Equals(_config.Backend, "basicvsrpp", StringComparison.OrdinalIgnoreCase) Then
                _config.InterpEnabled = False
                _config.InterpModel = ""
            End If
            SyncInterpSwitchFromConfig()
            AddHandler _switchInterp.CheckedChanged, AddressOf OnInterpSwitchChanged
            AddHandler _switchInterpHalf.CheckedChanged, AddressOf OnInterpHalfSwitchChanged
            Dim interpHeader = BuildOfficialModeHeader(
                "运动补帧", "", _switchInterp, _lblSwitchInterp, _switchInterpHalf)
            _cmbInterpBackend.WaterText = "选择后端…"
            ConfigureCombo(_cmbInterpBackend)
            _cmbInterpBackend.Items.Add("NCNN (Vulkan)")
            _cmbInterpBackend.Items.Add("CUDA (PyTorch)")
            _cmbInterpBackend.Items.Add("TensorRT (NVIDIA)")
            AddHandler _cmbInterpBackend.SelectedIndexChanged, AddressOf OnInterpBackendSelected
            _cmbInterp.WaterText = "选择补帧模型…"
            ConfigureModelSelector(_cmbInterp)
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
            ' 放大模型名称较长（例如 AnimeJaNai...-430K），给模型列保留更多文本区，
            ' 避免箭头区域遮住名称末尾；后端列仍足以完整显示 TensorRT (NVIDIA)。
            AddWorkbenchControl(root, upscaleBackendField, 187, 76, 0.0F, 0.38F, 0, -12)
            AddWorkbenchControl(root, upscaleModelField, 187, 76, 0.38F, 1.0F)
            AddWorkbenchControl(root, upscaleTileField, 263, 70, 0.0F, 0.46F, 0, -12)
            AddWorkbenchControl(root, tileHint, 263, 70, 0.46F, 1.0F)
            AddWorkbenchRow(root, interpHeader, 345, 38)
            ' 补帧后端的固定选项（尤其是 TensorRT (NVIDIA)）需要在箭头区域前保留
            ' 足够文本宽度；将窄列从 29% 调整到 34%，模型列仍保留主要空间。
            AddWorkbenchControl(root, interpBackendField, 383, 76, 0.0F, 0.34F, 0, -12)
            AddWorkbenchControl(root, interpModelField, 383, 76, 0.34F, 0.80F, 0, -12)
            AddWorkbenchControl(root, interpFactorField, 383, 76, 0.80F, 1.0F)
            AddWorkbenchControl(root, interpThresholdField, 459, 70, 0.0F, 0.34F, 0, -12)
            AddWorkbenchControl(root, interpFlowField, 459, 70, 0.34F, 0.80F, 0, -12)

            Dim orderRow As New ModernHorizontalPanel(150.0F, -54.0F, -46.0F) With {
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

            Dim imageInputRow As New ModernHorizontalPanel(
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

            Dim imageOutputRow As New ModernHorizontalPanel(170.0F, 12.0F, -1.0F)
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

            Dim imageOptionsRow As New ModernHorizontalPanel(
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

            Dim progressRow As New ModernHorizontalPanel(-1.0F, 16.0F, 300.0F)
            _imageProgress.Minimum = 0
            _imageProgress.Maximum = 1000
            _imageProgress.Dock = DockStyle.Fill
            _imageProgress.Margin = New Padding(0, 15, 0, 15)
            _imageProgress.TrackColor = Color.FromArgb(40, 220, 220, 220)
            _imageProgress.FillColor = UiAccent
            _imageProgress.FillGradientColor = Color.FromArgb(120, 204, 255)
            _imageProgress.FillGradientMode = ExcellentProgressBar.FillGradientModeEnum.WithinProgress
            _imageProgress.BackColor1 = Color.Transparent
            _imageProgress.BorderColor = Color.Transparent
            _imageProgress.BorderSize = 0
            _imageProgress.BorderRadius = 8
            _lblImageProgress.AutoSize = False
            _lblImageProgress.Dock = DockStyle.Fill
            _lblImageProgress.Margin = Padding.Empty
            _lblImageProgress.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _lblImageProgress.Text = "<font color=#888888>等待开始</font>"
            progressRow.AddColumn(_imageProgress, 0)
            progressRow.AddColumn(_lblImageProgress, 2)
            AddWorkbenchRow(root, progressRow, 808, 42)

            _pageUpscale.Controls.Add(root)
            BindScrollableGpuBackgroundSources(root, ModernPanel1)
            ' 为 LakeUI 覆盖式滚动条保留绘制带，避免子窗口覆盖父面板的 GPU 滚动条。
            SyncUpscaleRootBounds()
            UpdateModeStateLabels()
        End Sub

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
            args.Add("-upscale-precision") : args.Add(If(_config.UpscaleHalfPrecision, "auto", "float32"))

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

            Dim root As New ModernGridPanel With {
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
            root.AddAt(_lblPreviewTitle, 0, 0)

            _lblPreviewStatus.Text = "<font color=#888888>等待编码队列任务…</font>"
            _lblPreviewStatus.AutoSize = False
            _lblPreviewStatus.Dock = DockStyle.Fill
            _lblPreviewStatus.Margin = New Padding(0, 4, 0, 4)
            _lblPreviewStatus.Padding = New Padding(0, 2, 0, 2)
            _lblPreviewStatus.LineSpacing = 2
            _lblPreviewStatus.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft

            Dim taskRow As New ModernGridPanel With {
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
            taskRow.AddAt(_lblTask, 0, 0)
            taskRow.AddAt(_cmbTask, 1, 0)
            taskRow.AddAt(taskHint, 2, 0)
            root.AddAt(taskRow, 0, 1)
            root.AddAt(_lblPreviewStatus, 0, 2)

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
            _picPreview.ShowSelection = False
            _picPreview.BackColor1 = Color.Transparent
            _picPreview.BorderSize = 0
            _picPreview.BackgroundSource = ModernPanel1
            previewSurface.Controls.Add(_picPreview)
            root.AddAt(previewSurface, 0, 3)

            Dim footer As New ModernGridPanel With {
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
            footer.AddAt(_lblPreviewNote, 0, 0)
            footer.AddAt(_lblRate, 1, 0)
            footer.AddAt(_cmbRate, 2, 0)
            root.AddAt(footer, 0, 4)
            _pagePreview.Controls.Add(root)
        End Sub

        Private Sub BuildOfficialModelDownloadPage()
            _pageDownloader.Dock = DockStyle.Fill
            _pageDownloader.BackColor = Color.Transparent
            _pageDownloader.Padding = New Padding(0, 8, 0, 0)

            Dim root As New ModernGridPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 2,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            Dim header As New ModernGridPanel With {
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
            header.AddAt(CreateOfficialSectionHeading(
                "模型资源库", "从 ModelScope 获取模型与后端组件"), 0, 0)
            _btnDownloadPluginUpdate.Text = "下载全部"
            _btnDownloadPluginUpdate.Dock = DockStyle.Fill
            _btnDownloadPluginUpdate.AutoSize = False
            _btnDownloadPluginUpdate.Margin = New Padding(12, 7, 0, 7)
            ConfigureSecondaryButton(_btnDownloadPluginUpdate)
            AddHandler _btnDownloadPluginUpdate.Click, AddressOf OnDownloadAllClick
            header.AddAt(_btnDownloadPluginUpdate, 2, 0)
            _btnRefreshDownloads.Text = "刷新资源"
            _btnRefreshDownloads.Dock = DockStyle.Fill
            _btnRefreshDownloads.Margin = New Padding(12, 7, 0, 7)
            ConfigureSecondaryButton(_btnRefreshDownloads)
            AddHandler _btnRefreshDownloads.Click, Sub(sender, e) LoadDownloadModels(True)
            header.AddAt(_btnRefreshDownloads, 1, 0)
            root.AddAt(header, 0, 0)

            ConfigureDownloadList()
            root.AddAt(_downloadList, 0, 1)
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
                If Not ShowLakeConfirm(Me, message, "完整修复 Backend", defaultYes:=False) Then Return
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

            Dim root As New ModernGridPanel With {
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
            root.AddAt(CreateOfficialSectionHeading(
                "模型转换", "超分权重与 RIFE 补帧权重使用各自的 TensorRT 构建流程"), 0, 0)

            Dim inputRow As New ModernGridPanel With {
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
            inputRow.AddAt(_btnPickPth, 0, 0)
            inputRow.AddAt(CreateOfficialValueBox(_lblConvertInput), 2, 0)
            AddHandler inputRow.DragEnter, AddressOf OnConverterDragEnter
            AddHandler inputRow.DragDrop, AddressOf OnConverterDragDrop
            root.AddAt(inputRow, 0, 1)

            Dim outputRow As New ModernGridPanel With {
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
            outputRow.AddAt(outputCaption, 0, 0)
            outputRow.AddAt(CreateOfficialValueBox(_lblConvertOutput), 2, 0)
            root.AddAt(outputRow, 0, 2)

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
            root.AddAt(information, 0, 3)

            Dim actionRow As New ModernGridPanel With {
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
            actionRow.AddAt(_btnConvert, 0, 0)
            actionRow.AddAt(_lblConvertStatus, 1, 0)
            root.AddAt(actionRow, 0, 4)
            _pageConverter.Controls.Add(root)
        End Sub

        Private Sub BuildOfficialImporterPage()
            _pageImporter.Dock = DockStyle.Fill
            _pageImporter.BackColor = Color.Transparent
            _pageImporter.Padding = New Padding(0, 8, 0, 0)
            _pageImporter.AllowDrop = True
            AddHandler _pageImporter.DragEnter, AddressOf OnModelImportDragEnter
            AddHandler _pageImporter.DragDrop, AddressOf OnModelImportDragDrop

            Dim root As New ModernGridPanel With {
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
            root.AddAt(CreateOfficialSectionHeading(
                "模型导入", "安全预检架构、用途、倍率与后端能力，通过后安装到 models\User"), 0, 0)

            Dim sourceRow As New ModernPanel With {
                .Dock = DockStyle.Fill,
                .BackColor = Color.Transparent,
                .BackColor1 = Color.Transparent,
                .BorderSize = 0,
                .BackgroundSource = ModernPanel1,
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
            Dim sourceGap1 As New ModernPanel With {
                .Dock = DockStyle.Left, .Width = 10, .BackColor = Color.Transparent,
                .BackColor1 = Color.Transparent, .BorderSize = 0, .BackgroundSource = ModernPanel1
            }
            Dim sourceGap2 As New ModernPanel With {
                .Dock = DockStyle.Left, .Width = 12, .BackColor = Color.Transparent,
                .BackColor1 = Color.Transparent, .BorderSize = 0, .BackgroundSource = ModernPanel1
            }
            ' 按 3FUI Designer 的 Dock 顺序：Fill 先加，其他控件从右向左加入。
            sourceRow.Controls.Add(sourceValueBox)
            sourceRow.Controls.Add(sourceGap2)
            sourceRow.Controls.Add(_btnPickImportFolder)
            sourceRow.Controls.Add(sourceGap1)
            sourceRow.Controls.Add(_btnPickImportFile)
            root.AddAt(sourceRow, 0, 1)

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
                        "<font color=#888888>补帧仅接受能识别为 RIFE、GMFSS 或 GIMM 的权重；双击用户模型可修正能力，选中后按 Delete 或右键可删除。</font>"
            }
            root.AddAt(formats, 0, 2)

            ConfigureImportModelList()
            root.AddAt(_importModelList, 0, 3)

            Dim actionRow As New ModernPanel With {
                .Dock = DockStyle.Fill,
                .BackColor = Color.Transparent,
                .BackColor1 = Color.Transparent,
                .BorderSize = 0,
                .BackgroundSource = ModernPanel1,
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
            root.AddAt(actionRow, 0, 4)
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
                New UltraDetailListView.ListColumn("用户模型（双击修正 / Delete 删除）", 300),
                New UltraDetailListView.ListColumn("架构", 150),
                New UltraDetailListView.ListColumn("用途", 110),
                New UltraDetailListView.ListColumn("倍率", 70),
                New UltraDetailListView.ListColumn("后端", 210),
                New UltraDetailListView.ListColumn("格式", 100)
            })
            AddHandler _importModelList.ItemDoubleClick, AddressOf OnImportModelDoubleClick
            AddHandler _importModelList.KeyDown, AddressOf OnImportModelListKeyDown
            AddHandler _importModelList.PreviewKeyDown, AddressOf OnImportModelListPreviewKeyDown
            AddHandler _importModelList.MouseDown, AddressOf OnImportModelListMouseDown
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

        Private Sub OnImportModelListPreviewKeyDown(sender As Object, e As PreviewKeyDownEventArgs)
            If e.KeyCode = Keys.Delete Then e.IsInputKey = True
        End Sub

        Private Sub OnImportModelListKeyDown(sender As Object, e As KeyEventArgs)
            If e.KeyCode <> Keys.Delete OrElse e.Modifiers <> Keys.None Then Return
            e.Handled = True
            e.SuppressKeyPress = True
            Dim item = _importModelList.SelectedItem
            Dim model = TryCast(If(item Is Nothing, Nothing, item.Tag), UserModelItem)
            If model Is Nothing Then Return
            DeleteUserModelWithConfirmation(model)
        End Sub

        Private Sub OnImportModelListMouseDown(sender As Object, e As MouseEventArgs)
            If e.Button <> MouseButtons.Right OrElse _modelImportBusy Then Return
            Dim item = _importModelList.GetItemAt(e.X, e.Y)
            Dim model = TryCast(If(item Is Nothing, Nothing, item.Tag), UserModelItem)
            If model Is Nothing Then
                CloseUserModelContextMenu()
                Return
            End If
            Dim index = _importModelList.Items.IndexOf(item)
            If index >= 0 Then _importModelList.SelectedIndex = index
            ShowUserModelContextMenu(model, e.Location)
        End Sub

        Private Sub CloseUserModelContextMenu()
            Dim menu = _userModelContextMenu
            _userModelContextMenu = Nothing
            _contextUserModel = Nothing
            If menu IsNot Nothing Then
                Try
                    menu.Close()
                Catch
                End Try
            End If
        End Sub

        Private Sub ShowUserModelContextMenu(model As UserModelItem, location As Point)
            If model Is Nothing Then Return
            CloseUserModelContextMenu()
            Dim menu As New ModernContextMenu()
            ConfigureModelMenu(menu, reserveIconColumn:=False)
            Dim deleteItem As New ModernContextMenu.ModernMenuItem("删除用户模型") With {
                .CloseOnClick = True,
                .ForeColor = UiDanger
            }
            AddHandler deleteItem.Click,
                Sub(sender As Object, e As EventArgs)
                    Dim target = _contextUserModel
                    CloseUserModelContextMenu()
                    DeleteUserModelWithConfirmation(target)
                End Sub
            menu.Items.Add(deleteItem)
            _contextUserModel = model
            _userModelContextMenu = menu
            menu.Show(_importModelList, location)
        End Sub

        Private Async Sub DeleteUserModelWithConfirmation(model As UserModelItem)
            If model Is Nothing OrElse _modelImportBusy Then Return
            Dim question = "确定删除用户模型“" & model.DisplayName & "”？" & Environment.NewLine &
                "将删除 models\\User 中的安装文件/目录和能力清单记录。" & Environment.NewLine &
                "删除后无法通过本页恢复。" & Environment.NewLine & Environment.NewLine &
                "路径：" & model.RelativePath
            If Not ShowLakeConfirm(Me, question, "删除用户模型", defaultYes:=False) Then Return

            Dim exePath = PluginConfig.ResolveInstalledExePath(_config.ExePath)
            If String.IsNullOrWhiteSpace(exePath) OrElse Not File.Exists(exePath) Then
                _lblImportStatus.Text = "<font color=#EB5D5D>删除失败：找不到 videoenhancer.exe</font>"
                Return
            End If
            _modelImportBusy = True
            _lblImportStatus.Text = "<font color=#479CFF>正在删除用户模型…</font>"
            Try
                Dim errorText = Await Task.Run(Function() RunUserModelDelete(exePath, model.Id))
                If errorText.Length > 0 Then
                    _lblImportStatus.Text = "<font color=#EB5D5D>删除失败：" & EscapeHtml(errorText) & "</font>"
                    ShowStatus("用户模型删除失败：" & errorText, True)
                    Return
                End If
                RefreshModels()
                LoadUserModels()
                _lblImportStatus.Text = "<font color=#3FCD87>已删除用户模型，并刷新工作台模型列表</font>"
                ShowStatus("已删除用户模型：" & model.DisplayName, False)
            Catch ex As Exception
                _lblImportStatus.Text = "<font color=#EB5D5D>删除失败：" & EscapeHtml(ex.Message) & "</font>"
                ShowStatus("用户模型删除失败：" & ex.Message, True)
            Finally
                _modelImportBusy = False
            End Try
        End Sub

        Private Shared Function RunUserModelDelete(exePath As String, id As String) As String
            Try
                Dim psi As New ProcessStartInfo With {
                    .FileName = exePath, .UseShellExecute = False, .RedirectStandardOutput = True,
                    .RedirectStandardError = True, .CreateNoWindow = True,
                    .StandardOutputEncoding = Encoding.UTF8, .StandardErrorEncoding = Encoding.UTF8
                }
                psi.ArgumentList.Add("--delete-user-model")
                psi.ArgumentList.Add(id)
                Using child = Diagnostics.Process.Start(psi)
                    If child Is Nothing Then Return "无法启动用户模型删除进程"
                    Dim stdout = child.StandardOutput.ReadToEnd()
                    Dim stderr = child.StandardError.ReadToEnd()
                    If Not child.WaitForExit(30000) Then
                        Try
                            child.Kill(entireProcessTree:=True)
                        Catch
                        End Try
                        Return "用户模型删除进程超时"
                    End If
                    If child.ExitCode <> 0 Then Return LastNonEmptyLine(If(String.IsNullOrWhiteSpace(stderr), stdout, stderr))
                End Using
                Return ""
            Catch ex As Exception
                Return ex.Message
            End Try
        End Function

        Private Sub ShowUserModelCapabilityEditor(model As UserModelItem)
            Using dialog As New Form With {
                .Text = "修正模型能力 - " & model.DisplayName,
                .StartPosition = FormStartPosition.CenterParent,
                .FormBorderStyle = FormBorderStyle.None,
                .MaximizeBox = False,
                .MinimizeBox = False,
                .ShowInTaskbar = False,
                .BackColor = Color.FromArgb(24, 24, 24),
                .ForeColor = UiText,
                .ClientSize = New Size(720, 570),
                .Font = New Font("Microsoft YaHei UI", 9.0F)
            }
                Dim chrome As New ThisIsYourWindow With {
                    .BorderColor = Color.FromArgb(72, 72, 72),
                    .BorderSize = 1,
                    .CaptionBackColor = Color.FromArgb(30, 30, 30),
                    .CaptionOverlayColor = Color.Transparent,
                    .TitleForeColor = UiText,
                    .CaptionHeight = 34,
                    .ShowFullScreenButton = False
                }
                Dim grid As New ModernGridPanel With {
                    .Dock = DockStyle.Fill,
                    .ColumnCount = 2,
                    .RowCount = 11,
                    .Padding = New Padding(20, 16, 20, 16),
                    .BackColor = Color.Transparent,
                    .BackColor1 = Color.Transparent,
                    .BackgroundSource = ModernPanel1,
                    .BorderSize = 0
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
                        Dim label = CreateTextLabel(text, 9.0F, FontStyle.Regular, UiTextSecondary)
                        label.Dock = DockStyle.None
                        label.Margin = Padding.Empty
                        label.TextAlign = ContentAlignment.MiddleLeft
                        grid.AddAt(label, 0, row)
                    End Sub
                Dim readonlyValue As Func(Of String, LakeTextLabel) =
                    Function(text) CreateTextLabel(text, 9.0F, FontStyle.Regular, UiTextMuted)

                Dim architectureBox As New ModernTextBox With {.Text = model.Architecture}
                Dim purposeBox As New ModernTextBox With {.Text = model.Purpose}
                ConfigureOfficialTextBox(architectureBox, "模型架构")
                ConfigureOfficialTextBox(purposeBox, "模型用途")

                Dim scaleBox As New ModernNumericUpDown With {
                    .Minimum = 1D,
                    .Maximum = 16D,
                    .Value = Math.Max(1, Math.Min(16, model.Scale)),
                    .Increment = 1D,
                    .DecimalPlaces = 0,
                    .Editable = True,
                    .Dock = DockStyle.Left,
                    .Width = 150,
                    .Height = 34,
                    .BackColor1 = UiSurfaceRaised,
                    .ForeColor = UiText,
                    .BorderColor = Color.Transparent,
                    .BorderSize = 0,
                    .BorderRadius = 8
                }
                Dim multipleBox As New ModernNumericUpDown With {
                    .Minimum = 1D,
                    .Maximum = 1024D,
                    .Value = Math.Max(1, Math.Min(1024, model.InputMultiple)),
                    .Increment = 1D,
                    .DecimalPlaces = 0,
                    .Editable = True,
                    .Dock = DockStyle.Left,
                    .Width = 150,
                    .Height = 34,
                    .BackColor1 = UiSurfaceRaised,
                    .ForeColor = UiText,
                    .BorderColor = Color.Transparent,
                    .BorderSize = 0,
                    .BorderRadius = 8
                }
                If model.Task.Equals("interpolation", StringComparison.OrdinalIgnoreCase) OrElse
                    model.Task.Equals("restoration", StringComparison.OrdinalIgnoreCase) Then scaleBox.Enabled = False

                Dim backendPanel As New ModernPanel With {
                    .Dock = DockStyle.Fill,
                    .BackColor = Color.Transparent,
                    .BackColor1 = Color.Transparent,
                    .BackgroundSource = ModernPanel1,
                    .BorderSize = 0,
                    .LayoutMode = ModernPanel.LayoutModeEnum.Flow,
                    .FlowDirection = ModernPanel.FlowDirectionEnum.LeftToRight,
                    .WrapContents = True,
                    .ScrollBarMode = ModernPanel.ScrollMode.None,
                    .Padding = New Padding(0, 2, 0, 2)
                }
                Dim backendChecks As New List(Of ModernCheckBox)()
                Dim backendNames = New String() {"ncnn", "cuda", "tensorrt", "onnx", "flashvsr", "basicvsrpp"}
                For Each backendName In backendNames
                    Dim check = New ModernCheckBox With {
                        .Text = backendName,
                        .Checked = model.Backends.Contains(backendName, StringComparer.OrdinalIgnoreCase),
                        .AutoSize = False,
                        .Width = 150,
                        .Height = 32,
                        .Margin = New Padding(0, 2, 8, 2),
                        .ForeColor = UiText,
                        .BackColor = Color.Transparent,
                        .BackgroundSource = ModernPanel1,
                        .ClickAnywhere = True
                    }
                    backendChecks.Add(check)
                    backendPanel.Controls.Add(check)
                Next

                addCaption("模型文件", 0) : grid.AddAt(readonlyValue(model.RelativePath), 1, 0)
                addCaption("格式 / SHA-256", 1) : grid.AddAt(readonlyValue(model.Format.ToUpperInvariant() & "  ·  " & model.Sha256), 1, 1)
                addCaption("任务类别（只读）", 2) : grid.AddAt(readonlyValue(DisplayUserModelPurpose(model) & "  [" & model.Task & "]"), 1, 2)
                addCaption("架构", 3) : grid.AddAt(architectureBox, 1, 3)
                addCaption("用途", 4) : grid.AddAt(purposeBox, 1, 4)
                addCaption("模型倍率", 5) : grid.AddAt(scaleBox, 1, 5)
                addCaption("输入尺寸倍数", 6) : grid.AddAt(multipleBox, 1, 6)
                Dim sizeRequirement = If(model.MinimumSize > 0, "最小 " & model.MinimumSize.ToString() & " px", "无额外最小值") &
                    If(model.Square, "；要求正方形", "") & If(String.IsNullOrWhiteSpace(model.Tiling), "", "；切片 " & model.Tiling)
                addCaption("其他尺寸要求（只读）", 7) : grid.AddAt(readonlyValue(sizeRequirement), 1, 7)
                addCaption("可用后端", 8) : grid.AddAt(backendPanel, 1, 8)

                Dim hint = readonlyValue("保存前会校验模型格式、架构和后端组合；错误组合不会写入能力清单。")
                hint.ForeColor = UiTextMuted
                grid.AddAt(hint, 0, 9)
                grid.SetColumnSpan(hint, 2)
                Dim buttons As New ModernPanel With {
                    .Dock = DockStyle.Fill,
                    .BackColor = Color.Transparent,
                    .BackColor1 = Color.Transparent,
                    .BackgroundSource = ModernPanel1,
                    .BorderSize = 0,
                    .LayoutMode = ModernPanel.LayoutModeEnum.Flow,
                    .FlowDirection = ModernPanel.FlowDirectionEnum.LeftToRight,
                    .WrapContents = False
                }
                Dim saveButton As New ModernButton With {.Text = "保存修正", .Size = New Size(130, 40), .Margin = New Padding(8, 4, 0, 4)}
                Dim cancelButton As New ModernButton With {.Text = "取消", .Size = New Size(100, 40), .Margin = New Padding(8, 4, 0, 4)}
                ConfigurePrimaryButton(saveButton)
                ConfigureSecondaryButton(cancelButton)
                AddHandler cancelButton.Click, Sub(sender, args) dialog.Close()
                AddHandler saveButton.Click,
                    Sub(sender, args)
                        Dim selectedBackends = backendChecks.Where(Function(check) check.Checked).
                            Select(Function(check) check.Text).ToArray()
                        Dim errorText = UpdateUserModelCapabilities(model.Id, architectureBox.Text, purposeBox.Text,
                            CInt(Math.Round(scaleBox.Value)), CInt(Math.Round(multipleBox.Value)), selectedBackends)
                        If errorText.Length > 0 Then
                            ShowLakeInfo(dialog, errorText, "能力修正失败")
                            Return
                        End If
                        dialog.DialogResult = DialogResult.OK
                        dialog.Close()
                    End Sub
                buttons.Controls.Add(saveButton)
                buttons.Controls.Add(cancelButton)
                grid.AddAt(buttons, 0, 10)
                grid.SetColumnSpan(buttons, 2)
                dialog.Controls.Add(grid)
                chrome.Attach(dialog)
                Try
                    If dialog.ShowDialog(Me) = DialogResult.OK Then
                        LoadUserModels()
                        RefreshModels()
                        _lblImportStatus.Text = "<font color=#3FCD87>已保存能力修正，并刷新工作台模型列表</font>"
                    End If
                Finally
                    chrome.Detach(dialog)
                End Try
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

        Private Sub BuildMarkdownPage(page As ModernPanel, markdown As String)
            page.Dock = DockStyle.Fill
            page.BackColor = Color.Transparent
            page.BackColor1 = Color.Transparent
            page.BackgroundSource = ModernPanel1
            page.BorderSize = 0
            page.Padding = New Padding(0, 8, 0, 0)
            _markdownSources(page) = If(markdown, "")
        End Sub

        Private Sub EnsureMarkdownPage(page As ModernPanel)
            If page Is Nothing OrElse _markdownReady.Contains(page) Then Return
            Dim markdown As String = ""
            If Not _markdownSources.TryGetValue(page, markdown) Then Return
            Dim viewer As New MarkDownViewer With {
                .Dock = DockStyle.Fill,
                .Margin = Padding.Empty,
                .Padding = New Padding(10, 8, 10, 12),
                .BackColor = Color.Transparent,
                .BackgroundSource = ModernPanel1,
                .BorderSize = 0
            }
            viewer.ScrollBarWidth = 10
            viewer.ScrollBarTrackColor = Color.FromArgb(18, 18, 18)
            viewer.ScrollBarColor = Color.FromArgb(72, 72, 72)
            viewer.ScrollBarHoverColor = Color.FromArgb(104, 104, 104)
            viewer.HeadingColor = UiText
            viewer.BoldColor = UiText
            viewer.LinkColor = UiAccent
            viewer.CodeBackColor = Color.FromArgb(44, 44, 48)
            viewer.CodeBlockBackColor = Color.FromArgb(32, 34, 38)
            viewer.CodeBlockForeColor = UiTextSecondary
            AddHandler viewer.LinkClicked,
                Sub(sender, args)
                    Try
                        If args Is Nothing OrElse String.IsNullOrWhiteSpace(args.LinkText) Then Return
                        Process.Start(New ProcessStartInfo With {
                            .FileName = args.LinkText,
                            .UseShellExecute = True})
                    Catch
                        ' 外部链接无法打开时不影响教程页面和插件主流程。
                    End Try
                End Sub
            viewer.SetMarkdownImmediate(markdown)
            page.Controls.Add(viewer)
            _markdownReady.Add(page)
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
                CloseModelMenuToolTip()
                CloseUserModelContextMenu()
                StopEnvironmentCheck(5000)
                ' LakeUI 5.x 在 TabControl 隐藏时会重新显示当前绑定页。
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
                        _picPreview.Image = Nothing
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
            _syncingUpscaleHalfSwitch = True
            _switchUpscaleHalf.Checked = _config.UpscaleHalfPrecision
            _syncingUpscaleHalfSwitch = False
            ' 补帧开关：仅主开关开启时可操作
            If String.Equals(_config.Backend, "basicvsrpp", StringComparison.OrdinalIgnoreCase) Then
                _config.InterpEnabled = False
                _config.InterpModel = ""
            End If
            SyncInterpSwitchFromConfig()
            _syncingInterpHalfSwitch = True
            _switchInterpHalf.Checked = _config.InterpHalfPrecision
            _syncingInterpHalfSwitch = False
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
            Dim upscalePrecisionBackend = String.Equals(_config.Backend, "cuda", StringComparison.OrdinalIgnoreCase) OrElse
                String.Equals(_config.Backend, "tensorrt", StringComparison.OrdinalIgnoreCase)
            _switchUpscaleHalf.Enabled = _config.Enabled AndAlso _config.UpscaleEnabled AndAlso upscalePrecisionBackend
            Dim interpPrecisionBackend = String.Equals(_config.InterpBackend, "cuda", StringComparison.OrdinalIgnoreCase) OrElse
                String.Equals(_config.InterpBackend, "tensorrt", StringComparison.OrdinalIgnoreCase)
            _switchInterpHalf.Enabled = _config.Enabled AndAlso _config.InterpEnabled AndAlso interpPrecisionBackend
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
