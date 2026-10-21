using SharpTestBench.Tools;

try
{
    if (args.Length == 0 || args[0] is "--help" or "-h")
    {
        Console.WriteLine("SharpTestBench managed helpers: array-reference, audit-reviews, audit-surface, axi-python, axi-workload, axis-ci, priority-ci, benchmark, runner-benchmark, ci, cleanup-tests, clock-boundary, code-coverage, coverage-check, cpu-python, cpu-workload, debugger-tests, doctor, documentation, dsp-python, dsp-workload, dual-runner-ci, dual-runner-manifest, dual-runner-parity, icarus-layouts, icarus-layouts-check, installed-run, lfsr-ci, lifecycle, logging-check, logic-reference, managed-tests, native-build, native-build-check, native-debugger-tests, native-faults, native-package, native-performance, native-performance-prepare, package, package-container, packed-benchmark, publish-workers, verify-workers, packed-boundary, parity, prepare-upstream, queue-reference, release-bundle, release-preflight, run, runner-manifest-check, stage-tests, structured-reference, structured-reference-env, timing-parity, typecheck, wave-check, wave-tests, workspace-tests");
        return 0;
    }
    if (args[0] == "installed-run") { InstalledRunCommand.Execute(args[1..]); return 0; }
    if (args[0] == "test-api") { TestApiCommand.Execute(); return 0; }
    var context = new ToolContext(ToolContext.FindRoot());
    switch (args[0])
    {
        case "prepare-upstream": PrepareUpstreamCommand.Execute(context, args[1..]); break;
        case "logging-check" or "coverage-check" or "wave-check" or "typecheck" or "documentation": ValidationCommands.Execute(context, args[0], args[1..]); break;
        case "release-bundle" or "release-preflight": ReleaseCommands.Execute(args[0], args[1..]); break;
        case "dual-runner-parity": ParityContracts.Execute(args[1..]); break;
        case "publish-workers": WorkerPublishCommand.Execute(context, args[1..]); break;
        case "verify-workers": WorkerPublishCommand.Verify(context, args[1..]); break;
        case "native-build": if (args.Length != 1) throw new ArgumentException("native-build accepts no arguments"); Console.WriteLine(NativeBuildCommand.Build(context)); break;
        case "dual-runner-manifest": RunnerManifestCommand.Execute(context, args[1..]); break;
        case "native-build-check": NativeChecks.BuildCheck(context); break;
        case "windows-arm64-check": NativeChecks.Arm64(context); break;
        case "doctor": NativeChecks.Doctor(context); break;
        case "run": LegacyRunCommand.Execute(context, args[1..]); break;
        case "dual-runner-ci" or "lfsr-ci" or "axis-ci" or "priority-ci": ParityCiCommand.Execute(context, args[0], args[1..]); break;
        case "icarus-layouts" or "icarus-layouts-check": IcarusLayoutsCommand.Execute(context, args[0], args[1..]); break;
        case "cleanup-tests" or "lifecycle" or "wave-tests": LifecycleCommands.Execute(context, args[0], args[1..]); break;
        case "debugger-tests": DebuggerCommand.Execute(context, args[1..]); break;
        case "native-debugger-tests": NativeDebuggerCommand.Execute(context, args[1..]); break;
        case "stage-tests": StageCommand.Execute(context, args[1..]); break;
        case "workspace-tests": WorkspaceCommand.Execute(context, args[1..]); break;
        case "packed-boundary" or "clock-boundary": BoundaryCommands.Execute(args[0], args[1..]); break;
        case "native-faults": NativeFaultsCommand.Execute(context, args[1..]); break;
        case "native-package" or "package": PackageCommand.Execute(context, args[1..]); break;
        case "native-performance-prepare": PerformancePrepareCommand.Execute(context, args[1..]); break;
        case "packed-benchmark": PackedBenchmarkCommand.Execute(context, args[1..]); break;
        case "axi-python" or "dsp-python" or "cpu-python" or "structured-reference-env" or "structured-reference" or "parity" or "timing-parity": ReferenceCommand.Execute(context, args[0], args[1..]); break;
        case "axi-workload" or "dsp-workload" or "cpu-workload": WorkloadCommand.Execute(context, args[0], args[1..]); break;
        case "audit-reviews" or "audit-surface": AuditCommand.Execute(context, args[0], args[1..]); break;
        case "array-reference" or "logic-reference" or "queue-reference": OracleFixturesCommand.Execute(context, args[0], args[1..]); break;
        case "benchmark": BenchmarkCommand.Execute(context, args[1..]); break;
        case "runner-benchmark": RunnerBenchmarkCommand.Execute(context, args[1..]); break;
        case "native-performance": NativePerformanceCommand.Execute(context, args[1..]); break;
        case "package-container": PackageContainerCommand.Execute(context, args[1..]); break;
        case "runner-manifest-check": ManifestCheckCommand.Execute(context, args[1..]); break;
        case "ci": CiCommands.Execute(context, args[1..]); break;
        case "code-coverage": CoverageCommand.Execute(context, args[1..]); break;
        case "managed-tests": ManagedTestsCommand.Execute(context, args[1..]); break;
        default: throw new ArgumentException("Unknown helper: " + args[0]);
    }
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error.GetType().Name + ": " + error.Message);
    return 1;
}
