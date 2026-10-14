using SharpTestBench;
using System.Runtime.InteropServices;

public static class Entry
{
    [UnmanagedCallersOnly]
    public static int Initialize(nint p, int n) => Regression.Initialize(typeof(Entry).Assembly);
}

public static class StageTests
{
    // Names deliberately disagree with stage ordering.
    [SimTest(Stage = -2)]
    [SimCase(2)]
    [SimCase(1)]
    public static Task Z_First(TestContext c, int value)
    {
        c.Log($"stage parameter={value}");
        return Task.CompletedTask;
    }

    [SimTest(Stage = 0)]
    public static Task A_Fail(TestContext c) =>
        Environment.GetEnvironmentVariable("CS_STAGE_INJECT_FAILURE") == "1"
            ? Task.FromException(new FormatException("intentional stage failure")) : Task.CompletedTask;

    [SimTest(Stage = 0)]
    public static Task B_After(TestContext c) => Task.CompletedTask;

    [SimTest(Stage = 2)]
    public static Task A_Last(TestContext c) => Task.CompletedTask;
}
