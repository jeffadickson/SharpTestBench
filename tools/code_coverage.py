"""Run pinned Coverlet and ReportGenerator without rebuilding instrumented assemblies."""
from pathlib import Path
import json
import subprocess
import sys
from code_coverage_report import validate

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = Path('build/code-coverage')
SUITES = (
    ('managed', 'SharpTestBench.Tests', ['SharpTestBench']),
    ('generators', 'SharpTestBench.Generators.Tests', ['SharpTestBench.Generators']),
)

def run(command):
    subprocess.run(command, cwd=ROOT, check=True)

def main():
    (ROOT / OUTPUT).mkdir(parents=True, exist_ok=True)
    # Remove only this command's known completion artifacts; old success is never evidence.
    for name in ('summary.json', 'managed.cobertura.xml', 'generators.cobertura.xml'):
        (ROOT / OUTPUT / name).unlink(missing_ok=True)
    summaries = {}
    for label, project, assemblies in SUITES:
        assembly = f'tests/{project}/bin/Release/net10.0/{project}.dll'
        output = (OUTPUT / label).as_posix()
        args = f'{assembly} -xml {(OUTPUT / (label + "-tests.xml")).as_posix()}'
        run(['dotnet', 'tool', 'run', 'coverlet', assembly, '--target', 'dotnet',
             '--targetargs', args, '--format', 'cobertura', '--output', output,
             '--include', '[' + assemblies[0] + ']*'])
        summaries[label] = validate(ROOT / (output + '.cobertura.xml'), assemblies)
    run(['dotnet', 'tool', 'run', 'reportgenerator',
         '-reports:' + (OUTPUT / '*.cobertura.xml').as_posix(),
         '-targetdir:' + (OUTPUT / 'report').as_posix(),
         '-reporttypes:Html;Cobertura;JsonSummary;TextSummary', '-verbosity:Warning'])
    combined = validate(ROOT / OUTPUT / 'report/Cobertura.xml', ['SharpTestBench', 'SharpTestBench.Generators'])
    (ROOT / OUTPUT / 'summary.json').write_text(json.dumps({'suites': summaries, 'combined': combined}, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(combined, indent=2))

if __name__ == '__main__':
    main()
