using System.Globalization;
using SharpTestBench.Tools;
using Xunit;

public sealed class PriorityFaultTests
{
    static void Check(int configuration, string kind, int encoded)
    {
        var fault = PriorityFault.Parse(configuration.ToString(CultureInfo.InvariantCulture), kind, true);
        var environment = new Dictionary<string, string?> { ["PRIORITY_FAULT"] = "poison", ["PRIORITY_CONFIG"] = "poison", ["PRIORITY_KIND"] = "poison", ["KEEP"] = "retained" };
        fault.Apply(environment);
        Assert.Equal("1", environment["PRIORITY_FAULT"]);
        Assert.Equal(configuration.ToString(CultureInfo.InvariantCulture), environment["PRIORITY_CONFIG"]);
        Assert.Equal(encoded.ToString(CultureInfo.InvariantCulture), environment["PRIORITY_KIND"]);
        Assert.Equal("retained", environment["KEEP"]);
        Assert.Equal($"PRIORITY_MISMATCH configuration={configuration} fault={encoded} observed={kind} vector={(encoded == 0 ? 0 : 1)} ", fault.Marker(83));
        Assert.Equal(configuration == 0 && encoded == 0 ? "-negative" : $"-negative-c{configuration}-k{encoded}", fault.Suffix);
    }
    static void Reject(string? configuration, string? kind, bool enabled = true) =>
        Assert.Throws<ArgumentException>(() => PriorityFault.Parse(configuration, kind, enabled));
    [Fact] public void EncodeConfiguration0Valid() => Check(0, "valid", 0);
    [Fact] public void EncodeConfiguration0Index() => Check(0, "index", 1);
    [Fact] public void EncodeConfiguration0Onehot() => Check(0, "onehot", 2);
    [Fact] public void EncodeConfiguration1Valid() => Check(1, "valid", 0);
    [Fact] public void EncodeConfiguration1Index() => Check(1, "index", 1);
    [Fact] public void EncodeConfiguration1Onehot() => Check(1, "onehot", 2);
    [Fact] public void EncodeConfiguration2Valid() => Check(2, "valid", 0);
    [Fact] public void EncodeConfiguration2Index() => Check(2, "index", 1);
    [Fact] public void EncodeConfiguration2Onehot() => Check(2, "onehot", 2);
    [Fact] public void EncodeConfiguration3Valid() => Check(3, "valid", 0);
    [Fact] public void EncodeConfiguration3Index() => Check(3, "index", 1);
    [Fact] public void EncodeConfiguration3Onehot() => Check(3, "onehot", 2);
    [Fact] public void EncodeConfiguration4Valid() => Check(4, "valid", 0);
    [Fact] public void EncodeConfiguration4Index() => Check(4, "index", 1);
    [Fact] public void EncodeConfiguration4Onehot() => Check(4, "onehot", 2);
    [Fact] public void EncodeConfiguration5Valid() => Check(5, "valid", 0);
    [Fact] public void EncodeConfiguration5Index() => Check(5, "index", 1);
    [Fact] public void EncodeConfiguration5Onehot() => Check(5, "onehot", 2);
    [Fact] public void EncodeConfiguration6Valid() => Check(6, "valid", 0);
    [Fact] public void EncodeConfiguration6Index() => Check(6, "index", 1);
    [Fact] public void EncodeConfiguration6Onehot() => Check(6, "onehot", 2);
    [Fact] public void EncodeConfiguration7Valid() => Check(7, "valid", 0);
    [Fact] public void EncodeConfiguration7Index() => Check(7, "index", 1);
    [Fact] public void EncodeConfiguration7Onehot() => Check(7, "onehot", 2);
    [Fact] public void EncodeConfiguration8Valid() => Check(8, "valid", 0);
    [Fact] public void EncodeConfiguration8Index() => Check(8, "index", 1);
    [Fact] public void EncodeConfiguration8Onehot() => Check(8, "onehot", 2);
    [Fact] public void EncodeConfiguration9Valid() => Check(9, "valid", 0);
    [Fact] public void EncodeConfiguration9Index() => Check(9, "index", 1);
    [Fact] public void EncodeConfiguration9Onehot() => Check(9, "onehot", 2);
    [Fact] public void EncodeConfiguration10Valid() => Check(10, "valid", 0);
    [Fact] public void EncodeConfiguration10Index() => Check(10, "index", 1);
    [Fact] public void EncodeConfiguration10Onehot() => Check(10, "onehot", 2);
    [Fact] public void EncodeConfiguration11Valid() => Check(11, "valid", 0);
    [Fact] public void EncodeConfiguration11Index() => Check(11, "index", 1);
    [Fact] public void EncodeConfiguration11Onehot() => Check(11, "onehot", 2);
    [Fact] public void RejectNegativeConfiguration() => Reject("-1", null);
    [Fact] public void RejectConfigurationPastLast() => Reject("12", null);
    [Fact] public void RejectOverflowConfiguration() => Reject("4294967296", null);
    [Fact] public void RejectNonnumericConfiguration() => Reject("x", null);
    [Fact] public void RejectEmptyConfiguration() => Reject("", null);
    [Fact] public void RejectSignedConfiguration() => Reject("+0", null);
    [Fact] public void RejectUnknownKind() => Reject(null, "bogus");
    [Fact] public void RejectUppercaseKind() => Reject(null, "VALID");
    [Fact] public void RejectEmptyKind() => Reject(null, "");
    [Fact] public void RejectPositiveConfiguration() => Reject("0", null, false);
    [Fact] public void RejectPositiveKind() => Reject(null, "valid", false);
}
