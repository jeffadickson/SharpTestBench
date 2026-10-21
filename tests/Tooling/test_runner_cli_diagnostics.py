"""Production reflection-disabled Runner failure diagnostics stay bounded."""
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

    def test_long_executable_failure_is_bounded_without_changing_kind_or_schema(self):
        path,data=self.prepare();data['iverilog']='missing-compiler-'+('x'*24000)
        path.write_text(json.dumps(data),encoding='utf-8')
        result=self.run_cli(path);self.assertEqual(1,result.returncode)
        failure=self.artifact('failure-manifest.json')
        self.assertEqual('build_failure',failure['Kind']);self.assertLessEqual(len(failure['Error']),16384)
        self.assertTrue(failure['Error'].endswith('[diagnostic truncated]'))
        self.assertLess(len(result.stderr),18000)
        process=self.artifact('compile-process.json')
        self.assertFalse(process['Started']);self.assertLessEqual(len(process['Failure']),16384)
        self.released()
