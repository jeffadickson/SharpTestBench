# Local requirement contracts

This separate simulator assembly adds explicit behavior assertions without changing
Integration discovery or seed order. Current milestone50 covers edge selection,
owned subscriptions/borrowed tasks, deadlines, managed-clock lifecycle and job-local
identity/state. It is not full cocotb conformance or a native-clock implementation
qualification. The C# qualification command runs both Icarus orchestration paths:

    dotnet tools/SharpTestBench.Tools/bin/Release/net10.0/SharpTestBench.Tools.dll contracts-ci --through 50 --seed 83

The command derives effective per-case seeds from full discovery, verifies producer
inputs, selects exactly the requested numbered contracts and checks both runs.
Public simulation tests throw explicit failures instead of trusting agreement alone.
100 is the planned final milestone, not the current executed case count. See the
requirements100 development report for actual OS/backend runs and scope limits.
