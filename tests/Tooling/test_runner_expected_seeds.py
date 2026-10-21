"""Explicit per-case stimulus attestation, independent of result row ordering."""
import json
from pathlib import Path
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[2]

class Tests(unittest.TestCase):
    def verify(self, rows, names, seeds=None):
        payload=dict(rows=rows,count=len(rows),names=names)
        if seeds is not None: payload['expectedSeeds']=seeds
        result=subprocess.run(['dotnet',str(ROOT/'tests/RunnerValidation/bin/Release/net10.0/RunnerValidation.dll'),'results'],input=json.dumps(payload),text=True,capture_output=True,check=True,timeout=30)
        return json.loads(result.stdout)

    def row(self,name='Entry.Test',seed=17,status='passed'):
        return dict(QualifiedName=name,Seed=seed,Status=status)

    def test_old_manifest_preserves_seed_validation_policy(self):
        self.assertTrue(self.verify([self.row()],['Entry.Test'])['valid'])

    def test_passed_wrong_stimulus_cannot_attest(self):
        self.assertFalse(self.verify([self.row(seed=18)],['Entry.Test'],{'Entry.Test':17})['valid'])

    def test_failed_wrong_stimulus_cannot_attest(self):
        self.assertFalse(self.verify([self.row(seed=18,status='failed')],['Entry.Test'],{'Entry.Test':17})['valid'])

    def test_full_width_seeds_and_reordered_rows(self):
        rows=[self.row('b',18446744073709551615),self.row('a',0)]
        self.assertTrue(self.verify(rows,['a','b'],{'a':0,'b':18446744073709551615})['valid'])

    def test_selected_case_need_not_have_base_seed(self):
        self.assertTrue(self.verify([self.row(seed=102)],['Entry.Test'],{'Entry.Test':102})['valid'])

    def test_map_must_cover_exact_identities(self):
        for seeds in ({},{'other':17},{'Entry.Test':17,'other':17},{'entry.test':17}):
            with self.subTest(seeds=seeds):
                self.assertFalse(self.verify([self.row()],['Entry.Test'],seeds)['valid'])

    def test_seed_contract_requires_explicit_identities(self):
        self.assertFalse(self.verify([self.row()],None,{'Entry.Test':17})['valid'])

    def test_invalid_expected_seed_encodings_rejected(self):
        for value in (-1,18446744073709551616,True,'17',17.5):
            with self.subTest(value=value):
                self.assertFalse(self.verify([self.row()],['Entry.Test'],{'Entry.Test':value})['valid'])