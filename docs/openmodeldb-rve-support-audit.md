# RVE 对 OpenModelDB 模型的支持审计

审计时间：2026-08-25 19:46（Asia/Shanghai）

## 结论

OpenModelDB 当前官方数据库包含 671 个模型。对当前项目实际安装的 RVE 2.4 后端、内置 Spandrel 0.4.0 标识版本、CLI 模型发现规则和 RGB 视频管线进行交叉检查后，不能将其描述为“支持 OpenModelDB 全部 600 余个模型”。

- 架构层面：633/671 个模型属于当前 Spandrel 注册表可识别的 21 类图像架构；38 个属于当前没有对应通用加载器的架构。
- 当前 CUDA/PyTorch 入口：612 个模型同时具备受支持架构和 `.pth` 资源，能被 CLI 扫描；其中 11 个是 1 通道或 4 通道模型，而 RVE 视频管线固定输入 3 通道 RGB，因此静态兼容上限为 601 个。
- 当前 ONNX 入口：OpenModelDB 有 62 个模型提供 ONNX。相对上述 PyTorch 集合，22 个模型只能依赖 ONNX；其中 3 个 TSCUNet 是时序模型，而 RVE ONNX 超分后端明确只接受单个 4D 图像输入，不能直接使用。其余 19 个是候选兼容项，尚未逐模型下载实跑。
- 当前综合静态上限：601 个 PyTorch RGB 模型，加 19 个 ONNX-only 候选，共 620/671。这个数字是“代码契约允许进入”的上限，不是 620 个模型均已实机验证。
- 明确缺口：51 个。其中 31 个没有当前可用的架构/格式路径，11 个通道契约不兼容，6 个只有 safetensors 可形成新增路径但 CLI 不扫描，3 个 TSCUNet ONNX 输入契约不兼容。

## 审计基线

- 项目主线：`26c795c`，`main` 与 `fork/main` 一致。
- OpenModelDB 官方数据库快照：[`782aac0`](https://github.com/OpenModelDB/open-model-database/commit/782aac088bd83dd3fc438a42eb0c3868dc559110)，`data/models` 共 671 个 JSON。
- OpenModelDB 网站在审计时也显示 671 个模型：https://openmodeldb.info/
- 安装后端：Python 3.12.9、PyTorch 2.9.0+cu130、RVE 2.4、内置 Spandrel 版本标识 0.4.0，主注册表 44 项。
- 当前设备没有可调用的 `nvidia-smi`，本轮没有新增 CUDA/TensorRT 实跑；沿用项目既有 RTX 3060 代表架构矩阵作为历史运行证据，不把它扩写成 671 模型全量实测。

## 架构覆盖

当前通用 PyTorch 加载器覆盖的 OpenModelDB 架构如下，共 633 个模型：

| OpenModelDB 架构 | 模型数 | RVE 加载路径 |
|---|---:|---|
| ESRGAN | 368 | Spandrel ESRGAN |
| Compact | 85 | Spandrel Compact |
| DAT | 34 | Spandrel DAT |
| SPAN | 34 | Spandrel SPAN |
| SwinIR | 22 | Spandrel SwinIR |
| OmniSR | 20 | Spandrel OmniSR |
| RealPLKSR | 17 | Spandrel PLKSR |
| HAT | 7 | Spandrel HAT |
| RGT | 7 | Spandrel RGT |
| DRCT | 6 | Spandrel DRCT |
| ATD | 5 | Spandrel ATD |
| MoSR | 5 | Spandrel MoSR |
| RCAN | 4 | Spandrel RCAN |
| Real-CUGAN | 4 | Spandrel RealCUGAN |
| RealPLKSR-DySample | 4 | Spandrel PLKSR，源码包含 DySample 检测 |
| ESRGAN+ | 3 | Spandrel ESRGAN，源码显式识别 `conv1x1` 变体 |
| DITN | 2 | Spandrel DITN |
| GRL | 2 | Spandrel GRL |
| Swift-SRGAN | 2 | Spandrel SwiftSRGAN |
| CRAFT | 1 | Spandrel CRAFT |
| DCTLSA | 1 | Spandrel DCTLSA |

当前没有对应通用 PyTorch 路径的 38 个模型：

| OpenModelDB 架构 | 模型数 | 当前情况 |
|---|---:|---|
| SOFVSR | 9 | RVE 没有该权重加载器 |
| SRFormer | 9 | 当前包未注册 SRFormer/extra-arches；其中 2 个另有 ONNX 候选 |
| CAIN | 4 | 当前补帧工厂不支持 CAIN |
| CUGAN | 3 | 不等同于 Real-CUGAN；1 个 PTH 无加载器，2 个另有 ONNX 候选 |
| SPSR | 3 | Spandrel 因许可证原因移除 SPSR |
| TSCUNet | 3 | PyTorch 无加载器；其 ONNX 是时序模型，不符合单图 4D 输入契约 |
| LUDVAE | 2 | 当前包无加载器 |
| CAIN-YUV | 1 | 当前补帧工厂不支持 |
| EDSR | 1 | 当前注册表没有 EDSR；ESRGAN 检测器仅覆盖 RRDB 系列 |
| HMA | 1 | 当前包无加载器 |
| RIFE | 1 | OpenModelDB 条目是旧 RIFE v4；当前 RVE 检测器支持的 RIFE 从 4.6 等版本起，没有 v4 类 |
| SRResNet | 1 | 当前注册表没有 SRResNet，模型说明也要求 BasicSR |

注意：OpenModelDB 的 `CUGAN` 与 `Real-CUGAN` 是两个不同架构标签，不能因名称相近而合并计算。

## 文件格式缺口

RVE 内置 Spandrel 的 `ModelLoader` 实际支持 `.pth`、`.pt`、`.ckpt` 和 `.safetensors`，但当前 CLI 的 CUDA/TensorRT 模型发现只扫描 `.pth`、`.pt`、`.pkl`。因此加载器具备能力、界面却看不到 safetensors。

以下 6 个模型在当前路径下没有可替代的 PTH/ONNX，加入 safetensors 扫描后可形成新增兼容路径：

- `1x-Book-Compact`
- `1x-SuperScale-Alt-RPLKSR-S`
- `1x-SuperScale-RPLKSR-S`
- `1x-SuperScale`
- `2x-AnimeSharpV3-RCAN`
- `4x-RealPLKSR-dysample-pretrain`

另外，`4x-Swift-SRGAN` 的原始下载名是 `swift_srgan_4x.pth.tar`。OpenModelDB 客户端若按资源类型规范化为 `.pth` 可被识别；直接保留 `.pth.tar` 文件名则不会进入当前 CLI 列表。

## RGB 管线缺口

OpenModelDB 有 11 个“架构可加载、文件也可扫描”的模型不是 3→3 RGB。RVE 的 `UpscaleModelWrapper` 将输入通道固定为 3，`TorchUtils` 也按 RGB24/RGB48 解码，因此这些模型目前不能直接用于视频：

- 1→1：`1x-MangaJPEGHQ`、`1x-MangaJPEGHQPlus`、`1x-MangaJPEGLQ`、`1x-MangaJPEGMQ`、`4x-1ch-Alpha-Lite`、`4x-eula-digimanga-bw-v2-nc1`
- 1→3：`1x-SpongeColor-Lite`
- 4→4：`2x-Gen5-Alpha`、`4x-FireAlpha`、`4x-PocketMonsters-Alpha`、`8x-Sphax-Alpha-NN`

支持这些模型需要显式的灰度/Alpha 输入输出策略，不能只把它们加入白名单。

## ONNX、NCNN 与 TensorRT 边界

- ONNX：当前后端支持一个 4D 图像输入，支持 NCHW/NHWC、动态尺寸和静态尺寸分块。OpenModelDB 的 62 个 ONNX 模型中，40 个与可扫描 PTH 模型重叠，22 个没有当前可用的 PTH 路径；排除 3 个 TSCUNet 后，另外 19 个可作为 ONNX-only 候选。这 19 个仍需逐文件检查输入数量、布局、数据类型、输出和实际倍率。
- TSCUNet：3 个条目虽提供 ONNX，但模型说明明确要求专用时序脚本；不能算作当前通用 ONNX 超分支持。
- NCNN：OpenModelDB 当前 671 条模型元数据没有发布 NCNN 资源。架构页标注“可兼容 NCNN”只表示可转换，不代表数据库提供了可直接放入 RVE 的 `.param/.bin`。
- TensorRT：当前入口从 PTH 构建单图 Engine，但项目已有实测表明 AnimeSR、SwinIR、CRAFT 不兼容该适配器，GRL 需要 FP32。不能把 601 个 PyTorch RGB 候选等同为 601 个 TensorRT 支持；TensorRT 必须按架构和模型建立白名单并实机构建。

## 本地加载验证

本轮用安装后端的真实 `ModelLoader` 对现有 PTH 模型逐个加载，至少验证了以下 11 类架构能被当前包实例化：Compact、DITN、ESRGAN、OmniSR、SwinIR、CRAFT、SPAN、PLKSR、DAT、GRL、SPANPlus。项目既有 RTX 3060 代表矩阵还覆盖 NCNN、CUDA、TensorRT、ONNX、FlashVSR、BasicVSR++ 和补帧代表模型，但该矩阵是按架构选代表，不是 OpenModelDB 671 模型全量运行。

由于模型总下载量很大、部分资源依赖 Google Drive/Mega/MediaFire，且当前设备没有 NVIDIA 驱动，本轮没有下载并执行 671 个权重。所有数量均区分为：

- 已实测：当前模型镜像中的代表架构和既有 GPU 矩阵。
- 静态高置信：本地 Spandrel 注册表与 OpenModelDB 架构、资源类型、通道元数据一致。
- 候选：ONNX-only 的 19 个单图模型，仍需逐模型实跑。

## 建议优先级

1. 先修复 CLI 对 `.safetensors` 和 `.ckpt` 的发现、显式路径解析与帮助文本，并增加 CPU 模型加载预检，可低风险补回 6 个 OpenModelDB 模型。
2. 模型列表不要只按扩展名展示。应在后台调用轻量模型检查器，返回架构、倍率、输入/输出通道和支持后端；1/4 通道模型应显示“不兼容 RGB 视频管线”。
3. 建立 OpenModelDB JSON 审计脚本，固定输出模型总数、架构覆盖、格式缺口、非 RGB 清单和 ONNX-only 清单，便于数据库更新后重复运行。
4. 对 19 个 ONNX-only 候选逐个下载，先用 ONNX Runtime 读取输入/输出契约，再用 2～4 帧小样本实跑；不要直接宣称全部支持。
5. 若要提高架构覆盖，优先评估 SRFormer（9）和 SOFVSR（9）；CAIN、RIFE、TSCUNet 属于时序模型，应进入独立时序后端，而不是塞进单图超分加载器。
6. TensorRT 继续采用实测白名单，不从 PyTorch/Spandrel 支持数量自动推导。

## 参考

- [OpenModelDB 官方网站](https://openmodeldb.info/)
- [OpenModelDB 官方数据库源码](https://github.com/OpenModelDB/open-model-database)
- [Spandrel 官方仓库](https://github.com/chaiNNer-org/spandrel)
- [Spandrel Releases](https://github.com/chaiNNer-org/spandrel/releases)
