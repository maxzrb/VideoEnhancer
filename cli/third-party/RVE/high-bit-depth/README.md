# RVE RGB48 核心补丁

集成修订：`rgb48-v1`，2026-11-09。配套基线为 RVE `2.4.1-dev17`。

本目录保存高位深链路涉及的 16 个核心 Python 源文件。其余辅助入口位于 `cli/embedded-tools`，完整后端仍由独立 Python 组件提供。代码沿用 RVE 的 AGPL-3.0 许可，见上级 `AGPL-3.0.txt` 和 `SOURCE.txt`。

源输入：用户提供的 `RVE-high-bit-depth-20261108.zip`，SHA256 `e72eb55982e764865986121f3438aae0e26cde89e34a11d3e4a4e02a4c18fdd2`。在该快照基础上保留主线中文 ONNX 内存解析，并修正流标识编码参数、中文图片、转场量纲、帧尺寸转换、输入位深摘要、NCNN 借用输入数组生命周期及自动 Engine 精度缓存等问题。能力查询只读取 TensorRT 安装信息，实际构建或运行 Engine 时再加载原生库。

CLI 将源码内嵌，首次为旧后端安装时标记脚本归 CLI 管理，后续按哈希同步；独立高位深包则沿用 PR17 的所有权约定，验证 rgb48-v1 后保留全部 Python 文件，顺序和分段入口也使用后端自己的文件。独立包通过 .videoenhancer-rgb48-owner=external 转交所有权，覆盖安装也不会被旧 CLI 标记继续接管。同步失败时不继续推理。核心自动安装只接受配套基线。`src/version.py` 沿用上游版本，精度策略使用独立缓存指纹，不删除旧 Engine。

SDR/HDR 主帧协议均为 RGB48/uint16。`auto` 采用 FP32，显式 `float16` 保留加速选择；固定 ONNX/预构建 Engine 的内部精度仍取决于模型。预览和转场旁路可降为8bit，不写回主帧。NCNN 超分通过浮点 Mat 路径保留高位深；NCNN RIFE 原接口仅支持8bit，因此提供明确的8bit兼容模式，输入量化到8bit、插帧后扩展回RGB48。要求补帧计算保留真实高位深时使用CUDA/PyTorch或TensorRT。

配套前端默认最终编码为 SVT-AV1 / p6 / CRF12 / Main / yuv420p10le，仅未指定视频编码器时补入。跨后端中间文件、混合分段源切片及拼接使用 gbrp16le；最近邻使用 zscale point 保留低位，相关 FFmpeg 构建需含 zscale。RTX 原有 Main10 桥接和未验证的 HDR 色彩路径不因 RGB48 自动升级。

必要验证和已知边界见仓库 `docs/rve-high-bit-depth-review.md`，可复用回归位于 `cli/tests/test_high_bit_depth_regressions.py`。
