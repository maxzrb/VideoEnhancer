param(
    [string]$Version = '1.11.2',
    [string]$UpstreamBase = '1.4.2',
    [string]$HostBin = 'C:\Users\maxzr\AppData\Local\Temp\FFmpegFreeUI.6.1.39.extracted',
    [string]$Notes = '优化更新确认窗口，信息更简洁清晰。'
)

$ErrorActionPreference = 'Stop'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$root = Split-Path -Parent $PSScriptRoot
$pluginVersionFile = Join-Path $root 'VideoEnhancerPlugin\PluginVersion.vb'
$cliProject = Join-Path $root 'cli\VideoEnhancer.csproj'
$cliProgram = Join-Path $root 'cli\Program.cs'

$pluginVersionText = [System.IO.File]::ReadAllText($pluginVersionFile, [System.Text.Encoding]::UTF8)
$projectText = [System.IO.File]::ReadAllText($cliProject, [System.Text.Encoding]::UTF8)
$programText = [System.IO.File]::ReadAllText($cliProgram, [System.Text.Encoding]::UTF8)
if ($pluginVersionText -notmatch ('Public Const Current As String = "' + [regex]::Escape($Version) + '"')) {
    throw "PluginVersion.Current 与发布版本 $Version 不一致"
}
if ($projectText -notmatch ('<Version>' + [regex]::Escape($Version) + '</Version>')) {
    throw "VideoEnhancer.csproj 与发布版本 $Version 不一致"
}
if ($programText -notmatch ('ToolVersion = "' + [regex]::Escape($Version) + '"')) {
    throw "Program.ToolVersion 与发布版本 $Version 不一致"
}

& (Join-Path $root 'VideoEnhancerPlugin\build.ps1') -HostBin $HostBin -SkipInstall
if ($LASTEXITCODE -ne 0) { throw '插件构建失败' }
& (Join-Path $root 'cli\build.ps1')
if ($LASTEXITCODE -ne 0) { throw 'CLI 发布失败' }

$distRoot = Join-Path $PSScriptRoot 'dist\modelscope'
$versionRoot = Join-Path $distRoot (Join-Path 'releases' $Version)
$packageRoot = Join-Path $PSScriptRoot (Join-Path 'dist\package' $Version)
if (Test-Path -LiteralPath $versionRoot) { Remove-Item -LiteralPath $versionRoot -Recurse -Force }
if (Test-Path -LiteralPath $packageRoot) { Remove-Item -LiteralPath $packageRoot -Recurse -Force }
New-Item -ItemType Directory -Force -Path $versionRoot, $packageRoot | Out-Null

$runtimeFiles = @('videoenhancer.exe', 'videoenhancer.3fui.dll', 'videoenhancer-layout.json')
$packageFiles = @()
foreach ($name in $runtimeFiles) {
    $source = Join-Path $root $name
    if (-not (Test-Path -LiteralPath $source)) { throw "缺少发布文件：$source" }
    Copy-Item -LiteralPath $source -Destination (Join-Path $packageRoot $name) -Force
    $item = Get-Item -LiteralPath $source
    $packageFiles += [ordered]@{
        path = $name
        size = $item.Length
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $source).Hash.ToLowerInvariant()
    }
}

$packageManifest = [ordered]@{
    schemaVersion = 1
    version = $Version
    files = $packageFiles
}
$packageManifestPath = Join-Path $packageRoot 'package.json'
[System.IO.File]::WriteAllText(
    $packageManifestPath,
    ($packageManifest | ConvertTo-Json -Depth 5),
    $utf8NoBom)

$zipName = "VideoEnhancer-$Version-win-x64.zip"
$zipPath = Join-Path $versionRoot $zipName
Compress-Archive -Path (Join-Path $packageRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal
$zipItem = Get-Item -LiteralPath $zipPath
$stable = [ordered]@{
    schemaVersion = 1
    channel = 'stable'
    version = $Version
    upstreamBase = $UpstreamBase
    publishedAt = [DateTimeOffset]::Now.ToString('o')
    package = [ordered]@{
        path = "releases/$Version/$zipName"
        size = $zipItem.Length
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath).Hash.ToLowerInvariant()
    }
    notes = $Notes
}
[System.IO.File]::WriteAllText(
    (Join-Path $distRoot 'stable.json'),
    ($stable | ConvertTo-Json -Depth 5),
    $utf8NoBom)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'modelscope-README.md') -Destination (Join-Path $distRoot 'README.md') -Force

Write-Host "OK: $zipPath"
Write-Host "OK: $(Join-Path $distRoot 'stable.json')"
Write-Host "ModelScope 上传目录：$distRoot"
