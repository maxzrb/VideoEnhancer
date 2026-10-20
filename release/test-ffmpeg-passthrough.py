"""对照真实 FFmpeg 验证宿主工具命令转发，不使用视频文件或模型权重。"""
import argparse
import os
import re
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument("--runtime", required=True)
parser.add_argument("--ffmpeg", required=True)
options = parser.parse_args()
env = dict(os.environ, VIDEOENHANCER_FFMPEG=os.path.abspath(options.ffmpeg))


def run(executable, args, stdin=None):
    return subprocess.run([executable, *args], input=stdin, capture_output=True, env=env, timeout=30)


# 能力查询必须逐字节相同，否则宿主可能误判滤镜不可用。
filters = ["-hide_banner", "-filters"]
direct = run(options.ffmpeg, filters)
forwarded = run(options.runtime, filters)
assert direct.returncode == forwarded.returncode == 0
assert (direct.stdout, direct.stderr) == (forwarded.stdout, forwarded.stderr)
print("PASS: native filter query")

# 两输入指标评测验证多组 -i、过滤器引号与输出日志均未被增强参数解析器处理。
for metric in ("psnr", "ssim"):
    args = ["-hide_banner", "-f", "lavfi", "-i", "testsrc2=size=64x64:rate=1",
            "-f", "lavfi", "-i", "testsrc2=size=64x64:rate=1",
            "-lavfi", "[0:v][1:v]" + metric, "-frames:v", "1", "-f", "null", "-"]
    result = run(options.runtime, args)
    assert result.returncode == 0, result.stderr.decode("utf-8", errors="replace")
    expected = b"average:inf" if metric == "psnr" else b"All:1.000000"
    assert expected in result.stderr, result.stderr
    print("PASS: native " + metric)

# 标准输入、二进制标准输出与非零退出码必须原样经过中转器。
args = ["-hide_banner", "-loglevel", "error", "-f", "rawvideo", "-pixel_format", "rgb24",
        "-video_size", "2x2", "-i", "pipe:0", "-frames:v", "1", "-f", "rawvideo", "pipe:1"]
frame = bytes(range(12))
result = run(options.runtime, args, frame)
assert result.returncode == 0 and result.stdout == frame, (result.returncode, result.stderr)
print("PASS: binary stdin/stdout")
args = ["-hide_banner", "-videoenhancer_invalid_test_option"]
direct, forwarded = run(options.ffmpeg, args), run(options.runtime, args)
assert direct.returncode == forwarded.returncode != 0
assert (direct.stdout, direct.stderr) == (forwarded.stdout, forwarded.stderr)
print("PASS: stderr and failure exit code")
assert re.fullmatch(rb"[0-9]+\.[0-9]+\.[0-9]+\s*", run(options.runtime, ["--version"]).stdout)
result = run(options.runtime, ["-i", "unused", "-ffmpeg-settings", "-f null -", "-tile-size", "bad"])
assert result.returncode != 0 and "tile-size" in result.stderr.decode("utf-8", errors="replace")
print("PASS: plugin command routing")
