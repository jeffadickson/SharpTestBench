"""Guard native machine identification and architecture-correct adapter imports."""
import json
from pathlib import Path
import struct
import sys
import tarfile
import tempfile
import unittest
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'tests/Tooling/managed_compat'))
from windows_arm64_check import pe_machine
from prepare_upstream import ROOT, configure_native_build


class WindowsArm64Tests(unittest.TestCase):
    def test_pe_discriminates_x64_from_arm64(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder)/'native.dll'
            for machine in (0xaa64, 0x8664):
                image = bytearray(128)
                image[:2] = b'MZ'
                struct.pack_into('<I', image, 0x3c, 64)
                image[64:68] = b'PE\0\0'
                struct.pack_into('<H', image, 68, machine)
                path.write_bytes(image)
                self.assertEqual(machine, pe_machine(path))
            path.write_bytes(b'not an executable')
            with self.assertRaises(ValueError): pe_machine(path)

    def test_overlay_applies_to_pinned_source(self):
        metadata = json.loads((ROOT/'third_party/UPSTREAM.json').read_text())
        with tempfile.TemporaryDirectory() as folder:
            source = Path(folder)/'source'; source.mkdir()
            with tarfile.open(ROOT/'third_party'/metadata['source_archive']) as archive:
                member = archive.getmember('cocotb-source/CMakeLists.txt')
                (source/'CMakeLists.txt').write_bytes(archive.extractfile(member).read())
            configure_native_build(source)
            cmake = (source/'CMakeLists.txt').read_text()
            self.assertIn('set(STB_IMPORT_MACHINE ARM64)', cmake)
            self.assertIn('/machine:${STB_IMPORT_MACHINE}', cmake)
            self.assertNotIn('/machine:$<IF:', cmake)
