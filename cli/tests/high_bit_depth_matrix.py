"""RGB48 候选实机矩阵；轻量并发、大型独占，支持恢复与OOM独占重跑。"""

from __future__ import annotations

import argparse
import base64
import csv
import hashlib
import itertools
import json
import math
import os
from pathlib import Path
import re
import subprocess
import sys
import time
import threading
from collections import Counter
from concurrent.futures import ThreadPoolExecutor, wait, FIRST_COMPLETED

import numpy as np

FIXTURE_LOCK = threading.Lock()


def write_json(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


def capture(command, **kwargs):
    return subprocess.run([str(x) for x in command], capture_output=True, check=True,
                          timeout=120, **kwargs).stdout


def model_size(model):
    width, height = 96, 64
    match = re.search(r"(240x320|320x448|480x320)", model["id"])
    if match:
        height, width = map(int, match[0].split("x"))
    if model.get("architectureGroup") == "GIMM":
        width, height = 320, 240
    multiple = max(1, model.get("inputMultiple", 1))
    return math.ceil(width / multiple) * multiple, math.ceil(height / multiple) * multiple


def plan(root):
    cats = {}
    for p in root.glob("catalog-*-*.json"):
        phase, backend = p.stem[len("catalog-"):].split("-", 1)
        cats[phase, backend] = json.loads(p.read_text(encoding="utf-8"))
    cases = []

    def add(layer, up=None, interp=None, precision="auto", flow="interp-first", **extra):
        ub, um = up if up else ("", {})
        ib, im = interp if interp else ("", {})
        sizes = [model_size(m) for m in (um, im) if m] or [(96, 64)]
        w, h = max(x[0] for x in sizes), max(x[1] for x in sizes)
        case = dict(layer=layer, ub=ub, up=um.get("id", ""), ib=ib,
                    interp=im.get("id", ""), scale=um.get("scale", 1),
                    precision=precision, flow=flow, width=w, height=h,
                    kind="video", tile=0, output_scale=0, **extra)
        case["id"] = hashlib.sha256(json.dumps(case, sort_keys=True).encode()).hexdigest()[:16]
        if case not in cases:
            cases.append(case)

    # 全目录单测：固定 ONNX 输入精度和 NCNN 精度限制由实际日志揭示，不伪装成强制成功。
    for (phase, backend), models in sorted(cats.items()):
        for model in models:
            for precision in ("float32", "float16"):
                add("all-models", (backend, model) if phase == "upscale" else None,
                    (backend, model) if phase == "interp" else None, precision)

    reps = {}
    for (phase, backend), models in sorted(cats.items()):
        grouped = {}
        for m in models:
            # 静态图、固定半精度图、GMFSS Base/Union 分别保留代表。
            variant = "static" if re.search(r"\d{3}x\d{3}", m["id"]) else "dynamic"
            if phase == "upscale" and backend == "tensorrt":
                variant += "-onnx" if m["id"].startswith("ONNX/") else "-pth"
            if "fp16" in m["id"].lower():
                variant += "-fp16"
            if m.get("architectureGroup") == "GMFSS":
                variant += "-base" if "Base" in m["id"] else "-union"
            if "heavy" in m["id"].lower():
                variant += "-heavy"
            key = (m.get("architectureGroup", m["architecture"]), variant)
            grouped.setdefault(key, m)
        reps[phase, backend] = list(grouped.values())
        for m in grouped.values():
            add("auto-architecture", (backend, m) if phase == "upscale" else None,
                (backend, m) if phase == "interp" else None)

    # 每一种架构超分搭配 CUDA RIFE；每一种补帧架构搭配 CUDA Compact，均覆盖两种顺序。
    compact = next(m for m in cats["upscale", "cuda"] if "G6i2a-Compact" in m["id"])
    rife = next(m for m in cats["interp", "cuda"] if m["id"] == "RIFE/rife4.6")
    for (phase, backend), models in reps.items():
        for model in models:
            for flow in ("interp-first", "upscale-first"):
                add("architecture-orders", (backend, model) if phase == "upscale" else ("cuda", compact),
                    ("cuda", rife) if phase == "upscale" else (backend, model), flow=flow)

    # 后端笛卡尔积配对、明确双精度与双顺序。
    backend_reps = {}
    for (phase, backend), models in cats.items():
        if phase == "upscale":
            backend_reps[backend] = next((m for m in models if "G6i2a-Compact" in m["id"]), models[0])
    for (ub, um), ib, precision, flow in itertools.product(
            backend_reps.items(), ("cuda", "tensorrt"), ("float32", "float16"),
            ("interp-first", "upscale-first")):
        im = next(m for m in cats["interp", ib] if m["id"] == "RIFE/rife4.6")
        add("backend-grid", (ub, um), (ib, im), precision, flow)

    for backend, m in backend_reps.items():
        for precision in ("float32", "float16", "auto"):
            add("image", (backend, m), precision=precision)
            cases[-1]["kind"] = "image"
            if backend in ("ncnn", "cuda", "onnx", "tensorrt"):
                add("tile32", (backend, m), precision=precision)
                cases[-1]["tile"] = 32
                add("target-scale", (backend, m), precision=precision)
                cases[-1]["output_scale"] = 1
    for precision in ("auto", "float32", "float16"):
        add("mixed-segments", ("cuda", compact), precision=precision)
        cases[-1]["kind"] = "segments"
    add("default-encoder", ("cuda", compact))
    cases[-1]["kind"] = "default"
    add("ncnn-interp-gate", interp=("ncnn", rife), expected_error="NCNN.*8bit|NCNN.*8.bit|NCNN.*高位深")
    # id 最后计算，覆盖图片/分段/分块等入口差异。
    for c in cases:
        c["id"] = hashlib.sha256(json.dumps({k:v for k,v in c.items() if k != "id"}, sort_keys=True).encode()).hexdigest()[:16]
    return cases, cats


def fixture(root, ffmpeg, w, h):
    path = root / "fixtures" / f"rgb48-{w}x{h}.mkv"
    image = path.with_suffix(".png")
    if path.exists() and image.exists():
        return path, image
    path.parent.mkdir(exist_ok=True)
    y, x = np.mgrid[:h, :w]
    frames = []
    for index in range(4):
        ramp = (x + y * w) / (w * h - 1)
        red = 4096 + ramp * 52000
        green = 6000 + (0.65 * x / w + 0.35 * y / h) * 50000
        blue = 8000 + (0.3 + 0.25 * np.sin(x / w * 12 + index / 3) + 0.2 * y / h) * 48000
        rgb = np.stack((red, green, blue), axis=-1).astype("<u2")
        rgb[h//3:h//2, w//4+index:w//2+index] = [54000, 16000, 26000]
        frames.append(rgb)
    capture([ffmpeg, "-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb48le",
             "-s", f"{w}x{h}", "-r", "4", "-i", "pipe:0", "-c:v", "ffv1", "-level", "3",
             "-pix_fmt", "gbrp16le", path], input=np.stack(frames).tobytes())
    capture([ffmpeg, "-v", "error", "-y", "-i", path, "-frames:v", "1", "-pix_fmt", "rgb48be", image])
    return path, image


def command(root, c, src, image, output, cats):
    runtime = root / "runtime"
    args = [str(runtime / "videoenhancer.exe")]
    if c["kind"] == "image":
        output.mkdir(exist_ok=True)
        args += ["--image-input", str(image), "--image-output", str(output), "--image-png"]
    else:
        args += ["-i", str(src)]
    if c["up"]:
        args += ["-backend", c["ub"], "-modelpath", c["up"]]
    else:
        args += ["-no-upscale", "-backend", c["ib"]]
    if c["interp"]:
        args += ["-interp-backend", c["ib"], "-interp-model", c["interp"],
                 "-interp-factor", "2", "-process-order", c["flow"]]
    args += ["-upscale-precision", c["precision"], "-interp-precision", c["precision"],
             "-scene-threshold", "4", "--ffmpeg-path", str(runtime / "bin/ffmpeg/ffmpeg.exe"),
             "--ffprobe-path", str(runtime / "bin/ffmpeg/ffprobe.exe")]
    if c["tile"]:
        args += ["-tile-size", str(c["tile"])]
    if c["output_scale"]:
        args += ["-output-scale", str(c["output_scale"])]
    if c["kind"] == "segments":
        specs = []
        for start, end, backend in ((1, 2, "cuda"), (3, 4, "onnx")):
            m = next(m for m in cats["upscale", backend] if "G6i2a-Compact" in m["id"])
            specs.append(dict(Start=start, End=end, Backend=backend, Model=m["id"],
                              TargetWidth=c["width"]*2, TargetHeight=c["height"]*2))
        args += ["--allow-mixed-segment-backends", "--segments-base64", base64.b64encode(json.dumps(specs).encode()).decode()]
    if c["kind"] != "image":
        settings = f'"{output}" -y' if c["kind"] == "default" else f'-c:v ffv1 -level 3 -pix_fmt:v gbrp16le "{output}" -y'
        args += ["-ffmpeg-settings", settings]
    return args


def probe(root, path, c):
    ffmpeg = root / "runtime/bin/ffmpeg/ffmpeg.exe"
    ffprobe = ffmpeg.with_name("ffprobe.exe")
    meta = json.loads(capture([ffprobe, "-v", "error", "-select_streams", "v:0", "-count_frames",
        "-show_entries", "stream=width,height,pix_fmt,nb_read_frames,avg_frame_rate,codec_name", "-of", "json", path]))["streams"][0]
    data = capture([ffmpeg, "-v", "error", "-i", path, "-pix_fmt", "rgb48le", "-f", "rawvideo", "pipe:1"])
    pixels = np.frombuffer(data, "<u2").reshape(-1, meta["height"], meta["width"], 3)
    meta["levels"] = [int(np.unique(f[..., 0]).size) for f in pixels]
    meta["range"] = [int(np.ptp(f.astype(np.int32))) for f in pixels]
    meta["clipped_fraction"] = [float(np.mean((f == 0) | (f == 65535))) for f in pixels]
    expected_frames = 1 if c["kind"] == "image" else 7 if c["interp"] else 4
    scale = c["output_scale"] or c["scale"]
    if meta["width"] != c["width"] * scale or meta["height"] != c["height"] * scale:
        return "FAIL_DIMENSIONS", meta
    if len(pixels) != expected_frames:
        return "FAIL_FRAMES", meta
    formats = ("yuv420p10le",) if c["kind"] == "default" else ("gbrp16le", "rgb48be", "rgb48le", "rgba64be", "rgba64le")
    if meta["pix_fmt"] not in formats:
        return "FAIL_BITDEPTH", meta
    if min(meta["levels"]) <= 256 or min(meta["range"]) < 1024 or max(meta["clipped_fraction"]) > 0.90:
        return "FAIL_CONTENT", meta
    return "PASS", meta


def execute(root, c, cats, timeout, trt_timeout, attempt=""):
    ffmpeg = root / "runtime/bin/ffmpeg/ffmpeg.exe"
    with FIXTURE_LOCK:
        source, image = fixture(root, ffmpeg, c["width"], c["height"])
    stem = c["id"] + ("-" + attempt if attempt else "")
    out = root / "outputs" / (stem + ("" if c["kind"] == "image" else ".mkv"))
    args = command(root, c, source, image, out, cats)
    log = root / "logs" / (stem + ".log")
    started = time.monotonic()
    limit = trt_timeout if "tensorrt" in (c["ub"], c["ib"]) else timeout
    with log.open("w", encoding="utf-8") as stream:
        stream.write(subprocess.list2cmdline(args) + "\n")
        stream.flush()
        process = subprocess.Popen(args, stdout=stream, stderr=subprocess.STDOUT)
        try:
            code = process.wait(timeout=limit)
        except subprocess.TimeoutExpired:
            # CLI 会产生 Python/FFmpeg 子进程，超时后必须一并结束，避免与下一例争用 GPU。
            subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"], capture_output=True)
            process.wait()
            code = None
    text = log.read_text(encoding="utf-8-sig", errors="replace")
    record = {**c, "command": args, "exit_code": code, "seconds": round(time.monotonic()-started, 2),
              "finished_at": time.strftime("%Y-%m-%d %H:%M:%S"), "log": str(log)}
    status = "FAIL_EXIT"
    if "expected_error" in c:
        status = "PASS_GATE" if code and re.search(c["expected_error"], text, re.I) else "FAIL_GATE"
    elif code is None:
        status = "TIMEOUT"
    elif code:
        status = "RESOURCE_OOM" if re.search(r"out of memory|OutOfMemoryError|内存不足|显存不足|Error Code 2:.*Memory", text, re.I) else "FAIL_EXIT"
    else:
        try:
            actual = next(out.rglob("*.png")) if c["kind"] == "image" else out
            status, record["probe"] = probe(root, actual, c)
        except Exception as exc:
            status, record["probe_error"] = "FAIL_PROBE", str(exc)
    record["device_evidence"] = sorted(set(re.findall(
        r"[^\n]*(?:Using Device:|Loaded ONNX model with|Loaded prebuilt TensorRT|NVIDIA GeForce|vulkan|Vulkan)[^\n]*", text)))[:15]
    record["precision_evidence"] = sorted(set(re.findall(
        r"[^\n]*(?:precision|Precision|float16|float32|FP16|FP32)[^\n]*", text)))[-25:]
    if status == "PASS" and "Loaded ONNX model with CPUExecutionProvider" in text:
        status = "DEGRADED_CPU"
    if status == "PASS" and re.search(r"invalid value encountered|NaN|non.finite", text, re.I):
        status = "FAIL_NONFINITE_WARNING"
    record["status"] = status
    record["error"] = " | ".join([x.strip() for x in text.splitlines() if re.search(
        r"error|exception|traceback|失败|错误|fatal|out of memory", x, re.I)][-5:])[-1800:]
    return record


def report(root, cases, records):
    rows = [records[c["id"]] for c in cases if c["id"] in records]
    fields = ["id", "layer", "ub", "up", "ib", "interp", "precision", "flow", "kind", "tile", "output_scale", "status", "seconds", "execution_mode", "configured_jobs", "retry_reason", "error"]
    with (root / "results.csv").open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fields, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(rows)
    stats = Counter(r["status"] for r in rows)
    lines = ["# RGB48 扩大可用性矩阵", "", f"已完成 {len(rows)} / {len(cases)}；状态：{dict(stats)}", "",
             "FP16/FP32 是请求值；固定 ONNX/Engine 输入类型、自动回退以每例 precision_evidence 与日志为准。",
             "输出16bit、超过256级只能排查明显8bit截断/坏帧，不能证明模型拥有16bit有效精度或全部算子均在GPU。",
             "4输入帧的2倍补帧按现有端点约定验收7帧，不作为时长/画质/长片性能验收。", "",
             "| 层 | 完成/计划 | 结果 |", "|---|---:|---|"]
    for layer in dict.fromkeys(c["layer"] for c in cases):
        result = [r for r in rows if r["layer"] == layer]
        lines.append(f"| {layer} | {len(result)}/{sum(c['layer']==layer for c in cases)} | {dict(Counter(r['status'] for r in result))} |")
    lines += ["", "## 未通过用例", "", "| ID | 后端/模型 | 精度/顺序 | 状态 |", "|---|---|---|---|"]
    for r in rows:
        if r["status"] not in ("PASS", "PASS_GATE"):
            lines.append(f"| {r['id']} | {r['ub']} {r['up']} + {r['ib']} {r['interp']} | {r['precision']} / {r['flow']} | {r['status']} |")
    (root / "results.md").write_text("\n".join(lines)+"\n", encoding="utf-8")
    write_json(root / "summary.json", dict(planned=len(cases), completed=len(rows), statuses=dict(stats)))


def needs_exclusive(case, cats):
    if case["ub"] in ("flashvsr", "basicvsrpp") or case["width"] * case["height"] > 128 * 96:
        return True
    for phase, backend, name in (("upscale", case["ub"], case["up"]),
                                  ("interp", case["ib"], case["interp"])):
        model = next((m for m in cats.get((phase, backend), []) if m["id"] == name), {})
        if model.get("architectureGroup") in ("HAT", "DAT", "GRL", "SwinIR", "GIMM"):
            return True
    return False


def cache_keys(case):
    keys = set()
    precision = "float32" if case["precision"] == "auto" else case["precision"]
    if case["ub"] == "tensorrt":
        keys.add(("up", case["up"], precision, case["width"], case["height"], case["tile"], case["output_scale"]))
    if case["ib"] == "tensorrt":
        scale = (case["output_scale"] or case["scale"]) if case["flow"] == "upscale-first" else 1
        keys.add(("interp", case["interp"], precision, case["width"]*scale, case["height"]*scale))
    return keys


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--plan-only", action="store_true")
    parser.add_argument("--layer")
    parser.add_argument("--match", default="")
    parser.add_argument("--limit", type=int, default=0)
    parser.add_argument("--rerun-failed", action="store_true")
    parser.add_argument("--timeout", type=int, default=360)
    parser.add_argument("--trt-timeout", type=int, default=1200)
    parser.add_argument("--jobs", type=int, choices=(1, 2, 3, 4, 5), default=1)
    options = parser.parse_args()
    root = options.root.resolve()
    os.environ["PYTHONDONTWRITEBYTECODE"] = "1"
    os.environ["PYTHONIOENCODING"] = "utf-8"
    for name in ("TEMP", "TMP", "TORCH_HOME", "TORCHINDUCTOR_CACHE_DIR", "CUDA_CACHE_PATH"):
        folder = root / "cache" / name.lower()
        folder.mkdir(parents=True, exist_ok=True)
        os.environ[name] = str(folder)
    for name in ("logs", "outputs", "fixtures"):
        (root / name).mkdir(exist_ok=True)
    cases, cats = plan(root)
    fingerprint = hashlib.sha256((root / "runtime/videoenhancer.exe").read_bytes()).hexdigest()
    expected = json.loads((root / "candidate.json").read_text(encoding="utf-8"))["exe_sha256"]
    if fingerprint != expected:
        raise RuntimeError("候选EXE已改变，须另建测试目录，不能混入旧矩阵")
    write_json(root / "manifest.json", cases)
    records = {}
    result_path = root / "results.jsonl"
    if result_path.exists():
        for line in result_path.read_text(encoding="utf-8").splitlines():
            value = json.loads(line)
            records[value["id"]] = value
    report(root, cases, records)
    print("PLAN", len(cases), dict(Counter(c["layer"] for c in cases)), flush=True)
    if options.plan_only:
        return
    selected = [c for c in cases if (not options.layer or c["layer"] == options.layer)
                and options.match in json.dumps(c, ensure_ascii=False)
                and (c["id"] not in records or options.rerun_failed and records[c["id"]]["status"] not in ("PASS", "PASS_GATE"))]
    if options.limit:
        selected = selected[:options.limit]
    if options.jobs > 1:
        layers = {name: index for index, name in enumerate(dict.fromkeys(c["layer"] for c in cases))}
        selected.sort(key=lambda c: (layers[c["layer"]], needs_exclusive(c, cats)))
    serial_retries = []
    finished = 0

    def save(result, mode, retry=False):
        nonlocal finished
        result["execution_mode"] = mode
        result["configured_jobs"] = options.jobs
        if retry:
            result["retry_reason"] = "并发发生OOM，独占GPU重新核验"
        if result["status"] == "RESOURCE_OOM" and mode == "parallel-light":
            serial_retries.append(next(c for c in selected if c["id"] == result["id"]))
            result["serial_retry_pending"] = True
        with result_path.open("a", encoding="utf-8") as stream:
            stream.write(json.dumps(result, ensure_ascii=False) + "\n")
        records[result["id"]] = result
        report(root, cases, records)
        finished += 1
        print("RESULT", finished, result["id"], result["status"], result["seconds"], mode, flush=True)

    def start(case, mode):
        print("START", case["id"], case["ub"], case["up"], case["ib"], case["interp"], case["precision"], case["flow"], mode, flush=True)

    index, pending = 0, {}
    with ThreadPoolExecutor(max_workers=options.jobs) as pool:
        while index < len(selected) or pending or serial_retries:
            next_exclusive = index < len(selected) and needs_exclusive(selected[index], cats)
            occupied = set().union(*(cache_keys(case) for case, mode in pending.values())) if pending else set()
            cache_conflict = index < len(selected) and bool(cache_keys(selected[index]) & occupied)
            # OOM重试与大型案例先等待其他任务结束，保证它们独占GPU。
            if pending and (serial_retries or next_exclusive or cache_conflict or len(pending) >= options.jobs or index == len(selected)):
                done, _ = wait(pending, return_when=FIRST_COMPLETED)
                for future in done:
                    case, mode = pending.pop(future)
                    save(future.result(), mode)
                continue
            if serial_retries:
                case = serial_retries.pop(0)
                start(case, "exclusive-retry")
                save(execute(root, case, cats, options.timeout, options.trt_timeout, "serial-retry"), "exclusive-retry", True)
                continue
            case = selected[index]
            index += 1
            if next_exclusive or options.jobs == 1:
                start(case, "exclusive")
                save(execute(root, case, cats, options.timeout, options.trt_timeout), "exclusive")
            else:
                start(case, "parallel-light")
                pending[pool.submit(execute, root, case, cats, options.timeout, options.trt_timeout)] = (case, "parallel-light")
    print("FINISHED", json.loads((root / "summary.json").read_text(encoding="utf-8")), flush=True)


if __name__ == "__main__":
    main()
