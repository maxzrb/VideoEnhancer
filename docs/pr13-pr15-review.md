# PR #13–#15 审查记录

审查时间：2026-11-02 19:33（北京时间）。基线为 `a4f4bf0ce2587aac0b35aecb765fd1e6536a035a`，三个 PR 均保持 OPEN。本轮只审查和记录，没有修改产品代码、提交 GitHub 审核、合并或部署。

| PR | 固定审查提交 | 结论 |
| --- | --- | --- |
| [#13](https://github.com/maxzrb/VideoEnhancer/pull/13) | `ea56d0b99969dd9bd429afbdc750447a6e123aee` | 一项 P2，建议修正后合并 |
| [#14](https://github.com/maxzrb/VideoEnhancer/pull/14) | `22f05408ac91c03c22b8b58f61f0e43b883d2e14` | 本轮未发现阻挡项 |
| [#15](https://github.com/maxzrb/VideoEnhancer/pull/15) | `9cefb618fd5e3ff9842eea452e11974e43ad5bc9` | 一项 P2，建议修正后合并 |

## P2：#13 覆盖了按输出流序号指定的编码器

位置：`cli/VideoEncodingDefaults.cs:29–30`，`VideoEncoder()`。

新检测只识别 `-c`、`-codec`、`-vcodec` 和带 `:v` 的选项，遗漏合法的 `-c:0`、`-codec:0`。在当前后端第一条输出流是视频的情况下，`-c:0 ffv1 out.mkv` 已明确选择 FFV1，却被判断为没有视频编码器。`SplitFfmpegSettings()` 随后追加 `-c:v libsvtav1` 及整套有损预设，覆盖前面的用户选择，也可能让原本可用的任务被 SVT/y412 能力检查拒绝。

独立验证：真实 FFmpeg 用 `-c:0 ffv1` 生成一帧视频，ffprobe 确认为 `ffv1`；PR 构建产物的 `--debug-split` 对 `-c:0`、`-codec:0` 均追加 SVT，`-c:v:0` 对照则正确保留。证据在 `Artifacts/pr-review-20261102/encoder-options.json` 和 `explicit-ffv1.mkv`。

建议：按实际输出流映射识别数字流说明符；有明确视频编码器时不追加缺省编码。补一个数字流索引的定向回归即可。

## P2：#15 手动转换遗漏 ONNX 的尺寸倍数约束

位置：`VideoEnhancerPlugin/Pages/PluginPanel.ModelConverterPage.vb:196–198` 的新增 ONNX 路径；关联 `RunTensorRtConversion():310–313` 和 `cli/embedded-tools/convert_tensorrt.py:54–55,77–78`。

手动转换对 ONNX 跳过预检，调用 Python 时只传模型和输出目录，没有传 `--input-multiple`。转换器只从命令行或 ONNX metadata 取得倍数，因此这些约束仅登记在项目能力清单中的模型会按默认 1920×1080 编译。清单中的四个 RealHatGAN ONNX 要求 16 的倍数，1080 不满足；本机这四个原始模型均为动态高宽，metadata 为空。自动任务入口会读取清单并传入约束，手动入口没有相同处理。

独立 GPU 验证使用无 metadata、要求 16 倍数的动态 ONNX 小图：同一 PR 转换器在 64×56 下退出 1，补 `--input-multiple 16` 后按 64×64 编译、序列化成功并退出 0。这验证了入口遗漏约束会导致构建失败；本轮没有完整编译四个大型 HAT 模型。脚本与日志在 `Artifacts/pr-review-20261102/probe_onnx.py`、`unaligned.log`、`aligned.log`。

建议：手动入口复用自动任务的能力查询，将实际尺寸倍数传入转换器；保持固定尺寸 ONNX profile 的既有处理。增加一个无 metadata、约束来自能力清单的转换回归。

## #14 的实际兼容检查

本机实际宿主已升级为 **3FUI 6.2.43**，与旧 STATUS 的 6.2.38 记录不同。本轮从当前 EXE 提取托管程序集到项目忽略目录，在独立测试进程创建评测控件，加载 PR DLL 并调用真实 `RepairAsync()`，没有操作用户运行中的宿主。

真实 FFmpeg 扫描得到 17 个原始模型标识，经宿主分类及 CUDA 能力筛选后恢复 **24 个可用模型项**，选择仍为 `AUTO`。证据在 `Artifacts/pr-review-20261102/vmaf.log`。既有宿主接口回归 24 项通过。本轮未重复作者的完整 VMAF 评分、所有刷新竞争和手动模型场景，因此结论限于已审查代码与上述入口验证。

## 共同验证与边界

- 三个 PR 的插件普通 Release 构建均为 0 警告、0 错误，复用已存在的 FFF 2026.8.19 缓存且照常校验固定哈希。首次缓存路径误写为 `8.19` 导致下载失败，纠正后通过，不计为 PR 缺陷。
- #13、#15 CLI 托管构建均为 0 警告、0 错误；使用 `DesignTimeBuild=true`、框架依赖模式，不能视作完整发行打包验收。
- #15 现有模型能力 6 项、用户导入 9 项回归通过。这里只补必要的小尺寸 GPU 探针，不执行长视频、性能基准或大模型压力测试。
- 三组两两 `git merge-tree --write-tree` 检查均无冲突；没有实际合并或执行三 PR 组合版。
- GitHub 返回的 commit statuses 为空，不宣称远端 CI 已通过。
- 主线产品文件保持不变；新增本报告并更新两份交接记录。版本仍为 1.3.14。

## 2026-11-02 19:55 补修与组合复核

用户已授权修正并安全合并、测试，取代前面的初审限制。两项P2均已修复：PR13补修`7b0ea06`；PR15补修`0422327`。三个PR已在主线本地无冲突合并为`3c2fb3f`、`4a50f0b`、`2edcfb1`，保留原始PR完整祖先。原问题描述和初审证据保留用于追溯；不再表示当前源码仍存在这两项缺陷。准备普通推送后核对远端状态。

- PR13：数字第0路编码器保留，数字音频流仍会触发视频兜底。真实FFmpeg分别使用`-c:0 ffv1`、`-codec:0 ffv1`，ffprobe均确认FFV1。
- PR15：模型目录JSON新增`inputMultiple`，手动入口复用统一内置/用户能力解析；7项协议/旧字段默认/参数跨进程检查通过。无metadata约束小图经真实合并EXE目录和合并DLL启动，默认1920×1080对齐1920×1088，GPU构建及序列化通过。
- 完整solution publish成功；110项打包/隔离安装/API11、安装/自更新回滚及Burn检查通过。aria2生产下载首次EOF后重试成功，不更改生产源。
- 22项模型/导入/HDR Python回归、24项HostRuntime、图片参数与错误、29项预览、倍率/参数/编码回归、六项真实FFmpeg透传通过。
- 实际SPANF3 FP16 ONNX走合并EXE自动编译→验证→RVE→FFmpeg，两帧96×64得到192×128两帧；再次运行复用已验证缓存。小图验证不代表全模型、长视频或吞吐验收。
- 实际宿主6.2.43独立进程加载合并DLL，VMAF空列表恢复24模型项、AUTO保持。

完整证据在`Artifacts/pr13-pr15-merged/checks-summary.json`及同目录日志。本轮未覆盖用户正式安装、模型或后端，未发布新版本，版本仍为1.3.14。

## 2026-11-02 19:57 远端确认

普通推送主线成功，GitHub回读PR13、PR14、PR15均为MERGED（北京时间19:57:13）。主线1e9e2ed包含全部原始PR及两项补修；产品合并提交为2edcfb1，后续仅操作记录收尾。构建与必要测试均通过，结论为本轮修复和安全合并完成。未部署到用户正式安装，未发布新版本；版本仍为1.3.14。

## 2026-11-02 20:04 本机部署

已按用户授权部署到本机3FUI 6.2.43，仍显示1.3.14。45载荷文件与已验证ZIP逐项hash一致，5项保护文件和157模型清单保持；实际CLI版本、FFF API11及28项ONNX目录/HAT倍数约束通过。已同步配套的两个ONNX辅助脚本，未换Python/后端包/RTX/模型。备份：`C:\Codex Program\3fui plugin\Artifacts\pr13-pr15-merged\local-install\backup-20261102-200411`；完整证据：`Artifacts/pr13-pr15-merged/local-install/local-deployment.json`。没有启动宿主，等待用户重开窗口验收，未发布新版本。
