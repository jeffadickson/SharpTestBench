#!/usr/bin/env python3
"""Generate/check managed range and value-array fixtures from hash-verified pinned cocotb."""
import argparse
import hashlib
import importlib
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--check', action='store_true')
args = parser.parse_args()
modules = ['cocotb.types._range', 'cocotb.types._abstract_array', 'cocotb.types._array']
hashes = json.loads((root / 'docs/API_REVIEWS.json').read_text())['source_hashes']
for name in modules:
    module = importlib.import_module(name)
    if hashlib.sha256(Path(module.__file__).read_bytes()).hexdigest() != hashes[name]:
        raise SystemExit(f'Installed {name} differs from reviewed pinned source')
from cocotb.types import Array, Range

cases = []
for left in range(-3, 4):
    for right in range(-3, 4):
        for ascending in [True, False]:
            span = Range(left, 'to' if ascending else 'downto', right)
            values = [i % 3 for i in range(len(span))]
            array = Array(values, range=span)
            found = []
            for value in range(4):
                try:
                    index = array.index(value)
                except ValueError:
                    index = None
                found.append(index)
            row = dict(left=left, right=right, ascending=ascending, indices=list(span),
                       values=values, found=found, counts=[array.count(v) for v in range(4)])
            if len(span) >= 2:
                start, stop = span[0], span[1]
                array[start:stop] = [7, 8]
            row['after_slice'] = list(array)
            array.range = Range(10, 'to', 10 + len(array) - 1)
            row['reindexed'] = [array[i] for i in array.range]
            cases.append(row)
content = json.dumps(cases, indent=2) + '\n'
path = root / 'tests/SharpTestBench.Tests/array-reference.json'
if args.check:
    if path.read_text() != content:
        raise SystemExit('Range/array reference fixture drift; regenerate and review')
else:
    path.write_text(content)
print(f'Range/array reference: {len(cases)} cases verified against pinned cocotb')
