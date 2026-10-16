"""Packed group bounds and all-entry validation against the isolated fake GPI."""
import ctypes as c
import os
import sys
from pathlib import Path
lib = c.CDLL(sys.argv[1])
lib.cs_bind_gpi.restype = c.c_char_p
assert lib.cs_bind_gpi() is None
fake = c.CDLL(str(Path(sys.argv[1]).parent / 'libgpi.so'))
class Entry(c.Structure):
    _fields_ = [('signal', c.c_void_p), ('bits', c.c_uint32), ('offset', c.c_uint32)]
read, write = lib.cs_v1_read_plane_batch, lib.cs_v1_write_plane_batch
for fn in (read, write):
    fn.argtypes = [c.POINTER(Entry), c.c_uint32, c.c_void_p, c.c_uint32]
os.environ['CS_FAKE_WIDTH'] = '9'
os.environ['CS_FAKE_LOGIC'] = '100000001'
entries = (Entry * 2)(Entry(1, 9, 0), Entry(1, 9, 6))
buffer = (c.c_ubyte * 14)(*([0xAA] * 14))
p = c.cast(c.byref(buffer, 1), c.c_void_p)
checks = 0
def check(actual, expected):
    global checks
    assert actual == expected, (actual, expected)
    checks += 1
check(read(entries, 2, p, 12), 0)
check(list(buffer), [0xAA, 1, 1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0xAA])
before = fake.stb_test_writes()
buffer[8] = 0x80
check(write(entries, 2, p, 12), 1)
check(fake.stb_test_writes(), before)
buffer[8] = 1
check(write(entries, 2, p, 12), 0)
check(fake.stb_test_writes(), before + 2)
for fn in (read, write):
    check(fn(None, 0, None, 0), 0)
    check(fn(None, 1, p, 12), 1)
    check(fn(entries, 65537, p, 12), 1)
    check(fn(entries, 2, None, 12), 1)
    check(fn(entries, 2, p, 11), 1)
    check(fn(entries, 2, p, 67108865), 1)
    entries[1].offset = 0xFFFFFFFF
    check(fn(entries, 2, p, 12), 1)
    entries[1].offset = 6
    entries[1].bits = 8
    check(fn(entries, 2, p, 12), 2)
    entries[1].bits = 9
check(fake.stb_test_writes(), before + 2)
print(f'PASS group-boundary: {checks} checks')
