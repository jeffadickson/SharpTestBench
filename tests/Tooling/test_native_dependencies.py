import importlib.util, tempfile, unittest
from pathlib import Path
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('native_dependencies',ROOT/'tools/native_dependencies.py')
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)

class MachOImportsTests(unittest.TestCase):
    def test_install_identity_is_not_an_import(self):
        with tempfile.TemporaryDirectory() as temp:
            native=Path(temp);host=native/'host.dylib';host.touch()
            def output(args,**kwargs):
                if args[1]=='-D':return str(host)+':\n/tmp/123-host.dylib\n'
                return str(host)+':\n\t/tmp/123-host.dylib (compatibility version 0.0.0)\n\t/usr/lib/libSystem.B.dylib (compatibility version 1.0.0)\n'
            with patch.object(module.sys,'platform','darwin'),patch.object(module.subprocess,'check_output',side_effect=output):
                self.assertIn('host.dylib',module.inspect(native)['imports'])
    def test_missing_actual_import_still_fails(self):
        with tempfile.TemporaryDirectory() as temp:
            native=Path(temp);host=native/'host.dylib';host.touch()
            def output(args,**kwargs):
                if args[1]=='-D':return str(host)+':\n@rpath/host.dylib\n'
                return str(host)+':\n\t@rpath/host.dylib (compatibility version 0.0.0)\n\t@rpath/missing.dylib (compatibility version 1.0.0)\n'
            with patch.object(module.sys,'platform','darwin'),patch.object(module.subprocess,'check_output',side_effect=output):
                with self.assertRaisesRegex(RuntimeError,'missing.dylib'):module.inspect(native)

class WindowsImportsTests(unittest.TestCase):
    def test_simulator_executable_requires_explicit_declaration(self):
        with tempfile.TemporaryDirectory() as temp:
            native=Path(temp);(native/'icarus.dll').touch()
            with patch.object(module.sys,'platform','win32'),patch.object(module.subprocess,'check_output',return_value='DLL Name: vvp.exe\nDLL Name: KERNEL32.dll\n'):
                with self.assertRaisesRegex(RuntimeError,'vvp.exe'):module.inspect(native)
                self.assertEqual(['vvp.exe'],module.inspect(native,simulator_hosts=('vvp.exe',))['simulator_hosts'])

    def test_declared_host_does_not_allow_unbundled_support_library(self):
        with tempfile.TemporaryDirectory() as temp:
            native=Path(temp);(native/'icarus.dll').touch()
            with patch.object(module.sys,'platform','win32'),patch.object(module.subprocess,'check_output',return_value='DLL Name: vvp.exe\nDLL Name: missing.dll\n'):
                with self.assertRaisesRegex(RuntimeError,'missing.dll'):module.inspect(native,simulator_hosts=('vvp.exe',))
