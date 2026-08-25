# VideoEnhancer

VideoEnhancer 是一个面向 Windows 的视频增强工具，作为 3FUI 插件和命令行程序使用。它负责连接 FFmpeg、RVE 后端、推理模型与任务队列，提供视频超分辨率、运动补帧、图片推理和批处理能力。
原作者：[user-wing](https://github.com/user-Wing/VideoEnhancer)

当前版本：**1.1.1**

## 功能概览

- 通过 3FUI 图形界面管理视频增强任务、模型、推理后端和处理顺序。
- 通过 `videoenhancer.exe` 提供命令行处理入口，适合脚本和批量任务。
- 支持视频超分辨率、仅补帧、超分与补帧组合处理。
- 支持图片单张推理和图片文件夹处理。
- 支持 NCNN/Vulkan、CUDA/PyTorch、TensorRT、ONNX 和 FlashVSR 超分后端。
- 支持 NCNN、CUDA/PyTorch 和 TensorRT 补帧后端；RIFE TensorRT Engine 会按当前设备自动构建。
- 支持 `upscale-first` 与 `interp-first` 两种组合顺序；跨后端阶段使用临时无损中间文件。
- TensorRT Engine 按 GPU、运行时版本、输入尺寸、倍率、分块、精度和转换配置隔离缓存，并在失效时重建。
- 模型列表支持从 ModelScope 镜像读取、下载、校验和解压。
- 插件更新使用 GitHub Release 首选、ModelScope 兜底的双源机制，更新包带逐文件 SHA-256 校验和失败回滚。

## 下载

- 本体发布：<https://github.com/maxzrb/VideoEnhancer/releases>
- 本体镜像：[VideoEnhancer-Releases](https://www.modelscope.cn/datasets/AerithDream/VideoEnhancer-Releases)
- 模型镜像：[VideoEnhancer-Models](https://www.modelscope.cn/datasets/AerithDream/VideoEnhancer-Models)

每个 Release 只发布一个版本化 EXE 和更新清单：

```text
VideoEnhancer-<version>-win-x64.exe
stable.json
```

插件 DLL 已嵌入 EXE，双击安装或自动更新时会释放到 3FUI 的 `plugin` 目录。模型、Python 运行环境、FFmpeg 和其他大型资源不包含在本体 Release 中，需要在模型下载页按需获取。`PotPlayer.7z` 不属于本项目分发内容。

## 安装

### 使用 3FUI 插件

1. 安装或准备可运行的 3FUI。
2. 从 GitHub Release 下载 `VideoEnhancer-<version>-win-x64.exe`，无需手动改名或移动。
3. 双击版本化 EXE 并选择 3FUI 主程序；安装器会创建 `Plugin\videoenhancer`，把自身安装为固定名称 `videoenhancer.exe`，并将插件 DLL 放在 `Plugin` 根目录。
4. 启动 3FUI，VideoEnhancer 会自动识别子目录中的 `videoenhancer.exe`；也可在页面中手动指定。
5. 在模型下载页刷新远端清单，按当前后端下载需要的模型和运行环境。

插件配置默认保存在：

```text
%LocalAppData%\FFmpegFreeUI\videoenhancer.plugin.json
```

可使用环境变量 `VIDEOENHANCER_CONFIG_DIR` 指定测试或便携配置目录。

### 核心目录

安装后的目录结构为：

```text
Plugin\
├─ videoenhancer.3fui.dll
└─ videoenhancer\
   ├─ videoenhancer.exe
   ├─ bin\ffmpeg\ffmpeg.exe
   ├─ python\python\python.exe
   ├─ python\backend\rve-backend.py
   └─ models\...
```

首次运行时，安装程序可以创建 `models`、`python` 和 `bin` 目录；模型下载页也可以按资源类别自动放置文件。

## 推理后端

| 后端 | 超分 | 补帧 | 说明 |
| --- | --- | --- | --- |
| NCNN/Vulkan | 支持 | 支持 | 使用 Param-Bin 模型目录，适合不依赖 CUDA 的场景 |
| CUDA/PyTorch | 支持 | 支持 | 使用 `.pth`、`.pt` 或 `.pkl` 权重，需要可用 NVIDIA 环境 |
| TensorRT | 支持 | 支持 | 超分使用 PTH 源模型；RIFE 补帧首次使用自动构建 Engine |
| ONNX | 支持 | 不作为通用补帧后端 | 使用 ONNX 模型 |
| FlashVSR | 支持 | 不作为通用补帧后端 | 使用完整 FlashVSR 模型目录 |

TensorRT 不依赖远端预置 Engine。任务启动时会根据当前视频和设备配置生成或复用本地 Engine。没有 NVIDIA/TensorRT 环境时，应选择 NCNN 或其他可用后端。

BasicVSR++ 与运动补帧不能同时启用；切换到 BasicVSR++ 时，插件会关闭并禁用补帧开关，切回可组合后端后保持关闭但恢复可操作。

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
- HDR 目前要求 CUDA/PyTorch 或 TensorRT；NCNN、ONNX、FlashVSR 会明确拒绝不兼容配置。
- 同一后端的组合处理在单进程内完成，跨后端时使用临时 FFV1 无损中间视频。
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
3. 下载更新包时优先使用 GitHub Release 资产，失败后使用 ModelScope 镜像。
4. 下载完成后校验 EXE 大小和 SHA-256。
5. 新 EXE 作为临时更新器等待 3FUI 退出；旧平铺布局会事务迁移到 `Plugin\videoenhancer`，EXE 写入子目录，DLL 保留在 `Plugin` 根目录。短暂占用会重试，持续占用或迁移失败会恢复旧布局；进程中断后下次更新会先恢复未完成事务。

可配置环境变量：

- `VIDEOENHANCER_UPDATE_GITHUB_REPO=owner/name`
- `VIDEOENHANCER_UPDATE_GITHUB_TOKEN`
- `VIDEOENHANCER_UPDATE_DATASET=owner/name`

更新不会静默替换运行文件，需要用户确认；更新器会在成功重启后报告结果。从 `1.0.6` 起采用 EXE-only 更新协议，不兼容旧 ZIP 更新器；旧版本需要手动下载并运行一次 `1.0.6` EXE。

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

确认 3FUI 未被安全软件拦截，且 `videoenhancer.exe` 与插件 DLL 位于同一目录。更新器会先尝试 GitHub，再回退 ModelScope；两源都失败时不会替换本地文件。

## 从源码构建

CLI 要求 .NET 10 SDK：

```powershell
dotnet build .\cli\VideoEnhancer.csproj -c Release --no-restore
& .\cli\build.ps1
```

插件构建需要 .NET 10 SDK、Roslyn VB 编译器和 3FUI 开发版 `FFmpegFreeUI.dll`、`LakeUI.dll`：

```powershell
& .\VideoEnhancerPlugin\build.ps1 `
  -HostBin 'C:\path\to\FFmpegFreeUI.6.1.39.extracted' `
  -SkipInstall
```

完整发布和门禁流程见 [`release/发布流程.md`](release/发布流程.md)。

## 项目记录

- 当前版本和发布记录：[`version/版本迭代记录.md`](version/版本迭代记录.md)
- 开发进度：[`version/工作进度.md`](version/工作进度.md)
- AI/协作状态：[`docs/codex/STATUS.md`](docs/codex/STATUS.md)

## 模型来源与致谢

VideoEnhancer **不声称拥有下列模型或训练成果**。项目只负责模型发现、下载、格式适配和调用；模型名称中的 PTH、ONNX、NCNN、TensorRT 等格式可能是原作者文件，也可能是社区转换文件。相同模型的格式转换不会改变其原作者与原始授权条件。

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

完整镜像与文件级来源仍在持续审计。模型作者不等于模型架构论文作者；表内致谢不会取代原项目的论文引用要求。研究或公开发布结果时，请继续引用原项目 README 中列出的论文。

## 许可证和第三方资源

本项目代码、3FUI 宿主、RVE 后端、预训练权重、FFmpeg、Python 依赖和其他运行资源可能具有不同的许可证和再分发条件。使用或再分发前，请分别查看对应项目和资源的许可证、NOTICE 或来源说明；项目版本号或仓库标签不代表第三方模型权重获得了统一授权。

本仓库不分发 `PotPlayer.7z`。模型资源的来源和授权状态应以发布记录及远端资源说明为准。

## 反馈

提交问题时，请附上：

- VideoEnhancer 版本和 3FUI 版本；
- Windows 版本、GPU 型号和驱动版本；
- 使用的后端、模型、处理顺序和关键参数；
- `videoenhancer.exe --check` 输出；
- 完整错误日志和可复现步骤。
