param([string]$Installer = '', [string]$ArtifactsRoot = $env:VIDEOENHANCER_ARTIFACTS_DIR)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $ArtifactsRoot) { $ArtifactsRoot = Join-Path $root 'Artifacts' }
if (-not [IO.Path]::IsPathRooted($ArtifactsRoot)) { $ArtifactsRoot = Join-Path $root $ArtifactsRoot }
$ArtifactsRoot = [IO.Path]::GetFullPath($ArtifactsRoot)
if (-not $Installer) { $Installer = Join-Path $ArtifactsRoot 'VideoEnhancerInstaller.exe' }
$Installer = [IO.Path]::GetFullPath($Installer)
if (-not (Test-Path -LiteralPath $Installer -PathType Leaf)) { throw "缺少便携安装器：$Installer" }
$portablePayload = Join-Path $ArtifactsRoot '.installer\VideoEnhancerPortablePayload.exe'
if (-not (Test-Path -LiteralPath $portablePayload -PathType Leaf)) { throw "缺少内部便携载荷：$portablePayload" }

function Get-PeSubsystem([string]$path) {
    $bytes = [IO.File]::ReadAllBytes($path)
    $peOffset = [BitConverter]::ToInt32($bytes, 0x3C)
    if ($peOffset -lt 0x40 -or $peOffset -gt ($bytes.Length - 96)) { throw "PE 头无效：$path" }
    if ([Text.Encoding]::ASCII.GetString($bytes, $peOffset, 4) -ne "PE`0`0") { throw "PE 签名无效：$path" }
    return [BitConverter]::ToUInt16($bytes, $peOffset + 24 + 68)
}

if ((Get-PeSubsystem $Installer) -ne 2) { throw '安装器不是 GUI 子系统，会闪出黑色控制台窗口' }
if ((Get-PeSubsystem $portablePayload) -ne 2) { throw '内部便携载荷不是 GUI 子系统' }
if ((Get-PeSubsystem (Join-Path $ArtifactsRoot 'videoenhancer.exe')) -ne 3) {
    throw '运行 EXE 丢失控制台子系统'
}
$source = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'cli\InstallerManager.cs')
foreach ($part in @('FFmpegFreeUI.exe', 'VIDEOENHANCER_INSTALL_FAIL_AFTER')) {
    if (-not $source.Contains($part)) { throw "安装器缺少目录或回滚门禁：$part" }
}
$bundle = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'installer\Bundle\Bundle.wxs')
$theme = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'installer\Bundle\VideoEnhancerTheme.xml')
$localization = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'installer\Bundle\VideoEnhancerTheme.zh-CN.wxl')
foreach ($part in @('DisableModify="yes"', 'DisableRemove="yes"', '<ExePackage', 'InstallArguments=', 'Value=""')) {
    if (-not $bundle.Contains($part)) { throw "PR #7 安装窗口配置缺少：$part" }
}
if ($bundle.Contains('<MsiPackage') -or $bundle.Contains('RegistrySearch')) { throw '安装链仍包含 MSI 或注册表目录搜索' }
if ($bundle.Contains('--show-errors') -or $source.Contains('--show-errors')) { throw '安装链仍会显示重复的错误弹窗' }
foreach ($part in @('EulaRichedit', 'OptionsButton', 'BrowseDirectoryAction', 'VisibleCondition="InstallFolder"', 'Name="InstallUnavailableButton"', 'EnableCondition="0" VisibleCondition="NOT InstallFolder"')) {
    if (-not $theme.Contains($part)) { throw "PR #7 安装主题缺少：$part" }
}
if (-not $theme.Contains('#(loc.FailureInstallGuidance)') -or -not $localization.Contains('请点击“选择目录”')) { throw '安装失败页缺少可操作的中文提示' }
if ($theme.Contains('Name="FailureMessageText"')) { throw '安装失败页仍显示没有操作指引的系统错误文案' }

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
    if ((Get-FileHash -LiteralPath $exe).Hash -ne (Get-FileHash -LiteralPath (Join-Path $ArtifactsRoot 'videoenhancer.exe')).Hash) { throw '运行 EXE 哈希不一致' }
    $installedLicense = Join-Path $hostRoot 'Plugin\videoenhancer\LICENSE.txt'
    if (-not (Test-Path -LiteralPath $installedLicense -PathType Leaf) -or
        (Get-FileHash -LiteralPath $installedLicense).Hash -ne (Get-FileHash -LiteralPath (Join-Path $root 'LICENSE')).Hash) {
        throw '安装后的项目许可证缺失或内容不一致'
    }
    Rename-Item -LiteralPath (Join-Path $hostRoot 'FFmpegFreeUI.exe') -NewName 'FFmpegFreeUI.x64.exe'
    if ((Invoke-Installer $hostRoot) -ne 0) { throw 'x64 名称的 3FUI 目录安装失败' }
    $dllHash = (Get-FileHash -LiteralPath $dll).Hash
    $exeHash = (Get-FileHash -LiteralPath $exe).Hash
    $env:VIDEOENHANCER_INSTALL_FAIL_AFTER = '2'
    try {
        if ((Invoke-Installer $hostRoot) -ne 1) { throw '注入故障时安装器没有报错' }
    } finally { Remove-Item Env:VIDEOENHANCER_INSTALL_FAIL_AFTER -ErrorAction SilentlyContinue }
    if ((Get-FileHash -LiteralPath $dll).Hash -ne $dllHash -or (Get-FileHash -LiteralPath $exe).Hash -ne $exeHash) { throw '故障后未恢复原文件' }
    $residue = @(Get-ChildItem -LiteralPath (Join-Path $hostRoot 'Plugin') -Filter '.videoenhancer-install-*')
    if ($residue.Count -ne 0) { throw "故障后残留事务目录：$($residue.FullName -join ', ')；内容：$((Get-ChildItem -LiteralPath $residue[0].FullName -Recurse | Select-Object -ExpandProperty FullName) -join ', ')" }
    Write-Host 'INSTALLER_TESTS_PASS|pr7-theme|no-msi|gui-subsystem|runtime-console|empty-root-message|invalid-root-message|valid-root|payload-hashes|rollback'
} finally {
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}
