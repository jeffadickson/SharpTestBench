using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
namespace SharpTestBench.Tools;
internal sealed class DapSession(Stream input, Stream output)
{
    int sequence;
    readonly List<JsonElement> pending = [];
    public List<JsonElement> Transcript { get; } = [];
    public static async Task<JsonElement> ReadMessage(Stream input, CancellationToken token)
    {
        var header = new List<byte>(); var one = new byte[1];
        while (true)
        {
            if (await input.ReadAsync(one, token) != 1) throw new EndOfStreamException("Debugger exited");
            header.Add(one[0]); if (header.Count > 8192) throw new InvalidDataException("DAP header exceeds limit");
            if (header.Count >= 4 && header[^4] == 13 && header[^3] == 10 && header[^2] == 13 && header[^1] == 10) break;
        }
        var lengths = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Where(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (lengths.Length != 1 || !int.TryParse(lengths[0].Split(':', 2)[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var length) || length < 1 || length > 16 * 1024 * 1024) throw new InvalidDataException("Invalid DAP content length");
        var bytes = new byte[length]; await input.ReadExactlyAsync(bytes, token);
        using var message = StrictJson.Parse(new UTF8Encoding(false, true).GetString(bytes));
        if (message.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("DAP message must be a JSON object");
        return message.RootElement.Clone();
    }
    public JsonElement Receive(Func<JsonElement, bool> predicate)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (true)
        {
            var index = pending.FindIndex(message => predicate(message));
            if (index >= 0) { var found = pending[index]; pending.RemoveAt(index); return found; }
            var next = ReadMessage(input, deadline.Token).GetAwaiter().GetResult(); Transcript.Add(next);
            if (predicate(next)) return next; pending.Add(next);
        }
    }
    public JsonElement Send(string command, object? arguments = null, bool wait = true)
    {
        var current = ++sequence; var bytes = JsonSerializer.SerializeToUtf8Bytes(new { seq = current, type = "request", command, arguments = arguments ?? new { } });
        output.Write(Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n")); output.Write(bytes); output.Flush();
        if (!wait) return default;
        var response = Receive(message => message.TryGetProperty("request_seq", out var number) && number.TryGetInt32(out var value) && value == current);
        if (!response.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True) throw new InvalidDataException("Debugger request failed: " + command);
        return response.TryGetProperty("body", out var body) ? body : default;
    }
}
internal static class DebuggerCommand
{
    static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    static Process Start(string executable, IEnumerable<string> arguments, string directory, Dictionary<string, string?>? environment = null, bool input = false)
    {
        var settings = new ProcessStartInfo(executable) { WorkingDirectory = directory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = input };
        foreach (var argument in arguments) settings.ArgumentList.Add(argument);
        if (environment is not null) foreach (var pair in environment) settings.Environment[pair.Key] = pair.Value;
        return Process.Start(settings) ?? throw new IOException("Unable to launch debugger process");
    }
    public static void Execute(ToolContext context, string[] arguments)
    {
        var options = new Options(arguments); options.Only("--debugger"); var executable = Path.GetFullPath(options.Require("--debugger"));
        var work = context.At("build/debugger-check"); Directory.CreateDirectory(work);
        StrictJson.Write(Path.Combine(work, "evidence.json"), new { status = "started; validation incomplete" });
        var pidFile = Path.Combine(work, "pid"); File.Delete(pidFile);
        using var simulator = Start("dotnet", [typeof(ToolContext).Assembly.Location, "run", "--no-build", "--sim", "icarus", "--assembly", context.At("tests/DebugIntegration/bin/Debug/net10.0/DebugIntegration.dll"), "--source", context.At("examples/Counter/counter.sv"), "--top", "counter", "--entry-type", "Entry, DebugIntegration", "--work-dir", work, "--label", "debugger"], context.Root, new() { ["STB_DEBUG_PID"] = pidFile, ["STB_DEBUG_PTRACE"] = "1" });
        var stdout = simulator.StandardOutput.ReadToEndAsync(); var stderr = simulator.StandardError.ReadToEndAsync();
        Process? debugger = null; Task<string>? debuggerError = null; DapSession? session = null;
        try
        {
            var timer = Stopwatch.StartNew();
            while (!File.Exists(pidFile)) { if (simulator.HasExited || timer.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("Simulator did not become attachable"); Thread.Sleep(50); }
            debugger = Start(executable, ["--interpreter=vscode"], context.Root, input: true); debuggerError = debugger.StandardError.ReadToEndAsync();
            session = new DapSession(debugger.StandardOutput.BaseStream, debugger.StandardInput.BaseStream);
            session.Send("initialize", new { clientID = "SharpTestBench", adapterID = "coreclr", linesStartAt1 = true, columnsStartAt1 = true });
            session.Send("attach", new { processId = int.Parse(File.ReadAllText(pidFile), CultureInfo.InvariantCulture), justMyCode = false }, false);
            session.Receive(message => message.TryGetProperty("event", out var name) && name.GetString() == "initialized");
            var source = context.At("tests/DebugIntegration/Entry.cs"); var lines = File.ReadAllLines(source); var line = Array.FindIndex(lines, value => value.Contains("STB_BREAKPOINT", StringComparison.Ordinal)) + 1; Require(line > 0, "Breakpoint marker missing");
            session.Send("setBreakpoints", new { source = new { path = source }, breakpoints = new[] { new { line } } });
            session.Send("setExceptionBreakpoints", new { filters = new[] { "all" } }); session.Send("configurationDone");
            JsonElement Stopped() => session.Receive(message => message.TryGetProperty("event", out var name) && name.GetString() == "stopped").GetProperty("body");
            var stopped = Stopped(); Require(stopped.GetProperty("reason").GetString() == "breakpoint", "Breakpoint was not hit"); var thread = stopped.GetProperty("threadId").GetInt32();
            var frames = session.Send("stackTrace", new { threadId = thread }).GetProperty("stackFrames");
            var value = session.Send("evaluate", new { expression = "inspectedValue", frameId = frames[0].GetProperty("id").GetInt32(), context = "watch" }); Require(value.GetProperty("result").GetString() == "41", "Inspected local differs");
            session.Send("continue", new { threadId = thread }); stopped = Stopped(); Require(stopped.GetProperty("reason").GetString() == "exception", "Expected exception was not observed");
            thread = stopped.GetProperty("threadId").GetInt32(); var exception = session.Send("exceptionInfo", new { threadId = thread }); Require(exception.GetRawText().Contains("STB_DEBUG_EXPECTED_EXCEPTION", StringComparison.Ordinal), "Wrong debugger exception");
            session.Send("setExceptionBreakpoints", new { filters = Array.Empty<string>() }); session.Send("continue", new { threadId = thread });
            Require(simulator.WaitForExit(30000) && simulator.ExitCode == 1, "Expected regression failure did not exit");
            using var results = StrictJson.Read(Path.Combine(work, "debugger-results.json")); Require(results.RootElement.GetArrayLength() == 1 && results.RootElement[0].GetProperty("Status").GetString() == "failed" && results.RootElement.GetRawText().Contains("STB_DEBUG_EXPECTED_EXCEPTION", StringComparison.Ordinal), "Debugger fixture results differ");
            StrictJson.Write(Path.Combine(work, "evidence.json"), new Dictionary<string, object> { ["breakpoint"] = true, ["local"] = value, ["exception"] = exception, ["shutdown"] = "expected test failure; simulator exited" }); Console.WriteLine("DEBUGGER_PASS");
        }
        finally
        {
            if (debugger is not null) { if (!debugger.HasExited) debugger.Kill(true); debugger.WaitForExit(5000); File.WriteAllText(Path.Combine(work, "debugger-stderr.log"), debuggerError!.GetAwaiter().GetResult()); debugger.Dispose(); }
            if (!simulator.HasExited)
            {
                if (File.Exists(pidFile) && int.TryParse(File.ReadAllText(pidFile), out var pid)) { try { using var child = Process.GetProcessById(pid); child.Kill(true); } catch (ArgumentException) { } }
                if (!simulator.WaitForExit(5000)) simulator.Kill(true);
            }
            File.WriteAllText(Path.Combine(work, "driver.log"), stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult()); StrictJson.Write(Path.Combine(work, "dap-transcript.json"), session?.Transcript ?? []);
        }
    }
}
