# HDL ranges and value arrays

`HdlRange` represents inclusive HDL bounds and their declared direction.
`ValueArray<T>` stores a fixed number of mutable values addressed by those indices.
These are managed values; they neither read nor write simulator handles.

```csharp
var samples = new ValueArray<int>([10, 20, 10, 30], HdlRange.DownTo(1, -2));
samples.SetSlice(HdlRange.DownTo(0, -1), [40, 50]);
if (samples.TryIndexOf(50, out int index))
    Console.WriteLine(index); // -1 is a valid HDL index
samples.Range = HdlRange.To(10, 13); // relabel; values retain their order
int first = samples[10];             // 10
int occurrences = samples.CountOf(40); // 1
```

## Range contract

Left and Right retain inclusive bounds, including on null ranges. Ascending is
true for `to`, false for `downto`. The two-argument constructor infers direction;
To/DownTo specify it explicitly. IsNull identifies bounds inconsistent with the
declared direction. Contains tests membership; Offset maps an HDL index to its
zero-based position; Index maps a position to an HDL index. Enumeration runs from
Left toward Right and safely stops at either Int32 boundary.

Bounds are Int32, unlike Python's arbitrary-precision integers. Length throws
OverflowException above Int32.MaxValue, as does an unrepresentable Offset.
Index requires a representable Length. Contains and lazy enumeration still work
on larger spans. Invalid indices/positions throw IndexOutOfRangeException;
negative positional indexing is not supported. Use LINQ Reverse for reverse
iteration. Range equality includes bounds and direction: two empty ranges, or
two singleton ranges with different directions, may be unequal even though their
sequences match. Use SequenceEqual for sequence equality.

Python Range.from_range/to_range have no direct System.Range conversion:
System.Range is half-open, can use from-end indices, and cannot express a descending
or negative-index HDL range. Construct HdlRange explicitly and enumerate its
indices instead. The optional Python Range.index positional search window is not
an Offset overload; combine Contains/Offset with explicit positional bounds.
Python slicing/copy/repr and rich equality protocols are not emulated.

## Value array contract

Construction copies its input and requires an explicit range of matching length.
The Range setter changes only the indexing scheme and rejects length changes
before assigning it. ByOffset is positional; the indexer is HDL-indexed.
Enumeration follows stored order. Slice creates an independent shallow copy.

SetSlice accepts inclusive endpoints in the array's direction. It validates bounds,
snapshots the replacement (allowing self/overlapping sources), then validates its
length before any writes. Source enumeration failures and length/bounds errors
cause no writes by SetSlice. An enumerable's own side effects cannot be rolled
back. Like ordinary mutable .NET collections, these arrays are not thread-safe.

TryIndexOf returns a boolean plus the matching HDL index, so neither -1 nor 0 is
reserved as a missing-value sentinel. An optional searchRange restricts the
inclusive HDL search region. CountOf returns occurrence counts. Both default to
EqualityComparer<T>.Default and accept a custom comparer. Nulls are legitimate
values for nullable element types. Missing search results yield false/default,
where Python AbstractArray.index throws ValueError.

Explicit null slices are retained as a SharpTestBench extension: same-direction
null bounds need not be inside the array, return an empty copy, search no values,
and accept only an empty replacement. Pinned Python Array slicing rejects such
reversed endpoints. Direction mismatches always fail, including null slices.

ValueArray is sealed; AbstractArray/AbstractMutableArray external storage hooks
are intentionally unsupported. Bound metadata is accessed through Range.Left,
Range.Right and Range.Ascending rather than forwarding aliases. Python's optional
inferred constructor bounds, list/tuple equality, deep-copy and repr protocols
are not duplicated. .NET reference equality applies to ValueArray; use
SequenceEqual and an explicit comparer for value equality. Python indexing
migration warnings and LogicArray's specialized operations are separate audits.

## Evidence

Issue [#47](https://github.com/jeffadickson/SharpTestBench/issues/47) tracks this work.
`tools/SharpTestBench.Tools/OracleFixturesCommand.cs` verifies hashes of the installed pinned cocotb range,
abstract-array and array modules before generating/checking 98 reference cases.
`ArrayTests.PinnedRangeAndArrayReference` compares those results with C# ranges and
arrays. Additional tests cover aliasing, throwing enumerables, transactional
failures, null values, custom comparers, negative indices and Int32 boundaries.
CI regenerates the oracle in check mode and runs the managed tests on all three
supported operating systems. This validates the specified operations, not every
Python object protocol or simulator object-model behavior.
