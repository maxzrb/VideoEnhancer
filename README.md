# VideoEnhancer

VideoEnhancer 是一个面向 Windows 的视频增强工具，作为 3FUI 插件和命令行程序使用。它负责连接 FFmpeg、RVE 后端、推理模型与任务队列，提供视频超分辨率、运动补帧、RTX VSR / RTX Video HDR、图片推理和批处理能力。
原作者：[user-wing](https://github.com/user-Wing/VideoEnhancer)

当前稳定版本及发行日期以 [GitHub Releases](https://github.com/maxzrb/VideoEnhancer/releases) 为准。

## 功能概览

- 通过 3FUI 图形界面管理视频增强任务、模型、推理后端和处理顺序。
- 通过 `videoenhancer.exe` 提供命令行处理入口，适合脚本和批量任务。
- 支持视频超分辨率、仅补帧、超分与补帧组合处理，以及 NVIDIA RTX VSR 和 RTX Video HDR。
- 支持图片单张推理和图片文件夹处理；FlashVSR 与 BasicVSR++ 通过无损单帧视频桥处理图片。图片超分已独立为专用页，引擎与模型沿用超分工作台的当前选择。
- 支持 NCNN/Vulkan、CUDA/PyTorch、TensorRT、ONNX、FlashVSR、BasicVSR++ 和 RTX VSR 超分后端。
- 支持为 Windows 图片文件注册“超分辨率 → 模型”当前用户级联右键菜单，结果固定输出为 PNG。
- 支持按视频保存“分段超分”配置；默认按秒设置断点并自动吸附附近关键帧，仍可切换到精确帧模式。默认要求 NCNN/CUDA/TensorRT/ONNX 单帧模型保持同一模型后端；跨模型后端混用作为实验功能，需手动开启。FFmpeg 缩放与 Anime4K 可正常参与分段；固定倍率模型存在时由模型倍率统一决定整片输出尺寸。
- 支持 NCNN、CUDA/PyTorch 和 TensorRT 补帧后端；RIFE TensorRT Engine 会按当前设备自动构建。
- 支持 `upscale-first` 与 `interp-first` 两种组合顺序；跨后端阶段使用临时无损中间文件。
- TensorRT Engine 按 GPU、运行时版本、输入尺寸、倍率、分块、精度和转换配置隔离缓存，并在失效时重建。
- 模型列表支持从 ModelScope 镜像读取、下载、校验和解压。
- RTX 任务支持暂停与恢复，输出容器按输出扩展名直连（mkv/mp4/webm 等全部 FFmpeg 封装器），3FUI 编码参数（预设、调优、码率、CQ）直接传入 RTX 编码器。
- 插件更新检查使用 GitHub Release 首选、ModelScope 兜底；更新包下载使用 ModelScope 首选、GitHub 兜底，均带 SHA-256 校验，由 `videoenhancer.exe` 自行升级和回滚。

## 下载

- 本体发布：<https://github.com/maxzrb/VideoEnhancer/releases>
- 本体镜像：[VideoEnhancer-Releases](https://www.modelscope.cn/datasets/AerithDream/VideoEnhancer-Releases)
- 模型镜像：[VideoEnhancer-Models](https://www.modelscope.cn/datasets/AerithDream/VideoEnhancer-Models)

每个 Release 分别发布运行时更新包、首次安装器、手动安装 ZIP、更新清单及第三方组件对应源码：

```text
VideoEnhancer-<version>-win-x64.exe
VideoEnhancerInstaller-<version>-win-x64.exe
VideoEnhancer-<version>-manual-install.zip
aria2-next-2.5.6-source.tar.gz
stable.json
```

`VideoEnhancer-<version>-win-x64.exe` 是内嵌插件 DLL 的运行时更新包；`VideoEnhancerInstaller-<version>-win-x64.exe` 是首次安装器，内含插件 DLL、运行 EXE、独立的 `aria2-next` 及许可材料。模型、Python、FFmpeg 和其他大型资源需按需获取。

## 系统要求

- Windows 10 1809 或更高版本，64 位系统。`videoenhancer.exe` 是自包含单文件，不要求另外安装 .NET。
- 安装 [Microsoft Visual C++ 2015–2022 x64 运行库](https://aka.ms/vc14/vc_redist.x64.exe)。便携 Python 及部分推理扩展仍依赖该运行库。
- CUDA/PyTorch、TensorRT、FlashVSR 和 BasicVSR++ 需要 NVIDIA GPU。当前后端包含 CUDA 13.0，建议使用 580 或更高版本的 NVIDIA 驱动。
- RTX VSR / RTX Video HDR 需要 NVIDIA RTX 20 系及以上显卡、555 或更高版本的驱动和 RTX Video sidecar 运行组件。运行组件包发布在模型仓库 `Bin/rtx-video/RTXVideoRuntime_20260914.7z`，解压到 `Plugin\videoenhancer\bin\` 并重启 3FUI 即可。sidecar 基于 [`Zennmn/RTXHDR-RTXVSR`](https://github.com/Zennmn/RTXHDR-RTXVSR)（MIT）定制；本项目修改源码发布在 [`maxzrb/RTXHDR-RTXVSR`](https://github.com/maxzrb/RTXHDR-RTXVSR)，包内 NVIDIA SDK 运行库为 NVIDIA 专有组件，按其许可随显卡环境使用。
- NCNN 使用显卡驱动提供的 Vulkan 运行时，不要求安装 Vulkan SDK；显卡和驱动至少需要支持 Vulkan 1.0。

插件的环境检查会针对当前选择的后端实际导入关键模块并检查 GPU/执行提供程序，不会加载模型或 TensorRT Engine。若新机器不能运行，请先按检查结果处理 VC++ 运行库或显卡驱动问题。

## 安装

### 使用 3FUI 插件

1. 安装或准备可运行的 3FUI。
2. 从 GitHub Release 下载 `VideoEnhancerInstaller-<version>-win-x64.exe`。
3. 双击安装程序，点击“选择目录”，选中包含 `FFmpegFreeUI.exe` 的 3FUI 根目录，然后点击“安装”。
4. 启动 3FUI，打开「视频超分」插件页面。
5. 在模型下载页刷新远端清单，按当前后端下载需要的模型和运行环境。

插件配置固定保存在：

```text
Plugin\videoenhancer\videoenhancer.plugin.json
```

插件首次启动时会尝试迁移旧版配置；已有模型和用户文件会保留。

如需移除插件，先关闭 3FUI，再删除 `Plugin\videoenhancer.3fui.dll` 和 `Plugin\videoenhancer` 目录；删除前请备份其中的模型与配置。

### 核心目录

安装后的目录结构为：

```text
Plugin\
├─ videoenhancer.3fui.dll
└─ videoenhancer\
   ├─ videoenhancer.exe
   ├─ videoenhancer.plugin.json
   ├─ cache\...（运行缓存）
   ├─ .work\...（临时工作文件）
   ├─ .update\...（已下载的更新包）
   ├─ THIRD-PARTY-NOTICES.txt
   ├─ licenses\...（第三方许可证与来源说明）
   ├─ bin\aria2-next\aria2-next.exe（独立 GPL 下载组件）
   ├─ bin\embedded-tools\...（随版本同步的 Python 辅助脚本）
   ├─ bin\ffmpeg\ffmpeg.exe
   ├─ bin\rtx-video\runtime\vsr_backend.exe（RTX 运行组件，可选）
   ├─ python\python\python.exe
   ├─ python\backend\rve-backend.py
   └─ models\...
```

首次运行时，安装程序可以创建 `models`、`python` 和 `bin` 目录；模型下载页也可以按资源类别自动放置文件。

插件文件、配置、更新文件和运行缓存保存在 `Plugin\videoenhancer`。临时文件和子进程缓存分别使用其中的 `.work` 与 `cache` 目录。

## 推理后端

| 后端 | 超分 | 补帧 | 说明 |
| --- | --- | --- | --- |
| NCNN/Vulkan | 支持 | 支持 | 使用 Param-Bin 模型目录，适合不依赖 CUDA 的场景 |
| CUDA/PyTorch | 支持 | 支持 | 使用 `.pth`、`.pt` 或 `.pkl` 权重，需要可用 NVIDIA 环境 |
| TensorRT | 支持 | 支持 | 超分使用 PTH 源模型；RIFE 补帧首次使用自动构建 Engine |
| ONNX | 支持 | 不作为通用补帧后端 | 使用 ONNX 模型 |
| FlashVSR | 支持 | 不作为通用补帧后端 | 使用完整 FlashVSR 模型目录 |
| BasicVSR++ | 支持（时序） | 不支持组合 | 使用 BasicVSR++ REDS4 时序模型，与运动补帧互斥 |
| RTX VSR (NVIDIA RTX Video) | 支持 | 不作为补帧后端 | NVIDIA NGX 硬件超分，输出按扩展名直连最终容器，需 RTX 20 系及以上 |

TensorRT 不依赖远端预置 Engine。任务启动时会根据当前视频和设备配置生成或复用本地 Engine。没有 NVIDIA/TensorRT 环境时，应选择 NCNN 或其他可用后端。

BasicVSR++ 与运动补帧不能同时启用；切换到 BasicVSR++ 时，插件会关闭并禁用补帧开关，切回可组合后端后保持关闭但恢复可操作。

“分段超分”默认使用按秒模式：页面先读取视频时长和关键帧，新增或修改断点时会自动吸附到附近关键帧，并始终自动覆盖完整时长；需要逐帧边界时可切换到精确帧模式。默认情况下，NCNN、CUDA/PyTorch、TensorRT、ONNX 单帧模型之间不得跨模型后端混用；这是为了降低环境依赖和后端切换失败概率。页面提供默认关闭的“测试功能：跨模型后端混用”布尔开关，只有手动开启后才允许跨这些模型后端。FFmpeg 的 Lanczos/Bicubic/Bilinear/Nearest/Area/Spline 等缩放方式和 Anime4K libplacebo 着色器不受该实验门禁影响，可与单一模型后端正常组合。**FFmpeg 硬拉与 Anime4K 由 CLI 直接启动 FFmpeg 处理，不再经 Python 逐帧 rawvideo pipe**：纯自定义任务直接从源视频滤镜到最终编码；混合任务中只有模型段生成无损中间块，自定义段直接从原视频进入最终 `filter_complex`，再与模型块一次 concat/编码。运行日志中的 `SEGMENTED_DIRECT_DONE` / `SEGMENTED_DIRECT_GRAPH` 可用于观察直连 FFmpeg 阶段。若存在 2x/4x 等固定倍率模型，所有固定倍率模型必须倍率一致，且该倍率优先决定整片输出分辨率，FFmpeg/Anime4K 段自动跟随；若全片只使用 FFmpeg/Anime4K，则所有段使用统一的自定义目标宽高。当前分段模式仍不与运动补帧、RTX HDR 或 PQ/HLG HDR 输入组合。

## 补帧模型

补帧模型建议放在：

```text
models\Frame-Interpolation\
```

常见目录示例：

```text
models\Frame-Interpolation\RIFE\rife4.25.pkl
models\Frame-Interpolation\RIFE\rife4.26.pkl
models\Frame-Interpolation\RIFE\rife4.26.heavy.pkl
```

旧版 `models\RIFE` 路径仍提供兼容读取，但新下载使用 `Frame-Interpolation` 目录。TensorRT 补帧目前使用 RIFE 权重；GIMM-VFI 和 GMFSS 通过 CUDA/PyTorch 路径使用。

## 命令行用法

查看帮助：

```powershell
.\videoenhancer.exe -h
```


## HDR 和处理顺序

- HDR（PQ/HLG）处理使用 16-bit 中间格式。
- HDR 目前要求 CUDA/PyTorch、TensorRT、BasicVSR++ 或 RTX VSR/HDR；NCNN、ONNX、FlashVSR 会明确拒绝不兼容配置。
- 同一后端的组合处理在单进程内完成，跨后端时使用临时 FFV1 无损中间视频。
- RTX VSR 与补帧组合时固定先补帧、再 RTX；RTX HDR 始终最后执行。
- RTX 输出直接写入最终容器（按输出扩展名选择 FFmpeg 封装器）。容器装不下的流（如 TrueHD 进 MP4）会报 FFmpeg 原生错误；TrueHD/全景声源建议输出 mkv。
- 源视频已是 PQ/HLG 时，RTX HDR 会拒绝重复映射。
- 临时中间文件在任务结束后清理；任务停止时会尽量保留已经生成的有效输出。

## 模型下载和远端资源

模型下载默认使用：

```text
AerithDream/VideoEnhancer-Models
```

可通过 `VIDEOENHANCER_MODELSCOPE_DATASET=owner/name` 切换仓库。私有仓库使用 `VIDEOENHANCER_MODELSCOPE_TOKEN` 或 `MODELSCOPE_API_TOKEN`。令牌不会写入项目配置。

模型页面会显示资源分类、安装状态、文件大小和下载进度。下载压缩包后会自动解压到对应目录，并使用安装标记避免重复处理。

## 自动更新

插件更新顺序如下：

1. 优先从 GitHub `maxzrb/VideoEnhancer` 的最新 Release 读取 `stable.json`。
2. GitHub 检查失败时，从 ModelScope `AerithDream/VideoEnhancer-Releases` 读取 `stable.json`。
3. 下载更新包时优先使用 ModelScope 镜像，失败后使用 GitHub Release 资产。
4. 下载完成后校验 EXE 大小和 SHA-256。
5. 用户确认后，插件复制已校验的运行 EXE 为临时更新器，关闭 3FUI。
6. 更新器等待 3FUI 退出，替换运行 EXE 和插件 DLL；失败时按事务日志恢复旧文件。独立的 `aria2-next` 和模型不参与本体更新。
7. 更新器重新启动 3FUI，插件显示更新结果。

可配置环境变量：

- `VIDEOENHANCER_UPDATE_GITHUB_REPO=owner/name`
- `VIDEOENHANCER_UPDATE_GITHUB_TOKEN`
- `VIDEOENHANCER_UPDATE_DATASET=owner/name`

发现新版本后，可在插件界面确认更新。

## 故障排查

### 环境检查未通过

先确认以下文件存在：

```text
bin\ffmpeg\ffmpeg.exe
python\python\python.exe
python\backend\rve-backend.py
models\
```

模型库、补帧模型库和当前设备不兼容的 TensorRT Engine 属于可选资源，不应阻止插件页面启动。选择 TensorRT 或 CUDA 时，仍需准备对应的 NVIDIA 驱动、运行时和模型权重。

### 补帧开关不可用

BasicVSR++ 不支持与补帧组合。切换到 TensorRT、CUDA、NCNN 或其他可组合超分后端后，补帧开关会恢复可点击，但默认保持关闭。

### TensorRT Engine 为空或构建失败

确认使用的是 PTH 源模型、当前输入尺寸和分块参数有效，并检查 Python/TensorRT/Torch-TensorRT 环境。Engine 会在首次任务启动时构建，本机没有 NVIDIA 时无法完成该流程。

### 自动更新失败

确认 3FUI 已退出、当前用户可写 `Plugin` 目录，且 `videoenhancer.3fui.dll` 位于 `Plugin`、`videoenhancer.exe` 位于 `Plugin\videoenhancer`。如安装在受保护目录，需由用户使用有权限的账户运行 3FUI。更新失败结果保存在 `Plugin\videoenhancer\.update\update-result.txt`。

## 从源码构建

要求安装 .NET 10 SDK 和 PowerShell 7。构建不需要宿主 DLL 或相邻的 3FUI 源码仓库，
LakeUI 编译依赖通过 NuGet 固定为 5.110.0。
插件运行时通过 `HostRuntime.vb` 访问进程中已加载的宿主任务、预设和设置。
插件目标平台与 LakeUI 一致，为 Windows 10 1809 或更新版本。
运行插件的宿主必须提供 LakeUI 5.110+（仅支持 5.x），工作台滚动直接使用公开渲染事务接口。
插件不附带另一份 LakeUI。

仓库根目录的 `VideoEnhancer.slnx` 包含插件与 CLI；CLI 对插件声明了构建依赖，
因此不会再依赖预先存在的 `videoenhancer.3fui.dll`：

```powershell
dotnet build .\VideoEnhancer.slnx -c Release
```

生成安装程序和手动安装包：

```powershell
dotnet publish .\VideoEnhancer.slnx -c Release
```

解决方案发布会生成图形安装程序、运行 EXE 和手动安装 ZIP，并放在仓库根目录的 `Artifacts`：

```text
Artifacts\
  VideoEnhancerInstaller.exe
  videoenhancer.exe
  VideoEnhancer.zip
```

`VideoEnhancer.zip` 已包含手动安装所需的插件 DLL、纯运行版 CLI EXE、独立
`aria2-next`、许可证、来源说明和安装说明。解决方案发布会从上游 v2.5.6
Release 获取 `aria2-next`，SHA-256 不等于项目文件中锁定的值时立即失败；二进制
不再提交到 Git，也不再作为 .NET 嵌入资源。
如果只需要未改名的 CLI 单文件，可执行
`dotnet publish .\cli\VideoEnhancer.csproj -c Release`，产物位于 CLI 的标准
`bin\Release\net10.0-windows\win-x64\publish` 目录，不会生成 `Artifacts`，也不包含
`aria2-next` 或安装器载荷；需要下载功能时必须按上述便携布局配套放置独立组件。
只构建插件时可直接运行：

```powershell
dotnet build .\VideoEnhancerPlugin\VideoEnhancerPlugin.vbproj -c Release
```

如需同时安装插件 DLL，可设置 `VIDEOENHANCER_PLUGIN_DIR`，或附加
`"-p:PluginInstallDir=Artifacts\test-host\Plugin"`。相对路径以仓库根目录为基准。
未指定安装目录时只生成本项目产物。

| 环境变量 | 用途与默认值 |
| --- | --- |
| `VIDEOENHANCER_ARTIFACTS_DIR` | 发布输出目录，默认 `Artifacts`；也可用 MSBuild 的 `ArtifactsDirectory` 覆盖。 |
| `VIDEOENHANCER_PLUGIN_DIR` | 可选的插件安装目录；不设置时不向宿主目录复制。 |
| `VIDEOENHANCER_ARCHIVE_ROOT` | `deploy.ps1` 的存档根目录，默认发布输出目录下的 `releases`。 |
| `VIDEOENHANCER_EXE` | GPU 矩阵测试的 EXE，默认 `Artifacts\videoenhancer.exe`；也可传 `--exe`。 |
| `VIDEOENHANCER_REPOSITORY_ROOT` | 可选的界面测试仓库根目录；通常从测试程序所在目录自动查找。 |

发布、安装和存档目录的环境变量可使用相对仓库根目录的路径。
`deploy.ps1` 也支持 `-ArtifactsRoot`、`-ArchiveRoot` 和 `-PluginDirectories` 参数。

完整发布和门禁流程见 [`release/发布流程.md`](release/发布流程.md)。

## 项目记录

- 当前版本和发布记录：[`version/版本迭代记录.md`](version/版本迭代记录.md)
- 开发进度：[`version/工作进度.md`](version/工作进度.md)
- AI/协作状态：[`docs/codex/STATUS.md`](docs/codex/STATUS.md)

## 模型来源与致谢

下表列出模型及其来源。模型文件可能由原作者发布，也可能由社区转换；具体许可与引用要求以对应项目说明为准。

### 使用的程序与组件

本项目在下列程序与组件之上构建，感谢各自作者与社区：

- **3FUI（FFmpegFreeUI）**：视频处理宿主与插件框架。
- **LakeUI**：3FUI 插件使用的原生界面控件库。
- [FFmpeg](https://ffmpeg.org/)：解码、编码、滤镜与封装核心。
- [Zennmn/RTXHDR-RTXVSR](https://github.com/Zennmn/RTXHDR-RTXVSR)（MIT）：RTX Video sidecar 的基础实现；本项目在其上定制了编码参数透传、容器直连与任务暂停/恢复。
- [NVIDIA RTX Video SDK](https://developer.nvidia.com/rtx-video-sdk)：RTX VSR 与 RTX Video HDR 的专有运行组件。
- [NCNN](https://github.com/Tencent/ncnn)、[PyTorch](https://pytorch.org/)、[TensorRT](https://developer.nvidia.com/tensorrt)、[ONNX Runtime](https://onnxruntime.ai/)：推理运行时。
- [REAL-Video-Enhancer](https://github.com/TNTwise/REAL-Video-Enhancer) 与 [RVE 模型仓库](https://github.com/TNTwise/real-video-enhancer-models)：后端架构参考与模型镜像来源。
- [mkvtoolnix](https://mkvtoolnix.download/)：随包提供的字幕提取与封装工具。
- [ModelScope](https://www.modelscope.cn/)：模型与发布镜像托管。
- [aria2-next](https://github.com/AnInsomniacy/aria2-next)（GPL-2.0-or-later）：以独立进程提供分段和断点续传下载，安装在 `bin\aria2-next`；正式 Release 同时提供固定版本的对应源码归档。
- [SharpCompress](https://github.com/adamhathcock/sharpcompress)（MIT）：提供托管的 ZIP、7z、RAR、TAR、GZip、XZ 和 Zstandard 等格式读取，并负责生成发布流程使用的 7z 归档，取代原先分发和外部调用的 `7za.exe`。

下表覆盖当前模型镜像中可被程序选择的全部模型家族。带“待核实”的条目表示目前只能追溯到 RVE 的公开模型仓库或社区发布记录，尚未找到可确认的原作者正式发布页；这不是对模型所有权或再分发授权的主张。若作者、链接或授权信息有误，欢迎提交 Issue，本项目会及时更正或下架。

| 当前模型家族（包含的格式/变体） | 原作者或项目 | 原始出处 / 可追溯来源 | 授权备注 |
| --- | --- | --- | --- |
| AnimeJaNai V2、V3、V3.1、SD V1 beta（PTH / ONNX / NCNN） | The Database | [mpv-AnimeJaNai](https://github.com/the-database/mpv-AnimeJaNai) | 以原项目和具体模型发布页为准 |
| Ani4K（PTH） | Sirosky | [Upscale-Hub · Ani4K](https://github.com/Sirosky/Upscale-Hub/releases/tag/Ani4K) | 模型发布记录标注 CC-BY-NC-4.0 |
| AniScale2：DITN、ESRGAN、ESRGAN-Lite、Omni、Refiner、SwinIR（PTH） | Sirosky | [Upscale-Hub · AniScale2](https://github.com/Sirosky/Upscale-Hub/releases/tag/AniScale2) | 模型发布记录标注 CC-BY-NC-4.0 |
| AniSD：AC / DC / DB / PS / G6i1 / G6i1b，Compact、SPAN、SwinIR、CRAFT、DAT2、RealPLKSR（PTH / ONNX / NCNN） | Sirosky | [Upscale-Hub · AniSD](https://github.com/Sirosky/Upscale-Hub/releases/tag/AniSD)、[AniSD-RealPLKSR](https://github.com/Sirosky/Upscale-Hub/releases/tag/AniSD-RealPLKSR) | 模型发布记录标注 CC-BY-NC-4.0 |
| AniToon：RPLKSR、RPLKSR-L、RPLKSR-S（PTH） | Sirosky | [Upscale-Hub · AniToon](https://github.com/Sirosky/Upscale-Hub/releases/tag/AniToon) | 以模型发布页为准 |
| OpenProteus Compact（PTH / NCNN） | Sirosky | [Upscale-Hub · OpenProteus](https://github.com/Sirosky/Upscale-Hub/releases/tag/OpenProteus) | 以模型发布页为准 |
| AnimeSR V2（PTH） | Tencent ARC Lab | [AnimeSR](https://github.com/TencentARC/AnimeSR) | 代码与权重条件分别以原项目为准 |
| APISR：DAT、GRL、RRDB，2x / 4x（PTH） | Kiteretsu77 等 APISR 作者 | [APISR](https://github.com/Kiteretsu77/APISR) | 代码与权重条件分别以原项目为准 |
| Real-ESRGAN：AnimeVideoV3、General x4v3、x4plus Anime、JP Illustration（PTH / ONNX / NCNN） | Xintao Wang 等 Real-ESRGAN 作者及社区转换者 | [Real-ESRGAN](https://github.com/xinntao/Real-ESRGAN)、[RVE 模型仓库](https://github.com/TNTwise/real-video-enhancer-models) | JP Illustration 与格式转换文件的原始发布页待进一步核实 |
| Real-CUGAN Conservative（NCNN） | bilibili / nihui 的 NCNN 实现 | [realcugan-ncnn-vulkan](https://github.com/nihui/realcugan-ncnn-vulkan) | 以原项目模型说明为准 |
| Waifu2x：通用、Photo、Noise0–3（NCNN） | nagadomi / nihui 的 NCNN 实现 | [waifu2x](https://github.com/nagadomi/waifu2x)、[waifu2x-ncnn-vulkan](https://github.com/nihui/waifu2x-ncnn-vulkan) | 以各原项目说明为准 |
| DnCNN ColorBlind（NCNN） | Kai Zhang 等 | [DnCNN](https://github.com/cszn/DnCNN) | 当前 NCNN 转换文件来源见 RVE 模型仓库 |
| DenoiseH264 SuperUltraCompact（NCNN） | helaman | [RVE 模型仓库](https://github.com/TNTwise/real-video-enhancer-models) | 原始模型发布页待核实 |
| Nomos8k span OTF：weak、medium、strong（PTH / NCNN） | helaman | [OpenModelDB](https://openmodeldb.info/)、[RVE 模型仓库](https://github.com/TNTwise/real-video-enhancer-models) | OpenModelDB 记录为 CC-BY-4.0；转换文件条件仍以原模型为准 |
| ModernSpanimation V2 / V3（PTH / NCNN） | TNTwise | [REAL-Video-Enhancer](https://github.com/TNTwise/REAL-Video-Enhancer)、[RVE 模型仓库](https://github.com/TNTwise/real-video-enhancer-models) | 以原发布记录为准 |
| BHI SpanPlusDynamic Light（PTH） | 原作者待核实 | [RVE 模型仓库](https://github.com/TNTwise/real-video-enhancer-models) | 当前仅确认社区转换来源，原始发布页待核实 |
| Sudo Shuffle SPAN（PTH） | sudo | [OpenModelDB](https://openmodeldb.info/)、[RVE 模型仓库](https://github.com/TNTwise/real-video-enhancer-models) | 原始 SPAN 变体发布页待核实 |
| RealHatGAN：JP Illustration 1x / 2x / 4x、Universal Illustration 2x（ONNX） | 原作者待核实 | [RVE 模型仓库](https://github.com/TNTwise/real-video-enhancer-models) | 当前仅确认 ONNX 转换来源，原始发布页待核实 |
| FlashVSR（时序超分） | OpenImagingLab | [FlashVSR](https://github.com/OpenImagingLab/FlashVSR) | 原项目代码为 Apache-2.0；权重以原项目说明为准 |
| BasicVSR++ REDS4（时序超分） | OpenMMLab | [MMagic / BasicVSR++](https://github.com/open-mmlab/mmagic) | 原项目代码为 Apache-2.0；权重以模型卡为准 |
| RIFE 4.6、4.7、4.25、4.26、4.26 heavy（NCNN / PyTorch；TensorRT 由本机转换） | Hzwer | [Practical-RIFE](https://github.com/hzwer/Practical-RIFE)、[RVE 模型发布](https://github.com/TNTwise/real-video-enhancer-models/releases/tag/models) | 本机 TensorRT Engine 继承源权重条件，不单独主张授权 |
| GIMM-VFI：F、F-LPIPS、R、R-LPIPS（PyTorch） | GSeanCDAT 等 | [GIMM-VFI](https://github.com/GSeanCDAT/GIMM-VFI) | 代码与权重条件分别以原项目为准 |
| GMFSS Fortuna：Base、Union、Union-AnimeRun（PyTorch） | 98mxr | [GMFSS_Fortuna](https://github.com/98mxr/GMFSS_Fortuna) | 代码与权重条件分别以原项目为准 |

研究或公开发布结果时，请按原项目要求引用相关论文。

## 许可证和第三方资源

VideoEnhancer 项目源码采用 [MIT 许可证](LICENSE)，保留原作者及各贡献者的版权。可用 `videoenhancer.exe --license` 查看运行 EXE 内嵌的许可证全文；首次安装器和手动安装 ZIP 也会安装 `Plugin\videoenhancer\LICENSE.txt`。

3FUI 宿主、aria2-next、SharpCompress、RVE 后端、预训练权重、FFmpeg 和 Python 依赖分别使用各自的许可证。模型权重的许可条件请查看对应项目说明。

安装后的 `THIRD-PARTY-NOTICES.txt` 汇总本体直接分发的第三方组件。`aria2-next` 的 GPLv2 全文、作者与贡献者声明、二进制 SHA-256、上游标签/提交和对应源码地址位于 `licenses\aria2-next`；SharpCompress 的 MIT 许可证位于 `licenses\SharpCompress`。也可运行 `videoenhancer.exe --third-party-notices` 查看内嵌托管组件的声明。正式发布脚本会校验并同时上传 `aria2-next-2.5.6-source.tar.gz`。

RTX 运行组件包（模型仓库 `Bin/rtx-video`）包含基于 MIT 许可 sidecar 的定制构建、LGPL 动态链接的 FFmpeg 共享库，以及 NVIDIA 专有的 `nvngx_*.dll` 运行库；后者按 NVIDIA RTX Video SDK 许可随显卡环境使用，公开再分发前请自行完成许可复核。

本仓库不分发 `PotPlayer.7z`。模型资源的来源和授权状态应以发布记录及远端资源说明为准。

## 反馈

提交问题时，请附上：

- VideoEnhancer 版本和 3FUI 版本；
- Windows 版本、GPU 型号和驱动版本；
- 使用的后端、模型、处理顺序和关键参数；
- `videoenhancer.exe --check` 输出；
- 完整错误日志和可复现步骤。
