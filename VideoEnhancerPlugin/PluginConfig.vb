Imports System
Imports System.IO
Imports System.Text
Imports System.Text.Json
Imports System.Text.Json.Serialization

Namespace videoenhancer

    ''' <summary>插件配置，持久化到 Plugin\videoenhancer\videoenhancer.plugin.json。</summary>
    Public Class PluginConfig

        ''' <summary>配置保存完成后通知界面订阅者；事件不参与 JSON 持久化。</summary>
        Public Event Saved As EventHandler

        ''' <summary>处理程序路径由插件 DLL 所在目录唯一确定，不再允许配置外部 EXE。</summary>
        <JsonIgnore>
        Public ReadOnly Property ExePath As String
            Get
                Return ResolveInstalledExePath()
            End Get
        End Property
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
        Private _outputScale As Integer = 0
        ''' <summary>0 表示原生；旧配置超过8倍时按8倍载入，两页及队列保持一致。</summary>
        Public Property OutputScale As Integer
            Get
                Return _outputScale
            End Get
            Set(value As Integer)
                _outputScale = Math.Max(0, Math.Min(8, value))
            End Set
        End Property
        ''' <summary>超分优先使用半精度；关闭时对支持精度控制的后端强制 FP32。</summary>
        Public Property UpscaleHalfPrecision As Boolean = True
        ''' <summary>超分推理后端：ncnn、cuda、tensorrt、onnx 或 flashvsr。</summary>
        Public Property Backend As String = "ncnn"
        ''' <summary>补帧后端：ncnn、cuda（PyTorch 权重）或 tensorrt（RIFE 权重自动构建 Engine）。</summary>
        Public Property InterpBackend As String = "ncnn"
        ''' <summary>补帧优先使用半精度；关闭时对 CUDA/TensorRT 强制 FP32。</summary>
        Public Property InterpHalfPrecision As Boolean = True
        ''' <summary>组合处理顺序：upscale-first（画质优先，默认）或 interp-first（速度/算力优先）。</summary>
        Public Property ProcessOrder As String = "upscale-first"
        ''' <summary>对视频启用 NVIDIA RTX Video HDR 映射。</summary>
        Public Property RtxHdrEnabled As Boolean = False
        ''' <summary>RTX HDR 对比度（0-200，默认 100）。</summary>
        Public Property RtxHdrContrast As Integer = 100
        ''' <summary>RTX HDR 饱和度（0-200，默认 100）。</summary>
        Public Property RtxHdrSaturation As Integer = 100
        ''' <summary>RTX HDR 中灰度（10-100，默认 44）。</summary>
        Public Property RtxHdrMiddleGray As Integer = 44
        ''' <summary>RTX HDR 最大亮度（400-2000 nit，默认 1000）。</summary>
        Public Property RtxHdrMaxLuminance As Integer = 1000
        ''' <summary>RTX VSR 输出规格：倍率或按横竖方向映射的目标边。</summary>
        Public Property RtxTarget As String = "2x"
        ''' <summary>RTX VSR 质量等级（1-4）。</summary>
        Public Property RtxQuality As Integer = 3
        ''' <summary>资源管理器“超分辨率”级联菜单中启用的模型。</summary>
        Public Property ShellModels As New Collections.Generic.List(Of ShellUpscaleModel)()
        ''' <summary>按完整视频路径保存的分段超分配置；默认秒级关键帧断点，兼容精确帧。</summary>
        Public Property SegmentedVideos As New Collections.Generic.List(Of SegmentedVideoConfig)()
        Public Property ImageOutput As String = ""
        Public Property ImageOutputOriginal As Boolean = False
        Public Property ImagePng As Boolean = True
        Public Property ImageSuffix As String = "timestamp"
        ''' <summary>插件页面首次加载后是否在后台检查稳定版更新。</summary>
        Public Property AutoCheckUpdates As Boolean = True

        Public Shared ReadOnly Property PluginRoot As String
            Get
                Return PortableRuntime.PluginRoot
            End Get
        End Property

        Public Shared ReadOnly Property ApplicationRoot As String
            Get
                Return PortableRuntime.ApplicationRoot
            End Get
        End Property

        Public Shared ReadOnly Property ConfigPath As String
            Get
                Return Path.Combine(ApplicationRoot, "videoenhancer.plugin.json")
            End Get
        End Property

        Public Shared Function Load() As PluginConfig
            Dim cfg As PluginConfig = Nothing
            Dim loadedLegacyConfig As Boolean = False
            Try
                Dim sourcePath = ConfigPath
                If Not File.Exists(sourcePath) Then
                    Dim localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                    If Not String.IsNullOrWhiteSpace(localAppData) Then
                        Dim legacyPath = Path.Combine(localAppData, "FFmpegFreeUI", "videoenhancer.plugin.json")
                        If File.Exists(legacyPath) Then
                            sourcePath = legacyPath
                            loadedLegacyConfig = True
                        End If
                    End If
                End If
                If File.Exists(sourcePath) Then
                    cfg = JsonSerializer.Deserialize(Of PluginConfig)(File.ReadAllText(sourcePath))
                End If
            Catch
                ' 配置损坏时回退到默认
            End Try
            If cfg Is Nothing Then cfg = New PluginConfig()
            Dim configChanged = cfg.NormalizeRtxHdrParameters()
            If configChanged OrElse loadedLegacyConfig Then cfg.Save()
            Return cfg
        End Function

        ''' <summary>
        ''' 将旧配置缺少的 HDR 字段保留为属性默认值，并把越界值钳制到 sidecar 合法范围。
        ''' 返回是否发生修正，供 Load() 决定是否回写配置文件。
        ''' </summary>
        Public Function NormalizeRtxHdrParameters() As Boolean
            Dim changed As Boolean = False
            Dim contrast = ClampRtxHdrContrast(RtxHdrContrast)
            If contrast <> RtxHdrContrast Then RtxHdrContrast = contrast : changed = True
            Dim saturation = ClampRtxHdrSaturation(RtxHdrSaturation)
            If saturation <> RtxHdrSaturation Then RtxHdrSaturation = saturation : changed = True
            Dim middleGray = ClampRtxHdrMiddleGray(RtxHdrMiddleGray)
            If middleGray <> RtxHdrMiddleGray Then RtxHdrMiddleGray = middleGray : changed = True
            Dim maxLuminance = ClampRtxHdrMaxLuminance(RtxHdrMaxLuminance)
            If maxLuminance <> RtxHdrMaxLuminance Then RtxHdrMaxLuminance = maxLuminance : changed = True
            Return changed
        End Function

        Public Shared Function ClampRtxHdrContrast(value As Integer) As Integer
            Return Math.Max(0, Math.Min(200, value))
        End Function

        Public Shared Function ClampRtxHdrSaturation(value As Integer) As Integer
            Return Math.Max(0, Math.Min(200, value))
        End Function

        Public Shared Function ClampRtxHdrMiddleGray(value As Integer) As Integer
            Return Math.Max(10, Math.Min(100, value))
        End Function

        Public Shared Function ClampRtxHdrMaxLuminance(value As Integer) As Integer
            Return Math.Max(400, Math.Min(2000, value))
        End Function

        ''' <summary>处理程序固定为插件 DLL 同目录下的 videoenhancer\videoenhancer.exe。</summary>
        Public Shared Function ResolveInstalledExePath() As String
            Return Path.Combine(ApplicationRoot, "videoenhancer.exe")
        End Function

        Public Sub Save()
            TrySave()
        End Sub

        ''' <summary>返回配置是否已写入磁盘，供开关在保存失败时恢复真实状态。</summary>
        Public Function TrySave() As Boolean
            Dim temporary = ConfigPath & ".new"
            Dim saved As Boolean = False
            Try
                Directory.CreateDirectory(ApplicationRoot)
                File.WriteAllText(temporary,
                    JsonSerializer.Serialize(Me, New JsonSerializerOptions With {.WriteIndented = True}),
                    New UTF8Encoding(False))
                File.Move(temporary, ConfigPath, True)
                saved = True
                RaiseEvent Saved(Me, EventArgs.Empty)
            Catch
            Finally
                Try
                    If File.Exists(temporary) Then File.Delete(temporary)
                Catch
                End Try
            End Try
            Return saved
        End Function

    End Class

    Public Class ShellUpscaleModel
        Public Property Backend As String = "ncnn"
        Public Property Model As String = ""
        Public Property DisplayName As String = ""
    End Class

    Public Class SegmentedVideoConfig
        Public Property Path As String = ""
        Public Property FrameCount As Long
        Public Property DurationSeconds As Double
        Public Property SourceWidth As Integer
        Public Property SourceHeight As Integer
        Public Property BoundaryMode As String = ""
        Public Property AllowMixedModelBackends As Boolean = False
        Public Property Enabled As Boolean
        Public Property Segments As New Collections.Generic.List(Of SegmentedUpscaleRange)()
    End Class

    Public Class SegmentedUpscaleRange
        Public Property Start As Long
        Public Property [End] As Long
        Public Property StartSeconds As Double
        Public Property EndSeconds As Double
        Public Property Backend As String = ""
        Public Property Model As String = ""
        Public Property DisplayName As String = ""
        Public Property Scale As Integer
        Public Property TargetWidth As Integer
        Public Property TargetHeight As Integer
    End Class

End Namespace
