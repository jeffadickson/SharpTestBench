using System.Xml.Linq;
using SharpTestBench;
using Xunit;
using TestResult = SharpTestBench.TestResult;

public sealed class JunitRequirementTests
{
    [Fact] public void IllegalExceptionCharactersCannotBreakXmlEvidence()
    {
        const string error = "a\0b\a\b\vc\fd\u001fe\u007ff\ufffe\uffff\ud800g\udfff";
        var row = new TestResult("failed\0case", "failed", 17, 0, 1, 0.125, error);
        var xml = JunitReport.Create(new[] { row });
        var reloaded = XDocument.Parse(xml.ToString());
        Assert.Equal("failed#x00case", reloaded.Root!.Element("testcase")!.Attribute("name")!.Value);
        Assert.Equal("a#x00b#x07#x08#x0Bc#x0Cd#x1Fe#x7Ff#xFFFE#xFFFF#xD800g#xDFFF", reloaded.Root.Element("testcase")!.Element("failure")!.Value);
        Assert.Equal(error, row.Error);
    }
    [Fact] public void QualifiedClassIdentityDisambiguatesEqualShortNames()
    {
        var rows = new[] {
            new TestResult("Scoreboard", "passed", 1, 0, 1, 1, null) { QualifiedName = "Namespace.First.Scoreboard" },
            new TestResult("Scoreboard", "passed", 2, 0, 1, 1, null) { QualifiedName = "Namespace.Second+Nested.Scoreboard" },
            new TestResult("runtime", "failed", 0, 0, 0, 0, "fatal")
        };
        var cases = JunitReport.Create(rows).Root!.Elements("testcase").ToArray();
        Assert.Equal(new[] { "Namespace.First", "Namespace.Second+Nested", "SharpTestBench" }, cases.Select(x => x.Attribute("classname")!.Value));
        Assert.Equal(new[] { "Scoreboard", "Scoreboard", "runtime" }, cases.Select(x => x.Attribute("name")!.Value));
    }

    [Fact] public void WallTimeIsInvariantUnderCommaDecimalCulture()
    {
        var before = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
            var xml = JunitReport.Create(new[] { new TestResult("test", "passed", 1, 0, 1, 0.125, null) });
            Assert.Equal("0.125", XDocument.Parse(xml.ToString()).Root!.Element("testcase")!.Attribute("time")!.Value);
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = before; }
    }

    [Fact] public void ReportCountsAndPayloadKindsMatchObservedOutcomes()
    {
        var rows = new[] {
            new TestResult("pass", "passed", 1, 0, 1, 0, "diagnostic only"),
            new TestResult("fail", "failed", 2, 0, 1, 0, "failure reason"),
            new TestResult("skip", "skipped", 3, 0, 1, 0, "skip reason")
        };
        var suite = JunitReport.Create(rows).Root!;
        Assert.Equal("3", suite.Attribute("tests")!.Value); Assert.Equal("1", suite.Attribute("failures")!.Value);
        Assert.Equal("1", suite.Attribute("skipped")!.Value);
        var cases = suite.Elements("testcase").ToArray();
        Assert.Empty(cases[0].Elements()); Assert.Equal("failure reason", cases[1].Element("failure")!.Value);
        Assert.Equal("skip reason", cases[2].Element("skipped")!.Value);
        var empty = JunitReport.Create(Array.Empty<TestResult>()).Root!;
        Assert.Equal("0", empty.Attribute("tests")!.Value); Assert.Empty(empty.Elements());
    }

    [Fact] public void LegalUnicodeAndXmlMetacharactersRemainReadableAndRoundTrip()
    {
        const string text = "name <&\"'> \t\n \uD7FF\uE000\uFFFD \U0001F600 \U0010FFFF";
        var xml = JunitReport.Create(new[] { new TestResult(text, "failed", 1, 0, 1, 0, text) });
        var parsed = XDocument.Parse(xml.ToString()); var row = parsed.Root!.Element("testcase")!;
        Assert.Equal(text, row.Attribute("name")!.Value); Assert.Equal(text, row.Element("failure")!.Value);
        Assert.DoesNotContain("#x", row.Element("failure")!.Value);
    }
}
