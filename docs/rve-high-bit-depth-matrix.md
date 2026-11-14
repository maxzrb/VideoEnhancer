# RGB48 / PR17 扩大可用性矩阵

2026-11-09，按用户选择执行完整扩大矩阵。首轮629例已完成，随后对3个TensorRT FP16内容失败完成根因诊断、源码修复和隔离定向复验。

首轮结果为614 `PASS`、11 `RESOURCE_OOM`、3 `FAIL_CONTENT`、1 `PASS_GATE`。11个OOM全部是RTX3060 Laptop 6GB运行FlashVSR时的显存边界；3个内容失败均来自TensorRT FP16 Engine输出NaN，对应FP32 Engine输出全部有限。CLI与RVE后端现已对AniToon-RPLKSRL、DAT2、GRL回退FP32，并拒绝已知模型的旧FP16 Engine。

修复候选在独立目录`Artifacts/hbd-matrix-20261109-fp16fix`复验：3个原失败ID全部`PASS`，输出`gbrp16le`且逐帧约1.93万级色阶、范围约5.3万码、裁剪比例0；Ani4K-Compact与AniScale2-Refiner两个正常FP16对照仍加载FP16 Engine并通过。受管内嵌后端源码与安装副本SHA256一致，外部后端保护分支未关闭。合并本机结论为617个`PASS`、1个`PASS_GATE`、11个显存边界、0个已知内容失败。

候选来自后端 `RVE-high-bit-depth-20261109-r1.zip` 和前端 `VideoEnhancer-1.3.15-rgb48-pr17-candidate-20261109.zip`，沿用 1.3.15 版本号。测试入口为 [high_bit_depth_matrix.py](../cli/tests/high_bit_depth_matrix.py)。原安装、配置、模型权重不覆盖；隔离目录为本机 `Artifacts/hbd-matrix-20261109`。

| 层 | 例数 | 范围 |
|---|---:|---|
| 全目录双精度 | 372 | 186 个目录条目分别请求 FP32、FP16；包括全部 70 个 TensorRT 超分条目及 5 个 TensorRT RIFE |
| 自动精度架构 | 54 | 区分架构、静态图、固定 FP16 ONNX、PTH/ONNX 转换来源、RIFE heavy、GMFSS Base/Union |
| 架构与两种顺序 | 108 | 超分架构搭配 CUDA RIFE；补帧架构搭配 CUDA Compact；分别先补帧、先超分 |
| 后端配对 | 48 | 六超分后端 × CUDA/TensorRT 补帧 × FP32/FP16 × 两顺序 |
| 图片 | 18 | 六后端 × auto/FP32/FP16，16bit PNG 输出 |
| 分块与目标倍率 | 24 | 四常规后端 × 三精度，tile32 和目标 1x 分别验证 |
| 混合分段 | 3 | CUDA/ONNX 切换，三精度，16bit 中间链路 |
| 编码与门禁 | 2 | 缺省 AV1 Main10 4:2:0；NCNN 旧 8bit 补帧明确拒绝 |
| 合计 | **629** | 轻量五路并发、大型独占，逐例日志与结果落盘 |

目录条目数包括同一权重的多种后端入口，不代表 186 个不同的神经网络。上表 629 例保留正式发布时“NCNN RIFE 旧8bit接口明确拒绝”的历史结果。发布后新增 NCNN RIFE 8bit兼容模式；它允许补帧继续运行，并将插帧结果扩展回RGB48主帧，但不属于真实高位深补帧，需单独验收，不能并入此前的真实16bit精度结论。

输入采用 4 帧 RGB48 彩色渐变与移动色块，以 FFV1/gbrp16le 保存。普通输入 96×64，按模型输入倍数补齐；GIMM 使用 320×240，静态 ONNX 采用对应导出尺寸。不同网络采用适合其约束的尺寸，因此这组数据不能用于横向测速。所有案例验证实际输出尺寸、帧数、像素格式、逐帧色阶、范围和大面积截断，并保留命令、退出码、耗时、设备与精度日志。

补帧沿用当前端点约定：4 输入帧在 2 倍补帧下应输出 7 帧。它不是时长保持验收。16bit 输出与超过 256 个色阶只能发现明显坏帧或截断，不能证明模型拥有 16bit 有效精度；像素链路的精确保真另见既有恒等与 65536 码值核验。

FP32/FP16 字段表示请求值。GIMM 的兼容策略会强制 FP32，GMFSS 已有 FP16 实际运行通过记录；固定 ONNX/Engine 输入类型也可能覆盖请求，必须结合实际日志判读，不得把兼容回退写成原生 FP16/FP32 支持。ONNX 首选 GPU provider 不代表全部算子都在 GPU，当前日志不足以证明逐算子放置。

并发轻量案例若发生OOM，等待其他在途任务结束后独占重跑，分别保留两次日志和JSONL记录；最终报告取独占重跑结果，仍可追溯并发资源争用。

资源不足记为 `RESOURCE_OOM`，CPU provider 回退记为 `DEGRADED_CPU`，另外单列超时、退出失败、尺寸/帧数/位深/内容错误与非有限值警告；这些均不计入通过。`PASS_GATE` 只表示预期拒绝成立。

本机首轮结果文件：`Artifacts/hbd-matrix-20261109/manifest.json`、`results.jsonl`、`results.csv`、`results.md`、`summary.json` 和 `logs/`。JSONL 追加保留历史，CSV/Markdown 取每例最后一次结果。`candidate.json`、`run-context.json` 和 `original-models.json` 保存候选哈希、脚本/后端/GPU信息与原权重元数据，结束时核对隔离性。

修复定向证据：`Artifacts/hbd-matrix-20261109-fp16fix/results.jsonl`、`logs/`、`outputs/`、`candidate.json` 和汇总文件`fp16-fix-verification.json`。该目录使用新EXE独立指纹；模型缓存硬链接、Python/运行时目录联接只用于本机复验，不能直接迁移到其他设备。

使用匹配便携环境的 Python 执行：

```powershell
python cli/tests/high_bit_depth_matrix.py --root Artifacts/hbd-matrix-20261109 --plan-only
python cli/tests/high_bit_depth_matrix.py --root Artifacts/hbd-matrix-20261109 --jobs 5
python cli/tests/high_bit_depth_matrix.py --root Artifacts/hbd-matrix-20261109 --layer mixed-segments --rerun-failed
```

这些命令使用已准备好的本机候选目录。换设备需要重新准备运行环境、模型目录和目录清单，不能直接复用本机 Python/FFmpeg junction。

本轮重点为短片可用性；长片稳定性、1080p/4K 显存、画质、吞吐和 RTX/HDR 扩展矩阵不在这 629 例中，不能据此宣称通过。剩余11个FlashVSR OOM也不能据此宣称可在RTX3060 Laptop 6GB上默认运行。
