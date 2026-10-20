"""Check compiler profiles for platforms unavailable on the local test host."""
import sys
import unittest
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'tests/Tooling/managed_compat'))
from icarus_layouts import target_flags


class TargetProfiles(unittest.TestCase):
    def test_macos_bundle_resolves_compiler_symbols(self):
        flags = target_flags('darwin', 'Apple clang version 17')
        self.assertIn('-bundle', flags)
        self.assertIn('dynamic_lookup', flags)
        self.assertIn('-fvectorize', flags)
        self.assertNotIn('-shared', flags)

    def test_windows_static_runtime_dll(self):
        flags = target_flags('win32', 'g++ 14')
        self.assertIn('-shared', flags)
        self.assertIn('-static', flags)
        self.assertNotIn('-fPIC', flags)

    def test_linux_gcc_and_clang(self):
        self.assertIn('-ftree-loop-vectorize', target_flags('linux', 'g++ 14'))
        self.assertIn('-fslp-vectorize', target_flags('linux', 'clang version 18'))

    def test_profiles_preserve_portable_isa(self):
        for system in ('linux', 'darwin', 'win32'):
            flags = target_flags(system, 'clang 18')
            self.assertTrue({'-std=c++20', '-O3', '-flto'} <= set(flags))
            self.assertFalse(any(x.startswith(('-march', '-mavx', '-msse')) for x in flags))
        with self.assertRaises(ValueError):
            target_flags('unsupported', 'g++')
