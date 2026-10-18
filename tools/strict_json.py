"""Strict, bounded JSON reads for runner qualification inputs."""
import json
import math
from pathlib import Path

MAXIMUM_BYTES = 16 * 1024 * 1024


def loads(text):
    if not isinstance(text, str) or len(text.encode("utf-8")) > MAXIMUM_BYTES:
        raise ValueError("JSON must be text within the 16 MiB input limit")
    def pairs(items):
        result = {}
        for key, value in items:
            if key in result:
                raise ValueError("Duplicate JSON property: " + key)
            result[key] = value
        return result
    def invalid_constant(value):
        raise ValueError("Nonfinite JSON number: " + value)
    result = json.loads(text, object_pairs_hook=pairs, parse_constant=invalid_constant)
    pending = [(result, 0)]
    while pending:
        value, depth = pending.pop()
        if depth > 64: raise ValueError("JSON exceeds maximum nesting depth")
        if isinstance(value, float) and not math.isfinite(value): raise ValueError("Nonfinite JSON number")
        if isinstance(value, dict): pending.extend((item, depth + 1) for item in value.values())
        elif isinstance(value, list): pending.extend((item, depth + 1) for item in value)
        strings = list(value) if isinstance(value, dict) else [value] if isinstance(value, str) else []
        for text_value in strings:
            if any(0xD800 <= ord(char) <= 0xDFFF for char in text_value):
                raise ValueError("Invalid Unicode surrogate in JSON")
    return result


def load(path):
    with Path(path).open('rb') as stream:
        data = stream.read(MAXIMUM_BYTES + 1)
    if len(data) > MAXIMUM_BYTES: raise ValueError("JSON exceeds the 16 MiB input limit")
    return loads(data.decode('utf-8-sig'))
