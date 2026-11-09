# SPDX-License-Identifier: AGPL-3.0-only
# Copyright (c) 2026 VideoEnhancer contributors
# 本脚本与 RVE 结合运行；完整许可见 ../third-party/RVE/AGPL-3.0.txt。
"""Frame-segmented single-frame super-resolution backend."""

from __future__ import annotations

import argparse
import base64
import gc
import importlib.util
import json
import mmap
import os
import subprocess
import sys
import time
from fractions import Fraction
from pathlib import Path


def decode_json(value: str):
    return json.loads(base64.b64decode(value.encode("ascii")).decode("utf-8"))


MODEL_BACKENDS = {"ncnn", "cuda", "tensorrt", "onnx"}


def validate_segments(segments: list[dict], total_frames: int) -> tuple[int, int]:
    if not segments:
        raise ValueError("分段配置为空")
    expected_start = 1
    output_width = 0
    output_height = 0
    for index, segment in enumerate(segments, 1):
        start = int(segment.get("start", 0))
        end = int(segment.get("end", 0))
        backend = str(segment.get("backend", "")).strip().lower()
        model = str(segment.get("model", "")).strip()
        scale = int(segment.get("scale", 0))
        current_width = int(segment.get("outputWidth", 0))
        current_height = int(segment.get("outputHeight", 0))
        if start != expected_start or end < start:
            raise ValueError(
                f"第 {index} 段必须从第 {expected_start} 帧开始，当前为 {start}-{end}"
            )
        if backend not in MODEL_BACKENDS:
            raise ValueError(f"Python 分段模型后端不支持：{backend}")
        if not model:
            raise ValueError(f"第 {index} 段没有处理方式")
        if scale < 1:
            raise ValueError(f"第 {index} 段模型倍率无效")
        if current_width <= 0 or current_height <= 0:
            raise ValueError(f"第 {index} 段输出分辨率无效")
        if output_width == 0:
            output_width, output_height = current_width, current_height
        elif current_width != output_width or current_height != output_height:
            raise ValueError(
                f"第 {index} 段输出分辨率 {current_width}x{current_height} "
                f"与全片 {output_width}x{output_height} 不一致"
            )
        expected_start = end + 1
    if int(segments[-1]["end"]) != total_frames:
        raise ValueError(
            f"分段必须覆盖全部 {total_frames} 帧，当前最后一帧为 {segments[-1]['end']}"
        )
    return output_width, output_height


def probe_video(ffprobe: Path, source: Path) -> tuple[int, int, int, str]:
    command = [
        str(ffprobe), "-v", "error", "-select_streams", "v:0", "-count_frames",
        "-show_entries", "stream=width,height,nb_read_frames,nb_frames,avg_frame_rate,r_frame_rate",
        "-of", "json", str(source),
    ]
    completed = subprocess.run(command, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if completed.returncode != 0:
        raise RuntimeError(f"FFprobe 探测失败（退出码 {completed.returncode}）：{completed.stderr.strip()}")
    streams = json.loads(completed.stdout).get("streams", [])
    if not streams:
        raise RuntimeError("输入文件没有视频流")
    stream = streams[0]
    width, height = int(stream["width"]), int(stream["height"])
    total = int(stream.get("nb_read_frames") or stream.get("nb_frames") or 0)
    rate = stream.get("avg_frame_rate") or stream.get("r_frame_rate") or "0/1"
    if width <= 0 or height <= 0 or total <= 0 or Fraction(rate) <= 0:
        raise RuntimeError("无法取得有效的视频尺寸、帧数或帧率")
    return width, height, total, rate


def read_exact(stream, size: int) -> bytes:
    chunks = bytearray()
    while len(chunks) < size:
        value = stream.read(size - len(chunks))
        if not value:
            break
        chunks.extend(value)
    return bytes(chunks)


def load_image_backend(backend_root: Path):
    path = backend_root / "rve-image-backend.py"
    spec = importlib.util.spec_from_file_location("videoenhancer_image_backend", path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"无法加载图片后端：{path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module.ImageUpscaler


def open_pause_map(name: str):
    if not name or os.name != "nt":
        return None
    try:
        return mmap.mmap(-1, 1, tagname=name)
    except (OSError, ValueError):
        return None


def wait_while_paused(mapping) -> None:
    while mapping is not None:
        mapping.seek(0)
        if mapping.read_byte() != 1:
            return
        time.sleep(0.1)


def release_model(model) -> None:
    del model
    gc.collect()
    try:
        import torch
        if torch.cuda.is_available():
            torch.cuda.empty_cache()
    except ImportError:
        pass


def main() -> int:
    parser = argparse.ArgumentParser(description="Segmented single-frame video super-resolution")
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--segments-base64", required=True)
    parser.add_argument("--encoder-args-base64", required=True)
    parser.add_argument("--ffmpeg-path", required=True)
    parser.add_argument("--tile-size", type=int, default=0)
    parser.add_argument("--pause-shm", default="")
    parser.add_argument("--overwrite", action="store_true")
    args = parser.parse_args()

    source, output = Path(args.input).resolve(), Path(args.output).resolve()
    ffmpeg = Path(args.ffmpeg_path).resolve()
    ffprobe = ffmpeg.with_name("ffprobe.exe")
    backend_root = Path(os.environ.get("VIDEOENHANCER_BACKEND_DIR", Path(__file__).resolve().parent)).resolve()
    sys.path.insert(0, str(backend_root))
    from src.utils.PixelFormat import high_depth_encoder_args
    segments = decode_json(args.segments_base64)
    encoder_args = high_depth_encoder_args([str(value) for value in decode_json(args.encoder_args_base64)], str(ffmpeg))
    width, height, total_frames, frame_rate = probe_video(ffprobe, source)
    output_width, output_height = validate_segments(segments, total_frames)
    ImageUpscaler = None
    np = None

    reader_command = [
        str(ffmpeg), "-hide_banner", "-loglevel", "error", "-i", str(source),
        "-map", "0:v:0", "-fps_mode", "passthrough", "-f", "rawvideo", "-pix_fmt", "rgb48le", "pipe:1",
    ]
    writer_command = [
        str(ffmpeg), "-hide_banner", "-loglevel", "error", "-y" if args.overwrite else "-n",
        "-f", "rawvideo", "-pix_fmt", "rgb48le", "-s:v", f"{output_width}x{output_height}",
        "-r", frame_rate, "-i", "pipe:0", "-i", str(source),
        "-map", "0:v:0", "-map", "1:a?", "-map", "1:s?",
        *encoder_args, str(output),
    ]
    print(f"Total Output Frames: {total_frames}", flush=True)
    print(
        f"SEGMENTED_INFO|mixed|{len(segments)}|"
        f"{width}x{height}|{output_width}x{output_height}",
        flush=True,
    )

    reader = subprocess.Popen(reader_command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, cwd=backend_root)
    writer = subprocess.Popen(writer_command, stdin=subprocess.PIPE, stderr=subprocess.PIPE, cwd=backend_root)
    pause_map = open_pause_map(args.pause_shm)
    current_model = None
    current_processor_key = None
    current_processor_started_at = None
    current_processor_start_frame = 0
    current_processor_backend = ""
    segment_index = 0
    input_size = width * height * 6

    def report_processor_done(last_frame: int) -> None:
        nonlocal current_processor_started_at
        if current_processor_started_at is None or current_processor_start_frame <= 0:
            return
        elapsed = max(0.000001, time.perf_counter() - current_processor_started_at)
        frame_count = max(0, last_frame - current_processor_start_frame + 1)
        print(
            f"SEGMENTED_PROCESSOR_DONE|{current_processor_backend}|"
            f"{current_processor_start_frame}|{last_frame}|{frame_count}|"
            f"{elapsed:.3f}|{frame_count / elapsed:.2f}",
            flush=True,
        )
        current_processor_started_at = None
    try:
        assert reader.stdout is not None and writer.stdin is not None
        for frame_number in range(1, total_frames + 1):
            wait_while_paused(pause_map)
            payload = read_exact(reader.stdout, input_size)
            if len(payload) != input_size:
                raise RuntimeError(f"读取第 {frame_number} 帧失败：收到 {len(payload)} / {input_size} 字节")
            while frame_number > int(segments[segment_index]["end"]):
                segment_index += 1
            segment = segments[segment_index]
            backend = str(segment["backend"]).strip().lower()
            model_path = str(segment["model"])
            multiple = max(1, int(segment.get("inputMultiple", 1)))
            processor_key = (backend, model_path, multiple)
            if processor_key != current_processor_key:
                if current_model is not None:
                    report_processor_done(frame_number - 1)
                    previous_model, current_model = current_model, None
                    release_model(previous_model)
                print(
                    f"SEGMENTED_MODEL|{segment_index + 1}|{segment['start']}|"
                    f"{segment['end']}|{backend}|{model_path}",
                    flush=True,
                )
                os.environ["VIDEOENHANCER_UPSCALE_INPUT_MULTIPLE"] = str(multiple)
                os.environ["VIDEOENHANCER_ONNX_INPUT_MULTIPLE"] = str(multiple)
                if ImageUpscaler is None:
                    ImageUpscaler = load_image_backend(backend_root)
                if np is None:
                    import numpy as numpy_module
                    np = numpy_module
                current_model = ImageUpscaler(
                    backend, Path(model_path), width, height,
                    tile_size=max(0, args.tile_size) if backend == "ncnn" else 0,
                    use_rve_ncnn=backend == "ncnn",
                )
                current_processor_key = processor_key
                current_processor_started_at = time.perf_counter()
                current_processor_start_frame = frame_number
                current_processor_backend = backend
            if current_model is None:
                raise RuntimeError(f"第 {frame_number} 帧没有可用的模型处理器")
            if backend == "ncnn" and getattr(current_model, "use_rve_ncnn", False):
                output_payload = current_model.process_bytes(payload)
                expected_size = output_width * output_height * 6
                if len(output_payload) != expected_size:
                    raise RuntimeError(
                        f"第 {frame_number} 帧 NCNN 输出字节数异常："
                        f"{len(output_payload)}，预期 {expected_size}"
                    )
            else:
                frame = np.frombuffer(payload, dtype=np.uint16).reshape(height, width, 3)
                result = current_model(frame)
                expected_shape = (output_height, output_width, 3)
                if result.shape != expected_shape:
                    raise RuntimeError(
                        f"第 {frame_number} 帧输出尺寸异常：{result.shape}，预期 {expected_shape}"
                    )
                output_payload = np.ascontiguousarray(result).tobytes()
            writer.stdin.write(output_payload)
            print(f"FPS: 0 Current Frame: {frame_number} ETA: 0:00:00", flush=True)
        if current_model is not None:
            report_processor_done(total_frames)
        writer.stdin.close()
        reader_code = reader.wait()
        writer_code = writer.wait()
        reader_error = (reader.stderr.read() if reader.stderr else b"").decode("utf-8", "replace").strip()
        writer_error = (writer.stderr.read() if writer.stderr else b"").decode("utf-8", "replace").strip()
        if reader_code != 0:
            raise RuntimeError(f"FFmpeg 解码失败（退出码 {reader_code}）：{reader_error}")
        if writer_code != 0:
            raise RuntimeError(f"FFmpeg 编码失败（退出码 {writer_code}）：{writer_error}")
        print("SEGMENTED_COMPLETE", flush=True)
        return 0
    except BaseException:
        try:
            reader.kill()
        except OSError:
            pass
        try:
            writer.kill()
        except OSError:
            pass
        raise
    finally:
        if current_model is not None:
            previous_model, current_model = current_model, None
            release_model(previous_model)
        if pause_map is not None:
            pause_map.close()


if __name__ == "__main__":
    raise SystemExit(main())
