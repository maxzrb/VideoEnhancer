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
        ''' <summary>补帧开关：启用 RIFE 补帧（与超分互斥，不能同时开启）。</summary>
        Public Property InterpEnabled As Boolean = False
        ''' <summary>补帧模型：ncnn 为 models\RIFE 下的子文件夹名（如 rife-v4.25），cuda 为 .pth 文件名（如 rife46）。</summary>
        Public Property InterpModel As String = ""
        ''' <summary>补帧倍率（RIFE --interpolate_factor，默认 2；须为大于 1 的数字）。</summary>
        Public Property InterpFactor As Double = 2.0
        ''' <summary>推理后端：ncnn、cuda、tensorrt 或 onnx。</summary>
        Public Property Backend As String = "ncnn"
        Public Property ImageOutput As String = ""
        Public Property ImageOutputOriginal As Boolean = False
        Public Property ImagePng As Boolean = True
        Public Property ImageSuffix As String = "timestamp"

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
            Try
                If File.Exists(ConfigPath) Then
                    Dim cfg = JsonSerializer.Deserialize(Of PluginConfig)(File.ReadAllText(ConfigPath))
                    If cfg IsNot Nothing Then
                        Return cfg
                    End If
                End If
            Catch
                ' 配置损坏时回退到默认
            End Try
            Return New PluginConfig()
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
