#!/usr/bin/env python3
"""Validate exported functional coverage against the known HDL fixture."""
import argparse
import json
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--label', default='icarus')
args = parser.parse_args()
root = Path(__file__).resolve().parents[1] / 'build'
report = json.loads((root / (args.label + '-results.coverage.json')).read_text())
results = json.loads((root / (args.label + '-results.json')).read_text())
assert report['SchemaVersion'] == 1 and report['Kind'] == 'functional'
row = next(r for r in report['Tests'] if r['Test'] == 'AQ_FunctionalCoverage')
result = next(r for r in results if r['Name'] == row['Test'])
assert row['Seed'] == result['Seed'] and row['Status'] == result['Status'] == 'passed'
point, = row['Coverage']['Points']
assert point['Name'] == 'packet-payload'
assert (point['Samples'], point['Unmatched'], point['Percent']) == (4, 0, 50)
assert [(b['Name'], b['Hits'], b['Goal'], b['Covered']) for b in point['Bins']] == [
    ('zero', 2, 1, True), ('nonzero', 2, 3, False)]
assert row['Coverage']['Percent'] == 50
print('COVERAGE_PASS', args.label, 'exact HDL sample counts, goals and report identity')
