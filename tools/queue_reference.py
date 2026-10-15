#!/usr/bin/env python3
"""Compare immediate ordering/occupancy with hash-verified pinned cocotb queue code.

No simulator is needed for Python's immediate operations with no waiting tasks.
This comparison deliberately excludes waiter scheduling and equal-key stability.
"""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--label', required=True)
parser.add_argument('--build', type=Path, default=root / 'build')
args = parser.parse_args()
source = root / 'build/upstream/cocotb-source/src/cocotb/queue.py'
expected_hash = json.loads((root / 'docs/API_REVIEWS.json').read_text())['source_hashes']['cocotb.queue']
if hashlib.sha256(source.read_bytes()).hexdigest() != expected_hash:
    raise SystemExit('Pinned cocotb queue source hash mismatch')
spec = importlib.util.spec_from_file_location('queue_reference_upstream', source)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
expected = {}
for name, kind in [('fifo', module.Queue), ('lifo', module.LifoQueue),
                   ('priority', module.PriorityQueue), ('reverse', module.PriorityQueue)]:
    queue = kind(maxsize=4)
    rows = expected[name] = []
    for value in [4, -2, 4, 1]:
        queue.put_nowait(-value if name == 'reverse' else value)
        rows.append([value, queue.qsize(), int(queue.empty()), int(queue.full())])
    while not queue.empty():
        value = queue.get_nowait()
        rows.append([-value if name == 'reverse' else value, queue.qsize(), int(queue.empty()), int(queue.full())])
actual = json.loads((args.build / (args.label + '-trace.txt.queues.json')).read_text())
if actual != expected:
    raise SystemExit(f'Queue ordering/occupancy mismatch: actual={actual!r}, expected={expected!r}')
print('Queue reference: 32 ordering/occupancy rows match pinned cocotb (FIFO, LIFO, priority, reverse)')
