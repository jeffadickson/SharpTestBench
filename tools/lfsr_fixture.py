"""Bit-serial reference independent of the upstream parallel XOR-mask RTL."""
import random
from axis_fixture import integer

def config(width=32, polynomial=0x04c11db7, data_width=8, topology='GALOIS', reverse=True, feed_forward=False):
    integer(width,1,64,'width'); integer(data_width,1,64,'data width')
    integer(polynomial,1,(1<<width)-1,'polynomial')
    if not polynomial&1: raise ValueError('Polynomial must have a constant term')
    if topology not in ('GALOIS','FIBONACCI'): raise ValueError('Invalid topology')
    if type(reverse) is not bool or type(feed_forward) is not bool: raise ValueError('Invalid boolean setting')
    return dict(Width=width,Polynomial=polynomial,DataWidth=data_width,Topology=topology,
                Reverse=reverse,FeedForward=feed_forward)

def validate_config(value):
    fields={'Width','Polynomial','DataWidth','Topology','Reverse','FeedForward'}
    if not isinstance(value,dict) or set(value)!=fields: raise ValueError('Invalid configuration fields')
    return config(value['Width'],value['Polynomial'],value['DataWidth'],value['Topology'],value['Reverse'],value['FeedForward'])

def reverse_bits(value,width):
    integer(width,1,64,'width'); integer(value,0,(1<<width)-1,'value')
    return int(format(value,'0'+str(width)+'b')[::-1],2)

def transition(settings,state,data):
    c=validate_config(settings); w=c['Width']; dw=c['DataWidth']; mask=(1<<w)-1
    integer(state,0,mask,'state'); integer(data,0,(1<<dw)-1,'data')
    if c['Reverse']: state=reverse_bits(state,w); data=reverse_bits(data,dw)
    result=0
    for index in range(dw-1,-1,-1):
        bit=(data>>index)&1
        feedback=((state>>(w-1))&1)^bit
        if c['Topology']=='FIBONACCI': feedback^=(state&(c['Polynomial']>>1)).bit_count()&1
        result=(result<<1)|feedback
        injected=bit if c['FeedForward'] else feedback
        state=((state<<1)|injected)&mask
        if c['Topology']=='GALOIS' and injected: state^=c['Polynomial']&~1
    if c['Reverse']: state=reverse_bits(state,w); result=reverse_bits(result,dw)
    return state,result

def plan(settings,seed,count=64):
    c=validate_config(settings); integer(seed,0,2**64-1,'seed'); integer(count,1,4096,'count')
    w=c['Width'];dw=c['DataWidth']; rng=random.Random(seed)
    pairs=[(0,0),((1<<w)-1,0),(0,(1<<dw)-1),((1<<w)-1,(1<<dw)-1)]
    pairs += [(1<<bit,0) for bit in range(w)]+[(0,1<<bit) for bit in range(dw)]
    pairs += [(rng.getrandbits(w),rng.getrandbits(dw)) for _ in range(count)]
    vectors=[]
    for state,data in pairs:
        expected,output=transition(c,state,data)
        vectors.append(dict(State=state,Data=data,ExpectedState=expected,ExpectedData=output))
    return dict(Config=c,Seed=seed,Vectors=vectors)

CASES = {'crc32': (32, 79764919, 8, 'GALOIS', True, False), 'crc16': (16, 4129, 8, 'GALOIS', False, False), 'crc32c': (32, 517762881, 32, 'GALOIS', True, False), 'prbs7': (7, 65, 8, 'FIBONACCI', False, False), 'scrambler': (58, 549755813889, 64, 'FIBONACCI', True, False), 'descrambler': (58, 549755813889, 64, 'FIBONACCI', True, True), 'galois_ff': (16, 32773, 16, 'GALOIS', True, True), 'minimal': (1, 1, 1, 'FIBONACCI', False, False)}
STYLES = ('LOOP','REDUCTION')

def fixture(name,style):
    if not isinstance(name,str) or name not in CASES: raise ValueError('Unknown LFSR fixture')
    if style not in STYLES: raise ValueError('Unknown LFSR style')
    return 'lfsr_'+name+'_'+style.lower(),config(*CASES[name])

def validate_plan(value):
    if not isinstance(value,dict) or set(value)!={'Config','Seed','Vectors'}: raise ValueError('Invalid stimulus fields')
    c=validate_config(value['Config']);integer(value['Seed'],0,2**64-1,'seed')
    rows=value['Vectors'];w=c['Width'];dw=c['DataWidth']
    if not isinstance(rows,list) or not 5+w+dw <= len(rows) <= 4228: raise ValueError('Invalid vector count')
    prefix=[(0,0),((1<<w)-1,0),(0,(1<<dw)-1),((1<<w)-1,(1<<dw)-1)]
    prefix += [(1<<bit,0) for bit in range(w)]+[(0,1<<bit) for bit in range(dw)]
    for index,row in enumerate(rows):
        if not isinstance(row,dict) or set(row)!={'State','Data','ExpectedState','ExpectedData'}:
            raise ValueError('Invalid vector fields')
        for key,width in (('State',w),('ExpectedState',w),('Data',dw),('ExpectedData',dw)):
            integer(row[key],0,(1<<width)-1,key)
        if index<len(prefix) and (row['State'],row['Data'])!=prefix[index]: raise ValueError('Missing basis vectors')
        if transition(c,row['State'],row['Data'])!=(row['ExpectedState'],row['ExpectedData']):
            raise ValueError('Incorrect reference result')
    return value

def metrics(value,stimulus):
    validate_plan(stimulus)
    if not isinstance(value,dict) or set(value)!={'trace','vectors'}: raise ValueError('Invalid metric fields')
    rows=value['trace'];expected=stimulus['Vectors']
    integer(value['vectors'],1,4228,'vector count')
    if not isinstance(rows,list) or len(rows)!=len(expected) or value['vectors']!=len(rows):
        raise ValueError('Incomplete LFSR trace')
    for index,(row,item) in enumerate(zip(rows,expected)):
        if not isinstance(row,list) or len(row)!=5: raise ValueError('Invalid LFSR trace row')
        for word in row: integer(word,0,2**64-1,'trace word')
        if row!=[index,item['State'],item['Data'],item['ExpectedState'],item['ExpectedData']]:
            raise ValueError('LFSR trace does not match stimulus')
    return value

def verify_sources(root):
    import hashlib
    from pathlib import Path
    from strict_json import load
    folder=Path(root)/'tests/Fixtures/verilog-lfsr'
    if folder.is_symlink(): raise ValueError('Linked fixture directory')
    value=load(folder/'provenance.json')
    if (not isinstance(value,dict) or set(value)!={'repository','revision','license','files'} or
        value.get('repository')!='https://github.com/alexforencich/verilog-lfsr' or
        value.get('revision')!='c1f86d036745935f3812ec6ddc7821c70ef4c9de' or value.get('license')!='MIT' or
        not isinstance(value.get('files'),dict) or set(value['files'])!={'lfsr.v','COPYING'}):
        raise ValueError('Invalid upstream provenance')
    for name,item in value['files'].items():
        if not isinstance(item,dict) or set(item)!={'upstream','sha256'}: raise ValueError('Invalid source metadata')
        if item['upstream']!=('rtl/lfsr.v' if name=='lfsr.v' else 'COPYING'): raise ValueError('Invalid upstream path')
        digest=item['sha256'];path=folder/name
        if not isinstance(digest,str) or len(digest)!=64 or any(c not in '0123456789abcdef' for c in digest):
            raise ValueError('Invalid source digest')
        if path.is_symlink() or path.stat().st_size>1024*1024: raise ValueError('Invalid source file')
        if hashlib.sha256(path.read_bytes()).hexdigest()!=digest: raise ValueError('Upstream source changed')
    return value
