# videoenhancer.3fui.dll — FFmpegFreeUI（3FUI）视频超分插件

为 3FUI 主程序（FFmpegFreeUI.exe / VideoEnhancerGUI.exe）提供的插件：
在左侧导航最底部新增「视频超分」页面，启用后把「准备文件 → 加入编码队列」的
点击处理器替换为 videoenhancer.exe 中转，使队列任务经由 AI 超分 / 补帧后端执行。

## 安装

1. 把编译产物复制到主程序目录下的 Plugin 文件夹：
   ```
   <主程序目录>\Plugin\videoenhancer.3fui.dll
   ```
   （文件名必须以 `.3fui.dll` 结尾，宿主按 `*.3fui.dll` 扫描加载；
   程序集名保持 `videoenhancer`，入口类型为 `videoenhancer.Entry`。）
2. 重启主程序，左侧最下方出现「视频超分」页面。

## 使用

页面采用 LakeUI `ModernTabControl` 四分栏（顶栏标签），顶部「插件总开关」只作用于
「超分主界面」页；「实时预览」「高级功能」「模型转换器」页即使关闭插件总开关也能使用。

```text
┌ 超分主界面 │ 实时预览 │ 高级功能 │ 模型转换器 ┐
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
   `C:\Users\ARXChem\Documents\LakeUIApps\Video Enhancer GUI\videoenhancer.exe`。
   启用后：
   - 「准备文件 → 加入编码队列」按钮被钩子接管；
   - 主程序队列执行进程名被设置为 videoenhancer.exe
     （`设置_v6.实例对象.替代进程文件名`），队列不再直接调用 ffmpeg；
   - 关闭（再次点击）时停止对参数面板的 hook，队列恢复直接执行 ffmpeg。
2. 「超分开关」（BooleanSwitch）——打开时启用放大（需先开启插件总开关，与补帧互斥）；
   右侧「选择推理方式」支持 `NCNN (Vulkan)`（默认）、`CUDA (PyTorch)`、
   `TensorRT (NVIDIA)` 和 `ONNX Runtime`；
   「放大模型」下拉框：首次展开会调用 `videoenhancer.exe --search-models`
   读取 models 目录可用模型并缓存（CUDA 时为 `models` 下的 `.pth/.pt/.pkl` 模型）；
   选择的模型写入插件配置。
3. 「补帧开关」（BooleanSwitch）——打开时启用 RIFE 补帧（与超分互斥，不能同时开启）；
   「补帧模型」下拉框：首次展开会调用 `videoenhancer.exe --list-interp-models`
   读取 `models\RIFE` 下的补帧模型（如 `rife-v4.25`；CUDA 时为 `.pth` 文件名，如 `rife46`）；
   「补帧倍率」下拉框（2/3/4/8 倍）选择后弹窗提示：请前往「视频参数-画面帧」页面
   指定帧率为原视频的 X 倍；倍率作为 `-interp-factor` 传给 videoenhancer.exe。

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

### 高级功能

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

- 可选择或直接拖入 `.pth` 放大模型，调用后端 `convert_tensorrt.py` 离线编译。
- 输出目录自动设置为核心目录的 `models\TensorRT-Personalized`，与预置引擎分开管理。
- 页面说明 TensorRT 的推理效率优势、离线转换特性，以及 Engine 应在实际使用设备上重新编译。

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
- 超分与补帧互斥（不能同时开启）：打开其中一个时另一个会被自动关闭并提示。
- 仅补帧模式（未开超分开关、仅开补帧开关）时任务命令会自动附加 `-no-upscale`。
- CUDA 推理（`-backend cuda`）：超分需在 `models` 下放置 `.pth/.pt/.pkl` 放大模型，
  补帧需在 `models\RIFE` 下放置 `.pth` 补帧模型；当前开启的模式无 `.pth` 模型时
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
- 1.1（RIFE 补帧 + CUDA 推理版）：CLI 支持 videoenhancer.ini（core-path）定位分离部署的后端根目录；
  CLI 新增 `-interp-model / -interp-factor / -no-upscale / --list-interp-models`（RIFE 补帧）；
  插件 UI 简化为「插件总开关」布尔开关置顶，第二排「超分开关 + 放大模型」，
  第三排「选择推理方式」（NCNN / CUDA），第四排「补帧开关 + 补帧模型 + 补帧倍率」；
  超分/补帧互斥；补帧倍率选择后弹窗提示前往「视频参数-画面帧」设置帧率；
  CUDA 推理（PyTorch）按 rve-backend 传参（`-b pytorch --device cuda --pytorch_gpu_id 0`），
  需要 `models\RIFE` 下的 `.pth` 补帧模型；修复补帧强行停止时输出文件被销毁的问题
  （CLI 进程快照枚举改为 Unicode，停止时等待 ffmpeg 写进程 EOF 收尾，已处理部分正常写盘）。
