# 宿主程序名与质量评测兼容排查

2026-10-20，本地候选修复，版本保持 1.3.13，未发布或部署。

## 安装目录识别（已修复）

安装器与自更新器原来都只检查 `FFmpegFreeUI.exe`，文件选择器也只显示这个名称。
现在统一通过 `HostInstallation` 识别 `FFmpegFreeUI.exe` 和 `FFmpegFreeUI.x64.exe`，不要求用户改名；仍拒绝缺少主程序的目录以及 Plugin 子目录。安装提示与两份安装说明同步。

## 质量评测（确认并修复部分兼容问题，原反馈仍待复验）

反馈来自第三方用户，没有具体错误、复现步骤或其宿主版本，不能认定下列问题就是原反馈的全部原因。

对照本机 3FUI 6.2.38 与上游提交 `457b4c6e0265592be4fefd56a9f8a5c4e3c41dda` 的源码，确认：

- 插件启用时修改 `设置_v6.实例对象.替代进程文件名`。质量评测的滤镜查询和执行也通过 `获取FFmpeg进程文件名()` 使用此全局路径，原 CLI 对 `-hide_banner -filters`、两输入 `-lavfi` 等原生命令报未知参数。
- CLI 现在区分增强任务与原生 FFmpeg 调用，后者使用现有工具解析规则定位实际 FFmpeg，逐参数和逐字节转发标准输入/输出/错误并保留退出码；使用既有 Windows Job 随中转进程退出清理子进程。插件管理命令与带 `-ffmpeg-settings` 的增强任务保持各自路径。
- 原加载逻辑在插件已停用时没有清除旧会话持久化的插件替代路径。现在入口和面板加载都会清理本插件的现行或旧布局路径；停用时保留其他程序的替代路径，不再无条件清空。
- 质量评测会缓存滤镜/模型查询结果，因此修改开关后仍可能需要刷新 VMAF 模型列表或重开评测窗口。这是源码支持的可能机制，尚无原用户日志确认。

**尚未解决：启用插件时内置 VMAF 模型的自动枚举。**
3FUI 会扫描替代程序 EXE 及其相邻运行库中的模型字符串。启用时它扫描的是 VideoEnhancer，原生参数转发无法改变扫描目标。因此不能把本轮 PSNR/SSIM 与滤镜查询通过扩大为完整 VMAF 界面验收。暂时可停用插件后刷新/重开质量评测，或手动选择本地 VMAF 模型；后续需要与宿主配合提供独立的真实 FFmpeg 路径或每任务可执行程序接口，避免全局替代路径影响工具。未加入临时切换全局路径、改写宿主窗口数据等容易影响并行编码的补丁。

上游源码：[设置](https://github.com/Lake1059/FFmpegFreeUI/blob/457b4c6e0265592be4fefd56a9f8a5c4e3c41dda/FFmpegFreeUI/功能/设置_v6.vb)、[质量评测](https://github.com/Lake1059/FFmpegFreeUI/blob/457b4c6e0265592be4fefd56a9f8a5c4e3c41dda/FFmpegFreeUI/集成工具/Form_v6_集成工具_质量评测.vb)。读取副本在项目忽略目录 `Artifacts/host-quality-audit/evidence`。

## 定向验证

- 完整 solution Release publish 通过，生成运行 EXE、安装器、手动 ZIP 与源码 ZIP；未改正式版本。
- 实际内部载荷在项目临时目录验证两种宿主名安装、无效目录拒绝、载荷哈希与故障回滚；自更新在只有 `.x64.exe` 的目录完成 EXE/DLL 更新、独立组件保留和回滚。
- `release/test-ffmpeg-passthrough.py` 对照本机真实 FFmpeg：滤镜查询逐字节相同，PSNR/SSIM 两输入评测成功，二进制 stdin/stdout 原样通过，错误日志与退出码相同，增强命令仍由插件解析。首次测试发现 Windows 子进程未继承重定向流，已改为明确按字节转发并通过复验。
- `ModelMetadataUi --host-runtime` 通过，包含清除本插件路径及保留其他路径的行为检查；既有 `test_rtx_hdr_controls.py` 7 项通过。
- 首次 publish 因 GitHub aria2 下载 EOF 失败；后续通过项目内临时 localhost 服务复用已缓存 2.8.3 二进制，构建内部固定 SHA256 校验仍执行通过。未修改生产下载源；临时服务已关闭。

产物与日志在 `Artifacts/host-compat-20261020`。未自动覆盖本机插件、未启动 3FUI、未跑长视频或 GPU 测速。原反馈用户的实际质量评测异常与 VMAF 自动枚举仍是待办。
