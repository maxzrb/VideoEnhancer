Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports FFmpegFreeUI

Namespace videoenhancer

    ''' <summary>编码队列中的一个可预览任务（插件中转任务或 3FUI 原生 ffmpeg 任务）。</summary>
    Public Class PreviewTaskInfo
        Public Property Id As String = ""
        Public Property Name As String = ""
        Public Overrides Function ToString() As String
            Return If(String.IsNullOrWhiteSpace(Name), Id, Name)
        End Function
    End Class

    ''' <summary>
    ''' 实时预览引擎：轮询编码队列中正在执行的任务（支持多任务选择，默认最上面一个）。
    ''' 插件中转任务读取 BackendProgress 遥测（FPS / 帧号）；3FUI 原生 ffmpeg 任务直接读取
    ''' 队列已解析的进度（当前时间 / 输出大小），并用 ffprobe 探测输入帧率换算 FPS。
    ''' 用 ffmpeg 从输出文件抽取最新已完成帧（-sseof；失败时按任务进度回退输入文件）。
    ''' 事件通过宿主控件 BeginInvoke 封送回 UI 线程。
    ''' </summary>
    Friend Class PreviewEngine
        Implements IDisposable

        Public Event FrameReady(sender As Object, image As Image)
        Public Event StatusChanged(sender As Object, text As String, isError As Boolean)
        Public Event TasksChanged(sender As Object, tasks As List(Of PreviewTaskInfo))

        Private ReadOnly _config As PluginConfig
        Private ReadOnly _owner As Control
        Private ReadOnly _timer As New Timer() With {.Interval = 500}
        Private ReadOnly _lock As New Object()
        Private ReadOnly _timeEstimate As New Dictionary(Of String, TimeSample)(StringComparer.Ordinal)
        Private ReadOnly _inputFpsCache As New Dictionary(Of String, Double)(StringComparer.OrdinalIgnoreCase)

        Private _running As Boolean = False
        Private _visible As Boolean = True
        Private _busy As Boolean = False
        Private _intervalSeconds As Double = 1.0
        Private _keyframeMode As Boolean = False
        Private _ffmpeg As String = ""
        Private _ffprobe As String = ""
        Private _selectedTaskId As String = ""
        Private _lastTaskHash As String = ""
        Private _lastTaskId As String = ""
        Private _lastExtractSize As Long = -1
        Private _lastProgressSeconds As Double = -1
        Private _lastExtractAt As DateTime = DateTime.MinValue
        Private _lastExtractStartAt As DateTime = DateTime.MinValue
        Private _lastKeyframePts As Double = -1
        Private _lastProbeAt As DateTime = DateTime.MinValue
        Private _lastExtractError As String = ""
        Private _lastExtractFailAt As DateTime = DateTime.MinValue
        Private _pendingExtract As Boolean = False

        ' 抽帧节流：输出至少增长 64KB 或进度前进 0.25 秒才值得抽一帧；
        ' 两次抽帧开始之间至少间隔 max(0.5, 所选切换间隔) 秒，避免高帧率时进程风暴。
        Private Const MinSizeDelta As Long = 65536
        Private Const MinProgressDelta As Double = 0.25

        Private Structure TimeSample
            Public Wall As DateTime
            Public Seconds As Double
        End Structure

        ''' <summary>一次抽帧的计划：输出/输入路径 + 定位时间。</summary>
        Private Structure ExtractPlan
            Public Output As String
            Public Input As String
            Public SeekSeconds As Double      ' 关键帧模式的目标位置；<0 = 未指定
            Public ProgressSeconds As Double  ' 已知内容位置；<0 = 未知
        End Structure

        Public Sub New(config As PluginConfig, owner As Control)
            _config = config
            _owner = owner
            AddHandler _timer.Tick, AddressOf OnTick
        End Sub

        Public Property IntervalSeconds As Double
            Get
                Return _intervalSeconds
            End Get
            Set(value As Double)
                If value > 0 Then
                    _intervalSeconds = value
                    _keyframeMode = False
                End If
            End Set
        End Property

        Private ReadOnly Property MinExtractGap As Double
            Get
                Return Math.Max(0.5, _intervalSeconds)
            End Get
        End Property

        Public ReadOnly Property IsKeyframeMode As Boolean
            Get
                Return _keyframeMode
            End Get
        End Property

        Public Sub SetKeyframeMode(enabled As Boolean)
            _keyframeMode = enabled
        End Sub

        ''' <summary>当前要预览的任务 ID；为空时自动选择队列最上面的执行中任务。</summary>
        Public Property SelectedTaskId As String
            Get
                Return _selectedTaskId
            End Get
            Set(value As String)
                Dim v = If(value, "")
                If Not String.Equals(_selectedTaskId, v, StringComparison.Ordinal) Then
                    _selectedTaskId = v
                    ResetTaskState()
                End If
            End Set
        End Property

        ''' <summary>预览页是否可见；不可见时暂停引擎，减少 UI 线程负载（切换标签不卡顿）。</summary>
        Public Property PreviewVisible As Boolean
            Get
                Return _visible
            End Get
            Set(value As Boolean)
                _visible = value
                If _visible Then
                    If _running AndAlso Not _timer.Enabled Then
                        _timer.Start()
                    End If
                Else
                    _timer.Stop()
                End If
            End Set
        End Property

        Public Sub Start()
            If _running Then
                Return
            End If
            _running = True
            If _visible Then
                _timer.Start()
            End If
        End Sub

        Public Sub Stop_()
            _running = False
            _timer.Stop()
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Stop_()
            RemoveHandler _timer.Tick, AddressOf OnTick
            _timer.Dispose()
        End Sub

        Private Sub RaiseStatus(text As String, isError As Boolean)
            Try
                If _owner IsNot Nothing AndAlso _owner.IsHandleCreated Then
                    _owner.BeginInvoke(New Action(Sub() RaiseEvent StatusChanged(Me, text, isError)))
                End If
            Catch
            End Try
        End Sub

        Private Sub OnTick(sender As Object, e As EventArgs)
            If Not _running OrElse Not _visible Then
                Return
            End If
            Try
                ResolveTools()
                If _ffmpeg = "" Then
                    RaiseStatus("未找到 ffmpeg：请确认 videoenhancer.exe 同级已安装 bin 核心组件", True)
                    Return
                End If

                Dim tasks = CollectActiveTasks()
                RaiseTasksChanged(tasks)
                Dim task = ResolveSelectedTask(tasks)
                If task Is Nothing Then
                    ResetForIdle()
                    Return
                End If

                If _lastTaskId <> task.ID Then
                    _lastTaskId = task.ID
                    ResetTaskState()
                End If

                Dim tele = BackendProgress.GetTelemetry(task.ID)
                Dim fps As Double = 0
                Dim frame As Long = 0
                Dim total As Long = 0
                Dim nativeMode As Boolean = False
                If tele IsNot Nothing AndAlso tele.Frame > 0 Then
                    fps = tele.Fps
                    frame = tele.Frame
                    total = tele.TotalFrames
                ElseIf task.进度 IsNot Nothing AndAlso task.进度.当前时间 > TimeSpan.Zero Then
                    nativeMode = True
                    Dim inputFps = ProbeInputFps(task.输入文件)
                    Dim speed = EstimateFpsByTime(task.ID, task.进度.当前时间.TotalSeconds)
                    fps = speed * inputFps
                    If inputFps > 0 Then
                        frame = CLng(Math.Round(task.进度.当前时间.TotalSeconds * inputFps))
                        If task.进度.总时长 > TimeSpan.Zero Then
                            total = CLng(Math.Round(task.进度.总时长.TotalSeconds * inputFps))
                        End If
                    End If
                End If

                Dim output = task.输出文件
                Dim hasOutput As Boolean = Not String.IsNullOrWhiteSpace(output) AndAlso File.Exists(output)
                Dim size As Long = 0
                If hasOutput Then
                    Try
                        size = New FileInfo(output).Length
                    Catch
                        size = 0
                    End Try
                End If
                Dim progressSeconds As Double = 0
                If task.进度 IsNot Nothing Then
                    progressSeconds = task.进度.当前时间.TotalSeconds
                End If
                ' CLI 中转任务没有 3FUI 原生 ffmpeg 进度：用遥测帧号 + 输入帧率换算内容位置
                If progressSeconds <= 0 AndAlso frame > 0 Then
                    Dim inputFps = ProbeInputFps(task.输入文件)
                    If inputFps > 0 Then
                        progressSeconds = frame / inputFps
                    End If
                End If

                Dim shouldSwitch As Boolean = False
                Dim seekSeconds As Double = -1
                If _keyframeMode Then
                    shouldSwitch = CheckKeyframeProbe(output, size, seekSeconds)
                Else
                    Dim elapsed = (DateTime.UtcNow - _lastExtractStartAt).TotalSeconds
                    If elapsed >= MinExtractGap Then
                        ' 输出至少增长 64KB 或进度前进 0.25 秒才切换，避免高帧率下频繁抽帧
                        Dim sizeChanged As Boolean = size - _lastExtractSize >= MinSizeDelta
                        Dim progressChanged As Boolean = progressSeconds - _lastProgressSeconds >= MinProgressDelta
                        If hasOutput Then
                            shouldSwitch = sizeChanged OrElse progressChanged
                        Else
                            shouldSwitch = progressChanged
                        End If
                    End If
                End If
                _lastProgressSeconds = progressSeconds

                If shouldSwitch AndAlso Not _busy Then
                    StartExtract(output, task.输入文件, seekSeconds, progressSeconds, size)
                ElseIf shouldSwitch AndAlso _busy Then
                    ' 抽帧进行中：记下待补一次，完成后用最新位置补一帧（合并突发，避免进程风暴）
                    _pendingExtract = True
                End If
                ' busy 结束后只补一次最新帧（取最新进度）
                If Not _busy AndAlso _pendingExtract AndAlso
                   (DateTime.UtcNow - _lastExtractStartAt).TotalSeconds >= MinExtractGap Then
                    _pendingExtract = False
                    StartExtract(output, task.输入文件, seekSeconds, progressSeconds, size)
                End If

                RaiseStatus(BuildStatusText(task, fps, frame, total, nativeMode), False)
            Catch ex As Exception
                RaiseStatus("预览引擎异常：" & ex.Message, True)
            End Try
        End Sub

        Private Sub ResetTaskState()
            _lastExtractSize = -1
            _lastProgressSeconds = -1
            _lastExtractAt = DateTime.MinValue
            _lastExtractStartAt = DateTime.MinValue
            _lastKeyframePts = -1
            _lastProbeAt = DateTime.MinValue
            _pendingExtract = False
        End Sub

        Private Sub ResetForIdle()
            If _lastTaskId <> "" Then
                _lastTaskId = ""
                ResetTaskState()
                RaiseStatus("等待编码队列任务…", False)
            End If
        End Sub

        Private Function CollectActiveTasks() As List(Of PreviewTaskInfo)
            Dim result As New List(Of PreviewTaskInfo)()
            Try
                Dim queue = 编码队列_v6.队列
                SyncLock queue
                    For Each t In queue
                        If t.正在执行 Then
                            result.Add(New PreviewTaskInfo() With {
                                .Id = t.ID,
                                .Name = If(String.IsNullOrWhiteSpace(t.任务名称), t.ID, t.任务名称)
                            })
                        End If
                    Next
                End SyncLock
            Catch
            End Try
            Return result
        End Function

        Private Sub RaiseTasksChanged(tasks As List(Of PreviewTaskInfo))
            Dim sb As New StringBuilder()
            For Each t In tasks
                If sb.Length > 0 Then
                    sb.Append("|")
                End If
                sb.Append(t.Id)
            Next
            Dim hash = sb.ToString()
            If String.Equals(hash, _lastTaskHash, StringComparison.Ordinal) Then
                Return
            End If
            _lastTaskHash = hash
            Try
                If _owner IsNot Nothing AndAlso _owner.IsHandleCreated Then
                    Dim captured = tasks
                    _owner.BeginInvoke(New Action(Sub() RaiseEvent TasksChanged(Me, captured)))
                End If
            Catch
            End Try
        End Sub

        ''' <summary>返回要预览的任务：优先用户选择的；否则队列最上面（第一个）执行中的任务。</summary>
        Private Function ResolveSelectedTask(tasks As List(Of PreviewTaskInfo)) As 编码任务_v6
            If _selectedTaskId <> "" Then
                Dim selected = FindTaskById(_selectedTaskId)
                If selected IsNot Nothing AndAlso selected.正在执行 Then
                    Return selected
                End If
                ' 用户选择的已结束 → 自动回到最上面
                _selectedTaskId = ""
                Try
                    If _owner IsNot Nothing AndAlso _owner.IsHandleCreated Then
                        Dim captured = tasks
                        _owner.BeginInvoke(New Action(Sub() RaiseEvent TasksChanged(Me, captured)))
                    End If
                Catch
                End Try
            End If
            If tasks.Count > 0 Then
                _selectedTaskId = tasks(0).Id
                Return FindTaskById(tasks(0).Id)
            End If
            Return Nothing
        End Function

        Private Shared Function FindTaskById(id As String) As 编码任务_v6
            Try
                Dim queue = 编码队列_v6.队列
                SyncLock queue
                    Return queue.FirstOrDefault(Function(t) String.Equals(t.ID, id, StringComparison.Ordinal))
                End SyncLock
            Catch
                Return Nothing
            End Try
        End Function

        Private Function BuildStatusText(task As 编码任务_v6, fps As Double, frame As Long, total As Long, nativeMode As Boolean) As String
            Dim sb As New StringBuilder()
            If nativeMode Then
                sb.Append("原生 ffmpeg")
                If task.进度 IsNot Nothing Then
                    If task.进度.当前时间 > TimeSpan.Zero Then
                        sb.Append(" · 处理到 ").Append(FormatClock(task.进度.当前时间.TotalSeconds))
                        If frame > 0 Then
                            sb.Append("（约 ").Append(frame.ToString(CultureInfo.InvariantCulture)).Append(" 帧")
                            If total > 0 Then
                                sb.Append("/").Append(total.ToString(CultureInfo.InvariantCulture))
                            End If
                            sb.Append("）")
                        End If
                    End If
                    If Not String.IsNullOrWhiteSpace(task.进度.输出大小文本) Then
                        sb.Append(" · 输出 ").Append(task.进度.输出大小文本)
                    End If
                End If
            ElseIf fps > 0 OrElse frame > 0 Then
                If fps > 0 Then
                    sb.Append("后端 ").Append(fps.ToString("F2", CultureInfo.InvariantCulture)).Append(" FPS")
                End If
                If frame > 0 Then
                    If sb.Length > 0 Then
                        sb.Append(" · ")
                    End If
                    sb.Append("已处理 ").Append(frame.ToString(CultureInfo.InvariantCulture)).Append(" 帧")
                    If total > 0 Then
                        sb.Append("/").Append(total.ToString(CultureInfo.InvariantCulture))
                    End If
                End If
            Else
                sb.Append("等待进度…")
            End If

            If sb.Length > 0 Then
                sb.Append(" · ")
            End If
            If _keyframeMode Then
                sb.Append("关键帧模式：命中新关键帧才切换")
            Else
                Dim perSwitch = Math.Max(1, CInt(Math.Round(fps * _intervalSeconds)))
                sb.Append("每 ").Append(_intervalSeconds.ToString("0.##", CultureInfo.InvariantCulture)).Append(" 秒前进约 ").Append(perSwitch).Append(" 帧")
            End If
            ' 最近抽帧失败时附带原因（如输出容器尚未可读），便于排查黑屏
            SyncLock _lock
                If _lastExtractError <> "" AndAlso (DateTime.UtcNow - _lastExtractFailAt).TotalSeconds < 3 Then
                    sb.Append(" · 抽帧失败：").Append(_lastExtractError)
                End If
            End SyncLock
            Return sb.ToString()
        End Function

        Private Shared Function FormatClock(seconds As Double) As String
            If seconds < 0 Then
                seconds = 0
            End If
            Dim ts = TimeSpan.FromSeconds(seconds)
            If ts.TotalHours >= 1 Then
                Return ts.ToString("hh\:mm\:ss", CultureInfo.InvariantCulture)
            End If
            Return ts.ToString("mm\:ss", CultureInfo.InvariantCulture)
        End Function

        ''' <summary>关键帧模式：ffprobe 列出输出文件关键帧 pts，命中新关键帧才切换。</summary>
        Private Function CheckKeyframeProbe(output As String, size As Long, ByRef seekSeconds As Double) As Boolean
            If (DateTime.UtcNow - _lastProbeAt).TotalMilliseconds < 500 Then
                Return False
            End If
            _lastProbeAt = DateTime.UtcNow
            If _ffprobe = "" OrElse String.IsNullOrWhiteSpace(output) OrElse Not File.Exists(output) OrElse size <= _lastExtractSize Then
                Return False
            End If
            Dim maxPts As Double = -1
            Try
                Dim psi As New ProcessStartInfo()
                psi.FileName = _ffprobe
                psi.UseShellExecute = False
                psi.CreateNoWindow = True
                psi.RedirectStandardOutput = True
                psi.StandardOutputEncoding = Encoding.UTF8
                psi.ArgumentList.Add("-v")
                psi.ArgumentList.Add("error")
                psi.ArgumentList.Add("-select_streams")
                psi.ArgumentList.Add("v:0")
                psi.ArgumentList.Add("-skip_frame")
                psi.ArgumentList.Add("nokey")
                psi.ArgumentList.Add("-show_frames")
                psi.ArgumentList.Add("-show_entries")
                psi.ArgumentList.Add("frame=pts_time")
                psi.ArgumentList.Add("-of")
                psi.ArgumentList.Add("csv=p=0")
                psi.ArgumentList.Add(output)
                Using p = Process.Start(psi)
                    If p Is Nothing Then
                        Return False
                    End If
                    Dim text = p.StandardOutput.ReadToEnd()
                    p.WaitForExit(10000)
                    For Each line In text.Split(Convert.ToChar(10))
                        Dim t As Double
                        If Double.TryParse(line.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, t) Then
                            If t > maxPts Then
                                maxPts = t
                            End If
                        End If
                    Next
                End Using
            Catch
                Return False
            End Try
            If maxPts > _lastKeyframePts + 0.001 Then
                _lastKeyframePts = maxPts
                seekSeconds = maxPts
                Return True
            End If
            Return False
        End Function

        ''' <summary>在后台线程开始一次抽帧；记录开始时间与文件大小，避免同一状态反复重试。</summary>
        Private Sub StartExtract(output As String, input As String, seekSeconds As Double, progressSeconds As Double, size As Long)
            _busy = True
            _lastExtractStartAt = DateTime.UtcNow
            Dim plan As New ExtractPlan() With {
                .Output = output,
                .Input = input,
                .SeekSeconds = seekSeconds,
                .ProgressSeconds = progressSeconds
            }
            Try
                System.Threading.Tasks.Task.Run(New Action(Sub() DoExtract(plan)))
            Catch
                _busy = False
            End Try
            ' 无论成功与否都记录本次尝试时的文件大小，避免同一状态反复重试
            _lastExtractSize = size
        End Sub

        Private Sub DoExtract(plan As ExtractPlan)
            Try
                Dim img As Image = Nothing
                Dim errorText As String = ""
                Try
                    If Not String.IsNullOrWhiteSpace(plan.Output) AndAlso File.Exists(plan.Output) Then
                        img = ExtractFromOutput(plan, errorText)
                    End If
                Catch
                End Try
                If img Is Nothing Then
                    Try
                        img = ExtractFromInput(plan, errorText)
                    Catch
                    End Try
                End If

                SyncLock _lock
                    If img IsNot Nothing Then
                        _lastExtractError = ""
                        _lastExtractAt = DateTime.UtcNow
                    Else
                        _lastExtractError = errorText
                        _lastExtractFailAt = DateTime.UtcNow
                    End If
                End SyncLock

                If img IsNot Nothing Then
                    Try
                        If _owner IsNot Nothing AndAlso _owner.IsHandleCreated Then
                            Dim captured = img
                            _owner.BeginInvoke(New Action(Sub()
                                If Not _running Then
                                    captured.Dispose()
                                    Return
                                End If
                                RaiseEvent FrameReady(Me, captured)
                            End Sub))
                        Else
                            img.Dispose()
                        End If
                    Catch
                        img.Dispose()
                    End Try
                End If
            Catch
            Finally
                _busy = False
            End Try
        End Sub

        ''' <summary>
        ''' 从输出文件抽帧（优先显示增强后的画面）。
        ''' 实测：正在写入的 MKV 用 -sseof 抽帧会失败，而用 -ss 定位到已写入区域可以成功，
        ''' 因此按已知进度回退 0.2/1/2 秒尝试，全部失败再退到 -sseof，最后回退输入文件。
        ''' </summary>
        Private Function ExtractFromOutput(plan As ExtractPlan, ByRef errorText As String) As Image
            ' 关键帧模式：直接定位到目标关键帧
            If plan.SeekSeconds > 0 Then
                Dim img = ExtractAt(plan.Output, plan.SeekSeconds, errorText)
                If img IsNot Nothing Then
                    Return img
                End If
            End If
            ' 已知内容位置 → -ss 逐级回退（越靠前越容易读到已写完的数据）
            If plan.ProgressSeconds > 0 Then
                Dim offsets As Double() = {0.2, 1.0, 2.0}
                For Each off In offsets
                    Dim t = plan.ProgressSeconds - off
                    If t < 0 Then
                        t = 0
                    End If
                    Dim img = ExtractAt(plan.Output, t, errorText)
                    If img IsNot Nothing Then
                        Return img
                    End If
                Next
            End If
            ' 位置未知 → 尝试读取文件末尾（文件已完整写入或容器头可读时有效）
            Return ExtractOutputEof(plan.Output, errorText)
        End Function

        ''' <summary>输出文件不可读时回退输入文件（输入总是完整可读，显示当前处理的画面内容）。</summary>
        Private Function ExtractFromInput(plan As ExtractPlan, ByRef errorText As String) As Image
            Dim input = plan.Input
            If String.IsNullOrWhiteSpace(input) OrElse Not File.Exists(input) Then
                Return Nothing
            End If
            Dim t As Double = -1
            If plan.SeekSeconds > 0 Then
                t = plan.SeekSeconds
            ElseIf plan.ProgressSeconds > 0 Then
                t = plan.ProgressSeconds
            End If
            If t < 0 Then
                Return Nothing
            End If
            Dim img = ExtractAt(input, t, errorText)
            If img Is Nothing AndAlso t > 0.5 Then
                img = ExtractAt(input, t - 0.5, errorText)
            End If
            Return img
        End Function

        ''' <summary>ffmpeg -ss 定位抽帧（-ss 在 -i 之前，输入定位，速度快）。</summary>
        Private Function ExtractAt(path As String, seconds As Double, ByRef errorText As String) As Image
            Try
                Dim psi As New ProcessStartInfo()
                psi.FileName = _ffmpeg
                psi.UseShellExecute = False
                psi.CreateNoWindow = True
                psi.RedirectStandardOutput = True
                psi.RedirectStandardError = True
                psi.ArgumentList.Add("-v")
                psi.ArgumentList.Add("error")
                psi.ArgumentList.Add("-nostdin")
                psi.ArgumentList.Add("-ss")
                psi.ArgumentList.Add(seconds.ToString("0.###", CultureInfo.InvariantCulture))
                psi.ArgumentList.Add("-i")
                psi.ArgumentList.Add(path)
                psi.ArgumentList.Add("-frames:v")
                psi.ArgumentList.Add("1")
                psi.ArgumentList.Add("-f")
                psi.ArgumentList.Add("image2pipe")
                psi.ArgumentList.Add("-c:v")
                psi.ArgumentList.Add("mjpeg")
                psi.ArgumentList.Add("-q:v")
                psi.ArgumentList.Add("2")
                psi.ArgumentList.Add("-")
                Return RunCapture(psi, errorText)
            Catch
                Return Nothing
            End Try
        End Function

        ''' <summary>ffmpeg -sseof 读取文件末尾抽帧（仅对已完整/可定位文件可靠）。</summary>
        Private Function ExtractOutputEof(path As String, ByRef errorText As String) As Image
            Try
                Dim psi As New ProcessStartInfo()
                psi.FileName = _ffmpeg
                psi.UseShellExecute = False
                psi.CreateNoWindow = True
                psi.RedirectStandardOutput = True
                psi.RedirectStandardError = True
                psi.ArgumentList.Add("-v")
                psi.ArgumentList.Add("error")
                psi.ArgumentList.Add("-nostdin")
                psi.ArgumentList.Add("-sseof")
                psi.ArgumentList.Add("-0.2")
                psi.ArgumentList.Add("-i")
                psi.ArgumentList.Add(path)
                psi.ArgumentList.Add("-frames:v")
                psi.ArgumentList.Add("1")
                psi.ArgumentList.Add("-f")
                psi.ArgumentList.Add("image2pipe")
                psi.ArgumentList.Add("-c:v")
                psi.ArgumentList.Add("mjpeg")
                psi.ArgumentList.Add("-q:v")
                psi.ArgumentList.Add("2")
                psi.ArgumentList.Add("-")
                Return RunCapture(psi, errorText)
            Catch
                Return Nothing
            End Try
        End Function

        ''' <summary>运行 ffmpeg 并抓取 stdout 的图像字节（mjpeg），超时强制结束，返回克隆位图；失败时带回错误提示。</summary>
        Private Function RunCapture(psi As ProcessStartInfo, ByRef errorText As String) As Image
            Using p As New Process()
                p.StartInfo = psi
                If Not p.Start() Then
                    errorText = "无法启动 ffmpeg"
                    Return Nothing
                End If
                Dim ms As New MemoryStream()
                Try
                    Dim copy = p.StandardOutput.BaseStream.CopyToAsync(ms)
                    Dim errTask = p.StandardError.ReadToEndAsync()
                    Dim exited = p.WaitForExit(6000)
                    If Not exited Then
                        Try : p.Kill() : Catch : End Try
                        Try : copy.Wait(1000) : Catch : End Try
                        errorText = "抽帧超时"
                        Return Nothing
                    End If
                    Try : copy.Wait(1000) : Catch : End Try
                    If ms.Length <= 0 Then
                        Dim err = ""
                        Try
                            err = errTask.Result.Trim()
                        Catch
                        End Try
                        errorText = If(String.IsNullOrWhiteSpace(err), "无输出帧", LastLine(err))
                        Return Nothing
                    End If
                    ms.Position = 0
                    Using src = Image.FromStream(ms)
                        Return New Bitmap(src)
                    End Using
                Catch ex As Exception
                    errorText = "解码失败：" & ex.Message
                    Return Nothing
                Finally
                    ms.Dispose()
                End Try
            End Using
        End Function

        Private Shared Function LastLine(text As String) As String
            Try
                Dim lines = text.Split(Convert.ToChar(10))
                For i As Integer = lines.Length - 1 To 0 Step -1
                    Dim v = lines(i).Trim()
                    If v.Length > 0 Then
                        Return v
                    End If
                Next
            Catch
            End Try
            Return text
        End Function

Private Function EstimateFpsByTime(taskId As String, seconds As Double) As Double
            Dim now = DateTime.UtcNow
            Dim sample As TimeSample = Nothing
            If _timeEstimate.TryGetValue(taskId, sample) Then
                Dim dt = (now - sample.Wall).TotalSeconds
                Dim ds = seconds - sample.Seconds
                If dt >= 0.5 AndAlso dt <= 10 AndAlso ds >= 0 AndAlso ds < dt * 200 Then
                    Dim f = ds / dt
                    If f > 0 AndAlso f < 1000 Then
                        _timeEstimate(taskId) = New TimeSample With {.Wall = now, .Seconds = seconds}
                        Return f
                    End If
                End If
            End If
            _timeEstimate(taskId) = New TimeSample With {.Wall = now, .Seconds = seconds}
            Return 0
        End Function

        ''' <summary>用 ffprobe 探测输入视频帧率（avg_frame_rate，num/den），结果缓存。</summary>
        Private Function ProbeInputFps(inputPath As String) As Double
            If String.IsNullOrWhiteSpace(inputPath) Then
                Return 0
            End If
            Dim cached As Double = 0
            If _inputFpsCache.TryGetValue(inputPath, cached) Then
                Return cached
            End If
            Dim fps As Double = 0
            Try
                If _ffprobe <> "" Then
                    Dim psi As New ProcessStartInfo()
                    psi.FileName = _ffprobe
                    psi.UseShellExecute = False
                    psi.CreateNoWindow = True
                    psi.RedirectStandardOutput = True
                    psi.StandardOutputEncoding = Encoding.UTF8
                    psi.ArgumentList.Add("-v")
                    psi.ArgumentList.Add("error")
                    psi.ArgumentList.Add("-select_streams")
                    psi.ArgumentList.Add("v:0")
                    psi.ArgumentList.Add("-show_entries")
                    psi.ArgumentList.Add("stream=avg_frame_rate")
                    psi.ArgumentList.Add("-of")
                    psi.ArgumentList.Add("default=noprint_wrappers=1:nokey=1")
                    psi.ArgumentList.Add(inputPath)
                    Using p = Process.Start(psi)
                        If p IsNot Nothing Then
                            Dim text = p.StandardOutput.ReadToEnd()
                            p.WaitForExit(8000)
                            For Each line In text.Split(Convert.ToChar(10))
                                Dim v = ParseRationalFps(line.Trim())
                                If v > 0 Then
                                    fps = v
                                    Exit For
                                End If
                            Next
                        End If
                    End Using
                End If
            Catch
            End Try
            _inputFpsCache(inputPath) = fps
            Return fps
        End Function

        Private Shared Function ParseRationalFps(text As String) As Double
            If text = "" Then
                Return 0
            End If
            Dim idx = text.IndexOf("/"c)
            If idx > 0 Then
                Dim num As Double = 0
                Dim den As Double = 0
                If Double.TryParse(text.Substring(0, idx), NumberStyles.Float, CultureInfo.InvariantCulture, num) AndAlso
                   Double.TryParse(text.Substring(idx + 1), NumberStyles.Float, CultureInfo.InvariantCulture, den) Then
                    If den > 0 Then
                        Return num / den
                    End If
                End If
                Return 0
            End If
            Dim v As Double = 0
            If Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, v) Then
                Return v
            End If
            Return 0
        End Function

        ''' <summary>
        ''' 从 videoenhancer.exe 同级的 bin 目录定位 ffmpeg 与 ffprobe。
        ''' </summary>
        Private Sub ResolveTools()
            If _ffmpeg <> "" AndAlso File.Exists(_ffmpeg) Then
                Return
            End If
            _ffmpeg = ""
            _ffprobe = ""
            Try
                Dim exePath = If(_config Is Nothing, "", _config.ExePath)
                Dim exeDir = ""
                If Not String.IsNullOrWhiteSpace(exePath) Then
                    exeDir = Path.GetDirectoryName(exePath)
                End If
                If exeDir = "" Then
                    exeDir = Environment.CurrentDirectory
                End If
                Dim core As String = exeDir
                Dim ff1 = Path.Combine(core, "bin", "ffmpeg", "ffmpeg.exe")
                Dim ff2 = Path.Combine(core, "bin", "ffmpeg.exe")
                If File.Exists(ff1) Then
                    _ffmpeg = ff1
                ElseIf File.Exists(ff2) Then
                    _ffmpeg = ff2
                End If
                Dim fp1 = Path.Combine(core, "bin", "ffmpeg", "ffprobe.exe")
                Dim fp2 = Path.Combine(core, "bin", "ffprobe.exe")
                If File.Exists(fp1) Then
                    _ffprobe = fp1
                ElseIf File.Exists(fp2) Then
                    _ffprobe = fp2
                End If
            Catch
            End Try
        End Sub

    End Class

End Namespace