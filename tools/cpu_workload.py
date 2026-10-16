#!/usr/bin/env python3
"""Compare deterministic custom CPU transactions in C# and released cocotb 2.1.0."""
import argparse, hashlib, json, os, platform, statistics, subprocess, sys, time
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]


def plan(count):
    """Encode a memory/checksum loop with signed immediates, register-zero writes and both branch outcomes."""
    def instruction(op, rd=0, rs=0, rt=0, imm=0):
        """Encode the documented opcode, register fields and signed 16-bit immediate."""
        return (op << 28) | (rd << 25) | (rs << 22) | (rt << 19) | (imm & 65535)
    return [instruction(1, rd=0, imm=123), instruction(1, rd=1, imm=count),
        instruction(1, rd=2, imm=0), instruction(1, rd=3, imm=-1),
        instruction(1, rd=4, imm=1),
        instruction(5, rs=2, rt=3), instruction(4, rd=5, rs=2),
        instruction(2, rd=3, rs=5, rt=4), instruction(3, rd=6, rs=3, rt=5),
        instruction(5, rs=2, rt=6, imm=64), instruction(2, rd=2, rs=2, rt=4),
        instruction(1, rd=7, imm=-1), instruction(2, rd=1, rs=1, rt=7),
        instruction(6, rs=1, rt=0, imm=-8), instruction(15)]


def main():
    """Warm both runners, alternate measured processes, then require all injected faults to fail."""
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--iterations', type=int, default=256)
    p.add_argument('--repeat', type=int, default=3)
    args = p.parse_args()
    if not 2 <= args.iterations <= 32767 or args.repeat < 1: p.error('need 2..32767 iterations and >=1 repeat')
    work = ROOT / 'build/cpu-workload'; work.mkdir(parents=True, exist_ok=True)
    inputs = work / 'plan.json'; inputs.write_text(json.dumps(plan(args.iterations)))
    oracle = ROOT / 'build/reference-python' / ('Scripts/python.exe' if os.name == 'nt' else 'bin/python')
    if not oracle.exists(): raise RuntimeError('Provision pinned oracle using tools/structured_reference_env.py')
    baseline = None
    def execute(language, label, fault=0):
        """Retain per-process diagnostics and reject missing results and divergent full traces."""
        nonlocal baseline
        folder = work / label; folder.mkdir(exist_ok=True)
        report = folder / 'metrics.json'; report.unlink(missing_ok=True)
        env = dict(os.environ, CPU_PLAN=str(inputs), CPU_REPORT=str(report), CPU_FAULT=str(fault))
        if language == 'csharp':
            cmd = [sys.executable, str(ROOT/'tools/run.py'), '--no-build', '--assembly',
                str(ROOT/'examples/CpuCore/bin/Release/net10.0/CpuCore.dll'), '--source',
                str(ROOT/'examples/CpuCore/cpu_core.sv'), '--top', 'cpu_core', '--entry-type',
                'Entry, CpuCore', '--work-dir', str(folder), '--label', 'cpu']
        else: cmd = [str(oracle), str(ROOT/'tools/cpu_python.py'), '--work-dir', str(folder)]
        start = time.perf_counter()
        result = subprocess.run(cmd, env=env, text=True, capture_output=True, timeout=180)
        wall = time.perf_counter() - start
        log = result.stdout + result.stderr; (folder/'driver.log').write_text(log)
        if fault:
            if result.returncode == 0 or 'CPU_MISMATCH' not in log:
                raise RuntimeError(f'{language} missed fault {fault}: {log}')
            return dict(language=language, fault=fault, detected=True)
        if result.returncode or not report.exists(): raise RuntimeError(log)
        data = json.loads(report.read_text())
        contract = {key:data[key] for key in ('trace','cycles','commits','memoryStalls','pauses','memory')}
        if baseline is None: baseline = contract
        if contract != baseline: raise RuntimeError(f'{language}: full CPU cycle trace differs')
        if data['commits'] != 9 * args.iterations + 6:
            raise RuntimeError(f'{language}: unexpected instruction count')
        return dict(language=language, wall_seconds=wall, **{k:v for k,v in data.items() if k != 'trace'})
    for language in ('csharp','python'): execute(language, language+'-warmup')
    records = []
    for repeat in range(args.repeat):
        for language in (('csharp','python') if repeat % 2 == 0 else ('python','csharp')):
            records.append(execute(language, f'{language}-{repeat}'))
    negatives = [execute(language, f'{language}-fault{fault}', fault)
                 for fault in (1,2,3) for language in ('csharp','python')]
    sources = ['examples/CpuCore/cpu_core.sv','examples/CpuCore/Tests.cs','examples/CpuCore/test_cpu.py',
               'tools/cpu_workload.py','tools/cpu_python.py']
    summary = dict(schema=1, iterations=args.iterations, plan_sha256=hashlib.sha256(inputs.read_bytes()).hexdigest(),
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
            assembly_sha256=hashlib.sha256((ROOT/'examples/CpuCore/bin/Release/net10.0/CpuCore.dll').read_bytes()).hexdigest()),
        methodology='One fresh-process warmup per language, alternating measured processes. Wall includes HDL compilation and startup. Body includes driving, scoreboard and trace collection. C# cumulative allocations are recorded; no comparable Python allocation claim. Icarus only, one four-tap elastic custom CPU, not general CPU throughput.')
    (work/'summary.json').write_text(json.dumps(summary,indent=2)+'\n')
    print('CPU_WORKLOAD_PASS', json.dumps(summary['medians']), len(negatives), 'fault checks')

if __name__ == '__main__': main()
