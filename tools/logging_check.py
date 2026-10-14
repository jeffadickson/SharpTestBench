#!/usr/bin/env python3
"""Verify structured simulation log identity, source mapping and bounded text."""
import argparse
import json
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--label', default='icarus')
args = parser.parse_args()
root = Path(__file__).resolve().parents[1] / 'build'
events = [json.loads(line) for line in (root / (args.label + '-events.jsonl')).read_text().splitlines()]
results = json.loads((root / (args.label + '-results.json')).read_text())
result = next(r for r in results if r['Name'] == 'AR_StructuredLogging')
rows = [e for e in events if e['Test'] == result['QualifiedName']]
assert [e['Kind'] for e in rows] == ['test-start', 'message', 'message', 'message', 'test-end']
assert all(e['SchemaVersion'] == 1 and e['Seed'] == result['Seed'] and e['Stage'] == result['Stage'] for e in rows)
assert [e['TimeStep'] for e in rows] == sorted(e['TimeStep'] for e in rows)
parent, child, large = rows[1:4]
assert parent['Message'] == 'parent\nmessage' and parent['Level'] == 'Warning' and parent['TaskId'] is None
assert child['TaskName'] == 'log-child' and isinstance(child['TaskId'], int)
assert child['ExceptionType'] == 'System.FormatException' and 'AR_StructuredLogging' in child['ExceptionDetail']
assert child['SourceFile'].endswith('Tests.cs') and child['SourceLine'] > 0 and child['SourceMember'] == 'AR_StructuredLogging'
assert large['Truncated'] and len(large['Message']) == 16384
assert rows[-1]['Message'] == 'passed'
print('LOGGING_PASS', args.label, 'identity, time, source, exception, multiline and bounded JSON fields')
