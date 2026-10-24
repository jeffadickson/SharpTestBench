# Bounded public-export decisions

Pinned literal __all__ exposure review: 30 decisions among 44 names in cocotb and cocotb.triggers; 14 remain unreviewed in these two modules. Other modules, dynamic exports and runtime import behavior remain open. Existing 302 definition decisions are a separate inventory. Counts are not parity percentages.

| Exposure | Classification | Bounded decision |
|---|---|---|
| cocotb.RANDOM_SEED | intentional-difference | TestContext.Seed is an unsigned per-case ordinal seed, not cocotb's mutable global Python PRNG seed/hash policy. |
| cocotb.SIM_NAME | intentional-difference | Runtime.Simulator exposes the native product on the simulator thread rather than a module global. |
| cocotb.SIM_VERSION | gap | A separate public simulator-version global/property is not mapped by this audit. |
| cocotb.Param | gap | The imported Python parametrization descriptor has no reviewed C# exposure; SimCase literals do not establish its options or descriptor semantics. |
| cocotb.__version__ | gap | No dedicated cocotb version-export API is mapped. Standard managed assembly metadata identifies SharpTestBench rather than the upstream Python export. |
| cocotb.argv | gap | Simulator command construction is not a public runtime argv-list exposure. |
| cocotb.create_task | gap | Deferred unscheduled Python task construction has no reviewed public C# equivalent; owned StartSoon schedules work. |
| cocotb.end_test | gap | Returning a managed body does not implement the imported immediate EndTest exception API or its expectation rules. |
| cocotb.is_simulation | gap | Active runtime tracking is internal; no public equivalent of the exported Python is_simulation boolean global is mapped. |
| cocotb.log | intentional-difference | Managed structured logging is not the module-level Python logging.Logger object or its handler hierarchy. |
| cocotb.packages | gap | Explicit HDL package discovery does not establish cocotb's populated SimpleNamespace global. |
| cocotb.parametrize | gap | Static SimCase argument lists do not implement the imported decorator's expansion/Param/keyword semantics. |
| cocotb.pass_test | deprecated-omission | Pinned source deprecates forcing pass in2.1. No C# force-pass escape is added; failure/isolation checks remain authoritative. |
| cocotb.plusargs | gap | Passing simulator arguments is not the public parsed bool-or-string plusargs dictionary contract. |
| cocotb.skipif | gap | Static SimTest.Skip is supported, but the imported conditional Python decorator is not mapped as an equivalent public operation. |
| cocotb.start | deprecated-omission | Pinned source deprecates start in favor of start_soon. No wrapper promising its coroutine-yield scheduling behavior is exposed. |
| cocotb.start_soon | intentional-difference | Owned C# jobs start through delegates and cancellation tokens, not already-created Python coroutine/Task objects; cleanup ownership is explicit. |
| cocotb.test | intentional-difference | SimTest metadata plus Regression.Initialize replace the decorator, without claiming all decorator parameters or Python module collection semantics. |
| cocotb.top | intentional-difference | TestContext.Dut is a typed native handle rooted per test/runtime rather than a mutable module-level SimHandle global. |
| cocotb.xfail | intentional-difference | ExpectedException metadata handles typed managed exceptions but is not the imported conditional/strict Python xfail decorator surface. |
| cocotb.triggers.ClockCycles | intentional-difference | Typed C# async clock-cycle helper and ClockEdge policy differ from the re-exported Python awaitable class identity and constructor options. |
| cocotb.triggers.Combine | intentional-difference | C# factory returns a composed Trigger for typed inputs; imported Python object/class and heterogeneous-awaitable exposure is not claimed. |
| cocotb.triggers.Edge | deprecated-omission | Pinned Edge is deprecated since2.0; C# exposes ValueChange rather than recreating the deprecated Python class alias. |
| cocotb.triggers.Event | intentional-difference | SimEvent is a typed resettable synchronization primitive; Python Event class identity/introspection and payload history are not implied. |
| cocotb.triggers.FallingEdge | intentional-difference | Typed C# factory binds a one-bit native signal; it does not recreate Python class/caching identities. |
| cocotb.triggers.First | intentional-difference | C# factory/owned-job overloads have typed borrowing and cleanup rules, not Python imported class/type identity. |
| cocotb.triggers.GPITrigger | internal-adapter | Native callback triggers are an internal C# adapter; public Trigger extension is distinct from Python GPITrigger inheritance and callback-handle API. |
| cocotb.triggers.Lock | intentional-difference | SimLock exposes managed acquisition/lease ownership, not the imported Python Lock object/context-manager/introspection interface. |
| cocotb.triggers.NextTimeStep | intentional-difference | C# factory maps the native next-step callback through a Trigger, without Python class identity/caching equivalence. |
| cocotb.triggers.NullTrigger | gap | No dedicated public immediately-firing Trigger factory/class is mapped; Task.CompletedTask alone does not establish the Python trigger scheduler contract. |
