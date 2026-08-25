param(
    [Parameter(Mandatory = $true)]
    [string]$BaseRoot,
    [Parameter(Mandatory = $true)]
    [string]$TargetRoot,
    [Parameter(Mandatory = $true)]
    [string]$BaseVersion,
    [Parameter(Mandatory = $true)]
    [string]$TargetVersion,
    [Parameter(Mandatory = $true)]
    [string]$OutputArchive,
    [string]$SevenZip = '7z',
    [switch]$DisablePythonProbe
)

$ErrorActionPreference = 'Stop'
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$basePath = [System.IO.Path]::GetFullPath($BaseRoot).TrimEnd('\', '/')
$targetPath = [System.IO.Path]::GetFullPath($TargetRoot).TrimEnd('\', '/')
$outputPath = [System.IO.Path]::GetFullPath($OutputArchive)

if (-not (Test-Path -LiteralPath $basePath -PathType Container)) {
    throw "基础后端目录不存在：$basePath"
}
if (-not (Test-Path -LiteralPath $targetPath -PathType Container)) {
    throw "目标后端目录不存在：$targetPath"
}
if ([string]::IsNullOrWhiteSpace($BaseVersion) -or [string]::IsNullOrWhiteSpace($TargetVersion)) {
    throw '基础版本和目标版本不能为空'
}
if ($BaseVersion -eq $TargetVersion) {
    throw '基础版本和目标版本不能相同'
}

function Get-RelativeFileMap([string]$root) {
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
        $map[$relative] = $_
    }
    return $map
}

function Get-Sha256([string]$path) {
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
}

$baseFiles = Get-RelativeFileMap $basePath
$targetFiles = Get-RelativeFileMap $targetPath
$stagingParent = Join-Path ([System.IO.Path]::GetDirectoryName($outputPath)) '.backend-patch-staging'
$staging = Join-Path $stagingParent ([guid]::NewGuid().ToString('N'))
$payloadRoot = Join-Path $staging 'payload'
New-Item -ItemType Directory -Force -Path $payloadRoot | Out-Null

try {
    $operations = [System.Collections.Generic.List[object]]::new()
    foreach ($relative in ($targetFiles.Keys | Sort-Object)) {
        $targetFile = $targetFiles[$relative]
        $newHash = Get-Sha256 $targetFile.FullName
        if ($baseFiles.ContainsKey($relative)) {
            $baseFile = $baseFiles[$relative]
            $oldHash = Get-Sha256 $baseFile.FullName
            if ($oldHash -eq $newHash) { continue }
            $action = 'replace'
        }
        else {
            $oldHash = ''
            $action = 'add'
        }

        $payloadPath = Join-Path $payloadRoot $relative.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetDirectoryName($payloadPath)) | Out-Null
        Copy-Item -LiteralPath $targetFile.FullName -Destination $payloadPath -Force
        $operations.Add([ordered]@{
            action = $action
            path = $relative
            oldSha256 = $oldHash
            newSha256 = $newHash
            size = [long]$targetFile.Length
        })
    }

    foreach ($relative in ($baseFiles.Keys | Sort-Object)) {
        if ($targetFiles.ContainsKey($relative)) { continue }
        $baseFile = $baseFiles[$relative]
        $operations.Add([ordered]@{
            action = 'delete'
            path = $relative
            oldSha256 = Get-Sha256 $baseFile.FullName
            newSha256 = ''
            size = 0
        })
    }

    $manifest = [ordered]@{
        schemaVersion = 1
        baseVersion = $BaseVersion
        targetVersion = $TargetVersion
        createdAt = [DateTimeOffset]::Now.ToString('o')
        pythonProbe = -not $DisablePythonProbe
        healthCheckFiles = @('python/python.exe', 'backend/rve-backend.py')
        operations = $operations
    }
    [System.IO.File]::WriteAllText(
        (Join-Path $staging 'backend-patch.json'),
        ($manifest | ConvertTo-Json -Depth 6),
        $utf8NoBom)

    New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetDirectoryName($outputPath)) | Out-Null
    if (Test-Path -LiteralPath $outputPath) {
        Remove-Item -LiteralPath $outputPath -Force
    }
    & $SevenZip a -t7z -mx=9 $outputPath (Join-Path $staging '*') | Out-Host
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $outputPath)) {
        throw "7-Zip 创建补丁失败，退出码：$LASTEXITCODE"
    }

    $item = Get-Item -LiteralPath $outputPath
    $hash = Get-Sha256 $outputPath
    Write-Output "PATCH_COMPLETE|$outputPath|$($item.Length)|$hash|$($operations.Count)"
}
finally {
    if (Test-Path -LiteralPath $staging) {
        Remove-Item -LiteralPath $staging -Recurse -Force
    }
}
