Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text.Json
Imports System.Text.RegularExpressions
Imports FFmpegFreeUI

Namespace videoenhancer

    ''' <summary>
    ''' 订阅 3fui 编码队列事件（task.log / task.progress），把 rve-backend 的
    ''' "FPS: … Current Frame: … ETA: …"与"Total Output Frames: …"进度行
    ''' 换算成 3fui 任务进度字段（百分比/效率/剩余时间/输出大小），
    ''' 队列日志系统会自动触发界面刷新。同时把暂停/恢复事件转发给 PauseControl。
    ''' </summary>
    Friend Class BackendProgress

        Private Shared ReadOnly TotalFramesRegex As New Regex(
            "Total Output Frames:\s*(\d+)", RegexOptions.Compiled Or RegexOptions.IgnoreCase)

        Private Shared ReadOnly ProgressRegex As New Regex(
            "FPS:\s*([\d.]+)\s*Current Frame:\s*(\d+)\s*ETA:\s*([\d:]+)", RegexOptions.Compiled Or RegexOptions.IgnoreCase)

        Private Shared ReadOnly TotalFrames As New Dictionary(Of String, Long)(StringComparer.Ordinal)
        Private Shared ReadOnly LastSizeUpdate As New Dictionary(Of String, DateTime)(StringComparer.Ordinal)
        Private Shared ReadOnly TelemetryLock As New Object()
        Private Shared ReadOnly Telemetry As New Dictionary(Of String, PreviewTelemetry)(StringComparer.Ordinal)

        ''' <summary>预览引擎遥测：每次后端进度行更新时记录当前 FPS / 已处理帧 / 总输出帧。</summary>
        Public Class PreviewTelemetry
            Public Fps As Double = 0
            Public Frame As Long = 0
            Public TotalFrames As Long = 0
        End Class

        Public Shared Function GetTelemetry(taskId As String) As PreviewTelemetry
            SyncLock TelemetryLock
                Dim value As PreviewTelemetry = Nothing
                If Telemetry.TryGetValue(taskId, value) Then
                    Return value
                End If
            End SyncLock
            Return Nothing
        End Function

        Private Shared Sub ClearTelemetry(taskId As String)
            SyncLock TelemetryLock
                Telemetry.Remove(taskId)
            End SyncLock
        End Sub

        Public Shared Sub Attach(subscribe As Action(Of String, Object))
            If subscribe Is Nothing Then
                Return
            End If
            Try
                Dim handler As Action(Of String, String) = AddressOf OnQueueEvent
                subscribe("", handler) ' 空过滤器 = 接收全部事件
            Catch
            End Try
        End Sub

        Private Shared Sub OnQueueEvent(eventName As String, json As String)
            If String.IsNullOrWhiteSpace(json) Then
                Return
            End If

            ' 暂停/恢复/结束：交给共享内存控制器（写入暂停字节、重试、清理）
            If eventName = "task.paused" OrElse eventName = "task.resumed" OrElse
               eventName = "task.stopped" OrElse eventName = "task.completed" OrElse eventName = "task.failed" Then
                PauseControl.HandleQueueEvent(eventName, json)
                If eventName = "task.stopped" OrElse eventName = "task.completed" OrElse eventName = "task.failed" Then
                    Dim endedId = TryGetTaskId(json)
                    If Not String.IsNullOrEmpty(endedId) Then
                        ClearTelemetry(endedId)
                    End If
                End If
                If Not (eventName = "task.log" OrElse eventName = "task.progress") Then
                    Return
                End If
            End If

            If Not (eventName = "task.log" OrElse eventName = "task.progress") Then
                Return
            End If

            Try
                Using doc = JsonDocument.Parse(json)
                    Dim root = doc.RootElement
                    Dim taskEl As JsonElement = Nothing
                    If Not root.TryGetProperty("task", taskEl) Then
                        Return
                    End If
                    Dim idEl As JsonElement = Nothing
                    If Not taskEl.TryGetProperty("id", idEl) Then
                        Return
                    End If
                    Dim id = idEl.GetString()
                    If String.IsNullOrEmpty(id) Then
                        Return
                    End If

                    Dim text As String = Nothing
                    Dim logEl As JsonElement = Nothing
                    If root.TryGetProperty("log", logEl) Then
                        Dim textEl As JsonElement = Nothing
                        If logEl.TryGetProperty("text", textEl) Then
                            text = textEl.GetString()
                        End If
                    End If
                    If String.IsNullOrEmpty(text) Then
                        Return
                    End If

                    Dim totalMatch = TotalFramesRegex.Match(text)
                    If totalMatch.Success Then
                        Dim total As Long
                        If Long.TryParse(totalMatch.Groups(1).Value, NumberStyles.Integer, CultureInfo.InvariantCulture, total) Then
                            TotalFrames(id) = total
                        End If
                    End If

                    Dim progressMatch = ProgressRegex.Match(text)
                    If Not progressMatch.Success Then
                        Return
                    End If

                    Dim fps As Double
                    Dim frame As Long
                    If Not Double.TryParse(progressMatch.Groups(1).Value, NumberStyles.Float, CultureInfo.InvariantCulture, fps) Then
                        Return
                    End If
                    If Not Long.TryParse(progressMatch.Groups(2).Value, NumberStyles.Integer, CultureInfo.InvariantCulture, frame) Then
                        Return
                    End If
                    Dim eta = progressMatch.Groups(3).Value

                    Dim task = FindTask(id)
                    If task Is Nothing OrElse task.进度 Is Nothing Then
                        Return
                    End If

                    Dim known As Long = 0
                    TotalFrames.TryGetValue(id, known)
                    Dim percent As Double = 0
                    If known > 0 Then
                        percent = Math.Min(1.0, frame / CDbl(known))
                    End If
                    task.进度.百分比 = percent
                    If known > 0 Then
                        task.进度.进度文本 = (percent * 100).ToString("F1", CultureInfo.InvariantCulture) & "%"
                    Else
                        task.进度.进度文本 = ""
                    End If
                    task.进度.效率文本 = fps.ToString("F2", CultureInfo.InvariantCulture) & " FPS"
                    task.进度.时间文本 = eta
                    task.进度.当前阶段 = "视频超分"
                    SyncLock TelemetryLock
                        Telemetry(id) = New PreviewTelemetry With {.Fps = fps, .Frame = frame, .TotalFrames = known}
                    End SyncLock

                    Dim now = DateTime.Now
                    Dim last As DateTime = DateTime.MinValue
                    LastSizeUpdate.TryGetValue(id, last)
                    If (now - last).TotalSeconds >= 2 Then
                        LastSizeUpdate(id) = now
                        UpdateOutputSize(task)
                    End If
                End Using
            Catch
                ' 进度解析失败不影响队列
            End Try
        End Sub

        Private Shared Function TryGetTaskId(json As String) As String
            Try
                Using doc = JsonDocument.Parse(json)
                    Dim root = doc.RootElement
                    Dim taskEl As JsonElement = Nothing
                    If Not root.TryGetProperty("task", taskEl) Then
                        Return ""
                    End If
                    Dim idEl As JsonElement = Nothing
                    If Not taskEl.TryGetProperty("id", idEl) Then
                        Return ""
                    End If
                    Return idEl.GetString()
                End Using
            Catch
                Return ""
            End Try
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

        Private Shared Sub UpdateOutputSize(task As 编码任务_v6)
            Try
                Dim output = task.输出文件
                If String.IsNullOrWhiteSpace(output) OrElse Not File.Exists(output) Then
                    Return
                End If
                Dim length = New FileInfo(output).Length
                task.进度.输出大小KB = Math.Max(1, length \ 1024)
                task.进度.输出大小文本 = FormatSizeKb(length \ 1024)
            Catch
            End Try
        End Sub

        Private Shared Function FormatSizeKb(kb As Long) As String
            If kb >= 1024L * 1024L Then
                Return (kb / 1024.0 / 1024.0).ToString("F2", CultureInfo.InvariantCulture) & " GB"
            End If
            If kb >= 1024L Then
                Return (kb / 1024.0).ToString("F0", CultureInfo.InvariantCulture) & " MB"
            End If
            Return kb.ToString(CultureInfo.InvariantCulture) & " KB"
        End Function

    End Class

End Namespace
