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

    def controlled(self, cli_options=(), **behavior):
        fixture=ROOT/'tests/RunnerProcessFixture/bin/Release/net10.0'/('RunnerProcessFixture.exe' if os.name=='nt' else 'RunnerProcessFixture')
        config=self.folder/'process-fixture.json';config.write_text(json.dumps({'resultText':'[{"Name":"Contract","Status":"passed","Seed":1}]',**behavior}),encoding='utf-8')
        assembly=self.folder/'controlled.dll';assembly.write_bytes(b'controlled assembly placeholder')
        assembly.with_suffix('.runtimeconfig.json').write_text('{}',encoding='utf-8')
        source=self.folder/'input.sv';source.write_text('module top; endmodule',encoding='utf-8')
        env=dict(os.environ,CS_IVERILOG=str(fixture),CS_VVP=str(fixture),STB_PROCESS_FIXTURE_CONFIG=str(config))
        env.pop("CS_IVL_ROOT",None)
        return subprocess.run(['dotnet',str(self.cli),'run','--no-build','--work-dir',str(self.work),'--label','guard',
                               '--assembly',str(assembly),'--source',str(source),*cli_options],cwd=ROOT,env=env,
                               text=True,encoding='utf-8',capture_output=True,check=False,timeout=30)

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
