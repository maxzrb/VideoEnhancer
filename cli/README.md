# videoenhancer CLI

`videoenhancer.exe` 是 `Video Enhancer GUI`（`rve-backend.py`）的命令行中转工具：
把 `-i / -modelpath / -interp-model / -ffmpeg-settings` 等简化参数翻译成后端参数并启动
`python\python\python.exe python\backend\rve-backend.py`，实现"命令很舒服"的视频超分辨率处理。

## 配置（1.1：后端分离）

CLI 通过 exe 同目录的 `videoenhancer.ini` 定位后端根目录（后端分离部署时使用，
bin\ffmpeg、python、models 无需再与 exe 放在一起）：

```ini
; videoenhancer.ini（与 videoenhancer.exe 同目录）
core-path="C:\PortableSoft\VideoEnhancer-CLI"
```

- 第一行写入 `core-path="<核心程序路径>"`（带引号或裸路径均可，`;`/`#` 开头为注释）；
- 相对路径按 ini 所在目录解析；启动时校验该目录及 bin\ffmpeg、python、models 是否存在，
  缺失时输出"找不到对应的库"并退出（退出码 1）；
- 未放置 `videoenhancer.ini` 时回退到 exe 同目录布局（1.0 兼容）。

## 构建（单文件）

要求：.NET 10 SDK。

```powershell
.\build.ps1
```

等价的手动命令：

```powershell
dotnet publish .\VideoEnhancer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\.publish
Copy-Item .\.publish\videoenhancer.exe "..\Video Enhancer GUI\videoenhancer.exe"
```

产物是自包含单文件 `videoenhancer.exe`，复制到 `Video Enhancer GUI\` 根目录即可运行
（与 `bin\`、`python\`、`models\` 同级）。

## 用法

```text
videoenhancer.exe -i <输入视频> -modelpath <模型目录> -ffmpeg-settings "<FFmpeg 参数 + 输出路径>"
videoenhancer.exe -i <输入视频> -interp-model <补帧模型> [-no-upscale] -ffmpeg-settings "<FFmpeg 参数 + 输出路径>"
videoenhancer.exe -i <输入视频> -no-upscale -backend cuda -interp-model <CUDA 补帧模型> -ffmpeg-settings "<FFmpeg 参数 + 输出路径>"
```

- `-i` / `--input`：输入视频路径，含空格时加双引号。
- `-modelpath` / `--modelpath` / `--model`：放大模型（完整路径 / models 下相对路径 / 模型名），省略用默认模型；配合 `-no-upscale` 时可不提供（仅补帧模式）。
- `-interp-model` / `--interp-model`：补帧模型（RIFE，位于 `models\RIFE\<子文件夹>`，如 `rife-v4.25`，含 `flownet.param` / `flownet.bin`）；可与 `-modelpath` 同时使用（先补帧后放大）；`-backend cuda` 时改为 `.pth` 模型文件名（如 `rife46`）。
- `-interp-factor <N>`：补帧倍率（帧率倍数，默认 2，需大于 1；透传给 RIFE `--interpolate_factor`）。
- `-backend <ncnn|cuda|tensorrt>`：推理后端。`ncnn`（默认，Vulkan）；`cuda`（PyTorch）——
  放大模型使用 `models` 下的 `.pth/.pt/.pkl` 文件（如 `AnimeJaNai-V2-2x-Compact-36K`），
  补帧模型使用 `models\RIFE` 下的 `.pth` 文件；超分与补帧可独立使用。
  `tensorrt` 使用 `models` 及其所有子目录中的 `.engine` 文件；列表中以相对 `models` 的路径显示。
- `-no-upscale`：不放大（仅补帧模式，需配合 `-interp-model`）。
- `-ffmpeg-settings`：FFmpeg 编码参数片段，**最后一个参数是输出文件路径**（无 `-o`），末尾 `-y` 表示覆盖。
  - 自动处理：`-map` 流映射会被移除（后端写进程自带映射），`-map_metadata 0` / `-map_chapters 0` 改写为 `1`；
  - 进度行按每秒 1 行节流输出，避免界面闪烁；
  - FPS 精确重算：用「已渲染帧数 / 有效耗时（总耗时 − 暂停耗时）」计算并保留两位小数
    （后端自报为取整且包含暂停时间），ETA 按相同速率重算；暂停状态来自 `-pause-shm` 共享内存字节。
- `-h`：帮助（含 videoenhancer.ini 配置说明）；`-scale <N>`：强制倍率；`--list-models` / `--search-models`：列出放大模型（加 `-backend cuda` 则列出 `models` 下的 `.pth/.pt/.pkl` 放大模型）；`--list-interp-models`：列出 `models\RIFE` 下的补帧模型（加 `-backend cuda` 则列出 `.pth` 补帧模型；均可用 `--json` 输出一行 JSON 数组，供插件下拉框解析）；`--check`：仅检测环境。
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

# CUDA 推理（PyTorch，仅补帧，需要 models\RIFE\*.pth）：
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
- `models\RIFE\` 补帧模型库（ncnn：含 `flownet.param` / `flownet.bin` 的子文件夹；cuda：`*.pth` 模型文件；缺失时仅提示，不影响纯超分）

`--check` 会额外运行 `ffmpeg -version`、`import numpy, cv2` 与后端 `--version` 做功能验证。

## 目录结构

```text
Video Enhancer\              # 本 CLI 项目
  VideoEnhancer.csproj
  Program.cs
  build.ps1
  README.md
Video Enhancer GUI\          # 运行目录（exe 输出到这里）
  videoenhancer.exe
  videoenhancer.ini              # （可选）core-path="<后端根目录>"，后端分离部署时使用
  bin\ffmpeg\ffmpeg.exe
  python\python\python.exe
  python\backend\rve-backend.py
  models\...
```

