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
                     'Offer': index % 5 != 2, 'Data': rng.randrange(65536), 'Keep': rng.randrange(4),
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
    if (not isinstance(meta,dict) or set(meta)!={'repository','revision','license','files'} or
        meta.get('repository')!='https://github.com/alexforencich/verilog-axis' or
        meta.get('revision')!='48ff7a7e2ef782cf778d47910cf85835c64b1bce' or
        meta.get('license')!='MIT' or not isinstance(meta.get('files'),dict)):
        raise ValueError('Invalid provenance')
    for name, item in meta['files'].items():
        if name not in ('axis_register.v','COPYING'): raise ValueError('Unexpected upstream file')
        expected=('rtl/' if name.endswith('.v') else '')+name
        if not isinstance(item,dict) or set(item)!={'upstream','sha256'} or item.get('upstream')!=expected:
            raise ValueError('Invalid upstream file metadata')
        digest=item.get('sha256')
        if not isinstance(digest,str) or len(digest)!=64 or any(c not in '0123456789abcdef' for c in digest):
            raise ValueError('Invalid source digest')
        if (folder/name).is_symlink(): raise ValueError('Linked upstream fixture')
        if hashlib.sha256((folder/name).read_bytes()).hexdigest() != item['sha256']:
            raise ValueError('Upstream fixture changed: '+name)
    if set(meta['files']) != {'axis_register.v','COPYING'}: raise ValueError('Incomplete fixture')
    return meta

def configuration(mode, sidebands=True):
    integer(mode,0,2,'mode')
    if type(sidebands) is not bool: raise ValueError('Invalid sidebands')
    if not sidebands and mode != 2: raise ValueError('Disabled sidebands require skid mode')
    return 'axis_mode'+str(mode) if sidebands else 'axis_disabled'

def project(row, sidebands=True):
    if type(sidebands) is not bool: raise ValueError('Invalid sidebands')
    validate_plan([dict(plan(0)[0]),row]+plan(0)[-12:])
    return [row['Data'],row['Keep'] if sidebands else 3,
            int(row['Last']) if sidebands else 1,row['Id'] if sidebands else 0,
            row['Dest'] if sidebands else 0,row['User'] if sidebands else 0]

def metrics(data):
    names={'trace','accepted','consumed','flushed','stalls','resets','cycles'}
    if not isinstance(data,dict) or set(data)!=names: raise ValueError('Invalid stream metrics fields')
    for key in names-{'trace'}: integer(data[key],0,100000,key)
    trace=data['trace']
    if not isinstance(trace,list) or not 3 <= len(trace) <= 4110 or data['cycles']!=len(trace):
        raise ValueError('Invalid trace length')
    totals=dict(accepted=0,consumed=0,flushed=0,stalls=0,resets=0)
    backlog=0
    held=None
    for index,row in enumerate(trace):
        if not isinstance(row,list) or len(row)!=11: raise ValueError('Invalid trace row')
        for col,maximum in enumerate((4109,1,1,1,1,65535,3,1,15,15,3)):
            integer(row[col],0,maximum,'trace column '+str(col))
        cycle,reset,take,valid,ready,*word=row
        if cycle!=index: raise ValueError('Noncontiguous cycles')
        if index==0 and not reset: raise ValueError('Missing initial reset')
        if reset:
            if take or valid: raise ValueError('Reset transfers are forbidden')
            totals['flushed']+=backlog; backlog=0; held=None; totals['resets']+=1
        else:
            if held is not None and (not valid or word!=held): raise ValueError('Unstable stalled output')
            totals['accepted']+=take; backlog+=take
            if valid and ready and backlog==0: raise ValueError('Unsolicited output transfer')
            if valid and ready: totals['consumed']+=1; backlog-=1
            if valid and not ready: totals['stalls']+=1
            held=word if valid and not ready else None
        if not valid and any(word): raise ValueError('Invalid payload must be normalized')
    if backlog or held is not None or not totals['consumed']: raise ValueError('Incomplete stream')
    if any(data[key]!=value for key,value in totals.items()): raise ValueError('Trace counter mismatch')
    return data
