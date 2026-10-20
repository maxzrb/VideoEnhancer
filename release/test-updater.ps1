param([string]$Version = '', [string]$Package = '')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Package) { $Package = Join-Path $root 'Artifacts\videoenhancer.exe' }
$Package = [IO.Path]::GetFullPath($Package)
if (-not (Test-Path -LiteralPath $Package -PathType Leaf)) { throw "缺少运行时更新包：$Package" }
$source = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'VideoEnhancerPlugin\PluginUpdater.vb')
foreach ($part in @('SHA256.HashData(stream)', '"--apply-update"', '"--wait-pid"', 'ConsumeUpdateResult')) {
    if (-not $source.Contains($part)) { throw "插件更新协议缺少：$part" }
}
if ($source.Contains('VideoEnhancerInstaller.exe')) { throw '更新器仍依赖首次安装器' }

$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$testRoot = [IO.Path]::GetFullPath((Join-Path $tempBase ('VideoEnhancerSelfUpdateTest-' + [guid]::NewGuid().ToString('N'))))
if (-not $testRoot.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase)) { throw '测试目录超出临时目录' }
New-Item -ItemType Directory -Path $testRoot | Out-Null
try {
    $hostRoot = Join-Path $testRoot 'FFmpegFreeUI with spaces'
    $pluginRoot = Join-Path $hostRoot 'Plugin'
    $coreRoot = Join-Path $pluginRoot 'videoenhancer'
    New-Item -ItemType Directory -Force -Path $coreRoot | Out-Null
    [IO.File]::WriteAllText((Join-Path $hostRoot 'FFmpegFreeUI.x64.exe'), 'test host', [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $coreRoot 'aria2-next-keep.txt'), 'unchanged', [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $coreRoot 'videoenhancer.exe'), 'old exe', [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $pluginRoot 'videoenhancer.3fui.dll'), 'old dll', [Text.UTF8Encoding]::new($false))
    $updater = Join-Path $testRoot 'videoenhancer-updater.exe'
    Copy-Item -LiteralPath $Package -Destination $updater
    $start = [Diagnostics.ProcessStartInfo]::new($updater)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in @('--apply-update', '--update-package', $Package, '--update-target', $pluginRoot, '--wait-pid', '0', '--restart-exe', '')) {
        $start.ArgumentList.Add($argument)
    }
    $process = [Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "自更新失败：$($process.ExitCode)；$stdout $stderr" }
    if ((Get-FileHash -LiteralPath (Join-Path $coreRoot 'videoenhancer.exe')).Hash -ne (Get-FileHash -LiteralPath $Package).Hash) {
        throw '更新后运行 EXE 哈希不一致'
    }
    if ((Get-FileHash -LiteralPath (Join-Path $pluginRoot 'videoenhancer.3fui.dll')).Hash -ne
        (Get-FileHash -LiteralPath (Join-Path $root 'VideoEnhancerPlugin\obj\plugin-artifact\videoenhancer.3fui.dll')).Hash) {
        throw '更新后插件 DLL 哈希不一致'
    }
    if ((Get-Content -Raw -Encoding UTF8 (Join-Path $coreRoot 'aria2-next-keep.txt')) -ne 'unchanged') {
        throw '更新误改了独立组件'
    }
    if ((Get-Content -Raw -Encoding UTF8 (Join-Path $coreRoot '.update\update-result.txt')) -ne "OK|$Version") {
        throw '更新结果记录不正确'
    }
    $exeHash = (Get-FileHash -LiteralPath (Join-Path $coreRoot 'videoenhancer.exe')).Hash
    $dllHash = (Get-FileHash -LiteralPath (Join-Path $pluginRoot 'videoenhancer.3fui.dll')).Hash
    $oldBin = Join-Path $pluginRoot 'bin'
    New-Item -ItemType Directory -Path $oldBin | Out-Null
    [IO.File]::WriteAllText((Join-Path $oldBin 'keep.txt'), 'old layout', [Text.UTF8Encoding]::new($false))
    $env:VIDEOENHANCER_TEST_LAYOUT_FAIL_AFTER_MOVE = '1'
    try {
        $failed = [Diagnostics.Process]::Start($start)
        $failed.StandardOutput.ReadToEnd() | Out-Null
        $failed.StandardError.ReadToEnd() | Out-Null
        $failed.WaitForExit()
        if ($failed.ExitCode -eq 0) { throw '注入迁移故障后更新仍报告成功' }
    } finally { Remove-Item Env:VIDEOENHANCER_TEST_LAYOUT_FAIL_AFTER_MOVE -ErrorAction SilentlyContinue }
    if ((Get-FileHash -LiteralPath (Join-Path $coreRoot 'videoenhancer.exe')).Hash -ne $exeHash -or
        (Get-FileHash -LiteralPath (Join-Path $pluginRoot 'videoenhancer.3fui.dll')).Hash -ne $dllHash) {
        throw '更新失败后未恢复 EXE/DLL'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $oldBin 'keep.txt'))) { throw '更新失败后旧目录未恢复' }
    Write-Host 'UPDATER_TESTS_PASS|self-update|exe-dll-hashes|independent-component-preserved|rollback'
} finally {
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}
