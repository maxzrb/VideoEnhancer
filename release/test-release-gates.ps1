param([string]$SevenZip = '7z')

$ErrorActionPreference = 'Stop'
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('videoenhancer-release-gates-' + [guid]::NewGuid().ToString('N'))
$releaseScript = Join-Path $PSScriptRoot 'build-modelscope-release.ps1'
$prepareScript = Join-Path $PSScriptRoot 'prepare-backend-update.ps1'

function Write-TestText([string]$path, [string]$content) {
    New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetDirectoryName($path)) | Out-Null
    [System.IO.File]::WriteAllText($path, $content, $utf8NoBom)
}

function Assert-True([bool]$condition, [string]$message) {
    if (-not $condition) { throw "断言失败：$message" }
}

try {
    $unchangedBase = Join-Path $testRoot 'unchanged\base'
    $unchangedTarget = Join-Path $testRoot 'unchanged\target'
    Write-TestText (Join-Path $unchangedBase 'backend\stable.py') 'same'
    Write-TestText (Join-Path $unchangedTarget 'backend\stable.py') 'same'
    $validNotes = "[更改]修复旧脚本`n[新增]加入增量更新`n[移除]移除覆盖入口"
    $isolatedReleaseOutput = Join-Path $testRoot 'release-output'
    $validOutput = & pwsh -NoProfile -File $releaseScript -ValidateOnly `
        -Notes $validNotes `
        -BackendOutputRoot $isolatedReleaseOutput `
        -BackendBaseRoot $unchangedBase -BackendTargetRoot $unchangedTarget `
        -BackendBaseVersion 'same-1' -BackendTargetVersion 'same-1' 2>&1
    Assert-True ($LASTEXITCODE -eq 0) '合法逐行 Release Notes 和未变化后端应通过'
    Assert-True (($validOutput -join "`n").Contains('RELEASE_GATES_PASS|backendChanged=False')) '未输出未变化门禁结果'

    $invalidOutput = & pwsh -NoProfile -File $releaseScript -ValidateOnly `
        -Notes '修复旧脚本；新增增量更新' `
        -BackendOutputRoot $isolatedReleaseOutput `
        -BackendBaseRoot $unchangedBase -BackendTargetRoot $unchangedTarget `
        -BackendBaseVersion 'same-1' -BackendTargetVersion 'same-1' 2>&1
    Assert-True ($LASTEXITCODE -ne 0) '自由文本 Release Notes 必须被拒绝'
    Assert-True (($invalidOutput -join "`n").Contains('Release Notes 格式错误')) 'Release Notes 拒绝原因不明确'

    $changedBase = Join-Path $testRoot 'changed\base'
    $changedTarget = Join-Path $testRoot 'changed\target'
    $packagePython = Join-Path $testRoot 'changed\package\python'
    foreach ($root in @($changedBase, $changedTarget, $packagePython)) {
        Write-TestText (Join-Path $root 'python\python.exe') 'fake-python'
        Write-TestText (Join-Path $root 'backend\stable.py') 'stable'
    }
    Write-TestText (Join-Path $changedBase 'backend\cache\runtime.bin') 'old-cache'
    Write-TestText (Join-Path $changedTarget 'backend\cache\runtime.bin') 'new-cache'
    Write-TestText (Join-Path $packagePython 'backend\cache\runtime.bin') 'package-cache'
    Write-TestText (Join-Path $changedBase 'backend\rve-backend.py') 'old'
    Write-TestText (Join-Path $changedBase 'backend\delete.py') 'delete'
    foreach ($root in @($changedTarget, $packagePython)) {
        Write-TestText (Join-Path $root 'backend\rve-backend.py') 'new'
        Write-TestText (Join-Path $root 'backend\add.py') 'add'
    }
    $fullArchive = Join-Path $testRoot 'changed\python_full.7z'
    & $SevenZip a -t7z $fullArchive (Join-Path $testRoot 'changed\package\*') | Out-Null
    if ($LASTEXITCODE -ne 0) { throw '测试完整包创建失败' }
    $changedOutput = Join-Path $testRoot 'changed\output'
    & $prepareScript -BaseRoot $changedBase -TargetRoot $changedTarget `
        -BaseVersion 'base-1' -TargetVersion 'target-2' `
        -FullArchive $fullArchive -OutputRoot $changedOutput `
        -SentinelPaths 'backend/stable.py' -SevenZip $SevenZip | Out-Host
    $audit = Get-Content -Raw -Encoding UTF8 (Join-Path $changedOutput 'backend-release-audit.json') | ConvertFrom-Json
    Assert-True ($audit.hasChanges) '后端变化未被识别'
    Assert-True ($audit.counts.add -eq 1 -and $audit.counts.replace -eq 1 -and $audit.counts.delete -eq 1) '后端差异分类不正确'
    Assert-True ((Test-Path -LiteralPath $audit.patch.localPath) -and (Test-Path -LiteralPath $audit.channelPath)) '未生成增量包或 channel'

    $deferredOutput = Join-Path $testRoot 'changed\deferred-output'
    & $prepareScript -BaseRoot $changedBase -TargetRoot $changedTarget `
        -BaseVersion 'base-1' -TargetVersion 'target-2' `
        -OutputRoot $deferredOutput -SentinelPaths 'backend/stable.py' `
        -DeferFullArchive -SevenZip $SevenZip | Out-Host
    $deferredAudit = Get-Content -Raw -Encoding UTF8 (Join-Path $deferredOutput 'backend-release-audit.json') | ConvertFrom-Json
    Assert-True ($deferredAudit.counts.add -eq 1 -and $deferredAudit.counts.replace -eq 1 -and $deferredAudit.counts.delete -eq 1) '暂缓模式后端差异分类不正确'
    Assert-True ((Test-Path -LiteralPath $deferredAudit.patch.localPath) -and -not (Test-Path -LiteralPath (Join-Path $deferredOutput 'backend-channel.json'))) '暂缓模式应只生成增量包和审计，不应生成 channel'

    $badPackageRoot = Join-Path $testRoot 'bad-package\python'
    Write-TestText (Join-Path $badPackageRoot 'backend\rve-backend.py') 'stale'
    $badArchive = Join-Path $testRoot 'bad-package.7z'
    & $SevenZip a -t7z $badArchive (Join-Path $testRoot 'bad-package\*') | Out-Null
    $badOutput = & pwsh -NoProfile -File $prepareScript `
        -BaseRoot $changedBase -TargetRoot $changedTarget `
        -BaseVersion 'base-1' -TargetVersion 'target-2' `
        -FullArchive $badArchive -OutputRoot (Join-Path $testRoot 'bad-output') `
        -SentinelPaths 'backend/stable.py' 2>&1
    Assert-True ($LASTEXITCODE -ne 0) '与候选目录不一致的完整包必须被拒绝'
    Assert-True (($badOutput -join "`n").Contains('完整后端包')) '完整包拒绝原因不明确'

    Write-Output 'RELEASE_GATE_TESTS_PASS|5'
    $global:LASTEXITCODE = 0
}
finally {
    $resolvedTestRoot = [System.IO.Path]::GetFullPath($testRoot)
    $resolvedTempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if ($resolvedTestRoot.StartsWith($resolvedTempRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $resolvedTestRoot)) {
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
    }
}
