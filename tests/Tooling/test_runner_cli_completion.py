"""Production CLI completion with controlled C# child processes; no HDL simulation."""
import json
import os
from pathlib import Path
import subprocess
import unittest
import test_runner_cli

ROOT=Path(__file__).resolve().parents[2]


class Tests(unittest.TestCase):
    setUp=test_runner_cli.Tests.setUp
    manifest=test_runner_cli.Tests.manifest

    def prepare(self,**behavior):
        path,data=self.manifest()
        fixture=ROOT/'tests/RunnerProcessFixture/bin/Release/net10.0'/('RunnerProcessFixture.exe' if os.name=='nt' else 'RunnerProcessFixture')
        self.assertTrue(fixture.is_file(),'Build solution Release first')
        for name in ['input.sv','fixture.dll','host.bin','bootstrap.bin','gpi.bin','gpi-impl.bin']:
            (self.folder/name).write_bytes(b'controlled CLI contract input; not HDL/native code')
        (self.folder/'fixture.runtimeconfig.json').write_text('{}',encoding='utf-8')
        data.update(iverilog=str(fixture),vvp=str(fixture),expectedSeeds={'Entry.Test':data['seed']})
        behavior.setdefault('resultText',json.dumps([dict(Name='Test',QualifiedName='Entry.Test',Seed=data['seed'],Status='passed')]))
        (self.folder/'process-fixture.json').write_text(json.dumps(behavior),encoding='utf-8')
        path.write_text(json.dumps(data),encoding='utf-8')
        return path,data

    def run_cli(self,path,environment=None):
        env=os.environ.copy()
        if environment:env.update(environment)
        return subprocess.run(['dotnet',str(self.cli),'run',str(path)],cwd=self.folder,env=env,
                              # Cover the default120s compile and120s simulation budgets plus startup.
                              text=True,encoding='utf-8',capture_output=True,check=False,timeout=300)

    def artifact(self,name):
        return json.loads((self.folder/'output'/name).read_text(encoding='utf-8'))

    def released(self):
        self.assertFalse((self.folder/'output/.sharptestbench-run.lock').exists())
        self.assertEqual([],list((self.folder/'output').glob('.evidence-*.tmp')))

    def test_success_serializes_complete_run_evidence_with_reflection_disabled(self):
        path,data=self.prepare();result=self.run_cli(path)
        self.assertEqual(0,result.returncode,result.stderr)
        evidence=self.artifact('run-manifest.json');inputs=self.artifact('input-manifest.json')
        self.assertEqual('passed',evidence['Status']);self.assertEqual(1,evidence['Passed']);self.assertEqual(0,evidence['Failed'])
        self.assertEqual(data['seed'],evidence['Seed']);self.assertEqual(['Entry.Test'],evidence['TestNames'])
        self.assertEqual(evidence['RunId'],inputs['RunId']);self.assertEqual(inputs['Inputs'],evidence['Inputs'])
        self.assertEqual(64,len(evidence['AssemblySha256']));self.assertEqual(1,len(evidence['Sources']))
        for phase in ['compile','simulate']:
            process=self.artifact(phase+'-process.json')
            self.assertTrue(process['Started']);self.assertFalse(process['TimedOut']);self.assertEqual(0,process['ExitCode'])
        runtime=json.loads(self.cli.with_suffix('.runtimeconfig.json').read_text(encoding='utf-8'))
        self.assertFalse(runtime['runtimeOptions']['configProperties']['System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault'])
        self.assertFalse((self.folder/'output/failure-manifest.json').exists());self.released()

    def test_failed_functional_result_keeps_run_evidence_and_exits_one(self):
        rows=[dict(Name='Entry.Test',Seed=2**64-1,Status='failed',Error='controlled assertion')]
        path,_=self.prepare(resultText=json.dumps(rows));result=self.run_cli(path)
        self.assertEqual(1,result.returncode,result.stderr)
        evidence=self.artifact('run-manifest.json')
        self.assertEqual('failed',evidence['Status']);self.assertEqual(0,evidence['Passed']);self.assertEqual(1,evidence['Failed'])
        self.assertFalse((self.folder/'output/failure-manifest.json').exists());self.released()

    def test_wrong_expected_seed_rejects_otherwise_complete_result(self):
        path,data=self.prepare();data['expectedSeeds']['Entry.Test']=17;path.write_text(json.dumps(data),encoding='utf-8')
        result=self.run_cli(path)
        self.assertEqual(1,result.returncode,result.stderr)
        failure=self.artifact('failure-manifest.json')
        self.assertEqual('configuration_error',failure['Kind']);self.assertIn('seed differs',failure['Error'])
        self.assertIn('simulate-process.json',failure['LogFiles'])
        self.assertFalse((self.folder/'output/run-manifest.json').exists());self.released()

    def test_explicit_selection_overrides_inherited_filter_and_preserves_uint64_seed(self):
        path,data=self.prepare();data['testNames']=['Entry.Test'];path.write_text(json.dumps(data),encoding='utf-8')
        ambient={key:'ambient-wrong-selection' for key in ['CS_TEST_FILTER','CS_TEST_NAMES','CS_TEST_STAGES','CS_LIST_TESTS','CS_STAGE_FAILURE_POLICY']}
        result=self.run_cli(path,ambient);self.assertEqual(0,result.returncode,result.stderr)
        env=self.artifact('fixture-environment.json')
        self.assertEqual(['Entry.Test'],json.loads(env['CS_TEST_NAMES']));self.assertEqual(str(2**64-1),env['CS_SEED'])
        self.assertTrue(all(env[key] is None for key in ambient if key!='CS_TEST_NAMES'));self.released()

    def test_zero_exit_without_results_cannot_reuse_previous_success(self):
        path,_=self.prepare();first=self.run_cli(path);self.assertEqual(0,first.returncode,first.stderr)
        old_id=self.artifact('run-manifest.json')['RunId']
        path,_=self.prepare(omitResults=True);result=self.run_cli(path)
        self.assertEqual(1,result.returncode,result.stderr)
        failure=self.artifact('failure-manifest.json')
        self.assertEqual('configuration_error',failure['Kind']);self.assertIn('did not produce',failure['Error'])
        self.assertNotEqual(old_id,failure['RunId']);self.assertFalse((self.folder/'output/icarus-results.json').exists())
        self.assertFalse((self.folder/'output/run-manifest.json').exists());self.released()

    def test_duplicate_properties_in_completed_results_fail_closed(self):
        raw='[{"Name":"Entry.Test","Status":"failed","Status":"passed","Seed":18446744073709551615}]'
        path,_=self.prepare(resultText=raw);result=self.run_cli(path)
        self.assertEqual(1,result.returncode,result.stderr)
        failure=self.artifact('failure-manifest.json')
        self.assertEqual('configuration_error',failure['Kind']);self.assertIn('Duplicate',failure['Error'])
        self.assertEqual(raw,(self.folder/'output/icarus-results.json').read_text(encoding='utf-8'))
        self.assertIn('simulate-process.json',failure['LogFiles'])
        self.assertFalse((self.folder/'output/run-manifest.json').exists());self.released()

    def test_changed_source_after_capture_cannot_produce_a_success_manifest(self):
        for phase in ['compile','simulate']:
            with self.subTest(phase=phase):
                path,_=self.prepare(mutatePhase=phase);result=self.run_cli(path)
                self.assertEqual(1,result.returncode,result.stderr)
                failure=self.artifact('failure-manifest.json')
                self.assertEqual('configuration_error',failure['Kind']);self.assertIn('changed',failure['Error'].lower())
                self.assertTrue((self.folder/'output/icarus-results.json').is_file())
                self.assertFalse((self.folder/'output/run-manifest.json').exists());self.released()

    def test_report_failure_marker_overrides_valid_results_and_zero_process_exit(self):
        for stream in ['stdout','stderr']:
            with self.subTest(stream=stream):
                path,_=self.prepare(reportFailureStream=stream);result=self.run_cli(path)
                self.assertEqual(1,result.returncode,result.stderr)
                failure=self.artifact('failure-manifest.json')
                self.assertEqual('infrastructure_failure',failure['Kind'])
                self.assertEqual(0,self.artifact('simulate-process.json')['ExitCode'])
                self.assertIn('CS_REPORT_FAILURE',(self.folder/f'output/simulate.{stream}.log').read_text(encoding='utf-8'))
                self.assertTrue((self.folder/'output/icarus-results.json').is_file())
                self.assertFalse((self.folder/'output/run-manifest.json').exists());self.released()

    def test_simulation_timeout_retains_evidence_then_releases_workspace_for_recovery(self):
        # Compilation and simulation share one per-phase deadline. Give the controlled
        # compiler startup margin, then force only simulation beyond that budget.
        path,data=self.prepare(simulateDelayMs=30000);data['timeoutSeconds']=10;path.write_text(json.dumps(data),encoding='utf-8')
        result=self.run_cli(path);self.assertEqual(1,result.returncode,result.stderr)
        compiled=self.artifact('compile-process.json')
        self.assertTrue(compiled['Started']);self.assertFalse(compiled['TimedOut'],result.stderr)
        self.assertEqual(0,compiled['ExitCode'],result.stderr)
        failure=self.artifact('failure-manifest.json');process=self.artifact('simulate-process.json')
        self.assertEqual('timeout',failure['Kind']);self.assertTrue(process['Started']);self.assertTrue(process['TimedOut'])
        self.assertIn('simulate-process.json',failure['LogFiles']);self.released()
        path,_=self.prepare();recovery=self.run_cli(path);self.assertEqual(0,recovery.returncode,recovery.stderr)
        self.assertNotEqual(failure['RunId'],self.artifact('run-manifest.json')['RunId'])
        self.assertFalse((self.folder/'output/failure-manifest.json').exists());self.released()
