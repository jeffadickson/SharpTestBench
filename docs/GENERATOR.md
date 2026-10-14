# Incremental HDL bindings

SharpTestBench.Generators is a Roslyn incremental generator. It consumes explicit
`.stb.xml` AdditionalFiles at C# compile time; it does not parse Verilog/VHDL or
infer missing simulator metadata. An interface manifest must describe the actual
elaborated design. Generated Signal<W> construction checks the live width when
connected, so a stale manifest fails instead of silently truncating data.

```xml
<Bindings namespace="Generated" class="DutPorts">
  <Signal name="Data" width="8"><Child name="data" /></Signal>
  <Signal name="Memory2" width="32"><Child name="memory" /><Index value="2" /></Signal>
</Bindings>
```

Use a package reference to SharpTestBench.Generators and include the manifest:

```xml
<AdditionalFiles Include="Dut.stb.xml" />
```

Then construct `new Generated.DutPorts(ctx.Dut)`. Properties carry nested width
types such as `DutPorts.Width8`; write `UInt<DutPorts.Width8>` values. An incompatible
width is a C# compile error. Child and Index steps preserve hierarchy explicitly;
HDL names are escaped as C# literals, not interpreted as executable code.

STBGEN001 reports invalid XML, DTDs, unsupported schema/attributes, missing paths,
invalid identifiers, reserved Width names, duplicate properties and out-of-range
widths. Widths are 1..1048576; zero-width/null vectors need the runtime API. This
first manifest schema covers packed scalar/vector endpoints, including endpoints
inside arrays and records; it does not generate whole aggregate/enum/string APIs
or infer signedness. Existing runtime Bindings.Generate remains available.

Each manifest is an independent incremental input. Output ordering is ordinal and
line endings are fixed. Tests prove unchanged input caching, changed-width
invalidation, deterministic output, positive consumer compilation and negative
width/schema compilation. Duplicate namespace/class declarations across manifests
are invalid; a diagnostic is emitted. No simulator or filesystem probing occurs
inside the compiler.

The counter example uses the generator. CI builds it from project references and
from NuGet packages outside the checkout, then executes it on a real simulator.
