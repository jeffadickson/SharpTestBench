using SharpTestBench;

/// <summary>Direction checks use elaborated module ports, not signal names or testbench assumptions.</summary>
public static class PortDirectionTests
{
    /// <summary>Verify known ports, fail-closed guards, generated bindings and ordinary internal objects.</summary>
    [SimTest]
    public static Task PortDirections(TestContext c)
    {
        var dut = c.Dut;
        var input = dut.Child("incoming"); var output = dut.Child("outgoing");
        var io = dut.Child("bidirectional"); var internalValue = dut.Child("bench32");
        if (Environment.GetEnvironmentVariable("STB_EXPECT_PORT_DIRECTION_UNKNOWN") == "1")
        {
            Check(input.Direction == PortDirection.Unknown && output.Direction == PortDirection.Unknown, "Old host metadata");
            Reject<NotSupportedException>(() => new DirectionBindings.Ports(dut));
            return Task.CompletedTask;
        }
        Check(input.Direction == PortDirection.Input, "Input direction");
        Check(output.Direction == PortDirection.Output, "Output direction");
        Check(io.Direction == PortDirection.InOut, "Inout direction");
        Check(internalValue.Direction == PortDirection.Unknown, "Internal object must not be classified as port");
        Check(dut.Direction == PortDirection.Unknown, "Module must not be classified as port");
        Check(ReferenceEquals(input, input.RequireDirection(PortDirection.Input)), "Fluent guard identity");
        Reject<InvalidOperationException>(() => output.RequireDirection(PortDirection.Input));
        Reject<NotSupportedException>(() => internalValue.RequireDirection(PortDirection.Input));
        Reject<ArgumentOutOfRangeException>(() => input.RequireDirection(PortDirection.Unknown));
        Reject<ArgumentOutOfRangeException>(() => input.RequireDirection((PortDirection)99));
        var nested = dut.Child("directions").Child("incoming");
        if (c.Runtime.Capabilities.Family == SimulatorFamily.Icarus)
            Check(nested.Direction == PortDirection.Input, "Nested Icarus port metadata");
        else
        {
            Check(nested.Direction == PortDirection.Unknown, "Flattened nested port must remain Unknown");
            Reject<NotSupportedException>(() => nested.RequireDirection(PortDirection.Input));
        }
        var ports = new DirectionBindings.Ports(dut);
        Check(ports.Incoming.Width == 1 && ports.Outgoing.Width == 1, "Generated guarded bindings");
        // Repeated queries exercise borrowed-string copying and VPI handle release.
        for (int i = 0; i < 100; i++) Check(input.Direction == PortDirection.Input && output.Direction == PortDirection.Output, "Repeated lookup");
        return Task.CompletedTask;
    }

    /// <summary>Fail with a simulator-visible diagnostic.</summary>
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    /// <summary>Require the precise guard failure category.</summary>
    static void Reject<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception($"Expected {typeof(T).Name}");
    }
}
