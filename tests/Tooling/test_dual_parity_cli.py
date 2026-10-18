import json
from pathlib import Path
import sys
import unittest
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools'))
import dual_runner_parity as parity

import contextlib
import io
import tempfile

class Tests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.folder = Path(self.temp.name)
        self.left = self.folder/'legacy.json'
        self.right = self.folder/'native.json'
        self.expected = self.folder/'expected.json'
        self.report = self.folder/'report.json'
        self.rows = [{'Name':'a','Status':'passed','Seed':1}]
        for p, value in [(self.left,self.rows),(self.right,self.rows),(self.expected,['a'])]:
            p.write_text(json.dumps(value), encoding='utf-8')

    def run_cli(self):
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            return parity.main(['--legacy',str(self.left),'--native',str(self.right),'--expected',str(self.expected),'--report',str(self.report)])

    def read_report(self):
        return json.loads(self.report.read_text(encoding='utf-8'))


    def test_success(self):
        self.assertEqual(0,self.run_cli())
        self.assertTrue(self.read_report()['matched'])

    def test_mismatch(self):
        self.right.write_text(json.dumps([{'Name':'b','Status':'passed','Seed':1}]))
        self.assertEqual(1,self.run_cli())
        self.assertEqual(['a'], self.read_report()['missing']['native'])

    def test_legacy_missing(self):
        self.left.unlink()
        self.assertEqual(1,self.run_cli())
        self.assertFalse(self.read_report()["matched"])

    def test_legacy_malformed(self):
        self.left.write_text("{")
        self.assertEqual(1,self.run_cli())
        self.assertIn("error", self.read_report())

    def test_legacy_encoding(self):
        self.left.write_bytes(b"\xff")
        self.assertEqual(1,self.run_cli())

    def test_native_missing(self):
        self.right.unlink()
        self.assertEqual(1,self.run_cli())
        self.assertFalse(self.read_report()["matched"])

    def test_native_malformed(self):
        self.right.write_text("{")
        self.assertEqual(1,self.run_cli())
        self.assertIn("error", self.read_report())

    def test_native_encoding(self):
        self.right.write_bytes(b"\xff")
        self.assertEqual(1,self.run_cli())

    def test_expected_missing(self):
        self.expected.unlink()
        self.assertEqual(1,self.run_cli())
        self.assertFalse(self.read_report()["matched"])

    def test_expected_malformed(self):
        self.expected.write_text("{")
        self.assertEqual(1,self.run_cli())
        self.assertIn("error", self.read_report())

    def test_expected_encoding(self):
        self.expected.write_bytes(b"\xff")
        self.assertEqual(1,self.run_cli())

    def test_null(self):
        self.left.write_text(json.dumps(None))
        self.assertEqual(1,self.run_cli())
        self.assertFalse(self.read_report()["matched"])

    def test_object(self):
        self.left.write_text(json.dumps({}))
        self.assertEqual(1,self.run_cli())
        self.assertFalse(self.read_report()["matched"])

    def test_empty(self):
        self.left.write_text(json.dumps([]))
        self.assertEqual(1,self.run_cli())
        self.assertFalse(self.read_report()["matched"])

    def test_unknown(self):
        self.left.write_text(json.dumps([{'Name': 'a', 'Status': 'weird', 'Seed': 1}]))
        self.assertEqual(1,self.run_cli())
        self.assertFalse(self.read_report()["matched"])

    def test_duplicates(self):
        self.left.write_text(json.dumps([{'Name': 'a', 'Status': 'passed', 'Seed': 1}, {'Name': 'a', 'Status': 'passed', 'Seed': 1}]))
        self.assertEqual(1,self.run_cli())
        self.assertFalse(self.read_report()["matched"])

    def test_failure(self):
        self.left.write_text(json.dumps([{'Name': 'a', 'Status': 'failed', 'Seed': 1}]))
        self.assertEqual(1,self.run_cli())
        self.assertFalse(self.read_report()["matched"])

    def test_skip(self):
        self.left.write_text(json.dumps([{'Name': 'a', 'Status': 'skipped', 'Seed': 1}]))
        self.assertEqual(1,self.run_cli())
        self.assertFalse(self.read_report()["matched"])

    def test_stale_report(self):
        self.report.write_text('stale')
        self.assertEqual(0,self.run_cli())
        self.assertEqual(1,self.read_report()['schemaVersion'])

    def test_nested_report(self):
        self.report=self.folder/'nested'/'parity.json'
        self.assertEqual(0,self.run_cli())
        self.assertTrue(self.report.is_file())

    def test_seed_diagnostic(self):
        self.right.write_text(json.dumps([{'Name':'a','Status':'passed','Seed':2}]))
        self.assertEqual(1,self.run_cli())
        self.assertEqual(['a'],self.read_report()['seedMismatches'])

    def test_report_alias_left(self):
        self.report=self.left
        original=self.report.read_bytes()
        self.assertEqual(1,self.run_cli())
        self.assertEqual(original,self.report.read_bytes())

    def test_report_alias_right(self):
        self.report=self.right
        original=self.report.read_bytes()
        self.assertEqual(1,self.run_cli())
        self.assertEqual(original,self.report.read_bytes())

    def test_report_alias_expected(self):
        self.report=self.expected
        original=self.report.read_bytes()
        self.assertEqual(1,self.run_cli())
        self.assertEqual(original,self.report.read_bytes())

    def test_unwritable_report(self):
        self.report=self.folder
        self.assertEqual(1,self.run_cli())
