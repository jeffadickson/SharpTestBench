#!/usr/bin/env python3
"""Fail-closed comparison of independently produced runner results (issues #60/#61)."""


def signature(rows):
    """Return sorted case identities/statuses; malformed or nonpassing runs fail."""
    if not isinstance(rows, list) or not rows:
        raise ValueError('Runner must produce a nonempty result array')
    observed = set()
    result = []
    for row in rows:
        if not isinstance(row, dict):
            raise ValueError('Each result must be an object')
        identity = row.get('QualifiedName')
        if identity is None or identity == '':
            identity = row.get('Name')
        if not isinstance(identity, str) or not identity.strip():
            raise ValueError('Each result needs a nonempty test identity')
        if identity != identity.strip() or any(ord(c) < 32 or ord(c) == 127 for c in identity):
            raise ValueError('Test identity contains whitespace padding or control characters')
        if identity in observed:
            raise ValueError('Duplicate test identity: ' + identity)
        observed.add(identity)
        if row.get('Status') != 'passed':
            raise ValueError('Required test did not pass: ' + identity)
        result.append((identity, 'passed'))
    return sorted(result)
