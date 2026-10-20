import json
from pathlib import Path
import subprocess
import sys
import unittest
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'tests/Tooling/managed_compat'))
import strict_json

class Tests(unittest.TestCase):
    def native(self,text,mode='json'):
        result=subprocess.run(['dotnet',str(ROOT/'tests/RunnerValidation/bin/Release/net10.0/RunnerValidation.dll'),mode],input=text,text=True,encoding='utf-8',capture_output=True,check=True)
        return json.loads(result.stdout)['valid']

    def test_python_duplicate(self):
        with self.assertRaises(ValueError):
            strict_json.loads('{"a":1,"a":2}')

    def test_python_nested_duplicate(self):
        with self.assertRaises(ValueError):
            strict_json.loads('{"a":{"b":1,"b":2}}')

    def test_python_array_duplicate(self):
        with self.assertRaises(ValueError):
            strict_json.loads('[{"a":1,"a":2}]')

    def test_python_nan(self):
        with self.assertRaises(ValueError):
            strict_json.loads('NaN')

    def test_python_infinity(self):
        with self.assertRaises(ValueError):
            strict_json.loads('Infinity')

    def test_python_negative_infinity(self):
        with self.assertRaises(ValueError):
            strict_json.loads('-Infinity')

    def test_python_trailing_comma(self):
        with self.assertRaises(ValueError):
            strict_json.loads('[1,]')

    def test_python_comment(self):
        with self.assertRaises(ValueError):
            strict_json.loads('/* x */{}')

    def test_python_trailing_document(self):
        with self.assertRaises(ValueError):
            strict_json.loads('{}{}')

    def test_python_malformed(self):
        with self.assertRaises(ValueError):
            strict_json.loads('{')

    def test_python_unpaired_surrogate(self):
        with self.assertRaises(ValueError):
            strict_json.loads('"\\ud800"')

    def test_python_depth(self):
        with self.assertRaises(ValueError):
            strict_json.loads('[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[0]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]')

    def test_native_duplicate(self):
        self.assertFalse(self.native('{"a":1,"a":2}'))

    def test_native_nested_duplicate(self):
        self.assertFalse(self.native('{"a":{"b":1,"b":2}}'))

    def test_native_array_duplicate(self):
        self.assertFalse(self.native('[{"a":1,"a":2}]'))

    def test_native_nan(self):
        self.assertFalse(self.native('NaN'))

    def test_native_infinity(self):
        self.assertFalse(self.native('Infinity'))

    def test_native_negative_infinity(self):
        self.assertFalse(self.native('-Infinity'))

    def test_native_trailing_comma(self):
        self.assertFalse(self.native('[1,]'))

    def test_native_comment(self):
        self.assertFalse(self.native('/* x */{}'))

    def test_native_trailing_document(self):
        self.assertFalse(self.native('{}{}'))

    def test_native_malformed(self):
        self.assertFalse(self.native('{'))

    def test_case_duplicate_manifest(self):
        self.assertFalse(self.native('{"schemaVersion":1,"SchemaVersion":2}',mode='manifest'))

    def test_unknown_manifest_member(self):
        self.assertFalse(self.native('{"unexpected":true}',mode='manifest'))

    def test_valid_json_unicode(self):
        self.assertEqual({'λ':[1,True,None]},strict_json.loads('{"λ":[1,true,null]}'))
        self.assertTrue(self.native('{"λ":[1,true,null]}'))
