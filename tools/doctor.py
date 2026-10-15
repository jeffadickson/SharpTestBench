#!/usr/bin/env python3
"""Read-only prerequisite checks for building and running the Icarus alpha example."""
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
checks = []


def check(name, action):
    try:
        detail = action()
        checks.append({'name': name, 'ok': True, 'detail': str(detail)})
    except Exception as error:
        checks.append({'name': name, 'ok': False, 'detail': str(error)})


def executable(name, override=None):
    path = os.environ.get(override, '') if override else ''
    path = path or shutil.which(name)
    if not path or not Path(path).is_file(): raise RuntimeError('Missing executable: ' + name)
    return path


def dotnet():
    from native_build import sdk_root
    sdk = sdk_root()
    version = subprocess.check_output([str(sdk / ('dotnet.exe' if os.name == 'nt' else 'dotnet')), '--version'], cwd=ROOT, text=True).strip()
    expected = json.loads((ROOT / 'global.json').read_text())['sdk']['version']
    if version != expected: raise RuntimeError(f'Expected SDK {expected}, found {version}')
    return version


def adapters():
    from cocotb_tools import config
    entry = config.gpi_entry_point()
    for path in [entry.rsplit(',', 1)[0], config.lib_entry('vpi', 'icarus')]:
        if not Path(path).is_file(): raise RuntimeError('Missing adapter: ' + path)
    return entry


check('dotnet SDK', dotnet)
check('C++20 compiler', lambda: executable('clang++' if sys.platform == 'darwin' else 'g++', 'CXX'))
check('Icarus compiler', lambda: executable('iverilog', 'CS_IVERILOG'))
check('Icarus runtime', lambda: executable('vvp', 'CS_VVP'))
check('native adapters', adapters)
print(json.dumps({'platform': platform.platform(), 'architecture': platform.machine(), 'checks': checks}, indent=2))
sys.exit(0 if all(c['ok'] for c in checks) else 1)
