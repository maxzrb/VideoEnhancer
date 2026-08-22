$ErrorActionPreference = 'Stop'

$proj = Join-Path $PSScriptRoot 'VideoEnhancer.csproj'
$stage = Join-Path $PSScriptRoot '.publish'
$releaseRoot = Split-Path -Parent $PSScriptRoot

# 清理旧的临时发布目录
if (Test-Path -LiteralPath $stage) {
    Remove-Item -LiteralPath $stage -Recurse -Force
}

# 单文件自包含发布
dotnet publish $proj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $stage

# 只复制单个 exe 到当前 1.4 发布目录
$exe = Join-Path $stage 'videoenhancer.exe'
$dest = Join-Path $releaseRoot 'videoenhancer.exe'
Copy-Item -LiteralPath $exe -Destination $dest -Force
Write-Host "OK: $dest"
