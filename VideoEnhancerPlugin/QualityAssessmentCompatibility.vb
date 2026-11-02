Imports System
Imports System.Collections
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Threading.Tasks
Imports System.Windows.Forms

Namespace videoenhancer
    ''' <summary>为走全局中转路径的质量评测补全真实 FFmpeg 模型，不修改全局设置或宿主程序集。</summary>
    Friend NotInheritable Class QualityAssessmentCompatibility
        Private Const Flags As BindingFlags = BindingFlags.Public Or BindingFlags.NonPublic Or BindingFlags.Static Or BindingFlags.Instance
        Private Shared _busy As Boolean
        Private Shared _cacheKey As String = ""
        Private Shared _models As New List(Of String)()
        Private Shared _cuda As Boolean
        Private Shared ReadOnly UnsupportedTypes As New HashSet(Of Type)()

        Friend Shared Async Sub Poll(exePath As String)
            Try
                If _busy OrElse Not String.Equals(HostSettings.AlternativeProcessPath.Trim().Trim(""""c), exePath, StringComparison.OrdinalIgnoreCase) Then Return
                For Each form As Form In Application.OpenForms
                    If form.GetType().Name = "Form_v6_集成工具_质量评测" AndAlso form.Visible Then
                        Await RepairAsync(form, FfmpegToolResolver.Resolve("ffmpeg.exe"))
                        Return
                    End If
                Next
            Catch ex As Exception
                Trace.WriteLine("[VideoEnhancer][VMAF] 兼容入口未完成：" & ex.Message)
            End Try
        End Sub

        Friend Shared Async Function RepairAsync(form As Control, ffmpeg As String) As Task
            If _busy OrElse form Is Nothing OrElse form.IsDisposed OrElse UnsupportedTypes.Contains(form.GetType()) Then Return
            _busy = True
            Try
                Dim hostType = form.GetType()
                Dim scan = hostType.GetMethod("从FFmpeg运行库提取Vmaf模型列表", Flags)
                Dim query = hostType.GetMethod("运行FFmpeg模型查询Async", Flags)
                Dim containsFilter = hostType.GetMethod("FFmpeg滤镜列表包含", Flags)
                Dim buildItems = hostType.GetMethod("构建Vmaf模型显示项", Flags)
                If scan Is Nothing OrElse query Is Nothing OrElse containsFilter Is Nothing OrElse buildItems Is Nothing Then
                    UnsupportedTypes.Add(hostType)
                    Trace.WriteLine("[VideoEnhancer][VMAF] 当前宿主不提供兼容的模型枚举接口，保留原有界面。")
                    Return
                End If
                If IsWorking(form) OrElse DirectCast(HostRuntime.ReadMember(form, "可用Vmaf模型"), ICollection).Count > 0 Then Return
                If String.IsNullOrWhiteSpace(ffmpeg) OrElse Not File.Exists(ffmpeg) Then Return
                Dim processPath = HostSettings.AlternativeProcessPath
                Dim key = GetCacheKey(ffmpeg)
                If key <> _cacheKey Then
                    _models = New List(Of String)()
                    _cuda = False
                    ' 复用宿主的扫描、筛选和分类规则；不硬编码模型名称或 CUDA 支持。
                    Dim modelsTask = Task.Run(Function() DirectCast(scan.Invoke(Nothing, {ffmpeg}), List(Of String)))
                    Dim queryTask = DirectCast(query.Invoke(Nothing, {ffmpeg, "-hide_banner -filters", TimeSpan.FromSeconds(10)}), Task)
                    Await Task.WhenAll(modelsTask, queryTask)
                    Dim result = queryTask.GetType().GetProperty("Result").GetValue(queryTask)
                    If HostRuntime.ReadValue(Of Integer)(result, "ExitCode") <> 0 Then Return
                    Dim output = HostRuntime.ReadValue(Of String)(result, "Output")
                    If CBool(containsFilter.Invoke(Nothing, {output, "libvmaf"})) Then
                        _models = Await modelsTask
                        _cuda = CBool(containsFilter.Invoke(Nothing, {output, "libvmaf_cuda"}))
                    End If
                    _cacheKey = key
                End If
                ' 等待期间用户可能开始评测、刷新或切换 FFmpeg，不覆盖进行中的操作。
                If form.IsDisposed OrElse IsWorking(form) OrElse GetCacheKey(ffmpeg) <> key OrElse
                    Not String.Equals(HostSettings.AlternativeProcessPath, processPath, StringComparison.OrdinalIgnoreCase) OrElse
                    Not String.Equals(FfmpegToolResolver.Resolve("ffmpeg.exe"), ffmpeg, StringComparison.OrdinalIgnoreCase) OrElse
                    DirectCast(HostRuntime.ReadMember(form, "可用Vmaf模型"), ICollection).Count > 0 OrElse _models.Count = 0 Then Return
                Dim items = DirectCast(buildItems.Invoke(Nothing, {_models, _cuda}), IList)
                If items.Count = 0 Then Return
                Dim available = DirectCast(Activator.CreateInstance(items.GetType()), IList)
                For Each item In items : available.Add(item) : Next
                Dim combo = HostRuntime.ReadMember(form, "MCB_模型选择")
                Dim selected = HostRuntime.ReadMember(combo, "SelectedModel")
                Dim previousModel = HostRuntime.ReadValue(Of String)(combo, "SelectedModelValue")
                Dim previousCuda = HostRuntime.ReadValue(Of Boolean)(combo, "SelectedUsesCuda")
                If selected IsNot Nothing AndAlso Not HostRuntime.ReadValue(Of Boolean)(selected, "IsAuto") AndAlso
                    Not items.Cast(Of Object)().Any(Function(item) HostRuntime.ReadValue(Of String)(item, "ModelValue") = previousModel AndAlso
                        HostRuntime.ReadValue(Of Boolean)(item, "UseCuda") = previousCuda) Then items.Add(selected)
                combo.GetType().GetMethod("ReplaceDisplayItems", Flags).Invoke(combo, {items})
                UpdateToolTips(hostType, combo)
                Dim index = CInt(combo.GetType().GetMethod("FindModelIndex", Flags).Invoke(combo, {previousModel, previousCuda}))
                HostRuntime.WriteMember(combo, "SelectedIndex", If(index >= 0, index, HostRuntime.ReadValue(Of Integer)(combo, "FirstModelIndex")))
                HostRuntime.WriteMember(form, "可用Vmaf模型", available)
                Trace.WriteLine($"[VideoEnhancer][VMAF] 已从真实 FFmpeg 恢复 {available.Count} 个可用模型：{ffmpeg}")
            Catch ex As Exception
                If TypeOf ex Is MissingMemberException OrElse TypeOf ex Is InvalidCastException Then UnsupportedTypes.Add(form.GetType())
                Trace.WriteLine("[VideoEnhancer][VMAF] 兼容枚举未完成：" & ex.Message)
            Finally
                _busy = False
            End Try
        End Function

        Private Shared Function IsWorking(form As Control) As Boolean
            Return HostRuntime.ReadValue(Of Boolean)(form, "正在刷新Vmaf模型列表") OrElse HostRuntime.ReadValue(Of Boolean)(form, "正在评测")
        End Function

        Private Shared Function GetCacheKey(ffmpeg As String) As String
            Dim files As New SortedSet(Of String)(StringComparer.OrdinalIgnoreCase) From {Path.GetFullPath(ffmpeg)}
            For Each pattern In {"avfilter*.dll", "libavfilter*.dll", "libvmaf*.dll"}
                files.UnionWith(Directory.EnumerateFiles(Path.GetDirectoryName(ffmpeg), pattern, SearchOption.TopDirectoryOnly))
            Next
            Return String.Join("|", files.Select(Function(file)
                                                    Dim info As New FileInfo(file)
                                                    Return file & ":" & info.Length & ":" & info.LastWriteTimeUtc.Ticks
                                                End Function))
        End Function

        Private Shared Sub UpdateToolTips(hostType As Type, combo As Object)
            Dim tips = HostRuntime.ReadMember(combo, "ItemToolTips")
            Dim descriptions = DirectCast(HostRuntime.ReadMember(hostType, "Vmaf模型描述"), IDictionary)
            Dim seen As New HashSet(Of String)()
            tips.GetType().GetMethod("Clear").Invoke(tips, Nothing)
            For Each item In DirectCast(HostRuntime.ReadMember(combo, "Models"), IEnumerable)
                Dim title = HostRuntime.ReadValue(Of String)(item, "DisplayText")
                If Not seen.Add(title) Then Continue For
                Dim model = HostRuntime.ReadValue(Of String)(item, "ModelValue")
                Dim description = If(HostRuntime.ReadValue(Of Boolean)(item, "IsAuto"),
                    "按原视频分辨率选择 V1；≥48 fps 优先 HFR；同档没有 V1 时回退 V0 NEG。",
                    If(descriptions.Contains(model), CStr(descriptions(model)), ""))
                tips.GetType().GetMethod("Add", {GetType(String), GetType(String)}).Invoke(tips, {title, description})
            Next
        End Sub
    End Class
End Namespace
