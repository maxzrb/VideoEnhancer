using VideoEnhancer;

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"预期 {expected}，实际 {actual}");
}

Equal(0, OutputScale.Parse(""));
Equal(3, OutputScale.Parse("3"));
Equal(8, OutputScale.Parse("8"));
foreach (var value in new[] { "0", "9", "16", "17", "2.5", "wrong" })
{
    try { OutputScale.Parse(value); throw new Exception("应拒绝无效倍率"); }
    catch (ArgumentException) { }
}
Equal("-c:v ffv1", OutputScale.Encoder("-c:v ffv1", 96, 64, 0));
Equal("-c:v ffv1 -vf scale=288:192:flags=lanczos", OutputScale.Encoder("-c:v ffv1", 96, 64, 3));
Equal("-c:v ffv1 -vf scale=288:192:flags=lanczos", OutputScale.Encoder("-s 1920x1080 -c:v ffv1", 96, 64, 3));
Equal("-vf crop=64:48:0:0,scale=288:192:flags=lanczos -c:v ffv1",
    OutputScale.Encoder("-vf crop=64:48:0:0 -c:v ffv1", 96, 64, 3));
Equal("-filter:v:0 \"eq=contrast=1.1,scale=288:192:flags=lanczos\"",
    OutputScale.Encoder("-filter:v:0 \"eq=contrast=1.1\"", 96, 64, 3));
foreach (var architecture in new[] { "ESRGAN", "RRDBNet", "ESRGAN-Lite" })
    Equal("ESRGAN / RRDB", ModelArchitectureGroups.Get(architecture));
foreach (var architecture in new[] { "Compact", "RealESRGAN Compact", "realesr-compact", "SRVGGNetCompact", "ESRGAN-Refiner" })
    Equal("Compact", ModelArchitectureGroups.Get(architecture));
Equal("其他模型", ModelArchitectureGroups.Get("ONNX"));
Equal("SwinIR", ModelArchitectureGroups.Get("SwinIR"));
var options = CliArgumentParser.Parse(["-scale", "4", "-output-scale", "3"]);
Equal("4", options.ScaleOverride);
Equal("3", options.OutputScale);
Equal(true, options.HasScaleOverride);
// 数字第 0 路已明确指定视频编码器时，不能追加 SVT；仅指定音频时仍须兜底视频。
foreach (var option in new[] { "-c:0", "-codec:0", "-c:v:0", "-codec:v:0", "-vcodec" })
{
    Equal(true, VideoEncodingDefaults.HasVideoEncoder([option, "ffv1", "-c:a", "copy"]));
    Equal(false, VideoEncodingDefaults.RequiresSvtY412([option, "ffv1", "-pix_fmt:v", "yuv444p12le"]));
    Equal(true, VideoEncodingDefaults.RequiresSvtY412([option, "libsvtav1", "-pix_fmt:v", "yuv444p12le"]));
}
Equal(false, VideoEncodingDefaults.HasVideoEncoder(["-c:1", "aac"]));
Equal(false, VideoEncodingDefaults.HasVideoEncoder(["-codec:1", "copy"]));
Equal(false, VideoEncodingDefaults.HasVideoEncoder(["-c:a", "copy"]));
Equal(false, VideoEncodingDefaults.HasVideoEncoder([]));
Equal(false, VideoEncodingDefaults.RequiresSvtY412(["-c:0", "libsvtav1", "-c:v", "ffv1", "-pix_fmt:v", "yuv444p12le"]));
Console.WriteLine("VIDEO_ENCODING_DEFAULTS_TESTS_PASS|numeric-video|audio-only|last-option");
Console.WriteLine("模型分组、输出滤镜与参数解析验证通过");
