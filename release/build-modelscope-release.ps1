param(
    # 留空时自动读取 VideoEnhancerPlugin\PluginVersion.vb 的 Current（版本唯一人工维护点）。
    [string]$Version = '',
    [string]$UpstreamBase = '1.4.2',
    [string]$HostBin = 'C:\Users\maxzr\AppData\Local\Temp\FFmpegFreeUI.6.1.39.extracted',
    [string]$Notes = '版本检查改为 GitHub Releases（唯一标准），下载首选 ModelScope、GitHub 兜底；CLI 版本号改为单一来源。',
    [switch]$PublishGithub,
    [switch]$PublishModelScope,
    [string]$GithubRepo = 'maxzrb/VideoEnhancer',
    [string]$ModelScopeReleaseDataset = 'AerithDream/VideoEnhancer-Releases'
)

$ErrorActionPreference = 'Stop'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$root = Split-Path -Parent $PSScriptRoot
$pluginVersionFile = Join-Path $root 'VideoEnhancerPlugin\PluginVersion.vb'
$cliProject = Join-Path $root 'cli\VideoEnhancer.csproj'

$pluginVersionText = [System.IO.File]::ReadAllText($pluginVersionFile, [System.Text.Encoding]::UTF8)
if ($pluginVersionText -notmatch 'Public Const Current As String = "([^"]+)"') {
    throw '无法从 PluginVersion.vb 读取 Current 版本号'
}
$sourceVersion = $Matches[1]
if (-not $Version) { $Version = $sourceVersion }

$projectText = [System.IO.File]::ReadAllText($cliProject, [System.Text.Encoding]::UTF8)
if ($pluginVersionText -notmatch ('Public Const Current As String = "' + [regex]::Escape($Version) + '"')) {
    throw "PluginVersion.Current 为 $sourceVersion，与显式传入的发布版本 $Version 不一致"
}
if ($projectText -notmatch ('<Version>' + [regex]::Escape($Version) + '</Version>')) {
    throw "VideoEnhancer.csproj 与发布版本 $Version 不一致；CLI 版本唯一来源是 csproj 的 <Version>"
}

& (Join-Path $root 'VideoEnhancerPlugin\build.ps1') -HostBin $HostBin -SkipInstall
if ($LASTEXITCODE -ne 0) { throw '插件构建失败' }
& (Join-Path $root 'cli\build.ps1')
if ($LASTEXITCODE -ne 0) { throw 'CLI 发布失败' }

# 端到端校验：CLI 版本号运行时读自 csproj 程序集元数据，必须与发布版本一致。
$cliExe = Join-Path $root 'videoenhancer.exe'
$cliVersion = (& $cliExe --version) | Select-Object -First 1
if (("$cliVersion").Trim() -ne $Version) {
    throw "videoenhancer.exe 报告版本 '$cliVersion'，与发布版本 $Version 不一致"
}

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
$stablePath = Join-Path $distRoot 'stable.json'
[System.IO.File]::WriteAllText(
    $stablePath,
    ($stable | ConvertTo-Json -Depth 5),
    $utf8NoBom)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'modelscope-README.md') -Destination (Join-Path $distRoot 'README.md') -Force

Write-Host "OK: $zipPath"
Write-Host "OK: $stablePath"

# 原生命令在 EAP=Stop 下写 stderr 会被当成终止错误，发布前临时放宽。
function Invoke-Native {
    param([scriptblock]$Block)
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $Block 2>&1 | ForEach-Object { "$_" } | Write-Host
        return $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previous
    }
}

if ($PublishGithub) {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw '未找到 gh CLI；请安装 GitHub CLI 并 gh auth login 后重试'
    }
    $code = Invoke-Native { gh release create "v$Version" $zipPath $stablePath --repo $GithubRepo --title "VideoEnhancer $Version" --notes $Notes }
    if ($code -ne 0) { throw "gh release create v$Version 失败（$GithubRepo）" }
    Write-Host "OK: GitHub Release v$Version 已创建（$GithubRepo）"
}

if ($PublishModelScope) {
    if (-not (Get-Command modelscope -ErrorAction SilentlyContinue)) {
        throw '未找到 modelscope CLI；请先安装并 modelscope login 后重试'
    }
    $code = Invoke-Native { modelscope upload $ModelScopeReleaseDataset $distRoot }
    if ($code -ne 0) { throw "modelscope upload 失败（$ModelScopeReleaseDataset）" }
    Write-Host "OK: ModelScope 已同步（$ModelScopeReleaseDataset）"
}

if (-not $PublishGithub -and -not $PublishModelScope) {
    Write-Host "ModelScope 上传目录：$distRoot"
    Write-Host '手动发布命令：'
    Write-Host "  gh release create v$Version `"$zipPath`" `"$stablePath`" --repo $GithubRepo --title `"VideoEnhancer $Version`" --notes `"$Notes`""
    Write-Host "  modelscope upload $ModelScopeReleaseDataset `"$distRoot`""
}
