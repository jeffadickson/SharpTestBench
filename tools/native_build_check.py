#!/usr/bin/env python3
"""Check the actual optimized binary's C ABI, including linkage from a C11 caller."""
import ctypes
import json
import os
from pathlib import Path
import re
import subprocess
import sys
from native_build import HOST_NAME, MAC, WINDOWS

ROOT = Path(__file__).resolve().parents[1]
BUILD = ROOT / 'build'


def main():
    host = BUILD / HOST_NAME
    if WINDOWS:
        imports = re.findall(r'DLL Name:\s*(\S+)', subprocess.check_output(['objdump', '-p', str(host)], text=True))
        print('NATIVE_IMPORTS', json.dumps(imports), flush=True)
        unexpected = [name for name in imports if name.lower().startswith(('libwinpthread', 'libstdc++', 'libgcc'))]
        if unexpected: raise RuntimeError('Compiler runtime must be statically linked: ' + ', '.join(unexpected))
    library = ctypes.CDLL(str(host))
    header = (ROOT / 'native/abi.h').read_text()
    exports = re.findall(r'^CS_EXPORT .*?\b(cs_\w+)\(', header, re.M)
    if len(exports) < 40:
        raise RuntimeError('ABI declaration scan missed exports')
    for name in [*exports, 'sharptestbench_initialize']:
        getattr(library, name)  # Literal lookup detects C++ mangling or hidden exports.
    library.cs_abi_version.restype = ctypes.c_uint32
    library.cs_abi_features.restype = ctypes.c_uint64
    assert library.cs_abi_version() == 1 and library.cs_abi_features() == 31
    folder = BUILD / 'native-build-check'
    folder.mkdir(exist_ok=True)
    source = folder / 'consumer.c'
    source.write_text('#include "abi.h"\nint main(void) { return cs_abi_version() != 1 || cs_abi_features() != 31; }\n')
    executable = folder / ('consumer.exe' if WINDOWS else 'consumer')
    cc = os.environ.get('CC', 'clang' if MAC else 'gcc')
    subprocess.run([cc, '-std=c11', '-Wall', '-Wextra', '-Werror', '-I', str(ROOT / 'native'),
                    str(source), str(host), '-o', str(executable)], check=True)
    env = dict(os.environ)
    loader = 'PATH' if WINDOWS else 'DYLD_LIBRARY_PATH' if MAC else 'LD_LIBRARY_PATH'
    env[loader] = str(BUILD) + os.pathsep + env.get(loader, '')
    subprocess.run([str(executable)], env=env, check=True)
    result = {'exports_resolved': len(exports) + 1, 'c11_consumer': 'passed',
              'build': json.loads((BUILD / 'native-build.json').read_text())}
    (BUILD / 'native-build-check.json').write_text(json.dumps(result, indent=2) + '\n')
    print(f'PASS: {len(exports) + 1} unmangled exports; C11 consumer links and runs against optimized C++20 host')


if __name__ == '__main__':
    main()
