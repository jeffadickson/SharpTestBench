"""Test-only binding to the managed dual_runner_ci implementation."""
from managed_rpc import call, ROOT
def clean_environment(*args,**kwargs):
 result=call('dual_runner_ci','clean_environment',args,kwargs)
 return result
def metrics_contract(*args,**kwargs):
 result=call('dual_runner_ci','metrics_contract',args,kwargs)
 return result
def fault_detected(*args,**kwargs):
 result=call('dual_runner_ci','fault_detected',args,kwargs)
 return result
def clear_case(*args,**kwargs):
 result=call('dual_runner_ci','clear_case',args,kwargs)
 return result
