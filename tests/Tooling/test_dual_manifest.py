import json
from pathlib import Path
import sys
import unittest
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools'))
import dual_runner_manifest as manifest

import tempfile

class Tests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.adapter = {'bootstrap':'/adapter/bootstrap.vpi','library':'/adapter/gpi.so','entryPoint':'entry_point','implementation':'/adapter/icarus.so,entry_point','dotnetRoot':'/dotnet'}

    def make(self, fixture='counter', seed=1, output='build/native'):
        return manifest.make_manifest(self.root, fixture, seed, self.adapter, output)


    def test_seed_negative(self):
        with self.assertRaises(ValueError):
            self.make(seed=-1)

    def test_seed_overflow(self):
        with self.assertRaises(ValueError):
            self.make(seed=18446744073709551616)

    def test_seed_float(self):
        with self.assertRaises(ValueError):
            self.make(seed=1.5)

    def test_seed_bool(self):
        with self.assertRaises(ValueError):
            self.make(seed=False)

    def test_seed_string(self):
        with self.assertRaises(ValueError):
            self.make(seed='1')

    def test_seed_null(self):
        with self.assertRaises(ValueError):
            self.make(seed=None)

    def test_fixture_unknown(self):
        with self.assertRaises(ValueError):
            self.make(fixture='missing')

    def test_fixture_traversal(self):
        with self.assertRaises(ValueError):
            self.make(fixture='../counter')

    def test_fixture_empty(self):
        with self.assertRaises(ValueError):
            self.make(fixture='')

    def test_output_parent(self):
        with self.assertRaises(ValueError):
            self.make(output='../outside')

    def test_output_build_root(self):
        with self.assertRaises(ValueError):
            self.make(output='build')

    def test_output_root(self):
        with self.assertRaises(ValueError):
            self.make(output='.')

    def test_output_source(self):
        with self.assertRaises(ValueError):
            self.make(output='examples/Counter')

    def test_output_escape(self):
        with self.assertRaises(ValueError):
            self.make(output='build/../../escape')

    def test_adapter_bootstrap(self):
        self.adapter['bootstrap']=""
        with self.assertRaises(ValueError):
            self.make()

    def test_adapter_library(self):
        self.adapter['library']=""
        with self.assertRaises(ValueError):
            self.make()

    def test_adapter_entryPoint(self):
        self.adapter['entryPoint']=""
        with self.assertRaises(ValueError):
            self.make()

    def test_adapter_implementation(self):
        self.adapter['implementation']=""
        with self.assertRaises(ValueError):
            self.make()

    def test_adapter_dotnetRoot(self):
        self.adapter['dotnetRoot']=""
        with self.assertRaises(ValueError):
            self.make()

    def test_fixture_counter(self):
        actual=self.make(fixture='counter')
        self.assertEqual(['Entry.ResetAndCount'],actual["expectedNames"])
        self.assertEqual(1,actual["expectedTests"])
        self.assertTrue(Path(actual["assembly"]).is_absolute())

    def test_fixture_dsp(self):
        actual=self.make(fixture='dsp')
        self.assertEqual(['Entry.FirScoreboard'],actual["expectedNames"])
        self.assertEqual(1,actual["expectedTests"])
        self.assertTrue(Path(actual["assembly"]).is_absolute())

    def test_fixture_cpu(self):
        actual=self.make(fixture='cpu')
        self.assertEqual(['Entry.CpuScoreboard'],actual["expectedNames"])
        self.assertEqual(1,actual["expectedTests"])
        self.assertTrue(Path(actual["assembly"]).is_absolute())

    def test_seed_zero(self):
        self.assertEqual(0,self.make(seed=0)['seed'])

    def test_seed_max(self):
        self.assertEqual(2**64-1,self.make(seed=2**64-1)['seed'])

    def test_spaces(self):
        self.assertEqual(str((self.root/'build/with spaces').resolve()),self.make(output='build/with spaces')['outputDirectory'])
