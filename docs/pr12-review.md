# PR #12 审核

审核时间：2026-10-20 15:12，Codex。结论：建议先修复以下两项 P2 问题再合并。

- PR：https://github.com/maxzrb/VideoEnhancer/pull/12
- 固定审核头：`c68cb935677cf6f09a01ff5b60572f7dcee1881f`，基线 `472d62f`。
- 回读 PR 仍为 OPEN、CLEAN；托管检查列表为空，不能称 CI 通过。
- 当前主线已另行提交本地两组修复：`d7441ae`（宿主名/自更新/原生 FFmpeg 工具兼容），`99ed3d9`（图片根目录参数转义/完整错误详情）。未推送、发布或部署。

## 发现

### [P2] 帧率探测超时不能成为永久缓存的 0

位置：`VideoEnhancerPlugin/PreviewEngine.vb:571–589`，关联 `ProbeInputFpsAsync` 第 912 行。

`RunProbeAsync` 将 2500ms 超时转换成空字符串，调用方继而把 `fps=0` 写入 `_inputFpsCache`。对于首次打开耗时超过该限制的有效源文件（如慢磁盘、网络源或繁重的探测），之后即使工具/源已恢复，当前引擎也不再探测同一路径。切换任务、频率或隐藏后再打开仅重置任务游标，没有清除此缓存。

对 CLI 增强任务，缺失输入帧率还会使遥测帧号无法换算内容时间；进行中的容器又可能不支持 EOF 抽帧，因此可能持续没有预览。原生任务已有时间进度时至少会一直缺失估算帧率。这是新引入短超时后新增的失败路径，不能只以现有25项测试通过判定可恢复。

使用真实子进程夹具先延迟5秒，第二次改为即时返回24/1，对同一路径调用实际私有方法，结果：

```text
FPS_TIMEOUT_RETRY|first=0|second=0|probeCalls=1
FPS_FRESH_ENGINE|fps=24
```

建议明确区分超时/失败与有效探测结果，仅缓存正帧率，或为失败结果采用有限重试/失效时间。不要把用户主动取消吞掉。

### [P2] 不应将 CLI 中转参数的末尾绝对路径当作视频输出

位置：`VideoEnhancerPlugin/PreviewEngine.vb:351–358`。

新增逻辑只要任务有 `当前步骤`，就将末尾绝对路径当作该步骤的视频输出。然而真实3FUI 6.2.38也为普通命令行任务创建单个编码步骤，并非只有分段原生 FFmpeg 任务才有此成员。

合法 VideoEnhancer 命令以 `--ffprobe-path "C:\tools\ffprobe.exe"` 结尾、且没有可选的暂停/停止共享内存参数时，解析结果会是 ffprobe.exe，而不是 `task.输出文件`。一般间隔模式随后尝试解码 EXE，失败后回退显示输入原帧；关键帧模式探测 EXE 则没有画面，属于已有增强任务预览的回归。正常队列钩子追加共享内存名称时不触发这一分支，但 CLI 命令/其他调用方不要求传这些可选参数。

用 PR 编译的实际 `HostTask` 与 `ResolvePreviewOutput`、3FUI形状的任务/步骤夹具复现：任务输出设为 `enhanced.mkv`，实际返回末尾 `--ffprobe-path` 对应的 EXE。复现使用测试程序 EXE代替ffprobe.exe，路径误判与文件内容无关。

建议先识别中转命令（如 `-ffmpeg-settings` 或实际执行程序），沿用宿主任务输出；仅对确认的原生 FFmpeg 步骤提取实际视频输出，不能把任意最后一个绝对路径当作输出文件。

## 验证与边界

- 在项目 `Artifacts/pr12-review` detached worktree 审核固定头，没有修改 PR 跟踪文件。
- 普通 `dotnet build VideoEnhancerPlugin/VideoEnhancerPlugin.vbproj -c Release -p:PluginInstallDir=` 通过，0警告0错误；传入主项目已存在的FFF缓存路径，构建脚本依旧执行固定SHA校验。没有用DesignTimeBuild跳过门禁。
- PR自带 `ModelMetadataUi --preview-engine` 的25项预览回归、`--host-runtime` 及18项倍率UI检查通过。
- PixelPreview 在LakeUI5.110与5.112均通过实际插件回调和GPU表面红→蓝读回；不等同于完整宿主实际编码任务验收。
- 新增两项独立审核探针复现以上缺陷，位于忽略目录 `Artifacts/pr12-audit-probe`；第一次诊断探针没有泵WinForms同步上下文而挂起，已停止自身诊断进程并清除该同步上下文后完成复验，此失败不计作产品缺陷。
- 未跑长片、GPU推理或用户原始4K素材，不重复作者的长测速，也未修改本机安装。
- 日志：`Artifacts/pr12-review-build.log`、`pr12-review-preview-tests.log`、`pr12-review-host-tests.log`、`pr12-review-model-ui-tests.log`、`pr12-review-pixel-5110.log`、`pr12-review-pixel-5112.log`、`pr12-audit-probe/result.log`。

## 与当前两组修复的合并关系

`git merge-tree --write-tree HEAD origin/pr-12-review` 的只读合并检查显示：产品代码可自动合并；`cli/tests/ModelMetadataUi/Program.cs`、`docs/codex/STATUS.md`、`version/工作进度.md` 有冲突。后续合并需同时保留图片探针入口、PR预览夹具入口以及双方全部会话记录。未做实际合并或组合运行。

本次仅完成本地审核报告，没有向GitHub提交评论/Approve/Request changes，没有合并PR、推送主线、创建标签或Release。原先VMAF模型自动枚举限制、ARM架构检测建议及低显存问题不属于本PR新增发现。
