"""Test-only binding to the managed lfsr_fixture implementation."""
from managed_rpc import call, ROOT
def config(*args,**kwargs):
 result=call('lfsr_fixture','config',args,kwargs)
 return result
def validate_config(*args,**kwargs):
 result=call('lfsr_fixture','validate_config',args,kwargs)
 return result
def fixture(*args,**kwargs):
 result=call('lfsr_fixture','fixture',args,kwargs)
 return tuple(result)
def transition(*args,**kwargs):
 result=call('lfsr_fixture','transition',args,kwargs)
 return tuple(result)
def reverse_bits(*args,**kwargs):
 result=call('lfsr_fixture','reverse_bits',args,kwargs)
 return result
def plan(*args,**kwargs):
 result=call('lfsr_fixture','plan',args,kwargs)
 return result
def validate_plan(*args,**kwargs):
 result=call('lfsr_fixture','validate_plan',args,kwargs)
 return args[0]
def metrics(*args,**kwargs):
 result=call('lfsr_fixture','metrics',args,kwargs)
 return result
def verify_sources(*args,**kwargs):
 result=call('lfsr_fixture','verify_sources',args,kwargs)
 return result
def plan_digest(*args,**kwargs):
 result=call('lfsr_fixture','plan_digest',args,kwargs)
 return result
_constants=call('lfsr_fixture','constants')
CASES={name:tuple(values) for name,values in _constants['CASES'].items()}
STYLES=tuple(_constants['STYLES'])
