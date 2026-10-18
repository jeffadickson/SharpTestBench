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
