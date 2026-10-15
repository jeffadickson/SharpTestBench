# Logic-array transforms and collection operations

LogicArray is immutable: operators and WithElement/WithSlice return new values.
They do not drive a simulator signal; write the returned value through the signal
API when a hardware update is intended.

```csharp
var samples = new LogicArray("01XZLH", HdlRange.DownTo(1, -4));
var patched = samples.WithSlice(HdlRange.DownTo(0, -1), new LogicArray("10"));
// patched = 010ZLH; samples remains 01XZLH
var inverted = ~samples; // 10XX10, same declared bounds
var masked = samples & new LogicArray("000000");
// 000000; all nine-state truth-table rules apply, not integer coercion
if (samples.TryIndexOf(Logic.X, out int hdlIndex))
    Console.WriteLine(hdlIndex); // -1 is a successful HDL index
int weakHighCount = samples.CountOf(Logic.H);
```

## Operators and bounds

AND (`&`), OR (`|`), XOR (`^`) and NOT (`~`) apply the existing nine-state
truth tables element by element. They do not first resolve unknown values to
integers. Binary operands must have equal lengths; labels and directions may
differ. Pairing is by displayed position (leftmost with leftmost), not by equal
HDL index. Results preserve the left operand's declared range; NOT preserves its
operand's range. Empty operands are supported. Null operands throw
ArgumentNullException and length mismatches throw ArgumentException.

Pinned cocotb gives these operators new default descending bounds. SharpTestBench
intentionally preserves the left declaration; the reference tests verify both
Python's normalized metadata and C#'s preserved metadata while comparing all
result symbols. Thus `a & b` and `b & a` have the same symbol sequence but can
carry different bounds. Use WithRange when a different result label is wanted.

## Immutable replacement

WithElement replaces one HDL-indexed state. WithSlice replaces inclusive bounds
in the original's direction, consuming valid Logic values in displayed order.
Replacement labels have no effect; only sequence order and length matter.
The full replacement is evaluated and validated before the result is created.
Self, overlapping slices and lazy enumerables are supported. Invalid states,
length, direction, bounds or enumeration failures cannot mutate the original.

Slice and WithSlice retain the existing explicit null-slice extension: same-
direction null ranges may have out-of-bounds endpoints and designate zero values.
Only an empty replacement fits. A direction mismatch always fails. Pinned Python
rejects reversed slice endpoints rather than representing them as empty slices.

## Collection access and equality

IReadOnlyList<Logic> exposes Count and the existing zero-based positional indexer.
At(index) remains the explicit HDL-indexed accessor. Enumeration and LINQ Reverse
follow displayed order, regardless of HDL direction. There are no mutable list
interfaces. LINQ SequenceEqual provides exact state-sequence equality.

TryIndexOf returns the first exact state in declaration order, with an optional
inclusive HDL searchRange. It returns a boolean plus an HDL index; zero and -1
remain valid indices. CountOf counts exact state matches. L is distinct from 0,
and H from 1; neither search nor counting performs weak resolution. Invalid enum
values throw ArgumentOutOfRangeException. A missing search returns false/default,
where cocotb raises ValueError.

.NET reference equality for LogicArray is unchanged; there are no implicit
comparisons with integers, strings or lists. Use SequenceEqual for exact states,
explicit Resolve for state normalization, and ToUnsigned/ToSigned for numeric
comparison. ToString displays the nine-state symbols. Python-specific rich
formatting, implicit truth conversion, copy/deepcopy, repr, warning migration
and preview protocols are intentionally not duplicated. Numeric formatting can
be applied to the explicitly converted BigInteger; unresolved values still fail
numeric conversion. Immutability makes sharing a value safe without Python's
mutable slice/range setter semantics.

## Validation

Issue [#49](https://github.com/jeffadickson/SharpTestBench/issues/49) tracks this work.
The hash-verified logic oracle adds 81 two-operand cases (each containing all nine
states as well as the selected pair) and two directed collection cases with six
slice replacements. Tests compare bitwise results, unary inversion, search/count,
iteration, reverse iteration, extraction and replacement with pinned cocotb.
Separate tests cover empty ranges, mismatched widths, null operands, invalid enum
values, self/overlap replacement, throwing sources, negative HDL indices and
preservation of all original inputs. The class-level audit classification records
these contracts and deliberate differences rather than claiming Python protocol
or complete project parity.
