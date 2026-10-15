#!/usr/bin/env python3
"""Generate/check nine-state resolution and conversion fixtures from pinned cocotb."""
import argparse
import hashlib
import importlib
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--check', action='store_true')
args = parser.parse_args()
hashes = json.loads((root / 'docs/API_REVIEWS.json').read_text())['source_hashes']
for name in ['cocotb.types._logic', 'cocotb.types._logic_array', 'cocotb.types._resolve']:
    module = importlib.import_module(name)
    if hashlib.sha256(Path(module.__file__).read_bytes()).hexdigest() != hashes[name]:
        raise SystemExit(f'Installed {name} differs from pinned source')
from cocotb.types import Logic, LogicArray, Range
from cocotb.types._resolve import RESOLVE_X
from cocotb.preview import Feature, is_enabled
if RESOLVE_X is not None or is_enabled(Feature.STRICT_RESOLVE):
    raise SystemExit('Oracle requires default resolution and STRICT_RESOLVE disabled')

states = 'UX01ZWLH-'
scalar = [dict(symbol=c, resolvable=Logic(c).is_resolvable,
               resolved={p: str(Logic(c).resolve(p)) for p in ['weak', 'zeros', 'ones']}) for c in states]
vectors = []
for text in [a + b for a in states for b in states] + [states, '', '0000000000000001', '100000000', 'LHHL']:
    value = LogicArray(text)
    row = dict(bits=text, resolvable=value.is_resolvable,
               resolved={p: str(value.resolve(p)) for p in ['weak', 'zeros', 'ones']})
    try:
        row.update(unsigned=str(value.to_unsigned()), signed=str(value.to_signed()))
        try:
            row.update(big=list(value.to_bytes(byteorder='big')), little=list(value.to_bytes(byteorder='little')))
        except OverflowError:
            row['bytes_error'] = 'OverflowError'
    except ValueError:
        row['unsigned'] = None
    vectors.append(row)
integers = []
for width in [1, 2, 7, 8, 9, 16, 65, 128]:
    limit = 1 << (width - 1)
    for number in [-limit-1, -limit, -1, 0, 1, limit-1, limit, (1 << width) + 3]:
        for signed in [False, True]:
            for wrap in [False, True]:
                row = dict(width=width, value=str(number), signed=signed, wrap=wrap)
                try:
                    factory = LogicArray.from_signed if signed else LogicArray.from_unsigned
                    value = factory(number, Range(-3, 'to', width-4), on_overflow='wrap' if wrap else 'error')
                    row.update(bits=str(value), unsigned=str(value.to_unsigned()), signed_value=str(value.to_signed()),
                               big=list(value.to_bytes(byteorder='big')), little=list(value.to_bytes(byteorder='little')))
                except ValueError:
                    row['bits'] = None
                integers.append(row)
content = json.dumps(dict(scalar=scalar, vectors=vectors, integers=integers), separators=(",", ":")) + '\n'
path = root / 'tests/SharpTestBench.Tests/logic-reference.json'
if args.check:
    if path.read_text() != content:
        raise SystemExit('Logic reference drift; regenerate and review')
else:
    path.write_text(content)
print(f'Logic reference: {len(scalar)} scalar, {len(vectors)} vector, {len(integers)} integer cases recorded from pinned cocotb')
