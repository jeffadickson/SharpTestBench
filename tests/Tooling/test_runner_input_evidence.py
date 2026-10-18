import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
ROOT=Path(__file__).resolve().parents[2]

class Tests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup)
        self.folder=Path(self.temp.name)
        self.file=self.folder/'input.txt';self.file.write_bytes(b'original')

    def probe(self,mode,payload):
        result=subprocess.run(['dotnet',str(ROOT/'tests/RunnerValidation/bin/Release/net10.0/RunnerValidation.dll'),mode],input=json.dumps(payload),text=True,capture_output=True,check=True)
        return json.loads(result.stdout)

    def snapshot(self,paths=None,before=None):
        data={'paths':[str(path) for path in (paths if paths is not None else [self.file])]}
        if before is not None: data['before']=before
        return self.probe('fingerprint',data)

    def test_sha256(self):
        actual=self.snapshot()['fingerprints'][0]
        self.assertEqual(hashlib.sha256(b'original').hexdigest().upper(),actual['Sha256'])

    def test_byte_length(self):
        self.assertEqual(8,self.snapshot()['fingerprints'][0]['Bytes'])

    def test_canonical_path(self):
        self.assertTrue(self.file.samefile(self.snapshot()['fingerprints'][0]['Path']))
        self.assertTrue(Path(self.snapshot()['fingerprints'][0]['Path']).is_absolute())

    def test_role(self):
        self.assertEqual('input',self.snapshot()['fingerprints'][0]['Role'])

    def test_empty_file(self):
        self.file.write_bytes(b'')
        self.assertEqual(0,self.snapshot()['fingerprints'][0]['Bytes'])

    def test_unicode_bytes(self):
        self.file.write_bytes('λ'.encode())
        self.assertEqual(2,self.snapshot()['fingerprints'][0]['Bytes'])

    def test_binary_bytes(self):
        self.file.write_bytes(bytes(range(256)))
        self.assertEqual(hashlib.sha256(bytes(range(256))).hexdigest().upper(),self.snapshot()['fingerprints'][0]['Sha256'])

    def test_spaces(self):
        path=self.folder/'with spaces.txt'
        path.write_text('data')
        self.assertTrue(self.snapshot([path])['valid'])

    def test_unicode_path(self):
        path=self.folder/'λ.txt'
        path.write_text('data')
        self.assertTrue(self.snapshot([path])['valid'])

    def test_missing_file(self):
        self.file.unlink()
        self.assertFalse(self.snapshot()['valid'])

    def test_directory_input(self):
        self.assertFalse(self.snapshot([self.folder])['valid'])

    def test_stable_snapshot(self):
        before=self.snapshot()['fingerprints']
        self.assertTrue(self.snapshot(before=before)['valid'])

    def test_changed_bytes(self):
        before=self.snapshot()['fingerprints']
        self.file.write_bytes(b'changed!')
        self.assertFalse(self.snapshot(before=before)['valid'])

    def test_changed_size(self):
        before=self.snapshot()['fingerprints']
        self.file.write_bytes(b'longer than original')
        self.assertFalse(self.snapshot(before=before)['valid'])

    def test_changed_newline(self):
        before=self.snapshot()['fingerprints']
        self.file.write_bytes(b'original\n')
        self.assertFalse(self.snapshot(before=before)['valid'])

    def test_added_input(self):
        before=self.snapshot()['fingerprints']
        other=self.folder/'second';other.write_text('data')
        self.assertFalse(self.snapshot([self.file,other],before)['valid'])

    def test_removed_input(self):
        before=self.snapshot()['fingerprints']
        self.assertFalse(self.snapshot([],before)['valid'])

    def test_ordering(self):
        other=self.folder/'aaa';other.write_text('data')
        a=self.snapshot([self.file,other])['fingerprints']
        b=self.snapshot([other,self.file])['fingerprints']
        self.assertEqual(a,b)

    def test_renamed_input(self):
        before=self.snapshot()['fingerprints']
        other=self.folder/'renamed';self.file.rename(other)
        self.assertFalse(self.snapshot([other],before)['valid'])

    def test_atomic_create(self):
        path=self.folder/'report.json'
        self.assertTrue(self.probe('evidence',{'path':str(path),'value':{'passed':True}})['valid'])
        self.assertTrue(json.loads(path.read_text())['passed'])

    def test_atomic_replace(self):
        path=self.folder/'report.json';path.write_text('stale')
        self.assertTrue(self.probe('evidence',{'path':str(path),'value':{'passed':False}})['valid'])
        self.assertFalse(json.loads(path.read_text())['passed'])

    def test_nested_evidence(self):
        path=self.folder/'nested'/'report.json'
        self.assertTrue(self.probe('evidence',{'path':str(path),'value':{}})['valid'])
        self.assertTrue(path.is_file())

    def test_evidence_cleanup(self):
        path=self.folder/'report.json'
        self.probe('evidence',{'path':str(path),'value':{}})
        self.assertEqual([],list(self.folder.glob('.evidence-*.tmp')))

    def test_failed_evidence_cleanup(self):
        self.assertFalse(self.probe('evidence',{'path':str(self.folder),'value':{}})['valid'])
        self.assertEqual([],list(self.folder.glob('.evidence-*.tmp')))

    def test_evidence_unicode(self):
        path=self.folder/'report.json'
        self.assertTrue(self.probe('evidence',{'path':str(path),'value':{'value':'λ'}})['valid'])
        self.assertEqual('λ',json.loads(path.read_text())['value'])
