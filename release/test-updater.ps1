param(
    # 留空时自动读取 PluginVersion.vb 的当前版本。
    [string]$Version = '',
    # 开发阶段可显式传入刚构建的单文件 EXE；正式发布默认读取版本目录资产。
    [string]$Package = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Version) {
    $text = [System.IO.File]::ReadAllText((Join-Path $root 'VideoEnhancerPlugin\PluginVersion.vb'), [System.Text.Encoding]::UTF8)
    if ($text -notmatch 'Public Const Current As String = "([^"]+)"') { throw '无法从 PluginVersion.vb 读取版本号' }
    $Version = $Matches[1]
}
$updater = Join-Path $root 'videoenhancer.exe'
$package = if ([string]::IsNullOrWhiteSpace($Package)) {
    Join-Path $PSScriptRoot "dist\modelscope\releases\$Version\VideoEnhancer-$Version-win-x64.exe"
} else {
    [System.IO.Path]::GetFullPath($Package)
}
if (-not (Test-Path -LiteralPath $updater) -or -not (Test-Path -LiteralPath $package)) {
    throw '请先运行 release\build-modelscope-release.ps1'
}

$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('VideoEnhancerUpdaterTest-' + [guid]::NewGuid().ToString('N'))
$resolvedTemp = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$resolvedTest = [System.IO.Path]::GetFullPath($testRoot)
if (-not $resolvedTest.StartsWith($resolvedTemp, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "测试目录不在系统临时目录：$resolvedTest"
}
New-Item -ItemType Directory -Force -Path $testRoot | Out-Null
$originalPauseAfterMove = $env:VIDEOENHANCER_TEST_LAYOUT_PAUSE_AFTER_MOVE
$originalReadyFile = $env:VIDEOENHANCER_TEST_LAYOUT_READY_FILE

function New-DummyTarget([string]$name) {
    $target = Join-Path $testRoot $name
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    foreach ($file in @('videoenhancer.exe', 'videoenhancer.3fui.dll')) {
        [System.IO.File]::WriteAllText((Join-Path $target $file), "old-$file", [System.Text.UTF8Encoding]::new($false))
    }
    foreach ($directory in @('bin', 'models', 'python', '.videoenhancer-backend-update')) {
        $legacyDirectory = Join-Path $target $directory
        New-Item -ItemType Directory -Force -Path $legacyDirectory | Out-Null
        [System.IO.File]::WriteAllText((Join-Path $legacyDirectory 'migration-marker.txt'),
            "old-$directory", [System.Text.UTF8Encoding]::new($false))
    }
    return $target
}

function Assert-UpdatedLayout([string]$target) {
    $applicationRoot = Join-Path $target 'videoenhancer'
    $expectedExe = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root 'videoenhancer.exe')).Hash
    $actualExe = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $applicationRoot 'videoenhancer.exe')).Hash
    if ($actualExe -ne $expectedExe) { throw '新布局 EXE 哈希不一致' }
    $expectedDll = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root 'videoenhancer.3fui.dll')).Hash
    $actualDll = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $target 'videoenhancer.3fui.dll')).Hash
    if ($actualDll -ne $expectedDll) { throw 'Plugin 根目录 DLL 哈希不一致' }
    if (Test-Path -LiteralPath (Join-Path $target 'videoenhancer.exe')) { throw '更新后仍残留旧平铺 EXE' }
    foreach ($directory in @('bin', 'models', 'python', '.videoenhancer-backend-update')) {
        if (-not (Test-Path -LiteralPath (Join-Path $applicationRoot "$directory\migration-marker.txt"))) {
            throw "旧目录内容没有迁移：$directory"
        }
        if (Test-Path -LiteralPath (Join-Path $target $directory)) { throw "旧平铺目录仍存在：$directory" }
    }
}

function Invoke-Updater([string]$archive, [string]$target, [string]$result,
        [string]$restart = '', [int]$waitPid = 0) {
    $arguments = @('--apply-update', '--update-package', $archive, '--update-target', $target,
        '--wait-pid', $waitPid.ToString([Globalization.CultureInfo]::InvariantCulture),
        '--update-result', $result)
    if (-not [string]::IsNullOrWhiteSpace($restart)) {
        $arguments += @('--restart-exe', $restart)
    }
    & $updater @arguments | Out-Host
    return $LASTEXITCODE
}

try {
    $successTarget = New-DummyTarget 'success'
    $successResult = Join-Path $testRoot 'success-result.txt'
    if ((Invoke-Updater $package $successTarget $successResult) -ne 0) { throw '正常 EXE 更新测试失败' }
    Assert-UpdatedLayout $successTarget

    # 场景 2：宿主退出后残留的短暂文件占用应等待解除，而不是立即放弃更新。
    $transientTarget = New-DummyTarget 'transient-lock'
    $transientExe = Join-Path $transientTarget 'videoenhancer.exe'
    $lockReady = Join-Path $testRoot 'transient-lock-ready.txt'
    $lockJob = Start-Job -ScriptBlock {
        param($path, $ready)
        $stream = [System.IO.File]::Open($path, [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
        try {
            [System.IO.File]::WriteAllText($ready, 'ready', [System.Text.UTF8Encoding]::new($false))
            Start-Sleep -Seconds 2
        } finally {
            $stream.Dispose()
        }
    } -ArgumentList $transientExe, $lockReady
    try {
        $readyDeadline = [DateTime]::UtcNow.AddSeconds(10)
        while (-not (Test-Path -LiteralPath $lockReady) -and [DateTime]::UtcNow -lt $readyDeadline) {
            Start-Sleep -Milliseconds 100
        }
        if (-not (Test-Path -LiteralPath $lockReady)) { throw '临时占用测试未能建立文件锁' }
        if ((Invoke-Updater $package $transientTarget (Join-Path $testRoot 'transient-result.txt')) -ne 0) {
            throw '短暂文件占用解除后更新仍失败'
        }
    } finally {
        Wait-Job -Job $lockJob -Timeout 10 | Out-Null
        Remove-Job -Job $lockJob -Force
    }
    Assert-UpdatedLayout $transientTarget

    $tamperedPackage = Join-Path $testRoot 'tampered.exe'
    Copy-Item -LiteralPath $package -Destination $tamperedPackage
    $stream = [System.IO.File]::Open($tamperedPackage, [System.IO.FileMode]::Append, [System.IO.FileAccess]::Write, [System.IO.FileShare]::Read)
    try {
        $stream.WriteByte(0)
    } finally { $stream.Dispose() }
    $tamperedTarget = New-DummyTarget 'tampered'
    if ((Invoke-Updater $tamperedPackage $tamperedTarget (Join-Path $testRoot 'tampered-result.txt')) -eq 0) {
        throw '被篡改的 EXE 更新包未被拒绝'
    }

    $invalidPackage = Join-Path $testRoot 'invalid.exe'
    [System.IO.File]::WriteAllText($invalidPackage, 'not-a-videoenhancer-exe', [System.Text.UTF8Encoding]::new($false))
    $invalidTarget = New-DummyTarget 'invalid'
    if ((Invoke-Updater $invalidPackage $invalidTarget (Join-Path $testRoot 'invalid-result.txt')) -eq 0) {
        throw '无效 EXE 更新包未被拒绝'
    }

    $rollbackTarget = New-DummyTarget 'rollback'
    $before = @{}
    foreach ($file in @('videoenhancer.exe', 'videoenhancer.3fui.dll')) {
        $before[$file] = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $rollbackTarget $file)).Hash
    }
    $lockedPath = Join-Path $rollbackTarget 'videoenhancer.3fui.dll'
    $lock = [System.IO.File]::Open($lockedPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
    try {
        if ((Invoke-Updater $package $rollbackTarget (Join-Path $testRoot 'rollback-result.txt')) -eq 0) {
            throw '文件锁存在时更新器错误地报告成功'
        }
    } finally { $lock.Dispose() }
    foreach ($file in @('videoenhancer.exe', 'videoenhancer.3fui.dll')) {
        $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $rollbackTarget $file)).Hash
        if ($actual -ne $before[$file]) { throw "回滚后文件不一致：$file" }
    }
    foreach ($directory in @('bin', 'models', 'python', '.videoenhancer-backend-update')) {
        if (-not (Test-Path -LiteralPath (Join-Path $rollbackTarget "$directory\migration-marker.txt"))) {
            throw "失败后旧布局目录不完整：$directory"
        }
    }

    # 场景 6：迁移进程被强制终止后，下一次更新必须先按持久日志恢复，再重新完成迁移。
    $interruptedTarget = New-DummyTarget 'interrupted-layout'
    $interruptReady = Join-Path $testRoot 'layout-interrupt-ready.txt'
    $interruptResult = Join-Path $testRoot 'layout-interrupt-result.txt'
    $env:VIDEOENHANCER_TEST_LAYOUT_PAUSE_AFTER_MOVE = '2'
    $env:VIDEOENHANCER_TEST_LAYOUT_READY_FILE = $interruptReady
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $updater
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    foreach ($argument in @('--apply-update', '--update-package', $updater,
            '--update-target', $interruptedTarget, '--wait-pid', '0',
            '--update-result', $interruptResult)) {
        $startInfo.ArgumentList.Add($argument)
    }
    $interruptedProcess = [System.Diagnostics.Process]::Start($startInfo)
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds(20)
        while (-not (Test-Path -LiteralPath $interruptReady) -and [DateTime]::UtcNow -lt $deadline) {
            Start-Sleep -Milliseconds 100
        }
        if (-not (Test-Path -LiteralPath $interruptReady)) { throw '迁移中断测试未到达暂停点' }
        $interruptedProcess.Kill($true)
        $interruptedProcess.WaitForExit(10000) | Out-Null
    } finally {
        $interruptedProcess.Dispose()
        $env:VIDEOENHANCER_TEST_LAYOUT_PAUSE_AFTER_MOVE = $null
        $env:VIDEOENHANCER_TEST_LAYOUT_READY_FILE = $null
    }
    if (-not (Test-Path -LiteralPath (Join-Path $interruptedTarget '.videoenhancer-layout-pending.json'))) {
        throw '迁移进程中断后没有保留恢复日志'
    }
    if ((Invoke-Updater $updater $interruptedTarget $interruptResult) -ne 0) {
        throw '中断后的下一次更新未能恢复并完成迁移'
    }
    Assert-UpdatedLayout $interruptedTarget
    if (Test-Path -LiteralPath (Join-Path $interruptedTarget '.videoenhancer-layout-pending.json')) {
        throw '恢复完成后仍残留迁移日志'
    }

    # 场景 7：宿主已退出后，即使更新失败也必须重新启动，不能把用户留在已关闭状态。
    $restartMarker = Join-Path $testRoot 'restart-marker.txt'
    $restartScript = Join-Path $testRoot 'restart-host.cmd'
    [System.IO.File]::WriteAllText($restartScript,
        "@echo off`r`n> `"$restartMarker`" echo restarted`r`n", [System.Text.ASCIIEncoding]::new())
    $restartResult = Join-Path $testRoot 'restart-result.txt'
    $exitingHost = Start-Process -FilePath 'pwsh.exe' -ArgumentList @(
        '-NoProfile', '-Command', 'Start-Sleep -Seconds 1') -WindowStyle Hidden -PassThru
    try {
        if ((Invoke-Updater $invalidPackage (New-DummyTarget 'restart-on-failure') `
                $restartResult $restartScript $exitingHost.Id) -eq 0) {
            throw '无效更新包错误地报告成功'
        }
    } finally {
        $exitingHost.WaitForExit(10000) | Out-Null
        $exitingHost.Dispose()
    }
    $restartDeadline = [DateTime]::UtcNow.AddSeconds(10)
    while (-not (Test-Path -LiteralPath $restartMarker) -and [DateTime]::UtcNow -lt $restartDeadline) {
        Start-Sleep -Milliseconds 100
    }
    if (-not (Test-Path -LiteralPath $restartMarker)) { throw '更新失败后未重新启动宿主' }

    Write-Host 'PASS: migration / transient-lock / tamper / invalid-package / rollback / interruption-recovery / restart-on-failure'
} finally {
    $env:VIDEOENHANCER_TEST_LAYOUT_PAUSE_AFTER_MOVE = $originalPauseAfterMove
    $env:VIDEOENHANCER_TEST_LAYOUT_READY_FILE = $originalReadyFile
    if (Test-Path -LiteralPath $resolvedTest) {
        Remove-Item -LiteralPath $resolvedTest -Recurse -Force
    }
}
