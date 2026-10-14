import json,os,re,statistics,subprocess,sys
from pathlib import Path
from cocotb_tools import config
from find_libpython import find_libpython
root=Path(__file__).resolve().parents[1];build=root/'build';env=os.environ.copy();env['DOTNET_TieredCompilation']='0'
subprocess.run([sys.executable,str(root/'tools/run.py'),'--project','Benchmark','--label','benchmark-warm'],env=env,check=True,stdout=subprocess.DEVNULL)
records=[]
for rep in range(5):
 for language in (['python','csharp'] if rep%2==0 else ['csharp','python']):
  if language=='csharp':
   label=f'benchmark-csharp-{rep}'
   p=subprocess.run([sys.executable,str(root/'tools/run.py'),'--project','Benchmark','--no-build','--label',label],env=env,capture_output=True,text=True)
   text=(build/(label+'-run.log')).read_text()
  else:
   e=env.copy();e.update(GPI_IMPL=config.gpi_impl('icarus','vpi'),PYTHONPATH=str(root/'tests/Benchmark')+os.pathsep+os.pathsep.join(sys.path),PYGPI_PYTHON_BIN=sys.executable,COCOTB_TOPLEVEL='top',COCOTB_TEST_MODULES='python_bench',COCOTB_RESULTS_FILE=str(build/f'benchmark-python-{rep}.xml'))
   e.pop('COCOTB_TRUST_INERTIAL_WRITES',None) # Both implementations use deferred deposits.
   e['COCOTB_BOOTSTRAP']=os.pathsep.join([config.gpi_entry_point(),find_libpython(),config.pygpi_entry_point()])
   p=subprocess.run([e.get('CS_VVP','vvp'),'-m',str(config.libs_dir/'libcocotb_bootstrap.so'),str(build/'top.vvp')],env=e,cwd=build,capture_output=True,text=True)
   text=p.stdout+p.stderr;(build/f'benchmark-python-{rep}.log').write_text(text)
   import xml.etree.ElementTree as ET
   assert not ET.parse(build/f'benchmark-python-{rep}.xml').findall('.//failure')
  assert p.returncode==0,p.stdout+p.stderr
  assert 'checksum=694140833' in text,text
  record={'language':language,'repeat':rep,**{k:float(v)for k,v in re.findall(r'MEASURE (\w+) ([\d.eE+-]+)',text)}}
  assert 'timer' in record and 'rw32' in record;records.append(record)
summary={k:{language:statistics.median(x[k] for x in records if x['language']==language)for language in ('python','csharp')}for k in ('timer','rw32')}
for value in summary.values():value['python_over_csharp']=value['python']/value['csharp']
(build/'benchmark.json').write_text(json.dumps({'records':records,'summary':summary},indent=2));print(json.dumps(summary,indent=2))
