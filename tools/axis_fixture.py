"""Independent stimulus and evidence contracts for pinned AXI Stream RTL."""
import hashlib
from pathlib import Path
import random
from strict_json import load

def integer(value, low, high, name):
    if type(value) is not int or not low <= value <= high:
        raise ValueError('Invalid ' + name)
    return value

def plan(seed, count=64, stall=3, reset_at=None):
    integer(seed, 0, 2**64-1, 'seed')
    integer(count, 1, 4096, 'count')
    integer(stall, 0, 32, 'stall')
    if reset_at is not None: integer(reset_at, 1, count-1, 'reset_at')
    rng = random.Random(seed)
    rows = [{'Reset': True, 'Ready': False, 'Offer': False, 'Data': 0, 'Keep': 0,
             'Last': False, 'Id': 0, 'Dest': 0, 'User': 0}]
    for index in range(count):
        rows.append({'Reset': index == reset_at, 'Ready': index % (stall+1) == stall,
                     'Offer': True, 'Data': rng.randrange(65536), 'Keep': rng.randrange(4),
                     'Last': index % 7 == 6, 'Id': index % 16, 'Dest': (index*3) % 16,
                     'User': rng.randrange(4)})
    rows.extend(dict(rows[0], Reset=False, Ready=True) for _ in range(12))
    return rows

def validate_plan(rows):
    if not isinstance(rows, list) or not 3 <= len(rows) <= 4110:
        raise ValueError('Invalid stream plan length')
    fields = {'Reset','Ready','Offer','Data','Keep','Last','Id','Dest','User'}
    for row in rows:
        if not isinstance(row, dict) or set(row) != fields: raise ValueError('Invalid stream fields')
        for key in ('Reset','Ready','Offer','Last'):
            if type(row[key]) is not bool: raise ValueError('Invalid boolean: '+key)
        for key, maximum in (('Data',65535),('Keep',3),('Id',15),('Dest',15),('User',3)):
            integer(row[key],0,maximum,key)
    if not rows[0]['Reset']: raise ValueError('Stream must start in reset')
    if any(row['Reset'] or row['Offer'] or not row['Ready'] for row in rows[-12:]):
        raise ValueError('Stream must drain for twelve cycles')
    if not any(row['Offer'] and not row['Reset'] for row in rows): raise ValueError('Empty stream')
    return rows

def verify_sources(root):
    folder = Path(root)/'tests/Fixtures/verilog-axis'
    meta = load(folder/'provenance.json')
    if meta['license'] != 'MIT' or len(meta['revision']) != 40: raise ValueError('Invalid provenance')
    for name, item in meta['files'].items():
        if name not in ('axis_register.v','COPYING'): raise ValueError('Unexpected upstream file')
        if hashlib.sha256((folder/name).read_bytes()).hexdigest() != item['sha256']:
            raise ValueError('Upstream fixture changed: '+name)
    if set(meta['files']) != {'axis_register.v','COPYING'}: raise ValueError('Incomplete fixture')
    return meta
