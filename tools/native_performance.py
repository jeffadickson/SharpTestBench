#!/usr/bin/env python3
"""Linux native A/B measurement with identical managed code, HDL, and checked traces.

Provision the historical adapters in --baseline-python first. Each sample is a
fresh simulator process; only native host/adapters vary. No CI pass/fail threshold
is inferred from noisy shared-host timings. Raw samples and build hashes are saved.
"""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import platform
import random
import re
import statistics
import subprocess
import sys
import time
from axi_workload import plan
from native_build import build_host, sdk_root, HOST_NAME

ROOT = Path(__file__).resolve().parents[1]
BASELINE = '2ff0ac644602197f64fb7839d52ff9d848d75387'


def digest(path):
    """Identify the exact binary or workload file used by a measurement."""
    return hashlib.sha256(path.read_bytes()).hexdigest()


def comparison(records, variant, metric, baseline_variant='baseline'):
    """Bootstrap paired rounds, retaining shared round-to-round load variation."""
    base = [r['metrics'][metric] for r in records if r['variant'] == baseline_variant]
    candidate = [r['metrics'][metric] for r in records if r['variant'] == variant]
    rng = random.Random(20261016)
    ratios = []
    for _ in range(10000):
        indices = [rng.randrange(len(base)) for _ in base]
        ratios.append(statistics.median(base[i] for i in indices) /
                      statistics.median(candidate[i] for i in indices))
    ratios.sort()
    return {'baseline_median_seconds': statistics.median(base),
            'candidate_median_seconds': statistics.median(candidate),
            'speedup': statistics.median(base) / statistics.median(candidate),
            'paired_bootstrap_95': [ratios[250], ratios[9749]],
            'baseline_range': [min(base), max(base)], 'candidate_range': [min(candidate), max(candidate)]}


def main():
    """Build both hosts, warm up each configuration, then interleave checked samples."""
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--baseline-python', type=Path, required=True)
    p.add_argument('--baseline-host', type=Path, help='Use a preserved native host instead of reconstructing historical C')
    p.add_argument('--baseline-managed', type=Path, help='Preserved Benchmark/ and AxiMemory/ assembly directories')
    p.add_argument('--repeat', type=int, default=9)
    p.add_argument('--transactions', type=int, default=2048)
    p.add_argument('--packed-count', type=int, default=1000)
    p.add_argument('--output', type=Path, default=ROOT / 'build/native-performance')
    args = p.parse_args()
    if sys.platform != 'linux': p.error('Historical host reconstruction currently targets Linux')
    if args.repeat < 5 or args.transactions < 16 or args.packed_count < 500:
        p.error('Use at least 5 repeats, 16 transactions, and 500 packed round trips')
    output = args.output.resolve(); output.mkdir(parents=True, exist_ok=True)
    baseline_python = args.baseline_python.resolve()
    if not (baseline_python / 'cocotb_tools/config.py').is_file():
        p.error('Install the historical adapter build into --baseline-python first')
    old = output / 'baseline-host'; old.mkdir(exist_ok=True)
    files = ['native/abi.c', 'native/abi.h', 'native/host.c', 'native/platform.h',
             'native/upstream/gpi.h', 'native/upstream/exports.h',
             'native/upstream/vpi_user.h', 'native/upstream/vpi_compatibility.h', 'tools/native_build.py']
    for name in files:
        target = old / name; target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(subprocess.check_output(['git', 'show', BASELINE + ':' + name], cwd=ROOT))
    spec = importlib.util.spec_from_file_location('historical_native_build', old / 'tools/native_build.py')
    previous = importlib.util.module_from_spec(spec); spec.loader.exec_module(previous)
    if args.baseline_host is None: previous.build_host(old, sdk_root(), old)
    new = output / 'optimized-host'; new.mkdir(exist_ok=True)
    build_host(ROOT, sdk_root(), new)
    variants = {'baseline': (args.baseline_host.resolve() if args.baseline_host else old / HOST_NAME, baseline_python),
                'bridge_only': (new / HOST_NAME, baseline_python),
                'full': (new / HOST_NAME, None)}
    # Keep every measured process on one available logical CPU. This is a
    # single-simulator-thread benchmark, not a parallel throughput measurement.
    affinity = sorted(os.sched_getaffinity(0)); os.sched_setaffinity(0, {affinity[0]})
    plan_file = output / 'plan.json'
    plan_file.write_text(json.dumps(plan(args.transactions, 20261014)))
    expected_trace = None
    records = []

    def execute(variant, round_number):
        """Execute microbenchmarks and a realistic bus workload; reject wrong results."""
        nonlocal expected_trace
        host, python_path = variants[variant]
        env = dict(os.environ, DOTNET_TieredCompilation='0', CS_PACKED_COUNT=str(args.packed_count))
        env['CS_PACKED_REVERSE'] = str(int(round_number % 2 == 1))
        if python_path is not None: env['PYTHONPATH'] = str(python_path)
        else: env.pop('PYTHONPATH', None)
        metrics = {}
        allocations = {}
        for workload in ['micro', 'axi']:
            work = output / f'{round_number}-{variant}-{workload}'; work.mkdir(exist_ok=True)
            command = [sys.executable, str(ROOT / 'tools/run.py'), '--sim', 'icarus', '--no-build',
                       '--host', str(host), '--work-dir', str(work), '--label', 'perf']
            managed = args.baseline_managed.resolve() if args.baseline_managed and variant != 'full' else None
            if workload == 'micro':
                command += ['--project', 'Benchmark']
                if managed: command += ['--assembly', str(managed / 'Benchmark/Benchmark.dll')]

            else:
                command += ['--assembly', str(managed / 'AxiMemory/AxiMemory.dll' if managed else ROOT / 'examples/AxiMemory/bin/Release/net10.0/AxiMemory.dll'),
                            '--source', str(ROOT / 'examples/AxiMemory/axi_memory.sv'),
                            '--top', 'axi_memory', '--entry-type', 'Entry, AxiMemory']
                env.update(AXI_PLAN=str(plan_file), AXI_REPORT=str(work / 'metrics.json'), AXI_FAULT='0')
            start = time.perf_counter()
            result = subprocess.run(command, cwd=ROOT, env=env, text=True, capture_output=True, timeout=300)
            metrics[workload + '_wall'] = time.perf_counter() - start
            (work / 'driver.log').write_text(result.stdout + result.stderr)
            if result.returncode: raise RuntimeError(result.stdout + result.stderr)
            if workload == 'micro':
                for match in re.finditer(r'^MEASURE (timer|rw32) ([0-9.eE+-]+)', result.stdout, re.M):
                    metrics[match[1]] = float(match[2])
                for line in result.stdout.splitlines():
                    if line.startswith('PACKED_MEASURE '):
                        data = json.loads(line.removeprefix('PACKED_MEASURE '))
                        if data['count'] != args.packed_count: raise RuntimeError('Rebuild Benchmark with CS_PACKED_COUNT support')
                        metrics[f"{data['mode']}_{data['width']}"] = data['seconds']
                        allocations[f"{data['mode']}_{data['width']}"] = data['managedBytes']
                if len(metrics) != 7: raise RuntimeError('Missing benchmark metrics')
            else:
                data = json.loads((work / 'metrics.json').read_text())
                trace = {k: data[k] for k in ['trace', 'cycles', 'write_stalls', 'read_stalls']}
                if expected_trace is None: expected_trace = trace
                if trace != expected_trace: raise RuntimeError('AXI correctness/trace mismatch')
                metrics['axi_body'] = data['seconds']
        print(json.dumps({'round': round_number, 'variant': variant, 'metrics': metrics}), flush=True)
        return {'round': round_number, 'variant': variant, 'metrics': metrics, 'managed_allocated_bytes': allocations}

    for variant in variants: execute(variant, -1)
    # Rotate all three orders evenly (Latin-square schedule) instead of timing
    # one complete configuration before another; pair statistics by round.
    for round_number in range(args.repeat):
        names = list(variants); offset = round_number % len(names)
        for variant in names[offset:] + names[:offset]: records.append(execute(variant, round_number))
        (output / 'samples.json').write_text(json.dumps(records, indent=2) + '\n')
    from cocotb_tools import config
    library_hashes = {name: {p.name: digest(p) for p in directory.glob('*.so')}
                      for name, directory in [('baseline', baseline_python / 'cocotb/libs'), ('full', Path(config.libs_dir))]}
    summary = {'schema': 1, 'baseline_revision': BASELINE if args.baseline_host is None else 'preserved binary; see host hashes',
               'baseline_managed_sha256': {str(p.relative_to(args.baseline_managed)): digest(p) for p in args.baseline_managed.rglob('*.dll')} if args.baseline_managed else None,
               'candidate_revision': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
               'working_tree_dirty': bool(subprocess.check_output(['git', 'status', '--porcelain'], cwd=ROOT, text=True)),
               'native_upstream': json.loads((ROOT / 'third_party/UPSTREAM.json').read_text()),
               'optimized_host_build': json.loads((new / 'native-build.json').read_text()),
               'benchmark_source_sha256': digest(ROOT / 'tests/Benchmark/Bench.cs'),
               'repeat': args.repeat, 'packed_count': args.packed_count, 'transactions': args.transactions,
               'environment': {'platform': platform.platform(), 'cpu_affinity': [affinity[0]],
                               'cpu_model': next((x for x in Path('/proc/cpuinfo').read_text().splitlines() if x.startswith('model name')), ''),
                               'dotnet': subprocess.check_output([sdk_root() / 'dotnet', '--version'], text=True).strip(),
                               'simulator': subprocess.run([os.environ.get('CS_VVP', 'vvp'), '-V'], text=True, capture_output=True).stderr.splitlines()[:2],
                               'tiered_compilation': False},
               'host_sha256': {name: digest(host) for name, (host, _) in variants.items()},
               'adapter_sha256': library_hashes, 'plan_sha256': digest(plan_file),
               'axi_trace_sha256': hashlib.sha256(json.dumps(expected_trace, sort_keys=True).encode()).hexdigest(),
               'managed_sha256': {str(p.relative_to(ROOT)): digest(p) for p in [ROOT / 'tests/Benchmark/bin/Release/net10.0/Benchmark.dll', ROOT / 'examples/AxiMemory/bin/Release/net10.0/AxiMemory.dll', ROOT / 'src/SharpTestBench/bin/Release/net10.0/SharpTestBench.dll']},
               'records': records,
               'comparisons': {name: {metric: comparison(records, name, metric) for metric in records[0]['metrics']}
                               for name in ['bridge_only', 'full']},
               'adapter_increment': {metric: comparison(records, 'full', metric, 'bridge_only') for metric in records[0]['metrics']},
               'scope': 'Same managed/HDL code, Icarus, Linux x64; fresh processes; one warmup/configuration; interleaved rounds. Wall includes startup and HDL compile; body metrics exclude them. Bootstrap intervals describe these samples, not machine-to-machine guarantees. Packed microbenchmarks are synthetic; AXI fixture is single-outstanding AXI4-Lite-shaped traffic.'}
    (output / 'summary.json').write_text(json.dumps(summary, indent=2) + '\n')
    print('NATIVE_PERFORMANCE_COMPLETE', output / 'summary.json')


if __name__ == '__main__': main()
