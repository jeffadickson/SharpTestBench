"""Test-only binding to the managed axis_fixture implementation."""
from managed_rpc import call, ROOT
def integer(*args,**kwargs):
 result=call('axis_fixture','integer',args,kwargs)
 return result
def plan(*args,**kwargs):
 result=call('axis_fixture','plan',args,kwargs)
 return result
def validate_plan(*args,**kwargs):
 result=call('axis_fixture','validate_plan',args,kwargs)
 return args[0]
def configuration(*args,**kwargs):
 result=call('axis_fixture','configuration',args,kwargs)
 return result
def project(*args,**kwargs):
 result=call('axis_fixture','project',args,kwargs)
 return result
def metrics(*args,**kwargs):
 result=call('axis_fixture','metrics',args,kwargs)
 return result
def verify_sources(*args,**kwargs):
 result=call('axis_fixture','verify_sources',args,kwargs)
 return result
