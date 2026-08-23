# 一键发布：把 outputs 中的产物发布到版本存档目录 + 各运行目录
# 规则：每个版本更新都发布到 C:\Users\ARXChem\Documents\LakeUI-2\videoenhancer.3fui\<版本>\
param(
    # 留空时自动读取 PluginVersion.vb 的当前版本。
    [string]$Version = ''
)
$ErrorActionPreference = 'Stop'
$base = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $Version) {
    $text = [System.IO.File]::ReadAllText((Join-Path $base 'VideoEnhancerPlugin\PluginVersion.vb'), [System.Text.Encoding]::UTF8)
    if ($text -notmatch 'Public Const Current As String = "([^"]+)"') { throw '无法从 PluginVersion.vb 读取版本号' }
    $Version = $Matches[1]
}
$archiveRoot = 'C:\Users\ARXChem\Documents\LakeUI-2\videoenhancer.3fui'
$archive = Join-Path $archiveRoot $Version
New-Item -ItemType Directory -Force -Path $archive | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $archive 'cli') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $archive 'VideoEnhancerPlugin') | Out-Null

# 1) 主程序产物（CLI 单文件 + 3FUI 插件 DLL）
Copy-Item -LiteralPath (Join-Path $base 'videoenhancer.exe') -Destination (Join-Path $archive 'videoenhancer.exe') -Force
Copy-Item -LiteralPath (Join-Path $base 'videoenhancer.3fui.dll') -Destination (Join-Path $archive 'videoenhancer.3fui.dll') -Force
$layoutJson = Join-Path $base 'PluginDesigner\bin\Release\net10.0-windows\videoenhancer-layout.json'
if (Test-Path -LiteralPath $layoutJson) {
    Copy-Item -LiteralPath $layoutJson -Destination (Join-Path $archive 'videoenhancer-layout.json') -Force
}

# 2) CLI 源码（Program.cs / README / build.ps1 / csproj）
Copy-Item -LiteralPath (Join-Path $base 'cli\Program.cs') -Destination (Join-Path $archive 'cli\Program.cs') -Force
Copy-Item -LiteralPath (Join-Path $base 'cli\README.md') -Destination (Join-Path $archive 'cli\README.md') -Force
Copy-Item -LiteralPath (Join-Path $base 'cli\build.ps1') -Destination (Join-Path $archive 'cli\build.ps1') -Force
Copy-Item -LiteralPath (Join-Path $base 'cli\VideoEnhancer.csproj') -Destination (Join-Path $archive 'cli\VideoEnhancer.csproj') -Force

# 3) 插件源码 + 构建脚本 + 说明
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

# 5) 插件 DLL 复制到运行目录：开发版 GUI 插件目录 + 最新发布版 ReadyToRun 插件目录
$pluginDll = Join-Path $base 'videoenhancer.3fui.dll'
$pluginTargets = @(
    'C:\Users\ARXChem\Documents\LakeUIApps\Video Enhancer GUI\Plugin\videoenhancer.3fui.dll',
    'C:\PortableSoft\FFmpegFreeUI ReadyToRun x64\plugin\videoenhancer.3fui.dll'
)
foreach ($t in $pluginTargets) {
    try {
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $t) | Out-Null
        Copy-Item -LiteralPath $pluginDll -Destination $t -Force
        if (Test-Path -LiteralPath $layoutJson) {
            Copy-Item -LiteralPath $layoutJson -Destination (Join-Path (Split-Path -Parent $t) 'videoenhancer-layout.json') -Force
        }
        Write-Host "  已复制插件到 $t"
    } catch {
        Write-Host "  插件复制失败（跳过）：$t"
        Write-Host "    $($_.Exception.Message)"
    }
}

Write-Host ''
Write-Host "已发布 v$Version 到：$archive"
Write-Host '  插件已复制到 Video Enhancer GUI\Plugin 与 FFmpegFreeUI ReadyToRun x64\plugin。'
