# Project Status

Last updated: 2026-08-24 12:27
Updated by: Codex

## Current Snapshot

- Current objective: 在独立版本 1.0.3 基线上补齐 RIFE TensorRT 补帧权重，并保证 ModelScope 模型清单可完整分页读取。
- Current state: 当前独立版本仍为 1.0.3，本轮未发布新版本。RIFE TensorRT 接入已提交；5 个官方 RIFE `.pkl` 权重已上传公开数据集 `AerithDream/VideoEnhancer-Models`，远端路径、大小和 SHA-256 全部验证一致。CLI 已修复 ModelScope tree API 默认 100 条分页截断，在线模型页现完整返回 105 项，其中 5 个 RIFE TensorRT 权重可下载、可发现。最新 EXE、插件 DLL 和布局已替换到 `C:\Program portable\3FUI\plugin`，三项哈希与工作区构建物一致。
- Last active agent: Codex
- Likely next agent: user / Codex / ZCode
- Next recommended step: 在 NVIDIA 机器分别实测仅补帧、先超后补和先补后超的首次 Engine 构建及二次缓存命中；确认后再决定是否升至 1.0.4 并发布。

## Active TODO

- [x] Task: 评估并接入自有 ModelScope 模型镜像。
  - Owner: user / Codex
  - Status: `AerithDream/VideoEnhancer-Models` 已接入 CLI，排除 `PotPlayer.7z`；用户为测试暂时转为公开，后续仓库可见性由用户自行处理。2026-08-23 增量同步 `Frame-Interpolation/GIMM-VFI.7z`、`GMFSS.7z`、`RIFE.7z`，并补交旧 `RIFE/RIFE.7z` 兼容包。
  - Notes/blockers: CLI 默认仓库、可配置仓库 ID、显式私库令牌、认证错误码和插件提示均已实现；公开模式分页读取后当前清单对界面返回 105 项（旧 Python 包已隐藏），含 5 个新增 RIFE `.pkl`。真实 CLI 下载 `rife4.6.pkl` 并通过 SHA-256；仍需用户在真实 3FUI 界面验证交互。上游仓库仍没有逐文件 LICENSE/NOTICE/COPYING。

- [x] Task: 审查作者 v1.4.2 测试版并选择性合并。
  - Owner: Codex
  - Status: 已完成反编译审查、选择性移植、构建、隔离模型布局测试、ModelScope 增量同步与实际安装目录部署。
  - Relevant files: `cli/Program.cs`, `cli/README.md`, `VideoEnhancerPlugin/PluginConfig.vb`, `VideoEnhancerPlugin/PluginPanel.vb`
  - Notes/blockers: 保留旧 `models\RIFE` 兼容读取；新版 CUDA 补帧支持 `.pth/.pt/.pkl`；本轮 TensorRT 改为只收 RIFE 权重并由 RVE 自动构建 Engine；BasicVSR++ 优化目录为 1x，官方单 PTH 为 4x。未合并作者硬编码仓库、删除 `core-path`、旧环境检查和 1.4.2 版本号。GPU 推理仍待 NVIDIA 机器实测。

- [ ] Task: 建立独立项目发行与上游同步流程。
  - Owner: user / Codex
  - Status: 独立 SemVer、上游基线记录、版本文档、ModelScope Release 生成脚本与公开稳定通道已完成；GitHub Actions/Release 自动发布和正式上游同步清单仍待后续。
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

- Current known environment: Windows PowerShell，.NET SDK 10，工作区 `D:\pyprogram\3FUI plugin`。
- Recheck required before: 更换 3FUI/LakeUI 宿主版本后重新编译插件。
- Local-only notes: 本次从 3FUI 6.1.39 官方单文件包提取 `FFmpegFreeUI.dll`/`LakeUI.dll` 到临时目录，并通过 `VideoEnhancerPlugin/build.ps1 -HostBin <目录>` 编译；工作区 `videoenhancer.ini` 的 `core-path` 指向不存在的 `C:\PortableSoft\VideoEnhancer-CLI`，用于验证列表命令不再依赖该配置。本机没有可调用的 `nvidia-smi`；可用系统 Python 3.14 和 FFmpeg 8.1.1 完成伪后端/FFV1 测试。终端策略拒绝清理两个 2026-08-24 隔离测试目录，具体路径记录在最新 Session Log。

## Verification And Commands

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
- Branch: `main`
- Last known feature commit: `25dba3b feat: add RIFE TensorRT interpolation support`
- Upstream relation: 相对原作者 `origin/main` 为 ahead 16 / behind 5；`git pull --ff-only` 安全中止，独立维护线不合并该上游。
- Uncommitted changes: 本条记录提交前为 ModelScope 分页修复和收尾记录；完成第二个提交后以 `git status` 为准。
- Working tree clean: 本条记录提交前 no；收尾目标 yes。
- Commit recommended before switching agents/devices: yes，本轮分页修复和记录应先提交；推送 `fork` 需用户另行要求。

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
