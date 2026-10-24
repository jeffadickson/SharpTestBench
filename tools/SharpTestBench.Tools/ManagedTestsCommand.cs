using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
namespace SharpTestBench.Tools;

/// <summary>Discovers the current managed inventory and proves one isolated failing assertion cannot be reported as suite success.</summary>
internal static class ManagedTestsCommand
{
    internal sealed record Case(string Class,string Method,string Name);
    const string Target="ManagedTests.IntegerBoundaries";
    static bool IsTarget(Case item)=>item.Class=="ManagedTests"&&item.Method=="IntegerBoundaries"&&item.Name==Target;
    const int MaximumReportCharacters=16*1024*1024;
    static string Required(string? value,string field)
    {if(string.IsNullOrWhiteSpace(value)||value.Any(char.IsControl))throw new InvalidDataException("Managed case requires "+field);return value;}
    internal static HashSet<Case> Inventory(string text)
    {
        // xUnit appends terminal reset sequences even for its JSON listing. Strip
        // only actual ANSI bytes, not JSON-escaped characters in display names.
        text=Regex.Replace(text,@"\x1b\[[0-9;]*m","").Trim();if(text.Length>MaximumReportCharacters)throw new InvalidDataException("Managed discovery exceeds budget");
        using var document=StrictJson.Parse(text);if(document.RootElement.ValueKind!=JsonValueKind.Array)throw new InvalidDataException("Managed discovery requires an array");
        var cases=new HashSet<Case>();var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var row in document.RootElement.EnumerateArray())
        {
            string id=Required(row.GetProperty("ID").GetString(),"discovery ID");
            var item=new Case(Required(row.GetProperty("Class").GetString(),"class"),Required(row.GetProperty("Method").GetString(),"method"),Required(row.GetProperty("DisplayName").GetString(),"display name"));
            if(!ids.Add(id)||!cases.Add(item))throw new InvalidDataException("Duplicate managed discovery identity");
        }
        if(cases.Count==0||cases.Count(IsTarget)!=1)throw new InvalidDataException("Managed inventory lacks the mutation target");return cases;
    }
    internal static void Verify(string path,IReadOnlySet<Case> expected,bool mutation)
    {
        if(expected.Count==0||expected.Count(IsTarget)!=1)throw new InvalidDataException("Managed expected inventory needs one mutation target");
        using var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumReportCharacters});
        var document=XDocument.Load(reader);if(document.Root?.Name!="assemblies")throw new InvalidDataException("Managed report root differs");var assemblies=document.Root.Elements("assembly").ToArray();
        if(assemblies.Length!=1||assemblies[0].Attribute("errors")?.Value!="0"||assemblies[0].Attribute("not-run")?.Value!="0")throw new InvalidDataException("Managed run has assembly errors or unexecuted cases");
        // Bind every result to a direct collection of the sole assembly; orphaned
        // or nested lookalike rows cannot satisfy the discovered inventory.
        var testRows=assemblies[0].Elements("collection").SelectMany(collection=>collection.Elements("test")).ToArray();
        if(document.Descendants("assembly").Count()!=1||document.Descendants("test").Count()!=testRows.Length)throw new InvalidDataException("Managed results occur outside the assembly collections");
        // xUnit 3.2.2 XML adds backslash escaping after display-name formatting.
        // Encode expected names once; never permissively decode untrusted rows.
        // Raw control characters already reject in Required. The pinned sink is documented in TEST_TOOLING.md.
        var xmlExpected=expected.ToDictionary(item=>item with{Name=item.Name.Replace("\\","\\\\",StringComparison.Ordinal).Replace("\"","\\\"",StringComparison.Ordinal)},item=>item);
        var rows=new Dictionary<Case,string>();
        foreach(var row in testRows)
        {
            var item=new Case(Required((string?)row.Attribute("type"),"class"),Required((string?)row.Attribute("method"),"method"),Required((string?)row.Attribute("name"),"display name"));
            string result=(string?)row.Attribute("result")??"";
            if(!xmlExpected.ContainsKey(item)||!rows.TryAdd(item,result)||result is not ("Pass" or "Fail"))throw new InvalidDataException("Managed result identity/status differs from discovered inventory");
        }
        if(rows.Count!=expected.Count||assemblies[0].Attribute("total")?.Value!=expected.Count.ToString(System.Globalization.CultureInfo.InvariantCulture))throw new InvalidDataException("Incomplete managed result inventory");
        int failures=rows.Count(row=>row.Value=="Fail"),passes=rows.Count-failures;
        if(assemblies[0].Attribute("failed")?.Value!=failures.ToString(System.Globalization.CultureInfo.InvariantCulture)||assemblies[0].Attribute("passed")?.Value!=passes.ToString(System.Globalization.CultureInfo.InvariantCulture)||assemblies[0].Attribute("skipped")?.Value!="0")throw new InvalidDataException("Managed result counters differ from rows");
        // Display/class/method keys survive the isolated assembly's different file
        // path. Framework discovery IDs and XML run GUIDs do not, so are not paired.
        foreach(var row in rows)if(row.Value!=(mutation&&IsTarget(xmlExpected[row.Key])?"Fail":"Pass"))throw new InvalidDataException("Injected managed failure was not isolated to its target");
        if(mutation&&failures!=1)throw new InvalidDataException("One named managed failure is required");
    }
    public static void Execute(ToolContext context,string[] arguments)
    {
        if(arguments.Length!=0)throw new ArgumentException("managed-tests accepts no arguments");
        string project=context.At("tests/SharpTestBench.Tests"),build=context.At("build"),assembly=Path.Combine(project,"bin/Release/net10.0/SharpTestBench.Tests.dll");Directory.CreateDirectory(build);
        string completion=Path.Combine(build,"managed-validation.json");File.Delete(completion);
        // Execution expands theories; discover those same cases rather than one deferred method placeholder.
        var discovery=context.Run(["dotnet",assembly,"-preEnumerateTheories","-list","full/json"],capture:true,timeout:TimeSpan.FromSeconds(120));var inventory=Inventory(discovery.Output);
        StrictJson.Write(Path.Combine(build,"managed-inventory.json"),new{schemaVersion=1,cases=inventory.OrderBy(item=>item.Name,StringComparer.Ordinal).ToArray()});
        string baseline=Path.Combine(build,"managed-results.xml");File.Delete(baseline);context.Run(["dotnet",assembly,"-xml",baseline]);Verify(baseline,inventory,false);
        string probe=Path.Combine(build,"managed-failure-probe",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(probe);
        var projectDocument=XDocument.Parse(File.ReadAllText(Path.Combine(project,"SharpTestBench.Tests.csproj")));
        foreach(var reference in projectDocument.Descendants("ProjectReference"))reference.SetAttributeValue("Include",Path.GetFullPath(reference.Attribute("Include")!.Value,project));
        var properties=projectDocument.Root!.Element("PropertyGroup")!;
        // Keep the friend-assembly identity while isolating all bin/obj/source files.
        // New internal planner tests must compile in the copied negative control.
        properties.Add(new XElement("AssemblyName","SharpTestBench.Tests"));
        projectDocument.Save(Path.Combine(probe,"FailureProbe.csproj"));
        foreach(string file in Directory.EnumerateFiles(project,"*.cs"))File.Copy(file,Path.Combine(probe,Path.GetFileName(file)));
        foreach(string name in new[]{"array-reference.json","logic-reference.json","nine-state-reference.txt","packages.lock.json"})File.Copy(Path.Combine(project,name),Path.Combine(probe,name));
        string sourcePath=Path.Combine(probe,"Program.cs"),source=File.ReadAllText(sourcePath);const string original="new UInt<W8>(255).Value == 255";
        if(source.Split(original,StringSplitOptions.None).Length!=2)throw new InvalidDataException("Managed failure mutation anchor differs");File.WriteAllText(sourcePath,source.Replace(original,"new UInt<W8>(255).Value == 254",StringComparison.Ordinal),new UTF8Encoding(false));
        context.Run(["dotnet","build",Path.Combine(probe,"FailureProbe.csproj"),"-c","Release","--disable-build-servers","-m:1","-p:RestoreLockedMode=true"]);
        string failed=Path.Combine(probe,"injected-failure.xml");var result=context.Run(["dotnet",Path.Combine(probe,"bin/Release/net10.0/SharpTestBench.Tests.dll"),"-xml",failed],check:false);
        if(result.ExitCode!=1)throw new InvalidDataException("Managed mutation must fail through the test runner, not a crash or build error");Verify(failed,inventory,true);
        StrictJson.Write(completion,new{schemaVersion=1,status="verified",discoveredCases=inventory.Count,baselineFailures=0,mutationFailures=1,mutationTarget=Target,probeDirectory=probe,scope="Assertion-failure harness control; not production-code mutation strength"});
        Console.WriteLine($"MANAGED_PASS {inventory.Count} discovered cases; one named injected failure; remaining {inventory.Count-1} passed");
    }
}