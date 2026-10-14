#!/usr/bin/env python3
"""Build/run the C# framework. Python is build glue only, not embedded in C# tests."""
import argparse,json,os,re,shutil,subprocess,sys
import xml.etree.ElementTree as ET
from pathlib import Path
from cocotb_tools import config
from native_build import build_host, sdk_root, HOST_NAME, FXR_NAME, LOADER_PATH
root=Path(__file__).resolve().parents[1]
def command(args,**kw):
 print('+',' '.join(map(str,args)),flush=True)
 subprocess.run(list(map(str,args)),check=True,**kw)
def main():
 parser=argparse.ArgumentParser();parser.add_argument('--waves',type=Path,help='Enable run-wide VCD output at this path');parser.add_argument('--host',type=Path,help='Use an existing native host library');parser.add_argument('--assembly',type=Path,help='Prebuilt test DLL with adjacent runtimeconfig.json');parser.add_argument('--source',type=Path,action='append');parser.add_argument('--top',default='top');parser.add_argument('--entry-type');parser.add_argument('--no-build',action='store_true');parser.add_argument('--project',default='Integration',choices=['Integration','Benchmark']);parser.add_argument('--stage',type=int,action='append',help='Run selected regression stages (repeatable)');parser.add_argument('--stage-failure-policy',choices=['continue','stop-after-stage']);parser.add_argument('--log-json',type=Path,help='Write structured JSON Lines logs to this path');parser.add_argument('--list',action='store_true',help='List selected test metadata without executing test bodies');parser.add_argument('--exact',action='append',help='Select an exact short or qualified case name (repeatable)');parser.add_argument('--seed',type=int);parser.add_argument('--filter');parser.add_argument('--label');parser.add_argument('--sim',choices=['icarus','verilator','ghdl','nvc'],default='icarus');args=parser.parse_args()
 build=root/'build';build.mkdir(exist_ok=True)
 wave=args.waves.resolve() if args.waves else None
 if wave:
  if wave.suffix.lower()!='.vcd':parser.error('--waves requires a .vcd path')
  if args.list or os.environ.get('CS_LIST_TESTS')=='1':parser.error('--waves cannot be combined with discovery-only listing')
  if any(ord(c)<32 or ord(c)==127 for c in str(wave)):parser.error('Waveform path contains control characters')
  wave.parent.mkdir(parents=True,exist_ok=True)
  wave.unlink(missing_ok=True)
 label=args.label or args.sim
 if not label.replace('-','').replace('_','').isalnum():raise SystemExit('Label must be alphanumeric')
 (build/(label+'-artifacts.json')).unlink(missing_ok=True)
 sdk=sdk_root()
 assembly='VhdlIntegration' if args.sim in ('ghdl','nvc') else args.project
 if not args.no_build:
  if not args.assembly:command([sdk/'dotnet','build',root/f'tests/{assembly}/{assembly}.csproj','-c','Release','--nologo'])
  build_host(root,sdk,build)
 binary=root/f'tests/{assembly}/bin/Release/net10.0'
 if args.assembly:
  binary=args.assembly.resolve().parent;assembly=args.assembly.stem
 if not (binary/f'{assembly}.dll').is_file() or not (binary/f'{assembly}.runtimeconfig.json').is_file():raise SystemExit('Test DLL and runtimeconfig.json are required')
 sources=[p.resolve() for p in args.source] if args.source else [root/('tests/VhdlIntegration/top.vhd' if args.sim in ('ghdl','nvc') else 'tests/Integration/top.sv')]
 env=os.environ.copy();env.update(CS_HOSTFXR=str(max((sdk/'host/fxr').glob('*/'+FXR_NAME), key=lambda p: tuple(map(int, p.parent.name.split('-')[0].split('.'))))),CS_CONFIG=str(binary/f'{assembly}.runtimeconfig.json'),CS_ASSEMBLY=str(binary/f'{assembly}.dll'),CS_TOPLEVEL=args.top,CS_ENTRY_TYPE=args.entry_type or 'Entry, '+assembly,CS_RESULTS=str(build/(label+'-results.json')),CS_TRACE=str(build/(label+'-trace.txt')),CS_BINDINGS=str(build/'DutBindings.cs'),DOTNET_ROOT=str(sdk),LD_LIBRARY_PATH=str(args.host.resolve().parent if args.host else build)+os.pathsep+str(config.libs_dir)+os.pathsep+env.get('LD_LIBRARY_PATH',''))
 env['CS_GPI_LIBRARY']=config.gpi_entry_point().rsplit(',',1)[0]
 env[LOADER_PATH]=str(args.host.resolve().parent if args.host else build)+os.pathsep+str(config.libs_dir)+os.pathsep+env.get(LOADER_PATH,'')
 if args.filter:env['CS_TEST_FILTER']=args.filter
 if args.list:env['CS_LIST_TESTS']='1'
 if args.log_json:env['CS_LOG_JSON']=str(args.log_json.resolve())
 if args.exact is not None:env['CS_TEST_NAMES']=json.dumps(args.exact)
 if args.seed is not None:env['CS_SEED']=str(args.seed)
 if args.stage is not None:env['CS_TEST_STAGES']=','.join(map(str,args.stage))
 if args.stage_failure_policy:env['CS_STAGE_FAILURE_POLICY']=args.stage_failure_policy
 env['GPI_IMPL']=subprocess.check_output([sys.executable,'-m','cocotb_tools.config','--gpi-impl',args.sim,'vhpi' if args.sim=='nvc' else 'vpi'],text=True).strip()
 env['COCOTB_BOOTSTRAP']=os.pathsep.join([config.gpi_entry_point(),str(args.host.resolve() if args.host else build/HOST_NAME)+',sharptestbench_initialize'])
 if args.sim=='icarus':
  ivl=['-B',os.environ['CS_IVL_ROOT']] if 'CS_IVL_ROOT' in os.environ else []
  wave_options=[]
  if wave:
   if not re.fullmatch(r'[A-Za-z_][A-Za-z0-9_$]*',args.top):parser.error('Icarus wave capture requires a simple top-level identifier')
   helper=build/(label+'-wave-dump.sv')
   literal=str(wave).replace('\\','\\\\').replace('"','\\"')
   helper.write_text('module __sharptestbench_wave_dump; initial if ($test$plusargs("SHARPTESTBENCH_WAVES")) begin $dumpfile("'+literal+'"); $dumpvars(0,'+args.top+'); end endmodule\n')
   wave_options=['-s','__sharptestbench_wave_dump',helper]
  command([os.environ.get('CS_IVERILOG','iverilog'),*ivl,'-g2012','-s',args.top,'-o',build/'top.vvp',*wave_options,*sources])
  run=[os.environ.get('CS_VVP','vvp'),'-m',config.lib_entry('vpi','icarus'),build/'top.vvp']+(['+SHARPTESTBENCH_WAVES'] if wave else [])
 elif args.sim=='ghdl':
  ghdl=os.environ.get('CS_GHDL','ghdl')
  command([ghdl,'-a','--std=08',*sources],cwd=build,env=env)
  command([ghdl,'-e','--std=08',args.top],cwd=build,env=env)
  run=[ghdl,'-r','--std=08',args.top,'--vpi='+str(config.lib_entry('vpi','ghdl'))]+(['--vcd='+str(wave)] if wave else [])
 elif args.sim=='nvc':
  nvc=os.environ.get('CS_NVC','nvc')
  work='--work=work:'+str(build/'nvc-work')
  command([nvc,'--std=2008',work,'-a',*sources,'--preserve-case'],cwd=build,env=env)
  run=[nvc,'--std=2008',work,'-e',args.top,'--no-save','--jit','-r','--load='+str(config.lib_entry('vhpi','nvc'))]+(['--wave='+str(wave),'--format=vcd'] if wave else [])
 else:
  from cocotb_tools.runner import get_runner
  # Build with upstream's simulator-specific driver; run it with the C# bootstrap.
  runner=get_runner('verilator');runner.build(sources=sources,hdl_toplevel=args.top,build_dir=build/'verilator',always=True,waves=bool(wave),build_args=['--Wno-fatal','--timing'])
  run=[build/'verilator'/args.top]+(['--trace','--trace-file',str(wave)] if wave else [])
 if env.get('CS_INJECT_EARLY')=='1' and args.sim in ('icarus','verilator'):run.append('+CS_EARLY_FINISH')
 result=Path(env['CS_RESULTS']);result.unlink(missing_ok=True)
 p=subprocess.run(list(map(str,run)),env=env,cwd=build,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,timeout=int(env.get('CS_WALL_TIMEOUT','120')))
 (build/(label+'-run.log')).write_text(p.stdout);print(p.stdout)
 if wave:
  artifact={'Kind':'waveform','Format':'vcd','Path':os.path.relpath(wave,result.parent),'Exists':wave.is_file(),'Bytes':wave.stat().st_size if wave.is_file() else 0}
  (build/(label+'-artifacts.json')).write_text(json.dumps({'SchemaVersion':1,'Simulator':args.sim,'Results':result.name,'Artifacts':[artifact]},indent=2))
  if result.exists():
   reported=json.loads(result.read_text())
   for row in reported:row['Artifacts']=[artifact]
   result.write_text(json.dumps(reported,indent=2))
   junit=result.with_suffix('.xml')
   if junit.exists():
    document=ET.parse(junit)
    for case in document.findall('.//testcase'):
     props=case.find('properties')
     if props is None:props=ET.SubElement(case,'properties')
     ET.SubElement(props,'property',name='waveform',value=artifact['Path'])
    document.write(junit,encoding='unicode',xml_declaration=True)
  if not artifact['Exists'] or artifact['Bytes']==0:raise SystemExit('Requested waveform was not generated or is empty')
 if p.returncode or not result.exists():raise SystemExit('Simulator failed or produced no result file')
 rows=json.loads(result.read_text());failed=[r for r in rows if r['Status']=='failed']
 if failed:print(json.dumps(failed,indent=2));raise SystemExit(1)
 if args.list or env.get('CS_LIST_TESTS')=='1':
  print(result.with_suffix('.discovery.json').read_text());return
 if not rows:raise SystemExit('No results')
if __name__=='__main__':main()
