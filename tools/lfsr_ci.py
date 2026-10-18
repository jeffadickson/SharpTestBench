#!/usr/bin/env python3
"""Compare independent runner execution of pinned LFSR/CRC RTL."""
import argparse
import os
from pathlib import Path
import subprocess
import sys
from lfsr_fixture import CASES,STYLES,fixture,plan,validate_plan,metrics,verify_sources
from axis_fixture import integer
from dual_runner_manifest import installed_adapter,make_manifest
from dual_runner_ci import clean_environment,clear_case,fault_detected
from dual_runner_parity import compare,write_report
from strict_json import load

def run_case(root,name,style,seed,fault=0):
    root=Path(root).resolve();top,settings=fixture(name,style)
    integer(fault,0,2,'fault');stimulus=validate_plan(plan(settings,seed))
    provenance=verify_sources(root)
    work=root/'build'/f'lfsr-{top}-{seed}-fault{fault}'
    work.mkdir(parents=True,exist_ok=True)
    lock=work/'.parity.lock';descriptor=os.open(lock,os.O_CREAT|os.O_EXCL|os.O_WRONLY)
    try:
        with os.fdopen(descriptor,'w') as stream:stream.write(str(os.getpid()))
        clear_case(work)
        env=clean_environment(os.environ)
        env.update(LFSR_PLAN=str(work/'plan.json'),LFSR_FAULT=str(fault),STB_NATIVE_VPI_TIME='1')
        def execute(command,label,environment=env):
            with (work/(label+'.log')).open('w',encoding='utf-8') as log:
                return subprocess.run(list(map(str,command)),cwd=root,env=environment,stdout=log,
                                      stderr=subprocess.STDOUT,timeout=300).returncode
        if execute(['dotnet','build','examples/Lfsr','-c','Release','-v:q'],'build'):
            raise RuntimeError('LFSR scoreboard build failed')
        from native_build import build_host,sdk_root
        build_host(root,sdk_root(),root/'build')
        manifest=make_manifest(root,'counter',seed,installed_adapter(),work/'native')
        manifest.update(top=top,sources=[str(root/'examples/Lfsr/lfsr_fixture.sv'),
            str(root/'tests/Fixtures/verilog-lfsr/lfsr.v')],
            assembly=str(root/'examples/Lfsr/bin/Release/net10.0/Lfsr.dll'),entryType='Entry, Lfsr',
            expectedNames=['Entry.SerialScoreboard'],testNames=['Entry.SerialScoreboard'])
        write_report(work/'manifest.json',manifest);write_report(work/'plan.json',stimulus)
        from lfsr_fixture import plan_digest
        digest=plan_digest(work/'plan.json')
        command=[sys.executable,root/'tools/run.py','--sim','icarus','--no-build',
                 '--assembly',manifest['assembly'],'--top',top,'--entry-type',manifest['entryType'],
                 '--work-dir',work/'legacy','--label','legacy','--seed',seed,'--exact','Entry.SerialScoreboard']
        for source in manifest['sources']:command.extend(['--source',source])
        left_code=execute(command,'legacy',dict(env,LFSR_REPORT=str(work/'legacy/metrics.json')))
        right_code=execute(['dotnet',root/'tools/SharpTestBench.Runner/bin/Release/net10.0/SharpTestBench.Runner.dll',
                            'run',work/'manifest.json'],'native',dict(env,LFSR_REPORT=str(work/'native/metrics.json')))
        if plan_digest(work/'plan.json')!=digest:raise ValueError('LFSR stimulus changed during execution')
        verify_sources(root)
        left=load(work/'legacy/legacy-results.json');right=load(work/'native/icarus-results.json')
        if fault:
            report={'matched':fault_detected(left,left_code,manifest['expectedNames'],'LFSR_MISMATCH') and
                              fault_detected(right,right_code,manifest['expectedNames'],'LFSR_MISMATCH'),
                    'negativeControl':True}
        else:
            report=compare(left,right,manifest['expectedNames'])
            a=metrics(load(work/'legacy/metrics.json'),stimulus);b=metrics(load(work/'native/metrics.json'),stimulus)
            report['traceMatched']=a==b
            report['matched']=report['matched'] and a==b and left_code==right_code==0
        report.update(schemaVersion=1,fixture=name,style=style,seed=seed,fault=fault,
                      revision=provenance['revision'],planSha256=digest,exitCodes={'legacy':left_code,'native':right_code})
        write_report(work/('negative-control.json' if fault else 'parity.json'),report)
        if not report['matched']:raise ValueError('LFSR parity qualification failed')
        return report
    except Exception as error:
        write_report(work/'case-failure.json',{'status':'failed','error':str(error),'kind':type(error).__name__})
        raise
    finally:lock.unlink(missing_ok=True)

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--fixture',choices=CASES,required=True)
    parser.add_argument('--style',choices=STYLES,default='REDUCTION')
    parser.add_argument('--seed',type=int,default=1)
    parser.add_argument('--fault',type=int,choices=(0,1,2),default=0)
    args=parser.parse_args()
    print(run_case(Path(__file__).resolve().parents[1],args.fixture,args.style,args.seed,args.fault))

if __name__=='__main__':main()
