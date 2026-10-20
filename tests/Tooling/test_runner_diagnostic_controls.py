import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'tests/Tooling/managed_compat'))
import dual_runner_ci as ci
import strict_json

class Tests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup)
        self.folder=Path(self.temp.name)

    def probe(self,mode,data):
        result=subprocess.run(['dotnet',str(ROOT/'tests/RunnerValidation/bin/Release/net10.0/RunnerValidation.dll'),mode],input=json.dumps(data),text=True,encoding='utf-8',capture_output=True,check=True,timeout=max(60,data.get('timeout',0)+30))
        return json.loads(result.stdout)

    def process(self,code='pass',**changes):
        data={'executable':sys.executable,'arguments':['-c',code],'directory':str(self.folder),'phase':'simulate','timeout':30,'maxLogBytes':1024}
        data.update(changes)
        return self.probe('process',data)

    def metadata(self):return json.loads((self.folder/'simulate-process.json').read_text())

    def test_log_budget_low(self):
        self.assertFalse(self.process(maxLogBytes=1023)['valid'])

    def test_log_budget_high(self):
        self.assertFalse(self.process(maxLogBytes=64*1024*1024+1)['valid'])

    def test_unknown_phase(self):
        self.assertFalse(self.process(phase='unknown')['valid'])

    def test_timeout_boundaries(self):
        self.assertFalse(self.process(timeout=0)['valid'])
        self.assertFalse(self.process(timeout=3601)['valid'])

    def test_control_executable(self):
        self.assertFalse(self.process(executable='bad\n')['valid'])

    def test_missing_directory(self):
        self.assertFalse(self.process(directory=str(self.folder/'missing'))['valid'])

    def test_null_argument(self):
        self.assertFalse(self.process(arguments=[None])['valid'])

    def test_nul_argument(self):
        self.assertFalse(self.process(arguments=['bad\x00'])['valid'])

    def test_stdout_observed(self):
        self.assertTrue(self.process("import sys; sys.stdout.buffer.write(b'x'*100)")['valid'])
        self.assertEqual(100,self.metadata()['Stdout']['ObservedBytes'])

    def test_stdout_retained(self):
        self.process("import sys; sys.stdout.buffer.write(b'x'*100)")
        self.assertEqual(100,self.metadata()['Stdout']['RetainedBytes'])

    def test_stderr_accounting(self):
        self.process("import sys; sys.stderr.buffer.write(b'x'*123)")
        self.assertEqual(123,self.metadata()['Stderr']['ObservedBytes'])

    def test_truncated_stdout(self):
        self.assertFalse(self.process("import sys; sys.stdout.buffer.write(b'x'*2048)")['valid'])
        self.assertTrue(self.metadata()['Stdout']['Truncated'])
        self.assertEqual(1024,self.metadata()['Stdout']['RetainedBytes'])

    def test_truncated_stderr(self):
        self.assertFalse(self.process("import sys; sys.stderr.buffer.write(b'x'*2048)")['valid'])
        self.assertTrue(self.metadata()['Stderr']['Truncated'])

    def test_failure_detail(self):
        self.assertFalse(self.process('raise SystemExit(7)')['valid'])
        self.assertIn('7',self.metadata()['Failure'])

    def test_budget_metadata(self):
        self.process()
        self.assertEqual(1024,self.metadata()['MaxLogBytes'])

    def test_metadata_temp_cleanup(self):
        self.process()
        self.assertEqual([],list(self.folder.glob('.evidence-*.tmp')))

    def test_early_fault_environment(self):
        self.assertNotIn('CS_INJECT_EARLY',ci.clean_environment({'CS_INJECT_EARLY':'1'}))

    def test_timeout_environment(self):
        self.assertNotIn('CS_WALL_TIMEOUT',ci.clean_environment({'CS_WALL_TIMEOUT':'1'}))

    def test_stale_case_cleanup(self):
        (self.folder/'manifest.json').write_text('stale')
        ci.clear_case(self.folder)
        self.assertFalse((self.folder/'manifest.json').exists())

    def test_stale_child_cleanup(self):
        (self.folder/'native').mkdir()
        path=self.folder/'native'/'icarus-results.json';path.write_text('stale')
        ci.clear_case(self.folder)
        self.assertFalse(path.exists())

    def test_preserve_unrelated_case_file(self):
        path=self.folder/'keep.txt';path.write_text('input')
        ci.clear_case(self.folder)
        self.assertEqual('input',path.read_text())

    def test_native_json_overflow(self):
        result=subprocess.run(['dotnet',str(ROOT/'tests/RunnerValidation/bin/Release/net10.0/RunnerValidation.dll'),'json'],input='1e999',text=True,capture_output=True,check=True)
        self.assertFalse(json.loads(result.stdout)['valid'])

    def test_strict_utf8_file(self):
        path=self.folder/'input.json';path.write_bytes(b'\xff')
        self.assertFalse(self.probe('json-file',{'path':str(path)})['valid'])
        path.write_bytes(b'\xef\xbb\xbf{}')
        self.assertTrue(self.probe('json-file',{'path':str(path)})['valid'])
