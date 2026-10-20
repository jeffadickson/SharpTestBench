"""Production CLI must reject artifact aliases before deleting any input bytes."""
import json
import unittest
import test_runner_cli


# Independent contract inventory: do not import the implementation's artifact list.
ARTIFACTS = (
    'run-manifest.json', 'failure-manifest.json', 'input-manifest.json',
    'icarus-results.json', 'icarus-results.xml', 'icarus-trace.txt',
    'icarus-trace.txt.delta', 'top.vvp', 'DutBindings.cs',
    'compile.stdout.log', 'compile.stderr.log', 'simulate.stdout.log',
    'simulate.stderr.log', 'compile-process.json', 'simulate-process.json',
)


class Tests(unittest.TestCase):
    setUp = test_runner_cli.Tests.setUp
    run_cli = test_runner_cli.Tests.run_cli
    manifest = test_runner_cli.Tests.manifest

    def collision(self, role, artifact):
        path, data = self.manifest()
        output = self.folder / 'output'
        output.mkdir()
        data['outputDirectory'] = str(output)
        target = output / artifact
        if role == 'sources':
            data[role] = ['output/' + artifact]
        elif role == 'gpiImpl':
            data[role] = 'output/' + artifact + ':entry'
        elif role == 'includeDirectories':
            data[role] = ['output']
        else:
            data[role] = 'output/' + artifact
        target.write_bytes(b'input must survive runner cleanup')
        path.write_text(json.dumps(data), encoding='utf-8')
        sentinel = output / 'unrelated.txt'
        sentinel.write_bytes(b'unrelated owner data')
        before = {item.name: item.read_bytes() for item in output.iterdir()}
        result = self.run_cli('run', path)
        self.assertEqual(1, result.returncode, result.stderr)
        self.assertIn('configuration_error', result.stderr)
        self.assertIn('collides', result.stderr)
        self.assertEqual(before, {item.name: item.read_bytes() for item in output.iterdir()})

    def test_include_ancestor_of_output_is_rejected_without_deleting_inputs(self):
        path, data = self.manifest()
        data['includeDirectories'] = ['.']
        path.write_text(json.dumps(data), encoding='utf-8')
        output = self.folder / 'output'
        output.mkdir()
        sentinel = output / 'top.vvp'
        sentinel.write_bytes(b'protected include')
        result = self.run_cli('run', path)
        self.assertEqual(1, result.returncode, result.stderr)
        self.assertIn('include directory collides', result.stderr)
        self.assertEqual(b'protected include', sentinel.read_bytes())
        self.assertEqual([sentinel], list(output.iterdir()))

    def test_include_sibling_with_output_prefix_is_not_a_collision(self):
        path, data = self.manifest()
        include = self.folder / 'out'
        include.mkdir()
        sentinel = include / 'top.vvp'
        sentinel.write_bytes(b'valid sibling include')
        data['includeDirectories'] = ['out']
        path.write_text(json.dumps(data), encoding='utf-8')
        result = self.run_cli('run', path)
        self.assertEqual(1, result.returncode, result.stderr)
        self.assertNotIn('collides', result.stderr)
        self.assertIn('Required input not found:', result.stderr)
        self.assertEqual(b'valid sibling include', sentinel.read_bytes())
        failure = json.loads((self.folder / 'output/failure-manifest.json').read_text(encoding='utf-8'))
        self.assertEqual('configuration_error', failure['Kind'])
        self.assertFalse((self.folder / 'output/.sharptestbench-run.lock').exists())


def add_collision(role, artifact):
    def test(self):
        self.collision(role, artifact)
    test.__name__ = 'test_' + role + '_preserved_at_' + artifact.replace('.', '_').replace('-', '_')
    setattr(Tests, test.__name__, test)


for artifact in ARTIFACTS:
    add_collision('sources', artifact)
    add_collision('gpiImpl', artifact)
    add_collision('includeDirectories', artifact)

for role in ('sources', 'gpiImpl', 'includeDirectories'):
    add_collision(role, '.sharptestbench-run.lock')
