namespace SharpTestBench;

/// <summary>Transition policy for Triggers.ClockCycles.</summary>
public enum ClockEdge
{
    /// <summary>Count transitions to one.</summary>
    Rising,
    /// <summary>Count transitions to zero.</summary>
    Falling,
    /// <summary>Count every value change, including nonbinary transitions when exposed by the backend.</summary>
    Any
}
