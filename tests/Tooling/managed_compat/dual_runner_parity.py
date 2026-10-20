"""Test-only binding to the managed dual_runner_parity implementation."""
from managed_rpc import call, ROOT
def signature(*args,**kwargs):
 result=call('dual_runner_parity','signature',args,kwargs)
 return [tuple(row) for row in result]
def expected_identities(*args,**kwargs):
 result=call('dual_runner_parity','expected_identities',args,kwargs)
 return set(result)
def compare(*args,**kwargs):
 result=call('dual_runner_parity','compare',args,kwargs)
 return result
def write_report(*args,**kwargs):
 result=call('dual_runner_parity','write_report',args,kwargs)
 return result
def main(*args,**kwargs):
 result=call('dual_runner_parity','main',args,kwargs)
 return result
