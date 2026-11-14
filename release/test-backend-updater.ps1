param(
    [string]$BuildOutput = (Join-Path $PSScriptRoot '..\cli\bin\Release\net10.0-windows\win-x64'),
    [string]$ArchiveTool = '',
    [string]$PythonExe = ''
)

$ErrorActionPreference = 'Stop'
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('videoenhancer-backend-tests-' + [guid]::NewGuid().ToString('N'))
$activeExe = ''

if ([string]::IsNullOrWhiteSpace($ArchiveTool)) {
    $ArchiveTool = Join-Path $BuildOutput 'videoenhancer.exe'
}
if (-not (Test-Path -LiteralPath $ArchiveTool -PathType Leaf)) {
    throw "找不到托管归档工具：$ArchiveTool"
}
$archiveToolPath = [System.IO.Path]::GetFullPath($ArchiveTool)

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

function Use-CoreRoot([string]$coreRoot) {
    New-Item -ItemType Directory -Force -Path $coreRoot | Out-Null
    Copy-Item -Path (Join-Path $BuildOutput '*') -Destination $coreRoot -Recurse -Force
    $script:activeExe = Join-Path $coreRoot 'videoenhancer.exe'
}

function Invoke-VideoEnhancer([string[]]$arguments) {
    $commandOutput = & $script:activeExe @arguments
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

function Resolve-PythonExecutable([string]$configuredPath) {
    $candidates = [System.Collections.Generic.List[string]]::new()
    if (-not [string]::IsNullOrWhiteSpace($configuredPath)) {
        $candidates.Add($configuredPath)
    }

    $pyLauncher = Get-Command py -ErrorAction SilentlyContinue
    if ($null -ne $pyLauncher) {
        try {
            $registeredPython = & $pyLauncher.Source -3 -c 'import sys; print(sys.executable)' 2>$null
            if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($registeredPython)) {
                $candidates.Add(($registeredPython | Select-Object -Last 1).Trim())
            }
        }
        catch {
            # Continue with ordinary command discovery.
        }
    }

    foreach ($commandName in @('python', 'python3')) {
        $command = Get-Command $commandName -ErrorAction SilentlyContinue
        if ($null -ne $command -and -not [string]::IsNullOrWhiteSpace($command.Source)) {
            $candidates.Add($command.Source)
        }
    }

    foreach ($candidate in $candidates | Select-Object -Unique) {
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            continue
        }

        try {
            & $candidate --version *> $null
            if ($LASTEXITCODE -eq 0) {
                return [System.IO.Path]::GetFullPath($candidate)
            }
        }
        catch {
            # WindowsApps aliases can be discoverable without being usable files.
        }
    }

    throw '找不到可执行的 Python 3 解释器。可通过 -PythonExe 显式指定。'
}

try {
    if (-not (Test-Path -LiteralPath (Join-Path $BuildOutput 'videoenhancer.exe'))) {
        throw "找不到已构建 CLI：$BuildOutput"
    }
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
        -OutputArchive $successPatch -ArchiveTool $archiveToolPath -DisablePythonProbe | Out-Null
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
    Use-CoreRoot $successCore
    $statusOutput = & $script:activeExe --backend-status --backend-channel $channelPath --json
    Assert-True ($LASTEXITCODE -eq 0) '旧版后端状态检查应成功'
    $status = $statusOutput | Select-Object -Last 1 | ConvertFrom-Json
    Assert-True ($status.state -eq 'legacy-update-available') '应通过哨兵识别旧版基线'
    Assert-True ($status.mode -eq 'patch') '应选择增量补丁'
    Assert-True ($status.downloadSize -eq $patchItem.Length) '应报告补丁实际下载大小'
    Assert-True ($status.fullSize -eq 999) '状态应同时报告完整修复包大小'
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
    Use-CoreRoot $conflictCore
    $conflictOutput = & $script:activeExe --apply-backend-patch $successPatch 2>&1
    $exitCode = $LASTEXITCODE
    $conflictOutput | Out-Host
    Assert-True ($exitCode -ne 0) 'SHA 冲突必须失败'
    Assert-True (("$($conflictOutput -join "`n")").Contains('BACKEND_FULL_REQUIRED|')) '补丁冲突应提示可改用完整修复包'
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
        -OutputArchive $rollbackPatch -ArchiveTool $archiveToolPath -DisablePythonProbe | Out-Null
    Use-CoreRoot $rollbackCore
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
    Use-CoreRoot $recoveryCore
    $exitCode = Invoke-VideoEnhancer @('--apply-backend-patch', (Join-Path $recovery 'missing.7z'))
    Assert-True ($exitCode -ne 0) '恢复后缺失补丁仍应返回非零'
    Assert-True ((Get-Content -Raw -Encoding UTF8 (Join-Path $recoveryPython 'backend\rve-backend.py')) -eq 'old') '启动恢复未还原备份'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $recoveryPython 'backend\added-during-update.py'))) '启动恢复未删除事务新增文件'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $stateRoot 'pending.json'))) '启动恢复未清除 pending 日志'

    # 场景 5：即使存在可用补丁，用户也能明确选择完整包修复。
    $full = Join-Path $testRoot 'full'
    $fullCore = Join-Path $full 'core'
    $fullPython = Join-Path $fullCore 'python'
    New-BackendTree $fullPython 'unknown-old'
    Write-TestText (Join-Path $fullPython 'backend\obsolete.py') 'obsolete'
    Write-TestText (Join-Path $fullPython '.videoenhancer-backend.json') '{"version":"full-old"}'
    $fullPackageRoot = Join-Path $full 'package\python'
    Write-TestText (Join-Path $fullPackageRoot 'backend\rve-backend.py') 'fresh'
    $probeExe = Resolve-PythonExecutable $PythonExe
    $probeDirectory = Split-Path -Parent $probeExe
    New-Item -ItemType Directory -Force -Path (Join-Path $fullPackageRoot 'python') | Out-Null
    Copy-Item -LiteralPath $probeExe -Destination (Join-Path $fullPackageRoot 'python\python.exe')
    Get-ChildItem -LiteralPath $probeDirectory -File | Where-Object {
        $_.Name -match '^python\d+\.dll$' -or $_.Name -match '^vcruntime\d+.*\.dll$'
    } | Copy-Item -Destination (Join-Path $fullPackageRoot 'python')
    $fullArchive = Join-Path $full 'full.7z'
    & $archiveToolPath --create-7z (Join-Path $full 'package') $fullArchive | Out-Null
    if ($LASTEXITCODE -ne 0) { throw '完整包测试归档创建失败' }
    $fullItem = Get-Item -LiteralPath $fullArchive
    $unusedPatch = Join-Path $full 'unused-patch.7z'
    Copy-Item -LiteralPath $successPatch -Destination $unusedPatch
    $unusedPatchItem = Get-Item -LiteralPath $unusedPatch
    $fullChannel = [ordered]@{
        schemaVersion = 1
        latestVersion = 'full-2'
        full = [ordered]@{ path = 'full.7z'; size = [long]$fullItem.Length; sha256 = Get-Sha256 $fullArchive }
        patches = @([ordered]@{
            baseVersion = 'full-old'; targetVersion = 'full-2'; path = 'unused-patch.7z'
            size = [long]$unusedPatchItem.Length; sha256 = Get-Sha256 $unusedPatch
        })
        legacyBaselines = @()
    }
    $fullChannelPath = Join-Path $full 'channel.json'
    Write-TestText $fullChannelPath ($fullChannel | ConvertTo-Json -Depth 5)
    Use-CoreRoot $fullCore
    $fullStatusOutput = & $script:activeExe --backend-status --backend-channel $fullChannelPath --json
    Assert-True ($LASTEXITCODE -eq 0) '完整修复场景状态检查应成功'
    $fullStatus = $fullStatusOutput | Select-Object -Last 1 | ConvertFrom-Json
    Assert-True ($fullStatus.mode -eq 'patch') '测试前置条件必须先选择增量补丁'
    $exitCode = Invoke-VideoEnhancer @('--update-backend', '--force-backend-full', '--backend-channel', $fullChannelPath)
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
    Use-CoreRoot $partialCore
    $exitCode = Invoke-VideoEnhancer @('--apply-backend-patch', $successPatch)
    Assert-True ($exitCode -eq 0) '已部分自修补的后端应幂等完成'
    $partialMarker = Get-Content -Raw -Encoding UTF8 (Join-Path $partialPython '.videoenhancer-backend.json') | ConvertFrom-Json
    Assert-True ($partialMarker.version -eq 'target-2') '幂等更新未补写版本标记'

    # 场景 7：内嵌核心已把文件推进到后续补丁目标时，补丁链应继续推进版本标记。
    $ahead = Join-Path $testRoot 'ahead'
    $aheadCore = Join-Path $ahead 'core'
    $aheadPython = Join-Path $aheadCore 'python'
    $aheadBase = Join-Path $ahead 'base'
    $aheadMiddle = Join-Path $ahead 'middle'
    $aheadTarget = Join-Path $ahead 'target'
    New-BackendTree $aheadBase 'base-file'
    New-BackendTree $aheadMiddle 'middle-file'
    New-BackendTree $aheadTarget 'final-file'
    New-BackendTree $aheadPython 'final-file'
    Write-TestText (Join-Path $aheadPython '.videoenhancer-backend.json') '{"version":"ahead-1"}'
    $aheadPatch1 = Join-Path $ahead 'ahead-1_to_2.7z'
    $aheadPatch2 = Join-Path $ahead 'ahead-2_to_3.7z'
    & (Join-Path $PSScriptRoot 'build-backend-patch.ps1') `
        -BaseRoot $aheadBase -TargetRoot $aheadMiddle `
        -BaseVersion 'ahead-1' -TargetVersion 'ahead-2' `
        -OutputArchive $aheadPatch1 -ArchiveTool $archiveToolPath -DisablePythonProbe | Out-Null
    & (Join-Path $PSScriptRoot 'build-backend-patch.ps1') `
        -BaseRoot $aheadMiddle -TargetRoot $aheadTarget `
        -BaseVersion 'ahead-2' -TargetVersion 'ahead-3' `
        -OutputArchive $aheadPatch2 -ArchiveTool $archiveToolPath -DisablePythonProbe | Out-Null
    $aheadPatch1Item = Get-Item -LiteralPath $aheadPatch1
    $aheadPatch2Item = Get-Item -LiteralPath $aheadPatch2
    $aheadChannel = [ordered]@{
        schemaVersion = 1
        latestVersion = 'ahead-3'
        full = [ordered]@{ path = 'full.7z'; size = 999; sha256 = '' }
        patches = @(
            [ordered]@{
                baseVersion = 'ahead-1'; targetVersion = 'ahead-2'; path = 'ahead-1_to_2.7z'
                size = [long]$aheadPatch1Item.Length; sha256 = Get-Sha256 $aheadPatch1
            },
            [ordered]@{
                baseVersion = 'ahead-2'; targetVersion = 'ahead-3'; path = 'ahead-2_to_3.7z'
                size = [long]$aheadPatch2Item.Length; sha256 = Get-Sha256 $aheadPatch2
            }
        )
        legacyBaselines = @()
    }
    $aheadChannelPath = Join-Path $ahead 'channel.json'
    Write-TestText $aheadChannelPath ($aheadChannel | ConvertTo-Json -Depth 8)
    Use-CoreRoot $aheadCore
    $exitCode = Invoke-VideoEnhancer @('--update-backend', '--backend-channel', $aheadChannelPath)
    Assert-True ($exitCode -eq 0) '文件已提前到达后续目标时应完成整条补丁链'
    Assert-True ((Get-Content -Raw -Encoding UTF8 (Join-Path $aheadPython 'backend\rve-backend.py')) -eq 'final-file') '后续目标文件被错误改写'
    $aheadMarker = Get-Content -Raw -Encoding UTF8 (Join-Path $aheadPython '.videoenhancer-backend.json') | ConvertFrom-Json
    Assert-True ($aheadMarker.version -eq 'ahead-3') '后续目标文件未推进到最终版本标记'

    # 场景 8：无版本标记且哨兵误判为后一版时，应自动向前展开补丁并重试。
    $backtrack = Join-Path $testRoot 'backtrack'
    $backtrackCore = Join-Path $backtrack 'core'
    $backtrackPython = Join-Path $backtrackCore 'python'
    $backtrackBase = Join-Path $backtrack 'base'
    $backtrackMiddle = Join-Path $backtrack 'middle'
    $backtrackTarget = Join-Path $backtrack 'target'
    New-BackendTree $backtrackBase 'base-file'
    New-BackendTree $backtrackMiddle 'middle-file'
    Write-TestText (Join-Path $backtrackMiddle 'backend\from-first.py') 'middle-add'
    New-BackendTree $backtrackTarget 'final-file'
    Write-TestText (Join-Path $backtrackTarget 'backend\from-first.py') 'final-add'
    New-BackendTree $backtrackPython 'final-file'
    $backtrackPatch1 = Join-Path $backtrack 'backtrack-1_to_2.7z'
    $backtrackPatch2 = Join-Path $backtrack 'backtrack-2_to_3.7z'
    & (Join-Path $PSScriptRoot 'build-backend-patch.ps1') `
        -BaseRoot $backtrackBase -TargetRoot $backtrackMiddle `
        -BaseVersion 'backtrack-1' -TargetVersion 'backtrack-2' `
        -OutputArchive $backtrackPatch1 -ArchiveTool $archiveToolPath -DisablePythonProbe | Out-Null
    & (Join-Path $PSScriptRoot 'build-backend-patch.ps1') `
        -BaseRoot $backtrackMiddle -TargetRoot $backtrackTarget `
        -BaseVersion 'backtrack-2' -TargetVersion 'backtrack-3' `
        -OutputArchive $backtrackPatch2 -ArchiveTool $archiveToolPath -DisablePythonProbe | Out-Null
    $backtrackPatch1Item = Get-Item -LiteralPath $backtrackPatch1
    $backtrackPatch2Item = Get-Item -LiteralPath $backtrackPatch2
    $backtrackChannel = [ordered]@{
        schemaVersion = 1
        latestVersion = 'backtrack-3'
        full = [ordered]@{ path = 'full.7z'; size = 999; sha256 = '' }
        patches = @(
            [ordered]@{
                baseVersion = 'backtrack-1'; targetVersion = 'backtrack-2'; path = 'backtrack-1_to_2.7z'
                size = [long]$backtrackPatch1Item.Length; sha256 = Get-Sha256 $backtrackPatch1
            },
            [ordered]@{
                baseVersion = 'backtrack-2'; targetVersion = 'backtrack-3'; path = 'backtrack-2_to_3.7z'
                size = [long]$backtrackPatch2Item.Length; sha256 = Get-Sha256 $backtrackPatch2
            }
        )
        legacyBaselines = @([ordered]@{
            version = 'backtrack-2'
            sentinels = @([ordered]@{
                path = 'backend/rve-backend.py'
                sha256 = Get-Sha256 (Join-Path $backtrackPython 'backend\rve-backend.py')
            })
        })
    }
    $backtrackChannelPath = Join-Path $backtrack 'channel.json'
    Write-TestText $backtrackChannelPath ($backtrackChannel | ConvertTo-Json -Depth 8)
    Use-CoreRoot $backtrackCore
    $backtrackStatus = (& $script:activeExe --backend-status --backend-channel $backtrackChannelPath --json |
        Select-Object -Last 1) | ConvertFrom-Json
    Assert-True ($backtrackStatus.installedVersion -eq 'backtrack-2' -and $backtrackStatus.patchCount -eq 1) `
        '无标记误判测试的前置状态错误'
    $exitCode = Invoke-VideoEnhancer @('--update-backend', '--backend-channel', $backtrackChannelPath)
    Assert-True ($exitCode -eq 0) '哨兵误判后应自动回退并完成整条补丁链'
    Assert-True ((Get-Content -Raw -Encoding UTF8 (Join-Path $backtrackPython 'backend\from-first.py')) -eq 'final-add') '自动回退未补齐前序新增文件'
    $backtrackMarker = Get-Content -Raw -Encoding UTF8 (Join-Path $backtrackPython '.videoenhancer-backend.json') | ConvertFrom-Json
    Assert-True ($backtrackMarker.version -eq 'backtrack-3') '自动回退后版本标记错误'

    Write-Output 'BACKEND_UPDATER_TESTS_PASS|8'
}
finally {
    $resolvedTestRoot = [System.IO.Path]::GetFullPath($testRoot)
    $resolvedTempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if ($resolvedTestRoot.StartsWith($resolvedTempRoot, [System.StringComparison]::OrdinalIgnoreCase) `
        -and (Test-Path -LiteralPath $resolvedTestRoot)) {
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
    }
}
