SharpTestBench API documentation
========================================

Native bridge
-------------

This reference covers project-owned C++20 implementation and the public C ABI.
Upstream cocotb and simulator headers are excluded. Static implementation helpers
are included for maintainers; they are not supported public extension points.
The function comments describe ownership, callback lifetimes and error handling.

.. toctree::
   :maxdepth: 1

   native

C# XML documentation
--------------------

Every C# project emits an XML documentation file beside its assembly. The runtime
NuGet package carries that file for IntelliSense; the generator package includes
its XML next to the analyzer DLL. Triple-slash XML comments are the source of
truth. See ``docs/DOCUMENTATION.md`` for authoring and validation instructions.

The existing undocumented public library members are recorded in
``docs/api/csharp-undocumented.txt``. This is a tracked backlog, not a claim of
complete API documentation. The documentation check rejects new missing members.
