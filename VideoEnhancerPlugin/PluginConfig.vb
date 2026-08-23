Imports System
Imports System.IO
Imports System.Text.Json

Namespace videoenhancer

    ''' <summary>插件配置，持久化到 %LocalAppData%\FFmpegFreeUI\videoenhancer.plugin.json。</summary>
    Public Class PluginConfig

        Public Property ExePath As String = ""
        Public Property Model As String = ""
        Public Property Enabled As Boolean = False
        ''' <summary>超分开关：是否将"加入编码队列"hook 到 videoenhancer.exe 中转。</summary>
        Public Property UpscaleEnabled As Boolean = True
        ''' <summary>补帧开关：启用 RIFE、GIMM-VFI 或 GMFSS 补帧，可与超分组合。</summary>
        Public Property InterpEnabled As Boolean = False
        ''' <summary>补帧模型：优先使用 models\Frame-Interpolation 下的架构相对路径；旧 models\RIFE 继续兼容。</summary>
        Public Property InterpModel As String = ""
        ''' <summary>补帧倍率（RIFE --interpolate_factor，默认 2；须为大于 1 的数字）。</summary>
        Public Property InterpFactor As Double = 2.0
        ''' <summary>RIFE 动态光流尺度；仅 CUDA/PyTorch 有效，TensorRT 由 RVE 自动禁用。</summary>
        Public Property InterpDynamicScaledOpticalFlow As Boolean = False
        ''' <summary>RIFE 转场检测阈值；数值越低越容易判定为转场。</summary>
        Public Property SceneDetectThreshold As Double = 4.0
        ''' <summary>超分分块边长；0 表示使用 RVE 默认处理，不按显存自动试探。</summary>
        Public Property UpscaleTileSize As Integer = 0
        ''' <summary>超分推理后端：ncnn、cuda、tensorrt、onnx 或 flashvsr。</summary>
        Public Property Backend As String = "ncnn"
        ''' <summary>补帧后端：ncnn、cuda（PyTorch 权重）或 tensorrt（RIFE 权重自动构建 Engine）。</summary>
        Public Property InterpBackend As String = "ncnn"
        ''' <summary>组合处理顺序：upscale-first（画质优先，默认）或 interp-first（速度/算力优先）。</summary>
        Public Property ProcessOrder As String = "upscale-first"
        Public Property ImageOutput As String = ""
        Public Property ImageOutputOriginal As Boolean = False
        Public Property ImagePng As Boolean = True
        Public Property ImageSuffix As String = "timestamp"
        ''' <summary>插件页面首次加载后是否在后台检查稳定版更新。</summary>
        Public Property AutoCheckUpdates As Boolean = True

        Private Shared Function GetConfigDir() As String
            ' 支持环境变量覆盖（测试/便携部署用），默认 %LocalAppData%\FFmpegFreeUI
            Dim overrideDir = Environment.GetEnvironmentVariable("VIDEOENHANCER_CONFIG_DIR")
            If Not String.IsNullOrWhiteSpace(overrideDir) Then
                Return overrideDir
            End If
            Return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FFmpegFreeUI")
        End Function

        Private Shared ReadOnly ConfigDir As String = GetConfigDir()
        Private Shared ReadOnly ConfigPath As String = Path.Combine(ConfigDir, "videoenhancer.plugin.json")

        Public Shared Function Load() As PluginConfig
            Dim cfg As PluginConfig = Nothing
            Try
                If File.Exists(ConfigPath) Then
                    cfg = JsonSerializer.Deserialize(Of PluginConfig)(File.ReadAllText(ConfigPath))
                End If
            Catch
                ' 配置损坏时回退到默认
            End Try
            If cfg Is Nothing Then cfg = New PluginConfig()
            Dim detected = ResolveInstalledExePath(cfg.ExePath)
            If Not String.Equals(cfg.ExePath, detected, StringComparison.OrdinalIgnoreCase) Then
                cfg.ExePath = detected
                If Not String.IsNullOrWhiteSpace(detected) Then cfg.Save()
            End If
            Return cfg
        End Function

        ''' <summary>
        ''' 安装程序会把自身路径写入插件配置；配置丢失或路径失效时，再从 3FUI 与插件目录自动发现。
        ''' </summary>
        Public Shared Function ResolveInstalledExePath(Optional configuredPath As String = "") As String
            If Not String.IsNullOrWhiteSpace(configuredPath) AndAlso File.Exists(configuredPath) Then
                Return Path.GetFullPath(configuredPath)
            End If
            Dim candidates As New Collections.Generic.List(Of String)()
            Try
                candidates.Add(Path.Combine(AppContext.BaseDirectory, "videoenhancer.exe"))
            Catch
            End Try
            Try
                Dim assemblyDir = Path.GetDirectoryName(GetType(PluginConfig).Assembly.Location)
                If Not String.IsNullOrWhiteSpace(assemblyDir) Then
                    candidates.Add(Path.Combine(assemblyDir, "videoenhancer.exe"))
                    Dim hostDir = Directory.GetParent(assemblyDir)
                    If hostDir IsNot Nothing Then candidates.Add(Path.Combine(hostDir.FullName, "videoenhancer.exe"))
                End If
            Catch
            End Try
            Try
                Dim processPath = Environment.ProcessPath
                If Not String.IsNullOrWhiteSpace(processPath) Then
                    candidates.Add(Path.Combine(Path.GetDirectoryName(processPath), "videoenhancer.exe"))
                End If
            Catch
            End Try
            Try
                candidates.Add(Path.Combine(Environment.CurrentDirectory, "videoenhancer.exe"))
            Catch
            End Try
            For Each candidate In candidates
                Try
                    If File.Exists(candidate) Then Return Path.GetFullPath(candidate)
                Catch
                End Try
            Next
            Return ""
        End Function

        Public Sub Save()
            Try
                Directory.CreateDirectory(ConfigDir)
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Me, New JsonSerializerOptions With {.WriteIndented = True}))
            Catch
            End Try
        End Sub

    End Class

End Namespace
