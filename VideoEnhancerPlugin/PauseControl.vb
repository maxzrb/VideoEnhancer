Imports System
Imports System.Collections.Generic
Imports System.IO.MemoryMappedFiles
Imports System.Linq
Imports System.Text.Json
Imports FFmpegFreeUI

Namespace videoenhancer

    ''' <summary>
    ''' 暂停/恢复控制：向 rve-backend 的暂停共享内存写入 1/0 字节。
    ''' 3fui 的"暂停"按钮会直接挂起 videoenhancer.exe 进程，但真正的编码在
    ''' python(rve-backend) 进程里，所以必须通过共享内存让后端自行暂停。
    ''' 后端启动后才会创建共享内存，写失败时进入重试队列，由定时器补写。
    ''' </summary>
    Friend Class PauseControl

        Private Class PendingEntry
            Public Value As Byte = 0
            Public Since As DateTime = DateTime.UtcNow
        End Class

        Private Shared ReadOnly _pending As New Dictionary(Of String, PendingEntry)(StringComparer.Ordinal)
        Private Shared ReadOnly _lock As New Object()
        Private Shared _timer As Threading.Timer = Nothing

        ''' <summary>处理队列插件事件（task.paused / task.resumed / 任务结束清理）。</summary>
        Public Shared Sub HandleQueueEvent(eventName As String, json As String)
            If String.IsNullOrWhiteSpace(eventName) OrElse String.IsNullOrWhiteSpace(json) Then
                Return
            End If
            Try
                Using doc = JsonDocument.Parse(json)
                    Dim root = doc.RootElement
                    Dim id As String = Nothing
                    Dim taskEl As JsonElement = Nothing
                    If root.TryGetProperty("task", taskEl) Then
                        Dim idEl As JsonElement = Nothing
                        If taskEl.TryGetProperty("id", idEl) Then
                            id = idEl.GetString()
                        End If
                    End If
                    If String.IsNullOrEmpty(id) Then
                        Return
                    End If

                    Select Case eventName
                        Case "task.paused"
                            QueuePending(id, 1)
                        Case "task.resumed"
                            QueuePending(id, 0)
                        Case "task.stopped", "task.completed", "task.failed"
                            ClearPending(id)
                    End Select
                End Using
            Catch
            End Try
        End Sub

        ''' <summary>按钮路径：点击"暂停/恢复"时先写共享内存，再交给 3fui 原逻辑。</summary>
        Public Shared Sub WriteForSelectedTasks(value As Byte)
            Try
                Dim queueForm = HostAccess.GetDefaultInstance("Form_v6_编码队列")
                If queueForm Is Nothing Then
                    Return
                End If
                Dim listView = HostAccess.GetField(queueForm, "_UltraDetailListView1", "UltraDetailListView1")
                If listView Is Nothing Then
                    Return
                End If
                Dim selected = HostAccess.GetProperty(listView, "SelectedItems")
                Dim items = TryCast(selected, System.Collections.IEnumerable)
                If items Is Nothing Then
                    Return
                End If
                For Each item In items
                    Dim id = TryCast(HostAccess.GetProperty(item, "Tag"), String)
                    If Not String.IsNullOrWhiteSpace(id) Then
                        Dim task = FindTask(id)
                        If task IsNot Nothing Then
                            TryWriteForTask(task, value)
                        End If
                    End If
                Next
            Catch
            End Try
        End Sub

        ''' <summary>写入暂停字节；共享内存名兼容带/不带斜杠前缀。返回是否成功。</summary>
        Public Shared Function TryWriteByte(shmBase As String, value As Byte) As Boolean
            If String.IsNullOrWhiteSpace(shmBase) Then
                Return False
            End If
            Dim candidates As String() = {"/" & shmBase, shmBase}
            For Each name As String In candidates
                Try
                    Using mmf = MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.ReadWrite)
                        Using accessor = mmf.CreateViewAccessor(0, 1)
                            accessor.Write(0, value)
                            Return True
                        End Using
                    End Using
                Catch
                End Try
            Next
            Return False
        End Function

        Private Shared Sub QueuePending(id As String, value As Byte)
            Dim task = FindTask(id)
            If task Is Nothing Then
                Return
            End If
            If TryWriteForTask(task, value) Then
                Return
            End If
            SyncLock _lock
                _pending(id) = New PendingEntry With {.Value = value, .Since = DateTime.UtcNow}
            End SyncLock
            EnsureTimer()
        End Sub

        Private Shared Sub ClearPending(id As String)
            SyncLock _lock
                _pending.Remove(id)
            End SyncLock
        End Sub

        Private Shared Function TryWriteForTask(task As 编码任务_v6, value As Byte) As Boolean
            Dim shm = ExtractShmName(task)
            If String.IsNullOrEmpty(shm) Then
                Return False
            End If
            Return TryWriteByte(shm, value)
        End Function

        ''' <summary>从任务命令行里提取 -pause-shm 后面的共享内存名。</summary>
        Private Shared Function ExtractShmName(task As 编码任务_v6) As String
            Try
                Dim cmd = task.命令行
                If String.IsNullOrWhiteSpace(cmd) Then
                    Return ""
                End If
                Dim tokens = QueueHook.Tokenize(cmd)
                For i As Integer = 0 To tokens.Count - 2
                    If String.Equals(tokens(i).Text, "-pause-shm", StringComparison.OrdinalIgnoreCase) Then
                        Return tokens(i + 1).Text.Trim(""""c)
                    End If
                Next
            Catch
            End Try
            Return ""
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

        Private Shared Sub EnsureTimer()
            SyncLock _lock
                If _timer Is Nothing Then
                    _timer = New Threading.Timer(AddressOf OnRetryTick, Nothing, 500, 500)
                End If
            End SyncLock
        End Sub

        Private Shared Sub OnRetryTick(state As Object)
            Dim removeIds As New List(Of String)
            Dim snapshot As New List(Of KeyValuePair(Of String, PendingEntry))
            SyncLock _lock
                For Each kv In _pending
                    snapshot.Add(kv)
                Next
            End SyncLock

            For Each kv In snapshot
                Dim task = FindTask(kv.Key)
                If task Is Nothing Then
                    removeIds.Add(kv.Key)
                ElseIf (DateTime.UtcNow - kv.Value.Since).TotalSeconds > 120 Then
                    removeIds.Add(kv.Key)
                ElseIf TryWriteForTask(task, kv.Value.Value) Then
                    removeIds.Add(kv.Key)
                End If
            Next

            If removeIds.Count > 0 Then
                SyncLock _lock
                    For Each id In removeIds
                        _pending.Remove(id)
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

