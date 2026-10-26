using System.Text;

namespace SharpTestBench.Tools;

/// <summary>Compiles literal changes to the actual evidence guards; failed builds are invalid, never killed mutants.</summary>
internal static class EnduranceMutations
{
    internal const string Project = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors><NoWarn>CS0162;CS1591</NoWarn><PublishAot>false</PublishAot></PropertyGroup></Project>\n";
    internal const string Probe = """
        using SharpTestBench.Tools;
        var ids=args.Length==0||args[0]=="all"?EnduranceRules.MutationIds:new[]{args[0]};
        foreach(string id in ids)
        {
            string expected=id switch{"finished"=>"Unfinished campaign","path"=>"Unsafe portable path","hash"=>"Stale source hash","dirty"=>"Dirty source cannot be published","revision"=>"Source revision differs","coverage"=>"Invalid coverage evidence",_=>throw new ArgumentException("Unknown probe")};
            try
            {
                switch(id)
                {
                    case "finished":EnduranceRules.RequireFinished("running");break;
                    case "path":EnduranceRules.RequirePath("../outside");break;
                    case "hash":EnduranceRules.RequireHash(new string('a',64),new string('b',64));break;
                    case "dirty":EnduranceRules.RequireClean(true);break;
                    case "revision":EnduranceRules.RequireRevision(new string('a',40),new string('b',40));break;
                    case "coverage":EnduranceRules.ValidateCoverage(new("available",EnduranceRules.CoverageScope,2,1,0,1));break;
                }
            }
            catch(InvalidDataException error) when(error.Message==expected){continue;}
            catch(Exception){Console.WriteLine("UNRELATED_PROBE_FAILURE:"+id);return 2;}
            Console.WriteLine("ENDURANCE_MUTANT_DETECTED:"+id);return 1;
        }
        Console.WriteLine("ENDURANCE_PROBE_PASSED");return 0;
        """;
    internal static string Prepare(string snapshot, string directory, string? mutant)
    {
        Directory.CreateDirectory(directory);
        foreach (string file in new[] { "EnduranceRules.cs", "EnduranceModels.cs" })
        {
            string source = File.ReadAllText(Path.Combine(snapshot, "tools/SharpTestBench.Tools", file));
            if (file == "EnduranceRules.cs" && mutant is not null)
            {
                string marker = "// MUTATE:" + mutant; int end = source.IndexOf(marker, StringComparison.Ordinal);
                if (end < 0 || source.IndexOf(marker, end + marker.Length, StringComparison.Ordinal) >= 0) throw new InvalidDataException("Mutation anchor differs");
                int start = source.LastIndexOf("\n        if (", end, StringComparison.Ordinal);
                if (start < 0) throw new InvalidDataException("Mutation condition missing");
                source = source[..start] + "\n        if (false) " + source[end..];
            }
            File.WriteAllText(Path.Combine(directory, file), source, new UTF8Encoding(false));
        }
        File.WriteAllText(Path.Combine(directory, "Probe.csproj"), Project, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(directory, "Program.cs"), Probe, new UTF8Encoding(false));
        return Path.Combine(directory, "bin/Release/net10.0/Probe.dll");
    }
}
