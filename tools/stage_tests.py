#!/usr/bin/env python3
"""Fresh simulator processes verify stage order, selection and failure policies."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
SDK = Path(os.environ['CS_DOTNET_ROOT']) if 'CS_DOTNET_ROOT' in os.environ else Path(shutil.which('dotnet')).resolve().parent


def main():
    subprocess.run([str(SDK / 'dotnet'), 'build', str(ROOT / 'tests/StageIntegration'),
                    '-c', 'Release', '--disable-build-servers', '-m:1'], check=True)
    assembly = ROOT / 'tests/StageIntegration/bin/Release/net10.0/StageIntegration.dll'
    expected = ['Z_First[0]', 'Z_First[1]', 'A_Fail', 'B_After', 'A_Last']
    scenarios = [
        ('order', [], False, expected, ['passed'] * 5),
        ('continue', ['--stage-failure-policy', 'continue'], True, expected,
         ['passed', 'passed', 'failed', 'passed', 'passed']),
        ('stop', ['--stage-failure-policy', 'stop-after-stage'], True, expected,
         ['passed', 'passed', 'failed', 'passed', 'skipped']),
        ('selection', ['--stage', '-2', '--stage', '2'], True,
         ['Z_First[0]', 'Z_First[1]', 'A_Last'], ['passed'] * 3),
        ('filter', ['--stage', '-2', '--filter', '[1]'], False,
         ['Z_First[1]'], ['passed']),
        ('exact', ['--exact', 'StageTests.Z_First[1]'], False, ['Z_First[1]'], ['passed']),
        ('multiple', ['--exact', 'Z_First[0]', '--exact', 'A_Last'], False, ['Z_First[0]', 'A_Last'], ['passed'] * 2),
        ('empty', ['--stage', '99'], False, ['runtime'], ['failed']),
        ('list', ['--list'], True, [], []),
    ]
    for name, args, fail, names, statuses in scenarios:
        env = os.environ.copy()
        for key in ['CS_TEST_STAGES', 'CS_TEST_FILTER', 'CS_STAGE_FAILURE_POLICY', 'CS_LIST_TESTS', 'CS_TEST_NAMES']:
            env.pop(key, None)
        env.update(CS_STAGE_INJECT_FAILURE=str(int(fail)), CS_SEED='100')
        label = 'stage-' + name
        command = [sys.executable, str(ROOT / 'tools/run.py'), '--no-build',
                   '--assembly', str(assembly), '--label', label, *args]
        result = subprocess.run(command, env=env, text=True, capture_output=True, timeout=120)
        (ROOT / f'build/{label}-driver.log').write_text(result.stdout + result.stderr)
        rows = json.loads((ROOT / f'build/{label}-results.json').read_text())
        assert [r['Name'] for r in rows] == names, (name, rows)
        assert [r['Status'] for r in rows] == statuses, (name, rows)
        assert result.returncode == (1 if 'failed' in statuses else 0), (name, result.returncode)
        for row in rows:
            if row['Name'] in expected:
                index = expected.index(row['Name'])
                assert row['Seed'] == 100 + index, (name, row)
                assert row['QualifiedName'] == 'StageTests.' + row['Name']
                assert row['Stage'] == [-2, -2, 0, 0, 2][index]
        if name == 'list':
            discovery = json.loads((ROOT / f'build/{label}-results.discovery.json').read_text())
            assert discovery['SchemaVersion'] == 1
            assert [r['Name'] for r in discovery['Tests']] == expected
            assert [r['Seed'] for r in discovery['Tests']] == list(range(100, 105))
        if name == 'stop':
            assert rows[-1]['Error'] == 'Earlier stage 0 failed'
        print('STAGE_PASS', name)


if __name__ == '__main__':
    main()
