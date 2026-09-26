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

    Public Partial Class PluginPanel

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

        Private _upscaleRoot As ModernPanel
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
                设置_v6.实例对象.替代进程文件名 = ""
            Catch
            End Try
            _config.Enabled = False
            _config.Save()
            RefreshUi()
            ShowStatus("已停用：编码队列恢复为直接执行 ffmpeg", False)
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
                PortableRuntime.ConfigureProcess(psi)
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
                PortableRuntime.ConfigureProcess(psi)
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
            PortableRuntime.ConfigureProcess(psi)
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
            control.Size = New Size(320, 34)
            control.MinimumSize = New Size(0, 32)
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
            control.BorderRadius = 10
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
            ' ModernPanel 会在滚动时把子控件移动到负的 Top/Left。这里只能同步尺寸，
            ' 不能无条件把位置重置为 0，否则 LakeUI 会把当前位置重新记录为设计坐标，
            ' 下一次回到顶部时就会在内容上方留下一大片空白。
            Dim rootLeft As Integer = root.Left
            Dim rootTop As Integer = root.Top
            If _pageUpscale.VerticalScrollOffset <= 0 AndAlso rootTop <> 0 Then rootTop = 0
            If _pageUpscale.HorizontalScrollOffset <= 0 AndAlso rootLeft <> 0 Then rootLeft = 0
            If root.Left <> rootLeft OrElse root.Top <> rootTop OrElse root.Width <> width OrElse root.Height <> UpscaleContentHeight Then
                root.SetBounds(rootLeft, rootTop, width, UpscaleContentHeight)
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
                "插件总开关", "", _switchMaster, _lblMaster), 0, 40)

            Dim exeRow As New ModernHorizontalPanel(150.0F, 12.0F, -1.0F)
            Dim exeCaption = CreateOfficialCaption("固定处理程序")
            exeCaption.TextAlign = ContentAlignment.MiddleLeft
            exeCaption.Padding = New Padding(12, 0, 0, 0)
            _lblExe.AutoSize = False
            _lblExe.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _lblExe.ForeColor = UiText
            exeRow.AddColumn(exeCaption, 0)
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

            AddWorkbenchRow(root, upscaleHeader, 168, 38)
            ' 放大模型名称较长（例如 AnimeJaNai...-430K），给模型列保留更多文本区，
            ' 避免箭头区域遮住名称末尾；后端列仍足以完整显示 TensorRT (NVIDIA)。
            AddWorkbenchControl(root, upscaleBackendField, 206, 76, 0.0F, 0.38F, 0, -12)
            AddWorkbenchControl(root, _upscaleModelField, 206, 76, 0.38F, 1.0F)
            AddWorkbenchControl(root, _rtxTargetField, 206, 76, 0.38F, 1.0F)
            AddWorkbenchControl(root, _upscaleTileField, 282, 70, 0.0F, 0.46F, 0, -12)
            AddWorkbenchControl(root, _rtxQualityField, 282, 70, 0.0F, 0.46F, 0, -12)
            AddWorkbenchControl(root, _upscaleTileHint, 282, 70, 0.46F, 1.0F)
            AddWorkbenchRow(root, hdrHeader, 630, 38)
            AddWorkbenchControl(root, hdrModeField, 668, 76, 0.0F, 0.46F, 0, -12)
            ' HDR 原生参数采用两列两行数字输入框；允许键盘输入范围内任意整数。
            AddWorkbenchControl(root, _rtxHdrContrastField, 744, 70, 0.0F, 0.5F, 0, -4)
            AddWorkbenchControl(root, _rtxHdrSaturationField, 744, 70, 0.5F, 1.0F, 4, 0)
            AddWorkbenchControl(root, _rtxHdrMiddleGrayField, 814, 70, 0.0F, 0.5F, 0, -4)
            AddWorkbenchControl(root, _rtxHdrMaxLuminanceField, 814, 70, 0.5F, 1.0F, 4, 0)
            AddWorkbenchRow(root, interpHeader, 371, 38)
            ' 补帧后端的固定选项（尤其是 TensorRT (NVIDIA)）需要在箭头区域前保留
            ' 足够文本宽度；将窄列从 29% 调整到 34%，模型列仍保留主要空间。
            AddWorkbenchControl(root, interpBackendField, 409, 76, 0.0F, 0.34F, 0, -12)
            AddWorkbenchControl(root, interpModelField, 409, 76, 0.34F, 0.80F, 0, -12)
            AddWorkbenchControl(root, interpFactorField, 409, 76, 0.80F, 1.0F)
            AddWorkbenchControl(root, interpThresholdField, 485, 70, 0.0F, 0.34F, 0, -12)
            AddWorkbenchControl(root, interpFlowField, 485, 70, 0.34F, 0.80F, 0, -12)

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
            AddWorkbenchRow(root, orderRow, 555, 56)
            AddWorkbenchRow(root, CreateOfficialSeparator(), 884, 25)


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
            Dim rtxVsr = String.Equals(_config.Backend, "rtxvsr", StringComparison.OrdinalIgnoreCase)
            If _upscaleModelField IsNot Nothing Then _upscaleModelField.Visible = Not rtxVsr
            If _upscaleTileField IsNot Nothing Then _upscaleTileField.Visible = Not rtxVsr
            If _upscaleTileHint IsNot Nothing Then _upscaleTileHint.Visible = Not rtxVsr
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
