#!/usr/bin/env python3
"""Concurrent success/failure processes with independent artifacts and lock rejection."""
import json
import os
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
base = root / 'build/workspace-check'
base.mkdir(exist_ok=True)
processes = []
for name, mode in [('success', 'normal'), ('failure', 'both')]:
    work = base / name
    work.mkdir(exist_ok=True)
    env = os.environ.copy()
    env['CS_CLEANUP_MODE'] = mode
    log = (work / 'driver.log').open('w')
    command = [sys.executable, str(root / 'tools/run.py'), '--sim', 'icarus', '--no-build',
               '--work-dir', str(work), '--label', 'same-label', '--filter', 'Cleanup_A_Run']
    processes.append((name, work, log, command, subprocess.Popen(command, env=env, stdout=log, stderr=subprocess.STDOUT)))
for name, work, log, command, process in processes:
    try: status = process.wait(timeout=120)
    finally: log.close()
    rows = json.loads((work / 'same-label-results.json').read_text())
    assert len(rows) == 1 and rows[0]['Status'] == ('passed' if name == 'success' else 'failed'), rows
    assert (status == 0) == (name == 'success'), status
    assert (work / 'top.vvp').exists() and not (work / '.sharptestbench-run.lock').exists()
work = base / 'success'
lock = work / '.sharptestbench-run.lock'
lock.write_text('occupied-test')
try:
    result = subprocess.run(processes[0][3], capture_output=True, text=True, timeout=10)
    assert result.returncode != 0 and 'already in use' in result.stderr, result
finally:
    lock.unlink()
print('WORKSPACE_PASS concurrent success/failure isolation and occupied-directory rejection')
