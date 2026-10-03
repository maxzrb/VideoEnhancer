param(
    # 留空时自动读取 VideoEnhancerPlugin.vbproj 的 Version。
    [string]$Version = '',
    [string]$Notes = '',
    [string]$NotesFile = '',
    [string]$ArtifactsRoot = $env:VIDEOENHANCER_ARTIFACTS_DIR,
    [string]$BackendBaseRoot = '',
    [string]$BackendTargetRoot = '',
    [string]$BackendBaseVersion = '',
    [string]$BackendTargetVersion = '',
    [string]$BackendFullArchive = '',
    [string[]]$BackendSentinelPaths = @(),
    [string]$BackendFullRemotePath = '',
    [string]$BackendPatchRemotePath = '',
    [string]$BackendChannelUrl = '',
    [string]$BackendPreviousChannel = '',
    [string]$BackendOutputRoot = (Join-Path $PSScriptRoot 'dist\backend-update'),
    [string]$ArchiveTool = '',
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
if ([string]::IsNullOrWhiteSpace($ArtifactsRoot)) { $ArtifactsRoot = 'Artifacts' }
if (-not [IO.Path]::IsPathRooted($ArtifactsRoot)) { $ArtifactsRoot = Join-Path $root $ArtifactsRoot }
$ArtifactsRoot = [IO.Path]::GetFullPath($ArtifactsRoot)
$pluginProject = Join-Path $root 'VideoEnhancerPlugin\VideoEnhancerPlugin.vbproj'
$cliProject = Join-Path $root 'cli\VideoEnhancer.csproj'
$solution = Join-Path $root 'VideoEnhancer.slnx'

function Get-ProjectVersion([string]$projectPath) {
    $document = [System.Xml.XmlDocument]::new()
    $document.Load($projectPath)
    $nodes = @($document.SelectNodes('/Project/PropertyGroup/Version'))
    if ($nodes.Count -ne 1 -or [string]::IsNullOrWhiteSpace($nodes[0].InnerText)) {
        throw "项目必须声明且只能声明一个 Version：$projectPath"
    }
    return $nodes[0].InnerText.Trim()
}

function Get-ProjectProperty([string]$projectPath, [string]$name) {
    $document = [System.Xml.XmlDocument]::new()
    $document.Load($projectPath)
    $nodes = @($document.SelectNodes("/Project/PropertyGroup/$name"))
    if ($nodes.Count -ne 1 -or [string]::IsNullOrWhiteSpace($nodes[0].InnerText)) {
        throw "项目必须声明且只能声明一个 $name：$projectPath"
    }
    return $nodes[0].InnerText.Trim()
}

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
for ($index = 0; $index -lt $noteLines.Count; $index++) {
    $line = $noteLines[$index]
    # 最后一行可放一个 Markdown 标题，用于提示用户选择安装器。
    if ($index -eq $noteLines.Count - 1 -and $line -match '^## 下载提示：请下载 VideoEnhancerInstaller-\d+\.\d+\.\d+-win-x64\.exe 安装或更新插件$') {
        continue
    }
    if ($line -notmatch '^\[(更改|新增|移除)\]\S.*$') {
        throw "Release Notes 格式错误：$line；必须使用 [更改]xxxx、[新增]xxxx 或 [移除]xxxx，每条单独一行；末行可用指定的下载提示标题"
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

# 正式发布先构建一次托管归档工具，后端门禁不再依赖系统安装的 7-Zip。
if (-not $ValidateOnly -and [string]::IsNullOrWhiteSpace($ArchiveTool)) {
    $buildArguments = @('build', $solution, '-c', 'Release')
    & dotnet @buildArguments
    if ($LASTEXITCODE -ne 0) { throw '托管归档工具构建失败' }
    $ArchiveTool = Join-Path $root 'cli\bin\Release\net10.0-windows\win-x64\videoenhancer.exe'
}

$backendOutputRoot = [System.IO.Path]::GetFullPath($BackendOutputRoot)
$previousChannelSource = $BackendPreviousChannel
if ([string]::IsNullOrWhiteSpace($previousChannelSource) -and ($PublishGithub -or $PublishModelScope)) {
    $previousChannelSource = if ([string]::IsNullOrWhiteSpace($BackendChannelUrl)) {
        'https://www.modelscope.cn/datasets/' + $ModelScopeModelsDataset + '/resolve/master/Backend/channel.json'
    } else { $BackendChannelUrl }
}
& (Join-Path $PSScriptRoot 'prepare-backend-update.ps1') `
    -BaseRoot $BackendBaseRoot -TargetRoot $BackendTargetRoot `
    -BaseVersion $BackendBaseVersion -TargetVersion $BackendTargetVersion `
    -FullArchive $BackendFullArchive -OutputRoot $backendOutputRoot `
    -FullRemotePath $BackendFullRemotePath -PatchRemotePath $BackendPatchRemotePath `
    -SentinelPaths $BackendSentinelPaths -PreviousChannel $previousChannelSource `
    -DeferFullArchive:$DeferBackendPublish `
    -ArchiveTool $ArchiveTool
$backendAuditPath = Join-Path $backendOutputRoot 'backend-release-audit.json'
$backendAudit = Get-Content -Raw -Encoding UTF8 $backendAuditPath | ConvertFrom-Json
if ($ValidateOnly) {
    Write-Host "RELEASE_GATES_PASS|backendChanged=$($backendAudit.hasChanges)|$backendAuditPath"
    exit 0
}

$sourceVersion = Get-ProjectVersion $pluginProject
$cliSourceVersion = Get-ProjectVersion $cliProject
$aria2NextVersion = Get-ProjectProperty $cliProject 'Aria2NextVersion'
$aria2NextSourceSha256 = Get-ProjectProperty $cliProject 'Aria2NextSourceSha256'
if (-not $Version) { $Version = $sourceVersion }

if ($sourceVersion -ne $Version) {
    throw "VideoEnhancerPlugin.vbproj 的 Version 为 $sourceVersion，与发布版本 $Version 不一致"
}
if ($cliSourceVersion -ne $Version) {
    throw "VideoEnhancer.csproj 的 Version 为 $cliSourceVersion，与发布版本 $Version 不一致"
}

$publishArguments = @('publish', $solution, '-c', 'Release', "-p:ArtifactsDirectory=$ArtifactsRoot")
& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) { throw '插件与 CLI 发布失败' }

# 端到端校验：CLI 版本号运行时读自 csproj 程序集元数据，必须与发布版本一致。
$cliExe = Join-Path $root 'cli\bin\Release\net10.0-windows\win-x64\publish\videoenhancer.exe'
if (-not (Test-Path -LiteralPath $cliExe -PathType Leaf)) {
    throw "缺少 CLI 发布文件：$cliExe"
}
$cliVersion = (& $cliExe --version) | Select-Object -First 1
if (("$cliVersion").Trim() -ne $Version) {
    throw "videoenhancer.exe 报告版本 '$cliVersion'，与发布版本 $Version 不一致"
}

$distRoot = Join-Path $PSScriptRoot 'dist\modelscope'
$versionRoot = Join-Path $distRoot (Join-Path 'releases' $Version)
if (Test-Path -LiteralPath $versionRoot) { Remove-Item -LiteralPath $versionRoot -Recurse -Force }
New-Item -ItemType Directory -Force -Path $versionRoot | Out-Null

# GPL 二进制与精确对应源码归档必须出现在同一次正式发布中，并由固定哈希门禁。
$aria2NextSourceName = "aria2-next-$aria2NextVersion-source.tar.gz"
$aria2NextSourcePath = Join-Path $versionRoot $aria2NextSourceName
$aria2NextSourceUrl = "https://github.com/AnInsomniacy/aria2-next/archive/refs/tags/v$aria2NextVersion.tar.gz"
Invoke-WebRequest -UseBasicParsing -Uri $aria2NextSourceUrl -OutFile $aria2NextSourcePath
$actualAria2NextSourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $aria2NextSourcePath).Hash
if ($actualAria2NextSourceHash -ne $aria2NextSourceSha256) {
    throw "aria2-next 对应源码校验失败：期望 $aria2NextSourceSha256，实际 $actualAria2NextSourceHash"
}

$exeSource = Join-Path $artifactsRoot 'videoenhancer.exe'
if (-not (Test-Path -LiteralPath $exeSource)) { throw "缺少发布文件：$exeSource" }
$packageName = "VideoEnhancer-$Version-win-x64.exe"
$packagePath = Join-Path $versionRoot $packageName
Copy-Item -LiteralPath $exeSource -Destination $packagePath -Force
$installerSource = Join-Path $artifactsRoot 'VideoEnhancerInstaller.exe'
if (-not (Test-Path -LiteralPath $installerSource -PathType Leaf)) { throw "缺少首次安装程序：$installerSource" }
$installerName = "VideoEnhancerInstaller-$Version-win-x64.exe"
$installerPath = Join-Path $versionRoot $installerName
Copy-Item -LiteralPath $installerSource -Destination $installerPath -Force
$manualSource = Join-Path $artifactsRoot 'VideoEnhancer.zip'
if (-not (Test-Path -LiteralPath $manualSource -PathType Leaf)) {
    throw "缺少手动安装包：$manualSource"
}
$manualName = "VideoEnhancer-$Version-manual-install.zip"
$manualPath = Join-Path $versionRoot $manualName
Copy-Item -LiteralPath $manualSource -Destination $manualPath -Force
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
Write-Host "OK: $installerPath"
Write-Host "OK: $manualPath"
Write-Host "OK: $aria2NextSourcePath"
Write-Host "OK: $stablePath"

# 首次安装程序与运行时更新包分别验证。
& (Join-Path $PSScriptRoot 'test-installer.ps1') -Installer $installerPath
& (Join-Path $PSScriptRoot 'test-updater.ps1') -Version $Version -Package $packagePath

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
    if (@($remote.patches).Count -ne @($localChannel.patches).Count -or
        @($remote.legacyBaselines).Count -ne @($localChannel.legacyBaselines).Count) {
        throw '远端 Backend channel 的历史补丁或旧版哨兵数量与本次审计不一致'
    }
    for ($index = 0; $index -lt @($localChannel.patches).Count; $index++) {
        $remotePatch = $remote.patches[$index]
        $localPatch = $localChannel.patches[$index]
        if ($remotePatch.baseVersion -ne $localPatch.baseVersion -or
            $remotePatch.targetVersion -ne $localPatch.targetVersion -or
            $remotePatch.path -ne $localPatch.path -or
            $remotePatch.size -ne $localPatch.size -or
            $remotePatch.sha256 -ne $localPatch.sha256) {
            throw '远端 Backend channel 的增量链与本次审计不一致'
        }
    }
    for ($index = 0; $index -lt @($localChannel.legacyBaselines).Count; $index++) {
        $remoteBaseline = $remote.legacyBaselines[$index] | ConvertTo-Json -Depth 5 -Compress
        $localBaseline = $localChannel.legacyBaselines[$index] | ConvertTo-Json -Depth 5 -Compress
        if ($remoteBaseline -ne $localBaseline) {
            throw '远端 Backend channel 的旧版哨兵与本次审计不一致'
        }
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
    $code = Invoke-Native { gh release create "v$Version" $packagePath $installerPath $manualPath $aria2NextSourcePath $stablePath --repo $GithubRepo --title "VideoEnhancer $Version" --notes-file $releaseNotesPath }
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
    Write-Host "  gh release create v$Version `"$packagePath`" `"$installerPath`" `"$manualPath`" `"$aria2NextSourcePath`" `"$stablePath`" --repo $GithubRepo --title `"VideoEnhancer $Version`" --notes-file `"$releaseNotesPath`""
    Write-Host "  modelscope upload $ModelScopeReleaseDataset `"$distRoot`" --repo_type dataset"
    Write-Host "  modelscope upload $ModelScopeModelsDataset `"$packagePath`" Plugin/videoenhancer.exe --repo_type dataset --no-cache"
}
