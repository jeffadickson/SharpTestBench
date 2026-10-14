#!/usr/bin/env python3
"""Failure artifact retention and disabled capture in fresh Icarus processes."""
import json
import os
from pathlib import Path
import subprocess
import sys
from wave_check import check

root = Path(__file__).resolve().parents[1]
build = root / 'build'
env = os.environ.copy()
env['CS_INJECT_FAILURE'] = '1'
label = 'wave-failure'
command = [sys.executable, str(root / 'tools/run.py'), '--no-build', '--exact', 'P_ExitStatus',
           '--label', label, '--waves', str(build / 'failure trace.vcd')]
result = subprocess.run(command, env=env, capture_output=True, text=True, timeout=120)
(build / 'wave-failure-driver.log').write_text(result.stdout + result.stderr)
assert result.returncode == 1
check(label, known_values=False)
rows = json.loads((build / (label + '-results.json')).read_text())
assert len(rows) == 1 and rows[0]['Status'] == 'failed'
# Reusing the same label without --waves must not attach the old artifact.
env.pop('CS_INJECT_FAILURE')
result = subprocess.run(command[:-2], env=env, capture_output=True, text=True, timeout=120)
(build / 'wave-disabled-driver.log').write_text(result.stdout + result.stderr)
assert result.returncode == 0
assert not (build / (label + '-artifacts.json')).exists()
rows = json.loads((build / (label + '-results.json')).read_text())
assert all('Artifacts' not in row for row in rows)
print('WAVE_PASS failure retention; path with spaces; disabled capture has no stale artifact links')
