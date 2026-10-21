using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SharpTestBench;

internal static class JunitReport
{
    // XML 1.0 cannot carry every character permitted in an exception string.
    // Keep the JSON evidence intact and make rejected UTF-16 units visible in XML.
    internal static string Text(string? text)
    {
        if (text is null) return "";
        var result = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char value = text[i];
            if (char.IsHighSurrogate(value) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                result.Append(value).Append(text[++i]);
            }
            else if (XmlConvert.IsXmlChar(value) && value != 127) result.Append(value);
            else result.Append("#x").Append(((int)value).ToString(value <= 255 ? "X2" : "X4", CultureInfo.InvariantCulture));
        }
        return result.ToString();
    }

    static string ClassName(TestResult result)
    {
        var separator = result.QualifiedName?.LastIndexOf('.') ?? -1;
        return separator > 0 ? Text(result.QualifiedName![..separator]) : "SharpTestBench";
    }

    internal static XDocument Create(IReadOnlyList<TestResult> results)
    {
        var suite = new XElement("testsuite",
            new XAttribute("tests", results.Count),
            new XAttribute("failures", results.Count(x => x.Status == "failed")),
            new XAttribute("skipped", results.Count(x => x.Status == "skipped")),
            results.Select(x => new XElement("testcase",
                new XAttribute("name", Text(x.Name)), new XAttribute("classname", ClassName(x)), new XAttribute("time", x.Seconds),
                x.Status == "failed" ? new XElement("failure", Text(x.Error)) :
                x.Status == "skipped" ? new XElement("skipped", Text(x.Error)) : null)));
        return new XDocument(suite);
    }
}
