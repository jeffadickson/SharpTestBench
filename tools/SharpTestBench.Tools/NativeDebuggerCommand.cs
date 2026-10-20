namespace SharpTestBench.Tools;
internal sealed class DebuggerRunner(ICommandRunner inner, string executable, string debugger, string commands) : ICommandRunner
{
    public CommandResult? SimulatorResult { get; private set; }
    public CommandResult Run(IReadOnlyList<string> arguments, string directory, IReadOnlyDictionary<string, string?>? environment = null, bool capture = false, bool check = true, TimeSpan? timeout = null)
    {
        if (arguments.Count == 0 || arguments[0] != executable) return inner.Run(arguments, directory, environment, capture, check, timeout);
        var result = inner.Run([debugger, "--batch", "--return-child-result", "-x", commands, "--args", .. arguments], directory, environment, capture, check, timeout);
        SimulatorResult = result; return result;
    }
}
internal static class NativeDebuggerCommand
{
    public static void Execute(ToolContext context, string[] arguments)
    {
        if (arguments.Length != 0) throw new ArgumentException("native-debugger-tests accepts no arguments");
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Native debugger check requires Linux");
        var work = context.At("build/native-debugger-check"); Directory.CreateDirectory(work);
        var evidence = Path.Combine(work, "evidence.json"); StrictJson.Write(evidence, new { status = "started; validation incomplete" });
        var commands = Path.Combine(work, "commands.gdb");
        File.WriteAllText(commands, """
set pagination off
set confirm off
set breakpoint pending on
handle SIG34 nostop noprint pass
handle SIGPWR nostop noprint pass
break sharptestbench_initialize
commands
silent
printf "STB_NATIVE_START\n"
bt 4
continue
end
break cs_v1_gpi_finish
commands
silent
printf "STB_NATIVE_FINISH\n"
bt 4
continue
end
run
""" + "\n");
        var runner = new DebuggerRunner(context.Runner, Environment.GetEnvironmentVariable("CS_VVP") ?? "vvp", "gdb", commands);
        try
        {
            LegacyRunCommand.Execute(new ToolContext(context.Root, runner), ["--no-build", "--sim", "icarus", "--assembly", context.At("examples/Counter/bin/Release/net10.0/Counter.dll"), "--source", context.At("examples/Counter/counter.sv"), "--top", "counter", "--entry-type", "Entry, Counter", "--work-dir", work, "--label", "native-debugger"]);
            var result = runner.SimulatorResult ?? throw new InvalidDataException("Debugger did not run simulator"); var output = result.Output + result.Error;
            if (result.ExitCode != 0 || new[] { "STB_NATIVE_START", "STB_NATIVE_FINISH", "exited normally" }.Any(marker => !output.Contains(marker, StringComparison.Ordinal)) || output.Contains("fatal library error", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Native debugger qualification failed");
            StrictJson.Write(evidence, new { startup_breakpoint = true, finish_breakpoint = true, normal_exit = true, fatal_diagnostic = false, scope = "separate native debugger session; not simultaneous managed/native debugging" }); Console.WriteLine("NATIVE_DEBUGGER_PASS");
        }
        finally { if (runner.SimulatorResult is { } result) File.WriteAllText(Path.Combine(work, "driver.log"), result.Output + result.Error); }
    }
}
