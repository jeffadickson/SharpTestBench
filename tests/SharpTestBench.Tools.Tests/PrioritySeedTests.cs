using System.Globalization;
using System.Text.Json;
using SharpTestBench.Tools;
using Xunit;

public sealed class PrioritySeedTests
{
    static JsonElement Trace(ulong seed, int changed = -1)
    {
        using var reference = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "priority-seed-vectors.json")));
        var masks = reference.RootElement.GetProperty(seed.ToString(CultureInfo.InvariantCulture)).EnumerateArray().Select(x => x.GetUInt32()).ToArray();
        if (changed >= 0) masks[changed] ^= 0x80000001;
        var rows = new List<long[]>();
        foreach (uint mask in masks)
            foreach (int width in new[] { 2, 3, 5, 8, 17, 32 })
                foreach (bool low in new[] { false, true })
                {
                    int winner = Entry.Winner(mask, width, low);
                    rows.Add([width, low ? 1 : 0, mask, winner >= 0 ? 1 : 0, winner, winner >= 0 ? 1u << winner : 0]);
                }
        return JsonSerializer.SerializeToElement(new { trace = rows, vectors = masks.Length, configurations = 12 });
    }
    static void Accept(ulong seed) => PriorityTrace.Validate(Trace(seed), seed);
    static void Reject(int vector)
    {
        var trace = Trace(83, vector);
        PriorityTrace.Validate(trace); // Winners are valid; only seed attestation can reject this.
        var error = Assert.Throws<InvalidDataException>(() => PriorityTrace.Validate(trace, 83));
        Assert.Contains($"vector {vector}", error.Message);
    }
    [Fact] public void AcceptSeed0() => Accept(0UL);
    [Fact] public void AcceptSeed1() => Accept(1UL);
    [Fact] public void AcceptSeed17() => Accept(17UL);
    [Fact] public void AcceptSeed83() => Accept(83UL);
    [Fact] public void AcceptSeed4294967295() => Accept(4294967295UL);
    [Fact] public void AcceptSeed4294967296() => Accept(4294967296UL);
    [Fact] public void AcceptSeed9223372036854775808() => Accept(9223372036854775808UL);
    [Fact] public void AcceptSeed18446744073709551615() => Accept(18446744073709551615UL);
    [Fact] public void RejectWrongStimulusAtVector0() => Reject(0);
    [Fact] public void RejectWrongStimulusAtVector1() => Reject(1);
    [Fact] public void RejectWrongStimulusAtVector2() => Reject(2);
    [Fact] public void RejectWrongStimulusAtVector3() => Reject(3);
    [Fact] public void RejectWrongStimulusAtVector7() => Reject(7);
    [Fact] public void RejectWrongStimulusAtVector15() => Reject(15);
    [Fact] public void RejectWrongStimulusAtVector31() => Reject(31);
    [Fact] public void RejectWrongStimulusAtVector63() => Reject(63);
    [Fact] public void RejectWrongStimulusAtVector127() => Reject(127);
    [Fact] public void RejectWrongStimulusAtVector128() => Reject(128);
    [Fact] public void RejectWrongStimulusAtVector254() => Reject(254);
    [Fact] public void RejectWrongStimulusAtVector255() => Reject(255);
    [Fact] public void RejectWrongStimulusAtVector256() => Reject(256);
    [Fact] public void RejectWrongStimulusAtVector257() => Reject(257);
    [Fact] public void RejectWrongStimulusAtVector258() => Reject(258);
    [Fact] public void RejectWrongStimulusAtVector259() => Reject(259);
    [Fact] public void RejectWrongStimulusAtVector260() => Reject(260);
    [Fact] public void RejectWrongStimulusAtVector261() => Reject(261);
    [Fact] public void RejectWrongStimulusAtVector262() => Reject(262);
    [Fact] public void RejectWrongStimulusAtVector263() => Reject(263);
    [Fact] public void RejectWrongStimulusAtVector264() => Reject(264);
    [Fact] public void RejectWrongStimulusAtVector275() => Reject(275);
    [Fact] public void RejectWrongStimulusAtVector276() => Reject(276);
    [Fact] public void RejectWrongStimulusAtVector291() => Reject(291);
    [Fact] public void RejectWrongStimulusAtVector292() => Reject(292);
    [Fact] public void RejectWrongStimulusAtVector307() => Reject(307);
    [Fact] public void RejectWrongStimulusAtVector308() => Reject(308);
    [Fact] public void RejectWrongStimulusAtVector321() => Reject(321);
    [Fact] public void RejectWrongStimulusAtVector322() => Reject(322);
    [Fact] public void RejectWrongStimulusAtVector323() => Reject(323);
    [Fact] public void RejectWrongStimulusAtVector324() => Reject(324);
    [Fact] public void RejectWrongStimulusAtVector325() => Reject(325);
    [Fact] public void RejectWrongStimulusAtVector326() => Reject(326);
    [Fact] public void RejectWrongStimulusAtVector327() => Reject(327);
    [Fact] public void RejectWrongStimulusAtVector328() => Reject(328);
    [Fact] public void RejectWrongStimulusAtVector351() => Reject(351);
    [Fact] public void RejectWrongStimulusAtVector382() => Reject(382);
    [Fact] public void RejectWrongStimulusAtVector415() => Reject(415);
    [Fact] public void RejectWrongStimulusAtVector451() => Reject(451);
    [Fact] public void RejectWrongStimulusAtVector452() => Reject(452);
    [Fact] public void RejectTraceFromDifferentEffectiveSeed() => Assert.Throws<InvalidDataException>(() => PriorityTrace.Validate(Trace(83), 17));
}
