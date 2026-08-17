Imports System
Imports System.Collections
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Diagnostics
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Text
Imports System.Windows.Forms
Imports FFmpegFreeUI
Imports LakeUI

Namespace videoenhancer

    ''' <summary>
    ''' 把 3fui 所有"添加文件到编码队列"的入口改为 videoenhancer.exe 中转：
    ''' 1) "准备文件 - 加入编码队列"按钮；2) 编码队列页拖入文件；3) 编码队列右键菜单"添加文件到队列"。
    ''' 每个任务生成 -i / -modelpath / -ffmpeg-settings / -pause-shm / -stop-shm 参数，
    ''' 由 设置_v6.替代进程文件名 指向 videoenhancer.exe 执行。
    ''' 同时 hook 队列页的"暂停/恢复"按钮与空格键：先把暂停字节写入后端共享内存，
    ''' 再交给 3fui 原逻辑（原逻辑挂起/恢复的是中转进程本身，对真正的 python 后端无效）。
    ''' </summary>
    Friend Class QueueHook

        Public Shared HostAddMissionToQueueWithArgs As Action(Of String, String, String, String)

        Private Shared _prepareForm As Object
        Private Shared _queueButton As Control
        Private Shared _originalClick As [Delegate]
        Private Shared _hookedClick As EventHandler
        Private Shared _installed As Boolean = False

        ' 编码队列窗体相关 hook
        Private Shared _queueForm As Object
        Private Shared _listView As Control
        Private Shared _originalDragDrop As [Delegate]
        Private Shared _hookedDragDrop As DragEventHandler
        Private Shared _originalKeyDown As [Delegate]
        Private Shared _hookedKeyDown As KeyEventHandler
        Private Shared _menuItem As Object
        Private Shared _originalMenuItemClick As [Delegate]
        Private Shared _hookedMenuItemClick As EventHandler
        Private Shared _btnPause As Control
        Private Shared _originalPauseClick As [Delegate]
        Private Shared _hookedPauseClick As EventHandler
        Private Shared _btnResume As Control
        Private Shared _originalResumeClick As [Delegate]
        Private Shared _hookedResumeClick As EventHandler
        Private Shared _btnStop As Control
        Private Shared _originalStopClick As [Delegate]
        Private Shared _hookedStopClick As EventHandler

        Public Shared ReadOnly Property IsInstalled As Boolean
            Get
                Return _installed
            End Get
        End Property

        ''' <summary>安装钩子：替换"加入编码队列"按钮、队列页拖入/菜单、暂停/恢复按钮处理器。</summary>
        Public Shared Function Install() As Boolean
            Uninstall()

            Dim form = HostAccess.GetDefaultInstance("Form_v6_准备文件")
            If form Is Nothing Then
                Return False
            End If
            Dim button = HostAccess.FindQueueButton(form)
            If button Is Nothing Then
                Return False
            End If

            _prepareForm = form
            _queueButton = button
            _originalClick = RemoveControlEvent(button, "Click")
            _hookedClick = New EventHandler(AddressOf OnQueueClicked)
            AddHandler button.Click, _hookedClick

            HookQueueForm()

            _installed = True
            Return True
        End Function

        ''' <summary>卸载钩子：恢复 3fui 原始处理器。</summary>
        Public Shared Sub Uninstall()
            Try
                If _queueButton IsNot Nothing AndAlso _hookedClick IsNot Nothing Then
                    RemoveHandler _queueButton.Click, _hookedClick
                    RestoreControlEvent(_queueButton, "Click", _originalClick)
                End If
            Catch
            End Try
            Try
                If _listView IsNot Nothing Then
                    If _hookedDragDrop IsNot Nothing Then RemoveHandler _listView.DragDrop, _hookedDragDrop
                    RestoreControlEvent(_listView, "DragDrop", _originalDragDrop)
                    If _hookedKeyDown IsNot Nothing Then RemoveHandler _listView.KeyDown, _hookedKeyDown
                    RestoreControlEvent(_listView, "KeyDown", _originalKeyDown)
                End If
            Catch
            End Try
            Try
                If _menuItem IsNot Nothing Then
                    Dim field = _menuItem.GetType().GetField("ClickEvent", BindingFlags.NonPublic Or BindingFlags.Instance)
                    If field IsNot Nothing Then
                        field.SetValue(_menuItem, _originalMenuItemClick)
                    End If
                End If
            Catch
            End Try
            Try
                If _btnPause IsNot Nothing AndAlso _hookedPauseClick IsNot Nothing Then
                    RemoveHandler _btnPause.Click, _hookedPauseClick
                    RestoreControlEvent(_btnPause, "Click", _originalPauseClick)
                End If
                If _btnResume IsNot Nothing AndAlso _hookedResumeClick IsNot Nothing Then
                    RemoveHandler _btnResume.Click, _hookedResumeClick
                    RestoreControlEvent(_btnResume, "Click", _originalResumeClick)
                End If
                If _btnStop IsNot Nothing AndAlso _hookedStopClick IsNot Nothing Then
                    RemoveHandler _btnStop.Click, _hookedStopClick
                    RestoreControlEvent(_btnStop, "Click", _originalStopClick)
                End If
            Catch
            End Try

            _installed = False
            _prepareForm = Nothing
            _queueButton = Nothing
            _originalClick = Nothing
            _hookedClick = Nothing
            _queueForm = Nothing
            _listView = Nothing
            _originalDragDrop = Nothing
            _hookedDragDrop = Nothing
            _originalKeyDown = Nothing
            _hookedKeyDown = Nothing
            _menuItem = Nothing
            _originalMenuItemClick = Nothing
            _hookedMenuItemClick = Nothing
            _btnPause = Nothing
            _originalPauseClick = Nothing
            _hookedPauseClick = Nothing
            _btnResume = Nothing
            _originalResumeClick = Nothing
            _hookedResumeClick = Nothing
            _btnStop = Nothing
            _originalStopClick = Nothing
            _hookedStopClick = Nothing
        End Sub

        ''' <summary>挂载编码队列窗体的拖入/菜单/暂停恢复入口。</summary>
        Private Shared Sub HookQueueForm()
            Try
                Dim queueForm = HostAccess.GetDefaultInstance("Form_v6_编码队列")
                If queueForm Is Nothing Then
                    Return
                End If
                _queueForm = queueForm

                Dim listView = TryCast(HostAccess.GetField(queueForm, "_UltraDetailListView1", "UltraDetailListView1"), Control)
                If listView IsNot Nothing Then
                    _listView = listView
                    _originalDragDrop = RemoveControlEvent(listView, "DragDrop")
                    _hookedDragDrop = New DragEventHandler(AddressOf OnListDragDrop)
                    AddHandler listView.DragDrop, _hookedDragDrop
                    _originalKeyDown = RemoveControlEvent(listView, "KeyDown")
                    _hookedKeyDown = New KeyEventHandler(AddressOf OnListKeyDown)
                    AddHandler listView.KeyDown, _hookedKeyDown
                End If

                Dim menu = HostAccess.GetField(queueForm, "_任务菜单", "任务菜单")
                If menu IsNot Nothing Then
                    Dim itemsProp = menu.GetType().GetProperty("Items")
                    If itemsProp IsNot Nothing Then
                        Dim items = TryCast(itemsProp.GetValue(menu), IList)
                        If items IsNot Nothing AndAlso items.Count > 0 Then
                            _menuItem = items(0)
                            Dim field = _menuItem.GetType().GetField("ClickEvent", BindingFlags.NonPublic Or BindingFlags.Instance)
                            If field IsNot Nothing Then
                                _originalMenuItemClick = TryCast(field.GetValue(_menuItem), [Delegate])
                                _hookedMenuItemClick = New EventHandler(AddressOf OnMenuItemClicked)
                                field.SetValue(_menuItem, [Delegate].Combine(Nothing, _hookedMenuItemClick))
                            End If
                        End If
                    End If
                End If

                Dim btnPause = TryCast(HostAccess.GetField(queueForm, "_ModernButton2", "ModernButton2"), Control)
                If btnPause IsNot Nothing Then
                    _btnPause = btnPause
                    _originalPauseClick = RemoveControlEvent(btnPause, "Click")
                    _hookedPauseClick = New EventHandler(AddressOf OnPauseClicked)
                    AddHandler btnPause.Click, _hookedPauseClick
                End If

                Dim btnResume = TryCast(HostAccess.GetField(queueForm, "_ModernButton3", "ModernButton3"), Control)
                If btnResume IsNot Nothing Then
                    _btnResume = btnResume
                    _originalResumeClick = RemoveControlEvent(btnResume, "Click")
                    _hookedResumeClick = New EventHandler(AddressOf OnResumeClicked)
                    AddHandler btnResume.Click, _hookedResumeClick
                End If

                Dim btnStop = TryCast(HostAccess.GetField(queueForm, "_ModernButton4", "ModernButton4"), Control)
                If btnStop IsNot Nothing Then
                    _btnStop = btnStop
                    _originalStopClick = RemoveControlEvent(btnStop, "Click")
                    _hookedStopClick = New EventHandler(AddressOf OnStopClicked)
                    AddHandler btnStop.Click, _hookedStopClick
                End If
            Catch
            End Try
        End Sub

        ''' <summary>移除控件指定事件的全部处理器并返回原委托。</summary>
        Private Shared Function RemoveControlEvent(control As Control, eventName As String) As [Delegate]
            Dim events = GetEventHandlers(control)
            Dim key = GetControlEventKey(eventName)
            If events Is Nothing OrElse key Is Nothing Then
                Return Nothing
            End If
            Dim existing = events(key)
            If existing IsNot Nothing Then
                events.RemoveHandler(key, existing)
            End If
            Return existing
        End Function

        ''' <summary>把原委托放回控件事件列表（卸载钩子时恢复）。</summary>
        Private Shared Sub RestoreControlEvent(control As Control, eventName As String, original As [Delegate])
            If original Is Nothing Then
                Return
            End If
            Dim events = GetEventHandlers(control)
            Dim key = GetControlEventKey(eventName)
            If events IsNot Nothing AndAlso key IsNot Nothing Then
                events.AddHandler(key, original)
            End If
        End Sub

        ''' <summary>获取控件的事件列表（.NET Core 用 Events 属性，.NET Framework 用 events 字段）。</summary>
        Private Shared Function GetEventHandlers(control As Control) As EventHandlerList
            Try
                Dim prop = GetType(Control).GetProperty("Events", BindingFlags.NonPublic Or BindingFlags.Instance)
                If prop IsNot Nothing Then
                    Return TryCast(prop.GetValue(control), EventHandlerList)
                End If
            Catch
            End Try
            Try
                Dim field = GetType(Control).GetField("events", BindingFlags.NonPublic Or BindingFlags.Instance)
                If field IsNot Nothing Then
                    Return TryCast(field.GetValue(control), EventHandlerList)
                End If
            Catch
            End Try
            Return Nothing
        End Function

        ''' <summary>获取事件在 EventHandlerList 中的键（.NET Core+ 为 s_xxxEvent，.NET Framework 为 EventXxx）。</summary>
        Private Shared Function GetControlEventKey(eventName As String) As Object
            Try
                Dim coreName = "s_" & eventName & "Event"
                For Each field In GetType(Control).GetFields(BindingFlags.NonPublic Or BindingFlags.Static)
                    If field.FieldType Is GetType(Object) AndAlso String.Equals(field.Name, coreName, StringComparison.OrdinalIgnoreCase) Then
                        Return field.GetValue(Nothing)
                    End If
                Next
                Dim legacyName = "Event" & eventName
                For Each field In GetType(Control).GetFields(BindingFlags.NonPublic Or BindingFlags.Static)
                    If field.FieldType Is GetType(Object) AndAlso String.Equals(field.Name, legacyName, StringComparison.OrdinalIgnoreCase) Then
                        Return field.GetValue(Nothing)
                    End If
                Next
            Catch
            End Try
            Return Nothing
        End Function

        ''' <summary>"加入编码队列"被点击：把每个文件作为 videoenhancer.exe 命令行任务加入编码队列。</summary>
        Private Shared Sub OnQueueClicked(sender As Object, e As EventArgs)
            Try
                Dim form = _prepareForm
                If form Is Nothing Then
                    ShowTip("视频超分插件未就绪")
                    Return
                End If
                If Not PluginConfig.Load().Enabled Then
                    ShowTip("请先在""视频超分""页面点击启用")
                    Return
                End If

                Dim files = GetFilePaths(form)
                If files.Count = 0 Then
                    ShowTip("请先添加文件")
                    Return
                End If
                EnqueueWrappedFiles(files)
                ClearFileList(form)
            Catch ex As Exception
                ShowTip("加入队列失败：" & ex.Message)
            End Try
        End Sub

        ''' <summary>编码队列页拖入文件：转为 videoenhancer.exe 中转任务。</summary>
        Private Shared Sub OnListDragDrop(sender As Object, e As DragEventArgs)
            If e Is Nothing OrElse e.Data Is Nothing Then
                Return
            End If
            Dim files = TryCast(e.Data.GetData(DataFormats.FileDrop), String())
            If files Is Nothing OrElse files.Length = 0 Then
                Return
            End If
            Try
                EnqueueWrappedFiles(files)
            Catch ex As Exception
                ShowTip("加入队列失败：" & ex.Message)
            End Try
        End Sub

        ''' <summary>编码队列右键菜单"添加文件到队列"。</summary>
        Private Shared Sub OnMenuItemClicked(sender As Object, e As EventArgs)
            Try
                Using dialog As New OpenFileDialog With {
                    .Multiselect = True,
                    .Filter = "所有文件|*.*"
                }
                    If dialog.ShowDialog() <> DialogResult.OK Then
                        Return
                    End If
                    EnqueueWrappedFiles(dialog.FileNames)
                End Using
            Catch ex As Exception
                ShowTip("加入队列失败：" & ex.Message)
            End Try
        End Sub

        ''' <summary>暂停按钮：先写后端暂停字节，再执行 3fui 原逻辑。</summary>
        Private Shared Sub OnPauseClicked(sender As Object, e As EventArgs)
            Try
                PauseControl.WriteForSelectedTasks(1)
            Catch
            End Try
            InvokeOriginal(_originalPauseClick, sender, e)
        End Sub

        ''' <summary>恢复按钮：先写后端恢复字节，再执行 3fui 原逻辑。</summary>
        Private Shared Sub OnResumeClicked(sender As Object, e As EventArgs)
            Try
                PauseControl.WriteForSelectedTasks(0)
            Catch
            End Try
            InvokeOriginal(_originalResumeClick, sender, e)
        End Sub

        ''' <summary>停止按钮：先写后端停止字节（CLI 优雅停止并保留已处理部分），再把任务标记为手动停止。</summary>
        Private Shared Sub OnStopClicked(sender As Object, e As EventArgs)
            Try
                StopControl.StopSelectedTasks()
            Catch
            End Try
        End Sub

        ''' <summary>空格键暂停/恢复：先按当前状态写字节，再执行 3fui 原逻辑。</summary>
        Private Shared Sub OnListKeyDown(sender As Object, e As KeyEventArgs)
            If e IsNot Nothing AndAlso e.KeyCode = Keys.Space Then
                Try
                    Dim paused = HasPausedSelected()
                    PauseControl.WriteForSelectedTasks(If(paused, CByte(0), CByte(1)))
                Catch
                End Try
            End If
            InvokeOriginal(_originalKeyDown, sender, e)
        End Sub

        Private Shared Sub InvokeOriginal(original As [Delegate], ParamArray args As Object())
            If original Is Nothing Then
                Return
            End If
            Try
                original.DynamicInvoke(args)
            Catch
            End Try
        End Sub

        Private Shared Function HasPausedSelected() As Boolean
            Try
                Dim queueForm = _queueForm
                If queueForm Is Nothing Then
                    Return False
                End If
                Dim listView = HostAccess.GetField(queueForm, "_UltraDetailListView1", "UltraDetailListView1")
                If listView Is Nothing Then
                    Return False
                End If
                Dim selected = HostAccess.GetProperty(listView, "SelectedItems")
                Dim items = TryCast(selected, IEnumerable)
                If items Is Nothing Then
                    Return False
                End If
                For Each item In items
                    Dim id = TryCast(HostAccess.GetProperty(item, "Tag"), String)
                    If Not String.IsNullOrWhiteSpace(id) Then
                        Dim task = 编码队列_v6.根据ID获取任务(id)
                        If task IsNot Nothing AndAlso task.状态 = 编码任务状态_v6.已暂停 Then
                            Return True
                        End If
                    End If
                Next
            Catch
            End Try
            Return False
        End Function

        ''' <summary>把文件列表包装成 videoenhancer.exe 任务加入编码队列（支持目录递归）。</summary>
        Public Shared Sub EnqueueWrappedFiles(files As IEnumerable(Of String))
            If files Is Nothing Then
                Return
            End If
            If Not PluginConfig.Load().Enabled Then
                ShowTip("请先在""视频超分""页面点击启用")
                Return
            End If

            Dim entries As New List(Of String)
            For Each f In files
                If String.IsNullOrWhiteSpace(f) Then
                    Continue For
                End If
                If Directory.Exists(f) Then
                    entries.AddRange(Directory.GetFiles(f, "*", SearchOption.AllDirectories))
                Else
                    entries.Add(f)
                End If
            Next
            If entries.Count = 0 Then
                ShowTip("请先添加文件")
                Return
            End If
            Dim missing = entries.FirstOrDefault(Function(f) Not File.Exists(f))
            If missing IsNot Nothing Then
                ShowTip("文件不存在：" & missing)
                Return
            End If

            Dim cfg = PluginConfig.Load()
            If Not cfg.Enabled Then
                ShowTip("请先在""视频超分""页面开启插件总开关")
                Return
            End If
            If Not cfg.UpscaleEnabled AndAlso Not cfg.InterpEnabled Then
                ShowTip("请先打开超分或补帧开关")
                Return
            End If
            If cfg.UpscaleEnabled AndAlso String.IsNullOrWhiteSpace(cfg.Model) Then
                ShowTip("请先在""视频超分""页面选择放大模型")
                Return
            End If
            If cfg.InterpEnabled AndAlso String.IsNullOrWhiteSpace(cfg.InterpModel) Then
                ShowTip("请先在""视频超分""页面选择补帧模型")
                Return
            End If

            Dim panel = HostAccess.GetDefaultInstance("Form_v6_参数面板")
            If panel Is Nothing Then
                ShowTip("无法读取参数面板，请稍后重试")
                Return
            End If

            Dim preset = 预设管理_v6.从面板创建预设(DirectCast(panel, Form_v6_参数面板))
            Dim reserved As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Dim added As Integer = 0
            For Each input As String In entries
                Dim output = 编码队列_v6.计算输出位置_v6(input, preset, True, reserved)
                If String.IsNullOrWhiteSpace(output) Then
                    output = FallbackOutputPath(input, preset)
                End If
                reserved.Add(output)

                Dim settings = BuildFfmpegSettings(preset, input, output)
                Dim pauseShm = "ve_plugin_pause_" & Guid.NewGuid().ToString("N")
                Dim stopShm = "ve_plugin_stop_" & Guid.NewGuid().ToString("N")
                Dim args = BuildCliArgs(input, output, cfg.Model, settings, pauseShm, stopShm, cfg.UpscaleEnabled, cfg.InterpModel, cfg.InterpEnabled)
                AddQueueTask(args, Path.GetFileName(input), output, input)
                added += 1
            Next

            If added > 0 Then
                SwitchToQueueTab()
                ShowTip($"已添加 {added} 个视频超分任务到编码队列")
            End If
        End Sub

        Private Shared Sub AddQueueTask(args As String, name As String, output As String, input As String)
            Try
                If HostAddMissionToQueueWithArgs IsNot Nothing Then
                    HostAddMissionToQueueWithArgs(args, name, output, input)
                Else
                    插件管理.使用命令行添加任务到编码队列(args, name, output, input)
                End If
            Catch
            End Try
        End Sub

        Private Shared Function GetFilePaths(form As Object) As List(Of String)
            Dim result As New List(Of String)
            Dim listView = HostAccess.GetFileListView(form)
            If listView Is Nothing Then
                Return result
            End If
            Dim items = HostAccess.GetProperty(listView, "Items")
            Dim itemList = TryCast(items, IList)
            If itemList Is Nothing Then
                Return result
            End If
            Dim getPath = form.GetType().GetMethod("获取项路径", BindingFlags.Public Or BindingFlags.NonPublic Or BindingFlags.Instance)
            For Each item As Object In itemList
                If getPath IsNot Nothing Then
                    Dim path = TryCast(getPath.Invoke(form, {item}), String)
                    If Not String.IsNullOrWhiteSpace(path) Then
                        result.Add(path)
                    End If
                Else
                    Dim subItems = TryCast(item.GetType().GetProperty("SubItems").GetValue(item), IList)
                    If subItems IsNot Nothing AndAlso subItems.Count > 1 Then
                        Dim text = TryCast(subItems(1).GetType().GetProperty("Text").GetValue(subItems(1)), String)
                        If Not String.IsNullOrWhiteSpace(text) Then
                            result.Add(text)
                        End If
                    End If
                End If
            Next
            Return result
        End Function

        Private Shared Sub ClearFileList(form As Object)
            Dim listView = HostAccess.GetFileListView(form)
            If listView Is Nothing Then
                Return
            End If
            Dim items = TryCast(HostAccess.GetProperty(listView, "Items"), IList)
            If items IsNot Nothing Then
                items.Clear()
            End If
        End Sub

        Private Shared Sub SwitchToQueueTab()
            Try
                Dim main = HostAccess.GetDefaultInstance("FormMain_v6")
                If main Is Nothing Then
                    Return
                End If
                Dim tabControl = HostAccess.GetField(main, "_ModernTabListControl1", "ModernTabListControl1")
                HostAccess.SetProperty(tabControl, "SelectedIndex", 2)
            Catch
            End Try
        End Sub

        Private Shared Sub ShowTip(text As String)
            Try
                Dim anchor As Control = _queueButton
                If anchor Is Nothing AndAlso Application.OpenForms.Count > 0 Then
                    anchor = Application.OpenForms(0)
                End If
                If anchor Is Nothing Then
                    anchor = New Control()
                End If
                ExFloatingTipModule.ExFloatingTip(anchor, text, 2200)
            Catch
            End Try
        End Sub

        ' ────────────────────────── 命令构建 ──────────────────────────

        ''' <summary>构建 videoenhancer.exe 的参数：-i / -modelpath / -ffmpeg-settings / -pause-shm / -stop-shm / -interp-model / -no-upscale。</summary>
        Public Shared Function BuildCliArgs(input As String, output As String, model As String, ffmpegSettings As String, Optional pauseShm As String = "", Optional stopShm As String = "", Optional upscaleOn As Boolean = True, Optional interpModel As String = "", Optional interpOn As Boolean = False) As String
            Dim sb As New StringBuilder()
            sb.Append("-i ").Append(Arg(input))
            If upscaleOn AndAlso Not String.IsNullOrWhiteSpace(model) Then
                sb.Append(" -modelpath ").Append(Arg(model))
            End If
            If interpOn AndAlso Not String.IsNullOrWhiteSpace(interpModel) Then
                sb.Append(" -interp-model ").Append(Arg(interpModel))
            End If
            If interpOn AndAlso Not upscaleOn Then
                sb.Append(" -no-upscale")
            End If
            sb.Append(" -ffmpeg-settings ").Append(Arg(ffmpegSettings))
            If Not String.IsNullOrWhiteSpace(pauseShm) Then
                sb.Append(" -pause-shm ").Append(Arg(pauseShm))
            End If
            If Not String.IsNullOrWhiteSpace(stopShm) Then
                sb.Append(" -stop-shm ").Append(Arg(stopShm))
            End If
            Return sb.ToString()
        End Function

        ''' <summary>
        ''' 从参数面板预设生成 -ffmpeg-settings 内容：
        ''' 取 3fui 命令行模板中"-i 输入"之后的部分（编码参数 + 输出路径），
        ''' 输出路径替换为真实路径，末尾补 -y 让后端允许覆盖。
        ''' </summary>
        Public Shared Function BuildFfmpegSettings(preset As 预设数据_v6, input As String, output As String) As String
            Dim cmd = 预设管理_v6.将预设数据转换为命令行(preset, 预设管理_v6.输入占位符, 预设管理_v6.输出占位符)
            If String.IsNullOrWhiteSpace(cmd) Then
                Return QuotePath(output) & " -y"
            End If

            Dim tokens = Tokenize(cmd)

            ' 丢弃输入段：-hide_banner -y … -i "<输入文件>" 之前的所有内容
            Dim start As Integer = -1
            For i As Integer = 0 To tokens.Count - 2
                If tokens(i).Text = "-i" AndAlso tokens(i + 1).Text = 预设管理_v6.输入占位符 Then
                    start = i + 2
                    Exit For
                End If
            Next
            If start < 0 Then
                start = 0
                While start < tokens.Count AndAlso (tokens(start).Text = "-hide_banner" OrElse tokens(start).Text = "-y")
                    start += 1
                End While
            End If

            Dim kept = tokens.Skip(start).ToList()
            Dim duration As String = ""
            If kept.Any(Function(t) t.Text = 预设管理_v6.媒体总时长占位符) Then
                duration = ResolveDuration(input)
            End If
            Dim parts As New List(Of String)
            For Each token In kept
                Dim value = token.Text
                If value = 预设管理_v6.输出占位符 Then
                    parts.Add(QuotePath(output))
                ElseIf value = 预设管理_v6.媒体总时长占位符 Then
                    If Not String.IsNullOrEmpty(duration) Then
                        parts.Add(duration)
                    End If
                Else
                    If token.WasQuoted AndAlso value.Contains(" "c) Then
                        parts.Add(QuotePath(value))
                    Else
                        parts.Add(value)
                    End If
                End If
            Next

            If parts.Count = 0 Then
                Return QuotePath(output) & " -y"
            End If
            If Not String.Equals(parts(parts.Count - 1), "-y", StringComparison.OrdinalIgnoreCase) Then
                parts.Add("-y")
            End If
            Return String.Join(" ", parts)
        End Function

        Private Shared Function FallbackOutputPath(input As String, preset As 预设数据_v6) As String
            Dim dir = If(Path.GetDirectoryName(input), "")
            Dim name = Path.GetFileNameWithoutExtension(input)
            Dim ext = If(preset Is Nothing, "", (preset.输出容器 & "").Trim())
            If ext = "" Then
                ext = ".mkv"
            End If
            If Not ext.StartsWith("."c) Then
                ext = "." & ext
            End If
            Dim basePath = Path.Combine(dir, name & "_超分" & ext)
            If Not File.Exists(basePath) Then
                Return basePath
            End If
            Dim i As Integer = 1
            While True
                Dim candidate = Path.Combine(dir, $"{name}_超分 ({i}){ext}")
                If Not File.Exists(candidate) Then
                    Return candidate
                End If
                i += 1
            End While
            Return basePath
        End Function

        ''' <summary>用 videoenhancer.exe 自带的 ffprobe 解析媒体总时长（仅当模板含占位符时调用）。</summary>
        Private Shared Function ResolveDuration(input As String) As String
            Try
                Dim exeDir = Path.GetDirectoryName(PluginConfig.Load().ExePath)
                Dim ffprobe = If(exeDir Is Nothing, "ffprobe", Path.Combine(exeDir, "bin", "ffmpeg", "ffprobe.exe"))
                If Not File.Exists(ffprobe) Then
                    ffprobe = "ffprobe"
                End If
                Dim psi As New ProcessStartInfo With {
                    .FileName = ffprobe,
                    .UseShellExecute = False,
                    .RedirectStandardOutput = True,
                    .RedirectStandardError = True,
                    .CreateNoWindow = True,
                    .StandardOutputEncoding = Encoding.UTF8
                }
                psi.ArgumentList.Add("-v")
                psi.ArgumentList.Add("error")
                psi.ArgumentList.Add("-show_entries")
                psi.ArgumentList.Add("format=duration")
                psi.ArgumentList.Add("-of")
                psi.ArgumentList.Add("default=noprint_wrappers=1:nokey=1")
                psi.ArgumentList.Add(input)
                Using p = Process.Start(psi)
                    If p Is Nothing Then
                        Return ""
                    End If
                    Dim output = p.StandardOutput.ReadToEnd().Trim()
                    p.WaitForExit(15000)
                    Return output
                End Using
            Catch
                Return ""
            End Try
        End Function

        Private Shared Function QuotePath(value As String) As String
            Return """" & value & """"
        End Function

        ''' <summary>Windows 命令行参数加引号，内部双引号转义为 \"。</summary>
        Private Shared Function Arg(value As String) As String
            Return """" & value.Replace(""""c, "\""") & """"
        End Function

        Public Structure Token
            Public Text As String
            Public WasQuoted As Boolean
        End Structure

        ''' <summary>Windows 风格按空白拆分，双引号包裹的空格保留在令牌内。</summary>
        Public Shared Function Tokenize(line As String) As List(Of Token)
            Dim tokens As New List(Of Token)
            Dim sb As New StringBuilder()
            Dim inQuotes As Boolean = False
            Dim tokenQuoted As Boolean = False
            For Each c As Char In line
                If c = """"c Then
                    inQuotes = Not inQuotes
                    tokenQuoted = True
                ElseIf Char.IsWhiteSpace(c) AndAlso Not inQuotes Then
                    If sb.Length > 0 Then
                        tokens.Add(New Token With {.Text = sb.ToString(), .WasQuoted = tokenQuoted})
                        sb.Clear()
                        tokenQuoted = False
                    End If
                Else
                    sb.Append(c)
                End If
            Next
            If sb.Length > 0 Then
                tokens.Add(New Token With {.Text = sb.ToString(), .WasQuoted = tokenQuoted})
            End If
            Return tokens
        End Function

    End Class

End Namespace


