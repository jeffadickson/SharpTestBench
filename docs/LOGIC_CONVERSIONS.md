# Nine-state resolution and numeric conversion

`Logic` models U, X, 0, 1, Z, W, L, H and don't-care (`-`). `LogicArray`
is an immutable displayed sequence; the leftmost element is the most significant
bit regardless of HDL range direction. `LogicVector<W>` remains the separate
fixed-width four-state type; the new APIs do not silently change its semantics.

```csharp
var raw = new LogicArray("10XH", HdlRange.To(4, 7));
// raw.ToUnsigned() throws: X has no unambiguous integer value.
var resolved = raw.Resolve(LogicResolution.Zeros);
BigInteger value = resolved.ToUnsigned(); // 9 (1001)
var signed = LogicArray.FromSigned(-2, HdlRange.DownTo(7, 0));
byte[] wire = signed.ToBytes(ByteOrder.BigEndian); // FE
var received = LogicArray.FromBytes(wire, ByteOrder.BigEndian);
BigInteger decoded = received.ToSigned(); // -2
```

## Explicit resolution

LogicOps.IsResolvable and LogicArray.IsResolvable recognize only 0, 1, L and H.
An empty vector is vacuously resolvable, but has no integer or byte representation.
ToBool, ToUnsigned, ToSigned and ToBytes accept L/H as 0/1 and reject all other
unknown states with InvalidOperationException. No environment variable or global
unknown-resolution policy changes these conversions.

Resolve accepts an explicit LogicResolution:

| Policy | L/H | U/X/Z/W/- |
|---|---|---|
| Weak | 0/1 | Retained, except W becomes X to match pinned cocotb's default behavior |
| Zeros | 0/1 | 0 |
| Ones | 0/1 | 1 |
| Random | 0/1 | One caller-generator Next(2) draw per unknown, left to right |

Random requires an explicitly supplied Random, even for a known or empty input.
Pass a seeded generator for repeatability; Python's PRNG sequence is not reproduced.
Resolution preserves range/direction and never mutates the original vector.
Invalid enum values fail. Cocotb's STRICT_RESOLVE preview, global resolver,
internal string-resolver cache and `error` identity transform are not emulated.
The existing .NET bool type serves binary-only values; there is no Python Bit
subclass or Python dynamic coercion/identity protocol. Scalar bitwise operations
remain available through NineState.And/Or/Xor/Not and their existing truth-table tests.

## Integers and bytes

FromUnsigned/FromSigned accept BigInteger and an explicit nonempty HdlRange.
The default rejects overflow. `wrap: true` retains low bits modulo 2^Length;
unsigned construction still rejects negative values. Signed conversion uses two's
complement. Construction overflow throws OverflowException; null numeric ranges
throw ArgumentException. Bounds retain the Int32 HdlRange policy.

Byte order is always explicit. ToBytes produces ceil(Length/8) bytes, retaining
leading zeros and padding unused high bits with zero. FromBytes requires a
nonempty input and exactly eight bits per byte if bounds are supplied; otherwise
it chooses descending bounds. A non-byte-aligned vector therefore does not retain
its exact width through a byte-only round trip; transport width metadata separately.
Changing ascending/descending index direction does not change significance or
byte order. WithRange produces an immutable same-length reindexed copy.

Deprecated Python binstr/integer/signed_integer/buff aliases are omitted. Use
ToString, ToUnsigned, ToSigned, ToBytes and the corresponding factories instead.
LogicArray.range mutation is represented by WithRange rather than a setter.
Python exception classes and implicit conversions are not reproduced.

## Evidence and upstream discrepancy

`tools/logic_reference.py` hash-verifies installed pinned _logic, _logic_array and
_resolve source, rejects configured global/strict resolution, and generates
9 scalar, 86 vector and 256 integer records. The managed tests compare resolution,
checked/wrapped construction, numeric values and byte ordering against these records.
They separately verify injected PRNG call counts, immutable inputs, empty values,
invalid policies and widths beyond 64 bits. CI checks fixture drift on all platforms.

Pinned cocotb unexpectedly accepts leading don't-care as a numeric sign in `-0`,
`-1`, `-L` and `-H`, because its integer parser accepts a leading minus after
mapping L/H. This can even make to_unsigned return -1; byte conversion then fails
with OverflowError. The reference fixture retains those results. SharpTestBench
explicitly rejects all four as unresolved; tests assert that safer contract rather
than reproducing the parser accident. This is an intentional semantic difference.

Issue [#48](https://github.com/jeffadickson/SharpTestBench/issues/48) tracks conversions.
The LogicArray class-level review remains a gap for vector bitwise transforms,
slice replacement and collection operations, tracked in
[#49](https://github.com/jeffadickson/SharpTestBench/issues/49). Numeric-method
coverage is not full LogicArray parity; Python equality/formatting protocols and
preview behavior are not claimed equivalent.
