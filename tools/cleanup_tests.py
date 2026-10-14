#!/usr/bin/env python3
"""Real simulator lifecycle tests, including intentionally failed and hung cleanup."""
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
for mode in ['normal', 'both', 'timeout', 'cleanup-timeout', 'cancel-error', 'hang']:
    label = 'cleanup-' + mode
    result = subprocess.run([sys.executable, str(ROOT / 'tools/run.py'), '--sim', 'icarus', '--no-build', '--filter', 'Cleanup_', '--label', label],
        env=dict(os.environ, CS_CLEANUP_MODE=mode), capture_output=True, text=True, timeout=60)
    (ROOT / 'build' / (label + '-driver.log')).write_text(result.stdout + result.stderr)
    rows = json.loads((ROOT / 'build' / (label + '-results.json')).read_text())
    assert [r['Name'] for r in rows] == (['Cleanup_A_Run'] if mode == 'hang' else ['Cleanup_A_Run', 'Cleanup_B_Verify']), rows
    assert rows[0]['Status'] == ('passed' if mode == 'normal' else 'failed'), rows
    assert (result.returncode == 0) == (mode == 'normal')
    if mode != 'hang': assert rows[1]['Status'] == 'passed', rows
    error = rows[0].get('Error') or ''
    if mode == 'both': assert 'body marker' in error and 'cleanup marker' in error, rows
    if mode == 'timeout': assert 'exceeded 10' in error, rows
    if mode == 'cleanup-timeout': assert 'cleanup timed out' in error, rows
    if mode == 'cancel-error': assert 'cancellation marker' in error, rows
    if mode == 'hang': assert 'regression aborted' in error, rows
    print('CLEANUP_PASS', mode)
