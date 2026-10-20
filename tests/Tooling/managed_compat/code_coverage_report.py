"""Test-only binding to the managed code_coverage_report implementation."""
from managed_rpc import call, ROOT
MAXIMUM_BYTES=16*1024*1024
MAX_BYTES=MAXIMUM_BYTES
def validate(*args,**kwargs):
 result=call('code_coverage_report','validate',args,kwargs, maximum=MAX_BYTES)
 return result
