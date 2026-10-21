"""Opt-in simulator qualification; C# remains responsible for both launch paths."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tests/Tooling/managed_compat'))
from managed_rpc import call


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--configs', default=','.join(map(str, range(12))))
    parser.add_argument('--name', required=True)
    parser.add_argument('--vectors', default='0')
    parser.add_argument('--kinds', default='valid,index,onehot')
    args = parser.parse_args()
    configs = [int(x) for x in args.configs.split(',')]
    if not configs or len(set(configs)) != len(configs) or any(x not in range(12) for x in configs):
        parser.error('configs must be unique integers from 0 to 11')
    if not re.fullmatch(r'[a-zA-Z0-9_-]+', args.name):
        parser.error('name must be a simple report identifier')
    vectors = [int(x) for x in args.vectors.split(',')]
    kinds = args.kinds.split(',')
    if not vectors or len(set(vectors)) != len(vectors) or any(x not in range(453) for x in vectors):
        parser.error('vectors must be unique integers from 0 to 452')
    if not kinds or len(set(kinds)) != len(kinds) or any(x not in ('valid', 'index', 'onehot') for x in kinds):
        parser.error('kinds must be unique valid/index/onehot labels')
    report = ROOT / 'build' / (args.name + '.json')
    report.unlink(missing_ok=True)
    work = Path(tempfile.mkdtemp(prefix=args.name + '-', dir=ROOT / 'build'))
    rows = []
    value = dict(schemaVersion=1, matched=False, cases=rows, error='Qualification interrupted', workspace=str(work))
    try:
        golden = json.loads((ROOT / 'tests/SharpTestBench.Tools.Tests/priority-seed-vectors.json').read_text())
        requests = golden['83']
        tools = ROOT / 'tools/SharpTestBench.Tools/bin/Release/net10.0/SharpTestBench.Tools.dll'
        runner = ROOT / 'tools/SharpTestBench.Runner/bin/Release/net10.0/SharpTestBench.Runner.dll'
        base = json.loads((ROOT / 'build/priority-83/manifest.json').read_text(encoding='utf-8'))
        paths = [Path(x) for x in base['sources']] + [Path(base[k]) for k in ('assembly', 'nativeHost', 'gpiLibrary', 'icarusBootstrap')] + [tools, runner, ROOT / 'tests/SharpTestBench.Tools.Tests/priority-seed-vectors.json']
        def hashes():
            return {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in paths}
        before = hashes()
        for config in configs:
            for label in kinds:
                kind = ('valid', 'index', 'onehot').index(label)
                for onset in vectors:
                    case = work / f'c{config}-k{kind}-v{onset}'
                    case.mkdir()
                    manifest = dict(base, outputDirectory=str(case / 'native'))
                    manifest_path = case / 'manifest.json'
                    manifest_path.write_text(json.dumps(manifest), encoding='utf-8')
                    width = (2, 3, 5, 8, 17, 32)[config // 2]
                    detected = onset if kind == 0 else next((i for i in range(onset, 453) if requests[i] & ((1 << width) - 1)), None)
                    if detected is None:
                        raise ValueError('Requested fault is not observable')
                    marker = f'PRIORITY_MISMATCH configuration={config} fault={kind} observed={label} vector={detected} '
                    results = []
                    for side in ('legacy', 'native'):
                        env = call('dual_runner_ci', 'clean_environment', [dict(os.environ)])
                        env.update(PRIORITY_FAULT='1', PRIORITY_CONFIG=str(config), PRIORITY_KIND=str(kind),
                                   PRIORITY_VECTOR=str(onset), PRIORITY_REPORT=str(case / (side + '-metrics.json')), STB_NATIVE_VPI_TIME='1')
                        command = ['dotnet', str(runner), 'run', str(manifest_path)]
                        result_path = case / 'native/icarus-results.json'
                        if side == 'legacy':
                            command = ['dotnet', str(tools), 'run', '--sim', 'icarus', '--no-build',
                                       '--assembly', base['assembly'], '--top', base['top'],
                                       '--entry-type', base['entryType'], '--work-dir', str(case / 'legacy'),
                                       '--label', 'legacy', '--seed', '83', '--exact', 'Entry.PriorityScoreboard']
                            for source in base['sources']:
                                command += ['--source', source]
                            result_path = case / 'legacy/legacy-results.json'
                        process = subprocess.run(command, cwd=ROOT, env=env, text=True, encoding='utf-8',
                                                 capture_output=True, timeout=120, check=False)
                        (case / (side + '.log')).write_text(process.stdout + process.stderr, encoding='utf-8')
                        data = call('strict_json', 'load', [str(result_path)])
                        if not call('dual_runner_ci', 'fault_detected', [data, process.returncode, ['Entry.PriorityScoreboard'], marker]):
                            raise AssertionError(f'Wrong fault result: {config}/{label}/{side}')
                        if len(data) != 1 or type(data[0]['Seed']) is not int or data[0]['Seed'] != 83:
                            raise AssertionError('Incorrect seed or case count')
                        results.append(dict(side=side, exitCode=process.returncode, seed=data[0]['Seed'], error=data[0]['Error']))
                    rows.append(dict(configuration=config, kind=label, onsetVector=onset, detectedVector=detected, matched=True, results=results))
        if hashes() != before:
            raise AssertionError('Qualification inputs changed')
        value = dict(schemaVersion=1, matched=True, cases=rows, inputs=before, workspace=str(work))
    except Exception as error:
        value = dict(schemaVersion=1, matched=False, cases=rows, error=str(error), workspace=str(work))
        raise
    finally:
        temporary = work / 'summary.json'
        temporary.write_text(json.dumps(value, indent=2), encoding='utf-8')
        os.replace(temporary, report)
    print(json.dumps(dict(matched=True, cases=len(rows), report=str(report))))


if __name__ == '__main__':
    main()
