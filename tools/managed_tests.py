#!/usr/bin/env python3
"""Run managed xUnit cases and verify failure isolation in a disposable mutated copy."""
import os
from pathlib import Path
import shutil
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
SDK = str(Path(os.environ['CS_DOTNET_ROOT']) / 'dotnet') if 'CS_DOTNET_ROOT' in os.environ else 'dotnet'
PROJECT = ROOT / 'tests/SharpTestBench.Tests'
BUILD = ROOT / 'build'
EXPECTED_CASES = 480


def verify(report, failed):
    cases = ET.parse(report).findall('.//test')
    assert len(cases) == EXPECTED_CASES, f'Expected {EXPECTED_CASES} discovered cases, got {len(cases)}'
    failures = [case for case in cases if case.attrib['result'] == 'Fail']
    assert len(failures) == failed, failures
    assert sum(case.attrib['result'] == 'Pass' for case in cases) == EXPECTED_CASES - failed
    if failed:
        assert failures[0].attrib['name'] == 'ManagedTests.IntegerBoundaries'
        assert any(c.attrib['name'] == 'ManagedTests.FunctionalCoverage' and c.attrib['result'] == 'Pass' for c in cases)


def main():
    BUILD.mkdir(exist_ok=True)
    report = BUILD / 'managed-results.xml'
    report.unlink(missing_ok=True)
    subprocess.run([SDK, 'run', '--project', str(PROJECT), '-c', 'Release', '--no-build', '--', '-xml', str(report)], check=True)
    verify(report, 0)
    probe = BUILD / 'managed-failure-probe'
    probe.mkdir(exist_ok=True)
    project = (PROJECT / 'SharpTestBench.Tests.csproj').read_text().replace('../../src/SharpTestBench/SharpTestBench.csproj', str(ROOT / 'src/SharpTestBench/SharpTestBench.csproj'))
    (probe / 'FailureProbe.csproj').write_text(project)
    source = (PROJECT / 'Program.cs').read_text()
    original = 'new UInt<W8>(255).Value == 255'
    assert source.count(original) == 1
    (probe / 'Program.cs').write_text(source.replace(original, 'new UInt<W8>(255).Value == 254'))
    for name in (*sorted(p.name for p in PROJECT.glob('*.cs') if p.name != 'Program.cs'), 'array-reference.json', 'logic-reference.json'):
        shutil.copyfile(PROJECT / name, probe / name)
    shutil.copyfile(PROJECT / 'nine-state-reference.txt', probe / 'nine-state-reference.txt')
    shutil.copyfile(PROJECT / 'packages.lock.json', probe / 'packages.lock.json')
    subprocess.run([SDK, 'build', str(probe / 'FailureProbe.csproj'), '-c', 'Release', '--disable-build-servers', '-m:1', '-p:RestoreLockedMode=true'], check=True)
    report = BUILD / 'managed-injected-failure.xml'
    report.unlink(missing_ok=True)
    result = subprocess.run([SDK, str(probe / 'bin/Release/net10.0/FailureProbe.dll'), '-xml', str(report)])
    assert result.returncode != 0, 'Injected failure must fail the process'
    verify(report, 1)
    print(f'MANAGED_PASS {EXPECTED_CASES} cases; injected failure named correctly; remaining {EXPECTED_CASES - 1} cases passed')


if __name__ == '__main__':
    main()
