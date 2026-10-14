"""Platform-aware native host build; C compiler must support GNU typeof (GCC/Clang)."""
import os
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
    compiler = os.environ.get('CC', 'clang' if MAC else 'gcc')
    flags = ['-shared', '-Wl,--export-all-symbols'] if WINDOWS else ['-dynamiclib', '-fPIC', '-Wl,-install_name,@rpath/' + HOST_NAME] if MAC else ['-shared', '-fPIC', '-Wl,-soname,' + HOST_NAME]
    try:
        subprocess.run([compiler, *flags, '-Wall', '-Wextra', '-Werror', str(root / 'native/host.c'), str(root / 'native/abi.c'), '-I', str(root / 'native/upstream'), '-I', str(headers), *([] if WINDOWS or MAC else ['-ldl']), '-o', str(temporary)], check=True)
        temporary.replace(build / HOST_NAME)
    finally:
        temporary.unlink(missing_ok=True)
    return build / HOST_NAME
