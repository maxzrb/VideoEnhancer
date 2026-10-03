Imports System
Imports System.IO

Namespace videoenhancer

    ''' <summary>统一查找 3FUI 与插件目录中的 FFmpeg 工具。</summary>
    Friend NotInheritable Class FfmpegToolResolver

        Private Sub New()
        End Sub

        Friend Shared Function Resolve(fileName As String) As String
            If fileName <> "ffmpeg.exe" AndAlso fileName <> "ffprobe.exe" Then
                Throw New ArgumentException("不支持的 FFmpeg 工具名称", NameOf(fileName))
            End If

            Dim explicitFfmpeg = Environment.GetEnvironmentVariable("VIDEOENHANCER_FFMPEG")
            If Not String.IsNullOrWhiteSpace(explicitFfmpeg) Then
                Dim candidatePath = explicitFfmpeg.Trim().Trim(""""c)
                If fileName = "ffprobe.exe" Then
                    Dim directory = Path.GetDirectoryName(candidatePath)
                    candidatePath = If(String.IsNullOrWhiteSpace(directory), "", Path.Combine(directory, fileName))
                End If
                If candidatePath <> "" AndAlso File.Exists(candidatePath) Then Return Path.GetFullPath(candidatePath)
            End If

            ' 3FUI 可指定独立工作目录；读取宿主当前生效值而非进程当前目录。
            Dim workingDirectory = HostSettings.GetWorkingDirectory()
            If Not String.IsNullOrWhiteSpace(workingDirectory) Then
                Dim configuredTool = Path.Combine(workingDirectory, fileName)
                If File.Exists(configuredTool) Then Return Path.GetFullPath(configuredTool)
            End If

            ' 未在工作目录找到时，继续兼容放在 3FUI 根目录的工具。
            Dim hostRoot = Directory.GetParent(PortableRuntime.PluginRoot)
            If hostRoot IsNot Nothing Then
                Dim hostTool = Path.Combine(hostRoot.FullName, fileName)
                If File.Exists(hostTool) Then Return hostTool
            End If

            Dim currentTool = Path.Combine(Environment.CurrentDirectory, fileName)
            If File.Exists(currentTool) Then Return Path.GetFullPath(currentTool)

            Dim pathValue = Environment.GetEnvironmentVariable("PATH")
            If Not String.IsNullOrWhiteSpace(pathValue) Then
                For Each directory In pathValue.Split(Path.PathSeparator)
                    Dim entry = directory.Trim().Trim(""""c)
                    If entry.Length = 0 Then Continue For
                    Try
                        Dim tool = Path.Combine(entry, fileName)
                        If File.Exists(tool) Then Return Path.GetFullPath(tool)
                    Catch
                        ' 忽略无效的 PATH 条目，继续查找可用工具。
                    End Try
                Next
            End If

            Dim core = PortableRuntime.ApplicationRoot
            For Each tool In {Path.Combine(core, "bin", "ffmpeg", fileName),
                              Path.Combine(core, "bin", fileName),
                              Path.Combine(core, fileName)}
                If File.Exists(tool) Then Return tool
            Next
            Return ""
        End Function

    End Class

End Namespace
