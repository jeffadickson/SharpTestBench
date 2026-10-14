#!/usr/bin/env python3
"""Three-process Icarus comparison of C# packed versus legacy string round trips.
Requires the same CS_DOTNET_ROOT/CS_IVERILOG/CS_IVL_ROOT/CS_VVP setup as run.py.
"""
import json
import os
from pathlib import Path
import statistics
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
rows = []
for trial in range(3):
    label = f'v05-packed-bench-{trial}'
    args = [sys.executable, ROOT / 'tools/run.py', '--project', 'Benchmark', '--filter', 'PackedVectorBenchmark', '--label', label]
    if trial:
        args.append('--no-build')
    p = subprocess.run([str(a) for a in args], env=dict(os.environ, CS_PACKED_REVERSE=str(trial % 2)), text=True, capture_output=True)
    (ROOT / 'build' / (label + '-driver.log')).write_text(p.stdout + p.stderr)
    if p.returncode:
        raise SystemExit(p.stdout + p.stderr)
    measures = [json.loads(line.removeprefix('PACKED_MEASURE ')) for line in p.stdout.splitlines() if line.startswith('PACKED_MEASURE ')]
    if len(measures) != 4:
        raise SystemExit('Expected four benchmark measurements')
    rows.extend(dict(trial=trial, **m) for m in measures)
summary = []
for width in [256, 4096]:
    by_mode = {mode: [r for r in rows if r['width'] == width and r['mode'] == mode] for mode in ['string', 'packed']}
    times = {k: statistics.median(r['seconds'] for r in v) for k, v in by_mode.items()}
    allocations = {k: statistics.median(r['managedBytes'] / r['count'] for r in v) for k, v in by_mode.items()}
    summary.append(dict(width=width, medianSeconds=times, managedBytesPerRoundTrip=allocations, speedRatio=times['string']/times['packed']))
result = dict(description='500 immediate known-value write/read round trips; 100 warmups per mode; 3 fresh Icarus processes; alternate mode order. Native allocation excluded from managed byte counts. Not a Python or full-project benchmark.', measurements=rows, summary=summary)
(ROOT / 'build/packed-benchmark.json').write_text(json.dumps(result, indent=2)+'\n')
print(json.dumps(summary, indent=2))
