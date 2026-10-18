#!/usr/bin/env python3
"""Qualify pinned upstream AXI Stream RTL through independent runner paths."""
import argparse
import os
from pathlib import Path
import subprocess
import sys
from axis_fixture import configuration, plan, validate_plan, verify_sources, metrics
from dual_runner_manifest import installed_adapter, make_manifest
from dual_runner_ci import clean_environment, clear_case, fault_detected
from dual_runner_parity import compare, write_report
from strict_json import load

def run_case(root, mode, seed, sidebands=True, negative=False, count=64, stall=3, reset_at=32):
    root=Path(root).resolve()
    top=configuration(mode,sidebands)
    stimulus=validate_plan(plan(seed,count,stall,reset_at))
    provenance=verify_sources(root)
    suffix='-negative' if negative else ''
    work=root/'build'/f'axis-{top}-{seed}-{count}-{stall}-{reset_at}{suffix}'
    work.mkdir(parents=True,exist_ok=True)
    lock=work/'.parity.lock'
    descriptor=os.open(lock,os.O_CREAT|os.O_EXCL|os.O_WRONLY)
    try:
        with os.fdopen(descriptor,'w') as stream: stream.write(str(os.getpid()))
        clear_case(work)
        env=clean_environment(os.environ)
        env.update(AXIS_PLAN=str(work/'plan.json'),AXIS_SIDEBANDS='1' if sidebands else '0',
                   AXIS_FAULT='1' if negative else '0',STB_NATIVE_VPI_TIME='1')
        def execute(command,label,environment=env):
            with (work/(label+'.log')).open('w',encoding='utf-8') as log:
                return subprocess.run(list(map(str,command)),cwd=root,env=environment,
                                      stdout=log,stderr=subprocess.STDOUT,timeout=300).returncode
        if execute(['dotnet','build','examples/AxisRegister','-c','Release','-v:q'],'build'):
            raise RuntimeError('Stream scoreboard build failed')
        from native_build import build_host, sdk_root
        build_host(root,sdk_root(),root/'build')
        manifest=make_manifest(root,'counter',seed,installed_adapter(),work/'native')
        manifest.update(top=top,sources=[str(root/'examples/AxisRegister/axis_fixture.sv'),
            str(root/'tests/Fixtures/verilog-axis/axis_register.v')],
            assembly=str(root/'examples/AxisRegister/bin/Release/net10.0/AxisRegister.dll'),
            entryType='Entry, AxisRegister',expectedNames=['Entry.StreamScoreboard'],testNames=['Entry.StreamScoreboard'])
        write_report(work/'manifest.json',manifest)
        write_report(work/'plan.json',stimulus)
        legacy_env=dict(env,AXIS_REPORT=str(work/'legacy/metrics.json'))
        native_env=dict(env,AXIS_REPORT=str(work/'native/metrics.json'))
        command=[sys.executable,root/'tools/run.py','--sim','icarus','--no-build',
                 '--assembly',manifest['assembly'],'--top',top,'--entry-type',manifest['entryType'],
                 '--work-dir',work/'legacy','--label','legacy','--seed',seed,'--exact','Entry.StreamScoreboard']
        for source in manifest['sources']: command.extend(['--source',source])
        left_code=execute(command,'legacy',legacy_env)
        right_code=execute(['dotnet',root/'tools/SharpTestBench.Runner/bin/Release/net10.0/SharpTestBench.Runner.dll',
                            'run',work/'manifest.json'],'native',native_env)
        left=load(work/'legacy/legacy-results.json'); right=load(work/'native/icarus-results.json')
        if negative:
            report={'matched':fault_detected(left,left_code,manifest['expectedNames'],'AXIS_MISMATCH') and
                                 fault_detected(right,right_code,manifest['expectedNames'],'AXIS_MISMATCH'),
                    'negativeControl':True}
        else:
            report=compare(left,right,manifest['expectedNames'])
            a=metrics(load(work/'legacy/metrics.json')); b=metrics(load(work/'native/metrics.json'))
            report['traceMatched']=a==b
            report['matched']=report['matched'] and left_code==right_code==0 and a==b
        report.update(schemaVersion=1,top=top,seed=seed,revision=provenance['revision'],
                      exitCodes={'legacy':left_code,'native':right_code})
        write_report(work/('negative-control.json' if negative else 'parity.json'),report)
        if not report['matched']: raise ValueError('Stream parity qualification failed')
        return report
    except Exception as error:
        write_report(work/'case-failure.json',{'status':'failed','error':str(error),'kind':type(error).__name__})
        raise
    finally: lock.unlink(missing_ok=True)

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mode',type=int,choices=(0,1,2),default=2)
    parser.add_argument('--seed',type=int,default=1)
    parser.add_argument('--disabled-sidebands',action='store_true')
    parser.add_argument('--negative-control',action='store_true')
    args=parser.parse_args()
    print(run_case(Path(__file__).resolve().parents[1],args.mode,args.seed,
                   not args.disabled_sidebands,args.negative_control))

if __name__=='__main__': main()
