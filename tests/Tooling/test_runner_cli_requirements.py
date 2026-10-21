"""Production manifest path and ownership requirements, controlled children only."""
import hashlib
import json
import os
from pathlib import Path
import unittest
import test_runner_cli_completion as completion

class Tests(unittest.TestCase):
    setUp=completion.Tests.setUp
    manifest=completion.Tests.manifest
    prepare=completion.Tests.prepare
    run_cli=completion.Tests.run_cli
    artifact=completion.Tests.artifact
    released=completion.Tests.released

    def test_relative_gpi_implementation_uses_manifest_directory_in_child_environment(self):
        for name in ['gpi-impl.bin','implementation with spaces.bin']:
            path,data=self.prepare();library=self.folder/name;library.write_bytes(b'controlled GPI input')
            data['gpiImpl']=name+':custom_entry';path.write_text(json.dumps(data),encoding='utf-8')
            result=self.run_cli(path);self.assertEqual(0,result.returncode,result.stderr)
            self.assertEqual(str(library)+':custom_entry',self.artifact('fixture-environment.json')['GPI_IMPL'])
            data['gpiImpl']=str(library)+':custom_entry';path.write_text(json.dumps(data),encoding='utf-8')
            result=self.run_cli(path);self.assertEqual(0,result.returncode,result.stderr)
            self.assertEqual(str(library)+':custom_entry',self.artifact('fixture-environment.json')['GPI_IMPL']);self.released()

    def test_summary_fingerprints_match_verified_role_records_and_exact_bytes(self):
        path,_=self.prepare();result=self.run_cli(path);self.assertEqual(0,result.returncode,result.stderr)
        evidence=self.artifact('run-manifest.json');inputs=evidence['Inputs']
        assembly=next(item for item in inputs if item['Role']=='assembly')
        source=next(item for item in inputs if item['Role']=='source')
        self.assertEqual(assembly['Sha256'],evidence['AssemblySha256'])
        self.assertEqual([{'Path':source['Path'],'Sha256':source['Sha256']}],evidence['Sources'])
        for item in [source,assembly]:self.assertEqual(hashlib.sha256(Path(item['Path']).read_bytes()).hexdigest().upper(),item['Sha256'])
        self.released()

    def test_manifest_inside_output_is_preserved_by_output_containment_rejection(self):
        path,data=self.prepare();output=self.folder/'output';output.mkdir()
        data['outputDirectory']=str(output);protected=output/'input-manifest.json'
        protected.write_text(json.dumps(data),encoding='utf-8');before=protected.read_bytes()
        result=self.run_cli(protected);self.assertEqual(1,result.returncode)
        self.assertIn('below the manifest directory',result.stderr);self.assertEqual(before,protected.read_bytes())
        self.assertEqual([protected],list(output.iterdir()));self.released()

    def test_old_supplemental_results_and_trace_evidence_are_retired_on_failed_attempt(self):
        path,_=self.prepare(compileExit=7);output=self.folder/'output';output.mkdir()
        names=['icarus-results.coverage.json','icarus-results.discovery.json','icarus-trace.txt.scheduled','icarus-trace.txt.force','icarus-trace.txt.capabilities.json']
        for name in names:(output/name).write_bytes(b'previous successful evidence')
        result=self.run_cli(path);self.assertEqual(1,result.returncode)
        self.assertTrue(all(not (output/name).exists() for name in names));self.released()

    @unittest.skipIf(os.name=='nt','Unix symbolic link semantics')
    def test_linked_runtime_config_is_protected_before_artifact_cleanup(self):
        path,_=self.prepare();output=self.folder/'output';output.mkdir()
        target=output/'top.vvp';target.write_bytes(b'protected runtime config')
        config=self.folder/'fixture.runtimeconfig.json';config.unlink();config.symlink_to(target)
        result=self.run_cli(path);self.assertEqual(1,result.returncode);self.assertIn('collides',result.stderr)
        self.assertEqual(b'protected runtime config',target.read_bytes());self.assertEqual([target],list(output.iterdir()));self.released()

    @unittest.skipIf(os.name=='nt','Unix symbolic link semantics')
    def test_linked_source_directory_cannot_hide_generated_artifact_alias(self):
        path,data=self.prepare();output=self.folder/'output';output.mkdir()
        target=output/'top.vvp';target.write_bytes(b'protected source')
        alias=self.folder/'source-alias';alias.symlink_to(output,target_is_directory=True)
        data['sources']=['source-alias/top.vvp'];path.write_text(json.dumps(data),encoding='utf-8')
        result=self.run_cli(path);self.assertEqual(1,result.returncode);self.assertIn('collides',result.stderr)
        self.assertEqual(b'protected source',target.read_bytes());self.assertEqual([target],list(output.iterdir()));self.released()

    @unittest.skipIf(os.name=='nt','Unix symbolic link semantics')
    def test_manifest_link_target_cannot_alias_a_generated_artifact(self):
        path,data=self.prepare();output=self.folder/'output';output.mkdir()
        protected=output/'input-manifest.json';protected.write_text(json.dumps(data),encoding='utf-8')
        path.unlink();path.symlink_to(protected);before=protected.read_bytes()
        result=self.run_cli(path);self.assertEqual(1,result.returncode);self.assertIn('collides',result.stderr)
        self.assertEqual(before,protected.read_bytes());self.assertEqual([protected],list(output.iterdir()));self.released()
