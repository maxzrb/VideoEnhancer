param(
    [Parameter(Mandatory = $true)]
    [string]$BaseRoot,
    [Parameter(Mandatory = $true)]
    [string]$TargetRoot,
    [Parameter(Mandatory = $true)]
    [string]$BaseVersion,
    [Parameter(Mandatory = $true)]
    [string]$TargetVersion,
    [string]$FullArchive = '',
    [string]$OutputRoot = (Join-Path $PSScriptRoot 'dist\backend-update'),
    [string]$FullRemotePath = '',
    [string]$PatchRemotePath = '',
    [string[]]$SentinelPaths = @(),
    [switch]$DeferFullArchive,
    [string]$SevenZip = '7z'
)

$ErrorActionPreference = 'Stop'
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$basePath = [System.IO.Path]::GetFullPath($BaseRoot).TrimEnd('\', '/')
$targetPath = [System.IO.Path]::GetFullPath($TargetRoot).TrimEnd('\', '/')
$outputPath = [System.IO.Path]::GetFullPath($OutputRoot)

function Get-Sha256([string]$path) {
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
}

function Get-BackendFileMap([string]$root) {
    if (-not (Test-Path -LiteralPath $root -PathType Container)) {
        throw "后端目录不存在：$root"
    }
    $map = [System.Collections.Generic.Dictionary[string, object]]::new([System.StringComparer]::OrdinalIgnoreCase)
    Get-ChildItem -LiteralPath $root -Recurse -File | ForEach-Object {
        $relative = [System.IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/')
        if ($relative -eq '.videoenhancer-backend.json' -or
            $relative -match '^python(?:_\d{8})?\.7z(?:\.part|\.aria2)?$' -or
            $relative -match '^backend/cache/' -or
            $relative -match '(^|/)__pycache__(/|$)' -or
            $relative.EndsWith('.pyc', [System.StringComparison]::OrdinalIgnoreCase) -or
            $relative.EndsWith('.pyo', [System.StringComparison]::OrdinalIgnoreCase)) {
            return
        }
        $map[$relative] = [ordered]@{
            FullName = $_.FullName
            Length = [long]$_.Length
            Sha256 = Get-Sha256 $_.FullName
        }
    }
    return $map
}

function Assert-RemotePath([string]$value, [string]$name) {
    if ([string]::IsNullOrWhiteSpace($value) -or
        $value.StartsWith('/') -or $value.StartsWith('\') -or
        $value -match '(^|/|\\)\.\.(/|\\|$)') {
        throw "$name 必须是 Backend 下的安全仓库相对路径：$value"
    }
    $normalized = $value.Replace('\', '/')
    if (-not $normalized.StartsWith('Backend/', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$name 必须位于 Backend/ 下：$value"
    }
}

$baseFiles = Get-BackendFileMap $basePath
$targetFiles = Get-BackendFileMap $targetPath
$added = [System.Collections.Generic.List[string]]::new()
$replaced = [System.Collections.Generic.List[string]]::new()
$deleted = [System.Collections.Generic.List[string]]::new()

foreach ($relative in $targetFiles.Keys) {
    if (-not $baseFiles.ContainsKey($relative)) {
        $added.Add($relative)
    }
    elseif ($baseFiles[$relative].Sha256 -ne $targetFiles[$relative].Sha256) {
        $replaced.Add($relative)
    }
}
foreach ($relative in $baseFiles.Keys) {
    if (-not $targetFiles.ContainsKey($relative)) { $deleted.Add($relative) }
}

$hasChanges = ($added.Count + $replaced.Count + $deleted.Count) -gt 0
New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
$auditPath = Join-Path $outputPath 'backend-release-audit.json'

$audit = [ordered]@{
    schemaVersion = 1
    checkedAt = [DateTimeOffset]::Now.ToString('o')
    baseVersion = $BaseVersion
    targetVersion = $TargetVersion
    baseRoot = $basePath
    targetRoot = $targetPath
    hasChanges = $hasChanges
    counts = [ordered]@{ add = $added.Count; replace = $replaced.Count; delete = $deleted.Count }
    changes = [ordered]@{
        add = @($added | Sort-Object)
        replace = @($replaced | Sort-Object)
        delete = @($deleted | Sort-Object)
    }
}

if (-not $hasChanges) {
    if ($BaseVersion -ne $TargetVersion) {
        throw '后端文件没有变化，但基础版本和目标版本不同；禁止生成空版本更新'
    }
    [System.IO.File]::WriteAllText($auditPath, ($audit | ConvertTo-Json -Depth 8), $utf8NoBom)
    Write-Output "BACKEND_AUDIT_COMPLETE|UNCHANGED|$auditPath"
    return
}

if ($BaseVersion -eq $TargetVersion) {
    throw '检测到后端文件变化，但后端版本未递增'
}
if (-not $DeferFullArchive -and
    ([string]::IsNullOrWhiteSpace($FullArchive) -or -not (Test-Path -LiteralPath $FullArchive -PathType Leaf))) {
    throw '检测到后端变化，必须通过 -FullArchive 提供目标版本完整后端包'
}
if ($SentinelPaths.Count -eq 0) {
    throw '检测到后端变化，必须通过 -SentinelPaths 指定至少一个稳定旧版哨兵文件'
}

$fullPath = ''
if (-not $DeferFullArchive) {
    $fullPath = [System.IO.Path]::GetFullPath($FullArchive)
    if ([string]::IsNullOrWhiteSpace($FullRemotePath)) {
        $FullRemotePath = 'Backend/' + [System.IO.Path]::GetFileName($fullPath)
    }
    Assert-RemotePath $FullRemotePath 'FullRemotePath'
}
if ([string]::IsNullOrWhiteSpace($PatchRemotePath)) {
    $safeBase = $BaseVersion -replace '[^0-9A-Za-z._-]', '_'
    $safeTarget = $TargetVersion -replace '[^0-9A-Za-z._-]', '_'
    $PatchRemotePath = "Backend/patches/${safeBase}_to_${safeTarget}.7z"
}
Assert-RemotePath $PatchRemotePath 'PatchRemotePath'

# 完整包也必须与目标目录逐文件一致，避免补丁正确但全量修复包仍是旧版本。
if (-not $DeferFullArchive) {
    $fullValidationRoot = Join-Path $outputPath ('full-validation-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $fullValidationRoot | Out-Null
    try {
        & $SevenZip x $fullPath "-o$fullValidationRoot" -y | Out-Host
        if ($LASTEXITCODE -ne 0) { throw '完整后端包解压验证失败' }
        $fullContentRoot = if (Test-Path -LiteralPath (Join-Path $fullValidationRoot 'python') -PathType Container) {
            Join-Path $fullValidationRoot 'python'
        } else { $fullValidationRoot }
        $fullFiles = Get-BackendFileMap $fullContentRoot
        if ($fullFiles.Count -ne $targetFiles.Count) {
            throw "完整后端包文件数与目标目录不一致：包内 $($fullFiles.Count)，目标 $($targetFiles.Count)"
        }
        foreach ($relative in $targetFiles.Keys) {
            if (-not $fullFiles.ContainsKey($relative) -or
                $fullFiles[$relative].Length -ne $targetFiles[$relative].Length -or
                $fullFiles[$relative].Sha256 -ne $targetFiles[$relative].Sha256) {
                throw "完整后端包与目标目录内容不一致：$relative"
            }
        }
    }
    finally {
        if (Test-Path -LiteralPath $fullValidationRoot) {
            Remove-Item -LiteralPath $fullValidationRoot -Recurse -Force
        }
    }
}

$sentinels = [System.Collections.Generic.List[object]]::new()
foreach ($rawSentinel in $SentinelPaths) {
    $relative = $rawSentinel.Replace('\', '/').Trim('/')
    if (-not $baseFiles.ContainsKey($relative)) {
        throw "旧版哨兵文件不存在：$relative"
    }
    if ($added.Contains($relative) -or $replaced.Contains($relative) -or $deleted.Contains($relative)) {
        throw "哨兵文件本次发生变化，不能稳定识别旧版基线：$relative"
    }
    $sentinels.Add([ordered]@{ path = $relative; sha256 = $baseFiles[$relative].Sha256 })
}

$patchName = [System.IO.Path]::GetFileName($PatchRemotePath)
$patchPath = Join-Path $outputPath $patchName
& (Join-Path $PSScriptRoot 'build-backend-patch.ps1') `
    -BaseRoot $basePath -TargetRoot $targetPath `
    -BaseVersion $BaseVersion -TargetVersion $TargetVersion `
    -OutputArchive $patchPath -SevenZip $SevenZip
if ($LASTEXITCODE -ne 0) { throw '后端增量包生成失败' }

$patchItem = Get-Item -LiteralPath $patchPath
$audit['patch'] = [ordered]@{
    localPath = $patchPath
    remotePath = $PatchRemotePath
    size = [long]$patchItem.Length
    sha256 = Get-Sha256 $patchPath
}
if ($DeferFullArchive) {
    $audit['backendDeferred'] = $true
    [System.IO.File]::WriteAllText($auditPath, ($audit | ConvertTo-Json -Depth 8), $utf8NoBom)
    Write-Output "BACKEND_AUDIT_COMPLETE|CHANGED_DEFERRED|$auditPath"
    return
}

$fullItem = Get-Item -LiteralPath $fullPath
$channel = [ordered]@{
    schemaVersion = 1
    latestVersion = $TargetVersion
    full = [ordered]@{
        path = $FullRemotePath
        size = [long]$fullItem.Length
        sha256 = Get-Sha256 $fullPath
    }
    patches = @([ordered]@{
        baseVersion = $BaseVersion
        targetVersion = $TargetVersion
        path = $PatchRemotePath
        size = [long]$patchItem.Length
        sha256 = Get-Sha256 $patchPath
    })
    legacyBaselines = @([ordered]@{
        version = $BaseVersion
        sentinels = $sentinels
    })
}
$channelPath = Join-Path $outputPath 'channel.json'
[System.IO.File]::WriteAllText($channelPath, ($channel | ConvertTo-Json -Depth 8), $utf8NoBom)

$audit['full'] = [ordered]@{
    localPath = $fullPath
    remotePath = $FullRemotePath
    size = [long]$fullItem.Length
    sha256 = Get-Sha256 $fullPath
}
$audit['channelPath'] = $channelPath
[System.IO.File]::WriteAllText($auditPath, ($audit | ConvertTo-Json -Depth 8), $utf8NoBom)
Write-Output "BACKEND_AUDIT_COMPLETE|CHANGED|$auditPath"
