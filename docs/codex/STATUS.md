# Project Status

Last updated: 2026-09-26 18:25
Updated by: Codex

## Current Snapshot

- Latest PATH clarification (2026-09-26 18:25): 用户提醒 3FUI 还可使用环境变量中的 FFmpeg；实机 Machine PATH 确有 `ffmpeg.exe`，当前插件/CLI 源码均会搜索 PATH。把预览及四宫格缺失提示改为同时列出 3FUI 工作目录、程序目录、系统 PATH，提交 `5a23acb` 已推送 fork/main 并核对远端一致。重建完整候选安装器 0 警告/0 错误，安装器空/错误/正确目录哈希及回滚通过；最新 `Artifacts/VideoEnhancerInstaller.exe` SHA-256 `A4F39F74498A8DBE7315AC81632441134FA2DCD4CA064BDFDA800BAFAC76C9D1`，DLL `ACA73BFF15B33EB23C4E2B19A9115E389BC9AB45BE653455ED285842A2A95323`。真实 3FUI 预览与视觉仍待重新安装候选后验收。

- Latest installer follow-up (2026-09-26 18:20): 用户用安装器实测后发现实时预览报旧版 FFmpeg 缺失提示，且关闭插件总开关时补帧模型与 HDR 处理方式仍亮起。实机 `Settings.json` 的工作目录为空，3FUI 根/Plugin 内均无 ffmpeg.exe；系统 Machine PATH 有独立 FFmpeg 8.1.1 的 bin。已安装 DLL 哈希 `6F75B6A1...` 含旧版 `bin` 提示；当前源码的解析器会继续查 PATH。已在 `PluginPanel.UpscalePage.vb` 为两个下拉框补齐总开关与各自功能开关门禁，Release 插件构建 0/0。完整 Release publish 0/0，候选安装器 `Artifacts/VideoEnhancerInstaller.exe` SHA-256 `8591CA72...`，安装器门禁、有效目录文件哈希与回滚测试通过；ZIP 中插件 DLL 与新构建 SHA-256 同为 `FE8241D8...`。PR #7 隔离 worktree 的提交已在 main 且干净，已移除该测试 worktree，当前开发目录为本仓库 main。源码与记录提交 `1b97064` 已推送 fork/main 并核对远端一致。版本仍 1.3.5，未创建新 Release；用户须用本地候选重新安装并重启 3FUI，复验预览和按钮视觉。其余 2026-09-19 旧 PR 审查 worktree 未动。

- Latest copy audit (2026-09-24 19:34): 用户追问是否仍有防御性文字。审查 README、插件 README/教程、安装器 RTF/WiX、备用安装确认框、CLI/手动安装说明、第三方声明与 ModelScope 说明。清理备用确认框“不注册 Windows 应用”、教程“不是故障/设计上的能力限制”、README 的长否定式目录/授权解释、插件 README/CLI README 中多余实现历史、日志里的 Python pipe 解释、第三方声明里的二进制边界辩解。必要的兼容限制、错误原因、迁移冲突规则和第三方许可证保留。完整 Release publish 0/0，安装器目录/哈希/回滚门禁通过，`--third-party-notices` 输出新文字，git diff --check 无错误。改动提交 d324eb1 已推送至 fork/main，`git ls-remote` 核对一致；无新版本/Release。

- Latest license task (2026-09-24 19:20): 用户已确认获得 user-Wing/ARXChem 和 SteveYu000 对现有贡献按 MIT 发布的同意。根目录新增标准 MIT `LICENSE`；项目许可证嵌入运行 EXE 并通过 `--license` 输出，也随首次安装器和手动 ZIP 安装为 `Plugin\videoenhancer\LICENSE.txt`。第三方组件维持自身许可证。README/插件说明/安装窗口 RTF 等用户界面描述已去掉有关 Burn、MSI、是否保留安装器等防御性文字，保留直接操作步骤及 MIT 全文。版本仍 1.3.5，未发布新 Release。
- Latest license verification/Git: 解决方案 Release publish 与最终 WiX 重建 0 警告/0 错误；运行 EXE `--license`、手动 ZIP、安装后 LICENSE 全文/哈希一致；安装器 RTF 静态校验包含 MIT 全文且不含旧防御性文案；release/test-installer.ps1 通过空目录/错误目录/有效目录/许可证/哈希/回滚；git diff --check 无错误。源码与文案提交 aa0a728 已推送至 fork/main，`git ls-remote` 核对一致，GitHub API 识别仓库许可证为 MIT。真实 3FUI/UAC 安装及新 RTF 的可见窗口目视仍待验收。
- Latest FFmpeg routing (2026-09-24 19:01): 插件预览、四宫格、分段探测、时长探测统一优先读取 3FUI 当前工作目录中的 ffmpeg/ffprobe；排队时把路径传给 CLI，CLI 独立运行时读取宿主 Settings.json。源码/记录提交 f57a5ba 已推送至 fork/main，并核实远端同提交；工作树在此提交后曾干净。旧具名 stash 仍保留。
- Latest FFmpeg verification: Release build 0 警告/0 错误；单独 CLI publish 成功；临时 3FUI 根目录同时放置 ffmpeg.exe 且 Settings.json 指向独立工作目录时，`--check` 报告使用独立目录；Python 33/33；git diff --check 无错误。真实 3FUI 实机预览/四宫格/分段及完整视频处理仍待验收。

- Latest local sync (2026-09-24 18:37): fork/main 与本地主工作树 main 均已快进到交接提交 3da8ba4，PR #7 远端已 MERGED；隔离分支也在 3da8ba4。两处工作树同步后均干净。主工作树同步前独有的两份 HandShake 记录已经合并进远端交接历史，原样另存为具名 git stash（pre-PR7-merge HandShake records 2026-09-24），未丢失。后续作者从 main 即可接手；不必恢复该旧 stash，除非需要审计当时的记录。

- Latest Git outcome (2026-09-24 18:34): 用户认可当前安装器候选并授权选择性合并 PR #7、推送方便其他作者接手。五个 SteveYu000 原提交连同本地目录门禁提交 d68d680 均保留历史；最终修正提交 169abb9 已快进推送到 fork/main，GitHub PR #7 已显示 MERGED，mergeCommit=9bdb378。origin=user-Wing 上游未推送，本次目标为 maxzrb/VideoEnhancer。当前版本仍 1.3.5，未创建新 Release 或标签。
- Latest selection: 保留 PR 的统一版本元数据、PluginPanel 页面拆分、独立 aria2-next/SharpCompress 和 PR #7 的 WiX 中文窗口；最终安装链去掉 MSI、项目 HKLM 安装位置及卸载逻辑，窗口不出现在 Windows“已安装的应用”（Burn 仍留隐藏缓存）；安装器仅首次选择 3FUI 根目录，插件自身通过运行 EXE 事务更新 EXE/DLL，aria2-next 不参与本体更新。issue #6 仍 OPEN，未因本次合并关闭。
- Latest verification/handoff: 最终 1.3.5 候选 Release publish 0 警告/0 错误，Python 33/33，安装器空目录/错误目录/有效目录哈希/回滚、自更新/回滚、后端更新器 6/6、发布门禁 5/5、git diff --check 均通过。真实 3FUI/UAC 安装、受保护目录和 Burn 窗口最后一次视觉验收未覆盖；旧测试 MSI 误装记录未擅自卸载。本地隔离 worktree 后续清理，main 工作树仅 HandShake 记录未提交且本地 main 尚未快进到远端。

- Latest correction (2026-09-24 15:50): 用户实机指出错误弹窗与 PR #7 安装失败页重复。已从 Burn ExePackage 移除 --show-errors 条件参数，并从 InstallerManager.InstallCommand 移除错误 MessageBox 分支；安装失败只由原窗口 Failure 页展示中文目录/权限建议及日志入口，具体错误仍写 stderr/Burn 日志。最终安装器 11,779,980 bytes / SHA-256 CFD6C6ACAC019ED50A48CF5715F7DE23E2D94B7DABF587B0A2840876270C4838。
- Latest verification: Release publish 0 警告/0 错误；安装器空目录/错误目录提示、有效目录哈希及回滚测试通过；Python 33/33；git diff --check 无错误。Burn 实机失败页目视仍可由用户复验；未发布。

- Latest refinement (2026-09-24 15:44): PR #7 失败页移除只会显示“函数不正确”的原始系统错误控件，保留明确的中文目录/权限指引与日志链接。交互模式精确错误弹窗仍在。最终安装器 11,779,774 bytes / SHA-256 5D96341281250BB8CC398D4953B0454446986A88F877A2C2EEFB3C0B3DCC86D9；WiX build 0/0，安装器文案/目录/回滚测试通过。

- Latest fix (2026-09-24 15:41): 用户指出 Burn 把无效目录错误显示成“函数不正确”。交互模式的 ExePackage 现条件传入 --show-errors，便携载荷弹出明确中文错误；空目录指引点击“选择目录”，错误目录说明需包含 FFmpegFreeUI.exe 且不能选 Plugin，权限不足单独提示。失败页也展示可操作的目录/权限建议。静默模式不弹窗。新安装器 11,779,640 bytes / SHA-256 FEB0F52EF2018F4E8ACDFF15DB163CC293D02C9F5D7413FCE04032A860389881。
- Latest verification: Release publish 和最终 WiX rebuild 0 警告/0 错误；Win32 实际错误弹窗文字核对通过，空目录与错误目录 stderr 文案测试通过；Burn -quiet 空目录返回 1 且未传 --show-errors；安装器有效目录哈希/回滚、Python 33/33、git diff --check 通过。Burn 可见窗口前台弹窗位置和真实 UAC/3FUI 安装仍待人工验收。

- Latest fix (2026-09-24 15:22): 用户实机确认“未选目录但勾选许可条款后安装按钮可点击”。WiX 标准 BA 会在勾选时强制启用 InstallButton，覆盖主题 EnableCondition。主题现仅在 InstallFolder 非空时显示真正的 InstallButton；空目录同位置显示无安装动作的禁用占位按钮。便携载荷继续拒绝空目录。新候选 11,780,040 bytes / SHA-256 EA08A96C4EFC781E9EE34C18F26000F6C3AB2F18A160936A52BA036419D5F342。
- Latest verification: WiX Release build 0 警告/0 错误；Burn -quiet 空目录返回 1，日志确认空参数、载荷报错及缓存清理；安装器门禁/有效目录哈希/失败回滚通过；Python 33/33；git diff --check 无错误。可见窗口的按钮状态仍需用户关闭旧窗口并重新打开新候选目视复验。

- Latest correction (2026-09-24 15:13): 用户明确要保留 PR #7 的完整 WiX 安装窗口。候选已恢复主题、目录选择、进度页及中文 RTF；Bundle 只调用便携复制载荷，不再使用 MSI 或项目 HKLM 安装位置键，也不在 Windows“已安装的应用”显示。WiX Burn 自身仍留下隐藏注册和缓存。
- Latest verification: 安装器 11,779,936 bytes / SHA-256 5B1C410B92925817BD5225CBFBC2ECA22A5D0B48ECE527D5247FE6E6C5841943；Release 构建 0 警告/0 错误、安装器目录门禁/哈希/回滚、自更新回滚、Python 33/33 通过。真实窗口视觉、UAC 和实际 3FUI 安装待人工验收；未发布。

- Latest fix (2026-09-24 14:42): 用户实机反馈双击轻量安装器会闪黑色命令行框。`InstallerBundle` 现只把外层安装器 PE 子系统改为 GUI，释放的运行 EXE 恢复为控制台；另对 Windows 临时占用造成的事务目录清理失败做短暂重试。最新安装器 19,688,567 bytes / SHA-256 `3DC4761624BC7B007548FE676A6F91F3168BB3CCEC1C11F38B98F8091A026543`。
- Latest verification: PE 门禁确认安装器 GUI=2、运行 EXE Console=3；双击文件选择窗口截图目视；安装器两次完整门禁均通过（错误目录/有效目录/哈希/回滚/无事务残留），自更新及回滚通过；运行 EXE `--version`=1.3.5。仍未真实 UAC 安装或发布。
- Latest correction (2026-09-24 14:22): 用户指出新 WinForms 安装器过大且难用。已删除该工程，改用 PR #7 迁移 WiX 前的 `InstallerBundle` 自解包格式，在现有 CLI 内提供系统 `FFmpegFreeUI.exe` 文件选择窗口和显示完整 Plugin 路径的确认框；双击候选 UI 已截图目视确认。安装器从 65,832,375 bytes 降至 19,688,089 bytes，SHA-256 `8B23BD93973B1A550379AB29BF0EB4F6831FAE7C7CBD68BC371359CE96DF5DA6`。插件自更新仍独立于安装器。
- Latest verification: 改造后 Release publish、安装器错误目录/正确目录/文件哈希/回滚、自更新与回滚、Python 33/33 通过；真实 3FUI 安装与 UAC 目录仍未验收。源码/记录仍未提交、未推送、未合并、未发布；版本仍为 1.3.5 测试候选。
- Latest objective/state (2026-09-24 13:29): 按用户要求将 PR #7 安装机制改为首次安装专用便携安装器；后续更新由插件下载内嵌 DLL 的 `videoenhancer.exe` 自行事务替换 EXE/DLL，用户删除安装器不影响更新。安装器不写 HKLM、不注册 Windows 应用；aria2-next 仅随首次安装或手动 ZIP 提供，本体更新不覆盖它。
- Latest implementation/Git: 独立 worktree `C:\Codex Program\VideoEnhancer-pr7-installer-20260924`，分支 `fix/pr7-portable-installer`；沿用 PR #7 早期 `cli/InstallerBundle.cs`，新增 `cli/InstallerManager.cs`、`cli/SelfUpdateManager.cs`，恢复 `cli/ApplicationLayoutManager.cs`，移除不再使用的 WiX Bundle/Package 文件；发布脚本分别输出运行时更新包与首次安装器。源码与文档尚未提交、未推送、未合并、未发布。
- Latest verification: Release 解决方案发布成功；Python 33/33、发布门禁 5/5；便携安装器错误目录/有效目录/哈希/故障回滚通过；自更新 EXE/DLL 哈希、aria2 保留、迁移故障回滚通过。仍需真实 3FUI 窗口目视检查和受保护目录/UAC 验收。旧 WiX 误装在 `C:\Program Files\FFmpegFreeUI\` 的 MSI 登记未擅自卸载。
- Latest objective/state (2026-09-20 10:50): 1.3.5 已按正式流程发布完成；GitHub、ModelScope Releases、Models 备用 EXE、标签、远端分支和本机部署均已核验。
- Latest files/Git: 分段页面高 DPI 列宽、开关比例、刷新按钮列宽与对齐修复已由发布提交 `070cd8b` 推送；`v1.3.5` 精确指向该提交。当前只剩本次发布收尾记录待提交。
- Latest verification: 正式发布构建 0 警告/0 错误；Python 31/31、发布门禁 5/5、安装器五场景、更新器七场景、Backend 更新器 6/6；Backend 2026.09.19.1 审计 +0/~0/-0。GitHub 与 ModelScope 三资产实际下载及 Models 备用 EXE 哈希全部一致。
- Latest deployment: 最终 EXE 16,989,156 bytes / `38C606EC69B59E3B8E620280753BEE632A3D25BF56155E94C1A048AF291F910E`；手动 ZIP 14,003,730 bytes / `B6893EB0C73791767EF1121868FBD9A430DDDD3D7182182D81B812C831896B74`；stable.json 621 bytes / `032B4502993A5BACBF1DEDA49BEC91C9950D08744CF6A202C1FA12A07DD0A6A2`。本机 EXE/DLL 已部署，EXE 返回 1.3.5；备份位于 `C:\Users\maxzr\AppData\Local\Temp\videoenhancer-1.3.5-before-final-release-deploy-20260920-104937`。
- Latest remaining issue/research: 用户重启 3FUI 后继续目视确认分段页面在当前 DPI/窗口宽度下的最终效果；无发布阻塞。

- Current objective: 完成 1.3.4 分段超分页的高 DPI 布局修复和真实 3FUI 视觉验收，同时保持处理逻辑与已发布资产不变。
- Current state: 原矩阵两个 GIMM FAIL_EXIT 已定位为 DAT2/AniToon-RPLKSRL CUDA FP16 超分 NaN 并以 FP32 规则修复，两个组合各 7 帧哈希不同。RTX sidecar 现通过命名管道输出 NV12/P010/X2BGR10 原始帧，宿主 FFmpeg 执行用户 `ffmpeg-settings`；失败会清理零字节/部分输出；低分辨率输入自动走软件解码上传 D3D11。1.3.1、模型补全和手动安装包均已发布。
- Current verification: sidecar 单测 89/89；模型规则单测 5/5；RTX 编码专项 8/8（HQ/UHQ×MKV/MP4、libx264、libx265、libsvtav1、精确 map、H.264/P010 门禁）通过；RTX VSR 专项 18/18、HDR 相 15/15 通过。最终安装版用《缎带英雄》1080p 真实 72 帧样本按 p7/uhq/vbr/cq28/P010 原参数输出 3840x2160 HEVC Main10 yuv420p10le、72/72 帧，日志确认最终 FFmpeg 为 PATH 中的 8.1.2 full build。
- Last active agent: Codex
- Likely next agent: user / ZCode / Codex
- Next recommended step: 用户从当前 `main` 构建/部署候选版本，在真实 3FUI 中重点验收分段页面、关键帧断点、Anime4K、暂停/停止和长视频音画同步；确认后再按递增版本发布。

## Active TODO

- [x] Task: 正式发布 1.3.5。
  - Owner: Codex
  - Status: 发布提交 `070cd8b`、GitHub `v1.3.5`、ModelScope Releases/Models 和本机最终部署均完成。
  - Verification: EXE/ZIP/stable.json 双源实际下载哈希一致；标签与远端 main 均指向 `070cd8b`；Backend 2026.09.19.1 审计无变化且未重复发布。
  - Remaining: 仅待用户重启 3FUI 做最终视觉确认。

- [ ] Task: 修复 1.3.4 分段超分页高 DPI 布局。
  - Owner: Codex / user
  - Status: 两轮源码修复、自动化验证和本机 DLL 部署已完成；第二轮将刷新按钮列加宽至 180px，并用与左侧文件框相同的编辑区坐标和高度对齐。
  - Verification: Release 构建 0/0，专项 8/8、Python 31/31；本机 144 DPI 探针已确认上一轮表格与开关尺寸，本轮按钮与文件框共用 `Y=31`、`Height=max(32, ClientHeight-34)`。
  - Remaining: 用户重启 3FUI 视觉验收刷新按钮；通过后提交，若对外发布则使用高于 1.3.4 的新补丁版本。

- [x] Task: 正式发布 1.3.4 与 Backend 2026.09.19.1。
  - Owner: Codex
  - Status: 已按完整包→增量包→channel→GitHub Release→ModelScope Releases 顺序发布并完成独立回读；本机也已部署并通过 Backend 增量更新。
  - Backend audit: 线上 2026.09.12.1 基线包大小 2,790,833,001，SHA-256 `E607456F2384F3339F90E28E7C4965CCCACC40F591AB250276E276ECF01636F1`；候选完整包大小 2,645,812,004，SHA-256 `42A90717B30CCFE80E5ED3016AD9BD66F0FD59265363397342F3F0B1267DE97C`；差异 +0/~2/-0。
  - Remaining: 仅待用户重启 3FUI 后完成真实窗口与长视频体验验收。

- [x] Task: 评估并安全合并 PR #4、#5。
  - Owner: Codex
  - Status: 两项 PR 已合并并推送；PR #5 合并提交 `637914a`，PR #4 合并提交 `15bfdc5`，兼容修复提交 `9194450`。
  - Risk fixes: PR #4 原样会使自动更新用户丢失旧 AppData 设置并取消外置 `core-path`；已增加插件首次加载迁移和只读旧路径兼容。PR #5 原提交缺少跨后端 Python 增量；已从用户提供的 `backend.7z` 选择性合入两个脚本并保留便携临时目录修复。
  - Verification: 解决方案发布构建 0/0、Python 31/31、安装器/更新器/后端更新器门禁全部通过；真实 NCNN→CUDA 4 帧输出 128×128、4/4 帧。
  - Remaining: 未部署 EXE/DLL、未发布新版本；真实宿主 UI 与长视频验收仍待用户执行。

- [x] Task: 审查并整合 GitHub PR #3，同时保留候选 1.3.3 修复。
  - Owner: Codex
  - Status: PR #3 已通过 GitHub 正式合并，合并提交 `c80c351`；修复整合提交 `7af6e21` 已推送 `fork/main` 并作为 `v1.3.3` 标签目标。
  - PR review: PR 将手工 `vbc`/`build.ps1` 迁移为 `VideoEnhancer.slnx`、SDK 风格插件项目和 CLI `ProjectReference`，统一插件构建/嵌入，并将解决方案发布产物收口到 `Artifacts\VideoEnhancerInstaller.exe`、`Artifacts\VideoEnhancer.zip`；删除不参与运行时的 `PluginDesigner`。业务处理文件无覆盖，实际重叠仅 `cli/VideoEnhancer.csproj` 的 1.3.3 版本号，自动合并成功。
  - Verification: PR 原始分支独立 worktree 构建 0/0、Python 24/24，直接 CLI publish 不生成 `Artifacts`、解决方案 publish 正确生成两个产物；组合版构建 0/0、Python 29/29、安装器、更新器、Backend 更新器 6/6、发布门禁 5/5、ZIP 哈希一致性和滚动布局探针均通过。
  - Deployment: 使用正式发布产物覆盖本机 3FUI 的 DLL/EXE，部署前确认相关进程未运行；备份位于 `C:\Users\maxzr\AppData\Local\Temp\videoenhancer-1.3.3-before-final-release-deploy-20260916-104542`。
  - Remaining: 仍需用户在真实 3FUI 窗口验收 HDR 输入、滚动、RTX 预览及任务停止/移除。
  - Relevant files: `VideoEnhancer.slnx`, `VideoEnhancerPlugin/VideoEnhancerPlugin.vbproj`, `cli/VideoEnhancer.csproj`, `release/*.ps1`, `VideoEnhancerPlugin/HostQueueAccess.vb`, `VideoEnhancerPlugin/PluginPanel.vb`, `VideoEnhancerPlugin/StopControl.vb`。

- [x] Task: 修复 RTX HDR 参数、任务控制与预览兼容性（候选 1.3.3）。
  - Owner: Codex
  - Status: 已完成源码、正式构建、本机部署与 1.3.3 双源发布；标签和远端资产均已回读核验。
  - Implementation: PluginConfig 增加四项 HDR 整数参数并在加载时默认/钳制；PluginPanel 使用 LakeUI ModernNumericUpDown 两列两行布局；HDR 控件子类屏蔽滚轮、方向键/Page 键和不可见末端按钮步进，按钮区域压缩为 1px、箭头透明并增加文本内边距；QueueHook 传递四个 CLI 参数；CLI 校验并写入 sidecar `processing.hdr`；HostQueueAccess 反射优先调用 `获取队列快照()`/`根据ID获取任务()`；预览、进度、暂停和停止均移除旧 getter 绑定；按钮按字段名、控件名和文本识别；插件任务停止先恢复 CLI、写共享内存并等待 12 秒后调用宿主停止 API，CLI 取消后给 FFmpeg 8 秒封装窗口，并用成功标记保护可读取的部分输出。
  - Verification: CLI build/publish 0 errors（2 条既有 CA1416）；LakeUI 5.9 插件构建通过；Python 29/29；HDR sidecar JSON 反射收到 37/151/73/1370；四项越界 CLI 参数明确退出 2；源码无直接 `编码队列_v6.队列` 调用；`git diff --check` 通过。
  - Deployment: 当前 3FUI 6.2.20 未运行时完成备份和覆盖；备份 `C:\Users\maxzr\AppData\Local\Temp\videoenhancer-1.3.3-before-deploy-20260915-132742`；安装 EXE `--version` 返回 1.3.3。待用户重启后验收真实按钮、预览和停止残留进程。
  - Relevant files: `VideoEnhancerPlugin/HostQueueAccess.vb`, `VideoEnhancerPlugin/PluginConfig.vb`, `VideoEnhancerPlugin/PluginPanel.vb`, `VideoEnhancerPlugin/QueueHook.vb`, `VideoEnhancerPlugin/StopControl.vb`, `VideoEnhancerPlugin/PreviewEngine.vb`, `VideoEnhancerPlugin/BackendProgress.vb`, `VideoEnhancerPlugin/PauseControl.vb`, `cli/Program.cs`, `cli/RtxVideoBackendClient.cs`, `cli/tests/test_rtx_hdr_controls.py`.

- [x] Task: 修复 HDR 参数加入后工作台向上滚动出现顶部空白。
  - Owner: Codex
  - Status: 已定位 LakeUI `ModernPanel` 的设计坐标误记问题，修复完成并随 1.3.3 正式发布、部署。
  - Implementation: `SyncUpscaleRootBounds` 不再无条件执行 `root.SetBounds(0, 0, ...)`，而是在当前滚动坐标下保留根容器位置，仅同步宽度/高度；无滚动偏移且位置异常时才归零，避免滚动中的负位置被重新写入设计坐标。
  - Verification: 临时 .NET 布局探针在旧逻辑下稳定复现 `Top=-168 -> Sync Top=0 -> 回顶 Top=168`，新逻辑验证为 `Top=-168 -> Sync Top=-168 -> 回顶 Top=0`；插件构建通过，Python 29/29，`git diff --check` 通过。
  - Deployment: 当前 3FUI 未运行时完成目标 DLL 备份和覆盖，源/目标 SHA-256 均为 `4A328E31D18105CE1D433546CA10B2E97A845279EC3D75FD9063D97E407E59D5`。
  - Relevant files: `VideoEnhancerPlugin/PluginPanel.vb`, `cli/tests/test_rtx_hdr_controls.py`。

- [x] Task: RTX 处理帧交回 3FUI FFmpeg，并支持硬件/软件编码。
  - Owner: Codex
  - Status: sidecar 只执行 RTX VSR/HDR 与 D3D11 帧下载，通过命名管道输出 rawvideo；插件从宿主目录/PATH 解析 FFmpeg，并用原始 `ffmpeg-settings` 编码、映射和封装。历史 `bin\ffmpeg` 仅兼容回退。
  - Verification: sidecar 89/89；编码专项 8/8；RTX VSR 18/18；HDR 15/15；真实 1080p/72 帧样本按用户 UHQ/P010 参数输出 4K Main10 72/72。
  - Blockers: 仅完整电影待用户长时间实跑；低分辨率 D3D11VA 限制已处理。
  - Relevant files: `cli/Program.cs`, `cli/RtxVideoBackendClient.cs`, `cli/tests/rtx_nvenc_regression.py`, `cli/tests/gpu_matrix_runner.py`; sidecar `backend/src/jobs/job_types.h`, `backend/src/api/json_dto.cpp`, `backend/src/video/ffmpeg/ffmpeg_transcode_pipeline.cpp`, corresponding unit tests.

- [x] Task: 验证 PR1 文件框与 RTX HDR UI 修复。
  - Owner: user / Codex
  - Status: 用户已确认文件框空心和 RTX HDR 换行两处视觉问题修复。HDR 使用透明、右对齐、180px 状态列的 LakeUI `ModernButton`，不走 `HtmlColorLabel` 换行路径；`CreateOfficialValueBox` 已恢复背景依赖。
  - Verification: LakeUI 5.9 构建、Python `22/22`、安装 DLL SHA-256 `9ECC9B444321C08BED63BC63B618AEEAB530A3EF71476AC29B38ACC72A906555` 与部署一致；用户实机确认通过。
  - Blockers: 无；PR 仍保持未合并，等待整体验收。
  - Relevant files: `VideoEnhancerPlugin/PluginPanel.vb`

- [ ] Task: 验证换入的完整后端 python-20260912.7z。
  - Owner: user / ZCode
  - Status: 已将 `C:\Users\maxzr\Downloads\python-20260912.7z`（3,086,044,296 bytes，SHA-256 `DBC365B74E6D8AC039DFEAF218644202EAA7568118CA2320975A6358285BA78C`）解压换入 `Plugin\videoenhancer\python`，包内标记保持 2026.08.26.1；冒烟验证通过，等待用户真实任务测试。
  - Verification: 全树 blake2b 对比仅缓存/行尾差异；Python 3.12.9、torch 2.9.0+cu130 CUDA available、onnxruntime 1.29.0；`--backend-status` current；仓库测试 `22/22`。旧后端备份于 `C:\Users\maxzr\AppData\Local\Temp\rve-backend-prev-20260912-144723\python`。
  - Blockers: 未做真实视频任务回归；标记版本号是否改为 2026.09.12.x 留待用户决定；本轮按用户要求未走发布流程。RTX sidecar 缺失问题已解决：组件已部署且实跑验证通过。
  - Relevant files: `C:\Program portable\3FUI\3FUI\Plugin\videoenhancer\python`（安装位置，非仓库文件）

- [ ] Task: PR #1 本地安装回归。
  - Owner: user / Codex
  - Status: PR head 已建立独立本地分支并编译、安装；等待用户真实 3FUI 窗口测试，当前不合并、不发布。
  - Verification: `22/22` Python 测试；插件 LakeUI 5.9 构建；CLI 1.3.0 发布构建；安装目录 DLL/EXE 与构建源一致，后端脚本经 CLI 启动同步后仅改变为 CRLF 行尾且 UTF-8 内容一致；安装前备份已保存；本轮插件 DLL 已再次覆盖安装。
  - Blockers: 尚未完成真实宿主启动、分段页交互、RTX 功能和实际视频任务回归；PR 分支未合并。
  - Relevant files: `VideoEnhancerPlugin/SegmentedUpscalePage.vb`, `VideoEnhancerPlugin/PluginPanel.vb`, `cli/Program.cs`, `cli/embedded-tools/rve-image-backend.py`, `cli/embedded-tools/rve-segmented-backend.py`

- [x] Task: 完成 1.3.0 本地功能更新与部署。
  - Owner: Codex / user
  - Status: RTX sidecar、分辨率映射、HDR 兼容门禁、右键超分配置及逐项删除、图片清空、两个时序图片桥和分段超分已实现；最终 1.3.0 EXE/DLL/分段后端已部署到指定便携 3FUI，版本保持 1.3.0。
  - Verification: CLI Release 构建 0 错误（2 个既有 Windows 平台分析警告）；LakeUI 5.9 / FFmpegFreeUI 6.2.16 插件构建通过；Python 22/22；安装版 `--version` 和 RTX 环境检查通过；RTX VSR 320×180→640×360；RTX HDR 输出 HEVC Main10 BT.2020/PQ；BasicVSR++ 64×48→256×192；FlashVSR 64×64→256×256；两帧视频分别用两个 NCNN 模型处理并由同一 FFmpeg 输出为 256×256/2 帧，非法重复边界及混合倍率均被拒绝；用户已确认此前真实 3FUI UI 无问题，本轮新增分段页待重启后目视确认。
  - Blockers: 本地开发与部署无阻塞。RTX SDK 组件公开再分发前需复核 NVIDIA 许可；右键菜单必须由用户在“右键超分”页选择模型后主动点击“应用设置”，本轮未代为写入注册表。
  - Git: 功能提交 `9551296` 已推送至 `user-Wing/VideoEnhancer:feat/rtx-segmented-upscale-1.3.0`；已创建 `maxzrb/VideoEnhancer#1`。`user-Wing/main` 通过双父合并提交 `becc929` 保留原有历史并采用 maxzrb 维护线文件树，没有强推。
  - Relevant files: `cli/Program.cs`, `cli/RtxVideoBackendClient.cs`, `cli/embedded-tools/rve-image-backend.py`, `cli/embedded-tools/rve-segmented-backend.py`, `VideoEnhancerPlugin/PluginPanel.vb`, `VideoEnhancerPlugin/SegmentedUpscalePage.vb`, `VideoEnhancerPlugin/PluginConfig.vb`, `VideoEnhancerPlugin/QueueHook.vb`, `VideoEnhancerPlugin/ShellUpscaleMenu.vb`

- [x] Task: 按 LakeUI 官方背景映射链修复滚动断层。
  - Owner: Codex
  - Status: 第一版经用户实机截图否定后，按官方注册表的坐标依赖规则重新修改 `VideoEnhancerPlugin/PluginPanel.vb`；滚动页内每个 LakeUI V5 控件均显式绑定 `ModernPanel1`，让父级滚动触发所有子表面重绘。
  - Verification: `git pull --ff-only` 已同步；官方 LakeUI 5.6、FFmpegFreeUI 6.2.9 和插件构建均 0 错误；`git diff --check` 通过；无宿主启动的对象树核对显示滚动根下 76 个控件全部具有显式来源且均指向 `ModernPanel1`；第二版源 DLL 与实际 `C:\Program portable\3FUI\plugin\videoenhancer.3fui.dll` 均为 SHA-256 `A31B0CB0A0159AB1726424ED4BA6E343E3E987DBFE80E5D6B9DFA3FE6BB81828`。
  - Blockers: 无本轮源码阻塞；DPI 矩阵、500 帧预览压力、四宫格完整鼠标流程仍属于后续实机回归。
  - Relevant files: `VideoEnhancerPlugin/PluginPanel.vb`, `VideoEnhancerPlugin/build.ps1`

- [ ] Task: 修复自定义模型菜单打开前 LakeUI 原生 Overlay 短暂闪现。
  - Owner: Codex / user
  - Status: 已确认原因是官方 `ModernComboBox` 默认 300ms 展开/关闭动画与插件 `DropDownOpened -> DroppedDown=False -> 自定义菜单` 流程叠加；仅对 `_cmbModel`、`_cmbInterp` 设置 `DropDownAnimationDuration=0`。
  - Verification: 官方 LakeUI 5.6 源码对照完成；插件构建 0 错误；无宿主实例化核对显示两个模型框动画值均为 0 且仍为 Overlay；第三版 DLL 已部署，待用户实机确认。
  - Blockers: 当前 computer-use 原生管道不可用，无法代替用户确认点击动画；需用户在真实 3FUI 点击模型菜单并再次滚动。
  - Relevant files: `VideoEnhancerPlugin/PluginPanel.vb`, `VideoEnhancerPlugin/LakeLayoutPanel.vb`

- [x] Task: 发布 1.2.1 双源本体资产。
  - Owner: Codex
  - Status: `bcb4e42` 已推送到 `fork/main`；GitHub `v1.2.1`、ModelScope Releases 和 Models 备用 EXE 均已正式发布。
  - Verification: EXE `16,865,524` bytes，SHA-256 `c67c8dffd05f8b52ab3ad3c657e51048c922677bca3afea89a2ad71f7a3ee005`；stable.json `518` bytes，SHA-256 `e80955ea69aba4b19dad68725c74d3f5cb961f34276117447ab40aca82730433`；Backend 2026.08.26.1 审计 `0 add / 0 replace / 0 delete`；安装器、更新器、发布门禁和 Backend 事务测试通过；全量 Python 测试 `18/18`；GitHub、ModelScope Releases 和 Models 备用 EXE 三处远端回读大小/哈希一致，Release Notes 一致。
  - Blockers: 无发布阻塞；真实宿主第三版 DLL 的模型菜单点击回归仍待用户重启后确认。
  - Links: GitHub `https://github.com/maxzrb/VideoEnhancer/releases/tag/v1.2.1`；ModelScope Releases `https://www.modelscope.cn/datasets/AerithDream/VideoEnhancer-Releases`；Models 备用路径 `Plugin/videoenhancer.exe`
  - Relevant files: `VideoEnhancerPlugin/PluginVersion.vb`, `cli/VideoEnhancer.csproj`, `README.md`, `release/release-notes.txt`, `version/版本迭代记录.md`

- [x] Task: 发布 1.2.0 双源本体资产。
  - Owner: Codex
  - Status: 1.2.0 已提交为 `10ed1f2`，推送 `fork/release/1.2.0` 与 `fork/main`，GitHub `v1.2.0` Release、ModelScope Releases 和 Models 备用 EXE 均已上传并回读核验；Backend 基线 2026.08.26.1 审计为 0 变化。
  - Verification: EXE 16,882,467 bytes，SHA-256 `7cd35717ab2e3e268eb64a7ad7507c6655ce656906d4af18547e7a9041c6ec58`；stable.json 782 bytes，SHA-256 `add585dc126ca83ba8f9703fb31a9a6796d2f29157232f1c31e69be2beca25e8`；GitHub 与 ModelScope stable.json、GitHub/ModelScope 两份 EXE 内容一致；发布门禁 5/5、安装器和更新器测试通过。
  - Blockers: 无发布阻塞；DPI 矩阵、500 帧预览压力、四宫格完整鼠标流程和运行时控件树递归仍属于迁移后续回归。
  - Relevant files: `VideoEnhancerPlugin/PluginVersion.vb`, `cli/VideoEnhancer.csproj`, `README.md`, `release/release-notes.txt`, `version/版本迭代记录.md`

- [ ] Task: 完成 LakeUI 5.1 全插件原生控件迁移并完成宿主回归。
  - Owner: user / Codex
  - Status: `PluginPanel.vb`、`LakeLayoutPanel.vb`、`QuadGridControls.vb`、`QuadGridForm.vb` 和 `build.ps1` 已完成迁移；六页、编辑器和四宫格均只保留 LakeUI 可视控件或明确允许的宿主/系统外壳。
  - Verification: 实际 3FUI 6.2.3 + LakeUI `5.3.0.0` 构建并安装成功；LakeUI 5.1 最低版本门禁保留，旧 5.0/3.x 拒绝逻辑已验证；迁移源码及整个插件无旧 WinForms 可视控件/自绘视觉类静态命中；CLI Python 测试 18/18；六个标签页逐页点击后宿主均保持 `Responding=True`；组合框首字偏移、窄列末尾裁切和超分根容器右半空白已修复并截图确认；普通窗口/最大化/还原/宿主窄化连续采样时根内容宽度稳定；产物引用为 `FFmpegFreeUI 6.2.3.0`、`LakeUI 5.3.0.0`。
  - Blockers: 尚未完成 100%/125%/150% DPI 截图矩阵、500 帧预览压力、四宫格完整鼠标流程和运行时控件树递归检查；发布代码与记录已提交，工作树当前干净。
  - Relevant files: `VideoEnhancerPlugin/PluginPanel.vb`, `VideoEnhancerPlugin/LakeLayoutPanel.vb`, `VideoEnhancerPlugin/QuadGridControls.vb`, `VideoEnhancerPlugin/QuadGridForm.vb`, `VideoEnhancerPlugin/build.ps1`

- [x] Task: 发布 1.1.3。
  - Owner: Codex
  - Status: 已创建提交、推送 `fork/main`、创建 GitHub Release，并同步 ModelScope Releases 与 Models 备用 EXE；Backend 2026.08.26.1 无变化。
  - Relevant files: `VideoEnhancerPlugin/PluginVersion.vb`, `cli/VideoEnhancer.csproj`, `release/release-notes.txt`, `version/版本迭代记录.md`
  - Verification: EXE 16,877,609 bytes，SHA-256 `16e1728a99a2ed568909bdc44df1c7df7cea7356a4135e7661e57a127859bf50`；stable.json 673 bytes，SHA-256 `0da8e3cb5e49a62b199c048a3ecac41f36d38db539c9cfdffa29c25ad3060276`；GitHub 资产摘要、ModelScope stable.json 和两处 ModelScope EXE 回读一致。

- [x] Task: 为用户模型增加 Delete 键和右键删除。
  - Owner: Codex
  - Status: 已新增 `--delete-user-model`，删除前二次确认，删除成功后刷新用户清单和工作台模型菜单；CLI 隔离目录实测通过。
  - Relevant files: `cli/UserModelCatalog.cs`, `cli/Program.cs`, `VideoEnhancerPlugin/PluginPanel.vb`, `VideoEnhancerPlugin/README.md`
  - Verification: 全量 Python 测试 18/18；CLI 与插件构建成功；安装版 `--help` 已暴露删除参数；真实 3FUI 目录 DLL/CLI 哈希与构建产物一致。


- [ ] Task: 在真实 3FUI 中验证模型菜单布局与悬浮提示。
  - Owner: user / Codex
  - Status: 源码构建、93 项内置简介覆盖、教程文案收敛、滚轮锁定控件和已安装 DLL 反射探针均通过；尚未在真实宿主中用鼠标确认提示位置、换项、关闭行为和滚轮体验。
  - Relevant files: `VideoEnhancerPlugin/PluginPanel.vb`
  - Notes/blockers: 当前自定义 `ModernContextMenu` 没有公开的菜单项 Tooltip 属性，提示控制器通过当前 LakeUI `MenuPopupForm` 的私有布局信息定位悬停项；滚轮锁定通过 `WheelLockedComboBox.WndProc` 拦截 `WM_MOUSEWHEEL/WM_MOUSEHWHEEL`，LakeUI 版本变化后需重新核对。

- [x] Task: 排查 RVE 对 OpenModelDB 600 余个模型的支持情况。
  - Owner: Codex
  - Status: 已基于 OpenModelDB 官方 671 模型快照、本机 RVE/Spandrel 注册表、CLI 发现规则、RGB/ONNX 输入契约和现有代表模型运行证据完成审计。
  - Relevant files: `docs/openmodeldb-rve-support-audit.md`, `cli/Program.cs`，安装后端 `src/pytorch/UpscaleModelWrapper.py`、`src/onnx/UpscaleONNX.py`
  - Notes/blockers: 620 是静态兼容上限，不是 620 个权重逐一实跑；当前设备无 NVIDIA，19 个 ONNX-only 候选和大规模 TensorRT 仍需后续实机矩阵。

- [x] Task: 发布 1.1.2。
  - Owner: user / Codex
  - Status: 已完成 GitHub、ModelScope Releases 和 ModelScope Models 备用 EXE 发布；Backend 2026.08.26.1 无变化。
  - Relevant files: `VideoEnhancerPlugin/PluginVersion.vb`, `cli/VideoEnhancer.csproj`, `README.md`, `release/release-notes.txt`, `version/版本迭代记录.md`
  - Verification: Release `v1.1.2` 正式、非草稿、非预发布；EXE 16,878,865 bytes，SHA-256 `da4bbd96d2f21792c1b85abb1c185e2366e766b7131d8e8f2cc2379abb754605`；stable.json 718 bytes，SHA-256 `cecb2247cf80e8ca75f8bca9817007ef5680fe79ade58c589bb8b4065bb4b55a`；GitHub 与 ModelScope 清单一致。

- [x] Task: 增加超分/补帧独立半精度开关并完成 Windows 10 依赖审计。
  - Owner: Codex
  - Status: 已完成实现、实机 UI/GPU 回归、隔离安装测试和依赖自检；按用户要求只本地提交，不推送、不发布。
  - Relevant files: `VideoEnhancerPlugin/PluginConfig.vb`, `VideoEnhancerPlugin/PluginPanel.vb`, `VideoEnhancerPlugin/QueueHook.vb`, `cli/Program.cs`, `cli/embedded-tools/rve-ordered-backend.py`, `cli/tests/test_rve_ordered_backend.py`, `README.md`
  - Notes/blockers: `videoenhancer.exe` 为自包含单文件，无需单独安装 .NET；便携 Python/原生扩展仍要求 Microsoft VC++ 2015-2022 x64 运行库。当前 PyTorch 为 CUDA 13.0，NVIDIA 后端需要 580+ 驱动。NCNN 依赖显卡驱动的 Vulkan 运行时，不需要 Vulkan SDK。

- [ ] Task: 内置模型能力清单并修复尺寸依赖。
  - Owner: Codex
  - Status: 实现与 GPU/CPU 探针验证完成，等待提交；插件源码已改但本机缺少构建所需的 `FFmpegFreeUI.dll`/`LakeUI.dll` 临时引用目录。
  - Relevant files: `cli/model-capabilities.json`, `cli/ModelCapabilityCatalog.cs`, `cli/Program.cs`, `cli/VideoEnhancer.csproj`, `cli/tests/test_model_capabilities.py`, `VideoEnhancerPlugin/PluginPanel.vb`
  - Notes/blockers: 93 项清单覆盖 ncnn/cuda/tensorrt/onnx/flashvsr/basicvsrpp 的 21/42/36/28/1/1 项；RealHatGAN 1920x1080 在固定 512/256 块下于 6GB 显存 OOM，128 块实验通过但按用户要求仅作为测试记录，不进入能力清单或默认值。

- [ ] Task: 发布独立子目录布局版本 1.1.0。
  - Owner: Codex
  - Status: 已发布并按用户授权覆盖一次；功能提交、标签、三源资产和稳定清单均已回读一致。
  - Relevant files: `cli/ApplicationLayoutManager.cs`, `cli/Program.cs`, `VideoEnhancerPlugin/PluginConfig.vb`, `VideoEnhancerPlugin/PluginUpdater.vb`, `release/test-installer.ps1`, `release/test-updater.ps1`, `cli/tests/gpu_matrix_runner.py`
  - Notes/blockers: 679 项矩阵为 677 PASS、2 SKIP_OOM、0 失败；两条 OOM 均为 FlashVSR+GIMM 在 6GB RTX 3060 上的双流程，按用户要求不再重测。Backend 无发布内容变化。

- [x] Task: 发布自更新重启修正版 1.0.9。
  - Owner: user / Codex
  - Status: 已完成源码提交、标签、GitHub/ModelScope 发布及三源回读；远端 1.0.8 保持不变。
  - Relevant files: `cli/Program.cs`, `release/test-updater.ps1`
  - Notes/blockers: 本机 1.0.7→1.0.8 失败由目标 EXE 共享冲突触发；旧更新器失败后不重启。1.0.9 的下载 EXE 本身会作为临时更新器执行，因此 1.0.7 可直接使用新逻辑升级到 1.0.9。

- [x] Task: 实现后端增量更新机制。
  - Owner: Codex
  - Status: CLI、插件、补丁生成器、协议示例、隔离测试和发布说明均已完成；6 类事务测试全部通过。1.0.8 仍暂停。
  - Relevant files: `cli/BackendUpdateManager.cs`, `cli/Program.cs`, `VideoEnhancerPlugin/PluginPanel.vb`, `release/build-backend-patch.ps1`, `release/backend-channel.example.json`, `release/test-backend-updater.ps1`, `release/发布流程.md`
  - Notes/blockers: 尚未制作或上传真实生产补丁/`Backend/channel.json`，也未在实际 3.4GB 后端目录执行升级。远端通道上线前新 UI 会保守显示“更新信息不可用”，不会回退到旧覆盖解压。

- [x] Task: 准备并验证首个生产后端增量通道。
  - Owner: user / Codex
  - Status: 2026.08.25.1 完整包、生产补丁和 channel 已按顺序上传并激活；SDK 文件树大小/哈希、channel 回读和真实客户端增量选择均通过。
  - Relevant files: ModelScope `Backend/python_YYYYMMDD.7z`, `Backend/patches/*.7z`, `Backend/channel.json`
  - Notes/blockers: 需要取得已公开完整包对应的基线目录与目标目录，选择不会被旧 CLI 自修补的稳定哨兵。发布脚本现已强制审计并自动制包；上传顺序固定为完整包、补丁、最后 channel，回读核对前不会创建 GitHub Release。未获恢复发布指令前不上传。

- [x] Task: 完成 RTX 3060 代表模型 GPU 兼容矩阵。
  - Owner: Codex
  - Status: 578 项全部取得终态：576 PASS、2 SKIP_OOM、0 功能失败/超时。单模型 50/50、同后端 150/150、跨后端 376/378 通过；FlashVSR + GIMM 两种顺序因 6GB 显存不足按用户要求不再重测。
  - Relevant files: `cli/Program.cs`, `cli/tests/gpu_matrix_runner.py`, `test-results/gpu-matrix/*`
  - Notes/blockers: GIMM 通常使用 320x240/4 帧；时序超分 + GIMM 使用已验证数值稳定的 256x192/4 帧；其余动态模型使用 96x64/4 帧，静态 ONNX SwinIR 使用 320x240。AnimeSR/SwinIR/CRAFT 已验证不支持当前 TensorRT 单图直接 Engine 路径，不进入组合矩阵。

- [x] Task: 评估并接入自有 ModelScope 模型镜像。
  - Owner: user / Codex
  - Status: `AerithDream/VideoEnhancer-Models` 已接入 CLI，排除 `PotPlayer.7z`；用户为测试暂时转为公开，后续仓库可见性由用户自行处理。2026-08-23 增量同步 `Frame-Interpolation/GIMM-VFI.7z`、`GMFSS.7z`、`RIFE.7z`，并补交旧 `RIFE/RIFE.7z` 兼容包。
  - Notes/blockers: CLI 默认仓库、可配置仓库 ID、显式私库令牌、认证错误码和插件提示均已实现；公开模式分页读取后当前清单对界面返回 104 项（旧 Python 包、旧重复 RIFE 归档均隐藏），含 5 个新增 RIFE `.pkl`。真实 CLI 下载 `rife4.6.pkl` 并通过 SHA-256；仍需用户在真实 3FUI 界面刷新模型页确认交互。上游仓库仍没有逐文件 LICENSE/NOTICE/COPYING。

- [x] Task: 审查作者 v1.4.2 测试版并选择性合并。
  - Owner: Codex
  - Status: 已完成反编译审查、选择性移植、构建、隔离模型布局测试、ModelScope 增量同步与实际安装目录部署。
  - Relevant files: `cli/Program.cs`, `cli/README.md`, `VideoEnhancerPlugin/PluginConfig.vb`, `VideoEnhancerPlugin/PluginPanel.vb`
  - Notes/blockers: 保留旧 `models\RIFE` 兼容读取；新版 CUDA 补帧支持 `.pth/.pt/.pkl`；TensorRT 只收 RIFE 权重并由 RVE 自动构建 Engine；BasicVSR++ 优化目录为 1x，官方单 PTH 为 4x。未合并作者硬编码仓库、删除 `core-path`、旧环境检查和 1.4.2 版本号。RIFE heavy 与 GMFSS Base 的短样本 CUDA 已通过，完整视频和 TensorRT 仍待实测。

- [x] Task: 合并时序后端低内存修复并重构 CUDA/TensorRT 补帧能力检测。
  - Owner: user / Codex
  - Status: 低内存实现、内容架构检查、RIFE TRT 专用预构建、CLI/UI 分流和 CPU 测试均已完成；3060 上 RIFE heavy 与 GMFSS Base 的短样本 CUDA 已通过。
  - Relevant files: 外部 Python 后端的 `rve-basicvsrpp-backend.py`、`rve-flashvsr-backend.py`、`src/temporal_video.py`、`src/basicvsrpp/model.py`、`src/flashvsr/nodes.py`；仓库内 `cli/Program.cs`、`VideoEnhancerPlugin/PluginPanel.vb`。
  - Notes/blockers: 通用 `convert_tensorrt.py` 仍只用于单帧超分；RIFE `.pth/.pt/.pkl` 使用独立 flow/encode 构建。GMFSS 当前仅支持 CUDA/PyTorch，不进入 NCNN/TensorRT 列表；全部 3060 实测通过前不发布 1.0.8。

- [ ] Task: 建立独立项目发行与上游同步流程。
  - Owner: user / Codex
  - Status: 独立 SemVer、版本文档、ModelScope Release 生成脚本与公开稳定通道已完成；GitHub Actions/Release 自动发布和正式上游同步清单仍待后续。
  - Planned scope: GitHub Actions/Release、上游提交筛选与同步记录。
  - Notes/blockers: 用户确认不分发 `PotPlayer.7z`；ModelScope 创建数据集时自动填入 Apache-2.0 标签，项目自身与第三方代码/载荷的正式许可证仍需另行整理。

- [x] Task: 借助 ModelScope 实现插件自动更新。
  - Owner: Codex
  - Status: 1.11.1 稳定清单和 ZIP 已上传公开数据集；模型下载页兜底入口、插件与 CLI 更新协议、校验、回滚、重启和下次启动结果提示均已完成。
  - Relevant files: `VideoEnhancerPlugin/PluginUpdater.vb`, `PluginVersion.vb`, `PluginPanel.vb`, `PluginConfig.vb`, `cli/Program.cs`, `release/build-modelscope-release.ps1`, `release/test-updater.ps1`
  - Notes/blockers: 隔离测试覆盖成功、路径穿越、篡改和文件锁回滚；本机过渡 1.11.0 插件已从公开远端发现 1.11.1 并下载通过 SHA-256。仍需用户在真实 3FUI 中点击模型页兜底入口，确认退出和自动重启体验。

- [ ] Task: 为模型镜像建立逐文件来源/授权清单。
  - Owner: user / Codex
  - Status: 初步审计完成，97 个可下载文件均待逐文件核实；已识别 FlashVSR、RIFE、BasicVSR++、Real-ESRGAN、FFmpeg、Git、mkvtoolnix、PotPlayer 等来源线索。
  - Notes/blockers: 上游项目代码许可证不等于预训练权重或转换后 TensorRT/ONNX 文件的再分发授权；PotPlayer.7z 为高风险商业软件，且当前 CLI 不会列出根目录文件；Backend/python 压缩包是混合依赖集合，不能用单一 Apache 标记覆盖。

- [x] Task: 使用 LakeUI 列表视图重构模型下载页，减少窗口缩放时的大量控件布局和重绘。
  - Owner: Codex / next agent
  - Status: implemented in the root mainline; build and runtime data-model checks passed
  - Relevant files: `VideoEnhancerPlugin/PluginPanel.vb`
  - Notes/blockers: `UltraDetailListView` 真实清单为 8 groups / 90 items / 0 child controls；完整宿主中的视觉、鼠标操作和 DWM 动画仍需用户确认。

- [x] Task: 修复视频超分页面从最小化恢复时约 1 秒的背景穿透和分块重绘。
  - Owner: Codex / next agent
  - Status: implemented in the root mainline; official-source comparison and synthetic background verification passed
  - Relevant files: `VideoEnhancerPlugin/PluginPanel.vb`
  - Notes/blockers: 保留宿主背景穿透并删除多层表格父链；相同缩放测试 Paint 为 `31/45`，官方质量页为 `20/16`。截图确认背景与滚动条，仍需实际 3FUI/DWM 动画肉眼确认。

- [x] Task: 为 TensorRT 增加首次使用自动编译、兼容性缓存键、进度/取消和明确错误提示。
  - Owner: Codex / next agent
  - Status: 超分 PTH 的 CLI 预构建缓存已提交；本轮补帧 TensorRT 改为向 RVE 传入 RIFE 权重，由 RVE 根据实际阶段尺寸自动构建缓存；CLI/插件构建及隔离模型筛选通过。
  - Relevant files: `cli/Program.cs`, `VideoEnhancerPlugin/PluginPanel.vb`
  - Notes/blockers: `convert_tensorrt.py` 仍只负责单帧超分，补帧不得调用它；RIFE Engine 由 `InterpolateRifeTorch` 内部构建。ModelScope 已补齐 5 个兼容权重；本机没有 NVIDIA 环境，真实编译仍待测。
- [x] Task: 支持超分与补帧组合，并明确“先补后超/先超后补”策略。
  - Owner: Codex / next agent
  - Status: implemented and committed (`ce75515`); five-stage argument/pipeline integration test passed
  - Relevant files: `preview/1.9.6-preview.1/src/VideoEnhancerPlugin/PluginPanel.vb`, `QueueHook.vb`, `cli/Program.cs`
  - Notes/blockers: TensorRT 补帧现只解析 RIFE PyTorch 权重；同后端 `upscale-first` 包装器在放大后尺寸初始化插帧器，`interp-first` 使用源尺寸，跨后端阶段则由中间视频真实尺寸驱动。真实 GPU 回归仍待执行。

- [ ] Task: 补齐 RIFE PyTorch 权重并完成真实 TensorRT 补帧验证。
  - Owner: user / Codex
  - Status: 代码和模型筛选契约已就绪；`rife4.6.pkl`、`rife4.7.pkl`、`rife4.25.pkl`、`rife4.26.pkl`、`rife4.26.heavy.pkl` 已上传并完成远端 SHA-256/真实 CLI 下载/隔离发现验证。
  - Notes/blockers: 仍需在 NVIDIA 环境验证仅补帧、先超后补、先补后超及二次缓存命中；确认前不发布新版本。

- [ ] Task: 在用户实际 3FUI 环境做视觉与交互回归。
  - Owner: user / next agent
  - Status: pending runtime verification
  - Relevant files: `preview/1.9.6-preview.2/VideoEnhancer-1.9.6-preview.2-win-x64.zip`, `preview/1.9.6-preview.2/dist/*`
  - Notes/blockers: 当前工作区没有可直接启动的完整 3FUI 宿主，也没有可调用的 NVIDIA 驱动环境；源码已用 3FUI 6.1.39 官方程序集编译通过。

- [ ] Task: 验证 preview.2 组合管线的新帧级包装器和 HDR 中间格式。
  - Owner: user / next agent
  - Status: source/build/static checks complete; real RVE dependency and GPU runtime pending
  - Relevant files: `preview/1.9.6-preview.1/src/cli/Program.cs`, `preview/1.9.6-preview.1/src/cli/embedded-tools/rve-ordered-backend.py`
  - Notes/blockers: 当前机缺少完整 RVE Python 依赖和 NVIDIA 环境；包装器已通过 Python 语法检查，CLI 已成功编译/发布。

- [x] Task: 接入 RIFE 后端、转场阈值、动态光流尺度和超分分块尺寸选项。
  - Owner: Codex / next agent
  - Status: implemented; CLI/plugin builds and parameter validation passed
  - Relevant files: `preview/1.9.6-preview.1/src/VideoEnhancerPlugin/PluginConfig.vb`, `PluginPanel.vb`, `QueueHook.vb`, `cli/Program.cs`
  - Notes/blockers: RVE 真实运行仍待 GPU/完整 Python 依赖确认；动态光流只对 CUDA/PyTorch RIFE 透传，TensorRT 由 RVE 禁用；分块控件表示输入帧边长，不是固定块数量；`0` 是 RVE 默认整帧路径，不是显存自动选择。

- [x] Task: 刷新模型列表时识别本地已安装资源并避免重复下载。
  - Owner: Codex / next agent
  - Status: implemented; direct files and extracted archive layouts are checked
  - Relevant files: `preview/1.9.6-preview.1/src/VideoEnhancerPlugin/PluginPanel.vb`
  - Notes/blockers: 需在真实 core-path 下用已下载和清理归档两种状态做一次 UI 回归。

- [x] Clarification: 转场阈值恢复使用 RVE 官方外部标尺并直接透传。
  - Owner: Codex / next agent
  - Status: implemented; default 4.0, accepted range 0 < value <= 10
  - Relevant files: `preview/1.9.6-preview.1/src/VideoEnhancerPlugin/PluginConfig.vb`, `PluginPanel.vb`, `QueueHook.vb`, `cli/Program.cs`

## Recently Completed

- 2026-08-27 16:57: 按反馈将内置页标题改为“使用教程”，删除模型简介和教程中的短片、试片、几秒素材及 A/B 对比表述；重新构建并部署 DLL，已安装文案探针通过。

- 2026-08-27 16:35: 模型提示已按具体模型和变体给出用途、倍率、资源取向与试用建议；全部 93 个内置能力条目命中专用描述规则。所有插件选项框改用 `WheelLockedComboBox`，关闭状态滚轮不再改变单选值；内置教程扩展为 5,168 字符的逐步说明。

- 2026-08-27 16:05: 已将最新模型菜单插件 DLL 部署到 `C:\Program portable\3FUI\plugin\videoenhancer.3fui.dll`；3FUI 主进程未运行，部署后文件为 4,635,136 bytes，SHA-256 与构建产物一致。

- 2026-08-27 15:54: 一级模型分类菜单关闭无用图标列，二级模型菜单保留勾选列；内置模型项增加 LakeUI 悬浮提示，包含用途简介、倍率和支持后端。

- 2026-08-25 19:46: 完成 OpenModelDB 671 个模型兼容审计：当前架构覆盖 633 个，按 RGB/格式/ONNX 契约的综合静态上限约 620 个，明确缺口 51 个；详细名单见 `docs/openmodeldb-rve-support-audit.md`。

- 2026-08-25 20:16: 完成当前 93 个模型的输入尺寸依赖调查与能力清单接入；ONNX 奇数尺寸探针覆盖 28 项，PTH 奇数尺寸探针覆盖 42 项；修补后的 RealHatGAN 35x33、50x47 CUDA 通过，1920x1080 单帧在本机实验性 128 分块下输出尺寸正确。默认分块未写死。

- 2026-08-25 00:35: RTX 3060 代表模型矩阵收口：578 项中 576 通过、2 项 FlashVSR + GIMM 因 6GB OOM 跳过、0 功能失败；BasicVSR++ 跨后端入口修复后 12/12 组合通过，正式 EXE 与发布产物哈希一致。

- 2026-08-22 19:43: 模型下载资源区改为 LakeUI `UltraDetailListView`；82 个资源不再创建 302 个后代控件，真实列表渲染与全局 3 并发槽验证通过。

- 2026-08-22 11:13: LakeUI 可见控件统一、开关和按钮比例修复、ModelScope 错误分类与列表命令顺序修复；在线列表读取到 82 个文件，模型文件直链返回 HTTP 200。
- 2026-08-22 12:46: 基于远端 `220ebb4`（含 UI 提交 `a95bdfe`）制作 `1.9.6-preview.1`，合入本地 ModelScope 修复并生成可安装 ZIP；在线列表仍返回 82 项。
- 2026-08-22 13:01: 用户确认 v1.9.6-preview.1 升为主线，根目录三项运行产物已替换；完成 TensorRT 和组合管线源码调研。
- 2026-08-22 13:09: 交叉检查插件与 3FUI 6.1.39 源码，确认最小化恢复闪屏是玻璃背景映射下的透明控件逐层重绘，不是业务加载。
- 2026-08-22 14:11: 三个问题分别提交；发布 v1.9.6-preview.2。TensorRT 首次构建/缓存命中、两种组合顺序、混合后端分阶段、FFV1 中间编码和恢复重绘状态跃迁测试均通过。

## Decisions

- 2026-08-22 11:13:
  - Decision: 可见交互控件统一使用 LakeUI；WinForms `Panel`/`FlowLayoutPanel` 仅保留为布局容器，预览 `PictureBox` 保留以避免历史上的帧切换失效。
  - Reason: 兼顾全局视觉一致性和现有预览稳定性。
  - Impact: 标签、卡片、进度条、下拉框、复选框、数值滑块和按钮均走 LakeUI 渲染；预览逻辑不变。
- 2026-08-22 11:13:
  - Decision: `--list-download-models` 在 `core-path` 校验前执行，并只把真实 HTTP/超时异常标记为 `NO_NETWORK`。
  - Reason: 在线元数据不依赖本地后端目录；旧逻辑把配置错误和 JSON 错误都误报成断网。
  - Impact: 即使本地 `core-path` 暂时无效，模型列表仍可刷新；其他错误显示真实原因。
- 2026-08-22 12:46:
  - Decision: 抢先体验版使用独立目录和独立 Git 克隆，不覆盖根目录 v1.9.5 稳定产物。
  - Reason: 远端 UI 刚合并，仍需实际宿主回归；隔离可随时退回稳定版。
  - Impact: 用户可单独安装预览包，后续也能清晰比较远端更新和本地补丁。
- 2026-08-22 12:46:
  - Decision: 采用远端 UI 基线，但继续保留本地已验证的列表命令顺序、超时和错误分类修复。
  - Reason: 远端最新版仍会把部分非网络异常误报为 `NO_NETWORK`。
  - Impact: UI 跟随远端贡献，同时避免用户原先遇到的“当前无网络”误报。
- 2026-08-22 13:01:
  - Decision: v1.9.6-preview.1 从抢先体验分支提升为当前主线，v1.9.5 不再维护。
  - Reason: 用户明确指定远端新 UI 版本作为后续开发基线。
  - Impact: 当前运行产物使用 v1.9.6-preview.1；后续源码只修改 `preview/1.9.6-preview.1/src` 这份 Git 克隆，根目录旧源码视为非活动副本。
- 2026-08-22 13:01:
  - Decision: TensorRT 自动构建和超分/补帧组合列为下一阶段两个独立实现任务。
  - Reason: 官方 RVE 后端具备自动构建缓存及组合处理能力，但当前 3FUI 包装层改变了模型契约并在 UI 层强制互斥。
  - Impact: 不把现状误判为模型限制；实现时需要能力矩阵、独立后端、缓存键和失败回退，而不只是删除两个互斥判断。
- 2026-08-22 13:09:
  - Decision: 保留 3FUI 毛玻璃/自定义背景兼容，不通过改名 `ModernPanel1` 粗暴退出背景映射。
  - Reason: 背景映射是宿主明确提供给插件的个性化能力，问题在于插件没有提供稳定的回退底色和整页恢复重绘。
  - Impact: 修复优先采用根控件不透明兜底、缓存根面板引用、恢复时统一 Invalidate/Update，并在实测后决定是否减少透明容器。
- 2026-08-22 14:11:
  - Decision: TensorRT 自动缓存目录固定为 `models\TensorRT-Cache`，缓存名包含模型名、GPU、TensorRT 版本、输入宽高和源模型 SHA-256 摘要。
  - Reason: Engine 与设备/运行库/profile 及模型内容绑定，不能在不同机器或模型更新后盲目复用。
  - Impact: PTH 首次使用自动编译；缓存命中前验证，不兼容时自动重建；没有同名 PTH 时明确报错，不静默回退到其他超分后端。
- 2026-08-22 14:11:
  - Decision: 组合模式默认 `upscale-first`；`interp-first` 在同后端时走原生单程管线，其他情况使用 FFV1 `gbrp16le` 无损中间视频分阶段。
  - Reason: 用户明确要求默认画质优先，同时需要可选的速度/算力优先顺序；现有后端原生顺序固定为先补后超。
  - Impact: UI 明确显示“画质优先：先超分，再补帧。”和“速度/算力优先：先补帧，再超分。”；TensorRT/ONNX/FlashVSR 组合补帧默认使用 NCNN RIFE，避免格式错配。
- 2026-08-22 14:11:
  - Decision: 恢复闪屏修复不启用全窗 `WS_EX_COMPOSITED`，而是将 `ModernPanel1` 提升为宿主可反射字段，增加不透明兜底、根级双缓冲和恢复状态同步重绘。
  - Reason: 保留毛玻璃背景映射并避免与 LakeUI DirectX 控件产生新的合成冲突。
  - Impact: 代码级恢复路径已验证；最终观感仍需实际 3FUI 宿主确认。

## Risks And Blockers

- Risk/blocker: 根目录现为独立维护 Git 主线，`main` 与原作者 `origin/main` 已分叉。
  - Impact: 对原作者上游直接执行普通 `git pull` 不能快进，强行合并会把独立发行线与上游混在一起。
  - Mitigation or next check: 只选择性移植上游功能；独立维护提交推送到 `fork`，不合并或推送原作者 `origin`。
- Risk/blocker: 尚未在用户实际 3FUI 窗口中做 DPI 视觉截图回归。
  - Impact: 极端缩放或不同宿主版本下仍可能需要小幅坐标调整。
  - Mitigation or next check: 在实际宿主中检查主页面、模型下载页、图片超分页和视频对比工作室。
- Risk/blocker: 远端 UI 改版刚合并，且远端清理提交删掉了插件构建仍引用的生成载荷文件。
  - Impact: 未补回载荷时源码无法完整构建；新 UI 也可能存在尚未暴露的布局问题。
  - Mitigation or next check: 预览构建已从本地已验证产物机械恢复载荷；用户安装后优先检查主页面、模型下载和对比工作室。
- Risk/blocker: 当前构建机没有可调用的 `nvidia-smi`/NVIDIA 运行环境。
  - Impact: 自动构建逻辑已用伪后端执行，但真实 TensorRT 转换器、驱动和大模型推理尚未在本机跑通。
  - Mitigation or next check: 在实际 N 卡机器选择一个 PTH 模型，确认控制台出现带 GPU、TensorRT 和输入尺寸的缓存名，并完成第二次缓存命中。
- Risk/blocker: 完整 3FUI 毛玻璃宿主未在当前工作区运行。
  - Impact: 恢复重绘的状态跃迁和绘制属性已验证，无法在本机构成用户截图的最终肉眼对照。
  - Mitigation or next check: 安装 preview.2 后在毛玻璃开/关、100%/150% DPI 下各执行最小化/恢复；如仍有延迟，再采集宿主帧时间而不是继续增加合成样式。
- Risk/blocker: 跨后端的先超后补仍会在输出目录产生 FFV1 无损中间视频。
  - Impact: 长视频和高分辨率素材会显著占用临时磁盘空间；同一后端的帧级路径不产生整段中间视频。
  - Mitigation or next check: 任务完成/失败/停止都会自动清理；用户开始跨后端长任务前需确认输出盘空间，空间有限时选择同后端或先补后超。

### 2026-08-22 16:30 - Codex

- Objective: 按用户追加要求核对并接入 RIFE 后端、光流/转场参数、超分分块设置，并修复模型刷新后已有资源仍显示下载的问题。
- Research: 核对临时 RVE v2-main 源码，确认 RIFE 可用后端为 NCNN、PyTorch/CUDA、TensorRT；`--dynamic_scaled_optical_flow` 仅 PyTorch 有效，`--scene_detect_threshold` 控制转场检测，`--tilesize` 是超分分块边长，NCNN/PyTorch/TensorRT/ONNX 支持，FlashVSR 不使用。
- Work completed: `PluginConfig.vb` 新增独立参数；`QueueHook.vb` 和 `cli/Program.cs` 贯通 CLI；官方页面新增补帧后端、转场阈值、动态光流尺度和超分分块尺寸控件，并按后端能力禁用不适用项；模型资源刷新按 core-path 检查直接文件、压缩包和解压目录，已存在资源显示“已存在”，分类全存在时禁用“下载全部”。
- Files changed: `VideoEnhancerPlugin/PluginConfig.vb`、`PluginPanel.vb`、`QueueHook.vb`、`cli/Program.cs`、`cli/README.md`、`cli/VideoEnhancer.csproj`、`cli/embedded-tools/rve-ordered-backend.py`；更新 `preview/1.9.6-preview.2/dist/README-抢先体验版.md`、EXE、DLL、ZIP 和根目录 EXE/DLL。
- Commands run: 正确 canonical 仓库执行 `git pull`；`dotnet build cli/VideoEnhancer.csproj -c Release --no-restore`；插件 `build.ps1 -HostBin C:\Users\maxzr\AppData\Local\Temp\FFmpegFreeUI.6.1.39.extracted -SkipInstall`；`cli/build.ps1`；CLI help/非法参数检查；`python -m py_compile cli\embedded-tools\rve-ordered-backend.py`；ModelScope 列表读取；ZIP 清单/哈希；`git diff --check`。
- Verification: CLI 与插件构建成功，0 错误、2 个既有 CA1416 警告；帮助列出四个新增参数；`-scene-threshold 0` 与 `-tile-size 16` 均按预期拒绝；在线模型列表 82 项；ZIP 含 README、layout、EXE、DLL 四项；发布 EXE 与根目录 EXE 哈希一致。
- Decisions: UI 的“分块”按 RVE 原生含义显示为“分块尺寸”，不伪造固定块数量；TensorRT 不传动态光流开关，避免与 RVE 明确不支持的能力冲突；转场阈值使用 2/3/4/6/8 预设。
- Remaining: 未在真实 RVE Python 依赖/NVIDIA GPU 和完整 3FUI 宿主中执行视频与 DPI 视觉回归；canonical 源码有未提交修改，根目录不是 Git 仓库。
- Git status: `preview/1.9.6-preview.1/src` 为 `main...origin/main [ahead 4]`，未提交文件为上述源码/说明文件及新增内置 Python 包装器；工作树不干净。建议审核后 `git add` + `git commit`，再决定是否推送。

### 2026-08-22 16:45 - Codex

- User clarification: 用户指出转场阈值可能是 0-1，需要核对 RVE 后端实际量纲。
- Research result: `SceneDetect.py` 的 Sudo EfficientNet 检测器内部使用 `threshold * 0.1`，官方设置默认外部值为 `3.5`，所以模型实际比较阈值为 `0.35`；RVE 的旧命令行暴露的是 0-10 标尺，不是直接的概率值。
- Decision: 按用户要求恢复 RVE 官方外部阈值标尺，插件/CLI 使用 `0 < threshold <= 10`，默认 `4.0`，直接透传 `--scene_detect_threshold`；RVE 内部负责转换为模型使用的 0-1 阈值。
- Files changed: `VideoEnhancerPlugin/PluginConfig.vb`、`PluginPanel.vb`、`QueueHook.vb`、`cli/Program.cs`、`cli/README.md`；重新更新 preview.2 dist README/EXE/DLL/ZIP 和根目录 EXE/DLL。
- Verification: 上一版 0-1 语义的构建已通过；本次回退官方外部标尺后将重新构建并验证 `--help`、阈值范围和直接透传参数；`git diff --check`、Python 语法检查通过。
- Git status: canonical source `main...origin/main [ahead 4]`，工作树不干净，仍有本轮源码改动未提交；根目录不是 Git。建议审核后提交并推送。

### 2026-08-22 17:00 - Codex

- User correction: 用户明确要求使用 RVE 官方外部转场阈值，不要在 UI/CLI 层改成 0-1。
- Change: 恢复 `SceneDetectThreshold`/`SceneThreshold` 默认 `4.0`；UI 改为官方外部值预设 `1.0/2.0/3.5/4.0/6.0/8.0/10.0`；CLI 校验 `0 < value <= 10`，构建后端参数时直接传入 `--scene_detect_threshold`，不再乘 10。
- Reason: RVE 的 Sudo 检测器内部确实把外部阈值乘以 0.1，但外部 CLI/官方设置使用的是 0-10 标尺；转换应由 RVE 内部完成。
- Verification pending: 需要重新完成 CLI/插件构建、帮助和阈值参数验证，并重新更新 preview.2 产物/ZIP。
- Git status: canonical source `main...origin/main [ahead 4]`，工作树不干净；根目录不是 Git。

### 2026-08-22 17:10 - Codex

- Objective: 按用户最终确认恢复 RVE 官方转场阈值外部标尺，并完成 preview.2 重新发布。
- Work completed: `SceneDetectThreshold`/CLI 默认值恢复为 `4.0`；UI 预设为 `1.0/2.0/3.5/4.0/6.0/8.0/10.0`；CLI 接受 `0 < value <= 10`，直接透传 RVE，不做 0-1 包装或乘 10；README 和 preview.2 说明同步。
- Commands run: 插件 `build.ps1 -HostBin C:\Users\maxzr\AppData\Local\Temp\FFmpegFreeUI.6.1.39.extracted -SkipInstall`；`cli/build.ps1`；CLI `--help`、非法 `10.1` 校验；在线列表；Python 语法检查；`git diff --check`；ZIP 重压与 SHA-256。
- Verification: CLI/插件构建成功，0 错误、2 个既有 CA1416 警告；帮助显示官方 0-10 标尺；`10.1` 被拒绝；ModelScope 列表 82 项；ZIP 含 4 个发布文件；preview.2 dist 与根目录 EXE/DLL 哈希一致。发布哈希：EXE `E2BEE65CD3D41EF173D5833C050E0E7E86E4C4D6F0955E30F20DE3FEB51FE0E7`，DLL `0738D515F31422992AD43C92BBF721B252B096EDF371364C468D5DE734F00874`，ZIP `BAA3D8D73E98ED40AFD9FDCA649E236664DDD1898B1FA8FF40B36E0743B2D372`。
- Git status: canonical source `main...origin/main [ahead 4]`，源码工作树不干净，新增功能尚未提交；根目录不是 Git。建议审核后 `git add` + `git commit`，再推送或切换工具。
- Risk/blocker: 新增 RIFE/分块参数尚未在实际 RVE GPU 运行时验证。
  - Impact: RVE 官方参数已静态核对，但不同后端、模型和显存配置可能有运行时限制。
  - Mitigation or next check: 短视频分别测试 NCNN、CUDA、TensorRT；确认 CUDA 动态光流生效、TensorRT 自动关闭，并记录分块尺寸对峰值显存和速度的影响。

### 2026-08-22 17:25 - Codex

- Objective: 核对并修正超分分块的官方参数语义、后端能力边界和 TensorRT 缓存隔离。
- Research: RVE 官方 `--tilesize` 是输入帧分块边长；PyTorch 显式分块按 `ceil(width / tile)` 与 `ceil(height / tile)` 形成网格并使用 10 像素 padding。`tilesize=0` 走整帧推理，不存在按显存自动试探/回退算法；当前 RVE ONNX 实现声明但不使用该参数，FlashVSR 也不应使用。
- Work completed: UI 默认项改为“RVE 默认（0）”，并说明不是显存自动选择；UI/CLI/队列仅对 NCNN、CUDA/PyTorch、TensorRT 传递显式分块，ONNX/FlashVSR 显式传入会拒绝；TensorRT Engine 缓存名加入 `__tile-<值>`，避免整帧和分块 Engine 复用。
- Files changed: `preview/1.9.6-preview.1/src/VideoEnhancerPlugin/PluginConfig.vb`、`PluginPanel.vb`、`QueueHook.vb`、`cli/Program.cs`、`cli/README.md`；同步 `preview/1.9.6-preview.2/README-抢先体验版.md`、`dist/README-抢先体验版.md`、EXE、DLL、ZIP 及根目录运行副本。
- Commands run: `git pull`（因本地未提交改动被 Git 阻止，远端已领先 2 个提交）；RVE 临时源码 `rg`/UTF-8 核对；`dotnet build cli/VideoEnhancer.csproj -c Release --no-restore`；插件 `build.ps1 -HostBin ... -SkipInstall`；`cli/build.ps1`；CLI `--help`；ONNX + `-tile-size 128` 参数拒绝检查；`git diff --check`；preview.2 发布文件重新同步和 ZIP 重压。
- Verification: CLI 与插件构建成功，0 错误、2 个既有 CA1416 警告；帮助显示“0 为 RVE 默认、仅 NCNN/CUDA/TensorRT”；ONNX 显式分块返回退出码 2；preview.2 dist 与根目录 EXE/DLL 已同步。新哈希：EXE `5ECDEA1D036082D56E5A33646130CF8FDAF2320CDF0A2FF2012D99189CA34DE6`，DLL `67D5A3B05913B948E5804BB7BEB177FBEBA5B8D57890460120384055CC290886`，ZIP `84DEB0C3503529248BD989744DE9B5D114586FCF91A1E14473D1DE212C363C26`。
- Decisions: 界面预设值是常用便利值，不宣称为 RVE 固定枚举；分块尺寸表示输入边长而非块数量；不新增显存探测伪逻辑；TensorRT 缓存按 tile 隔离。
- Remaining: 尚未在真实 RVE Python 依赖/NVIDIA GPU 上测量各分块的峰值显存、速度及边缘画质；远端 `origin/main` 已领先 2 个提交，源码工作树仍有未提交改动，不能直接 `git pull`。
- Git status: canonical source `main...origin/main [ahead 4, behind 2]`，本地源码和内置包装器未提交；根目录不是 Git。建议先审核并提交本地改动，再合并远端提交后推送。

## Environment Notes

- Current known environment: Windows PowerShell，工作区 `C:\Codex Program\3fui plugin`；Git 2.55.0、.NET SDK 10.0.400、系统 Python 3.14.6、uv Python 3.13.14、FFmpeg 8.1.2、GitHub CLI 2.93.0、ModelScope CLI 1.39.1。
- GPU: NVIDIA GeForce RTX 3060 Laptop GPU，驱动 610.88，显存 6144 MiB。3FUI 插件目录已具备当前 Python 后端和补帧模型；已完成短样本 RIFE heavy 与 GMFSS Base CUDA 实际推理，TensorRT 和完整视频仍待验证。
- 3FUI: 安装路径 `C:\Program portable\3FUI`，版本 6.1.39（commit `642ddf4`）；实际插件目录为 `C:\Program portable\3FUI\plugin`，开发程序集缓存位于 `%LocalAppData%\VideoEnhancerDev\FFmpegFreeUI.6.1.39.extracted`，当前插件源码编译通过。
- Authentication: GitHub CLI 已登录 `maxzrb`；ModelScope CLI 已安装并加入用户 PATH，但此设备尚未登录。不要把 Token 写入仓库。
- Local-only note: 解析回归辅助文件 `C:\Program portable\3FUI\3FUI\plugin\videoenhancer-resolver-test.exe` 因终端安全策略拒绝删除而保留；3FUI 配置仍指向正式 `videoenhancer.exe`，不会加载该测试副本。CUDA 回归样本及 `output-gmfss-base.mkv` 位于 `%TEMP%\videoenhancer-cuda-scene-test`，同样未绕过终端策略强制清理。补帧能力缓存位于 `%LocalAppData%\VideoEnhancer\cache\interpolation-capabilities-v1.json`。
- Recheck required before: 3FUI/LakeUI 版本变化后重新提取宿主程序集；真实 TRT 测试前确认 CUDA/TensorRT/Torch-TensorRT 与当前驱动兼容。

## Verification And Commands

- Latest 2026-08-24 GMFSS checks: 权重元数据确认 Base 顶层无 `rife`、Union 有 `rife`；Base/Union CUDA 初始化均通过。实际 GMFSS Base 640x360 CUDA 任务退出码 0，输入 24 帧、输出 47 帧。CUDA 下拉框列出 12 项，冷扫描约 3.98 秒，缓存后 5 次为 0.368–0.383 秒；TensorRT 与 NCNN 各只列 5 个 RIFE，不含 GMFSS。正式部署 EXE 版本 1.0.7，源/目标 SHA-256 均为 `9F2959857B5D67E2B837213E80E70B5933173263728B74D725FDFF0E4EF1B819`。
- Latest 2026-08-24 CUDA scene-detect check: 实际 RTX 3060 运行 `rife4.26.heavy.pkl` 成功，RVE 参数使用 `--scene_detect_method pyscenedetect` 且不再传 NCNN 模型目录；任务退出码 0，640x360 输入 24 帧、输出 47 帧，符合 2 倍补帧。正式部署 EXE 源/目标 SHA-256 均为 `64268C901BB124697A04509DE6999D469C8FD7987706C2655D19A6AD7910FE16`。
- Latest 2026-08-24 RTX 3060 checks: CLI/插件构建成功，内置 Python 脚本 `py_compile` 通过，顺序后端 unittest 6/6 通过；CUDA 列表为 8 项、TensorRT 列表为 5 项。`RIFE/rife4.26.heavy`、`rife4.26.heavy`、带 `.pkl` 路径及普通 `rife4.26` 共 6 种输入均解析到正确权重；正式部署 EXE SHA-256 为 `B18D735C4B213CAE00B851E7238D39F58C19D086189425E9544E0CB59CB8BE62`。
- Latest 2026-08-24 checks: CLI Release build and single-file publish passed with version 1.0.3 (0 errors, 2 existing CA1416 warnings); ordered backend tests 6/6 passed; ModelScope returned 127 tree entries and 105 downloadable items after pagination; all five RIFE remote sizes/SHA-256 matched local assets; isolated real CLI download and TensorRT discovery of five weights passed.
- Commands run:
  - `dotnet build cli\VideoEnhancer.csproj -c Release`: 成功，0 错误，2 个既有 Windows 平台分析警告。
  - `cli\build.ps1`: 成功发布单文件 `videoenhancer.exe`。
  - `VideoEnhancerPlugin\build.ps1 -HostBin <3FUI 6.1.39 提取目录>`: 成功生成 `videoenhancer.3fui.dll`。
  - `videoenhancer.exe --list-download-models --json`: 在无效 `core-path` 配置下退出码 0，解析到 82 个文件。
  - `Invoke-WebRequest` 检查 ModelScope tree API 与 `FlashVSR/README.md` 直链: 均为 HTTP 200。
- Preview commands run:
  - 克隆 `https://github.com/user-Wing/VideoEnhancer` 到 `preview/1.9.6-preview.1/src`，基线 HEAD 为 `220ebb4`。
  - `preview/.../VideoEnhancerPlugin/build.ps1 -HostBin <3FUI 6.1.39 提取目录> -SkipInstall`: 成功生成预览插件 DLL。
  - `preview/.../cli/build.ps1`: 成功发布并嵌入预览插件，EXE 报告 v1.9.6-preview.1。
  - `dist/videoenhancer.exe --list-download-models --json`: 退出码 0，解析到 82 个文件。
  - 检查 ZIP 清单和 SHA-256：ZIP 为 `3084ECCEBE6F15C34BFF1CC09FBF036C1B215D7A6D4524E5A23DA750E4773374`。
- Mainline/research commands run:
  - `git pull --ff-only`: 远端已是最新，主线源码基线仍为 `220ebb4`。
  - 将 dist 的 EXE/DLL/layout 复制到根目录并复核 SHA-256；EXE 报告 v1.9.6-preview.1。
  - 静态追踪插件互斥、队列参数、CLI 模型解析、TensorRT 验证与手动转换逻辑。
  - 克隆官方 `TNTwise/REAL-Video-Enhancer` v2-main `edb9b12` 到临时目录，核对自动 Engine 构建缓存及组合帧循环。
  - 克隆 3FUI 6.1.39 对应 `Lake1059/FFmpegFreeUI` `642ddf4` 到临时目录，核对插件 `ModernPanel1` 的毛玻璃背景绑定逻辑。
  - 统计 `PluginPanel.vb`：96 处透明背景赋值、40 个 Panel、29 个 TableLayoutPanel、26 个 FluentCardPanel；插件类本身无 `SetStyle`、`DoubleBuffered` 或恢复窗口处理。
- Previous preview checks: v1.9.6-preview.1 的 CLI 编译、插件 Option Strict 编译、在线列表解析、文件直链和 SHA-256 均通过；当前根目录已由 preview.2 替换。
- v1.9.6-preview.2 verification:
  - TensorRT 伪后端：首次无缓存自动构建成功，缓存名包含 `NVIDIA-GeForce-RTX-4090`、`trt-10.8.0`、`input-1920x1080` 和 12 位源摘要；第二次命中并验证成功。
  - 组合管线伪后端：默认先超后补生成两个阶段；NCNN 先补后超单程同时传两模型；ONNX 超分 + NCNN 补帧分两阶段；共 5 条后端调用记录断言通过，临时文件为 0。
  - `ffmpeg 8.1.1` 实际编码 FFV1 `gbrp16le` 成功。
  - 插件布局实例化：两个顺序选项和默认项正确；`ModernPanel1` 字段、不透明 `#181818` 兜底、双缓冲和模拟最小化恢复同步重绘均通过。额外使用渐变背景模拟 3FUI `BackgroundSource`，恢复后整页渲染完整，抽样 13,800 像素仅 11 个动态控件像素变化（0.08%）。
  - 最终插件/CLI 重建成功，0 错误、2 个既有 CA1416 警告；EXE 报告 v1.9.6-preview.2；在线列表 82 项；ZIP 解压清单、版本和根目录哈希一致。
- Not run: 实际 NVIDIA TensorRT 大模型编译/推理；实际 3FUI 毛玻璃宿主中的最小化恢复肉眼 A/B；大型模型完整下载。

## Git Sync

- Git repository: 根目录 yes
- Branch: `main`，跟踪 `fork/main`
- Last known commit: `30bf203 docs: record 1.1.2 release`
- Remote topology: `fork=https://github.com/maxzrb/VideoEnhancer.git`；`origin=https://github.com/user-Wing/VideoEnhancer.git`。
- Upstream relation: `main` 跟踪独立维护线 `fork/main`；本次仅快进同步 `fork/main`，未合并或推送原作者 `origin`。
- Uncommitted changes: `VideoEnhancerPlugin/PluginPanel.vb`、`docs/openmodeldb-rve-support-audit.md`（未跟踪）、`docs/codex/STATUS.md`、`version/工作进度.md`。
- Working tree clean: no；运行代码已与 `fork/main` 同步，模型菜单改动、审计文件和 HandShake 记录待提交。
- Commit recommended before switching agents/devices: yes；建议真实 3FUI 回归后审核并将本次源码与记录作为提交推送到 `fork/main`。

## Session Log

Append new entries below this line. Use `YYYY-MM-DD HH:MM` so same-day work remains ordered. Do not overwrite previous entries.

### 2026-08-22 15:30 - Codex

- Objective: 修复 preview.2 的 FFmpeg 下载路径、组合任务二次编码风险和先超后补产生巨大中间文件的问题，并响应 HDR 位深要求。
- Work completed: `Bin/*` 下载目标改为 `bin`，并加入旧 `models\ffmpeg` 兼容迁移；同一后端先超后补新增内置 `rve-ordered-backend.py`，在同一 RVE 进程内按 raw RGB 帧执行，最终只调用一次用户编码器；跨后端仍使用 FFV1 无损中间视频，SDR 使用 `gbrp10le`，PQ/HLG 使用 `gbrp16le` 并传递 `--hdr_mode`。
- Files changed: `preview/1.9.6-preview.1/src/cli/Program.cs`、`cli/VideoEnhancer.csproj`、`cli/README.md`、`cli/embedded-tools/rve-ordered-backend.py`、`VideoEnhancerPlugin/PluginPanel.vb`；更新 `preview/1.9.6-preview.2/dist/README-抢先体验版.md`、EXE、ZIP 和根目录 EXE。
- Commands run: `git pull --ff-only`；`python -m py_compile`；`dotnet build cli/VideoEnhancer.csproj -c Release --no-restore`；`cli/build.ps1`；在线模型列表和 ZIP 清单检查；`git diff --check`。
- Verification: CLI 编译/发布成功，0 错误、2 个既有 CA1416 警告；模型列表 82 项；`Bin/ffmpeg.7z` 路径确认；ZIP 含 4 个发布文件；根目录 EXE 与 preview.2 dist EXE SHA-256 一致。包装器 Python 语法通过，但因本机临时 RVE 环境缺少 `cv2`，未执行真实模型运行。
- Decisions/risks: 不使用有损 H.264/H.265 作为中间格式；同后端不落盘，跨后端仍可能因 10/16-bit RGB 无损传递占用较多空间；HDR 实机和完整 RVE 依赖仍待验证。插件 DLL 未重新编译，因为当前机找不到 3FUI 6.1.39 宿主程序集，下载路径说明已同步到源码。
- Git status: 根目录非 Git；canonical source `main...origin/main [ahead 4]`，本次修复尚未提交，未推送远端。
- Next step: 在实际 RVE/NVIDIA 环境用短 SDR、10-bit 和 PQ/HLG 视频验证两种顺序、编码器进程数、帧数和临时文件清理；确认后提交并推送本次源码修复。

### 2026-08-22 15:50 - Codex

- Objective: 修复处理顺序下拉框样式/溢出，并纠正模型下载的并发行为。
- Work completed: 处理顺序行改用百分比列、`DockStyle.Fill`、`AutoSize=False`、最小高度和更紧凑边距；模型下载取消全局下载锁，单资源可以独立并行；“下载全部”使用最多 3 个并发任务的滑动窗口，任一任务完成立即补下下一个；相同资源路径仍防止重复启动；清理压缩包与下载任务保持互斥。
- Files changed: `preview/1.9.6-preview.1/src/VideoEnhancerPlugin/PluginPanel.vb`；重新生成 `videoenhancer.3fui.dll`、`videoenhancer.exe`、preview.2 ZIP 和根目录产物；更新 preview.2 README。
- Verification: `VideoEnhancerPlugin/build.ps1 -HostBin C:\Users\maxzr\AppData\Local\Temp\FFmpegFreeUI.6.1.39.extracted -SkipInstall` 成功；`cli/build.ps1` 成功，0 错误、2 个既有 CA1416 警告；Python 包装器语法检查、`git diff --check` 通过。
- Decisions/risks: “下载全部”不再按三件一批等待，而是滑动补位；如果某资源失败，不再启动新的资源，但已启动的下载会完成并释放路径锁。实际多进程网络下载仍需用户环境确认。
- Git status: canonical source `main...origin/main [ahead 4]`，本次修复仍未提交；根目录不是 Git，未推送远端。
- Next step: 用户在真实宿主中确认下拉框宽度、长文本显示及 3 并发下载行为；审核后提交本次源码改动。

### 2026-08-22 11:13 - Codex

- Objective: 修复 LakeUI 未全局应用导致的 UI 破损、开关/按钮比例异常，以及 ModelScope 被误报为无网络。
- Work completed: 用 LakeUI `ModernPanel`、`HtmlColorLabel`、`ExcellentProgressBar`、`ModernComboBox`、`ModernCheckBox`、`ExcellentTrackBar` 替换可见原生控件；统一按钮高度 34px；开关改为标准 55×32 且取消拉伸 Dock；修复在线列表命令顺序、异常分类、超时处理和界面错误信息。
- Files changed: `AGENTS.md`, `cli/Program.cs`, `cli/VideoEnhancer.csproj`, `VideoEnhancerPlugin/PluginPanel.vb`, `VideoEnhancerPlugin/QuadGridControls.vb`, `VideoEnhancerPlugin/QuadGridForm.vb`, `VideoEnhancerPlugin/build.ps1`, `VideoEnhancerPlugin/README.md`, `videoenhancer.exe`, `videoenhancer.3fui.dll`, `docs/codex/*`, `version/*`。
- Commands run: CLI build/publish、插件编译、ModelScope API/直链检查、CLI 在线列表解析、静态原生控件审计。
- Verification: 两个项目均编译成功；最终 EXE 已重新发布并嵌入最新插件 DLL；无效 core-path 下在线列表返回 82 项；模型文件直链 HTTP 200；EXE 报告 v1.9.5。
- TODO changes: 新增实际 3FUI 宿主 DPI/视觉与小文件下载回归。
- Decisions/risks: 保留布局容器和预览 PictureBox；当前目录无 Git，无法自动回退；未执行大文件下载。
- Environment notes: 插件使用 3FUI 6.1.39 官方程序集验证；构建脚本新增可移植 `-HostBin`/`FFMPEGFREEUI_DEV_BIN` 支持。
- Git status: 非 Git 仓库。
- Next step: 用户替换运行目录中的 EXE/DLL 后提供实际截图；如有局部间距问题，再进行第二轮像素级微调。

### 2026-08-22 12:46 - Codex

- Objective: 停止继续修改本地旧 UI，基于原作者远端新 UI 制作可安装的抢先体验版。
- Work completed: 克隆远端 HEAD `220ebb4`（UI 提交 `a95bdfe`）；合入 ModelScope 列表顺序、真实网络错误识别、45 秒超时和详情提示；版本提升为 `1.9.6-preview.1`；构建 EXE/DLL、整理 dist 并生成 ZIP。
- Files changed: `preview/1.9.6-preview.1/src/cli/Program.cs`、`cli/VideoEnhancer.csproj`、`VideoEnhancerPlugin/PluginPanel.vb`、`VideoEnhancerPlugin/build.ps1`；新增预览 README、dist 和 ZIP。构建所需的 `out/EmbeddedFffNativePayload.vb` 从本地已验证生成物机械恢复，受远端忽略规则管理。
- Commands run: Git 克隆和状态检查、插件与 CLI 构建、在线模型列表、版本检查、ZIP 内容检查、SHA-256 计算。
- Verification: 两个项目均构建成功，0 错误（2 个既有 CA1416 警告）；dist EXE 报告 v1.9.6-preview.1；在线列表返回 82 项；ZIP 包含 EXE、DLL、布局 JSON 和安装说明。
- TODO changes: 实际宿主 DPI/视觉与小文件下载回归改为针对预览包执行。
- Decisions/risks: 稳定版 v1.9.5 不动；预览版隔离交付；远端新 UI 尚未在用户宿主实测，且远端清理提交遗漏了构建载荷。
- Environment notes: 使用 Windows PowerShell、.NET SDK 10 和 3FUI 6.1.39 提取的宿主程序集构建。
- Git status: 根目录非 Git；预览源码基线 `220ebb4`，有 4 个本地补丁文件未提交。
- Next step: 用户备份后安装 `VideoEnhancer-1.9.6-preview.1-win-x64.zip`，反馈实际 UI 截图和模型页下载结果。

### 2026-08-22 13:01 - Codex

- Objective: 将抢先体验版提升为主线，并判断 TensorRT 是否自动编译、缺少哪些防呆，以及超分/补帧不能并用的真实限制层级。
- Work completed: 同步远端；把 v1.9.6-preview.1 EXE/DLL/layout 提升到根目录；追踪插件、CLI 和官方 RVE 后端源码；形成下一阶段实现边界。
- Files changed: 根目录 `videoenhancer.exe`、`videoenhancer.3fui.dll`、`videoenhancer-layout.json`；`docs/codex/STATUS.md`、`version/工作进度.md`、`version/版本迭代记录.md`。
- Commands run: `git pull --ff-only`、源码 `rg`/UTF-8 读取、主线产物哈希和版本验证、官方 RVE v2-main 浅克隆与源码核对、NVIDIA TensorRT 官方兼容性资料核对。
- Verification: 根目录三项产物哈希与 dist 一致，EXE 为 v1.9.6-preview.1；远端主线无新增提交；官方 RVE 代码确认超分和补帧可同时启用、运行顺序为先补后超，且 TensorRT 缓存缺失时会自动构建。
- TODO changes: 新增 TensorRT 自动构建/缓存/回退任务；新增组合模式、独立后端和顺序策略任务。
- Decisions/risks: v1.9.5 退役；当前 TensorRT 包装层只消费 `.engine`，不同设备不可靠；TensorRT + RIFE 当前存在模型格式错配，不能只删除 UI 互斥。
- Environment notes: 官方 RVE 调研克隆位于本机临时目录，不是项目依赖；未下载 2.64 GB 后端包，也未执行真实 GPU 推理。
- Git status: 根目录非 Git；主线源码 `main...origin/main`，4 个本地补丁文件未提交。
- Next step: 优先实现 TensorRT 首次运行自动构建和安全回退，再实现 NCNN/CUDA 组合模式及可选处理顺序。

### 2026-08-22 13:09 - Codex

- Objective: 追加诊断视频超分页面从任务栏恢复时约 1 秒的背景穿透/骨架加载现象。
- Work completed: 对照用户截图审计插件透明背景、控件树、绘制样式和定时器；克隆并检查 3FUI 6.1.39 宿主的插件背景绑定源码。
- Files changed: 仅更新 `docs/codex/STATUS.md`、`version/工作进度.md` 和当前版本风险说明；未修改功能源码。
- Commands run: UTF-8 `rg`/源码读取、控件类型与透明赋值计数、3FUI 官方仓库浅克隆及宿主源码定位。
- Verification: 宿主会递归找到名为 `ModernPanel1` 的插件根面板，在毛玻璃模式下将 `BackColor/BackColor1` 设为透明并把 `BackgroundSource` 设为主窗体；插件根 UserControl、tabs、pages 和大量布局容器同样透明，且没有整页缓冲或恢复重绘。截图中的壁纸和矩形骨架与该绘制链路一致。
- TODO changes: 新增恢复闪屏修复与毛玻璃开/关、100%/150% DPI 回归任务。
- Decisions/risks: 保留宿主背景映射能力；不优先使用可能与 LakeUI DirectX 冲突的全窗 `WS_EX_COMPOSITED`。
- Environment notes: 3FUI 调研克隆位于本机临时目录，HEAD/tag 为 6.1.39 `642ddf4`，与插件构建宿主版本一致。
- Git status: 根目录非 Git；主线源码仍有此前 4 个本地补丁文件，未新增源码修改。
- Next step: 先实现插件根控件不透明兜底、保存 `ModernPanel1` 引用和主窗体恢复时统一重绘，再做真实宿主 A/B 测试。

### 2026-08-22 14:11 - Codex

- Objective: 将三个问题作为三个独立提交修复，优先完成 TensorRT 自动构建和超分/补帧双顺序，再修复最小化恢复重绘，并发布新的当前主线。
- Work completed: 先把原有 4 个抢先版补丁提交为基线 `956be92`；实现 TensorRT PTH 自动构建、GPU/TensorRT/输入尺寸/源摘要缓存键、缓存验证和取消；解除 UI 互斥，新增默认画质优先及速度优先顺序，按后端能力选择单阶段或 FFV1 无损双阶段；把 `ModernPanel1` 提升为宿主可反射字段，加入不透明兜底、根级双缓冲和主窗体恢复同步重绘；版本提升并发布 v1.9.6-preview.2。
- Files changed: canonical Git repo 中的 `cli/Program.cs`、`cli/VideoEnhancer.csproj`、`VideoEnhancerPlugin/PluginConfig.vb`、`PluginPanel.vb`、`QueueHook.vb`；发布目录 `preview/1.9.6-preview.2/dist/*`、ZIP 和根目录三项运行产物；HandShake 状态及中文版本记录。
- Commits: `762cabb feat: auto-build device-specific TensorRT engines`；`ce75515 feat: support configurable upscale and interpolation order`；`f774532 fix: redraw plugin atomically after window restore`。另有基线提交 `956be92`。
- Commands run: `git pull --ff-only`/status/log；插件 `build.ps1 -HostBin ... -SkipInstall`；CLI `dotnet build`/`cli/build.ps1`；TensorRT 伪后端反射集成测试；五阶段组合管线伪后端测试；FFmpeg FFV1 编码测试；插件布局/恢复状态测试；ZIP 压缩、解压、版本/列表/哈希复验。
- Verification: TensorRT 首次构建和缓存命中通过；缓存名四类信息齐全；默认先超后补、NCNN 原生先补后超、ONNX+NCNN 混合后端均通过；中间文件自动清理；恢复重绘状态请求正常完成，模拟毛玻璃背景的恢复后渲染完整；最终 EXE v1.9.6-preview.2、ModelScope 82 项、ZIP 4 项和根目录哈希一致。
- Release hashes: EXE `6426105CB4E3C1D9D94CCFEDAB477A4B605095D52DC8200B21560C752CDDD475`；DLL `AFFD656264DFCE0A388DB8F7466DEFAD95B57C4736656A1161C732CA968F415F`；layout `3CBDAEBB8CE7A38BE260CDAAD315DB71A9E91885E8B126ECB1FD9488FFAF598D`；ZIP `B78E053DB156B5D0756EB1C70719FCFB653B0231150D5C1059F6EFF0D8D61A1C`。
- TODO changes: 三个实现任务完成；保留用户实际 NVIDIA TensorRT 和 3FUI 毛玻璃/DPI 视觉回归。
- Decisions/risks: TensorRT 不做静默后端回退；先超后补使用输出盘旁的无损临时文件；未启用 `WS_EX_COMPOSITED`。本机构建证据充分，但不能替代真实 N 卡和完整宿主肉眼验证。
- Git status: canonical source `main...origin/main [ahead 4]`，工作树干净；根目录不是 Git。未推送远端。
- Next step: 用户安装 preview.2，先用短视频验证两种顺序，再验证 TensorRT 首次/二次运行和毛玻璃最小化恢复；确认后再推送本地提交。

### 2026-08-22 17:25 - Codex

- Objective: 核对并修正超分分块的官方参数语义、后端能力边界和 TensorRT 缓存隔离。
- Research: RVE 官方 `--tilesize` 是输入帧分块边长；PyTorch 显式分块按 `ceil(width / tile)` 与 `ceil(height / tile)` 形成网格并使用 10 像素 padding。`tilesize=0` 走整帧推理，不存在按显存自动试探/回退算法；当前 RVE ONNX 实现声明但不使用该参数，FlashVSR 也不应使用。
- Work completed: UI 默认项改为“RVE 默认（0）”，并说明不是显存自动选择；UI/CLI/队列仅对 NCNN、CUDA/PyTorch、TensorRT 传递显式分块，ONNX/FlashVSR 显式传入会拒绝；TensorRT Engine 缓存名加入 `__tile-<值>`，避免整帧和分块 Engine 复用。
- Files changed: `preview/1.9.6-preview.1/src/VideoEnhancerPlugin/PluginConfig.vb`、`PluginPanel.vb`、`QueueHook.vb`、`cli/Program.cs`、`cli/README.md`；同步 `preview/1.9.6-preview.2/README-抢先体验版.md`、`dist/README-抢先体验版.md`、EXE、DLL、ZIP 及根目录运行副本。
- Commands run: `git pull`（因本地未提交改动被 Git 阻止，远端已领先 2 个提交）；RVE 临时源码 `rg`/UTF-8 核对；`dotnet build cli/VideoEnhancer.csproj -c Release --no-restore`；插件 `build.ps1 -HostBin ... -SkipInstall`；`cli/build.ps1`；CLI `--help`；ONNX + `-tile-size 128` 参数拒绝检查；`git diff --check`；preview.2 发布文件重新同步和 ZIP 重压。
- Verification: CLI 与插件构建成功，0 错误、2 个既有 CA1416 警告；帮助显示“0 为 RVE 默认、仅 NCNN/CUDA/TensorRT”；ONNX 显式分块返回退出码 2；preview.2 dist 与根目录 EXE/DLL 已同步。新哈希：EXE `5ECDEA1D036082D56E5A33646130CF8FDAF2320CDF0A2FF2012D99189CA34DE6`，DLL `67D5A3B05913B948E5804BB7BEB177FBEBA5B8D57890460120384055CC290886`，ZIP `84DEB0C3503529248BD989744DE9B5D114586FC91A1E14473D1DE212C363C26`。
- Decisions: 界面预设值是常用便利值，不宣称为 RVE 固定枚举；分块尺寸表示输入边长而非块数量；不新增显存探测伪逻辑；TensorRT 缓存按 tile 隔离。
- Remaining: 尚未在真实 RVE Python 依赖/NVIDIA GPU 上测量各分块的峰值显存、速度及边缘画质；远端 `origin/main` 已领先 2 个提交，源码工作树仍有未提交改动，不能直接 `git pull`。
- Git status: canonical source `main...origin/main [ahead 4, behind 2]`，本地源码和内置包装器未提交；根目录不是 Git。建议先审核并提交本地改动，再合并远端提交后推送。
### 2026-08-22 17:30 - ZCode

- Objective: 用户询问插件如何修改 LakeUI（当前排版观感差），做只读代码分析并说明排版来源。
- Research: 插件不修改 LakeUI 源码、不继承 LakeUI 控件；`VideoEnhancerPlugin/build.ps1` 通过 `-r:` 引用 3FUI 宿主的 `LakeUI.dll`。LakeUI 控件全部在 `PluginPanel.vb` 中实例化并用属性赋值定制外观（`ConfigureCombo`/`ConfigurePrimaryButton`/`ConfigureSecondaryButton`/`ConfigureDpiSwitch`/`ConfigureOfficialTextBox`/`CreateOfficialValueBox` 等）。实际使用的 LakeUI 控件：ModernPanel(5)、ModernButton(20)、ModernComboBox(13)、ModernTextBox、ModernTabControl、ModernColorDialog、HtmlColorLabel(34)、BooleanSwitch(5)。注意 `QuadGridControls.vb` 中自绘的 `FluentCardPanel`（26 处使用）与 `FluentProgressBar` 是插件自有类（同命名空间遮蔽 LakeUI 同名控件）；布局容器仍是原生 TableLayoutPanel/FlowLayoutPanel/Panel，大量说明文字用原生 Label。
- Layout findings: 排版全部手写在 `PluginPanel.vb`：`InitializeUi`(根结构) / `BuildTabs`(左侧选项卡) / `BuildOfficialUpscalePage`(主页面 Dock=Top + 固定总高 586、12 行绝对行高、`applyDensity` 仅两档密度) / `BuildOfficialPreviewPage` / `BuildOfficialAdvancedPage` / `BuildOfficialModelDownloadPage` / `BuildOfficialConverterPage` / `CreateDownloadRow`。主要薄弱点：主页面顶部停靠固定高度导致高窗口下方留白；绝对像素列宽（150/12/170/220/82 等）不随窗口宽度与 DPI 缩放；行高为绝对值且未按 DPI 缩放（仅 BooleanSwitch 38x20 有缩放）；imageOptionsRow 8 列含空占位列；原生 Label 与 HtmlColorLabel 混用导致文字渲染观感不一致。
- Files changed: 无源码修改；仅追加本记录与 `version/工作进度.md` 条目。
- Commands run: 只读 `rg`/文件读取；canonical 仓库 `git status --short --branch`。
- Git status: canonical source `main...origin/main [ahead 4, behind 2]`，6 个修改文件 + 1 个未跟踪 Python 包装器未提交；根目录不是 Git。与上次记录一致，无新变化。
- Next step: 等用户指出具体页面/DPI/窗口尺寸下的排版问题（最好附截图），再逐页调整布局；调整前建议先提交当前未提交的 RVE/分块修改。
### 2026-08-22 17:45 - ZCode

- Objective: 按用户要求提交 canonical 仓库中此前多个会话积累的未提交改动。
- Review: `git status` 为 6 个修改文件 + 未跟踪 `cli/embedded-tools/rve-ordered-backend.py`，共 +740/−158；PluginConfig.vb/QueueHook.vb/VideoEnhancer.csproj 全量审核，Program.cs/PluginPanel.vb 做 hunk 头与新增行扫描；`git diff --check` 干净；少量“删同加同”行经 `-w` 复核确认为纯行尾差异（混合 CRLF/LF），不影响编译，未改写；未发现调试遗留（新增 `Console.WriteLine` 均为 HDR/迁移/管线状态提示）。
- Decision: 多组功能（RIFE 参数、下载并发、分块语义、帧级包装器）在同一批文件的 hunks 间交错，按 hunk 拆分会产生无法独立构建的中间提交，故合为一个提交并在 message 正文分条说明。
- Work completed: 提交 `aaa32b5` “feat: add RIFE backend selection and RVE tuning parameters”（7 文件），工作树干净。
- Git status: canonical source `main...origin/main [ahead 5, behind 2]`，无未提交文件；根目录不是 Git。
- Next step: 推送前先 `git pull` 合并远端 2 个领先提交（可能冲突）；实机 RVE/GPU 与 3FUI 视觉回归仍待用户执行。

### 2026-08-22 17:37 - Codex

- Objective: 修复用户截图中的超分工作台小窗口裁剪：补帧后端/转场阈值/补帧倍率右侧箭头消失，组合处理顺序高度异常，底部图片增强区域显示不全。
- Changes: 在 `preview/1.9.6-preview.1/src/VideoEnhancerPlugin/PluginPanel.vb` 的 LakeUI `ModernComboBox` 公共配置中统一 `AutoSize=False`、`Dock=Fill`、32px 最小高度和 `DropDownDisplayMode.Overlay`；字段编辑器同步设置最小高度；补帧参数列改为 29/47/24 比例；工作台根布局改为 `Dock=Top`、固定真实内容高度并由 `_pageUpscale.AutoScroll=True` 承载小窗口溢出；组合顺序行缩为与其他下拉框一致的 48px。
- Verification: `dotnet build cli\VideoEnhancer.csproj -c Release --no-restore` 通过；`VideoEnhancerPlugin\build.ps1 -HostBin ... -SkipInstall` 通过并生成插件 DLL；`git diff --ignore-space-at-eol --check` 通过；静态检查确认补帧倍率等控件均调用 `ConfigureCombo`。未替换任何 LakeUI 交互控件为 WinForms 原生控件。
- Risk: 当前环境没有完整可启动的 3FUI 宿主，尚未做截图级 DPI/小窗口视觉回归；文件保留既有混合换行差异，未回滚此前用户改动。
- Git status: canonical source `main...origin/main [ahead 5, behind 2]`，`VideoEnhancerPlugin/PluginPanel.vb` 有未提交修改；工作树不干净；根目录不是 Git。建议先审核并提交本次布局修复，再处理远端领先的 2 个提交。

### 2026-08-22 17:43 - Codex

- Feedback: 用户提供小窗与最大化对比截图，确认最大化基本正常；小窗仍存在右侧下拉框被滚动条边界遮挡，组合处理顺序需下移，且中间框选中文本不显示。
- Changes: 工作台根 `TableLayoutPanel` 增加 12px 右侧安全内边距，宽度不再手工减滚动条宽度；组合顺序行增加 8px 顶部间距并将紧凑/宽松行高分别调整为 56/64px；`Editable=False` 后重新设置 `_cmbProcessOrder.SelectedIndex`，恢复选中文本。
- Verification: CLI 构建 0 错误、2 个既有 CA1416 警告；插件构建成功生成 `videoenhancer.3fui.dll`；`git diff --check` 通过。需用户替换最新 DLL 后复测小窗右侧箭头和组合顺序文字。
- Git status: canonical source `main...origin/main [ahead 5, behind 2]`，工作树仍有未提交的 `VideoEnhancerPlugin/PluginPanel.vb`；根目录不是 Git。建议先提交布局修复，再处理远端领先的 2 个提交。

### 2026-08-22 17:50 - Codex

- Decision: 按用户要求放弃超分/补帧左右并排布局，改为视频超分在上、运动补帧在下的单列布局。
- Changes: `modes` 改为一列两行；两个处理面板各占约 190px，补帧三列改为整行宽度，根工作台紧凑/宽松内容高度同步调整为 850/926px；小窗口只纵向滚动，不再压缩补帧控件的右侧空间。
- Verification: CLI 构建 0 错误；插件构建成功生成 `preview/1.9.6-preview.1/src/videoenhancer.3fui.dll`；静态检查确认 `modes.Controls.Add(interpPane, 0, 1)` 且 `git diff --ignore-space-at-eol --check` 通过。
- Git status: canonical source `main...origin/main [ahead 5, behind 2]`，工作树有未提交的 `VideoEnhancerPlugin/PluginPanel.vb`；根目录不是 Git。建议替换 DLL 实机确认后提交，再处理远端领先的 2 个提交。

### 2026-08-22 18:05 - Codex

- Feedback: 用户确认上下布局可接受，但纵向滚动条与深色界面不协调。
- Changes: 在 `PluginPanel.vb` 中为工作台 AutoScroll 生成的 `VScrollBar/HScrollBar` 增加 `DarkMode_Explorer` Windows 主题、深色背景和浅色前景，并在布局完成及尺寸变化后重新应用；未改变滚动逻辑，也未替换 LakeUI 控件。
- Verification: CLI 构建 0 错误、2 个既有 CA1416 警告；插件构建成功生成 `preview/1.9.6-preview.1/src/videoenhancer.3fui.dll`；差异检查通过。需用户在实际 3FUI 宿主中确认滚动条主题是否被宿主覆盖。
- Git status: canonical source `main...origin/main [ahead 5, behind 2]`，工作树有未提交的 `VideoEnhancerPlugin/PluginPanel.vb`；根目录不是 Git。建议替换 DLL 确认后提交。

### 2026-08-22 18:18 - Codex

- Feedback: 宿主仍显示白色滚动条，组合处理顺序下拉框有箭头但没有选中文本。
- Changes: `ModernComboBox` 初始化和 `UpdateProcessOrderState` 同时写入 `SelectedIndex` 与 `Text` 并调用 `Refresh()`；滚动条主题从 WinForms 控件递归查找改为枚举工作台真实子 HWND，匹配 `ScrollBar` 类名后应用 `DarkMode_Explorer`。
- Verification: CLI 构建 0 错误；插件构建成功生成 `preview/1.9.6-preview.1/src/videoenhancer.3fui.dll`；差异检查通过。需替换 DLL 后确认宿主实际显示。
- Git status: canonical source `main...origin/main [ahead 5, behind 2]`，工作树有未提交的 `VideoEnhancerPlugin/PluginPanel.vb`；根目录不是 Git。

### 2026-08-22 18:30 - Codex

- Feedback: 用户明确要求滚动条为黑色，并要求组合处理顺序只有超分和补帧同时启用时才点亮。
- Changes: 增加工作台黑色自绘滚动条覆盖层，轨道为黑色、滑块为深灰色，支持拖动并按实际滚动范围显示/隐藏；`_cmbProcessOrder.Enabled` 改为插件总开关、超分开关、补帧开关三者同时为真。
- Verification: CLI 构建 0 错误、2 个既有 CA1416 警告；插件构建成功生成 `preview/1.9.6-preview.1/src/videoenhancer.3fui.dll`；差异检查通过。
- Git status: canonical source `main...origin/main [ahead 5, behind 2]`，工作树有未提交的 `VideoEnhancerPlugin/PluginPanel.vb`；根目录不是 Git。

### 2026-08-22 18:45 - Codex

- Feedback: 组合处理顺序在实际初始化后仍不显示当前状态文字。
- Changes: 增加句柄创建后的延迟同步队列；同步时重新设置 `SelectedIndex`，临时切换 `Editable=True` 写入 `Text`，再恢复 `Editable=False`，最后执行 `Invalidate/Update`，绕过 LakeUI 首次布局清空文本缓存的问题。
- Verification: CLI 构建 0 错误；插件构建成功生成 `preview/1.9.6-preview.1/src/videoenhancer.3fui.dll`；差异检查通过。
- Git status: canonical source `main...origin/main [ahead 5, behind 2]`，工作树有未提交的 `VideoEnhancerPlugin/PluginPanel.vb`；根目录不是 Git。

### 2026-08-22 19:00 - Codex

- Objective: 将补帧/超分同时开启时的后端组合行为写入插件内置使用教程。
- Changes: “快速上手”新增组合顺序启用条件；新增“同后端与跨后端”章节，说明同后端逐帧单进程、先超后补帧传递包装器、跨后端 FFV1 无损中间 MKV、SDR `gbrp10le`、PQ/HLG HDR `gbrp16le`、音频字幕复制、临时文件自动清理和磁盘空间要求。
- Verification: 插件构建成功生成 `preview/1.9.6-preview.1/src/videoenhancer.3fui.dll`；差异检查通过。
- Git status: canonical source `main...origin/main [ahead 5, behind 2]`，工作树有未提交的 `VideoEnhancerPlugin/PluginPanel.vb`；根目录不是 Git。

### 2026-08-22 19:15 - Codex

- Feedback: 用户指出 3FUI 性能监控页已有贴合主题的滚动条，不应重复实现。
- Finding: `LakeUI.dll` 暴露公开的 `LakeUI.V3_ScrollBarRenderer`，提供 `ComputeLayout`、拖动和滚轮计算；宿主源码目录当前为空，因此通过实际程序集反射确认 API。
- Changes: 工作台黑色滚动条覆盖层改为复用 `V3_ScrollBarRenderer.ComputeLayout` 计算轨道和滑块几何，保留黑色主题绘制；教程内容保持不变。
- Verification: 插件构建成功生成 `preview/1.9.6-preview.1/src/videoenhancer.3fui.dll`；差异检查通过。
- Git status: canonical source `main...origin/main [ahead 5, behind 2]`，工作树有未提交的 `VideoEnhancerPlugin/PluginPanel.vb`；根目录不是 Git。

### 2026-08-22 19:35 - Codex

- Feedback: 用户希望参考 3FUI/LakeUI 的性能优化，解决插件首屏首次渲染较慢和操作卡顿。
- Finding: 插件构造阶段一次性创建所有页面，并立即创建“模型指南”和“使用教程”的两个 `WebBrowser` 控件；`OnPanelLoad` 后才异步启动预览/模型读取。首屏同步开销主要来自浏览器引擎初始化和未批量挂起的选项卡布局。
- Changes: Markdown 教程页改为保存源文本，首次切换到对应选项卡时才创建 `WebBrowser`；`BuildTabs()` 外层增加 `_tabs.SuspendLayout/ResumeLayout(False)`，减少首次添加选项卡时的重复布局。
- Verification: 插件构建成功生成 `preview/1.9.6-preview.1/src/videoenhancer.3fui.dll`；差异检查通过。未在完整宿主中取得毫秒级基准，需用户实测首次打开插件和首次进入教程页的响应。
- Git status: canonical source `main...origin/main [ahead 5, behind 2]`，工作树有未提交的 `VideoEnhancerPlugin/PluginPanel.vb`；根目录不是 Git。

### 2026-08-22 19:55 - Codex

- Feedback: 用户截图确认白色系统滚动条仍位于工作台非客户区，覆盖层无法遮住；组合顺序文字仍为空，对此前修复提出质疑。
- Changes: 通过 `ShowScrollBar(hwnd, SB_VERT, False)` 隐藏工作台真实系统滚动条，保留 `LakeUI.V3_ScrollBarRenderer` 黑色覆盖层；覆盖层显示条件改为按内容控件实际底部判断。组合顺序延迟同步最终保持 LakeUI 可绘制文本模式，避免 `Editable=False` 清空显示缓存。
- Performance: 教程 WebBrowser 延迟初始化，选项卡构造批量挂起布局。
- Verification: 插件构建成功生成 `preview/1.9.6-preview.1/src/videoenhancer.3fui.dll`；差异检查通过。需替换 DLL 后确认白色系统条消失、黑色覆盖层出现、组合文字显示。
- Git status: canonical source `main...origin/main [ahead 5, behind 2]`，工作树有未提交的 `VideoEnhancerPlugin/PluginPanel.vb`；根目录不是 Git。

### 2026-08-22 20:15 - Codex

- Feedback: 用户确认滚动一次后白色系统滚动条会复现，组合处理顺序文字仍为空。
- Changes: 在工作台 `Scroll` 事件中立即及 `BeginInvoke` 后再次调用 `ShowScrollBar(..., SB_VERT, False)`；组合顺序增加 LakeUI `HtmlColorLabel` 显示层，直接绑定当前配置文本，底层 `ModernComboBox` 保留箭头和下拉交互。
- Verification: 插件构建成功生成 `preview/1.9.6-preview.1/src/videoenhancer.3fui.dll`；差异检查通过。需替换 DLL 后重点复测滚动前后系统白条和组合文字。
- Git status: canonical source `main...origin/main [ahead 5, behind 2]`，工作树有未提交的 `VideoEnhancerPlugin/PluginPanel.vb`；根目录不是 Git。

### 2026-08-22 20:40 - Codex

- Feedback: 用户截图显示系统 AutoScroll、覆盖滚动条和组合文字覆盖层同时工作，产生白条、重复控件和滚动错位，要求重新学习 LakeUI 正确用法。
- Finding: 反射 LakeUI 3.22.0 确认 `ModernPanel` 内置 `V3_ScrollBarRenderer`，公开 `ScrollBarMode/Width/TrackColor/ThumbColor/VerticalScrollStep/ScrollTo`；`ModernComboBox` 内部使用 `SingleLineTextBoxRenderer`，应通过真实 `SelectedIndex` 变化同步，不应叠加标签或强写 `Text`。
- Changes: `_pageUpscale` 从 WinForms `Panel` 改为 LakeUI `ModernPanel`，启用原生 Vertical 滚动并关闭系统 AutoScroll；删除全部 P/Invoke、系统滚动条主题、自绘覆盖层和组合文字覆盖层；组合顺序通过 `SelectedIndex=-1` 后重新选中目标项更新内部 renderer。
- Verification: 插件构建成功；实例化插件后读取到 `PageType=LakeUI.ModernPanel, ScrollMode=Vertical, AutoScroll=False, Track=(18,18,18)`；组合框读取到 `SelectedIndex=0, SelectedItem/Text=画质优先：先超分，再补帧, Editable=False`；差异检查通过。
- Git status: canonical source `main...origin/main [ahead 5, behind 2]`，工作树有未提交的 `VideoEnhancerPlugin/PluginPanel.vb`；根目录不是 Git。

### 2026-08-22 18:53 - Codex

- Objective: 将模型下载页接入 LakeUI 3.22.0 原生滚动 API，优化拖动残影，并调查插件在最大化、最小化恢复时明显整页重绘的问题。
- Finding: `_downloadList` 仍是 `FlowLayoutPanel.AutoScroll`，且列表宽度变化后用 `BeginInvoke` 重置 `AutoScrollMinSize`；插件顶层同时启用了 `ControlStyles.ResizeRedraw`，并监听宿主恢复后递归执行三层 `Invalidate(True)/Update()`。这些路径会让透明 WinForms 子层重复布局和同步绘制，3FUI 内置 LakeUI 页面没有这套额外强制重绘。
- Changes: `_downloadList` 改为 `LakeUI.ModernPanel`，配置 `LayoutMode=Flow`、`FlowDirection=TopDown`、`ScrollBarMode=Vertical`、10px 深色轨道/滑块和 48px 步长；内部下载分组内容也改为 ModernPanel Flow；移除系统 AutoScroll 宽度重置队列；加载和列表重建使用 `SuspendLayout/ResumeLayout` 一次性提交，并给滚动面使用不透明画布。删除插件 `ResizeRedraw`、宿主 Resize 监听及恢复时的强制全树同步重绘。
- Verification: `dotnet build cli\VideoEnhancer.csproj -c Release --no-restore` 成功（0 错误、2 个既有 CA1416 警告）；插件 `build.ps1 -HostBin ... -SkipInstall` 成功；`git diff --ignore-space-at-eol --check` 通过。运行时实例化确认 `DownloadType=LakeUI.ModernPanel`、`LayoutMode=Flow`、`ScrollBarMode=Vertical`、`AutoScroll=False`、轨道 `(18,18,18)`、滑块 `(72,72,72)`，且顶层 `ResizeRedraw=False`。
- Remaining: 当前环境没有可启动的完整 3FUI 宿主，无法替代实机肉眼验证滚动拖动动画和最大化/最小化恢复观感；需替换最新 DLL 后复测。
- Git sync: `git pull --ff-only` 因 `ahead 5, behind 2` 分叉中止且未改变工作树。canonical source 仅 `VideoEnhancerPlugin/PluginPanel.vb` 未提交，工作树不干净；根目录不是 Git。

### 2026-08-22 19:03 - Codex

- Feedback: 用户指出 3FUI 原生控件密集页面在最大化/小窗切换时自然流畅，怀疑插件没有使用 LakeUI DPI 和窗口适配机制。
- Finding: `LakeUI.V3_DpiContext` 是内部类型，`ModernPanel` 与 `ModernTabControl` 自身实现 `OnDpiChangedBeforeParent/AfterParent`；运行时插件为 `AutoScaleMode=Inherit`、tabs 为 `AutoScaleMode=Dpi`，因此 DPI 链没有缺失。最大化/还原在同一显示器不会改变 DPI。插件独有的 `ModernPanel1` 宿主背景映射会进入 `D3D_BackgroundPenetration.OnSourceAncestorResized`，窗口尺寸变化时重建背景源缓存；再叠加反射开启的根面板/tabs/TableLayout 双缓冲和工作台高度阈值 `ResumeLayout(True)`，形成可见整页重绘。
- Changes: 根字段从 `ModernPanel1` 改为 `_rootPanel`/`VideoEnhancerRoot`，明确 `BackgroundSource=Nothing`；根、tabs 与全部页面使用不透明 `UiCanvas #181818`；删除 UserControl 自定义绘制样式和 `EnableControlDoubleBuffer` 反射路径，让 LakeUI 使用自身绘制机制；删除工作台按窗口高度切换 850/926 行高并整页重排的 `ClientSizeChanged` 处理，固定内容高度交由原生滚动视口承载。
- Verification: 插件与 CLI 构建成功，0 错误；`git diff --ignore-space-at-eol --check` 通过。运行时确认 `PanelAutoScaleMode=Inherit`、`TabsAutoScaleMode=Dpi`、旧 `ModernPanel1` 字段不存在、根 `BackgroundSource=<null>`、根/tabs/工作台背景均为 `(24,24,24)`，插件不再手工启用 `OptimizedDoubleBuffer`。
- Tradeoff: 为获得与 3FUI 原生参数页一致的稳定不透明绘制，本次主动取消插件个性化背景穿透；LakeUI 控件的主题、DPI 和原生双缓冲仍保留。
- Remaining: 需用户在完整 3FUI 宿主中确认窗口动画观感；本地反射/构建无法验证肉眼可见的 DWM 切换动画。
- Git status: canonical source `main...origin/main [ahead 5, behind 2]`，仅 `VideoEnhancerPlugin/PluginPanel.vb` 未提交；工作树不干净，根目录不是 Git。

### 2026-08-22 19:16 - Codex

- Feedback: 用户截图显示最大化/小窗切换期间，超分和补帧右列短暂停留在视口外，观感像控件被重新创建。
- Host research: 用 Mono.Cecil 反编译 `FFmpegFreeUI.dll`。`插件管理.添加自定义Winform面板` 只把 Entry 传入的同一个 Control 保存到字典并调用 `FormMain_v6.添加插件选项卡`；宿主仅设置 `Dock=Fill` 和 `BoundControl`，Resize 不会再次执行插件 Entry。宿主背景映射严格要求 ModernPanel 的 `Name="ModernPanel1"` 且 `Dock=Fill`，当前 `VideoEnhancerRoot` 不会被绑定。
- Diagnosis: 宽窗/小窗测试中对象 ID 和 HWND 均保持不变，排除控件/句柄重建。截图对应 `_pageUpscale` 的 LakeUI Absolute 滚动视口已经缩小，而其 TableLayoutPanel 仍使用宽窗宽度的中间帧；嵌套列因此暂时绘制到视口外。
- Changes: 在 `_pageUpscale.ClientSizeChanged` 中执行单一 `syncViewportWidth` 布局事务：计算当前 ClientSize，若宽度变化则 SuspendLayout、修改现有 root.Width、ResumeLayout(True)。不修改行高，不调用 Controls.Clear/Remove/Add，也不延迟到 BeginInvoke。
- Verification: 插件构建成功；`git diff --ignore-space-at-eol --check` 通过。隐藏宿主窗体模拟从 1600x900 切到 1000x700 再切回：页面/根宽度立即为 `1530/1518 -> 930/918 -> 1530/1518`，无需等待 DoEvents；PluginPanel、root、后端下拉框对象和 Handle 全程相同（`StableObjects=True`, `StableHandles=True`）。
- Remaining: 完整 3FUI/DWM 动画仍需用户肉眼验证，但已针对截图中的旧宽度中间帧提供可重复的运行时验证。
- Git status: canonical source `main...origin/main [ahead 5, behind 2]`，仅 `VideoEnhancerPlugin/PluginPanel.vb` 未提交；工作树不干净，根目录不是 Git。

### 2026-08-22 19:43 - Codex

- Objective: 按 LakeUI 作者建议，将模型下载页从大量嵌套控件改为 LakeUI 3.22.0 列表视图，减少首次渲染、滚动和窗口缩放时的布局/重绘成本。
- Changes: `_downloadList` 改为 `LakeUI.UltraDetailListView`；配置 4 列、原生分组/折叠、深色滚动条和响应式首列；每个分类增加“下载本组”数据行，单文件下载通过操作列点击；加载、离线和错误状态改为列表项；删除每组面板、展开按钮、每文件标签和按钮的创建及递归按钮遍历。
- Download behavior: 单文件和分组下载共用全局 3 槽上限；分组内部先并行启动可用槽，任一任务结束立即补下一个，不等待固定批次完成；下载进度只更新对应列表子项。
- Verification: `dotnet build cli\VideoEnhancer.csproj -c Release --no-restore` 成功（0 错误、2 个既有 CA1416 警告）；`VideoEnhancerPlugin\build.ps1 -HostBin ... -SkipInstall` 成功；真实 CLI 清单返回 82 项并渲染为 8 groups / 90 items / 0 child controls；旧结构按 8 组 82 文件会创建 302 个后代控件；并发槽反射测试为 `True,True,True,False`，清理后活动数 0；列表对象与 HWND 在尺寸切换后稳定；`git diff --ignore-space-at-eol --check` 通过。
- Remaining: 当前环境没有可直接启动的完整 3FUI 宿主，仍需用户实机确认列表样式、分组折叠、操作列命中、滚动和最大化/还原动画。
- Git status: canonical source `main...origin/main [ahead 5, behind 2]`，仅 `VideoEnhancerPlugin/PluginPanel.vb` 未提交；工作树不干净。此前 `git pull --ff-only` 因分叉中止，未执行合并、变基或回滚；根目录不是 Git。

### 2026-08-22 19:54 - Codex

- Feedback: 用户确认模型页改造后，超分工作台在最大化/还原时仍呈现明显的控件重绘。
- Diagnosis: 运行时基线测得工作台 78 个控件一次宽度切换触发 61 次 Layout 和 `362/404` 次 Paint。控件并未重建；主要来源是 `_pageUpscale.ClientSizeChanged` 手工修改根表宽并 `ResumeLayout(True)`，以及多层透明 TableLayoutPanel 向父级逐层请求背景绘制。
- Changes: 根表改为固定 850px 高度、左右 Anchor，删除尺寸事件和整树强制布局；工作台页面、根表、字段容器、标题、分隔、模式区和图片区布局层统一为不透明 `UiCanvas #181818`，不改变 LakeUI 交互控件和视觉颜色。
- Exception fix: 诊断关闭流程截获 `ObjectDisposedException: LakeUI.ModernPanel`，栈位于 LakeUI 3.22.0 `ModernTabControl.OnVisibleChanged -> 显示绑定控件`。插件 `Dispose` 现在在基类销毁子页面前将所有 `ModernTab.BoundControl` 设为 `Nothing`，相同显示/缩放/关闭测试为 `ThreadException=<none>`。
- Verification: 优化后相同缩放测试为 58 次 Layout、`22/28` 次 Paint，约减少 93%；PluginPanel、页面、根表、后端下拉框对象和 HWND 均稳定。1000x700 宿主下关键下拉框高度 36-39px，根内容 `946x850`、LakeUI Vertical 滚动有效。CLI 构建 0 错误（2 个既有 CA1416 警告），插件构建成功，`git diff --ignore-space-at-eol --check` 通过。
- Remaining: 当前环境没有完整 3FUI 宿主，最终 DWM 最大化/还原动画仍需用户实机确认。
- Git status: `main...origin/main [ahead 5, behind 2]`；仅 `VideoEnhancerPlugin/PluginPanel.vb` 未提交，工作树不干净。`git pull --ff-only` 再次因分叉安全中止；未执行 merge/rebase/revert。

### 2026-08-22 20:37 - Codex

- Feedback: 用户拒绝取消个性化背景的优化方案，要求直接研究 FFmpegFreeUI 官方源码，并恢复超分工作台 LakeUI 滚动条。
- Official research: 检出 `Lake1059/FFmpegFreeUI` 6.1.39（`642ddf4`）；确认参数页以 `ModernPanel1/Vertical` 为背景与滚动根，内部直接使用固定高度、`Dock.Top/Left/Fill` 的普通 `Panel` 和 LakeUI 控件，不使用 `TableLayoutPanel`。`Module1.DoubleBuffer` helper 在官方源码中没有调用；宿主只给字段名、控件名均为 `ModernPanel1` 且 `Dock=Fill` 的 LakeUI 面板绑定 `BackgroundSource`。
- Changes: 恢复 `ModernPanel1` 透明背景契约；工作台使用 LakeUI `ModernPanel.ScrollMode.Vertical` 黑色滚动条；将视频超分和运动补帧的多层“模式区/处理区/字段表”改为根 ModernPanel 直接承载字段，少量横向行使用轻量普通 Panel 布局；保留所有 LakeUI 下拉框、开关、按钮和销毁前解绑 BoundControl 的异常修复。
- Verification: 官方质量页同宿主对照为小窗/宽窗 `13 Layout, 20/16 Paint`；插件旧基线 `61 Layout, 362/404 Paint`，本版为 `62 Layout, 31/45 Paint`。930px 小窗下 11 个工作台下拉框最小 `207x36`，组合顺序 `SelectedItem/Text` 均正确；内容 `864x850` 大于视口 `876x544`，Vertical 滚动有效。彩色背景截图 `C:\Users\maxzr\AppData\Local\Temp\videoenhancer-official-layout-probe.png` 确认背景透出、黑色轨道/深灰滑块可见；关闭测试 `ThreadException=<none>`。CLI 构建 0 错误（2 个既有 CA1416 警告），插件构建成功，差异检查通过。
- Files changed: canonical source `VideoEnhancerPlugin/PluginPanel.vb`；同步更新 `docs/codex/STATUS.md`、`version/工作进度.md`。临时探针位于 `%TEMP%\ve-layout-test`，不属于项目源码。
- Remaining: 完整 3FUI/DWM 最大化、还原、滚动拖动观感仍需用户实机确认；模型列表鼠标操作也需实机回归。
- Git status: canonical source `main...origin/main [ahead 5, behind 2]`，仅 `VideoEnhancerPlugin/PluginPanel.vb` 未提交，工作树不干净。启动时 `git pull --ff-only` 因分叉安全中止；未执行 merge/rebase/revert。建议先提交当前插件修改，再单独决定如何同步远端 2 个提交。

### 2026-08-22 20:44 - Codex

- Feedback: 用户实机确认最大化、还原和滚动已流畅，但截图中工作台滚动条仍未出现。
- Diagnosis: 实际安装的 `C:\Program portable\3FUI\plugin\videoenhancer.3fui.dll` 为 19:53 旧版，SHA-256 `8277EF28C4C9175FD1172D860EE70EDD2BFD3F38098C113ECD0C390B968720C3`；最新构建为 20:38，SHA-256 `1B265683B8CF0B0A6A88FD13B14E82CD8D651E3FC1126384E16922FC4FF56C48`。实机尚未加载带原生 LakeUI 滚动条的最新代码。
- Action: 尝试覆盖安装时被 Windows 拒绝，确认占用进程为 `C:\Program portable\3FUI\FFmpegFreeUI.exe`（PID 48132）。最新 DLL 已暂存为 `C:\Program portable\3FUI\plugin\videoenhancer.3fui.dll.new`，暂存文件哈希与构建输出一致。
- Blocker/next: 等待用户完全退出 3FUI 后，将 `.dll.new` 原位替换为 `videoenhancer.3fui.dll` 并再次核对哈希；不应继续修改滚动代码或用旧 DLL 判断结果。
- Git status: canonical source 仍为 `main...origin/main [ahead 5, behind 2]`，仅 `VideoEnhancerPlugin/PluginPanel.vb` 未提交。

### 2026-08-22 20:47 - Codex

- User action: 用户完全退出 3FUI，解除旧插件 DLL 文件占用。
- Install: 已将最新 `preview/1.9.6-preview.1/src/videoenhancer.3fui.dll` 覆盖到 `C:\Program portable\3FUI\plugin\videoenhancer.3fui.dll`。安装文件长度 `4549632`，时间戳 `2026-08-22 20:38:55`，源与目标 SHA-256 均为 `1B265683B8CF0B0A6A88FD13B14E82CD8D651E3FC1126384E16922FC4FF56C48`。
- Cleanup: 终端安全策略拒绝删除同目录 `.dll.new` 暂存文件；该后缀不会被 3FUI 插件加载，不影响运行，未绕过策略。
- Next: 用户重新启动 3FUI 后复测工作台 LakeUI 滚动条。若仍不显示，此时才基于确认安装的新 DLL 继续调查实机绘制差异。
- Git status: canonical source 仍为 `main...origin/main [ahead 5, behind 2]`，仅 `VideoEnhancerPlugin/PluginPanel.vb` 未提交。

### 2026-08-22 20:50 - Codex

- Feedback: 最新 DLL 的背景与滚动条已生效，但插件总开关、视频超分和运动补帧开关被拉成约 42x40 的大圆，用户要求恢复胶囊样式并直接安装 DLL。
- Cause: 浅层布局新增的 `HorizontalLayoutPanel.OnLayout` 无条件把子控件拉伸到整列，覆盖了 `ConfigureDpiSwitch` 按 DPI 设置的 96 DPI 基准 `38x20` 尺寸；`Anchor=None` 没有被布局器尊重。
- Change: 横向布局器对 `Anchor=None` 的定尺寸控件保留当前 DPI 尺寸并在列内水平/垂直居中；其他按钮、标签、下拉框继续填充列。代码注释使用中文。
- Verification: 插件构建成功；小窗与宽窗运行时探针均读取三个工作台 BooleanSwitch 为 `38x20,38x20,38x20`；Paint 为 `41/43`，关闭 `ThreadException=<none>`；差异检查通过。
- Install: 已直接覆盖 `C:\Program portable\3FUI\plugin\videoenhancer.3fui.dll`，源与安装文件 SHA-256 均为 `171B2E4F04BC86D73FBA4F958F09C3E62C8724780C3C9686D0D4FD711E9E6820`，安装时间戳 `2026-08-22 20:49:45`。
- Git status: canonical source 仍为 `main...origin/main [ahead 5, behind 2]`，仅 `VideoEnhancerPlugin/PluginPanel.vb` 未提交。建议实机确认后提交，再处理远端分叉。

### 2026-08-22 21:07 - Codex

- Incident: 用户运行 NCNN `upscale-first`（1920x1080 2x 超分 + RIFE 2x）时，`rve-ordered-backend.py:47` 在 `sceneDetect.detect(frame)` 抛出 `ValueError: cannot reshape array of size 24883200 into shape (1080,1920,3)`。
- Diagnosis: `24883200 = 3840*2160*3`，实际帧已是 4K RGB24；RVE `UpscaleNCNN.__call__` 却使用源 `self.width/self.height` 构造返回 Frame，导致元数据仍是 1920x1080。插件包装器又把转场检测放在超分后，NCNN 检测器 clone/resize 时按错误元数据解析字节。RVE 原生 RenderVideo 在超分前执行转场检测，因此不触发。
- Recommended fix: 在插件包装器中先对源尺寸 Frame 调用 `sceneDetect.detect` 并保存结果，再超分并把结果送入补帧；同时按实际超分倍率校正 NCNN 返回 Frame 的 width/height，避免后续消费者再次依赖错误元数据。该修复可局限于本项目，不要求先修改外部 RVE 项目。
- Additional finding: 日志显示 RVE 读帧为 `yuv420p -> rgb24`，虽然最终编码指定 `yuv420p10le`，当前直接帧管线实际仍按 8-bit RGB 推理；这与“源视频 8/10/12-bit 自动传递”的长期目标不同，需单独处理，不能把 10-bit 输出编码误认为 10-bit 内部处理。
- Action: 本轮按用户“这什么情况”的问题只完成只读诊断，未修改代码。渲染线程已异常退出，当前任务大概率无法自行完成，应停止任务后再修复重试。
- Git status: canonical source 仍为 `main...origin/main [ahead 5, behind 2]`，仅 `VideoEnhancerPlugin/PluginPanel.vb` 未提交。

### 2026-08-22 21:35 - Codex

- Objective: 修复 NCNN `upscale-first` 的 4K 字节/1080p Frame 元数据崩溃，并全面复核现有超分、RIFE 补帧、组合顺序、失败传播、位深和 HDR 边界。
- Changes: `rve-ordered-backend.py` 在源帧上执行转场检测，分别按源尺寸初始化 SceneDetect、按最终超分尺寸初始化 RIFE；统一包装 ONNX 裸 bytes 并校正 NCNN/PyTorch/TensorRT Frame 尺寸；检查 RGB24/RGB48 字节长度；渲染线程异常会停止队列并以 `VIDEOENHANCER_FATAL` 非零退出。CLI 内置工具缓存改为 SHA-256 比较；异步输出排空后识别 traceback/fatal，部分输出不再掩盖失败；HDR 对 NCNN/ONNX/FlashVSR 增加拒绝逻辑。教程补充 SDR 内部仍为 8-bit RGB24、10-bit 输出不等于 10-bit 推理。
- Verification: Python 编译通过；`unittest` 6 项通过；CLI/插件构建成功（0 错误、2 个既有 CA1416 警告）；NCNN 真实 3 帧短片验证仅超分、仅补帧、先补后超、先超后补全部退出 0，输出分别为 `256x144/2fps/3帧`、`128x72/4fps/5帧`、`256x144/4fps/5帧`、`256x144/4fps/5帧`。模型发现：NCNN/CUDA/TensorRT/ONNX/FlashVSR 超分为 `22/43/44/28/1`，RIFE NCNN/CUDA/TensorRT 为 `5/0/0`。缓存脚本与源码 SHA-256 一致。
- Install: 已覆盖 `C:\Program portable\3FUI\plugin\videoenhancer.exe` 与 `videoenhancer.3fui.dll`。EXE SHA-256 `011C6DCBE0A7C8399BACD0A5218118FEAA79EF935D7148AC3A63B9EB97603E18`；DLL SHA-256 `972EC2F0E98586886F4B4801EF516E7F7ECEE7A6236127F7AFC0BAAD72813312`；源/目标一致。
- Remaining: 用户要求停止自动实测并自行验证原视频。合成 PQ/BT.2020 10-bit 样本未触发 `DetectHdrMode`，HDR 拒绝逻辑因此未进入，必须后续修复探测；`--check` 还报告当前 AMD 主机上的一个 TensorRT Engine 不兼容，这是预期环境限制。CUDA/TensorRT RIFE 本机无 `.pth` 模型且无 NVIDIA GPU，未做真实推理。Python 编译生成的 `cli/embedded-tools/__pycache__` 因终端删除策略未能清理。
- Git status: canonical source `main...origin/main [ahead 5, behind 2]`；修改 `VideoEnhancerPlugin/PluginPanel.vb`、`cli/Program.cs`、`cli/README.md`、`cli/embedded-tools/rve-ordered-backend.py`，新增 `cli/tests/`，并有生成的未跟踪 `cli/embedded-tools/__pycache__/`。工作树不干净；建议提交源码/测试但排除 `__pycache__`，再处理远端分叉。

### 2026-08-22 22:30 - Codex

- Objective: 按功能将当天有效修改实际推送到 `user-Wing/VideoEnhancer`，每个方向单独创建 PR，PR 标题使用中文。
- PRs: #3 `自动构建设备专用的 TensorRT 引擎缓存`；#4 `支持可配置的超分与补帧处理顺序`；#5 `增加 RIFE 独立后端与推理参数设置`；#6 `优化 LakeUI 工作台布局与窗口渲染`；#7 `重构模型下载列表并支持三路连续并发`；#8 `修复先超后补帧管线与异常传播`；#9 Draft `增加 HDR 检测与位深能力门禁`。
- Changes: #6 只保留 LakeUI 官方浅层布局、ModernPanel 背景契约、Vertical 黑色滚动条、DPI 定尺寸开关和 Dispose 前解绑；#7 使用 `UltraDetailListView`、本地已安装识别和最多 3 路连续补位下载，并修复旧 `models\ffmpeg` 到 `bin\ffmpeg` 的迁移；#8 使用内置帧包装器修正超分 Frame 尺寸、源帧转场检测、错误传播、SHA-256 工具缓存和 6 项 Python 测试；#9 使用 ffprobe JSON 识别 PQ/HLG 并加入后端能力门禁。
- Verification: #5/#6/#7/#8/#9 的独立 worktree 均 clean 且已推 fork；CLI/插件构建通过（#3-#8 保留 2 个既有 CA1416 警告，#9 构建 0 警告）；#8 Python unittest 6/6 通过；模型清单返回 81 文件，旧 FFmpeg 迁移探针成功；工作台小窗探针 `62 Layout / 36 Paint`、宽窗 `62 Layout / 44 Paint`，11 个下拉框最小 207x36，开关 38x20，关闭无 ThreadException。
- Limitations: 当前合成 PQ 文件被编码器写成 `color_transfer=unknown`，无法作为 HDR 探测的有效实机证据；#9 已保持 Draft。canonical 主工作树仍保留用户未提交修改，未做 reset/clean；主仓库 `main...origin/main [ahead 5, behind 2]`，远端分叉未合并。
- Git status: PR worktrees `03-rife-tuning`、`04-lakeui-workbench`、`05-model-downloads`、`06-frame-pipeline`、`07-hdr-bit-depth` 均 clean 并跟踪 fork 分支；canonical source 仍 dirty，建议用户审查 PR 后再决定是否提交主工作树。

### 2026-08-22 21:46 - Codex

- Objective: 按用户要求把今天的工作整理成若干待提交 PR，不在本轮创建远端 PR 或改写现有 Git 历史。
- Repository finding: 本地 `main` 从共同基线 `220ebb4` 分叉；本地领先 5 个提交，远端新增 `b0ce34b` 与合并提交 `5cafaca`（1.4）。远端 1.4 同时修改 `PluginPanel.vb`、`PluginConfig.vb`、`Program.cs`、README、构建脚本等高冲突文件，不能直接推当前分支。
- PR plan: PR1 TensorRT 设备专用 Engine 自动构建/缓存（以 `762cabb` 为来源）；PR2 超分与补帧组合顺序和跨后端 FFV1 管线（以 `ce75515` 为来源）；PR3 RIFE 独立后端、官方转场阈值、动态光流和超分分块参数（以 `aaa32b5` 为来源，依赖 PR2）；PR4 LakeUI 工作台/模型页性能与布局（合并当前最终方案，吸收但不原样提交 `f774532`）；PR5 模型下载页 `UltraDetailListView` 与全局最多 3 个连续补位下载（从当前 `PluginPanel.vb` 选择性拆分）；PR6 `upscale-first` Frame 契约、线程错误传播、缓存哈希和测试（依赖 PR2/PR3，已验证）；PR7 HDR/位深探测、后端能力门禁与文档（Draft，依赖 PR6）。所有功能合并后再建独立 Release PR。
- Exclusions: `956be92` 是旧预览主线建立提交，不单独发 PR；`f774532` 的强制恢复重绘方案已被官方 LakeUI 浅层布局替代，不原样发 PR；`__pycache__` 不提交；生成 EXE/DLL 不进入功能 PR。HDR 后端拒绝代码和位深文档需等 `DetectHdrMode` 漏判修复及测试后才能进入 PR6。
- Recommended order: PR1 可独立；PR2 -> PR3 -> PR6 -> PR7 为后端依赖链；PR4 在 PR3 后提交，PR5 在 PR4 后提交以减少 `PluginPanel.vb` 冲突；Release PR 最后。
- Git status: `main...origin/main [ahead 5, behind 9]`，工作树仍不干净，未创建/切换分支、未提交、未推送。

### 2026-08-23 00:40

- 用户反馈实际安装的 `videoenhancer.exe` 只有约 162 KB，并要求暂停 PR、先在主线测试。
- 根因：误用了 `cli\bin\Release\net10.0\win-x64\videoenhancer.exe`，这是普通构建启动器，不是自包含发布文件。
- 处理：未清理或覆盖主线未提交修改；使用主线当前插件 DLL `videoenhancer.3fui.dll` 作为载荷，执行自包含单文件发布，保留 2 个既有 CA1416 警告。
- 已复制到 `C:\Program portable\3FUI\plugin`：`videoenhancer.3fui.dll`、`videoenhancer.exe`、`videoenhancer-layout.json`。
- 校验：目标 EXE `16681479` 字节，SHA-256 `693F2C6B531E8685C8EDBC0FF7067A10A912692A1F29D26E345D4671EE5E60E1`；目标 DLL `4550656` 字节，SHA-256 `972EC2F0E98586886F4B4801EF516E7F7ECEE7A6236127F7AFC0BAAD72813312`；三项均与主线源文件一致。
- Git：canonical source 仍为 `main...origin/main [ahead 5, behind 9]`，主线工作树仍有用户未提交修改和 `cli\tests`/`__pycache__` 未跟踪文件；未创建新 PR、未提交、未执行清理或回滚。

### 2026-08-23 00:50

- 复核用户另一台机器的 TensorRT“模型库缺失”反馈：主线 `ListModels` 已按后端筛选，但主线 `RunCheck()` 仍固定调用 `DiscoverModelFolders()`，因此没有包含 PR #10 的环境检查修复。
- 本机用已复制的主线 EXE 执行 `--check -backend tensorrt`：模型库能识别 22 个 NCNN 模型，随后因 AMD 主机无法加载现有 TensorRT Engine 报设备不兼容；这证明 EXE 已是完整发布版，但不能证明 TensorRT 检查修复已进入主线。
- 用户日志中的模型路径 `C:\Program portable\3FUI\3FUI\Plugin\models` 比标准插件路径多一层 `3FUI`，需在另一台机器检查 `videoenhancer.ini` 的 `core-path` 或实际插件目录。
- 当前未修改源码、未创建 PR、未提交；下一步应先选择“主线集成 PR #10”或“仅用 PR #10 构建物做本地测试”。

### 2026-08-23 01:05

- 用户要求将 PR #10 集成到主线，已直接修改 canonical source，未创建 PR。
- 集成内容：`RunCheck()` 接收实际超分后端；按 NCNN/CUDA/TensorRT/ONNX/FlashVSR 识别模型；插件环境检查透传 `_config.Backend`。移除当前主线不存在的 `DiscoverBasicVsrPlusPlusModels()` 分支。
- 构建：插件 DLL 成功；CLI 自包含发布成功，保留 2 个既有 CA1416 警告。
- 安装：已复制到 `C:\Program portable\3FUI\plugin`。目标 EXE `16681657` 字节，SHA-256 `CDB0463554756206FFAED0DFC5F04389FAEDB48C4984C8B96ACA8BBE9D1DE7F2`；目标 DLL `4550656` 字节，SHA-256 `DD23523A5C8291DA3F8D83B18DA8A28224530ECA06DD0FD08E988B9F4DFC5529`。
- 验证：目标 EXE `--check -backend tensorrt` 识别 `44 个可用模型（tensorrt）`；当前 AMD 机器随后因没有 CUDA GPU 报 Engine 不兼容，这是设备限制，不是模型库缺失。
- Git：主线仍为 `main...origin/main [ahead 5, behind 9]`，工作树不干净；未提交、未推送。现有 `PluginPanel.vb` 等大范围差异和未跟踪测试/缓存均保留，建议审查后再提交。

### 2026-08-23 01:15

- 复核 PR #10：修复逻辑本身有效，但其分支基线包含当前主线没有的 `BasicVSR++` 与 `core-path` 代码，不能作为当前主线的无冲突补丁直接合并。
- 当前主线已采用兼容性集成版本：保留现有五种后端，移除不存在的 `DiscoverBasicVsrPlusPlusModels()` 依赖；本机 TensorRT 检查已识别 44 个模型。
- 结论：PR #10 原分支需要按当前主线重新整理后再合并；当前已安装版本可继续在 3060 实机测试。未创建新 PR、未提交。

### 2026-08-23 01:40 - Codex

- 目标：完成项目目录整理和 HandShake 收尾。
- 变更：根目录确认为唯一活动主线；旧源码、旧构建物和 PR worktree 已归档到 `archive/2026-08-23-before-root-mainline`；空 `preview` 目录不再承载源码；删除同目录布局下冗余的 `videoenhancer.ini`。
- 记录：更新根目录 `AGENTS.md`、`docs/codex/INDEX.md`、`docs/codex/STATUS.md`、`version/工作进度.md` 和 `.gitignore`。
- 验证：`git pull` 已为 up to date；`git diff --cached --check` 通过；整理提交为 `bc5f5a5 chore: 整理主线目录并归档旧文件`。生成的 EXE、DLL 和 `__pycache__` 未纳入提交。
- Git：`main` 相对 `origin/main` 为 ahead 9；工作树 clean，整理提交已完成，远端尚未推送。

### 2026-08-23 12:30 - Codex

- 用户提出是否应将模型下载源迁移到自有 ModelScope 仓库；本轮按咨询性质仅做只读评估，未修改业务源码、未创建 PR、未提交或推送。
- 检查结果：`cli/Program.cs` 中 tree API 和 resolve 根地址硬编码为 `ARXChem/VideoEnhancer-Models`；下载器已经保留远端相对路径到 `models`、`python`、`bin` 的映射，并校验文件大小与 SHA-256，因此迁移可优先采用同路径镜像 + 可配置 endpoint 的小改动方案。
- 风险与决策：原仓库及其中第三方模型的再分发许可证尚未核实；在获得用户确认和许可证依据前，不实施整库搬运或公开镜像。建议先确认自有仓库命名、是否保留旧源 fallback，以及是否需要一次性迁移脚本。
- Git：启动时 `git pull` 因本地 `main` 与远端分叉产生 3 个文件冲突，已执行 `git merge --abort` 撤销本次同步；当前 `main...origin/main [ahead 9, behind 4]`，工作树 clean。

### 2026-08-23 12:45 - Codex

- 用户决策：项目作为独立项目维护，不再追随原作者的 QQ 群 Release；GitHub 作为源码/Release 主站，ModelScope 作为模型和大文件源，仅不定期同步上游功能。
- 处理边界：可以实现独立发行、可配置 ModelScope、迁移清单、文件校验和上游同步流程；不上传或协助公开再分发明知无授权的模型文件，许可证未知时优先支持私有镜像或占位配置。
- 本轮未修改业务源码、未访问 QQ 资源、未上传模型或创建远端仓库。下一步需要用户提供独立 GitHub 仓库与 ModelScope 仓库 ID，或明确先只做本地代码骨架。

### 2026-08-23 13:10 - Codex

- 对 `ARXChem/VideoEnhancer-Models` 做了在线只读审计：API 返回 118 项，排除目录、`.gitkeep`、README 和 `.gitattributes` 后 101 个 blob；按当前 CLI `allowedRoots` 实际可下载 97 个文件，总体约 13.56 GiB。
- 授权证据：根 README 只声明数据集卡片 `Apache License 2.0`；`FlashVSR/README.md` 声明 `apache-2.0` 并附上游项目链接。仓库没有逐文件许可证、NOTICE 或来源映射。
- 待核实/不可直接视为已授权：全部 97 个可下载文件，尤其 `Backend/*.7z`、`Param-Bin/NCNN-20260821.7z`、`RIFE/RIFE.7z`、15 个 `TensorRT-Default/*.engine`、28 个 `ONNX/*.onnx`、42 个 `PTH/*.pth`、`Bin/*.7z` 和 5 个 `FlashVSR/*` 权重文件。`BasicVSR++/*.pth` 与根目录 `PotPlayer.7z` 虽不在当前下载筛选中，也没有逐文件授权证据；PotPlayer 属于商业软件，风险最高。
- 交叉核对到的上游代码许可证线索：FlashVSR Apache-2.0、Real-ESRGAN BSD-3-Clause、RIFE MIT、OpenMMLab/BasicVSR++ Apache-2.0、FFmpeg 至少存在 LGPL/GPL 构建差异；这些只证明代码/项目线索，不足以证明对应权重、转换产物或打包二进制可按同一许可证再分发。
- Git：仍为 `main...origin/main [ahead 9, behind 4]`；仅 HandShake 文档有未提交修改，未上传或复制任何模型文件。

### 2026-08-23 13:20 - Codex

- 用户明确确认：`PotPlayer.7z` 不纳入自有发行线；希望继续推进模型分发。
- 决策：后续技术实现排除 `PotPlayer.7z`，并将模型源、清单、校验和 Release 流程继续解耦；对没有授权证据的第三方资源只支持私有镜像、手动导入或占位配置，不代为公开上传或发行。
- 本轮未修改业务源码、未上传模型；Git 仍为 `main...origin/main [ahead 9, behind 4]`，工作树只有 HandShake 文档修改。

### 2026-08-23 13:25 - Codex

- 用户要求先上传到私有 ModelScope 库。当前等待私有数据集 ID；不在聊天中接收或保存 Token，上传时使用用户本机环境变量。
- 预估范围：排除 `PotPlayer.7z` 后，待处理资源约 13.56 GiB；仍需用户确认是否上传其余 97 个可下载文件，或先分批上传模型权重。
- 本轮未上传文件、未修改业务源码；Git 状态未变化。

### 2026-08-23 13:30 - Codex

- 私有镜像目标 `AerithDream/VideoEnhancer-Models` 已创建并保持 private；ModelScope CLI 登录身份为 `AerithDream`。
- 下载：从 `ARXChem/VideoEnhancer-Models` 下载到本机 `D:\modelscope-mirror\VideoEnhancer-Models`，共 108 个文件/约 13.5 GiB，明确排除 `PotPlayer.7z`；本地 D 盘余量约 49 GiB。
- 上传：使用 `modelscope upload` 目录批处理、断点缓存和 4 workers；第一次使用 `path_in_repo='.'` 时服务端返回 `invalid commit action` 且未提交，随后省略该参数重试成功。
- 结果：上传报告 108/108 committed，0 failed；其中 95 个 LFS blob 服务端复用、13 个普通文件提交。通过 ModelScope SDK 查询目标与上游元数据：目标 100 个非 `.gitkeep` blob，上游 101 个，唯一缺失为 `PotPlayer.7z`；路径、大小、SHA-256 全部一致。
- 验证：私有目标 `README.md` 可成功下载；`modelscope info` 显示 `AerithDream/VideoEnhancer-Models` 为 private。
- Git：工作树仍只有 `docs/codex/STATUS.md` 和 `version/工作进度.md` 修改，`main...origin/main [ahead 9, behind 4]`；未提交源码或生成物。

### 2026-08-23 18:59 - Codex

- 目标：继续 CLI 本地化，将模型下载切换到自有 ModelScope，并复核上游是否值得合并。
- 上游：执行 `git fetch origin --prune`；`origin/main` 最新仍为 `375a3f5`（2026-08-23 08:39，合并 PR #9），本地仍为 `ahead 9, behind 4`。PR #10 的后端感知检查已在本地主线等价实现；PR #9 混有删除 `core-path`、移除 UI 和格式噪音，只把结构化 `ffprobe` HDR 探测保留为以后选择性移植候选。本轮没有合并上游提交。
- 源码：`cli/Program.cs` 默认仓库改为 `AerithDream/VideoEnhancer-Models`，支持 `VIDEOENHANCER_MODELSCOPE_DATASET`、`VIDEOENHANCER_MODELSCOPE_TOKEN` 和 `MODELSCOPE_API_TOKEN`；私库清单请求增加认证，私库下载使用进程内 HTTP，避免令牌出现在 aria2 命令行；增加 `AUTH_REQUIRED` 错误码。`PluginPanel.vb` 增加列表、单项和批量下载的认证提示；`cli/README.md` 同步说明。
- 认证调研：ModelScope `modelscope login` 的 API 会话位于 Python pickle Cookie 的 `m_session_id`，`credentials/git_token` 不能访问私有 tree/resolve API；CLI 不自动反序列化不稳定的 Python pickle。曾临时写入用户级令牌用于验证，用户随后把测试集转为公开，已删除该用户环境变量；没有输出令牌或写入仓库。
- 验证：CLI Release 构建 0 错误、2 个既有 CA1416 警告；插件用 3FUI 6.1.39 官方程序集构建成功；私库令牌模式清单返回 82 项，README 和 740,318 字节 ONNX 下载及 SHA-256 校验通过；用户将测试集公开后，无令牌模式清单仍返回 82 项，aria2 下载同一 ONNX 成功并通过校验。测试下载文件均已清理。
- 部署：执行 `cli/build.ps1` 生成单文件 CLI；将 `videoenhancer.exe`（17,399,816 字节）、`videoenhancer.3fui.dll`（5,276,160 字节）和布局复制到 `C:\Program portable\3FUI\plugin`，三项 SHA-256 与工作区构建物一致；部署后的 CLI 无令牌读取 82 项成功。
- 用户决策：ModelScope 测试集目前公开，Codex 不再修改其可见性；插件功能测试结束后由用户自行决定是否恢复私有。
- Git：`main...origin/main [ahead 9, behind 4]`，修改文件为 `VideoEnhancerPlugin/PluginPanel.vb`、`cli/Program.cs`、`cli/README.md`、`docs/codex/STATUS.md`、`version/工作进度.md`；工作树不干净，未提交、未推送。生成的 EXE/DLL 未纳入 Git。

### 2026-08-23 19:13 - Codex

- 用户反馈 3FUI 启动反复显示“环境检测未通过：[环境检查] videoenhancer v1.9.6-preview.2”。
- 根因一：`cli/Program.cs` 的 `ToolVersion` 仍是 preview.2，而 `cli/VideoEnhancer.csproj` 已为 1.10.1；已统一为正式版本 1.10.1。
- 根因二：`RunCheck(verbose: true)` 会无条件验证目录内全部 TensorRT Engine，即使当前后端不是 TensorRT；当前机器无 CUDA，因此无关 Engine 导致启动检测失败。现仅在实际后端为 `tensorrt` 时验证 Engine，显式 `--validate-engines` 功能不变。
- 根因三：插件环境检查只取 stdout 第一条 `[环境检查]`，因此失败提示只显示版本行；现并行读取 stdout/stderr，优先显示 `[缺失]`，否则显示最后一条环境总结，避免隐藏真实原因和 stderr 管道阻塞。
- 当前用户配置原为 `Backend=tensorrt`、PTH 模型；已在 3FUI 未运行时改为 `Backend=ncnn` 和同名 `Param-Bin/AnimeJaNai-V3-2x-HD-Sharp1-Compact-430K`，其他设置保持不变。该配置位于 `%LocalAppData%\FFmpegFreeUI\videoenhancer.plugin.json`，不在 Git 中。
- 验证：CLI Release 构建成功（0 错误、2 个既有 CA1416 警告）；插件用 3FUI 6.1.39 程序集构建成功；单文件 EXE 报告 v1.10.1。安装目录执行 `--check -backend ncnn` 识别 22 个模型并全部通过；显式 TensorRT 检查仍因无 CUDA 正确退出 1 并显示真实缺失项。
- 部署：重新生成并复制 `videoenhancer.exe`（17,399,914 字节）、`videoenhancer.3fui.dll`（5,276,672 字节）和布局到 `C:\Program portable\3FUI\plugin`，三项哈希与工作区一致。
- 版本：`version/版本迭代记录.md` 将 1.10.1 设为正式独立维护主线，1.9.6-preview.2 移入历史；尚未创建 GitHub Release。
- Git：`main...origin/main [ahead 9, behind 4]`，工作树不干净，未提交、未推送；生成的 EXE/DLL 未纳入 Git。

### 2026-08-23 20:08 - Codex

- 目标：审查作者最新测试版 `D:\read\videoenhancer.exe`，选择性合并适合独立主线的新增能力，并部署测试。
- 样本：作者 EXE 为 v1.4.2，17,441,903 字节，SHA-256 `3FE47EE098C680AC2F31FA9EE771DF07FE4D1437511F95E62A7A248D65CFA737`，未签名；已用临时工具解包和反编译。`origin/main` 仍停在 `375a3f5`，没有该测试版源码。
- 取舍：移植 `models\Frame-Interpolation`、CUDA `.pth/.pt/.pkl` 递归发现、TensorRT `.engine`、GIMM-VFI/GMFSS 自动切 CUDA、BasicVSR++ `config.py + chkpts.pth` 1x 优化目录、ModelScope 新分类和逐包安装标记；保留旧 `models\RIFE` 双读兼容。拒绝作者测试版中的 `ARXChem` 硬编码、删除 `core-path`、旧 `RunCheck` 和 1.4.2 版本号。
- 源码：修改 `cli/Program.cs`、`cli/README.md`、`VideoEnhancerPlugin/PluginConfig.vb`、`VideoEnhancerPlugin/PluginPanel.vb`。补帧目录从超分自动发现和显式路径解析中排除；TensorRT 新目录只列 Engine，旧 RIFE PTH 仅保留兼容入口；BasicVSR++ 官方 PTH 为 4x、优化目录为 1x。
- ModelScope：从作者仓库增量下载并校验 3 个文件，总计 774,800,482 字节；上传到 `AerithDream/VideoEnhancer-Models` 时服务端复用 3 个 LFS blob，0 失败。目标仍为 public；3 个新路径的大小和 SHA-256 与上游一致。旧 `RIFE/RIFE.7z` 已补交，resolve 返回 HTTP 200；tree API 暂有缓存延迟。
- 验证：CLI Release 构建成功（0 错误、2 个既有 CA1416 警告）；插件用 3FUI 6.1.39 程序集构建成功。隔离目录 6 项发现断言全部通过，覆盖新 RIFE、GIMM-VFI、GMFSS、旧 RIFE、补帧目录排除和 BasicVSR++ 两种格式；文本确认官方 4x、优化目录 1x。真实从自有镜像下载 `Frame-Interpolation/RIFE.7z` 成功，解压到新目录、生成 1 个 `.downloads` 标记并发现 5 个实际 NCNN 模型。
- 部署：执行 `cli/build.ps1`，复制到 `C:\Program portable\3FUI\plugin`。EXE 17,402,650 字节、SHA-256 `37116A21FEB65A36A40EE4BD9DD5F9727846163EACC64799A43BA8320A47686B`；DLL 5,278,208 字节、SHA-256 `BAB95BB500DC06C4D0368CAC55492920584CA3E916603EE06A62A3F1AF1D27E2`；布局哈希也与工作区一致。安装版 `-h` 显示 v1.10.1，公开镜像清单可见 3 个新版补帧包。
- 限制：当前机器没有 NVIDIA 推理环境，GIMM-VFI、GMFSS 和 TensorRT Engine 只完成发现/参数面验证，未做真实 GPU 推理。终端安全策略拒绝递归删除隔离测试目录，残留位于 `%TEMP%\videoenhancer-model-layout-test-8db6c226beb44eddac5584126473562f`，不在仓库或 3FUI 安装目录。
- Git：启动 `git pull` 因 `PluginPanel.vb`、`Program.cs`、`README.md` 的本地修改会被覆盖而中止，没有改写工作树。当前仍为 `main...origin/main [ahead 9, behind 4]`；工作树不干净，未提交、未推送。建议完成 3FUI 界面和 NVIDIA 实机验证后提交源码与 HandShake 记录，排除生成的 EXE/DLL。

### 2026-08-23 20:58 - Codex

- 目标与版本：用户确认采用独立 SemVer，并单独记录上游基线；正式版本从 1.10.1 升为 1.11.0，上游基线为 1.4.2，不使用上游版本决定更新。
- 启动同步：重新读取 `AGENTS.md`、`docs/codex/INDEX.md`、`docs/codex/STATUS.md` 和 HandShake skill；`git pull` 因本地重叠修改中止且未改写工作树。远端新增 `cbfda2f` 的 HDR/后端检查在本地已有等价实现，未整体合并。
- 插件：新增 `PluginVersion.vb` 和 `PluginUpdater.vb`；`PluginConfig` 增加 `AutoCheckUpdates`。插件加载后后台读取 ModelScope `stable.json`，底部提供“检查更新”按钮；发现更高 SemVer 后由用户确认，下载时校验大小与 SHA-256，随后复制 CLI 为临时更新器、退出并重启 3FUI；下次启动消费更新结果文件并显示成功或错误。
- CLI：新增 `--apply-update` 内部模式及更新包、目标目录、等待 PID、重启程序和结果文件参数。ZIP 只允许 `package.json` 与三项运行文件；包内清单逐文件校验大小和 SHA-256。宿主退出后先完整备份再替换，任一失败恢复原文件；更新模式在 `core-path` 和后端检查前执行。
- 发布端：新增 `release/build-modelscope-release.ps1`，校验版本一致后构建插件和自包含 CLI，生成含逐文件清单的 ZIP 与 `stable.json`；新增 `release/test-updater.ps1`。根 README、CLI/插件 README、csproj、deploy 默认版本和版本记录均更新为独立 1.11.0。
- ModelScope：以登录身份 `AerithDream` 创建公开数据集 `AerithDream/VideoEnhancer-Releases`，上传 README、`stable.json` 和 `releases/1.11.0/VideoEnhancer-1.11.0-win-x64.zip`，3/3 提交成功、0 失败；未修改 `AerithDream/VideoEnhancer-Models` 的 public 状态。ModelScope 未显式指定许可证时自动标记 Apache-2.0，需在后续许可证整理中复核。
- 验证：插件编译成功；CLI Release/单文件发布成功，仅保留 2 个既有 CA1416 Windows 平台警告。隔离更新测试通过正常替换、`../` 路径拒绝、包内篡改拒绝，以及 EXE/DLL 已替换后布局文件写入失败的真实回滚；编译后的插件下载客户端也正确拒绝故意错误的外层 SHA-256。远端 ZIP 为 15,143,282 字节，SHA-256 `2C1562711069D3683806EF19B7DE63B6F1158DF318843BA6E36FD3509F106E22`，与远端清单一致；插件客户端读取远端得到 Current=Remote=1.11.0、HasUpdate=False。
- 部署：确认 3FUI 未运行后，将 EXE 17,449,371 字节、DLL 5,293,568 字节和布局复制到 `C:\Program portable\3FUI\plugin`；三项源/目标 SHA-256 一致，安装版帮助显示 v1.11.0。
- Git：`main...origin/main [ahead 9, behind 5]`；工作树包含本轮与前序未提交源码/文档修改和新增 release 脚本，仍不干净，未提交、未推送。生成的根 EXE/DLL 与 `release/dist` 由 `.gitignore` 排除。建议实机确认更新按钮和重启体验后提交，再单独处理远端分叉。

### 2026-08-23 21:15 - Codex

- 目标：按用户要求发布 1.11.1，并让模型下载页也能触发插件更新，作为底部检查更新按钮的兜底。
- 启动：读取 `AGENTS.md`、HandShake skill、`docs/codex/INDEX.md` 与 `STATUS.md`；`git pull` 因 README、插件、CLI 和发布脚本的既有未提交修改会被覆盖而中止，没有改写、stash 或回滚工作树。仍为 `main...origin/main [ahead 9, behind 5]`。
- UI：`PluginPanel.vb` 在官方模型下载页标题栏新增 `_btnDownloadPluginUpdate`“下载插件更新”，与刷新资源并列；按钮复用 `CheckForUpdatesAsync`，检查或模型下载忙碌时正确禁用。插件 ZIP 继续使用独立 Release 数据集，不进入模型 tree API、模型目录或解压逻辑。
- 过渡测试：先保持版本 1.11.0 构建带兜底入口的 DLL，并在 3FUI 未运行时只覆盖安装目录 DLL，保留 1.11.0 CLI；安装 DLL SHA-256 与过渡构建一致。随后才把源码和发布配置升至 1.11.1，以便真实测试升级发现。
- 版本：`PluginVersion.Current`、CLI `ToolVersion`、csproj、deploy、两个 release 脚本、根 README 和版本记录统一为 1.11.1；发布脚本新增 CLI 常量一致性校验。上游基线保持 1.4.2。
- 构建与测试：`release/build-modelscope-release.ps1` 成功构建插件和自包含 CLI，仅有 2 条既有 CA1416 Windows 平台警告；`release/test-updater.ps1` 的正常替换、路径穿越拒绝、包内篡改拒绝和部分替换后文件锁回滚全部通过。
- ModelScope：向公开 `AerithDream/VideoEnhancer-Releases` 上传 `stable.json` 和 `releases/1.11.1/VideoEnhancer-1.11.1-win-x64.zip`，2 个新/变更文件提交成功、0 失败；未使用 `--sync`，1.11.0 历史包保留。1.11.1 ZIP 为 15,143,363 字节，SHA-256 `1ED57927DF9B81315A27BB79097B10576C25E5239914EB7EAF01B6477448AE62`，远端实读一致。
- 升级发现：直接加载已安装的过渡 1.11.0 DLL，读取远端得到 Remote=1.11.1、HasUpdate=True；实际下载到 `%LocalAppData%\FFmpegFreeUI\VideoEnhancer\updates\1.11.1`，长度和 SHA-256 与清单一致。未启动替换，刻意保留给用户在真实模型下载页点击测试。
- 实机入口：已启动可见的 `C:\Program portable\3FUI\FFmpegFreeUI.exe`（启动 PID 3676）。用户需在启动自动提示中先点“否”，再从模型下载页点“下载插件更新”，确认退出、三文件替换、自动重启和结果提示。
- 剩余：源码工作树仍不干净且未提交；实机确认后建议提交，再处理 Git 分叉。

### 2026-08-23 23:59 - ZCode

- Objective: 按用户要求统一 CLI 内部版本号与独立发行版本，并把版本检查迁移到 GitHub Releases（唯一标准），ModelScope 首选下载、GitHub 兜底；下一版延续 1.11 系。
- Orientation: 读取 `AGENTS.md`、HandShake skill、`docs/codex/INDEX.md`、`STATUS.md`；`git pull` 因本地未提交改动被阻止（按记录预期，未改写工作树）。发现工作树版本已是 1.11.2 且 ModelScope 线上 stable.json 也是 1.11.2（2026-08-23 21:27 发布，"优化更新确认窗口"——该轮未记入 STATUS，本条补记），故下一版定为 1.11.3。
- 计划确认: 经计划模式探索（两个只读代理 + 人工核读 PluginUpdater.vb/Program.cs/发布脚本）后用户批准；用户确认 GitHub 托管在现有 fork `maxzrb/VideoEnhancer`，发布脚本要一键自动上传。
- Work completed（提交 `43db6fc` 基线 + `f37986e` + `47d4b8a`）:
  - Step 0: 提交此前未提交的 1.11.x 更新器体系（16 文件）。
  - 版本单一来源: `Program.cs` 删除 `ToolVersion` 字面量，运行时读 csproj `<Version>` 的 InformationalVersion（裁剪发布下保留，`+`后缀剥离，回退 Assembly Version）；新增 `-v/--version`；`deploy.ps1`、`release/build-modelscope-release.ps1`、`release/test-updater.ps1` 的 `$Version` 默认改为正则读取 `PluginVersion.vb`；发布脚本构建后运行 EXE `--version` 做端到端校验。
  - 更新协议: `PluginUpdater.vb` 重写——`FetchLatestManifestAsync` 走 `api.github.com/repos/<repo>/releases/latest`（默认 `maxzrb/VideoEnhancer`，env `VIDEOENHANCER_UPDATE_GITHUB_REPO` 覆盖、`VIDEOENHANCER_UPDATE_GITHUB_TOKEN` 可选），解析 `tag_name`（去 `v` 前缀）、按名找 `stable.json` 资产、沿用原清单校验并交叉校验标签与清单版本一致；404 报"远端尚无稳定版 Release"；`DownloadPackageAsync` 双源（ModelScope `BuildResolveUrl(manifest.Package.Path)` 首选 → GitHub 资产按文件名匹配回退，两源均校验大小+SHA-256，双败报两源错误）；面板文案"从 GitHub 检查更新"。
  - 发布一键化: `release/build-modelscope-release.ps1` 新增 `-PublishGithub`（`gh release create v<V> zip stable.json --repo <repo>`）/ `-PublishModelScope`（`modelscope upload <ds> <dist>`），未加开关时打印手动命令；`Invoke-Native` 包装避免 PS5.1 EAP=Stop 把原生 stderr 当终止错误。
  - 用户 UI 反馈（游戏中口头反馈）: 更新按钮右对齐——底部状态栏与模型页头部交换列位使更新按钮位于最右（`检查更新`/`下载插件更新`），更新弹窗移除"更新内容"段。
  - 版本全面升至 1.11.3；README/插件 README/cli README/modelscope-README 同步协议说明；`.gitignore` 加 `/.zcode/`。
- 补记 1.11.2 轮次: 2026-08-23 21:27 左右有一轮未记录的发布——版本升 1.11.2、精简更新确认窗口文案、构建并上传 ModelScope（线上 stable.json publishedAt=21:27:41），本机曾构建过渡 DLL。该轮无 STATUS/工作进度条目，本轮已把 1.11.2 写入版本记录历史段。
- Commands/verification:
  - `release/build-modelscope-release.ps1`: 三次全量构建均通过（0 错误、2 个既有 CA1416 警告），版本一致性 + `--version` 端到端校验通过；产物 `releases/1.11.3/VideoEnhancer-1.11.3-win-x64.zip`（15,020,251 字节，SHA-256 `922e24f6…`）。
  - `release/test-updater.ps1`: success/traversal/tamper/rollback 四项 PASS。
  - Git: fork `maxzrb/VideoEnhancer` 推送 `5cafaca→f37986e→47d4b8a`（含一次 amend force-push 清除误入的 build-err.txt）；tag `v1.11.3` 两次重切（首次产物缺 UI 修复、零下载后删除重发）最终指向 `47d4b8a`；`gh release view` 确认双资产。
  - 实机验证（过渡 1.11.2 DLL 装入 `C:\Program portable\3FUI\plugin`）: 3FUI 启动后台检查弹出"发现新版本"（GitHub 检查 ✓，用户查看后反馈 UI 意见）；反射 harness 加载真实 DLL 得 manifest.version=1.11.3、HasUpdate=True、githubFallback URL 正确；真实下载 15,020,251 字节 SHA-256 与清单一致（ModelScope 失效 → GitHub 兜底路径实际命中）。
  - 真实替换: 直接以安装 EXE 自更新触发 IO_SharingViolation → 正确回滚并写 ERROR 结果文件（意外验证回滚）；按真实流程复制到临时 updater 目录执行 → `OK|1.11.3`，安装目录 EXE/DLL/layout 三文件与发布包哈希一致，EXE `--version`=1.11.3。
  - ModelScope: `-PublishModelScope` 上传报告 committed 0 失败；但 resolve/tree/raw API 与 SDK 下载读取侧缓存延迟超 1 小时仍显示 1.11.2（写入侧"already committed"确认已落库）。单文件重传 stable.json 亦报告成功但读取未变。
- Incidents/notes:
  - 会话中 3FUI 曾被最小化、前台为用户游戏进程；CUA 拒绝焦点抢占（正确行为），改用反射 harness + CLI 直跑 apply-update 完成验证，弹窗内"是"的最终肉眼确认留给用户下次启动。
  - Write 工具写出的 .ps1 为无 BOM UTF-8，PS5.1 按 ANSI 解析导致中文脚本语法错误；已按原约定重编码为 UTF-8 BOM 修复。
  - 一次编辑意外把 `sectionStatus.RowStyles.Add` 与 `_lblStatus.AutoSize` 挤到同一行（BC30205），且 Bash 管道掩盖构建失败导致旧 DLL 被误装；已修复并改用退出码判断。
- Remaining:
  - 用户实机确认 1.11.3 更新结果提示、右对齐按钮布局；新弹窗样式要到 1.11.4 才会再次出现。
  - ModelScope 读缓存追平 1.11.3 待观察；追平前老客户端（≤1.11.2）无法发现新版。
  - `main` 相对 `origin/main`（上游 user-Wing）为 ahead 12 / behind 5，独立维护不合并不推送 origin；fork 已同步。
- Git status: 工作树干净（记录文件更新前）；1.11.3 相关三个提交已推送 fork 并打 tag `v1.11.3`；建议本条记录随收尾提交。

### 2026-08-24 00:15 - ZCode

- User decision: 用户询问版本号为何到 1.11.3，决定自 1.0 系重新开始；经确认选择立即切换并发布 1.0.3。
- Explanation recorded: 1.11.x 来源于独立维护开始时继承的本机 1.9.5 基数（1.9.6-preview → 1.10.1 → 1.11.0 独立 SemVer 起点 → 同日 .1/.2/.3 迭代），不代表真实迭代跨度；上游线停在 1.4.2 仅作基线记录。
- Work completed: `PluginVersion.Current`、csproj `<Version>`、根 README 统一改为 1.0.3（ToolVersion 自动跟随）；`release/build-modelscope-release.ps1 -Notes "版本编号重置…"` 构建通过，EXE `--version`=1.0.3，四项更新隔离测试 PASS；`-PublishGithub -PublishModelScope` 一键发布成功。
- Verification: GitHub `releases/latest` API 实测已指向 `v1.0.3`（双资产 stable.json + ZIP）；本机 `C:\Program portable\3FUI\plugin` 三文件已用发布包覆盖，EXE/DLL SHA-256 与 package 一致，`update-result.txt` 写为 `OK|1.0.3`（下次启动显示"已更新到 v1.0.3"）；再次运行 `modelscope upload` 返回 "All files were already committed"（7/7），确认 1.0.3 已写入服务端。
- Known issue持续: ModelScope resolve/tree/raw API 读侧快照仍停在 2026-08-23 21:27 的 1.11.2（>2.5 小时未追平），写入侧一切正常。影响：≤1.11.2 旧客户端在追平前看不到新版本；1.11.x 已装机器对 1.0.3 永远不提示更新（数值比较），需手动升级一次（本机已完成，3060 机待用户手动处理）。1.0.3 起新协议客户端走 GitHub 检查+兜底不受影响。
- Git status: 提交前工作树含版本切换与记录修改；将随本次收尾提交并推送 fork。`main` 相对 `origin/main`（上游）为 ahead 14 / behind 5，独立维护不合并不推送。

### 2026-08-24 00:25 - ZCode

- User finding: 用户发现 ModelScope 上多了一个同名**模型**仓库 `AerithDream/VideoEnhancer-Releases`——此前"读缓存延迟"诊断错误，真实根因如下。
- Root cause: `modelscope upload` 不带 `--repo_type` 时默认按 model 处理；`release/build-modelscope-release.ps1` 的 `-PublishModelScope` 漏写该参数，昨晚起所有发布上传都进了自动创建的同名模型仓库（数据集从未收到 1.11.3/1.0.3，所以 resolve 一直停在 1.11.2）。且上传目录内的本地断点缓存 `.ms_upload_cache` 的 key 不含仓库类型，导致后续重试（即使补了 `--repo_type dataset`）也被误判为"already committed"而跳过。
- Fix: 发布脚本两处 `modelscope upload` 补 `--repo_type dataset`（含手动命令提示行）；删除 `release/dist/modelscope/.ms_upload_cache` 后重新以 dataset 类型上传——7/7 committed（5 个 ZIP 服务端 LFS 复用）。
- Verification: 数据集 resolve 的 `stable.json` 现返回 **1.0.3**，`releases/1.0.3/...zip` 返回 302；"ModelScope 读缓存延迟"问题不存在，已关闭该观察项。
- Remaining (user manual): 误建的同名模型仓库 API 禁止删除（E3001 仅网页控制台可删），需用户在 https://www.modelscope.cn/models/AerithDream/VideoEnhancer-Releases 页面手动删除。
- Git status: 脚本修复与记录随本次提交推送 fork。


### 2026-08-24 00:59 - Codex

- Objective: 按用户要求重新通读大改后的 1.0.3 项目，并继续核对此前未完成的 TensorRT 补帧接入与“两套 Python”问题。
- Orientation/Git: 读取 `AGENTS.md`、HandShake skill/protocol、`docs/codex/INDEX.md`、完整 `STATUS.md`、根 README 与核心源码。前序 `git pull` 进入冲突后已立即 `git merge --abort`，用户提交未丢失；本轮不再合并上游。`HEAD=32a0888`，`main` 与 `fork/main` 一致，相对原作者 `origin/main` 为 ahead 15 / behind 5。
- Work completed: 修复前序编辑造成的 5 个文件整文件换行噪声，按字节仅重放真实语义改动；TensorRT 补帧发现只接受 RIFE `.pth/.pt/.pkl`，不再选择生成的 `.engine`；默认补帧后端在超分为 TensorRT 时跟随 TensorRT；UI、CLI 帮助和 README 同步“RVE 按实际输入尺寸自动构建 Engine”；在线模型清单隐藏旧 `Backend/python.7z`，保留最新日期包。
- Architecture finding: 超分 TensorRT 的外置 `convert_tensorrt.py` 只支持单帧 NCHW 放大模型；补帧 Engine 必须由 RVE `InterpolateRifeTorch` 内部构建。`interp-first` 使用源分辨率；同后端 `upscale-first` 包装器把插帧器初始化为最终放大分辨率，同时保留源尺寸转场检测；跨后端第二阶段通过真实中间视频尺寸初始化。
- Python finding: CLI 实际只运行 `python\python\python.exe`；`python\python\Lib\venv\scripts\nt\python.exe` 是标准库创建 venv 时复制的模板，不是第二套运行环境。真正重复的是本地镜像缓存中的 `Backend/python.7z` 与 `Backend/python_20260823.7z`，合计约 5.27 GB；当前在线 UI 只展示日期包。
- Verification: 插件构建成功；`dotnet build cli/VideoEnhancer.csproj -c Release --no-restore` 成功（0 错误，2 个既有 CA1416）；`python -m unittest cli.tests.test_rve_ordered_backend -v` 6/6 通过；单文件 CLI 发布成功且 `--version=1.0.3`。隔离目录验证 CUDA 列出 GIMM/GMFSS/RIFE 共 5 个权重，TensorRT 只列 3 个 RIFE 权重并排除 GIMM/GMFSS/`.engine`，NCNN 只列模型目录。ModelScope 在线清单 84 项，只显示 `Backend/python_20260823.7z`。
- Blocker: 在线 `Frame-Interpolation` 只有 GIMM-VFI、GMFSS、RIFE 三个压缩包，兼容 TensorRT 的 RIFE PyTorch 权重为 0；本机无可调用的 NVIDIA 环境，因此没有执行真实 Engine 编译。未上传模型、未部署到 3FUI、未创建 1.0.4、未发布。
- Files changed: `cli/Program.cs`、`cli/README.md`、`VideoEnhancerPlugin/PluginConfig.vb`、`VideoEnhancerPlugin/PluginPanel.vb`、`VideoEnhancerPlugin/README.md`、`docs/codex/STATUS.md`、`version/工作进度.md`。根目录 EXE/DLL 为本地构建产物并由 Git 忽略。
- Git status: 功能与文档文件及 HandShake 记录有未提交修改，工作树不干净；建议审核后提交到 fork，再继续模型上传和 GPU 实测。

### 2026-08-24 01:28 - Codex

- Objective: 按用户要求先提交 RIFE TensorRT 接入，再向公开 ModelScope 数据集补充兼容 RVE 的 RIFE 权重。
- Orientation/Git: 读取 `AGENTS.md`、HandShake skill、`docs/codex/INDEX.md` 和完整 `STATUS.md`；`git pull --ff-only` 因独立主线与原作者上游分叉安全中止，没有合并或改写工作树。先将已验证源码和记录提交为 `25dba3b feat: add RIFE TensorRT interpolation support`。
- Model source: 从 `TNTwise/real-video-enhancer-models` 的 GitHub Release `models` 下载 `rife4.6.pkl`、`rife4.7.pkl`、`rife4.25.pkl`、`rife4.26.pkl`、`rife4.26.heavy.pkl` 到本地镜像。5 个文件大小与 GitHub 资产元数据一致；GitHub 仅为较新的 heavy 资产提供服务端 digest，其余 4 个旧资产 digest 为空，本地 SHA-256 均已记录并用于上传后比对。
- ModelScope upload: 逐文件上传至公开数据集 `AerithDream/VideoEnhancer-Models/Frame-Interpolation/RIFE/`，每次显式使用 `--repo_type dataset --no-cache`，没有 `--sync`、没有删除远端文件、没有改变公开状态。5/5 提交成功。
- Hashes: `rife4.6.pkl` `008646e761f0e67cb77f0c6c44cfe3c3e5a05d9d9465311b9681ca650ce030db`；`rife4.7.pkl` `fcf3492b10f17fb035156ea4177ed87b1f517eae54fe4500e878f5d186043d5e`；`rife4.25.pkl` `6615790efd627772917205db291f51cd392528a157ecbb2ecaeec3bff8eb6de2`；`rife4.26.pkl` `45c7f74156704769dc9f85cfcaf8552e1e926f9399dcfa3a553dee88fac6f53f`；`rife4.26.heavy.pkl` `4cc518e172156ad6207b9c7a43364f518832d83a4325d484240493a9e2980537`。ModelScope tree API 的 5 个远端路径、大小和 SHA-256 与本地逐项一致。
- Pagination finding/fix: 上传后 CLI 只返回 85 项，检查确认远端 Git HEAD 有 116 个文件且本轮 5 个提交只有新增。真实根因是 ModelScope tree API 返回 `PageSize=100, TotalCount=127`，旧 CLI 只读取第一页。`cli/Program.cs` 现每页请求 500 条，并按 `TotalCount`/实际返回数继续翻页；完整可下载清单为 105 项，不再漏掉 Param-Bin、PTH、旧 RIFE 和 TensorRT-Default。
- Verification: CLI Release 构建成功（0 错误、2 个既有 CA1416）；顺序后端单元测试 6/6；单文件发布版本仍为 1.0.3；在线清单为 105 项且含 5 个 RIFE `.pkl`。隔离 CLI 从 ModelScope 真实下载 `rife4.6.pkl`（21,273,159 字节）并通过 SHA-256，TensorRT 补帧发现结果为 `RIFE/rife4.25`、`RIFE/rife4.26.heavy`、`RIFE/rife4.26`、`RIFE/rife4.6`、`RIFE/rife4.7`。
- Limitations/local notes: 本机无 NVIDIA 环境，未执行真实 Engine 编译。终端策略拒绝递归删除两个已验证位于 `%TEMP%` 的隔离目录，残留为 `videoenhancer-rife-download-test-0070ee4c40ad420e8ca182335695a312` 和 `videoenhancer-modelscope-history-40b44d10fcb04d409d1641a6d487d493`；二者不在仓库、模型镜像或 3FUI 安装目录。
- Version/release: 版本保持 1.0.3；未部署到 3FUI、未创建 GitHub Release、未修改 `version/版本迭代记录.md`。
- Git status: 分页源码和本次收尾记录待第二个提交；完成后预计工作树 clean。独立主线不合并、不推送原作者 `origin`，推送 `fork` 仍待用户另行要求。

### 2026-08-24 12:27 - Codex

- Objective: 按用户要求把当前分页修复和 RIFE 权重支持版本替换到实际 3FUI 安装目录供测试。
- Precondition: 检查确认 `FFmpegFreeUI`/`3FUI` 均未运行，未强制结束进程。
- Deployment: 将工作区 `videoenhancer.exe`（17,453,200 字节）、`videoenhancer.3fui.dll`（5,300,224 字节）和 `videoenhancer-layout.json`（3,434 字节）复制到 `C:\Program portable\3FUI\plugin`。
- Verification: 三项安装文件均与工作区源文件 SHA-256 一致；EXE `FC6EA682A30023CA69844ADCC326982651D5F42E1A15208F67976C2516D535B7`，DLL `E78952503174C155663DB05C1216169FC20DB77DB719F320EEB08ADB327A1151`，layout `7AF4F4F276CBB893B906F4ACFE38D20283CECFF31CEEBC2C594E013B12BB1212`。
- User next step: 启动 3FUI，刷新模型列表并确认总数、RIFE TensorRT 权重显示和下载交互；当前机器没有 NVIDIA 环境，不能在本机验证真实 Engine 构建。
- Git status: 部署只影响 Git 忽略的本地安装目录；仓库源码状态仍以 `git status` 为准，推送 fork 尚未执行。

### 2026-08-24 12:39 - Codex

- User report: 实机截图显示运动补帧开关不可点击，TensorRT 补帧模型下拉为空。
- Diagnosis: 当前配置选择 `Backend=basicvsrpp`（BasicVSR++ 视频时序超分）；源码会主动禁用补帧开关，并在 CLI 层拒绝 BasicVSR++ 与 RIFE 同时运行。这不是控件失效，测试 RIFE TensorRT 需切换“推理后端”为 `TensorRT (NVIDIA)`、再选择兼容超分模型。
- Local fix: 使用已安装的 `videoenhancer.exe --download-model` 从公开 ModelScope 下载 5 个 RIFE `.pkl` 到 `C:\Program portable\3FUI\plugin\models\Frame-Interpolation\RIFE`；逐文件 SHA-256 与镜像一致。清理用户配置中错误的 `InterpModel: "[]"`，改为空字符串，避免下拉框保留伪选择。
- Verification: 安装版 CLI `--list-interp-models --json -interp-backend tensorrt` 返回 `RIFE/rife4.25`、`RIFE/rife4.26.heavy`、`RIFE/rife4.26`、`RIFE/rife4.6`、`RIFE/rife4.7`；本地 RIFE 权重数量为 5。未修改源码或版本号。
- User next step: 关闭/刷新模型页后把超分后端切到 TensorRT，再打开运动补帧并选择 RIFE 权重；BasicVSR++ 仍不能与补帧组合。真实 Engine 构建仍需 NVIDIA 环境。
- Git status: 本次只修改 Git 忽略的本地模型目录和用户配置，仓库源码状态未改变。

### 2026-08-24 12:52 - Codex

- User report: NCNN 补帧下拉框同时出现 `RIFE/rife-v4.*` 与 `rife-v4.*` 两组同名模型。
- Root cause: 本地同时存在新目录 `models\Frame-Interpolation\RIFE\rife-v4.*` 和旧兼容目录 `models\RIFE\rife-v4.*`；CLI 的 NCNN 发现逻辑递归扫描两个根目录，导致同一套 `.param/.bin` 重复显示。
- Fix: `cli/Program.cs` 的 NCNN 发现改为按目录名去重，优先新 `Frame-Interpolation` 路径；只有新路径没有对应名称时才显示旧 `models\RIFE` 兼容模型。CUDA/TensorRT 权重筛选不变。
- Verification: CLI 构建成功（0 错误、2 个既有 CA1416）；顺序后端测试 6/6；安装版 EXE 替换成功，源/目标长度均 17,453,523 字节，SHA-256 `A66170AABD9ABD0840FC43498BE351B29B41832E3A9774926F9C2B8E765EEEB1`；安装版 `--list-interp-models --json -interp-backend ncnn` 返回 5 项，TensorRT 返回 5 项。
- User next step: 在 3FUI 模型下拉框刷新；如仍保留旧列表则重启 3FUI。BasicVSR++ 仍不能和补帧组合。
- Git status: 源码和记录待提交；本地模型与安装目录属于 Git 忽略/外部路径。

### 2026-08-24 12:57 - Codex

- Objective: 处理用户指出的远端重复 RIFE 资源，避免新客户端在模型下载页显示两个同名归档。
- Finding: ModelScope 实际清单同时包含 `Frame-Interpolation/RIFE.7z`（50,802,746 bytes）和 `RIFE/RIFE.7z`（50,299,097 bytes）；两者内部均为同一套五个 NCNN RIFE 模型目录，只是压缩包重新打包，后者是旧兼容路径。五个 `Frame-Interpolation/RIFE/*.pkl` 权重不是重复资源，继续保留。
- Fix: `cli/Program.cs` 在 `FetchRemoteModels()` 中仅过滤 `RIFE/RIFE.7z` 的当前列表项，不删除或修改 ModelScope 远端文件；旧客户端仍可访问旧路径，新客户端显示新版归档、5 个权重和其他补帧资源。
- Verification: `dotnet build cli/VideoEnhancer.csproj -c Release --no-restore` 成功（0 错误、2 个既有 CA1416）；单文件发布成功，版本 `1.0.3`；发布版及安装版 `--list-download-models --json` 均返回 104 项，RIFE 区域无 `RIFE/RIFE.7z`、保留 `Frame-Interpolation/RIFE.7z` 和 5 个 `.pkl`；安装版 EXE 与发布版 SHA-256 均为 `78C1A4DE4D1EA84085C580B0612A3CE9E58B68D71C49DD894E9691E1A7A6B8F9`。
- Changed files: `cli/Program.cs`、`docs/codex/STATUS.md`、`version/工作进度.md`；版本未变化，未修改 `version/版本迭代记录.md`。
- Git status: 上述源码和记录待提交；本地模型及 `C:\Program portable\3FUI\plugin` 安装目录不在仓库跟踪范围内。独立主线仍不合并、不推送原作者 `origin`。

### 2026-08-24 13:06 - Codex

- User report: 即使选择 `TensorRT (NVIDIA)` 超分、`NCNN (Vulkan)` 补帧和 `RIFE/rife-v4.25`，运动补帧开关仍不可点击。
- Root cause: 页面构造时若之前的 `Backend` 是 `basicvsrpp`，会把 `_switchInterp.Enabled` 设为 `False`；`OnBackendSelected` 切换到其他后端后只刷新模型和高级控件，没有重新赋值开关状态，因此保留灰色状态。用户配置实测为 `Enabled=true`、`Backend=tensorrt`、`InterpBackend=ncnn`，与该路径一致。
- Fix: `VideoEnhancerPlugin/PluginPanel.vb` 新增 `UpdateInterpSwitchState()`，在后端切换后同步 `_switchInterp.Enabled`；只有 BasicVSR++ 继续禁用组合补帧，NCNN/CUDA/TensorRT/ONNX/FlashVSR 均可操作。
- Verification: `VideoEnhancerPlugin/build.ps1 -HostBin C:\Users\maxzr\AppData\Local\Temp\FFmpegFreeUI.6.1.39.extracted -SkipInstall` 成功；生成插件 DLL SHA-256 `680697451D037710702E1CE5CD885C170DCC1B592032E614C4C01109D54D222E`，已复制到 `C:\Program portable\3FUI\plugin\videoenhancer.3fui.dll` 并逐字节哈希一致。未修改 CLI 或版本号。
- User next step: 重启 3FUI 后重新从 BasicVSR++ 切换到 TensorRT，确认运动补帧开关变为可点击；若仍灰色，再检查是否宿主加载了其他插件目录的旧 DLL。
- Git status: 修复已提交为 `1e2c054 fix: re-enable interpolation after backend switch`；工作树干净，安装目录属于仓库外部路径。

### 2026-08-24 13:19 - Codex

- Objective: 按用户“不兼容旧版、旧版应升级”的决定，核对 ModelScope 远端哪些资源可以清理。
- Remote tree: 原始 API 返回 127 个条目（含目录和 `.gitkeep`）。当前可下载清单过滤后为 104 项；原始树仍存在 `Backend/python.7z`、`Backend/python_20260823.7z`、`RIFE/RIFE.7z`、`Frame-Interpolation/RIFE.7z`。
- Safe cleanup candidates: `Backend/python.7z` 与 `Backend/python_20260823.7z` 内容重复，保留日期版、删除无日期旧包可释放 2,639,607,910 bytes（约 2.46 GiB）；`RIFE/RIFE.7z` 与新版 `Frame-Interpolation/RIFE.7z` 均含同一套五个 NCNN 模型，保留新版、删除旧路径可释放 50,299,097 bytes（约 48 MiB）。对应根目录 `.gitkeep` 也可一并清理，但不影响容量。
- Optional aggressive cleanup: `TensorRT-Default/*.engine` 共 15 个、约 188.5 MiB，当前 CLI 仍支持直接发现和下载，删除后会强制用户使用 PTH 自动构建设备专用 Engine；这不是旧版兼容文件，除非决定完全取消预置 Engine，否则建议保留。`Param-Bin/NCNN-20260821.7z`、PTH、ONNX、FlashVSR、BasicVSR++ 和五个 RIFE `.pkl` 都仍对应当前功能，不建议按“旧版兼容”理由删除。
- Action: 本轮只做远端只读核对，没有执行 ModelScope 删除；等待用户确认清理范围。

### 2026-08-24 13:36 - Codex

- Objective: 回答设备专用 TensorRT Engine 是否支持复用、参数变化隔离和原 Engine 不可用时的自动重建。
- Super-resolution path: CLI `EnsureTensorRtEngine()` 使用 `models\TensorRT-Cache`；缓存名包含源模型文件名、源模型 SHA-256 前 12 位、GPU 名称、TensorRT 版本、输入宽高和 `tile-size`，使用同名 Windows Mutex 串行构建；命中后调用 `validate_tensorrt_engines.py` 反序列化/创建 execution context，失败会删除缓存并重新构建；源 PTH 变化、GPU/输入尺寸/分块键变化可产生新缓存。
- Gaps found: `RunTensorRtConverter()` 没有传 `--width`、`--height`、`--output-scale` 或 `--tile-size`，所以转换器实际仍按默认静态 `1920x1080` 构建，外层文件名的尺寸/分块信息与二进制 profile 可能不一致；外层键没有 Torch-TensorRT 版本；验证器只验证反序列化和最优 profile，不验证本次视频尺寸是否在 profile 内。有效但 profile 不匹配的 Engine 可能在实际帧推理时才失败，当前不会回退重建。
- Prebuilt engine gap: 用户直接选择 `TensorRT-Default/*.engine` 时，若能反序列化即直接使用，不进入外层缓存；若失效，只有文件名含 `__gpu-` 且能剥出同名 PTH 时才可自动重建，远端 `*-x2-tensorrt.engine` 这类预置命名通常无法匹配对应 PTH。
- RIFE interpolation path: RIFE TensorRT 由 RVE `InterpolateRifeTorch` 内部构建两套 Engine（flow/encode），缓存名包含权重文件名、静态或动态 profile、FP16、scale、GPU、TensorRT、Torch-TensorRT、ensemble 和优化级别，参数/设备/profile 变化会换名复用；但 `check_engine_exists()` 只检查文件存在，加载损坏/不兼容 Engine 时不会自动删除并重建，权重内容替换但文件名不变也不会因 SHA-256 变化而失效。
- Conclusion: 当前“超分常规 PTH 缓存”具备基本复用和失效重建；“RIFE 补帧缓存”和“预置 Engine 失效重建”尚未达到可靠的自动重建标准，且超分转换尺寸参数传递需要修复。仅做代码核对，未修改实现、未执行真实 GPU 测试。

### 2026-08-24 15:10 - Codex

- Objective: 实现用户要求的设备专用 TensorRT Engine 机制：任务按当前配置自动生成/复用，RIFE 失效重建，3FUI 与模型转换页显示构建进度。
- Changes: `cli/Program.cs` 增加 Torch-TensorRT 运行时探测、schema/精度/优化级别/转换配置缓存键；任务将输入宽高、输出倍率、tile、tile padding 传给 `convert_tensorrt.py`；Engine profile 验证器接收请求宽高并在超出 profile 时判定失效；TensorRT 下拉和远端下载列表不再展示预置 Engine。`VideoEnhancerPlugin/BackendProgress.vb` 解析任务构建事件；`PluginPanel.vb` 实时读取转换器进度并正确提取最终 `.engine` 路径。README 已同步本机缓存与预置 Engine 策略。
- Python runtime changes (outside Git): `convert_tensorrt.py` 真正使用 width/height/output-scale/tile/precision/optimization 参数；`InterpolateRIFE.py` 加入权重 SHA-256、flow/encode 成对缓存清理、构建后加载失败重建和构建阶段事件；`validate_tensorrt_engines.py` 增加 profile 尺寸检查。
- Packaging: 基于 `Backend/python_20260823.7z` 创建并上传 `Backend/python_20260824.7z`；远端 HTTP HEAD 返回 200，大小 `3447393513`，ETag/SHA-256 `dc399b4dc257b64b09d3175ac9afa3ca66bc388bc40e6313c9b85c5559055b17`。
- Verification: `dotnet build cli/VideoEnhancer.csproj -c Release --no-restore` 通过（0 错误，2 个既有 CA1416）；`VideoEnhancerPlugin/build.ps1 -HostBin ... -SkipInstall` 通过；`cli/build.ps1` 单文件发布通过，CLI `--version` 为 `1.0.3`；`python -m py_compile` 覆盖转换器、RIFE、验证器和 TensorRTHandler；`python -m unittest cli.tests.test_rve_ordered_backend -v` 6/6 通过；`git diff --check` 通过。无 NVIDIA 环境，未声称真实 TRT 构建成功。
- Deployment: 根目录 EXE/DLL/layout 已复制到 `C:\Program portable\3FUI\plugin`，EXE/DLL 源目标 SHA-256 一致。
- Remote cleanup: 未删除 ModelScope 远端 `TensorRT-Default` 或其他文件；新客户端已隐藏预置 Engine，远端删除仍需用户明确确认。
- Git status: `cli/Program.cs`、`cli/README.md`、`VideoEnhancerPlugin/BackendProgress.vb`、`VideoEnhancerPlugin/PluginPanel.vb`、`VideoEnhancerPlugin/README.md`、本记录和 `version/工作进度.md` 有未提交修改；独立主线未合并原作者 `origin`。建议完成上传确认后提交并推送 fork。

### 2026-08-24 15:35 - Codex

- Objective: 修复用户报告的启动环境检查误导和补帧开关状态分裂。
- Changes: `VideoEnhancerPlugin/PluginPanel.vb` 增加环境检查进行中/完成状态；环境检查超时会停止子进程并显示非错误的加载中提示；仅基础组件缺失显示红色失败，模型目录尚未准备好显示“基础环境已就绪，模型列表仍在加载”。模型列表读取期间不再把空列表作为启动错误。新增 `SyncInterpSwitchFromConfig()`，在后端切换与 UI 刷新时同步 `_config.InterpEnabled`、开关 Checked/Enabled 和状态标签；BasicVSR++ 自动关闭补帧后，切回可组合后端保持关闭且显示一致。
- Verification: `VideoEnhancerPlugin/build.ps1 -HostBin C:\Users\maxzr\AppData\Local\Temp\FFmpegFreeUI.6.1.39.extracted -SkipInstall` 通过；插件 DLL 已复制至 `C:\Program portable\3FUI\plugin`，源/目标 SHA-256 均为 `82603a6f6c0797058440a5695f7c97cf9426b32d13ce3a400ca3663e93c1fe3a`；`git diff --check` 通过。
- Environment: 当前无 NVIDIA，未执行真实 TRT；`git pull` 因本地未提交修改被拒绝，未合并上游。
- Git status: 工作树仍有本轮及上一轮功能、文档和记录修改，未提交；建议在 3FUI 重启回归后提交。

### 2026-08-24 16:05 - Codex

- Objective: 修复用户截图中补帧开关视觉为开启、但右侧状态文字为关闭的问题。
- Changes: `VideoEnhancerPlugin/PluginPanel.vb` 的官方、普通和 legacy 三条页面构建路径改用 `SyncInterpSwitchFromConfig()`；同步函数加入空控件/已释放保护、`Try...Finally` 同步标志恢复，并在赋值 Checked/Enabled 后显式调用 LakeUI 控件的 `Invalidate(True)`、`Refresh()` 和 `Update()`，强制刷新自绘开关外观。
- Verification: `VideoEnhancerPlugin/build.ps1 -HostBin C:\Users\maxzr\AppData\Local\Temp\FFmpegFreeUI.6.1.39.extracted -SkipInstall` 通过；源 DLL 与 `C:\Program portable\3FUI\plugin\videoenhancer.3fui.dll` SHA-256 均为 `E478BFEA006D8ADB7949829FDAD6827F5C1C419B39EFB9C9C02631D2ED6E755D`；`git diff --check` 通过。
- Runtime limitation: 当前没有完整可控的 3FUI 窗口自动化和 NVIDIA 环境，仍需用户重启宿主后验证 BasicVSR++ → TensorRT、TensorRT → BasicVSR++、手动开启补帧三组视觉交互。
- Git status: `main...origin/main [ahead 24, behind 5]`；`VideoEnhancerPlugin/PluginPanel.vb` 及前序 TensorRT/记录文件仍有未提交修改。建议先在 3FUI 实测后提交，且不要直接合并原作者 `origin`。

### 2026-08-24 16:20 - Codex

- User report: 实机截图显示补帧开关滑块仍在右侧，虽然状态文字为“关闭”，说明 LakeUI GPU 绘制缓存未被普通 `Refresh` 替换。
- Change: 在 `SyncInterpSwitchFromConfig()` 中加入 `RequestLakeSwitchRender()`，通过反射调用 LakeUI `BooleanSwitch` 的私有 `请求V3渲染(Boolean)`；若宿主版本没有该方法则回退到标准刷新，不影响兼容性。
- Verification: `VideoEnhancerPlugin/build.ps1 -HostBin C:\Users\maxzr\AppData\Local\Temp\FFmpegFreeUI.6.1.39.extracted -SkipInstall` 通过；新 DLL SHA-256 为 `7A94ABF6E8A20495B0E5F49B9F1FB5E1EC5F1D8AD5C640D6CFA59B550A250D75`。
- Deployment: 用户退出 3FUI 后已覆盖安装目录并核对源/目标哈希一致；可直接启动宿主进行视觉回归。

### 2026-08-24 16:25 - Codex

- Deployment: 用户确认已退出 3FUI，已将新构建的 `videoenhancer.3fui.dll` 复制到 `C:\Program portable\3FUI\plugin\videoenhancer.3fui.dll`。
- Verification: 工作区和安装目录 SHA-256 均为 `7A94ABF6E8A20495B0E5F49B9F1FB5E1EC5F1D8AD5C640D6CFA59B550A250D75`。
- Next: 启动 3FUI，验证 BasicVSR++ 下开关为关闭且禁用，切回 TensorRT 后仍为关闭但可点击，手动打开后滑块和状态文字同时变为开启。

### 2026-08-24 16:45 - Codex

- User report: 上一版加入 V3 请求后仍存在 Checked/视觉状态矛盾。
- Root cause: LakeUI `BooleanSwitch.Checked` 使用动画助手；同步时随后设置 `Enabled=False` 会停止动画但保留旧的 `Progress=1`，因此字段已为 False 仍绘制右侧滑块。
- Fix: `SyncInterpSwitchFromConfig()` 暂时保存并设置 `AnimationDuration=0`，先同步 Checked 让动画进度立即落到目标，再设置 Enabled，最后恢复原动画时长；移除不必要的私有反射渲染调用。
- Verification: 插件构建通过；新 DLL 已部署，源/目标 SHA-256 均为 `811BF85019877A31F90EF0EBF678065902B1E14AE73CAEEC3B6EE3748DF570A9`；`git diff --check` 通过。
- Next: 用户重启 3FUI 后复测 BasicVSR++ ↔ TensorRT 和手动开关。

### 2026-08-24 17:00 - Codex

- User report: 主页面“检查更新”按钮白色背景贴住窗口最底部边框。
- Fix: `VideoEnhancerPlugin/PluginPanel.vb` 将根布局底部状态行从 48px 调整为 60px，状态栏增加 8px 下内边距；按钮自身布局和更新逻辑不变。
- Verification: 插件构建通过；新 DLL 工作区 SHA-256 为 `71F5439F5F071823A5BF5D23E651B3AF891C710FD95C2A3A251162AD1024C959`；`git diff --check` 通过。
- Deployment blocker: 用户已重新启动 3FUI 进行前一版测试，安装目录 DLL 被占用，尚未覆盖部署；退出宿主后继续复制并校验。

### 2026-08-24 17:05 - Codex

- Deployment: 用户退出 3FUI 后已成功覆盖安装底部间距修复版 DLL。
- Verification: 工作区与 `C:\Program portable\3FUI\plugin\videoenhancer.3fui.dll` SHA-256 均为 `71F5439F5F071823A5BF5D23E651B3AF891C710FD95C2A3A251162AD1024C959`。
- Next: 启动 3FUI 检查“检查更新”按钮与底部边框之间是否保留可见间距。

### 2026-08-24 17:30 - Codex

- User report: 环境检测再次显示“存在缺失项”；同时指出模型下载页“下载插件更新”和检查更新功能重复，原意是把插件 EXE 放入资源名称列表。
- Changes: `PluginPanel.vb` 的环境检查只认以 `[缺失]` 开头且不属于模型库/补帧库/GPU/TensorRT Engine 的行；下载页按钮改为“下载全部”，点击后批量处理资源列表中所有未安装项；新增 `Plugin` 分类本地安装判断和下载目标映射。
- Changes: `cli/Program.cs` 允许 `Plugin` 根目录、将 `Plugin/videoenhancer.exe` 下载至 `AppRoot`；ModelScope 已上传当前 `videoenhancer.exe` 到 `Plugin/videoenhancer.exe`。
- Verification: CLI 构建和发布通过，插件构建通过；安装版 CLI `--check -backend tensorrt` 的 GPU Engine 不兼容不再应被插件解析为基础环境错误；`--list-download-models --json` 已返回 `Plugin/videoenhancer.exe`；`git diff --check` 通过。
- Deployment: 最新 CLI EXE 已覆盖安装目录且 SHA-256 为 `62D1A92FDF206017991170CFBC9B1E26DFA2C07041726D6A2DB1A09D4491C74F`；插件 DLL 因 3FUI 正在运行被锁定，安装目录仍为上一版 `71F5439F...`，待用户退出宿主后覆盖。

### 2026-08-24 18:05 - Codex

- Objective: 修复“下载全部”重复下载当前 `videoenhancer.exe`，并将自动更新检查/下载源改为 GitHub 首选、ModelScope 兜底。
- Changes: `VideoEnhancerPlugin/PluginPanel.vb` 的 `OnDownloadAllClick` 排除 `Plugin/videoenhancer.exe`；`PluginUpdater.vb` 拆分 GitHub/ModelScope 清单读取，GitHub 失败时解析 ModelScope `stable.json`，更新包下载顺序改为 GitHub→ModelScope，并为 ModelScope 清单构造 GitHub Release 资产 URL；版本源升至 1.0.5；发布脚本默认说明同步。
- Release: GitHub `maxzrb/VideoEnhancer` 已创建 `v1.0.5`；ModelScope `AerithDream/VideoEnhancer-Releases` 已上传 `stable.json` 与 `releases/1.0.5/VideoEnhancer-1.0.5-win-x64.zip`。
- Commands/verification: `dotnet build cli/VideoEnhancer.csproj -c Release --no-restore` 成功（2 个既有 CA1416 警告）；插件 `build.ps1 -SkipInstall` 成功；`release/test-updater.ps1 -Version 1.0.5` 的 success/traversal/tamper/rollback 全部通过；GitHub API、GitHub stable.json、ModelScope stable.json、ModelScope ZIP HEAD 均 HTTP 200；`git diff --check` 通过。
- Environment: 本机无 NVIDIA，未执行真实 TensorRT 构建；发布首次因 `out/vbc.rsp` 被并发编译短暂占用，重试成功。
- Git: `main...origin/main [ahead 24, behind 5]`，工作树含本轮与此前累计未提交修改；未执行提交、推送或上游合并。建议用户实测后统一提交。

### 2026-08-24 18:20 - Codex

- Objective: 为 GitHub / ModelScope 双本体发布和 ModelScope 模型资源发布建立统一门禁流程。
- Changes: 新增 `release/发布流程.md`，记录独立 SemVer、仓库职责、EXE/DLL/layout 三文件更新包约束、构建与更新器隔离测试、GitHub→ModelScope 发布顺序、模型资源单独上传、PotPlayer 排除、远端哈希/版本交叉核验、失败重试与回滚、HandShake 收尾和最终签字表。
- Verification: 文档中的命令与当前 `release/build-modelscope-release.ps1`、`release/test-updater.ps1`、`cli/Program.cs`、`PluginUpdater.vb` 的实际行为逐项对齐；`git diff --check` 通过。
- Git: `main...origin/main [ahead 24, behind 5]`，新增文档及此前累计源码/记录修改均未提交；未执行上游合并。

### 2026-08-24 18:40 - Codex

- Objective: 重写 GitHub 首页 README，移除原作者教程、个人评价和不适合当前项目的宣传表述。
- Changes: 根目录 `README.md` 改为功能、安装、后端、模型目录、CLI、HDR/处理顺序、ModelScope 下载、自动更新、故障排查、源码构建、许可证边界和反馈信息；删除“独立维护版”“独立版本”和上游基线表述，只保留当前版本 `1.0.5`。
- Verification: README 全文检索无 `独立维护版`、`独立维护`、`独立版本`、`上游` 残留；`git diff --check` 通过。
- Git: `main...origin/main [ahead 24, behind 5]`，README、发布流程和此前累计源码/记录修改均未提交。

### 2026-08-24 18:55 - Codex

- Objective: 按用户要求提交当前累计修改并推送 GitHub，同时将“发布后默认提交与推送”加入门禁。
- Changes: `release/发布流程.md` 新增“默认提交与推送”，要求发布完成后确认推送目标、提交源码和记录、推送 `fork/main` 并核对远端；明确不得误推原作者 `origin`。
- Git: 主发布提交 `8e59154 release: 1.0.5` 已推送到 `fork/main`（`https://github.com/maxzrb/VideoEnhancer.git`），推送范围 `32a0888..8e59154`。`origin` 仍为 `user-Wing/VideoEnhancer`，未推送、未合并。
- Verification: 提交前 `git diff --cached --check` 通过；`git push fork HEAD:main` 成功。完成本条记录后将追加纯记录提交并再次推送，目标是干净工作树。

### 2026-08-24 19:35 - Codex

- Objective: 按用户决定从 1.0.6 直接启用 EXE-only 更新协议，并发布 1.0.7 供真实自动更新测试，不兼容 1.0.5 旧 ZIP 协议。
- Changes: `cli/Program.cs` 的更新器改为校验/替换单 EXE，并从新 EXE 内嵌资源释放插件 DLL；`PluginUpdater.vb` 以下载的新 EXE作为临时更新器；发布脚本只生成版本化 EXE和 `stable.json`，同时上传 GitHub、ModelScope Releases及模型仓库 `Plugin/videoenhancer.exe`；隔离测试和全部 README/发布流程同步新协议。
- Verification: CLI/插件构建通过（仅 2 个既有 CA1416）；`release/test-updater.ps1 -Version 1.0.7` 的 success/tamper/invalid-package/rollback 全部通过。首次发布重建因百度网盘短暂锁定 `cli/obj` 失败，关闭 .NET 构建服务器后重试成功，未终止用户同步进程。
- Release: GitHub latest 为 `v1.0.7`，资产仅 `stable.json` 和 `VideoEnhancer-1.0.7-win-x64.exe`；GitHub/ModelScope stable.json 均为 1.0.7，路径、大小 `17417235`、SHA-256 `877b64f1920eac60732b0aa959eaba5a22779da59ab0566735fbedddf1751cef` 一致；GitHub EXE、ModelScope Releases EXE、模型仓库 EXE 哈希一致。
- Deployment: `C:\Program portable\3FUI\plugin` 已部署并保留 1.0.6 EXE/DLL，源目标哈希一致，CLI 报告 1.0.6；没有提前覆盖 1.0.7，供用户实测自动升级。
- Git: 本轮源码和记录待提交；发布门禁要求提交并推送 `fork/main`，不推送原作者 `origin`。

### 2026-08-24 19:45 - Codex

- Git closeout: `release: 1.0.7 exe-only updater` 提交最初为 `9904c41`；首次推送发现用户在 GitHub README 上新增 `6aa1002`、`8272b21`、`afa2410`，未强推。读取确认这些提交为 README 精简、ModelScope 链接和原作者链接后，将本地提交无冲突变基到 `fork/main`，最终提交为 `12a0bf2`。
- Push verification: `git push fork HEAD:main` 成功，推送范围 `afa2410..12a0bf2`；本地 `HEAD` 与 `fork/main` 均为 `12a0bf27709e853c6f0c26ddd23c1774b23b1568`。`origin` 未推送、未合并。
- Runtime test state: `C:\Program portable\3FUI\plugin\videoenhancer.exe --version` 仍为 1.0.6；用户可启动 3FUI 测试发现并安装远端 1.0.7。源码工作树在追加本记录前干净。

### 2026-08-24 19:50 - Codex

- Objective: 下载并审计作者 ModelScope `ARXChem/VideoEnhancer-Models` 最新 Python 后端，核实 FlashVSR/BasicVSR++ 内存修复，并规划 CUDA/TensorRT 补帧。
- Upstream artifact: `Backend/python_20260824.7z` 下载到本机隔离目录 `D:\read\ARXChem-VideoEnhancer-Models-audit`；大小 `2672713470`，SHA-256 `ebe7c07a41f3b7d62127d327eed57e76580d617086ea50b89255d88ff09729cf`，与作者数据集 tree API 完全一致。未覆盖 3FUI 安装目录。
- Low-memory finding: 作者新增 `src/temporal_video.py`，FlashVSR 使用 29 帧窗口/4 帧上下文，BasicVSR++ 默认 4 帧窗口/1 帧上下文，均改为窗口解码、窗口推理、立即通过 FFmpeg 写出，不再保存整段输入和输出；FlashVSR 复用 pipeline，BasicVSR++ 增加 CPU feature cache 与优化目录模型加载。
- Verification: 作者 `test_temporal_low_memory.py` 的 100 帧无重复/无丢帧和短视频测试 2/2 通过；相关 Python 文件 `py_compile` 通过。无 NVIDIA，未运行真实 FlashVSR/BasicVSR++。
- Interpolation finding: 安装目录五个 RIFE `.pkl` 均通过现有 `ArchDetect`，分别识别为 RIFE46、RIFE47、RIFE425 和 RIFE425_heavy。RVE 的 `torch.load`/RIFE TRT 路径不依赖 `.pth` 扩展名；真正限制 `.pth` 的是插件模型转换页和单帧超分专用 `convert_tensorrt.py`。该转换器使用 `UpscaleModelWrapper` 和单输入 NCHW，不能用于 RIFE，即使把补帧权重改成 `.pth` 也不可靠。
- Integration risk: 不能整包覆盖作者 Python 包，因为它缺少本地主线后来加入的 RIFE Engine 权重哈希、失效成对清理/重建、构建进度以及超分转换 profile 校验等改动。应选择性移植低内存文件，并为补帧建立独立的架构检查和 Engine 预构建入口。
- Git/worktree: 本轮仅在仓库外下载和提取审计包；仓库代码未修改。HandShake 记录与中文进度因本次 substantial audit 更新，工作树因此不再干净；`main...origin/main [ahead 31, behind 5]`，未合并原作者分支。

### 2026-08-24 20:30 - Codex

- Objective: 实施作者 FlashVSR/BasicVSR++ 低内存修复，并修复 `.pkl` RIFE 在 CUDA/TensorRT 补帧和模型转换页中的错误分流。
- Python backend: 在 `C:\Program portable\3FUI\plugin\python\backend` 选择性部署有限窗口、即时 FFmpeg 写出、Flash pipeline 复用、BasicVSR++ 优化目录加载/CPU cache 和测试；未整包覆盖本地主线 TRT 改动。
- Architecture/capability: 新增并嵌入 `inspect_interpolation_models.py`，通过 `ArchDetect` 读取权重内容。CUDA 列出 RIFE/GMFSS/GIMM，TensorRT 只列 RIFE，不再根据 `.pth/.pkl` 或目录名猜测。安装目录 5 个 RIFE `.pkl` 均识别成功。
- RIFE TRT: 新增并嵌入 `prepare_rife_tensorrt.py`，直接实例化 RVE `InterpolateRifeTorch` 构建 flow/encode Engine；新增 CLI 检查、预构建、宽高和静态 shape 参数。无 CUDA 时先检查并清晰返回退出码 1，避免进入 Torch-TensorRT 原生崩溃。
- Plugin: 模型转换页接受 `.pth/.pt/.pkl`，异步读取内部架构；普通超分 `.pth` 继续调用 `convert_tensorrt.py`，RIFE 改走 1080p 专用 flow/encode 预构建，GMFSS/GIMM 明确提示只支持 CUDA。使用当前官方 FFmpegFreeUI/LakeUI 源码构建依赖后，插件真实编译通过。
- Verification: CLI `dotnet publish` 成功（仅 2 个既有 CA1416）；Python `py_compile` 通过；低内存 unittest 3/3 通过；CUDA/TRT 模型列表和 `--inspect-interp-model` 端到端通过；`git diff --check` 通过。当前机器无 NVIDIA，未声称真实 CUDA/TRT 推理通过。
- Deployment: 新 EXE 已覆盖便携目录，源/目标 SHA-256 均为 `1294A309F24DF2F2B3EEA3C1A1A03C8F60A1C0B35E21E88C20526FEF204BFC77`；新插件 DLL SHA-256 为 `CA9403729650EABFE5C5F3F8F703FC04C4F620278174B15A0B645DFD2BB56181`，但安装 DLL 被正在运行的 3FUI 锁定。新 EXE 已内嵌新 DLL。
- Release decision: 用户要求先提交到远端并换 3060 设备继续开发；本轮不得发布 1.0.8 Release，也不更新 GitHub/ModelScope 发布清单。
- Git: 准备提交并推送 `fork/main`；`origin` 是原作者仓库，不推送、不合并。

### 2026-08-24 21:00 - Codex

- ModelScope base correction: 用户指出必须以其下午已更新后端为基线。已放弃且未上传基于作者 2.67 GB 包生成的错误候选；从 `AerithDream/VideoEnhancer-Models` 原样下载远端 `Backend/python_20260824.7z`，大小 `3447393513`，SHA-256 `dc399b4dc257b64b09d3175ac9afa3ca66bc388bc40e6313c9b85c5559055b17`，与 API 元数据完全一致。
- Package composition: 在正确基线上只更新 8 个文件：两个时序入口、`src/temporal_video.py`、BasicVSR++ model、FlashVSR nodes、低内存测试、补帧架构检查器和 RIFE TRT 预构建器。8/8 解包哈希与安装后端一致；下午版 `convert_tensorrt.py`、`validate_tensorrt_engines.py`、`InterpolateRIFE.py` 哈希保持不变。
- Package verification: 新包大小 `3447405317`，SHA-256 `3e55dcc3e773ceb5098d3b6bd90e9d490462b6323a5931d0aaba9e983a61482b`；7-Zip 整包测试通过，包含 6540 个目录、37533 个文件，解压总尺寸 `6410577325`。
- ModelScope upload: 已覆盖 `AerithDream/VideoEnhancer-Models/Backend/python_20260824.7z`，提交说明为 `backend: low-memory temporal windows and RIFE TRT tooling`。远端回读大小和 SHA 与本地新包一致，revision 为 `c0d5b6c09e08df7a2b25af49ed6f73e586a4bd1c`。
- Release guard: 本次仅更新模型仓库后端资源并准备 Git 提交；未创建 1.0.8 Release，未修改 stable.json 或 GitHub/ModelScope Releases。

### 2026-08-24 21:15 - Codex

- Git publication: 功能提交 `753a3a4 feat: add RIFE TensorRT tooling and temporal low-memory support` 已推送到 `https://github.com/maxzrb/VideoEnhancer.git` 的 `main`；本地 `HEAD` 与 `fork/main` 均为 `753a3a447dc2c16a79c607389a2daeec561fb087`。原作者 `origin` 未推送、未合并。
- Handoff: 3060 设备应先拉取 `fork/main`，再从 `AerithDream/VideoEnhancer-Models` 下载远端 revision `c0d5b6c09e08df7a2b25af49ed6f73e586a4bd1c` 的 `Backend/python_20260824.7z`。优先验证 RIFE CUDA、RIFE TensorRT 首次构建/缓存命中，以及 FlashVSR/BasicVSR++ 长视频内存。
- Release guard: 项目版本保持 1.0.7；用户本地验证无误前不得发布 1.0.8。

### 2026-08-24 21:30 - Codex

- User report: CUDA 补帧选择 `RIFE/rife4.26.heavy` 后报“补帧模型名不唯一”。
- Root cause: `ResolveInterpModel()` 已精确匹配 `RIFE/rife4.26.heavy`，但旧逻辑仍调用通用 `Path.GetFileNameWithoutExtension(raw)`；Windows 将 `.heavy` 视为扩展名并得到 `rife4.26`，从而又匹配普通 `rife4.26.pkl`，错误形成两个候选。实际模型目录中 heavy 和普通权重各只有一份。
- Fix: `cli/Program.cs` 改为先按 `InterpModelDisplayName` 精确匹配完整相对架构路径；短名称回退只剥离 `.pth/.pt/.pkl/.engine` 四种真实权重扩展名，保留 `.heavy` 等模型名后缀；直接候选统一返回规范绝对路径，解决带扩展名和混合分隔符的二次架构检查失败；真正歧义时列出候选相对路径。
- Verification: 基于远端 `7030ba1` 构建插件和自包含 CLI 成功（仅 2 个既有 CA1416）；内置 Python 脚本编译通过；顺序后端测试 6/6。真实便携后端列出 CUDA 8 项、TensorRT 5 项；heavy/普通 4.26 的相对路径、短名和带 `.pkl` 输入共 6/6 正确解析，heavy 架构为 `RIFE425_heavy`、普通为 `RIFE425`。
- Deployment: 仅覆盖 `C:\Program portable\3FUI\3FUI\plugin\videoenhancer.exe`，未修改 DLL、Python 或模型；源/目标 SHA-256 均为 `B18D735C4B213CAE00B851E7238D39F58C19D086189425E9544E0CB59CB8BE62`。部署版再次检查 `RIFE/rife4.26.heavy` 退出码 0，可直接重试任务，无需重启 3FUI。
- Local cleanup: 终端安全策略拒绝删除测试副本 `videoenhancer-resolver-test.exe`；配置未引用该文件，不影响运行。
- Release/Git: 版本保持 1.0.7，未发布 1.0.8。源码和本记录待提交，未推送。

### 2026-08-24 21:45 - Codex

- User report: 模型解析修复后任务进入 CUDA 后端，但在转场检测阶段报 `ValueError: The provided filename ...\EfficientNet-SceneDetect is a directory`，用户询问是否由 `.pkl` 引起以及换 `.pth` 是否更好。
- Root cause: `.pkl` 权重已被 PyTorch 成功加载并识别为 `RIFE425_heavy`；崩溃与补帧权重格式无关。当前模型包只含 `EfficientNet-SceneDetect.bin/.param`（NCNN 格式），旧 CLI 却对所有后端都传入该目录；RVE 在 CUDA/TensorRT 主后端会选择 PyTorch 转场检测器并调用 `torch.jit.load(directory)`，因此失败。
- Fix: `BuildBackendArgs()` 按主后端选择转场检测方式。CUDA/TensorRT 使用 RVE 内置且无需外部模型的 `pyscenedetect`；NCNN/ONNX 继续使用 `sudo_scene_detect` 和现有 EfficientNet NCNN 模型。转场检测和用户阈值仍然启用。
- Verification: CLI Release 构建、自包含发布、6/6 既有后端测试及模型解析检查通过。RTX 3060 上实际运行 `rife4.26.heavy.pkl` CUDA 任务退出码 0；640x360 输入 24 帧、输出 47 帧，符合 2 倍补帧 `2N-1`，证明 `.pkl` 和修复后的转场检测链路均可工作。
- Deployment: 正式 EXE 已覆盖 `C:\Program portable\3FUI\3FUI\plugin\videoenhancer.exe`；源/目标 SHA-256 均为 `64268C901BB124697A04509DE6999D469C8FD7987706C2655D19A6AD7910FE16`。未修改 DLL、Python 后端或模型文件，不建议仅为扩展名改成 `.pth`。
- Remaining: 用户重试原始 1920x1080 完整任务；随后验证 RIFE TensorRT 首次构建/缓存命中及 FlashVSR/BasicVSR++ 长视频内存。
- Release/Git: 版本保持 1.0.7，未发布 1.0.8。工作树包含 CLI 与两份交接记录改动，尚未提交或推送；建议完整视频验证后单独提交本轮修复。

### 2026-08-24 22:00 - Codex

- User report: `GMFSS/GMFSS-Fortuna-Base` CUDA 初始化报 `KeyError: 'rife'`，且安装 GMFSS 后补帧模型下拉框读取明显慢于只有 RIFE 时；用户同时询问 GMFSS 是否存在 NCNN/TensorRT 可用版本。
- Root cause: Base 权重的 `metadata` 明确为 `architecture=gmfss, model_type=base`，顶层只有 `flownet/metricnet/feat_ext/fusionnet`，设计上没有 Union 才需要的 `rife`。旧 RVE 加载器默认 `model_type=union`，无条件访问 `combined_state_dict["rife"]`，随后还错误使用 Union 的 `FusionNet_u`。列表变慢则因为每次新 CLI 进程都导入 PyTorch 并完整扫描所有权重；当前目录新增 4 个 GIMM 和 3 个 GMFSS 大权重后更明显。
- Fix: CLI 启动时只对结构完全匹配的已知 RVE 2.4 GMFSS 加载器应用 UTF-8、换行符保持的兼容补丁；未来结构不匹配则不覆盖。加载器读取 `metadata.model_type`，Base 使用 `FusionNet_b` 且不创建 IFNet，Union 使用 `FusionNet_u` 并按 `rife` 内部架构选择 IFNet。架构检查器对 GMFSS/GIMM 使用 `torch.load(..., weights_only=True, mmap=True)` 的轻量元数据路径；CLI 新增按绝对路径、文件大小和 UTC 修改时间自动失效的本地能力缓存。
- Verification: GMFSS Base 与 Union 在 RTX 3060 上均完成 CUDA 模型初始化。Base 实际 640x360 补帧任务退出码 0，输入 24 帧、输出 47 帧，符合 2 倍补帧 `2N-1`。CLI 构建/发布成功（0 错误、2 个既有 CA1416），Python `py_compile` 和顺序后端 unittest 6/6 通过。12 项 CUDA 权重冷扫描约 3.98 秒；缓存后正式 EXE 连续 5 次为 0.368–0.383 秒。模型文件指纹变化会自动重检。
- Backend scope: 当前项目和所用 RVE 原生 GMFSS 路径仅实现 CUDA/PyTorch。正式列表验证 CUDA 有 3 个 GMFSS，TensorRT 与 NCNN 均只列 5 个 RIFE；RVE 源码也明确提示 GMFSS TensorRT 尚未实现。更正：外部 `vs-gmfss_union` / `vs-gmfss_fortuna` 与 Enhancr 已实现 GMFSS Union/Fortuna TensorRT，但不是当前 RVE 可直接启用的现成路径。
- Deployment: 正式 EXE 已覆盖 `C:\Program portable\3FUI\3FUI\plugin\videoenhancer.exe`，版本仍为 1.0.7；源/目标 SHA-256 均为 `9F2959857B5D67E2B837213E80E70B5933173263728B74D725FDFF0E4EF1B819`。安装后端的 `GMFSS.py` 与架构检查器已更新；未修改模型权重、插件 DLL 或发布通道。
- Remaining: 用户重试原始 1920x1080 GMFSS Base 任务并比较 Union/AnimeRun 画面；随后继续 RIFE TensorRT 首次构建/缓存命中和时序模型长视频内存验证。
- Release/Git: 版本保持 1.0.7，未发布 1.0.8。`HEAD` 与 `fork/main` 均为 `7030ba1`；工作树有 `cli/Program.cs`、架构检查器和两份记录共 4 个文件未提交，建议完整任务验证后提交并推送。

### 2026-08-24 22:10 - Codex

- Decision: 用户决定暂不接入 GMFSS TensorRT。已更正此前“GMFSS Fortuna 没有 TensorRT 实现”的错误记录：当前 RVE 2.4 原生 GMFSS 类确实只执行 PyTorch/CUDA并在 TensorRT 下回退，但 Enhancr 通过 `vs-gmfss_union` / `vs-gmfss_fortuna` 已实现 Union 与 Fortuna TensorRT。
- RVE model audit: 本机 RVE 2.4.1-dev17 的补帧工厂支持 RIFE、GIMM、GMFSS、IFRNet；当前项目能力检查只放行前三者，因此唯一尚未接入的现成补帧架构是 IFRNet PyTorch/CUDA。IFRNet 在 TensorRT 参数下只回退 PyTorch，不应列入 TensorRT；NCNN 插帧器仍是 RIFE 专用。ModelScope 当前还未镜像 RIFE 4.15、4.18、4.20、4.22/4.22-lite 等 RVE 已兼容的旧权重变体，但这属于补权重而非新增架构。
- Restoration audit: RVE 命令行完整支持可重复传入的 `--extra_restoration_models` 1x 修复链，官方模型表包括 DeH264、DRUNet、DnCNN。本机已有 `DenoiseH264-SuperUltraCompact-1x`、`DnCNN-ColorBlind-1x` 的 NCNN 权重及两个 1x PTH/ONNX 修复模型，但当前 CLI/插件没有独立修复模型和链式顺序入口；作为普通 1x 主模型出现不等于完整接入修复链。
- Non-model capability: RIFE DRBA 类仍存在，但当前 `rve-backend.py` 把 `drba=False` 写死，不能视为可直接接入的命令行能力。ONNX 插帧文件存在，但 `RenderVideo` 主路径没有完整接线，也不列为已支持后端。
- Priority: 建议先补 IFRNet CUDA 的权重来源、ModelScope 分发、架构扫描和下拉框；再实现 DeH264/DRUNet/DnCNN 修复链。GMFSS TensorRT 保持暂缓。
- Release/Git: 本次只审计和更正记录，不修改功能代码、模型或部署文件；版本保持 1.0.7，未发布 1.0.8。当前功能改动仍建议在完整视频验证后提交并推送。

### 2026-08-24 22:20 - Codex

- Question: 用户询问去压缩/去噪模型与后续不同后端的兼容关系。本次只审计现有代码，不实施功能。
- Native RVE semantics: `--extra_restoration_models` 可以重复传入，但没有独立 backend 参数；`RenderVideo.setupExtraRestoration()` 使用整个进程的 `self.backend`。原生和项目内 `upscale-first` 包装器都固定按“全部 1x 修复 → 补帧/超分”执行。PyTorch/TensorRT 共用 `UpscalePytorch`，NCNN 使用 scale=1 的 `UpscaleNCNN`；ONNX/DirectML 没有 extra restoration 分支，FlashVSR/BasicVSR++ 会提前转入专用时序入口，也不接收修复链。
- Cross-backend design: 修复后端与第一项后续操作相同时可合并在一个 RVE 进程内，避免中间编码；不同时必须扩展现有 `RunVideoPipeline()`，用 RGB FFV1 无损中间视频拆成 2–3 阶段。建议固定修复优先，并按 DeH264 → denoise → interpolation/upscale 排序。当前管线只规划超分与补帧两项，尚不能表达独立修复阶段。
- HDR boundary: 本项目已拒绝 HDR + NCNN/ONNX，且 RVE 自身会把 NCNN HDR 回退到 SDR。因此 HDR 修复阶段只能用 CUDA/PyTorch 或经验证的 TensorRT；即使后续切回 CUDA，前一 NCNN/ONNX 阶段造成的 HDR 丢失也无法恢复。
- Model/backend risk: DeH264 类普通单输入 1x PTH/NCNN 模型最适合首批验证。标准 DnCNN 可走 NCNN，PyTorch 注册表也支持，但标记为不支持 FP16，CUDA 通常会回退 FP32。DRUNet 同样不支持 FP16，且当前 `UpscaleModelWrapper` 从 Spandrel 描述器取出原始 `model.model`，丢掉用于拼接噪声图的 `call_fn`；直接按三通道调用四通道 DRUNet 有结构性风险，接入前必须修补并实测。TensorRT 通过 Torch-TensorRT 尝试编译每个 1x PTH，不等于所有架构都可编译；需要按模型建立白名单和本机缓存验证。
- Resource risk: 同进程最终会重新加载补帧、超分和全部修复模型；RTX 3060 6 GB 在多模型 CUDA/TRT 组合下可能 OOM。跨进程阶段较稳但增加 FFV1 磁盘空间、读写时间和一次 RGB 解码/编码边界。
- Release/Git: 未修改功能代码、模型或部署文件，只更新 HandShake 记录；版本仍为 1.0.7。工作树继续包含此前两个源码修改和两份记录，建议完整 GPU 验证后再统一提交。

### 2026-08-24 23:25 - Codex

- Matrix scope: 用户将“所有模型”调整为“每类架构选择代表模型，代表组合全部测试”。建立 `cli/tests/gpu_matrix_runner.py`，生成 4 帧 FFV1 夹具、逐项 JSONL、CSV/Markdown 报告、超时日志、断点恢复和失败重跑。经 TensorRT 能力校正后有效矩阵为 578 项：单模型 50、同后端 150、跨后端 378。
- Single-model result: 当前可选的补帧/超分代表 50/50 全部通过。NCNN 8 类、CUDA 14 类、TensorRT 11 类、ONNX 9 类、FlashVSR、BasicVSR++，以及 NCNN/CUDA/TensorRT RIFE、CUDA GIMM、GMFSS Base/Union 均有真实输出，并按尺寸与逐帧计数验证。
- Fixes from runtime: 修复 NCNN 在完整输出后的 Vulkan `0xC0000005` 误报（仅严格 ffprobe 校验通过时归一成功）；过滤场景检测模型；GIMM 新字段加载、FP32/autocast 与 320x240 最低夹具；SwinIR/GRL CUDA FP32；渲染线程异常后主动清理；ONNX 带点名称与 `-2x.onnx` 倍率；TensorRT 64 高度探测、MAX_PATH 缓存键和 GRL FP32 Engine。
- TensorRT capability: AnimeSR 是 5D 时序输入，SwinIR 命中 Torch-TensorRT 切片分解错误，CRAFT 生成混合分区多输出 Engine，均不兼容当前单图直接 Engine 适配器，已从 TensorRT 清单排除。GRL FP16 失败、FP32 Engine 验证和实际视频均通过，因此保留并按模型选择 FP32。
- Verification/deployment: 多轮 `dotnet build/publish` 均 0 错误（仅 2 个既有 CA1416），顺序包装器 unittest 6/6；正式 `C:\Program portable\3FUI\3FUI\Plugin\videoenhancer.exe` 已同步。版本保持 1.0.7，未发布 1.0.8；工作树尚未提交，矩阵完整收口后统一建议提交。

### 2026-08-25 09:18 - Codex

- User decision: 用户明确暂停 1.0.8 发布，要求先评估老用户能否及时更新 Python 后端，以及覆盖安装是否会留下旧脚本。本轮未修改版本号、未构建或上传发布资产、未创建 GitHub Release/标签。
- Current mechanism: `DownloadRepositoryModel()` 将 Backend 归档下载到活动 `CoreRoot/python`，随后 `7z x -y` 直接解压到 `CoreRoot`。该路径只覆盖同名文件，不删除新版归档中已取消或改名的旧文件；没有暂存环境、版本清单、健康检查、目录交换或失败回滚，中断后可能形成半新半旧环境。
- Discovery issue: `PluginPanel.IsDownloadInstalled()` 对 Backend 只检查 `python/python.exe`。只要任意旧后端存在，最新日期归档也显示“本地已安装”，单项下载函数随即返回，因此正常用户看不到可操作的后端更新入口。
- Compatibility boundary: 当前待发布 EXE 会在启动时对已知 RVE 2.4 文件结构同步补帧检查器，并以保守文本匹配修补 GMFSS、GIMM 和 ONNX；这能临时兜住本轮四个后端脚本变化，但结构不匹配时会放弃修改，不能作为长期升级方案。
- Recommended design: 后端更新独立于普通模型下载。使用远端/本地版本与 SHA-256 标记；归档下载到活动目录之外；解压到同盘暂存目录；验证 Python、关键脚本和基本导入；等待任务与 3FUI 退出后，把现有 `python` 目录改名为备份并将新目录原子换入；失败立即回滚。旧目录保留到新后端健康检查通过，未知用户文件不直接删除。
- Release/Git: `main` 与 `fork/main` 在审计前均为 `7030ba1`，工作树已有矩阵相关未提交变更。版本保持 1.0.7；建议完成事务式后端更新器和隔离测试后，再恢复 1.0.8 发布并提交推送。

### 2026-08-25 09:27 - Codex

- User concern: 每次后端更新重新下载约 3.4GB 不可接受，询问是否可采用游戏式增量更新。本轮仍只做设计，不恢复发布、不上传资源。
- Recommendation: 首版采用文件级增量包，而不是对压缩后的 `.7z` 做二进制差分。本轮远端基线后仅 4 个 Python 脚本变化，原始总量约 47KB，补丁可降到几十 KB；压缩包二进制差分对重压缩和精确基础文件高度敏感，不适合作为第一版。
- Patch contract: 远端通道清单列出目标版本、全量包及可用补丁边；每个补丁记录 `baseVersion`、`targetVersion`、新增/替换/删除操作、每个旧文件和新文件 SHA-256、补丁自身大小与 SHA-256、最低 CLI 版本及健康检查。客户端根据本地版本选择最小可达补丁链。
- Transaction: 下载到活动目录外并校验；拒绝直接修改正在运行的 Python；更新前只备份补丁涉及的文件，删除操作改为移动到事务备份；新文件先写 `.new` 并验哈希，再原子替换；全程写 pending journal。进程中断后下次启动可回滚或继续，健康检查通过后才提交新版本标记。
- Compatibility: 遗留环境没有版本标记时，用少量已知哨兵文件哈希识别公开基线；能识别则补写标记并走增量，无法识别、关键文件被改动或补丁链过大时才提示全量修复安装。未知用户文件保留，只有旧清单中受项目管理且补丁明确声明删除的路径才会移动。
- Scope decision: 保留 `Backend/python_YYYYMMDD.7z` 作为新装与修复兜底；普通脚本/小 DLL 更新走文件级补丁。若未来单个大型二进制频繁变化，再考虑内容寻址分块或 xdelta，不在 MVP 中增加复杂度。

### 2026-08-25 10:09 - Codex

- Objective: 按用户授权实现游戏式后端增量更新机制，同时继续暂停 1.0.8 发布。
- Implementation: 新增 `BackendUpdateManager`，实现 schema v1 通道/补丁解析、遗留哨兵识别、按下载字节数选择最小补丁链、本地版本标记、逐文件 old/new SHA-256、add/replace/delete、已自修补文件幂等跳过、活动 Python 进程阻止、受影响文件备份、pending journal、原子替换、健康检查、即时回滚和下次启动恢复。未知/损坏基线走完整包暂存探测和同卷目录切换，避免覆盖解压残留旧脚本。
- Plugin/UI: Backend 状态与模型列表分别查询；下载页显示当前版本、目标版本、增量/完整模式及有效下载大小；Backend 不再进入分类批量、下载全部或三路并行，单独调用 `--update-backend`。远端 channel 不可用时保守禁用旧覆盖路径。
- Release tooling: 新增 `release/build-backend-patch.ps1`、`release/backend-channel.example.json`、`release/test-backend-updater.ps1`；发布文档规定完整包/补丁/channel 上传顺序和哈希门禁。CLI 新增 `--backend-status`、`--update-backend`、`--apply-backend-patch`、`--backend-channel`，并拒绝 `--download-model Backend/...` 的旧覆盖安装。
- Verification: `dotnet build cli/videoenhancer.csproj -c Release --no-restore` 成功（0 错误、2 个既有 CA1416）；插件用 `%LocalAppData%\VideoEnhancerDev\FFmpegFreeUI.6.1.39.extracted` 构建成功；后端更新隔离测试 6/6（通道增量、SHA 冲突、健康失败回滚、中断恢复、完整修复、幂等部分补丁）；顺序包装器 unittest 6/6；两个 Python 工具 `py_compile` 通过；CLI 帮助和 `git diff --check` 通过。
- Files changed this session: `cli/BackendUpdateManager.cs`, `cli/Program.cs`, `VideoEnhancerPlugin/PluginPanel.vb`, `release/build-backend-patch.ps1`, `release/backend-channel.example.json`, `release/test-backend-updater.ps1`, `release/发布流程.md`, `docs/codex/STATUS.md`, `version/工作进度.md`。工作树还包含此前 GPU 矩阵与运行时修复，均保留未覆盖。
- Release/Git: 版本保持 1.0.7；没有上传 ModelScope/GitHub 资产、创建标签或恢复 1.0.8 发布。`main...fork/main`，工作树不干净。首个生产补丁仍需从真实公开基线和目标后端生成并做实际安装目录验证；建议先提交当前源码与测试，再准备远端通道。

### 2026-08-25 10:34 - Codex

- Objective: 按用户要求强化发布流程：每次严格检查 Backend 变动并制作增量包；GitHub Release 正文强制逐行使用 `[更改]xxxx`、`[新增]xxxx`、`[移除]xxxx`。
- Release gate: `build-modelscope-release.ps1` 取消默认自由文本说明，支持 `-NotesFile`，在构建前拒绝空行、自由段落、未知分类和一行多个条目；生成 `release-notes.txt`，`stable.json.notes` 与 GitHub `--notes-file` 使用同一规范化内容。每次运行必须提供 Backend 基线/候选目录及版本，新增 `-ValidateOnly` 供无构建预检。
- Backend packaging: 新增 `prepare-backend-update.ps1`，逐文件计算长度/SHA-256 并分类 add/replace/delete。无变化时要求版本不变；有变化时要求版本递增、完整包和稳定哨兵，解压完整包并与候选目录逐文件核对，随后调用补丁生成器并输出增量包、`channel.json`、`backend-release-audit.json`。正式双源发布在 Backend 变化时先按完整包→补丁→channel 上传 ModelScope 并回读核对，之后才创建 GitHub Release。
- Documentation/tests: 新增 `release/release-notes.example.txt` 和 `release/test-release-gates.ps1`；更新 `release/发布流程.md` 的门禁、构建、一键发布、上传顺序、后端制包、Release Notes 模板和最终签字项。
- Verification: PowerShell 三个发布脚本语法解析通过；模板文件通过 `-ValidateOnly`；旧式自由文本说明按预期拒绝；正式门禁测试 4/4 通过，覆盖合法逐行模板、非法自由文本、Backend add/replace/delete 自动制包及完整包不一致拒绝；`git diff --check` 通过。
- Release/Git: 版本保持 1.0.7，1.0.8 仍暂停；未创建 Release/标签、未上传后端或本体资产。`git pull --ff-only` 为 Already up to date，`main...fork/main`；工作树含本轮和此前 GPU/增量运行时改动，尚未提交，建议在准备真实发布资产前提交。

### 2026-08-25 10:56 - Codex

- Objective: 按用户恢复发布的指令准备 1.0.8 本体，同时明确暂缓 Backend 包与更新通道；移除现行发行元数据中的 `UpstreamBase/upstreamBase`。
- Backend audit: 从公开 `python_20260824.7z` 提取 2026.08.24.1 基线，与当前候选 2026.08.25.1 逐文件哈希。修正审计器以排除顶层下载归档和 `backend/cache/` Triton 运行缓存，最终为 0 add、7 replace、0 delete；暂缓模式本地补丁 14,699 字节，不生成 channel，未上传任何 Backend 资产。
- Release tooling: `build-modelscope-release.ps1` 新增显式 `-DeferBackendPublish`，仍执行真实审计和补丁生成，但跳过完整包、补丁及 channel 上传；门禁测试增加暂缓模式与缓存排除，5/5 通过。发布文档要求此模式只能在用户明确授权时使用，不能宣称后端已发布。
- Version/metadata: `PluginVersion.Current`、CLI csproj 和 README 更新为 1.0.8；删除 `PluginVersion.UpstreamBase`、更新清单的 `upstreamBase` 属性和 `stable.json` 生成字段。Release Notes 已按每行单项的固定模板准备。
- Verification: CLI 与插件 Release 构建成功（0 错误、2 个既有 Windows CA1416）；顺序包装器 unittest 6/6、Python `py_compile`、后端事务测试 6/6、发布门禁 5/5、EXE 更新器 success/tamper/invalid-package/rollback 均通过。产物版本 1.0.8，大小 17,471,662，SHA-256 `ee510e5599029e6637fdd072f8b64a78dacab07b21d3a6f28c54a8c401afd46e`；清单哈希一致且无 `upstreamBase`。
- Release/Git: 此记录时尚未提交、推送、创建标签或上传本体。下一步提交源码并仅发布本体双源；Backend 继续暂缓。

### 2026-08-25 11:04 - Codex

- Release: 提交 `2ed6c2e release: 1.0.8 backend incremental updater` 已推送到 `fork/main`，注释标签 `v1.0.8` 已推送；GitHub Release `https://github.com/maxzrb/VideoEnhancer/releases/tag/v1.0.8` 创建成功。
- Dual-source upload: ModelScope `AerithDream/VideoEnhancer-Releases` 已同步 1.0.8 版本目录、`stable.json`、说明文件和 README；`AerithDream/VideoEnhancer-Models/Plugin/videoenhancer.exe` 已同步。发布脚本在上传前明确进入 `-DeferBackendPublish` 分支，没有上传 Backend 完整包、补丁或 channel。
- Remote verification: GitHub Release 非草稿、非预发布，正文与 `release/release-notes.txt` 逐行一致，资产严格为 EXE 和 `stable.json` 两项。GitHub EXE、ModelScope Releases EXE、模型页 EXE 均为 17,471,664 字节，SHA-256 `fd249941331cbaa139cb52d770b1e60ec7d2454c7c65eb00fb9495a78345d820`；GitHub/ModelScope 清单版本、路径、大小和哈希一致，均不含 `upstreamBase`。
- Remaining: Backend 2026.08.25.1 通道尚未发布，用户目前只能验证 1.0.7→1.0.8 本体升级。发布记录更新后需再提交并推送，保持工作树干净。
- Local cleanup: 远端回读副本和公开后端基线解压目录均已校验位于 `%TEMP%`，但递归删除命令被当前执行策略拒绝，未强行绕过。仍保留 `videoenhancer-1.0.8-remote-verify`、`videoenhancer-backend-base-20260824-0e85c2f0237b43a0af0fb15c8a2c1139` 及路径记录文件，可在不再需要复核时手动删除；原始公开后端归档未触碰。

### 2026-08-25 11:18 - Codex

- Objective: 调查用户报告的 1.0.7→1.0.8 自动更新在关闭 3FUI 后没有重启的问题，并实现本地修复；未修改远端 Release。
- Evidence/root cause: `%LocalAppData%\FFmpegFreeUI\VideoEnhancer\update-result.txt` 明确记录 `ERROR|IO_SharingViolation_File, ...\Plugin\videoenhancer.exe`；正式插件目录 EXE 仍报告 1.0.7。旧逻辑在宿主退出后只尝试一次覆盖，遇到短暂共享冲突即失败；重启调用只位于成功分支，因此失败后 3FUI 保持关闭。
- Fix: `ApplyUpdate` 将等待宿主退出前移并记录退出确认；覆盖 EXE/DLL 时只针对 Windows sharing/lock violation 每 250ms 重试，最长 10 秒。宿主已确认退出后，重启调用移到 `finally`，更新成功、校验失败、共享冲突超时或回滚后都会尝试恢复 3FUI；宿主退出超时则不重复启动实例。
- Tests: `release/test-updater.ps1` 新增短暂占用后成功替换，以及真实 wait PID 退出后更新失败仍执行重启脚本两项。插件构建、单文件 CLI publish 成功（0 错误、2 个既有 CA1416）；更新器 success/transient-lock/tamper/invalid-package/rollback/restart-on-failure 六场景全部通过，`git diff --check` 通过。
- Release/Git: 远端 1.0.8、标签和清单均未修改；源码版本仍为 1.0.8。当前只有 `cli/Program.cs`、`release/test-updater.ps1` 及两份 HandShake 记录待提交。修复应作为 1.0.9 发布，不能覆盖 1.0.8。

### 2026-08-25 11:38 - Codex

- Objective: 按用户明确授权发布 1.0.9 及相应 Backend 更新；使用 HandShake/GitHub 发布流程，GitHub 与 ModelScope 凭据均有效。
- Version/assets: 版本源、README、Release Notes 和版本记录已更新到 1.0.9。新后端完整包 `python_20260825.7z` 从实际候选目录生成，排除旧 `python_*.7z`、本地版本标记、`backend/cache`、`__pycache__`、`.pyc/.pyo`；包内 29,707 文件，解压 6,261,268,671 字节，压缩 2,790,829,396 字节，SHA-256 `8c598d90e594a4e3957b48421f780c491cd06c73fb1914600b69950b0a44ab2d`。
- Backend verification: 完整包两次解压并与候选目录逐文件核对通过；2026.08.24.1→2026.08.25.1 审计始终为 0 add、7 replace、0 delete。生产补丁在包含真实旧脚本和可运行 Python 的最小基线中离线应用成功，7 个新哈希与候选一致，版本标记为 2026.08.25.1，使用本地 channel 查询状态为 `current`。
- Release tooling fix: 发现 `test-release-gates.ps1` 的 `-ValidateOnly` 会覆盖 `release/dist/backend-update` 正式审计输出。发布脚本新增 `-BackendOutputRoot`，测试改用独立临时目录，并显式清零成功后的 native 退出码；5/5 复验通过且正式审计文件哈希保持不变。
- Verification: 插件与单文件 CLI 构建成功（0 错误、2 个既有 CA1416）；EXE 报告 1.0.9，预发布产物 17,471,989 字节、SHA-256 `03c4738e21bb767db3681f6421dd69a4ab73e893e5f7b22a2e00181b162f4385`，stable 内容一致且无旧上游字段。自更新 6/6、后端事务 6/6、发布门禁 5/5、顺序包装器 6/6、Python 语法和 `git diff --check` 全部通过。
- Git/release: GitHub 尚无 v1.0.9。本轮期间用户另行将 `release/发布流程.md` 的一键发布前置条件补充为先提交到 GitHub，已识别为相关改动并保留；当前等待统一提交推送后发布。

### 2026-08-25 11:58 - Codex

- Release: `e6f5344 release: 1.0.9 updater and backend channel` 已推送到 `fork/main`，注释标签 v1.0.9 已推送；GitHub Release `https://github.com/maxzrb/VideoEnhancer/releases/tag/v1.0.9` 为正式非草稿/非预发布，正文严格使用三行分类模板，资产仅 EXE 和 `stable.json`。
- Backend publication: 发布脚本第三次完成完整包解压/逐文件核对后，按 `Backend/python_20260825.7z`→`Backend/patches/2026.08.24.1_to_2026.08.25.1.7z`→`Backend/channel.json` 顺序上传，并在创建 GitHub Release 前回读 channel 成功。ModelScope SDK 文件树确认完整包大小 2,790,829,396、SHA-256 `8c598d90e594a4e3957b48421f780c491cd06c73fb1914600b69950b0a44ab2d`；补丁大小 14,698、SHA-256 `57ac66c15f88f8de90044b26f40a9ab9462018ed0d79efbd332f186f870ccf60`。
- App verification: GitHub Release、ModelScope Releases 和 `VideoEnhancer-Models/Plugin/videoenhancer.exe` 三处 EXE 均为 17,471,986 字节、SHA-256 `ceee62ae201e57efde39955f219d95e565899f8c307955495088c2553a682ddb`。GitHub/ModelScope stable 版本、路径、大小、哈希和 Notes 完全一致，均无旧上游字段。
- Client/channel verification: 用正式 1.0.9 客户端对当前真实 3FUI Backend 目录读取公开 channel，识别安装版本 2026.08.24.1、最新 2026.08.25.1、模式 `patch`、补丁数 1、下载量 14,698 字节，没有误选 2.79 GB 全量包。正式安装 EXE 仍保留 1.0.7，供用户真实验证升级和自动重启。
- Remaining/local: 新后端完整包保留在 `C:\Program portable\3FUI\3FUI\Plugin\python_20260825.7z` 作为已发布资产来源；最小化生产补丁测试目录位于 `%TEMP%\videoenhancer-backend-production-test-20bf217bd6e74cdba05f6be664851fc7`，仅含少量测试文件。发布记录更新后需提交推送并确认工作树干净。

### 2026-08-25 12:27 - Codex

- Objective/root cause: 用户在 1.0.9 模型下载页应用 14,698 字节 Backend 补丁时收到“后端仍在运行”。进程证据显示宿主启动的 `videoenhancer.exe --check -backend tensorrt` 正在通过子 Python 执行 `validate_tensorrt_engines.py`，并加载 3840x2160 RIFE Engine；不是下载或补丁校验失败。该自检子进程也可能是 1.0.7 更新时 EXE 共享冲突的诱因之一。
- Fix: `RunCheck(verbose)` 只保留 FFmpeg、Python、后端脚本、库导入、模型目录和后端版本等轻量检查，不再隐式执行全部 TensorRT Engine 反序列化；显式 `--validate-engines` 和实际推理时按需校验保持不变。插件新增自检取消令牌与任务跟踪，在本体自更新、Backend 更新、插件停用和控件销毁前终止自检进程树并等待退出；CLI 对真实视频任务的后端占用门禁没有放宽。
- Verification: CLI Release 构建成功（0 错误、2 个既有 CA1416 警告）；插件构建成功；后端事务测试 6/6；`git diff --check` 通过。将候选 EXE 临时放入真实 Plugin 根目录执行 `--check -backend tensorrt`，36 个 TensorRT 模型可发现，基础检查退出码 0、耗时约 1.27 秒，输出不含 Engine 反序列化；显式 `--validate-engines` 代码入口仍存在。
- Files/Git: 修改 `cli/Program.cs`、`VideoEnhancerPlugin/PluginPanel.vb`、`docs/codex/STATUS.md`、`version/工作进度.md`。版本仍为 1.0.9，未提交、未推送、未发布；建议审核后作为 1.0.10 提交。测试副本 `C:\Program portable\3FUI\3FUI\Plugin\videoenhancer-check-test.exe` 已退出且不参与运行，但当前执行策略拒绝删除该二进制，需后续手动清理。

### 2026-08-25 13:12 - Codex

- Release: 按用户授权将修复升级为 1.0.10；发布分支 `release/1.0.10` 提交 `a7220fb release: 1.0.10 lightweight environment check`，随后快进合并并推送 `fork/main`。GitHub Release `https://github.com/maxzrb/VideoEnhancer/releases/tag/v1.0.10` 已创建，正式非草稿/非预发布；标签和 main 均指向 `a7220fb`，正文严格为三条 `[更改]`。
- Backend gate: 从已发布 `python_20260825.7z` 重新解压 29,707 文件、6,261,268,671 字节作为基线，与当前候选 Backend 逐文件审计；结果 0 add、0 replace、0 delete，版本两端均为 2026.08.25.1，因此本次没有制作或上传新后端包、补丁或 channel。
- Verification: CLI/插件构建通过（0 错误、2 个既有 CA1416）；顺序包装器 6/6、自更新器 success/transient-lock/tamper/invalid-package/rollback/restart-on-failure、Backend 事务 6/6、发布门禁 5/5 均通过。自更新测试脚本会继承故意失败样例的 native 退出码 1，但最终 PASS 哨兵完整输出，其余脚本退出 0。
- Remote readback: GitHub、ModelScope Releases `releases/1.0.10/VideoEnhancer-1.0.10-win-x64.exe` 和 ModelScope Models `Plugin/videoenhancer.exe` 均为 17,473,022 字节、SHA-256 `8f890047b20344fac530a98f8bc01adfc8c80624787b57a67da77dd5618ce37c`；GitHub/ModelScope `stable.json` 均为 555 字节、SHA-256 `d54d5564e3ea78bc5fb7b0fd17fc12284f43713f101ecd4c1ac957384ebc5272`。
- Migration/local: 1.0.9 尚无更新前取消自检逻辑，首次升级 1.0.10 时若旧自检仍运行，应等待其退出再点更新；进入 1.0.10 后由新生命周期管理解决。临时完整基线保留于 `%TEMP%\videoenhancer-backend-base-20260825-release-1010`（约 6.26 GB），测试 EXE 保留于 `C:\Program portable\3FUI\3FUI\Plugin\videoenhancer-check-test.exe`，当前执行策略拒绝删除，需手动清理。

### 2026-08-25 13:47 - Codex

- Objective/correction: 用户在另一台电脑应用 Backend 补丁时报告 `GMFSS.py` 旧文件 SHA-256 不匹配，并指出 UI 没有完整包下载按钮。此前对当前本机 GMFSS 文件所做的哈希比较不能解释另一台电脑的状态，已明确排除为故障结论；可确认的是远端电脑文件不符合补丁声明的旧哈希，事务拒绝覆盖和回滚正确，但 1.0.10 缺少用户可选的全量恢复路径。
- CLI/protocol: 新增 `--update-backend --force-backend-full`，即使状态原本选择增量也会跳过补丁并下载/校验/事务安装 channel 完整包；`--backend-status --json` 新增 `fullSize`。补丁解压后发生旧哈希冲突或健康检查失败时输出结构化 `BACKEND_FULL_REQUIRED|`，同时保留原始错误详情。
- Plugin/UI: Backend 条目保存完整包大小和强制完整修复状态。增量失败且收到结构化信号后，大小列切换为完整包大小，状态为“增量补丁不适用”，操作为“下载完整修复包”；点击时显示完整包大小、替换范围与回滚说明，默认选择“否”，只有用户确认才传入强制完整修复参数。
- Tests/docs: `release/test-backend-updater.ps1` 增加 `fullSize`、冲突修复信号以及“状态存在补丁但用户强制完整包”的端到端断言；原 6 场景全部通过。CLI Release 构建、插件构建、单文件 publish、帮助文本与 `git diff --check` 通过，仅有 2 个既有 CA1416 警告。更新 `cli/README.md` 和发布流程测试覆盖说明。
- Git/release: 修改 `cli/Program.cs`、`VideoEnhancerPlugin/PluginPanel.vb`、`release/test-backend-updater.ps1`、`cli/README.md`、`release/发布流程.md` 及两份 HandShake 记录。当前版本仍为 1.0.10，未提交、未推送、未发布；建议作为 1.0.11 发布。Backend 文件本身没有改动，不应制作新后端版本或重复上传 2.79 GB 包。

### 2026-08-25 14:09 - Codex

- Objective: 按用户授权提交并正式发布 1.0.11，提供 Backend 增量冲突后的完整修复入口。
- Git/release: 功能提交 `d859f7b` 已由 `release/1.0.11` 快进合并并推送到 `fork/main`；标签 `v1.0.11` 与远端 main 均指向该提交。GitHub Release `https://github.com/maxzrb/VideoEnhancer/releases/tag/v1.0.11` 已创建，正文严格使用逐行 `[新增]`、`[更改]` 模板。
- Assets: GitHub、ModelScope Releases 和 ModelScope Models 的 EXE 均为 17,473,820 字节，SHA-256 `cdfb7f38688778e332da15566e88a98680ebc662624d49d52e0b1f19fbb4027f`；GitHub/ModelScope `stable.json` 均为 539 字节，SHA-256 `e9372cc5a9440a95268fa9fa6b76ae485f7d9bb1981d23b0596b6fd22df9a93c`。
- Backend: 2026.08.25.1 发布基线与当前候选审计为 0 add/replace/delete，因此未制作或上传新完整包、补丁和 channel。完整修复会先验证并暂存新包，再将旧 `python` 后端整目录移入事务备份，切换成功后清理备份，失败或中断则恢复；不会在旧目录上覆盖遗留脚本。
- Verification: CLI/插件构建、单文件 publish、顺序处理 6/6、自更新六类场景、Backend 事务 6/6、发布门禁 5/5 通过；正式发布资产已从 GitHub 和 ModelScope SDK 回读核对。下一步仅需用户在发生冲突的另一台电脑更新至 1.0.11 后验证“下载完整修复包”交互。
- Git status: 发布记录将在本条之后单独提交并推送；除记录提交外应保持工作树干净。

### 2026-08-25 17:28 - Codex

- Objective/layout: 为全新安装与旧版升级实现固定布局：`Plugin\videoenhancer.3fui.dll` 保留在插件根目录，EXE、`bin`、`models`、`python` 和 Backend 更新状态迁入 `Plugin\videoenhancer`。安装与更新共用可恢复事务，写入移动意图后再操作，支持占用重试、失败回滚和进程中断恢复；未知插件文件不移动。
- Real migration: 在 `C:\Program portable\3FUI\3FUI` 完成真实迁移。迁移前后 47,676 个受管文件、24,989,634,005 字节的完整 SHA-256 清单一致，0 missing、0 extra、0 changed；配置自动改写为嵌套 EXE，3FUI 插件页显示新路径并通过环境检测。首次关闭宿主的 PowerShell 辅助命令误用了只读 `$Host` 变量，未按预期关闭窗口，但迁移当时没有 VideoEnhancer/Python/FFmpeg 任务且事务成功；随后使用 `$hostProcess` 正常关闭并重启，单实例验证通过。
- GPU matrix: 动态枚举实际安装目录中的 129 个超分和 22 个补帧模型，组合沿用代表方案，共 679 项。先三路后按用户要求五路并行，最终 677 PASS、2 `SKIP_OOM`、0 失败；跳过项仅为 FlashVSR+GIMM 两种顺序在 6GB RTX 3060 上 OOM。测试发现并修复 ONNX `1x3xHxW...-2x` 与 TensorRT 缓存尺寸导致的倍率误判；最终候选对 3 个行为变化模型及两个 `fix1/fix2` 防回归模型全部实机通过。
- Verification: 正式构建两次完成 Backend `UNCHANGED` 审计；安装器场景全过（fresh/migration/transient lock/injected rollback/permanent lock），本体更新器场景全过（migration/transient/tamper/invalid/rollback/interruption/restart），Backend 事务 6/6、RVE 有序管线 6/6、发布门禁 5/5、Python 语法和 `git diff --check` 通过。最终实机 EXE 与正式资产哈希一致，`--version` 为 1.1.0，`--check` 全部通过。
- Backend decision: 实机目录相对 2026.08.25.1 干净基线仅多运行时 `.pyc`、Triton 缓存、本地状态和完整包，不属于发布内容；Backend 源码/依赖不变，因此版本保持 2026.08.25.1，不生成空补丁、不重复上传约 2.79GB 全量包。
- Files/Git: 修改安装布局、插件路径解析、发布脚本/门禁、GPU runner、README、发布说明与版本记录；新增 `cli/ApplicationLayoutManager.cs`、`release/test-installer.ps1`。当前 `main` 基于 `f3e5566`，工作树只有本次预期改动，尚未提交/推送/发布；下一步创建发布提交并发布 v1.1.0。

### 2026-08-25 18:34 - Codex

- Authorization/release: 用户明确授权在不升版本号的前提下覆盖刚发布的 1.1.0。功能提交 `46d7a02` 已推送，远端 `main` 与强制移动后的 `v1.1.0` 均指向该提交；GitHub Release 保持正式版且仍只有 EXE、`stable.json` 两个资产，正文为 7 条逐行 `[新增]` / `[更改]` 模板。
- UI fix: `PluginPanel.vb` 将 Backend 当前状态缩短为“已是最新”，操作列改为“无需操作”，避免窄状态列触发双行项高而产生视觉上浮。正式更新器部署到真实 `C:\Program portable\3FUI\3FUI\Plugin` 后，EXE/DLL 哈希与候选一致；真实 3FUI 模型下载页截图确认两段文字垂直居中。
- Attribution: 根 README 新增模型来源与致谢表，覆盖当前 129 个超分与 22 个补帧可选条目所属的全部模型家族，列出原作者、原项目/可追溯来源和授权边界；无法确认原始正式发布页的 BHI、RealHatGAN 等条目明确标为待核实。README 中 27 个外部链接全部返回 HTTP 200。
- Verification: Backend 2026.08.25.1 干净基线与真实安装目录审计为 0 add/replace/delete；插件/CLI 正式构建通过，安装器五类场景和更新器七类场景全部通过，实机 `--check` 全通过。覆盖后 GitHub、ModelScope Releases、ModelScope Models 三处 EXE 均为 17,549,975 字节、SHA-256 `6420df26aac81dd10285e5e79c7d561928ec15d9a259c7a67f97d842d943d3c0`；GitHub/ModelScope `stable.json` 均为 962 字节、SHA-256 `ee32dd5492f1851076c351c2a21375385e0d74fdf40a107c31ea5597991260ae`。未制作或上传 Backend 包、补丁或 channel。
- Git status: 发布记录将在本条后单独提交并推送；完成后应保持工作树干净。后续正式发布不应再次覆盖同一版本，除非用户再次明确授权。

### 2026-08-25 20:16 - Codex

- Objective: 接入本地模型能力清单，修复用户的 RealHatGAN ONNX `Reshape` 非整除尺寸错误，并移除选择补帧倍率后要求手动指定帧率的提示窗；随后按用户要求调查全部现有模型的尺寸依赖。
- Capability catalog: 新增嵌入式 `cli/model-capabilities.json` 与 trim-safe `ModelCapabilityCatalog.cs`，包含 93 个唯一模型 ID，覆盖实际清单 ncnn/cuda/tensorrt/onnx/flashvsr/basicvsrpp 的 21/42/36/28/1/1 项且 0 missing。倍率解析对已知模型优先查询清单，未知自定义模型保留保守文件名回退；TensorRT 缓存名可映射回源 PTH 条目。
- Size research: 28 个 ONNX 文件完成图输入检查与 32x32、35x33、50x47 推理探针；6 个 SwinIR 是固定输入并由现有静态分块处理，18 个动态模型接受奇数尺寸，只有 4 个 RealHatGAN 要求宽高为 16 的倍数。42 个 PTH 权重完成 35x33/50x47 探针；确认 AnimeSR 为 4 倍数，`APISR-RRDB 2x` 与两个 `AniScale2-ESRGAN 2x` 为 2 倍数，其他架构在 float32 下无固有尺寸倍数问题。RIFE/GMFSS/GIMM 与 NCNN RIFE 源码已确认内置 32/64 补边裁剪。
- Backend fix: CLI 首次运行保守修补 RVE `UpscaleONNX.py` 与 `UpscaleTorch.py`。动态 ONNX 和 PyTorch 对清单声明的倍数补边，推理后裁回原始倍率尺寸；TensorRT Engine 构建尺寸同步向上对齐。RealHatGAN 35x33、50x47 CUDA 输出精确通过；AnimeSR 35x33 得到 140x132，RRDB 2x 得到 70x66。1920x1080 RealHatGAN 在固定 512/256 分块时于 6GB RTX 3060 OOM，实验性 128 分块输出 3840x2160、耗时 94.625 秒；用户明确要求能力清单和默认值不得记录该机器安全值，因此 `preferredTileSize` 已完全移除，默认继续跟随后端自动/0。
- UI: `PluginPanel.vb` 的倍率选择事件只保存 `InterpFactor`，不再弹出“前往视频参数-画面帧手动指定帧率”；相关旧提示字符串已不存在。
- Verification: CLI Release build 和单文件 trimmed publish 均成功，仅 2 个既有 Windows CA1416 警告；能力清单 unittest 3/3、JSON 解析、Python `py_compile`、`git diff --ignore-space-at-eol --check` 均通过。插件构建未完成：先前使用的 `%TEMP%\FFmpegFreeUI.6.1.39.extracted` 已被清理，本机当前找不到编译所需 `FFmpegFreeUI.dll` 与 `LakeUI.dll`；源码改动简单且已静态核对，但不能声称 DLL 已验证。
- Files/Git: 修改 `VideoEnhancerPlugin/PluginPanel.vb`、`cli/Program.cs`、`cli/VideoEnhancer.csproj`、`docs/codex/STATUS.md`、`version/工作进度.md`；新增 `cli/ModelCapabilityCatalog.cs`、`cli/model-capabilities.json`、`cli/tests/test_model_capabilities.py`。分支 `main` 基于 `26c795c`，未提交、未推送、未发布，版本仍为 1.1.0。

### 2026-08-26 01:57 - Codex

- Objective: 按用户授权准备正式发布 1.1.1，整合模型导入、能力清单、二级菜单、尺寸兼容和 ONNX 分块修复。
- Backend: 相对 2026.08.25.1 为 1 新增、2 替换、0 删除，版本递增至 2026.08.26.1；完整包含 29,708 个受管文件、6,261,278,626 字节，压缩后 3,070,513,730 字节，已独立解压逐文件复验；增量补丁含 3 项操作。
- Verification: CLI/插件/单文件构建通过；Python/契约测试 16/16、RVE 有序管线 6/6、Backend 事务 6/6、发布门禁 5/5、安装器五类和更新器七类场景全部通过。本地安装 EXE/DLL 与正式候选一致，已导入 ONNX 用户模型可出现在 ONNX 模型目录中。
- Git/release: 当前待创建发布提交、推送 main/tag，并上传 Backend、GitHub 与 ModelScope 资产。

### 2026-08-26 12:00 - Codex

- Release: 功能提交 `1a57c7f release: 1.1.1 model import and capability catalog` 已推送，`v1.1.1` GitHub Release 已创建；Backend 2026.08.26.1 按完整包、增量包、channel 顺序上传。远端 channel 的 latest/full/patch 路径、大小和 SHA-256 与本地审计全部一致。
- Assets: GitHub Release、ModelScope Releases 和 ModelScope Models 备用 EXE 已同步。发布后发现 LakeUI `ModernComboBox.Editable` 未显式关闭，导致模型下拉框可以输入；统一配置入口已设为 `Editable=False`，从而让模型、后端、倍率、分块等选项型下拉框与 3FUI 行为一致。
- Verification: 热修复插件构建成功；新单文件 EXE 报告 1.1.1，安装器 fresh/legacy/transient-lock/injected-rollback/permanent-lock 场景通过。覆盖资产为 16,873,792 字节、SHA-256 `fd9adfcc8428a6a3309d52ed866b8ed3bb5ccba6754adb58e164b5b0d6bd51af`；GitHub 摘要和 ModelScope 强制回读一致。
- Local: 真实 3FUI 进程 PID 12692 正在占用插件 DLL，已启动隐藏等待助手 PID 6980；宿主退出后自动替换本机 DLL 与 EXE，下次启动生效。后续只需做一次真实界面只读交互确认。

### 2026-08-26 12:34 - Codex

- Correction/root cause: 用户截图证明 12:00 的首轮热修复没有生效。直接反编译已安装插件后发现 `ConfigureCombo` 前段新增的 `Editable=False` 在同一函数后段被既有 `Editable=True` 再次覆盖；截图显示的 `Ch)` 是 `CUDA (PyTorch)` 文本获得编辑焦点后横向滚动到末尾的结果。此前关于“已修复”的结论错误。
- Fix: 删除前段重复赋值，把原有最终赋值从 `True` 改为 `False`。插件 DLL 与内嵌 DLL 的 1.1.1 EXE 已重新构建；反编译最终成品及本机安装 DLL 的 `ConfigureCombo` 均确认只有一条 `combo.Editable = false`，不存在后续反向覆盖。
- Publication/local: 1.1.1 GitHub Release、ModelScope Releases 和 ModelScope Models 备用 EXE 已再次覆盖。最终 EXE 为 16,873,786 字节、SHA-256 `bed5db6baa5d318d3e54fb8b8fa1a95165ef5114df5c481ce9e083594f63b1b7`；GitHub 摘要一致。本机当时已完全退出 3FUI，因此 DLL/EXE 已直接替换且与构建产物哈希一致。

### 2026-08-26 13:10 - Codex

- UI root cause/fix: LakeUI `ModernContextMenu.Show(Control, Point)` 会正确转换屏幕坐标，但菜单总高度超过锚点下方空间时会向上翻转；若翻转后 Top 为负则钳制到工作区顶端，因此架构列表看起来从屏幕顶端展开。新增 `FitModelMenuBelowAnchor`，按当前屏幕工作区、锚点、DPI 和架构项数量动态压缩根菜单行高，优先确保菜单从模型框下方展开；子模型菜单保持原尺寸。
- Updater safety: 旧流程只在下载前询问一次，下载校验后直接 `StartUpdate` 并 `Application.Exit`。现改为两阶段确认：第一次只同意下载；校验通过后弹出“确认重启安装”警告框，默认按钮为“否”。拒绝时直接返回，不停止自检、不启动更新器、不退出 3FUI；只有明确选择“是”才继续关闭与重启。
- Verification/publication: 插件与内嵌 DLL 的 1.1.1 EXE 构建成功，仅两个既有 CA1416 警告；成品反编译确认动态菜单适配在 `Show` 前调用，第二确认位于 `StopEnvironmentCheck`、`StartUpdate`、`Application.Exit` 之前。覆盖 EXE 为 16,874,426 字节、SHA-256 `bda65213b198c57da7e926ea29cc66963c73f2a09e5c2e48236ebe7b2727589a`，三处资产已上传。真实 3FUI PID 9212 正在运行，隐藏助手 PID 33816 等待宿主正常退出后替换本机 DLL/EXE。

### 2026-08-26 17:54 - Codex

- Objective: 按用户要求为视频超分和运动补帧分别加入“半精度推理”LakeUI 开关；默认开启表示 FP16 优先且不兼容时自动回退，关闭表示强制 FP32。两个阶段不得共用一个精度参数，同后端组合不得因此增加 FFV1 中转；同时审计全新 Windows 10 设备的依赖问题。本轮只提交、不推送、不发布。
- Precision pipeline: 配置、队列和图片增强入口分别传递 `-upscale-precision` / `-interp-precision`。同后端两种处理顺序统一走内置有序包装器，在单 Python 进程中分别初始化两个模型并在模型边界转换张量精度；跨后端仍使用原有 FFV1 中转。SwinIR/GRL/GIMM 等已知不兼容路径继续自动固定 FP32；TensorRT 缓存键包含真实精度，强制 FP32 不会复用 FP16 Engine。
- UI: 两个开关分别位于超分/补帧主开关右侧，只在相应模式开启且 CUDA/TensorRT 后端时可操作。首次实机截图暴露“半精度推理”使用小号说明字体且固定宽度不足；改用 11pt 正文标签、按 DPI 实测文字宽度分配至少 108px，并设置 `MiddleCenter`。高 DPI 实机截图确认两行字体、基线、完整文字和间距正确。
- GPU verification: RTX 3060 短样本完成 CUDA 单超强制 FP32、RIFE4.25 单补自动精度、同后端先超后补（超分 auto / 补帧 FP32）和先补后超（补帧 auto / 超分 FP32），输出均为预期尺寸和 7 帧；两种组合日志显示独立精度且没有 FFV1 中间阶段。
- Dependency audit: CLI 单文件为 `net10.0` self-contained/trimmed，不要求用户安装 .NET；插件只引用 3FUI 6.1.39 宿主程序集和 LakeUI。便携后端实测版本为 torch 2.9.0+cu130、TensorRT 10.12.0.36、ONNX Runtime 1.29.0、NCNN 1.0.20250916。新增 Windows x64/1809、MSVCP140 和当前所选后端的轻量导入/设备检查，不加载模型或 Engine；README 记录 VC++ 2015-2022 x64、CUDA 13 驱动 580+ 和 NCNN Vulkan 要求。
- Verification: 插件构建、CLI Release/trimmed 单文件发布和 Python `py_compile` 通过，仅 2 个既有 CA1416；NCNN/CUDA/TensorRT/ONNX/FlashVSR/BasicVSR++ 六类真实依赖检查全部退出 0；安装器五场景、Backend 事务 6/6、发布门禁 5/5、有序包装器 unittest 8/8 和 `git diff --check` 通过。最终候选 DLL SHA-256 `333439cd925c0e4585bca228ddc23ba3a27a382f4551eb47fb9a1b4d299fc386`，EXE SHA-256 `876b772a1889b91f326630b90ed099f6ea4793853a75cd5de7a82f2aefc9be37`。
- Git/local: 分支 `release/1.1.1` 无 tracking upstream，启动时 `git pull` 因此未执行同步；基线为已发布提交 `3b6473e`。真实 3FUI 当前运行中，先前已安装并目视验证同源码 UI 构建，最终候选未在宿主运行时强行覆盖。功能与记录已创建本地提交，未推送、未发布；提交完成后工作树干净。

### 2026-08-26 19:02 - Codex

- Release: 按用户要求升版并发布 1.1.2。`PluginVersion.vb`、`cli/VideoEnhancer.csproj`、README、Release Notes 和版本迭代记录已更新；版本提交为 `a5047c5 release: prepare 1.1.2`，已推送 `fork/main`，远端 `v1.1.2` 标签指向该提交。
- Backend gate: 以本机受管 Python 目录的 `2026.08.26.1` 标记作为基线和候选逐文件审计，结果 `0 add / 0 replace / 0 delete`；没有制作空补丁、完整包或 channel，也没有重复上传约 3 GB 后端包。
- Release assets: GitHub Release `v1.1.2` 已创建为正式、非草稿、非预发布；ModelScope `AerithDream/VideoEnhancer-Releases` 已同步版本化 EXE、stable.json 和 README，`AerithDream/VideoEnhancer-Models/Plugin/videoenhancer.exe` 已同步备用 EXE。EXE 16,878,865 bytes，SHA-256 `da4bbd96d2f21792c1b85abb1c185e2366e766b7131d8e8f2cc2379abb754605`；stable.json 718 bytes，SHA-256 `cecb2247cf80e8ca75f8bca9817007ef5680fe79ade58c589bb8b4065bb4b55a`。GitHub 资产摘要和 ModelScope stable.json 完全一致，模型页备用 EXE HEAD 返回 200/16,878,865 bytes。
- Verification: 正式脚本重新完成插件/CLI 构建；2 个既有 CA1416 警告、无错误；安装器五场景、更新器 7 场景、Backend 事务 6/6、发布门禁 5/5、有序管线 8/8 和 `git diff --check` 通过。脚本中更新器故意注入的冲突/占用错误均属于预期测试输出，最终 PASS 哨兵完整出现。
- Git/local: 当前分支 `release/1.1.2`，`fork/main` 和 `v1.1.2` 均指向 `a5047c5`；工作树在记录更新前干净，发布生成目录由 Git 忽略。当前 3FUI 进程仍可能占用本机安装 DLL，未强制关闭用户进程；用户升级时由正常更新流程处理。

### 2026-08-27 15:28 - Codex

- Objective: 同步远端最新独立主线，并保留此前本地 OpenModelDB 兼容审计记录。
- Orientation: 已读取 `AGENTS.md`、`CLAUDE.md`、`docs/codex/INDEX.md`、`docs/codex/STATUS.md` 及 HandShake 启动/冲突/收尾规则；本次为 Codex 继续现有项目会话。
- Git sync: 首次 `git pull --ff-only` 已抓取 `fork/main` 的 `30bf203`，但因本地两份记录和未跟踪审计文件未提交而中止；随后创建 `stash@{0}` 保护本地内容，快进 `26c795c..30bf203` 成功，再恢复审计文件和进度记录。未合并原作者 `origin`，未执行破坏性 Git 操作。
- Remote changes: 同步 7 个远端提交，包含 1.1.1/1.1.2 发布记录、半精度控制及相关模型能力/用户模型目录更新；当前 `HEAD` 与 `fork/main` 均为 `30bf203`。
- Preserved files: `docs/openmodeldb-rve-support-audit.md` 已保留；`docs/codex/STATUS.md` 与 `version/工作进度.md` 已合并本地审计摘要和本次同步记录。恢复过程中出现的 `.baiduyun.uploading.cfg` 为同步软件临时文件，复核结束时未残留。
- Verification: 已通过 `git status --short --branch`、`git branch --show-current`、`git log -1 --oneline --decorate` 核对分支、工作树和提交；本轮未运行构建或业务测试，因为仅进行 Git 同步和文档记录整理。
- Git status: `main` 与 `fork/main` 已同步；当前仅审计文件和两份 HandShake 记录待提交。临时保护 stash 在确认记录完整后应删除；提交前建议执行 `git diff --check`。
- Next: 审核并提交审计文件及两份记录；实现阶段继续按审计报告处理 `.safetensors/.ckpt` 发现、元数据预检和 19 个 ONNX-only 候选。

### 2026-08-27 15:54 - Codex

- Objective: 按用户反馈优化模型菜单空白区域，并为内置模型补充悬浮简介。
- Changes: `VideoEnhancerPlugin/PluginPanel.vb` 将一级 `ModernContextMenu` 的 `IconSize` 设为 `0`，二级模型菜单保留 `IconSize=24` 和 `CheckMarkSize`；新增按模型架构/名称生成简介、倍率和支持后端信息的提示文本。
- LakeUI integration: 当前 `ModernContextMenu.ModernMenuItem` 没有公开 Tooltip 属性，因此使用 LakeUI `FloatingToolTipForm`；控制器按悬停的 `MenuPopupForm`、私有项目索引和项目区域显示对应模型提示，菜单关闭或插件销毁时释放。
- Verification: `VideoEnhancerPlugin\build.ps1 -HostBin C:\Users\maxzr\AppData\Local\Temp\FFmpegFreeUI.6.1.39.extracted -SkipInstall` 成功；成品 DLL 探针确认一级 `IconSize=0`、二级 `IconSize=24`/`CheckMarkSize=10`，AniSD 内置项提示文本完整，提示控制器构造/关闭成功；`git diff --check` 通过。
- Files: 修改 `VideoEnhancerPlugin/PluginPanel.vb`，并更新 `docs/codex/STATUS.md`、`version/工作进度.md`；项目版本与发布资产未变化。
- Remaining: 尚未在真实 3FUI 中目视确认一级菜单左移、二级勾选、提示位置和菜单切换时的隐藏行为；当前 `main` 与 `fork/main` 均为 `30bf203`，工作树包含本轮源码及记录待提交。
- Next: 用户实机回归通过后提交；若 LakeUI 升级，重新核对 `MenuPopupForm` 的悬停索引/区域成员。

### 2026-08-27 16:05 - Codex

- Objective: 按用户要求将模型菜单布局与悬浮提示改动放入本机 3FUI。
- Deployment: 已把仓库构建产物 `videoenhancer.3fui.dll` 复制到实际插件目录 `C:\Program portable\3FUI\plugin\videoenhancer.3fui.dll`；根目录中旧的同名 DLL 未作为当前插件目录覆盖目标。
- Safety: 部署前确认没有 `FFmpegFreeUI.exe`/`3FUI.exe` 主进程运行，因此未强制结束用户进程。
- Verification: 部署前源文件为 4,635,136 bytes，部署后目标文件同为 4,635,136 bytes；源/目标 SHA-256 均为 `B49BA31904E911EA04B2A5829B31528DB07D14416BF2AD6A5C5E69CDA2F4C365`。
- Git: `main` 与 `fork/main` 仍为 `30bf203`；工作树仍包含 `VideoEnhancerPlugin/PluginPanel.vb`、OpenModelDB 审计文件和两份 HandShake 记录的未提交改动。部署目标在仓库外，不改变 Git 状态。
- Next: 启动 3FUI，打开超分/补帧模型菜单，确认一级文字左移、二级勾选和模型提示的显示/隐藏；回归通过后审核并提交源码与记录。

### 2026-08-27 16:35 - Codex

- Objective: 按用户反馈完善模型说明、单选下拉框交互和内置使用教程，并把最新版本放入本机 3FUI。
- Model descriptions: 重写 `ModelIntroduction`，按 AnimeJaNai、AniSD、RealESRGAN 各倍率/题材、RealHatGAN 输入约束、Waifu2x 去噪级别、Nomos8k 强弱、AniToon L/S、APISR、RIFE/GMFSS/GIMM 等具体模型或变体给出差异化用途和选择建议；不再使用“Real-ESRGAN 系列按名称区分用途”模板。93/93 个内置能力条目均命中专用规则。
- Combo behavior: 新增 `WheelLockedComboBox`，所有 13 个插件选项框在关闭状态拦截垂直/水平鼠标滚轮消息；LakeUI 独立下拉列表仍可在打开后的列表窗口内滚动。覆盖推理后端、补帧后端、补帧倍率、转场阈值以及其他单选项。
- Tutorial: 将内置“使用教程”改为 5,168 字符的小白手册，逐步说明程序路径、模型下载/导入、后端匹配、模型选择、半精度、分块、补帧倍率、转场阈值、组合顺序、试片和常见故障。
- Verification: `VideoEnhancerPlugin\\build.ps1 -HostBin C:\\Users\\maxzr\\AppData\\Local\\Temp\\FFmpegFreeUI.6.1.39.extracted -SkipInstall` 成功；源 DLL 与实际安装 DLL 均通过反射探针，93/93 简介非空且无旧模板，教程内容完整，滚轮覆盖和 13 个字段类型正确。最新安装 DLL 为 4,658,176 bytes，SHA-256 `5FD9B0B2BF85C7B02BB5EA7C03C927707CF5EC9DD34E6B5DEB19735FF9BEAA0C`。
- Git/local: 部署前确认 `FFmpegFreeUI.exe`/`3FUI.exe` 未运行，已直接覆盖 `C:\\Program portable\\3FUI\\plugin\\videoenhancer.3fui.dll`；`main` 与 `fork/main` 仍为 `30bf203`，工作树包含源码、OpenModelDB 审计文件和两份记录的未提交改动。
- Next: 启动真实 3FUI 做鼠标和教程视觉回归；通过后审核并提交源码与记录，若 LakeUI 升级则重新核对菜单私有布局和滚轮消息行为。

### 2026-08-27 16:57 - Codex

- Objective: 按用户反馈收敛模型提示和教程措辞。
- Changes: 将内置 Markdown 页标题从“小白使用教程”改为“使用教程”；删除模型简介和教程中的短片、试片、几秒素材、A/B 对比等建议，仅保留模型适用条件、参数含义和操作步骤。
- Verification: 重新执行插件构建；已安装 DLL 反射探针确认教程标题为 `# 使用教程`，93 个内置模型简介均不含上述措辞，教程长度为 5,116 字符；`git diff --check` 通过。
- Deployment: 最新 DLL 已覆盖 `C:\Program portable\3FUI\plugin\videoenhancer.3fui.dll`，大小 4,657,664 bytes，SHA-256 `FBA27AC1EAA05B4D86BAE3D197B7F7B482AFEE26AFF820DAFF8EA4FCB765441D`；部署前确认 3FUI 主进程未运行。
- Git: `main` 与 `fork/main` 仍为 `30bf203`；工作树包含 `VideoEnhancerPlugin/PluginPanel.vb`、OpenModelDB 审计文件和两份 HandShake 记录的未提交改动。
- Next: 启动真实 3FUI 做一次菜单、滚轮和教程视觉回归；通过后审核并提交源码与记录。

### 2026-08-27 17:42 - Codex

- Objective: 为“模型导入”页补上用户模型删除能力。
- Changes: `UserModelCatalog` 新增路径校验和临时目录回滚的事务式删除；CLI 新增 `--delete-user-model <ID>`；插件列表支持选中后按 `Delete`，右键显示 LakeUI “删除用户模型”菜单，确认后调用 CLI，并刷新用户模型列表、超分目录和补帧目录。导入页提示和插件 README 已同步操作方式，静态契约测试已同步更新。
- Verification: `dotnet build cli\\VideoEnhancer.csproj -c Release --no-restore` 成功（0 错误、2 个既有 CA1416）；插件 `build.ps1 -HostBin C:\\Users\\maxzr\\AppData\\Local\\Temp\\FFmpegFreeUI.6.1.39.extracted -SkipInstall` 成功；全量 Python 测试 18/18；隔离模型目录实际删除后目录不存在、清单为空、再次列出为 `[]`；安装版 `--help` 暴露删除参数，版本为 1.1.2；`git diff --check` 通过。
- Deployment: 执行 `cli\\build.ps1` 发布单文件 CLI，并在确认 3FUI/CLI 进程未运行后覆盖 `C:\\Program portable\\3FUI\\plugin\\videoenhancer.3fui.dll` 与 `C:\\Program portable\\3FUI\\plugin\\videoenhancer\\videoenhancer.exe`。DLL 源/目标 SHA-256 均为 `A23EBFD8A329C11A55C28DEF2BD6C7AC69D74E167D8DECE39836A8C8A58F7A7F`；CLI 源/目标均为 `5F276950F0048755B9198D4C6000190CEB9F2C6EAA4F5142CD1BA2204BBFF4EB`。
- Git: `main` 与 `fork/main` 仍为 `30bf203`；工作树不干净，包含本轮删除功能源码/README/测试和此前的 `PluginPanel.vb`、OpenModelDB 审计文件及 HandShake 记录改动，未提交、未推送。
- Next: 启动真实 3FUI 在模型导入页实测选中用户模型按 Delete、右键删除、取消确认、删除后工作台菜单刷新；确认后建议提交当前源码和记录。

### 2026-08-27 18:15 - Codex

- Objective: 正式发布 1.1.3，纳入用户模型删除、具体模型悬浮说明、关闭下拉框滚轮锁定和面向初学者的使用教程。
- Release commit: 在 `release/1.1.3` 创建并提交 `5a7d309 release: 1.1.3`，先推送 `fork/release/1.1.3`，再将同一提交快进推送到 `fork/main`；远端 `v1.1.3` 标签指向该提交。
- Build and gates: 官方 `release/build-modelscope-release.ps1` 以 Backend 2026.08.26.1 同目录审计，结果 `UNCHANGED`；重新完成插件/CLI 构建、全量 Python 测试 18/18、发布门禁 5/5、安装器五场景和更新器隔离测试，全部通过。构建仅有既有 2 条 CA1416 警告。
- Assets: 发布 EXE 16,877,609 bytes，SHA-256 `16e1728a99a2ed568909bdc44df1c7df7cea7356a4135e7661e57a127859bf50`；stable.json 673 bytes，SHA-256 `0da8e3cb5e49a62b199c048a3ecac41f36d38db539c9cfdffa29c25ad3060276`。GitHub Release `https://github.com/maxzrb/VideoEnhancer/releases/tag/v1.1.3` 为正式、非草稿、非预发布，包含版本 EXE 和 stable.json；ModelScope Releases 与 Models 备用 EXE 均上传成功，稳定 JSON、两处 EXE 的版本/路径/大小/哈希回读一致。
- Git/worktree: 发布过程中本地 `main` 快进曾受百度同步临时占用影响，未终止同步进程；已通过远端快进完成主线发布。当前在 `release/1.1.3`，已开始补最终记录；后续需将记录提交并使本地 `main` 与 `fork/main` 对齐。真实 3FUI 鼠标交互仍待用户确认。

### 2026-09-01 08:40 - Codex

- Objective: 继续执行 LakeUI 5.0 全插件原生控件迁移方案，修复 5.0 GPU 控件表面与透明 WinForms 容器造成的黑块、文字重叠和裁切风险。
- Changes: 新增 `VideoEnhancerPlugin/LakeLayoutPanel.vb`，提供只计算边界、不自行绘制的 `ModernGridPanel` 与 `ModernHorizontalPanel`；`PluginPanel.vb` 六个页面、统一 `ModernPanel1` 背景映射、LakeUI Markdown 教程、`ExcellentProgressBar`、`PixelPictureBox`、模型能力编辑器和 LakeUI `ExMsgBox` 已接入。`QuadGridControls.vb` 删除 `FluentCardPanel`、`FluentProgressBar`、`SmoothButton` 和旧自绘时间轴，视频槽改为 `ModernPanel.Image`，时间轴直接使用 `ExcellentTrackBar`；`QuadGridForm.vb` 的预览、下拉框、数值框、复选框、标签、承载面板和分割线均已迁移。
- Compatibility: `build.ps1` 读取 `LakeUI.dll` 元数据并强制主版本为 5；插件构造时增加运行时门禁，旧版本显示明确升级提示。旧 LakeUI `3.22.0.0` 构建已验证被拒绝。
- Verification: 使用 `C:\Users\maxzr\AppData\Local\Temp\videoenhancer-lakeui5-build-current` 中 `FFmpegFreeUI 6.1.39` 与 LakeUI `5.0.0.0` 构建成功；产物引用 `LakeUI, Version=5.0.0.0`。迁移源码和整个插件静态扫描均无 `Panel`/`TableLayoutPanel`/`FlowLayoutPanel`/`Label`/`PictureBox`/`WebBrowser`/`NumericUpDown`/`CheckBox`/`ComboBox` 等旧可视控件构造及自绘视觉类；CLI Python 测试 18/18；`git diff --check` 通过。
- Runtime scope: 尚未在真实 3FUI 中执行六页 DPI 截图、控件树递归检查、连续 500 帧预览压力和四宫格完整交互；这些属于下一步宿主实机回归，不在当前无 GUI 构建环境中宣称已通过。
- Git: 分支 `release/1.1.2`，远端同步基线保持不变；工作树包含上述 5 个源码/构建文件改动及本次记录，未提交、未推送。建议宿主实测前先创建本地提交。

### 2026-09-01 08:51 - Codex

- Baseline recheck: 从官方 3FUI 6.2.2 x64 包临时提取的新版 `FFmpegFreeUI.dll`（引用 LakeUI 5.1.0.0）也可通过本插件构建；随后重新用 LakeUI 5.0.0.0 基线生成最终忽略产物，引用核对为 `LakeUI, Version=5.0.0.0`。
- Tutorial: 为 LakeUI `MarkDownViewer.LinkClicked` 增加系统默认浏览器打开处理，保留教程链接点击行为；失败时仅忽略打开错误，不影响插件。
- Lifecycle: 预览/四宫格关闭路径先解除控件图片引用，再释放旧位图；四宫格视频槽使用 `ModernPanel.ImageFillMode.Zoom` 保持缩略图比例。
- Verification: LakeUI 5.0 基线构建、静态迁移门禁、旧 3.22 拒绝门禁和 `git diff --check` 均再次通过。

### 2026-09-01 08:57 - Codex

- Final verification: 重新用 LakeUI `5.0.0.0` 构建并核对插件引用；再次以旧 LakeUI `3.22.0.0` 执行构建，收到预期的版本拒绝；静态旧控件门禁与 `git diff --check` 均通过。
- Existing tests: `python -m unittest discover -s cli/tests -p 'test_*.py'` 结果 `Ran 18 tests ... OK`。本轮未修改 CLI、视频处理、模型、配置或队列业务逻辑。
- Git: 当前仍为 `release/1.1.2`，`HEAD` 与 `fork/main` 均为 `6120a8b`；工作树仅含迁移源码、构建门禁和两份状态记录改动，尚未提交或推送。

### 2026-09-01 09:03 - Codex

- Runtime metadata gate: 在加载 LakeUI 5.0 与宿主程序集后递归核对 `PluginPanel`、`QuadGridForm`、`VideoSlotCard`、`ModernGridPanel`、`ModernHorizontalPanel` 类型及其字段，未发现原生 WinForms 可视控件字段；根类型分别为宿主允许的 `UserControl`/`Form` 和 LakeUI `ModernPanel`。
- Final artifact remains built from LakeUI `5.0.0.0`;真实 3FUI GUI 截图、DPI、500 帧和鼠标交互仍需用户宿主执行。

### 2026-09-01 09:07 - Codex

- Closeout checks: 修正静态扫描命令后，迁移源码与整个 `VideoEnhancerPlugin` 均通过旧 WinForms 可视控件/自绘视觉类门禁；`git diff --check` 无错误（仅保留换行格式提示）。
- Git: 工作树仍包含本轮迁移源码、构建门禁和状态记录，尚未提交；真实 3FUI 宿主回归仍是下一步。

### 2026-09-01 09:14 - Codex

- Baseline upgrade: 根据用户要求将基线从 LakeUI 5.0 切换到官方 LakeUI 5.1；运行时门禁、构建脚本提示和 README 依赖说明均改为严格 `5.1.x`。
- Verification: 使用官方 3FUI 6.2.2 宿主与 LakeUI `5.1.0.0` 构建通过，产物引用 `FFmpegFreeUI, Version=6.2.2.0` 和 `LakeUI, Version=5.1.0.0`；LakeUI `5.0.0.0`、`3.22.0.0` 均被构建门禁拒绝；类型元数据门禁通过。
- Runtime scope: 真实 3FUI 截图、DPI、500 帧预览压力及四宫格鼠标回归仍需宿主环境执行；源码和记录尚未提交。

### 2026-09-01 09:16 - Codex

- Final verification: 以 LakeUI `5.1.0.0` / 3FUI 6.2.2 重新构建成功；CLI Python 测试 `18/18` 通过（测试中的预期异常堆栈仍被正确捕获）；全插件旧控件静态门禁和 `git diff --check` 通过。
- Git: 当前分支 `release/1.1.2`，`HEAD` 与 `fork/main` 均为 `6120a8b`；工作树包含本轮源码、README、构建门禁和记录改动，未提交、未推送。

### 2026-09-01 11:35 - Codex

- Baseline/runtime: 实际安装的 3FUI 已自动更新为 `6.2.3`，随附 LakeUI `5.3.0.0`。按用户要求保留 LakeUI `5.1` 为最低基线并允许后续 5.x，重新以实际宿主构建，产物引用核对为 `FFmpegFreeUI 6.2.3.0` / `LakeUI 5.3.0.0`。
- Rendering fixes: LakeUI 5.3 的 `ModernPanel` 构造期间会提前触发布局，`ModernGridPanel`、`ModernHorizontalPanel` 和 `VideoSlotCard` 的布局重写已增加初始化保护；移除 `ModernTabControl.BackgroundSource` 的全树绑定，避免 D3D `RequestRender` 递归导致宿主无响应。页面仍由 `ModernPanel1` 和各内容页分别映射背景。
- Combo text fix: 新增 `LakeComboBox`，在只读组合框选项变化、字体变化和尺寸变化时清零 LakeUI `SingleLineTextBoxRenderer` 的旧 `_scrollXOffset`；同时将补帧后端列从 29% 调整为 34%、收紧内边距。实机截图确认 `TensorRT (NVIDIA)` 上下两处均从首字母完整显示，模型名从 `AnimeJaNai...` 开头显示。
- Real host verification: 覆盖安装到 `C:\Program portable\3FUI\3FUI\Plugin\videoenhancer.3fui.dll`，安装文件 SHA-256 为 `793D869D27682E233E9E532BB6BC013C4AF4154C309874235E16D70D53A781C1`；启动实际 3FUI PID `10456` 后插件可打开，六个标签页逐页点击均保持 `Responding=True`。截图保存在 `%TEMP%\3fui-after-layout-plugin.png` 和 `%TEMP%\3fui-after-layout-plugin-combo-crop.png`。
- Verification: 插件构建成功；旧 WinForms 可视控件静态门禁通过；CLI Python 测试 `18/18` 通过；`git diff --check` 无内容错误，仅报告仓库已有的换行格式/CRLF 提示。尚未宣称 DPI 矩阵、500 帧压力、四宫格完整鼠标流程和控件树递归已完成。
- Git: 分支 `release/1.1.2`，`HEAD` 与 `fork/main` 仍为 `6120a8b`；工作树含未提交源码、README、构建门禁和记录改动。建议在后续回归前创建本地提交，便于回滚实机安装测试。

### 2026-09-01 11:48 - Codex

- Final install: 重新构建并覆盖安装实际插件 DLL；最终安装 SHA-256 为 `BB3E64617A6D4445184ABB5265AF03AAA623D8BF61569A78F6FC75CC7CF6F049`。启动实际 3FUI PID `34320`，插件页可加载。
- Final tab check: 通过鼠标依次点击实时预览、模型下载、模型转换、模型导入、使用教程和超分工作台，六页均返回 `Responding=True`，未复现渲染递归卡死；最终工作台截图为 `%TEMP%\3fui-final-installed-plugin.png`，组合框局部截图为 `%TEMP%\3fui-final-installed-plugin-bottom-combos.png`。
- Combo result: 组合框首字母横向偏移和补帧后端窄列右侧裁切均已修复；两个 TensorRT 下拉框完整显示 `TensorRT (NVIDIA)`，模型下拉框从 `AnimeJaNai...` 开头显示。
- Remaining scope unchanged: 尚未完成 DPI 100%/125%/150% 矩阵、500 帧预览压力、四宫格完整鼠标流程和运行时控件树递归；工作树仍未提交。

### 2026-09-01 11:52 - Codex

- Post-normalization rebuild/install: 清理新增文本的混合换行并保留已有文件的 UTF-8/BOM 与历史换行布局后重新构建并覆盖安装；最终 DLL SHA-256 为 `695E1ADA472755B13AC60404FA579A852CF401C88948E7D4BBE3E65CDC5A8B50`。
- Final real-host pass: 实际 3FUI PID `29648` 启动后插件再次打开，六个标签页逐页点击均 `Responding=True`；最终截图 `%TEMP%\3fui-final-installed-plugin-lf.png` 中上下两个 `TensorRT (NVIDIA)` 均完整可见，模型名从 `AnimeJaNai...` 开头显示。
- `git diff --check` 现已返回退出码 0；仅保留 Git 关于仓库 `core.autocrlf` 将 LF 转为 CRLF 的提示。CLI Python 测试仍为 `18/18`，静态旧控件门禁通过，工作树尚未提交。

### 2026-09-01 11:58 - Codex

- Closeout verification: 最终再次执行旧 WinForms 可视控件静态扫描（PASS）、`python -m unittest discover -s cli/tests -p 'test_*.py'`（18/18，预期异常被测试捕获）和 `git diff --check`（退出码 0）。实际 3FUI PID `29648` 仍为 `Responding=True`。

### 2026-09-01 12:55 - Codex

- Model-column fix: 用户反馈放大模型名称右侧仍被截断；将超分后端/模型列比例从 `46%/54%` 调整为 `38%/62%`，组合框保持左对齐偏移重置和 6px 内边距。当前实机截图已完整显示 `AnimeJaNai-V3-2x-HD-Sharp1-Compact-430K`，两个 `TensorRT (NVIDIA)` 也完整可见。
- Final install: 重新以实际 3FUI 6.2.3 / LakeUI 5.3.0.0 构建并覆盖安装，源文件与实际插件 DLL SHA-256 均为 `DE8AB0FB3C5E2D2D0F5C49127BEFBD0BE5CC827794C1FA5C9ADD05F623433AE3`。截图为 `%TEMP%\3fui-model-width-fix.png`。
- Final runtime check: 实际 3FUI PID `34656` 启动响应正常，插件页可打开；此前六页切换验证保持 `Responding=True`。静态旧控件门禁、CLI Python 18/18 和 `git diff --check` 均通过。
- Remaining scope: 100%/125%/150% DPI 矩阵、500 帧预览压力、四宫格完整鼠标流程和运行时控件树递归仍待后续执行；工作树未提交。

### 2026-09-01 12:56 - Codex

- Latest host pass: 使用包含模型列宽修正的最终 DLL 再次点击六个标签页，实时预览、模型下载、模型转换、模型导入、使用教程和超分工作台全部保持 `Responding=True`。

### 2026-09-01 14:19 - Codex

- LakeUI source research: 克隆并阅读官方 LakeUI 5.3 `ModernPanel.vb` 与 `ModernTabControl.vb`，确认 `ModernTabControl` 会把绑定页加入内部透明内容面板并强制 `Dock=Fill`；`ModernPanel` 的绝对布局仍会经过 WinForms 基类布局，带 `Anchor.Right` 的子控件会恢复创建时缓存的窄宽度。官方 README 的 v5 GPU HWND Swap Chain 说明也要求透明界面使用 LakeUI 原生控件和 `ModernPanel` 背景映射。
- Root layout fix: 超分工作台根 `ModernPanel` 改为 `Dock=None`、仅 `Anchor.Top|Left`，由 `SyncUpscaleRootBounds` 在插件、页面和 TabControl 布局完成后显式提交宽度；保留一次 `BeginInvoke` 补同步，避免布局重入。删除临时宽度 trace 和计时器，不再依赖 `Anchor.Right` 或 `Dock=Top`。
- Real-host verification: 重新构建并覆盖安装 `C:\Program portable\3FUI\3FUI\Plugin\videoenhancer.3fui.dll`；源 DLL、`VideoEnhancerPlugin\out\videoenhancer.dll` 与安装 DLL SHA-256 均为 `D490EEA6CD9804E652A1EA6A57ED9BA7F231A83E7020A083433123DE43FAB6D6`。实际 3FUI 6.2.3 / LakeUI 5.3.0.0 PID `30736` 保持 `Responding=True`，六个标签页依次切换通过；普通窗口连续采样根内容为 `1434x850` 且稳定，最大化/还原/宿主窄化采样均无宽度跳变。前台截图：`%TEMP%\3fui-final-host-foreground.png`。
- Verification: 静态旧 WinForms 控件门禁 PASS；`python -m unittest discover -s cli/tests -p 'test_*.py'` 为 `18/18`；`git diff --check` 退出码 0（仅 core.autocrlf 换行转换提示）。DPI 100%/125%/150% 矩阵、500 帧预览压力、四宫格完整鼠标流程和运行时控件树递归仍未宣称完成。
- Git: 分支 `release/1.1.2`，`HEAD` 与 `fork/main` 仍为 `6120a8b`；工作树包含本轮源码、构建门禁、README 和状态记录改动，未提交、未推送。建议保留本次实机基线后创建本地提交。

### 2026-09-01 14:26 - Codex

- Rebuild/install: 使用实际 3FUI 6.2.3 / LakeUI 5.3.0.0 再次执行 `VideoEnhancerPlugin\build.ps1 -SkipInstall`，构建门禁报告 `LakeUI baseline: 5.3.0.0`；编译器生成的新 DLL 元数据使本次二进制哈希更新为 `BF14C138EBEB86ECF19F1F63589A96AC4E62ED8D439D985C3F3C860F19E44CAB`，`videoenhancer.3fui.dll`、`VideoEnhancerPlugin\out\videoenhancer.dll` 和实机安装文件三者一致。
- Regression record: 根宽度和六页响应已由上一份源码等价构建在真实宿主中完成验证；本次仅因构建元数据变化覆盖安装，未修改 UI/业务逻辑。静态旧控件门禁 PASS、CLI Python 测试 `18/18`、`git diff --check` 退出码 0。
- Git: 分支 `release/1.1.2`，`HEAD` 与 `fork/main` 仍为 `6120a8b`；新增实机 DLL 备份位于宿主目录，不属于仓库。工作树源码和记录仍未提交、未推送。

### 2026-09-02 16:38 - Codex

- 根据实机截图定位插件外沿白线：六个内容页字段均为 `New ModernPanel`，LakeUI 5.x 默认 `BorderSize=1`、`BorderColor=Gray`；页面根面板正好覆盖 TabControl 内容区，因此默认边框被渲染成围绕整个插件内容的亮线。`ModernGridPanel`、`ModernPanel1`、`ModernTabControl.ContentBorderWidth` 和下载列表本身均已显式无边框，问题不在系统窗口边框。
- 在 `PluginPanel.vb` 页面统一样式处为 `_pageUpscale`、`_pagePreview`、`_pageDownloader`、`_pageConverter`、`_pageImporter`、`_pageTutorial` 统一设置 `BorderColor=Transparent`、`BorderSize=0`、`BorderRadius=0`；重新使用实际 3FUI 6.2.3 / LakeUI 5.3.0.0 构建并覆盖安装 `C:\Program portable\3FUI\3FUI\Plugin\videoenhancer.3fui.dll`。
- 构建门禁报告 `LakeUI baseline: 5.3.0.0`，源 DLL 与实机安装 DLL SHA-256 均为 `3FAFC169ED605C403916B2BA00CABF8FF8F7E7FB44FD5AEBC64A8F2C71ED0835`；本轮未启动宿主，待用户下次打开插件确认白线消失。源码和记录尚未提交。

### 2026-09-02 22:25 - Codex

- 按用户授权启动 1.2.0 发布流程：创建本地 `release/1.2.0` 分支；`PluginVersion.Current`、`cli/VideoEnhancer.csproj`、根 README 和 `release/release-notes.txt` 已切换到 1.2.0；1.1.3 记录已移入版本历史。
- 本地检查通过：LakeUI 5.3 插件构建、CLI Release 构建（0 错误，仅既有 Windows CA1416 警告）、旧 WinForms 控件静态门禁 `STATIC_SCAN_OK`、Python 测试 `18/18`、发布门禁 `RELEASE_GATE_TESTS_PASS|5`、Backend 更新事务 `BACKEND_UPDATER_TESTS_PASS|6`、`git diff --check`。
- 认证检查通过：`gh auth status` 为 `maxzrb`，`modelscope whoami` 为 `AerithDream`。远端 Backend channel 当前为 `2026.08.26.1`；本次 UI-only 发布不改变 Backend，候选构建将使用实机同版本目录做零差异审计。
- 尚未生成 1.2.0 双源资产、提交或上传；工作树包含本轮迁移源码、版本源和记录改动。下一步运行正式构建脚本生成 EXE/stable.json 并执行安装器、更新器隔离测试。

### 2026-09-02 22:55 - Codex

- 正式发布完成：提交 `10ed1f2 release: 1.2.0 LakeUI 5.1 migration` 已推送到 `fork/release/1.2.0` 和 `fork/main`；GitHub Release `v1.2.0` 已创建，远端标签指向同一提交。
- 发布脚本使用实际 3FUI 6.2.3 / LakeUI 5.3.0.0 构建，Backend 2026.08.26.1 审计为 0 新增、0 替换、0 删除，未重复上传后端包。安装器 `INSTALLER_TESTS_PASS`、更新器六类场景 PASS、发布门禁 5/5 均通过。
- 双源资产核验：`VideoEnhancer-1.2.0-win-x64.exe` 16,882,467 bytes，SHA-256 `7cd35717ab2e3e268eb64a7ad7507c6655ce656906d4af18547e7a9041c6ec58`；`stable.json` 782 bytes，SHA-256 `add585dc126ca83ba8f9703fb31a9a6796d2f29157232f1c31e69be2beca25e8`。GitHub 下载、ModelScope Releases、ModelScope Models 备用 EXE 均返回 200，三份 EXE 内容哈希一致，两个 stable.json 内容哈希一致；Release 资产仅包含版本 EXE 和 stable.json。
- GitHub Release：`https://github.com/maxzrb/VideoEnhancer/releases/tag/v1.2.0`；ModelScope Releases：`https://www.modelscope.cn/datasets/AerithDream/VideoEnhancer-Releases`；Models 备用路径：`Plugin/videoenhancer.exe`。发布后记录收尾提交为 `f797c78`，已推送到 `fork/release/1.2.0` 和 `fork/main`；DPI/压力/四宫格完整回归仍待后续。

### 2026-09-05 17:25 - Codex

- Startup/sync: 已阅读 `AGENTS.md`、`docs/codex/INDEX.md`、`docs/codex/STATUS.md`，执行 `git pull --ff-only` 并切换到 `main` 后再次同步；当前 `main...fork/main`，基线为 `9402a74`，未使用破坏性 Git 操作。
- LakeUI research: 临时检出官方 LakeUI 5.6（tag `cce294a`）和 FFmpegFreeUI 6.2.9 源码。LakeUI 的 `ModernPanel.TryGetTransparentBackgroundForward` 明确排除滚动面板；V5 表面在几何/位置变化时使背景消费者失效；官方 FFmpegFreeUI 的滚动页面只绑定根 `ModernPanel1`，没有让内部固定内容根再次直连宿主背景。
- Diagnosis/fix: `VideoEnhancerPlugin/PluginPanel.vb` 的 `_pageUpscale` 和 `_upscaleRoot` 原先同时设置 `BackgroundSource = ModernPanel1`。滚轮改变子根位置后，两层独立 GPU 表面分别按宿主坐标裁剪，控件边界处会出现不同采样链的背景断层。已删除 `_upscaleRoot` 的直接宿主映射，保留透明背景，让 LakeUI 自动从滚动页父级表面取样；布局、宽度同步、滚动条和业务逻辑未改动。
- Verification: LakeUI 5.6 构建 0 错误；FFmpegFreeUI 6.2.9 构建 0 错误；插件 `build.ps1 -HostBin <官方 6.2.9 Debug 输出> -SkipInstall` 构建 0 错误；LakeUI.Tests 输出 `LakeUI tests passed.`；CLI Python 测试 `18/18 OK`；插件实例化/反射确认 `PageSource=ModernPanel1`、`RootSource=<null>`、`RootParent=ModernPanel`、`PageScrollMode=Vertical`；`git diff --check` 通过。
- Runtime limitation: 本轮 computer-use 原生管道报“系统找不到指定的文件”，无法自动操作真实 3FUI 完成滚轮截图；未覆盖实际安装目录，也未声称实机视觉回归完成。生成 DLL 位于被 `.gitignore` 忽略的 `VideoEnhancerPlugin/out/videoenhancer.dll` 和根目录构建产物，源码仅修改 `VideoEnhancerPlugin/PluginPanel.vb`。
- Git/next: 工作树包含源码改动和本轮 HandShake 记录，尚未提交、未推送；建议先检查并提交，再关闭实际 3FUI 后部署 DLL 做鼠标滚轮回归。

### 2026-09-05 17:34 - Codex

- Deployment: 确认实际 3FUI 进程未运行后，将 `VideoEnhancerPlugin/out/videoenhancer.dll` 覆盖到 `C:\Program portable\3FUI\plugin\videoenhancer.3fui.dll`；未强制结束任何进程。
- Deployment verification: 源 DLL 与安装 DLL 均为 4,621,312 bytes，SHA-256 均为 `363420DDF7F438568246E781555D271953D6BD9E58291309805DA30D4C67EDEE`。覆盖前的安装 DLL 已备份到 `C:\Users\maxzr\AppData\Local\Temp\3fui-plugin-backup-20260905-173400\videoenhancer.3fui.dll`。
- User test: 已将实机测试入口交给用户；待用户启动 3FUI 后用真实鼠标滚轮检查超分工作台控件与背景之间的断层。尚未把部署视为视觉回归通过。
- Git: `main...fork/main`，基线 `9402a74`；源码 `PluginPanel.vb` 与 HandShake 记录仍未提交、未推送。测试确认后建议提交源码和记录。

### 2026-09-05 17:56 - Codex

- User regression: 用户在实际 3FUI + 最新插件中确认第一版仍有明显背景断层，因此不能把“移除 `_upscaleRoot` 的直接宿主映射”视为完成。
- LakeUI evidence: 官方 5.6 `D3D_ControlSurfaceRegistry.TryDrawAutomaticGpuBackdrop` 明确以 `registerDependency:=False` 调用背景采样；显式 `BackgroundSource` 则在 `TryDrawBackground` 中注册来源和坐标链，`控件几何已变化` 会向所有采样消费者请求重绘。滚轮移动 `_upscaleRoot` 时，自动祖先取景不足以使其子控件表面同步，这是截图所对应的原因。
- Second fix: 新增 `BindScrollableGpuBackgroundSources`，递归遍历超分滚动内容根及其 LakeUI V5 子控件；对尚无显式来源的控件设置 `BackgroundSource = ModernPanel1`，从稳定宿主映射根直接采样并注册坐标依赖。已有显式来源不覆盖；布局、滚动步长、业务逻辑未改动。
- Verification/deployment: `git diff --check` 通过；以官方 FFmpegFreeUI 6.2.9/LakeUI 5.6.0.0 构建插件 0 错误；确认 3FUI/FFmpegFreeUI 未运行后备份并覆盖 `C:\Program portable\3FUI\plugin\videoenhancer.3fui.dll`。源/目标 DLL 均 4,621,312 bytes，SHA-256 `A31B0CB0A0159AB1726424ED4BA6E343E3E987DBFE80E5D6B9DFA3FE6BB81828`；旧 DLL 备份在 `%TEMP%\3fui-plugin-backup-20260905-175631\videoenhancer.3fui.dll`。
- Runtime object check: 未启动宿主实例化第二版插件并遍历 `_upscaleRoot`，结果为 76/76 控件具有 `BackgroundSource` 属性、76/76 已显式绑定、0 个指向其他来源；首次 PowerShell 解析器回调探针因加载资源递归失败，改用纯 .NET 文件解析后核对通过。
- Next: 用户完全退出并重启 3FUI 后，在超分工作台滚动到截图位置来回滚动；未取得用户确认前不宣称视觉回归通过。版本号未变，因此未更新版本迭代记录。
- Git: `main...fork/main`，基线 `9402a74`；`PluginPanel.vb`、`docs/codex/STATUS.md`、`version/工作进度.md` 均未提交、未推送。

### 2026-09-05 18:18 - Codex

- User regression: 用户确认第二版已修好滚动背景断层，但反馈放大模型交互框在自定义菜单出现前会短暂显示浅灰蓝色、带当前模型名称的覆盖框；图 2 是期望状态。
- LakeUI evidence: 官方 5.6 `ModernComboBox` 的 `DropDownAnimationDuration` 默认值为 300ms；Overlay 模式在选中项区域绘制当前模型文本和 `DropDownSelectedColor`。插件的 `DropDownOpened` 处理器随后调用 `DroppedDown=False`，再打开自定义 `ModernContextMenu`，因此原生 Overlay 关闭动画会在锚点上短暂可见。
- Fix: 新增 `ConfigureModelSelector`，在复用通用组合框样式后只将 `_cmbModel` 和 `_cmbInterp` 的 `DropDownAnimationDuration` 设为 0；普通后端、倍率、阈值等原生组合框仍保留 LakeUI 默认动画。
- Verification: `git pull --ff-only` 已同步；插件以官方 FFmpegFreeUI 6.2.9/LakeUI 5.6.0.0 构建 0 错误；`git diff --check` 通过；无宿主实例化核对显示两个模型框动画值均为 0、下拉模式仍为 Overlay。
- Deployment: 确认 3FUI/FFmpegFreeUI 未运行后备份并覆盖 `C:\Program portable\3FUI\plugin\videoenhancer.3fui.dll`。源/目标 DLL 均 4,621,312 bytes，SHA-256 `456D3E08DABC36AFACC9B9072561860B9BB916FDFCE250EF1C7EB345CDC644E0`；旧 DLL 备份在 `%TEMP%\3fui-plugin-backup-20260905-181755\videoenhancer.3fui.dll`。
- Post-deployment state: 最终只读检查发现用户随后启动了 `FFmpegFreeUI` PID `35744`；未强制结束进程、未再次覆盖正在使用的 DLL。用户需退出并重新启动宿主以明确加载第三版。
- Next: 用户重启 3FUI，点击放大模型/补帧模型确认图 2 样式，并再次滚动确认背景断层未回退；未取得用户确认前不宣称视觉回归完成。版本号未变，因此未更新版本迭代记录。
- Git: `main...fork/main`，基线 `9402a74`；`PluginPanel.vb`、`docs/codex/STATUS.md`、`version/工作进度.md` 均未提交、未推送。

### 2026-09-05 18:43 - Codex

- Commit split: 滚动背景修复已独立提交为 `c5e9be8 fix: repair LakeUI scrolling background dependencies`；模型选择框 Overlay 修复已独立提交为 `fad5bff fix: suppress model selector overlay animation`。两个修复没有混入版本发布提交。
- Version prep: `PluginVersion.Current`、`cli/VideoEnhancer.csproj`、根 README 和 `release/release-notes.txt` 已统一为 1.2.1；1.2.0 已移入版本历史，1.2.1 记录已建立。
- Build: 使用 `pwsh 7.6.4` 执行官方 `release/build-modelscope-release.ps1`，因为 Windows PowerShell 5.1 会错误解析仓库无 BOM UTF-8 中文脚本；Backend 2026.08.26.1 同目录基线/候选审计为 `0 add / 0 replace / 0 delete`；LakeUI 5.6 插件构建 0 错误；CLI 发布并由 EXE `--version` 报告 1.2.1。
- Candidate assets: `VideoEnhancer-1.2.1-win-x64.exe` 16,865,524 bytes，SHA-256 `c67c8dffd05f8b52ab3ad3c657e51048c922677bca3afea89a2ad71f7a3ee005`；stable.json 518 bytes，SHA-256 `e80955ea69aba4b19dad68725c74d3f5cb961f34276117447ab40aca82730433`；Release Notes 格式校验通过。
- Verification: 安装器 `INSTALLER_TESTS_PASS` 6 场景、更新器 `PASS` 7 场景、发布门禁 `RELEASE_GATE_TESTS_PASS|5`、Backend 事务 `BACKEND_UPDATER_TESTS_PASS|6`、`python -m unittest discover -s cli/tests -p 'test_*.py' -v` 为 18/18、`git diff --check` 均通过。发布文档列出的四个历史 Python 文件在当前仓库不存在，未将该旧路径的 py_compile 命令计为通过；本次未修改 Python 工具。
- Next: 提交并推送 1.2.1 发布准备，创建 GitHub `v1.2.1`，同步 ModelScope Releases 与 Models 备用 EXE；上传后回读两处 stable.json、EXE 和 Release Notes，并补充最终发布记录。
- Git: 当前 `main` 相对 `fork/main` ahead 2（两个独立修复 commit）；版本/发布记录仍未提交，候选 dist 及构建 DLL/EXE 均被 `.gitignore` 排除。

### 2026-09-05 18:47 - Codex

- Publish commit: `bcb4e42 release: 1.2.1 UI fixes` 已推送到 `fork/main`；前置两个修复仍保持独立提交 `c5e9be8`、`fad5bff`。
- GitHub: 已创建正式、非草稿、非预发布 `v1.2.1`，资产仅为版本 EXE 与 stable.json；Release Notes 与本地文件逐行一致。
- ModelScope: `AerithDream/VideoEnhancer-Releases` 已同步 1.2.1 目录、stable.json、README 和 Release Notes；`AerithDream/VideoEnhancer-Models/Plugin/videoenhancer.exe` 已同步备用 EXE，上传均无失败。
- Cross-source readback: GitHub stable.json、ModelScope stable.json 与本地清单均为 version `1.2.1`、package size `16865524`、SHA-256 `c67c8dffd05f8b52ab3ad3c657e51048c922677bca3afea89a2ad71f7a3ee005`；GitHub EXE、ModelScope Releases EXE、Models 备用 EXE 下载回读三者大小和哈希一致；ModelScope README 的 GitHub 首选/稳定清单兜底说明通过。
- Post-release: 3FUI 当前仍可能加载发布前部署的第三版 DLL，用户需完全退出并重启后实测模型菜单动画和滚动背景；发布资产本身无需重新生成。
- Git: 收尾文档尚未提交；当前应新增一个纯文档 closeout commit，推送后保持 `main...fork/main` 一致并确认工作树干净。

### 2026-09-12 09:55 - Codex

- Scope: 按用户要求仅在本地仓库和指定 3FUI 目录开发，不执行任何 Git 命令，不创建提交、标签或发布资产。
- RTX implementation: 新增 `RtxVideoBackendClient`，按 `RTXHDR-RTXVSR` 本地 HTTP 协议启动 sidecar、检查 D3D11/RTX SDK/VSR/TrueHDR/NVENC 能力、提交/轮询/取消任务。RTX VSR 支持 1x/1.5x/2x/3x/4x 与 1080p/1440p/2160p/4320p 映射，倍率限制 1-4，最终边长取偶数；与补帧组合时固定先补帧。RTX HDR 位于最后阶段，常规超分/补帧通过 RGB FFV1 无损中间视频衔接，已是 PQ/HLG 的输入拒绝重复映射。
- Plugin/UI: 超分工作台增加 HDR 映射区；RTX 后端使用专用输出规格/质量控件并与模型/分块控件互斥显示；内容高度从 850 调整为 970，后续行整体下移避免重叠。图片输入栏增加“移除所有”。顶部增加“右键超分”页，可配置当前用户 `SystemFileAssociations\image` 的“超分辨率 → 模型”级联菜单，提供应用与移除按钮，输出固定 PNG。
- Image backend: `rve-image-backend.py` 新增 FlashVSR/BasicVSR++ 图片路径：构造 21/4 帧重复 FFV1 视频、执行对应时序后端、提取第一帧；小于 64 像素的输入先补边再按原尺寸裁切。错误信息包含源图片、失败阶段、退出码和后端尾部日志。
- Runtime: 从 `Zennmn/RTXHDR-RTXVSR` v1.0.1 便携包把 sidecar、FFmpeg 运行库、`nvngx_vsr.dll`、`nvngx_truehdr.dll` 和许可/NOTICE 文件放入 `C:\PortableSoft\FFmpegFreeUI ReadyToRun x64\plugin\videoenhancer\bin\rtx-video`。公开分发这些 NVIDIA 文件前必须重新核对许可。
- Verification: LakeUI 5.9 和 FFmpegFreeUI 6.2.16 官方源码本地构建成功；插件类型检查/构建成功；CLI 构建成功，仅保留既有 Windows CA1416 分析警告。RTX 4070 Laptop 实跑 RTX VSR 320×180→640×360；RTX HDR 为 HEVC `yuv420p10le`、BT.2020/PQ；BasicVSR++ 图片 64×48→256×192；FlashVSR 图片 64×64→256×256；Python 脚本语法通过。
- Version: `PluginVersion.Current`、CLI 工程、README、Release Notes 和版本迭代记录已切换为 1.3.0 开发中；未发布。
- Remaining: 重新以最终 1.3.0 源码构建 DLL/单文件 EXE，部署到指定目录并在真实 3FUI 检查页面布局；运行现有自动测试。Git 状态因用户禁令未知。

### 2026-09-12 10:03 - Codex

- Closeout: 移除“右键超分”页在控件句柄创建前调用 `BeginInvoke` 的风险点，改为仅在切入该标签页时加载模型；用 LakeUI 5.9 / FFmpegFreeUI 6.2.16 重新构建插件。
- Deployment: 最终产物已覆盖到 `C:\PortableSoft\FFmpegFreeUI ReadyToRun x64`。EXE 为 16,906,285 bytes、SHA-256 `F687EBFAB5C1BE65BCE3159D80B86782B6D675632F519DC66A5324AD721FD713`；DLL 为 4,632,576 bytes、SHA-256 `27D130D0F89BBDCFA1C16526C5B2F1804B388DE5449FD680BB1C920AA1BBE425`，源/目标哈希分别一致。覆盖前备份位于 `C:\Users\ARXChem\AppData\Local\Temp\videoenhancer-1.3.0-backup-20260912-095800`。
- Final verification: CLI Release 构建 0 错误（仅 2 个既有 CA1416 Windows 平台分析警告）、Python 单元测试 18/18、安装版 `--version` 为 1.3.0、RTX VSR / RTX Video HDR 环境探测全部通过；用户手动确认新增真实 3FUI UI 无问题。
- Side effects/Git: “应用设置”未代替用户点击，因此本轮没有主动写入资源管理器右键菜单；用户可在“右键超分”页选择模型后自行应用。本轮始终没有执行 Git 命令，分支和工作树状态未知，也没有提交、推送或发布。

### 2026-09-12 10:47 - Codex

- Follow-up UI: HDR“处理方式”下拉保持可点击，即使当前只有 RTXHDR；“右键超分”模型表增加逐项删除，同时保留“移除所有”和显式“应用设置/移除右键菜单”。
- Segmented upscale: 顶栏新增“分段超分”页，从 3FUI 当前添加文件列表识别视频并用 ffprobe 读取精确帧数；每个视频独立保存总开关与分段。首帧/尾帧锁定，内部边界联动，新增/删除后仍连续覆盖全部帧，模型选择限定为 NCNN/CUDA/TensorRT/ONNX 单帧算法。第一段会锁定后端类别和倍率，后续段只显示兼容模型；FlashVSR、BasicVSR++、RTX VSR 等流式/时序路径不进入列表。
- Runtime pipeline: 新增嵌入式 `rve-segmented-backend.py`，单次 FFmpeg 解码逐帧分发到各段模型，再交给同一 FFmpeg 编码并映射原音频/字幕。因为同一视频流必须保持固定尺寸，除后端类别外也锁定第一段倍率。分段任务当前明确拒绝与运动补帧、RTX HDR 以及 PQ/HLG 输入组合。
- Feasibility/verification: 两帧验证视频分别使用 AnimeJaNai 2x 和 RealESRGAN AnimeVideoV3 2x，源码构建、trimmed publish 与安装版 EXE 均成功输出 256×256、2 帧视频；重复/缺口边界与混合倍率均按预期拒绝。CLI/插件重新构建通过，Python 全量测试 22/22。
- Deployment: 用户退出 3FUI 后覆盖最终文件。EXE 源/目标均为 16,919,192 bytes、SHA-256 `2E75BF83CEFF8B24A9228EBD61876923E7C30AF4FE4D8B417006C53726B0C9A8`；DLL 源/目标均为 4,654,592 bytes、SHA-256 `9F1BF2328BACAB8A7A86C7F3318BD3543DBB0655BE71C348F86785B9AF889FCD`；分段后端源/目标 SHA-256 均为 `2B972FDC115518EE8C3BCCD0552E23DDD062F3E6AA00A2D39E07948BB9BF9C41`。安装版仍报告 1.3.0，部署后未代为启动宿主。
- Backup/Git: 覆盖前备份位于 `%LocalAppData%\Temp\videoenhancer-1.3.0-segment-backup-20260912-103000`。本轮没有执行任何 Git 命令，未提交、推送或发布。

### 2026-09-12 12:55 - Codex

- Repository relationship: 确认 `user-Wing/VideoEnhancer` 是父仓库，`maxzrb/VideoEnhancer` 是当前主要维护 fork。本地基线与 `maxzrb/main` 一致；同步前两边 main 已分叉，maxzrb 独有 62 个提交、user-Wing 独有 5 个提交。
- Commit/PR: 将 1.3.0 更新提交为 `9551296 feat: add RTX and segmented upscale workflows`，推送至 `user-Wing/VideoEnhancer:feat/rtx-segmented-upscale-1.3.0`；向 `maxzrb/VideoEnhancer:main` 创建 PR #1：`https://github.com/maxzrb/VideoEnhancer/pull/1`。
- Direct user-Wing update: 为避免强推覆盖 user-Wing 的独有历史，以功能提交和旧 `user-Wing/main` 为双父创建 `becc929 merge: sync user-Wing with maxzrb 1.3.0 maintenance line`。合并树与功能提交逐字节一致，随后从 `cbfda2f` 快进推送到 `user-Wing/main`；双方历史均保持可达，当前文件内容以 maxzrb 维护线为准。
- Scope: Git 提交仅包含 18 个本次功能/测试/文档文件；未包含 EXE、DLL、INI、Python 缓存、临时验证素材或安装目录文件。版本保持 1.3.0，未创建标签、Release 或发布资产。

### 2026-09-12 13:14 - Codex

- Startup/sync: 按项目要求读取 `AGENTS.md`、`docs/codex/INDEX.md`、`docs/codex/STATUS.md` 和 HandShake skill；执行 `git pull --ff-only` 成功。起始分支 `release/1.2.2` 工作树干净，随后未合并地获取 PR #1。
- PR checkout: PR 标题为 `feat: add RTX and segmented upscale workflows`，base 为 `maxzrb/main@1a475e9`，head 为 `user-Wing/feat/rtx-segmented-upscale-1.3.0@00946ce`；建立本地 `pr/1-rtx-segmented-upscale`，跟踪 `origin/feat/rtx-segmented-upscale-1.3.0`，没有使用 PR merge ref 或执行合并。
- Backend input: 核验 `C:\Users\maxzr\Downloads\backend更新.zip`，SHA-256 为 `74B0377597EBCC9925026D4D24C82A2D9BF1DA96E71E36C550D0783B99993DF8`；其中 `rve-image-backend.py`（16,461 bytes）和 `rve-segmented-backend.py`（9,654 bytes）与 PR 内嵌脚本逐字节一致。
- Verification: `python -m unittest discover -s cli/tests -p 'test_*.py' -v` 为 `22/22`；PR `git diff --check` 通过；`pwsh VideoEnhancerPlugin/build.ps1 -HostBin C:\Users\maxzr\AppData\Local\Temp\3fui-core-compat-host -SkipInstall` 成功，LakeUI baseline `5.9.0.0`；`pwsh cli/build.ps1` 成功，只有既有 CA1416 Windows 平台警告；CLI `--version` 为 `1.3.0`。
- Build artifacts: 插件 DLL 与 `VideoEnhancerPlugin/out/videoenhancer.dll` 均为 4,654,592 bytes、SHA-256 `FEC4117758409897530AED7EAF499572C9D63A60274CF10730E31196BC072DA3`；CLI EXE 与 `cli/.publish/videoenhancer.exe` 均为 16,919,258 bytes、SHA-256 `C8657D85C694088F7215E36D3FE0AD7E03EB452EB140BF369FD3151B68A617B6`。
- Deployment: 确认 `FFmpegFreeUI`、`3FUI` 和 `videoenhancer` 进程均未运行；覆盖 `C:\Program portable\3FUI\3FUI\Plugin\videoenhancer.3fui.dll`、`Plugin\videoenhancer\videoenhancer.exe` 及两个后端脚本。初次复制时目标哈希与构建/ZIP 源一致；安装版 `--version` 启动同步后仅将两个脚本的 LF 行尾改为 CRLF，UTF-8 解码内容一致。覆盖前备份位于 `C:\Users\maxzr\AppData\Local\Temp\3fui-pr1-backup-20260912-131309`。
- Runtime limit: PR1 分支不包含此前兼容性工作分支的 `HostCompatibility` 回归项目，本轮未声称该项通过；未启动真实 3FUI，也未声称分段页、RTX 页面或真实视频任务视觉/功能回归通过。
- Git/next: 源码分支仍未合并 PR；HandShake 本次记录和中文进度记录产生待提交文档改动，构建产物被忽略。下一步由用户重启 3FUI 做本地测试；测试结果确认后再决定是否提交、合并或回滚。

### 2026-09-12 13:40 - Codex

- UI regression diagnosis: 对照 PR1 与 `6bc7b3a` 旧版本确认，PR1 的 `CreateOfficialValueBox` 删除了 `HtmlColorLabel.BackgroundSource = box`，导致 `_lblExe` 等文件/路径显示标签直接采样宿主背景，恢复该绑定以修复框内空心。RTX HDR 状态提示原本使用固定 112px 状态列，`RTX Video HDR` 在截图中换行；为 `BuildOfficialModeHeader` 增加可选状态列宽，HDR 调用使用 150px，并保留 `MiddleRight`、清空 Padding。
- Changed file: `VideoEnhancerPlugin/PluginPanel.vb`。
- Build: 执行 `pwsh VideoEnhancerPlugin/build.ps1 -HostBin C:\Users\maxzr\AppData\Local\Temp\3fui-core-compat-host -SkipInstall`，LakeUI baseline `5.9.0.0`，构建成功；Python 测试 `22/22`，`git diff --check` 通过，源码断言确认背景绑定、HDR 宽度和右对齐设置存在。
- Deployment: 确认宿主及 CLI 未运行后，覆盖 `C:\Program portable\3FUI\3FUI\Plugin\videoenhancer.3fui.dll`。构建源和安装目标均为 4,654,592 bytes、SHA-256 `358939CF211853B03A29C4584BC2C57C59B0F40150566535088780DC672CA8B1`；旧 DLL 备份位于 `C:\Users\maxzr\AppData\Local\Temp\3fui-pr1-ui-fix-backup-20260912-133939`。
- Runtime limit: 未启动真实 3FUI，因此尚未声称文件框底色和 RTX HDR 单行提示的视觉回归通过；未合并、未推送、未发布。
- Git/next: 当前分支 `pr/1-rtx-segmented-upscale` 仍基于 `00946ce`；工作树含 `PluginPanel.vb` 以及 HandShake 两份记录的未提交修改。下一步用户重启 3FUI 核对两个截图问题，确认后再决定提交和 PR 合并。

### 2026-09-12 14:04 - Codex

- Follow-up diagnosis: 用户反馈 150px 状态列仍然换行。对照 LakeUI `HtmlColorLabel` 源码确认其 HTML 排版路径固定按可绘制单元换行，控件没有可用的 `NoWrap` 属性，因此仅增加宽度不能保证单行。
- Source fix: `PluginPanel.vb` 将 `_lblSwitchRtxHdr` 改为透明的 LakeUI `ModernButton` 文本控件，设置 `TextAlign=Right`、无内边距、无边框、无渐变/水波纹/长按交互、180px 状态列；ModernButton 的 `DrawText` 默认 `wordWrap=False`。RTX HDR 开启/关闭状态改为普通文本和 `ForeColor`，其他状态标签保持原有 `HtmlColorLabel` 实现。
- Build/deployment: `pwsh VideoEnhancerPlugin/build.ps1 -HostBin C:\Users\maxzr\AppData\Local\Temp\3fui-core-compat-host -SkipInstall` 成功，LakeUI baseline `5.9.0.0`；Python 测试 `22/22`、源码断言和 `git diff --check` 通过。确认宿主进程未运行后覆盖安装 `C:\Program portable\3FUI\3FUI\Plugin\videoenhancer.3fui.dll`，源/目标 SHA-256 均为 `9ECC9B444321C08BED63BC63B618AEEAB530A3EF71476AC29B38ACC72A906555`。
- Backup/runtime: 覆盖前 DLL 备份位于 `C:\Users\maxzr\AppData\Local\Temp\3fui-pr1-ui-fix-backup-20260912-135714`。已启动真实 3FUI 主程序做宿主检查，但未自动进入插件页，故不宣称两个视觉问题已确认；随后测试宿主已关闭。PR 仍未合并、未推送、未发布。
- Git/next: 当前分支仍为 `pr/1-rtx-segmented-upscale`，工作树有 `PluginPanel.vb`、`docs/codex/STATUS.md`、`version/工作进度.md` 未提交修改；下一步用户完全重启 3FUI，进入 VideoEnhancer 插件页确认文件框底色和 RTX HDR 单行提示。完成本轮后建议考虑 Git 提交，再继续 PR 决策。

### 2026-09-12 14:49 - ZCode

- Startup/orientation: 按 `AGENTS.md` 读取 `INDEX.md` 与 `STATUS.md`；`git pull` 显示已最新；分支 `pr/1-rtx-segmented-upscale`，工作树含上一轮 UI 修复与记录的未提交改动。用户确认两处 UI 视觉问题已修复。
- Backend intake: 核验 `C:\Users\maxzr\Downloads\python-20260912.7z` = 3,086,044,296 bytes，SHA-256 `DBC365B74E6D8AC039DFEAF218644202EAA7568118CA2320975A6358285BA78C`。解压到临时暂存区（32,637 文件 / 6.0 GB）后与换出前安装做 blake2b 全树对比：content_diff 2,898 个中非缓存仅 8 个（标记 JSON 时间戳、3 个 triton 内核、2 个 inspect 脚本与 2 个 rve 脚本的行尾差异，内容一致）；only_installed 5,021 个全部为 `__pycache__` .pyc、triton 缓存和旧 `python_20260824.7z`。代码内容与原环境一致，torch 2.9.0+cu130 / Python 3.12.9 / onnxruntime 1.29.0 相同。
- Swap: 确认 3FUI/FFmpegFreeUI/videoenhancer/python 无进程后，旧 `Plugin\videoenhancer\python` 整目录移动备份至 `C:\Users\maxzr\AppData\Local\Temp\rve-backend-prev-20260912-144723\python`，新后端自暂存区整目录移入，暂存目录删除。models、bin、插件 DLL/EXE 未改动。
- Marker decision: 包内 `.videoenhancer-backend.json` 仍为 2026.08.26.1（打包方未更新版本号），本轮未擅自改号以免与未来发布编号冲突；`--backend-status` 因此仍报 current/2026.08.26.1，不会触发更新提示。
- Verification: 嵌入式 Python 3.12.9 启动正常；`import torch` 2.9.0+cu130 且 `torch.cuda.is_available()=True`；onnxruntime 1.29.0；安装版 `videoenhancer.exe --version`=1.3.0（启动同步未改变包内 rve 脚本哈希）；`--backend-status --json` state=current；仓库 Python 测试 `22/22`。
- Scope/Git: 按用户要求仅本地替换用于测试，未使用发布脚本，未上传任何远端，未合并 PR，未创建标签/Release。工作树仍为 `PluginPanel.vb` + 两份记录的未提交改动。
- Next: 用户在真实 3FUI 继续实测新后端；之后决定标记版本号、提交与 PR 合并。

### 2026-09-12 15:01 - ZCode

- User report: 真实 3FUI 中 RTX 环境检查报 `[缺失] RTX Video sidecar ... vsr_backend.exe`，退出码 1，用户询问与后端换入的关系。
- Diagnosis: 后端换入仅替换 `Plugin\videoenhancer\python`（Python 推理环境）；RTX VSR/HDR 依赖独立的 `bin\rtx-video\vsr_backend.exe` HTTP sidecar（RTXHDR-RTXVSR v1.0.1 便携包组件 + nvngx DLL + FFmpeg 运行库），不在 Python 包内。本机安装 `bin` 下只有 PortableGit/ffmpeg/mkvtoolnix，`bin\rtx-video` 从未存在；`C:\PortableSoft` 旧安装不在本机；全盘（C 盘 5 层深度）未找到任何 `vsr_backend.exe`。此前 sidecar 只部署在另一环境（`C:\PortableSoft\FFmpegFreeUI ReadyToRun x64`）。环境检查报缺失并退出 1 属正确行为，与后端换入无关。
- Note: 本机 `torch.cuda.is_available()=True`（有 NVIDIA GPU），sidecar 部署后 RTX 路径应可用。CLI 接受 `rtx-video\vsr_backend.exe` 或 `rtx-video\runtime\vsr_backend.exe` 两种布局（`RtxVideoBackendClient.FindBackend`）。
- Next: 等用户从原机器拷贝 `bin\rtx-video` 整目录，或重新提供 RTXHDR-RTXVSR v1.0.1 便携包后按原布局部署。本轮无文件修改，仓库状态不变。

### 2026-09-12 15:17 - ZCode

- Package intake: 用户提供 `C:\Users\maxzr\Downloads\RTX.Video.Converter_1.0.2_x64-portable.zip`（116,754,076 bytes，SHA-256 `03547ACE397A8B0408F1C8E8AE361B8FFD00AA1F32566BB3A5E3D22D0403F248`），内含 `runtime\vsr_backend.exe`（2026-09-12 打包）、`nvngx_vsr.dll`、`nvngx_truehdr.dll`、7 个 FFmpeg 共享库 DLL 与 `THIRD_PARTY_LICENSES\`。仅提取 `runtime\*` 与 `THIRD_PARTY_LICENSES\*` 到 `C:\Program portable\3FUI\3FUI\Plugin\videoenhancer\bin\rtx-video\`（对应 CLI `FindBackend` 第二候选布局），未部署转换器 GUI 部分。
- First check: `--check -backend rtxvsr` 显示 sidecar/D3D11/SDK 文件通过，但 RTX SDK 运行时初始化失败且三个 NVENC 路径全部不可用。本机 GPU 为 RTX 3060 Laptop（驱动 616.92，支持 VSR/HDR），三症状同向指向 Optimus 双显卡下 sidecar 把 D3D11 设备建在核显。
- Fix: 按 Windows 标准机制为 `vsr_backend.exe` 写入用户级 GPU 偏好：`HKCU\Software\Microsoft\DirectX\UserGpuPreferences`，值名 `C:\Program portable\3FUI\3FUI\Plugin\videoenhancer\bin\rtx-video\runtime\vsr_backend.exe`，值 `GpuPreference=2;`（等同系统设置→显示→显卡中指定高性能；删除该值即可回滚）。重跑检查全部通过；av1_nvenc 不可用属 RTX 3060（Ampere）无 AV1 编码硬件的正常表现。
- Real-run verification: testsrc2 320×180/1s 输入，RTX VSR 2x 输出 640×360 h264（30 帧）；RTX HDR 1x 输出 HEVC `yuv420p10le`、`bt2020nc`/`smpte2084`/`bt2020`，与 1.3.0 在原机器上的验证记录一致。临时测试素材已清理。
- Scope/Git: 仅本地安装目录新增 `bin\rtx-video` 与一条 HKCU 注册表偏好；未改动仓库代码、DLL/EXE、models、python 环境；未发布、未合并。工作树仍为 `PluginPanel.vb` + 两份记录的未提交修改。
- Next: 用户在插件页重跑 RTX 环境检查应全部通过（无需重启宿主）；继续实测其他后端后决定提交与 PR 合并。

### 2026-09-12 15:28 - ZCode

- User report: 真实 3FUI 任务（杜比全景声F1测试.m2ts，rtxvsr 2x 质量四 + RTX HDR）失败：`FFmpeg could not write the MP4 header.：Experimental feature`。用户询问是否因 VSR+HDR 同时开启。
- Diagnosis: 不是。RTX 流水线中 sidecar 先把结果写入临时 MP4（`CreateJobJson` 硬编码 `container: "mp4"`、`audioMode: "copy"`），成功后才由 `CommitRtxOutput` 重封装为用户目标容器。ffprobe 确认该 m2ts 含 TrueHD+Dolby Atmos 音轨；FFmpeg 将 TrueHD 封装进 MP4 视为实验性特性，sidecar 内部 FFmpeg 因此写头失败。
- Verification: 用 bundled ffmpeg 生成含 TrueHD 音轨的测试输入，CLI 同参数完整复现同一错误；E-AC3 Atmos 输入与无音轨输入对照均成功。同时确认 RTX 路径的最终重封装固定 `-map 0 -c copy`，用户 ffmpeg-settings 中的 `-map/-c:a` 参数只影响 `SelectRtxCodec` 编码器选择，不参与最终封装。
- Workaround/fix options: 当前设置层面无解（audioMode 硬编码）。可选修复：CLI 探测到 TrueHD 等与 MP4 不兼容音轨时，先剥离音频交给 sidecar，最终重封装时从原输入按用户设置混回音轨/字幕（`Program.cs` RTX 分支 + `CommitRtxOutput`）。待用户决定是否实现。
- Scope/Git: 仅临时测试文件（已清理），仓库与安装目录无改动。

### 2026-09-12 15:41 - ZCode

- User direction: 用户询问能否改掉或绕过 RTX 临时 MP4，倾向 MKV 中转（不喜欢 MP4 容器）；要求先做可行性验证再实现。
- MKV feasibility: 反汇编级字符串扫描 + 直连 sidecar HTTP 实测确认不可行。`vsr_backend.exe` 明确返回 `unsupported_container: "The first backend release writes MP4 output."`，`container` 仅支持 mp4；该组件为闭源二进制，无法修改。注意 `videoCodec` 需传通用名（h264/hevc/av1），而非 ffmpeg 编码器名（hevc_nvenc 会被 `unsupported_video_codec` 拒绝）；CLI `SelectRtxCodec` 已做该映射。
- Bypass validated: 手动驱动 sidecar 实测通过——TrueHD 输入 + `audioMode: "none"`（sidecar 原生支持的选项）→ 临时 MP4 仅含 hevc 640×360 视频；再用 bundled ffmpeg `-map 0:v:0 -map 1:a? -map 1:s? -c copy` 与原输入混流 → 最终 .mkv 含 hevc 视频 + TrueHD 音轨原样保留。
- Planned implementation (await user approval): CLI 探测 `rveInput` 音轨，命中 MP4 不兼容编码（truehd/mlp/pcm_bluray/pcm_dvd）时任务 JSON 改用 `audioMode: "none"`；`CommitRtxOutput` 增加"从原输入混回音轨/字幕"分支（mkv 输出 `-c copy`；mp4 输出音轨转 AAC 并提示，位图字幕跳过）。改动集中在 `Program.cs` RTX 分支、`CreateJobJson`、`CommitRtxOutput`。
- Scope/Git: 本轮仅实验（临时文件已清理、实验 sidecar 已关停），仓库与安装目录无改动。

### 2026-09-12 15:58 - ZCode

- User context: 插件的核心诉求就是让 3FUI 参数控制编码；RTX 路径参数被 sidecar 固定，用户询问是否让 RTX Video Converter 保持独立软件。
- Source-verified facts (Zennmn/RTXHDR-RTXVSR，MIT 许可的原生源码，NVIDIA SDK 组件仍按 NVIDIA 许可)：任务 JSON 无任何编码器选项字段；NVENC 参数在 `ffmpeg_transcode_pipeline.cpp` 硬编码为 preset p7、tune hq、rc vbr、cq 18.0（h264/hevc）/24.0（AV1，`nvenc_policy.h`）、spatial-aq 1、rc-lookahead 32、multipass 2（全分辨率）。该固定组合质量高于用户惯用的 cq 28。CLI 的 `SelectRtxCodec` 仅映射编码器家族。独立 GUI 版的编码参数同样写死，独立使用并不带来更多控制。
- Upstream bug mechanism confirmed: 音频/字幕 skip 策略用 `avformat_query_codec(FF_COMPLIANCE_NORMAL)`，TrueHD 在 MP4 有封装标签被放行，movenc 写头时才按实验性合规拒绝 → 整个任务失败而非优雅跳过音轨。可向上游提 issue/PR。
- Build feasibility: 本机无 MSVC/vcpkg（仅 .NET 工具链）。上游依赖轻（cpp-httplib、nlohmann-json、gtest）且有 windows-ci.yml；自建还需 NVIDIA RTX Video SDK（专有，不可再分发）与最小 LGPL FFmpeg（上游提供构建脚本）。
- Options presented to user: A) 向上游提 issue/PR 请求参数透传+TrueHD 修复（成本最低，作者活跃，今天刚发 v1.0.2）；B) fork 自建 sidecar（满足完全控制，但需搭建 C++/vcpkg/NVIDIA SDK 构建链并长期 rebase）；C) 维持现状（RTX 固定高画质，参数控制覆盖常规后端）。推荐 A 与 TrueHD CLI 绕过修复并行，视上游响应再决定是否 B。

### 2026-09-12 17:42 - ZCode

- User direction: 用户明确插件核心诉求是 3FUI 端码率/文件大小与画质的精细控制，拒绝维持 sidecar 固定参数；随后要求本地构建（工具链写入环境变量复用）、废除 mp4 中转、mkv 兜底。
- Local build chain (persisted for reuse): VS BuildTools 18（MSVC 14.51.36231，`C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools`），用户 PATH 追加 cmake/ninja/MSVC bin；用户环境变量 `FFMPEG_ROOT=C:\Codex Program\RTXHDR-RTXVSR-artifacts\ffmpeg-root`、`CMAKE_TLS_VERIFY=0`（代理吊销检查失败所需）。FFmpeg dev 布局：头文件取自 v1.0.2 发布页对应源码包（commit a09be9b，avutil 61/avcodec 63 与 3FUI 运行时 DLL 匹配），导入库用 dumpbin/lib.exe 从 `bin\rtx-video\runtime` 的 DLL 生成（初版 .def 误抓 RVA 列已修正），`python\Lib` 外依赖走 CMake FetchContent，无需 vcpkg。NVIDIA RTX Video SDK 1.1.0 直链下载解压至 `C:\Codex Program\RTX_Video_SDK_v1.1.0`（`-DVSR_RTX_SDK_ROOT` 指定）。CMake 配置 `-DVSR_ENABLE_FFMPEG=ON -DVSR_ENABLE_RTX_SDK=ON`。
- Sidecar fork patch (`C:\Codex Program\RTXHDR-RTXVSR` 分支 `rve-patches` 提交 `a9e63d9`，5 文件 +130/-12)：`job_types.h` OutputSettings 增加 `encoder_options` 并在 validate_request 白名单校验（preset/tune/rc/cq/qp/b:v/maxrate/bufsize/spatial-aq/temporal-aq/aq-strength/rc-lookahead/multipass/g）；`json_dto.cpp` 解析 `output.encoderOptions`（字符串/数值/布尔均收）；`ffmpeg_transcode_pipeline.cpp` 默认参数后应用覆盖（非法值降级为日志跳过）、容器选择支持 mkv/matroska、TrueHD/MLP 实验性门禁仅作用于 mov/mp4 系封装器、m2ts 音轨 TrueHD 补 sample_rate=48000、参数不完整音频流跳过并告警。单测新增 2 例（encoderOptions 解析/拒绝未知选项、muxer 兼容性含 matroska TrueHD），85/85 通过。
- CLI patch: `RtxVideoBackendClient` CreateJobJson/RunAsync 增加 container/audioMode/encoderOptions 参数并透传 job warnings；`Program.cs` 新增 `ParseRtxEncoderOptions`（剔除 `:v:0` 流后缀、白名单映射）、`ProbeStreamTypes`；RTX 分支重构：.mkv 目标 sidecar 直写最终文件（sidecar 内部 TemporaryOutputCleanupGuard 失败清理 + 成功才 replace，崩溃安全），非 mkv 后缀一律 mkv 临时文件 + `CommitRtxOutput` 无损重封装（mp4 目标 TrueHD→AAC 256k、位图字幕省略，均有提示），sidecar 警告以 `[RTX Video] 警告：` 透传。曾修复两处自伤 bug：直写路径误入 finally 清理删除成品、ffprobe csv 字段顺序按位置无关解析。
- Verification: 真实 F1 m2ts 5 秒剪辑（h264+TrueHD+AC3）经安装版 CLI：mkv 直写输出 hevc 3840×2160 10bit PQ + TrueHD 原音轨；cq 20/28/45 输出 505KB/约180KB/104KB 量级差异证明码率控制生效；mp4 目标走 mkv 兜底转 AAC 256k；上游单测 85/85、仓库 Python 22/22、CLI 0 错误（仅既有 CA1416 警告）。
- Deployment: 覆盖 `Plugin\videoenhancer\videoenhancer.exe`（SHA-256 `de46a44f7b8dd97de94df6746736c55eaa9f200f21c74afbbb70598d121e390d`）与 `bin\rtx-video\runtime\vsr_backend.exe`（SHA-256 `02b94a54291620f3fb1d2a59f78d64d9cbae8533c493844395fbc7219c2104dd`），旧文件备份于 `C:\Users\maxzr\AppData\Local\Temp\rtx-patch-deploy-20260912-170117`。nvngx/FFmpeg 运行库保持 1.0.2 包原件未覆盖。
- Known behavior notes: 经查随包 FFmpeg 源码 nvenc_hevc.c，`tune uhq`（Ultra high quality，NV_ENC_TUNING_INFO_ULTRA_HIGH_QUALITY）为合法值并原样生效，此前"不合法/降级"的说法有误已更正；m2ts 的 ac3 副轨因 demux 元数据不完整（0 声道）被跳过并告警，主 TrueHD 轨正常；Optimus 双显卡下新 exe 路径需 GPU 偏好（已为构建/部署路径写入）。
- Git/next: 仓库工作树 5 个未提交修改（PluginPanel.vb、cli/Program.cs、cli/RtxVideoBackendClient.cs、两份记录），建议尽快提交；sidecar 补丁建议整理后向上游提 PR；用户继续真实任务实测。

### 2026-09-12 18:05 - ZCode

- User direction: 最终架构定为"直连最终容器"——3FUI 输出什么后缀就用什么容器，不做 mkv 兜底与无损搬运；容器装不下的特性直接透出 FFmpeg 原生报错。
- Architecture change: sidecar `container:"auto"`（或任意值）时按最终输出文件后缀 `av_guess_format` 选择封装器（mkv/mp4/webm/avi/ts 等全部 FFmpeg 封装器），validate_request 移除容器白名单；写头失败信息改为携带真实容器名（"could not write the webm header..."）；`ffmpeg_muxer_supports_copy` 回归纯 `avformat_query_codec` 语义（移除我此前加的 TrueHD/mp4 门禁，TrueHD 进 MP4 时由 movenc 在 write_header 原生报 Experimental feature）。CLI 删除 CommitRtxOutput/ProbeStreamTypes/HasMp4IncompatibleAudio 与 mkv 临时文件，一律 `RunAsync(input, outputFile, ...,"auto","copy",...)` 直写。保留：TrueHD 48kHz 元数据修复、参数不完整音频流跳过告警（属源数据修复而非容器规避）。
- Fix during build: 上游源码为非 BOM/936 代码页，我误写的中文注释触发 C4819，已改英文注释；`ffmpeg_error` 参数为 const char*，临时 std::string 需取 c_str()。
- Verification (端到端矩阵，安装版 CLI + 真实 F1 m2ts 剪辑): .mkv 直连成功（hevc 3840×2160 10bit PQ + TrueHD 原音轨，ac3 副轨元数据不完整跳过并告警）；.mp4 直连原生报 `could not write the MP4 header：Experimental feature` 且最终文件未被创建；.webm 原生报 `could not write the webm header...Invalid argument`（新文案带真实容器名）；正常 h264+aac 素材 .mp4 直连成功（hevc+aac）；上游单测 85/85、仓库 Python 22/22、CLI 0 错误。
- Git: sidecar fork 分支 `rve-patches` 修订为 `5de2ef9`（5 文件 +133/-10）；插件仓库工作树未提交修改为 PluginPanel.vb、cli/Program.cs、cli/RtxVideoBackendClient.cs 与两份记录。
- Next: 用户完整 F1 文件等真实任务实测；将 `rve-patches` 整理为上游 PR（补丁即 PR 内容，含单测）；建议提交当前插件仓库改动。

### 2026-09-12 18:16 - ZCode

- User direction: 确认门禁也不应设——`avformat_query_codec` 只是提示性接口（TrueHD 误报案例），真正的裁决者是 `avformat_write_header`；另外更正此前错误：查随包 FFmpeg 源码 `nvenc_hevc.c`，`tune uhq`（Ultra high quality，NV_ENC_TUNING_INFO_ULTRA_HIGH_QUALITY）为合法调优档且原样生效，"不合法/降级"说法已更正。
- Sidecar change (fork `rve-patches` 修订为 `fe0b269`，6 文件 +119/-37)：音频/字幕 copy 循环移除 `ffmpeg_muxer_supports_copy` 预过滤（连同 helper、头声明与单测），封装器兼容性完全交给 `avformat_write_header` 原生判断；写头错误信息携带真实容器名。保留 TrueHD 48kHz 修复与参数残缺流跳过告警（属源数据修复，非容器规避）。
- Verification (84/84 单测 + 安装版 CLI 真实 F1 素材矩阵): .mkv 直连成功（hevc 4K 10bit PQ + TrueHD 原音轨，ac3 副轨元数据残缺跳过告警）；.mp4 直连原生报 `could not write the mp4 header...Experimental feature`；.webm 直连原生报 `could not write the webm header...Invalid argument`；正常 h264+aac 素材 .mp4 直连成功（hevc+aac）；仓库 Python 22/22。
- Next: 用户完整文件实测；`rve-patches`（fe0b269）整理上游 PR（注意：copy 策略变更是 API 语义调整，PR 描述需说明理由）；插件仓库 5 个未提交文件待用户确认后提交。

### 2026-09-12 18:35 - ZCode

- User direction: 把 `RTX_PROGRESS|p|frames|fps|eta` 进度拆解映射到 3FUI 任务进度。
- Implementation: `RtxVideoBackendClient.RunAsync` 轮询改为输出插件 `BackendProgress` 原生协议——首次拿到 `framesTotal` 时打印 `Total Output Frames: N`（总量变化时重打），每拍打印 `FPS: … Current Frame: … ETA: H:MM:SS`（etaSeconds 按后端惯例格式化为 H:MM:SS），替换原 `RTX_PROGRESS` 行。插件端零改动：`BackendProgress.OnQueueEvent` 既有解析直接得到 百分比=帧/总、进度文本、效率文本（FPS）、时间文本与预览遥测。已知语义：sidecar 无暂停 API，3FUI 暂停时 RTX 编码继续、进度如实显示。
- Verification: 0 错误构建并部署 CLI；真实 F1 剪辑任务实测输出 `Total Output Frames: 121` 与逐拍 `FPS: 22.4 Current Frame: 88 ETA: 0:00:02`。仓库 Python 22/22。
- Git: 插件仓库工作树 5 个未提交修改不变。

### 2026-09-12 19:56 - ZCode

- User direction: 为 sidecar 增加暂停能力，打通 3FUI 暂停按钮到 RTX 任务的链路。
- Sidecar implementation (fork `rve-patches` 修订为 `9ec12f6`，9 文件 +210/-37)：`CancellationToken` 增加 `paused` 原子标志；`job_runner` 增加 `request_pause/request_resume`（镜像 cancel 的活动令牌查找）；`http_server` 新增 `POST /api/jobs/{id}/pause` 与 `/resume` 路由；管道在读循环与 `process_decoded_frame` 检查点调用 `wait_while_job_paused`（50ms 轮询等待，取消优先于暂停，进入/恢复各记一条日志）。
- CLI implementation: `RunAsync` 增加 `Func<bool>? isPaused`，轮询循环内边沿触发 `POST /pause` 或 `/resume`（失败不中断、下轮重试）；`Program.cs` 传入 `() => ReadShmByte(pauseShm) == 1`，3FUI 的 PauseControl 写共享内存即驱动 sidecar 暂停/恢复。
- Verification: 单测 84/84；真实暂停测试（120s 素材，PowerShell 模拟 3FUI 写暂停字节）：Current Frame 在暂停窗口冻结于 210（19:54:33 与 19:54:36 两行相同），恢复后帧数走动、FPS 从 87 回升至 190，任务完成耗时准确包含暂停时长；mkv 直连与完成状态回归正常。首轮 30s 素材因处理太快未到暂停触发点，改用 120s 素材完成验证。
- Known notes: sidecar 无暂停 API 的旧说法已过时；RTX 任务暂停期间进度帧数冻结、FPS 显示下降属真实状态；取消优先于暂停。
- Git: 插件仓库工作树 5 个未提交修改（PluginPanel.vb、cli/Program.cs、cli/RtxVideoBackendClient.cs、两份记录）。
- Next: 用户完整文件实测（含 3FUI 界面点暂停）；`rve-patches`（9ec12f6）整理上游 PR；插件仓库改动待用户确认后提交。

### 2026-09-12 20:15 - ZCode

- User report: RTX 输出视频时间轴异常，2 分钟的 F1 视频时间轴显示 11:02。
- Root cause: 完整 F1 m2ts 的时间戳基线为 600.000 秒（蓝光标准做法，全部流 start_time=600），内容时长 62.752 秒；600+62.75=662.75s=11:02.75 与用户所见完全吻合。sidecar 直接用 libavformat 写封装，缺少 ffmpeg 命令行默认的起始偏移归零，600 秒基线被原样写入 mkv。我此前的 30s 测试剪辑偏移仅 1.44s，故复现不明显（31.5s vs 30.08s）。
- Fix: 管道在读入后计算 `input_start_time_us`（input->start_time，>0 时），编码视频与拷贝流两条写包路径逐包按当前流时间基换算偏移并钳制非负（matroska 写头会改写流时间基，故必须逐包计算）；效果与 ffmpeg CLI 默认行为一致。
- Verification: 30s 剪辑输出 start_time 归零（video 0.016/audio 0.000）、时长 30.058s；完整 F1 文件 + 用户原始参数直连 mkv：全部流 start_time=0.000000、duration=62.752s、1506 帧（62.75×24 ✓）；完整文件的 ac3/eac3 副轨元数据完整、全部保留（此前"跳过"仅发生在剪辑测试产物上）。单测与 Python 22/22 前轮已过，本轮无 CLI 改动。
- Git: fork `rve-patches` 修订为 `ea16ce4`（9 文件 +242/-40）。F1 修复验证产物：`C:\压制测试\F1_fix_test.mkv`（可直接播放核对时间轴）。
- Next: 用户播放 `F1_fix_test.mkv` 核对时间轴与音画；插件仓库 5 个未提交文件建议提交；fork 补丁待整理上游 PR。

### 2026-09-12 20:25 - ZCode

- User direction: 提交本地改动；sidecar 不向上游提 PR（用户确认已脱离其框架），fork 分支 `rve-patches`（ea16ce4）保持本地。
- Commit plan: ① `fix: restore value box background and single-line RTX HDR status label`（PluginPanel.vb）；② `feat: mux RTX output directly into the target container`（cli/Program.cs、cli/RtxVideoBackendClient.cs、两份记录）。仅本地提交，不推送、不合并 PR #1。

### 2026-09-12 20:40 - ZCode

- Merge record: PR #1（9551296+00946ce，merge commit 198842b）与 PR #2（ddc3325+607e0b1，merge commit e154af1）均已合并进 maxzrb/main；其中 PR #1 的 head 分支在 user-Wing 仓库（早期会话所推），合并过程对 user-Wing 仅读取未写入；PR #2 起全部在 maxzrb 仓库内完成。
- User preference: maxzrb 是用户自有仓库，后续改动**直接推 main，不再走 PR 流程**；本地分支与 fork/main 的对齐已在本次完成（rebase 后仅文档提交新增）。

### 2026-09-12 21:05 - ZCode

- User direction: 图片超分从超分工作台独立为新选项卡，位置在「实时预览」右侧、「模型下载」左侧，沿用工作台的超分引擎与模型。
- Implementation (`PluginPanel.vb`): 新增 `_pageImage`/`_imageRoot` 与 `BuildOfficialImagePage()`/`SyncImageRootBounds()`；「图片增强」区（标题/选图/输出目录/命名与格式/开始增强/进度条）整体从工作台迁出为独立页（Y 12/48/102/156/210，根高 260），标题改为「图片超分·沿用超分工作台的超分引擎与模型」；工作台根高 970→730、SyncUpscaleRootBounds 850→730；页面样式数组与选项卡注册插入 tabImage（实时预览与模型下载之间）。控件与事件处理全部原样复用，`OnStartImageProcessing` 仍读工作台的 `_config.Backend`/模型（rtxvsr 依旧拒绝图片）。
- Verification: 插件构建 0 错误（LakeUI 5.9.0.0）；DLL SHA-256 `ceb94ddf9cac0fca…` 构建与安装目录一致，已部署 `C:\Program portable\3FUI\3FUI\Plugin\videoenhancer.3fui.dll`。待用户重启 3FUI 目视确认新选项卡位置与布局。
- Git: 已按用户新流程直接提交并推送 fork/main（见下条提交）；sidecar fork 不向上游提 PR（用户确认脱离其框架）。

### 2026-09-12 21:20 - ZCode

- User report: 重启 3FUI 后未看到图片超分新选项卡。
- Root cause: 上轮部署时 INST 变量误写为 `Plugin\videoenhancer`（CLI 子目录），新 DLL 被拷到 `Plugin\videoenhancer\videoenhancer.3fui.dll`（错误位置，多出一层），真正的 `Plugin\videoenhancer.3fui.dll` 未更新（仍是 13:56 的 9ecc9b44 旧版）；当时的验证查询了同一个错误路径造成假成功。教训：部署核验必须使用规范绝对路径常量，而非拼接变量。
- Fix: DLL 已拷入正确路径并核验（构建/目标 SHA-256 一致 `ceb94ddf…`，二进制含「图片超分」UTF-16 资源），误拷文件已删除；3FUI 未运行、无文件占用。用户需再次重启 3FUI 验证。

### 2026-09-12 21:20 - ZCode

- User report + screenshot: 分段超分页布局歪斜——标题/标签贴左缘裁切、添加分段按钮与操作列溢出右缘、状态条通栏贴边。
- Root cause: `BuildOfficialSegmentedPage` 根面板用 `Dock=Fill`，但 LakeUI ModernPanel 绝对布局在窗体尺寸变化时不触发子项重排，各控件停留在构建瞬间的不同宽度状态。
- Fix (`SegmentedUpscalePage.vb`): 根面板改为与超分工作台同款的显式宽度同步（`_segmentRoot` + `SyncSegmentedRootBounds`，页 Resize/ClientSizeChanged 触发 SetBounds 重排），根高固定 700，页边距与滚动余量口径一致。另：进程名实为 `FFmpegFreeUI.exe`（此前 tasklist 过滤 3FUI.exe 误判"未运行"）；上轮插件 DLL 曾误拷至 `Plugin\videoenhancer\` 子目录（已删），本轮起部署核验一律使用规范绝对路径。
- Deployment: DLL SHA-256 `7fb2664f…`（构建=安装目录，4,656,640 bytes，21:13），含图片超分选项卡与分段页修复。用户重启 3FUI 验证。

### 2026-09-12 21:40 - ZCode

- User direction: ① 右键超分页「添加当前模型/清空模型列表」按钮宽度不足（6 字按钮按代码惯例 140→170px）；② 超分工作台三大模块重排为 超分 → 补帧 → HDR，模块之间空一行（38px），组合处理顺序行紧跟补帧模块。
- Implementation: `PluginPanel.vb` actionRow 列宽 140→170；工作台 Y 重映射（interpHeader 459→371、interp 字段 497→409、573→485、orderRow 643→555、hdrHeader 345→649、hdrModeField 383→687、分隔线 699→763、根高 730→795）。
- Verification: 构建 0 错误；DLL SHA-256 `f0a8d6de…` 构建与安装目录一致，已部署（3FUI 未运行）。提交 `cafa68a` 已直接推送 fork/main。待用户目视验收。

### 2026-09-12 21:55 - ZCode

- User direction: 模块间空行由整行（38px）改半行（19px），「视频处理」标题与超分模块之间也空半行。
- Layout: 视频处理标题 113-149 → 超分模块整体下移 19（upscaleHeader 149→168、字段 187→206、263→282）；超分尾 352+19=371 补帧原位不动（371/409/485）；组合处理顺序 555 不变；611+19=630 HDR（hdrHeader 630、hdrModeField 668）；分隔线 769；根高 795→776。
- Verification: 构建 0 错误；DLL SHA-256 `f898f361…` 构建=安装目录，已部署（3FUI 未运行）。提交推送后待用户目视验收。

### 2026-09-12 22:20 - ZCode

- Release 1.3.0 已按 `release/发布流程.md` 完成并双源核验通过。
- Backend: 基线取自 ModelScope 渠道 `python_20260826.7z`（sha256 af3ef9fd… 校验一致）；候选取自用户提供的 python-20260912.7z 并剔除 `backend/cache`（TRT/triton）、`__pycache__`、`*.pyc` 后重建干净完整包 `python_20260912.7z`（2,790,833,001 bytes，sha256 `e607456f…`）。审计 +1/~3/-0（新增分段后端，更新图片桥与 inspect 工具），哨兵 `backend/rve-backend.py`。完整包、增量包（10,839 bytes）、channel.json 按“完整包→增量包→channel”顺序上传 ModelScope 并回读核对（latestVersion=2026.09.12.1）。
- 本体: GitHub `v1.3.0` 已创建（EXE 16,921,057 bytes，sha256 `1c608322…`，stable.json），Release Notes 14 行逐行校验格式合规且与本地一致；ModelScope Releases 镜像与 Models 备用 EXE 已同步；双源 package path/size/sha256 一致，两处 EXE HEAD 200。安装器/更新器隔离测试在流水线中通过。
- RTX 运行组件包: `Bin/rtx-video/RTXVideoRuntime_20260912.7z`（22,759,743 bytes，sha256 `2b668aaa…`，回读一致），含定制 sidecar（fork rve-patches @ ea16ce4）、nvngx 运行库、FFmpeg 共享库、许可文件与安装说明；nvngx 为 NVIDIA 专有组件，按“风险接受”发布，许可复核建议完成。
- 版本迭代记录已更新。GPU 矩阵未按 1.1.0 规模重跑（本轮以 RTX/新路径实机验证替代，已在记录中注明）。
- 收尾提交: 版本迭代记录 + 状态文件直接提交并推送 fork/main。

### 2026-09-12 22:35 - ZCode

- User direction: 按项目实际更新 README 的介绍、引用程序与致谢。
- Implementation: 版本状态改为 1.3.0 已发布；功能概览补图片超分独立页、RTX 暂停/容器直连/编码参数透传；系统要求更新 RTX 运行组件包获取方式与许可口径；推理后端表补 BasicVSR++ 与 RTX VSR 两行；HDR 和处理顺序补 RTX 顺序/容器/门禁规则；模型来源与致谢新增「使用的程序与组件」小节（3FUI、LakeUI、FFmpeg、Zennmn/RTXHDR-RTXVSR、NVIDIA RTX Video SDK、NCNN/PyTorch/TensorRT/ONNX Runtime、RVE、mkvtoolnix、ModelScope）；许可证章节补 RTX 运行组件包许可说明；核心目录树补 bin\rtx-video。
- Git: 直接提交并推送 fork/main。

### 2026-09-13 19:45 - ZCode

- User direction: 因 1.3.0 架构改动大，重测 GPU 模型兼容矩阵并引入 RTX/HDR 多模型多步骤；过程中先后要求 RTX 作为新增相、全量单测+抽样交叉扩到约 1200 项、Stop 矩阵先补全模型、手动安装包改为完整目录树模式、矩阵增量并降为 3 路并行。
- Matrix design: `cli/tests/gpu_matrix_runner.py` 重构为三相结构（--phase upscale|interp|hdr）：upscale=已装模型全量单测+RTX 用例（目标/质量/容器扫描+全部补帧代表组合），interp=补帧单测+代表全网格(44×6×2)+后端类代表层(192)+哈希稳定抽样层(348，模型名 sha256 轮转配对，增删模型不失效既有用例)，hdr=RTX HDR 组合（纯 HDR/VSR+HDR/三步骤/4 后端+HDR/多步骤）+6 个门禁期望失败用例。新增判定：RTX 输出映射行为权威基准、GIMM 强制顺序横幅校验、HDR 10bit 校验、SKIP_ENV、FAIL_GATE/FAIL_GATE_MISMATCH；RTX/GIMM 用例 GPU 独占串行。
- Execution: 全量 1262 用例（upscale 157 + interp 1090 + hdr 15）终态 1252 PASS + 8 SKIP_OOM（FlashVSR×4 GIMM 变体×2 顺序，6GB 已知 OOM）+ 2 FAIL_EXIT（GIMM-VFI-R-LPIPS NaN，DAT2/AniToon-RPLKSRL 两组合 upscale-first 可复现）。RTX 33 项按最终 sidecar 全部重跑通过。5 路并行出现 4 个 rife4.25 TRT 挂起与显存满载（用户要求降 3 路），串行复测全部通过，属争用非回归；SwinIR-480x320×GMFSS 重组合 420s 超时复测 24s 通过。
- Product fix 1 (CLI): 矩阵发现 sidecar 只支持 D3D11VA 硬解（choose_d3d11_format 无回退），FFV1 无硬件解码器导致 1.3.0 的 RTX+补帧、传统超分/补帧+RTX HDR 全部在生产环境损坏（解码首包失败）。修复：RunVideoWithRtx 的 RTX 前置中间文件从 FFV1 gbrp10le/16le 改为无损 HEVC Main10（x265 lossless=1, yuv420p10le，HDR 输入时经 x265 VUI 写 BT.2020/PQ 标记）；重建部署 videoenhancer.exe（备份 Temp/rve-cli-matrixfix-backup）。
- Product fix 2/3 (sidecar fork rve-patches 新修订): ① NVENC max_b_frames=0（新版 ffmpeg 自动开 B 帧导致编码重排）；② drain_encoder 对 duration=0 的编码包补 1（D3D11VA 解码帧无 duration → movenc 轨道时长短一帧 → mp4 demux edit list 把末帧标 DISCARD 真丢帧，mp4 容器用例 4 帧只解出 3 帧）。重建部署 vsr_backend.exe（备份 Temp/rtx-sidecar-bf0-backup）。已知边界记录：D3D11VA 解码 192x128 失败、320x240 起 OK，矩阵 RTX 夹具因此统一 640x360；生产小分辨率视频+RTX 会失败，CLI 预检待做。
- Models: 用户下载 5 个官方权重入库（PTH：RealESRGAN_x4plus 67MB、x2plus 67MB、x4plus_anime_6B 18MB、realesr-general-wdn-x4v3 4.9MB、Nomos8k-span-otf-4x-strong 9MB）；能力探针识别 ESRGAN×3/Compact(wdn)/SPAN；cuda+tensorrt 试跑 12/12 PASS（TRT 引擎入缓存）；model-capabilities.json 93→98（cuda 42→47、trt 36→41；x2plus inputMultiple=4），架构分组按用户反馈从探针名(ESRGAN/Compact)统一修正为 RealESRGAN 家族；单测计数断言同步，22/22。证据审计结论：登记层面无缺口（磁盘=登记）；官方有而未收录=RealESRGAN x4plus/x2plus/wdn（已补）+Nomos8k strong（已补）；CUGAN PTH、AnimeJaNai V3.1 PTH、RealHatGAN PTH 待人工获取；Waifu2x/DnCNN 官方无 PTH 不补。
- Manual install: 应用户朋友需求，build.ps1 新增产出 videoenhancer-manual-install.zip（plugin/videoenhancer.3fui.dll + plugin/videoenhancer/videoenhancer.exe + 手动安装说明.txt，目录树与实机一致，整体复制即装）；先实验过 SFX 方案（zip 追加 exe 尾部，--version/--check/zipfile/bsdtar 全通过）但按用户反馈该形态不合需求，已撤销。发布流程.md 1.3 节补录第三个资产。PS 坑记录：中文 ps1 需 UTF-8 BOM；扩展方法在 PS5.1 不能实例调用。
- Deployment: 实机 CLI exe 已更新（目录修正版，无 SFX 负载）；models/PTH 已含 5 新权重；插件 DLL 未改动。
- Git: 工作树未提交（runner、Program.cs、model-capabilities.json、test_model_capabilities.py、build.ps1、发布流程.md、STATUS.md、工作进度.md）；sidecar fork rve-patches 本地新修订未推送。建议尽快提交。
- TODO: ① GIMM R-LPIPS NaN 回归调查（1.1.0 同组合通过，疑似 20260912.1 后端 torch 2.9 引入）；② ModelScope 模型镜像上传 5 新权重；③ manual-install.zip 纳入下版发布资产；④ 小分辨率+RTX 的 CLI 预检；⑤ Nomos8k ncnn weak 转换（需转换工具链）；⑥ DPI 矩阵/500 帧预览压力仍挂账。

### 2026-09-13 20:00 - ZCode

- User report: 重启 3FUI 后首次进入模型下载页不自动拉取列表；使用教程页空白。
- Root cause: `OnTabChanged` 硬编码页签索引（2=下载、4=导入、5=分段、6=右键、7=教程）；1.3.0 在实时预览后插入图片超分页使后续页签整体 +1，全部懒加载钩子错位（下载钩子实际落在图片超分页上，教程懒渲染永不触发）。
- Fix: PluginPanel.vb 构建页签时按 Add 顺序捕获各页签索引入字段（_tabIndexDownloader 等 5 个），OnTabChanged 改用字段比较；插入新页后钩子不再错位。
- Verification: 插件构建成功（LakeUI 5.9.0.0）；新 DLL SHA-256 `9bd4ed7e…` 已覆盖安装 `C:\Program portableFUIFUI\Pluginideoenhancer.3fui.dll`（原 f898f361…）；待用户重启 3FUI 验收自动拉取、教程内容、清理按钮可见性与各页懒加载。
- Answer: 纯 RTX VSR 不需要下载 python 后端——插件环境检查按当前后端传参（--check -backend rtxvsr 仅查 sidecar/D3D11/SDK/NVENC），CLI RTX 路径也不调用 python；RTX 叠加补帧或传统超分引擎时才需要对应推理后端。

### 2026-09-13 21:18 - Codex

- Request: 接手修复用户《缎带英雄》1080p→4K RTX VSR 质量 4，HEVC p7/uhq/vbr/cq28 在首帧前 NVENC Invalid argument。
- Evidence: 旧安装版对原片短样本可复现；HQ 成功，UHQ 失败。原生 FFmpeg UHQ 成功，但添加 -bf 0 后同样 InitializeEncoder invalid param (8)。确认直接原因是前序 sidecar max_b_frames=0 补丁与 UHQ 冲突；早期转义/显卡选择推测不作为结论。
- Implementation: sidecar 删除强制零 B 帧，保留 packet duration=1 补齐；非零起始时间的 PTS 归零提前到 avcodec_send_frame 前，删除 drain_encoder 的事后偏移/截断，保留负 DTS。边界测试曾抓出 pts(0)<dts(9875)，提前归零后解决。MSVC 增加 /utf-8，修复中文注释被 CP936 误读造成的编译失败。保留前序 NVIDIA 适配器选择等改动。
- Verification: cmake --build build/backend-hw --config Release --target vsr_backend --parallel 4 成功（仅既有适配器名称 wchar_t→char 警告）；python cli/tests/rtx_nvenc_regression.py --core <实机插件目录> 四项通过：带 10 秒起始偏移的 4 帧，HQ/UHQ × MKV/MP4，实际解码 4/4，无 DISCARD、包 duration>0、DTS 单调。原片短样本实际输入 72 帧、输出 4K HEVC 72 帧，CLI 的 Total Output Frames=73 为估算，不能作为实际帧数。未处理完整电影。
- Deployment: 最终 vsr_backend.exe SHA-256 C633253E998A02D6BDB0E72DCF34827AAA42F5B41565FADE23DE1C5438E6166D；构建与 C:\Program portable\3FUI\3FUI\Plugin\videoenhancer\bin\rtx-video\runtime\vsr_backend.exe 一致。旧安装版备份 C:\Users\maxzr\AppData\Local\Temp\rve-nvenc-debug-20260913-203505\vsr_backend-before-fix.exe；样本和回归产物保留 Temp 下便于复核。未改原片。
- Git/checks: 主仓库 pull 快进到 30d5782（README 一行），sidecar pull 已同步。sidecar diff --check 通过；主仓库全局检查报既有 cli/build.ps1 行尾空白，未改无关文件。主仓库和 sidecar 均非干净，建议分别提交；未创建发布或上传远端组件。
- Next: 用户用原预设重新开始完整任务；后续独立处理 SDR p010le 透传、估算帧数包含起始偏移的问题。已有模型/发布/UI 待办保持。

### 2026-09-13 22:24 - Codex

- Request: 继续调查 GIMM R-LPIPS，明确撤销“NaN 时复制前帧”的违背补帧语义方案；同时系统检查 RTX VSR，并研究 RTX 处理能否和软件编码配合。
- GIMM root cause: 在安装后端临时加入数值探针后确认，失败组合送入 GIMM 的 frame0/frame1 已经是 NaN；源头是 DAT2 与 AniToon-RPLKSRL 在当前 PyTorch/CUDA FP16 路径的超分输出，不是 GIMM 推理本身。Program.cs 删除两段复制 frame0 的兼容补丁，安装版 InterpolateGIMM.py 恢复 `raise ValueError("Nans in output")`，且已通过 py_compile。
- GIMM fix/verification: CUDA auto 精度规则把 DAT2、AniToon-RPLKSRL 与既有 SwinIR/GRL 一样固定到 FP32；原先两个 FAIL_EXIT 组合（各自 + GIMM-VFI-R-LPIPS，upscale-first）均成功，输出 640x480、7 帧，framemd5 均为 7 个不同哈希。矩阵新增 signalstats 首帧亮度范围校验与 FAIL_CONTENT，避免 NaN 经 clamp/cast 变黑仍按尺寸/帧数误报 PASS；模型规则单测 5/5。
- RTX fixes: CLI 将 `-pix_fmt[:v:n]` 解析为 auto/nv12/p010le 并传给 sidecar；sidecar 为 SDR P010 建立 D3D11 P010 编码表面，HEVC 设置 Main10，H.264+P010 明确返回 h264_10bit_unsupported。HDR 原 X2BGR10 直送路径不变。保留 UHQ B 帧、编码前 PTS 归零、packet duration 补齐方案。
- RTX verification/deployment: sidecar 85/85 单测；CLI Release publish 成功（仅 2 个既有 CA1416）；HQ/UHQ × MKV/MP4 实机回归全部解码 4/4，无 DISCARD、DTS 单调，HQ 为 yuv420p，UHQ 为 yuv420p10le/Main10；用户原参数在 1080p 样本上输出 3840x2160 Main10、72/72 帧。安装版 CLI/sidecar 哈希分别为 `DBA1EF73FE26B8BE72DA448D516748F787F2C78E22CA16A87C68E49EE1E62DD3`、`272B926D7A13AEC919C3D98EC7B5B93CB77FE7BE2B509C932D9C110472ABAE47`；部署前备份在 `%TEMP%\videoenhancer-p010-backup-20260913-221435`。
- Software encoding research: NVIDIA/FFmpeg 官方资料与本机能力均表明可行：RTX SDK 保持 D3D11 处理，随后下载硬件帧至 CPU；NV12/P010 再转换成软件编码器支持的 yuv420p/yuv420p10le，送 libx264/libx265/SVT-AV1/libaom。实测 RTX P010 结果经 libx265 ultrafast 输出 HEVC Main10 4/4 帧。当前 sidecar 仍硬编码 NVENC；推荐后续直接在同一 libav 管道实现下载+swscale+软件编码，避免巨大或有损中间文件。参考：https://docs.nvidia.com/video-technologies/video-codec-sdk/13.1/ffmpeg-with-nvidia-gpu/index.html 、https://ffmpeg.org/ffmpeg-filters.html 。
- Remaining risks: sidecar 目前按模式复制全部音轨/字幕，未严格兑现 ffmpeg-settings 的 `-map`；D3D11VA 对低于约 240p 输入有限制；Total Output Frames 是时长估算，72 帧样本显示 73。完整电影仍待用户运行。主仓库与 sidecar 仓库均非干净、未提交，建议分仓库整理提交后再切换工具/设备。

### 2026-09-14 10:22 - Codex

- Request/clarification: 用户明确正确架构应为“RTX VSR 先处理，随后走 3FUI 的编码系统”；3FUI FFmpeg 来自宿主 EXE 目录或 PATH，不应把插件历史遗留的 `bin\ffmpeg` 当作主路径。确认本轮修改对象是 VideoEnhancer 插件与 sidecar，未修改 3FUI 本体。
- Architecture: `videoenhancer.exe` 现在按显式 `VIDEOENHANCER_FFMPEG`、当前/宿主目录、PATH、旧 `bin\ffmpeg` 回退的顺序解析 ffmpeg/ffprobe。RTX sidecar 新增受限本机命名管道字段，只允许 `\\.\pipe\videoenhancer-rtx-*` 且必须禁用音频/字幕复制；D3D11 RTX 输出经 `av_hwframe_transfer_data` 下载为 NV12/P010/X2BGR10，并以 rawvideo 写入管道。CLI 将管道帧作为输入 0、原始源文件作为输入 1，宿主 FFmpeg 执行用户编码参数、音轨/字幕映射与最终封装。sidecar 不再接收最终编码参数，也不要求其自身 NVENC 可用。
- Correctness fixes: `libx265`/`libx264`/AV1 编码族不再被 NVENC 能力误判；H.264 NVENC + P010 在 CLI 启动输出前明确拒绝；sidecar/FFmpeg 失败会删除零字节或部分输出。`-map` 从原始 3FUI 设置解析为类型内序号，最终编码从原视频复制选中的音轨/字幕。帧总数在解码完成后由估算值纠正为实际处理帧数。HDR 矩阵正向用例改为显式 HEVC/P010，不再依赖旧 sidecar 静默覆盖 H.264；WebM 门禁接受宿主 FFmpeg 的原生容器错误。
- Verification: CLI Release/publish 0 错误（2 个既有 CA1416）；sidecar 构建成功，单测 89/89；Python py_compile 与模型规则 5/5；`git diff --check` 对本轮相关文件通过。安装版环境检查在 sidecar 报告其 NVENC 路径不可用时仍正确通过 D3D11/SDK/VSR/HDR。编码专项 8/8：HQ/UHQ × MKV/MP4 均 4/4、无 DISCARD、DTS 单调；RTX + libx264、libx265 Main10、libsvtav1 10-bit 均 4/4；3 音轨选 1、3 字幕选 2；H.264/P010 门禁不留文件。RTX VSR 专项 18/18、HDR 相 15/15。最终安装版以《缎带英雄》1080p/72 帧真实短样本和用户 p7/uhq/vbr/cq28/P010 参数输出 3840x2160 HEVC Main10 yuv420p10le、72/72 帧；控制台确认使用 PATH 的 FFmpeg 8.1.2 full build。
- Deployment: `C:\Program portable\3FUI\3FUI\Plugin\videoenhancer\videoenhancer.exe` SHA-256 `53B624BF86CD840A4256BB8113856ACDAFC7DFEFE25C17F0D8B58CF4B310B27A`；`bin\rtx-video\runtime\vsr_backend.exe` SHA-256 `2BDD5DDE81D40DD5BD7C0C37B15AD4CAAE91D908589AC1A250883E50F8705B8E`。最终备份：`%TEMP%\videoenhancer-before-framepipe-final-20260914-094237` 与 `%TEMP%\videoenhancer-before-framepipe-cli-final-20260914-101246.exe`。测试证据：`%TEMP%\rtx-framepipe-upscale-full-20260914-095844`、`%TEMP%\rtx-framepipe-hdr-full-20260914-095135`、`%TEMP%\rtx-nvenc-regression-4ftk02si`、`%TEMP%\ribbon-framepipe-final-20260914-102128.mkv`。
- Remaining/Git: 完整电影尚未运行；低于约 240p 的 D3D11VA 限制未处理；全量 1262 项未整体重跑。主仓库 `main@30d5782` 与 sidecar `rve-patches@ea16ce4` 均已 pull、远端无新增，但工作树均非干净且包含前序改动；未提交、未推送、未发布，建议分仓库整理提交后再切换工具或设备。

### 2026-09-14 11:35 - Codex

- Request: 检查前序提交、Backend/模型更新和 RTX 后端推送方式，补做低分辨率 D3D11VA 修复，并发布 1.3.1；用户确认从本版起把手动安装 ZIP 纳入正式发布资产。
- Low-resolution fix: sidecar 对宽度小于 320 或高度小于 240 的输入改用软件解码；yuv420p/yuv420p10le 在进程内手动打包为 NV12/P010 后上传 D3D11，避免引入缺少 `libgcc_s_seh-1.dll` 的 swscale 依赖。192×128 8/10-bit、320×180 和 320×240 边界均通过，内容哈希确认 4 帧互不相同。
- RTX distribution: 定制源码提交 `c83df0f` 推送至 `maxzrb/RTXHDR-RTXVSR:rve-patches`，标签 `videoenhancer-runtime-2026.09.14.1`；运行包 `Bin/rtx-video/RTXVideoRuntime_20260914.7z` 为 22,480,820 bytes、SHA-256 `bccc9493…`，已上传 ModelScope Models 并回读一致。5 个新增 PTH 权重也已逐项上传并核对远端 SHA-256。
- Backend audit: 下载并校验当前远端完整包 `python_20260912.7z`（2,790,833,001 bytes，SHA-256 `e607456f…`）；安装目录仅 4 个同步脚本存在 LF/CRLF 差异，归一化后逐字相同，因此 Backend 保持 2026.09.12.1，不发布空版本。
- Release tooling: `cli/build.ps1` 严格生成手动安装 ZIP，缺插件 DLL 直接失败；`release/build-modelscope-release.ps1` 将版本化 ZIP 作为第三个 GitHub/ModelScope Release 资产；发布文档和门禁口径同步。历史工作树中将更新源改为 ModelScope 首选的未记录改动与既定 GitHub 权威策略冲突，已恢复 GitHub 首选/ModelScope 兜底且未进入提交。
- Verification: sidecar 89/89；Python 23/23；发布门禁 5/5；Backend 更新器 6/6；已安装 1.3.1 专项通过 HQ/UHQ×MKV/MP4、libx264/libx265/libsvtav1、精确 map、H.264/P010 门禁及 192×128 libx264/libx265。GitHub/ModelScope 五个公开下载 URL 均 HTTP 200。
- Release: GitHub `v1.3.1` 正式发布，标签指向 `4f7b97b`；EXE 16,967,002 bytes / `4b1a2dfd…`，手动 ZIP 13,975,563 bytes / `715d2eca…`，stable.json 1,259 bytes / `9f3ff72e…`。ModelScope Releases 清单和两个资产、Models 备用 EXE均回读一致。
- Deployment: 实机 3FUI 已部署 1.3.1；部署前备份在 `%TEMP%\videoenhancer-1.3.1-before-deploy-20260914-1132`。完整电影未代为启动，用户需按原参数重新开始任务。
- Git: 主仓库功能/发布提交 `4f7b97b`、发布记录提交 `44d7669` 与 sidecar `c83df0f` 均已推送；两个仓库收尾时工作树干净。全量 1262 项矩阵未整体重跑，作为已记录的剩余风险。

### 2026-09-14 12:05 - Codex

- Request: 用户担心 ModelScope 同时显示 RTXVideoRuntime_20260912/20260914 导致重复下载，并要求模型列表增加右键删除，随后发布修正版。
- Implementation: 将 Backend Python 与 RTX runtime 统一为“远端保留历史、当前客户端只展示最新版本化归档”；模型权重继续使用稳定路径覆盖更新。模型下载页为已安装的本地单文件模型增加“删除本地模型”右键菜单、二次确认和状态刷新；CLI 新增 `--delete-download-model`，仅允许 `models` 下已知模型分类并经 `SafeCombine` 定位，拒绝压缩包、Backend、Bin、Plugin 与重解析点，且删除不依赖网络。
- Verification: CLI 与 LakeUI 5.1 插件构建通过（仅 2 条既有 CA1416）；Python 24/24；实时 ModelScope 列表共 96 项，RTX 仅 `RTXVideoRuntime_20260914.7z`、Backend 仅 `python_20260912.7z`；隔离夹具中 PTH 删除成功、RTX runtime 删除返回退出码 1；正式 1.3.2 构建、安装器、更新器、发布门禁 5/5、Backend 更新器 6/6 均通过，Backend 审计 0/0/0。
- Candidate assets: EXE 16,969,237 bytes / SHA-256 `6adeb303d46c45dd9d1e88c0fbce9b7afd61097de437c7386a00806e2aa383d9`；手动 ZIP 13,978,769 bytes / `b428b158305c8ef150b8da18a333400c48cde7b94c5de67ed35c9e6ab3555a9a`；stable.json 752 bytes / `bed2759333f2295066af9c5501705fb5dacd82afc1eabf2028d4b10a6b57a6a5`。
- Git/next: 源码与记录尚未提交；下一步提交并推送 `fork/main`，创建 v1.3.2、同步 ModelScope 两个数据集、回读资产后部署。`.tmp-delete-model-1-3-2/` 为隔离夹具，递归清理命令被执行策略拒绝，必须排除提交。

### 2026-09-14 12:22 - Codex

- Release: `efed38a` 已推送 `fork/main`；GitHub `v1.3.2` 标签指向该提交并含 EXE、手动安装 ZIP、stable.json。ModelScope Releases 与 Models 备用 EXE 已同步；Backend/RTX runtime 无变化，未重复上传。
- Remote verification: GitHub EXE、ModelScope Releases EXE、Models EXE 均为 16,969,234 bytes / SHA-256 `b50694d34811ae5846690e2e3977bb90c5157857ce9ed413cc44ebf429751191`；ModelScope ZIP 为 13,978,728 bytes / `82e0e74ef891e0e1f713c70b97312f3dbe6be1498b6852c5578785fba9c07487`，与 GitHub API digest 一致；双源 stable.json 为 752 bytes / `9c5c5b1a8e66994a4fd8989f331fe882265e774bad69e53bd43f4806b395cdab`。GitHub ZIP 直连一次因网络超时未完整下载，但 API digest 与 ModelScope 回读足以交叉确认。
- Deployment: 确认 3FUI/videoenhancer/FFmpeg/sidecar 均未运行后部署 1.3.2；安装 EXE 与发布哈希一致，DLL 4,660,224 bytes / `60cd7495b41562e74f6f2144117d8e0b2e20a28bfa72b992269769e4dd7bb272`。旧文件备份在 `%TEMP%\videoenhancer-1.3.2-before-deploy-20260914-1222`。
- Git: 发布记录提交 `0270e16` 已推送 `fork/main`；`.tmp-delete-model-1-3-2/` 是已完成验证的测试夹具，清理被执行策略拒绝，已加入 `.git/info/exclude` 且未进入版本库。收尾工作树干净。

### 2026-09-14 12:36 - Codex

- Request: 用户指出 RTX runtime 最容易积累多个版本，不接受 1.3.2 禁止删除运行组件的过度保守策略，并明确要求直接覆盖 1.3.2。
- Decision/implementation: RTX 条目右键改为“卸载 RTX 运行组件”；CLI 对严格匹配的 `Bin/rtx-video/RTXVideoRuntime_YYYYMMDD.7z` 走专用卸载，删除整个专用 `bin/rtx-video`，但保留 `bin` 公共层的 README/许可证及其他工具。卸载前拒绝正在运行的 `vsr_backend` 和任意重解析点。成功下载安装 runtime 后自动删除该目录内所有日期归档；`--clean-download-archives` 也增加同一专用目录顶层扫描。
- Verification: LakeUI 插件与 CLI 构建通过（仅 2 条既有 CA1416），Python 24/24、发布门禁 5/5。隔离测试确认清理命令删除 20260912/20260914 两个归档但保留 sidecar；专用卸载删除 RTX 根目录且不影响相邻 `bin/ffmpeg`；真实 ModelScope 22,480,820-byte runtime 完成下载、解压并自动删除归档，sidecar 950,272 bytes 存在。反射行为测试确认同版本同哈希 `HasUpdate=False`、同版本大小变化 `HasUpdate=True`。
- Git/next: 本轮改动未提交；下一步以 Backend 2026.09.12.1 同目录审计构建 1.3.2，提交后按用户授权移动同版本标签、clobber GitHub 资产并覆盖 ModelScope，再完成回读与部署。

### 2026-09-14 16:19 - Codex

- Release overwrite: 按用户明确授权将 `v1.3.2` 从 `efed38a` 经中间修订最终移动到 `57c0dcbac61eec0502e79bb4fc8185e8dfe4c438`；GitHub 三资产使用 `--clobber` 替换，Release Notes 更新为 6 项；ModelScope Releases 与 Models 备用 EXE 均覆盖成功。Backend 与 RTX runtime 包未重新发布。
- Final assets: EXE 16,970,305 bytes / `ee3b66b5b7d680925f1f91161b4f12035d7f67d7f18e3e5e2be174c396e0ec68`；手动 ZIP 13,980,064 bytes / `f69a68b6cd98b0e92845b37af76f1614fc47ca23e2800d2aba02f3fb6346c31b`；stable.json 1,142 bytes / `40e7943806f8315211ef45e5bc8ce82226eaf679b00e5e80bf1fc8de0dcb77b1`。GitHub API digest 与 ModelScope 四份实际下载回读一致。
- Same-version delivery: `PluginUpdater.HasUpdate` 在远端版本等于当前版本时比较已安装 EXE 大小与 SHA-256。反射实测旧 1.3.2 EXE 返回 True，最终 EXE 返回 False，覆盖版可自动到达且不会循环提示。
- Deployment/cleanup: 确认相关进程未运行后部署最终 EXE/DLL；旧文件备份在 `%TEMP%\videoenhancer-1.3.2-final-before-deploy-20260914-1621`。直接 PowerShell 删除被策略阻止后，改用已发布程序重新校验解压 runtime 并自动删除 20260912/20260914 两个归档；安装目录归档数 0，sidecar SHA-256 仍为 `f66b15e0…`。
- Git/next: 最终版本记录、STATUS 和中文进度待收尾提交并推送；功能提交已推送，标签已核对。提交后应确认工作树干净。

### 2026-09-15 13:35 - Codex

- Request: 用户要求按“修复 RTX HDR 参数、任务控制与预览兼容性”计划实施，限定只修改 VideoEnhancer 插件与 CLI，并部署到当前本机 3FUI，不发布远端版本。
- Orientation: 已读取 `AGENTS.md`、`docs/codex/INDEX.md`、`docs/codex/STATUS.md` 及 HandShake 2.1.0-beta 说明；`git pull --ff-only` 已显示 Already up to date，基线 `e8b900f`，当前分支 `main...fork/main`。
- Implementation: 新增 `HostQueueAccess.vb`，反射优先读取 6.2.20 的 `获取队列快照()`/`根据ID获取任务()`，旧宿主回退反射属性；替换预览、进度、暂停、停止中所有旧队列 getter。队列按钮支持字段名、控件名、按钮文本识别并保存/恢复宿主原事件；普通任务走宿主停止 API，插件任务共享内存优雅停止，12 秒后强制回退。
- HDR: `PluginConfig` 增加对比度/饱和度/中灰度/最大亮度，加载时默认和钳制；PluginPanel 使用四个可编辑、0 位小数 `ModernNumericUpDown`，两列两行并扩展滚动内容高度；QueueHook、CLI 和 `RtxVideoBackendClient` 传递四个整数到 sidecar JSON。
- CLI stop: sidecar 取消后关闭帧管道/FFmpeg stdin，最多等待 8 秒完成封装；退出码 0 且非空输出时保留部分文件，否则沿现有失败清理；常规 RTX 任务仍返回 130。
- Verification: `dotnet build cli/VideoEnhancer.csproj --configuration Release --no-restore` 通过（2 条既有 CA1416）；`VideoEnhancerPlugin/build.ps1 -HostBin C:\Users\maxzr\AppData\Local\Temp\3fui-core-compat-host -SkipInstall` 通过；`python -m unittest discover -s cli/tests -p test_*.py` 29/29；sidecar JSON 反射确认非默认值；CLI 越界参数返回 2；源码无 `编码队列_v6.队列` 直接引用；`git diff --check` 通过。
- Deployment: 发布脚本生成候选 1.3.3；确认 `FFmpegFreeUI`、`videoenhancer`、`ffmpeg`、`vsr_backend` 均未运行后备份并覆盖当前安装。最终 DLL 覆盖前备份目录 `C:\Users\maxzr\AppData\Local\Temp\videoenhancer-1.3.3-before-remove-race-dll-20260915-1408`；最终安装 DLL `18F568D9C0CE2983A09475027B11006005F95991B93FC1A67D1500C73CCC8260`，安装 EXE `EE4F85F0BC9C0BF328536788E4491951323728E1C512C33E1698BE46E9DF2377`；安装 EXE `--version` 返回 `1.3.3`。
- Remaining: 真实 3FUI UI/预览/任务控制、sidecar 无响应强停和 ffprobe 部分输出验证待用户重启后执行；未创建标签、未推送、未发布远端版本。当前工作树含源码、测试和记录改动，建议提交后再进行用户验收。

### 2026-09-15 13:40 - Codex

- Final verification: 重新执行 `python -m unittest discover -s cli/tests -p 'test_*.py'`，29/29 通过；`dotnet build cli/VideoEnhancer.csproj --configuration Release --no-restore` 0 警告/0 错误；插件 `build.ps1 -SkipInstall` 构建通过；静态检查确认无 `编码队列_v6.队列` 直接引用；`git diff --check` 通过。
- Deployment check: 当前安装 DLL SHA-256 为 `18F568D9C0CE2983A09475027B11006005F95991B93FC1A67D1500C73CCC8260`，CLI `--version` 返回 1.3.3，EXE SHA-256 为 `EE4F85F0BC9C0BF328536788E4491951323728E1C512C33E1698BE46E9DF2377`；部署目录未残留相关运行进程。
- Documentation: `version/版本迭代记录.md` 已将 1.3.3 标记为本地候选，并保留 1.3.2 历史记录；当前源码、测试和记录均未提交，建议先提交后再进行用户实机验收。

### 2026-09-15 14:02 - Codex

- Stop-output safeguard: CLI 在 RTX 优雅停止且 FFmpeg 以 0 退出并生成非空输出时写入 `.videoenhancer-stop-ok` 标记；插件临时避免宿主 MP4 清理，宿主收尾后恢复原输出路径，并按标记保留有效部分或按原设置清理残缺 MP4。
- Final build/deployment: 重新执行 CLI publish（0 错误，2 条既有 CA1416）、插件构建、Python 29/29 和安装核对；当前 DLL `18F568D9C0CE2983A09475027B11006005F95991B93FC1A67D1500C73CCC8260`，EXE `EE4F85F0BC9C0BF328536788E4491951323728E1C512C33E1698BE46E9DF2377`，`--version` 为 1.3.3。
- Backup/Git: 最终 DLL 覆盖前备份为 `C:\Users\maxzr\AppData\Local\Temp\videoenhancer-1.3.3-before-remove-race-dll-20260915-1408`；未创建标签、未推送、未发布远端，工作树仍未提交，建议提交后再让用户重启 3FUI 验收。

### 2026-09-15 14:31 - Codex

- Request: 用户要求 HDR 四个输入框不受鼠标滚轮影响、只能输入数字、去掉步进按钮，并修复数值被控件区域遮挡。
- Implementation: `PluginPanel.vb` 新增 `RtxHdrNumericUpDown`，继承 LakeUI `ModernNumericUpDown`；不调用基类滚轮处理，拦截 Up/Down/PageUp/PageDown，并拦截右端不可见按钮区域。四个控件保留整数可编辑输入，`ButtonAreaWidth=1`、`DividerSize=0`、按钮/箭头透明，增加左右 10px 内边距和左对齐，释放文本绘制空间。
- Verification: `python -m unittest discover -s cli/tests -p 'test_*.py'` 29/29 通过；`VideoEnhancerPlugin\build.ps1 -HostBin C:\Users\maxzr\AppData\Local\Temp\3fui-core-compat-host -SkipInstall` 构建通过；源 DLL SHA-256 `1C46C9583DED8CCD2AD6E81F46F0BC7270CD3B1352F5C4843AF5051AC9E9B564`。
- Deployment: 确认 3FUI、插件、FFmpeg 和 RTX 进程均未运行后覆盖 `C:\Program portable\3FUI\3FUI\Plugin\videoenhancer.3fui.dll`；部署前备份为 `C:\Users\maxzr\AppData\Local\Temp\videoenhancer-1.3.3-before-hdr-input-20260915-142919`，目标哈希与源一致。
- Git/next: 版本仍为本地候选 1.3.3，未创建标签、未推送、未发布远端；需收尾执行 `git diff --check`/状态核对并由用户重启 3FUI 实测滚轮、键盘输入和四个数值完整显示。工作树仍含本轮未提交改动，建议提交后验收。

### 2026-09-15 16:40 - Codex

- Request: 用户反馈加入 HDR 参数后工作台可以向上滚动到顶部大面积空白，要求说明原因并修复。
- Root cause: LakeUI `ModernPanel` 滚动时会把子控件根节点移动到负的视觉 `Top`；`SyncUpscaleRootBounds` 原先无条件执行 `root.SetBounds(0, 0, ...)`，位置变更事件随后把当前滚动偏移误写进设计坐标，回到顶部时根节点因此停在正的 `Top`。
- Implementation: `PluginPanel.vb` 的同步方法改为保留当前滚动坐标，仅更新根节点宽度/高度；仅在水平/垂直滚动偏移为零且位置异常时归零，避免改变 LakeUI 设计坐标。
- Verification: 临时 .NET 布局探针复现旧逻辑 `ScrollTo(0,180)` 后 `root.Top=-168`、同步后 `Top=0`、回顶后 `Top=168`；新 DLL 验证同步后仍为 `Top=-168`、回顶为 `Top=0`。插件构建通过，Python 29/29，`git diff --check` 通过。
- Deployment: 确认相关进程未运行后备份并覆盖本机 `C:\Program portable\3FUI\3FUI\Plugin\videoenhancer.3fui.dll`；备份 `C:\Users\maxzr\AppData\Local\Temp\videoenhancer-1.3.3-before-scroll-fix-20260915-1638`，源/目标 SHA-256 均为 `4A328E31D18105CE1D433546CA10B2E97A845279EC3D75FD9063D97E407E59D5`。
- Git/next: 版本仍为本地候选 1.3.3，未创建标签、未推送、未发布远端；当前工作树含所有候选改动，建议提交后重启 3FUI 验收滚动和 HDR 输入。

### 2026-09-16 10:22 - Codex

- Request: 用户要求审查 `maxzrb/VideoEnhancer#3` 的构建优化，并在不冲突的前提下整合此前 HDR、任务控制、预览兼容和滚动修复。
- Orientation/Git: 已读取 `AGENTS.md`、`docs/codex/INDEX.md`、`docs/codex/STATUS.md`、HandShake 2.1.0-beta 与冲突流程；`git pull --ff-only` 为 Already up to date。PR #3 包含 `ff49abd`、`615875d`，无 CI checks 和评审评论。
- Review: PR 主要将插件迁移到 SDK 风格 `VideoEnhancerPlugin.vbproj`，新增根解决方案 `VideoEnhancer.slnx`，CLI 通过项目引用自动构建并嵌入插件，区分普通 CLI publish 与解决方案安装产物；删除旧 `build.ps1` 和非运行时 `PluginDesigner`。PR 未修改 HDR、队列、停止、预览或滚动业务代码。
- Conflict handling: 在独立 worktree 先验证 PR 原始状态，再将当前工作树补丁三方应用到 PR head；全部已跟踪文件零冲突，新增 `HostQueueAccess.vb` 自动进入 SDK 默认编译。正式整合时用带 untracked 的临时 stash 保护现有改动，`main` 快进到 PR head 后恢复成功，stash 已正常删除。
- Verification: PR 原始状态 restore/build 0 警告/0 错误、Python 24/24；直接 CLI publish 不生成 `Artifacts`，解决方案 publish 只生成安装程序和手动 ZIP。组合版 restore/build 0/0、Python 29/29；安装器全新安装/迁移/短暂占用/注入回滚/持续占用通过，更新器迁移/占用/防篡改/无效包/回滚/中断恢复/失败重启通过，Backend 更新器 6/6、发布门禁 5/5；`git diff --check` 通过。
- Artifacts: `Artifacts\VideoEnhancerInstaller.exe` 为 1.3.3，SHA-256 `2ECBF72CCCCB275F9F9D1DD3DBBC9766EBE801F0929F9F52A67C9C2F505D6923`；`Artifacts\VideoEnhancer.zip` SHA-256 `896B851640621BE287E6C52D18B0E06569D356B6B762398424B2388FB16A8316`；插件 SHA-256 `03E6EC01FACA68EA40AFABF66CE534F48C4F4E6A2305609F7C462DD82D58A033`。ZIP 内 EXE/DLL 与构建源逐项一致。
- UI regression: SDK 插件产物经布局探针验证滚动后 `root.Top=-168`，尺寸同步后仍为 `-168`，回顶为 `0`，PR 构建迁移未破坏顶部空白修复。
- Deployment: 确认 3FUI、videoenhancer、FFmpeg、sidecar 均未运行后备份并覆盖本机 DLL/EXE；备份目录 `C:\Users\maxzr\AppData\Local\Temp\videoenhancer-1.3.3-before-pr3-integration-20260916-1022`，目标哈希与源一致，安装版 `--version` 为 1.3.3。
- Git/next: 本地 `main` 相对 `fork/main` ahead 2（PR 提交），此前候选 1.3.3 源码/测试/记录仍未提交；未推送、未合并远端 PR、未发布。下一步建议提交候选整合改动，并由用户重启 3FUI 做真实 UI 与任务流程验收。

### 2026-09-16 10:49 - Codex

- Request: 用户授权在 GitHub 合并 PR #3，并按正式发布流程发布 1.3.3。
- Git/PR: 候选修复先提交为 `b650231`；GitHub PR #3 以 merge commit `c80c351a664a7fd51fc1ac90aa0934482f7ec5c3` 正式合并。同步远端主线后 cherry-pick 形成发布代码提交 `7af6e211b3eeede57d7d4555d9588e09dda57784`，已推送 `fork/main`；`v1.3.3` 标签指向该提交。
- Release: GitHub Release `https://github.com/maxzrb/VideoEnhancer/releases/tag/v1.3.3` 已创建为正式版本；ModelScope Releases 与 Models 备用 EXE 已同步。Backend 审计为 `UNCHANGED`，继续使用 2026.09.12.1；RTX runtime 未修改，继续使用 2026.09.14.1。
- Verification: 正式构建、Python 29/29、安装器五场景、更新器七场景、Backend 更新器 6/6、发布门禁 5/5 均通过。EXE 16,967,565 bytes / `6A87BF50B39C2056BA521A405730C26FE377CB431FDC7A2E50730AFBB6D615AF`；手动 ZIP 13,973,833 bytes / `8A9538879C94163077E376D7852B6647C12A55BA9641D8348D8CFF9C843E7958`；stable.json 1,219 bytes / `8DB68209FCE0BB74B616E65223C54E6D8E37152CC9AF8797FE89715D306247F1`。GitHub 与 ModelScope 实际下载回读全部一致。
- Deployment: 确认相关进程未运行后，将正式 DLL/EXE 部署到 `C:\Program portable\3FUI\3FUI\Plugin`；备份位于 `C:\Users\maxzr\AppData\Local\Temp\videoenhancer-1.3.3-before-final-release-deploy-20260916-104542`。安装 DLL 为 `AADB2A5B8905544F78FA8D9ECB63F1D15D109EDA8EE2698E786EFF21244F7DEF`，EXE 与正式发布哈希一致，`--version` 返回 1.3.3，未残留 videoenhancer/sidecar/FFmpeg 进程。
- Remaining: 真实 3FUI 中的 HDR 输入、滚动、RTX 预览及开始/暂停/恢复/停止/停止后移除仍需用户重启后验收；发布记录提交 `4698f1e` 已推送，收尾状态已核对。

### 2026-09-16 12:29 - Codex

- Request: 用户反馈启动时 RTX HDR 最大亮度默认值 `1000` 显示为 `000`，并询问中灰度的合适取值。
- Root cause: LakeUI `SingleLineTextBoxRenderer` 在数字框经历窄尺寸中间布局时可能缓存左对齐文本的横向滚动偏移；后续宽度变大时，左对齐路径不会自动清零，于是只裁掉四位数的首位，底层值仍为 `1000`。
- Implementation: `ConfigureRtxHdrNumeric` 先设置 320×34 的临时尺寸、字体和整数值，再交给工作台最终布局；`RtxHdrNumericUpDown` 在尺寸/字体变化时临时使用居中对齐，让 LakeUI 在文本未溢出时清除旧偏移，然后恢复左对齐。新增静态回归断言。
- Verification: `dotnet build VideoEnhancerPlugin/VideoEnhancerPlugin.vbproj -c Release -p:HostBin=...` 0 警告/0 错误；Python 29/29；布局探针显示四项文本 `100`/`100`/`44`/`1000` 且 `scroll=0`；`git diff --check` 通过。
- Deployment: 确认相关进程退出后覆盖本机 `C:\Program portable\3FUI\3FUI\Plugin\videoenhancer.3fui.dll`；备份 `C:\Users\maxzr\AppData\Local\Temp\videoenhancer-1.3.3-before-hdr-display-fix-final-20260916-122756`；源/目标 SHA-256 均为 `DBDFEC451252A61A83F6F6878F2537170D33C4C9984881C8C2011ADBFAC61A71`；安装 EXE `--version` 为 1.3.3，未残留相关进程。
- Decision: 不移动或覆盖已发布的 `v1.3.3`；本轮改动待本地提交，若需要对外发布应作为后续 `1.3.4` 补丁版本。
- Middle gray guidance: NVIDIA TrueHDR 文档给出的 SDK 默认值为 50；当前项目的 44 是面向约 200 nit 纸白/SDR 伽马 2.2 的实用起点。建议先保留 44，显示器或环境偏亮时试 50–60，若中间调过亮/发白试 35–40，并以校准后的显示器和熟悉画面判断。
- Git/next: 本轮源码、测试和记录已提交，尚未推送；用户重启 3FUI 后应确认 `1000` 不再显示为 `000`，并验收滚轮/键盘输入和保存重载。

### 2026-09-16 13:00 - Codex

- Request: 用户要求将插件更新包下载恢复为 ModelScope 首选。
- Decision: 只调整更新包下载顺序；版本清单仍由 GitHub Release 首选、ModelScope 兜底，以保持 GitHub 作为版本权威来源。
- Implementation: `PluginUpdater.DownloadPackageAsync` 先调用 ModelScope resolve URL，失败或校验失败后再使用 GitHub Release 资产；两次下载均经过原有大小和 SHA-256 校验。同步更新根 README、发布流程和 ModelScope 数据集说明。
- Verification: 新增下载顺序静态回归测试；Python 测试 30/30；插件 SDK 构建 0 警告/0 错误；`git diff --check` 通过；静态核对确认清单顺序 GitHub→ModelScope、包下载顺序 ModelScope→GitHub。
- Deployment: 确认相关进程退出后覆盖本机 `C:\Program portable\3FUI\3FUI\Plugin\videoenhancer.3fui.dll`；备份 `C:\Users\maxzr\AppData\Local\Temp\videoenhancer-before-modelscope-download-priority-20260916-125954`；源/目标 SHA-256 均为 `20A9E49E4A8B19A7E8135B448FE3AB0E4FDC2C13FD3B94CB94805063E131A679`；安装 EXE `--version` 为 1.3.3，未残留相关进程。
- Git/next: 已发布的 `v1.3.3` 未改写，本轮源码、测试、文档和记录已提交、尚未推送；如需对外分发此顺序变更，应创建后续 `1.3.4`，不要覆盖既有版本。

### 2026-09-16 14:01 - Codex

- Request: 用户明确授权覆盖远端 `v1.3.3`，使更新包下载恢复为 ModelScope 首选。
- Git: `239f8e2` 与发布规则/说明提交 `7b3c46b` 已推送 `fork/main`；远端标签 `v1.3.3` 已从 `7af6e21` 强制移动到 `7b3c46b`。
- Build/gates: 以 Backend 2026.09.12.1 同目录审计（UNCHANGED）重新执行官方发布构建；解决方案构建 0 警告/0 错误，Python 30/30，安装器、更新器门禁通过。
- Assets: 新 EXE 16,967,974 bytes / `a2a390e3e2065a89bf7db61ce47db9b287080013c8c03fc55f6e8fb7c830137e`；手动 ZIP 13,974,530 bytes / `ab7af3b3b4d5b4b73ffdb62891719749454c239596b4f1b89f8d24085c4c42e4`；stable.json 1,300 bytes / `683764b372df9d97c9aa58af3023f89c70a79c198012b88363ebcb3a4dd48cac`。
- Remote verification: GitHub Release 三资产使用 `--clobber` 覆盖；ModelScope Releases 的 stable.json、EXE、手动 ZIP 和 Models 备用 `Plugin/videoenhancer.exe` 均同步覆盖。两源下载回读与 Models 备用 EXE 均逐字节匹配本地产物；stable.json 的包路径、大小和 SHA-256 一致。
- Release note: 新增 `[更改]更新包下载优先使用 ModelScope，失败后回退 GitHub Release`；Backend 与 RTX runtime 未重新发布。
- Local deployment: 覆盖远端资产后重新部署本机 EXE/DLL；DLL 源/目标 SHA-256 均为 `6B27A41CEBCEE7261F7EB3C4D29B6DE16A0AC569E6EB22639063D82CA2EF05E4`，EXE 源/目标 SHA-256 均为 `A2A390E3E2065A89BF7DB61CE47DB9B287080013C8C03FC55F6E8FB7C830137E`，安装版 EXE `--version` 为 1.3.3，相关进程已退出；部署前备份 `C:\Users\maxzr\AppData\Local\Temp\videoenhancer-1.3.3-before-remote-overwrite-deploy-20260916-140628`。
- Remaining: 用户重启 3FUI 后实机点“检查更新”确认正常路径；当前远端已覆盖，不再创建 1.3.4 仅用于本次下载顺序修订。记录收尾提交 `10f69af` 已推送，需确认工作树干净。

### 2026-09-16 14:06 - Codex

- Deployment follow-up: 远端 `v1.3.3` 覆盖完成后，确认 3FUI、插件、FFmpeg 和 RTX 相关进程均未运行，备份旧本机文件后覆盖最新 EXE/DLL。
- Verification: 本机 DLL 源/目标 SHA-256 `6b27a41cebcee7261f7eb3c4d29b6de16a0ac569e6eb22639063d82ca2ef05e4`，EXE 源/目标 SHA-256 `a2a390e3e2065a89bf7db61ce47db9b287080013c8c03fc55f6e8fb7c830137e`；EXE `--version` 返回 1.3.3，相关进程数 0。
- Backup: `C:\Users\maxzr\AppData\Local\Temp\videoenhancer-1.3.3-before-remote-overwrite-deploy-20260916-140628`。

### 2026-09-19 17:22 - Codex

- Request: 用户要求评估 PR #4、#5 是否可安全合并、是否造成现有功能回退，并在审查后安全合并；随后确认 `C:\Users\maxzr\Downloads\backend.7z` 是 PR #5 的配套增量后端。
- Orientation/Git: 已读取 `AGENTS.md`、`docs/codex/INDEX.md`、`docs/codex/STATUS.md`、HandShake 2.1.0-beta 及冲突流程；启动时 `git pull --ff-only` 为 Already up to date，基线 `7f27068`，工作树干净。
- Review: PR #4 基于旧提交 `615875d` 且与主线冲突，原样会让自动更新用户因便携配置不存在而恢复默认，并有意移除 `core-path`；PR #5 基于当前主线且可自动合并，但提交未包含跨模型后端所需 Python 增量，现有脚本会拒绝不同后端。
- Integration: 先合并 PR #5 为 `637914a`，再解决 PR #4 的 `PluginConfig.vb`、`SegmentedUpscalePage.vb` 冲突并形成 `15bfdc5`。增加旧 AppData 配置首次加载迁移、旧 `videoenhancer.ini core-path` 只读兼容，并修复遗漏的 `ResolveInstalledExePath` 调用。
- Backend: `backend.7z` 为 779,888 bytes，SHA-256 `E306D81770863A16619EC157879AACE84354462BF053751A5AF712545403A70B`，404 文件、无路径穿越。与已安装后端比较仅 4 个文件不同，其中两个 inspector 仅行尾不同；选择性合入 `rve-image-backend.py`、`rve-segmented-backend.py`，未纳入 `__pycache__`、日志或整套旧文件。兼容修复提交为 `9194450`。
- Verification: PR 原始分支分别构建 0/0、Python 24/24 与 30/30；组合版发布构建 0/0、Python 31/31、安装器 5 场景、更新器 7 场景、后端更新器 6/6、`git diff --check` 通过。旧 AppData 配置反射探针生成便携 JSON 且移除 `ExePath`；旧 `core-path` 指向本机后端的 `--check -backend ncnn` 全通过。PR #5 的 32 帧 FFmpeg 两段直连输出 640×360、32/32 包并保留音频；真实 NCNN→CUDA 4 帧混合任务输出 128×128、4/4 帧并成功退出。
- Local side effect: `core-path` 探针按既有自同步逻辑更新了本机安装后端的两个辅助脚本；未覆盖 EXE/DLL。旧脚本恢复副本位于 `C:\Users\maxzr\AppData\Local\Temp\videoenhancer-before-pr5-backend-sync-20260919`。
- Git/remote: `main` 与 `fork/main` 均为 `9194450c75c7e588f72e26433004d33ed569305c`；GitHub PR #4、#5 均显示 MERGED。未改版本号、标签或发布资产。记录更新后需追加收尾提交并推送，最终核对工作树。

### 2026-09-19 22:13 - Codex

- Request: 用户要求在验证通过后按正式流程发布 1.3.4 Release。
- Backend baseline: 从 ModelScope 稳定通道下载并验证已发布 2026.09.12.1 完整包，2,790,833,001 bytes，SHA-256 `E607456F2384F3339F90E28E7C4965CCCACC40F591AB250276E276ECF01636F1`；未误用本机同名但哈希不同的下载归档。
- Backend candidate: 首轮审计发现 4 个替换，其中两个 inspector 仅行尾不同；正式候选从已发布基线构造，仅叠加 `rve-image-backend.py`、`rve-segmented-backend.py`，最终审计 +0/~2/-0。完整包 2,645,812,004 bytes / `42A90717B30CCFE80E5ED3016AD9BD66F0FD59265363397342F3F0B1267DE97C`，增量包 8,296 bytes / `10FD29A868877584F355120C92A8E7421B18A3A3D26DFD80BB69034D54B9D7F9`。
- Version/build: `PluginVersion.vb` 与 CLI csproj 递增到 1.3.4，更新正式 Release Notes。解决方案 build/publish 均为 0 警告/0 错误；正式 EXE 报告 1.3.4。
- Verification: Python 31/31，发布门禁 5/5，安装器 5 场景，更新器 7 场景，Backend 更新器 6/6，`git diff --check` 均通过；完整包已重新解压并与 29,710 文件候选逐文件核对一致。
- Artifacts: EXE 16,988,806 bytes / `8F5E1F618D85AC0AF031287DC3E52F585C7A303464947330C22C66B473C25400`；手动 ZIP 14,002,905 bytes / `C4F8A28E4B7DEC59CA4F9653EA333446C2F4811B90459BB98F85AEF2ED262341`；stable.json SHA-256 `B77B662136A9F275DE547A12E4ED8A030CDBAEC2623BA7F45F4F7463BF787AC6`。
- Git/next: 当前尚未发布远端；下一步提交并推送发布源，然后按 Backend 完整包、增量包、channel、GitHub Release、ModelScope Releases 顺序发布并回读。

### 2026-09-19 22:36 - Codex

- Release: 发布提交 `68dd75e` 已推送；GitHub Release `v1.3.4` 已创建且标签精确指向该提交。按完整包、增量包、channel 顺序发布 Backend 2026.09.19.1 后，再发布 GitHub 与 ModelScope 本体资产。
- Final artifacts: EXE 16,988,813 bytes / `75517B1B8F2DA9281CB9690AE1C2BD5D88A58AFE26AC51C9A99A0666B620357A`；手动 ZIP 14,002,992 bytes / `5F9942B6278A9FECF4AEEA702AAD2BAE7DEA61E470C113D346214C06E64547D5`；stable.json 973 bytes / `B4D48B4FAA84C2083C95E801EA3E782CF66820E7AA7CDCD71265FE1F91DF3630`。
- Backend: 完整包 2,645,812,004 bytes / `42A90717B30CCFE80E5ED3016AD9BD66F0FD59265363397342F3F0B1267DE97C`；增量包 8,292 bytes / `12CCD23C450A773890E501C4615C8B1E2D30445A489E9064340A6AB5D975BFDC`；channel.json 780 bytes / `ACD1EAB32290CEED002D1058FCE29FBB8F284078F9F6CAE76584A04F5D1D0B28`。
- Remote verification: GitHub 三资产 API 摘要与实际下载均匹配；ModelScope Releases 三资产、Models 备用 EXE、Backend channel/patch 实际下载匹配；Backend 全量包由服务端 LFS Size/SHA-256 回读匹配。
- Local deployment: 无相关进程运行时备份并覆盖 EXE/DLL；备份 `C:\Users\maxzr\AppData\Local\Temp\videoenhancer-1.3.4-before-final-release-deploy-20260919-223501`。EXE 返回 1.3.4，DLL/EXE 源目标哈希一致；正式通道 Backend 增量更新成功，最终状态 current 2026.09.19.1，NCNN 环境检查全部通过。
- Catalog: 安装版下载清单 96 项，`Plugin/videoenhancer.exe` 恰好 1 项，`PotPlayer.7z` 为 0 项。
- Remaining: 用户重启 3FUI 后验收新增分段 UI、Anime4K、暂停/停止和长视频音画同步；记录提交推送后核对工作树干净。

### 2026-09-20 09:53 - Codex

- Request: 用户在 3FUI 截图中标出分段超分页四处问题：刷新视频列表按钮过大、跨模型后端标题列不足、入/出点等表格列宽不足、开关比例应与超分工作台 DPI 缩放一致。
- Orientation/Git: 按 HandShake 2.1.0-beta 读取 `AGENTS.md`、`docs/codex/INDEX.md`、`docs/codex/STATUS.md` 与完整协议；`git pull --ff-only` 为 Already up to date，起始 `main`/`fork/main` 均为 `60cd313`，工作树干净。本次为同工具发布后续作，不覆写 `v1.3.4`。
- Implementation: 视频选择行改为弹性主列、12px 间距、150px 操作列，刷新按钮固定 138×38 并居中；分段总开关和跨后端开关改为与 `BuildOfficialModeHeader` 相同的 42px 开关列、居中锚定和空 Margin；标题列按实际字体测量并留 12px；表头与数据行共用 `CreateSegmentGridPanel`，入/出点 156px、目标宽高 116px、操作 86px、间距 12px。
- Verification: `dotnet build VideoEnhancer.slnx -c Release -p:HostBin=...` 0 警告/0 错误；分段相关契约测试 8/8、全量 Python 31/31；本机 LakeUI 5.9 运行时布局探针在 144 DPI 下确认表头全部容纳、处理方式弹性列 710px、刷新按钮 138×38、开关 42×30；`git diff --check` 通过。
- Deployment: 相关进程数 0 时备份并覆盖本机 `videoenhancer.3fui.dll`，源/目标 SHA-256 `AB8640BC80E669D69568790D24223BFDBBB25ED43B33800DEE7839B9A9C64238`，EXE 保持 1.3.4；备份 `C:\Users\maxzr\AppData\Local\Temp\videoenhancer-1.3.4-before-segmented-layout-fix-20260920-095313`。
- Git/next: 当前源码、测试及两份 HandShake 记录尚未提交/推送；用户重启 3FUI 视觉验收后再提交，若发布必须递增版本，不能覆盖 `v1.3.4`。

### 2026-09-20 10:18 - Codex

- Request: 用户第二张实机截图指出刷新视频列表按钮列仍偏窄，且按钮应与左侧视频文件框对齐。
- Orientation/Git: 继续按 HandShake 2.1.0-beta 读取完整技能、协议、`docs/codex/INDEX.md` 与 `STATUS.md`；`git pull --ff-only` 为 Already up to date，保留上一轮 4 个预期未提交文件。
- Implementation: 视频行操作列由 150px 扩至 180px；增加透明 `refreshField` 容器，并让按钮使用与 `CreateOfficialField` 编辑器完全相同的 `Y=31` 和 `Height=max(32, ClientHeight-34)`，消除此前在整行垂直居中导致的上移。
- Verification: 分段专项测试 8/8、Python 全量 31/31；解决方案 Release 构建 0 警告/0 错误；`git diff --check` 通过。
- Deployment: 确认相关进程数为 0 后备份并覆盖本机插件 DLL，源/目标 SHA-256 `CA53879719A714983A3E6C361C78681908A8DB702EA5C01CCF948EE571C6AEF4`，大小 4,667,392 bytes；备份 `C:\Users\maxzr\AppData\Local\Temp\videoenhancer-1.3.4-before-segmented-refresh-align-20260920-101801`。
- Git/next: 仍为源码、测试及两份 HandShake 记录 4 个未提交修改；用户重启 3FUI 确认按钮宽度和对齐后建议提交，不改写已发布 `v1.3.4`。

### 2026-09-20 10:50 - Codex

- Request: 用户授权按发布流程正式发布 1.3.5。
- Version/build: `PluginVersion.vb` 与 CLI csproj 统一递增到 1.3.5；Release Notes 仅包含分段页面高 DPI 列宽、开关比例和刷新按钮对齐三项修复；1.3.4 已归档到历史版本。正式发布构建 0 警告/0 错误。
- Gates: Python 31/31、发布门禁 5/5、安装器五场景、更新器七场景、Backend 更新器 6/6、`git diff --check` 均通过。Backend 2026.09.19.1 使用同目录基线/候选审计，结果 +0/~0/-0，因此未上传 Backend 资产或 channel。
- Git/release: 发布提交 `070cd8b37d509f9f594c57f7968ca62a4b34ca63` 已推送 `fork/main`；GitHub 正式 Release `v1.3.5` 已创建，标签精确指向该提交；ModelScope Releases 和 Models 备用 EXE 已同步。
- Remote verification: 最终 EXE 16,989,156 bytes / `38C606EC69B59E3B8E620280753BEE632A3D25BF56155E94C1A048AF291F910E`；手动 ZIP 14,003,730 bytes / `B6893EB0C73791767EF1121868FBD9A430DDDD3D7182182D81B812C831896B74`；stable.json 621 bytes / `032B4502993A5BACBF1DEDA49BEC91C9950D08744CF6A202C1FA12A07DD0A6A2`。GitHub API 摘要及实际下载、ModelScope Releases 三项实际下载、Models 备用 EXE 均一致。
- Local deployment: 相关进程数为 0 时备份并覆盖本机 EXE/DLL；EXE 返回 1.3.5，DLL SHA-256 `9A7C658C68C921199323FF9030DAFC56CFDAD094D968299BD218243238299C63`；备份 `C:\Users\maxzr\AppData\Local\Temp\videoenhancer-1.3.5-before-final-release-deploy-20260920-104937`。
- Git/next: 下一步提交并推送本条发布记录，随后核对工作树干净；用户重启 3FUI 后做最终视觉验收。

### 2026-09-24 12:26 - Codex

- Request: 审查 GitHub PR #7、issue #6、最新评论及安装器逻辑，列出手动调整和真实验收优先级；识别另一份庞大代码文件。
- Orientation/Git: 新 Codex 会话；读取 `AGENTS.md`、`docs/codex/INDEX.md`、`docs/codex/STATUS.md` 和 HandShake skill；`git pull --ff-only` 为 Already up to date，`main`/`fork/main` 为 `1c0b493`，起始工作树干净。fetch PR #7 头 `9bdb378` 到只读引用 `fork/pr-7`。
- Review: GitHub PR #7 为 open、非 draft、MERGEABLE；5 commits/52 files；SteveYu000 于 2026-09-22 评论安装逻辑需手动调整。审查 Bundle、Theme、MSI、旧版清理、更新器、相关测试和源码 diff；issue #6 仍 open，PR 采用 aria2-next 独立分发方案 B。
- Findings: InstallFolder 仅以非空作为 Burn 门禁，未核验宿主 EXE；MSI 直接运行仍有 ProgramFiles64Folder 默认路径；commit 阶段旧配置迁移与清理 Return=ignore，需验证 UAC 账户上下文和失败可见性；HKLM 卸载清理、旧版文件冲突、回滚和残留需真实安装验证。PluginPanel.vb 已拆至 Pages；另一候选大文件为 `cli/Program.cs`（约 6804 行）。
- Verification/next: `git diff --check main...fork/pr-7` 通过；未构建、未运行提权安装、升级、卸载或窗口视觉检查。下一步在隔离的真实 3FUI 测试目录进行按优先级验收，修复发现的问题后再考虑合并与发布；不得以现有自动化代替实机验收。

### 2026-09-24 12:36 - Codex

- Request: 用户要求先做一个安装器，供后续手动验收。
- Git/worktree: `git pull --ff-only` 已同步且未改动主分支；从 PR #7 头 `9bdb378` 创建独立 detached worktree `C:\Codex Program\VideoEnhancer-pr7-installer-20260924`，未合并 PR。主工作树只有前轮及本轮 HandShake 记录修改；候选 worktree 源码干净。
- Environment: .NET SDK 10.0.400。首次使用旧 `FFmpegFreeUI.6.1.39.extracted` HostBin 构建被 LakeUI 3.22 门禁拒绝；改用本机已验证的 `C:\Users\maxzr\AppData\Local\Temp\3fui-core-compat-host`（LakeUI 5.9.0.0）成功构建。这是本机路径，仅供本机会话参考。
- Build: `dotnet publish .\VideoEnhancer.slnx -c Release -p:HostBin=...` 完成，0 警告/0 错误，自动获取并校验锁定版本 aria2-next；CLI `--version` 为 1.3.5。
- Artifacts: Burn EXE 11,690,830 bytes / SHA-256 `7C22857C29094A259AD91BEE8CE8B5506D5DC6D1855B8FDB84599E4ED10395F3`；MSI 10,817,536 bytes / `F408797825F9C0A60719CFAEA4B6C90B20AEA91C2AE1A5BB446EFC6F88759350`；ZIP 11,934,874 bytes / `6A2969F38C0F18E6FC96D31E58DE9BA06BA67E1632955F95345ABF40F770B4FC`。均位于独立 worktree。
- Verification: `release/test-installer.ps1`、`release/test-updater.ps1`、`release/test-release-gates.ps1` 均 PASS；`python -m unittest discover -s cli/tests -p 'test_*.py'` 33/33 OK；主工作树 `git diff --check` 通过。未运行需要 UAC 的安装/升级/卸载，也未目视检查窗口。
- Next: 用户在隔离测试环境验收；特别检查错误安装目录能否被拒绝、UAC 下旧配置迁移、升级回滚与卸载残留。候选仍使用 1.3.5 版本号，不可误作高于已发布 1.3.5 的正式升级包。

### 2026-09-24 12:52 - Codex

- Request: 用户报告未选择目录仍能安装，要求修复路径风险，并解释 StevenYu000 在 PR #7 的实际改动。
- Diagnosis: 只读查询 HKLM `Software\VideoEnhancer\InstallRoot` 得 `C:\Program Files\FFmpegFreeUI\`。该目录无宿主 `FFmpegFreeUI.exe`，却有 PR 安装的插件 DLL 与 CLI。Burn 日志 `VideoEnhancer_20260924123830.log` 显示 `InstallFolder` 初始化为空、传 MSI 的 `THREEFUIROOT` 因条件 false 被跳过；MSI 日志确认目录回落到 `ProgramFiles64Folder\FFmpegFreeUI` 并写入注册表。故此前主题按钮 VisibleCondition 不构成安装门禁。
- Fix: 在独立 PR worktree 的 `installer/Package/Package.wxs` 增加 `VALID3FUIROOT` AppSearch/FileSearch 和 LaunchCondition，安装/修复必须能找到所选根目录的 `FFmpegFreeUI.exe`，卸载与管理映像例外；移除 MSI 内部注册表回填以要求 Burn 显式传参。更新 README、Burn 注释和安装测试，成品 MSI 反编译门禁纳入回归。提交到本地 `fix/pr7-root-guard` 分支 `d68d680`，未推送、未合并。
- Verification: .NET Release publish 0/0；`release/test-installer.ps1` PASS，含 MSI 反编译审计；真实 `msiexec /i /qn` 分别用空和不存在的根目录，均在 LaunchConditions 失败，日志最终 1603，无目标文件写入。修复候选 EXE `Artifacts\VideoEnhancerInstaller-PR7-root-guard.exe` 11,690,596 bytes / SHA-256 `67830A26BE425F47BEBAAB14832A1F38C5F261DC70555C1D12FF3584AC232EDA`。`git diff --check` 通过，修复分支工作树干净。
- Remaining: 旧误装仍位于 `C:\Program Files\FFmpegFreeUI\` 且 HKLM 记录存在；未卸载。修复包仍 1.3.5，需正常卸载旧误装后在隔离环境测有效根目录、UAC 安装/升级/卸载及 Burn/MSI UI；issue #6 仍 open。主分支仅 HandShake 记录未提交。

### 2026-09-24 13:29 - Codex

- Request/decision: 用户明确便携安装器只负责首次选 3FUI 目录，后续更新必须依托已安装的 VideoEnhancer；删除安装器不应影响更新；aria2-next 分发不能卡住本体更新。
- Orientation/Git: 同工具续作，沿用已读取的 AGENTS.md、docs/codex/INDEX.md、STATUS.md 与 HandShake；前序已 `git pull --ff-only`。在 PR #7 独立 worktree 的 `fix/pr7-portable-installer` 分支实施；main 工作树未改业务代码。
- Changes: 新增便携 WinForms 安装器，只接受包含 FFmpegFreeUI.exe 的根目录，复制 Plugin 布局、故障回滚、不写注册表；CLI 内嵌插件 DLL 并恢复 --apply-update 自更新及布局事务；插件下载已校验运行 EXE，等待宿主退出后更新并重启；发布脚本将运行 EXE 作为 stable.json 更新资产，另发首次安装器；更新 README/发布流程及测试；移除不再使用的 WiX 工程。aria2-next 保留为独立首次安装组件，本体更新不触碰。
- Commands/verification: dotnet publish VideoEnhancer.slnx -c Release -p:HostBin=... 成功；python -m unittest discover -s cli/tests -p test_*.py -q 33/33；release/test-installer.ps1 四场景通过；release/test-updater.ps1 自更新、哈希、独立组件保留、迁移故障回滚通过；release/test-release-gates.ps1 5/5；发布脚本 PowerShell 解析通过；git diff --check 通过。
- Remaining/risk: 版本仍为已发布 1.3.5，仅候选，不可同版本覆盖发布；未在真实 3FUI 窗口及需要 UAC 的受保护目录目视/实装。旧版 MSI 误装登记仍在本机，应由用户经 Windows 正常卸载，不要手删 MSI 文件。工作树未提交、未推送；切换设备前建议提交。

### 2026-09-24 14:22 - Codex

- Correction: 用户指出新造 WinForms 安装器太大且不好用，要求改造 PR #7 原方案。核查发现 PR #7 当前 Burn/MSI 与“不注册 Windows 应用”的目标冲突；PR #7 在迁移 WiX 前已有轻量自解包容器。
- Implementation: 删除新 `installer/Portable` 工程，恢复 PR #7 早期 `cli/InstallerBundle.cs` 格式；`cli/InstallerManager.cs` 在现有运行 EXE 内实现双击文件选择 FFmpegFreeUI.exe、显示完整目标 Plugin 路径并确认、事务复制与失败回滚。解决方案发布直接由运行 EXE 附加载荷生成首次安装器，后续插件自更新仍使用纯运行 EXE。无 HKLM / Windows 应用注册。
- Verification: Release publish 成功；新安装器 19,688,089 bytes / SHA-256 8B23BD93973B1A550379AB29BF0EB4F6831FAE7C7CBD68BC371359CE96DF5DA6（前版 WinForms 65,832,375 bytes）；双击文件选择窗口截图目视通过；release/test-installer.ps1、release/test-updater.ps1、Python 33/33 通过。
- Remaining/Git: 确认框和真实写入仍需用户在真实 3FUI/UAC 场景验收；版本仍为 1.3.5 本地候选，独立分支尚未提交、推送、合并或发布。旧 MSI 误装未擅自卸载。

### 2026-09-24 14:42 - Codex

- Request: 用户发现双击轻量安装器闪黑色命令行框再出现文件选择。
- Cause/fix: PR #7 自解包安装器直接以 CLI 控制台 PE 为前缀。构建安装器时只将外层 PE 子系统从 CUI(3) 改为 GUI(2)，容器清单记录并校验修改后的前缀；释放运行 EXE 后恢复 CUI(3)，保持 CLI 标准输出。GUI 构建后故障回滚一度留空事务目录，增加 Windows 短暂占用时的清理重试。
- Verification: Release publish 成功；PE 门禁 GUI=2、运行 CUI=3；双击系统选择窗口已截图目视；安装器门禁连续两次通过错误目录/有效目录/哈希/回滚/无残留，自更新/回滚通过；运行 EXE --version 为 1.3.5；git diff --check 通过。
- Candidate/Git: Artifacts/VideoEnhancerInstaller.exe 19,688,567 bytes / SHA-256 3DC4761624BC7B007548FE676A6F91F3168BB3CCEC1C11F38B98F8091A026543。仍为本地 1.3.5 候选，未提交、未推送、未合并、未发布；真实 UAC 目录和确认框人工操作未验收。

### 2026-09-24 15:13 - Codex

- Request: 用户纠正前两轮安装器方案，明确要 PR #7 的完整窗口，但无需 MSI、项目 HKLM 登记和 Windows 已安装应用入口。
- Changes: 恢复 installer/Bundle 的原主题、中文本地化、RTF 与 WiX 工程；Bundle 改用隐藏入口的 ExePackage 包装便携安装载荷，InstallFolder 初始为空并在窗口中显示目标目录；移除原 MSI 包工程，构建流程和安装验收脚本改为外层 Burn + 内部便携载荷；文档说明 Burn 隐藏缓存及插件独立更新。
- Verification: dotnet publish VideoEnhancer.slnx -c Release 成功且 WiX 0 警告/0 错误；release/test-installer.ps1 通过 PR7 主题、无 MSI、目录、哈希和回滚；release/test-updater.ps1 自更新/回滚通过；Python 33/33；git diff --check 无错误。直接启动 Burn 的日志显示主题载入、InstallFolder=false，未执行真实安装。
- Risk/Git: WiX Burn 为保留 WixStdBA 仍有隐藏注册和缓存，真实窗口视觉、受保护目录/UAC 和实际 3FUI 安装待人工验收。版本保持 1.3.5 本地候选；独立 worktree 与 main 的记录均未提交，建议切换前提交。旧 MSI 登记未擅自卸载。

### 2026-09-24 15:22 - Codex

- Request: 用户实机发现空目录勾选许可条款后仍可点击安装，要求修复。
- Cause: WiX WixStdBA 的 InstallButton 启用状态由许可勾选逻辑强制控制，主题 EnableCondition 被覆盖；仅靠内部载荷拒绝空目录，窗口仍会进入安装流程。
- Changes: 在 installer/Bundle/VideoEnhancerTheme.xml 中将真正的 InstallButton 改为仅 InstallFolder 非空时可见，空目录时同坐标显示没有安装动作的禁用占位按钮；同步更新 release/test-installer.ps1 与 Python 静态契约测试。没有更改便携载荷的空目录拒绝。
- Verification: WiX Release build 0/0；Burn -quiet 空目录退出 1，日志显示 --install-folder ""、载荷失败和缓存清理；安装器错误/有效目录、哈希与回滚测试通过；Python 33/33；git diff --check 无错误。新候选 11,780,040 bytes / SHA-256 EA08A96C4EFC781E9EE34C18F26000F6C3AB2F18A160936A52BA036419D5F342。
- Remaining/Git: 真实可见窗口须关闭旧进程后重开新候选，人工确认勾选条款后占位按钮仍不可安装、选择有效目录后可安装。真实 3FUI/UAC 仍待验收；本地 worktree 与 main 记录未提交、未发布，建议切换前提交。

### 2026-09-24 15:41 - Codex

- Request: 用户指出目录错误只显示系统“函数不正确”，要求给出能让用户修正操作的有效信息。
- Cause: 便携载荷将具体错误写入 stderr，但 Burn 只收到 EXE 退出码 1，在失败页显示通用系统消息。
- Changes: ExePackage 在 WixBundleUILevel=4 的交互模式条件加入 --show-errors；InstallerManager 对空目录、非 3FUI 根目录、无写入权限给出中文说明并在交互模式显示置顶错误弹窗；静默模式保留 stderr 与非零退出码。PR #7 失败页新增目录、权限和关闭宿主的建议；安装器脚本新增错误文案与静默模式门禁。
- Verification: dotnet publish VideoEnhancer.slnx -c Release 成功；最终 WiX build 0/0；实际启动内部载荷并用 Win32 读取空目录错误弹窗文字，退出码 1；Burn -quiet 空目录退出 1，日志确认未传 --show-errors；release/test-installer.ps1 通过空目录、错误目录文案、有效目录哈希及回滚；Python 33/33；git diff --check 无错误。
- Remaining/Git: 用户需关闭旧安装器后，在新 Burn 可见窗口人工确认错误弹窗层级和正确目录的正常安装；真实 3FUI/UAC 尚未验收。1.3.5 为本地未发布候选；独立 worktree 与 main 记录均未提交，切换前建议提交。

### 2026-09-24 15:44 - Codex

- Follow-up: 为避免用户在明确弹窗后仍看到“函数不正确”，移除主题 FailureMessageText 控件，失败页保留中文处理建议和日志链接；测试增加禁止原始系统错误控件的门禁。
- Verification: WiX Release build 0 警告/0 错误；release/test-installer.ps1 通过空目录/错误目录提示、有效目录哈希及回滚；git diff --check 无错误。最终安装器 11,779,774 bytes / SHA-256 5D96341281250BB8CC398D4953B0454446986A88F877A2C2EEFB3C0B3DCC86D9。
- Remaining/Git: 真实 PR #7 可见窗口的弹窗层级与 UAC/3FUI 实装待人工验收；独立 worktree 和 main 记录未提交，未发布，建议切换前提交。

### 2026-09-24 15:50 - Codex

- Request: 用户提供截图，明确指出错误弹窗与安装失败页重复，建议取消额外弹窗。
- Changes: 移除 Bundle.wxs 的 --show-errors 条件命令行及 InstallerManager 的 FailInstall/错误 MessageBox；便携载荷错误仍写 stderr，Burn Failure 页保留目录、权限和关闭宿主的中文建议及日志链接；测试禁止弹窗参数回归。
- Verification: dotnet publish VideoEnhancer.slnx -c Release 0/0；release/test-installer.ps1 空目录、错误目录文案、有效目录哈希及回滚通过；Python 33/33；git diff --check 无错误。最终安装器 11,779,980 bytes / SHA-256 CFD6C6ACAC019ED50A48CF5715F7DE23E2D94B7DABF587B0A2840876270C4838。
- Remaining/Git: 用户可在可见 PR #7 窗口复验一次失败时只有安装失败页。真实 3FUI/UAC 安装仍未验收；本地候选 1.3.5 未提交、未发布，独立 worktree 与 main 记录均不干净，切换前建议提交。

### 2026-09-24 18:34 - Codex

- Request: 用户认可当前安装器版本，授权选择性合并 GitHub PR #7 并推送远端，以便其他作者接手。
- Orientation/sync: 同工具续作；读取 AGENTS.md、docs/codex/INDEX.md、STATUS.md、HandShake skill；git fetch fork main 与 PR 头、git pull --ff-only fork main，远端 main 基线为 1c0b493，PR 头 9bdb378，PR open/mergeable，主工作树仅旧 HandShake 记录未提交。将主工作树独有的 12:26、12:36、12:52 审查记录补入隔离分支，避免交接历史丢失。
- Selection/changes: 接受 PR 的统一版本元数据、第三方工具分离、PluginPanel 页面拆分和 WiX 中文窗口；覆盖原 MSI/HKLM/卸载型方案为首次安装便携复制，保留根目录门禁、失败页中文说明、无重复弹窗；插件自更新独立于安装器和 aria2-next。本次代码/说明提交 169abb9，父链保留 SteveYu000 的五个 PR 提交及 d68d680。
- Commands/verification: dotnet publish VideoEnhancer.slnx -c Release -p:HostBin=... 0/0；release/test-installer.ps1、release/test-updater.ps1、release/test-backend-updater.ps1 6/6、release/test-release-gates.ps1 5/5、Python 33/33、git diff --check 均通过。git push fork HEAD:main 快进 1c0b493..169abb9；gh pr view 7 确认 MERGED，mergeCommit=9bdb378；远端 main=169abb9。
- Remaining/risks: 无新版本或 Release；issue #6 仍 OPEN。未做真实 3FUI/UAC 安装、受保护目录、最后一次 Burn 视觉验收；Burn 隐藏注册/cache 仍存在；旧测试 MSI 误装未卸载。origin 上游未推送。隔离 worktree 将提交本次交接记录；本地主工作树仍有两份记录未提交、本地 main 落后远端。建议后续作者从 fork/main 最新提交接手，并在切换前确认 git status。

### 2026-09-24 18:37 - Codex

- Closeout sync: 代码提交 169abb9、交接提交 3da8ba4 已推至 maxzrb/VideoEnhancer:main；GitHub PR #7 状态 MERGED，SteveYu000 原提交保留。主工作树原先仅有两份 HandShake 记录未提交，其 12:26/12:36/12:52 独有内容已并入交接提交；同步前用具名 stash 保存原样，再 git pull --ff-only fork main，主工作树快进至 3da8ba4。隔离分支亦在 3da8ba4，两处工作树均曾验证干净。
- Remaining: 真实 3FUI/UAC 与最终 Burn 视觉验收仍待执行，issue #6 保持 OPEN，版本仍为 1.3.5、无新 Release。origin=user-Wing 上游未推送。旧测试 MSI 误装未动，Burn 隐藏缓存仍属当前方案限制。具名 stash 保留用于审计，后续无需直接应用到已同步 main。
- Git handoff: 本次状态收尾会再形成一个 docs 提交并推送 fork/main；接手者以远端 main 最新提交为准，切换设备前确认工作树干净。

### 2026-09-24 18:57 - Codex

- Request: 用户要求所有 VideoEnhancer FFmpeg 用途优先复用 3FUI，随后纠正 3FUI FFmpeg 可位于自定义“工作目录”，不一定在 3FUI 根目录。
- Orientation/sync: 同工具续作；读取 AGENTS.md、docs/codex/INDEX.md、STATUS.md、HandShake skill；`git pull --ff-only fork main` 显示已最新，起始 main=3bf2951 且干净。对照 3FUI 官方源码 `设置_v6.vb`、`编码任务_v6.vb`，确认当前工作目录 API 与 `Settings.json` 持久化字段；插件启用时会占用“替代进程文件名”，因此不能用该字段作为 FFmpeg 路径。
- Changes: 新增 `VideoEnhancerPlugin/FfmpegToolResolver.vb`；预览、四宫格、分段探测、时长探测统一查当前 3FUI 工作目录，再查宿主根目录、当前目录/PATH、旧插件目录。排队时将 ffmpeg/ffprobe 解析路径传给 CLI；CLI 新增可选路径参数，独立运行时读取宿主 `Settings.json` 工作目录；更新两处缺失提示和 README。修改文件：`VideoEnhancerPlugin/FfmpegToolResolver.vb`、`PreviewEngine.vb`、`QuadGridForm.vb`、`QueueHook.vb`、`Pages/PluginPanel.SegmentedUpscalePage.vb`、`VideoEnhancerPlugin/README.md`、`cli/Program.cs`、`cli/README.md`、本 STATUS 与 `version/工作进度.md`。下载页的旧 FFmpeg 包安装状态检查未改，因为它只代表独立下载资源是否存在。
- Verification: `dotnet build VideoEnhancer.slnx -c Release -p:HostBin=...` 0 警告/0 错误；`dotnet publish cli/VideoEnhancer.csproj -c Release -p:HostBin=...` 成功；临时 3FUI/Settings.json 夹具中根目录与自定义工作目录同时放置 ffmpeg.exe，`--check` 选择自定义工作目录；`python -m unittest discover -s cli/tests -p test_*.py -q` 33/33；`git diff --check` 无错误。初次编译出现 VB 局部变量 `path` 遮蔽 `Path` 错误，已改名并重新编译通过。
- Remaining/Git: 真实 3FUI 中四类插件功能和真实编码/UAC 安装仍需人工验收；未发布新版本。当前 main 工作树有本次源码/记录改动，未提交、未推送，切换工具或设备前建议提交。旧具名 stash 未动。

### 2026-09-24 19:01 - Codex

- Follow-up: 用户要求提交推送此前完成的 3FUI 工作目录 FFmpeg 路径改动。复核 main/fork/main 基线 3bf2951、改动文件和 `git diff --check`；修正两处注释后提交 f57a5ba `fix: follow 3FUI FFmpeg working directory`。`git push fork main` 快进 3bf2951..f57a5ba；`git ls-remote fork refs/heads/main` 精确等于 f57a5ba；本地工作树当时干净。origin=user-Wing 未推送；未发布新版本。

### 2026-09-24 19:17 - Codex

- Request: 用户指出插件缺少许可证，授权选择合适许可证；异步确认已取得 user-Wing/ARXChem 与 SteveYu000 对现有贡献使用 MIT 的同意。随后要求清除用户可见描述中的 Burn/MSI/安装器保留等防御性解释。
- Research/decision: 查 GitHub/Choose a License 官方 MIT 文本、3FUI 官方 MIT 状态、仓库贡献历史、现有第三方声明。选择标准 MIT，版权归 VideoEnhancer contributors 各自所有；aria2-next GPL-2.0-or-later、SharpCompress MIT、模型/FFmpeg/Python 等仍按各自许可处理。
- Changes: 新增根目录 `LICENSE`，`cli/VideoEnhancer.csproj` 把它嵌入运行 EXE 并放入安装器与手动 ZIP；`cli/Program.cs` 新增 `--license` 展示全文；`release/test-installer.ps1` 检查安装后的 LICENSE 哈希；更新 README、插件/CLI README、手动安装说明、ModelScope 说明、WiX 中文文案和发布流程，用户可见文本改成直接安装/更新步骤。
- Verification: `dotnet publish VideoEnhancer.slnx -c Release -p:HostBin=...` 成功，WiX Bundle 重建 0 警告/0 错误；EXE `--license` 与根目录 LICENSE 按行一致，手动 ZIP LICENSE 按行一致；`release/test-installer.ps1` 通过包含安装后许可证哈希、有效/无效目录、回滚的门禁；`git diff --check` 无错误。首次 EXE 全文比较因 PowerShell `Out-String` 换行格式误报，改为按行比较后通过。
- Git/remaining: 许可证、文案和本次记录尚待提交推送至 fork/main；版本保持 1.3.5，未创建 Release/标签。真实 UAC/3FUI 安装仍待人工验收；不修改 `version/版本迭代记录.md`。

### 2026-09-24 19:20 - Codex

- Steering: 用户补充要求移除各种插件描述中的防御性安装解释，点名安装器 RTF 中关于“可删除安装器/Burn 缓存/已安装应用”的段落。
- Changes: 精简 `README.md`、`VideoEnhancerPlugin/README.md`、`cli/README.md`、`cli/manual-install-readme.txt`、`release/modelscope-README.md` 和 `installer/Bundle/VideoEnhancerTheme.zh-CN.wxl` 的用户文字；重写 `installer/Package/InstallerNotice.rtf` 为直接安装步骤、标准 MIT 全文与第三方组件许可名称。开发专用的 `release/发布流程.md` 保留安装机制事实。
- Verification: 最终 WiX Bundle 重建 0 警告/0 错误，`release/test-installer.ps1` 通过；RTF 解码检查含 MIT 全文且无旧防御性措辞；`git diff --check` 无错误。真实窗口仍需目视确认滚动内容。
- Git: 本轮许可证及文案改动尚未提交推送，接下来提交到 fork/main；没有新版本或 Release。

### 2026-09-24 19:22 - Codex

- Closeout: MIT 许可证、安装器 RTF、用户文案及打包变更提交为 aa0a728 `docs: license VideoEnhancer under MIT`，`git push fork main` 快进 f57a5ba..aa0a728；`git ls-remote fork refs/heads/main` 等于 aa0a728，GitHub license API 返回 MIT。此后工作树曾验证干净，版本仍 1.3.5，未创建新 Release 或标签；origin=user-Wing 未推送。
- Verification summary: Release 解决方案 publish、最终 WiX rebuild 0 警告/0 错误；EXE/ZIP/安装后 LICENSE 一致；安装器空/错误/有效目录、许可证哈希及回滚门禁通过；新 RTF 静态全文与文案检查通过。人工可见窗口、真实 3FUI/UAC 安装仍待验收。
- Handoff: 本次仅剩当前状态记录收尾提交并推送 fork/main；切换工具或设备时以远端 main 最新提交为准。旧具名 stash 不需恢复。

### 2026-09-24 19:33 - Codex

- Request: 用户追问是否还有防御性文字，按已有会话偏好继续清理用户可见描述。
- Orientation/sync: 同工具续作；复核 STATUS 与 Git，`git pull --ff-only fork main` 已最新，基线 6e62d36 且工作树干净。
- Audit/changes: 搜索 README、插件教程、CLI、安装器主题/RTF、手动安装说明、第三方声明和发布页。修改 `README.md`、`VideoEnhancerPlugin/README.md`、`Pages/PluginPanel.TutorialPage.vb`、`cli/README.md`、`cli/InstallerManager.cs`、`cli/Program.cs`、`cli/THIRD-PARTY-NOTICES.txt`、`cli/manual-install-readme.txt`；删除多余的系统注册、故障辩解、PR 编号和实现细节。保留模型/后端真实限制、无效目录提示、迁移冲突规则与第三方许可正文。源码注释及发布流程中的 Burn/MSI 信息是维护者记录，不出现在用户说明中。
- Verification: `dotnet publish VideoEnhancer.slnx -c Release -p:HostBin=...` 成功（WiX 0 警告/0 错误）；`release/test-installer.ps1` 空/错误/有效目录、文件哈希和回滚通过；运行 EXE `--third-party-notices` 输出新文本；`git diff --check` 无错误。未做真实 3FUI/UAC 目视安装。
- Git/remaining: 当前 main 有本轮源码、文案及交接记录改动，待提交推送 fork/main；版本保持 1.3.5，无新 Release。

### 2026-09-24 19:34 - Codex

- Closeout: 本轮用户文案清理提交 d324eb1 `docs: simplify user-facing installation copy`，`git push fork main` 快进 6e62d36..d324eb1；`git ls-remote fork refs/heads/main` 与 d324eb1 一致。工作树在推送后干净。origin=user-Wing 未推送，版本仍 1.3.5，无新 Release。
- Remaining: 当前记录收尾会追加一个 docs 提交并推送；真实 3FUI/UAC 及新安装说明可见窗口目视验收仍未完成。技术开发文档/源码注释中的 Burn、MSI 信息用于维护和测试，用户可见错误原因及第三方许可证仍保留。

### 2026-09-26 18:20 - Codex

- Request: 修复安装器实测后实时预览的 FFmpeg 缺失问题与总开关关闭时两处下拉框仍亮的问题；PR #7 已测试完，回到当前项目目录。
- Orientation/Git: 同工具续作，读取 AGENTS.md、docs/codex/INDEX.md、STATUS.md 与 HandShake；`git pull --ff-only fork main` 已最新，起始 main=17af84a 且干净。独立 `fix/pr7-portable-installer` worktree 无未提交文件，HEAD 3bf2951 已被 main 包含，移除前核验绝对路径和 Git 状态；主工作树一直为 `C:\Codex Program\3fui plugin`。
- Diagnosis: 安装的 DLL 与本轮构建 SHA-256 不同，含旧版 `bin` 核心组件提示；实机 3FUI Settings.json 的工作目录为空，宿主与 Plugin 目录无 ffmpeg.exe，系统 Machine PATH 有 FFmpeg 8.1.1。当前源码解析器已支持工作目录、宿主、PATH 等查找；新候选安装后需重启宿主复验。`PluginPanel.UpscalePage.vb` 遗漏 `_cmbInterp` 状态更新，`_cmbRtxHdrMode` 初始化强制 Enabled=True。
- Changes: 为补帧模型及 HDR 处理方式补充总开关和各功能开关门禁；保留原 UTF-8 与 CRLF。移除已完成的 PR #7 隔离 worktree；未改其他旧 PR 审查 worktree 或真实安装目录。文件：`VideoEnhancerPlugin/Pages/PluginPanel.UpscalePage.vb`、本 STATUS、`version/工作进度.md`。
- Verification: 插件 Release 构建 0 警告/0 错误；解决方案 Release publish 成功，WiX 0/0；`release/test-installer.ps1` 通过 PR7 主题、无 MSI、空/错误目录、有效目录哈希与回滚；ZIP 内 DLL 哈希 `FE8241D8632ED290E2F732F7AA18915B771A4A49E8950DF75B5B675DDB4DC9AD` 与新构建一致。候选安装器 `Artifacts/VideoEnhancerInstaller.exe` 11,781,326 bytes / `8591CA72F2B6DF44F3378C22541DD43908CE34E5BD2CE3371670DE7C55D5B24C`。真实窗口视觉和预览运行待用户安装候选并重启 3FUI 验收。
- Git/release: 版本仍 1.3.5，无新 Release；当前源码和记录待提交推送 fork/main，origin 未推送。旧发布安装器尚不含本次修复。

### 2026-09-26 18:22 - Codex

- Closeout: 界面门禁与本次记录提交为 `1b97064` (`fix: disable inactive interpolation and HDR controls`)，`git push fork main` 快进 `17af84a..1b97064`，`git ls-remote fork refs/heads/main` 与本地 HEAD 一致；工作树在推送后干净。候选安装器仍仅位于本地 `Artifacts/VideoEnhancerInstaller.exe`，未发布 Release。后续用候选安装到真实 3FUI、重启宿主并核对预览与下拉框视觉；无需保留 PR #7 测试工作树。

### 2026-09-26 18:25 - Codex

- Steering: 用户补充 3FUI 除工作目录与同级目录，还使用环境变量 PATH 中的 FFmpeg。
- Verification/diagnosis: 实机 Machine 与当前进程 PATH 都包含 `C:\Program portable\ffmpeg-8.1.1-full_build-shared\ffmpeg-8.1.1-full_build-shared\bin`；当前 `FfmpegToolResolver.vb` 与 CLI `Resolve3FuiFfmpegTool` 均会搜索 PATH，因此预览旧错误来自已安装旧 DLL，而非新解析器遗漏 PATH。当前 3FUI 未运行，实机安装目录未被改动。
- Changes: `PreviewEngine.vb`、`QuadGridForm.vb` 的 FFmpeg 缺失提示列出工作目录、程序目录和系统 PATH；更新本 STATUS 与 `version/工作进度.md`。此前界面门禁提交 `1b97064` 已推送到 fork/main。
- Verification: `dotnet publish VideoEnhancer.slnx -c Release -p:HostBin=...` 通过，WiX 0 警告/0 错误；`release/test-installer.ps1` 所有门禁/哈希/回滚通过；`git diff --check` 无错误。候选安装器 11,781,660 bytes / SHA-256 `A4F39F74498A8DBE7315AC81632441134FA2DCD4CA064BDFDA800BAFAC76C9D1`，DLL `ACA73BFF15B33EB23C4E2B19A9115E389BC9AB45BE653455ED285842A2A95323`。真实预览和界面视觉仍待用户重新安装本地候选并重启 3FUI 验收；版本 1.3.5，无新 Release。
- Git: 当前只有两处提示文案和交接记录待提交推送，origin 不推送。

### 2026-09-26 18:27 - Codex

- Closeout: PATH 提示与交接记录提交 `5a23acb` (`fix: clarify FFmpeg lookup paths in preview`) 已推送 fork/main，`git ls-remote` 与本地 HEAD 精确一致，推送后工作树干净。当前本地候选安装器已更新，GitHub 既有 Release 资产未替换；真实 3FUI 复验仍待完成。
