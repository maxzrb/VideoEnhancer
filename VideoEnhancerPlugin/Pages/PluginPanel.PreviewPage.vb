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

        ' 定期把「预览输出」右键菜单项挂到编码队列窗体（窗体实例重建后自动恢复）
        Private ReadOnly _queueMenuTimer As New Timer() With {.Interval = 2000}
        Private ReadOnly _taskIds As New List(Of String)()
        Private _pendingPreviewTaskId As String = ""
        Private _quadForm As QuadGridForm
        Private _engine As PreviewEngine
        Private _lastPreviewImage As Image
        ' ────────────────────────── 实时预览页 ──────────────────────────

        Private Sub BuildOfficialPreviewPage()
            _pagePreview.Dock = DockStyle.Fill
            _pagePreview.BackColor = Color.Transparent
            _pagePreview.Padding = New Padding(0, 4, 0, 0)

            Dim root As New ModernGridPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 5,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 44.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))

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
            taskRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 88.0F))
            taskRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
            taskRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 240.0F))
            taskRow.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            _lblTask.Text = "<font color=#C0C0C0>预览任务</font>"
            _lblTask.AutoSize = False
            _lblTask.Dock = DockStyle.Fill
            _lblTask.Margin = Padding.Empty
            _lblTask.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _cmbTask.WaterText = "选择要预览的任务…"
            ConfigureCombo(_cmbTask)
            _cmbTask.Dock = DockStyle.Fill
            _cmbTask.Margin = New Padding(0, 4, 0, 4)
            AddHandler _cmbTask.SelectedIndexChanged, AddressOf OnTaskSelected
            Dim taskHint = CreateOfficialCaption("可查看处理中或已经完成的帧")
            taskHint.TextAlign = ContentAlignment.MiddleLeft
            taskHint.Margin = New Padding(UiColumnGap, 0, 0, 0)
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
            footer.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 80.0F))
            footer.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 140.0F))
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
            _cmbRate.Margin = New Padding(UiColumnGap, 4, 0, 4)
            AddHandler _cmbRate.SelectedIndexChanged, AddressOf OnRateSelected
            footer.AddAt(_lblPreviewNote, 0, 0)
            footer.AddAt(_lblRate, 1, 0)
            footer.AddAt(_cmbRate, 2, 0)
            root.AddAt(footer, 0, 4)
            _pagePreview.Controls.Add(root)
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
            If _tabs.SelectedIndex = _tabIndexTutorial Then
                EnsureMarkdownPage(_pageTutorial)
            End If
            ' 切换页面时清除底部状态提示
            ClearStatus()
            _btnCleanArchives.Visible = (_tabs.SelectedIndex = _tabIndexDownloader)
            If _tabs.SelectedIndex = _tabIndexDownloader Then
                LoadDownloadModels(False)
            End If
            If _tabs.SelectedIndex = _tabIndexImporter Then
                LoadUserModels()
            End If
            If _tabs.SelectedIndex = _tabIndexSegmented Then ActivateSegmentedPage()
            If _tabs.SelectedIndex = _tabIndexShell Then LoadShellModels()
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
    End Class

End Namespace
