import copy
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'tools'))
from axis_fixture import verify_sources, plan, validate_plan, metrics
from dual_runner_ci import clean_environment
import test_axis_metrics as metric_samples

class Tests(unittest.TestCase):
    def provenance(self,edit):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory); folder=root/'tests/Fixtures/verilog-axis'
            shutil.copytree(ROOT/'tests/Fixtures/verilog-axis',folder)
            data=json.loads((folder/'provenance.json').read_text())
            edit(data); (folder/'provenance.json').write_text(json.dumps(data))
            with self.assertRaises(ValueError): verify_sources(root)
    def evidence(self,edit):
        data=metric_samples.Tests().sample(); edit(data)
        with self.assertRaises(ValueError): metrics(data)

    def test_missing_meta_repository(self):
        self.provenance(lambda data:data.pop('repository'))

    def test_missing_meta_revision(self):
        self.provenance(lambda data:data.pop('revision'))

    def test_missing_meta_license(self):
        self.provenance(lambda data:data.pop('license'))

    def test_missing_meta_files(self):
        self.provenance(lambda data:data.pop('files'))

    def test_meta_repository_0(self):
        self.provenance(lambda data:data.update(repository='https://example.org/other'))

    def test_meta_repository_1(self):
        self.provenance(lambda data:data.update(repository=None))

    def test_meta_revision_0(self):
        self.provenance(lambda data:data.update(revision='0000000000000000000000000000000000000000'))

    def test_meta_revision_1(self):
        self.provenance(lambda data:data.update(revision='short'))

    def test_meta_revision_2(self):
        self.provenance(lambda data:data.update(revision=None))

    def test_meta_license_0(self):
        self.provenance(lambda data:data.update(license='GPL'))

    def test_meta_license_1(self):
        self.provenance(lambda data:data.update(license=None))

    def test_meta_files_0(self):
        self.provenance(lambda data:data.update(files=[]))

    def test_meta_files_1(self):
        self.provenance(lambda data:data.update(files=None))

    def test_missing_axis_register_v_upstream(self):
        self.provenance(lambda data:data["files"]['axis_register.v'].pop('upstream'))

    def test_missing_axis_register_v_sha256(self):
        self.provenance(lambda data:data["files"]['axis_register.v'].pop('sha256'))

    def test_missing_COPYING_upstream(self):
        self.provenance(lambda data:data["files"]['COPYING'].pop('upstream'))

    def test_missing_COPYING_sha256(self):
        self.provenance(lambda data:data["files"]['COPYING'].pop('sha256'))

    def test_digest_0(self):
        self.provenance(lambda data:data["files"]["axis_register.v"].update(sha256=''))

    def test_digest_1(self):
        self.provenance(lambda data:data["files"]["axis_register.v"].update(sha256=None))

    def test_digest_2(self):
        self.provenance(lambda data:data["files"]["axis_register.v"].update(sha256='GGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGG'))

    def test_digest_3(self):
        self.provenance(lambda data:data["files"]["axis_register.v"].update(sha256='000000000000000000000000000000000000000000000000000000000000000'))

    def test_digest_4(self):
        self.provenance(lambda data:data["files"]["axis_register.v"].update(sha256='0000000000000000000000000000000000000000000000000000000000000000'))

    def test_upstream_path(self):
        self.provenance(lambda data:data["files"]["axis_register.v"].update(upstream="../other.v"))

    def test_unknown_file(self):
        self.provenance(lambda data:data["files"].update(extra={}))

    def test_missing_license_file(self):
        self.provenance(lambda data:data["files"].pop("COPYING"))

    def test_clean_axis_plan(self):
        self.assertNotIn('AXIS_PLAN',clean_environment({'AXIS_PLAN':"poison","PATH":"ok"}))

    def test_clean_axis_report(self):
        self.assertNotIn('AXIS_REPORT',clean_environment({'AXIS_REPORT':"poison","PATH":"ok"}))

    def test_clean_axis_fault(self):
        self.assertNotIn('AXIS_FAULT',clean_environment({'AXIS_FAULT':"poison","PATH":"ok"}))

    def test_clean_axis_sidebands(self):
        self.assertNotIn('AXIS_SIDEBANDS',clean_environment({'AXIS_SIDEBANDS':"poison","PATH":"ok"}))

    def test_trace_fault_0(self):
        self.evidence(lambda data:data["trace"].clear())

    def test_trace_fault_1(self):
        self.evidence(lambda data:data["trace"].append(data["trace"][-1]))

    def test_trace_fault_2(self):
        self.evidence(lambda data:data["trace"][1].pop())

    def test_trace_fault_3(self):
        self.evidence(lambda data:data["trace"][1].__setitem__(0,2))

    def test_trace_fault_4(self):
        self.evidence(lambda data:data["trace"][0].__setitem__(1,0))

    def test_trace_fault_5(self):
        self.evidence(lambda data:data["trace"][1].__setitem__(2,0))

    def test_trace_fault_6(self):
        self.evidence(lambda data:data["trace"][2].__setitem__(5,1))

    def test_trace_fault_7(self):
        self.evidence(lambda data:data.update(extra=1))

    def test_trace_fault_8(self):
        self.evidence(lambda data:data["trace"].__setitem__(1,None))

    def test_trace_fault_9(self):
        self.evidence(lambda data:data["trace"][1].__setitem__(5,True))

    def test_changed_bytes_axis_register_v(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory); folder=root/'tests/Fixtures/verilog-axis'
            shutil.copytree(ROOT/'tests/Fixtures/verilog-axis',folder)
            with (folder/'axis_register.v').open('ab') as output: output.write(b'changed')
            with self.assertRaises(ValueError): verify_sources(root)

    def test_changed_bytes_COPYING(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory); folder=root/'tests/Fixtures/verilog-axis'
            shutil.copytree(ROOT/'tests/Fixtures/verilog-axis',folder)
            with (folder/'COPYING').open('ab') as output: output.write(b'changed')
            with self.assertRaises(ValueError): verify_sources(root)

    def test_valid_stall(self):
        data=metric_samples.Tests().sample()
        data['trace'].insert(2,[2,0,0,1,1,123,3,1,4,5,2])
        data['trace'][1][4]=0; data['trace'][3][0]=3
        data.update(cycles=4,stalls=1)
        self.assertEqual(data,metrics(data))

    def test_clean_axis_mode(self):
        self.assertNotIn('AXIS_MODE',clean_environment({'AXIS_MODE':'poison'}))

    def test_bypass_stall_before_accept(self):
        data=metric_samples.Tests().sample()
        data['trace'].insert(1,[1,0,0,1,0,123,3,1,4,5,2])
        data['trace'][2][0]=2; data['trace'][3][0]=3
        data.update(cycles=4,stalls=1)
        self.assertEqual(data,metrics(data))

    def test_changed_stalled_payload(self):
        data=metric_samples.Tests().sample()
        data['trace'].insert(1,[1,0,0,1,0,124,3,1,4,5,2])
        data['trace'][2][0]=2; data['trace'][3][0]=3
        data.update(cycles=4,stalls=1)
        with self.assertRaises(ValueError): metrics(data)
