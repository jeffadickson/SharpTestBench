"""Invalid JSON keys are configuration errors, including with reflection disabled."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT=Path(__file__).resolve().parents[2]

class Tests(unittest.TestCase):
    def parse(self,text):
        result=subprocess.run(['dotnet',str(ROOT/'tests/RunnerValidation/bin/Release/net10.0/RunnerValidation.dll'),'json'],input=text,text=True,capture_output=True,check=True,timeout=30)
        return json.loads(result.stdout)

    def test_unpaired_key_surrogates_are_invalid_data(self):
        for text in ('{"\\ud800":1}','{"\\udc00":1}','{"nested":[{"\\ud800":null}]}'):
            with self.subTest(text=text):
                result=self.parse(text)
                self.assertFalse(result['valid'])
                self.assertEqual('InvalidDataException',result['kind'])
                self.assertIn('property name',result['error'])

    def test_valid_non_bmp_key_and_value_survive(self):
        self.assertTrue(self.parse('{"\\ud83d\\ude00":"\\ud83d\\ude01"}')['valid'])

    def test_production_cli_classifies_malformed_key_as_configuration_error(self):
        with tempfile.TemporaryDirectory(prefix='stb-json-key-') as temporary:
            manifest=Path(temporary)/'manifest.json'
            manifest.write_text('{"\\ud800":1}',encoding='utf-8')
            cli=Path(os.environ.get('STB_RUNNER_CLI_DLL',str(ROOT/'tools/SharpTestBench.Runner/bin/Release/net10.0/SharpTestBench.Runner.dll')))
            result=subprocess.run(['dotnet',str(cli),'run',str(manifest)],text=True,capture_output=True,timeout=30)
            self.assertEqual(1,result.returncode)
            self.assertIn('configuration_error',result.stderr)
            self.assertNotIn('infrastructure_failure',result.stderr)
            self.assertEqual(['manifest.json'],[x.name for x in Path(temporary).iterdir()])