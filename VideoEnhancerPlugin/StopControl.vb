Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Runtime.InteropServices
Imports FFmpegFreeUI

Namespace videoenhancer

    ''' <summary>
    ''' 停止控制：把 videoenhancer.exe 的 -stop-shm 共享内存字节写为 1，
    ''' 触发 CLI 优雅停止（终止 python 后端，让 ffmpeg 写进程完成封装，已处理部分写入磁盘）。
    ''' 停止共享内存由 CLI 启动时创建并持有，插件只需打开写入；写入失败（CLI 尚未就绪）时进入重试队列。
    ''' 同时把任务标记为手动停止，3fui 调度器在 CLI 退出后按“手动停止”收尾，保留输出文件。
    ''' </summary>
    Friend Class StopControl

        Private Shared ReadOnly _lock As New Object()
        Private Shared ReadOnly _pending As New Dictionary(Of String, DateTime)(StringComparer.Ordinal)
        Private Shared _timer As Threading.Timer = Nothing

        Private Const PROCESS_SUSPEND_RESUME As UInteger = &H800

        <DllImport("ntdll.dll")>
        Private Shared Function NtResumeProcess(processHandle As IntPtr) As Integer
        End Function

        <DllImport("kernel32.dll", SetLastError:=True)>
        Private Shared Function OpenProcess(dwDesiredAccess As UInteger, bInheritHandle As Boolean, dwProcessId As Integer) As IntPtr
        End Function

        <DllImport("kernel32.dll", SetLastError:=True)>
        Private Shared Function CloseHandle(hObject As IntPtr) As Boolean
        End Function

        ''' <summary>点击“停止”：对选中任务写入停止字节并标记为手动停止。</summary>
        Public Shared Sub StopSelectedTasks()
            Dim tasks = GetSelectedTasks()
            For Each task In tasks
                StopTask(task)
            Next
        End Sub

        Private Shared Sub StopTask(task As 编码任务_v6)
            ' 若任务处于暂停（CLI 进程被 3fui 挂起），先解除挂起，让 CLI 能读到停止字节
            If task.状态 = 编码任务状态_v6.已暂停 Then
                ResumeCliProcess(task)
            End If

            Dim shm = ExtractStopShm(task)
            If Not String.IsNullOrEmpty(shm) Then
                QueueWrite(shm)
            End If

            ' 先于 CLI 退出标记手动停止：CLI 优雅退出（退出码 130）后，
            ' 3fui 调度器按“手动停止”分支收尾，保留已处理部分
            task.手动停止 = True
            task.状态 = 编码任务状态_v6.已停止
        End Sub

        ''' <summary>解除 3fui 对 CLI 进程的挂起（暂停功能挂的是中转进程，停止前必须恢复）。</summary>
        Private Shared Sub ResumeCliProcess(task As 编码任务_v6)
            Try
                Dim pid = task.当前进程ID
                If pid = 0 Then
                    Return
                End If
                Dim handle = OpenProcess(PROCESS_SUSPEND_RESUME, False, pid)
                If handle <> IntPtr.Zero Then
                    Try
                        NtResumeProcess(handle)
                    Finally
                        CloseHandle(handle)
                    End Try
                End If
            Catch
            End Try
        End Sub

        ''' <summary>先尝试写 1；失败（CLI 尚未创建共享内存）则进入重试队列。</summary>
        Private Shared Sub QueueWrite(shm As String)
            If PauseControl.TryWriteByte(shm, 1) Then
                Return
            End If
            SyncLock _lock
                If Not _pending.ContainsKey(shm) Then
                    _pending(shm) = DateTime.UtcNow
                End If
            End SyncLock
            EnsureTimer()
        End Sub

        Private Shared Function GetSelectedTasks() As List(Of 编码任务_v6)
            Dim result As New List(Of 编码任务_v6)
            Try
                Dim queueForm = HostAccess.GetDefaultInstance("Form_v6_编码队列")
                If queueForm Is Nothing Then
                    Return result
                End If
                Dim listView = HostAccess.GetField(queueForm, "_UltraDetailListView1", "UltraDetailListView1")
                If listView Is Nothing Then
                    Return result
                End If
                Dim selected = HostAccess.GetProperty(listView, "SelectedItems")
                Dim items = TryCast(selected, System.Collections.IEnumerable)
                If items Is Nothing Then
                    Return result
                End If
                For Each item In items
                    Dim id = TryCast(HostAccess.GetProperty(item, "Tag"), String)
                    If Not String.IsNullOrWhiteSpace(id) Then
                        Dim task = FindTask(id)
                        If task IsNot Nothing Then
                            result.Add(task)
                        End If
                    End If
                Next
            Catch
            End Try
            Return result
        End Function

        Private Shared Function FindTask(id As String) As 编码任务_v6
            Try
                Dim queue = 编码队列_v6.队列
                SyncLock queue
                    Return queue.FirstOrDefault(Function(t) String.Equals(t.ID, id, StringComparison.Ordinal))
                End SyncLock
            Catch
                Return Nothing
            End Try
        End Function

        ''' <summary>从任务命令行提取 -stop-shm 后面的共享内存名。</summary>
        Private Shared Function ExtractStopShm(task As 编码任务_v6) As String
            Try
                Dim cmd = task.命令行
                If String.IsNullOrWhiteSpace(cmd) Then
                    Return ""
                End If
                Dim tokens = QueueHook.Tokenize(cmd)
                For i As Integer = 0 To tokens.Count - 2
                    If String.Equals(tokens(i).Text, "-stop-shm", StringComparison.OrdinalIgnoreCase) Then
                        Return tokens(i + 1).Text.Trim(""""c)
                    End If
                Next
            Catch
            End Try
            Return ""
        End Function

        Private Shared Sub EnsureTimer()
            SyncLock _lock
                If _timer Is Nothing Then
                    _timer = New Threading.Timer(AddressOf OnRetryTick, Nothing, 500, 500)
                End If
            End SyncLock
        End Sub

        Private Shared Sub OnRetryTick(state As Object)
            Dim removeNames As New List(Of String)
            Dim snapshot As New List(Of KeyValuePair(Of String, DateTime))
            SyncLock _lock
                For Each kv In _pending
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
                        _pending.Remove(name)
                    Next
                    If _pending.Count = 0 AndAlso _timer IsNot Nothing Then
                        Try
                            _timer.Dispose()
                        Catch
                        End Try
                        _timer = Nothing
                    End If
                End SyncLock
            End If
        End Sub

    End Class

End Namespace
