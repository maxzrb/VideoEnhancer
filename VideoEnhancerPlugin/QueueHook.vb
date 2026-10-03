Imports System
Imports System.Collections
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Diagnostics
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Text
Imports System.Text.Json
Imports System.Windows.Forms
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
        Private Shared _contextMenu As Object
        Private Shared _previewMenuItem As Object
        Private Shared _originalMouseUp As [Delegate]
        Private Shared _hookedMouseUp As MouseEventHandler
        Private Shared _hookedListView As Control
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
                ' 注意：「预览输出」菜单项不随 hook 卸载而移除（实时预览与主开关无关）。
                If _hookedListView IsNot Nothing AndAlso _hookedMouseUp IsNot Nothing Then
                    RemoveHandler _hookedListView.MouseUp, _hookedMouseUp
                    RestoreControlEvent(_hookedListView, "MouseUp", _originalMouseUp)
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
            _contextMenu = Nothing
            _previewMenuItem = Nothing
            _originalMouseUp = Nothing
            _hookedMouseUp = Nothing
            _hookedListView = Nothing
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

                ' 右键菜单：追加「预览输出」（跳到实时预览页并选中该任务）
                ' 「预览输出」右键菜单项与插件总开关无关：实时预览始终可用，由
                ' EnsurePreviewMenuItem 统一管理（挂 MouseUp 保证显示菜单前已存在）。
                EnsurePreviewMenuItem()

                HookActionButtons(queueForm)
            Catch ex As Exception
                Trace.WriteLine("[VideoEnhancer][队列] 挂载队列控件失败：" & ex.Message)
            End Try
        End Sub

        ''' <summary>按字段名优先、控件 Name/Text 辅助识别暂停、恢复和停止按钮。</summary>
        Private Shared Sub HookActionButtons(queueForm As Object)
            If queueForm Is Nothing Then Return
            Dim btnPause = FindQueueActionButton(queueForm, "pause")
            Dim btnResume = FindQueueActionButton(queueForm, "resume")
            Dim btnStop = FindQueueActionButton(queueForm, "stop")

            If btnPause IsNot Nothing AndAlso Not ReferenceEquals(_btnPause, btnPause) Then
                If _btnPause IsNot Nothing AndAlso _hookedPauseClick IsNot Nothing Then
                    RemoveHandler _btnPause.Click, _hookedPauseClick
                    RestoreControlEvent(_btnPause, "Click", _originalPauseClick)
                End If
                _btnPause = btnPause
                _originalPauseClick = RemoveControlEvent(btnPause, "Click")
                _hookedPauseClick = New EventHandler(AddressOf OnPauseClicked)
                AddHandler btnPause.Click, _hookedPauseClick
            End If
            If btnResume IsNot Nothing AndAlso Not ReferenceEquals(_btnResume, btnResume) Then
                If _btnResume IsNot Nothing AndAlso _hookedResumeClick IsNot Nothing Then
                    RemoveHandler _btnResume.Click, _hookedResumeClick
                    RestoreControlEvent(_btnResume, "Click", _originalResumeClick)
                End If
                _btnResume = btnResume
                _originalResumeClick = RemoveControlEvent(btnResume, "Click")
                _hookedResumeClick = New EventHandler(AddressOf OnResumeClicked)
                AddHandler btnResume.Click, _hookedResumeClick
            End If
            If btnStop IsNot Nothing AndAlso Not ReferenceEquals(_btnStop, btnStop) Then
                If _btnStop IsNot Nothing AndAlso _hookedStopClick IsNot Nothing Then
                    RemoveHandler _btnStop.Click, _hookedStopClick
                    RestoreControlEvent(_btnStop, "Click", _originalStopClick)
                End If
                _btnStop = btnStop
                _originalStopClick = RemoveControlEvent(btnStop, "Click")
                _hookedStopClick = New EventHandler(AddressOf OnStopClicked)
                AddHandler btnStop.Click, _hookedStopClick
            End If
        End Sub

        Private Shared Function FindQueueActionButton(queueForm As Object, role As String) As Control
            Dim aliases As String()
            Select Case role
                Case "pause"
                    aliases = New String() {"_ModernButton2", "ModernButton2", "暂停", "pause"}
                Case "resume"
                    aliases = New String() {"_ModernButton3", "ModernButton3", "恢复", "resume"}
                Case Else
                    aliases = New String() {"_ModernButton4", "ModernButton4", "停止", "stop"}
            End Select

            ' 先按已知字段名读取，避免宿主控件尚未加入 Controls 集合时漏挂。
            For Each aliasName In aliases
                Dim fieldControl = TryCast(HostAccess.GetField(queueForm, aliasName), Control)
                If fieldControl IsNot Nothing Then Return fieldControl
            Next

            Dim root = TryCast(queueForm, Control)
            If root Is Nothing Then Return Nothing
            Return FindActionButtonRecursive(root, role)
        End Function

        Private Shared Function FindActionButtonRecursive(parent As Control, role As String) As Control
            For Each child As Control In parent.Controls
                Dim name = If(child.Name, "")
                Dim text = If(child.Text, "")
                Dim typeName = child.GetType().Name
                Dim nameMatch = role = "pause" AndAlso (name.IndexOf("pause", StringComparison.OrdinalIgnoreCase) >= 0 OrElse name.Contains("暂停")) OrElse
                    role = "resume" AndAlso (name.IndexOf("resume", StringComparison.OrdinalIgnoreCase) >= 0 OrElse name.Contains("恢复")) OrElse
                    role = "stop" AndAlso (name.IndexOf("stop", StringComparison.OrdinalIgnoreCase) >= 0 OrElse name.Contains("停止"))
                Dim textMatch = role = "pause" AndAlso text.Contains("暂停") OrElse
                    role = "resume" AndAlso text.Contains("恢复") OrElse
                    role = "stop" AndAlso text.Contains("停止")
                If nameMatch OrElse (textMatch AndAlso typeName.IndexOf("button", StringComparison.OrdinalIgnoreCase) >= 0) Then
                    Return child
                End If
                Dim nested = FindActionButtonRecursive(child, role)
                If nested IsNot Nothing Then Return nested
            Next
            Return Nothing
        End Function

        Private Shared Sub UnhookActionButtons()
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
            Catch ex As Exception
                Trace.WriteLine("[VideoEnhancer][队列] 卸载按钮钩子失败：" & ex.Message)
            End Try
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

        ''' <summary>
        ''' 把「预览输出」右键菜单项挂到当前编码队列窗体。
        ''' 与插件总开关无关：实时预览始终可用，插件面板启动时与定期调用本方法。
        ''' </summary>
        Public Shared Sub AttachQueueMenu()
            Try
                If SyncQueueForm() Then
                    EnsurePreviewMenuItem()
                End If
            Catch
            End Try
        End Sub

        ''' <summary>同步队列窗体引用；窗体实例变化时重置相关字段（下次 Ensure 时重挂）。</summary>
        Private Shared Function SyncQueueForm() As Boolean
            Dim queueForm = HostAccess.GetDefaultInstance("Form_v6_编码队列")
            If queueForm Is Nothing Then
                Return False
            End If
            If Not ReferenceEquals(_queueForm, queueForm) Then
                UnhookActionButtons()
                _queueForm = queueForm
                _listView = TryCast(HostAccess.GetField(queueForm, "_UltraDetailListView1", "UltraDetailListView1"), Control)
                _contextMenu = TryCast(HostAccess.GetField(queueForm, "_右键菜单", "右键菜单"), ModernContextMenu)
                _previewMenuItem = Nothing
                _hookedListView = Nothing
                _originalMouseUp = Nothing
                _hookedMouseUp = Nothing
                If _installed Then HookActionButtons(queueForm)
            End If
            Return _queueForm IsNot Nothing
        End Function

        ''' <summary>
        ''' 确保当前队列窗体的右键菜单包含「预览输出」项，并保证右键时先同步再显示。
        ''' 队列窗体实例重建后会自动重挂（MouseUp 先执行同步，再调用 3fui 原生逻辑显示菜单）。
        ''' </summary>
        Private Shared Sub EnsurePreviewMenuItem()
            If Not SyncQueueForm() Then
                Return
            End If
            Dim cm = TryCast(_contextMenu, ModernContextMenu)
            If cm Is Nothing Then
                _contextMenu = TryCast(HostAccess.GetField(_queueForm, "_右键菜单", "右键菜单"), ModernContextMenu)
                cm = TryCast(_contextMenu, ModernContextMenu)
            End If
            If cm Is Nothing Then
                Return
            End If

            ' 已创建过则复用；否则查找同名项（避免重复添加），再创建
            Dim preview As ModernContextMenu.ModernMenuItem = TryCast(_previewMenuItem, ModernContextMenu.ModernMenuItem)
            If preview Is Nothing Then
                For Each it As ModernContextMenu.ModernMenuItem In cm.Items
                    If String.Equals(it.Text, "预览输出", StringComparison.Ordinal) Then
                        preview = it
                        Exit For
                    End If
                Next
            End If
            If preview Is Nothing Then
                preview = New ModernContextMenu.ModernMenuItem("预览输出")
                AddHandler preview.Click, AddressOf OnPreviewMenuClicked
                cm.Items.Add(preview)
            ElseIf Not cm.Items.Contains(preview) Then
                cm.Items.Add(preview)
            End If
            _previewMenuItem = preview

            ' 右键时先同步菜单再交给原生逻辑显示：把 MouseUp 换成我们的前置处理器
            If _listView IsNot Nothing AndAlso Not ReferenceEquals(_hookedListView, _listView) Then
                If _hookedListView IsNot Nothing AndAlso _hookedMouseUp IsNot Nothing Then
                    Try
                        RemoveHandler _hookedListView.MouseUp, _hookedMouseUp
                        RestoreControlEvent(_hookedListView, "MouseUp", _originalMouseUp)
                    Catch
                    End Try
                End If
                _originalMouseUp = RemoveControlEvent(_listView, "MouseUp")
                _hookedMouseUp = New MouseEventHandler(AddressOf OnListMouseUp)
                AddHandler _listView.MouseUp, _hookedMouseUp
                _hookedListView = _listView
            End If
        End Sub

        ''' <summary>列表右键：先确保「预览输出」存在，再执行 3fui 原逻辑（显示菜单）。</summary>
        Private Shared Sub OnListMouseUp(sender As Object, e As MouseEventArgs)
            Try
                EnsurePreviewMenuItem()
            Catch
            End Try
            InvokeOriginal(_originalMouseUp, sender, e)
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

        ''' <summary>右键菜单「预览输出」：把选中的第一个任务交给插件实时预览页。</summary>
        Private Shared Sub OnPreviewMenuClicked(sender As Object, e As EventArgs)
            Try
                Dim ids = GetSelectedTaskIds()
                If ids.Count = 0 Then
                    ShowTip("请先选中一个队列任务")
                    Return
                End If
                Dim panel = PluginPanel.Current
                If panel Is Nothing Then
                    ShowTip("视频超分插件面板尚未就绪")
                    Return
                End If
                panel.ShowPreviewForTask(ids(0))
            Catch ex As Exception
                ShowTip("预览输出失败：" & ex.Message)
            End Try
        End Sub

        ''' <summary>读取队列列表当前选中项的 Tag（任务 ID）。</summary>
        Private Shared Function GetSelectedTaskIds() As List(Of String)
            Dim result As New List(Of String)()
            Try
                Dim queueForm = _queueForm
                If queueForm Is Nothing Then
                    Return result
                End If
                Dim listView = HostAccess.GetField(queueForm, "_UltraDetailListView1", "UltraDetailListView1")
                If listView Is Nothing Then
                    Return result
                End If
                Dim selected = HostAccess.GetProperty(listView, "SelectedItems")
                Dim items = TryCast(selected, IEnumerable)
                If items Is Nothing Then
                    Return result
                End If
                For Each item In items
                    Dim id = TryCast(HostAccess.GetProperty(item, "Tag"), String)
                    If Not String.IsNullOrWhiteSpace(id) Then
                        result.Add(id)
                    End If
                Next
            Catch ex As Exception
                Trace.WriteLine("[VideoEnhancer][队列] 读取选中任务 ID 失败：" & ex.Message)
            End Try
            Return result
        End Function

        ''' <summary>暂停按钮：先写后端暂停字节，再执行 3fui 原逻辑。</summary>
        Private Shared Sub OnPauseClicked(sender As Object, e As EventArgs)
            Try
                PauseControl.WriteForSelectedTasks(1)
            Catch ex As Exception
                Trace.WriteLine("[VideoEnhancer][暂停] 写入暂停控制失败：" & ex.Message)
            End Try
            InvokeOriginal(_originalPauseClick, sender, e)
        End Sub

        ''' <summary>恢复按钮：先写后端恢复字节，再执行 3fui 原逻辑。</summary>
        Private Shared Sub OnResumeClicked(sender As Object, e As EventArgs)
            Try
                PauseControl.WriteForSelectedTasks(0)
            Catch ex As Exception
                Trace.WriteLine("[VideoEnhancer][恢复] 写入恢复控制失败：" & ex.Message)
            End Try
            InvokeOriginal(_originalResumeClick, sender, e)
        End Sub

        ''' <summary>停止按钮：插件任务优雅停止，普通任务继续使用宿主原生停止。</summary>
        Private Shared Sub OnStopClicked(sender As Object, e As EventArgs)
            Dim handled = False
            Try
                handled = StopControl.StopSelectedTasks()
            Catch ex As Exception
                Trace.WriteLine("[VideoEnhancer][停止] 插件停止处理失败：" & ex.Message)
            End Try
            If Not handled Then
                InvokeOriginal(_originalStopClick, sender, e)
            End If
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
                Trace.WriteLine("[VideoEnhancer][队列] 宿主原始事件处理器为空")
                Return
            End If
            Try
                original.DynamicInvoke(args)
            Catch ex As Exception
                Trace.WriteLine("[VideoEnhancer][队列] 调用宿主原始事件失败：" & ex.Message)
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
                        Dim task = HostQueueAccess.FindTask(id)
                        If task IsNot Nothing AndAlso task.IsPaused Then
                            Return True
                        End If
                    End If
                Next
            Catch ex As Exception
                Trace.WriteLine("[VideoEnhancer][暂停] 读取暂停状态失败：" & ex.Message)
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
            If cfg.SegmentedVideos Is Nothing Then cfg.SegmentedVideos = New List(Of SegmentedVideoConfig)()
            Dim segmentedByInput As New Dictionary(Of String, SegmentedVideoConfig)(StringComparer.OrdinalIgnoreCase)
            For Each input In entries
                Dim segmentConfig = FindSegmentedConfig(cfg, input)
                If segmentConfig Is Nothing OrElse Not segmentConfig.Enabled Then Continue For
                Dim validationError = SegmentEditingRules.ValidateSegmentedConfig(segmentConfig)
                If validationError.Length > 0 Then
                    ShowTip(Path.GetFileName(input) & " 的分段配置无效：" & validationError)
                    Return
                End If
                segmentedByInput(Path.GetFullPath(input)) = segmentConfig
            Next
            If segmentedByInput.Count > 0 AndAlso (cfg.InterpEnabled OrElse cfg.RtxHdrEnabled) Then
                ShowTip("分段超分当前不能与运动补帧或 RTX HDR 同时启用")
                Return
            End If
            If Not cfg.UpscaleEnabled AndAlso Not cfg.InterpEnabled AndAlso Not cfg.RtxHdrEnabled AndAlso segmentedByInput.Count = 0 Then
                ShowTip("请先打开超分、补帧或 HDR 映射开关")
                Return
            End If
            Dim needsRegularUpscale = cfg.UpscaleEnabled AndAlso entries.Any(
                Function(filePath) Not segmentedByInput.ContainsKey(IO.Path.GetFullPath(filePath)))
            If needsRegularUpscale AndAlso Not String.Equals(cfg.Backend, "rtxvsr", StringComparison.OrdinalIgnoreCase) AndAlso String.IsNullOrWhiteSpace(cfg.Model) Then
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

            Dim preset = HostPresetAccess.从面板创建预设(panel)
            Dim reserved As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Dim added As Integer = 0
            For Each input As String In entries
                Dim output = HostQueueAccess.ComputeOutputPath(input, preset, True, reserved)
                If String.IsNullOrWhiteSpace(output) Then
                    output = FallbackOutputPath(input, preset)
                End If
                reserved.Add(output)

                Dim settings = BuildFfmpegSettings(preset, input, output)
                Dim pauseShm = "ve_plugin_pause_" & Guid.NewGuid().ToString("N")
                Dim stopShm = "ve_plugin_stop_" & Guid.NewGuid().ToString("N")
                Dim segmentConfig As SegmentedVideoConfig = Nothing
                segmentedByInput.TryGetValue(Path.GetFullPath(input), segmentConfig)
                Dim segmentsBase64 = ""
                Dim effectiveBackend = cfg.Backend
                Dim effectiveModel = cfg.Model
                Dim effectiveUpscale = cfg.UpscaleEnabled
                Dim effectiveInterp = cfg.InterpEnabled
                Dim effectiveHdr = cfg.RtxHdrEnabled
                If segmentConfig IsNot Nothing Then
                    segmentsBase64 = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(segmentConfig.Segments))
                    Dim modelSegment = segmentConfig.Segments.FirstOrDefault(
                        Function(segment)
                            Dim backend = If(segment.Backend, "").Trim().ToLowerInvariant()
                            Return backend = "ncnn" OrElse backend = "cuda" OrElse
                                backend = "tensorrt" OrElse backend = "onnx"
                        End Function)
                    effectiveBackend = If(modelSegment Is Nothing, "ncnn", modelSegment.Backend)
                    effectiveModel = ""
                    effectiveUpscale = True
                    effectiveInterp = False
                    effectiveHdr = False
                End If
                Dim allowMixedSegmentBackends = segmentConfig IsNot Nothing AndAlso segmentConfig.AllowMixedModelBackends
                Dim args = BuildCliArgs(input, output, effectiveModel, settings, pauseShm, stopShm, effectiveUpscale, cfg.InterpModel, effectiveInterp, effectiveBackend, cfg.InterpFactor, cfg.ProcessOrder, cfg.InterpBackend, cfg.InterpDynamicScaledOpticalFlow, cfg.SceneDetectThreshold, cfg.UpscaleTileSize, cfg.UpscaleHalfPrecision, cfg.InterpHalfPrecision, effectiveHdr, cfg.RtxTarget, cfg.RtxQuality, segmentsBase64, cfg.RtxHdrContrast, cfg.RtxHdrSaturation, cfg.RtxHdrMiddleGray, cfg.RtxHdrMaxLuminance, allowMixedSegmentBackends, FfmpegToolResolver.Resolve("ffmpeg.exe"), FfmpegToolResolver.Resolve("ffprobe.exe"), cfg.OutputScale)
                AddQueueTask(args, Path.GetFileName(input), output, input)
                added += 1
            Next

            If added > 0 Then
                SwitchToQueueTab()
                ShowTip($"已添加 {added} 个视频超分任务到编码队列")
            End If
        End Sub

        Private Shared Function FindSegmentedConfig(cfg As PluginConfig, input As String) As SegmentedVideoConfig
            If cfg Is Nothing OrElse cfg.SegmentedVideos Is Nothing Then Return Nothing
            Dim fullPath As String
            Try
                fullPath = Path.GetFullPath(input)
            Catch
                Return Nothing
            End Try
            Return cfg.SegmentedVideos.FirstOrDefault(
                Function(item)
                    If item Is Nothing OrElse String.IsNullOrWhiteSpace(item.Path) Then Return False
                    Try
                        Return String.Equals(Path.GetFullPath(item.Path), fullPath, StringComparison.OrdinalIgnoreCase)
                    Catch
                        Return False
                    End Try
                End Function)
        End Function

        Private Shared Sub AddQueueTask(args As String, name As String, output As String, input As String)
            Try
                If HostAddMissionToQueueWithArgs IsNot Nothing Then
                    HostAddMissionToQueueWithArgs(args, name, output, input)
                Else
                    HostRuntime.InvokeShared("插件管理", "使用命令行添加任务到编码队列", args, name, output, input)
                End If
            Catch
            End Try
        End Sub

        Public Shared Function GetCurrentPrepareFilePaths() As List(Of String)
            Dim form = _prepareForm
            If form Is Nothing Then form = HostAccess.GetDefaultInstance("Form_v6_准备文件")
            If form Is Nothing Then Return New List(Of String)()
            Return GetFilePaths(form)
        End Function

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
        Public Shared Function BuildCliArgs(input As String, output As String, model As String, ffmpegSettings As String, Optional pauseShm As String = "", Optional stopShm As String = "", Optional upscaleOn As Boolean = True, Optional interpModel As String = "", Optional interpOn As Boolean = False, Optional backend As String = "ncnn", Optional interpFactor As Double = 2.0, Optional processOrder As String = "upscale-first", Optional interpBackend As String = "ncnn", Optional dynamicOpticalFlow As Boolean = False, Optional sceneThreshold As Double = 4.0, Optional tileSize As Integer = 0, Optional upscaleHalfPrecision As Boolean = True, Optional interpHalfPrecision As Boolean = True, Optional rtxHdr As Boolean = False, Optional rtxTarget As String = "2x", Optional rtxQuality As Integer = 3, Optional segmentsBase64 As String = "", Optional rtxHdrContrast As Integer = 100, Optional rtxHdrSaturation As Integer = 100, Optional rtxHdrMiddleGray As Integer = 44, Optional rtxHdrMaxLuminance As Integer = 1000, Optional allowMixedSegmentBackends As Boolean = False, Optional ffmpegPath As String = "", Optional ffprobePath As String = "", Optional outputScale As Integer = 0) As String
            Dim sb As New StringBuilder()
            Return QueueCommandBuilder.BuildCliArgs(New QueueJobOptions With {.input = input, .output = output, .model = model, .ffmpegSettings = ffmpegSettings, .pauseShm = pauseShm, .stopShm = stopShm, .upscaleOn = upscaleOn, .interpModel = interpModel, .interpOn = interpOn, .backend = backend, .interpFactor = interpFactor, .processOrder = processOrder, .interpBackend = interpBackend, .dynamicOpticalFlow = dynamicOpticalFlow, .sceneThreshold = sceneThreshold, .tileSize = tileSize, .upscaleHalfPrecision = upscaleHalfPrecision, .interpHalfPrecision = interpHalfPrecision, .rtxHdr = rtxHdr, .rtxTarget = rtxTarget, .rtxQuality = rtxQuality, .segmentsBase64 = segmentsBase64, .rtxHdrContrast = rtxHdrContrast, .rtxHdrSaturation = rtxHdrSaturation, .rtxHdrMiddleGray = rtxHdrMiddleGray, .rtxHdrMaxLuminance = rtxHdrMaxLuminance, .allowMixedSegmentBackends = allowMixedSegmentBackends, .ffmpegPath = ffmpegPath, .ffprobePath = ffprobePath, .outputScale = outputScale})
        End Function

        Public Shared Function BuildFfmpegSettings(preset As HostPreset, input As String, output As String) As String
            Return QueueCommandBuilder.BuildFfmpegSettings(preset, input, output)
        End Function

        Private Shared Function FallbackOutputPath(input As String, preset As HostPreset) As String
            Return QueueCommandBuilder.FallbackOutputPath(input, preset)
        End Function

        Public Structure Token
            Public Text As String
            Public WasQuoted As Boolean
        End Structure

        ''' <summary>Windows 风格按空白拆分，双引号包裹的空格保留在令牌内。</summary>
        Public Shared Function Tokenize(line As String) As List(Of Token)
            Return CommandLineTokenizer.Tokenize(line)
        End Function


    End Class

End Namespace


