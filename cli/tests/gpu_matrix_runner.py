#!/usr/bin/env python3
"""在真实 RVE 环境中运行可断点恢复的 GPU 兼容矩阵（1.3.0 三相结构）。

相结构：
- upscale 超分相：全部已安装超分模型单测 + RTX VSR（目标/质量/容器/补帧组合）。
- interp 补帧相：全部已安装补帧模型单测 + 超分×补帧代表全网格 + 后端类代表层 + 抽样交叉层。
- hdr HDR 相：RTX HDR 组合（纯 HDR / VSR+HDR / 三步骤 / 传统超分+HDR）+ 门禁预期失败用例。
"""

from __future__ import annotations

import argparse
import base64
import csv
import hashlib
import json
import os
import re
import subprocess
import sys
import time
from collections import Counter, defaultdict
from concurrent.futures import ThreadPoolExecutor, as_completed
from dataclasses import asdict, dataclass, replace
from pathlib import Path
from typing import Iterable

# 终态：PASS / 显存不足跳过 / RTX 环境缺失跳过。其余状态视为失败，可被 --rerun-failed 重跑。
TERMINAL_STATUSES = {"PASS", "SKIP_OOM", "SKIP_ENV"}

FLOWS = ("upscale-first", "interp-first")

# RTX/HDR 相共用的夹具尺寸：大于 GIMM 的 320x240 下限，且对 NGX VSR/NVENC 是常规尺寸。
RTX_WIDTH, RTX_HEIGHT = 640, 360
RTX_TARGETS = ("1x", "1.5x", "2x", "3x", "4x", "1080p", "1440p", "2160p")
RTX_BASE_QUALITY = 3
RTX_QUALITY_SWEEP = (1, 2, 4)

RTX_ORDER_BANNER = "先补帧，再 RTX 超分"


if os.name == "nt":
    # PowerShell 的活动代码页可能不是 UTF-8，确保中文矩阵进度和日志稳定输出。
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")


@dataclass(frozen=True)
class UpscaleModel:
    backend: str
    category: str
    name: str
    scale: int
    width: int = 96
    height: int = 64


@dataclass(frozen=True)
class InterpModel:
    backend: str
    category: str
    name: str
    width: int = 96
    height: int = 64


@dataclass(frozen=True)
class MatrixCase:
    case_id: str
    phase: str  # upscale | interp | hdr
    layer: str  # single | rtx | rep-grid | class-rep | sampled | hdr-combo | rule
    flow: str
    kind: str  # traditional | rtx | rule
    upscale_backend: str
    upscale_category: str
    upscale_model: str
    interp_backend: str
    interp_category: str
    interp_model: str
    width: int
    height: int
    expected_width: int
    expected_height: int
    expected_frames: int
    rtx_target: str = "-"
    rtx_quality: int = 0
    rtx_hdr: bool = False
    container: str = "mkv"
    no_upscale: bool = False
    pq_input: bool = False
    expected_error: str = ""


# 每类架构选择一个代表；Base/Union 因加载结构不同，分别保留。
INTERP_MODELS = [
    InterpModel("ncnn", "RIFE Heavy", "RIFE/rife-v4.26-heavy"),
    InterpModel("cuda", "RIFE Heavy", "RIFE/rife4.26.heavy"),
    # 96x64 会令 GIMM 的 0.5x 光流分支产生 NaN；320x240 是已实测通过的低分辨率夹具。
    InterpModel("cuda", "GIMM", "GIMM-VFI/GIMM-VFI-R-LPIPS", 320, 240),
    InterpModel("cuda", "GMFSS Base", "GMFSS/GMFSS-Fortuna-Base"),
    InterpModel("cuda", "GMFSS Union", "GMFSS/GMFSS-Fortuna-Union-AnimeRun"),
    InterpModel("tensorrt", "RIFE Heavy", "RIFE/rife4.26.heavy"),
]


NCNN_UPSCALE = [
    ("Compact", "Param-Bin/AnimeJaNai-V3-2x-HD-Sharp1-Compact-430K", 2),
    ("SPAN", "Param-Bin/AniSD-DC-SPAN-2x-92500", 2),
    ("CUGAN", "Param-Bin/CUGAN-Conservative-2x", 2),
    ("DeH264 1x", "Param-Bin/DenoiseH264-SuperUltraCompact-1x-float16", 1),
    ("DnCNN 1x", "Param-Bin/DnCNN-ColorBlind-1x", 1),
    ("Nomos SPAN", "Param-Bin/Nomos8k-span-otf-4x-medium", 4),
    ("RealESRGAN", "Param-Bin/RealESRGAN-AnimeVideoV3-2x", 2),
    ("Waifu2x", "Param-Bin/Waifu2x-Noise2-2x", 2),
]


PYTORCH_UPSCALE = [
    ("Compact", "PTH/AnimeJaNai-V3-2x-HD-Sharp1-Compact-430K", 2),
    ("AnimeSR", "PTH/AnimeSR-V2-4x", 4),
    ("DITN", "PTH/AniScale2-DITN-i16-75K-2x", 2),
    ("ESRGAN", "PTH/AniScale2-ESRGAN-Lite-i16-165K-2x", 2),
    ("OmniSR", "PTH/AniScale2-Omni-i16-40K-2x", 2),
    ("SPAN 1x", "PTH/AniScale2-Refiner-10K-1x", 1),
    ("SwinIR", "PTH/AniScale2-SwinIR-i16-265K-2x", 2),
    ("CRAFT", "PTH/AniSD-AC-CRAFT-92500-2x", 2),
    ("RealPLKSR", "PTH/AniSD-AC-RealPLKSR-127500-2x", 2),
    ("DAT2", "PTH/AniSD-DC-DAT2-97500-2x", 2),
    ("GRL", "PTH/APISR-GRL-GAN-generator-4x", 4),
    ("RRDB", "PTH/APISR-RRDB-GAN-generator-2x", 2),
    ("SPANPlus", "PTH/BHI-SpanPlusDynamic-2x-Light", 2),
    ("sudo-SPAN", "PTH/Sudo-Shuffle-Span-2x-NoUpdateParams", 2),
]


ONNX_UPSCALE = [
    ("SPANF3", "ONNX/AnimeJaNai-HD-V3.1-Performance-SPANF3-b5f48-unshuffle-fp16-2x", 2, 96, 64),
    ("Compact", "ONNX/AniSD-AC-G6i2a-Compact-72500-fp32-2x", 2, 96, 64),
    ("SPAN", "ONNX/AniSD-AC-G6i2b-SPAN-190K-fp32-2x", 2, 96, 64),
    ("SwinIR static", "ONNX/AniSD-AC-G6i2b-SwinIR-117500-240x320-fp32-2x", 2, 320, 240),
    ("RealPLKSR", "ONNX/AniSD-AC-RealPLKSR-127500-fp32-FO-dynamic-2x", 2, 96, 64),
    ("DAT2", "ONNX/AniSD-DC-DAT2-97500-fp32FO-2x", 2, 96, 64),
    ("RealESRGAN", "ONNX/RealESRGAN-x4-jp-Illustration-fix2", 4, 96, 64),
    ("RealHatGAN", "ONNX/RealHatGAN-JP-Illustration-2x-fix1", 2, 96, 64),
    ("SPAN 1x", "ONNX/AniSD-DB-i2-SPAN-85K-fp32-1x", 1, 96, 64),
]


UPSCALE_MODELS: list[UpscaleModel] = [
    *(UpscaleModel("ncnn", category, name, scale) for category, name, scale in NCNN_UPSCALE),
    *(UpscaleModel("cuda", category, name, scale)
      for category, name, scale in PYTORCH_UPSCALE),
    *(UpscaleModel("tensorrt", category, name, scale)
      for category, name, scale in PYTORCH_UPSCALE
      if category not in {"AnimeSR", "SwinIR", "CRAFT"}),
    *(UpscaleModel("onnx", category, name, scale, width, height)
      for category, name, scale, width, height in ONNX_UPSCALE),
    UpscaleModel("flashvsr", "FlashVSR", "FlashVSR", 4),
    UpscaleModel(
        "basicvsrpp",
        "BasicVSR++",
        "BasicVSR++/basicvsr_plusplus_c64n7_8x1_600k_reds4_20210217-db622b2f",
        4,
    ),
]


# HDR 相传统超分 + RTX HDR 的代表后端：每类取 UPSCALE_MODELS 首个模型。
HDR_TRADITIONAL_REPS = ("ncnn", "cuda", "tensorrt", "onnx")


def stable_case_id(parts: Iterable[str]) -> str:
    raw = "\0".join(parts).encode("utf-8")
    return hashlib.sha256(raw).hexdigest()[:16]


def rtx_output_dims(target: str, src_w: int, src_h: int) -> tuple[int, int]:
    """复刻 CLI ResolveRtxScale：倍率档直接生效；分辨率档以短边为基准并 clamp 到 1~4。"""
    if target.endswith("x"):
        scale = float(target[:-1])
    else:
        requested = {"1080p": 1080, "1440p": 1440, "2160p": 2160, "4320p": 4320}[target]
        scale = requested / min(src_w, src_h)
    scale = max(1.0, min(4.0, scale))
    out_w = max(2, round(src_w * scale))
    out_h = max(2, round(src_h * scale))
    if out_w % 2:
        out_w += 1
    if out_h % 2:
        out_h += 1
    return out_w, out_h


def make_case(
    phase: str,
    layer: str,
    flow: str,
    upscale: UpscaleModel | None,
    interp: InterpModel | None,
) -> MatrixCase:
    width = max(upscale.width if upscale else 96, interp.width if interp else 96)
    height = max(upscale.height if upscale else 64, interp.height if interp else 64)
    if (
        upscale is not None
        and upscale.backend in {"flashvsr", "basicvsrpp"}
        and interp is not None
        and interp.category == "GIMM"
    ):
        # GIMM 在更小尺寸会产生 NaN；256x192 已实测稳定，同时可避免 6GB 显卡上
        # FlashVSR 以 320 宽输入拆成多个 tile 时的显存峰值。
        width, height = 256, 192
    scale = upscale.scale if upscale else 1
    parts = [
        phase, layer, flow,
        upscale.backend if upscale else "none",
        upscale.name if upscale else "none",
        interp.backend if interp else "none",
        interp.name if interp else "none",
        f"{width}x{height}",
    ]
    return MatrixCase(
        case_id=stable_case_id(parts),
        phase=phase,
        layer=layer,
        flow=flow,
        kind="traditional",
        upscale_backend=upscale.backend if upscale else "-",
        upscale_category=upscale.category if upscale else "-",
        upscale_model=upscale.name if upscale else "-",
        interp_backend=interp.backend if interp else "-",
        interp_category=interp.category if interp else "-",
        interp_model=interp.name if interp else "-",
        width=width,
        height=height,
        expected_width=width * scale,
        expected_height=height * scale,
        expected_frames=7 if interp else 4,
    )


def make_rtx_case(
    phase: str,
    layer: str,
    flow: str,
    *,
    target: str = "-",
    quality: int = 0,
    hdr: bool = False,
    interp: InterpModel | None = None,
    container: str = "mkv",
    no_upscale: bool = False,
) -> MatrixCase:
    """RTX VSR / 纯 RTX HDR 用例：upscale_backend 固定 rtxvsr，模型固定 "-"。"""
    width = max(RTX_WIDTH, interp.width if interp else 0)
    height = max(RTX_HEIGHT, interp.height if interp else 0)
    if target != "-" and not no_upscale:
        expected_w, expected_h = rtx_output_dims(target, width, height)
    else:
        expected_w, expected_h = width, height
    parts = [
        phase, layer, flow,
        "rtxvsr", target, str(quality), "hdr" if hdr else "sdr",
        container, "noup" if no_upscale else "up",
        interp.backend if interp else "none",
        interp.name if interp else "none",
        f"{width}x{height}",
    ]
    return MatrixCase(
        case_id=stable_case_id(parts),
        phase=phase,
        layer=layer,
        flow=flow,
        kind="rtx",
        upscale_backend="rtxvsr",
        upscale_category="RTX VSR" if not no_upscale else "-",
        upscale_model="-",
        interp_backend=interp.backend if interp else "-",
        interp_category=interp.category if interp else "-",
        interp_model=interp.name if interp else "-",
        width=width,
        height=height,
        expected_width=expected_w,
        expected_height=expected_h,
        expected_frames=7 if interp else 4,
        rtx_target=target,
        rtx_quality=quality,
        rtx_hdr=hdr,
        container=container,
        no_upscale=no_upscale,
    )


def make_hdr_traditional_case(
    upscale: UpscaleModel,
    interp: InterpModel | None,
) -> MatrixCase:
    """传统后端超分（可选补帧）+ RTX HDR：RVE 出无损 HEVC 中间文件，sidecar 做 TrueHDR 编码。

    夹具强制 RTX 尺寸（640x360）：sidecar 的 D3D11VA 硬解在过小分辨率上建解码器失败
    （实测 192x128 失败、320x240 起正常），传统相默认的 96x64 夹具不适用于 RTX 类用例。
    """
    base = make_case(
        "hdr", "hdr-combo", "rtx-hdr",
        replace(upscale, width=RTX_WIDTH, height=RTX_HEIGHT),
        replace(interp, width=RTX_WIDTH, height=RTX_HEIGHT) if interp else None,
    )
    parts = [
        base.phase, base.layer, base.flow,
        base.upscale_backend, base.upscale_model,
        base.interp_backend, base.interp_model,
        f"{base.width}x{base.height}", "rtx-hdr",
    ]
    return replace(base, case_id=stable_case_id(parts), kind="rtx", rtx_hdr=True)


def make_rule_case(
    flow: str,
    *,
    expected_error: str,
    pq: bool = False,
    container: str = "mkv",
    width: int = 96,
    height: int = 64,
) -> MatrixCase:
    """门禁用例：命令应当被 CLI 拒绝，错误信息需命中 expected_error。"""
    parts = ["hdr", "rule", flow, f"{width}x{height}", container, "pq" if pq else "sdr"]
    return MatrixCase(
        case_id=stable_case_id(parts),
        phase="hdr",
        layer="rule",
        flow=flow,
        kind="rule",
        upscale_backend="-",
        upscale_category="-",
        upscale_model="-",
        interp_backend="-",
        interp_category="-",
        interp_model="-",
        width=width,
        height=height,
        expected_width=0,
        expected_height=0,
        expected_frames=0,
        container=container,
        pq_input=pq,
        expected_error=expected_error,
    )


def infer_scale(backend: str, model_name: str) -> int:
    if backend in {"flashvsr", "basicvsrpp"}:
        return 4
    lowered = model_name.lower()
    if lowered.endswith("realesr-animevideov3"):
        return 4
    match = re.search(r"(?:^|[-_])([1-4])x(?:[-_]|$)", lowered)
    if match:
        return int(match.group(1))
    match = re.search(r"x([1-4])", lowered)
    if match:
        return int(match.group(1))
    raise ValueError(f"无法从模型名推断放大倍率：{backend} {model_name}")


def single_upscale_model(backend: str, model_name: str) -> UpscaleModel:
    width, height = 96, 64
    if backend == "onnx":
        static_shape = re.search(r"-(240x320|320x448|480x320)-", model_name, re.I)
        if static_shape:
            input_height, input_width = (int(value) for value in static_shape.group(1).split("x"))
            width, height = input_width, input_height
    return UpscaleModel(
        backend,
        "全部已安装模型",
        model_name,
        infer_scale(backend, model_name),
        width,
        height,
    )


def single_interp_model(backend: str, model_name: str) -> InterpModel:
    is_gimm = model_name.startswith("GIMM-VFI/")
    # GIMM 在 96x64 会产生 NaN，全部 GIMM 模型一律使用 320x240 夹具。
    width, height = (320, 240) if is_gimm else (96, 64)
    return InterpModel(backend, "GIMM" if is_gimm else "全部已安装模型", model_name, width, height)


def rtx_cases() -> list[MatrixCase]:
    """超分相的 RTX VSR 用例：目标/质量/容器扫描 + 与全部补帧代表的组合。"""
    cases: list[MatrixCase] = []
    for target in RTX_TARGETS:
        cases.append(make_rtx_case("upscale", "rtx", "rtx", target=target, quality=RTX_BASE_QUALITY))
    for quality in RTX_QUALITY_SWEEP:
        cases.append(make_rtx_case("upscale", "rtx", "rtx", target="2x", quality=quality))
    # mkv 是基线容器；mp4 验证直写；webm 与 NVENC h264 不兼容，放进门禁相验证原生报错。
    cases.append(make_rtx_case(
        "upscale", "rtx", "rtx", target="2x", quality=RTX_BASE_QUALITY, container="mp4"))
    for interp in INTERP_MODELS:
        cases.append(make_rtx_case(
            "upscale", "rtx", "rtx-interp", target="2x", quality=RTX_BASE_QUALITY, interp=interp))
    return cases


def rule_cases() -> list[MatrixCase]:
    """门禁用例：CLI 必须拒绝非法组合，且错误信息可被用户读懂。"""
    return [
        make_rule_case("rule-pq-ncnn", pq=True, expected_error=r"PQ/HLG|16-bit"),
        make_rule_case("rule-pq-rtx-hdr", pq=True, expected_error=r"已经是 PQ/HLG"),
        make_rule_case("rule-no-upscale-invalid", expected_error=r"未启用补帧或 RTX HDR"),
        make_rule_case("rule-segment-interp", expected_error=r"不能同时启用运动补帧或 RTX HDR"),
        make_rule_case("rule-pq-segment", pq=True, expected_error=r"PQ/HLG HDR 输入"),
        make_rule_case(
            "rule-webm-native", container="webm", width=RTX_WIDTH, height=RTX_HEIGHT,
            expected_error=r"Only VP8 or VP9 or AV1|[Cc]ould not write (?:the webm )?header"),
    ]


def hdr_cases() -> list[MatrixCase]:
    """HDR 相：RTX HDR 功能组合 + 门禁预期失败。"""
    cases: list[MatrixCase] = [
        # 纯 RTX HDR：超分关闭，输入直通 sidecar 做 SDR→HDR 映射（等效 1x）。
        make_rtx_case("hdr", "hdr-combo", "rtx-hdr-only", hdr=True, no_upscale=True),
        # RTX VSR + HDR 同步执行。
        make_rtx_case("hdr", "hdr-combo", "rtx", target="2x", quality=RTX_BASE_QUALITY, hdr=True),
        # RTX VSR + 补帧 + HDR 三步骤（CLI 强制先补帧后 RTX）。
        make_rtx_case(
            "hdr", "hdr-combo", "rtx-interp", target="2x", quality=RTX_BASE_QUALITY, hdr=True,
            interp=INTERP_MODELS[1]),
    ]
    chosen: dict[str, UpscaleModel] = {}
    for model in UPSCALE_MODELS:
        if model.backend in HDR_TRADITIONAL_REPS and model.backend not in chosen:
            chosen[model.backend] = model
    for backend in HDR_TRADITIONAL_REPS:
        cases.append(make_hdr_traditional_case(chosen[backend], None))
    # 传统超分 + 补帧 + RTX HDR 多步骤：cuda/tensorrt 各一组。
    for backend in ("cuda", "tensorrt"):
        cases.append(make_hdr_traditional_case(chosen[backend], INTERP_MODELS[1]))
    cases.extend(rule_cases())
    return cases


def generate_cases(
    available_upscale: dict[str, list[str]],
    available_interp: dict[str, list[str]],
    sample_extra: int,
) -> list[MatrixCase]:
    cases: list[MatrixCase] = []

    # ── upscale 超分相：全部已安装模型单测 + RTX 用例 ──
    for backend, model_names in available_upscale.items():
        for model_name in model_names:
            cases.append(make_case(
                "upscale", "single", "single-upscale",
                single_upscale_model(backend, model_name), None,
            ))
    cases.extend(rtx_cases())

    # ── interp 补帧相：全部已安装模型单测 + 组合分层 ──
    for backend, model_names in available_interp.items():
        for model_name in model_names:
            cases.append(make_case(
                "interp", "single", "single-interp",
                None, single_interp_model(backend, model_name),
            ))

    # 代表全网格：超分代表 × 补帧代表 × 两种顺序。
    for upscale in UPSCALE_MODELS:
        for interp in INTERP_MODELS:
            for flow in FLOWS:
                cases.append(make_case("interp", "rep-grid", flow, upscale, interp))

    # 后端类代表层：全部已安装补帧模型 × 每类超分后端一个代表 × 两种顺序；
    # 与代表网格重叠的组合跳过。
    class_reps: list[UpscaleModel] = []
    seen_backends: set[str] = set()
    for model in UPSCALE_MODELS:
        if model.backend not in seen_backends:
            class_reps.append(model)
            seen_backends.add(model.backend)
    grid_pairs = {
        (upscale.backend, upscale.name, interp.backend, interp.name)
        for upscale in UPSCALE_MODELS
        for interp in INTERP_MODELS
    }
    for backend, model_names in available_interp.items():
        for model_name in model_names:
            interp = single_interp_model(backend, model_name)
            for rep in class_reps:
                if (rep.backend, rep.name, interp.backend, interp.name) in grid_pairs:
                    continue
                for flow in FLOWS:
                    cases.append(make_case("interp", "class-rep", flow, rep, interp))

    # 抽样交叉层：非代表超分模型 × 确定性轮转的补帧代表 × 两种顺序。
    rep_keys = {(model.backend, model.name) for model in UPSCALE_MODELS}
    non_rep = sorted(
        (single_upscale_model(backend, name)
         for backend, names in available_upscale.items()
         for name in names),
        key=lambda model: (model.backend, model.name),
    )
    non_rep = [model for model in non_rep if (model.backend, model.name) not in rep_keys]
    partner_count = max(1, sample_extra)
    for model in non_rep:
        # 配对用模型名哈希而不是列表索引：模型库增删时既有配对保持稳定，
        # 新增模型只会产生自己的增量用例，不会让已跑用例的配对整体失效。
        digest = hashlib.sha256(model.name.encode("utf-8")).digest()
        partners = {
            INTERP_MODELS[digest[offset] % len(INTERP_MODELS)]
            for offset in range(min(partner_count, len(digest)))
        }
        for partner in sorted(partners, key=lambda item: (item.backend, item.name)):
            for flow in FLOWS:
                cases.append(make_case("interp", "sampled", flow, model, partner))

    # ── hdr HDR 相 ──
    cases.extend(hdr_cases())

    # case_id 兜底去重（分层设计理论上已避免重叠）。
    deduped: dict[str, MatrixCase] = {}
    for case in cases:
        deduped.setdefault(case.case_id, case)
    return list(deduped.values())


def run_capture(command: list[str], timeout: int) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        command,
        text=True,
        encoding="utf-8",
        errors="replace",
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        timeout=timeout,
        check=False,
    )


def parse_json_array(output: str) -> list[str]:
    for line in reversed(output.splitlines()):
        line = line.strip()
        if line.startswith("[") and line.endswith("]"):
            value = json.loads(line)
            if isinstance(value, list):
                return [str(item) for item in value]
    raise ValueError("命令输出中没有 JSON 数组")


def load_and_validate_catalog(
    exe: Path,
) -> tuple[dict[str, list[str]], dict[str, list[str]]]:
    available_upscale: dict[str, list[str]] = {}
    for backend in ("ncnn", "cuda", "tensorrt", "onnx", "flashvsr", "basicvsrpp"):
        result = run_capture([str(exe), "--list-models", "--backend", backend, "--json"], 120)
        if result.returncode != 0:
            raise RuntimeError(f"无法读取 {backend} 超分清单：{result.stderr.strip()}")
        available_upscale[backend] = parse_json_array(result.stdout)

    available_interp: dict[str, list[str]] = {}
    for backend in ("ncnn", "cuda", "tensorrt"):
        result = run_capture(
            [str(exe), "--list-interp-models", "--interp-backend", backend, "--json"],
            180,
        )
        if result.returncode != 0:
            raise RuntimeError(f"无法读取 {backend} 补帧清单：{result.stderr.strip()}")
        available_interp[backend] = parse_json_array(result.stdout)

    missing = [
        f"超分 {model.backend}: {model.name}"
        for model in UPSCALE_MODELS
        if model.name not in available_upscale[model.backend]
    ]
    missing.extend(
        f"补帧 {model.backend}: {model.name}"
        for model in INTERP_MODELS
        if model.name not in available_interp[model.backend]
    )
    if missing:
        raise RuntimeError("代表模型缺失：\n" + "\n".join(missing))
    # 单模型阶段覆盖安装目录中 CLI 实际列出的全部模型；重复项会导致用例 ID 冲突，必须拒绝。
    for label, catalog in (("超分", available_upscale), ("补帧", available_interp)):
        duplicates = [
            f"{backend}: {name}"
            for backend, names in catalog.items()
            for name, count in Counter(names).items()
            if count > 1
        ]
        if duplicates:
            raise RuntimeError(label + "模型清单存在重复项：\n" + "\n".join(duplicates))
    return available_upscale, available_interp


def ensure_fixture(
    ffmpeg: Path, fixture_dir: Path, width: int, height: int, tag: str = "sdr",
) -> Path:
    """tag: sdr=FFV1（RVE 软解）、h264=libx264（sidecar 只能 D3D11VA 硬解）、pq=x265 PQ。"""
    fixture_dir.mkdir(parents=True, exist_ok=True)
    path = fixture_dir / f"matrix-{tag}-{width}x{height}-4f.mkv"
    if path.exists() and path.stat().st_size > 0:
        return path
    if tag == "h264":
        # sidecar 强制 D3D11VA 硬解，FFV1 无硬件解码器；RTX 用例输入一律用 h264。
        command = [
            str(ffmpeg), "-hide_banner", "-loglevel", "error", "-y",
            "-f", "lavfi", "-i", f"testsrc2=size={width}x{height}:rate=4",
            "-frames:v", "4", "-c:v", "libx264", "-preset", "ultrafast",
            "-pix_fmt", "yuv420p",
            str(path),
        ]
        result = run_capture(command, 120)
        if result.returncode != 0 or not path.exists():
            raise RuntimeError(f"无法生成测试视频 {path}：{result.stderr.strip()}")
        return path
    if tag == "sdr":
        command = [
            str(ffmpeg), "-hide_banner", "-loglevel", "error", "-y",
            "-f", "lavfi", "-i", f"testsrc2=size={width}x{height}:rate=4",
            "-frames:v", "4", "-c:v", "ffv1", "-level", "3", "-pix_fmt", "yuv420p",
            str(path),
        ]
        result = run_capture(command, 120)
        if result.returncode != 0 or not path.exists():
            raise RuntimeError(f"无法生成测试视频 {path}：{result.stderr.strip()}")
        return path
    # PQ 夹具：10bit + BT.2020/smpte2084 完整色彩标记，供 CLI 的 DetectHdrMode 识别。
    # 实测本机 ffmpeg：-color_trc 选项会令 mkv 里 transfer 归为 unknown（ffv1 同样丢），
    # 只有 x265 自带的 -x265-params 写 VUI 后 mkv 才保留 smpte2084；
    # 因此 x265-params 参数必须独占，不能再叠加 -color_* 选项。生成后自校验 smpte2084。
    attempts = [
        ("x265", ["-c:v", "libx265", "-preset", "ultrafast", "-pix_fmt", "yuv420p10le",
                  "-x265-params", "colorprim=bt2020:transfer=smpte2084:colormatrix=bt2020nc"]),
        ("ffv1", ["-c:v", "ffv1", "-level", "3", "-pix_fmt", "yuv420p10le",
                  "-color_primaries", "bt2020", "-color_trc", "smpte2084",
                  "-colorspace", "bt2020nc"]),
    ]
    for label, encoder_args in attempts:
        trial = fixture_dir / f"matrix-pq-{width}x{height}-4f-{label}.mkv"
        command = [
            str(ffmpeg), "-hide_banner", "-loglevel", "error", "-y",
            "-f", "lavfi", "-i", f"testsrc2=size={width}x{height}:rate=4",
            "-frames:v", "4", *encoder_args,
            str(trial),
        ]
        result = run_capture(command, 120)
        if result.returncode != 0 or not trial.exists():
            trial.unlink(missing_ok=True)
            continue
        check = run_capture([str(ffmpeg), "-hide_banner", "-i", str(trial)], 60)
        if re.search(r"smpte2084", check.stderr + check.stdout, re.I):
            if path.exists():
                path.unlink()
            trial.replace(path)
            return path
        trial.unlink(missing_ok=True)
    raise RuntimeError(f"无法生成带 smpte2084 标记的 PQ 测试视频 {path}")


def ffmpeg_settings(output: Path) -> str:
    escaped = str(output).replace('"', '\\"')
    return (
        "-c:v ffv1 -level 3 -coder 1 -context 1 -g 1 "
        f"-pix_fmt gbrp10le \"{escaped}\" -y"
    )


def rtx_ffmpeg_settings(output: Path, *, hdr: bool = False) -> str:
    """RTX 处理帧交回宿主 FFmpeg；矩阵显式选择与位深匹配的最终编码器。"""
    escaped = str(output).replace('"', '\\"')
    encoder = "-c:v hevc_nvenc -pix_fmt p010le" if hdr else "-c:v h264_nvenc"
    return f"{encoder} \"{escaped}\" -y"


def build_rule_command(
    exe: Path, case: MatrixCase, source: Path, output: Path,
) -> list[str]:
    command = [str(exe), "-i", str(source)]
    if case.flow == "rule-pq-ncnn":
        command.extend(["-backend", "ncnn", "-modelpath", NCNN_UPSCALE[0][1]])
    elif case.flow == "rule-pq-rtx-hdr":
        command.extend(["-no-upscale", "-backend", "rtxvsr", "-rtx-hdr"])
    elif case.flow == "rule-no-upscale-invalid":
        command.extend(["-no-upscale", "-backend", "ncnn"])
    elif case.flow == "rule-segment-interp":
        command.extend([
            "-backend", "ncnn",
            "--segments-base64", base64.b64encode(b"[]").decode("ascii"),
            "-interp-model", INTERP_MODELS[0].name,
            "-interp-backend", "ncnn",
        ])
    elif case.flow == "rule-pq-segment":
        # 用 cuda 超分后端，避开「ncnn 不支持 HDR 输入」的先触门禁，直击分段门禁本身。
        command.extend([
            "-backend", "cuda",
            "--segments-base64", base64.b64encode(b"[]").decode("ascii"),
        ])
    elif case.flow == "rule-webm-native":
        command.extend([
            "-backend", "rtxvsr", "-rtx-target", "2x",
            "-rtx-quality", str(RTX_BASE_QUALITY),
        ])
    else:
        raise ValueError(f"未知门禁用例：{case.flow}")
    command.extend(["-ffmpeg-settings", rtx_ffmpeg_settings(output, hdr=case.rtx_hdr)])
    return command


def build_command(exe: Path, case: MatrixCase, source: Path, output: Path) -> list[str]:
    if case.kind == "rule":
        return build_rule_command(exe, case, source, output)
    command = [str(exe), "-i", str(source)]
    if case.kind == "rtx":
        if case.no_upscale:
            # 纯 RTX HDR：插件在该场景不传 -backend，CLI 默认 ncnn，超分按 1x 直通 sidecar。
            command.append("-no-upscale")
        else:
            command.extend(["-backend", case.upscale_backend])
            if case.upscale_model != "-":
                command.extend(["-modelpath", case.upscale_model])
            if case.rtx_target != "-":
                command.extend([
                    "-rtx-target", case.rtx_target,
                    "-rtx-quality", str(case.rtx_quality),
                ])
        if case.rtx_hdr:
            command.append("-rtx-hdr")
    elif case.upscale_model == "-":
        command.extend(["-no-upscale", "-backend", case.interp_backend])
    else:
        command.extend([
            "-backend", case.upscale_backend,
            "-modelpath", case.upscale_model,
        ])
    if case.interp_model != "-":
        command.extend([
            "-interp-backend", case.interp_backend,
            "-interp-model", case.interp_model,
            "-interp-factor", "2",
        ])
        command.extend(["-scene-threshold", "4"])
        if case.kind == "rtx":
            # 插件在该场景会传用户设置的顺序；CLI 强制“先补帧，再 RTX 超分”。
            # 这里传默认 upscale-first，校验强制横幅确实出现。
            command.extend(["-process-order", "upscale-first"])
        elif case.upscale_model != "-":
            command.extend(["-process-order", case.flow])
    elif case.kind == "traditional" and case.flow in FLOWS:
        command.extend(["-process-order", case.flow])
    command.extend([
        "-scene-threshold", "4",
        "-ffmpeg-settings",
        rtx_ffmpeg_settings(output, hdr=case.rtx_hdr) if case.kind in {"rtx", "rule"} else ffmpeg_settings(output),
    ])
    return command


def probe_video(ffprobe: Path, output: Path) -> dict[str, int | str]:
    command = [
        str(ffprobe), "-v", "error", "-select_streams", "v:0", "-count_frames",
        "-show_entries",
        "stream=width,height,nb_read_frames,nb_frames,avg_frame_rate,pix_fmt",
        "-of", "json", str(output),
    ]
    result = run_capture(command, 120)
    if result.returncode != 0:
        raise RuntimeError(result.stderr.strip() or "ffprobe 失败")
    payload = json.loads(result.stdout)
    streams = payload.get("streams") or []
    if not streams:
        raise RuntimeError("输出没有视频流")
    stream = streams[0]
    frame_text = stream.get("nb_read_frames") or stream.get("nb_frames") or "0"
    return {
        "width": int(stream.get("width") or 0),
        "height": int(stream.get("height") or 0),
        "frames": int(frame_text) if str(frame_text).isdigit() else 0,
        "fps": str(stream.get("avg_frame_rate") or ""),
        "pix_fmt": str(stream.get("pix_fmt") or ""),
    }


def probe_luma_range(ffmpeg: Path, output: Path) -> float:
    """检查首帧是否退化成纯色；NaN 转 uint8 会被静默写成整帧黑色。"""
    command = [
        str(ffmpeg), "-v", "error", "-i", str(output), "-frames:v", "1",
        "-vf", "signalstats,metadata=print:file=-", "-f", "null", "-",
    ]
    result = run_capture(command, 120)
    if result.returncode != 0:
        raise RuntimeError(result.stderr.strip() or "FFmpeg 内容探测失败")
    minimum = re.search(r"lavfi\.signalstats\.YMIN=([\d.]+)", result.stdout)
    maximum = re.search(r"lavfi\.signalstats\.YMAX=([\d.]+)", result.stdout)
    if not minimum or not maximum:
        raise RuntimeError("FFmpeg 未返回首帧亮度范围")
    return float(maximum.group(1)) - float(minimum.group(1))


def error_summary(stdout: str, stderr: str) -> str:
    lines = [line.strip() for line in (stderr + "\n" + stdout).splitlines() if line.strip()]
    preferred = [
        line for line in lines
        if re.search(r"error|exception|traceback|失败|错误|fatal|out of memory", line, re.I)
    ]
    chosen = preferred[-4:] if preferred else lines[-4:]
    return " | ".join(chosen)[:1200]


def timeout_for(case: MatrixCase, default_timeout: int, trt_timeout: int) -> int:
    if case.kind == "rule":
        return 180
    if "tensorrt" in (case.upscale_backend, case.interp_backend):
        return trt_timeout
    if case.kind == "rtx":
        return max(default_timeout, 600)
    if case.upscale_backend in ("flashvsr", "basicvsrpp"):
        return max(default_timeout, 1200)
    return default_timeout


def case_needs_sidecar(case: MatrixCase) -> bool:
    return case.kind == "rtx" or case.flow == "rule-webm-native"


def execute_case(
    case: MatrixCase,
    exe: Path,
    ffmpeg: Path,
    ffprobe: Path,
    result_dir: Path,
    default_timeout: int,
    trt_timeout: int,
    keep_failed_output: bool,
    rtx_env_ok: bool,
) -> dict[str, object]:
    fixture_tag = "pq" if case.pq_input else ("h264" if case_needs_sidecar(case) else "sdr")
    fixture = ensure_fixture(
        ffmpeg, result_dir / "fixtures", case.width, case.height, fixture_tag)
    output_dir = result_dir / "outputs"
    output_dir.mkdir(parents=True, exist_ok=True)
    output = output_dir / f"{case.case_id}.{case.container}"
    if output.exists():
        output.unlink()

    started = time.monotonic()
    command: list[str] = []
    stdout = ""
    stderr = ""
    status = "FAIL_EXIT"
    probe: dict[str, int | str] = {}
    exit_code: int | None = None
    if case_needs_sidecar(case) and not rtx_env_ok:
        status = "SKIP_ENV"
        stderr = "RTX 环境检查未通过，用例跳过"
    else:
        command = build_command(exe, case, fixture, output)
        try:
            result = run_capture(command, timeout_for(case, default_timeout, trt_timeout))
            stdout, stderr, exit_code = result.stdout, result.stderr, result.returncode
            combined_output = stderr + "\n" + stdout
            if case.kind == "rule":
                # 门禁用例：预期失败。退出码非零且错误命中期望模式才算通过。
                if result.returncode == 0:
                    status = "FAIL_GATE"
                elif re.search(case.expected_error, combined_output, re.I):
                    status = "PASS"
                else:
                    status = "FAIL_GATE_MISMATCH"
            elif result.returncode != 0:
                status = "SKIP_OOM" if re.search(
                    r"CUDA out of memory|torch\.OutOfMemoryError|检测到内存不足",
                    combined_output,
                    re.I,
                ) else "FAIL_EXIT"
            elif not output.exists() or output.stat().st_size == 0:
                status = "FAIL_OUTPUT"
            else:
                expected_width, expected_height = case.expected_width, case.expected_height
                if case.kind == "rtx" and case.rtx_target != "-":
                    # CLI 打印的输出映射是权威基准（含 clamp 后的实际倍率）。
                    mapping = re.search(
                        r"\[RTX VSR\] 输出映射：\d+x\d+ × [\d.]+ → (\d+)x(\d+)", stdout)
                    if mapping:
                        expected_width, expected_height = (
                            int(mapping.group(1)), int(mapping.group(2)))
                try:
                    probe = probe_video(ffprobe, output)
                    probe["luma_range"] = probe_luma_range(ffmpeg, output)
                    if (case.kind == "rtx" and case.upscale_backend == "rtxvsr"
                            and case.interp_model != "-" and RTX_ORDER_BANNER not in stdout):
                        status = "FAIL_ORDER"
                    elif probe["width"] != expected_width or probe["height"] != expected_height:
                        status = "FAIL_DIMENSIONS"
                    elif probe["frames"] != case.expected_frames:
                        status = "FAIL_FRAMES"
                    elif case.rtx_hdr and "10" not in str(probe.get("pix_fmt", "")):
                        status = "FAIL_BITDEPTH"
                    elif float(probe["luma_range"]) < 1.0:
                        status = "FAIL_CONTENT"
                    else:
                        status = "PASS"
                except Exception as exc:  # noqa: BLE001 - 需要把探测失败写入矩阵
                    status = "FAIL_PROBE"
                    stderr += f"\nPROBE ERROR: {exc}"
        except subprocess.TimeoutExpired as exc:
            status = "TIMEOUT"
            stdout = exc.stdout or ""
            stderr = exc.stderr or ""
            if isinstance(stdout, bytes):
                stdout = stdout.decode("utf-8", "replace")
            if isinstance(stderr, bytes):
                stderr = stderr.decode("utf-8", "replace")

    elapsed = round(time.monotonic() - started, 3)
    record: dict[str, object] = {
        **asdict(case),
        "status": status,
        "exit_code": exit_code,
        "elapsed_seconds": elapsed,
        "actual_width": probe.get("width", 0),
        "actual_height": probe.get("height", 0),
        "actual_frames": probe.get("frames", 0),
        "actual_fps": probe.get("fps", ""),
        "actual_pix_fmt": probe.get("pix_fmt", ""),
        "luma_range": probe.get("luma_range", 0),
        "output_bytes": output.stat().st_size if output.exists() else 0,
        "error": error_summary(stdout, stderr) if status != "PASS" else "",
        "command": command,
        "finished_at": time.strftime("%Y-%m-%d %H:%M:%S"),
    }
    if status != "PASS" and status != "SKIP_ENV":
        log_dir = result_dir / "logs"
        log_dir.mkdir(parents=True, exist_ok=True)
        (log_dir / f"{case.case_id}.log").write_text(
            "COMMAND\n" + subprocess.list2cmdline(command)
            + "\n\nSTDOUT\n" + stdout + "\n\nSTDERR\n" + stderr,
            encoding="utf-8",
        )
    if output.exists() and (status == "PASS" or not keep_failed_output):
        output.unlink()
    return record


def read_records(path: Path) -> dict[str, dict[str, object]]:
    records: dict[str, dict[str, object]] = {}
    if not path.exists():
        return records
    for line in path.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        record = json.loads(line)
        if (
            record.get("status") == "FAIL_EXIT"
            and re.search(
                r"CUDA out of memory|torch\.OutOfMemoryError|检测到内存不足",
                str(record.get("error", "")),
                re.I,
            )
        ):
            # 历史运行已经保留完整日志；读取时升级为资源跳过，避免再次触发 OOM。
            record["status"] = "SKIP_OOM"
        records[str(record["case_id"])] = record
    return records


def markdown_cell(value: object) -> str:
    return str(value).replace("|", "\\|").replace("\n", " ")


def write_reports(result_dir: Path, records: dict[str, dict[str, object]], total_cases: int) -> None:
    ordered = sorted(
        records.values(),
        key=lambda row: (str(row["phase"]), str(row["layer"]), str(row["upscale_backend"]),
                         str(row["interp_backend"]), str(row["upscale_category"]),
                         str(row["interp_category"])),
    )
    csv_path = result_dir / "matrix.csv"
    fields = [
        "case_id", "phase", "layer", "flow", "kind", "upscale_backend", "upscale_category",
        "upscale_model", "interp_backend", "interp_category", "interp_model",
        "rtx_target", "rtx_quality", "rtx_hdr", "container", "status", "exit_code",
        "elapsed_seconds", "expected_width", "expected_height", "expected_frames",
        "actual_width", "actual_height", "actual_frames", "actual_fps", "actual_pix_fmt", "luma_range",
        "output_bytes", "error",
    ]
    with csv_path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(ordered)

    status_counts = Counter(str(row["status"]) for row in ordered)
    groups: dict[tuple[str, str, str, str], Counter[str]] = defaultdict(Counter)
    for row in ordered:
        key = (
            str(row["phase"]), str(row["layer"]),
            str(row["upscale_backend"]), str(row["interp_backend"]),
        )
        groups[key][str(row["status"])] += 1

    summary_lines = [
        "# GPU 模型兼容性矩阵（1.3.0 三相）",
        "",
        f"- 计划用例：{total_cases}",
        f"- 已有结果：{len(ordered)}",
        f"- 通过：{status_counts.get('PASS', 0)}",
        f"- 资源不足跳过：{status_counts.get('SKIP_OOM', 0)}",
        f"- 环境跳过：{status_counts.get('SKIP_ENV', 0)}",
        f"- 失败/超时：{sum(count for status, count in status_counts.items() if status not in TERMINAL_STATUSES)}",
        "- 输入：传统相默认 96×64、4 帧；固定输入 ONNX 模型及 GIMM 使用较大低分辨率夹具；"
        f"RTX/HDR 相使用 {RTX_WIDTH}×{RTX_HEIGHT}、4 帧。",
        "",
        "## 分层通过性",
        "",
        "| 相 | 层级 | 超分后端 | 补帧后端 | 通过 | 跳过 | 失败 | 总数 |",
        "|---|---|---|---|---:|---:|---:|---:|",
    ]
    for key, counts in sorted(groups.items()):
        passed = counts.get("PASS", 0)
        skipped = counts.get("SKIP_OOM", 0) + counts.get("SKIP_ENV", 0)
        total = sum(counts.values())
        summary_lines.append(
            f"| {key[0]} | {key[1]} | {key[2]} | {key[3]} | {passed} | {skipped} | "
            f"{total - passed - skipped} | {total} |"
        )
    summary_lines.extend([
        "",
        "## 状态统计",
        "",
        "| 状态 | 数量 |",
        "|---|---:|",
    ])
    for status, count in sorted(status_counts.items()):
        summary_lines.append(f"| {status} | {count} |")
    failures = [row for row in ordered if row["status"] not in TERMINAL_STATUSES]
    skipped = [row for row in ordered if row["status"] in ("SKIP_OOM", "SKIP_ENV")]
    summary_lines.extend([
        "",
        "## 资源/环境跳过项",
        "",
        "| ID | 相/层级 | 超分 | 补帧 | 状态 | 原因 |",
        "|---|---|---|---|---|---|",
    ])
    for row in skipped:
        summary_lines.append(
            "| {case_id} | {phase}/{layer} | {upscale_backend}/{upscale_category} | "
            "{interp_backend}/{interp_category} | {status} | {error} |".format(
                **{key: markdown_cell(value) for key, value in row.items()}
            )
        )
    if not skipped:
        summary_lines.append("| - | - | - | - | - | 暂无 |")
    summary_lines.extend([
        "",
        "## 失败项",
        "",
        "| ID | 相/层级 | 超分 | 补帧 | 状态 | 错误摘要 |",
        "|---|---|---|---|---|---|",
    ])
    for row in failures:
        summary_lines.append(
            "| {case_id} | {phase}/{layer} | {upscale_backend}/{upscale_category} | "
            "{interp_backend}/{interp_category} | {status} | {error} |".format(
                **{key: markdown_cell(value) for key, value in row.items()}
            )
        )
    if not failures:
        summary_lines.append("| - | - | - | - | - | 暂无 |")
    (result_dir / "summary.md").write_text("\n".join(summary_lines) + "\n", encoding="utf-8")

    detail_lines = [
        "# GPU 矩阵逐项结果",
        "",
        "| ID | 相 | 层级 | 流程 | 超分后端/类别 | 补帧后端/类别 | 结果 | 秒 | 输出 | 帧数 |",
        "|---|---|---|---|---|---|---|---:|---|---:|",
    ]
    for row in ordered:
        detail_lines.append(
            f"| {row['case_id']} | {row['phase']} | {row['layer']} | {row['flow']} | "
            f"{row['upscale_backend']}/{row['upscale_category']} | "
            f"{row['interp_backend']}/{row['interp_category']} | {row['status']} | "
            f"{row['elapsed_seconds']} | {row['actual_width']}×{row['actual_height']} | "
            f"{row['actual_frames']} |"
        )
    (result_dir / "matrix.md").write_text("\n".join(detail_lines) + "\n", encoding="utf-8")


def filtered_cases(cases: list[MatrixCase], args: argparse.Namespace) -> list[MatrixCase]:
    selected = cases
    if args.phase != "all":
        selected = [case for case in selected if case.phase == args.phase]
    if args.backend_pair:
        upscale_backend, interp_backend = args.backend_pair.split(":", 1)
        selected = [
            case for case in selected
            if case.upscale_backend == upscale_backend and case.interp_backend == interp_backend
        ]
    if args.case_id:
        selected = [case for case in selected if case.case_id == args.case_id]
    if args.match:
        pattern = re.compile(args.match, re.I)
        selected = [case for case in selected if pattern.search(json.dumps(asdict(case), ensure_ascii=False))]
    return selected[: args.max_cases] if args.max_cases else selected


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--exe",
        type=Path,
        default=Path(os.environ.get("VIDEOENHANCER_EXE") or
                     Path(__file__).resolve().parents[2] / "Artifacts" / "videoenhancer.exe"),
    )
    parser.add_argument(
        "--result-dir",
        type=Path,
        default=Path(__file__).resolve().parents[2] / "test-results" / "gpu-matrix-1.3.0",
    )
    parser.add_argument("--phase", choices=("all", "upscale", "interp", "hdr"), default="all")
    parser.add_argument("--backend-pair", help="只运行超分:补帧后端，例如 cuda:tensorrt")
    parser.add_argument("--case-id")
    parser.add_argument("--match", help="按用例 JSON 正则筛选")
    parser.add_argument("--max-cases", type=int, default=0)
    parser.add_argument("--timeout", type=int, default=420)
    parser.add_argument("--trt-timeout", type=int, default=2400)
    parser.add_argument("--jobs", type=int, default=1, help="并行运行的 GPU 用例数")
    parser.add_argument("--sample-extra", type=int, default=2,
                        help="抽样交叉层每个非代表超分模型配对的补帧代表数量")
    parser.add_argument("--rerun-failed", action="store_true")
    parser.add_argument("--keep-failed-output", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--report-only", action="store_true")
    args = parser.parse_args()
    if args.jobs < 1:
        parser.error("--jobs 必须大于等于 1")
    if args.sample_extra < 1:
        parser.error("--sample-extra 必须大于等于 1")

    exe = args.exe.resolve()
    core_root = exe.parent
    ffmpeg = core_root / "bin" / "ffmpeg" / "ffmpeg.exe"
    ffprobe = core_root / "bin" / "ffmpeg" / "ffprobe.exe"
    for required in (exe, ffmpeg, ffprobe):
        if not required.is_file():
            parser.error(f"文件不存在：{required}")

    args.result_dir.mkdir(parents=True, exist_ok=True)
    available_upscale, available_interp = load_and_validate_catalog(exe)
    all_cases = generate_cases(available_upscale, available_interp, args.sample_extra)
    selected = filtered_cases(all_cases, args)
    result_path = args.result_dir / "results.jsonl"
    records = read_records(result_path)
    current_case_ids = {case.case_id for case in all_cases}
    # 模型最低尺寸等矩阵定义调整后，历史 ID 仍留在 JSONL 供审计，但不进入当前报告。
    records = {case_id: record for case_id, record in records.items() if case_id in current_case_ids}

    if args.report_only:
        write_reports(args.result_dir, records, len(all_cases))
        print(args.result_dir / "summary.md")
        return 0

    phase_counts = Counter(case.phase for case in all_cases)
    layer_counts = Counter((case.phase, case.layer) for case in all_cases)
    print(
        "矩阵计划（按相）："
        + ", ".join(f"{phase}={count}" for phase, count in sorted(phase_counts.items()))
        + f", total={len(all_cases)}, selected={len(selected)}"
    )
    print("矩阵计划（按相/层）："
          + ", ".join(f"{phase}/{layer}={count}"
                      for (phase, layer), count in sorted(layer_counts.items())))

    if args.dry_run:
        for case in selected:
            print(json.dumps(asdict(case), ensure_ascii=False))
        return 0

    # RTX 用例依赖 sidecar；环境检查失败时整批记 SKIP_ENV，不逐个启动失败。
    rtx_env_ok = True
    if any(case_needs_sidecar(case) for case in selected):
        check = run_capture([str(exe), "--check", "-backend", "rtxvsr"], 120)
        rtx_env_ok = check.returncode == 0
        if not rtx_env_ok:
            print("[警告] RTX 环境检查未通过，RTX 用例将记 SKIP_ENV："
                  + ((check.stdout.strip().splitlines() or [""])[-1]))

    pending = []
    for case in selected:
        previous = records.get(case.case_id)
        if previous and previous.get("status") in TERMINAL_STATUSES:
            continue
        if previous and not args.rerun_failed:
            continue
        pending.append(case)
    print(f"已完成/保留 {len(selected) - len(pending)}，本批待运行 {len(pending)}")

    def announce(index: int, case: MatrixCase) -> None:
        print(
            f"[{index}/{len(pending)}] {case.case_id} {case.phase}/{case.layer}/{case.flow} "
            f"up={case.upscale_backend}:{case.upscale_category} "
            f"interp={case.interp_backend}:{case.interp_category}",
            flush=True,
        )

    def persist(result_file, case: MatrixCase, record: dict[str, object]) -> None:
        records[case.case_id] = record
        result_file.write(json.dumps(record, ensure_ascii=False) + "\n")
        print(
            f"  => {case.case_id} {record['status']} {record['elapsed_seconds']}s "
            f"{record['actual_width']}x{record['actual_height']} {record['actual_frames']}f",
            flush=True,
        )
        write_reports(args.result_dir, records, len(all_cases))

    def is_special(case: MatrixCase) -> bool:
        # RTX/门禁用例共享 sidecar 与 NVENC，GIMM 对显存峰值敏感，均独占 GPU 运行。
        return case.kind != "traditional" or case.interp_category == "GIMM"

    with result_path.open("a", encoding="utf-8", buffering=1) as result_file:
        if args.jobs == 1:
            for index, case in enumerate(pending, 1):
                announce(index, case)
                record = execute_case(
                    case, exe, ffmpeg, ffprobe, args.result_dir,
                    args.timeout, args.trt_timeout, args.keep_failed_output, rtx_env_ok,
                )
                persist(result_file, case, record)
        else:
            # GPU 推理由工作线程并行；JSONL 与报告始终由主线程串行写入。
            with ThreadPoolExecutor(max_workers=args.jobs) as executor:
                queued = list(enumerate(pending, 1))
                active = {}

                def submit_next() -> bool:
                    if any(is_special(active_case) for active_case in active.values()):
                        return False
                    # 优先提交传统用例；队列只剩特殊用例且 GPU 空闲时才独占提交一个。
                    position = next(
                        (
                            position for position, (_, queued_case) in enumerate(queued)
                            if not is_special(queued_case)
                        ),
                        None,
                    )
                    if position is None:
                        if active or not queued:
                            return False
                        position = 0
                    index, case = queued.pop(position)
                    announce(index, case)
                    future = executor.submit(
                        execute_case,
                        case, exe, ffmpeg, ffprobe, args.result_dir,
                        args.timeout, args.trt_timeout, args.keep_failed_output, rtx_env_ok,
                    )
                    active[future] = case
                    return True

                while len(active) < args.jobs and submit_next():
                    pass
                while active:
                    completed = next(as_completed(active))
                    case = active.pop(completed)
                    persist(result_file, case, completed.result())
                    while len(active) < args.jobs and submit_next():
                        pass

    write_reports(args.result_dir, records, len(all_cases))
    return 0 if all(record.get("status") in TERMINAL_STATUSES for record in records.values()) else 1


if __name__ == "__main__":
    sys.exit(main())
