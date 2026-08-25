param(
    [string]$Installer = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Installer)) {
    $Installer = Join-Path $root 'videoenhancer.exe'
}
$Installer = [System.IO.Path]::GetFullPath($Installer)
if (-not (Test-Path -LiteralPath $Installer)) {
    throw "缺少待测安装程序：$Installer"
}

$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('VideoEnhancerInstallerTest-' + [guid]::NewGuid().ToString('N'))
$resolvedTemp = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$resolvedTest = [System.IO.Path]::GetFullPath($testRoot)
if (-not $resolvedTest.StartsWith($resolvedTemp, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "测试目录不在系统临时目录：$resolvedTest"
}
New-Item -ItemType Directory -Force -Path $resolvedTest | Out-Null
$versionedInstaller = Join-Path $resolvedTest 'VideoEnhancer-test-win-x64.exe'
Copy-Item -LiteralPath $Installer -Destination $versionedInstaller
$originalInstallHost = $env:VIDEOENHANCER_INSTALL_HOST
$originalFailAfterMove = $env:VIDEOENHANCER_TEST_LAYOUT_FAIL_AFTER_MOVE

function New-TestHost([string]$name, [switch]$withOldPlugin) {
    $hostRoot = Join-Path $resolvedTest $name
    $pluginRoot = Join-Path $hostRoot 'plugin'
    New-Item -ItemType Directory -Force -Path $pluginRoot | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $hostRoot 'ffmpegfreeui.exe'), 'host', [System.Text.UTF8Encoding]::new($false))
    if ($withOldPlugin) {
        [System.IO.File]::WriteAllText((Join-Path $pluginRoot 'videoenhancer.exe'), 'old-exe', [System.Text.UTF8Encoding]::new($false))
        [System.IO.File]::WriteAllText((Join-Path $pluginRoot 'videoenhancer.3fui.dll'), 'old-dll', [System.Text.UTF8Encoding]::new($false))
        foreach ($directory in @('bin', 'models', 'python', '.videoenhancer-backend-update')) {
            $legacyDirectory = Join-Path $pluginRoot $directory
            New-Item -ItemType Directory -Force -Path $legacyDirectory | Out-Null
            [System.IO.File]::WriteAllText((Join-Path $legacyDirectory 'migration-marker.txt'),
                "old-$directory", [System.Text.UTF8Encoding]::new($false))
        }
        [System.IO.File]::WriteAllText((Join-Path $pluginRoot 'ffmpeg_log.txt'), 'old-log', [System.Text.UTF8Encoding]::new($false))
        [System.IO.File]::WriteAllText((Join-Path $pluginRoot 'python_20260825.7z'), 'old-archive', [System.Text.UTF8Encoding]::new($false))
    }
    return $hostRoot
}

function Assert-MigratedContent([string]$hostRoot) {
    $pluginRoot = Join-Path $hostRoot 'plugin'
    $applicationRoot = Join-Path $pluginRoot 'videoenhancer'
    foreach ($directory in @('bin', 'models', 'python', '.videoenhancer-backend-update')) {
        if (-not (Test-Path -LiteralPath (Join-Path $applicationRoot "$directory\migration-marker.txt"))) {
            throw "旧目录内容没有迁移到新布局：$directory"
        }
        if (Test-Path -LiteralPath (Join-Path $pluginRoot $directory)) {
            throw "迁移后仍存在旧平铺目录：$directory"
        }
    }
    foreach ($file in @('ffmpeg_log.txt', 'python_20260825.7z')) {
        if (-not (Test-Path -LiteralPath (Join-Path $applicationRoot $file)) -or
            (Test-Path -LiteralPath (Join-Path $pluginRoot $file))) {
            throw "旧文件没有迁移到新布局：$file"
        }
    }
}

function Invoke-Installer([string]$hostRoot) {
    $env:VIDEOENHANCER_INSTALL_HOST = Join-Path $hostRoot 'ffmpegfreeui.exe'
    @('y', 'y', '') | & $versionedInstaller 2>&1 | ForEach-Object { "$_" } | Write-Host
    return $LASTEXITCODE
}

function Assert-NewInstall([string]$hostRoot) {
    $pluginRoot = Join-Path $hostRoot 'plugin'
    $applicationRoot = Join-Path $pluginRoot 'videoenhancer'
    $installedExe = Join-Path $applicationRoot 'videoenhancer.exe'
    $installedDll = Join-Path $pluginRoot 'videoenhancer.3fui.dll'
    if (-not (Test-Path -LiteralPath $installedExe) -or -not (Test-Path -LiteralPath $installedDll)) {
        throw '全新安装没有生成固定名称的 EXE 和插件 DLL'
    }
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $installedExe).Hash -ne
        (Get-FileHash -Algorithm SHA256 -LiteralPath $versionedInstaller).Hash) {
        throw '固定名称 EXE 与版本化安装器哈希不一致'
    }
    foreach ($directory in @('models', 'python', 'bin')) {
        if (-not (Test-Path -LiteralPath (Join-Path $applicationRoot $directory) -PathType Container)) {
            throw "核心目录未创建在 videoenhancer 子目录：$directory"
        }
        if (Test-Path -LiteralPath (Join-Path $pluginRoot $directory)) {
            throw "Plugin 根目录仍残留旧平铺目录：$directory"
        }
    }
    if (Test-Path -LiteralPath (Join-Path $resolvedTest 'models')) {
        throw '安装器错误地在下载目录创建了核心目录'
    }
}

try {
    # 场景 1：任意版本化文件名从下载目录运行后，必须安装为固定名称。
    $freshHost = New-TestHost 'fresh'
    if ((Invoke-Installer $freshHost) -ne 0) { throw '全新安装测试失败' }
    Assert-NewInstall $freshHost

    # 场景 2：旧 EXE 短暂占用时等待解除，再完成事务替换。
    $transientHost = New-TestHost 'transient-lock' -withOldPlugin
    $transientExe = Join-Path $transientHost 'plugin\videoenhancer.exe'
    $readyPath = Join-Path $resolvedTest 'transient-ready.txt'
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
    } -ArgumentList $transientExe, $readyPath
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds(10)
        while (-not (Test-Path -LiteralPath $readyPath) -and [DateTime]::UtcNow -lt $deadline) {
            Start-Sleep -Milliseconds 100
        }
        if (-not (Test-Path -LiteralPath $readyPath)) { throw '短暂占用测试未建立文件锁' }
        if ((Invoke-Installer $transientHost) -ne 0) { throw '短暂占用解除后安装仍失败' }
    } finally {
        Wait-Job -Job $lockJob -Timeout 10 | Out-Null
        Remove-Job -Job $lockJob -Force
    }
    Assert-NewInstall $transientHost
    Assert-MigratedContent $transientHost
    if (Test-Path -LiteralPath $transientExe) { throw '迁移成功后仍残留旧平铺 EXE' }

    # 场景 3：目录已经迁移一部分后发生异常，必须立即恢复全部旧布局。
    $injectedFailureHost = New-TestHost 'injected-failure' -withOldPlugin
    $env:VIDEOENHANCER_TEST_LAYOUT_FAIL_AFTER_MOVE = '2'
    try {
        if ((Invoke-Installer $injectedFailureHost) -eq 0) { throw '迁移测试注入失败时安装器错误地报告成功' }
    } finally {
        $env:VIDEOENHANCER_TEST_LAYOUT_FAIL_AFTER_MOVE = $null
    }
    foreach ($directory in @('bin', 'models', 'python', '.videoenhancer-backend-update')) {
        if (-not (Test-Path -LiteralPath (Join-Path $injectedFailureHost "plugin\$directory\migration-marker.txt"))) {
            throw "部分迁移失败后旧目录没有恢复：$directory"
        }
    }
    if (Test-Path -LiteralPath (Join-Path $injectedFailureHost 'plugin\videoenhancer\videoenhancer.exe')) {
        throw '部分迁移失败后残留新布局 EXE'
    }

    # 场景 4：目标持续占用时必须在迁移前失败，不改变旧文件。
    $rollbackHost = New-TestHost 'permanent-lock' -withOldPlugin
    $rollbackExe = Join-Path $rollbackHost 'plugin\videoenhancer.exe'
    $rollbackDll = Join-Path $rollbackHost 'plugin\videoenhancer.3fui.dll'
    $beforeExe = (Get-FileHash -Algorithm SHA256 -LiteralPath $rollbackExe).Hash
    $beforeDll = (Get-FileHash -Algorithm SHA256 -LiteralPath $rollbackDll).Hash
    $lock = [System.IO.File]::Open($rollbackDll, [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
    try {
        if ((Invoke-Installer $rollbackHost) -eq 0) { throw '持续占用时安装器错误地报告成功' }
    } finally {
        $lock.Dispose()
    }
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $rollbackExe).Hash -ne $beforeExe -or
        (Get-FileHash -Algorithm SHA256 -LiteralPath $rollbackDll).Hash -ne $beforeDll) {
        throw '持续占用失败后的安装文件未正确回滚'
    }
    foreach ($directory in @('bin', 'models', 'python', '.videoenhancer-backend-update')) {
        if (-not (Test-Path -LiteralPath (Join-Path $rollbackHost "plugin\$directory\migration-marker.txt"))) {
            throw "持续占用失败后旧目录发生变化：$directory"
        }
    }

    Write-Host 'INSTALLER_TESTS_PASS|fresh-subdirectory|legacy-migration|transient-lock|injected-rollback|permanent-lock'
} finally {
    $env:VIDEOENHANCER_INSTALL_HOST = $originalInstallHost
    $env:VIDEOENHANCER_TEST_LAYOUT_FAIL_AFTER_MOVE = $originalFailAfterMove
    if (Test-Path -LiteralPath $resolvedTest) {
        Remove-Item -LiteralPath $resolvedTest -Recurse -Force
    }
}
