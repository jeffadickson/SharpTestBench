import json
from pathlib import Path
import sys
import unittest
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools'))
import dual_runner_parity as parity

import os
import subprocess
import tempfile

class Tests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.folder=Path(self.temp.name)

    def probe(self, mode, data, environment=None):
        executable=ROOT/'tests/RunnerValidation/bin/Release/net10.0/RunnerValidation.dll'
        result=subprocess.run(['dotnet',str(executable),mode],input=json.dumps(data),capture_output=True,text=True,check=True,env=environment,timeout=max(60,data.get('timeout',0)+30))
        return json.loads(result.stdout)

    def process(self, code, phase='simulate', timeout=30, environment=None, arguments=None):
        return self.probe('process',{'executable':sys.executable,'arguments':['-c',code]+(arguments or []),
                         'directory':str(self.folder),'phase':phase,'timeout':timeout,'environment':environment})

    def metadata(self, phase='simulate'):
        return json.loads((self.folder/(phase+'-process.json')).read_text())

    def workspace(self, contend=False):
        return self.probe('workspace',{'directory':str(self.folder),'contend':contend})

    def manifest(self, **changes):
        data={'schemaVersion':1,'simulator':'icarus','top':'top','sources':['top.sv'],'assembly':'a.dll',
              'nativeHost':'host.so','icarusBootstrap':'b.vpi','gpiLibrary':'g.so','dotnetRoot':'sdk',
              'outputDirectory':'out','gpiImpl':'impl','expectedTests':1}
        data.update(changes)
        return self.probe('manifest',data)


    def test_stdout(self):
        self.assertTrue(self.process("print('out')")['valid'])
        self.assertEqual('out',(self.folder/'simulate.stdout.log').read_text().strip())

    def test_stderr(self):
        self.assertTrue(self.process("import sys; print('err',file=sys.stderr)")['valid'])
        self.assertEqual('err',(self.folder/'simulate.stderr.log').read_text().strip())

    def test_arguments_spaces(self):
        self.assertTrue(self.process('import sys; print(sys.argv[1])',arguments=['a b; literal'])['valid'])
        self.assertEqual('a b; literal',(self.folder/'simulate.stdout.log').read_text().strip())

    def test_unicode_output(self):
        self.assertTrue(self.process("import sys; sys.stdout.buffer.write('λ'.encode())")['valid'])
        self.assertEqual('λ',(self.folder/'simulate.stdout.log').read_text(encoding='utf-8'))

    def test_compile_exit(self):
        self.assertEqual('build_failure',self.process('raise SystemExit(7)',phase='compile')['kind'])
        self.assertEqual(7,self.metadata('compile')['ExitCode'])

    def test_simulate_exit(self):
        self.assertEqual('simulator_failure',self.process('raise SystemExit(9)')['kind'])
        self.assertEqual(9,self.metadata()['ExitCode'])

    def test_missing_compile(self):
        data={"executable":str(self.folder/"nonexistent-tool"),"arguments":[],"directory":str(self.folder),"phase":'compile',"timeout":5}
        self.assertEqual('build_failure',self.probe("process",data)["kind"])
        self.assertFalse(self.metadata('compile')["Started"])

    def test_missing_simulate(self):
        data={"executable":str(self.folder/"nonexistent-tool"),"arguments":[],"directory":str(self.folder),"phase":'simulate',"timeout":5}
        self.assertEqual('simulator_failure',self.probe("process",data)["kind"])
        self.assertFalse(self.metadata('simulate')["Started"])

    def test_timeout(self):
        self.assertEqual('timeout',self.process('import time; time.sleep(4)',timeout=1)['kind'])
        self.assertTrue(self.metadata()['TimedOut'])

    def test_selection_environment(self):
        environment=dict(os.environ,CS_TEST_FILTER='hidden',CS_TEST_NAMES='[]',CS_LIST_TESTS='1')
        data={'executable':sys.executable,'arguments':['-c',"import os; print(any(k in os.environ for k in ['CS_TEST_FILTER','CS_TEST_NAMES','CS_LIST_TESTS']))"],'directory':str(self.folder),'phase':'simulate','timeout':5}
        self.assertTrue(self.probe('process',data,environment)['valid'])
        self.assertEqual('False',(self.folder/'simulate.stdout.log').read_text().strip())

    def test_explicit_environment(self):
        self.assertTrue(self.process("import os; print(os.environ['STB_TEST_VALUE'])",environment={'STB_TEST_VALUE':'literal value'})['valid'])
        self.assertEqual('literal value',(self.folder/'simulate.stdout.log').read_text().strip())

    def test_bounded_output(self):
        actual=self.process("import sys; sys.stdout.buffer.write(b'x'*(17*1024*1024))")
        self.assertFalse(actual['valid'])
        self.assertEqual('simulator_failure',actual['kind'])
        self.assertFalse(self.metadata()['TimedOut'])
        self.assertTrue(self.metadata()['Stdout']['Truncated'])
        self.assertEqual(17*1024*1024,self.metadata()['Stdout']['ObservedBytes'])
        self.assertEqual(16*1024*1024,(self.folder/'simulate.stdout.log').stat().st_size)
        self.assertTrue(self.metadata()['Started'])

    def test_success_metadata(self):
        self.assertTrue(self.process('pass')['valid'])
        self.assertEqual(0,self.metadata()['ExitCode'])
        self.assertFalse(self.metadata()['TimedOut'])

    def test_failure_logs(self):
        actual=self.process("import sys; print('diagnostic',file=sys.stderr); raise SystemExit(3)")
        self.assertEqual('simulator_failure',actual['kind'])
        self.assertEqual(3,self.metadata()['ExitCode'])
        self.assertFalse(self.metadata()['TimedOut'])
        self.assertIn('diagnostic',(self.folder/'simulate.stderr.log').read_text())

    def test_workspace_contention(self):
        self.assertFalse(self.workspace(contend=True)['valid'])
        self.assertFalse((self.folder/'.sharptestbench-run.lock').exists())

    def test_clear_success_evidence(self):
        path=self.folder/'run-manifest.json'
        path.write_text('stale')
        self.assertTrue(self.workspace()['valid'])
        self.assertFalse(path.exists())

    def test_clear_results(self):
        path=self.folder/'icarus-results.json'
        path.write_text('stale')
        self.assertTrue(self.workspace()['valid'])
        self.assertFalse(path.exists())

    def test_preserve_unrelated(self):
        path=self.folder/'keep.txt'
        path.write_text('input')
        self.assertTrue(self.workspace()['valid'])
        self.assertEqual('input',path.read_text())

    def test_workspace_release(self):
        self.assertTrue(self.workspace()['valid'])
        self.assertTrue(self.workspace()['valid'])

    def test_output_parent(self):
        self.assertFalse(self.probe('output',{'root':str(self.folder),'output':'../escape'})['valid'])

    def test_output_root(self):
        self.assertFalse(self.probe('output',{'root':str(self.folder),'output':'.'})['valid'])

    def test_output_nested(self):
        self.assertTrue(self.probe('output',{'root':str(self.folder),'output':'build/with spaces'})['valid'])
        if os.name == 'nt':
            self.assertTrue(self.probe('output',{'root':str(self.folder),'output':str(self.folder).upper()+'/child'})['valid'])

    def test_timeout_lower_bound(self):
        self.assertFalse(self.manifest(timeoutSeconds=0)['valid'])

    def test_timeout_upper_bound(self):
        self.assertFalse(self.manifest(timeoutSeconds=3601)['valid'])

    def test_unsigned_seed(self):
        self.assertTrue(self.manifest(seed=2**64-1)['valid'])
