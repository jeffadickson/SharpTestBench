"""Production legacy CLI ownership checks precede cleanup and compiler startup."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT=Path(__file__).resolve().parents[2]


class Tests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(prefix='stb legacy safety ');self.addCleanup(self.temp.cleanup)
        self.folder=Path(self.temp.name);self.work=self.folder/'output';self.work.mkdir()
        self.cli=Path(os.environ.get('STB_MANAGED_TEST_API_DLL',str(ROOT/'tools/SharpTestBench.Tools/bin/Release/net10.0/SharpTestBench.Tools.dll')))

    def run_cli(self,source,*extra):
        return subprocess.run(['dotnet',str(self.cli),'run','--no-build','--work-dir',str(self.work),'--label','guard',
                              '--assembly',str(self.folder/'missing.dll'),'--source',str(source),*map(str,extra)],
                              cwd=ROOT,text=True,encoding='utf-8',capture_output=True,check=False,timeout=30)

    def previous(self):
        files={}
        for suffix in ['.json','.xml','.coverage.json','.discovery.json']:
            path=self.work/('guard-results'+suffix);path.write_bytes(b'old successful evidence');files[path]=path.read_bytes()
        return files

    def released(self):self.assertFalse((self.work/'.sharptestbench-run.lock').exists())

    def test_waveform_input_alias_rejects_before_deleting_source(self):
        source=self.folder/'input.vcd';source.write_bytes(b'protected HDL input')
        result=self.run_cli(source,'--waves',source)
        self.assertEqual(1,result.returncode);self.assertIn('collides with an input',result.stderr)
        self.assertEqual(b'protected HDL input',source.read_bytes());self.released()

    def test_compiler_and_bindings_input_aliases_survive_rejection(self):
        for name in ['top.vvp','DutBindings.cs','guard-results.coverage.json','guard-compile.json']:
            source=self.work/name;source.write_bytes(b'protected input')
            result=self.run_cli(source)
            self.assertEqual(1,result.returncode);self.assertIn('collides with an input',result.stderr)
            self.assertEqual(b'protected input',source.read_bytes());self.released()

    def test_log_destination_cannot_overwrite_a_source(self):
        source=self.folder/'input.sv';source.write_bytes(b'protected source')
        result=self.run_cli(source,'--log-json',source)
        self.assertEqual(1,result.returncode);self.assertIn('collides with an input',result.stderr)
        self.assertEqual(b'protected source',source.read_bytes());self.released()

    def test_preparation_failure_retires_all_previous_result_sidecars(self):
        old=self.previous();source=self.folder/'input.sv';source.write_bytes(b'source')
        result=self.run_cli(source)
        self.assertEqual(1,result.returncode);self.assertIn('Test DLL and runtimeconfig',result.stderr)
        self.assertTrue(all(not path.exists() for path in old));self.assertEqual(b'source',source.read_bytes());self.released()

    def test_ownership_rejection_preserves_even_a_prior_result_used_as_input(self):
        old=self.previous();source=self.work/'guard-results.json';result=self.run_cli(source)
        self.assertEqual(1,result.returncode);self.assertIn('collides with an input',result.stderr)
        self.assertEqual(old,{path:path.read_bytes() for path in old});self.released()

    def test_output_output_alias_rejects_before_retiring_evidence(self):
        old=self.previous();source=self.folder/'input.sv';source.write_bytes(b'source')
        result=self.run_cli(source,'--log-json',self.work/'guard-results.json')
        self.assertEqual(1,result.returncode);self.assertIn('alias each other',result.stderr)
        self.assertEqual(old,{path:path.read_bytes() for path in old});self.released()

    def test_case_alias_policy_is_platform_specific_without_source_damage(self):
        source=self.work/'TOP.vvp';source.write_bytes(b'source');result=self.run_cli(source)
        self.assertEqual(1,result.returncode)
        self.assertIn('collides with an input' if os.name=='nt' else 'Test DLL and runtimeconfig',result.stderr)
        self.assertEqual(b'source',source.read_bytes());self.released()

    def test_ghdl_generated_directory_input_is_rejected_before_adapter_or_compiler(self):
        generated=self.work/'ghdl-work';generated.mkdir()
        for name in ['top','top.exe','work-obj08.cf','unit.o']:
            source=generated/name;source.write_bytes(b'protected VHDL input')
            result=self.run_cli(source,'--sim','ghdl')
            self.assertEqual(1,result.returncode);self.assertIn('contains an input',result.stderr)
            self.assertEqual(b'protected VHDL input',source.read_bytes());self.released()

    def controlled(self, cli_options=(), mock_adapter=False, child_environment=None, **behavior):
        fixture=ROOT/'tests/RunnerProcessFixture/bin/Release/net10.0'/('RunnerProcessFixture.exe' if os.name=='nt' else 'RunnerProcessFixture')
        if mock_adapter:
            for name in ['bootstrap.bin','gpi.bin','implementation.bin']:
                target=self.folder/name
                if not target.exists():target.write_bytes(b'protected adapter input')
            behavior['adapter']=dict(Version='controlled',Python=str(fixture),LibsDir=str(self.folder),Bootstrap=str(self.folder/'bootstrap.bin'),
                GpiEntryPoint=str(self.folder/'gpi.bin')+',entry',Implementation=str(self.folder/'implementation.bin')+':entry',PyGpiEntryPoint=None,LibPython=None)
        config=self.folder/'process-fixture.json';config.write_text(json.dumps({'resultText':'[{"Name":"Contract","Status":"passed","Seed":1}]',**behavior}),encoding='utf-8')
        assembly=self.folder/'controlled.dll';assembly.write_bytes(b'controlled assembly placeholder')
        assembly.with_suffix('.runtimeconfig.json').write_text('{}',encoding='utf-8')
        source=self.folder/'input.sv';source.write_text('module top; endmodule',encoding='utf-8')
        host=self.folder/'host.bin';host.write_bytes(b'controlled native host placeholder')
        env=dict(os.environ,CS_IVERILOG=str(fixture),CS_VVP=str(fixture),STB_PROCESS_FIXTURE_CONFIG=str(config))
        env.pop("CS_IVL_ROOT",None)
        if mock_adapter:env["CS_PYTHON"]=str(fixture)
        if child_environment:env.update(child_environment)
        return subprocess.run(['dotnet',str(self.cli),'run','--no-build','--work-dir',str(self.work),'--label','guard',
                               '--assembly',str(assembly),'--source',str(source),'--host',str(host),*cli_options],cwd=ROOT,env=env,
                               # The CLI defaults to 120s each for compilation and simulation. This
                               # outer rescue must cover both phases plus instrumented startup;
                               # status/error/lock assertions below remain the acceptance checks.
                               text=True,encoding='utf-8',capture_output=True,check=False,timeout=300)

    def test_controlled_compilation_records_exact_image_and_accepts_results(self):
        import hashlib
        result=self.controlled();self.assertEqual(0,result.returncode,result.stderr)
        metadata=json.loads((self.work/'guard-compile.json').read_text(encoding='utf-8'))
        self.assertEqual('compiled',metadata['status'])
        self.assertEqual(hashlib.sha256((self.work/'top.vvp').read_bytes()).hexdigest().upper(),metadata['image']['Sha256'])
        self.released()

    def test_successful_compiler_cannot_reuse_prior_image_when_output_missing_or_empty(self):
        for behavior in [{'omitCompiled':True},{'emptyCompiled':True}]:
            (self.work/'top.vvp').write_bytes(b'stale executable image');old=self.previous()
            result=self.controlled(**behavior)
            self.assertEqual(1,result.returncode);self.assertIn('nonempty output image',result.stderr)
            self.assertFalse((self.work/'guard-compile.json').exists())
            self.assertTrue(all(not path.exists() for path in old));self.released()

    def test_passing_result_does_not_hide_compiled_image_mutation(self):
        result=self.controlled(mutateCompiled=True)
        self.assertEqual(1,result.returncode);self.assertIn('changed',result.stderr)
        self.assertTrue((self.work/'guard-results.json').exists());self.released()

    def test_failed_compiler_retires_previous_success_and_does_not_start_simulation(self):
        old=self.previous();result=self.controlled(compileExit=7)
        self.assertEqual(1,result.returncode);self.assertTrue(all(not path.exists() for path in old))
        self.assertFalse((self.work/'fixture-environment.json').exists());self.released()

    def test_automatic_managed_build_rejects_input_in_intermediate_tree(self):
        source=ROOT/'tests/Integration/obj/Release/net10.0/ownership-guard-never-written.sv'
        self.assertFalse(source.exists())
        result=subprocess.run(['dotnet',str(self.cli),'run','--source',str(source)],cwd=ROOT,
                              text=True,encoding='utf-8',capture_output=True,check=False,timeout=30)
        self.assertEqual(1,result.returncode);self.assertIn('contains an input',result.stderr)
        self.assertFalse(source.exists())

    def test_unknown_completion_status_is_not_a_success(self):
        for status in ['unknown','PASSED','',None,True,17]:
            with self.subTest(status=status):
                result=self.controlled(resultText=json.dumps([dict(Name='Contract',Seed=1,Status=status)]))
                self.assertEqual(1,result.returncode);self.assertIn('test status',result.stderr);self.released()

    def test_malformed_or_duplicate_completion_identities_are_rejected(self):
        for rows in [[dict(Name=name,Seed=1,Status='passed')] for name in ['', ' padded','bad\u0085name',True]]+[
                [dict(Name='same',Seed=1,Status='passed'),dict(Name='same',Seed=2,Status='passed')]]:
            result=self.controlled(resultText=json.dumps(rows));self.assertEqual(1,result.returncode)
            self.assertIn('identity',result.stderr.lower());self.released()

    def test_completion_seed_must_be_uint64(self):
        for seed in [None,True,-1,2**64,1.5,'1']:
            result=self.controlled(resultText=json.dumps([dict(Name='Contract',Seed=seed,Status='passed')]))
            self.assertEqual(1,result.returncode);self.assertIn('64-bit Seed',result.stderr);self.released()
        result=self.controlled(resultText=json.dumps([dict(Name='Contract',Seed=2**64-1,Status='passed')]))
        self.assertEqual(0,result.returncode,result.stderr);self.released()

    def test_generic_run_preserves_skips_while_rejecting_empty_or_nonarray_completion(self):
        result=self.controlled(resultText=json.dumps([dict(Name='pass',Seed=0,Status='passed'),dict(Name='skip',Seed=1,Status='skipped')]))
        self.assertEqual(0,result.returncode,result.stderr)
        for rows in [[],{},None,[17]]:
            result=self.controlled(resultText=json.dumps(rows));self.assertEqual(1,result.returncode);self.released()

    def test_compiler_options_preserve_independent_expected_argv_and_literal_expressions(self):
        first=self.folder/'include first';second=self.folder/'include second'
        first.mkdir();second.mkdir()
        result=self.controlled(cli_options=['--hdl-standard','2005-sv','--include-dir',str(second),'--include-dir',str(first),
            '--define','Z=4==4','--define','BARE','--define','A=hello world','--parameter','WIDTH=8','--parameter',"OFFSET=(1 << 2)"])
        self.assertEqual(0,result.returncode,result.stderr)
        actual=json.loads((self.folder/'fixture-compile-argv.json').read_text(encoding='utf-8'))
        self.assertEqual(['-g2005-sv','-s','top','-o',str(self.work/'top.vvp'),'-I',str(second),'-I',str(first),
            '-D','A=hello world','-D','BARE','-D','Z=4==4','-P','top.OFFSET=(1 << 2)','-P','top.WIDTH=8',str(self.folder/'input.sv')],actual)
        self.released()

    def test_invalid_duplicate_and_empty_compiler_options_reject_before_launch(self):
        for options in [['--hdl-standard','2099'],['--define','A=1','--define','A=2'],['--define','bad.name=1'],
                        ['--define','A='],['--parameter','WIDTH'],['--parameter','WIDTH='],
                        ['--parameter','WIDTH=8','--parameter','WIDTH=4'],['--include-dir','']]:
            result=self.controlled(cli_options=options)
            self.assertEqual(1,result.returncode);self.assertFalse((self.folder/'fixture-compile-argv.json').exists());self.released()

    def test_automatic_build_rejects_include_trees_overlapping_managed_outputs(self):
        source=self.folder/'input.sv';source.write_bytes(b'protected source')
        for tree in ['bin','obj']:
            result=subprocess.run(['dotnet',str(self.cli),'run','--source',str(source),'--include-dir',str(ROOT/'tests/Integration'/tree)],
                cwd=ROOT,text=True,encoding='utf-8',capture_output=True,check=False,timeout=30)
            self.assertEqual(1,result.returncode);self.assertIn('overlaps an include directory',result.stderr)
            self.assertEqual(b'protected source',source.read_bytes())

    def test_implementation_library_cannot_be_used_as_log_destination(self):
        target=self.folder/'implementation.bin';target.write_bytes(b'protected implementation')
        old=self.previous();result=self.controlled(cli_options=['--log-json',str(target)],mock_adapter=True,writeLog=True)
        self.assertEqual(1,result.returncode);self.assertIn('collides with an input',result.stderr)
        self.assertEqual(b'protected implementation',target.read_bytes())
        self.assertEqual(old,{path:path.read_bytes() for path in old});self.released()

    def test_managed_dependency_cannot_be_used_as_log_destination(self):
        target=self.folder/'extra dependency.dll';target.write_bytes(b'protected managed dependency')
        old=self.previous();result=self.controlled(cli_options=['--log-json',str(target)],mock_adapter=True,writeLog=True)
        self.assertEqual(1,result.returncode);self.assertIn('collides with an input',result.stderr)
        self.assertEqual(b'protected managed dependency',target.read_bytes())
        self.assertEqual(old,{path:path.read_bytes() for path in old});self.released()

    def failure(self):
        data=json.loads((self.work/'guard-failure.json').read_text(encoding='utf-8'))
        self.assertEqual(1,data['schemaVersion']);self.assertEqual('failed',data['status'])
        self.assertEqual(32,len(data['runId']));int(data['runId'],16)
        self.assertTrue(data['errorType']);self.assertTrue(data['error']);self.assertTrue(data['recordedAtUtc'])
        self.assertTrue(all((self.work/name).is_file() for name in data['artifacts']))
        self.assertEqual([],list(self.work.glob('*.tmp')));self.released();return data

    def test_failure_evidence_distinguishes_compile_simulate_configuration_and_publication(self):
        cases=[({'compileExit':7},'compile','build_failure'),({'simulateExit':9},'simulate','simulator_failure'),
               ({'omitResults':True},'attest','configuration_error'),({'resultText':'{'},'attest','configuration_error'),
               ({'reportFailureStream':'stderr'},'attest','infrastructure_failure')]
        ids=[]
        for behavior,phase,kind in cases:
            result=self.controlled(mock_adapter=True,**behavior);self.assertEqual(1,result.returncode)
            evidence=self.failure();ids.append(evidence['runId'])
            self.assertEqual(phase,evidence['phase']);self.assertEqual(kind,evidence['kind'])
            if behavior.get('simulateExit'):self.assertEqual(9,evidence['simulatorExitCode'])
        self.assertEqual(len(ids),len(set(ids)))

    def test_functional_failure_is_distinct_from_infrastructure_and_success_retires_failure(self):
        rows=[dict(Name='Contract',Seed=1,Status='failed',Error='controlled assertion')]
        result=self.controlled(mock_adapter=True,resultText=json.dumps(rows));self.assertEqual(1,result.returncode)
        evidence=self.failure();self.assertEqual('test_failure',evidence['kind']);self.assertEqual('attest',evidence['phase'])
        self.assertIn('guard-results.json',evidence['artifacts'])
        result=self.controlled(mock_adapter=True);self.assertEqual(0,result.returncode,result.stderr)
        self.assertFalse((self.work/'guard-failure.json').exists());self.released()

    def test_timeout_records_current_failure_and_releases_workspace_for_recovery(self):
        result=self.controlled(mock_adapter=True,child_environment={'CS_WALL_TIMEOUT':'1'},simulateDelayMs=4000)
        self.assertEqual(1,result.returncode);evidence=self.failure()
        self.assertEqual('timeout',evidence['kind']);self.assertEqual('simulate',evidence['phase'])
        result=self.controlled(mock_adapter=True);self.assertEqual(0,result.returncode,result.stderr)
        self.assertFalse((self.work/'guard-failure.json').exists());self.released()

    def test_nonzero_simulator_failure_wins_over_publication_marker_and_passing_json(self):
        result=self.controlled(mock_adapter=True,simulateExit=7,reportFailureStream='stdout')
        self.assertEqual(1,result.returncode);evidence=self.failure()
        self.assertEqual('simulator_failure',evidence['kind']);self.assertEqual(7,evidence['simulatorExitCode'])
        self.assertIn('Simulator failed (exit 7)',evidence['error'])

    def test_failed_evidence_publication_does_not_mask_original_configuration_error(self):
        result=self.controlled(mock_adapter=True,blockFailureArtifact=True,resultText='{}')
        self.assertEqual(1,result.returncode)
        self.assertIn('Legacy failure evidence could not be written',result.stderr)
        self.assertIn('InvalidDataException: No results',result.stderr)
        self.assertTrue((self.work/'guard-failure.json').is_dir());self.released()
        self.assertEqual([],list(self.work.glob('*.tmp')))

    def test_ownership_rejection_does_not_publish_or_replace_failure_evidence(self):
        failure=self.work/'guard-failure.json';failure.write_bytes(b'protected prior evidence')
        source=self.folder/'protected.vcd';source.write_bytes(b'protected source')
        result=self.run_cli(source,'--waves',source)
        self.assertEqual(1,result.returncode);self.assertEqual(b'protected prior evidence',failure.read_bytes())
        self.assertEqual(b'protected source',source.read_bytes());self.released()

    def test_invalid_wall_timeout_is_configuration_failure_before_compilation(self):
        result=self.controlled(mock_adapter=True,child_environment={'CS_WALL_TIMEOUT':'invalid'})
        self.assertEqual(1,result.returncode);evidence=self.failure()
        self.assertEqual('configuration_error',evidence['kind']);self.assertEqual('prepare',evidence['phase'])
        self.assertFalse((self.folder/'fixture-compile-argv.json').exists())

    def test_direct_legacy_run_rejects_source_mutation_despite_passing_results(self):
        for phase in ['compile','simulate']:
            result=self.controlled(mock_adapter=True,mutatePhase=phase)
            self.assertEqual(1,result.returncode);self.assertIn('inputs changed',result.stderr)
            evidence=self.failure();self.assertEqual('configuration_error',evidence['kind'])
            captured=json.loads((self.work/'guard-input-manifest.json').read_text(encoding='utf-8'))
            self.assertEqual('captured',captured['status']);self.assertEqual(evidence['runId'],captured['runId'])

    def test_bootstrap_mutation_is_rejected_including_windows_staged_copy(self):
        result=self.controlled(mock_adapter=True,mutateBootstrap=True)
        self.assertEqual(1,result.returncode);self.assertIn('inputs changed',result.stderr)
        self.assertEqual('configuration_error',self.failure()['kind'])

    def test_direct_input_evidence_keeps_invocation_identity_and_verified_bytes(self):
        import hashlib
        result=self.controlled(mock_adapter=True);self.assertEqual(0,result.returncode,result.stderr)
        inputs=json.loads((self.work/'guard-input-manifest.json').read_text(encoding='utf-8'))
        invocation=json.loads((self.work/'guard-invocation.json').read_text(encoding='utf-8'))
        self.assertEqual('verified',inputs['status']);self.assertEqual(inputs['runId'],invocation['runId'])
        for item in inputs['inputs']+inputs['generated']:
            self.assertEqual(hashlib.sha256(Path(item['Path']).read_bytes()).hexdigest().upper(),item['Sha256'])
        self.assertEqual(['staged-bootstrap'] if os.name=='nt' else [],[item['Role'] for item in inputs['generated']]);self.released()

    def test_parent_log_publication_failure_is_infrastructure_not_simulator_failure(self):
        result=self.controlled(mock_adapter=True,blockRunLog=True)
        self.assertEqual(1,result.returncode);evidence=self.failure()
        self.assertEqual('infrastructure_failure',evidence['kind']);self.assertEqual('record',evidence['phase'])
        self.assertEqual(0,evidence['simulatorExitCode']);self.assertTrue((self.work/'guard-results.json').is_file())

    def test_compiler_deadline_rejects_invalid_configuration_before_launch(self):
        for value in ['invalid','0','3601']:
            result=self.controlled(mock_adapter=True,child_environment={'CS_COMPILE_TIMEOUT':value})
            self.assertEqual(1,result.returncode);evidence=self.failure()
            self.assertEqual('prepare',evidence['phase']);self.assertEqual('configuration_error',evidence['kind'])
            self.assertFalse((self.folder/'fixture-compile-argv.json').exists())

    def test_compiler_diagnostics_and_exit_are_retained_separately_from_simulation(self):
        result=self.controlled(mock_adapter=True,compileExit=7)
        self.assertEqual(1,result.returncode);self.assertEqual('build_failure',self.failure()['kind'])
        evidence=json.loads((self.work/'guard-compile-process.json').read_text(encoding='utf-8'))
        self.assertEqual(7,evidence['exitCode']);self.assertTrue(evidence['diagnosticsRetained'])
        self.assertFalse(evidence['timedOut']);self.assertEqual('failed',evidence['status'])
        self.assertIn('controlled compiler diagnostic',(self.work/'guard-compile.stderr.log').read_text(encoding='utf-8'))
        self.assertFalse((self.work/'guard-results.json').exists())

    def test_compiler_start_failure_has_current_metadata_and_no_stale_diagnostics(self):
        for suffix in ['-compile.stdout.log','-compile.stderr.log','-compile-process.json']:
            (self.work/('guard'+suffix)).write_text('old diagnostics',encoding='utf-8')
        result=self.controlled(mock_adapter=True,child_environment={'CS_IVERILOG':str(self.folder/'missing compiler')})
        self.assertEqual(1,result.returncode);self.assertEqual('build_failure',self.failure()['kind'])
        evidence=json.loads((self.work/'guard-compile-process.json').read_text(encoding='utf-8'))
        self.assertIsNone(evidence['exitCode']);self.assertFalse(evidence['diagnosticsRetained'])
        self.assertFalse((self.work/'guard-compile.stdout.log').exists());self.assertFalse((self.work/'guard-compile.stderr.log').exists())

    def test_compiler_timeout_releases_lease_and_next_run_replaces_failure(self):
        result=self.controlled(mock_adapter=True,child_environment={'CS_COMPILE_TIMEOUT':'1'},compileDelayMs=4000)
        self.assertEqual(1,result.returncode);evidence=self.failure()
        self.assertEqual('timeout',evidence['kind']);self.assertEqual('compile',evidence['phase'])
        process=json.loads((self.work/'guard-compile-process.json').read_text(encoding='utf-8'))
        self.assertTrue(process['timedOut']);self.assertIsNone(process['exitCode'])
        result=self.controlled(mock_adapter=True);self.assertEqual(0,result.returncode,result.stderr)
        self.assertFalse((self.work/'guard-failure.json').exists())
        process=json.loads((self.work/'guard-compile-process.json').read_text(encoding='utf-8'))
        self.assertEqual('completed',process['status']);self.assertEqual(0,process['exitCode'])

    def test_generated_wave_helper_mutation_cannot_hide_behind_passing_result_rows(self):
        result=self.controlled(mock_adapter=True,cli_options=['--waves',str(self.folder/'waves.vcd')],mutateWaveHelper=True)
        self.assertEqual(1,result.returncode);self.assertIn('inputs changed',result.stderr)
        self.assertEqual('configuration_error',self.failure()['kind'])

    def test_compiler_log_io_failure_does_not_claim_complete_diagnostic_retention(self):
        result=self.controlled(mock_adapter=True,blockCompileLog=True)
        self.assertEqual(1,result.returncode);self.failure()
        evidence=json.loads((self.work/'guard-compile-process.json').read_text(encoding='utf-8'))
        self.assertEqual(0,evidence['exitCode']);self.assertFalse(evidence['diagnosticsRetained'])
        self.assertEqual('failed',evidence['status']);self.assertFalse((self.work/'guard-results.json').exists())

    def test_successful_compiler_log_and_metadata_publication_failures_are_infrastructure(self):
        for behavior in ['blockCompileLog','blockCompileStdoutLog','blockCompileProcess']:
            # Each obstruction is scoped to a fresh child-owned directory.
            for suffix in ['-compile.stderr.log','-compile.stdout.log','-compile-process.json']:
                target=self.work/('guard'+suffix)
                if target.is_dir():target.rmdir()
            result=self.controlled(mock_adapter=True,**{behavior:True})
            self.assertEqual(1,result.returncode);failure=self.failure()
            self.assertEqual('infrastructure_failure',failure['kind']);self.assertEqual('compile-record',failure['phase'])
            self.assertFalse((self.work/'guard-results.json').exists())

    def test_nonzero_compiler_failure_wins_over_secondary_publication_obstructions(self):
        for behavior in ['blockCompileLog','blockCompileProcess']:
            for suffix in ['-compile.stderr.log','-compile-process.json']:
                target=self.work/('guard'+suffix)
                if target.is_dir():target.rmdir()
            result=self.controlled(mock_adapter=True,compileExit=7,**{behavior:True})
            self.assertEqual(1,result.returncode);failure=self.failure()
            self.assertEqual('build_failure',failure['kind']);self.assertEqual('compile',failure['phase'])
            self.assertIn('compiler failed (exit 7)',failure['error'])

    def test_compiler_stderr_budget_retains_bounded_diagnostics_and_current_failure(self):
        result=self.controlled(mock_adapter=True,compileStderrBytes=17*1024*1024)
        self.assertEqual(1,result.returncode);failure=self.failure();self.assertEqual('build_failure',failure['kind'])
        process=json.loads((self.work/'guard-compile-process.json').read_text(encoding='utf-8'))
        self.assertTrue(process['stderrLimited']);self.assertFalse(process['stdoutLimited']);self.assertTrue(process['captureSettled'])
        self.assertTrue(process['diagnosticsRetained']);self.assertFalse(process['diagnosticsComplete'])
        self.assertEqual(16*1024*1024,(self.work/'guard-compile.stderr.log').stat().st_size)
        self.assertLess(len(failure['error']),4300)

    def test_compile_timeout_retains_partial_diagnostics_without_claiming_complete_output(self):
        result=self.controlled(mock_adapter=True,child_environment={'CS_COMPILE_TIMEOUT':'1'},compileDelayMs=4000)
        self.assertEqual(1,result.returncode);self.assertEqual('timeout',self.failure()['kind'])
        evidence=json.loads((self.work/'guard-compile-process.json').read_text(encoding='utf-8'))
        self.assertTrue(evidence['diagnosticsRetained']);self.assertFalse(evidence['diagnosticsComplete']);self.assertTrue(evidence['captureSettled'])
        self.assertIn('compile',(self.work/'guard-compile.stdout.log').read_text(encoding='utf-8'))
        result=self.controlled(mock_adapter=True);self.assertEqual(0,result.returncode,result.stderr)
        self.assertFalse((self.work/'guard-failure.json').exists())

    def test_simulator_timeout_keeps_separate_partial_streams_and_recoverable_workspace(self):
        result=self.controlled(mock_adapter=True,child_environment={'CS_WALL_TIMEOUT':'1'},simulateDelayMs=4000,stderrBytes=32)
        self.assertEqual(1,result.returncode);self.assertEqual('timeout',self.failure()['kind'])
        evidence=json.loads((self.work/'guard-simulate-capture.json').read_text(encoding='utf-8'))
        self.assertEqual('interrupted',evidence['status']);self.assertFalse(evidence['diagnosticsComplete']);self.assertTrue(evidence['captureSettled'])
        self.assertIn('simulate',(self.work/'guard-simulate.stdout.log').read_text(encoding='utf-8'))
        self.assertEqual('E'*32,(self.work/'guard-simulate.stderr.log').read_text(encoding='utf-8'))
        result=self.controlled(mock_adapter=True);self.assertEqual(0,result.returncode,result.stderr)
        self.assertFalse((self.work/'guard-failure.json').exists())

    def test_simulator_stdout_budget_cannot_accept_passing_result_json(self):
        result=self.controlled(mock_adapter=True,earlyResults=True,stdoutBytes=17*1024*1024)
        self.assertEqual(1,result.returncode);failure=self.failure()
        self.assertEqual('simulator_failure',failure['kind']);self.assertEqual('simulate',failure['phase'])
        evidence=json.loads((self.work/'guard-simulate-capture.json').read_text(encoding='utf-8'))
        self.assertEqual('passed',json.loads((self.work/'guard-results.json').read_text(encoding='utf-8'))[0]['Status'])
        self.assertTrue(evidence['stdoutLimited']);self.assertFalse(evidence['stderrLimited']);self.assertTrue(evidence['captureSettled'])
        self.assertEqual(16*1024*1024,(self.work/'guard-simulate.stdout.log').stat().st_size)

    def test_simulator_stderr_budget_is_independent_and_cannot_accept_passing_rows(self):
        result=self.controlled(mock_adapter=True,earlyResults=True,stderrBytes=17*1024*1024)
        self.assertEqual(1,result.returncode);self.assertEqual('simulator_failure',self.failure()['kind'])
        evidence=json.loads((self.work/'guard-simulate-capture.json').read_text(encoding='utf-8'))
        self.assertEqual('passed',json.loads((self.work/'guard-results.json').read_text(encoding='utf-8'))[0]['Status'])
        self.assertTrue(evidence['stderrLimited']);self.assertFalse(evidence['stdoutLimited'])
        self.assertEqual(16*1024*1024,(self.work/'guard-simulate.stderr.log').stat().st_size)

    def test_nonzero_simulator_failure_wins_over_secondary_publication_obstructions(self):
        for behavior,suffix in [('blockRunLog','-run.log'),('blockSimulateCapture','-simulate-capture.json')]:
            result=self.controlled(mock_adapter=True,simulateExit=7,**{behavior:True})
            self.assertEqual(1,result.returncode);failure=self.failure()
            self.assertEqual('simulator_failure',failure['kind']);self.assertEqual('simulate',failure['phase'])
            self.assertIn('Simulator failed (exit 7)',failure['error']);self.assertEqual('System.IO.IOException',failure['errorType'])
            self.assertEqual(7,failure['simulatorExitCode']);self.assertIn('Simulator diagnostics could not be written',result.stderr)
            (self.work/('guard'+suffix)).rmdir()
