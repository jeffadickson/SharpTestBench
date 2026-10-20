"""Test-only binding to the managed windows_arm64_check implementation."""
from managed_rpc import call, ROOT
def pe_machine(*args,**kwargs):
 result=call('windows_arm64_check','pe_machine',args,kwargs)
 return result
