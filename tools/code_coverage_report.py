"""Validate Cobertura counters before publishing managed code coverage."""
from pathlib import Path
import math
import xml.etree.ElementTree as ET

MAX_BYTES = 16 * 1024 * 1024

def validate(path, expected):
    with Path(path).open('rb') as stream:
        raw = stream.read(MAX_BYTES + 1)
    if len(raw) > MAX_BYTES:
        raise ValueError('Coverage report exceeds size limit')
    raw = raw.replace(b'<!DOCTYPE coverage SYSTEM "http://cobertura.sourceforge.net/xml/coverage-04.dtd">', b'')
    if b'<!DOCTYPE' in raw.upper() or b'<!ENTITY' in raw.upper():
        raise ValueError('Coverage DTD/entity declarations are forbidden')
    root = ET.fromstring(raw)
    if root.tag != 'coverage':
        raise ValueError('Expected Cobertura coverage root')
    totals = {}
    for kind in ('lines', 'branches'):
        valid = int(root.attrib[kind + '-valid'])
        covered = int(root.attrib[kind + '-covered'])
        rate = float(root.attrib[('line' if kind == 'lines' else 'branch') + '-rate'])
        if valid <= 0 or covered < 0 or covered > valid:
            raise ValueError('Invalid coverage counters')
        if not math.isfinite(rate) or not 0 <= rate <= 1 or abs(rate - covered / valid) > 0.000100000001:
            raise ValueError('Coverage rate disagrees with counters')
        totals[kind] = {'covered': covered, 'valid': valid, 'percent': 100 * covered / valid}
    packages = root.findall('./packages/package')
    names = [item.attrib['name'] for item in packages]
    if len(names) != len(set(names)) or set(names) != set(expected):
        raise ValueError('Coverage assembly set differs from expected assemblies')
    if any(not package.findall('./classes/class') for package in packages):
        raise ValueError('Coverage package has no source classes')
    return {'assemblies': sorted(names), **totals}
