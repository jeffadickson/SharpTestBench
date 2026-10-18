import json
from pathlib import Path
import sys
import unittest
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools'))
import dual_runner_parity as parity

class Tests(unittest.TestCase):

    def test_expected_null(self):
        with self.assertRaises(ValueError):
            parity.compare([{'Name':'a','Status':'passed','Seed':1}], [{'Name':'a','Status':'passed','Seed':1}], None)

    def test_expected_object(self):
        with self.assertRaises(ValueError):
            parity.compare([{'Name':'a','Status':'passed','Seed':1}], [{'Name':'a','Status':'passed','Seed':1}], {})

    def test_expected_string(self):
        with self.assertRaises(ValueError):
            parity.compare([{'Name':'a','Status':'passed','Seed':1}], [{'Name':'a','Status':'passed','Seed':1}], 'a')

    def test_expected_empty(self):
        with self.assertRaises(ValueError):
            parity.compare([{'Name':'a','Status':'passed','Seed':1}], [{'Name':'a','Status':'passed','Seed':1}], [])

    def test_expected_blank(self):
        with self.assertRaises(ValueError):
            parity.compare([{'Name':'a','Status':'passed','Seed':1}], [{'Name':'a','Status':'passed','Seed':1}], [''])

    def test_expected_whitespace(self):
        with self.assertRaises(ValueError):
            parity.compare([{'Name':'a','Status':'passed','Seed':1}], [{'Name':'a','Status':'passed','Seed':1}], [' '])

    def test_expected_numeric(self):
        with self.assertRaises(ValueError):
            parity.compare([{'Name':'a','Status':'passed','Seed':1}], [{'Name':'a','Status':'passed','Seed':1}], [1])

    def test_expected_null_name(self):
        with self.assertRaises(ValueError):
            parity.compare([{'Name':'a','Status':'passed','Seed':1}], [{'Name':'a','Status':'passed','Seed':1}], [None])

    def test_expected_duplicate(self):
        with self.assertRaises(ValueError):
            parity.compare([{'Name':'a','Status':'passed','Seed':1}], [{'Name':'a','Status':'passed','Seed':1}], ['a', 'a'])

    def test_expected_padded(self):
        with self.assertRaises(ValueError):
            parity.compare([{'Name':'a','Status':'passed','Seed':1}], [{'Name':'a','Status':'passed','Seed':1}], [' a'])

    def test_expected_control(self):
        with self.assertRaises(ValueError):
            parity.compare([{'Name':'a','Status':'passed','Seed':1}], [{'Name':'a','Status':'passed','Seed':1}], ['a\n'])

    def test_missing_legacy(self):
        rows = lambda names: [{'Name':n, 'Status':'passed', 'Seed':1} for n in names]
        self.assertFalse(parity.compare(rows(['a']),rows(['a', 'b']),['a', 'b'])['matched'])

    def test_missing_native(self):
        rows = lambda names: [{'Name':n, 'Status':'passed', 'Seed':1} for n in names]
        self.assertFalse(parity.compare(rows(['a', 'b']),rows(['a']),['a', 'b'])['matched'])

    def test_unexpected_both(self):
        rows = lambda names: [{'Name':n, 'Status':'passed', 'Seed':1} for n in names]
        self.assertFalse(parity.compare(rows(['a', 'b']),rows(['a', 'b']),['a'])['matched'])

    def test_wrong_both(self):
        rows = lambda names: [{'Name':n, 'Status':'passed', 'Seed':1} for n in names]
        self.assertFalse(parity.compare(rows(['b']),rows(['b']),['a'])['matched'])

    def test_different(self):
        rows = lambda names: [{'Name':n, 'Status':'passed', 'Seed':1} for n in names]
        self.assertFalse(parity.compare(rows(['a']),rows(['b']),['a'])['matched'])

    def test_seed_negative(self):
        with self.assertRaises(ValueError):
            parity.compare([{'Name':'a','Status':'passed','Seed':-1}], [{'Name':'a','Status':'passed','Seed':1}], ['a'])

    def test_seed_overflow(self):
        with self.assertRaises(ValueError):
            parity.compare([{'Name':'a','Status':'passed','Seed':18446744073709551616}], [{'Name':'a','Status':'passed','Seed':1}], ['a'])

    def test_seed_string(self):
        with self.assertRaises(ValueError):
            parity.compare([{'Name':'a','Status':'passed','Seed':'1'}], [{'Name':'a','Status':'passed','Seed':1}], ['a'])

    def test_seed_boolean(self):
        with self.assertRaises(ValueError):
            parity.compare([{'Name':'a','Status':'passed','Seed':True}], [{'Name':'a','Status':'passed','Seed':1}], ['a'])

    def test_seed_float(self):
        with self.assertRaises(ValueError):
            parity.compare([{'Name':'a','Status':'passed','Seed':1.0}], [{'Name':'a','Status':'passed','Seed':1}], ['a'])

    def test_seed_null(self):
        with self.assertRaises(ValueError):
            parity.compare([{'Name':'a','Status':'passed','Seed':None}], [{'Name':'a','Status':'passed','Seed':1}], ['a'])

    def test_seed_mismatch(self):
        self.assertFalse(parity.compare([{'Name':'a','Status':'passed','Seed':1}],[{'Name':'a','Status':'passed','Seed':2}],['a'])['matched'])

    def test_order_independent(self):
        rows = lambda names: [{'Name':n, 'Status':'passed', 'Seed':1} for n in names]
        self.assertTrue(parity.compare(rows(['b','a']),rows(['a','b']),['a','b'])['matched'])

    def test_missing_seed(self):
        with self.assertRaises(ValueError):
            parity.compare([{'Name':'a','Status':'passed'}],[{'Name':'a','Status':'passed','Seed':1}],['a'])
