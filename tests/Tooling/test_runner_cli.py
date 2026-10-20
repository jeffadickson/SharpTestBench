"""Production Runner entry-point boundaries; these tests do not simulate HDL."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]


class Tests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='stb runner cli ')
        self.addCleanup(self.temp.cleanup)
        self.folder = Path(self.temp.name)
        self.cli = Path(os.environ.get('STB_RUNNER_CLI_DLL', str(
            ROOT / 'tools/SharpTestBench.Runner/bin/Release/net10.0/SharpTestBench.Runner.dll')))

    def run_cli(self, *args):
        return subprocess.run(['dotnet', str(self.cli), *map(str, args)],
                              cwd=self.folder, text=True, encoding='utf-8',
                              capture_output=True, timeout=60, check=False)

    def manifest(self):
        sdk = os.environ.get('CS_DOTNET_ROOT') or os.environ.get('DOTNET_ROOT')
        if not sdk:
            sdk = str(Path(shutil.which('dotnet')).resolve().parent)
        data = dict(schemaVersion=1, simulator='icarus', top='top',
                    sources=['input.sv'], assembly='fixture.dll', nativeHost='host.bin',
                    icarusBootstrap='bootstrap.bin', gpiLibrary='gpi.bin',
                    gpiImpl='gpi-impl.bin:entry', dotnetRoot=sdk,
                    outputDirectory='output', expectedTests=1,
                    expectedNames=['Entry.Test'], seed=18446744073709551615)
        path = self.folder / 'manifest.json'
        path.write_text(json.dumps(data), encoding='utf-8')
        return path, data

    # Iterations 46–50: real production CLI subprocesses, not the RPC harness.
    def test_help_exits_successfully_without_creating_workspace(self):
        result = self.run_cli('--help')
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn('standalone runner', result.stdout)
        self.assertEqual([], list(self.folder.iterdir()))

    def test_usage_error_exits_two_without_creating_workspace(self):
        result = self.run_cli('unexpected')
        self.assertEqual(2, result.returncode)
        self.assertIn('Usage:', result.stderr)
        self.assertEqual([], list(self.folder.iterdir()))

    def test_malformed_manifest_fails_before_workspace_creation(self):
        path = self.folder / 'manifest.json'
        path.write_text('{', encoding='utf-8')
        result = self.run_cli('run', path)
        self.assertEqual(1, result.returncode)
        self.assertIn('configuration_error', result.stderr)
        self.assertEqual([path], list(self.folder.iterdir()))

    def test_missing_source_records_configuration_failure_and_releases_lock(self):
        path, _ = self.manifest()
        result = self.run_cli('run', path)
        self.assertEqual(1, result.returncode, result.stderr)
        output = self.folder / 'output'
        failure = json.loads((output / 'failure-manifest.json').read_text(encoding='utf-8'))
        self.assertEqual('configuration_error', failure['Kind'])
        self.assertIn('input.sv', failure['Error'])
        self.assertEqual([], failure['LogFiles'])
        self.assertFalse((output / '.sharptestbench-run.lock').exists())
        self.assertFalse((output / 'run-manifest.json').exists())

    def test_compile_failure_retains_phase_evidence_with_production_json_switch(self):
        runtime = json.loads(self.cli.with_suffix('.runtimeconfig.json').read_text(encoding='utf-8'))
        self.assertIs(False, runtime['runtimeOptions']['configProperties'][
            'System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault'])
        path, data = self.manifest()
        # Only file existence/hashing is needed before compile. The Python
        # interpreter rejects Icarus arguments, so no dummy DLL can be loaded.
        for name in ['input.sv', 'fixture.dll', 'host.bin', 'bootstrap.bin', 'gpi.bin', 'gpi-impl.bin']:
            (self.folder / name).write_bytes(b'controlled compile-boundary input')
        (self.folder / 'fixture.runtimeconfig.json').write_text('{}', encoding='utf-8')
        data['iverilog'] = sys.executable
        path.write_text(json.dumps(data), encoding='utf-8')
        result = self.run_cli('run', path)
        self.assertEqual(1, result.returncode, result.stderr)
        output = self.folder / 'output'
        failure = json.loads((output / 'failure-manifest.json').read_text(encoding='utf-8'))
        process = json.loads((output / 'compile-process.json').read_text(encoding='utf-8'))
        inputs = json.loads((output / 'input-manifest.json').read_text(encoding='utf-8'))
        self.assertEqual('build_failure', failure['Kind'])
        self.assertIn('compile-process.json', failure['LogFiles'])
        self.assertEqual('compile', process['Phase'])
        self.assertTrue(process['Started'])
        self.assertFalse(process['TimedOut'])
        self.assertNotEqual(0, process['ExitCode'])
        self.assertEqual(failure['RunId'], inputs['RunId'])
        self.assertIn('hostfxr', {item['Role'] for item in inputs['Inputs']})
        self.assertFalse((output / 'run-manifest.json').exists())
        self.assertFalse((output / '.sharptestbench-run.lock').exists())
        self.assertEqual([], list(output.glob('.evidence-*.tmp')))
