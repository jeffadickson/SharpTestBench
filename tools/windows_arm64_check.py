"""Reject emulated x64 binaries in the native Windows ARM64 validation job."""
import json
import os
from pathlib import Path
import shutil
import struct
import sys


def pe_machine(path):
    """Read the PE machine tag without loading or executing the binary."""
    with Path(path).open('rb') as stream:
        if stream.read(2) != b'MZ': raise ValueError(f'Not a PE image: {path}')
        stream.seek(0x3c)
        offset = struct.unpack('<I', stream.read(4))[0]
        stream.seek(offset)
        if stream.read(4) != b'PE\0\0': raise ValueError(f'Invalid PE signature: {path}')
        return struct.unpack('<H', stream.read(2))[0]


def main():
    """Check every in-process native component, plus compiler and runtime launcher."""
    from cocotb_tools import config
    from native_build import sdk_root
    root = Path(__file__).resolve().parents[1]
    libraries = [Path(config.gpi_entry_point().rsplit(',', 1)[0]),
                 Path(config.gpi_impl('icarus', 'vpi').rsplit(':', 1)[0]),
                 Path(config.lib_entry('vpi', 'icarus'))]
    paths = [Path(sys.executable), sdk_root()/'dotnet.exe',
             Path(shutil.which('vvp')), Path(os.environ['STB_IVL_ROOT'])/'ivl.exe',
             root/'build/sharptestbench_host.dll', *libraries]
    paths += list((sdk_root()/'host/fxr').glob('*/hostfxr.dll'))
    report = {str(p): hex(pe_machine(p)) for p in paths}
    if any(value != '0xaa64' for value in report.values()):
        raise RuntimeError('Expected ARM64 PE machine 0xaa64 throughout:\n' + json.dumps(report, indent=2))
    (root/'build/windows-arm64-binaries.json').write_text(json.dumps(report, indent=2)+'\n')
    print('ARM64_PE_PASS', len(report), 'native components')


if __name__ == '__main__': main()
