"""验证并发调度的独占屏障和OOM重试，不加载GPU模型。"""

import contextlib
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import sys
import tempfile
import threading
import time
import unittest
from unittest.mock import patch


SPEC = importlib.util.spec_from_file_location("hbd_matrix", Path(__file__).with_name("high_bit_depth_matrix.py"))
MATRIX = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MATRIX)


class SchedulerTests(unittest.TestCase):
    def test_five_workers_drain_before_heavy_case_and_oom_retry(self):
        cases = []
        for index in range(8):
            cases.append(dict(id=str(index), layer="probe", ub="flashvsr" if index == 6 else "cuda",
                              up="probe", ib="", interp="", scale=2, precision="float32",
                              flow="interp-first", width=96, height=64, kind="video", tile=0,
                              output_scale=0))
        lock = threading.Lock()
        active, peak, attempts = 0, 0, {}

        def execute(root, case, cats, timeout, trt_timeout, attempt=""):
            nonlocal active, peak
            with lock:
                active += 1
                peak = max(peak, active)
                attempts[case["id"]] = attempts.get(case["id"], 0) + 1
                if case["ub"] == "flashvsr" or attempt:
                    self.assertEqual(active, 1)
            time.sleep(0.04)
            status = "RESOURCE_OOM" if case["id"] == "0" and not attempt else "PASS"
            log = root / "logs" / (case["id"] + attempt + ".log")
            log.write_text(status, encoding="utf-8")
            with lock:
                active -= 1
            return {**case, "status": status, "seconds": 0.04, "error": "", "log": str(log)}

        artifacts = Path(__file__).resolve().parents[2] / "Artifacts"
        artifacts.mkdir(exist_ok=True)
        with tempfile.TemporaryDirectory(dir=artifacts, prefix="matrix-scheduler-") as folder:
            root = Path(folder)
            (root / "runtime").mkdir()
            (root / "runtime/videoenhancer.exe").write_bytes(b"scheduler-probe")
            MATRIX.write_json(root / "candidate.json", {"exe_sha256": hashlib.sha256(b"scheduler-probe").hexdigest()})
            args = ["matrix", "--root", str(root), "--jobs", "5"]
            with patch.object(sys, "argv", args), patch.object(MATRIX, "plan", return_value=(cases, {})), \
                 patch.object(MATRIX, "execute", side_effect=execute), contextlib.redirect_stdout(io.StringIO()):
                MATRIX.main()
            rows = [json.loads(line) for line in (root / "results.jsonl").read_text(encoding="utf-8").splitlines()]
            self.assertEqual(peak, 5)
            self.assertEqual(len(rows), 9)
            self.assertEqual(attempts["0"], 2)
            first, retry = [row for row in rows if row["id"] == "0"]
            self.assertTrue(first["serial_retry_pending"])
            self.assertEqual(retry["execution_mode"], "exclusive-retry")
            self.assertEqual(retry["status"], "PASS")
            self.assertNotEqual(first["log"], retry["log"])
            self.assertEqual(Path(first["log"]).read_text(encoding="utf-8"), "RESOURCE_OOM")
            self.assertEqual(json.loads((root / "summary.json").read_text(encoding="utf-8"))["statuses"], {"PASS": 8})


if __name__ == "__main__":
    unittest.main()
