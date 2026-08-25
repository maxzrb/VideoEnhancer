#!/usr/bin/env python3
"""在真实 RVE 环境中运行可断点恢复的代表模型 GPU 兼容矩阵。"""

from __future__ import annotations

import argparse
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
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Iterable


TERMINAL_STATUSES = {"PASS", "SKIP_OOM"}


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
    phase: str
    flow: str
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


def stable_case_id(parts: Iterable[str]) -> str:
    raw = "\0".join(parts).encode("utf-8")
    return hashlib.sha256(raw).hexdigest()[:16]


def make_case(
    phase: str,
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
    expected_frames = 7 if interp else 4
    parts = [
        phase,
        flow,
        upscale.backend if upscale else "none",
        upscale.name if upscale else "none",
        interp.backend if interp else "none",
        interp.name if interp else "none",
        f"{width}x{height}",
    ]
    return MatrixCase(
        case_id=stable_case_id(parts),
        phase=phase,
        flow=flow,
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
        expected_frames=expected_frames,
    )


def generate_cases() -> list[MatrixCase]:
    cases: list[MatrixCase] = []
    for interp in INTERP_MODELS:
        cases.append(make_case("single", "single-interp", None, interp))
    for upscale in UPSCALE_MODELS:
        cases.append(make_case("single", "single-upscale", upscale, None))

    for upscale in UPSCALE_MODELS:
        for interp in INTERP_MODELS:
            phase = "same" if upscale.backend == interp.backend else "cross"
            for flow in ("upscale-first", "interp-first"):
                cases.append(make_case(phase, flow, upscale, interp))
    return cases


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


def validate_catalog(exe: Path) -> None:
    available_upscale: dict[str, set[str]] = {}
    for backend in ("ncnn", "cuda", "tensorrt", "onnx", "flashvsr", "basicvsrpp"):
        result = run_capture([str(exe), "--list-models", "--backend", backend, "--json"], 120)
        if result.returncode != 0:
            raise RuntimeError(f"无法读取 {backend} 超分清单：{result.stderr.strip()}")
        available_upscale[backend] = set(parse_json_array(result.stdout))

    available_interp: dict[str, set[str]] = {}
    for backend in ("ncnn", "cuda", "tensorrt"):
        result = run_capture(
            [str(exe), "--list-interp-models", "--interp-backend", backend, "--json"],
            180,
        )
        if result.returncode != 0:
            raise RuntimeError(f"无法读取 {backend} 补帧清单：{result.stderr.strip()}")
        available_interp[backend] = set(parse_json_array(result.stdout))

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


def ensure_fixture(ffmpeg: Path, fixture_dir: Path, width: int, height: int) -> Path:
    fixture_dir.mkdir(parents=True, exist_ok=True)
    path = fixture_dir / f"matrix-{width}x{height}-4f.mkv"
    if path.exists() and path.stat().st_size > 0:
        return path
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


def ffmpeg_settings(output: Path) -> str:
    escaped = str(output).replace('"', '\\"')
    return (
        "-c:v ffv1 -level 3 -coder 1 -context 1 -g 1 "
        f"-pix_fmt gbrp10le \"{escaped}\" -y"
    )


def build_command(exe: Path, case: MatrixCase, source: Path, output: Path) -> list[str]:
    command = [str(exe), "-i", str(source)]
    if case.upscale_model == "-":
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
    if case.flow in ("upscale-first", "interp-first"):
        command.extend(["-process-order", case.flow])
    command.extend([
        "-scene-threshold", "4",
        "-ffmpeg-settings", ffmpeg_settings(output),
    ])
    return command


def probe_video(ffprobe: Path, output: Path) -> dict[str, int | str]:
    command = [
        str(ffprobe), "-v", "error", "-select_streams", "v:0", "-count_frames",
        "-show_entries", "stream=width,height,nb_read_frames,nb_frames,avg_frame_rate",
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
    }


def error_summary(stdout: str, stderr: str) -> str:
    lines = [line.strip() for line in (stderr + "\n" + stdout).splitlines() if line.strip()]
    preferred = [
        line for line in lines
        if re.search(r"error|exception|traceback|失败|错误|fatal|out of memory", line, re.I)
    ]
    chosen = preferred[-4:] if preferred else lines[-4:]
    return " | ".join(chosen)[:1200]


def timeout_for(case: MatrixCase, default_timeout: int, trt_timeout: int) -> int:
    if "tensorrt" in (case.upscale_backend, case.interp_backend):
        return trt_timeout
    if case.upscale_backend in ("flashvsr", "basicvsrpp"):
        return max(default_timeout, 1200)
    return default_timeout


def execute_case(
    case: MatrixCase,
    exe: Path,
    ffmpeg: Path,
    ffprobe: Path,
    result_dir: Path,
    default_timeout: int,
    trt_timeout: int,
    keep_failed_output: bool,
) -> dict[str, object]:
    fixture = ensure_fixture(ffmpeg, result_dir / "fixtures", case.width, case.height)
    output_dir = result_dir / "outputs"
    output_dir.mkdir(parents=True, exist_ok=True)
    output = output_dir / f"{case.case_id}.mkv"
    if output.exists():
        output.unlink()
    command = build_command(exe, case, fixture, output)
    started = time.monotonic()
    stdout = ""
    stderr = ""
    status = "FAIL_EXIT"
    probe: dict[str, int | str] = {}
    exit_code: int | None = None
    try:
        result = run_capture(command, timeout_for(case, default_timeout, trt_timeout))
        stdout, stderr, exit_code = result.stdout, result.stderr, result.returncode
        if result.returncode != 0:
            combined_output = stderr + "\n" + stdout
            status = "SKIP_OOM" if re.search(
                r"CUDA out of memory|torch\.OutOfMemoryError|检测到内存不足",
                combined_output,
                re.I,
            ) else "FAIL_EXIT"
        elif not output.exists() or output.stat().st_size == 0:
            status = "FAIL_OUTPUT"
        else:
            try:
                probe = probe_video(ffprobe, output)
                if probe["width"] != case.expected_width or probe["height"] != case.expected_height:
                    status = "FAIL_DIMENSIONS"
                elif probe["frames"] != case.expected_frames:
                    status = "FAIL_FRAMES"
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
        "output_bytes": output.stat().st_size if output.exists() else 0,
        "error": error_summary(stdout, stderr) if status != "PASS" else "",
        "command": command,
        "finished_at": time.strftime("%Y-%m-%d %H:%M:%S"),
    }
    if status != "PASS":
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
        key=lambda row: (str(row["phase"]), str(row["flow"]), str(row["upscale_backend"]),
                         str(row["interp_backend"]), str(row["upscale_category"]), str(row["interp_category"])),
    )
    csv_path = result_dir / "matrix.csv"
    fields = [
        "case_id", "phase", "flow", "upscale_backend", "upscale_category", "upscale_model",
        "interp_backend", "interp_category", "interp_model", "status", "exit_code",
        "elapsed_seconds", "expected_width", "expected_height", "expected_frames",
        "actual_width", "actual_height", "actual_frames", "actual_fps", "output_bytes", "error",
    ]
    with csv_path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(ordered)

    status_counts = Counter(str(row["status"]) for row in ordered)
    groups: dict[tuple[str, str, str, str], Counter[str]] = defaultdict(Counter)
    for row in ordered:
        key = (
            str(row["phase"]), str(row["flow"]),
            str(row["upscale_backend"]), str(row["interp_backend"]),
        )
        groups[key][str(row["status"])] += 1

    summary_lines = [
        "# GPU 代表模型兼容性矩阵",
        "",
        f"- 计划用例：{total_cases}",
        f"- 已有结果：{len(ordered)}",
        f"- 通过：{status_counts.get('PASS', 0)}",
        f"- 资源不足跳过：{status_counts.get('SKIP_OOM', 0)}",
        f"- 失败/超时：{sum(count for status, count in status_counts.items() if status not in TERMINAL_STATUSES)}",
        "- 输入：默认 96×64、4 帧；固定输入 ONNX 模型及 GIMM 使用较大低分辨率夹具，均为 4 帧。",
        "",
        "## 流程通过性",
        "",
        "| 阶段 | 流程 | 超分后端 | 补帧后端 | 通过 | 跳过 | 失败 | 总数 |",
        "|---|---|---|---|---:|---:|---:|---:|",
    ]
    for key, counts in sorted(groups.items()):
        passed = counts.get("PASS", 0)
        skipped = counts.get("SKIP_OOM", 0)
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
    skipped = [row for row in ordered if row["status"] == "SKIP_OOM"]
    summary_lines.extend([
        "",
        "## 资源不足跳过项",
        "",
        "| ID | 流程 | 超分 | 补帧 | 原因 |",
        "|---|---|---|---|---|",
    ])
    for row in skipped:
        summary_lines.append(
            "| {case_id} | {flow} | {upscale_backend}/{upscale_category} | "
            "{interp_backend}/{interp_category} | {error} |".format(
                **{key: markdown_cell(value) for key, value in row.items()}
            )
        )
    if not skipped:
        summary_lines.append("| - | - | - | - | 暂无 |")
    summary_lines.extend([
        "",
        "## 失败项",
        "",
        "| ID | 流程 | 超分 | 补帧 | 状态 | 错误摘要 |",
        "|---|---|---|---|---|---|",
    ])
    for row in failures:
        summary_lines.append(
            "| {case_id} | {flow} | {upscale_backend}/{upscale_category} | "
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
        "| ID | 阶段 | 流程 | 超分后端/类别 | 补帧后端/类别 | 结果 | 秒 | 输出 | 帧数 |",
        "|---|---|---|---|---|---|---:|---|---:|",
    ]
    for row in ordered:
        detail_lines.append(
            f"| {row['case_id']} | {row['phase']} | {row['flow']} | "
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
        default=Path(r"C:\Program portable\3FUI\3FUI\Plugin\videoenhancer.exe"),
    )
    parser.add_argument(
        "--result-dir",
        type=Path,
        default=Path(__file__).resolve().parents[2] / "test-results" / "gpu-matrix",
    )
    parser.add_argument("--phase", choices=("all", "single", "same", "cross"), default="all")
    parser.add_argument("--backend-pair", help="只运行超分:补帧后端，例如 cuda:tensorrt")
    parser.add_argument("--case-id")
    parser.add_argument("--match", help="按用例 JSON 正则筛选")
    parser.add_argument("--max-cases", type=int, default=0)
    parser.add_argument("--timeout", type=int, default=420)
    parser.add_argument("--trt-timeout", type=int, default=2400)
    parser.add_argument("--jobs", type=int, default=1, help="并行运行的 GPU 用例数")
    parser.add_argument("--rerun-failed", action="store_true")
    parser.add_argument("--keep-failed-output", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--report-only", action="store_true")
    args = parser.parse_args()
    if args.jobs < 1:
        parser.error("--jobs 必须大于等于 1")

    exe = args.exe.resolve()
    core_root = exe.parent
    ffmpeg = core_root / "bin" / "ffmpeg" / "ffmpeg.exe"
    ffprobe = core_root / "bin" / "ffmpeg" / "ffprobe.exe"
    for required in (exe, ffmpeg, ffprobe):
        if not required.is_file():
            parser.error(f"文件不存在：{required}")

    args.result_dir.mkdir(parents=True, exist_ok=True)
    all_cases = generate_cases()
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

    validate_catalog(exe)
    counts = Counter(case.phase for case in all_cases)
    print(
        "矩阵计划："
        + ", ".join(f"{phase}={count}" for phase, count in sorted(counts.items()))
        + f", total={len(all_cases)}, selected={len(selected)}"
    )
    if args.dry_run:
        for case in selected:
            print(json.dumps(asdict(case), ensure_ascii=False))
        return 0

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
            f"[{index}/{len(pending)}] {case.case_id} {case.phase}/{case.flow} "
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

    with result_path.open("a", encoding="utf-8", buffering=1) as result_file:
        if args.jobs == 1:
            for index, case in enumerate(pending, 1):
                announce(index, case)
                record = execute_case(
                    case, exe, ffmpeg, ffprobe, args.result_dir,
                    args.timeout, args.trt_timeout, args.keep_failed_output,
                )
                persist(result_file, case, record)
        else:
            # GPU 推理由工作线程并行；JSONL 与报告始终由主线程串行写入。
            with ThreadPoolExecutor(max_workers=args.jobs) as executor:
                queued = list(enumerate(pending, 1))
                active = {}

                def submit_next() -> bool:
                    active_has_gimm = any(
                        active_case.interp_category == "GIMM"
                        for active_case in active.values()
                    )
                    eligible_position = next(
                        (
                            position for position, (_, queued_case) in enumerate(queued)
                            if queued_case.interp_category != "GIMM" or not active_has_gimm
                        ),
                        None,
                    )
                    if eligible_position is None:
                        return False
                    index, case = queued.pop(eligible_position)
                    announce(index, case)
                    future = executor.submit(
                        execute_case,
                        case, exe, ffmpeg, ffprobe, args.result_dir,
                        args.timeout, args.trt_timeout, args.keep_failed_output,
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
