#!/usr/bin/env python3
"""Run with an isolated Python environment containing cocotb==2.1.0.
Compare a preceding SharpTestBench Icarus run's structured trace with released Python semantics.
"""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
import cocotb
from cocotb_tools import config
from find_libpython import find_libpython

parser = argparse.ArgumentParser()
parser.add_argument('--label', required=True)
args = parser.parse_args()
if cocotb.__version__ != '2.1.0':
    raise SystemExit('Run in a separate environment containing cocotb==2.1.0')
root = Path(__file__).resolve().parents[1]
build = root / 'build'
report = build / 'structured-python.xml'
trace = build / 'structured-python.json'
report.unlink(missing_ok=True)
trace.unlink(missing_ok=True)
env = os.environ.copy()
# Do not inherit the C# bootstrap or development-version adapter configuration.
for name in ('COCOTB_BOOTSTRAP', 'GPI_IMPL', 'GPI_EXTRA', 'COCOTB_TEST_FILTER', 'COCOTB_TESTCASE'):
    env.pop(name, None)
env.update(PYTHONPATH=str(root / 'tests/Integration'), PYGPI_PYTHON_BIN=sys.executable,
           LIBPYTHON_LOC=find_libpython(), GPI_USERS=';'.join([find_libpython(), config.pygpi_entry_point()]), COCOTB_TOPLEVEL='top',
           COCOTB_TEST_MODULES='test_structured_reference', COCOTB_RESULTS_FILE=str(report),
           STRUCTURED_TRACE=str(trace))
bootstrap = Path(config.lib_name_path('vpi', 'icarus'))
if sys.platform == 'win32':
    staged = build / 'cocotb21-reference.vpi'
    shutil.copy2(bootstrap, staged)
    bootstrap = staged
    env['PATH'] = str(config.libs_dir) + os.pathsep + env.get('PATH', '')
result = subprocess.run([env.get('CS_VVP', 'vvp'), '-m', str(bootstrap), str(build / 'top.vvp')],
                        cwd=build, env=env, capture_output=True, text=True, timeout=120)
(build / 'structured-python.log').write_text(result.stdout + result.stderr)
if result.returncode or not report.exists():
    raise SystemExit(result.stdout + result.stderr)
document = ET.parse(report)
if len(document.findall('.//testcase')) != 1 or document.findall('.//failure') or document.findall('.//error'):
    raise SystemExit(result.stdout + result.stderr)
expected = json.loads(trace.read_text())
actual = json.loads((build / (args.label + '-trace.txt.structured.json')).read_text())
# cocotb 2.1 rejects yielding again during cancellation. Its waiter can finish
# while asynchronous finally work has not completed. Assert both sides explicitly;
# do not normalize that difference away or call these cases equivalent.
differences = {
    'selection': ([0, 11, 5, 1], [0, 11, 2, 0]),
    'failure_after_cleanup': (5, 2),
    'parent_cleanup': (4, 2),
    'cleanup_failure': ('propagated', 'ignored'),
}
for key, (managed, python) in differences.items():
    if actual.get(key) != managed or expected.get(key) != python:
        raise SystemExit(f'Changed cancellation contract for {key}: C#={actual}; cocotb 2.1={expected}')
if {k: v for k, v in actual.items() if k not in differences} != {k: v for k, v in expected.items() if k not in differences}:
    raise SystemExit(f'Structured trace mismatch: C#={actual}; cocotb 2.1={expected}')
(build / 'structured-comparison.json').write_text(json.dumps({'cocotb_version': cocotb.__version__,
    'managed': actual, 'python': expected, 'intentional_differences': list(differences)}, indent=2))
print('STRUCTURED_REFERENCE_PASS cocotb 2.1.0', json.dumps(expected, sort_keys=True))
