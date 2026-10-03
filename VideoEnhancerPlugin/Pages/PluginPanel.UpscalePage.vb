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
Imports LakeUI

Namespace videoenhancer

    Public Partial Class PluginPanel

        Private Const UpscaleContentHeight As Integer = 744
        Private _environmentCheckCompleted As Boolean = False
        Private ReadOnly _environmentCheckSync As New Object()
        Private _environmentCheckCancellation As System.Threading.CancellationTokenSource
        Private _environmentCheckTask As Task
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
        Private ReadOnly _switchRtxHdr As New LakeUI.BooleanSwitch()
        ' RTX HDR 状态使用无换行的 LakeUI 文本按钮，避免 HtmlColorLabel 按空格拆成两行。
        Private ReadOnly _lblSwitchRtxHdr As New ModernButton()
        Private ReadOnly _cmbRtxHdrMode As New WheelLockedComboBox()
        Private ReadOnly _numRtxHdrContrast As New RtxHdrNumericUpDown()
        Private ReadOnly _numRtxHdrSaturation As New RtxHdrNumericUpDown()
        Private ReadOnly _numRtxHdrMiddleGray As New RtxHdrNumericUpDown()
        Private ReadOnly _numRtxHdrMaxLuminance As New RtxHdrNumericUpDown()
        Private ReadOnly _cmbOutputScale As New WheelLockedComboBox()
        Private _outputScaleField As Control
        Private _outputScaleHint As LakeTextLabel
        Private _syncingOutputScale As Boolean
        Private ReadOnly _cmbRtxTarget As New WheelLockedComboBox()
        Private ReadOnly _cmbRtxQuality As New WheelLockedComboBox()
        Private _upscaleModelField As Control
        Private _upscaleTileField As Control
        Private _upscaleTileHint As LakeTextLabel
        Private _rtxTargetField As Control
        Private _rtxQualityField As Control
        Private _rtxHdrContrastField As Control
        Private _rtxHdrSaturationField As Control
        Private _rtxHdrMiddleGrayField As Control
        Private _rtxHdrMaxLuminanceField As Control
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
        Private _syncingRtxHdrSwitch As Boolean = False
        Private _syncingRtxHdrParameters As Boolean = False
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

        Private _upscaleRoot As DpiLayoutPanel
        Private _upscaleRootSyncPending As Boolean
        Public Function TryEnable(exePath As String, Optional silent As Boolean = False) As Boolean
            Try
                Dim canonicalExe = PluginConfig.ResolveInstalledExePath()
                If Not Path.GetFullPath(exePath).Equals(
                    Path.GetFullPath(canonicalExe), StringComparison.OrdinalIgnoreCase) Then
                    If Not silent Then ShowStatus("处理程序路径固定为：" & canonicalExe, True)
                    Return False
                End If
                If Not File.Exists(exePath) Then
                    If Not silent Then
                        ShowStatus("videoenhancer.exe 不存在：" & exePath, True)
                    End If
                    Return False
                End If
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
                HostSettings.AlternativeProcessPath = ""
            Catch
            End Try
            _config.Enabled = False
            _config.Save()
            RefreshUi()
            ShowStatus("已停用：编码队列恢复为直接执行 ffmpeg", False)
            QueueHostParameterRefresh()
        End Sub

        ''' <summary>"插件总开关"切换：开 → 使用固定便携 EXE；关 → 停止对参数面板的 hook。</summary>
        Private Sub OnMasterSwitchChanged(sender As Object, e As EventArgs)
            If _syncingMaster Then
                Return
            End If
            If _switchMaster.Checked Then
                Dim exePath = _config.ExePath
                If Not File.Exists(exePath) Then
                    ShowStatus("固定位置缺少 videoenhancer.exe：" & exePath, True)
                    _syncingMaster = True
                    _switchMaster.Checked = False
                    _syncingMaster = False
                    Return
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

        Private Sub OnRtxHdrSwitchChanged(sender As Object, e As EventArgs)
            If _syncingRtxHdrSwitch Then Return
            If _switchRtxHdr.Checked AndAlso Not _config.Enabled Then
                _switchRtxHdr.Checked = False
                ShowStatus("请先开启「插件总开关」", True)
                Return
            End If
            _config.RtxHdrEnabled = _switchRtxHdr.Checked
            _config.Save()
            UpdateModeStateLabels()
            UpdateAdvancedControlState()
            UpdateHookState()
        End Sub

        Private Sub OnRtxHdrContrastChanged(sender As Object, e As EventArgs)
            If _syncingRtxHdrParameters Then Return
            _config.RtxHdrContrast = PluginConfig.ClampRtxHdrContrast(CInt(_numRtxHdrContrast.Value))
            _config.Save()
        End Sub

        Private Sub OnRtxHdrSaturationChanged(sender As Object, e As EventArgs)
            If _syncingRtxHdrParameters Then Return
            _config.RtxHdrSaturation = PluginConfig.ClampRtxHdrSaturation(CInt(_numRtxHdrSaturation.Value))
            _config.Save()
        End Sub

        Private Sub OnRtxHdrMiddleGrayChanged(sender As Object, e As EventArgs)
            If _syncingRtxHdrParameters Then Return
            _config.RtxHdrMiddleGray = PluginConfig.ClampRtxHdrMiddleGray(CInt(_numRtxHdrMiddleGray.Value))
            _config.Save()
        End Sub

        Private Sub OnRtxHdrMaxLuminanceChanged(sender As Object, e As EventArgs)
            If _syncingRtxHdrParameters Then Return
            _config.RtxHdrMaxLuminance = PluginConfig.ClampRtxHdrMaxLuminance(CInt(_numRtxHdrMaxLuminance.Value))
            _config.Save()
        End Sub

        Private Sub OnRtxTargetSelected(sender As Object, e As EventArgs)
            If _cmbRtxTarget.SelectedItem Is Nothing Then Return
            _config.RtxTarget = _cmbRtxTarget.SelectedItem.ToString().Split(" "c)(0).Trim()
            _config.Save()
        End Sub

        Private Sub OnRtxQualitySelected(sender As Object, e As EventArgs)
            If _cmbRtxQuality.SelectedIndex < 0 Then Return
            _config.RtxQuality = _cmbRtxQuality.SelectedIndex + 1
            _config.Save()
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
                (_config.UpscaleEnabled OrElse _config.InterpEnabled OrElse _config.RtxHdrEnabled OrElse HasEnabledSegmentedVideo())
            If wantHook Then
                If Not QueueHook.Install() Then
                    ShowStatus("未能挂载""加入编码队列""按钮，请确认 3FUI 版本兼容", True)
                    Return
                End If
                HostSettings.AlternativeProcessPath = _config.ExePath
                ShowStatus("已启用：编码队列将通过 videoenhancer.exe 中转执行", False)
            Else
                Try
                    QueueHook.Uninstall()
                    HostSettings.AlternativeProcessPath = ""
                Catch
                End Try
                ShowStatus("已停用：编码队列恢复为直接执行 ffmpeg", False)
            End If
            QueueHostParameterRefresh()
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

        ''' <summary>重新读取模型列表（启用 / 下拉重试共用）。</summary>
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
                         Dim catalog = ModelCatalogClient.RunModelCatalog(exePath, "--list-model-catalog", "-backend", backend)
                         Dim models As List(Of String) = Nothing
                         If catalog.Count = 0 Then
                             models = ModelCatalogClient.RunListModels(exePath, "--search-models", "-backend", backend)
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
                         Dim catalog = ModelCatalogClient.RunModelCatalog(exePath, "--list-interp-model-catalog", "-interp-backend", backend)
                         Dim models As List(Of String) = Nothing
                         If catalog.Count = 0 Then
                             models = ModelCatalogClient.RunListModels(exePath, "--list-interp-models", "-interp-backend", backend)
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

        ''' <summary>工作台滚动依赖 LakeUI 5.110 的公开渲染事务。</summary>
        Private Shared Function LakeUiScrollTransactionsAvailable() As Boolean
            Try
                Dim version = GetType(ModernPanel).Assembly.GetName().Version
                Return version IsNot Nothing AndAlso version.Major = 5 AndAlso version.Minor >= 110
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
                        "<font color=#C0C0C0>需要升级 3FUI/LakeUI 5.110 或更新的 5.x 版本后才能继续使用。</font>"
            }
            ModernPanel1.Controls.Add(message)
            Controls.Add(ModernPanel1)
        End Sub

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
            For Each group In catalog.GroupBy(Function(item) If(String.IsNullOrWhiteSpace(item.ArchitectureGroup), If(String.IsNullOrWhiteSpace(item.Architecture), "其他模型", item.Architecture), item.ArchitectureGroup)).
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
                    tooltipEntries(child) = ModelDescriptionProvider.ModelTooltipText(selectedEntry, interpolation)
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
            Dim tooltipController = New ModelMenuToolTipController(root, tooltipEntries)
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
            If saveConfig Then
                If interpolation Then
                    SaveInterpModelSelection(entry.Id)
                Else
                    _config.Model = entry.Id
                    _config.OutputScale = 0
                    _config.Save()
                End If
            End If
            SyncOutputScaleControls()
        End Sub

        Private Sub ApplyModelList(models As List(Of String))
            ' CLI 版本不一致或旧进程缓存时，从候选 models 目录补扫 TensorRT PTH/Engine。
            If models.Count = 0 AndAlso String.Equals(_config.Backend, "tensorrt", StringComparison.OrdinalIgnoreCase) Then
                Try
                    Dim dirs = New List(Of String) From {
                        Path.Combine(PluginConfig.ApplicationRoot, "models")
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
                    .Architecture = ModelDescriptionProvider.FallbackArchitecture(modelId),
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
                    .Architecture = ModelDescriptionProvider.FallbackArchitecture(modelId),
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
            _config.OutputScale = 0
            _config.Save()
            SyncOutputScaleControls()
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
            ElseIf backend = "rtxvsr" AndAlso _config.InterpEnabled Then
                _config.ProcessOrder = "interp-first"
            End If
            _config.Save()
            SyncInterpSwitchFromConfig()
            ' 切换后端后重新读取两个模型列表（CUDA 需要 .pth 模型；活动模式无 .pth 时由 Apply*List 自动回退）
            If backend <> "rtxvsr" Then RefreshUpscaleModels()
            RefreshInterpModels()
            UpdateModeStateLabels()
            UpdateProcessOrderState()
            UpdateAdvancedControlState()
            Dim modeText = If(backend = "rtxvsr",
                "RTX VSR（NVIDIA RTX Video）：无需模型；可选倍率或目标分辨率，与补帧组合时固定先补帧",
                If(backend = "basicvsrpp",
                "BasicVSR++（NVIDIA）：官方 x4 权重或 1x 优化目录；图片通过单帧视频桥处理",
                If(backend = "tensorrt",
                "TensorRT（NVIDIA）：超分与 RIFE 补帧均按实际输入尺寸自动构建 Engine",
                If(backend = "onnx",
                "ONNX Runtime：超分用 .onnx；补帧可独立选择 NCNN 或 CUDA",
                If(backend = "flashvsr",
                "FlashVSR（NVIDIA）：连续视频帧扩散超分；组合补帧会自动分两阶段",
                If(backend = "cuda",
                "CUDA（PyTorch）：超分用 models 下的权重，补帧用 Frame-Interpolation 下的权重",
                "NCNN（Vulkan）"))))))
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
            If text.Contains("RTX VSR") Then
                Return "rtxvsr"
            End If
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
                             PortableRuntime.ConfigureProcess(psi)
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
                                                      Optional rightMargin As Integer = UiColumnGap) As Control
            Dim layout As New DpiLayoutPanel With {
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
            editor.MinimumSize = New Size(0, UiControlHeight)
            editor.Margin = Padding.Empty
            layout.Controls.Add(label)
            layout.Controls.Add(editor)
            Dim arrange =
                Sub()
                    label.SetBounds(layout.ScaleX(2), 0, Math.Max(0, layout.ClientSize.Width - layout.ScaleX(4)), layout.ScaleY(UiFieldCaptionHeight))
                    editor.SetBounds(0, layout.ScaleY(UiFieldEditorTop), layout.ClientSize.Width, layout.ScaleY(UiControlHeight))
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
            button.BorderRadius = UiCornerRadius
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
            button.BorderRadius = UiCornerRadius
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
            ' 统一紧凑高度，保留字体与箭头的可读空间；下拉列表仍使用宿主 Overlay 模式。
            combo.AutoSize = False
            combo.MinimumSize = New Size(0, UiControlHeight)
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
            combo.BorderRadius = UiCornerRadius
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

        ''' <summary>配置 RTX HDR 数字控件：整数、可编辑，并保留 LakeUI 统一外观。</summary>
        Private Shared Sub ConfigureRtxHdrNumeric(control As ModernNumericUpDown,
                                                   minimum As Integer, maximum As Integer,
                                                   value As Integer, increment As Integer)
            ' LakeUI 文本内核会在 Value/DecimalPlaces 设置时按当时的控件宽度计算横向滚动偏移。
            ' 数字框尚未加入工作台时可能仍是很窄的默认尺寸；四位最大亮度会因此缓存偏移，
            ' 后续布局变宽后左对齐不会自动清零，导致启动时首位被裁掉（1000 显示为 000）。
            ' 先给控件一个足够容纳初始值的临时宽度，再设置文本相关属性，Dock=Fill 后仍使用最终布局宽度。
            control.AutoSize = False
            control.Font = New Font("Microsoft YaHei UI", 10.0F)
            control.Size = New Size(320, UiControlHeight)
            control.MinimumSize = New Size(0, UiControlHeight)
            control.Minimum = CDec(minimum)
            control.Maximum = CDec(maximum)
            control.Value = CDec(Math.Max(minimum, Math.Min(maximum, value)))
            control.Increment = CDec(increment)
            control.DecimalPlaces = 0
            control.Editable = True
            control.Dock = DockStyle.Fill
            ' 隐藏默认上下按钮并取消右侧预留，避免按钮覆盖最后几位数值；文本两侧保留明确内边距。
            control.ButtonAreaWidth = 1
            control.DividerSize = 0
            control.ButtonBackColor1 = Color.Transparent
            control.ButtonBackColor2 = Color.Transparent
            control.HoverButtonBackColor1 = Color.Transparent
            control.HoverButtonBackColor2 = Color.Transparent
            control.PressedButtonBackColor1 = Color.Transparent
            control.PressedButtonBackColor2 = Color.Transparent
            control.ArrowColor = Color.Transparent
            control.HoverArrowColor = Color.Transparent
            control.PressedArrowColor = Color.Transparent
            control.Padding = New Padding(10, 0, 10, 0)
            control.TextAlign = ModernNumericUpDown.TextAlignMode.Left
            control.BackColor1 = UiSurfaceRaised
            control.ForeColor = UiText
            control.BorderColor = Color.Transparent
            control.BorderSize = 0
            control.BorderRadius = UiCornerRadius
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

        ''' <summary>让超分页的固定内容根节点跟随宿主实际宽度变化。</summary>
        Private Sub SyncUpscaleRootBounds()
            Dim root = _upscaleRoot
            If root Is Nothing OrElse root.IsDisposed OrElse
               _pageUpscale Is Nothing OrElse _pageUpscale.IsDisposed Then Return
            ' 使用页面实际视口；取各层旧宽度的最大值会让缩小时的内容越过背景表面。
            ' 宿主 Dock 的中间尺寸在布局完成后的延迟同步中收敛。
            Dim availableWidth = _pageUpscale.ClientSize.Width
            Dim width = Math.Max(0, availableWidth - root.ScaleX(_pageUpscale.ScrollBarWidth + 2))
            Dim contentHeight = root.ScaleY(UpscaleContentHeight)
            ' ModernPanel 会在滚动时把子控件移动到负的 Top/Left。这里只能同步尺寸，
            ' 不能无条件把位置重置为 0，否则 LakeUI 会把当前位置重新记录为设计坐标，
            ' 下一次回到顶部时就会在内容上方留下一大片空白。
            Dim rootLeft As Integer = root.Left
            Dim rootTop As Integer = root.Top
            If _pageUpscale.VerticalScrollOffset <= 0 AndAlso rootTop <> 0 Then rootTop = 0
            If _pageUpscale.HorizontalScrollOffset <= 0 AndAlso rootLeft <> 0 Then rootLeft = 0
            If root.Left <> rootLeft OrElse root.Top <> rootTop OrElse root.Width <> width OrElse root.Height <> contentHeight Then
                root.SetBounds(rootLeft, rootTop, width, contentHeight)
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
                        If IsDisposed OrElse Disposing Then Return
                        SyncUpscaleRootBounds()
                        ' 拉伸期间 GPU 背景可能先于宿主的新尺寸提交；布局稳定后使整条背景链失效。
                        Dim source As Control = Nothing
                        If ModernPanel1.TryGetBackgroundSource(source) AndAlso source IsNot Nothing AndAlso Not source.IsDisposed Then
                            source.Invalidate()
                        End If
                        ModernPanel1.Invalidate(True)
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
            _pageUpscale.ScrollBarTrackColor = UiSurface
            _pageUpscale.ScrollBarThumbColor = UiScrollThumb
            _pageUpscale.ScrollBarThumbHoverColor = UiScrollThumbHover
            _pageUpscale.VerticalScrollStep = 48
            _pageUpscale.AllowDrop = True
            AddHandler _pageUpscale.DragEnter, AddressOf OnImageDragEnter
            AddHandler _pageUpscale.DragDrop, AddressOf OnImageDragDrop

            ' 根容器保持固定内容高度；窗口较小时由页面滚动承载。
            ' 横向由一次性的宿主布局同步，避免 LakeUI 自定义 Dock/Anchor 布局重入。
            ' 滚动根及其 V5 子控件使用稳定的 ModernPanel1 背景源。
            ' 位置变化和透明背景重新取景由 SmoothScrollPanel 的渲染事务统一提交。
            ' 宽度由 SyncUpscaleRootBounds 明确提交；不使用 Anchor.Right，
            ' 避免 WinForms 默认布局恢复创建时的窄尺寸。
            Dim root As New GpuScrollContentPanel With {
                .Dock = DockStyle.None,
                .Anchor = AnchorStyles.Top Or AnchorStyles.Left,
                .AutoSize = False,
                .MinimumSize = New Size(0, UpscaleContentHeight),
                .Height = UpscaleContentHeight,
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
                "插件总开关", "", _switchMaster, _lblMaster), 0, 32)

            Dim exeRow As New ModernHorizontalPanel(108.0F, CSng(UiColumnGap), -1.0F)
            Dim exeCaption = CreateOfficialCaption("固定处理程序")
            exeCaption.TextAlign = ContentAlignment.MiddleLeft
            exeCaption.Padding = New Padding(8, 0, 0, 0)
            _lblExe.AutoSize = False
            _lblExe.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _lblExe.ForeColor = UiText
            exeRow.AddColumn(exeCaption, 0)
            exeRow.AddColumn(CreateOfficialValueBox(_lblExe), 2)
            AddWorkbenchRow(root, exeRow, 32, UiRowHeight)
            AddWorkbenchRow(root, CreateOfficialSeparator(), 68, 16)

            AddWorkbenchRow(root, CreateOfficialSectionHeading(
                "视频处理", "超分与补帧可同时开启；默认按画质优先先超分、再补帧"), 92, 32)

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
            _cmbBackend.Items.Add("RTX VSR (NVIDIA RTX Video)")
            AddHandler _cmbBackend.SelectedIndexChanged, AddressOf OnBackendSelected
            _cmbModel.WaterText = "选择放大模型…"
            ConfigureModelSelector(_cmbModel)
            AddHandler _cmbModel.DropDownOpened, AddressOf OnModelDropDownOpened
            AddHandler _cmbModel.Click, AddressOf OnModelComboClicked
            AddHandler _cmbModel.SelectedIndexChanged, AddressOf OnModelSelected
            Dim upscaleBackendField = CreateOfficialField("推理后端", _cmbBackend)
            _upscaleModelField = CreateOfficialField("放大模型", _cmbModel, 0)
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
            _upscaleTileField = CreateOfficialField("超分分块尺寸", _cmbTileSize)
            _upscaleTileHint = CreateOfficialCaption("0=RVE默认；越小越省显存但更慢", UiTextMuted)
            _upscaleTileHint.TextAlign = ContentAlignment.BottomLeft
            _upscaleTileHint.Margin = Padding.Empty
            ConfigureOutputScaleCombo(_cmbOutputScale)
            _outputScaleField = CreateOfficialField("输出倍率", _cmbOutputScale)
            _outputScaleHint = CreateOfficialCaption("原生推理倍率", UiTextMuted)
            _outputScaleHint.TextAlign = ContentAlignment.MiddleLeft
            _outputScaleHint.Padding = New Padding(0, UiFieldEditorTop, 0, 5)

            _cmbRtxTarget.WaterText = "选择目标分辨率…"
            ConfigureCombo(_cmbRtxTarget)
            For Each target In New String() {"1x 原尺寸", "1.5x", "2x", "3x", "4x", "1080p", "1440p", "2160p", "4320p"}
                _cmbRtxTarget.Items.Add(target)
            Next
            AddHandler _cmbRtxTarget.SelectedIndexChanged, AddressOf OnRtxTargetSelected
            _cmbRtxQuality.WaterText = "质量 3"
            ConfigureCombo(_cmbRtxQuality)
            For quality = 1 To 4 : _cmbRtxQuality.Items.Add("质量 " & quality) : Next
            AddHandler _cmbRtxQuality.SelectedIndexChanged, AddressOf OnRtxQualitySelected
            _rtxTargetField = CreateOfficialField("RTX 输出规格", _cmbRtxTarget)
            _rtxQualityField = CreateOfficialField("RTX VSR 质量", _cmbRtxQuality)

            ConfigureDpiSwitch(_switchRtxHdr)
            _syncingRtxHdrSwitch = True
            _switchRtxHdr.Checked = _config.RtxHdrEnabled
            _syncingRtxHdrSwitch = False
            _switchRtxHdr.Enabled = _config.Enabled
            AddHandler _switchRtxHdr.CheckedChanged, AddressOf OnRtxHdrSwitchChanged
            Dim hdrHeader = BuildOfficialModeHeader(
                "HDR 映射", "", _switchRtxHdr, _lblSwitchRtxHdr, stateWidth:=180.0F)
            _cmbRtxHdrMode.Items.Add("RTX Video HDR")
            _cmbRtxHdrMode.SelectedIndex = 0
            ConfigureCombo(_cmbRtxHdrMode)
            _cmbRtxHdrMode.Enabled = _config.Enabled AndAlso _config.RtxHdrEnabled
            Dim hdrModeField = CreateOfficialField("HDR 处理方式", _cmbRtxHdrMode)
            ConfigureRtxHdrNumeric(_numRtxHdrContrast, 0, 200, _config.RtxHdrContrast, 1)
            ConfigureRtxHdrNumeric(_numRtxHdrSaturation, 0, 200, _config.RtxHdrSaturation, 1)
            ConfigureRtxHdrNumeric(_numRtxHdrMiddleGray, 10, 100, _config.RtxHdrMiddleGray, 1)
            ConfigureRtxHdrNumeric(_numRtxHdrMaxLuminance, 400, 2000, _config.RtxHdrMaxLuminance, 10)
            AddHandler _numRtxHdrContrast.ValueChanged, AddressOf OnRtxHdrContrastChanged
            AddHandler _numRtxHdrSaturation.ValueChanged, AddressOf OnRtxHdrSaturationChanged
            AddHandler _numRtxHdrMiddleGray.ValueChanged, AddressOf OnRtxHdrMiddleGrayChanged
            AddHandler _numRtxHdrMaxLuminance.ValueChanged, AddressOf OnRtxHdrMaxLuminanceChanged
            _rtxHdrContrastField = CreateOfficialField("对比度（0-200）", _numRtxHdrContrast, 8)
            _rtxHdrSaturationField = CreateOfficialField("饱和度（0-200）", _numRtxHdrSaturation, 8)
            _rtxHdrMiddleGrayField = CreateOfficialField("中灰度（10-100）", _numRtxHdrMiddleGray, 8)
            _rtxHdrMaxLuminanceField = CreateOfficialField("最大亮度（nit，400-2000）", _numRtxHdrMaxLuminance, 12)
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

            AddWorkbenchRow(root, upscaleHeader, 128, 32)
            ' 放大模型名称较长（例如 AnimeJaNai...-430K），给模型列保留更多文本区，
            ' 避免箭头区域遮住名称末尾；后端列仍足以完整显示 TensorRT (NVIDIA)。
            AddWorkbenchControl(root, upscaleBackendField, 160, UiFieldHeight, 0.0F, 0.38F, 0, -UiColumnGap)
            AddWorkbenchControl(root, _upscaleModelField, 160, UiFieldHeight, 0.38F, 1.0F)
            AddWorkbenchControl(root, _rtxTargetField, 160, UiFieldHeight, 0.38F, 1.0F)
            AddWorkbenchControl(root, _upscaleTileField, 220, UiFieldHeight, 0.0F, 0.28F, 0, -UiColumnGap)
            AddWorkbenchControl(root, _outputScaleField, 220, UiFieldHeight, 0.28F, 0.50F, 0, -UiColumnGap)
            AddWorkbenchControl(root, _outputScaleHint, 220, UiFieldHeight, 0.50F, 1.0F)
            AddWorkbenchControl(root, _rtxQualityField, 220, UiFieldHeight, 0.0F, 0.46F, 0, -UiColumnGap)
            _upscaleTileHint.Visible = False
            AddWorkbenchRow(root, hdrHeader, 500, 32)
            AddWorkbenchControl(root, hdrModeField, 532, UiFieldHeight, 0.0F, 0.46F, 0, -UiColumnGap)
            ' HDR 原生参数采用两列两行数字输入框；允许键盘输入范围内任意整数。
            AddWorkbenchControl(root, _rtxHdrContrastField, 592, UiFieldHeight, 0.0F, 0.5F, 0, -4)
            AddWorkbenchControl(root, _rtxHdrSaturationField, 592, UiFieldHeight, 0.5F, 1.0F, 4, 0)
            AddWorkbenchControl(root, _rtxHdrMiddleGrayField, 652, UiFieldHeight, 0.0F, 0.5F, 0, -4)
            AddWorkbenchControl(root, _rtxHdrMaxLuminanceField, 652, UiFieldHeight, 0.5F, 1.0F, 4, 0)
            AddWorkbenchRow(root, interpHeader, 288, 32)
            ' 补帧后端的固定选项（尤其是 TensorRT (NVIDIA)）需要在箭头区域前保留
            ' 足够文本宽度；将窄列从 29% 调整到 34%，模型列仍保留主要空间。
            AddWorkbenchControl(root, interpBackendField, 320, UiFieldHeight, 0.0F, 0.34F, 0, -UiColumnGap)
            AddWorkbenchControl(root, interpModelField, 320, UiFieldHeight, 0.34F, 0.80F, 0, -UiColumnGap)
            AddWorkbenchControl(root, interpFactorField, 320, UiFieldHeight, 0.80F, 1.0F)
            AddWorkbenchControl(root, interpThresholdField, 380, UiFieldHeight, 0.0F, 0.34F, 0, -UiColumnGap)
            AddWorkbenchControl(root, interpFlowField, 380, UiFieldHeight, 0.34F, 0.80F, 0, -UiColumnGap)

            Dim orderRow As New ModernHorizontalPanel(112.0F, -54.0F, -46.0F) With {
                .Margin = Padding.Empty
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
            _cmbProcessOrder.Margin = New Padding(0, 4, UiColumnGap, 4)
            AddHandler _cmbProcessOrder.SelectedIndexChanged, AddressOf OnProcessOrderSelected
            _lblProcessOrder.AutoSize = False
            _lblProcessOrder.Dock = DockStyle.Fill
            _lblProcessOrder.Margin = Padding.Empty
            _lblProcessOrder.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            orderRow.AddColumn(orderCaption, 0)
            orderRow.AddColumn(_cmbProcessOrder, 1)
            orderRow.AddColumn(_lblProcessOrder, 2)
            AddWorkbenchRow(root, orderRow, 448, UiRowHeight)
            AddWorkbenchRow(root, CreateOfficialSeparator(), 716, 16)


            _pageUpscale.Controls.Add(root)
            BindScrollableGpuBackgroundSources(root, ModernPanel1)
            ' 为 LakeUI 覆盖式滚动条保留绘制带，避免子窗口覆盖父面板的 GPU 滚动条。
            SyncUpscaleRootBounds()
            UpdateModeStateLabels()
            UpdateAdvancedControlState()
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
            _lblSwitchRtxHdr.Text = If(_config.RtxHdrEnabled, "RTX Video HDR", "关闭")
            _lblSwitchRtxHdr.ForeColor = If(_config.RtxHdrEnabled,
                Color.FromArgb(212, 169, 255),
                Color.FromArgb(136, 136, 136))
        End Sub

        ''' <summary>后端切换后同步补帧开关的可用状态；只有 BasicVSR++ 不支持组合补帧。</summary>
        Private Sub UpdateInterpSwitchState()
            SyncInterpSwitchFromConfig()
        End Sub

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
            Dim forceInterpFirst = combined AndAlso String.Equals(_config.Backend, "rtxvsr", StringComparison.OrdinalIgnoreCase)
            If forceInterpFirst Then _config.ProcessOrder = "interp-first"
            _cmbProcessOrder.Enabled = _config.Enabled AndAlso combined AndAlso Not forceInterpFirst
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

        Private Sub ConfigureOutputScaleCombo(combo As WheelLockedComboBox)
            ConfigureCombo(combo)
            combo.Items.Add("原生")
            For outputFactor As Integer = 1 To 8 : combo.Items.Add(outputFactor.ToString() & "x") : Next
            combo.SelectedIndex = Math.Max(0, Math.Min(8, _config.OutputScale))
            AddHandler combo.SelectedIndexChanged,
                Sub(sender, e)
                    If _syncingOutputScale Then Return
                    _config.OutputScale = Math.Max(0, combo.SelectedIndex)
                    _config.Save()
                    SyncOutputScaleControls()
                End Sub
        End Sub

        Private Sub SyncOutputScaleControls()
            _syncingOutputScale = True
            Try
                For Each combo In New WheelLockedComboBox() {_cmbOutputScale, _cmbImageOutputScale}
                    If combo.Items.Count > 0 Then combo.SelectedIndex = Math.Max(0, Math.Min(8, _config.OutputScale))
                    combo.Enabled = _config.Enabled AndAlso _config.UpscaleEnabled AndAlso _config.Backend <> "rtxvsr"
                Next
                Dim selected = _modelCatalog.FirstOrDefault(Function(item) String.Equals(item.Id, _config.Model, StringComparison.OrdinalIgnoreCase))
                Dim nativeScale = If(selected Is Nothing, 0, selected.Scale)
                Dim text = If(nativeScale > 0, "原生 " & nativeScale.ToString() & "x", "原生倍率以模型为准")
                If _config.OutputScale > 0 AndAlso _config.OutputScale <> nativeScale Then
                    If selected IsNot Nothing AndAlso selected.InferenceScales.Contains(_config.OutputScale) Then
                        text &= "；后端直接推理 " & _config.OutputScale.ToString() & "x"
                    Else
                        text &= "；原生推理后缩放至 " & _config.OutputScale.ToString() & "x"
                    End If
                End If
                If _outputScaleHint IsNot Nothing Then _outputScaleHint.Text = text
                If _imageOutputScaleHint IsNot Nothing Then _imageOutputScaleHint.Text = text
            Finally
                _syncingOutputScale = False
            End Try
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
            _syncingRtxHdrSwitch = True
            _switchRtxHdr.Checked = _config.RtxHdrEnabled
            _syncingRtxHdrSwitch = False
            _switchRtxHdr.Enabled = _config.Enabled
            SyncRtxHdrNumericControls()
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
            _syncingBackend = False
            _syncingFactor = True
            SyncFactorCombo()
            _syncingFactor = False
            _syncingInterpBackend = True
            SyncInterpBackendCombo()
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
            If _cmbRtxTarget.Items.Count > 0 Then
                Dim targetIndex = 2
                For i = 0 To _cmbRtxTarget.Items.Count - 1
                    If _cmbRtxTarget.Items(i).ToString().StartsWith(_config.RtxTarget, StringComparison.OrdinalIgnoreCase) Then targetIndex = i : Exit For
                Next
                _cmbRtxTarget.SelectedIndex = targetIndex
            End If
            _cmbRtxQuality.SelectedIndex = Math.Max(0, Math.Min(3, _config.RtxQuality - 1))
            UpdateAdvancedControlState()
            _syncingProcessOrder = True
            If _cmbProcessOrder.Items.Count > 0 Then
                _cmbProcessOrder.SelectedIndex = If(String.Equals(_config.ProcessOrder, "interp-first", StringComparison.OrdinalIgnoreCase), 1, 0)
            End If
            _syncingProcessOrder = False
            UpdateModeStateLabels()
            UpdateProcessOrderState()
            If Not File.Exists(_config.ExePath) Then
                _lblExe.Text = "<font color=#F4707A>固定位置缺少：" & EscapeHtml(_config.ExePath) & "</font>"
            Else
                _lblExe.Text = "<font color=#DCDCDC>" & EscapeHtml(_config.ExePath) & "</font>"
            End If
        End Sub

        ''' <summary>把配置的推理后端同步到下拉框（6=RTX VSR）。</summary>
        Private Sub SyncBackendCombo()
            If _cmbBackend.Items.Count = 0 Then
                Return
            End If
            _cmbBackend.SelectedIndex = If(_config.Backend = "rtxvsr", 6, If(_config.Backend = "basicvsrpp", 5, If(_config.Backend = "flashvsr", 4, If(_config.Backend = "onnx", 3, If(_config.Backend = "tensorrt", 2, If(_config.Backend = "cuda", 1, 0))))))
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
            _cmbBackend.Enabled = _config.Enabled AndAlso _config.UpscaleEnabled
            _cmbInterpBackend.Enabled = _config.Enabled AndAlso _config.InterpEnabled
            _cmbFactor.Enabled = _config.Enabled AndAlso _config.InterpEnabled
            Dim rtxVsr = String.Equals(_config.Backend, "rtxvsr", StringComparison.OrdinalIgnoreCase)
            SyncOutputScaleControls()
            If _outputScaleField IsNot Nothing Then _outputScaleField.Visible = Not rtxVsr
            If _outputScaleHint IsNot Nothing Then _outputScaleHint.Visible = Not rtxVsr
            If _upscaleModelField IsNot Nothing Then _upscaleModelField.Visible = Not rtxVsr
            If _upscaleTileField IsNot Nothing Then _upscaleTileField.Visible = Not rtxVsr
            If _upscaleTileHint IsNot Nothing Then _upscaleTileHint.Visible = False
            If _rtxTargetField IsNot Nothing Then _rtxTargetField.Visible = rtxVsr
            If _rtxQualityField IsNot Nothing Then _rtxQualityField.Visible = rtxVsr
            _cmbRtxTarget.Enabled = _config.Enabled AndAlso _config.UpscaleEnabled AndAlso rtxVsr
            _cmbRtxQuality.Enabled = _config.Enabled AndAlso _config.UpscaleEnabled AndAlso rtxVsr
            Dim hdrParametersEnabled = _config.Enabled AndAlso _config.RtxHdrEnabled
            _cmbRtxHdrMode.Enabled = hdrParametersEnabled
            _numRtxHdrContrast.Enabled = hdrParametersEnabled
            _numRtxHdrSaturation.Enabled = hdrParametersEnabled
            _numRtxHdrMiddleGray.Enabled = hdrParametersEnabled
            _numRtxHdrMaxLuminance.Enabled = hdrParametersEnabled
            _cmbModel.Enabled = _config.Enabled AndAlso _config.UpscaleEnabled AndAlso Not rtxVsr
            _cmbInterp.Enabled = _config.Enabled AndAlso _config.InterpEnabled
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

        Private Sub SyncRtxHdrNumericControls()
            If _numRtxHdrContrast Is Nothing Then Return
            Dim previous = _syncingRtxHdrParameters
            _syncingRtxHdrParameters = True
            Try
                _numRtxHdrContrast.Value = CDec(PluginConfig.ClampRtxHdrContrast(_config.RtxHdrContrast))
                _numRtxHdrSaturation.Value = CDec(PluginConfig.ClampRtxHdrSaturation(_config.RtxHdrSaturation))
                _numRtxHdrMiddleGray.Value = CDec(PluginConfig.ClampRtxHdrMiddleGray(_config.RtxHdrMiddleGray))
                _numRtxHdrMaxLuminance.Value = CDec(PluginConfig.ClampRtxHdrMaxLuminance(_config.RtxHdrMaxLuminance))
            Finally
                _syncingRtxHdrParameters = previous
            End Try
        End Sub

    End Class

End Namespace
