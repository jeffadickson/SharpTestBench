#!/usr/bin/env python3
"""Run isolated legacy/shadow Icarus fixtures with retained differential evidence."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
from strict_json import load as load_json

from dual_runner_manifest import FIXTURES, installed_adapter, make_manifest
from dual_runner_parity import compare, expected_identities, write_report

SELECTION_VARIABLES = ('CS_TEST_FILTER', 'CS_TEST_NAMES', 'CS_TEST_STAGES', 'CS_LIST_TESTS',
                       'CS_STAGE_FAILURE_POLICY', 'LFSR_PLAN', 'LFSR_REPORT', 'LFSR_FAULT', 'AXIS_MODE', 'AXIS_PLAN', 'AXIS_REPORT', 'AXIS_FAULT', 'AXIS_SIDEBANDS', 'DSP_FAULT', 'CPU_FAULT', 'DSP_PLAN', 'CPU_PLAN',
                       'DSP_REPORT', 'CPU_REPORT', 'CS_INJECT_EARLY', 'CS_WALL_TIMEOUT')


def clean_environment(environment):
    return {key: value for key, value in environment.items() if key not in SELECTION_VARIABLES}


def metrics_contract(fixture, data):
    """Compare functional traces/counters, excluding allocation and elapsed time."""
    keys = {'dsp': ('trace', 'cycles', 'accepted', 'consumed', 'stalls'),
            'cpu': ('trace', 'cycles', 'commits', 'memoryStalls', 'pauses', 'memory')}
    if fixture not in keys:
        raise ValueError('No metrics contract for fixture: ' + fixture)
    if not isinstance(data, dict) or any(key not in data for key in keys[fixture]):
        raise ValueError('Missing workload metric fields')
    result = {key: data[key] for key in keys[fixture]}
    trace = result['trace']
    if not isinstance(trace, list) or not trace or any(not isinstance(row, list) or not row or
            any(type(value) is not int for value in row) for row in trace):
        raise ValueError('Workload trace must contain nonempty integer rows')
    for key, value in result.items():
        if key not in ('trace', 'memory') and (type(value) is not int or value < 0):
            raise ValueError('Invalid workload counter: ' + key)
    if result['cycles'] <= 0:
        raise ValueError('Workload must execute cycles')
    if fixture == 'cpu' and (not isinstance(result['memory'], list) or not result['memory'] or
            any(type(value) is not int or not 0 <= value < 2**32 for value in result['memory'])):
        raise ValueError('CPU memory must contain unsigned 32-bit words')
    return result


def fault_detected(rows, exit_code, expected, marker):
    """A negative control passes only for an executed, named scoreboard failure."""
    expected = expected_identities(expected)
    if exit_code != 1 or not isinstance(rows, list) or len(rows) != len(expected):
        return False
    observed = set()
    for row in rows:
        if not isinstance(row, dict):
            return False
        identity = row.get('QualifiedName') or row.get('Name')
        if not isinstance(identity, str) or identity in observed:
            return False
        observed.add(identity)
        if row.get('Status') != 'failed' or not isinstance(row.get('Error'), str) or marker not in row['Error']:
            return False
        if type(row.get('Seed')) is not int or not 0 <= row['Seed'] < 2**64:
            return False
    return observed == expected


def clear_case(work):
    """Clear known old reports without deleting fixture inputs or unrelated files."""
    for directory in ('legacy', 'native'):
        folder = work / directory
        if folder.is_symlink(): raise ValueError('Parity output must not be a symbolic link')
        if (folder / '.sharptestbench-run.lock').exists(): raise RuntimeError('Child workspace is already in use')
    for name in ('parity.json', 'negative-control.json', 'case-failure.json', 'manifest.json',
                 'expected.json', 'plan.json', 'build.log', 'legacy.log', 'native.log'):
        (work / name).unlink(missing_ok=True)
    for directory in ('legacy', 'native'):
        folder = work / directory
        if folder.is_symlink(): raise ValueError('Parity output must not be a symbolic link')
        folder.mkdir(exist_ok=True)
        for name in ('metrics.json', 'legacy-results.json', 'legacy-results.xml', 'icarus-results.json',
                     'run-manifest.json', 'failure-manifest.json', 'input-manifest.json'):
            (folder / name).unlink(missing_ok=True)


def run_case(root, fixture, seed, negative_control=False):
    root = Path(root).resolve()
    if fixture not in FIXTURES or type(seed) is not int or not 0 <= seed < 2**64:
        raise ValueError('Invalid fixture or seed')
    if negative_control and fixture == 'counter':
        raise ValueError('Counter has no injected scoreboard fault')
    item = FIXTURES[fixture]
    suffix = '-negative' if negative_control else ''
    work = root / 'build' / f'dual-{fixture}-{seed}{suffix}'
    work.mkdir(parents=True, exist_ok=True)
    lock = work / '.parity.lock'
    try:
        descriptor = os.open(lock, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
    except FileExistsError:
        raise RuntimeError('Parity workspace is already in use: ' + str(work))
    try:
        with os.fdopen(descriptor, 'w') as output:
            output.write(str(os.getpid()))
        clear_case(work)
        env = clean_environment(os.environ)
        env['STB_NATIVE_VPI_TIME'] = '1'

        def execute(command, phase, environment=env):
            with (work / (phase + '.log')).open('w', encoding='utf-8') as log:
                result = subprocess.run(list(map(str, command)), cwd=root, env=environment,
                                        stdout=log, stderr=subprocess.STDOUT, timeout=300)
            return result.returncode

        if execute(['dotnet', 'build', root / item['project'], '-c', 'Release', '-v:q'], 'build'):
            raise RuntimeError('Fixture build failed; see build.log')
        from native_build import build_host, sdk_root
        build_host(root, sdk_root(), root / 'build')
        manifest = make_manifest(root, fixture, seed, installed_adapter(), work / 'native')
        write_report(work / 'manifest.json', manifest)
        write_report(work / 'expected.json', manifest['expectedNames'])
        if fixture != 'counter':
            if fixture == 'dsp':
                from dsp_workload import plan
                stimulus = plan(128, seed)
            else:
                from cpu_workload import plan
                stimulus = plan(16)
            write_report(work / 'plan.json', stimulus)
            prefix = fixture.upper()
            env[prefix + '_PLAN'] = str(work / 'plan.json')
            env[prefix + '_FAULT'] = '1' if negative_control else '0'
        legacy_env, native_env = dict(env), dict(env)
        if fixture != 'counter':
            legacy_env[fixture.upper() + '_REPORT'] = str(work / 'legacy/metrics.json')
            native_env[fixture.upper() + '_REPORT'] = str(work / 'native/metrics.json')
        legacy_code = execute([sys.executable, root / 'tools/run.py', '--sim', 'icarus', '--no-build',
                               '--assembly', manifest['assembly'], '--source', manifest['sources'][0],
                               '--top', manifest['top'], '--entry-type', manifest['entryType'],
                               '--work-dir', work / 'legacy', '--label', 'legacy', '--seed', seed], 'legacy', legacy_env)
        native_code = execute(['dotnet', root / 'tools/SharpTestBench.Runner/bin/Release/net10.0/SharpTestBench.Runner.dll',
                               'run', work / 'manifest.json'], 'native', native_env)
        legacy = load_json(work / 'legacy/legacy-results.json')
        native = load_json(work / 'native/icarus-results.json')
        if negative_control:
            marker = fixture.upper() + '_MISMATCH'
            report = {'schemaVersion': 1, 'negativeControl': True,
                      'legacyDetected': fault_detected(legacy, legacy_code, manifest['expectedNames'], marker),
                      'nativeDetected': fault_detected(native, native_code, manifest['expectedNames'], marker)}
            write_report(work / 'negative-control.json', report)
            if not report['legacyDetected'] or not report['nativeDetected']:
                raise ValueError('Injected scoreboard fault was not detected by both runners')
        else:
            report = compare(legacy, native, manifest['expectedNames'])
            report['exitCodes'] = {'legacy': legacy_code, 'native': native_code}
            report['matched'] = report['matched'] and legacy_code == native_code == 0
            if fixture != 'counter':
                left, right = (load_json(work / path / 'metrics.json')
                               for path in ('legacy', 'native'))
                report['traceMatched'] = metrics_contract(fixture, left) == metrics_contract(fixture, right)
                report['matched'] = report['matched'] and report['traceMatched']
            write_report(work / 'parity.json', report)
            if not report['matched']:
                raise ValueError('Runner outcomes or transaction traces differ')
        print(json.dumps(report, indent=2))
        return report
    except Exception as error:
        write_report(work / 'case-failure.json', {'schemaVersion': 1, 'status': 'failed',
                                                'error': str(error), 'kind': type(error).__name__})
        raise
    finally:
        lock.unlink(missing_ok=True)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--fixture', choices=FIXTURES, required=True)
    parser.add_argument('--seed', type=int, default=1)
    parser.add_argument('--negative-control', action='store_true')
    args = parser.parse_args(argv)
    run_case(Path(__file__).resolve().parents[1], args.fixture, args.seed, args.negative_control)


if __name__ == '__main__':
    main()
