# videoenhancer CLI

`videoenhancer.exe` 是 `Video Enhancer GUI`（`rve-backend.py`）的命令行中转工具：
把 `-i / -modelpath / -interp-model / -ffmpeg-settings` 等简化参数翻译成后端参数并启动
`python\python\python.exe python\backend\rve-backend.py`，实现"命令很舒服"的视频超分辨率处理。

## 配置

无需 `videoenhancer.ini`。安装程序会把任意版本化发行 EXE 安装为
`3FUI\Plugin\videoenhancer\videoenhancer.exe`，并在同级建立 `models`、`python`、`bin`
三个便携核心目录；插件 DLL 单独保留在 `3FUI\Plugin` 根目录，3FUI 加载后会自动识别子目录 EXE。

## 构建（单文件）

要求：.NET 10 SDK。

```powershell
.\build.ps1
```

等价的手动命令：

```powershell
dotnet publish .\VideoEnhancer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\.publish
Copy-Item .\.publish\videoenhancer.exe "..\FFmpegFreeUI\Plugin\videoenhancer\videoenhancer.exe"
```

产物是自包含单文件 `videoenhancer.exe`。作为 3FUI 插件使用时放在
`Plugin\videoenhancer` 中运行，并与 `bin\`、`python\`、`models\` 同级。

## 用法

```text
videoenhancer.exe -i <输入视频> -modelpath <模型目录> -ffmpeg-settings "<FFmpeg 参数 + 输出路径>"
videoenhancer.exe -i <输入视频> -interp-model <补帧模型> [-no-upscale] -ffmpeg-settings "<FFmpeg 参数 + 输出路径>"
videoenhancer.exe -i <输入视频> -no-upscale -backend cuda -interp-model <CUDA 补帧模型> -ffmpeg-settings "<FFmpeg 参数 + 输出路径>"
```

- `-i` / `--input`：输入视频路径，含空格时加双引号。
- `-modelpath` / `--modelpath` / `--model`：放大模型（完整路径 / models 下相对路径 / 模型名），省略用默认模型。NCNN、CUDA、TensorRT 都会递归搜索 `models` 的分类子目录，并排除独立的 `models\Frame-Interpolation` 和旧 `models\RIFE` 补帧目录；配合 `-no-upscale` 时可不提供（仅补帧模式）。
- `-interp-model` / `--interp-model`：补帧模型，优先放在 `models\Frame-Interpolation` 并使用架构相对路径，如 `RIFE/rife-v4.25`、`GIMM-VFI/gimm-vfi`、`GMFSS/gmfss`；旧 `models\RIFE` 目录继续兼容。NCNN 使用含 `.param/.bin` 的目录，CUDA 递归识别 `.pth/.pt/.pkl`；TensorRT 目前只支持 RIFE `.pth/.pt/.pkl`，首次使用时由 RVE 按实际输入尺寸和当前 GPU 自动构建 Engine，GIMM/GMFSS 暂不提供 TensorRT。
- `-interp-factor <N>`：补帧倍率（帧率倍数，默认 2，需大于 1；透传给 RIFE `--interpolate_factor`）。
- `-interp-backend <ncnn|cuda|tensorrt>`：独立补帧后端；NCNN 使用 Vulkan 模型目录，CUDA/PyTorch 使用权重文件，TensorRT 从 RIFE 权重自动构建 Engine。GIMM-VFI 与 GMFSS 选择后会自动切换到 CUDA。
- `-scene-threshold <N>`：转场检测阈值，使用 RVE 官方外部 0-10 标尺，默认 4；数值越低越敏感，越容易跳过转场处的插帧，直接透传给 RVE。
- `-dynamic-optical-flow`：开启动态光流尺度，仅 CUDA/PyTorch RIFE 有效；TensorRT 会由 RVE 自动禁用。
- `-tile-size <N>`：超分分块边长，0 表示 RVE 默认处理（不按显存自动试探）；显式值是输入帧边长，用于降低峰值显存但会增加处理时间。支持 NCNN、CUDA/PyTorch、TensorRT 和动态输入 ONNX；FlashVSR 不使用该参数。
- `-process-order <upscale-first|interp-first>`：组合处理顺序；同一后端时在单进程内逐帧执行，只进行一次最终编码且不产生整段中间视频；跨后端时才使用无损 RGB FFV1 中间视频（SDR `gbrp10le`，HDR `gbrp16le`），任务结束后自动清理。当前 RVE 的 SDR 内部帧为 8-bit `rgb24`，最终输出指定 10-bit 像素格式不代表 10-bit 模型推理。
- 位深与 HDR：PQ/HLG HDR 使用 16-bit `rgb48le`，且只允许 CUDA/PyTorch 或 TensorRT；NCNN、ONNX、FlashVSR 遇到 HDR 会明确报错，避免静默降为 SDR。
- `-backend <ncnn|cuda|tensorrt|onnx|flashvsr>`：推理后端。`ncnn`（默认，Vulkan）；`cuda`（PyTorch）——
  放大模型使用 `models` 及子目录下的 `.pth/.pt/.pkl/.ckpt/.safetensors` 文件（如 `PTH/AnimeJaNai-V2-2x-Compact-36K`），
  补帧模型使用 `models\Frame-Interpolation` 下的 `.pth/.pt/.pkl` 文件；超分与补帧可独立使用。
  `tensorrt` 的超分只选择 PTH 源模型，任务启动时按当前 GPU、输入宽高、输出倍率、分块、精度及转换器版本自动生成或复用 `models\TensorRT-Cache` 中的 Engine；不再使用远端预置 `.engine`。补帧只选择 RIFE 权重并自动生成 flow/encode Engine，权重或运行时变化会使用新缓存，加载失败会删除成对缓存后自动重建。NCNN 使用递归发现的 `.param/.bin` 模型文件夹。放大模型列表统一以相对 `models` 的路径显示。
- `-no-upscale`：不放大（仅补帧模式，需配合 `-interp-model`）。
- `--inspect-interp-model <权重>`：读取 `.pth/.pt/.pkl` 内部结构并输出架构及 CUDA/TensorRT 能力 JSON，不用文件扩展名或目录名猜测。
- `--inspect-upscale-model <权重>`：安全读取 PTH/PT/CKPT/safetensors 或 ONNX，输出架构、用途、倍率、输入尺寸和后端能力。
- `--import-model <文件、目录或压缩包>`：预检模型并计算 SHA-256；通过后事务安装到 `models\User`，同时生成用户能力清单。支持 PyTorch/safetensors、ONNX、NCNN param+bin 及 ZIP/7Z/RAR 等压缩包；失败模型不会进入工作台下拉栏。
- `--list-model-catalog` / `--list-interp-model-catalog`：按当前后端输出结构化模型列表，包含稳定 ID、架构大类、倍率、来源和后端能力，供 LakeUI 二级模型菜单使用。
- `--list-user-models --json`：输出用户模型的完整能力清单，供模型导入页列表使用。
- `--update-user-model <ID>`：与 `--user-architecture`、`--user-purpose`、`--user-scale`、`--user-input-multiple`、`--user-backends` 配合修正自动识别结果；写入前会校验格式、任务和后端组合。
- `--prepare-interp-engine <RIFE 权重> --prepare-width <宽> --prepare-height <高>`：调用 RVE 的真实 RIFE flow/encode 路径预构建 TensorRT Engine；可加 `--prepare-static-shape`。该命令不经过单帧超分转换器。
- `-ffmpeg-settings`：FFmpeg 编码参数片段，**最后一个参数是输出文件路径**（无 `-o`），末尾 `-y` 表示覆盖。
  - 自动处理：`-map` 流映射会被移除（后端写进程自带映射），`-map_metadata 0` / `-map_chapters 0` 改写为 `1`；
  - 进度行按每秒 1 行节流输出，避免界面闪烁；
  - FPS 精确重算：用「已渲染帧数 / 有效耗时（总耗时 − 暂停耗时）」计算并保留两位小数
    （后端自报为取整且包含暂停时间），ETA 按相同速率重算；暂停状态来自 `-pause-shm` 共享内存字节。
- `-h`：帮助（含 videoenhancer.ini 配置说明）；`-scale <N>`：强制倍率；`--list-models` / `--search-models`：按后端递归列出 `models` 子目录中的放大模型，并排除补帧目录；`--list-interp-models`：列出 `models\Frame-Interpolation` 与旧 `models\RIFE` 下的兼容补帧模型（CUDA 列全部权重，TensorRT 只列可自动构建的 RIFE 权重；均可用 `--json` 输出一行 JSON 数组，供插件下拉框解析）；`--check`：仅检测环境。
- `-pause-shm <ID>`：透传暂停共享内存名（供插件暂停/恢复后端）；`-stop-shm <ID>`：停止共享内存名，字节变 1 时优雅停止，已处理部分正常写入输出文件（退出码 130）。
- `--debug-split`：仅打印 `-ffmpeg-settings` 的拆分结果（`custom_encoder` / `output` / `overwrite`），用于调试 -map 剥除逻辑。

PowerShell 示例：

```powershell
.\videoenhancer.exe -i "D:\videos\input.mp4" -modelpath RealESRGAN-AnimeVideoV3-2x `
    -ffmpeg-settings '-c:v libx264 -preset medium -crf 18 -pix_fmt yuv420p -c:a copy "D:\videos\out.mp4"'

# 仅补帧（2 倍帧率，不放大）：
.\videoenhancer.exe -i "D:\videos\input.mp4" -no-upscale -interp-model rife-v4.25 `
    -ffmpeg-settings '-c:v libx264 -preset medium -crf 18 -pix_fmt yuv420p -c:a copy "D:\videos\out_60fps.mp4"'

# 补帧 + 放大：
.\videoenhancer.exe -i "D:\videos\input.mp4" -modelpath RealESRGAN-AnimeVideoV3-2x -interp-model rife-v4.25 `
    -ffmpeg-settings '-c:v libx264 -preset medium -crf 18 -pix_fmt yuv420p -c:a copy "D:\videos\out.mp4"'

# CUDA 推理（PyTorch，仅补帧，需要 models\Frame-Interpolation\RIFE\*.pth）：
.\videoenhancer.exe -i "D:\videos\input.mp4" -no-upscale -backend cuda -interp-model rife46 `
    -ffmpeg-settings '-c:v libx264 -preset medium -crf 18 -r 60 -c:a copy "D:\videos\out_60fps.mp4"'

# CUDA 超分（PyTorch，需要 models\*.pth 放大模型）：
.\videoenhancer.exe -i "D:\videos\input.mp4" -backend cuda -modelpath AnimeJaNai-V2-2x-Compact-36K `
    -ffmpeg-settings '-c:v libx264 -preset medium -crf 18 -pix_fmt yuv420p -c:a copy "D:\videos\out_cuda.mp4"'
```

## 环境检测

启动时自动检测（任一缺失即报错退出）：

- `bin\ffmpeg\ffmpeg.exe`
- `python\python\python.exe` + `python\backend\rve-backend.py` + python 库
- `models\` 模型库（含 `.param` / `.bin` 的模型文件夹）
- `models\Frame-Interpolation\` 补帧模型库（NCNN：含 `.param/.bin` 的子文件夹；CUDA：`*.pth/*.pt/*.pkl`；TensorRT：`RIFE/*.pth/*.pt/*.pkl`，Engine 首次使用自动生成；缺失时仅提示，不影响纯超分）
- `models\RIFE\` 旧版补帧目录（只为已有安装和配置保留兼容读取，新下载不再写入）

`--check` 会额外运行 `ffmpeg -version`、`import numpy, cv2` 与后端 `--version` 做功能验证。

## ModelScope 镜像

模型下载默认使用 `AerithDream/VideoEnhancer-Models`。如需切换到其他 `owner/name` 仓库，设置环境变量 `VIDEOENHANCER_MODELSCOPE_DATASET`。

下载列表支持 `BasicVSR++` 与 `Frame-Interpolation` 分类。补帧压缩包解压到 `models\Frame-Interpolation`，并在其 `.downloads` 子目录写入逐包安装标记，因此清理下载压缩包后仍能准确识别每个资源是否已安装。

私有仓库需要设置 `VIDEOENHANCER_MODELSCOPE_TOKEN` 或 ModelScope SDK 通用的 `MODELSCOPE_API_TOKEN`。CLI 不会把令牌写入配置文件，也不会读取 `modelscope login` 生成的 Python pickle Cookie；下载私有文件时使用进程内 HTTP 客户端，避免令牌出现在 aria2 命令行参数中。

## Backend 更新与完整修复

`--update-backend` 默认选择下载量最小的增量补丁链。本地受管理文件与补丁基线 SHA-256 不一致时，更新会安全回滚并输出 `BACKEND_FULL_REQUIRED|`。界面随后显示“下载完整修复包”，经用户确认后使用 `--update-backend --force-backend-full` 跳过增量补丁并事务安装通道中的完整 Backend；不会在未确认时自动下载数 GB 完整包。

## 插件自动更新

插件以 GitHub Release 为唯一版本标准：优先读取 `maxzrb/VideoEnhancer` 的 `releases/latest` 及其 `stable.json` 清单资产；GitHub 不可达时读取 ModelScope `stable.json` 兜底。可用 `VIDEOENHANCER_UPDATE_GITHUB_REPO=owner/name` 覆盖检查仓库，`VIDEOENHANCER_UPDATE_GITHUB_TOKEN` 供私有仓库或提高 API 限频使用。更新包下载同样首选 GitHub Release 资产，失败时回退 ModelScope 数据集 `AerithDream/VideoEnhancer-Releases`（可用 `VIDEOENHANCER_UPDATE_DATASET=owner/name` 切换）；两源都校验清单中的大小与 SHA-256。发现更高 SemVer 后必须由用户确认。

从 1.0.6 起，更新资产是单个版本化 `videoenhancer.exe`。1.1.0 起，临时更新器在 3FUI 退出后将旧平铺布局事务迁入 `Plugin\videoenhancer`，把 EXE 安装到子目录并把内嵌 DLL 保留在 `Plugin` 根目录；短暂占用会重试，失败回滚，进程中断后下次安装会先恢复。成功后自动重启 3FUI。布局 JSON 已嵌入 DLL，不再作为更新资产。`--apply-update` 及相关参数是插件内部更新协议，不作为普通处理命令使用。

## 目录结构

```text
Video Enhancer\              # 本 CLI 项目
  VideoEnhancer.csproj
  Program.cs
  build.ps1
  README.md
FFmpegFreeUI\Plugin\
  videoenhancer.3fui.dll
  videoenhancer\
    videoenhancer.exe
    bin\ffmpeg\ffmpeg.exe
    python\python\python.exe
    python\backend\rve-backend.py
    models\...
```

