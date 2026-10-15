"""Portable C++20 release build with mandatory LTO and retained debugger symbols."""
import os
import hashlib
import json
from pathlib import Path
import platform
import shutil
import subprocess
import sys

WINDOWS = sys.platform == 'win32'
MAC = sys.platform == 'darwin'
HOST_NAME = 'sharptestbench_host.dll' if WINDOWS else 'libsharptestbench_host.dylib' if MAC else 'libsharptestbench_host.so'
FXR_NAME = 'hostfxr.dll' if WINDOWS else 'libhostfxr.dylib' if MAC else 'libhostfxr.so'
LOADER_PATH = 'PATH' if WINDOWS else 'DYLD_LIBRARY_PATH' if MAC else 'LD_LIBRARY_PATH'


def sdk_root():
    return Path(os.environ['CS_DOTNET_ROOT']) if 'CS_DOTNET_ROOT' in os.environ else Path(shutil.which('dotnet')).resolve().parent


def build_host(root, sdk, build):
    arch = {'amd64': 'x64', 'x86_64': 'x64', 'arm64': 'arm64', 'aarch64': 'arm64'}.get(platform.machine().lower())
    if arch is None: raise RuntimeError('Unsupported native architecture: ' + platform.machine())
    rid = ('win' if WINDOWS else 'osx' if MAC else 'linux') + '-' + arch
    paths = list((sdk / ('packs/Microsoft.NETCore.App.Host.' + rid)).glob('*/runtimes/' + rid + '/native'))
    if not paths: raise RuntimeError('Missing .NET native host headers for ' + rid)
    headers = max(paths, key=lambda p: tuple(map(int, p.parents[2].name.split('-')[0].split('.'))))
    temporary = build / (str(os.getpid()) + '-' + HOST_NAME)
    compiler = os.environ.get('CXX', 'clang++' if MAC else 'g++')
    flags = ['-shared', '-static-libgcc', '-static-libstdc++'] if WINDOWS else ['-dynamiclib', '-fPIC', '-Wl,-install_name,@rpath/' + HOST_NAME] if MAC else ['-shared', '-fPIC', '-Wl,-soname,' + HOST_NAME]
    # No fast-math or host-specific ISA: preserve HDL real values and portable packages.
    profile = ['-std=c++20', '-O3', '-flto', '-g', '-DNDEBUG', '-DSTB_HOST_BUILD', '-fvisibility=hidden']
    sources = [root / 'native/host.cpp', root / 'native/abi.cpp']
    command = [compiler, *flags, *profile, '-Wall', '-Wextra', '-Werror', *map(str, sources),
               '-I', str(root / 'native/upstream'), '-I', str(headers),
               *([] if WINDOWS or MAC else ['-ldl']), '-o', str(temporary)]
    try:
        subprocess.run(command, check=True)
        temporary.replace(build / HOST_NAME)
    finally:
        temporary.unlink(missing_ok=True)
    output = build / HOST_NAME
    manifest = {'rid': rid, 'language': 'C++20', 'profile': profile, 'command': command,
                'compiler': subprocess.check_output([compiler, '--version'], text=True).splitlines()[0],
                'sources': {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest()
                            for p in [*sources, root / 'native/abi.h', root / 'native/platform.h']},
                'output_sha256': hashlib.sha256(output.read_bytes()).hexdigest()}
    (build / 'native-build.json').write_text(json.dumps(manifest, indent=2) + '\n')
    return output
