#!/usr/bin/env python3
"""Qualify manifest compiler options, exact selection, and input attestation on Icarus."""
import json
import os
from pathlib import Path
import subprocess
import sys

from dual_runner_ci import clean_environment
from dual_runner_manifest import installed_adapter, make_manifest
from dual_runner_parity import compare, write_report
from strict_json import load

ROOT = Path(__file__).resolve().parents[1]


def main():
    work = ROOT / 'build/manifest-check'
    work.mkdir(parents=True, exist_ok=True)
    lock = work / '.manifest-check.lock'
    with lock.open('x', encoding='utf-8') as output:
        output.write(str(os.getpid()))
    try:
        (work / 'summary.json').unlink(missing_ok=True)
        env = clean_environment(os.environ)
        env['STB_NATIVE_VPI_TIME'] = '1'

        def execute(command, label, expected=0):
            with (work / (label + '.log')).open('w', encoding='utf-8') as log:
                result = subprocess.run(list(map(str, command)), cwd=ROOT, env=env,
                                        stdout=log, stderr=subprocess.STDOUT, timeout=180)
            if result.returncode != expected:
                raise RuntimeError(label + ' did not produce expected exit code; see its log')

        execute(['dotnet', 'build', ROOT / 'examples/Counter', '-c', 'Release', '-v:q'], 'build-counter')
        from native_build import build_host, sdk_root
        build_host(ROOT, sdk_root(), ROOT / 'build')
        adapter = installed_adapter()
        include = work / 'include with spaces'
        include.mkdir(exist_ok=True)
        (include / 'counter_config.vh').write_text('`define COUNTER_INCREMENT `STB_INCREMENT\n', encoding='utf-8')
        source = work / 'parameterized counter.sv'
        source.write_text('''`include "counter_config.vh"
module counter #(parameter WIDTH=4)(input logic clk, reset, enable, output logic [WIDTH-1:0] count);
    always @(posedge clk)
        if (reset) count <= 0;
        else if (enable) count <= count + `COUNTER_INCREMENT;
endmodule
''', encoding='utf-8')
        runner = ROOT / 'tools/SharpTestBench.Runner/bin/Release/net10.0/SharpTestBench.Runner.dll'
        manifest = make_manifest(ROOT, 'counter', 17, adapter, work / 'counter-native')
        manifest.update(sources=[str(source)], includeDirectories=[str(include)], defines={'STB_INCREMENT': '1'},
                        parameters={'WIDTH': '8'}, testNames=['Entry.ResetAndCount'])
        path = work / 'counter.json'
        write_report(path, manifest)
        execute(['dotnet', runner, 'run', path], 'compile-options')
        evidence = load(work / 'counter-native/run-manifest.json')
        if evidence['Status'] != 'passed' or not any(item['Role'] == 'include' for item in evidence['Inputs']):
            raise RuntimeError('Compiler options did not produce an attested passing run')
        # Removing the override exposes the four-bit default and must fail the eight-bit binding.
        invalid = dict(manifest, outputDirectory=str(work / 'wrong-width'), parameters={})
        write_report(work / 'wrong-width.json', invalid)
        execute(['dotnet', runner, 'run', work / 'wrong-width.json'], 'parameter-negative', expected=1)
        failure = load(work / 'wrong-width/icarus-results.json')
        if len(failure) != 1 or failure[0]['Status'] != 'failed' or 'is 4 bits, expected 8' not in failure[0].get('Error', ''):
            raise RuntimeError('Parameter negative control did not reject the expected width mismatch')
        # Select one test from a real multi-case assembly, preserving discovery-assigned seeds.
        selected = 'Tests.A_TypesAndSignals'
        manifest.update(assembly=str(ROOT / 'tests/Integration/bin/Release/net10.0/Integration.dll'),
                        sources=[str(ROOT / 'tests/Integration/top.sv')], top='top', entryType='Entry, Integration',
                        includeDirectories=[], defines={}, parameters={}, outputDirectory=str(work / 'selection-native'),
                        expectedNames=[selected], testNames=[selected])
        write_report(work / 'selection.json', manifest)
        execute(['dotnet', runner, 'run', work / 'selection.json'], 'native-selection')
        execute([sys.executable, ROOT / 'tools/run.py', '--sim', 'icarus', '--no-build', '--assembly', manifest['assembly'],
                 '--source', manifest['sources'][0], '--top', 'top', '--entry-type', 'Entry, Integration',
                 '--work-dir', work / 'selection-legacy', '--label', 'selected', '--exact', selected, '--seed', '17'],
                'legacy-selection')
        comparison = compare(load(work / 'selection-legacy/selected-results.json'),
                             load(work / 'selection-native/icarus-results.json'), [selected])
        if not comparison['matched']:
            raise RuntimeError('Selected native and existing runner cases differ')
        write_report(work / 'summary.json', {'schemaVersion': 1, 'compilerOptions': True,
                     'parameterNegativeControl': True, 'selectedCaseParity': comparison,
                     'inputAttestation': True})
        print('MANIFEST_CHECK_PASS compiler options, parameter negative, exact selection and provenance')
    finally:
        lock.unlink(missing_ok=True)


if __name__ == '__main__':
    main()
