param(
    # 留空时自动读取 VideoEnhancerPlugin\PluginVersion.vb 的 Current（版本唯一人工维护点）。
    [string]$Version = '',
    [string]$HostBin = 'C:\Users\maxzr\AppData\Local\Temp\FFmpegFreeUI.6.1.39.extracted',
    [string]$Notes = '',
    [string]$NotesFile = '',
    [string]$BackendBaseRoot = '',
    [string]$BackendTargetRoot = '',
    [string]$BackendBaseVersion = '',
    [string]$BackendTargetVersion = '',
    [string]$BackendFullArchive = '',
    [string[]]$BackendSentinelPaths = @(),
    [string]$BackendFullRemotePath = '',
    [string]$BackendPatchRemotePath = '',
    [string]$BackendChannelUrl = '',
    [string]$BackendOutputRoot = (Join-Path $PSScriptRoot 'dist\backend-update'),
    [switch]$DeferBackendPublish,
    [switch]$ValidateOnly,
    [switch]$PublishGithub,
    [switch]$PublishModelScope,
    [string]$GithubRepo = 'maxzrb/VideoEnhancer',
    [string]$ModelScopeReleaseDataset = 'AerithDream/VideoEnhancer-Releases',
    [string]$ModelScopeModelsDataset = 'AerithDream/VideoEnhancer-Models'
)

$ErrorActionPreference = 'Stop'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$root = Split-Path -Parent $PSScriptRoot
$pluginVersionFile = Join-Path $root 'VideoEnhancerPlugin\PluginVersion.vb'
$cliProject = Join-Path $root 'cli\VideoEnhancer.csproj'

if (-not [string]::IsNullOrWhiteSpace($NotesFile)) {
    if (-not (Test-Path -LiteralPath $NotesFile -PathType Leaf)) {
        throw "Release Notes 文件不存在：$NotesFile"
    }
    if (-not [string]::IsNullOrWhiteSpace($Notes)) {
        throw '-Notes 与 -NotesFile 不能同时使用'
    }
    $Notes = [System.IO.File]::ReadAllText([System.IO.Path]::GetFullPath($NotesFile), [System.Text.Encoding]::UTF8)
}
$normalizedNotes = $Notes.Replace("`r", '')
# 文本文件通常以一个换行结束；它不是空条目，但连续两个换行仍按空行拒绝。
if ($normalizedNotes.EndsWith("`n", [System.StringComparison]::Ordinal)) {
    $normalizedNotes = $normalizedNotes.Substring(0, $normalizedNotes.Length - 1)
}
$noteLines = $normalizedNotes.Split("`n")
if ($noteLines.Count -eq 0 -or @($noteLines | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -gt 0) {
    throw 'GitHub Release Notes 不能为空或包含空行；每个更新条目必须单独一行'
}
foreach ($line in $noteLines) {
    if ($line -notmatch '^\[(更改|新增|移除)\]\S.*$') {
        throw "Release Notes 格式错误：$line；必须使用 [更改]xxxx、[新增]xxxx 或 [移除]xxxx，每条单独一行"
    }
    $remaining = $line.Substring($Matches[0].IndexOf(']') + 1)
    if ($remaining -match '\[(更改|新增|移除)\]') {
        throw "一行只能包含一个更新条目：$line"
    }
}
$Notes = $noteLines -join "`n"

foreach ($requiredValue in ([ordered]@{
    BackendBaseRoot = $BackendBaseRoot
    BackendTargetRoot = $BackendTargetRoot
    BackendBaseVersion = $BackendBaseVersion
    BackendTargetVersion = $BackendTargetVersion
}).GetEnumerator()) {
    if ([string]::IsNullOrWhiteSpace([string]$requiredValue.Value)) {
        throw "发布前必须提供 -$($requiredValue.Key)，用于严格检查后端变动"
    }
}

$backendOutputRoot = [System.IO.Path]::GetFullPath($BackendOutputRoot)
& (Join-Path $PSScriptRoot 'prepare-backend-update.ps1') `
    -BaseRoot $BackendBaseRoot -TargetRoot $BackendTargetRoot `
    -BaseVersion $BackendBaseVersion -TargetVersion $BackendTargetVersion `
    -FullArchive $BackendFullArchive -OutputRoot $backendOutputRoot `
    -FullRemotePath $BackendFullRemotePath -PatchRemotePath $BackendPatchRemotePath `
    -SentinelPaths $BackendSentinelPaths -DeferFullArchive:$DeferBackendPublish
$backendAuditPath = Join-Path $backendOutputRoot 'backend-release-audit.json'
$backendAudit = Get-Content -Raw -Encoding UTF8 $backendAuditPath | ConvertFrom-Json
if ($ValidateOnly) {
    Write-Host "RELEASE_GATES_PASS|backendChanged=$($backendAudit.hasChanges)|$backendAuditPath"
    exit 0
}

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
if (Test-Path -LiteralPath $versionRoot) { Remove-Item -LiteralPath $versionRoot -Recurse -Force }
New-Item -ItemType Directory -Force -Path $versionRoot | Out-Null

$exeSource = Join-Path $root 'videoenhancer.exe'
if (-not (Test-Path -LiteralPath $exeSource)) { throw "缺少发布文件：$exeSource" }
$packageName = "VideoEnhancer-$Version-win-x64.exe"
$packagePath = Join-Path $versionRoot $packageName
Copy-Item -LiteralPath $exeSource -Destination $packagePath -Force
$packageItem = Get-Item -LiteralPath $packagePath
$stable = [ordered]@{
    schemaVersion = 1
    channel = 'stable'
    version = $Version
    publishedAt = [DateTimeOffset]::Now.ToString('o')
    package = [ordered]@{
        path = "releases/$Version/$packageName"
        size = $packageItem.Length
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $packagePath).Hash.ToLowerInvariant()
    }
    notes = $Notes
}
$stablePath = Join-Path $distRoot 'stable.json'
[System.IO.File]::WriteAllText(
    $stablePath,
    ($stable | ConvertTo-Json -Depth 5),
    $utf8NoBom)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'modelscope-README.md') -Destination (Join-Path $distRoot 'README.md') -Force
$releaseNotesPath = Join-Path $distRoot 'release-notes.txt'
[System.IO.File]::WriteAllLines($releaseNotesPath, $noteLines, $utf8NoBom)

Write-Host "OK: $packagePath"
Write-Host "OK: $stablePath"

# 目录结构升级属于安装门禁：正式资产必须通过全新安装、旧布局迁移、占用回退和中断恢复。
& (Join-Path $PSScriptRoot 'test-installer.ps1') -Installer $packagePath
& (Join-Path $PSScriptRoot 'test-updater.ps1') -Version $Version

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

function Confirm-BackendChannel {
    if (-not $backendAudit.hasChanges) { return }
    $url = if ([string]::IsNullOrWhiteSpace($BackendChannelUrl)) {
        'https://www.modelscope.cn/datasets/' + $ModelScopeModelsDataset + '/resolve/master/Backend/channel.json'
    } else { $BackendChannelUrl }
    $remote = Invoke-RestMethod -Uri $url -TimeoutSec 30
    if ($remote.latestVersion -ne $BackendTargetVersion) {
        throw "远端 Backend channel 目标版本不是 $BackendTargetVersion：$url"
    }
    $localChannel = Get-Content -Raw -Encoding UTF8 $backendAudit.channelPath | ConvertFrom-Json
    if ($remote.full.path -ne $localChannel.full.path -or
        $remote.full.size -ne $localChannel.full.size -or
        $remote.full.sha256 -ne $localChannel.full.sha256) {
        throw '远端 Backend channel 的完整包信息与本次审计不一致'
    }
    $remotePatch = $remote.patches | Where-Object {
        $_.baseVersion -eq $BackendBaseVersion -and $_.targetVersion -eq $BackendTargetVersion
    } | Select-Object -First 1
    $localPatch = $localChannel.patches[0]
    if ($null -eq $remotePatch -or $remotePatch.path -ne $localPatch.path -or
        $remotePatch.size -ne $localPatch.size -or $remotePatch.sha256 -ne $localPatch.sha256) {
        throw '远端 Backend channel 的增量包信息与本次审计不一致'
    }
    Write-Host "OK: 远端 Backend channel 已核对（$url）"
}

# 后端有变化时，必须先上传完整包和补丁，最后上传 channel；核对成功后才允许创建 GitHub Release。
if ($backendAudit.hasChanges -and $DeferBackendPublish) {
    Write-Warning '本次发布已显式暂缓 Backend 上传：增量包和 channel 仅保存在本地，不会激活远端后端更新。'
}
elseif ($PublishModelScope -and $backendAudit.hasChanges) {
    if (-not (Get-Command modelscope -ErrorAction SilentlyContinue)) {
        throw '未找到 modelscope CLI；后端变化必须先上传增量通道，不能继续创建 Release'
    }
    $code = Invoke-Native { modelscope upload $ModelScopeModelsDataset $backendAudit.full.localPath $backendAudit.full.remotePath --repo_type dataset --no-cache }
    if ($code -ne 0) { throw 'ModelScope 后端完整包上传失败' }
    $code = Invoke-Native { modelscope upload $ModelScopeModelsDataset $backendAudit.patch.localPath $backendAudit.patch.remotePath --repo_type dataset --no-cache }
    if ($code -ne 0) { throw 'ModelScope 后端增量包上传失败' }
    $code = Invoke-Native { modelscope upload $ModelScopeModelsDataset $backendAudit.channelPath 'Backend/channel.json' --repo_type dataset --no-cache }
    if ($code -ne 0) { throw 'ModelScope 后端 channel 上传失败' }
    Confirm-BackendChannel
}
elseif ($PublishGithub -and $backendAudit.hasChanges) {
    Confirm-BackendChannel
}

if ($PublishGithub) {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw '未找到 gh CLI；请安装 GitHub CLI 并 gh auth login 后重试'
    }
    $code = Invoke-Native { gh release create "v$Version" $packagePath $stablePath --repo $GithubRepo --title "VideoEnhancer $Version" --notes-file $releaseNotesPath }
    if ($code -ne 0) { throw "gh release create v$Version 失败（$GithubRepo）" }
    Write-Host "OK: GitHub Release v$Version 已创建（$GithubRepo）"
}

if ($PublishModelScope) {
    if (-not (Get-Command modelscope -ErrorAction SilentlyContinue)) {
        throw '未找到 modelscope CLI；请先安装并 modelscope login 后重试'
    }
    $code = Invoke-Native { modelscope upload $ModelScopeReleaseDataset $distRoot --repo_type dataset }
    if ($code -ne 0) { throw "modelscope upload 失败（$ModelScopeReleaseDataset）" }
    Write-Host "OK: ModelScope 已同步（$ModelScopeReleaseDataset）"
    $code = Invoke-Native { modelscope upload $ModelScopeModelsDataset $packagePath 'Plugin/videoenhancer.exe' --repo_type dataset --no-cache }
    if ($code -ne 0) { throw "ModelScope 插件 EXE 兜底上传失败（$ModelScopeModelsDataset）" }
    Write-Host "OK: ModelScope 模型页插件 EXE 已同步（$ModelScopeModelsDataset/Plugin/videoenhancer.exe）"
}

if (-not $PublishGithub -and -not $PublishModelScope) {
    Write-Host "ModelScope 上传目录：$distRoot"
    Write-Host '手动发布命令：'
    Write-Host "  gh release create v$Version `"$packagePath`" `"$stablePath`" --repo $GithubRepo --title `"VideoEnhancer $Version`" --notes-file `"$releaseNotesPath`""
    Write-Host "  modelscope upload $ModelScopeReleaseDataset `"$distRoot`" --repo_type dataset"
    Write-Host "  modelscope upload $ModelScopeModelsDataset `"$packagePath`" Plugin/videoenhancer.exe --repo_type dataset --no-cache"
}
