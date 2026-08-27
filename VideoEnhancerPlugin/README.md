# videoenhancer.3fui.dll — FFmpegFreeUI（3FUI）视频超分插件

为 3FUI 主程序（FFmpegFreeUI.exe / VideoEnhancerGUI.exe）提供的插件：
在左侧导航最底部新增「视频超分」页面，启用后把「准备文件 → 加入编码队列」的
点击处理器替换为 videoenhancer.exe 中转，使队列任务经由 AI 超分 / 补帧后端执行。

## 安装

1. 双击 GitHub Release 中的版本化 EXE，选择 3FUI 主程序。安装器会生成：
   ```
   <主程序目录>\Plugin\videoenhancer.3fui.dll
   <主程序目录>\Plugin\videoenhancer\videoenhancer.exe
   <主程序目录>\Plugin\videoenhancer\bin\...
   <主程序目录>\Plugin\videoenhancer\python\...
   <主程序目录>\Plugin\videoenhancer\models\...
   ```
   （文件名必须以 `.3fui.dll` 结尾，宿主按 `*.3fui.dll` 扫描加载；
   程序集名保持 `videoenhancer`，入口类型为 `videoenhancer.Entry`。）
2. 重启主程序，左侧最下方出现「视频超分」页面。

## 使用

页面采用 LakeUI `ModernTabControl` 六个顶栏标签；「对比工具」和独立「模型指南」已经移除，
当前顺序为超分工作台、实时预览、模型下载、模型转换、模型导入、使用教程。

```text
┌ 超分工作台 │ 实时预览 │ 模型下载 │ 模型转换 │ 模型导入 │ 使用教程 ┐
│                                  │
│ [插件总开关]  BooleanSwitch       │
│ [超分开关] BooleanSwitch          │
│    [选择推理方式] ModernComboBox  │
│    [放大模型] ModernComboBox      │
│ [补帧开关] BooleanSwitch          │
│    [补帧模型] ModernComboBox      │
│    [补帧倍率] ModernComboBox      │
└──────────────────────────────────┘
```

### 超分主界面

1. 「插件总开关」（BooleanSwitch，最上方）——打开时若未指定过 videoenhancer.exe，
   会弹出文件选择框，请选择
   `<主程序目录>\Plugin\videoenhancer\videoenhancer.exe`。
   启用后：
   - 「准备文件 → 加入编码队列」按钮被钩子接管；
   - 主程序队列执行进程名被设置为 videoenhancer.exe
     （`设置_v6.实例对象.替代进程文件名`），队列不再直接调用 ffmpeg；
   - 关闭（再次点击）时停止对参数面板的 hook，队列恢复直接执行 ffmpeg。
2. 「超分开关」（BooleanSwitch）——打开时启用放大（需先开启插件总开关，可与补帧组合）；
   右侧「选择推理方式」支持 `NCNN (Vulkan)`（默认）、`CUDA (PyTorch)`、
   `TensorRT (NVIDIA)` 和 `ONNX Runtime`；
   「放大模型」使用 LakeUI 原生二级菜单，调用 `--list-model-catalog` 获取结构化能力，
   按“架构大类 → 具体模型”显示；配置仍保存稳定的 models 相对路径。
3. 「补帧开关」（BooleanSwitch）——打开时启用 RIFE、GIMM-VFI 或 GMFSS 补帧，可与超分组合；
   「补帧模型」同样使用结构化二级菜单，按 RIFE、GMFSS、GIMM 等架构分组；
   「补帧倍率」下拉框（2/3/4/8 倍）直接保存并作为 `-interp-factor` 传给 videoenhancer.exe，不再弹出手动帧率提示。

### 实时预览

- 中央使用原生 .NET `PictureBox`（`SizeMode=Zoom`）预览已完成帧，不再使用
  LakeUI `PixelPictureBox`（其位图切换在快速轮询下不可靠）。
- 顶部「选择预览哪个」`ModernComboBox`：自动枚举编码队列中所有正在执行的任务
  （默认选中最上面的一个）；切换预览对象即时生效。
- 帧源：优先读取任务输出文件并用 ffmpeg 抽取最新帧；对于**直接处理视频**
  （未走超分中转、由 3FUI 原生 ffmpeg 执行）的任务，读取 3FUI 自身进度
  （`task.进度.当前时间`）并用 ffprobe 探测输入文件帧率（`avg_frame_rate`，
  结果缓存）估算帧号后从输入文件抽帧，状态栏标注「原生 ffmpeg」。
- 抽帧策略（修复黑屏）：实测正在写入的 MKV 用 `-sseof` 抽帧必然失败
  （“File ended prematurely”），而用 `-ss` 定位到已写入区域可以成功。
  因此改为按已知进度 `-ss (进度-0.2s)` → 回退 `-1.0s` → `-2.0s` → `-sseof -0.2`
  逐级尝试输出文件，全部失败再回退输入文件；CLI 中转任务没有原生进度时，
  用遥测 `帧号 ÷ 输入帧率` 换算内容位置。抽帧失败会把原因显示在状态栏
  （3 秒后消失），不再无声黑屏。
- 切换判定：固定间隔模式下，时间到达且「输出至少增长 64KB 或进度前进 0.25 秒」
  才切换；无输出文件时靠进度时间前进切换，避免停在第一帧。两次抽帧开始之间
  至少间隔 `max(0.5, 所选切换间隔)` 秒；抽帧进行中收到新进度只记一个待补标记，
  完成后用最新位置补抽一帧（合并突发，高帧率输出不再频繁拉起 ffmpeg 进程）。
  抽帧格式改用 `mjpeg -q:v 2`（解码更快、内存占用更小）。
- 底部「切换频率」下拉框：`0.5 秒 / 1 秒 / 2 秒 / 3 秒 / 关键帧模式`；
  关键帧模式命中新关键帧才切换画面。
- 说明文字：「处理速度较慢时，可能存在预览停顿」。
- 本页不依赖插件总开关；页面不可见时轮询自动暂停（消除切页动画卡顿）。

### 历史对比工具（当前版本已移除）

以下内容仅记录旧版本曾提供的四宫格对比功能；当前插件不再显示或提供该页面。

- 「制作四宫格比对视频」`ModernButton`：打开独立二级窗口 `QuadGridForm`
  （不依赖 FFmpegFreeUI 主界面），支持拖入/浏览 1-4 个视频。
- 选项：输出大小（如 3840x2160）、缩放算法（lanczos 等）、分割线宽度（像素，
  预览区实时渲染分割线，随缩放等比）、分割线颜色（LakeUI `ModernColorDialog`）、
  排版方式、输出按钮。
- 输出命令：`-filter_complex` 将各视频 `scale` 到输出尺寸后按排版截取对应区域，
  `xstack` 拼接 → `drawbox` 画分割线 → 自动生成 `_labels.ass` 字幕标注；
  编码用 `av1_nvenc -preset p1 -cq 28 -b:v 0 -pix_fmt yuv420p10le -c:a copy`。
- 排版自适应：1 个视频直接铺满输出；2 个视频可选上下/左右裁切排版；
  3 个视频采用 1+2（可选四种方向）；4 个视频固定 2×2 四宫格。预览与 ffmpeg 输出
  共用同一套区域：四路依次裁切左上、右上、左下、右下，不拉伸整帧。
- 四宫格预览采用 1.1 的单画面方案：一个 ffmpeg 进程同时读取全部输入并输出已经拼好的
  MJPEG 帧，`PictureBox` 原子替换整张画面。不再创建四个 3FPlayer 子窗口，因此不会出现
  第一格已经到位、其余三格仍在追帧，也避免原生 HWND 覆盖 LakeUI 文字产生重影。
- 播放使用单一壁钟和 15 fps 合成预览流；拖动时间轴期间只更新滑块，松开后只提取最新
  时间点的一张合成帧。连续导入和选项变化有 90ms 合并窗口，四路 2160p 不会积压四次加载。
- 输入卡片显示编号、首帧缩略图和文件名；空卡片的拖入提示为 18pt。预览及烧录
  的文件名位置统一为：1 路左上，2 路左上+右下，3 路分别在各自小块的左上，
  4 路分别在四角。
- 四宫格窗口的文字、标题、按钮和文件名角标使用 LakeUI `HtmlColorLabel` /
  `ModernButton`。只有视频卡片背景与渐变时间轴保留双缓冲自绘，避免透明按钮的文字残影。
- 二级窗口的全部长方形控件由 `videoenhancer-layout.json` 的中心点、宽度和高度驱动；
  窗口缩放时以 1200×720 设计坐标分别换算 X/Y 倍率。构建时 JSON 会嵌入插件并复制到
  DLL 同目录，外部 JSON 优先，便于不重新编译即可调整布局。
- 右侧选项区基于同一组 JSON 设计坐标统一执行 60% 水平缩放，预览区和顶部视频卡片
  自动扩展到新的右侧边界。无边框标题栏的图标、标题文字及其他空白区域均可拖动窗口。
- videoenhancer.exe 路径与「更改路径」按钮已放回「超分主界面」页。

### 模型转换器

- 可选择或直接拖入 `.pth/.pt/.pkl`。页面会读取权重内部结构：普通 `.pth` 放大模型调用 `convert_tensorrt.py`，RIFE 权重调用独立的 flow/encode TensorRT 构建流程，不能互相混用。
- 放大 Engine 输出到 `models\TensorRT-Personalized`；RIFE Engine 缓存在权重旁，并按分辨率、显卡和 TensorRT 运行时隔离。转换进度会实时显示在页面中。
- 页面说明 TensorRT 的推理效率优势、离线转换特性，以及 Engine 应在实际使用设备上重新编译。

### 模型导入

- 支持选择或拖入 PTH、PT、PKL、CKPT、safetensors、ONNX、NCNN param/bin 文件夹，以及 ZIP、7Z、RAR 等压缩包。
- 导入前在临时区预检架构、用途、倍率、通道、输入尺寸要求、精度和后端能力，并按 SHA-256 去重；失败文件不会进入正式模型列表。
- 通过后事务安装到 `models\User\Upscale`、`Interpolation` 或 `Restoration`，并写入 `models\User\model-catalog.json`。普通 1x 修复模型暂不混入超分下拉栏。
- 工作台只显示与当前后端匹配的用户模型；用户模型和内置模型使用同一套 LakeUI 二级架构菜单。
- 已导入模型以 LakeUI 列表展示；双击模型会展开两列能力编辑器，可修正架构、用途、倍率、输入尺寸倍数与后端。文件路径、格式和 SHA-256 保持只读，修正后立即刷新工作台下拉列表。
- 选中用户模型后按 `Delete`，或在模型行上单击右键选择「删除用户模型」；确认后会同时删除 `models\User` 中的安装文件/目录、能力清单记录，并刷新工作台模型菜单。

### 队列执行流程

1. 在「准备文件」页添加视频、在参数面板设置 FFmpeg 编码参数（命令行模板），
   点击「加入编码队列」：
   - 每个文件生成一条编码队列任务；
   - 任务命令 = `-i "<输入>" [-modelpath "<放大模型>"] [-interp-model "<补帧模型>" -backend <ncnn|cuda> -interp-factor <N> -no-upscale] -ffmpeg-settings "<参数面板生成的 FFmpeg 参数 + 输出路径>"`；
   - 任务自动切换到「编码队列」页显示。
2. 队列执行时由 videoenhancer.exe 启动 rve-backend：
   - 日志中的 `FPS: … Current Frame: … ETA: …` 与
     `Total Output Frames: …` 会被插件解析并写回任务进度
     （百分比 / 效率 / 剩余时间 / 当前阶段「视频超分」）；
     FPS 保留两位小数，由 videoenhancer.exe 按「排除暂停时间的有效耗时」精确重算；
   - 进度文本每秒更新一次，灰色参数区保持显示、不再闪烁；
   - 输出文件大小每 2 秒刷新一次（`输出大小文本 / 输出大小KB`）。
3. 除「准备文件」按钮外，编码队列页的拖入文件、右键菜单「添加文件到队列」也走同样的中转（任务命令含 `-pause-shm` / `-stop-shm`）。
4. 编码队列**单个任务右键 → 「预览输出」**：自动切换到 3FUI 主界面左侧「视频超分」页
   并选中该任务（任务未开始时会记住选择，开始执行后自动选中）。
   「预览输出」与插件总开关无关：插件加载即挂载（每 2 秒同步一次），队列窗体实例
   重建后也会自动恢复；右键时先确保菜单项存在再显示菜单。
4. 暂停/恢复：队列页的「暂停/恢复」按钮与空格键会先把暂停字节写入后端共享内存（`-pause-shm`），后端自行暂停/恢复。
5. 停止：队列页「停止」按钮被插件接管——向 `-stop-shm` 共享内存写停止字节，videoenhancer.exe 优雅结束（已处理部分正常写入输出文件，退出码 130）；不再调用 3FUI 原停止逻辑（原逻辑立刻 Kill 中转进程会丢弃输出）。任务状态置为「已停止」。
6. 再次关闭「插件总开关」停用：恢复原「加入编码队列」处理器，并清空
   替代进程文件名，队列恢复直接执行 ffmpeg。

## 配置

插件配置保存在
`%LocalAppData%\FFmpegFreeUI\videoenhancer.plugin.json`
（ExePath / Model / Enabled / UpscaleEnabled / InterpEnabled / InterpModel / InterpFactor / Backend）。
支持环境变量 `VIDEOENHANCER_CONFIG_DIR` 覆盖配置目录（便携/测试用）。

## 自动更新

插件页面加载后会在后台向 GitHub `maxzrb/VideoEnhancer` 的 `releases/latest` 检查更新（GitHub 是版本唯一标准），底部也可手动点击“检查更新”。远端独立 SemVer 高于当前版本时才提示，用户确认后下载并校验更新包；不会静默覆盖运行文件。

模型下载页会显示 `Plugin/videoenhancer.exe` 资源，但“下载全部”会排除当前插件 EXE；正式升级使用底部“检查更新”入口。插件本体不混入模型解压目录。

从 1.0.6 起，Release 只分发内嵌插件 DLL 的 EXE。1.1.0 起，新 EXE 作为临时更新器等待 3FUI 完全退出，把旧 `Plugin` 平铺目录事务迁入 `Plugin\videoenhancer`；EXE、Backend、模型和工具进入子目录，DLL 留在根目录。短暂占用自动重试，失败恢复旧布局，中断后下次更新先恢复事务，成功后重启 3FUI。布局 JSON 使用 DLL 内嵌资源，不再单独更新。下载首选 GitHub Release 资产，失败回退 ModelScope 镜像（`VIDEOENHANCER_UPDATE_DATASET=owner/name` 可覆盖）；GitHub 检查或下载不可达时均使用 ModelScope 兜底，检查仓库可用 `VIDEOENHANCER_UPDATE_GITHUB_REPO=owner/name` 覆盖；配置中的 `AutoCheckUpdates` 可关闭启动后台检查。

## 构建

在 `VideoEnhancerPlugin\` 目录执行：

```
pwsh -ExecutionPolicy Bypass -File .\build.ps1
```

产物：`out\videoenhancer.dll`，脚本自动复制为
`..\Video Enhancer GUI\Plugin\videoenhancer.3fui.dll`。

依赖：
- .NET SDK 10（使用自带 Roslyn vbc，无需 NuGet restore）；
- 3FUI 开发版程序集：
  `FFmpegFreeUI\FFmpegFreeUI\bin\Debug\net10.0-windows10.0.26100.0\FFmpegFreeUI.dll` 与 `LakeUI.dll`。

## 宿主兼容说明

- 依赖 3FUI 插件约定（`插件管理.vb`）：
  程序集名 + `.Entry` 类型、静态 `Entry()` 方法、
  `SetHost_AddCustomWinformPanel` 等 6 个回调注入。
- 钩子通过 WinForms `Control` 的事件列表反射实现：
  .NET Core+ 使用 `Events` 属性与 `s_clickEvent` 键，
  .NET Framework 回退到 `events` 字段与 `EventClick` 键。
- 若 3FUI 升级改变「加入编码队列」按钮字段名
  （`_MB_加入编码队列` / `_UltraDetailListView1`）或表单名，
  需同步更新 `HostAccess.vb` 中的查找逻辑。

## 注意事项

- 启用后队列任务不再直接执行 ffmpeg，而是执行 videoenhancer.exe
  （内部再调用 ffmpeg + rve-backend），请确保 videoenhancer.exe 的
  bin\ffmpeg / python / models 环境完整（`videoenhancer.exe --check` 可检测）。
- 超分与补帧可同时开启；组合时可选择先超后补或先补后超，跨后端会使用 FFV1 无损中间视频。
- 仅补帧模式（未开超分开关、仅开补帧开关）时任务命令会自动附加 `-no-upscale`。
- CUDA 推理（`-backend cuda`）：超分需在 `models` 下放置 `.pth/.pt/.pkl` 放大模型，
  补帧需在 `models\Frame-Interpolation\RIFE` 下放置 RIFE `.pth/.pt/.pkl` 权重（旧 `models\RIFE` 继续兼容）；当前开启的模式无兼容模型时
  插件自动回退到 NCNN 并在状态区提示。
- 多个插件同时修改 `替代进程文件名` 会互相影响，属已知限制。

## 更新日志

- 1.1（布局同步 + 预览输出常驻 + 预览性能优化 + 发布目标）：UI 按设计器坐标同步——
  修复底部状态栏与选项卡内容区 Dock 重叠（状态栏被选项卡覆盖、预览页底栏被裁切的问题），
  「插件总开关」文案加宽为「关闭此开关时，超分主页面功能不生效」，实时预览页左侧留 30px 边距；
  「预览输出」右键菜单项不再依赖插件总开关（实时预览始终可用），队列窗体重建后自动重挂；
  预览抽帧节流：最小抽帧间隔 + 64KB/0.25 秒阈值 + busy 期间合并待补一帧 + mjpeg 输出；
  deploy.ps1 追加复制插件 DLL 到 `C:\PortableSoft\FFmpegFreeUI ReadyToRun x64\plugin`
  （最新发布版 3FUI 插件目录）与开发版 `Video Enhancer GUI\Plugin`。

- 1.1（实时预览抽帧修复 + 预览输出 + 设计器绝对定位）：实时预览抽帧改用 `-ss` 进度定位
  回退链（修复输出文件写入期间 `-sseof` 必然失败导致的持续黑屏）；CLI 中转任务
  用遥测帧号换算内容位置；抽帧失败原因显示在状态栏；编码队列右键新增「预览输出」
  （先切 3FUI 主界面左侧导航到「视频超分」页，再切到「实时预览」选项卡并选中该任务）；
  设计器（PluginDesigner）的 PreviewLayoutForm 改为绝对坐标布局（无 Dock），
  可在 Visual Studio 设计视图中直接拖动控件；编译产物不含任何运行时调节功能。
- 1.1（实时预览修复 + 四宫格比对工具）：实时预览改用原生 .NET `PictureBox` 抽帧切换
  （修复永远停在第一帧）；新增「选择预览哪个」下拉（多任务时默认最上面一个）；
  支持预览 3FUI 原生 ffmpeg 任务（读取队列进度 + ffprobe 输入帧率估算）；
  切页动画卡顿修复（页面不可见时暂停轮询、动画时长归零）；「补帧开关」标签加宽
  回到一行；exe 路径与说明移回「超分主界面」页；状态提示 5 秒后/切页后自动消失；
  高级功能页新增「制作四宫格比对视频」二级窗口（拖入/浏览、输出大小/缩放算法/
  分割线宽度颜色实时预览、xstack 滤镜、2/3/4 视频排版自适应）；新增实时预览页
  原生布局设计器（PluginDesigner\PreviewLayoutForm）。
- 1.1（ModernTabControl 三分栏 + 实时预览）：插件页面重构为「超分主界面 / 实时预览 / 高级功能」
  三个选项卡；全部文字改用 LakeUI `HtmlColorLabel`（HTML 颜色字体 + 文字对齐 + Dock 自适应）；
  「插件总开关」置顶且仅控制超分主界面页，「实时预览」「高级功能」页不依赖总开关；
  新增实时预览页：`PixelPictureBox` 中央预览 + 切换频率下拉（0.5/1/2/3 秒 + 关键帧模式，
  关键帧模式用 ffprobe 探测输出文件关键帧 pts），按编码队列帧率自动计算切换节奏；
  高级功能页显示 exe 路径与「更改路径」按钮及 HTML 说明文字。
- 1.1 增量更新（CUDA 超分 + FPS 精确化 + 布局微调）：
  超分页面支持 `models` 下的 `.pth/.pt/.pkl` 放大模型（`--list-models -backend cuda` 列出，
  `-backend cuda` 对超分与补帧均生效）；CLI 进度行 FPS 重算为两位小数并排除暂停时间（ETA 同步重算）；
  插件 UI 微调：「插件总开关 / 超分开关 / 补帧开关」文字与左侧开关拉开间距（标签内边距），
  「放大模型」下拉框加宽 20%（380→456）并右移、「超分开关」与「放大模型」之间拉开距离。
- 1.1 曾使用独立路径配置；1.4 已移除此机制，核心目录固定为 videoenhancer.exe 同级；
  CLI 新增 `-interp-model / -interp-factor / -no-upscale / --list-interp-models`（RIFE 补帧）；
  插件 UI 简化为「插件总开关」布尔开关置顶，第二排「超分开关 + 放大模型」，
  第三排「选择推理方式」（NCNN / CUDA），第四排「补帧开关 + 补帧模型 + 补帧倍率」；
  超分/补帧互斥；补帧倍率选择后弹窗提示前往「视频参数-画面帧」设置帧率；
  CUDA 推理（PyTorch）按 rve-backend 传参（`-b pytorch --device cuda --pytorch_gpu_id 0`），
  需要 `models\RIFE` 下的 `.pth` 补帧模型；修复补帧强行停止时输出文件被销毁的问题
  （CLI 进程快照枚举改为 Unicode，停止时等待 ffmpeg 写进程 EOF 收尾，已处理部分正常写盘）。
