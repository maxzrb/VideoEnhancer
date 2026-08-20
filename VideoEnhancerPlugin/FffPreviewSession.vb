Imports System
Imports System.IO
Imports System.Runtime.InteropServices

Namespace videoenhancer

    ''' <summary>
    ''' FFF.Player / 3FP API 11 的轻量预览桥。只暴露对比窗口需要的打开、播放、暂停、
    ''' 精确拖动与快照；渲染仍完全由最新版 FFF.Native 的 D3D11 内核完成。
    ''' </summary>
    Friend NotInheritable Class FffPreviewSession
        Implements IDisposable

        Friend Enum SessionState As UInteger
            Idle = 0
            Opening = 1
            Ready = 2
            Playing = 3
            Paused = 4
            Ended = 5
            Failed = 6
            Closed = 7
        End Enum

        <StructLayout(LayoutKind.Sequential)>
        Private Structure NativeConfiguration
            Public Size As UInteger
            Public Version As UInteger
            Public OutputWindow As IntPtr
            Public DecodeMode As UInteger
            Public ColorMode As UInteger
            Public SdrPeakNits As Single
            Public HdrPeakNits As Single
            Public SdrPaperWhiteNits As Single
            Public AudioEndpointUtf8 As IntPtr
            Public EventCallback As IntPtr
            Public EventContext As IntPtr
            Public VideoScalingQuality As UInteger
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Private Structure NativeSnapshot
            Public Size As UInteger
            Public Version As UInteger
            Public State As UInteger
            Public DecodeMode As UInteger
            Public RequestedColorMode As UInteger
            Public ActualColorMode As UInteger
            Public Position100ns As Long
            Public Duration100ns As Long
            Public FrameIndex As Long
            Public FramePts As Long
            Public FrameTimeBaseNumerator As Integer
            Public FrameTimeBaseDenominator As Integer
            Public SelectedVideoStream As Integer
            Public SelectedAudioStream As Integer
            Public VideoWidth As UInteger
            Public VideoHeight As UInteger
            Public IsHdrSource As UInteger
            Public IsExternalAudio As UInteger
            Public ExternalAudioOffset100ns As Long
            Public DecodedVideoFrames As ULong
            Public PresentedVideoFrames As ULong
            Public DroppedVideoFrames As ULong
            Public QueuedVideoFrames As UInteger
            Public SourcePeakNits As UInteger
            Public DecodedAudioFrames As ULong
            Public AudioPosition100ns As Long
            Public BufferedAudio100ns As Long
            Public AudioUnderruns As ULong
            Public AudioTimestampJitterFrames As ULong
            Public AudioDiscontinuities As ULong
            Public AudioInsertedSilenceFrames As ULong
            Public AudioDroppedOverlapFrames As ULong
            Public CoalescedVideoFrames As ULong
            Public AudioRejectedFrames As ULong
            Public SwapChainPresents As ULong
            Public PresentWait100ns As ULong
            Public DeviceLockWait100ns As ULong
            Public HardwareTransfer100ns As ULong
            Public SoftwareConvert100ns As ULong
            Public VideoBitRate As ULong
            Public AudioBitRate As ULong
            Public VideoOutputBitDepth As UInteger
            Public VideoScalingMode As UInteger
            Public TimelineGeneration As ULong
            Public HdrFormat As UInteger
            Public CompatibleHdrFormats As UInteger
            Public HdrProcessingPath As UInteger
            Public DolbyVisionProfile As UInteger
            Public DolbyVisionLevel As UInteger
            Public HasDolbyVisionRpu As UInteger
            Public HasDolbyVisionEnhancementLayer As UInteger
            Public DolbyVisionEnhancementLayer As UInteger
            Public DynamicHdrMetadataActive As UInteger
            Public HdrFallbackActive As UInteger
            Public DisplayMinLuminanceMilliNits As UInteger
            Public DisplayPeakNits As UInteger
            Public DisplayFullFramePeakNits As UInteger
            Public EffectiveTargetPeakNits As UInteger
        End Structure

        Friend Structure Snapshot
            Public State As SessionState
            Public Position As TimeSpan
            Public Duration As TimeSpan
            Public VideoSize As Drawing.Size
        End Structure

        Private Const ApiVersion As UInteger = 11UI
        Private Shared ReadOnly NativeApi As NativeApiTable = NativeApiTable.Load()
        Private _handle As IntPtr
        Private _disposed As Boolean

        Friend Sub New(outputWindow As IntPtr)
            If outputWindow = IntPtr.Zero Then Throw New ArgumentException("预览输出窗口尚未创建。", NameOf(outputWindow))
            Dim actualVersion = NativeApi.GetApiVersion()
            If actualVersion <> ApiVersion Then
                Throw New InvalidOperationException("FFF.Native API 版本不兼容：需要 11，实际 " & actualVersion.ToString() & "。")
            End If
            ' 对比窗口只需要小尺寸预览；Balanced 可显著降低 2160p 四路缩放开销。
            ' 最终生成仍由 ffmpeg 使用用户选择的 lanczos/bicubic 算法，不影响输出质量。
            Dim config As New NativeConfiguration With {
                .Size = CUInt(Marshal.SizeOf(Of NativeConfiguration)()),
                .Version = ApiVersion,
                .OutputWindow = outputWindow,
                .DecodeMode = 2UI,
                .ColorMode = 0UI,
                .SdrPeakNits = 100.0F,
                .HdrPeakNits = 0.0F,
                .SdrPaperWhiteNits = 203.0F,
                .AudioEndpointUtf8 = IntPtr.Zero,
                .EventCallback = IntPtr.Zero,
                .EventContext = IntPtr.Zero,
                .VideoScalingQuality = 0UI
            }
            CheckResult(NativeApi.Create(config, _handle), "创建 FFF.Player 预览会话")
        End Sub

        Friend Sub Open(path As String)
            If String.IsNullOrWhiteSpace(path) Then Throw New ArgumentException("视频路径为空。", NameOf(path))
            Dim utf8 = Marshal.StringToCoTaskMemUTF8(path)
            Try
                CheckResult(NativeApi.Open(Handle, utf8), "打开视频")
            Finally
                Marshal.FreeCoTaskMem(utf8)
            End Try
        End Sub

        Friend Sub Play()
            CheckResult(NativeApi.Play(Handle), "播放")
        End Sub

        Friend Sub Pause()
            CheckResult(NativeApi.Pause(Handle), "暂停")
        End Sub

        Friend Sub DiscardAudio()
            CheckResult(NativeApi.DiscardAudioOutput(Handle), "关闭预览音频")
        End Sub

        Friend Sub MuteAudio()
            ' 音量置零但不关闭音频路径：WASAPI 时钟仍然节流视频，
            ' 避免多路比对时从会话按解码速度瞬间跑到结尾。
            CheckResult(NativeApi.SetVolume(Handle, 0.0001F, 0UI), "静音预览音频")
        End Sub

        Friend Sub Seek(position As TimeSpan)
            Dim ticks = Math.Max(0L, position.Ticks)
            CheckResult(NativeApi.Seek(Handle, ticks), "拖动预览")
        End Sub

        Friend Sub RebindOutput(outputWindow As IntPtr)
            If outputWindow <> IntPtr.Zero Then CheckResult(NativeApi.SetOutputWindow(Handle, outputWindow), "重绑预览窗口")
        End Sub

        Friend Function ReadSnapshot() As Snapshot
            ' API 11 保持快照结构版本 8；原生端会拒绝把播放器 API 版本写到这里。
            Dim nativeValue As New NativeSnapshot With {
                .Size = CUInt(Marshal.SizeOf(Of NativeSnapshot)()),
                .Version = 8UI}
            CheckResult(NativeApi.GetSnapshot(Handle, nativeValue), "读取播放状态")
            Return New Snapshot With {
                .State = CType(nativeValue.State, SessionState),
                .Position = TimeSpan.FromTicks(Math.Max(0L, nativeValue.Position100ns)),
                .Duration = TimeSpan.FromTicks(Math.Max(0L, nativeValue.Duration100ns)),
                .VideoSize = New Drawing.Size(CInt(nativeValue.VideoWidth), CInt(nativeValue.VideoHeight))
            }
        End Function

        Private ReadOnly Property Handle As IntPtr
            Get
                If _disposed OrElse _handle = IntPtr.Zero Then Throw New ObjectDisposedException(NameOf(FffPreviewSession))
                Return _handle
            End Get
        End Property

        Private Shared Sub CheckResult(result As Integer, operation As String)
            If result <> 0 Then Throw New InvalidOperationException(operation & "失败，FFF.Native 返回 " & result.ToString() & "。")
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            If _disposed Then Return
            _disposed = True
            Dim oldHandle = _handle
            _handle = IntPtr.Zero
            If oldHandle <> IntPtr.Zero Then
                Try
                    NativeApi.Destroy(oldHandle)
                Catch
                End Try
            End If
        End Sub

        Private NotInheritable Class NativeApiTable
            <UnmanagedFunctionPointer(CallingConvention.Cdecl)> Private Delegate Function GetApiVersionDelegate() As UInteger
            <UnmanagedFunctionPointer(CallingConvention.Cdecl)> Private Delegate Function CreateDelegate(ByRef config As NativeConfiguration, ByRef player As IntPtr) As Integer
            <UnmanagedFunctionPointer(CallingConvention.Cdecl)> Private Delegate Function OpenDelegate(player As IntPtr, pathUtf8 As IntPtr) As Integer
            <UnmanagedFunctionPointer(CallingConvention.Cdecl)> Private Delegate Function PlayerDelegate(player As IntPtr) As Integer
            <UnmanagedFunctionPointer(CallingConvention.Cdecl)> Private Delegate Function SetVolumeDelegate(player As IntPtr, volume As Single, muted As UInteger) As Integer
            <UnmanagedFunctionPointer(CallingConvention.Cdecl)> Private Delegate Function SeekDelegate(player As IntPtr, position100ns As Long) As Integer
            <UnmanagedFunctionPointer(CallingConvention.Cdecl)> Private Delegate Function SetOutputWindowDelegate(player As IntPtr, outputWindow As IntPtr) As Integer
            <UnmanagedFunctionPointer(CallingConvention.Cdecl)> Private Delegate Function GetSnapshotDelegate(player As IntPtr, ByRef snapshot As NativeSnapshot) As Integer
            <UnmanagedFunctionPointer(CallingConvention.Cdecl)> Private Delegate Sub DestroyDelegate(player As IntPtr)

            Private ReadOnly _getApiVersion As GetApiVersionDelegate
            Private ReadOnly _create As CreateDelegate
            Private ReadOnly _open As OpenDelegate
            Private ReadOnly _play As PlayerDelegate
            Private ReadOnly _pause As PlayerDelegate
            Private ReadOnly _discardAudioOutput As PlayerDelegate
            Private ReadOnly _setVolume As SetVolumeDelegate
            Private ReadOnly _seek As SeekDelegate
            Private ReadOnly _setOutputWindow As SetOutputWindowDelegate
            Private ReadOnly _getSnapshot As GetSnapshotDelegate
            Private ReadOnly _destroy As DestroyDelegate

            Private Sub New(handle As IntPtr)
                _getApiVersion = GetDelegate(Of GetApiVersionDelegate)(handle, "FFF3FP_GetApiVersion")
                _create = GetDelegate(Of CreateDelegate)(handle, "FFF3FP_Create")
                _open = GetDelegate(Of OpenDelegate)(handle, "FFF3FP_Open")
                _play = GetDelegate(Of PlayerDelegate)(handle, "FFF3FP_Play")
                _pause = GetDelegate(Of PlayerDelegate)(handle, "FFF3FP_Pause")
                _discardAudioOutput = GetDelegate(Of PlayerDelegate)(handle, "FFF3FP_DiscardAudioOutput")
                _setVolume = GetDelegate(Of SetVolumeDelegate)(handle, "FFF3FP_SetVolume")
                _seek = GetDelegate(Of SeekDelegate)(handle, "FFF3FP_Seek")
                _setOutputWindow = GetDelegate(Of SetOutputWindowDelegate)(handle, "FFF3FP_SetOutputWindow")
                _getSnapshot = GetDelegate(Of GetSnapshotDelegate)(handle, "FFF3FP_GetSnapshot")
                _destroy = GetDelegate(Of DestroyDelegate)(handle, "FFF3FP_Destroy")
            End Sub

            Friend Shared Function Load() As NativeApiTable
                Dim nativePath = EmbeddedFffNativePayload.EnsureExtracted()
                Dim handle = System.Runtime.InteropServices.NativeLibrary.Load(nativePath)
                Return New NativeApiTable(handle)
            End Function

            Private Shared Function GetDelegate(Of T As Class)(handle As IntPtr, exportName As String) As T
                Dim address = System.Runtime.InteropServices.NativeLibrary.GetExport(handle, exportName)
                Return DirectCast(CObj(Marshal.GetDelegateForFunctionPointer(address, GetType(T))), T)
            End Function

            Friend Function GetApiVersion() As UInteger
                Return _getApiVersion()
            End Function
            Friend Function Create(ByRef c As NativeConfiguration, ByRef p As IntPtr) As Integer
                Return _create(c, p)
            End Function
            Friend Function Open(p As IntPtr, path As IntPtr) As Integer
                Return _open(p, path)
            End Function
            Friend Function Play(p As IntPtr) As Integer
                Return _play(p)
            End Function
            Friend Function Pause(p As IntPtr) As Integer
                Return _pause(p)
            End Function
            Friend Function DiscardAudioOutput(p As IntPtr) As Integer
                Return _discardAudioOutput(p)
            End Function
            Friend Function SetVolume(p As IntPtr, v As Single, m As UInteger) As Integer
                Return _setVolume(p, v, m)
            End Function
            Friend Function Seek(p As IntPtr, t As Long) As Integer
                Return _seek(p, t)
            End Function
            Friend Function SetOutputWindow(p As IntPtr, w As IntPtr) As Integer
                Return _setOutputWindow(p, w)
            End Function
            Friend Function GetSnapshot(p As IntPtr, ByRef s As NativeSnapshot) As Integer
                Return _getSnapshot(p, s)
            End Function
            Friend Sub Destroy(p As IntPtr)
                _destroy(p)
            End Sub
        End Class
    End Class

End Namespace
