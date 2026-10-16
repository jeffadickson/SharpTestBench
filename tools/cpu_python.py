#!/usr/bin/env python3
"""Run only inside the pinned released-cocotb oracle venv."""
import argparse, os, shutil, subprocess, sys
from pathlib import Path
import xml.etree.ElementTree as ET
import cocotb
from cocotb_tools import config
from find_libpython import find_libpython
assert cocotb.__version__=='2.1.0'
p=argparse.ArgumentParser();p.add_argument('--work-dir',type=Path,required=True);a=p.parse_args()
root=Path(__file__).resolve().parents[1];work=a.work_dir.resolve();work.mkdir(parents=True,exist_ok=True)
env=os.environ.copy()
for name in ('COCOTB_BOOTSTRAP','GPI_IMPL','GPI_EXTRA','COCOTB_TEST_FILTER','COCOTB_TESTCASE'):env.pop(name,None)
report=work/'results.xml';report.unlink(missing_ok=True)
env.update(PYTHONPATH=str(root/'examples/CpuCore'),PYGPI_PYTHON_BIN=sys.executable,LIBPYTHON_LOC=find_libpython(),
 GPI_USERS=';'.join([find_libpython(),config.pygpi_entry_point()]),COCOTB_TOPLEVEL='cpu_core',COCOTB_TEST_MODULES='test_cpu',COCOTB_RESULTS_FILE=str(report))
bootstrap=Path(config.lib_name_path('vpi','icarus'))
if sys.platform=='win32':
 staged=bootstrap.with_suffix('.vpi');shutil.copy2(bootstrap,staged);bootstrap=staged
 env['PATH']=str(config.libs_dir)+os.pathsep+env.get('PATH','')
ivl=['-B',env['CS_IVL_ROOT']] if 'CS_IVL_ROOT' in env else []
subprocess.run([env.get('CS_IVERILOG','iverilog'),*ivl,'-g2012','-s','cpu_core','-o',str(work/'top.vvp'),str(root/'examples/CpuCore/cpu_core.sv')],check=True)
r=subprocess.run([env.get('CS_VVP','vvp'),'-m',str(bootstrap),str(work/'top.vvp')],cwd=work,env=env,text=True,capture_output=True,timeout=150)
print(r.stdout+r.stderr)
if r.returncode or not report.exists():raise SystemExit(1)
x=ET.parse(report)
if len(x.findall('.//testcase'))!=1 or x.findall('.//failure') or x.findall('.//error'):raise SystemExit(1)
