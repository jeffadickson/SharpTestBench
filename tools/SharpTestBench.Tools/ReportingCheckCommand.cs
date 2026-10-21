using System.Text.Json;
using System.Xml.Linq;
using SharpTestBench.Internal;

namespace SharpTestBench.Tools;

internal static class ReportingCheckCommand
{
    const string Selected = "Tests.P_ExitStatus";
    public static void Execute(ToolContext context, string[] arguments)
    {
        if (arguments.Length != 0) throw new ArgumentException("reporting-check accepts no arguments");
        NativeBuildCommand.Build(context);
        foreach (var mode in new[] { "text", "io" })
        {
            ParityCiCommand.InWorkspace(context, "reporting-" + mode + "-" + Guid.NewGuid().ToString("N"), (work, environment) =>
            {
                environment["CS_INJECT_FAILURE"] = null;
                environment["CS_INJECT_XML_FAILURE"] = null;
                environment["CS_INJECT_REPORT_IO_FAILURE"] = null;
                var legacy = Path.Combine(work, "legacy"); var native = Path.Combine(work, "native");
                var assembly = context.At("tests/Integration/bin/Release/net10.0/Integration.dll");
                var source = context.At("tests/Integration/top.sv");
                CommandResult RunLegacy(string label, bool list = false)
                {
                    var command = new List<string>(ToolContext.Self) { "run", "--sim", "icarus", "--no-build", "--assembly", assembly,
                        "--entry-type", "Entry, Integration", "--source", source, "--top", "top", "--seed", "1",
                        "--work-dir", legacy, "--label", label, "--exact", Selected };
                    if (list) command.Add("--list");
                    var result = context.Run(command.ToArray(), environment: environment, capture: true, check: false, timeout: TimeSpan.FromSeconds(60));
                    File.WriteAllText(Path.Combine(work, label + ".log"), result.Output + result.Error); return result;
                }
                var discovery = RunLegacy("discovery", true);
                if (discovery.ExitCode != 0) throw new InvalidDataException("Reporting fixture discovery failed");
                using var discovered = StrictJson.Read(Path.Combine(legacy, "discovery-results.discovery.json"));
                var cases = discovered.RootElement.GetProperty("Tests");
                if (cases.GetArrayLength() != 1 || cases[0].GetProperty("QualifiedName").GetString() != Selected)
                    throw new InvalidDataException("Reporting fixture selection differs");
                ulong seed = cases[0].GetProperty("Seed").GetUInt64();
                var manifest = RunnerManifestCommand.Make(context.Root, "counter", 1, RunnerManifestCommand.InstalledAdapter(context), native);
                manifest["assembly"] = assembly; manifest["entryType"] = "Entry, Integration";
                manifest["top"] = "top"; manifest["sources"] = new[] { source };
                RunnerManifestCommand.BindSingleCase(manifest, Selected, seed);
                StrictJson.Write(Path.Combine(work, "manifest.json"), manifest);
                environment[mode == "text" ? "CS_INJECT_XML_FAILURE" : "CS_INJECT_REPORT_IO_FAILURE"] = "1";
                var left = RunLegacy("legacy");
                var right = context.Run(["dotnet", context.At("tools/SharpTestBench.Runner/bin/Release/net10.0/SharpTestBench.Runner.dll"), "run", Path.Combine(work, "manifest.json")],
                    environment: environment, capture: true, check: false, timeout: TimeSpan.FromSeconds(60));
                File.WriteAllText(Path.Combine(work, "native.log"), right.Output + right.Error);
                if (left.ExitCode != 1 || right.ExitCode != 1) throw new InvalidDataException("Reporting negatives must fail both runners without watchdog expiry");
                Dictionary<string, object> report;
                if (mode == "text")
                {
                    using var a = StrictJson.Read(Path.Combine(legacy, "legacy-results.json"));
                    using var b = StrictJson.Read(Path.Combine(native, "icarus-results.json"));
                    report = ParityContracts.CompareFaults(a.RootElement, left.ExitCode, b.RootElement, right.ExitCode, JsonSerializer.SerializeToElement(new[] { Selected }), "REPORT_XML_FAILURE");
                    var expected = JsonSerializer.SerializeToElement(new Dictionary<string, ulong> { [Selected] = seed });
                    bool seeds = ParityContracts.MatchExpectedSeeds(a.RootElement, expected) && ParityContracts.MatchExpectedSeeds(b.RootElement, expected);
                    report["expectedSeedMatched"] = seeds; report["matched"] = (bool)report["matched"] && seeds;
                    foreach (var pair in new[] { (Rows: a.RootElement, Xml: Path.Combine(legacy, "legacy-results.xml")), (Rows: b.RootElement, Xml: Path.Combine(native, "icarus-results.xml")) })
                    {
                        var error = pair.Rows[0].GetProperty("Error").GetString()!;
                        var failure = XDocument.Load(pair.Xml).Root!.Element("testcase")!.Element("failure")!.Value;
                        if (!error.Contains('\0') || !error.Contains('\a') || !failure.Contains("#x00") || !failure.Contains("#x07") || !failure.Contains("😀") || !failure.Contains("&<>"))
                            throw new InvalidDataException("JSON diagnostic or safe XML text differs");
                    }
                    report["xmlSanitized"] = true;
                }
                else
                {
                    bool a = left.Output.Split('\n').Concat(left.Error.Split('\n')).Any(line => line.StartsWith(ReportFailureEvidence.Marker, StringComparison.Ordinal));
                    bool b = File.ReadLines(Path.Combine(native, "simulate.stderr.log")).Any(line => line.StartsWith(ReportFailureEvidence.Marker, StringComparison.Ordinal));
                    using var failure = StrictJson.Read(Path.Combine(native, "failure-manifest.json"));
                    bool retired = !File.Exists(Path.Combine(legacy, "legacy-results.json")) && !File.Exists(Path.Combine(native, "icarus-results.json"));
                    bool classified = failure.RootElement.GetProperty("Kind").GetString() == "infrastructure_failure";
                    report = new() { ["schemaVersion"] = 1, ["negativeControl"] = true, ["legacyDetected"] = a, ["nativeDetected"] = b,
                        ["matched"] = a && b && retired && classified, ["primaryResultsRetired"] = retired, ["nativeInfrastructureFailure"] = classified };
                }
                report["mode"] = mode; report["exitCodes"] = new { legacy = left.ExitCode, native = right.ExitCode };
                return report;
            });
        }
    }
}
