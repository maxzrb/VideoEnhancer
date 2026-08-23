param(
    # 留空时自动读取 PluginVersion.vb 的当前版本。
    [string]$Version = ''
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$root = Split-Path -Parent $PSScriptRoot
if (-not $Version) {
    $text = [System.IO.File]::ReadAllText((Join-Path $root 'VideoEnhancerPlugin\PluginVersion.vb'), [System.Text.Encoding]::UTF8)
    if ($text -notmatch 'Public Const Current As String = "([^"]+)"') { throw '无法从 PluginVersion.vb 读取版本号' }
    $Version = $Matches[1]
}
$updater = Join-Path $root 'videoenhancer.exe'
$package = Join-Path $PSScriptRoot "dist\modelscope\releases\$Version\VideoEnhancer-$Version-win-x64.zip"
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

function New-DummyTarget([string]$name) {
    $target = Join-Path $testRoot $name
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    foreach ($file in @('videoenhancer.exe', 'videoenhancer.3fui.dll', 'videoenhancer-layout.json')) {
        [System.IO.File]::WriteAllText((Join-Path $target $file), "old-$file", [System.Text.UTF8Encoding]::new($false))
    }
    return $target
}

function Invoke-Updater([string]$archive, [string]$target, [string]$result) {
    & $updater --apply-update --update-package $archive --update-target $target --wait-pid 0 --update-result $result | Out-Host
    return $LASTEXITCODE
}

try {
    $successTarget = New-DummyTarget 'success'
    $successResult = Join-Path $testRoot 'success-result.txt'
    if ((Invoke-Updater $package $successTarget $successResult) -ne 0) { throw '正常更新测试失败' }
    foreach ($file in @('videoenhancer.exe', 'videoenhancer.3fui.dll', 'videoenhancer-layout.json')) {
        $expected = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root $file)).Hash
        $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $successTarget $file)).Hash
        if ($actual -ne $expected) { throw "正常更新哈希不一致：$file" }
    }

    $traversalPackage = Join-Path $testRoot 'traversal.zip'
    Copy-Item -LiteralPath $package -Destination $traversalPackage
    $archive = [System.IO.Compression.ZipFile]::Open($traversalPackage, [System.IO.Compression.ZipArchiveMode]::Update)
    try {
        $entry = $archive.CreateEntry('../escape.txt')
        $writer = [System.IO.StreamWriter]::new($entry.Open(), [System.Text.UTF8Encoding]::new($false))
        try { $writer.Write('blocked') } finally { $writer.Dispose() }
    } finally { $archive.Dispose() }
    $traversalTarget = New-DummyTarget 'traversal'
    if ((Invoke-Updater $traversalPackage $traversalTarget (Join-Path $testRoot 'traversal-result.txt')) -eq 0) {
        throw '路径穿越更新包未被拒绝'
    }
    if (Test-Path -LiteralPath (Join-Path $testRoot 'escape.txt')) { throw '路径穿越文件被写出' }

    $tamperedPackage = Join-Path $testRoot 'tampered.zip'
    Copy-Item -LiteralPath $package -Destination $tamperedPackage
    $archive = [System.IO.Compression.ZipFile]::Open($tamperedPackage, [System.IO.Compression.ZipArchiveMode]::Update)
    try {
        $entry = $archive.GetEntry('videoenhancer-layout.json')
        $entry.Delete()
        $entry = $archive.CreateEntry('videoenhancer-layout.json')
        $writer = [System.IO.StreamWriter]::new($entry.Open(), [System.Text.UTF8Encoding]::new($false))
        try { $writer.Write('tampered') } finally { $writer.Dispose() }
    } finally { $archive.Dispose() }
    $tamperedTarget = New-DummyTarget 'tampered'
    if ((Invoke-Updater $tamperedPackage $tamperedTarget (Join-Path $testRoot 'tampered-result.txt')) -eq 0) {
        throw '被篡改的更新包未被拒绝'
    }

    $rollbackTarget = New-DummyTarget 'rollback'
    $before = @{}
    foreach ($file in @('videoenhancer.exe', 'videoenhancer.3fui.dll', 'videoenhancer-layout.json')) {
        $before[$file] = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $rollbackTarget $file)).Hash
    }
    $lockedPath = Join-Path $rollbackTarget 'videoenhancer-layout.json'
    # 允许更新器读取并完成备份，但拒绝后续覆盖写入，强制走“部分替换后回滚”。
    $lock = [System.IO.File]::Open($lockedPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
    try {
        if ((Invoke-Updater $package $rollbackTarget (Join-Path $testRoot 'rollback-result.txt')) -eq 0) {
            throw '文件锁存在时更新器错误地报告成功'
        }
    } finally { $lock.Dispose() }
    foreach ($file in @('videoenhancer.exe', 'videoenhancer.3fui.dll', 'videoenhancer-layout.json')) {
        $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $rollbackTarget $file)).Hash
        if ($actual -ne $before[$file]) { throw "回滚后文件不一致：$file" }
    }

    Write-Host 'PASS: success / traversal / tamper / rollback'
} finally {
    if (Test-Path -LiteralPath $resolvedTest) {
        Remove-Item -LiteralPath $resolvedTest -Recurse -Force
    }
}
