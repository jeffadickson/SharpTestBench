"""Independent arbitrary-precision reference for the committed priority masks."""
import argparse
import json
from pathlib import Path


def schedules():
    result = {}
    for initial in (0, 1, 17, 83, 2**32-1, 2**32, 2**63, 2**64-1):
        masks = list(range(256)) + [2**32-1, 2**31, 2**31+1, 0xaaaaaaaa, 0x55555555]
        for bit in range(32):
            masks += [2**bit, (2**32-1)-2**bit]
        state = initial
        for _ in range(128):
            state = (6364136223846793005 * state + 1442695040888963407) % 2**64
            masks.append(state // 2**32)
        assert len(masks) == 453
        result[str(initial)] = masks
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument('--check', type=Path)
    mode.add_argument('--output', type=Path)
    args = parser.parse_args()
    expected = schedules()
    if args.check:
        if json.loads(args.check.read_text(encoding='utf-8')) != expected:
            raise SystemExit('Committed priority masks differ from the reference')
        print('8 independent priority schedules match')
    else:
        args.output.write_text(json.dumps(expected) + '\n', encoding='utf-8', newline='\n')
