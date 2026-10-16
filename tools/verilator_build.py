"""Build a reusable Verilator model with the portable C++20 release profile."""
import os
import hashlib
import json
import platform
from pathlib import Path
import subprocess


def build_model(sources, top, directory, waves=False):
    """Compile once; callers may reuse this exact executable in fresh processes.

    Keep model compilation outside measured simulator execution. The runtime
    bridge and managed test assembly are selected independently at process start.
    No native CPU tuning or fast-math is used, so HDL real semantics and portable
    instruction selection remain intact.
    """
    from cocotb_tools.runner import get_runner
    compiler = os.environ.get('CXX', 'c++')
    version = subprocess.check_output([compiler, '--version'], text=True)
    vector = ('-fvectorize -fslp-vectorize' if 'clang' in version.lower()
              else '-ftree-loop-vectorize -ftree-slp-vectorize')
    # The Python runner invokes make itself, so Verilator's -MAKEFLAGS does
    # not reach it. User CFLAGS appear after OPT_FAST/OPT_GLOBAL (-Os); place
    # -O3 here so it is the effective optimization level for every object.
    flags = ['--Wno-fatal', '--timing', '-CFLAGS',
             '-std=c++20 -O3 -flto ' + vector, '-LDFLAGS', '-O3 -flto']
    runner = get_runner('verilator')
    runner.build(sources=sources, hdl_toplevel=top, build_dir=directory,
                 always=True, waves=waves,
                 build_args=flags)
    executable = Path(directory) / top
    manifest = {'architecture': platform.machine(), 'platform': platform.system(), 'compiler': version.splitlines()[0], 'build_args': flags,
                'simulator': subprocess.check_output(['verilator', '--version'], text=True).strip(),
                'waves': waves, 'top': top,
                'sources': {str(p): hashlib.sha256(Path(p).read_bytes()).hexdigest() for p in sources},
                'executable_sha256': hashlib.sha256(executable.read_bytes()).hexdigest()}
    (Path(directory) / 'model-build.json').write_text(json.dumps(manifest, indent=2) + '\n')
    return executable
