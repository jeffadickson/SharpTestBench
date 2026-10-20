"""Test-only binding to the managed icarus_layouts implementation."""
from managed_rpc import call, ROOT
def target_flags(*args,**kwargs):
 result=call('icarus_layouts','target_flags',args,kwargs)
 return result
