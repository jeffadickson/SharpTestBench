using System.Text.Json;

namespace SharpTestBench;

internal static class ResultPublication
{
    internal static void Complete(string output, IReadOnlyList<TestResult> results,
        IReadOnlyList<TestDescriptor> discovered, IReadOnlyList<object> coverage, bool listOnly, Action? finish)
    {
        bool primaryPromoted = false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            var options = new JsonSerializerOptions { WriteIndented = true };
            using var xml = new MemoryStream();
            JunitReport.Create(results).Save(xml);
            // Prepare all content before replacing any existing evidence. A JSON
            // conversion failure must not leave new results beside old coverage.
            var reports = new (string Path, byte[] Contents)[] {
                (Path.ChangeExtension(output, ".discovery.json"), System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { SchemaVersion = 1, Tests = discovered }, options))),
                (output, System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(results, options))),
                (Path.ChangeExtension(output, ".coverage.json"), System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { SchemaVersion = 1, Kind = "functional", Tests = coverage }, options))),
                (Path.ChangeExtension(output, "xml"), xml.ToArray())
            };
            var comparison = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            if (reports.Select(report => Path.GetFullPath(report.Path)).Distinct(comparison).Count() != reports.Length)
                throw new InvalidDataException("Result destination aliases a report sidecar.");
            foreach (var report in reports)
            {
                var pending = report.Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllBytes(pending, report.Contents); File.Move(pending, report.Path, true);
                    if (report.Path == output) primaryPromoted = true;
                }
                finally { File.Delete(pending); }
            }
            if (listOnly && results.Count == 0) Console.WriteLine($"CS_DISCOVERY {discovered.Count} tests; no test bodies executed");
            else Console.WriteLine($"CS_SUMMARY {results.Count(x => x.Status == "passed")} passed {results.Count(x => x.Status == "failed")} failed {results.Count(x => x.Status == "skipped")} skipped");
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(SharpTestBench.Internal.ReportFailureEvidence.Marker + error);
            // Retire only a primary report promoted by this failed invocation.
            // Preparation failures leave the prior complete evidence untouched.
            if (primaryPromoted)
            {
                try { File.Delete(output); }
                catch (Exception retirement) when (retirement is IOException or UnauthorizedAccessException)
                { Console.Error.WriteLine("CS_REPORT_RETIRE_FAILURE " + retirement); }
            }
            throw;
        }
        finally { finish?.Invoke(); }
    }
}
