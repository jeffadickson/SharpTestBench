#!/usr/bin/env python3
"""Compare fresh pinned-Python ordering traces with a preceding managed run.
Run tools/run.py for the same simulator first to elaborate the HDL and emit traces.
"""
import argparse
import os
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET
from cocotb_tools import config
from find_libpython import find_libpython

parser = argparse.ArgumentParser()
parser.add_argument('--sim', choices=['icarus', 'verilator', 'ghdl', 'nvc'], default='icarus')
parser.add_argument('--label', required=True)
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
build = root / 'build'
reference = build / (args.label + '-python-order')
xml = reference.with_suffix('.xml')
env = os.environ.copy()
env.update(GPI_IMPL=config.gpi_impl(args.sim, 'vhpi' if args.sim == 'nvc' else 'vpi'),
           PYTHONPATH=str(root / 'tests/Integration') + os.pathsep + os.pathsep.join(sys.path),
           PYGPI_PYTHON_BIN=sys.executable, COCOTB_TOPLEVEL='top',
           COCOTB_TEST_MODULES='test_timing_order', PARITY_TRACE=str(reference),
           TIMING_SIM=args.sim, COCOTB_RESULTS_FILE=str(xml))
env['COCOTB_BOOTSTRAP'] = os.pathsep.join([config.gpi_entry_point(), find_libpython(), config.pygpi_entry_point()])
bootstrap = str(config.libs_dir / 'libcocotb_bootstrap.so')
if args.sim == 'icarus':
    command = [env.get('CS_VVP', 'vvp'), '-m', bootstrap, str(build / 'top.vvp')]
elif args.sim == 'verilator':
    command = [str(build / 'verilator/top')]
elif args.sim == 'ghdl':
    command = [env.get('CS_GHDL', 'ghdl'), '-r', '--std=08', 'top', '--vpi=' + bootstrap]
else:
    command = [env.get('CS_NVC', 'nvc'), '--std=2008', '--work=work:' + str(build / 'nvc-work'),
               '-e', 'top', '--no-save', '--jit', '-r', '--load=' + bootstrap]
suffixes = ['.scheduled'] + (['.force'] if args.sim == 'icarus' else [])
xml.unlink(missing_ok=True)
for suffix in suffixes:
    Path(str(reference) + suffix).unlink(missing_ok=True)
result = subprocess.run(command, env=env, cwd=build, capture_output=True, text=True, timeout=120)
reference.with_suffix('.log').write_text(result.stdout + result.stderr)
assert result.returncode == 0, result.stdout + result.stderr
report = ET.parse(xml)
assert report.findall('.//testcase') and not report.findall('.//failure'), reference.with_suffix('.log')
for suffix in suffixes:
    expected = Path(str(reference) + suffix).read_text()
    actual = (build / (args.label + '-trace.txt' + suffix)).read_text()
    assert actual == expected, (args.sim, suffix, actual, expected)
print('TIMING_PARITY_PASS', args.sim, ','.join(suffixes))
