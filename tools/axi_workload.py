#!/usr/bin/env python3
"""Deterministic AXI-memory correctness, negative tests and fresh-process benchmark."""
import argparse, hashlib, json, os, platform, random, statistics, subprocess, sys, time
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]

def plan(count, seed):
    rng = random.Random(seed)
    result = []
    def add(op, address=0, data=0, strobe=0):
        result.append(dict(Op=op,Address=address,Data=data,Strobe=strobe,
                           AwDelay=rng.randrange(6),WDelay=rng.randrange(6),ReadyDelay=rng.randrange(8,20)))
    # Directed byte-lane coverage plus randomized traffic and invalid addresses.
    for i in range(count):
        address = rng.randrange(64)*4
        if i % 23 == 0: address = 256
        elif i % 29 == 0: address = 3
        add('write',address,rng.getrandbits(32),i%16)
        add('read',address)
        if i % 32 == 31:
            add('reset_'+('aw','w','b','r')[(i//32)%4]); add('read',0)
    # Always cover all reset cut points even in a small CI workload.
    for phase in ['aw','w','b','r']:
        add('reset_'+phase); add('read',0)
    return result

def python_run(work, plan_file, report, fault, profile):
    python = ROOT / 'build/reference-python' / ('Scripts/python.exe' if os.name=='nt' else 'bin/python')
    if not python.exists(): raise RuntimeError('Provision pinned oracle with tools/structured_reference_env.py first')
    env = dict(os.environ, AXI_PLAN=str(plan_file), AXI_REPORT=str(report), AXI_FAULT=str(fault), AXI_PROFILE=str(int(profile)))
    return [str(python),str(ROOT/'tools/axi_python.py'),'--work-dir',str(work)], env

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--transactions',type=int,default=128)
    p.add_argument('--seed',type=int,default=20261014)
    p.add_argument('--repeat',type=int,default=3)
    p.add_argument('--skip-negative',action='store_true')
    args=p.parse_args()
    if args.transactions<16 or args.repeat<1:p.error('at least 16 transactions and one measured repeat required')
    build=ROOT/'build/axi-workload';build.mkdir(parents=True,exist_ok=True)
    plan_file=build/'plan.json';plan_file.write_text(json.dumps(plan(args.transactions,args.seed)))
    records=[];baseline=None
    def execute(language, label, fault=0, profile=False):
        nonlocal baseline
        work=build/label;work.mkdir(exist_ok=True);report=work/'metrics.json';report.unlink(missing_ok=True)
        if language=='csharp':
            cmd=[sys.executable,str(ROOT/'tools/run.py'),'--no-build','--sim','icarus','--assembly',str(ROOT/'examples/AxiMemory/bin/Release/net10.0/AxiMemory.dll'),
                 '--source',str(ROOT/'examples/AxiMemory/axi_memory.sv'),'--top','axi_memory','--entry-type','Entry, AxiMemory','--work-dir',str(work),'--label','axi']
            env=dict(os.environ,AXI_PLAN=str(plan_file),AXI_REPORT=str(report),AXI_FAULT=str(fault))
        else:cmd,env=python_run(work,plan_file,report,fault,profile)
        start=time.perf_counter();r=subprocess.run(cmd,env=env,text=True,capture_output=True,timeout=180);wall=time.perf_counter()-start
        (work/'driver.log').write_text(r.stdout+r.stderr)
        if fault:
            expected='AXI_TIMEOUT' if fault==3 else 'AXI_DATA'
            if r.returncode==0 or expected not in r.stdout+r.stderr:raise RuntimeError(f'{language} did not detect fault {fault}: {r.stdout+r.stderr}')
            return dict(language=language,fault=fault,detected=True)
        if r.returncode or not report.exists():raise RuntimeError(r.stdout+r.stderr)
        data=json.loads(report.read_text())
        contract={k:data[k] for k in ('trace','cycles','write_stalls','read_stalls')}
        if baseline is None:baseline=contract
        if contract!=baseline:raise RuntimeError(f'{language} transaction/cycle trace mismatch')
        return dict(language=language,label=label,wall_seconds=wall,**{k:v for k,v in data.items() if k!='trace'})
    for language in ['csharp','python']:
        execute(language,language+'-warmup')
    # Alternate order to reduce monotonic machine-load/JIT-cache bias.
    for i in range(args.repeat):
        for language in (['csharp','python'] if i%2==0 else ['python','csharp']):
            records.append(execute(language,f'{language}-{i}'))
    profile=execute('python','python-allocation',profile=True)
    negatives=[]
    if not args.skip_negative:
        for fault in (1,2,3):
            for language in ['csharp','python']:negatives.append(execute(language,f'{language}-fault{fault}',fault))
    summary=dict(schema=1,seed=args.seed,commands=len(json.loads(plan_file.read_text())),plan_sha256=hashlib.sha256(plan_file.read_bytes()).hexdigest(),
        trace_sha256=hashlib.sha256(json.dumps(baseline,sort_keys=True).encode()).hexdigest(),records=records,python_allocation_profile=profile,negative_tests=negatives,
        medians={language:{key:statistics.median(r[key] for r in records if r['language']==language) for key in ('wall_seconds','seconds')} for language in ['csharp','python']},
        scope='Icarus AXI4-Lite-shaped single-outstanding memory fixture; not full AXI conformance or CPU/DSP benchmark',
        methodology='One unmeasured fresh-process warmup per language; repeated fresh processes in alternating order. Wall time includes HDL compile and runner/runtime startup. Body time includes bus driving, scoreboard and trace accumulation. C# cumulative allocations and Python separately-profiled peak traced memory are different metrics, not directly comparable.')
    summary['environment']={'os':platform.platform(),'architecture':platform.machine(),'python_runner':sys.version,'cocotb_oracle':'2.1.0','source_revision':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),'working_tree_dirty':bool(subprocess.check_output(['git','status','--porcelain'],cwd=ROOT,text=True)),'native_upstream':json.loads((ROOT/'third_party/UPSTREAM.json').read_text())}
    summary['environment']['simulator']=subprocess.run([os.environ.get('CS_VVP','vvp'),'-V'],text=True,capture_output=True).stderr.splitlines()[:2]
    (build/'summary.json').write_text(json.dumps(summary,indent=2))
    print('AXI_WORKLOAD_PASS',json.dumps(summary['medians']),len(negatives),'fault checks')
if __name__=='__main__':main()
