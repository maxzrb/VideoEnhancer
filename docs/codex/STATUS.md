# Project Status

Last updated: 2026-08-25 14:09
Updated by: Codex

## Current Snapshot

- Current objective: 1.0.11 已发布，为 Backend 增量补丁冲突提供用户确认的完整修复入口，并完成双源资产复核。
- Current state: 源码提交 `d859f7b`、`main`、标签 `v1.0.11` 和 GitHub Release 已发布；GitHub 与 ModelScope 两个数据集的 EXE 均为 17,473,820 字节、SHA-256 `cdfb7f38688778e332da15566e88a98680ebc662624d49d52e0b1f19fbb4027f`，两端 `stable.json` 均为 539 字节、SHA-256 `e9372cc5a9440a95268fa9fa6b76ae485f7d9bb1981d23b0596b6fd22df9a93c`。Backend 2026.08.25.1 审计为 0 add/replace/delete，未重复上传完整包、补丁或 channel。完整修复按“新包暂存验证→旧后端整目录移入事务备份→新目录切换→成功清理/失败恢复”执行。
- Last active agent: Codex
- Likely next agent: user / Codex / ZCode
- Next recommended step: 在另一台电脑先更新到 1.0.11；增量补丁若再次报告 SHA-256 不匹配，使用新增的“下载完整修复包”并确认事务式整体替换。

## Active TODO

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
- 3FUI: 安装路径 `C:\Program portable\3FUI\3FUI`，版本 6.1.39（commit `642ddf4`）；开发程序集缓存位于 `%LocalAppData%\VideoEnhancerDev\FFmpegFreeUI.6.1.39.extracted`，当前插件源码编译通过。
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
- Last known commit: `7030ba1 docs: record backend upload and GPU handoff`
- Remote topology: `fork=https://github.com/maxzrb/VideoEnhancer.git`；`origin=https://github.com/user-Wing/VideoEnhancer.git`。
- Upstream relation: 修复前 `main` 与 `fork/main` 一致；独立维护线不直接合并或推送原作者 `origin`。
- Uncommitted changes: `cli/Program.cs`、`docs/codex/STATUS.md`、`version/工作进度.md`。
- Working tree clean: no，本轮模型解析修复和记录待提交。
- Commit recommended before switching agents/devices: yes；解析修复应作为独立提交，验证后再决定是否推送 `fork/main`。不得发布 1.0.8。

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
