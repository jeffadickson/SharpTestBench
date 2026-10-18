#!/usr/bin/env python3
"""Measure matched Icarus execution with/without a standalone direct-VPI probe.

This measures process wall time, not GPI-vs-VPI or C# scheduler performance.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import statistics
import subprocess
import tempfile
import time

ROOT = Path(__file__).resolve().parents[1]

def invoke(command, *, cwd=None):
    start = time.perf_counter_ns()
    result = subprocess.run(command, cwd=cwd, text=True, capture_output=True, check=False)
    elapsed = (time.perf_counter_ns() - start) / 1e9
    if result.returncode:
        raise RuntimeError(f"Command failed ({result.returncode}): {command}\n{result.stdout}\n{result.stderr}")
    return elapsed, result.stdout, result.stderr

def main():
    p = argparse.ArgumentParser()
    p.add_argument("--cycles", type=int, default=20000)
    p.add_argument("--repeats", type=int, default=7)
    p.add_argument("--warmups", type=int, default=2)
    p.add_argument("--output", type=Path, default=ROOT / "build/independent-vpi/benchmark.json")
    a = p.parse_args()
    if not 10 <= a.cycles <= 1000000 or not 3 <= a.repeats <= 30 or not 0 <= a.warmups <= 10:
        p.error("cycles must be 10..1000000, repeats 3..30, warmups 0..10")
    fixture = (ROOT / "tests/rtl/independent_vpi_counter.sv").read_text()
    if fixture.count("repeat (6)") != 1:
        raise RuntimeError("Expected exact counter fixture anchor for reproducible cycle count")
    fixture = fixture.replace("repeat (6)", f"repeat ({a.cycles})")
    with tempfile.TemporaryDirectory(prefix="stb-vpi-bench-") as temp:
        work = Path(temp)
        source = work / "top.sv"
        source.write_text(fixture)
        module = work / "top.vvp"
        probe_src = ROOT / "native/independent/icarus_probe.cpp"
        _, build_stdout, build_stderr = invoke(
            ["iverilog-vpi", str(probe_src)], cwd=work)
        _, compile_stdout, compile_stderr = invoke(
            ["iverilog", "-g2012", "-s", "top", "-o", str(module),
             str(ROOT / "examples/Counter/counter.sv"), str(source)], cwd=work)
        probe = work / "icarus_probe.vpi"
        if not probe.exists():
            raise RuntimeError("Expected icarus_probe.vpi not produced")
        commands = {
            "hdl_only": ["vvp", str(module)],
            "direct_vpi": ["vvp", "-M", str(work), "-m", "icarus_probe", str(module)]
        }
        samples = {k: [] for k in commands}
        outputs = {k: [] for k in commands}
        for i in range(a.warmups + a.repeats):
            # Alternate order to reduce drift; always run identical compiled design.
            order = list(commands)
            if i % 2: order.reverse()
            for key in order:
                elapsed, stdout, stderr = invoke(commands[key], cwd=work)
                if key == "direct_vpi":
                    if len(re.findall(r"^STB_VPI_PASS ", stdout, re.MULTILINE)) != 1 or "STB_VPI_FAIL" in stdout:
                        raise RuntimeError(f"Probe incorrect: {stdout}\n{stderr}")
                elif "STB_VPI_" in stdout:
                    raise RuntimeError("Baseline unexpectedly loaded VPI probe")
                if i >= a.warmups:
                    samples[key].append(elapsed)
                    outputs[key].append(hashlib.sha256(stdout.encode()).hexdigest())
        median = {key: statistics.median(values) for key, values in samples.items()}
        report = {
            "schemaVersion": 1, "scope": "Icarus process wall time; HDL-only vs direct VPI callback probe",
            "notComparableTo": ["cocotb GPI runtime", "managed SharpTestBench scheduler"],
            "cycles": a.cycles, "repeats": a.repeats, "warmups": a.warmups,
            "toolVersions": {
                tool: invoke([tool, flag])[1].strip().splitlines()[:2]
                for tool, flag in [("iverilog", "-V"), ("vvp", "-V")]
            },
            "sourceSha256": hashlib.sha256(source.read_bytes()).hexdigest(),
            "probeSha256": hashlib.sha256(probe_src.read_bytes()).hexdigest(),
            "samplesSeconds": samples, "medianSeconds": median,
            "directVpiToHdlOnlyRatio": median["direct_vpi"] / median["hdl_only"],
            "outputHashes": outputs
        }
        a.output.parent.mkdir(parents=True, exist_ok=True)
        a.output.write_text(json.dumps(report, indent=2) + "\n")
        print(f"HDL-only median: {median['hdl_only']:.6f}s")
        print(f"Direct-VPI median: {median['direct_vpi']:.6f}s")
        print(f"Direct-VPI/HDL-only ratio: {report['directVpiToHdlOnlyRatio']:.3f}x")
        print(a.output)

if __name__ == "__main__":
    main()
