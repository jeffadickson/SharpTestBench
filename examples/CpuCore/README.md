# CPU retirement and memory workload

This example checks a small custom CPU fixture using independent C# and cocotb
scoreboards. It provides instruction execution, branches, external memory wait
states and enable pauses as an end-to-end verification workload. It is not a
RISC-V implementation or a production CPU benchmark: it has no pipeline, cache,
interrupts or exception machinery. Memory is provided by each testbench.

The core has eight 32-bit registers (`r0` is immutable), a word-indexed PC and a
256-word external memory address space. Instruction bits `[31:28]` are the opcode,
`[27:25]` the destination, `[24:22]` source A, `[21:19]` source B and `[15:0]` a
signed immediate. Other bits are reserved and zero in the supplied program.

| Opcode | Instruction | Effect |
|---|---|---|
| 1 | MOVI | Sign-extend immediate into destination |
| 2 | ADD | Add sources modulo 2^32 |
| 3 | XOR | XOR sources |
| 4 | LOAD | Read memory at `(source A + immediate) mod 256` |
| 5 | STORE | Write source B at that memory address |
| 6 | BNE | If sources differ, add immediate to current PC; otherwise increment PC |
| 15 | HALT | Retire, increment PC and halt |

Undefined opcodes are outside the test contract. Normally an instruction increments
PC by one. A low enable pauses execution. A load/store retires only when memory
ready is high; the register/PC state must hold during a wait. Retirement outputs
identify the completed register write. The test checks both the pre-edge memory
request and post-edge retirement, PC and halt on every cycle.

The supplied program writes `r0` to test protection, sets up a loop, then performs
stores, loads, wrapping addition and XOR checksums. It uses a negative immediate
to decrement the loop counter and exercises taken and untaken backward branches.
The first data addition wraps `0xffffffff` to zero. More than 256 loop iterations
also wrap the word address. Deterministic enable pauses and memory-ready stalls
are driven by an identical 32-bit LCG in both testbenches. An initial reset is
checked through the first request/retirement; mid-program reset is outside this
fixture's current scope.

The independent references use C# unsigned arithmetic and Python arbitrary-
precision arithmetic with explicit modulo. Both full cycle traces and all 256
final memory words must agree. The harness independently requires exactly
`9 * iterations + 6` retired instructions. Three HDL fault modes corrupt ADD,
change a taken branch target, or corrupt store data. Each must fail in both
languages with a scoreboard mismatch; an unrelated setup failure does not pass.

```sh
dotnet build examples/CpuCore -c Release
dotnet run --project tools/SharpTestBench.Tools -c Release -- cpu-workload --iterations 2048 --repeat 5
```

Provision the native host, Icarus and the pinned released-cocotb 2.1.0 environment
as for [DspFir](../DspFir/README.md). Each language receives one fresh-process
warmup, followed by alternating measured processes. Logs, complete traces, final
memory and a hashed summary are retained in `build/cpu-workload`. Body timing
includes drivers, scoreboards and trace collection; wall timing also includes
HDL compilation and startup. C# cumulative allocations and GC counts are recorded;
no cross-language memory-efficiency claim is made.

Linux, Windows and macOS CI run the 256-iteration variant and all six fault checks.
Verilator measurements, a production ISA core and richer memory systems remain
future benchmark work.

## Initial Linux measurement

Icarus 12, .NET 10.0.402, cocotb 2.1.0; 2,048 loop iterations, five alternating
measured processes after warmups. Median body time: C# **0.5183 s**, Python
**1.4887 s** (2.87x ratio). Median process wall time: C# **0.8048 s**, Python
**1.9254 s** (2.39x ratio). Every cycle trace and final memory image matched;
all six injected-fault checks passed. This is a workload-specific comparison,
not an incremental bridge improvement or a universal CPU benchmark claim.
Raw samples, source/binary hashes and the native build manifest are in
[the evidence file](../../docs/benchmarks/cpu-core-linux-20261016.json).
