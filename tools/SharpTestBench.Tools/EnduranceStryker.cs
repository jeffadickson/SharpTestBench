using System.Text.Json;
using System.Text.RegularExpressions;

namespace SharpTestBench.Tools;

/// <summary>Stryker.NET owns mutation generation/execution; character-span selection limits the campaign to reviewed evidence guards.</summary>
internal static class EnduranceStrykerEngine
{
    internal const string Version = EnduranceRules.StrykerVersion;
    internal const string Scope = EnduranceRules.StrykerScope;
    internal static (int Start,int End)[] Ranges(string source,IReadOnlyList<string> guards)=>guards.Select(id=>
    {
        if(!EnduranceRules.MutationIds.Contains(id,StringComparer.Ordinal))throw new ArgumentException("Unknown Stryker guard");
        int marker=source.IndexOf("// MUTATE:"+id,StringComparison.Ordinal);
        int start=marker<0?-1:source.LastIndexOf("\n        if (",marker,StringComparison.Ordinal);
        if(start<0)throw new InvalidDataException("Stryker span anchor differs");
        return(start+1,marker);
    }).ToArray();
    internal static (string Config, string TestDirectory, string Output) Prepare(string snapshot, string work, IReadOnlyList<string> guards)
    {
        string source = File.ReadAllText(Path.Combine(snapshot, "tools/SharpTestBench.Tools/EnduranceRules.cs"));
        var spans = new List<string>();
        foreach (var range in Ranges(source,guards))
        {
            // Stryker spans are UTF-16 character indices, NOT line numbers.
            spans.Add("**/EnduranceRules.cs{" + range.Start + ".." + range.End + "}");
        }
        string output = Path.Combine(work, "stryker"); Directory.CreateDirectory(output);
        string config = Path.Combine(output, "config.json");
        EnduranceStorage.WriteJson(config, new Dictionary<string, object>
        {
            ["stryker-config"] = new Dictionary<string, object>
            {
                ["project"] = "SharpTestBench.Endurance.MutationSubject.csproj", ["test-projects"] = new[] { "SharpTestBench.Endurance.Mutation.Tests.csproj" },
                ["test-runner"] = "vstest", ["configuration"] = "Release", ["concurrency"] = 1,
                ["mutate"] = spans, ["reporters"] = new[] { "json" },
                ["verbosity"] = "warning", ["additional-timeout"] = 1000, ["timeout-ratio"] = 1.5,
                ["thresholds"] = new { high = 100, low = 0, @break = 0 }
            }
        });
        return (config, Path.Combine(snapshot, "tests/SharpTestBench.Endurance.Mutation.Tests"), output);
    }
    internal static EnduranceStryker Read(string output, string source,IReadOnlyList<string>? guards=null)
    {
        string[] reports = Directory.GetFiles(output, "mutation-report.json", SearchOption.AllDirectories);
        if (reports.Length != 1) throw new InvalidDataException("Stryker completion report is missing or ambiguous");
        byte[] bytes = EnduranceStorage.Read(reports[0], 16777216);
        using var document = StrictJson.Parse(new System.Text.UTF8Encoding(false, true).GetString(bytes));
        var mutants = new List<EnduranceMutation>(); var ids = new HashSet<string>(StringComparer.Ordinal);
        var ranges=Ranges(source,guards??EnduranceRules.MutationIds);int excluded=0,excludedInvalid=0;
        string[] lines=source.Split('\n');
        int Offset(JsonElement point)
        {
            int line=point.GetProperty("line").GetInt32(),column=point.GetProperty("column").GetInt32();
            if(line<1||line>lines.Length||column<1||column>lines[line-1].Length+1)throw new InvalidDataException("Invalid Stryker source location");
            return lines.Take(line-1).Sum(value=>value.Length+1)+column-1;
        }
        foreach (var file in document.RootElement.GetProperty("files").EnumerateObject())
        {
            var rows = file.Value.GetProperty("mutants"); if (rows.GetArrayLength() == 0) continue;
            if (!file.Name.Replace('\\', '/').EndsWith("EnduranceRules.cs", StringComparison.Ordinal) || file.Value.GetProperty("source").GetString() != source) throw new InvalidDataException("Stryker tested source or scope differs");
            foreach (var row in rows.EnumerateArray())
            {
                string id = row.GetProperty("id").GetString()!;
                if (!Regex.IsMatch(id, "\\A[0-9]{1,8}\\z", RegexOptions.CultureInvariant) || !ids.Add(id) || ids.Count > 4096) throw new InvalidDataException("Invalid Stryker mutant identity");
                string outcome = row.GetProperty("status").GetString() switch
                {
                    "Killed" => "killed", "Survived" => "survived", "CompileError" or "RuntimeError" => "invalid",
                    "Timeout" => "timed-out", "NoCoverage" => "no-coverage", "Ignored" => "ignored", "Pending" => "pending",
                    _ => throw new InvalidDataException("Unknown Stryker outcome")
                };
                var location=row.GetProperty("location");int start=Offset(location.GetProperty("start")),end=Offset(location.GetProperty("end"));
                if(end<start)throw new InvalidDataException("Invalid Stryker span");
                if(!ranges.Any(range=>start<range.End&&end>range.Start))
                {
                    excluded++;if(outcome=="invalid")excludedInvalid++;continue;
                }
                mutants.Add(new("stryker-" + id, outcome));
            }
        }
        if (!mutants.Any(item => item.Outcome != "ignored")) throw new InvalidDataException("Stryker produced no selected mutants");
        return new(Version, mutants.Any(item => item.Outcome == "pending") ? "partial" : "completed", Scope, mutants, EnduranceStorage.Hash(bytes),excluded,excludedInvalid);
    }
    internal static bool HasFindings(EnduranceStryker value) => EnduranceRules.StrykerHasFindings(value);
}
