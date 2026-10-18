#!/usr/bin/env python3
"""Build explicit fixture manifests for dual-runner CI; no discovery from results."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys

FIXTURES = {
    'counter': {'project': 'examples/Counter', 'assembly': 'Counter', 'source': 'counter.sv', 'top': 'counter', 'test': 'Entry.ResetAndCount'},
    'dsp': {'project': 'examples/DspFir', 'assembly': 'DspFir', 'source': 'dsp_fir.sv', 'top': 'dsp_fir', 'test': 'Entry.FirScoreboard'},
    'cpu': {'project': 'examples/CpuCore', 'assembly': 'CpuCore', 'source': 'cpu_core.sv', 'top': 'cpu_core', 'test': 'Entry.CpuScoreboard'},
}


def make_manifest(root, fixture, seed, adapter, output_directory):
    """Resolve all paths independently of the generated manifest's location."""
    root = Path(root).resolve()
    if fixture not in FIXTURES:
        raise ValueError('Unknown parity fixture: ' + str(fixture))
    if type(seed) is not int or not 0 <= seed < 2**64:
        raise ValueError('Seed must be an unsigned 64-bit integer')
    for key in ('bootstrap', 'library', 'entryPoint', 'implementation', 'dotnetRoot'):
        if not isinstance(adapter.get(key), str) or not adapter[key].strip():
            raise ValueError('Missing adapter setting: ' + key)
    output = (root / output_directory).resolve()
    build = (root / 'build').resolve()
    if output == build or not output.is_relative_to(build):
        raise ValueError('Parity output must be a directory below build/')
    item = FIXTURES[fixture]
    return {
        'schemaVersion': 1, 'simulator': 'icarus', 'top': item['top'],
        'sources': [str(root / item['project'] / item['source'])],
        'assembly': str(root / item['project'] / 'bin/Release/net10.0' / (item['assembly'] + '.dll')),
        'nativeHost': str(root / 'build/libsharptestbench_host.so'),
        'icarusBootstrap': str(Path(adapter['bootstrap']).resolve()),
        'gpiLibrary': str(Path(adapter['library']).resolve()),
        'gpiEntryPoint': adapter['entryPoint'], 'gpiImpl': adapter['implementation'],
        'dotnetRoot': str(Path(adapter['dotnetRoot']).resolve()),
        'outputDirectory': str(output), 'seed': seed,
        'entryType': 'Entry, ' + item['assembly'],
        'expectedTests': 1, 'expectedNames': [item['test']],
    }


def installed_adapter():
    """Inspect the pinned development adapter only when preparing simulator CI."""
    from cocotb_tools import config
    library, symbol = config.gpi_entry_point().rsplit(',', 1)
    return {'bootstrap': str(config.lib_entry('vpi', 'icarus')), 'library': library,
            'entryPoint': symbol,
            'implementation': subprocess.check_output([sys.executable, '-m', 'cocotb_tools.config', '--gpi-impl', 'icarus', 'vpi'], text=True).strip(),
            'dotnetRoot': os.environ.get('DOTNET_ROOT', '/usr/share/dotnet')}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--fixture', choices=FIXTURES, required=True)
    parser.add_argument('--seed', type=int, default=1)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--output-directory', required=True)
    args = parser.parse_args(argv)
    root = Path(__file__).resolve().parents[1]
    manifest = make_manifest(root, args.fixture, args.seed, installed_adapter(), args.output_directory)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
