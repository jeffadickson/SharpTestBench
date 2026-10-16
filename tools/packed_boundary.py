"""ABI boundary checks; invoked by native_faults.py against an isolated fake GPI."""
import ctypes as c
import os
import sys

lib = c.CDLL(sys.argv[1])
lib.cs_bind_gpi.restype = c.c_char_p
assert lib.cs_bind_gpi() is None
read = lib.cs_v1_read_planes
write = lib.cs_v1_write_planes
read.argtypes = [c.c_void_p, c.c_uint32, c.c_void_p, c.c_uint32]
write.argtypes = read.argtypes + [c.c_int32]
# Guard bytes surround exactly six output bytes for a nine-bit vector.
guarded = (c.c_ubyte * 8)(*([0xAA] * 8))
p = c.cast(c.byref(guarded, 1), c.c_void_p)
checks = 0

def check(actual, expected):
    global checks
    assert actual == expected, (actual, expected)
    checks += 1

check(read(1, 9, p, 6), 0)
check(list(guarded), [0xAA, 9, 1, 0x66, 0, 0x22, 0, 0xAA])
check(read(1, 9, None, 6), 1)
check(read(None, 9, p, 6), 1)
check(read(1, 0, p, 0), 1)
check(read(1, 1048577, p, 6), 1)
check(read(1, 9, p, 5), 1)
check(read(1, 8, p, 3), 2)
check(write(1, 9, p, 6, 0), 0)
check(write(1, 9, p, 6, 99), 1)
check(write(1, 8, p, 3, 0), 2)
guarded[2] = 0x80
check(write(1, 9, p, 6, 0), 1)  # Nonzero unused bits.
guarded[2] = 1
guarded[1] |= 0x2
check(write(1, 9, p, 6, 0), 1)  # Value overlaps unknown plane.
guarded[1] = 9
guarded[5] |= 0x80
check(write(1, 9, p, 6, 0), 1)  # Z must be a subset of unknown.
guarded[5] = 0x22
os.environ['CS_FAKE_CONSTANT'] = '1'
check(write(1, 9, p, 6, 0), 6)
del os.environ['CS_FAKE_CONSTANT']
os.environ['CS_FAKE_LOGIC'] = 'UX01ZWLH-'
check(read(1, 9, p, 6), 3)
os.environ['CS_FAKE_LOGIC'] = '101'
check(read(1, 9, p, 6), 5)
check((guarded[0], guarded[7]), (0xAA, 0xAA))
# Length errors must retain precedence even when unsupported symbols occur first.
for text in ['U01', 'U' + '0' * 9]:
    os.environ['CS_FAKE_LOGIC'] = text
    check(read(1, 9, p, 6), 5)
# Exercise SIMD full chunks, tails, stack/heap boundary and mixed 01XZ symbols.
for width in [1, 7, 8, 127, 128, 129, 4096, 4097, 8193]:
    os.environ['CS_FAKE_WIDTH'] = str(width)
    text = ('01xZ' * ((width + 3) // 4))[:width]
    os.environ['CS_FAKE_LOGIC'] = text
    n = (width + 7) // 8
    storage = (c.c_ubyte * (3*n + 2))(*([0xAA] * (3*n + 2)))
    address = c.cast(c.byref(storage, 1), c.c_void_p)
    check(read(1, width, address, 3*n), 0)
    expected = [0] * (3*n)
    for i, symbol in enumerate(reversed(text)):
        if symbol == '1': expected[i//8] |= 1 << (i%8)
        if symbol in 'xZ': expected[n+i//8] |= 1 << (i%8)
        if symbol == 'Z': expected[2*n+i//8] |= 1 << (i%8)
    check(list(storage), [0xAA] + expected + [0xAA])
    check(write(1, width, address, 3*n, 0), 0)
print(f'PASS packed-boundary: {checks} checks')
