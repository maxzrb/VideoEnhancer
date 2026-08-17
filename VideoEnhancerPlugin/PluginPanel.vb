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
        Private _syncingMaster As Boolean = False
        Private _syncingSwitch As Boolean = False
        Private _syncingInterpSwitch As Boolean = False
        Private _modelsLoaded As Boolean = False
        Private _loadingModels As Boolean = False
        Private _interpModelsLoaded As Boolean = False
        Private _loadingInterpModels As Boolean = False
        Private _uiReady As Boolean = False

        Public Sub New(config As PluginConfig)
            _config = config
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
            ' 上次退出时已启用且 exe 存在 → 自动恢复启用状态
            If _config.Enabled AndAlso File.Exists(_config.ExePath) Then
                TryEnable(_config.ExePath, True)
            End If
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
            _config.UpscaleEnabled = _switchUpscale.Checked
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
            _config.InterpEnabled = _switchInterp.Checked
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
            Task.Run(Sub()
                         Dim models = RunListModels(exePath, "--search-models")
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
            Task.Run(Sub()
                         Dim models = RunListModels(exePath, "--list-interp-models")
                         Try
                             If Me.IsHandleCreated Then
                                 Me.BeginInvoke(New Action(Sub()
                                                               ApplyInterpModelList(models)
                                                               _loadingInterpModels = False
                                                           End Sub))
                             Else
                                 ApplyInterpModelList(models)
                                 _loadingInterpModels = False
                             End If
                         Catch
                             _loadingInterpModels = False
                         End Try
                     End Sub)
        End Sub

        Private Sub ApplyModelList(models As List(Of String))
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
                ShowStatus($"已从 videoenhancer.exe 读取 {models.Count} 个可用模型", False)
            Else
                _cmbModel.WaterText = "未找到可用模型"
                ShowStatus("未在 models 目录找到含 .param/.bin 的模型", True)
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
                ShowStatus($"已读取 {models.Count} 个补帧模型（models" & Convert.ToChar(92) & "RIFE）", False)
            Else
                _cmbInterp.WaterText = "未找到补帧模型"
                ShowStatus("未在 models" & Convert.ToChar(92) & "RIFE 目录找到含 .param/.bin 的补帧模型", True)
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

        Private Shared Function RunListModels(exePath As String, listArg As String) As List(Of String)
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
                psi.ArgumentList.Add(listArg)
                psi.ArgumentList.Add("--json")
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

            ' ── 第 1 行：插件总开关（主开关；关闭时停止对参数面板的 hook）──
            Dim sectionMaster As New Panel() With {.Dock = DockStyle.Top, .Height = 50, .BackColor = Color.Transparent, .Padding = New Padding(0, 10, 0, 0)}
            root.Controls.Add(sectionMaster)

            _switchMaster.Dock = DockStyle.Left
            _switchMaster.Size = New Size(66, 34)
            _switchMaster.TrackColorOn = Color.FromArgb(80, 200, 120)
            _switchMaster.TrackColorOff = Color.FromArgb(90, 90, 100)
            _switchMaster.KnobColor = Color.FromArgb(235, 235, 235)
            ' 构造时即按配置同步开关状态（AddHandler 之前赋值，避免触发事件）
            _switchMaster.Checked = _config.Enabled
            AddHandler _switchMaster.CheckedChanged, AddressOf OnMasterSwitchChanged
            sectionMaster.Controls.Add(_switchMaster)

            _lblMaster.Text = "插件总开关"
            _lblMaster.AutoSize = False
            _lblMaster.Size = New Size(130, 40)
            _lblMaster.Dock = DockStyle.Left
            _lblMaster.ForeColor = Color.Gainsboro
            _lblMaster.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            sectionMaster.Controls.Add(_lblMaster)

            ' ── 第 2 行：超分开关 + 放大模型选择 ──
            Dim sectionUpscale As New Panel() With {.Dock = DockStyle.Top, .Height = 56, .BackColor = Color.Transparent, .Padding = New Padding(0, 10, 0, 0)}
            root.Controls.Add(sectionUpscale)

            Dim rowUpscale As New Panel() With {.Dock = DockStyle.Top, .Height = 40, .BackColor = Color.Transparent}
            sectionUpscale.Controls.Add(rowUpscale)

            _switchUpscale.Dock = DockStyle.Left
            _switchUpscale.Size = New Size(66, 34)
            _switchUpscale.TrackColorOn = Color.FromArgb(80, 200, 120)
            _switchUpscale.TrackColorOff = Color.FromArgb(90, 90, 100)
            _switchUpscale.KnobColor = Color.FromArgb(235, 235, 235)
            _switchUpscale.Checked = _config.UpscaleEnabled
            _switchUpscale.Enabled = _config.Enabled
            AddHandler _switchUpscale.CheckedChanged, AddressOf OnUpscaleSwitchChanged
            rowUpscale.Controls.Add(_switchUpscale)

            _lblSwitch.Text = "超分开关"
            _lblSwitch.AutoSize = False
            _lblSwitch.Size = New Size(80, 40)
            _lblSwitch.Dock = DockStyle.Left
            _lblSwitch.ForeColor = Color.Gainsboro
            _lblSwitch.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            rowUpscale.Controls.Add(_lblSwitch)

            Dim lblUpscaleModel As New HtmlColorLabel() With {
                .Text = "放大模型",
                .AutoSize = False,
                .Size = New Size(110, 40),
                .Dock = DockStyle.Left,
                .ForeColor = Color.Gainsboro,
                .TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            }
            rowUpscale.Controls.Add(lblUpscaleModel)

            _cmbModel.Dock = DockStyle.None
            _cmbModel.Location = New Point(295, 0)
            _cmbModel.Size = New Size(380, 40)
            _cmbModel.Anchor = AnchorStyles.Left Or AnchorStyles.Top
            _cmbModel.WaterText = "点击选择放大模型…"
            _cmbModel.BorderRadius = 8
            _cmbModel.BorderSize = 1
            AddHandler _cmbModel.DropDownOpened, AddressOf OnModelDropDownOpened
            AddHandler _cmbModel.Click, AddressOf OnModelComboClicked
            AddHandler _cmbModel.SelectedIndexChanged, AddressOf OnModelSelected
            rowUpscale.Controls.Add(_cmbModel)

            ' ── 第 3 行：补帧开关 + 补帧模型选择 ──
            Dim sectionInterp As New Panel() With {.Dock = DockStyle.Top, .Height = 56, .BackColor = Color.Transparent, .Padding = New Padding(0, 10, 0, 0)}
            root.Controls.Add(sectionInterp)

            Dim rowInterp As New Panel() With {.Dock = DockStyle.Top, .Height = 40, .BackColor = Color.Transparent}
            sectionInterp.Controls.Add(rowInterp)

            _switchInterp.Dock = DockStyle.Left
            _switchInterp.Size = New Size(66, 34)
            _switchInterp.TrackColorOn = Color.FromArgb(80, 200, 120)
            _switchInterp.TrackColorOff = Color.FromArgb(90, 90, 100)
            _switchInterp.KnobColor = Color.FromArgb(235, 235, 235)
            _switchInterp.Checked = _config.InterpEnabled
            _switchInterp.Enabled = _config.Enabled
            AddHandler _switchInterp.CheckedChanged, AddressOf OnInterpSwitchChanged
            rowInterp.Controls.Add(_switchInterp)

            _lblSwitchInterp.Text = "补帧开关"
            _lblSwitchInterp.AutoSize = False
            _lblSwitchInterp.Size = New Size(80, 40)
            _lblSwitchInterp.Dock = DockStyle.Left
            _lblSwitchInterp.ForeColor = Color.Gainsboro
            _lblSwitchInterp.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            rowInterp.Controls.Add(_lblSwitchInterp)

            Dim lblInterpModel As New HtmlColorLabel() With {
                .Text = "补帧模型",
                .AutoSize = False,
                .Size = New Size(110, 40),
                .Dock = DockStyle.Left,
                .ForeColor = Color.Gainsboro,
                .TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            }
            rowInterp.Controls.Add(lblInterpModel)

            _cmbInterp.Dock = DockStyle.None
            _cmbInterp.Location = New Point(295, 0)
            _cmbInterp.Size = New Size(380, 40)
            _cmbInterp.Anchor = AnchorStyles.Left Or AnchorStyles.Top
            _cmbInterp.WaterText = "点击选择补帧模型…"
            _cmbInterp.BorderRadius = 8
            _cmbInterp.BorderSize = 1
            AddHandler _cmbInterp.DropDownOpened, AddressOf OnInterpDropDownOpened
            AddHandler _cmbInterp.Click, AddressOf OnInterpComboClicked
            AddHandler _cmbInterp.SelectedIndexChanged, AddressOf OnInterpModelSelected
            rowInterp.Controls.Add(_cmbInterp)

            ' ── 第 4 行：videoenhancer.exe 路径 + 更改路径 ──
            Dim sectionExe As New Panel() With {.Dock = DockStyle.Top, .Height = 44, .BackColor = Color.Transparent, .Padding = New Padding(0, 8, 0, 0)}
            root.Controls.Add(sectionExe)

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

            ' ── 状态区 ──
            Dim sectionStatus As New Panel() With {.Dock = DockStyle.Fill, .BackColor = Color.Transparent, .Padding = New Padding(0, 16, 0, 0)}
            root.Controls.Add(sectionStatus)

            _lblStatus.AutoSize = True
            _lblStatus.Dock = DockStyle.Fill
            _lblStatus.ForeColor = Color.FromArgb(190, 190, 190)
            _lblStatus.Text = ""
            sectionStatus.Controls.Add(_lblStatus)
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
            _lblExe.Text = "videoenhancer.exe：" & If(String.IsNullOrWhiteSpace(_config.ExePath), "（未指定）", _config.ExePath)
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
            _lblStatus.Text = text
            If error_ Then
                _lblStatus.ForeColor = Color.FromArgb(230, 120, 120)
            Else
                _lblStatus.ForeColor = Color.FromArgb(150, 210, 160)
            End If
        End Sub

    End Class

End Namespace
