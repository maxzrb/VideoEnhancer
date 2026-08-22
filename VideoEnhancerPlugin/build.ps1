# ============================================================
# videoenhancer.3fui.dll 插件构建脚本
# 依赖：本机已安装 .NET SDK 10（含 Roslyn vbc），
#       以及 FFmpegFreeUI 开发版程序集（FFmpegFreeUI\bin\Debug\net10.0-windows10.0.26100.0\）
# 产物：videoenhancer.dll → 复制为 ..\Video Enhancer GUI\Plugin\videoenhancer.3fui.dll
# ============================================================
param(
    [string]$HostBin = 'C:\Users\ARXChem\Documents\LakeUIApps\FFmpegFreeUI\FFmpegFreeUI\bin\Debug\net10.0-windows10.0.26100.0',
    [switch]$SkipInstall
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$outDir = Join-Path $scriptDir 'out'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# 定位 .NET SDK 的 Roslyn vbc
$sdkRoot = Get-ChildItem 'C:\Program Files\dotnet\sdk' -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$vbc = Join-Path $sdkRoot.FullName 'Roslyn\bincore\vbc.dll'
if (-not (Test-Path $vbc)) { throw "vbc not found: $vbc" }

$netRef = Get-ChildItem 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref' -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$netRef = Join-Path $netRef.FullName 'ref\net10.0'
$winRef = Get-ChildItem 'C:\Program Files\dotnet\packs\Microsoft.WindowsDesktop.App.Ref' -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$winRef = Join-Path $winRef.FullName 'ref\net10.0'
if (-not (Test-Path $netRef) -or -not (Test-Path $winRef)) { throw "net10 ref packs not found" }

if (-not (Test-Path (Join-Path $hostBin 'FFmpegFreeUI.dll')) -or -not (Test-Path (Join-Path $hostBin 'LakeUI.dll'))) {
    throw "host assemblies not found: $hostBin"
}

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('-nologo')
$lines.Add('-target:library')
$lines.Add('-nostdlib')
$lines.Add('-vbruntime-')
$lines.Add('-optionstrict+')
$lines.Add('-optionexplicit+')
$lines.Add('-optioninfer+')
$lines.Add('"-out:' + (Join-Path $outDir 'videoenhancer.dll') + '"')
Get-ChildItem $netRef -Filter *.dll | ForEach-Object { $lines.Add('"-r:' + $_.FullName + '"') }
Get-ChildItem $winRef -Filter *.dll | ForEach-Object { $lines.Add('"-r:' + $_.FullName + '"') }
$lines.Add('"-r:' + (Join-Path $hostBin 'FFmpegFreeUI.dll') + '"')
$lines.Add('"-r:' + (Join-Path $hostBin 'LakeUI.dll') + '"')
Get-ChildItem $scriptDir -Filter *.vb | Sort-Object Name | ForEach-Object { $lines.Add('"' + $_.FullName + '"') }
$layoutJson = Join-Path $scriptDir '..\videoenhancer-layout.json'
if (-not (Test-Path -LiteralPath $layoutJson)) { throw "layout JSON not found: $layoutJson" }
$lines.Add('"-resource:' + $layoutJson + ',videoenhancer-layout.json"')
$modelImage = Join-Path $scriptDir 'assets\model-introduction.jpg'
if (-not (Test-Path -LiteralPath $modelImage)) { throw "model introduction image not found: $modelImage" }
$lines.Add('"-resource:' + $modelImage + ',videoenhancer-model-introduction.jpg"')

$rsp = Join-Path $outDir 'vbc.rsp'
[System.IO.File]::WriteAllLines($rsp, $lines, (New-Object System.Text.UTF8Encoding($false)))

& dotnet $vbc "@$rsp"
if ($LASTEXITCODE -ne 0) { throw 'vbc compile failed' }

$dll = Join-Path $outDir 'videoenhancer.dll'
$releaseDll = Join-Path (Split-Path -Parent $scriptDir) 'videoenhancer.3fui.dll'
Copy-Item $dll $releaseDll -Force
if (-not $SkipInstall) {
    $pluginDir = 'C:\Users\ARXChem\Documents\LakeUIApps\Video Enhancer GUI\Plugin'
    try {
        New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
        Copy-Item $dll (Join-Path $pluginDir 'videoenhancer.3fui.dll') -Force
        Copy-Item -LiteralPath $layoutJson -Destination (Join-Path $pluginDir 'videoenhancer-layout.json') -Force
        Write-Host "OK: $(Join-Path $pluginDir 'videoenhancer.3fui.dll')"
    } catch {
        Write-Warning "插件已在版本目录构建完成，但开发版插件目录复制失败：$($_.Exception.Message)"
    }
}
Write-Host "OK: $releaseDll"
