import os,sys,subprocess,argparse
from pathlib import Path
from cocotb_tools import config
from find_libpython import find_libpython
parser=argparse.ArgumentParser();parser.add_argument('--label',default='icarus');args=parser.parse_args()
root=Path(__file__).resolve().parents[1];build=root/'build';env=os.environ.copy()
env.update(GPI_IMPL=config.gpi_impl('icarus','vpi'),PYTHONPATH=str(root/'tests/Integration')+os.pathsep+os.pathsep.join(sys.path),PYGPI_PYTHON_BIN=sys.executable,COCOTB_TOPLEVEL='top',COCOTB_TEST_MODULES='test_parity',PARITY_TRACE=str(build/'python-trace.txt'),COCOTB_RESULTS_FILE=str(build/'python-results.xml'))
env['COCOTB_BOOTSTRAP']=os.pathsep.join([config.gpi_entry_point(),find_libpython(),config.pygpi_entry_point()])
(build/'python-trace.txt').unlink(missing_ok=True)
p=subprocess.run([os.environ.get('CS_VVP','vvp'),'-m',str(config.libs_dir/'libcocotb_bootstrap.so'),str(build/'top.vvp')],env=env,cwd=build,capture_output=True,text=True)
(build/'python-parity.log').write_text(p.stdout+p.stderr)
assert p.returncode==0,p.stdout+p.stderr
import xml.etree.ElementTree as ET
assert not ET.parse(build/'python-results.xml').findall('.//failure')
assert (build/'python-trace.txt').read_text()==(build/(args.label+'-trace.txt')).read_text()
assert (build/'python-trace.txt.delta').read_text()==(build/(args.label+'-trace.txt.delta')).read_text()
print('PARITY_PASS edge/NBA/ReadOnly trace matches Python; deposit coalescing and 20 multi-NBA chain transitions checked')
