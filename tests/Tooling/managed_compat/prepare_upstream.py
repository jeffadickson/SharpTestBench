"""Test-only binding to the managed prepare_upstream implementation."""
from managed_rpc import call, ROOT
def configure_native_build(*args,**kwargs):
 result=call('prepare_upstream','configure_native_build',args,kwargs)
 return result
