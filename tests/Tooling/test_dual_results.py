import json
from pathlib import Path
import sys
import unittest
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tests/Tooling/managed_compat'))
import dual_runner_parity as parity

class Tests(unittest.TestCase):

    def test_null(self):
        with self.assertRaises(ValueError):
            parity.signature(None)

    def test_object(self):
        with self.assertRaises(ValueError):
            parity.signature({})

    def test_string(self):
        with self.assertRaises(ValueError):
            parity.signature('passed')

    def test_number(self):
        with self.assertRaises(ValueError):
            parity.signature(1)

    def test_boolean(self):
        with self.assertRaises(ValueError):
            parity.signature(True)

    def test_empty(self):
        with self.assertRaises(ValueError):
            parity.signature([])

    def test_null_row(self):
        with self.assertRaises(ValueError):
            parity.signature([None])

    def test_array_row(self):
        with self.assertRaises(ValueError):
            parity.signature([[]])

    def test_string_row(self):
        with self.assertRaises(ValueError):
            parity.signature(['x'])

    def test_no_identity(self):
        with self.assertRaises(ValueError):
            parity.signature([{'Status': 'passed'}])

    def test_empty_identity(self):
        with self.assertRaises(ValueError):
            parity.signature([{'Name': '', 'Status': 'passed'}])

    def test_blank_identity(self):
        with self.assertRaises(ValueError):
            parity.signature([{'Name': ' ', 'Status': 'passed'}])

    def test_numeric_identity(self):
        with self.assertRaises(ValueError):
            parity.signature([{'Name': 3, 'Status': 'passed'}])

    def test_padded_identity(self):
        with self.assertRaises(ValueError):
            parity.signature([{'Name': ' a', 'Status': 'passed'}])

    def test_control_identity(self):
        with self.assertRaises(ValueError):
            parity.signature([{'Name': 'a\n', 'Status': 'passed'}])

    def test_missing_status(self):
        with self.assertRaises(ValueError):
            parity.signature([{'Name': 'a'}])

    def test_null_status(self):
        with self.assertRaises(ValueError):
            parity.signature([{'Name': 'a', 'Status': None}])

    def test_failed(self):
        with self.assertRaises(ValueError):
            parity.signature([{'Name': 'a', 'Status': 'failed'}])

    def test_skipped(self):
        with self.assertRaises(ValueError):
            parity.signature([{'Name': 'a', 'Status': 'skipped'}])

    def test_unknown_status(self):
        with self.assertRaises(ValueError):
            parity.signature([{'Name': 'a', 'Status': 'success'}])

    def test_numeric_status(self):
        with self.assertRaises(ValueError):
            parity.signature([{'Name': 'a', 'Status': 1}])

    def test_duplicates(self):
        with self.assertRaises(ValueError):
            parity.signature([{'Name': 'a', 'Status': 'passed'}, {'Name': 'a', 'Status': 'passed'}])

    def test_qualified_name(self):
        self.assertEqual([('Suite.a', 'passed')], parity.signature([{'Name':'a','QualifiedName':'Suite.a','Status':'passed'}]))

    def test_short_name(self):
        self.assertEqual([('a', 'passed')], parity.signature([{'Name':'a','Status':'passed'}]))

    def test_sorted_cases(self):
        self.assertEqual([('a','passed'),('b','passed')], parity.signature([{'Name':n,'Status':'passed'} for n in ['b','a']]))
