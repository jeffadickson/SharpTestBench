#!/usr/bin/env python3
"""Build/run the C# framework. Python is build glue only, not embedded in C# tests."""
import argparse,json,os,shutil,subprocess,sys
from pathlib import Path
from cocotb_tools import config
root=Path(__file__).resolve().parents[1]
def command(args,**kw):
 print('+',' '.join(map(str,args)),flush=True)
 subprocess.run(list(map(str,args)),check=True,**kw)
def main():
 parser=argparse.ArgumentParser();parser.add_argument('--host',type=Path,help='Use an existing native host library');parser.add_argument('--assembly',type=Path,help='Prebuilt test DLL with adjacent runtimeconfig.json');parser.add_argument('--source',type=Path,action='append');parser.add_argument('--top',default='top');parser.add_argument('--entry-type');parser.add_argument('--no-build',action='store_true');parser.add_argument('--project',default='Integration',choices=['Integration','Benchmark']);parser.add_argument('--filter');parser.add_argument('--label');parser.add_argument('--sim',choices=['icarus','verilator','ghdl','nvc'],default='icarus');args=parser.parse_args()
 build=root/'build';build.mkdir(exist_ok=True)
 label=args.label or args.sim
 if not label.replace('-','').replace('_','').isalnum():raise SystemExit('Label must be alphanumeric')
 sdk=Path(os.environ['CS_DOTNET_ROOT']) if 'CS_DOTNET_ROOT' in os.environ else Path(shutil.which('dotnet')).resolve().parent
 assembly='VhdlIntegration' if args.sim in ('ghdl','nvc') else args.project
 if not args.no_build:
  if not args.assembly:command([sdk/'dotnet','build',root/f'tests/{assembly}/{assembly}.csproj','-c','Release','--nologo'])
  headers=sorted((sdk/'packs/Microsoft.NETCore.App.Host.linux-x64').glob('*/runtimes/linux-x64/native'), key=lambda p: tuple(map(int, p.parents[2].name.split('-')[0].split('.'))))[-1]
  temporary=build/f'host-{os.getpid()}.so'
  try:
   command(['gcc','-shared','-fPIC','-Wall','-Wextra','-Werror','-Wl,-soname,libsharptestbench_host.so',root/'native/host.c',root/'native/abi.c','-I',root/'native/upstream','-I',headers,'-ldl','-o',temporary])
   temporary.replace(build/'libsharptestbench_host.so')
  finally:temporary.unlink(missing_ok=True)
 binary=root/f'tests/{assembly}/bin/Release/net10.0'
 if args.assembly:
  binary=args.assembly.resolve().parent;assembly=args.assembly.stem
 if not (binary/f'{assembly}.dll').is_file() or not (binary/f'{assembly}.runtimeconfig.json').is_file():raise SystemExit('Test DLL and runtimeconfig.json are required')
 sources=[p.resolve() for p in args.source] if args.source else [root/('tests/VhdlIntegration/top.vhd' if args.sim in ('ghdl','nvc') else 'tests/Integration/top.sv')]
 env=os.environ.copy();env.update(CS_HOSTFXR=str(max((sdk/'host/fxr').glob('*/libhostfxr.so'), key=lambda p: tuple(map(int, p.parent.name.split('-')[0].split('.'))))),CS_CONFIG=str(binary/f'{assembly}.runtimeconfig.json'),CS_ASSEMBLY=str(binary/f'{assembly}.dll'),CS_TOPLEVEL=args.top,CS_ENTRY_TYPE=args.entry_type or 'Entry, '+assembly,CS_RESULTS=str(build/(label+'-results.json')),CS_TRACE=str(build/(label+'-trace.txt')),CS_BINDINGS=str(build/'DutBindings.cs'),DOTNET_ROOT=str(sdk),LD_LIBRARY_PATH=str(args.host.resolve().parent if args.host else build)+os.pathsep+str(config.libs_dir)+os.pathsep+env.get('LD_LIBRARY_PATH',''))
 if args.filter:env['CS_TEST_FILTER']=args.filter
 env['GPI_IMPL']=subprocess.check_output([sys.executable,'-m','cocotb_tools.config','--gpi-impl',args.sim,'vhpi' if args.sim=='nvc' else 'vpi'],text=True).strip()
 env['COCOTB_BOOTSTRAP']=os.pathsep.join([config.gpi_entry_point(),str(args.host.resolve() if args.host else build/'libsharptestbench_host.so')+',sharptestbench_initialize'])
 if args.sim=='icarus':
  ivl=['-B',os.environ['CS_IVL_ROOT']] if 'CS_IVL_ROOT' in os.environ else []
  command([os.environ.get('CS_IVERILOG','iverilog'),*ivl,'-g2012','-s',args.top,'-o',build/'top.vvp',*sources])
  run=[os.environ.get('CS_VVP','vvp'),'-m',config.libs_dir/'libcocotb_bootstrap.so',build/'top.vvp']
 elif args.sim=='ghdl':
  ghdl=os.environ.get('CS_GHDL','ghdl')
  command([ghdl,'-a','--std=08',*sources],cwd=build,env=env)
  command([ghdl,'-e','--std=08',args.top],cwd=build,env=env)
  run=[ghdl,'-r','--std=08',args.top,'--vpi='+str(config.libs_dir/'libcocotb_bootstrap.so')]
 elif args.sim=='nvc':
  nvc=os.environ.get('CS_NVC','nvc')
  work='--work=work:'+str(build/'nvc-work')
  command([nvc,'--std=2008',work,'-a',*sources,'--preserve-case'],cwd=build,env=env)
  run=[nvc,'--std=2008',work,'-e',args.top,'--no-save','--jit','-r','--load='+str(config.libs_dir/'libcocotb_bootstrap.so')]
 else:
  from cocotb_tools.runner import get_runner
  # Build with upstream's simulator-specific driver; run it with the C# bootstrap.
  runner=get_runner('verilator');runner.build(sources=sources,hdl_toplevel=args.top,build_dir=build/'verilator',always=True,build_args=['--Wno-fatal','--timing'])
  run=[build/'verilator'/args.top]
 if env.get('CS_INJECT_EARLY')=='1' and args.sim in ('icarus','verilator'):run.append('+CS_EARLY_FINISH')
 result=Path(env['CS_RESULTS']);result.unlink(missing_ok=True)
 p=subprocess.run(list(map(str,run)),env=env,cwd=build,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,timeout=int(env.get('CS_WALL_TIMEOUT','120')))
 (build/(label+'-run.log')).write_text(p.stdout);print(p.stdout)
 if p.returncode or not result.exists():raise SystemExit('Simulator failed or produced no result file')
 rows=json.loads(result.read_text());failed=[r for r in rows if r['Status']=='failed']
 if failed:print(json.dumps(failed,indent=2));raise SystemExit(1)
 if not rows:raise SystemExit('No results')
if __name__=='__main__':main()
