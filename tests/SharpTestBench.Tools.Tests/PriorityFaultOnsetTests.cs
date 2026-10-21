using System.Globalization;
using System.Text.Json;
using SharpTestBench.Tools;
using Xunit;

public sealed class PriorityFaultOnsetTests
{
    static void Check(int config, string kind, int vector, int detected)
    {
        var fault = PriorityFault.Parse(config.ToString(CultureInfo.InvariantCulture), kind, true, vector.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(vector, fault.OnsetVector);
        Assert.Equal(detected, fault.DetectedVector(83));
        int encoded = kind == "valid" ? 0 : kind == "index" ? 1 : 2;
        Assert.Equal($"PRIORITY_MISMATCH configuration={config} fault={encoded} observed={kind} vector={detected} ", fault.Marker(83));
        var expected = JsonSerializer.SerializeToElement(new[] { "Entry.PriorityScoreboard" });
        JsonElement Rows(string marker) => JsonSerializer.SerializeToElement(new[] { new { QualifiedName = "Entry.PriorityScoreboard", Status = "failed", Error = marker, Seed = 83UL } });
        var correct = Rows(fault.Marker(83));
        Assert.True((bool)ParityContracts.CompareFaults(correct, 1, correct, 1, expected, fault.Marker(83))["matched"]);
        var wrong = Rows(fault.Marker(83).Replace($"vector={detected} ", $"vector={detected + 1} "));
        Assert.False((bool)ParityContracts.CompareFaults(correct, 1, wrong, 1, expected, fault.Marker(83))["matched"]);
        var prefix = Rows(fault.Marker(83).Replace($"vector={detected} ", $"vector={detected}0 "));
        Assert.False(ParityContracts.FaultDetected(prefix, 1, expected, fault.Marker(83)));
        var env = new Dictionary<string, string?> { ["PRIORITY_VECTOR"] = "poison" };
        fault.Apply(env);
        Assert.Equal(vector.ToString(CultureInfo.InvariantCulture), env["PRIORITY_VECTOR"]);
        Assert.Equal("-negative" + (config == 0 && encoded == 0 ? "" : $"-c{config}-k{encoded}") + (vector == 0 ? "" : $"-v{vector}"), fault.Suffix);
    }
    static void Reject(string value, bool enabled = true) => Assert.Throws<ArgumentException>(() => PriorityFault.Parse(null, null, enabled, value));
    [Fact] public void DetectFinalValidConfiguration0() => Check(0, "valid", 452, 452);
    [Fact] public void DetectFinalValidConfiguration1() => Check(1, "valid", 452, 452);
    [Fact] public void DetectFinalValidConfiguration2() => Check(2, "valid", 452, 452);
    [Fact] public void DetectFinalValidConfiguration3() => Check(3, "valid", 452, 452);
    [Fact] public void DetectFinalValidConfiguration4() => Check(4, "valid", 452, 452);
    [Fact] public void DetectFinalValidConfiguration5() => Check(5, "valid", 452, 452);
    [Fact] public void DetectFinalValidConfiguration6() => Check(6, "valid", 452, 452);
    [Fact] public void DetectFinalValidConfiguration7() => Check(7, "valid", 452, 452);
    [Fact] public void DetectFinalValidConfiguration8() => Check(8, "valid", 452, 452);
    [Fact] public void DetectFinalValidConfiguration9() => Check(9, "valid", 452, 452);
    [Fact] public void DetectFinalValidConfiguration10() => Check(10, "valid", 452, 452);
    [Fact] public void DetectFinalValidConfiguration11() => Check(11, "valid", 452, 452);
    [Fact] public void DeferInvalidZeroIndexConfiguration0() => Check(0, "index", 0, 1);
    [Fact] public void DeferInvalidZeroIndexConfiguration1() => Check(1, "index", 0, 1);
    [Fact] public void DeferInvalidZeroIndexConfiguration2() => Check(2, "index", 0, 1);
    [Fact] public void DeferInvalidZeroIndexConfiguration3() => Check(3, "index", 0, 1);
    [Fact] public void DeferInvalidZeroIndexConfiguration4() => Check(4, "index", 0, 1);
    [Fact] public void DeferInvalidZeroIndexConfiguration5() => Check(5, "index", 0, 1);
    [Fact] public void DeferInvalidZeroIndexConfiguration6() => Check(6, "index", 0, 1);
    [Fact] public void DeferInvalidZeroIndexConfiguration7() => Check(7, "index", 0, 1);
    [Fact] public void DeferInvalidZeroIndexConfiguration8() => Check(8, "index", 0, 1);
    [Fact] public void DeferInvalidZeroIndexConfiguration9() => Check(9, "index", 0, 1);
    [Fact] public void DeferInvalidZeroIndexConfiguration10() => Check(10, "index", 0, 1);
    [Fact] public void DeferInvalidZeroIndexConfiguration11() => Check(11, "index", 0, 1);
    [Fact] public void DeferInvalidZeroOnehotConfiguration0() => Check(0, "onehot", 0, 1);
    [Fact] public void DeferInvalidZeroOnehotConfiguration1() => Check(1, "onehot", 0, 1);
    [Fact] public void DeferInvalidZeroOnehotConfiguration2() => Check(2, "onehot", 0, 1);
    [Fact] public void DeferInvalidZeroOnehotConfiguration3() => Check(3, "onehot", 0, 1);
    [Fact] public void DeferInvalidZeroOnehotConfiguration4() => Check(4, "onehot", 0, 1);
    [Fact] public void DeferInvalidZeroOnehotConfiguration5() => Check(5, "onehot", 0, 1);
    [Fact] public void DeferInvalidZeroOnehotConfiguration6() => Check(6, "onehot", 0, 1);
    [Fact] public void DeferInvalidZeroOnehotConfiguration7() => Check(7, "onehot", 0, 1);
    [Fact] public void DeferInvalidZeroOnehotConfiguration8() => Check(8, "onehot", 0, 1);
    [Fact] public void DeferInvalidZeroOnehotConfiguration9() => Check(9, "onehot", 0, 1);
    [Fact] public void DeferInvalidZeroOnehotConfiguration10() => Check(10, "onehot", 0, 1);
    [Fact] public void DeferInvalidZeroOnehotConfiguration11() => Check(11, "onehot", 0, 1);
    [Fact] public void DetectScheduleBoundary0() => Check(11, "valid", 0, 0);
    [Fact] public void DetectScheduleBoundary1() => Check(11, "valid", 1, 1);
    [Fact] public void DetectScheduleBoundary127() => Check(11, "valid", 127, 127);
    [Fact] public void DetectScheduleBoundary255() => Check(11, "valid", 255, 255);
    [Fact] public void DetectScheduleBoundary256() => Check(11, "valid", 256, 256);
    [Fact] public void DetectScheduleBoundary260() => Check(11, "valid", 260, 260);
    [Fact] public void DetectScheduleBoundary324() => Check(11, "valid", 324, 324);
    [Fact] public void DetectScheduleBoundary325() => Check(11, "valid", 325, 325);
    [Fact] public void RejectNegativeOnset() => Reject("-1");
    [Fact] public void RejectPastFinalOnset() => Reject("453");
    [Fact] public void RejectNonnumericOnset() => Reject("x");
    [Fact] public void RejectPositiveOnset() => Reject("0", false);
    [Fact] public void RejectUnobservableFinalIndex() => Assert.Throws<InvalidDataException>(() => PriorityFault.Parse("0", "index", true, "452").DetectedVector(1));
}
