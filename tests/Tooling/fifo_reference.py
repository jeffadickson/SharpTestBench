"""Independent data-only replay oracle for the pinned FIFO fixture.

C# owns compilation, simulator execution and evidence publication. This module
checks a captured cycle trace against the requested source/ready/reset stimulus.
"""
from collections import deque
import argparse
import json

FIELDS = {'Reset', 'Ready', 'Offer', 'Data', 'Keep', 'Last', 'Id', 'Dest', 'User'}
COUNTERS = {'accepted', 'consumed', 'flushed', 'stalls', 'resets', 'cycles'}


def validate(plan, metrics, sidebands=True):
    def require(condition, message):
        if not condition:
            raise ValueError(message)
    require(type(sidebands) is bool, 'sideband mode must be boolean')
    require(type(plan) is list and len(plan) >= 3, 'nonempty stimulus required')
    require(type(metrics) is dict and set(metrics) == COUNTERS | {'trace'}, 'metric fields differ')
    trace = metrics['trace']
    require(type(trace) is list and len(trace) == len(plan), 'trace length differs')
    require(all(type(metrics[k]) is int and metrics[k] >= 0 for k in COUNTERS), 'invalid counters')
    queue = deque()
    pending = held = None
    counts = dict.fromkeys(COUNTERS, 0)
    counts['cycles'] = len(plan)
    for index, (stimulus, observation) in enumerate(zip(plan, trace)):
        require(type(stimulus) is dict and set(stimulus) == FIELDS, 'stimulus fields differ')
        require(all(type(stimulus[k]) is bool for k in ['Reset', 'Ready', 'Offer', 'Last']), 'invalid stimulus flags')
        for key, maximum in [('Data',65535),('Keep',3),('Id',15),('Dest',15),('User',3)]:
            require(type(stimulus[key]) is int and 0 <= stimulus[key] <= maximum, 'invalid stimulus payload')
        require(type(observation) is list and len(observation) == 11 and all(type(x) is int and x >= 0 for x in observation), 'invalid trace row')
        cycle, reset, take, valid, ready, *word = observation
        require(cycle == index and reset == int(stimulus['Reset']) and ready == int(stimulus['Ready']), 'control differs from stimulus')
        require(take in (0,1) and valid in (0,1), 'invalid transfer flags')
        require(valid or not any(word), 'invalid output payload must be normalized')
        if reset:
            require(not take and not valid, 'reset transfer')
            counts['flushed'] += len(queue)
            counts['resets'] += 1
            queue.clear()
            pending = held = None
            continue
        if held is not None:
            require(valid and tuple(word) == held, 'stalled output changed')
        if pending is None and stimulus['Offer']:
            pending = (stimulus['Data'], stimulus['Keep'] if sidebands else 3,
                       int(stimulus['Last']) if sidebands else 1,
                       stimulus['Id'] if sidebands else 0, stimulus['Dest'] if sidebands else 0,
                       stimulus['User'] if sidebands else 0)
        if take:
            require(pending is not None, 'accepted without offered source beat')
            queue.append(pending)
            pending = None
            counts['accepted'] += 1
        if valid:
            require(queue and tuple(word) == queue[0], 'output differs from accepted order')
            if ready:
                queue.popleft()
                counts['consumed'] += 1
            else:
                counts['stalls'] += 1
        held = tuple(word) if valid and not ready else None
    require(plan[0]['Reset'], 'missing initial reset')
    require(not queue and pending is None and held is None, 'incomplete drain')
    require(counts['accepted'] > 0 and counts['consumed'] > 0, 'empty workload')
    require(all(metrics[k] == counts[k] for k in COUNTERS), 'counter mismatch')
    return {'schemaVersion': 1, 'valid': True, **counts}


def _pairs(values):
    result = {}
    for key, value in values:
        if key in result:
            raise ValueError('duplicate JSON key: ' + key)
        result[key] = value
    return result



MAXIMUM_JSON_BYTES = 16 * 1024 * 1024


def read_json(path, maximum=MAXIMUM_JSON_BYTES):
    """Read bounded oracle data with the same strict scalar rules as managed inputs."""
    import math
    if type(maximum) is not int or not 1 <= maximum <= MAXIMUM_JSON_BYTES:
        raise ValueError('invalid JSON byte budget')
    with open(path, 'rb') as stream:
        raw = stream.read(maximum + 1)
    if len(raw) > maximum:
        raise ValueError('JSON byte budget exceeded')
    def constant(_):
        raise ValueError('nonfinite JSON number')
    try:
        value = json.loads(raw.decode('utf-8-sig'), object_pairs_hook=_pairs, parse_constant=constant)
    except (UnicodeError, RecursionError) as error:
        raise ValueError('invalid JSON encoding or nesting') from error
    pending = [(value, 0)]
    while pending:
        item, depth = pending.pop()
        if isinstance(item, (dict, list)):
            depth += 1
            if depth > 64:
                raise ValueError('JSON nesting budget exceeded')
            children = list(item.keys()) + list(item.values()) if isinstance(item, dict) else item
            pending.extend((child, depth) for child in children)
        elif isinstance(item, str):
            if any(0xD800 <= ord(character) <= 0xDFFF for character in item):
                raise ValueError('invalid JSON Unicode scalar')
        elif isinstance(item, float) and not math.isfinite(item):
            raise ValueError('nonfinite JSON number')
    return value

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', required=True)
    parser.add_argument('--metrics', required=True)
    parser.add_argument('--disabled-sidebands', action='store_true')
    args = parser.parse_args()
    plan = read_json(args.plan)
    metrics = read_json(args.metrics)
    print(json.dumps(validate(plan, metrics, not args.disabled_sidebands)))


if __name__ == '__main__':
    main()
