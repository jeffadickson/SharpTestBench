#!/usr/bin/env python3
"""Matched whole-C#-framework DSP verification benchmark, existing GPI bridge.

Compares optimized direct-VPI fast paths with their disabled legacy GPI path.
It is NOT a cocotb-free adapter benchmark until native bootstrap/callbacks are replaced.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import statistics
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--sim", choices=("icarus", "verilator"), required=True)
    parser.add_argument("--cycles", type=int, default=8192)
    parser.add_argument("--repeat", type=int, default=5)
    parser.add_argument("--seed", type=int, default=20261016)
    parser.add_argument("--out", type=Path, default=ROOT / "build/whole-framework-benchmark")
    args = parser.parse_args()
    if args.cycles < 64 or args.repeat < 3:
        parser.error("cycles >=64 and repeat >=3 required")
    # Use the same deterministic stimulus generator as the existing signed FIR oracle.
    from dsp_workload import plan
    folder = args.out.resolve() / args.sim
    folder.mkdir(parents=True, exist_ok=True)
    stimulus = folder / "stimulus.json"
    stimulus.write_text(json.dumps(plan(args.cycles, args.seed)))
    assembly = ROOT / "examples/DspFir/bin/Release/net10.0/DspFir.dll"
    rtl = ROOT / "examples/DspFir/dsp_fir.sv"
    for source in (assembly, rtl):
        if not source.is_file():
            raise RuntimeError(f"Missing prebuilt input {source}")
    reference = None
    records = []
    for iteration in range(-1, args.repeat):
        # Warm both configurations once, then alternate execution order.
        order = ("legacy", "optimized") if iteration % 2 == 0 else ("optimized", "legacy")
        for mode in order:
            name = f"{mode}-{'warmup' if iteration < 0 else iteration}"
            work = folder / name
            work.mkdir(exist_ok=True)
            metrics = work / "metrics.json"
            env = dict(os.environ, DSP_PLAN=str(stimulus), DSP_REPORT=str(metrics), DSP_FAULT="0")
            env.update(STB_DIRECT_PACKED="0" if mode == "legacy" else "1",
                       STB_BATCH_TRANSFERS="0" if mode == "legacy" else "1")
            command = [sys.executable, str(ROOT / "tools/run.py"), "--no-build",
                       "--sim", args.sim, "--assembly", str(assembly),
                       "--source", str(rtl), "--top", "dsp_fir",
                       "--entry-type", "Entry, DspFir", "--work-dir", str(work),
                       "--label", "fir"]
            started = time.perf_counter()
            result = subprocess.run(command, env=env, text=True, capture_output=True, timeout=240)
            wall = time.perf_counter() - started
            (work / "runner.log").write_text(result.stdout + result.stderr)
            if result.returncode or not metrics.is_file():
                raise RuntimeError(f"{name} failed (exit {result.returncode}); see {work/'runner.log'}")
            data = json.loads(metrics.read_text())
            results = json.loads((work / "fir-results.json").read_text())
            if len(results) != 1 or results[0]["Status"] != "passed":
                raise RuntimeError(f"{name} regression not passed: {results}")
            signature = {key: data[key] for key in ("trace", "cycles", "accepted", "consumed", "stalls")}
            if reference is None:
                reference = signature
            elif signature != reference:
                raise RuntimeError(f"{name} mismatched independent FIR scoreboard trace")
            if iteration >= 0:
                records.append({"mode": mode, "iteration": iteration, "wall_seconds": wall,
                                "body_seconds": data["seconds"],
                                "allocated_bytes": data["allocated_bytes"],
                                "gc_collections": data["gc_collections"]})
    by_mode = {mode: [v for v in records if v["mode"] == mode] for mode in ("legacy", "optimized")}
    metrics = ("wall_seconds", "body_seconds", "allocated_bytes")
    medians = {mode: {m: statistics.median(row[m] for row in by_mode[mode]) for m in metrics}
               for mode in by_mode}
    report = {
        "schemaVersion": 1, "backend": "cocotb-derived GPI bootstrap (both modes)",
        "independentBackendQualified": False,
        "simulator": args.sim, "seed": args.seed, "cycles": args.cycles,
        "repeatsPerMode": args.repeat,
        "rtlSha256": hashlib.sha256(rtl.read_bytes()).hexdigest(),
        "assemblySha256": hashlib.sha256(assembly.read_bytes()).hexdigest(),
        "traceSha256": hashlib.sha256(json.dumps(reference["trace"]).encode()).hexdigest(),
        "records": records, "medians": medians,
        "optimizedOverLegacyBodyRatio": medians["optimized"]["body_seconds"] / medians["legacy"]["body_seconds"],
    }
    output = folder / "summary.json"
    output.write_text(json.dumps(report, indent=2) + "\n")
    print(f"{args.sim}: {len(reference['trace'])} matching trace records, {args.repeat} runs/mode")
    print("Median body seconds:", medians, "optimized/legacy =", report["optimizedOverLegacyBodyRatio"])
    print(f"Results: {output}")


if __name__ == "__main__":
    main()
