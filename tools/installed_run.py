#!/usr/bin/env python3
"""Standalone installed-package Icarus runner; requires Python stdlib, .NET and Icarus."""
import argparse
import hashlib
import json
import os
import platform
from pathlib import Path
import shutil
import subprocess
import sys

p = argparse.ArgumentParser()
p.add_argument('--native-dir', type=Path, required=True)
p.add_argument('--assembly', type=Path, required=True)
p.add_argument('--source', type=Path, required=True)
p.add_argument('--top', required=True)
p.add_argument('--entry-type', required=True)
p.add_argument('--work-dir', type=Path, required=True)
a = p.parse_args()
native = a.native_dir.resolve()
manifest = json.loads((native / 'native-manifest.json').read_text())
arch={'x86_64':'x64','amd64':'x64','aarch64':'arm64','arm64':'arm64'}.get(platform.machine().lower())
rid=('win' if os.name=='nt' else 'osx' if sys.platform=='darwin' else 'linux')+'-'+str(arch)
if manifest.get('rid')!=rid: raise SystemExit('Native package RID mismatch: expected '+rid)
for field in ('host','gpi','implementation','bootstrap'):
    name=manifest[field]
    if Path(name).name!=name or name not in manifest['sha256']: raise SystemExit('Unverified native manifest reference: '+field)

for name, digest in manifest['sha256'].items():
    if Path(name).name != name or hashlib.sha256((native / name).read_bytes()).hexdigest() != digest:
        raise SystemExit('Native package checksum mismatch: ' + name)
work = a.work_dir.resolve()
work.mkdir(parents=True, exist_ok=True)
lock = work / '.sharptestbench-run.lock'
try: descriptor = os.open(lock, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
except FileExistsError: raise SystemExit('Workspace is already in use: ' + str(work))
os.close(descriptor)
try:
    sdk = Path(os.environ['CS_DOTNET_ROOT']) if 'CS_DOTNET_ROOT' in os.environ else Path(shutil.which('dotnet')).resolve().parent
    fxr_name = 'hostfxr.dll' if os.name == 'nt' else 'libhostfxr.dylib' if sys.platform == 'darwin' else 'libhostfxr.so'
    fxr = max((sdk / 'host/fxr').glob('*/' + fxr_name), key=lambda x: tuple(map(int, x.parent.name.split('-')[0].split('.'))))
    assembly = a.assembly.resolve()
    env = os.environ.copy()
    env.update(CS_HOSTFXR=str(fxr), CS_CONFIG=str(assembly.with_suffix('.runtimeconfig.json')),
               CS_ASSEMBLY=str(assembly), CS_ENTRY_TYPE=a.entry_type, CS_TOPLEVEL=a.top,
               CS_RESULTS=str(work / 'results.json'), CS_TRACE=str(work / 'trace.txt'), DOTNET_ROOT=str(sdk),
               CS_GPI_LIBRARY=str(native / manifest['gpi']),
               GPI_IMPL=str(native / manifest['implementation']) + ':' + manifest['implementation_entry'],
               COCOTB_BOOTSTRAP=os.pathsep.join([str(native / manifest['gpi']) + ',' + manifest['gpi_entry'],
                                               str(native / manifest['host']) + ',sharptestbench_initialize']))
    loader = 'PATH' if os.name == 'nt' else 'DYLD_LIBRARY_PATH' if sys.platform == 'darwin' else 'LD_LIBRARY_PATH'
    # Unix package checks must not fall back to an externally installed cocotb directory.
    env[loader] = str(native) + (os.pathsep + env.get(loader, '') if os.name=='nt' else '')
    ivl = ['-B', env['CS_IVL_ROOT']] if 'CS_IVL_ROOT' in env else []
    subprocess.run([env.get('CS_IVERILOG', 'iverilog'), *ivl, '-g2012', '-s', a.top, '-o', str(work / 'top.vvp'), str(a.source.resolve())], check=True)
    report = work / 'results.json'
    report.unlink(missing_ok=True)
    result = subprocess.run([env.get('CS_VVP', 'vvp'), '-m', str(native / manifest['bootstrap']), str(work / 'top.vvp')],
                            env=env, cwd=work, capture_output=True, text=True, timeout=120)
    (work / 'run.log').write_text(result.stdout + result.stderr)
    print(result.stdout + result.stderr)
    if result.returncode or not report.exists(): raise SystemExit('Installed simulator run failed')
    rows = json.loads(report.read_text())
    if not rows or any(r['Status'] != 'passed' for r in rows): raise SystemExit('Installed consumer tests failed')
finally:
    lock.unlink(missing_ok=True)
