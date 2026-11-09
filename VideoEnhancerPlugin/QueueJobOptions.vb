Namespace videoenhancer
    Friend NotInheritable Class QueueJobOptions
        Friend input As String
        Friend output As String
        Friend model As String
        Friend ffmpegSettings As String
        Friend pauseShm As String = ""
        Friend stopShm As String = ""
        Friend upscaleOn As Boolean = True
        Friend interpModel As String = ""
        Friend interpOn As Boolean = False
        Friend backend As String = "ncnn"
        Friend interpFactor As Double = 2.0
        Friend processOrder As String = "upscale-first"
        Friend interpBackend As String = "cuda"
        Friend dynamicOpticalFlow As Boolean = False
        Friend sceneThreshold As Double = 4.0
        Friend tileSize As Integer = 0
        Friend outputScale As Integer = 0
        Friend upscaleHalfPrecision As Boolean = False
        Friend interpHalfPrecision As Boolean = False
        Friend rtxHdr As Boolean = False
        Friend rtxTarget As String = "2x"
        Friend rtxQuality As Integer = 3
        Friend segmentsBase64 As String = ""
        Friend rtxHdrContrast As Integer = 100
        Friend rtxHdrSaturation As Integer = 100
        Friend rtxHdrMiddleGray As Integer = 44
        Friend rtxHdrMaxLuminance As Integer = 1000
        Friend allowMixedSegmentBackends As Boolean = False
        Friend ffmpegPath As String = ""
        Friend ffprobePath As String = ""
    End Class
End Namespace
