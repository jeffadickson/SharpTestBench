"""Deterministic native clock ownership tests; no wall-clock performance assertions."""
import ctypes as c
import os
from pathlib import Path
import sys

host = c.CDLL(sys.argv[1])
fake = c.CDLL(str(Path(sys.argv[1]).parent / 'libgpi.so'))
host.cs_bind_gpi.restype = c.c_char_p
assert host.cs_bind_gpi() is None
callback = c.CFUNCTYPE(c.c_int, c.c_void_p)
host.cs_v1_clock_start.argtypes = [c.c_void_p, c.c_uint64, c.c_uint64, callback, c.c_void_p, c.POINTER(c.c_int)]
host.cs_v1_clock_start.restype = c.c_void_p
host.cs_v1_clock_stop.argtypes = [c.c_void_p]
host.cs_v1_clock_update.argtypes = [c.c_void_p, c.c_uint64, c.c_uint64]
fake.stb_test_delay.restype = c.c_uint64
fake.stb_test_write_hook.argtypes = [callback]
os.environ.update(CS_FAKE_CLOCK='1', CS_FAKE_WIDTH='1')
checks = 0
owned = None
failures = 0
callback_results = []

def check(actual, expected):
    """Assert a state transition and retain a stable evidence count."""
    global checks
    assert actual == expected, (actual, expected)
    checks += 1

@callback
def failed(_):
    """Model managed cleanup occurring reentrantly inside a failing native timer."""
    global owned, failures
    failures += 1
    callback_results.append(host.cs_v1_clock_stop(owned))
    owned = None
    return 0

@callback
def cancel_on_write(_):
    """Cancel during the setter, when the native callback's stack still owns the clock."""
    global owned
    callback_results.append(host.cs_v1_clock_stop(owned))
    owned = None
    return 0

def start(high=4, low=5, expected=0):
    """Start one clock and check explicit status plus handle ownership."""
    global owned
    status = c.c_int(-1)
    owned = host.cs_v1_clock_start(1, high, low, failed, None, c.byref(status))
    check(status.value, expected)
    check(bool(owned), expected == 0)

start(0, 5, 1)
os.environ['CS_FAKE_WIDTH'] = '9'; start(expected=2)
os.environ['CS_FAKE_WIDTH'] = '1'; os.environ['CS_FAKE_CONSTANT'] = '1'; start(expected=6)
del os.environ['CS_FAKE_CONSTANT']
os.environ['CS_FAKE_CLOCK_FAIL_AT'] = '1'; start(expected=8)
del os.environ['CS_FAKE_CLOCK_FAIL_AT']; fake.stb_test_reset()
start(); check(fake.stb_test_delay(), 5)
check(host.cs_v1_clock_update(owned, 2, 5), 0)
fake.stb_test_fire(1); check(fake.stb_test_delay(), 4); check(fake.stb_test_pending(), 2)
fake.stb_test_fire(0); check(fake.stb_test_value(), 1)
fake.stb_test_fire(1); check(fake.stb_test_delay(), 5)
fake.stb_test_fire(0); check(fake.stb_test_value(), 0)
fake.stb_test_fire(1); check(fake.stb_test_delay(), 2)
# Cancellation between timer and ReadWrite must suppress the queued edge.
check(host.cs_v1_clock_stop(owned), 0); owned = None
check(fake.stb_test_pending(), 0); check(fake.stb_test_writes(), 2)
for variable, value in [('CS_FAKE_CLOCK_FAIL_RW', '1'), ('CS_FAKE_CLOCK_FAIL_AT', '2')]:
    fake.stb_test_reset(); os.environ[variable] = value; start()
    fake.stb_test_fire(1); check(owned, None); check(fake.stb_test_pending(), 0)
    del os.environ[variable]
check(failures, 2)
fake.stb_test_reset(); start(); fake.stb_test_write_hook(cancel_on_write)
fake.stb_test_fire(1); fake.stb_test_fire(0)
check(owned, None); check(fake.stb_test_pending(), 0)
# A failed removal must retain storage for a harmless late callback and safe retry.
fake.stb_test_reset(); start(); os.environ['CS_FAKE_CLOCK_REMOVE_FAIL'] = '1'
check(host.cs_v1_clock_stop(owned), 8)
fake.stb_test_fire(1); check(fake.stb_test_writes(), 0)
del os.environ['CS_FAKE_CLOCK_REMOVE_FAIL']
check(host.cs_v1_clock_stop(owned), 0); owned = None
check(fake.stb_test_pending(), 0)
for result in callback_results: check(result, 0)
print(f'PASS native-clock-boundary: {checks} checks')
