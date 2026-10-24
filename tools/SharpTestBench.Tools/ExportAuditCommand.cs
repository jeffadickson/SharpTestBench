using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace SharpTestBench.Tools;

/// <summary>Checks a bounded literal-__all__ exposure review against pinned source, without importing cocotb.</summary>
internal static class ExportAuditCommand
{
    // Python owns parsing its syntax; C# owns discovery, containment, review policy and evidence.
    // The bounded grammar permits one literal declaration and read-only iteration.
    // Calls, aliases, chained writes and direct mutations reject; no import executes.
    // Reflective/dynamic runtime namespace behavior remains outside this inventory.
    const string ParseExports = "import ast,json,sys\ntree=ast.parse(sys.stdin.read())\nparents={c:p for p in ast.walk(tree) for c in ast.iter_child_nodes(p)}\nrefs=[n for n in ast.walk(tree) if isinstance(n,ast.Name) and n.id=='__all__' and isinstance(n.ctx,ast.Load)]\nif any(not(isinstance(parents[n],ast.For) and parents[n].iter is n) for n in refs): raise ValueError('literal __all__ supports read-only iteration, not calls, aliases or mutation')\nwrites=[n for n in ast.walk(tree) if isinstance(n,ast.Name) and n.id=='__all__' and isinstance(n.ctx,(ast.Store,ast.Del))]\nif len(writes)!=1: raise ValueError('one unconditional literal __all__ assignment required')\nvalues=[]\nfor n in tree.body:\n if isinstance(n,ast.Assign) and len(n.targets)==1 and isinstance(n.targets[0],ast.Name) and n.targets[0].id=='__all__': values.append(ast.literal_eval(n.value))\nif len(values)!=1 or not isinstance(values[0],(list,tuple)) or any(not isinstance(x,str) for x in values[0]): raise ValueError('one literal string __all__ required')\nprint(json.dumps(values[0]))";
    internal static string SourcePath(string sourceRoot,string module)
    {
        if(!Regex.IsMatch(module,@"\Acocotb(?:\.[A-Za-z][A-Za-z0-9_]*)*\z",RegexOptions.CultureInvariant))
            throw new InvalidDataException("Invalid public module identity");
        string relative=module=="cocotb"?"cocotb/__init__.py":module.Replace('.', '/')+".py";
        return AuditCommand.EvidencePath(sourceRoot,relative);
    }
    internal static IReadOnlyList<string> Exports(string path)
    {
        var start=new ProcessStartInfo(Environment.GetEnvironmentVariable("CS_PYTHON")??"python")
        {RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
        start.ArgumentList.Add("-c");start.ArgumentList.Add(ParseExports);
        using var process=Process.Start(start)??throw new IOException("Cannot start Python AST adapter");
        var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        process.StandardInput.Write(File.ReadAllText(path));process.StandardInput.Close();
        if(!process.WaitForExit(30000)){process.Kill(entireProcessTree:true);throw new TimeoutException("Export AST parser deadline");}
        if(process.ExitCode!=0)throw new InvalidDataException("Literal export parse failed: "+error.GetAwaiter().GetResult());
        using var document=StrictJson.Parse(output.GetAwaiter().GetResult());
        var exports=document.RootElement.EnumerateArray().Select(value=>value.GetString()!).ToArray();
        if(exports.Length==0||exports.Any(string.IsNullOrWhiteSpace)||exports.Distinct(StringComparer.Ordinal).Count()!=exports.Length)
            throw new InvalidDataException("Exports must be nonempty distinct names");
        return exports;
    }
    internal static object Validate(string repository,string source,string reviewPath)
    {
        string sourceRoot=AuditCommand.SourceDirectory(source);
        using var upstream=StrictJson.Read(Path.Combine(repository,"third_party/UPSTREAM.json"));
        using var document=StrictJson.Read(reviewPath);
        var review=document.RootElement;
        if(review.GetProperty("schema_version").GetInt32()!=1 || review.GetProperty("source_revision").GetString()!=upstream.RootElement.GetProperty("commit").GetString())
            throw new InvalidDataException("Export review schema/revision differs from pin");
        var exported=new Dictionary<string,HashSet<string>>(StringComparer.Ordinal);
        foreach(var hash in review.GetProperty("source_hashes").EnumerateObject())
        {
            string path=SourcePath(sourceRoot,hash.Name);
            if(CiCommands.Hash(path)!=hash.Value.GetString())throw new InvalidDataException("Export source hash changed: "+hash.Name);
            if(!exported.TryAdd(hash.Name,Exports(path).ToHashSet(StringComparer.Ordinal)))throw new InvalidDataException("Duplicate source module");
        }
        if(exported.Count==0)throw new InvalidDataException("No source modules");
        string[] statuses=["idiomatic-equivalent","intentional-difference","gap","internal-adapter","deprecated-omission"];
        var seen=new HashSet<(string Module,string Export)>();
        foreach(var entry in review.GetProperty("reviews").EnumerateArray())
        {
            string module=entry.GetProperty("module").GetString()!,name=entry.GetProperty("export").GetString()!;
            if(!exported.TryGetValue(module,out var names)||!names.Contains(name)||!seen.Add((module,name)))throw new InvalidDataException("Unknown or duplicate public export");
            if(!statuses.Contains(entry.GetProperty("classification").GetString())||string.IsNullOrWhiteSpace(entry.GetProperty("reason").GetString()))throw new InvalidDataException("Missing exposure decision");
            var evidence=entry.GetProperty("evidence");if(evidence.GetArrayLength()==0)throw new InvalidDataException("Exposure evidence missing");
            foreach(var item in evidence.EnumerateArray())
            {
                string? anchor=item.GetProperty("contains").GetString();
                if(string.IsNullOrWhiteSpace(anchor))throw new InvalidDataException("Exposure anchor missing");
                string path=AuditCommand.EvidencePath(repository,item.GetProperty("path").GetString());
                if(!File.Exists(path)||!File.ReadAllText(path).Contains(anchor,StringComparison.Ordinal))throw new InvalidDataException("Stale exposure evidence");
            }
        }
        if(seen.Count==0)throw new InvalidDataException("No reviewed exports");
        return new {schemaVersion=1,literalExports=exported.Sum(module=>module.Value.Count),reviewedExports=seen.Count,
            unreviewedExports=exported.Sum(module=>module.Value.Count)-seen.Count,
            note="Bounded literal __all__ review only. Other modules, dynamic names and runtime import behavior are unmeasured; counts are not parity percentages."};
    }
    internal static void Execute(ToolContext context,string[] arguments)
    {
        var options=new Options(arguments);options.Only("--source");
        Console.WriteLine(JsonSerializer.Serialize(Validate(context.Root,options.Require("--source"),context.At("docs/UPSTREAM_EXPORT_REVIEWS.json"))));
    }
}