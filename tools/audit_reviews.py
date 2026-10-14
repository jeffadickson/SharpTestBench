#!/usr/bin/env python3
"""Validate evidence-backed API reviews independently of the generated inventory."""
import argparse, csv, hashlib, json
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
STATUSES = {'idiomatic-equivalent', 'intentional-difference', 'gap', 'internal-adapter', 'deprecated-omission'}

def validate(source):
    inventory = list(csv.DictReader((ROOT / 'docs/UPSTREAM_API_INVENTORY.csv').open()))
    keys = {(r['Module'], r['Definition'], int(r['SourceLine'])) for r in inventory}
    review = json.loads((ROOT / 'docs/API_REVIEWS.json').read_text())
    upstream = json.loads((ROOT / 'third_party/UPSTREAM.json').read_text())
    if review['source_revision'] != upstream['commit']: raise ValueError('Review revision differs from pinned upstream')
    seen = set()
    counts = {}
    for row in review['reviews']:
        key = (row['module'], row['definition'], row['source_line'])
        if key not in keys or key in seen: raise ValueError(f'Unknown or duplicate review: {key}')
        seen.add(key)
        if row['classification'] not in STATUSES: raise ValueError(f'Unknown classification: {key}')
        if not row['reason'].strip() or not row['evidence']: raise ValueError(f'Missing evidence: {key}')
        for evidence in row['evidence']:
            path = ROOT / evidence['path']
            if not path.is_file() or evidence['contains'] not in path.read_text(): raise ValueError(f'Stale evidence: {key} {evidence}')
        counts[row['classification']] = counts.get(row['classification'], 0) + 1
    for module, expected in review['source_hashes'].items():
        path = source / 'src' / Path(*module.split('.')).with_suffix('.py')
        if hashlib.sha256(path.read_bytes()).hexdigest() != expected: raise ValueError(f'Upstream source changed: {module}; re-review required')
    if any(r['module'] not in review['source_hashes'] for r in review['reviews']): raise ValueError('Review lacks a source hash')
    return {'inventoried_definitions': len(inventory), 'reviewed_definitions': len(seen),
            'unreviewed_definitions': len(keys - seen), 'classifications': counts,
            'note': 'Review counts are not a parity percentage; aliases and dynamic exports remain outside this inventory.'}

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', required=True, type=Path)
    args = parser.parse_args()
    print(json.dumps(validate(args.source), indent=2))

if __name__ == '__main__': main()
