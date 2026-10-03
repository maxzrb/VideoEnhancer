# 本地部署：默认存档到仓库 Artifacts\releases；安装目录由参数或环境变量明确提供。
param(
    # 留空时自动读取 VideoEnhancerPlugin.vbproj 的 Version。
    [string]$Version = '',
    [string]$ArtifactsRoot = $env:VIDEOENHANCER_ARTIFACTS_DIR,
    [string]$ArchiveRoot = $env:VIDEOENHANCER_ARCHIVE_ROOT,
    [string[]]$PluginDirectories = @()
)
$ErrorActionPreference = 'Stop'
$base = Split-Path -Parent $MyInvocation.MyCommand.Path

function Resolve-ProjectPath([string]$value) {
    if (-not [IO.Path]::IsPathRooted($value)) { $value = Join-Path $base $value }
    return [IO.Path]::GetFullPath($value)
}

function Get-ProjectVersion([string]$projectPath) {
    $document = [System.Xml.XmlDocument]::new()
    $document.Load($projectPath)
    $nodes = @($document.SelectNodes('/Project/PropertyGroup/Version'))
    if ($nodes.Count -ne 1 -or [string]::IsNullOrWhiteSpace($nodes[0].InnerText)) {
        throw "项目必须声明且只能声明一个 Version：$projectPath"
    }
    return $nodes[0].InnerText.Trim()
}

$pluginProject = Join-Path $base 'VideoEnhancerPlugin\VideoEnhancerPlugin.vbproj'
$cliProject = Join-Path $base 'cli\VideoEnhancer.csproj'
$pluginVersion = Get-ProjectVersion $pluginProject
$cliVersion = Get-ProjectVersion $cliProject
if (-not $Version) { $Version = $pluginVersion }
if ($pluginVersion -ne $Version -or $cliVersion -ne $Version) {
    throw "项目版本不一致：插件=$pluginVersion，CLI=$cliVersion，部署版本=$Version"
}
if ([string]::IsNullOrWhiteSpace($ArtifactsRoot)) { $ArtifactsRoot = 'Artifacts' }
$ArtifactsRoot = Resolve-ProjectPath $ArtifactsRoot
if ([string]::IsNullOrWhiteSpace($ArchiveRoot)) { $ArchiveRoot = Join-Path $ArtifactsRoot 'releases' }
$ArchiveRoot = Resolve-ProjectPath $ArchiveRoot
$archive = Join-Path $ArchiveRoot $Version
$installerArtifact = Join-Path $artifactsRoot 'VideoEnhancerInstaller.exe'
$manualArtifact = Join-Path $artifactsRoot 'VideoEnhancer.zip'
$pluginDll = Join-Path $base 'VideoEnhancerPlugin\obj\plugin-artifact\videoenhancer.3fui.dll'
New-Item -ItemType Directory -Force -Path $archive | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $archive 'cli') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $archive 'VideoEnhancerPlugin') | Out-Null

# 1) 主程序产物（安装程序 + 手动安装包）
Copy-Item -LiteralPath $installerArtifact -Destination (Join-Path $archive 'VideoEnhancerInstaller.exe') -Force
Copy-Item -LiteralPath $manualArtifact -Destination (Join-Path $archive 'VideoEnhancer.zip') -Force
Copy-Item -LiteralPath (Join-Path $base 'VideoEnhancer.slnx') -Destination (Join-Path $archive 'VideoEnhancer.slnx') -Force

# 2) CLI 源码（Program.cs / README / csproj）
Copy-Item -LiteralPath (Join-Path $base 'cli\Program.cs') -Destination (Join-Path $archive 'cli\Program.cs') -Force
Copy-Item -LiteralPath (Join-Path $base 'cli\README.md') -Destination (Join-Path $archive 'cli\README.md') -Force
Copy-Item -LiteralPath (Join-Path $base 'cli\VideoEnhancer.csproj') -Destination (Join-Path $archive 'cli\VideoEnhancer.csproj') -Force

# 3) 插件源码、项目文件与说明
$pluginSrc = Join-Path $base 'VideoEnhancerPlugin'
$pluginDst = Join-Path $archive 'VideoEnhancerPlugin'
Get-ChildItem -LiteralPath $pluginSrc -File | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $pluginDst $_.Name) -Force
}
$assetSrc = Join-Path $pluginSrc 'assets'
if (Test-Path -LiteralPath $assetSrc) {
    Copy-Item -LiteralPath $assetSrc -Destination $pluginDst -Recurse -Force
}

# 4) 部署脚本本身
Copy-Item -LiteralPath $MyInvocation.MyCommand.Path -Destination (Join-Path $archive 'deploy.ps1') -Force

# 5) 只有明确指定安装目录时才复制插件，避免写入其他机器的固定目录。
if ($PluginDirectories.Count -eq 0 -and -not [string]::IsNullOrWhiteSpace($env:VIDEOENHANCER_PLUGIN_DIR)) {
    $PluginDirectories = @($env:VIDEOENHANCER_PLUGIN_DIR)
}
foreach ($directory in $PluginDirectories) {
    $destination = Resolve-ProjectPath $directory
    $t = Join-Path $destination 'videoenhancer.3fui.dll'
    try {
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $t) | Out-Null
        Copy-Item -LiteralPath $pluginDll -Destination $t -Force
        Write-Host "  已复制插件到 $t"
    } catch {
        Write-Host "  插件复制失败（跳过）：$t"
        Write-Host "    $($_.Exception.Message)"
    }
}

Write-Host ''
Write-Host "已存档 v$Version 到：$archive"
if ($PluginDirectories.Count -eq 0) { Write-Host '  未指定插件安装目录，仅生成本地存档。' }
