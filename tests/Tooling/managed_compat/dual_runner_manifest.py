"""Test-only binding to the managed dual_runner_manifest implementation."""
from managed_rpc import call, ROOT
def make_manifest(*args,**kwargs):
 result=call('dual_runner_manifest','make_manifest',args,kwargs)
 return result
FIXTURES=call('dual_runner_manifest','constants')
