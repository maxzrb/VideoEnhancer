Imports System
Imports System.Diagnostics
Imports System.IO
Imports System.Linq
Imports System.Net.Http
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.Json
Imports System.Threading.Tasks

Namespace videoenhancer

    Public NotInheritable Class UpdateManifest
        Public Property SchemaVersion As Integer
        Public Property Channel As String = ""
        Public Property Version As String = ""
        Public Property UpstreamBase As String = ""
        Public Property PublishedAt As String = ""
        Public Property Package As UpdatePackageInfo
        Public Property Notes As String = ""
    End Class

    Public NotInheritable Class UpdatePackageInfo
        Public Property Path As String = ""
        Public Property Size As Long
        Public Property Sha256 As String = ""
    End Class

    ''' <summary>从 ModelScope 读取独立发行清单、下载更新包并启动临时更新器。</summary>
    Public NotInheritable Class PluginUpdater

        Private Const DefaultDataset As String = "AerithDream/VideoEnhancer-Releases"
        Private Shared ReadOnly JsonOptions As New JsonSerializerOptions With {
            .PropertyNameCaseInsensitive = True
        }

        Private Sub New()
        End Sub

        Public Shared Async Function FetchStableManifestAsync() As Task(Of UpdateManifest)
            Dim url = BuildResolveUrl("stable.json")
            Using client = CreateClient(TimeSpan.FromSeconds(30))
                Dim json = Await client.GetStringAsync(url)
                Dim manifest = JsonSerializer.Deserialize(Of UpdateManifest)(json, JsonOptions)
                If manifest Is Nothing OrElse manifest.SchemaVersion <> 1 OrElse
                    String.IsNullOrWhiteSpace(manifest.Version) OrElse manifest.Package Is Nothing Then
                    Throw New InvalidDataException("ModelScope 更新清单格式无效")
                End If
                Dim parsed As Version = Nothing
                If Not Version.TryParse(manifest.Version, parsed) Then
                    Throw New InvalidDataException("更新清单版本号不是有效 SemVer：" & manifest.Version)
                End If
                ValidateRelativePath(manifest.Package.Path)
                If manifest.Package.Size <= 0 OrElse manifest.Package.Sha256.Length <> 64 Then
                    Throw New InvalidDataException("更新清单缺少有效的文件大小或 SHA-256")
                End If
                Return manifest
            End Using
        End Function

        Public Shared Function HasUpdate(manifest As UpdateManifest) As Boolean
            Dim currentVersion As Version = Nothing
            Dim remoteVersion As Version = Nothing
            If Not Version.TryParse(PluginVersion.Current, currentVersion) OrElse
                Not Version.TryParse(manifest.Version, remoteVersion) Then Return False
            Return remoteVersion > currentVersion
        End Function

        Public Shared Async Function DownloadPackageAsync(
                manifest As UpdateManifest,
                progress As Action(Of Integer)) As Task(Of String)
            ValidateRelativePath(manifest.Package.Path)
            Dim updateDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FFmpegFreeUI", "VideoEnhancer", "updates", manifest.Version)
            Directory.CreateDirectory(updateDirectory)
            Dim fileName = Path.GetFileName(manifest.Package.Path.Replace("/"c, Path.DirectorySeparatorChar))
            Dim destination = Path.Combine(updateDirectory, fileName)
            Dim temporary = destination & ".download"
            Try
                Using client = CreateClient(TimeSpan.FromMinutes(30))
                    Using response = Await client.GetAsync(
                            BuildResolveUrl(manifest.Package.Path), HttpCompletionOption.ResponseHeadersRead)
                        response.EnsureSuccessStatusCode()
                        Using source = Await response.Content.ReadAsStreamAsync()
                            Using output As New FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)
                                Dim buffer(1024 * 128 - 1) As Byte
                                Dim total As Long = 0
                                Dim lastPercent = -1
                                Do
                                    Dim count = Await source.ReadAsync(buffer.AsMemory(0, buffer.Length))
                                    If count <= 0 Then Exit Do
                                    Await output.WriteAsync(buffer.AsMemory(0, count))
                                    total += count
                                    Dim percent = CInt(Math.Min(100, total * 100L \ manifest.Package.Size))
                                    If percent <> lastPercent Then
                                        lastPercent = percent
                                        If progress IsNot Nothing Then progress(percent)
                                    End If
                                Loop
                            End Using
                        End Using
                    End Using
                End Using

                Dim info As New FileInfo(temporary)
                If info.Length <> manifest.Package.Size Then
                    Throw New InvalidDataException("更新包大小校验失败")
                End If
                Using stream = File.OpenRead(temporary)
                    Dim actual = Convert.ToHexString(SHA256.HashData(stream))
                    If Not actual.Equals(manifest.Package.Sha256, StringComparison.OrdinalIgnoreCase) Then
                        Throw New InvalidDataException("更新包 SHA-256 校验失败")
                    End If
                End Using
                File.Move(temporary, destination, True)
                Return destination
            Catch
                Try
                    If File.Exists(temporary) Then File.Delete(temporary)
                Catch
                End Try
                Throw
            End Try
        End Function

        Public Shared Sub StartUpdate(packagePath As String, installedExe As String,
                                      targetDirectory As String, waitPid As Integer,
                                      restartExe As String)
            Dim updaterDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FFmpegFreeUI", "VideoEnhancer", "updater", Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(updaterDirectory)
            Dim updaterExe = Path.Combine(updaterDirectory, "videoenhancer-updater.exe")
            File.Copy(installedExe, updaterExe, True)
            Dim resultPath = GetResultPath()
            Try
                If File.Exists(resultPath) Then File.Delete(resultPath)
            Catch
            End Try

            Dim startInfo As New ProcessStartInfo With {
                .FileName = updaterExe,
                .UseShellExecute = False,
                .CreateNoWindow = True,
                .WorkingDirectory = updaterDirectory
            }
            startInfo.ArgumentList.Add("--apply-update")
            startInfo.ArgumentList.Add("--update-package")
            startInfo.ArgumentList.Add(packagePath)
            startInfo.ArgumentList.Add("--update-target")
            startInfo.ArgumentList.Add(targetDirectory)
            startInfo.ArgumentList.Add("--wait-pid")
            startInfo.ArgumentList.Add(waitPid.ToString(Globalization.CultureInfo.InvariantCulture))
            startInfo.ArgumentList.Add("--restart-exe")
            startInfo.ArgumentList.Add(restartExe)
            startInfo.ArgumentList.Add("--update-result")
            startInfo.ArgumentList.Add(resultPath)
            Process.Start(startInfo)
        End Sub

        Public Shared Function ConsumeUpdateResult() As String
            Dim resultPath = GetResultPath()
            Try
                If Not File.Exists(resultPath) Then Return ""
                Dim value = File.ReadAllText(resultPath, Encoding.UTF8).Trim()
                File.Delete(resultPath)
                Return value
            Catch
                Return ""
            End Try
        End Function

        Private Shared Function CreateClient(timeout As TimeSpan) As HttpClient
            Dim client As New HttpClient With {.Timeout = timeout}
            client.DefaultRequestHeaders.UserAgent.ParseAdd("VideoEnhancer/" & PluginVersion.Current)
            Return client
        End Function

        Private Shared Function BuildResolveUrl(relativePath As String) As String
            ValidateRelativePath(relativePath)
            Dim dataset = Environment.GetEnvironmentVariable("VIDEOENHANCER_UPDATE_DATASET")
            If String.IsNullOrWhiteSpace(dataset) Then dataset = DefaultDataset
            Dim datasetParts = dataset.Trim().Trim("/"c).Split("/"c)
            If datasetParts.Length <> 2 OrElse datasetParts.Any(Function(part) String.IsNullOrWhiteSpace(part)) Then
                Throw New InvalidDataException("VIDEOENHANCER_UPDATE_DATASET 必须是 owner/name")
            End If
            Dim escapedDataset = String.Join("/", datasetParts.Select(Function(part) Uri.EscapeDataString(part)))
            Dim escapedPath = String.Join("/", relativePath.Replace("\"c, "/"c).Split("/"c).
                Select(Function(part) Uri.EscapeDataString(part)))
            Return "https://www.modelscope.cn/datasets/" & escapedDataset & "/resolve/master/" & escapedPath
        End Function

        Private Shared Sub ValidateRelativePath(relativePath As String)
            If String.IsNullOrWhiteSpace(relativePath) Then Throw New InvalidDataException("更新文件路径为空")
            Dim normalized = relativePath.Replace("\"c, "/"c)
            If normalized.StartsWith("/", StringComparison.Ordinal) OrElse
                normalized.Split("/"c).Any(Function(part) part.Length = 0 OrElse part = "." OrElse part = "..") Then
                Throw New InvalidDataException("更新文件路径不安全：" & relativePath)
            End If
        End Sub

        Private Shared Function GetResultPath() As String
            Return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FFmpegFreeUI", "VideoEnhancer", "update-result.txt")
        End Function

    End Class

End Namespace
