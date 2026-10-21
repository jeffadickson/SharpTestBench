"""Artifact integrity and process evidence through the production Runner CLI."""
import hashlib
import json
import unittest
import test_runner_cli_completion as completion


class Tests(unittest.TestCase):
    setUp=completion.Tests.setUp
    manifest=completion.Tests.manifest
    prepare=completion.Tests.prepare
    run_cli=completion.Tests.run_cli
    artifact=completion.Tests.artifact
    released=completion.Tests.released

    def failed(self,result,kind):
        self.assertEqual(1,result.returncode,result.stderr)
        evidence=self.artifact('failure-manifest.json')
        self.assertEqual(kind,evidence['Kind'])
        self.assertFalse((self.folder/'output/run-manifest.json').exists());self.released()
        return evidence

    def test_missing_compiler_records_unstarted_phase_and_preserves_inputs(self):
        path,data=self.prepare();data['iverilog']=str(self.folder/'missing compiler executable')
        path.write_text(json.dumps(data),encoding='utf-8');source=(self.folder/'input.sv').read_bytes()
        result=self.run_cli(path);failure=self.failed(result,'build_failure')
        process=self.artifact('compile-process.json')
        self.assertFalse(process['Started']);self.assertIsNone(process['ExitCode']);self.assertFalse(process['TimedOut'])
        self.assertIn('compile-process.json',failure['LogFiles'])
        self.assertEqual(source,(self.folder/'input.sv').read_bytes())
        self.assertFalse((self.folder/'output/simulate-process.json').exists())

    def test_nonzero_simulator_exit_overrides_passing_result_bytes(self):
        path,_=self.prepare(simulateExit=7);result=self.run_cli(path)
        self.failed(result,'simulator_failure')
        self.assertEqual(7,self.artifact('simulate-process.json')['ExitCode'])
        self.assertEqual('passed',self.artifact('icarus-results.json')[0]['Status'])

    def test_zero_exit_compiler_must_produce_a_nonempty_image_before_simulation(self):
        for behavior in [dict(omitCompiled=True),dict(emptyCompiled=True)]:
            with self.subTest(behavior=behavior):
                path,_=self.prepare(**behavior);result=self.run_cli(path)
                failure=self.failed(result,'build_failure')
                self.assertIn('nonempty output',failure['Error'])
                self.assertEqual(0,self.artifact('compile-process.json')['ExitCode'])
                self.assertFalse((self.folder/'output/simulate-process.json').exists())
                self.assertFalse((self.folder/'output/icarus-results.json').exists())

    def test_completed_run_fingerprints_the_exact_compiled_image(self):
        path,_=self.prepare();result=self.run_cli(path);self.assertEqual(0,result.returncode,result.stderr)
        compiled=self.folder/'output/top.vvp';data=compiled.read_bytes()
        evidence=self.artifact('run-manifest.json')['Compiled']
        self.assertEqual('compiled-image',evidence['Role']);self.assertEqual(str(compiled),evidence['Path'])
        self.assertEqual(len(data),evidence['Bytes']);self.assertGreater(evidence['Bytes'],0)
        self.assertEqual(hashlib.sha256(data).hexdigest().upper(),evidence['Sha256']);self.released()

    def test_simulator_cannot_modify_the_compiled_image_and_still_attest_success(self):
        path,_=self.prepare(mutateCompiled=True);result=self.run_cli(path)
        failure=self.failed(result,'configuration_error')
        self.assertIn('changed',failure['Error'].lower())
        self.assertEqual(0,self.artifact('simulate-process.json')['ExitCode'])
        self.assertEqual('passed',self.artifact('icarus-results.json')[0]['Status'])

    def test_over_budget_simulation_log_cannot_hide_behind_valid_results(self):
        path,data=self.prepare(stdoutBytes=8192);data['maxLogBytes']=1024;path.write_text(json.dumps(data),encoding='utf-8')
        result=self.run_cli(path);failure=self.failed(result,'simulator_failure')
        process=self.artifact('simulate-process.json')
        self.assertEqual(0,process['ExitCode']);self.assertTrue(process['Stdout']['Truncated'])
        self.assertEqual(1024,process['Stdout']['RetainedBytes']);self.assertGreater(process['Stdout']['ObservedBytes'],1024)
        self.assertEqual(1024,(self.folder/'output/simulate.stdout.log').stat().st_size)
        self.assertIn('byte limit',failure['Error'])

    def test_non_utf8_result_bytes_are_rejected_after_successful_process(self):
        path,_=self.prepare(resultHex='5BFF5D');result=self.run_cli(path)
        failure=self.failed(result,'configuration_error')
        self.assertIn('DecoderFallbackException',failure['ErrorType'])
        self.assertEqual(b'[\xff]',(self.folder/'output/icarus-results.json').read_bytes())
        self.assertEqual(0,self.artifact('simulate-process.json')['ExitCode'])

    def test_skipped_required_case_cannot_count_as_a_successful_manifest(self):
        rows=[dict(Name='Entry.Test',Seed=2**64-1,Status='skipped')]
        path,_=self.prepare(resultText=json.dumps(rows));result=self.run_cli(path)
        failure=self.failed(result,'configuration_error');self.assertIn('Skipped',failure['Error'])
        self.assertEqual(0,self.artifact('simulate-process.json')['ExitCode'])

    def test_later_compile_failure_retires_prior_success_and_simulation_evidence(self):
        path,_=self.prepare();first=self.run_cli(path);self.assertEqual(0,first.returncode,first.stderr)
        old_id=self.artifact('run-manifest.json')['RunId']
        path,_=self.prepare(compileExit=9);result=self.run_cli(path)
        failure=self.failed(result,'build_failure');self.assertNotEqual(old_id,failure['RunId'])
        self.assertEqual(9,self.artifact('compile-process.json')['ExitCode'])
        self.assertFalse((self.folder/'output/icarus-results.json').exists())
        self.assertFalse((self.folder/'output/simulate-process.json').exists())
        self.assertFalse((self.folder/'output/simulate.stdout.log').exists())

    def test_multicase_completion_attests_identity_seeds_independent_of_result_order(self):
        rows=[dict(Name='b',Seed=2**64-1,Status='passed'),dict(Name='a',Seed=0,Status='passed')]
        path,data=self.prepare(resultText=json.dumps(rows))
        data.update(expectedTests=2,expectedNames=['a','b'],expectedSeeds={'a':0,'b':2**64-1})
        path.write_text(json.dumps(data),encoding='utf-8');result=self.run_cli(path)
        self.assertEqual(0,result.returncode,result.stderr)
        run=self.artifact('run-manifest.json');self.assertEqual(2,run['Passed']);self.assertEqual(['a','b'],run['TestNames'])
        data['expectedSeeds']={'a':2**64-1,'b':0};path.write_text(json.dumps(data),encoding='utf-8')
        result=self.run_cli(path);failure=self.failed(result,'configuration_error')
        self.assertIn('seed differs',failure['Error'])
