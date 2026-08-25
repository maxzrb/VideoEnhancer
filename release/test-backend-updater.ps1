param(
    [string]$BuildOutput = (Join-Path $PSScriptRoot '..\cli\bin\Release\net10.0\win-x64'),
    [string]$SevenZip = '7z'
)

$ErrorActionPreference = 'Stop'
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('videoenhancer-backend-tests-' + [guid]::NewGuid().ToString('N'))
$appRoot = Join-Path $testRoot 'app'

function Write-TestText([string]$path, [string]$content) {
    New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetDirectoryName($path)) | Out-Null
    [System.IO.File]::WriteAllText($path, $content, $utf8NoBom)
}

function New-BackendTree([string]$root, [string]$backendText, [switch]$WithDeletedFile) {
    Write-TestText (Join-Path $root 'python\python.exe') 'fake-python'
    Write-TestText (Join-Path $root 'backend\rve-backend.py') $backendText
    if ($WithDeletedFile) {
        Write-TestText (Join-Path $root 'backend\delete-me.py') 'delete'
    }
}

function Set-CorePath([string]$coreRoot) {
    Write-TestText (Join-Path $appRoot 'videoenhancer.ini') ('core-path="' + $coreRoot + '"')
}

function Invoke-VideoEnhancer([string[]]$arguments) {
    $commandOutput = & (Join-Path $appRoot 'videoenhancer.exe') @arguments
    $commandExitCode = $LASTEXITCODE
    $commandOutput | Out-Host
    return $commandExitCode
}

function Assert-True([bool]$condition, [string]$message) {
    if (-not $condition) { throw "断言失败：$message" }
}

function Get-Sha256([string]$path) {
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
}

try {
    if (-not (Test-Path -LiteralPath (Join-Path $BuildOutput 'videoenhancer.exe'))) {
        throw "找不到已构建 CLI：$BuildOutput"
    }
    New-Item -ItemType Directory -Force -Path $appRoot | Out-Null
    Copy-Item -Path (Join-Path $BuildOutput '*') -Destination $appRoot -Recurse -Force

    # 场景 1：旧版哨兵识别、状态计算、在线通道式增量更新。
    $success = Join-Path $testRoot 'success'
    $successCore = Join-Path $success 'core'
    $successPython = Join-Path $successCore 'python'
    $successBase = Join-Path $success 'base'
    $successTarget = Join-Path $success 'target'
    New-BackendTree $successPython 'old' -WithDeletedFile
    New-BackendTree $successBase 'old' -WithDeletedFile
    New-BackendTree $successTarget 'new'
    Write-TestText (Join-Path $successTarget 'backend\add-me.py') 'add'
    $successPatch = Join-Path $success 'patch.7z'
    & (Join-Path $PSScriptRoot 'build-backend-patch.ps1') `
        -BaseRoot $successBase -TargetRoot $successTarget `
        -BaseVersion 'base-1' -TargetVersion 'target-2' `
        -OutputArchive $successPatch -SevenZip $SevenZip -DisablePythonProbe | Out-Null
    $patchItem = Get-Item -LiteralPath $successPatch
    $channel = [ordered]@{
        schemaVersion = 1
        latestVersion = 'target-2'
        full = [ordered]@{ path = 'full.7z'; size = 999; sha256 = '' }
        patches = @([ordered]@{
            baseVersion = 'base-1'; targetVersion = 'target-2'; path = 'patch.7z'
            size = [long]$patchItem.Length; sha256 = Get-Sha256 $successPatch
        })
        legacyBaselines = @([ordered]@{
            version = 'base-1'
            sentinels = @([ordered]@{
                path = 'backend/rve-backend.py'
                sha256 = Get-Sha256 (Join-Path $successBase 'backend\rve-backend.py')
            })
        })
    }
    $channelPath = Join-Path $success 'channel.json'
    Write-TestText $channelPath ($channel | ConvertTo-Json -Depth 8)
    Set-CorePath $successCore
    $statusOutput = & (Join-Path $appRoot 'videoenhancer.exe') --backend-status --backend-channel $channelPath --json
    Assert-True ($LASTEXITCODE -eq 0) '旧版后端状态检查应成功'
    $status = $statusOutput | Select-Object -Last 1 | ConvertFrom-Json
    Assert-True ($status.state -eq 'legacy-update-available') '应通过哨兵识别旧版基线'
    Assert-True ($status.mode -eq 'patch') '应选择增量补丁'
    Assert-True ($status.downloadSize -eq $patchItem.Length) '应报告补丁实际下载大小'
    $exitCode = Invoke-VideoEnhancer @('--update-backend', '--backend-channel', $channelPath)
    Assert-True ($exitCode -eq 0) '增量更新应成功'
    Assert-True ((Get-Content -Raw -Encoding UTF8 (Join-Path $successPython 'backend\rve-backend.py')) -eq 'new') '替换文件内容错误'
    Assert-True ((Get-Content -Raw -Encoding UTF8 (Join-Path $successPython 'backend\add-me.py')) -eq 'add') '新增文件内容错误'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $successPython 'backend\delete-me.py'))) '删除操作未生效'
    $marker = Get-Content -Raw -Encoding UTF8 (Join-Path $successPython '.videoenhancer-backend.json') | ConvertFrom-Json
    Assert-True ($marker.version -eq 'target-2') '版本标记未提交'

    # 场景 2：用户改过旧文件时必须因 SHA 冲突停止，不能覆盖。
    $conflict = Join-Path $testRoot 'conflict'
    $conflictCore = Join-Path $conflict 'core'
    $conflictPython = Join-Path $conflictCore 'python'
    New-BackendTree $conflictPython 'locally-modified' -WithDeletedFile
    Set-CorePath $conflictCore
    $exitCode = Invoke-VideoEnhancer @('--apply-backend-patch', $successPatch)
    Assert-True ($exitCode -ne 0) 'SHA 冲突必须失败'
    Assert-True ((Get-Content -Raw -Encoding UTF8 (Join-Path $conflictPython 'backend\rve-backend.py')) -eq 'locally-modified') 'SHA 冲突后文件被错误覆盖'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $conflictPython '.videoenhancer-backend.json'))) '失败更新不应写版本标记'

    # 场景 3：应用后健康检查失败，替换和删除都应自动回滚。
    $rollback = Join-Path $testRoot 'rollback'
    $rollbackCore = Join-Path $rollback 'core'
    $rollbackPython = Join-Path $rollbackCore 'python'
    $rollbackBase = Join-Path $rollback 'base'
    $rollbackTarget = Join-Path $rollback 'target'
    New-BackendTree $rollbackPython 'old'
    New-BackendTree $rollbackBase 'old'
    Write-TestText (Join-Path $rollbackTarget 'python\python.exe') 'fake-python'
    $rollbackPatch = Join-Path $rollback 'health-failure.7z'
    & (Join-Path $PSScriptRoot 'build-backend-patch.ps1') `
        -BaseRoot $rollbackBase -TargetRoot $rollbackTarget `
        -BaseVersion 'base-1' -TargetVersion 'broken-2' `
        -OutputArchive $rollbackPatch -SevenZip $SevenZip -DisablePythonProbe | Out-Null
    Set-CorePath $rollbackCore
    $exitCode = Invoke-VideoEnhancer @('--apply-backend-patch', $rollbackPatch)
    Assert-True ($exitCode -ne 0) '健康检查失败必须返回非零'
    Assert-True ((Get-Content -Raw -Encoding UTF8 (Join-Path $rollbackPython 'backend\rve-backend.py')) -eq 'old') '健康检查失败后未恢复原文件'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $rollbackPython '.videoenhancer-backend.json'))) '回滚后不应留下版本标记'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $rollbackCore '.videoenhancer-backend-update\pending.json'))) '回滚后不应留下 pending 日志'

    # 场景 4：模拟进程在变更中断，下次启动应先按 pending 日志恢复。
    $recovery = Join-Path $testRoot 'recovery'
    $recoveryCore = Join-Path $recovery 'core'
    $recoveryPython = Join-Path $recoveryCore 'python'
    New-BackendTree $recoveryPython 'partially-updated'
    Write-TestText (Join-Path $recoveryPython 'backend\added-during-update.py') 'partial-add'
    $stateRoot = Join-Path $recoveryCore '.videoenhancer-backend-update'
    $transaction = Join-Path $stateRoot 'transaction-test'
    Write-TestText (Join-Path $transaction 'backup\backend\rve-backend.py') 'old'
    $journal = [ordered]@{
        schemaVersion = 1
        transactionDirectory = $transaction
        backedUpPaths = @('backend/rve-backend.py')
        addedPaths = @('backend/added-during-update.py')
        oldMarkerBase64 = ''
    }
    Write-TestText (Join-Path $stateRoot 'pending.json') ($journal | ConvertTo-Json -Depth 5)
    Set-CorePath $recoveryCore
    $exitCode = Invoke-VideoEnhancer @('--apply-backend-patch', (Join-Path $recovery 'missing.7z'))
    Assert-True ($exitCode -ne 0) '恢复后缺失补丁仍应返回非零'
    Assert-True ((Get-Content -Raw -Encoding UTF8 (Join-Path $recoveryPython 'backend\rve-backend.py')) -eq 'old') '启动恢复未还原备份'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $recoveryPython 'backend\added-during-update.py'))) '启动恢复未删除事务新增文件'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $stateRoot 'pending.json'))) '启动恢复未清除 pending 日志'

    # 场景 5：无法识别的旧后端走完整包，先在暂存区探测，再整体切换目录。
    $full = Join-Path $testRoot 'full'
    $fullCore = Join-Path $full 'core'
    $fullPython = Join-Path $fullCore 'python'
    New-BackendTree $fullPython 'unknown-old'
    Write-TestText (Join-Path $fullPython 'backend\obsolete.py') 'obsolete'
    $fullPackageRoot = Join-Path $full 'package\python'
    Write-TestText (Join-Path $fullPackageRoot 'backend\rve-backend.py') 'fresh'
    $probeExe = (Get-Command python -ErrorAction Stop).Source
    $probeDirectory = Split-Path -Parent $probeExe
    New-Item -ItemType Directory -Force -Path (Join-Path $fullPackageRoot 'python') | Out-Null
    Copy-Item -LiteralPath $probeExe -Destination (Join-Path $fullPackageRoot 'python\python.exe')
    Get-ChildItem -LiteralPath $probeDirectory -File | Where-Object {
        $_.Name -match '^python\d+\.dll$' -or $_.Name -match '^vcruntime\d+.*\.dll$'
    } | Copy-Item -Destination (Join-Path $fullPackageRoot 'python')
    $fullArchive = Join-Path $full 'full.7z'
    & $SevenZip a -t7z -mx=1 $fullArchive (Join-Path $full 'package\*') | Out-Null
    if ($LASTEXITCODE -ne 0) { throw '完整包测试归档创建失败' }
    $fullItem = Get-Item -LiteralPath $fullArchive
    $fullChannel = [ordered]@{
        schemaVersion = 1
        latestVersion = 'full-2'
        full = [ordered]@{ path = 'full.7z'; size = [long]$fullItem.Length; sha256 = Get-Sha256 $fullArchive }
        patches = @()
        legacyBaselines = @()
    }
    $fullChannelPath = Join-Path $full 'channel.json'
    Write-TestText $fullChannelPath ($fullChannel | ConvertTo-Json -Depth 5)
    Set-CorePath $fullCore
    $exitCode = Invoke-VideoEnhancer @('--update-backend', '--backend-channel', $fullChannelPath)
    Assert-True ($exitCode -eq 0) '完整后端修复应成功'
    Assert-True ((Get-Content -Raw -Encoding UTF8 (Join-Path $fullPython 'backend\rve-backend.py')) -eq 'fresh') '完整后端未切换到新目录'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $fullPython 'backend\obsolete.py'))) '完整后端切换后仍残留旧脚本'
    $fullMarker = Get-Content -Raw -Encoding UTF8 (Join-Path $fullPython '.videoenhancer-backend.json') | ConvertFrom-Json
    Assert-True ($fullMarker.version -eq 'full-2') '完整后端版本标记错误'

    # 场景 6：旧 CLI 已经自修补过部分文件时，新补丁应跳过相同目标内容并补写版本标记。
    $partial = Join-Path $testRoot 'partial'
    $partialCore = Join-Path $partial 'core'
    $partialPython = Join-Path $partialCore 'python'
    New-BackendTree $partialPython 'new'
    Write-TestText (Join-Path $partialPython 'backend\add-me.py') 'add'
    Set-CorePath $partialCore
    $exitCode = Invoke-VideoEnhancer @('--apply-backend-patch', $successPatch)
    Assert-True ($exitCode -eq 0) '已部分自修补的后端应幂等完成'
    $partialMarker = Get-Content -Raw -Encoding UTF8 (Join-Path $partialPython '.videoenhancer-backend.json') | ConvertFrom-Json
    Assert-True ($partialMarker.version -eq 'target-2') '幂等更新未补写版本标记'

    Write-Output 'BACKEND_UPDATER_TESTS_PASS|6'
}
finally {
    $resolvedTestRoot = [System.IO.Path]::GetFullPath($testRoot)
    $resolvedTempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if ($resolvedTestRoot.StartsWith($resolvedTempRoot, [System.StringComparison]::OrdinalIgnoreCase) `
        -and (Test-Path -LiteralPath $resolvedTestRoot)) {
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
    }
}
