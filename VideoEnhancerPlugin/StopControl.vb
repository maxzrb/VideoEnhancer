Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.InteropServices

Namespace videoenhancer

    ''' <summary>
    ''' 停止控制：插件任务先走共享内存优雅停止，超过期限后再调用宿主停止 API。
    ''' 普通 FFmpeg 任务不拦截，直接交给宿主原生停止逻辑。
    ''' </summary>
    Friend Class StopControl

        Private NotInheritable Class PendingStop
            Public Property Id As String = ""
            Public Property RequestedAt As DateTime = DateTime.UtcNow
            Public Property LastForceAt As DateTime = DateTime.MinValue
        End Class

        Private Shared ReadOnly _lock As New Object()
        Private Shared ReadOnly _pendingShm As New Dictionary(Of String, DateTime)(StringComparer.Ordinal)
        Private Shared ReadOnly _pendingStops As New Dictionary(Of String, PendingStop)(StringComparer.Ordinal)
        Private Shared ReadOnly _preservedOutputs As New Dictionary(Of String, PreservedOutput)(StringComparer.Ordinal)
        Private Shared _timer As Threading.Timer = Nothing
        Private Shared _stopTimer As Threading.Timer = Nothing

        Private Const PROCESS_SUSPEND_RESUME As UInteger = &H800
        Private Const GracefulStopTimeoutSeconds As Double = 12.0
        Private Const ForceRetrySeconds As Double = 1.0
        Private Const OutputMarkerSuffix As String = ".videoenhancer-stop-ok"
        Private Const OutputRestoreDelaySeconds As Double = 2.0

        Private NotInheritable Class PreservedOutput
            Public Property Id As String = ""
            Public Property OriginalPath As String = ""
            Public Property TemporaryPath As String = ""
            Public Property MarkerPath As String = ""
            Public Property NotRunningSince As DateTime = DateTime.MinValue
        End Class

        <DllImport("ntdll.dll")>
        Private Shared Function NtResumeProcess(processHandle As IntPtr) As Integer
        End Function

        <DllImport("kernel32.dll", SetLastError:=True)>
        Private Shared Function OpenProcess(dwDesiredAccess As UInteger, bInheritHandle As Boolean, dwProcessId As Integer) As IntPtr
        End Function

        <DllImport("kernel32.dll", SetLastError:=True)>
        Private Shared Function CloseHandle(hObject As IntPtr) As Boolean
        End Function

        ''' <summary>
        ''' 处理当前选中任务。返回 True 表示插件已处理至少一个任务；返回 False 时由
        ''' QueueHook 回退调用宿主原始 Click 委托，保证旧宿主普通任务仍可停止。
        ''' </summary>
        Public Shared Function StopSelectedTasks() As Boolean
            Dim tasks = GetSelectedTasks()
            If tasks.Count = 0 Then Return False

            Dim nativeIds As New List(Of String)()
            Dim handled As Boolean = False
            Dim hasPluginTask As Boolean = False
            For Each task In tasks
                If IsPluginTask(task) AndAlso (task.正在执行 OrElse task.IsPaused) Then
                    hasPluginTask = True
                    RequestGracefulStop(task)
                    handled = True
                ElseIf Not String.IsNullOrWhiteSpace(task.ID) Then
                    nativeIds.Add(task.ID)
                End If
            Next

            If nativeIds.Count > 0 Then
                If HostQueueAccess.InvokeTaskCommand("停止任务", nativeIds) Then
                    handled = True
                ElseIf Not hasPluginTask Then
                    ' 让 QueueHook 调用捕获的原始委托；不能在混合选择中误杀插件任务。
                    Return False
                Else
                    Trace.WriteLine("[VideoEnhancer][停止] 宿主停止 API 不可用，普通任务将由原生回退处理")
                End If
            End If
            Return handled
        End Function

        Private Shared Function IsPluginTask(task As HostTask) As Boolean
            If task Is Nothing Then Return False
            Try
                Dim command = task.命令行
                Return Not String.IsNullOrWhiteSpace(command) AndAlso
                    command.IndexOf("-stop-shm", StringComparison.OrdinalIgnoreCase) >= 0
            Catch
                Return False
            End Try
        End Function

        ''' <summary>先恢复被宿主挂起的 CLI，写停止共享内存，并设置手动停止标记。</summary>
        Private Shared Sub RequestGracefulStop(task As HostTask)
            If task Is Nothing Then Return
            If task.IsPaused Then
                ResumeCliProcess(task)
            End If

            PreserveOutputPath(task)

            Dim shm = ExtractStopShm(task)
            If Not String.IsNullOrWhiteSpace(shm) Then
                QueueWrite(shm)
            Else
                Trace.WriteLine("[VideoEnhancer][停止] 任务缺少 -stop-shm：" & task.ID)
            End If

            ' 不提前写入“已停止”状态；让宿主观察 CLI 退出后按手动停止分支收尾，
            ' 否则宿主可能跳过正在执行阶段，导致输出封装和资源释放被截断。
            task.手动停止 = True
            SyncLock _lock
                If task.正在执行 Then
                    _pendingStops(task.ID) = New PendingStop With {
                        .Id = task.ID,
                        .RequestedAt = DateTime.UtcNow
                    }
                End If
            End SyncLock
            EnsureStopTimer()
        End Sub

        ''' <summary>
        ''' 3FUI 手动停止会按全局设置清理 MP4。把任务显示路径临时改成非 MP4，
        ''' 等宿主完成收尾后再恢复；CLI 用标记文件区分可保留的完整尾部封装和残缺输出。
        ''' </summary>
        Private Shared Sub PreserveOutputPath(task As HostTask)
            Try
                If task Is Nothing OrElse String.IsNullOrWhiteSpace(task.ID) OrElse
                   String.IsNullOrWhiteSpace(task.输出文件) Then Return
                Dim original = task.输出文件
                SyncLock _lock
                    If _preservedOutputs.ContainsKey(task.ID) Then Return
                End SyncLock

                Dim marker = original & OutputMarkerSuffix
                Try
                    If File.Exists(marker) Then File.Delete(marker)
                Catch ex As Exception
                    Trace.WriteLine("[VideoEnhancer][停止] 清理旧输出标记失败：" & ex.Message)
                End Try

                Dim temporary = original & ".videoenhancer-stopping-" & task.ID
                task.输出文件 = temporary
                SyncLock _lock
                    _preservedOutputs(task.ID) = New PreservedOutput With {
                        .Id = task.ID,
                        .OriginalPath = original,
                        .TemporaryPath = temporary,
                        .MarkerPath = marker
                    }
                End SyncLock
            Catch ex As Exception
                Trace.WriteLine("[VideoEnhancer][停止] 保护部分输出路径失败：" & ex.Message)
            End Try
        End Sub

        ''' <summary>解除 3FUI 对 CLI 进程的挂起，确保 CLI 能读到停止字节。</summary>
        Private Shared Sub ResumeCliProcess(task As HostTask)
            Try
                Dim pid = task.当前进程ID
                If pid = 0 Then Return
                Dim handle = OpenProcess(PROCESS_SUSPEND_RESUME, False, pid)
                If handle <> IntPtr.Zero Then
                    Try
                        NtResumeProcess(handle)
                    Finally
                        CloseHandle(handle)
                    End Try
                End If
            Catch ex As Exception
                Trace.WriteLine("[VideoEnhancer][停止] 恢复暂停 CLI 失败：" & ex.Message)
            End Try
        End Sub

        ''' <summary>先尝试写 1；失败（CLI 尚未创建共享内存）则进入重试队列。</summary>
        Private Shared Sub QueueWrite(shm As String)
            If PauseControl.TryWriteByte(shm, 1) Then Return
            SyncLock _lock
                If Not _pendingShm.ContainsKey(shm) Then _pendingShm(shm) = DateTime.UtcNow
            End SyncLock
            EnsureTimer()
        End Sub

        Private Shared Function GetSelectedTasks() As List(Of HostTask)
            Dim result As New List(Of HostTask)()
            Try
                Dim queueForm = HostAccess.GetDefaultInstance("Form_v6_编码队列")
                If queueForm Is Nothing Then Return result
                Dim listView = HostAccess.GetField(queueForm, "_UltraDetailListView1", "UltraDetailListView1")
                If listView Is Nothing Then Return result
                Dim selected = HostAccess.GetProperty(listView, "SelectedItems")
                Dim items = TryCast(selected, System.Collections.IEnumerable)
                If items Is Nothing Then Return result
                For Each item In items
                    Dim id = TryCast(HostAccess.GetProperty(item, "Tag"), String)
                    If String.IsNullOrWhiteSpace(id) Then Continue For
                    Dim task = HostQueueAccess.FindTask(id)
                    If task IsNot Nothing Then result.Add(task)
                Next
            Catch ex As Exception
                Trace.WriteLine("[VideoEnhancer][停止] 读取选中任务失败：" & ex.Message)
            End Try
            Return result
        End Function

        ''' <summary>从任务命令行提取 -stop-shm 后面的共享内存名。</summary>
        Private Shared Function ExtractStopShm(task As HostTask) As String
            Try
                Dim cmd = task.命令行
                If String.IsNullOrWhiteSpace(cmd) Then Return ""
                Dim tokens = QueueHook.Tokenize(cmd)
                For i As Integer = 0 To tokens.Count - 2
                    If String.Equals(tokens(i).Text, "-stop-shm", StringComparison.OrdinalIgnoreCase) Then
                        Return tokens(i + 1).Text.Trim(""""c)
                    End If
                Next
            Catch ex As Exception
                Trace.WriteLine("[VideoEnhancer][停止] 解析停止共享内存失败：" & ex.Message)
            End Try
            Return ""
        End Function

        Private Shared Sub EnsureTimer()
            SyncLock _lock
                If _timer Is Nothing Then _timer = New Threading.Timer(AddressOf OnRetryTick, Nothing, 500, 500)
            End SyncLock
        End Sub

        Private Shared Sub EnsureStopTimer()
            SyncLock _lock
                If _stopTimer Is Nothing Then _stopTimer = New Threading.Timer(AddressOf OnStopTick, Nothing, 250, 250)
            End SyncLock
        End Sub

        ''' <summary>共享内存尚未创建时最多重试 30 秒。</summary>
        Private Shared Sub OnRetryTick(state As Object)
            Dim removeNames As New List(Of String)()
            Dim snapshot As New List(Of KeyValuePair(Of String, DateTime))()
            SyncLock _lock
                For Each kv In _pendingShm
                    snapshot.Add(kv)
                Next
            End SyncLock

            For Each kv In snapshot
                If (DateTime.UtcNow - kv.Value).TotalSeconds > 30 Then
                    removeNames.Add(kv.Key)
                ElseIf PauseControl.TryWriteByte(kv.Key, 1) Then
                    removeNames.Add(kv.Key)
                End If
            Next

            If removeNames.Count > 0 Then
                SyncLock _lock
                    For Each name In removeNames
                        _pendingShm.Remove(name)
                    Next
                    If _pendingShm.Count = 0 AndAlso _timer IsNot Nothing Then
                        Try
                            _timer.Dispose()
                        Catch
                        End Try
                        _timer = Nothing
                    End If
                End SyncLock
            End If
        End Sub

        ''' <summary>
        ''' 优雅停止最多等待 12 秒。CLI 已退出时让宿主自然收尾；仍运行时调用宿主
        ''' 停止 API（必要时直接调用任务实例停止）强制终止整个进程树。
        ''' </summary>
        Private Shared Sub OnStopTick(state As Object)
            Dim snapshot As New List(Of PendingStop)()
            SyncLock _lock
                For Each pending In _pendingStops.Values
                    snapshot.Add(pending)
                Next
            End SyncLock

            Dim removeIds As New List(Of String)()
            For Each pending In snapshot
                Dim task = HostQueueAccess.FindTask(pending.Id)
                If task Is Nothing OrElse Not task.正在执行 OrElse task.当前进程ID = 0 Then
                    removeIds.Add(pending.Id)
                    Continue For
                End If

                Dim elapsed = (DateTime.UtcNow - pending.RequestedAt).TotalSeconds
                If elapsed < GracefulStopTimeoutSeconds Then Continue For
                If (DateTime.UtcNow - pending.LastForceAt).TotalSeconds < ForceRetrySeconds Then Continue For

                Dim forced = HostQueueAccess.InvokeTaskCommand("停止任务", New String() {pending.Id})
                If Not forced Then forced = HostQueueAccess.InvokeTaskInstance(task, "停止")
                pending.LastForceAt = DateTime.UtcNow
                If Not forced Then
                    Trace.WriteLine("[VideoEnhancer][停止] 无法调用宿主强制停止 API：" & pending.Id)
                End If
            Next

            If removeIds.Count > 0 Then
                SyncLock _lock
                    For Each id In removeIds
                        _pendingStops.Remove(id)
                    Next
                End SyncLock
            End If

            RestorePreservedOutputs()
            TryDisposeStopTimerIfIdle()
        End Sub

        ''' <summary>宿主完成停止清理后恢复输出路径，并按标记保留或清理残缺 MP4。</summary>
        Private Shared Sub RestorePreservedOutputs()
            Dim snapshot As New List(Of PreservedOutput)()
            SyncLock _lock
                For Each item In _preservedOutputs.Values
                    snapshot.Add(item)
                Next
            End SyncLock

            Dim removeIds As New List(Of String)()
            For Each item In snapshot
                Dim task = HostQueueAccess.FindTask(item.Id)
                If task Is Nothing Then
                    Dim removedTaskCanKeep = File.Exists(item.MarkerPath) AndAlso File.Exists(item.OriginalPath)
                    If removedTaskCanKeep Then
                        Try
                            removedTaskCanKeep = New FileInfo(item.OriginalPath).Length > 0
                        Catch
                            removedTaskCanKeep = False
                        End Try
                    End If
                    DeleteStopMarker(item.MarkerPath)
                    If Not removedTaskCanKeep Then CleanupInvalidStoppedOutput(item.OriginalPath)
                    removeIds.Add(item.Id)
                    Continue For
                End If
                If task.正在执行 Then Continue For
                If item.NotRunningSince = DateTime.MinValue Then
                    item.NotRunningSince = DateTime.UtcNow
                    Continue For
                End If
                If (DateTime.UtcNow - item.NotRunningSince).TotalSeconds < OutputRestoreDelaySeconds Then Continue For

                Dim canKeep = File.Exists(item.MarkerPath) AndAlso File.Exists(item.OriginalPath)
                If canKeep Then
                    Try
                        canKeep = New FileInfo(item.OriginalPath).Length > 0
                    Catch
                        canKeep = False
                    End Try
                End If
                DeleteStopMarker(item.MarkerPath)
                If Not canKeep Then CleanupInvalidStoppedOutput(item.OriginalPath)

                Try
                    If String.Equals(task.输出文件, item.TemporaryPath, StringComparison.OrdinalIgnoreCase) Then
                        task.输出文件 = item.OriginalPath
                    End If
                Catch ex As Exception
                    Trace.WriteLine("[VideoEnhancer][停止] 恢复输出路径失败：" & ex.Message)
                End Try
                removeIds.Add(item.Id)
            Next

            If removeIds.Count > 0 Then
                SyncLock _lock
                    For Each id In removeIds
                        _preservedOutputs.Remove(id)
                    Next
                End SyncLock
            End If
        End Sub

        Private Shared Sub DeleteStopMarker(path As String)
            If String.IsNullOrWhiteSpace(path) Then Return
            Try
                If File.Exists(path) Then File.Delete(path)
            Catch ex As Exception
                Trace.WriteLine("[VideoEnhancer][停止] 清理输出标记失败：" & ex.Message)
            End Try
        End Sub

        ''' <summary>残缺 MP4 沿用 3FUI 的删除设置；无法读取设置时不主动删除用户文件。</summary>
        Private Shared Sub CleanupInvalidStoppedOutput(path As String)
            If String.IsNullOrWhiteSpace(path) OrElse Not File.Exists(path) OrElse
               Not System.IO.Path.GetExtension(path).Equals(".mp4", StringComparison.OrdinalIgnoreCase) Then Return
            Try
                Dim mode = ReadHostOutputCleanupMode()
                Select Case mode
                    Case 0
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin)
                    Case 1
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.DeletePermanently)
                End Select
            Catch ex As Exception
                Trace.WriteLine("[VideoEnhancer][停止] 清理残缺 MP4 输出失败：" & ex.Message)
            End Try
        End Sub

        Private Shared Function ReadHostOutputCleanupMode() As Integer
            Try
                Dim instance = HostSettings.Instance
                If instance Is Nothing Then Return 2
                Dim modeProperty = instance.GetType().GetProperty("任务失败自动删除输出文件",
                    BindingFlags.Public Or BindingFlags.NonPublic Or BindingFlags.Instance)
                If modeProperty Is Nothing Then Return 2
                Return Convert.ToInt32(modeProperty.GetValue(instance), Globalization.CultureInfo.InvariantCulture)
            Catch ex As Exception
                Trace.WriteLine("[VideoEnhancer][停止] 读取宿主输出清理设置失败：" & ex.Message)
                Return 2
            End Try
        End Function

        Private Shared Sub TryDisposeStopTimerIfIdle()
            SyncLock _lock
                If _pendingStops.Count > 0 OrElse _preservedOutputs.Count > 0 OrElse _stopTimer Is Nothing Then Return
                Try
                    _stopTimer.Dispose()
                Catch
                End Try
                _stopTimer = Nothing
            End SyncLock
        End Sub

    End Class

End Namespace
