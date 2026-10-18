import json
from pathlib import Path
import sys
import unittest
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools'))
import dual_runner_parity as parity

import subprocess

class Tests(unittest.TestCase):
    def verify(self, rows, count=1, names=None):
        executable=ROOT/'tests/RunnerValidation/bin/Release/net10.0/RunnerValidation.dll'
        payload={'rows':rows,'count':count,'names':names}
        result=subprocess.run(['dotnet',str(executable),'results'],input=json.dumps(payload),capture_output=True,text=True,check=True)
        return json.loads(result.stdout)


    def test_null(self):
        self.assertFalse(self.verify(None)["valid"])

    def test_object(self):
        self.assertFalse(self.verify({})["valid"])

    def test_empty(self):
        self.assertFalse(self.verify([])["valid"])

    def test_null_row(self):
        self.assertFalse(self.verify([None])["valid"])

    def test_array_row(self):
        self.assertFalse(self.verify([[]])["valid"])

    def test_numeric_row(self):
        self.assertFalse(self.verify([1])["valid"])

    def test_missing_name(self):
        self.assertFalse(self.verify([{'Status': 'passed', 'Seed': 1}])["valid"])

    def test_blank_name(self):
        self.assertFalse(self.verify([{'Name': ' ', 'Status': 'passed', 'Seed': 1}])["valid"])

    def test_padded_name(self):
        self.assertFalse(self.verify([{'Name': ' a', 'Status': 'passed', 'Seed': 1}])["valid"])

    def test_bad_qualified(self):
        self.assertFalse(self.verify([{'Name': 'a', 'QualifiedName': 3, 'Status': 'passed', 'Seed': 1}])["valid"])

    def test_missing_status(self):
        self.assertFalse(self.verify([{'Name': 'a', 'Seed': 1}])["valid"])

    def test_numeric_status(self):
        self.assertFalse(self.verify([{'Name': 'a', 'Status': 1, 'Seed': 1}])["valid"])

    def test_unknown_status(self):
        self.assertFalse(self.verify([{'Name': 'a', 'Status': 'success', 'Seed': 1}])["valid"])

    def test_skipped(self):
        self.assertFalse(self.verify([{'Name': 'a', 'Status': 'skipped', 'Seed': 1}])["valid"])

    def test_missing_seed(self):
        self.assertFalse(self.verify([{'Name': 'a', 'Status': 'passed'}])["valid"])

    def test_negative_seed(self):
        self.assertFalse(self.verify([{'Name': 'a', 'Status': 'passed', 'Seed': -1}])["valid"])

    def test_string_seed(self):
        self.assertFalse(self.verify([{'Name': 'a', 'Status': 'passed', 'Seed': '1'}])["valid"])

    def test_overflow_seed(self):
        self.assertFalse(self.verify([{'Name': 'a', 'Status': 'passed', 'Seed': 18446744073709551616}])["valid"])

    def test_duplicates(self):
        self.assertFalse(self.verify([{'Name':'a','Status':'passed','Seed':1}]*2,count=2)['valid'])

    def test_wrong_count(self):
        self.assertFalse(self.verify([{'Name':'a','Status':'passed','Seed':1}],count=2)['valid'])

    def test_wrong_identity(self):
        self.assertFalse(self.verify([{'Name':'a','Status':'passed','Seed':1}],names=['b'])['valid'])

    def test_passed(self):
        self.assertEqual(1,self.verify([{'Name':'a','Status':'passed','Seed':1}])['passed'])

    def test_failed_evidence(self):
        actual=self.verify([{'Name':'a','Status':'failed','Seed':1}])
        self.assertTrue(actual['valid'])
        self.assertEqual(1,actual['failed'])

    def test_qualified(self):
        self.assertTrue(self.verify([{'Name':'a','QualifiedName':'Entry.a','Status':'passed','Seed':1}],names=['Entry.a'])['valid'])

    def test_mixed_outcomes(self):
        actual=self.verify([{'Name':'a','Status':'passed','Seed':1},{'Name':'b','Status':'failed','Seed':2}],count=2,names=['a','b'])
        self.assertTrue(actual['valid'])
        self.assertEqual(1,actual['failed'])
