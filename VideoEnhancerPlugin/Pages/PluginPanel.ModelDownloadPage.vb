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

        ' ── 模型下载页 ──
        Private Const DownloadActionColumn As Integer = 3
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
        Private ReadOnly _downloadCoordinator As New ModelDownloadCoordinator()
        Private ReadOnly _downloadProcessLifetime As New DownloadProcessLifetime()
        Private _downloadActionsEnabled As Boolean = True
        Private _downloadAllBusy As Boolean = False
        Private _downloadAllStopRequested As Boolean = False
        Private ReadOnly _downloadCancellations As New Dictionary(Of String, DownloadCancellationRequest)(StringComparer.OrdinalIgnoreCase)
        Private _downloadListConfigured As Boolean = False
        Private ReadOnly _downloadItemsByPath As New Dictionary(Of String, UltraDetailListView.ListItem)(StringComparer.OrdinalIgnoreCase)
        Private ReadOnly _downloadGroupItems As New Dictionary(Of String, UltraDetailListView.ListItem)(StringComparer.OrdinalIgnoreCase)
        Private _downloadModelContextMenu As ModernContextMenu
        Private _contextDownloadModel As DownloadModelEntry
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
            Public Property Cancelled As Boolean

            Public ReadOnly Property Outcome As ModelDownloadCoordinator.ItemOutcome
                Get
                    If ExitCode = 0 Then Return ModelDownloadCoordinator.ItemOutcome.Succeeded
                    If Cancelled Then Return ModelDownloadCoordinator.ItemOutcome.Cancelled
                    If Errors.Contains("NO_NETWORK|") Then Return ModelDownloadCoordinator.ItemOutcome.Offline
                    If Errors.Contains("AUTH_REQUIRED|") Then Return ModelDownloadCoordinator.ItemOutcome.AuthenticationRequired
                    Return ModelDownloadCoordinator.ItemOutcome.Failed
                End Get
            End Property
        End Class
        Private Sub BuildOfficialModelDownloadPage()
            _pageDownloader.Dock = DockStyle.Fill
            _pageDownloader.BackColor = Color.Transparent
            _pageDownloader.Padding = New Padding(0, 4, 0, 0)

            Dim root As New ModernGridPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 2,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, CSng(UiRowHeight)))
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
            header.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 128.0F))
            header.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 136.0F))
            header.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            header.AddAt(CreateOfficialSectionHeading(
                "模型资源库", "从 ModelScope 获取模型与后端组件"), 0, 0)
            _btnDownloadPluginUpdate.Text = "下载全部"
            _btnDownloadPluginUpdate.Dock = DockStyle.Fill
            _btnDownloadPluginUpdate.AutoSize = False
            _btnDownloadPluginUpdate.Margin = New Padding(UiColumnGap, 4, 0, 4)
            ConfigureSecondaryButton(_btnDownloadPluginUpdate)
            AddHandler _btnDownloadPluginUpdate.Click, AddressOf OnDownloadAllClick
            header.AddAt(_btnDownloadPluginUpdate, 2, 0)
            _btnRefreshDownloads.Text = "刷新资源"
            _btnRefreshDownloads.Dock = DockStyle.Fill
            _btnRefreshDownloads.Margin = New Padding(UiColumnGap, 4, 0, 4)
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
            ConfigureTransparentListAppearance(_downloadList)
            _downloadList.HeaderVisible = True
            _downloadList.HeaderHeight = 30
            _downloadList.AllowColumnResize = True
            _downloadList.MultiSelect = False
            _downloadList.AllowDragReorder = False
            _downloadList.ItemPadding = New Padding(10, 5, 8, 5)
            _downloadList.ItemSpacing = 2
            _downloadList.ContentPadding = New Padding(0, 4, 0, 4)
            _downloadList.GroupHeight = 38
            _downloadList.Columns.AddRange(New UltraDetailListView.ListColumn() {
                New UltraDetailListView.ListColumn("资源名称", 520),
                New UltraDetailListView.ListColumn("大小", 110),
                New UltraDetailListView.ListColumn("状态", 130),
                New UltraDetailListView.ListColumn("操作", 138)
            })
            AddHandler _downloadList.ItemClick, AddressOf OnDownloadListItemClick
            AddHandler _downloadList.MouseDown, AddressOf OnDownloadListMouseDown
            ConfigureDpiListColumns(_downloadList, 260)
        End Sub

        Private Function DownloadExecutablePath() As String
            Return PluginConfig.ResolveInstalledExePath()
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
            If _downloadsLoading OrElse _archiveCleanupBusy OrElse _downloadCoordinator.ActiveCount > 0 OrElse
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
                        PortableRuntime.ConfigureProcess(psi)
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
                        PortableRuntime.ConfigureProcess(backendPsi)
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
                                .Installed = DownloadInstallStatus.IsDownloadInstalled(If(relativePath, ""), ResolveCoreRoot(), PluginConfig.ResolveInstalledExePath())
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
            Dim row = TryCast(e.Item.Tag, DownloadListRowTag)
            If row Is Nothing Then Return
            If row.Entry IsNot Nothing AndAlso _downloadCoordinator.IsPathActive(row.Entry.RelativePath) Then
                Dim cancellation As DownloadCancellationRequest = Nothing
                If _downloadCancellations.TryGetValue(row.Entry.RelativePath, cancellation) Then
                    If cancellation.Installing Then
                        ShowStatus("正在安装后端，请等待事务完成。", False)
                        Return
                    End If
                    Try
                        cancellation.Cancel()
                        SetDownloadRowState(row.Entry.RelativePath, "正在取消", "请稍候...", UiTextMuted, UiTextMuted)
                    Catch ex As Exception
                        ShowStatus(ex.Message, True)
                    End Try
                End If
                Return
            End If
            If Not _downloadActionsEnabled OrElse Not _downloadOnline OrElse _downloadsLoading OrElse _archiveCleanupBusy Then Return
            If row.Entry IsNot Nothing AndAlso Not row.Entry.Installed AndAlso Not row.Entry.IsBackend AndAlso
                Not row.Entry.RelativePath.Equals("Plugin/videoenhancer.exe", StringComparison.OrdinalIgnoreCase) Then
                Dim queued = _downloadCoordinator.Enqueue(DownloadCategory(row.Entry.RelativePath), row.Entry.RelativePath)
                Select Case queued
                    Case ModelDownloadCoordinator.EnqueueResult.Enqueued, ModelDownloadCoordinator.EnqueueResult.AlreadyQueued
                        ' 调度可能立即开始，不用排队文案覆盖正在下载的百分比。
                        If Not _downloadCoordinator.IsPathActive(row.Entry.RelativePath) Then
                            SetDownloadRowState(row.Entry.RelativePath, "待下载", "排队中", UiTextMuted, UiTextMuted)
                        End If
                        Return
                    Case ModelDownloadCoordinator.EnqueueResult.Stopping
                        ShowStatus("队列正在停止，请结束后再重试。", False)
                        Return
                End Select
            End If
            If _downloadAllBusy Then Return
            If row.Entry IsNot Nothing Then
                Await DownloadSingleItemAsync(row.Entry)
            ElseIf row.BatchPaths IsNot Nothing Then
                Await DownloadGroupItemsAsync(row.Category, row.BatchPaths)
            End If
        End Sub

        Private Shared Function CanDeleteDownloadedModel(entry As DownloadModelEntry) As Boolean
            If entry Is Nothing OrElse Not entry.Installed Then Return False
            If DownloadInstallStatus.IsRtxVideoRuntimeDownload(entry.RelativePath) Then Return True
            If DownloadInstallStatus.IsDownloadArchive(entry.RelativePath) Then Return False
            Dim category = DownloadCategory(entry.RelativePath)
            Return Not category.Equals("Backend", StringComparison.OrdinalIgnoreCase) AndAlso
                Not category.Equals("Bin", StringComparison.OrdinalIgnoreCase) AndAlso
                Not category.Equals("Plugin", StringComparison.OrdinalIgnoreCase)
        End Function

        Private Sub OnDownloadListMouseDown(sender As Object, e As MouseEventArgs)
            If e.Button <> MouseButtons.Right OrElse _downloadsLoading OrElse _archiveCleanupBusy OrElse
                _downloadCoordinator.ActiveCount > 0 Then Return
            Dim item = _downloadList.GetItemAt(e.X, e.Y)
            Dim row = TryCast(If(item Is Nothing, Nothing, item.Tag), DownloadListRowTag)
            Dim entry = If(row Is Nothing, Nothing, row.Entry)
            If Not CanDeleteDownloadedModel(entry) Then
                CloseDownloadModelContextMenu()
                Return
            End If
            Dim index = _downloadList.Items.IndexOf(item)
            If index >= 0 Then _downloadList.SelectedIndex = index
            ShowDownloadModelContextMenu(entry, e.Location)
        End Sub

        Private Sub CloseDownloadModelContextMenu()
            Dim menu = _downloadModelContextMenu
            _downloadModelContextMenu = Nothing
            _contextDownloadModel = Nothing
            If menu Is Nothing Then Return
            Try
                menu.Close()
            Catch
            End Try
        End Sub

        Private Sub ShowDownloadModelContextMenu(entry As DownloadModelEntry, location As Point)
            CloseDownloadModelContextMenu()
            Dim menu As New ModernContextMenu()
            ConfigureModelMenu(menu, reserveIconColumn:=False)
            Dim actionText = If(DownloadInstallStatus.IsRtxVideoRuntimeDownload(entry.RelativePath),
                "卸载 RTX 运行组件", "删除本地模型")
            Dim deleteItem As New ModernContextMenu.ModernMenuItem(actionText) With {
                .CloseOnClick = True,
                .ForeColor = UiDanger
            }
            AddHandler deleteItem.Click,
                Sub(sender As Object, args As EventArgs)
                    Dim target = _contextDownloadModel
                    CloseDownloadModelContextMenu()
                    DeleteDownloadedModelWithConfirmation(target)
                End Sub
            menu.Items.Add(deleteItem)
            _contextDownloadModel = entry
            _downloadModelContextMenu = menu
            menu.Show(_downloadList, location)
        End Sub

        Private Async Sub DeleteDownloadedModelWithConfirmation(entry As DownloadModelEntry)
            If Not CanDeleteDownloadedModel(entry) Then Return
            Dim isRtxRuntime = DownloadInstallStatus.IsRtxVideoRuntimeDownload(entry.RelativePath)
            Dim dialogTitle = If(isRtxRuntime, "卸载 RTX 运行组件", "删除本地模型")
            Dim question = If(isRtxRuntime,
                "确定卸载本机 RTX 运行组件？" & Environment.NewLine &
                    "将删除 bin\rtx-video 中的 sidecar、运行库和所有历史日期归档；" &
                    "不影响 ModelScope 远端资源，可随时重新下载。",
                "确定删除本地模型“" & entry.Name & "”？" & Environment.NewLine &
                    "只删除本机 models 目录中的这个模型文件，不影响 ModelScope 远端资源。") &
                Environment.NewLine & Environment.NewLine & "路径：" & entry.RelativePath
            If Not ShowLakeConfirm(Me, question, dialogTitle, defaultYes:=False) Then Return

            Dim exePath = DownloadExecutablePath()
            If String.IsNullOrWhiteSpace(exePath) OrElse Not File.Exists(exePath) Then
                ShowStatus("删除失败：找不到 videoenhancer.exe", True)
                Return
            End If
            SetDownloadActionsEnabled(False)
            Try
                Dim errorText = Await Task.Run(Function() RunDownloadedModelDelete(exePath, entry.RelativePath))
                If errorText.Length > 0 Then
                    ShowStatus("本地模型删除失败：" & errorText, True)
                    Return
                End If
                entry.Installed = DownloadInstallStatus.IsDownloadInstalled(entry.RelativePath, ResolveCoreRoot(), PluginConfig.ResolveInstalledExePath())
                SetDownloadRowState(entry.RelativePath, "未安装", "下载", UiTextMuted, UiAccent)
                RefreshDownloadGroupSummary(DownloadCategory(entry.RelativePath))
                RefreshModels()
                ShowStatus(If(isRtxRuntime,
                    "已卸载 RTX 运行组件", "已删除本地模型：" & entry.Name), False)
            Finally
                SetDownloadActionsEnabled(True)
            End Try
        End Sub

        Private Shared Function RunDownloadedModelDelete(exePath As String, relativePath As String) As String
            Try
                Dim psi As New ProcessStartInfo With {
                    .FileName = exePath, .WorkingDirectory = Path.GetDirectoryName(exePath),
                    .UseShellExecute = False, .RedirectStandardOutput = True,
                    .RedirectStandardError = True, .CreateNoWindow = True,
                    .StandardOutputEncoding = Encoding.UTF8, .StandardErrorEncoding = Encoding.UTF8
                }
                PortableRuntime.ConfigureProcess(psi)
                psi.ArgumentList.Add("--delete-download-model")
                psi.ArgumentList.Add(relativePath)
                Using child = Diagnostics.Process.Start(psi)
                    If child Is Nothing Then Return "无法启动模型删除进程"
                    Dim stdout = child.StandardOutput.ReadToEnd()
                    Dim stderr = child.StandardError.ReadToEnd()
                    If Not child.WaitForExit(45000) Then
                        Try
                            child.Kill(entireProcessTree:=True)
                        Catch
                        End Try
                        Return "模型删除进程超时"
                    End If
                    If child.ExitCode <> 0 Then Return LastNonEmptyLine(If(String.IsNullOrWhiteSpace(stderr), stdout, stderr))
                End Using
                Return ""
            Catch ex As Exception
                Return ex.Message
            End Try
        End Function

        Private Async Sub OnDownloadAllClick(sender As Object, e As EventArgs)
            If _downloadAllBusy Then
                _downloadAllStopRequested = True
                For Each cancellation In _downloadCancellations.Values.ToList()
                    Try
                        cancellation.Cancel()
                    Catch ex As Exception
                        Trace.WriteLine(ex)
                    End Try
                Next
                UpdateDownloadUtilityButtons()
                ShowStatus("正在停止全部下载，等待当前任务安全结束。", False)
                Return
            End If
            If Not _downloadActionsEnabled OrElse Not _downloadOnline OrElse _downloadsLoading OrElse
                _archiveCleanupBusy OrElse _downloadCoordinator.ActiveCount > 0 OrElse _downloadAllBusy Then Return
            ' 插件 EXE 由自动更新流程管理；Backend 先按状态选择增量或完整事务安装。
            Dim paths As New List(Of String)()
            For Each item As UltraDetailListView.ListItem In _downloadList.Items
                Dim row = TryCast(item.Tag, DownloadListRowTag)
                If row Is Nothing OrElse row.Entry Is Nothing Then Continue For
                Dim path = row.Entry.RelativePath
                If Not path.Equals("Plugin/videoenhancer.exe", StringComparison.OrdinalIgnoreCase) AndAlso
                    Not DownloadCategory(path).Equals("Backend", StringComparison.OrdinalIgnoreCase) Then paths.Add(path)
            Next
            Dim backendEntry As DownloadModelEntry = Nothing
            For Each pair In _downloadItemsByPath
                If Not DownloadCategory(pair.Key).Equals("Backend", StringComparison.OrdinalIgnoreCase) Then Continue For
                Dim row = TryCast(pair.Value.Tag, DownloadListRowTag)
                If row IsNot Nothing AndAlso row.Entry IsNot Nothing AndAlso Not row.Entry.Installed Then
                    backendEntry = row.Entry
                    Exit For
                End If
            Next
            If paths.Count = 0 AndAlso backendEntry Is Nothing Then
                ShowStatus("全部资源已安装。", False)
                Return
            End If
            _downloadAllStopRequested = False
            _downloadAllBusy = True
            Dim modelQueueStarted = False
            UpdateDownloadUtilityButtons()
            Try
                If backendEntry IsNot Nothing Then
                    Dim attemptedPatch = Not backendEntry.ForceBackendFull
                    Await DownloadSingleItemAsync(backendEntry)
                    If _downloadAllStopRequested Then Return
                    If attemptedPatch AndAlso backendEntry.ForceBackendFull AndAlso Not backendEntry.Installed Then
                        ' 增量补丁校验失败时，沿用单项下载的确认与事务回滚流程尝试完整包。
                        Await DownloadSingleItemAsync(backendEntry)
                    End If
                    If _downloadAllStopRequested OrElse Not backendEntry.Installed Then Return
                End If
                If paths.Count > 0 Then
                    modelQueueStarted = True
                    Await DownloadGroupItemsAsync("全部资源", paths)
                End If
            Finally
                If _downloadAllStopRequested AndAlso Not modelQueueStarted Then
                    ShowStatus("已停止全部下载，后续资源尚未启动。", False)
                End If
                _downloadAllBusy = False
                _downloadAllStopRequested = False
                UpdateDownloadUtilityButtons()
            End Try
        End Sub

        Private Async Function DownloadSingleItemAsync(entry As DownloadModelEntry) As Task
            If entry Is Nothing OrElse entry.Installed Then Return
            If _downloadCoordinator.ActiveCount >= 3 Then
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
            ElseIf result.Cancelled Then
                SetDownloadRowState(relativePath, "已取消", "重试", UiTextMuted, UiAccent)
                ShowStatus("下载已取消，可点击重试。", False)
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
            If allPaths Is Nothing OrElse allPaths.Count = 0 OrElse _downloadCoordinator.IsGroupActive(category) Then Return
            If _downloadCoordinator.ActiveCount >= 3 Then
                ShowStatus("当前已有 3 个并行下载，请等待任一文件完成。", True)
                Return
            End If
            Dim paths = allPaths.Where(Function(path)
                Dim item As UltraDetailListView.ListItem = Nothing
                If Not _downloadItemsByPath.TryGetValue(path, item) Then Return False
                Dim row = TryCast(item.Tag, DownloadListRowTag)
                Return row IsNot Nothing AndAlso row.Entry IsNot Nothing AndAlso Not row.Entry.Installed AndAlso
                    Not _downloadCoordinator.IsPathActive(path)
            End Function).ToList()
            If paths.Count = 0 Then
                RefreshDownloadGroupSummary(category)
                Return
            End If
            Dim exePath = DownloadExecutablePath()
            If String.IsNullOrWhiteSpace(exePath) Then Return
            SetDownloadGroupState(category, "待下载 " & paths.Count, "下载中", UiAccent)
            For Each path In paths
                SetDownloadRowState(path, "待下载", "排队中", UiTextMuted, UiTextMuted)
            Next
            Dim batch = Await _downloadCoordinator.RunGroupAsync(Of DownloadExecutionResult)(
                category, paths,
                Function(relativePath)
                    UpdateDownloadUtilityButtons()
                    SetDownloadRowState(relativePath, "下载中", "准备中...", UiAccent, UiAccent)
                    Return ExecuteDownloadAsync(exePath, relativePath,
                        Sub(text)
                            Try
                                BeginInvoke(New Action(Sub()
                                    SetDownloadRowState(relativePath, "下载中", text, UiAccent, UiAccent)
                                End Sub))
                            Catch
                            End Try
                        End Sub)
                End Function,
                Function(result) result.Outcome,
                Sub(finishedPath, result)
                    If result.ExitCode <> 0 Then
                        SetDownloadRowState(finishedPath,
                            If(result.Cancelled, "已取消", If(result.Errors.Contains("AUTH_REQUIRED|"), "需要认证", "下载失败")),
                            "重试", If(result.Cancelled, UiTextMuted, UiDanger), UiAccent)
                        If result.Errors.Contains("NO_NETWORK|") Then _downloadOnline = False
                    Else
                        MarkDownloadInstalled(finishedPath)
                    End If
                End Sub,
                Sub(state)
                    Dim stopping = state.StopReason <> ModelDownloadCoordinator.QueueStopReason.None
                    SetDownloadGroupState(category, state.Summary,
                        If(stopping, "等待当前任务", "下载中"), If(stopping, UiTextMuted, UiAccent))
                    ShowStatus(state.Summary, False)
                End Sub,
                Function() _downloadAllBusy AndAlso _downloadAllStopRequested)

            For Each item As UltraDetailListView.ListItem In _downloadList.Items
                Dim row = TryCast(item.Tag, DownloadListRowTag)
                If row IsNot Nothing AndAlso row.Entry IsNot Nothing AndAlso item.SubItems(DownloadActionColumn).Text = "排队中" AndAlso
                    Not _downloadCoordinator.IsPathActive(row.Entry.RelativePath) AndAlso
                    Not _downloadCoordinator.IsPathQueued(row.Entry.RelativePath) Then
                    SetDownloadRowState(row.Entry.RelativePath, "未安装", "下载", UiTextMuted, UiAccent)
                End If
            Next
            For Each affectedCategory In paths.Select(Function(path) DownloadCategory(path)).Distinct(StringComparer.OrdinalIgnoreCase)
                RefreshDownloadGroupSummary(affectedCategory)
            Next
            SetDownloadGroupState(category, batch.Summary,
                If(batch.Pending > 0 OrElse batch.Cancelled > 0 OrElse batch.Failed > 0, "下载本组", "已全部存在"), UiAccent)
            UpdateDownloadUtilityButtons()
            Dim summary = "下载结束：" & batch.Summary
            If batch.StopReason = ModelDownloadCoordinator.QueueStopReason.Offline Then
                SetDownloadActionsEnabled(False)
                ShowOfflineDownloadStatus()
                ShowStatus(summary & "；网络中断，检查网络后刷新并继续。", True)
            ElseIf batch.StopReason = ModelDownloadCoordinator.QueueStopReason.AuthenticationRequired Then
                ShowStatus(summary & "；需要认证，设置令牌后继续。", True)
            ElseIf batch.StopReason = ModelDownloadCoordinator.QueueStopReason.UserStopped Then
                ShowStatus(summary & "；已停止全部下载。", False)
            ElseIf batch.Failed > 0 OrElse batch.Cancelled > 0 Then
                ShowStatus(summary, batch.Failed > 0)
            Else
                ShowStatus("该分类 " & batch.Succeeded & " 个文件已全部下载完成", False)
            End If
        End Function

        Private Async Function ExecuteDownloadAsync(exePath As String, relativePath As String,
                                                     progress As Action(Of String),
                                                     Optional forceBackendFull As Boolean = False) As Task(Of DownloadExecutionResult)
            Dim cancellation As New DownloadCancellationRequest()
            _downloadCancellations(relativePath) = cancellation
            Dim watch = Stopwatch.StartNew()
            Try
                Dim result = Await Task.Run(Function() ExecuteModelDownload(exePath, relativePath,
                    Sub(text)
                        If Not cancellation.Requested Then progress(text)
                    End Sub, cancellation, forceBackendFull))
                result.Cancelled = result.ExitCode <> 0 AndAlso
                    (cancellation.Requested OrElse result.Errors.Contains("DOWNLOAD_CANCELLED|"))
                If result.ExitCode <> 0 Then
                    Try
                        Dim folder = Path.Combine(PortableRuntime.ApplicationRoot, "logs")
                        Directory.CreateDirectory(folder)
                        File.AppendAllText(Path.Combine(folder, "downloads.log"),
                            "[" & DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") & "] " & relativePath &
                            " exit=" & result.ExitCode & " cancelled=" & result.Cancelled &
                            " elapsed=" & watch.Elapsed.ToString() & Environment.NewLine & result.Errors &
                            Environment.NewLine, New UTF8Encoding(False))
                    Catch ex As Exception
                        Trace.WriteLine(ex)
                    End Try
                End If
                Return result
            Finally
                _downloadCancellations.Remove(relativePath)
                Try
                    cancellation.Clean()
                Catch ex As Exception
                    Trace.WriteLine(ex)
                End Try
                EndDownload(relativePath)
            End Try
        End Function

        Private Function ExecuteModelDownload(exePath As String, relativePath As String, progress As Action(Of String),
                                              cancellation As DownloadCancellationRequest, Optional forceBackendFull As Boolean = False) As DownloadExecutionResult
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
                PortableRuntime.ConfigureProcess(psi)
                psi.Environment("VIDEOENHANCER_CANCEL_FILE") = cancellation.Marker
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
                                If parts.Length > 1 Then progress(parts(1) & "% · 取消")
                            ElseIf ev.Data.StartsWith("EXTRACT_START|", StringComparison.Ordinal) Then
                                progress("准备解压 · 取消")
                            ElseIf ev.Data.StartsWith("EXTRACT_PROGRESS|", StringComparison.Ordinal) Then
                                Dim parts = ev.Data.Split("|"c)
                                If parts.Length > 1 Then progress("解压 " & parts(1) & "% · 取消")
                            ElseIf ev.Data.StartsWith("DOWNLOAD_STAGE|", StringComparison.Ordinal) Then
                                progress(ev.Data.Substring("DOWNLOAD_STAGE|".Length) & " · 取消")
                            ElseIf ev.Data.StartsWith("BACKEND_INSTALL_START|", StringComparison.Ordinal) Then
                                cancellation.Installing = True
                                progress(ev.Data.Substring("BACKEND_INSTALL_START|".Length))
                            ElseIf ev.Data.StartsWith("EXTRACT_COMPLETE|", StringComparison.Ordinal) Then
                                progress("解压完成")
                            ElseIf ev.Data.StartsWith("BACKEND_PATCH_START|", StringComparison.Ordinal) Then
                                progress("连接增量下载 · 取消")
                            ElseIf ev.Data.StartsWith("BACKEND_FULL_START|", StringComparison.Ordinal) Then
                                progress("连接完整包下载 · 取消")
                            ElseIf ev.Data.StartsWith("BACKEND_PATCH_COMPLETE|", StringComparison.Ordinal) Then
                                progress("补丁已应用")
                            End If
                        End Sub
                    AddHandler process.ErrorDataReceived, Sub(s, ev) If ev.Data IsNot Nothing Then errors.AppendLine(ev.Data)
                    process.Start()
                    Try
                        _downloadProcessLifetime.Register(process)
                    Catch
                        Try
                            process.Kill(entireProcessTree:=True)
                        Catch
                        End Try
                        Throw
                    End Try
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
            RefreshDownloadGroupSummary(DownloadCategory(relativePath))
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
            Dim started = _downloadCoordinator.TryBegin(relativePath)
            If started Then UpdateDownloadUtilityButtons()
            Return started
        End Function

        Private Sub EndDownload(relativePath As String)
            _downloadCoordinator.EndPath(relativePath)
            UpdateDownloadUtilityButtons()
        End Sub

        Private Sub UpdateDownloadUtilityButtons()
            _btnRefreshDownloads.Enabled = Not _downloadsLoading AndAlso
                _downloadCoordinator.ActiveCount = 0 AndAlso Not _archiveCleanupBusy AndAlso Not _downloadAllBusy
            _btnDownloadPluginUpdate.Text = If(_downloadAllBusy, If(_downloadAllStopRequested, "正在停止", "停止全部"), "下载全部")
            _btnDownloadPluginUpdate.Enabled = If(_downloadAllBusy, Not _downloadAllStopRequested,
                _downloadsLoaded AndAlso _downloadActionsEnabled AndAlso _downloadOnline AndAlso
                _downloadCoordinator.ActiveCount = 0 AndAlso Not _archiveCleanupBusy)
            _btnCleanArchives.Enabled = _downloadCoordinator.ActiveCount = 0 AndAlso Not _archiveCleanupBusy AndAlso Not _downloadAllBusy
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
            If _archiveCleanupBusy OrElse _downloadCoordinator.ActiveCount > 0 Then
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
                        PortableRuntime.ConfigureProcess(psi)
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
    End Class

End Namespace
