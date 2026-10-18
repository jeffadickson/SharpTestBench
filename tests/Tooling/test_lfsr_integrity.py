import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'tools'))
from lfsr_fixture import config,fixture,transition,reverse_bits,plan,verify_sources,plan_digest
from dual_runner_ci import clean_environment

class Tests(unittest.TestCase):
    def provenance(self,edit):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);folder=root/'tests/Fixtures/verilog-lfsr'
            shutil.copytree(ROOT/'tests/Fixtures/verilog-lfsr',folder)
            value=json.loads((folder/'provenance.json').read_text())
            edit(value);(folder/'provenance.json').write_text(json.dumps(value))
            with self.assertRaises(ValueError):verify_sources(root)

    def test_missing_repository(self):
        self.provenance(lambda x:x.pop('repository'))

    def test_missing_revision(self):
        self.provenance(lambda x:x.pop('revision'))

    def test_missing_license(self):
        self.provenance(lambda x:x.pop('license'))

    def test_missing_files(self):
        self.provenance(lambda x:x.pop('files'))

    def test_wrong_repository(self):
        self.provenance(lambda x:x.update(repository='other'))

    def test_wrong_revision(self):
        self.provenance(lambda x:x.update(revision='0000000000000000000000000000000000000000'))

    def test_wrong_license(self):
        self.provenance(lambda x:x.update(license='GPL'))

    def test_wrong_files(self):
        self.provenance(lambda x:x.update(files=None))

    def test_missing_lfsr_v_upstream(self):
        self.provenance(lambda x:x["files"]['lfsr.v'].pop('upstream'))

    def test_missing_lfsr_v_sha256(self):
        self.provenance(lambda x:x["files"]['lfsr.v'].pop('sha256'))

    def test_missing_COPYING_upstream(self):
        self.provenance(lambda x:x["files"]['COPYING'].pop('upstream'))

    def test_missing_COPYING_sha256(self):
        self.provenance(lambda x:x["files"]['COPYING'].pop('sha256'))

    def test_digest_0(self):
        self.provenance(lambda x:x["files"]["lfsr.v"].update(sha256=None))

    def test_digest_1(self):
        self.provenance(lambda x:x["files"]["lfsr.v"].update(sha256=''))

    def test_digest_2(self):
        self.provenance(lambda x:x["files"]["lfsr.v"].update(sha256='000000000000000000000000000000000000000000000000000000000000000'))

    def test_digest_3(self):
        self.provenance(lambda x:x["files"]["lfsr.v"].update(sha256='GGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGG'))

    def test_digest_4(self):
        self.provenance(lambda x:x["files"]["lfsr.v"].update(sha256='0000000000000000000000000000000000000000000000000000000000000000'))

    def test_bad_seed_0(self):
        with self.assertRaises(ValueError):plan(config(),-1)

    def test_bad_seed_1(self):
        with self.assertRaises(ValueError):plan(config(),18446744073709551616)

    def test_bad_seed_2(self):
        with self.assertRaises(ValueError):plan(config(),True)

    def test_bad_seed_3(self):
        with self.assertRaises(ValueError):plan(config(),None)

    def test_bad_count_0(self):
        with self.assertRaises(ValueError):plan(config(),1,0)

    def test_bad_count_1(self):
        with self.assertRaises(ValueError):plan(config(),1,4097)

    def test_bad_count_2(self):
        with self.assertRaises(ValueError):plan(config(),1,True)

    def test_bad_count_3(self):
        with self.assertRaises(ValueError):plan(config(),1,1.5)

    def test_bad_state_0(self):
        with self.assertRaises(ValueError):transition(config(),-1,0)

    def test_bad_state_1(self):
        with self.assertRaises(ValueError):transition(config(),4294967296,0)

    def test_bad_state_2(self):
        with self.assertRaises(ValueError):transition(config(),True,0)

    def test_bad_state_3(self):
        with self.assertRaises(ValueError):transition(config(),None,0)

    def test_bad_data_0(self):
        with self.assertRaises(ValueError):transition(config(),0,-1)

    def test_bad_data_1(self):
        with self.assertRaises(ValueError):transition(config(),0,256)

    def test_bad_data_2(self):
        with self.assertRaises(ValueError):transition(config(),0,True)

    def test_bad_data_3(self):
        with self.assertRaises(ValueError):transition(config(),0,None)

    def test_clean_lfsr_plan(self):
        self.assertNotIn('LFSR_PLAN',clean_environment({'LFSR_PLAN':"poison"}))

    def test_clean_lfsr_report(self):
        self.assertNotIn('LFSR_REPORT',clean_environment({'LFSR_REPORT':"poison"}))

    def test_clean_lfsr_fault(self):
        self.assertNotIn('LFSR_FAULT',clean_environment({'LFSR_FAULT':"poison"}))

    def test_bad_fixture_unknown(self):
        with self.assertRaises(ValueError):fixture('unknown',"LOOP")

    def test_bad_fixture_CRC32(self):
        with self.assertRaises(ValueError):fixture('CRC32',"LOOP")

    def test_bad_fixture_None(self):
        with self.assertRaises(ValueError):fixture(None,"LOOP")

    def test_bad_style_0(self):
        with self.assertRaises(ValueError):fixture("crc32",'UNKNOWN')

    def test_digest_matches_bytes(self):
        import hashlib
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)/'plan.json';path.write_text(json.dumps(plan(config(),1,1)))
            self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(),plan_digest(path))

    def test_digest_changed_format(self):
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)/'plan.json';text=json.dumps(plan(config(),1,1));path.write_text(text)
            before=plan_digest(path);path.write_text(text+'\n')
            self.assertNotEqual(before,plan_digest(path))

    def test_digest_rejects_wrong_expectation(self):
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)/'plan.json';value=plan(config(),1,1);value['Vectors'][0]['ExpectedState']=1
            path.write_text(json.dumps(value))
            with self.assertRaises(ValueError):plan_digest(path)
