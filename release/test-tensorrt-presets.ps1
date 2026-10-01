param([string]$Runtime = '', [string]$Assembly = '')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Runtime) { $Runtime = Join-Path $root 'Artifacts\videoenhancer.exe' }
if (-not $Assembly) { $Assembly = Join-Path $root 'cli\bin\Release\net10.0-windows\win-x64\videoenhancer.dll' }
$program = [Reflection.Assembly]::LoadFrom($Assembly).GetType('VideoEnhancer.Program')
$flags = [Reflection.BindingFlags]'NonPublic,Static'
$parse = $program.GetMethod('TryResolveTensorRtOutputScalePreset', $flags)
foreach ($scale in @(2, 3, 4)) {
    $arguments = [object[]]@("PTH\realesr-animevideov3-${scale}x", '', 0)
    if (-not $parse.Invoke($null, $arguments) -or $arguments[1] -ne 'PTH/realesr-animevideov3' -or $arguments[2] -ne $scale) {
        throw "TensorRT 倍率预设未正确解析：${scale}x"
    }
}
foreach ($request in @('PTH/realesr-animevideov3', 'PTH/realesr-animevideov3-1x', 'PTH/realesr-animevideov3-5x', 'PTH/other-2x')) {
    $arguments = [object[]]@($request, '', 0)
    if ($parse.Invoke($null, $arguments)) { throw "误识别模型预设：$request" }
}

$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
$testRoot = Join-Path $tempBase ('VideoEnhancerTensorRtPresets-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force (Join-Path $testRoot 'models\PTH') | Out-Null
try {
    Copy-Item -LiteralPath $Runtime -Destination (Join-Path $testRoot 'videoenhancer.exe')
    $weight = Join-Path $testRoot 'models\PTH\realesr-animevideov3.pth'
    [IO.File]::WriteAllText($weight, 'catalog discovery only')
    $exe = Join-Path $testRoot 'videoenhancer.exe'
    $trt = & $exe --list-model-catalog --json -backend tensorrt | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $trt.Count -ne 3) { throw 'TensorRT 模型列表未生成三个预设' }
    foreach ($scale in @(2, 3, 4)) {
        $entry = @($trt | Where-Object { $_.id -eq "PTH/realesr-animevideov3-${scale}x" -and $_.scale -eq $scale })
        if ($entry.Count -ne 1) { throw "模型列表缺少 ${scale}x" }
    }
    $cuda = & $exe --list-model-catalog --json -backend cuda | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or @($cuda).Count -ne 1 -or $cuda.scale -ne 4) { throw 'TensorRT 预设误改 CUDA 列表' }

    $runtimeType = $program.GetNestedType('TensorRtRuntime', [Reflection.BindingFlags]'NonPublic')
    $runtimeInfo = [Activator]::CreateInstance($runtimeType, [object[]]@('test GPU', '10', '2'))
    $cache = $program.GetMethod('BuildTensorRtCachePath', $flags)
    $paths = foreach ($scale in @(2, 3, 4)) {
        $cache.Invoke($null, [object[]]@([string]$weight, $runtimeInfo.PSObject.BaseObject, 64, 48, 0, [int]$scale, 'fp16'))
    }
    if (@($paths | Select-Object -Unique).Count -ne 3) { throw '不同倍率复用了相同 Engine 缓存' }
    Write-Host 'TENSORRT_PRESET_TESTS_PASS|parse-2-3-4|reject-invalid|catalog|cuda-unchanged|cache-isolation'
} finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    if (-not $resolved.StartsWith($tempBase + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw '拒绝清理非临时测试目录'
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
