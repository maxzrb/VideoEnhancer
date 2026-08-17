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

页面布局（自上而下，全部为 LakeUI 控件）：

```text
[插件总开关]  BooleanSwitch          ← 第一排（主开关，最上方）
[超分开关]    BooleanSwitch   [放大模型] ModernComboBox   ← 第二排
[补帧开关]    BooleanSwitch   [补帧模型] ModernComboBox   ← 第三排
videoenhancer.exe：<路径>  [更改路径]                      ← 第四排
状态信息区
```

1. 第一排「插件总开关」（布尔开关，最上方）——打开时若未指定过 videoenhancer.exe，
   会弹出文件选择框，请选择
   `C:\Users\ARXChem\Documents\LakeUIApps\Video Enhancer GUI\videoenhancer.exe`。
   启用后：
   - 「准备文件 → 加入编码队列」按钮被钩子接管；
   - 主程序队列执行进程名被设置为 videoenhancer.exe
     （`设置_v6.实例对象.替代进程文件名`），队列不再直接调用 ffmpeg；
   - 关闭（再次点击）时停止对参数面板的 hook，队列恢复直接执行 ffmpeg。
2. 第二排「超分开关」（布尔开关）——打开时启用放大（需先开启插件总开关）；
   右侧「放大模型」下拉框：首次展开会调用 `videoenhancer.exe --search-models`
   读取 models 目录可用模型并缓存；选择的模型写入插件配置。
3. 第三排「补帧开关」（布尔开关）——打开时启用 RIFE 补帧（可与超分同时开启）；
   右侧「补帧模型」下拉框：首次展开会调用 `videoenhancer.exe --list-interp-models`
   读取 `models\RIFE` 下的补帧模型（如 `rife-v4.25`）。
4. 第四排显示 videoenhancer.exe 路径与「更改路径」按钮。
5. 在「准备文件」页添加视频、在参数面板设置 FFmpeg 编码参数（命令行模板），
   点击「加入编码队列」：
   - 每个文件生成一条编码队列任务；
   - 任务命令 = `-i "<输入>" [-modelpath "<放大模型>"] [-interp-model "<补帧模型>" -no-upscale] -ffmpeg-settings "<参数面板生成的 FFmpeg 参数 + 输出路径>"`；
   - 任务自动切换到「编码队列」页显示。
6. 队列执行时由 videoenhancer.exe 启动 rve-backend：
   - 日志中的 `FPS: … Current Frame: … ETA: …` 与
     `Total Output Frames: …` 会被插件解析并写回任务进度
     （百分比 / 效率 / 剩余时间 / 当前阶段「视频超分」）；
   - 进度文本每秒更新一次，灰色参数区保持显示、不再闪烁；
   - 输出文件大小每 2 秒刷新一次（`输出大小文本 / 输出大小KB`）。
7. 除「准备文件」按钮外，编码队列页的拖入文件、右键菜单「添加文件到队列」也走同样的中转（任务命令含 `-pause-shm` / `-stop-shm`）。
8. 暂停/恢复：队列页的「暂停/恢复」按钮与空格键会先把暂停字节写入后端共享内存（`-pause-shm`），后端自行暂停/恢复。
9. 停止：队列页「停止」按钮被插件接管——向 `-stop-shm` 共享内存写停止字节，videoenhancer.exe 优雅结束（已处理部分正常写入输出文件，退出码 130）；不再调用 3FUI 原停止逻辑（原逻辑立刻 Kill 中转进程会丢弃输出）。任务状态置为「已停止」。
10. 再次关闭「插件总开关」停用：恢复原「加入编码队列」处理器，并清空
    替代进程文件名，队列恢复直接执行 ffmpeg。

## 配置

插件配置保存在
`%LocalAppData%\FFmpegFreeUI\videoenhancer.plugin.json`
（ExePath / Model / Enabled / UpscaleEnabled / InterpEnabled / InterpModel）。
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
- 仅补帧模式（未开超分开关、仅开补帧开关）时任务命令会自动附加 `-no-upscale`；
  若超分与补帧同时开启，则先补帧后放大。
- 多个插件同时修改 `替代进程文件名` 会互相影响，属已知限制。

## 更新日志

- 1.1（RIFE 补帧版）：CLI 支持 videoenhancer.ini（core-path）定位分离部署的后端根目录；
  CLI 新增 `-interp-model / -interp-factor / -no-upscale / --list-interp-models`（RIFE 补帧）；
  插件 UI 简化为「插件总开关」布尔开关置顶，第二排「超分开关 + 放大模型」，
  第三排「补帧开关 + 补帧模型」；总开关/超分/补帧开关关闭时停止对参数面板的 hook。