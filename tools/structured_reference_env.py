#!/usr/bin/env python3
"""Provision an isolated released-oracle environment, preserving native development adapters."""
from pathlib import Path
import os
import subprocess
import sys
import venv

root = Path(__file__).resolve().parents[1]
location = root / 'build/reference-python'
venv.EnvBuilder(with_pip=True).create(location)
python = location / ('Scripts/python.exe' if os.name == 'nt' else 'bin/python')
subprocess.run([str(python), '-m', 'pip', 'install', 'cocotb==2.1.0', 'find_libpython==0.5.1'], check=True)
subprocess.run([str(python), str(root / 'tools/structured_reference.py'), *sys.argv[1:]], check=True)
