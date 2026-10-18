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


def expected_identities(names):
    """Validate an explicit expected set, independent of either runner's output."""
    if not isinstance(names, list) or not names:
        raise ValueError('Expected identities must be a nonempty array')
    signature([{'Name': name, 'Status': 'passed'} for name in names])
    return set(names)


def compare(legacy, native, expected_names):
    """Attest exact selection and seed agreement, without comparing wall time."""
    expected = expected_identities(expected_names)
    a, b = signature(legacy), signature(native)

    def seeds(rows):
        result = {}
        for row in rows:
            seed = row.get('Seed')
            if type(seed) is not int or not 0 <= seed < 2**64:
                raise ValueError('Each result needs an unsigned 64-bit Seed')
            identity = row.get('QualifiedName') or row.get('Name')
            result[identity] = seed
        return result

    left, right = seeds(legacy), seeds(native)
    missing = {'legacy': sorted(expected - left.keys()), 'native': sorted(expected - right.keys())}
    unexpected = {'legacy': sorted(left.keys() - expected), 'native': sorted(right.keys() - expected)}
    different_seeds = sorted(name for name in left.keys() & right.keys() if left[name] != right[name])
    return {'schemaVersion': 1, 'legacy': a, 'native': b,
            'expected': sorted(expected), 'missing': missing, 'unexpected': unexpected,
            'seedMismatches': different_seeds,
            'matched': a == b and not any(missing.values()) and not any(unexpected.values()) and not different_seeds}
