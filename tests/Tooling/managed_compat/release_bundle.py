"""Test-only binding to the managed release_bundle implementation."""
from managed_rpc import call, ROOT
def assemble(*args,**kwargs):
 result=call('release_bundle','assemble',args,kwargs)
 return result
RIDS=tuple(call('release_bundle','constants'))
