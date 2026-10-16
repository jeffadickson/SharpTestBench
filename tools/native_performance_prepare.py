#!/usr/bin/env python3
"""Build an isolated pre-C++20 adapter installation for native_performance.py.

The implementation archive is checksum-verified. Only archive version metadata
is supplied; historical CMake language/optimization settings are left intact.
This does not replace the user's current cocotb installation.
"""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tarfile

ROOT = Path(__file__).resolve().parents[1]


def main():
    """Extract pinned sources and install their original build into a separate target."""
    metadata = json.loads((ROOT / 'third_party/UPSTREAM.json').read_text())
    archive = ROOT / 'third_party' / metadata['source_archive']
    if hashlib.sha256(archive.read_bytes()).hexdigest() != metadata['source_sha256']:
        raise RuntimeError('Pinned source archive checksum mismatch')
    work = ROOT / 'build/native-performance/baseline'
    target = ROOT / 'build/native-performance/baseline-python'
    if target.exists():
        raise RuntimeError('Baseline installation already exists; use it or select a fresh checkout')
    work.mkdir(parents=True, exist_ok=True)
    with tarfile.open(archive) as source: source.extractall(work, filter='data')
    project = work / 'cocotb-source/pyproject.toml'
    text = project.read_text()
    dynamic = 'dynamic = ["version"]'
    provider = '[tool.scikit-build.metadata.version]\nprovider = "setuptools_git_versioning.scikit_metadata"'
    if text.count(dynamic) != 1 or text.count(provider) != 1:
        raise RuntimeError('Pinned upstream packaging changed; review historical preparation')
    project.write_text(text.replace(dynamic, 'version = ' + json.dumps(metadata['version'])).replace(provider, ''))
    env = dict(os.environ, CC=os.environ.get('CC', 'gcc'), CXX=os.environ.get('CXX', 'g++'))
    env.setdefault('CMAKE_BUILD_PARALLEL_LEVEL', '4')
    subprocess.run([sys.executable, '-m', 'pip', 'install', '--no-deps', '--target', str(target), str(project.parent)],
                   env=env, check=True)
    print(target)


if __name__ == '__main__': main()
