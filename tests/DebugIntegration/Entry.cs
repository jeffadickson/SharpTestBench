using System.Diagnostics;
using System.Runtime.InteropServices;
using SharpTestBench;

public static class Entry
{
    [DllImport("libc", SetLastError = true)]
    private static extern int prctl(int option, nuint arg2, nuint arg3, nuint arg4, nuint arg5);

    [UnmanagedCallersOnly]
    public static int Initialize(nint p, int n)
    {
        // Opt-in, fixture-only permission for a sibling debugger on Linux/Yama.
        if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("STB_DEBUG_PTRACE") == "1")
            if (prctl(0x59616d61, unchecked((nuint)(nint)(-1)), 0, 0, 0) != 0)
                Console.Error.WriteLine($"Fixture ptrace opt-in unavailable: errno {Marshal.GetLastPInvokeError()}");
        var path = Environment.GetEnvironmentVariable("STB_DEBUG_PID");
        if (path is not null)
        {
            File.WriteAllText(path, Environment.ProcessId.ToString());
            var deadline = Stopwatch.StartNew();
            while (!Debugger.IsAttached && deadline.Elapsed < TimeSpan.FromSeconds(45)) Thread.Sleep(20);
            if (!Debugger.IsAttached) return 1;
        }
        return Regression.Initialize(typeof(Entry).Assembly);
    }

    [SimTest]
    public static async Task BreakpointAndException(TestContext c)
    {
        int inspectedValue = 41;
        await Triggers.Timer(1);
        inspectedValue++; // STB_BREAKPOINT
        c.Log($"Debugger local = {inspectedValue}");
        throw new InvalidOperationException("STB_DEBUG_EXPECTED_EXCEPTION");
    }
}
