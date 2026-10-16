#!/usr/bin/env python3
"""Compare deterministic signed FIR transactions in C# and released cocotb 2.1.0."""
import argparse, hashlib, json, os, platform, random, statistics, subprocess, sys, time
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]


def plan(count, seed):
    """Directed extremes, long stalls and reset cuts followed by reproducible random traffic."""
    rng = random.Random(seed)
    rows = [dict(Reset=True, Ready=False, Offer=False, Sample=0)]
    # Each burst accepts the negative extreme before stalling, then resets a held
    # output/pending input. Other bursts drain signed extrema and impulse samples.
    for block in range(4):
        for i in range(24):
            rows.append(dict(Reset=i == 12 and block % 2 == 0,
                Ready=i not in range(2, 12), Offer=True,
                Sample=[-32768, 32767, -1, 0, 1, 16384][i % 6]))
    for i in range(count):
        rows.append(dict(Reset=i % 257 == 256, Ready=rng.randrange(4) != 0,
            Offer=rng.randrange(5) != 0, Sample=rng.randrange(-32768, 32768)))
    rows.extend(dict(Reset=False, Ready=True, Offer=False, Sample=0) for _ in range(4))
    return rows


def main():
    """Warm both runners, alternate measured processes, then require all injected faults to fail."""
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--cycles', type=int, default=1024)
    p.add_argument('--repeat', type=int, default=3)
    p.add_argument('--seed', type=int, default=20261016)
    args = p.parse_args()
    if args.cycles < 64 or args.repeat < 1: p.error('need >=64 cycles and >=1 repeat')
    work = ROOT / 'build/dsp-workload'; work.mkdir(parents=True, exist_ok=True)
    inputs = work / 'plan.json'; inputs.write_text(json.dumps(plan(args.cycles, args.seed)))
    oracle = ROOT / 'build/reference-python' / ('Scripts/python.exe' if os.name == 'nt' else 'bin/python')
    if not oracle.exists(): raise RuntimeError('Provision pinned oracle using tools/structured_reference_env.py')
    baseline = None
    def execute(language, label, fault=0):
        """Retain per-process diagnostics and reject missing results and divergent full traces."""
        nonlocal baseline
        folder = work / label; folder.mkdir(exist_ok=True)
        report = folder / 'metrics.json'; report.unlink(missing_ok=True)
        env = dict(os.environ, DSP_PLAN=str(inputs), DSP_REPORT=str(report), DSP_FAULT=str(fault))
        if language == 'csharp':
            cmd = [sys.executable, str(ROOT/'tools/run.py'), '--no-build', '--assembly',
                str(ROOT/'examples/DspFir/bin/Release/net10.0/DspFir.dll'), '--source',
                str(ROOT/'examples/DspFir/dsp_fir.sv'), '--top', 'dsp_fir', '--entry-type',
                'Entry, DspFir', '--work-dir', str(folder), '--label', 'dsp']
        else: cmd = [str(oracle), str(ROOT/'tools/dsp_python.py'), '--work-dir', str(folder)]
        start = time.perf_counter()
        result = subprocess.run(cmd, env=env, text=True, capture_output=True, timeout=180)
        wall = time.perf_counter() - start
        log = result.stdout + result.stderr; (folder/'driver.log').write_text(log)
        if fault:
            if result.returncode == 0 or 'DSP_MISMATCH' not in log:
                raise RuntimeError(f'{language} missed fault {fault}: {log}')
            return dict(language=language, fault=fault, detected=True)
        if result.returncode or not report.exists(): raise RuntimeError(log)
        data = json.loads(report.read_text())
        contract = {key:data[key] for key in ('trace','cycles','accepted','consumed','stalls')}
        if baseline is None: baseline = contract
        if contract != baseline: raise RuntimeError(f'{language}: full FIR cycle trace differs')
        return dict(language=language, wall_seconds=wall, **{k:v for k,v in data.items() if k != 'trace'})
    for language in ('csharp','python'): execute(language, language+'-warmup')
    records = []
    for repeat in range(args.repeat):
        for language in (('csharp','python') if repeat % 2 == 0 else ('python','csharp')):
            records.append(execute(language, f'{language}-{repeat}'))
    negatives = [execute(language, f'{language}-fault{fault}', fault)
                 for fault in (1,2,3) for language in ('csharp','python')]
    sources = ['examples/DspFir/dsp_fir.sv','examples/DspFir/Tests.cs','examples/DspFir/test_dsp.py',
               'tools/dsp_workload.py','tools/dsp_python.py']
    summary = dict(schema=1, seed=args.seed, plan_sha256=hashlib.sha256(inputs.read_bytes()).hexdigest(),
        trace_sha256=hashlib.sha256(json.dumps(baseline,sort_keys=True).encode()).hexdigest(),
        contract={k:v for k,v in baseline.items() if k != 'trace'}, records=records, negative_tests=negatives,
        source_hashes={p:hashlib.sha256((ROOT/p).read_bytes()).hexdigest() for p in sources},
        medians={language:{key:statistics.median(r[key] for r in records if r['language']==language)
                 for key in ('seconds','wall_seconds')} for language in ('csharp','python')},
        environment=dict(platform=platform.platform(), cocotb='2.1.0',
            revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
            dirty=bool(subprocess.check_output(['git','status','--porcelain'],cwd=ROOT,text=True)),
            simulator=subprocess.run([os.environ.get('CS_VVP','vvp'),'-V'],text=True,capture_output=True).stderr.splitlines()[:2],
            native_manifest=json.loads((ROOT/'build/native-build.json').read_text()),
            assembly_sha256=hashlib.sha256((ROOT/'examples/DspFir/bin/Release/net10.0/DspFir.dll').read_bytes()).hexdigest()),
        methodology='One fresh-process warmup per language, alternating measured processes. Wall includes HDL compilation and startup. Body includes driving, scoreboard and trace collection. C# cumulative allocations are recorded; no comparable Python allocation claim. Icarus only, one four-tap elastic signed FIR, not general DSP throughput.')
    (work/'summary.json').write_text(json.dumps(summary,indent=2)+'\n')
    print('DSP_WORKLOAD_PASS', json.dumps(summary['medians']), len(negatives), 'fault checks')

if __name__ == '__main__': main()
