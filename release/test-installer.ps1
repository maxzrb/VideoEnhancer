param([string]$Installer = '')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Installer) { $Installer = Join-Path $root 'Artifacts\VideoEnhancerInstaller.exe' }
$Installer = [IO.Path]::GetFullPath($Installer)
if (-not (Test-Path -LiteralPath $Installer -PathType Leaf)) { throw "缺少便携安装器：$Installer" }
$portablePayload = Join-Path $root 'Artifacts\.installer\VideoEnhancerPortablePayload.exe'
if (-not (Test-Path -LiteralPath $portablePayload -PathType Leaf)) { throw "缺少内部便携载荷：$portablePayload" }

function Get-PeSubsystem([string]$path) {
    $bytes = [IO.File]::ReadAllBytes($path)
    $peOffset = [BitConverter]::ToInt32($bytes, 0x3C)
    if ($peOffset -lt 0x40 -or $peOffset -gt ($bytes.Length - 96)) { throw "PE 头无效：$path" }
    if ([Text.Encoding]::ASCII.GetString($bytes, $peOffset, 4) -ne "PE`0`0") { throw "PE 签名无效：$path" }
    return [BitConverter]::ToUInt16($bytes, $peOffset + 24 + 68)
}

if ((Get-PeSubsystem $Installer) -ne 3) { throw '安装器没有保留命令行安装模式' }
if ((Get-PeSubsystem $portablePayload) -ne 3) { throw '内部便携载荷不是控制台子系统' }
if ((Get-PeSubsystem (Join-Path $root 'Artifacts\videoenhancer.exe')) -ne 3) {
    throw '运行 EXE 丢失控制台子系统'
}
$source = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'cli\InstallerManager.cs')
foreach ($part in @('FFmpegFreeUI.exe', 'VIDEOENHANCER_INSTALL_FAIL_AFTER')) {
    if (-not $source.Contains($part)) { throw "安装器缺少目录或回滚门禁：$part" }
}

function Invoke-Installer([string]$folder) {
    $start = [Diagnostics.ProcessStartInfo]::new($portablePayload)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardError = $true
    $start.RedirectStandardOutput = $true
    foreach ($arg in @('--install-folder', $folder, '--quiet', '--skip-legacy-cleanup')) { $start.ArgumentList.Add($arg) }
    $process = [Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    $script:lastInstallerError = $stderr
    if ($process.ExitCode -ne 0 -and $stderr) { Write-Host "INSTALLER_DIAGNOSTIC|$stderr" }
    return $process.ExitCode
}

$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$testRoot = [IO.Path]::GetFullPath((Join-Path $tempBase ('VideoEnhancerPortableTest-' + [guid]::NewGuid().ToString('N'))))
if (-not $testRoot.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase)) { throw '测试目录超出临时目录' }
New-Item -ItemType Directory -Path $testRoot | Out-Null
try {
    $hostRoot = Join-Path $testRoot 'FFmpegFreeUI with spaces'
    New-Item -ItemType Directory -Path $hostRoot | Out-Null
    if ((Invoke-Installer '') -ne 1) { throw '空目录安装器没有拒绝' }
    if (-not $script:lastInstallerError.Contains('请先点击“选择目录”') -or -not $script:lastInstallerError.Contains('FFmpegFreeUI.exe')) { throw '空目录错误信息没有说明选择方法' }
    if ((Invoke-Installer $hostRoot) -ne 1) { throw '未含 3FUI 主程序时安装器没有拒绝' }
    if (-not $script:lastInstallerError.Contains('所选目录不是 3FUI 根目录') -or -not $script:lastInstallerError.Contains('不要选择 Plugin 子目录')) { throw '错误目录提示没有说明原因或修复方法' }
    if (Test-Path -LiteralPath (Join-Path $hostRoot 'Plugin')) { throw '无效目录下写入了插件' }
    [IO.File]::WriteAllText((Join-Path $hostRoot 'FFmpegFreeUI.exe'), 'test host', [Text.UTF8Encoding]::new($false))
    if ((Invoke-Installer $hostRoot) -ne 0) { throw '有效 3FUI 目录安装失败' }
    $dll = Join-Path $hostRoot 'Plugin\videoenhancer.3fui.dll'
    $exe = Join-Path $hostRoot 'Plugin\videoenhancer\videoenhancer.exe'
    if ((Get-FileHash -LiteralPath $dll).Hash -ne (Get-FileHash -LiteralPath (Join-Path $root 'VideoEnhancerPlugin\obj\plugin-artifact\videoenhancer.3fui.dll')).Hash) { throw '插件 DLL 哈希不一致' }
    if ((Get-FileHash -LiteralPath $exe).Hash -ne (Get-FileHash -LiteralPath (Join-Path $root 'Artifacts\videoenhancer.exe')).Hash) { throw '运行 EXE 哈希不一致' }
    $installedLicense = Join-Path $hostRoot 'Plugin\videoenhancer\LICENSE.txt'
    if (-not (Test-Path -LiteralPath $installedLicense -PathType Leaf) -or
        (Get-FileHash -LiteralPath $installedLicense).Hash -ne (Get-FileHash -LiteralPath (Join-Path $root 'LICENSE')).Hash) {
        throw '安装后的项目许可证缺失或内容不一致'
    }
    [xml]$project = Get-Content (Join-Path $root 'cli\VideoEnhancer.csproj') -Raw
    $ariaVersion = $project.SelectSingleNode('/Project/PropertyGroup/Aria2NextVersion').InnerText
    $aria = Join-Path $hostRoot 'Plugin\videoenhancer\bin\aria2-next\aria2-next.exe'
    if ((Get-FileHash $aria).Hash -ne (Get-FileHash (Join-Path $root "cli\obj\third-party\aria2-next\$ariaVersion\aria2-next.exe")).Hash) {
        throw '安装后的 aria2-next 哈希不一致'
    }
    foreach ($name in @('COPYING', 'AUTHORS', 'SOURCE.txt', 'DEPENDENCY-LICENSES.txt')) {
        if ((Get-FileHash (Join-Path $hostRoot "Plugin\videoenhancer\licenses\aria2-next\$name")).Hash -ne
            (Get-FileHash (Join-Path $root "cli\third-party\aria2-next\$name")).Hash) { throw "许可证未正确释放：$name" }
    }
    [IO.File]::WriteAllText($dll, 'previous plugin version')
    [IO.File]::WriteAllText($exe, 'previous runtime version')
    $dllHash = (Get-FileHash -LiteralPath $dll).Hash
    $exeHash = (Get-FileHash -LiteralPath $exe).Hash
    $env:VIDEOENHANCER_INSTALL_FAIL_AFTER = '2'
    try {
        if ((Invoke-Installer $hostRoot) -ne 1) { throw '注入故障时安装器没有报错' }
    } finally { Remove-Item Env:VIDEOENHANCER_INSTALL_FAIL_AFTER -ErrorAction SilentlyContinue }
    if ((Get-FileHash -LiteralPath $dll).Hash -ne $dllHash -or (Get-FileHash -LiteralPath $exe).Hash -ne $exeHash) { throw '故障后未恢复原文件' }
    $residue = @(Get-ChildItem -LiteralPath (Join-Path $hostRoot 'Plugin') -Filter '.videoenhancer-install-*')
    if ($residue.Count -ne 0) { throw "故障后残留事务目录：$($residue.FullName -join ', ')；内容：$((Get-ChildItem -LiteralPath $residue[0].FullName -Recurse | Select-Object -ExpandProperty FullName) -join ', ')" }
    # 用重命名的主程序验证交互安装，以及用户输入 N 时无副作用。
    $renamedRoot = Join-Path $testRoot 'Renamed 3FUI'
    New-Item -ItemType Directory $renamedRoot | Out-Null
    $renamedHost = Join-Path $renamedRoot '3FUI.exe'
    Copy-Item (Join-Path $hostRoot 'FFmpegFreeUI.exe') $renamedHost
    $interactive = [Diagnostics.ProcessStartInfo]::new($Installer)
    $interactive.UseShellExecute = $false
    $interactive.CreateNoWindow = $true
    $interactive.RedirectStandardInput = $true
    $interactive.RedirectStandardOutput = $true
    $interactive.RedirectStandardError = $true
    $interactive.Environment['VIDEOENHANCER_INSTALL_HOST'] = $renamedHost
    foreach ($answer in @("N`n", "Y`nY`n`n")) {
        $process = [Diagnostics.Process]::Start($interactive)
        $process.StandardInput.Write($answer)
        $process.StandardInput.Close()
        $output = $process.StandardOutput.ReadToEnd()
        $errorText = $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) { throw "交互安装失败：$output $errorText" }
        if ($answer.StartsWith('N') -and (Test-Path (Join-Path $renamedRoot 'Plugin'))) { throw '取消安装仍写入文件' }
    }
    foreach ($name in @('models', 'python', 'bin')) {
        if (-not (Test-Path (Join-Path $renamedRoot "Plugin\videoenhancer\$name"))) { throw "核心目录未初始化：$name" }
    }
    Write-Host 'INSTALLER_TESTS_PASS|console-installer|empty-root|invalid-root|payload-hashes|licenses|rollback|cancel|renamed-host'
} finally {
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}
