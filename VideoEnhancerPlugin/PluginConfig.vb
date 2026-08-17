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
        ''' <summary>补帧开关：启用 RIFE 补帧（与超分可同时开启）。</summary>
        Public Property InterpEnabled As Boolean = False
        ''' <summary>补帧模型（models\RIFE 下的子文件夹名，如 rife-v4.25）。</summary>
        Public Property InterpModel As String = ""

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
