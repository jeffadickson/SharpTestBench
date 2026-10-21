"""Parent parity and production Runner must agree on Unicode identity policy."""
import json
import subprocess
import unittest
from test_fifo_contract import call
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]


class Tests(unittest.TestCase):
    def native(self,rows,names):
        process=subprocess.run(['dotnet',str(ROOT/'tests/RunnerValidation/bin/Release/net10.0/RunnerValidation.dll'),'results'],
            input=json.dumps(dict(rows=rows,names=names,count=len(rows))),text=True,encoding='utf-8',capture_output=True,check=True,timeout=30)
        return json.loads(process.stdout)

    def test_c1_controls_cannot_satisfy_either_identity_contract(self):
        for codepoint in [0x80,0x85,0x9f]:
            name='Entry.'+chr(codepoint)+'Test'
            rows=[dict(Name=name,Seed=17,Status='passed')]
            self.assertFalse(self.native(rows,[name])['valid'])
            with self.assertRaises(ValueError):call('dual_runner_parity','compare',[rows,rows,[name]])
            with self.assertRaises(ValueError):call('dual_runner_parity','signature',[rows])

    def test_c1_controls_cannot_qualify_an_expected_failure(self):
        name='Entry.\u009fTest';rows=[dict(Name=name,Seed=17,Status='failed',Error='AXIS_MISMATCH')]
        self.assertFalse(self.native(rows,[name])['valid'])
        with self.assertRaises(ValueError):call('dual_runner_ci','fault_detected',[rows,1,[name],'AXIS_MISMATCH'])

    def test_legitimate_unicode_remains_ordinal_and_unmodified(self):
        names=['Entry.é','Entry.e\u0301','Entry.😀']
        rows=[dict(Name=name,Seed=i,Status='passed') for i,name in enumerate(names)]
        self.assertTrue(self.native(rows,names)['valid'])
        self.assertTrue(call('dual_runner_parity','compare',[rows,list(reversed(rows)),names])['matched'])
