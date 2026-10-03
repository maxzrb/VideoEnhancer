Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.IO
Imports System.Linq
Imports System.Text

Namespace videoenhancer
    Friend NotInheritable Class QueueCommandBuilder
        Friend Shared Function BuildCliArgs(options As QueueJobOptions) As String
            Dim input = options.input
            Dim output = options.output
            Dim model = options.model
            Dim ffmpegSettings = options.ffmpegSettings
            Dim pauseShm = options.pauseShm
            Dim stopShm = options.stopShm
            Dim upscaleOn = options.upscaleOn
            Dim interpModel = options.interpModel
            Dim interpOn = options.interpOn
            Dim backend = options.backend
            Dim interpFactor = options.interpFactor
            Dim processOrder = options.processOrder
            Dim interpBackend = options.interpBackend
            Dim dynamicOpticalFlow = options.dynamicOpticalFlow
            Dim sceneThreshold = options.sceneThreshold
            Dim tileSize = options.tileSize
            Dim upscaleHalfPrecision = options.upscaleHalfPrecision
            Dim interpHalfPrecision = options.interpHalfPrecision
            Dim rtxHdr = options.rtxHdr
            Dim rtxTarget = options.rtxTarget
            Dim rtxQuality = options.rtxQuality
            Dim segmentsBase64 = options.segmentsBase64
            Dim rtxHdrContrast = options.rtxHdrContrast
            Dim rtxHdrSaturation = options.rtxHdrSaturation
            Dim rtxHdrMiddleGray = options.rtxHdrMiddleGray
            Dim rtxHdrMaxLuminance = options.rtxHdrMaxLuminance
            Dim allowMixedSegmentBackends = options.allowMixedSegmentBackends
            Dim ffmpegPath = options.ffmpegPath
            Dim ffprobePath = options.ffprobePath
            Dim sb As New StringBuilder()
            sb.Append("-i ").Append(Arg(input))
            If upscaleOn AndAlso Not String.IsNullOrWhiteSpace(model) Then
                sb.Append(" -modelpath ").Append(Arg(model))
            End If
            If interpOn AndAlso Not String.IsNullOrWhiteSpace(interpModel) Then
                sb.Append(" -interp-model ").Append(Arg(interpModel))
            End If
            If Not upscaleOn Then
                sb.Append(" -no-upscale")
            End If
            ' 这里传入超分后端；CLI 会为补帧安全推导 CUDA 或 NCNN，避免模型格式错配。
            If interpOn OrElse upscaleOn Then
                Dim b = If(String.IsNullOrWhiteSpace(backend), "ncnn", backend.Trim().ToLowerInvariant())
                sb.Append(" -backend ").Append(Arg(b))
            End If
            If interpOn Then
                Dim ib = If(String.IsNullOrWhiteSpace(interpBackend), "ncnn", interpBackend.Trim().ToLowerInvariant())
                If ib <> "ncnn" AndAlso ib <> "cuda" AndAlso ib <> "tensorrt" Then ib = "ncnn"
                sb.Append(" -interp-backend ").Append(Arg(ib))
                Dim f = If(interpFactor <= 1, 2.0, interpFactor)
                sb.Append(" -interp-factor ").Append(f.ToString("0", System.Globalization.CultureInfo.InvariantCulture))
                If dynamicOpticalFlow AndAlso String.Equals(ib, "cuda", StringComparison.OrdinalIgnoreCase) Then
                    sb.Append(" -dynamic-optical-flow")
                End If
                Dim threshold = If(sceneThreshold <= 0, 4.0, sceneThreshold)
                sb.Append(" -scene-threshold ").Append(threshold.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))
                If upscaleOn Then
                    Dim order = If(String.Equals(processOrder, "interp-first", StringComparison.OrdinalIgnoreCase), "interp-first", "upscale-first")
                    sb.Append(" -process-order ").Append(Arg(order))
                End If
            End If
            If upscaleOn AndAlso options.outputScale > 0 AndAlso backend <> "rtxvsr" AndAlso String.IsNullOrWhiteSpace(segmentsBase64) Then
                sb.Append(" -output-scale ").Append(options.outputScale.ToString(System.Globalization.CultureInfo.InvariantCulture))
            End If
            If upscaleOn Then
                sb.Append(" -upscale-precision ").Append(If(upscaleHalfPrecision, "auto", "float32"))
            End If
            If String.Equals(backend, "rtxvsr", StringComparison.OrdinalIgnoreCase) AndAlso upscaleOn Then
                sb.Append(" -rtx-target ").Append(Arg(If(String.IsNullOrWhiteSpace(rtxTarget), "2x", rtxTarget)))
                sb.Append(" -rtx-quality ").Append(Math.Max(1, Math.Min(4, rtxQuality)).ToString(System.Globalization.CultureInfo.InvariantCulture))
            End If
            If rtxHdr Then
                sb.Append(" -rtx-hdr")
                sb.Append(" -rtx-hdr-contrast ").Append(PluginConfig.ClampRtxHdrContrast(rtxHdrContrast).ToString(System.Globalization.CultureInfo.InvariantCulture))
                sb.Append(" -rtx-hdr-saturation ").Append(PluginConfig.ClampRtxHdrSaturation(rtxHdrSaturation).ToString(System.Globalization.CultureInfo.InvariantCulture))
                sb.Append(" -rtx-hdr-middle-gray ").Append(PluginConfig.ClampRtxHdrMiddleGray(rtxHdrMiddleGray).ToString(System.Globalization.CultureInfo.InvariantCulture))
                sb.Append(" -rtx-hdr-max-luminance ").Append(PluginConfig.ClampRtxHdrMaxLuminance(rtxHdrMaxLuminance).ToString(System.Globalization.CultureInfo.InvariantCulture))
            End If
            If Not String.IsNullOrWhiteSpace(segmentsBase64) Then
                sb.Append(" --segments-base64 ").Append(Arg(segmentsBase64))
                If allowMixedSegmentBackends Then sb.Append(" --allow-mixed-segment-backends")
            End If
            If interpOn Then
                sb.Append(" -interp-precision ").Append(If(interpHalfPrecision, "auto", "float32"))
            End If
            Dim tileBackend = String.Equals(backend, "ncnn", StringComparison.OrdinalIgnoreCase) OrElse
                String.Equals(backend, "cuda", StringComparison.OrdinalIgnoreCase) OrElse
                String.Equals(backend, "tensorrt", StringComparison.OrdinalIgnoreCase) OrElse
                String.Equals(backend, "onnx", StringComparison.OrdinalIgnoreCase)
            If upscaleOn AndAlso tileSize > 0 AndAlso tileBackend Then
                sb.Append(" -tile-size ").Append(tileSize.ToString(System.Globalization.CultureInfo.InvariantCulture))
            End If
            sb.Append(" -ffmpeg-settings ").Append(Arg(ffmpegSettings))
            If Not String.IsNullOrWhiteSpace(ffmpegPath) Then sb.Append(" --ffmpeg-path ").Append(Arg(ffmpegPath))
            If Not String.IsNullOrWhiteSpace(ffprobePath) Then sb.Append(" --ffprobe-path ").Append(Arg(ffprobePath))
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
        Friend Shared Function BuildFfmpegSettings(preset As HostPreset, input As String, output As String) As String
            Dim cmd = HostPresetAccess.将预设数据转换为命令行(preset, HostPresetAccess.输入占位符, HostPresetAccess.输出占位符)
            If String.IsNullOrWhiteSpace(cmd) Then
                Return QuotePath(output) & " -y"
            End If

            Dim tokens = CommandLineTokenizer.Tokenize(cmd)

            ' 丢弃输入段：-hide_banner -y … -i "<输入文件>" 之前的所有内容
            Dim start As Integer = -1
            For i As Integer = 0 To tokens.Count - 2
                If tokens(i).Text = "-i" AndAlso tokens(i + 1).Text = HostPresetAccess.输入占位符 Then
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
            If kept.Any(Function(t) t.Text = HostPresetAccess.媒体总时长占位符) Then
                duration = ResolveDuration(input)
            End If
            Dim parts As New List(Of String)
            For Each token In kept
                Dim value = token.Text
                If value = HostPresetAccess.输出占位符 Then
                    parts.Add(QuotePath(output))
                ElseIf value = HostPresetAccess.媒体总时长占位符 Then
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

        Friend Shared Function FallbackOutputPath(input As String, preset As HostPreset) As String
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

        ''' <summary>优先用 3FUI 的 ffprobe 解析媒体总时长（仅当模板含占位符时调用）。</summary>
        Private Shared Function ResolveDuration(input As String) As String
            Try
                Dim ffprobe = FfmpegToolResolver.Resolve("ffprobe.exe")
                If ffprobe = "" Then ffprobe = "ffprobe"
                Dim psi As New ProcessStartInfo With {
                    .FileName = ffprobe,
                    .UseShellExecute = False,
                    .RedirectStandardOutput = True,
                    .RedirectStandardError = True,
                    .CreateNoWindow = True,
                    .StandardOutputEncoding = Encoding.UTF8
                }
                PortableRuntime.ConfigureProcess(psi)
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

    End Class
End Namespace
