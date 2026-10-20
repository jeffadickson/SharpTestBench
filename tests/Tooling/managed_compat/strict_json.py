"""Test-only binding to the managed strict_json implementation."""
from managed_rpc import call, ROOT
MAXIMUM_BYTES=16*1024*1024
MAX_BYTES=MAXIMUM_BYTES
def loads(*args,**kwargs):
 result=call('strict_json','loads',args,kwargs, maximum=MAXIMUM_BYTES)
 return result
def load(*args,**kwargs):
 result=call('strict_json','load',args,kwargs, maximum=MAXIMUM_BYTES)
 return result
