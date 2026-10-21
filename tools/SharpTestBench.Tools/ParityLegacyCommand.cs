using System.Globalization;
using System.Text.Json;

namespace SharpTestBench.Tools;

internal static class ParityLegacyCommand
{
    internal static RunManifest Read(Dictionary<string,object> manifest) =>
        JsonInput.Manifest(JsonSerializer.Serialize(manifest));

    internal static string[] Build(Dictionary<string,object> manifest,string work,ulong seed,string? exact)
    {
        var config=Read(manifest);
        HdlCompileValidation.Validate(config);
        if (config.Simulator != "icarus" || config.SchemaVersion != 1) throw new InvalidDataException("Parity requires an Icarus v1 manifest.");
        if (config.Iverilog is not null || config.Vvp is not null || config.TimeoutSeconds != 120 || config.MaxLogBytes != 16*1024*1024)
            throw new InvalidDataException("Parity bridge does not support custom tools, timeout or log limits.");
        if (config.Seed != seed) throw new InvalidDataException("Parity seed differs from manifest seed.");
        if (exact is not null && config.TestNames is { } names && (names.Length!=1 || names[0]!=exact))
            throw new InvalidDataException("Parity selection differs from manifest testNames.");
        string Resolve(string path) => Path.GetFullPath(path,work);
        var arguments=new List<string>(ToolContext.Self) { "run","--sim","icarus","--no-build","--assembly",Resolve(config.Assembly),
            "--host",Resolve(config.NativeHost),"--top",config.Top,"--entry-type",config.EntryType ?? "Entry, "+Path.GetFileNameWithoutExtension(config.Assembly),
            "--work-dir",Path.Combine(work,"legacy"),"--label","legacy","--seed",seed.ToString(CultureInfo.InvariantCulture),"--hdl-standard",config.HdlStandard };
        foreach (var source in CompilePlan.Sources(work,config)) arguments.AddRange(["--source",source]);
        foreach (var include in config.IncludeDirectories ?? []) arguments.AddRange(["--include-dir",Resolve(include)]);
        foreach (var (name,value) in (config.Defines ?? []).OrderBy(pair=>pair.Key,StringComparer.Ordinal)) arguments.AddRange(["--define",name+(value is null ? "" : "="+value)]);
        foreach (var (name,value) in (config.Parameters ?? []).OrderBy(pair=>pair.Key,StringComparer.Ordinal)) arguments.AddRange(["--parameter",name+"="+value]);
        foreach (var selected in exact is null ? config.TestNames ?? [] : new[] { exact }) arguments.AddRange(["--exact",selected]);
        return arguments.ToArray();
    }

    internal static Dictionary<string,object> BindTools(ToolContext context,Dictionary<string,object> manifest,string work,Dictionary<string,string?> left,Dictionary<string,string?> right)
    {
        var config=Read(manifest);
        string Tool(string key,string fallback,string? configured)
        {
            var selected=configured ?? (left.TryGetValue(key,out var value) ? value ?? fallback : Environment.GetEnvironmentVariable(key) ?? fallback);
            return selected.Contains(Path.DirectorySeparatorChar) || selected.Contains(Path.AltDirectorySeparatorChar) ? Path.GetFullPath(selected,configured is null ? context.Root : work) : selected;
        }
        var compiler=Tool("CS_IVERILOG","iverilog",config.Iverilog);var simulator=Tool("CS_VVP","vvp",config.Vvp);
        left["CS_IVERILOG"]=compiler;left["CS_VVP"]=simulator;left["CS_DOTNET_ROOT"]=Path.GetFullPath(config.DotnetRoot,work);
        left["CS_IVL_ROOT"]=null;right["CS_IVL_ROOT"]=null;
        left["CS_COMPILE_TIMEOUT"]=null;right["CS_COMPILE_TIMEOUT"]=null;
        return new Dictionary<string,object>(manifest) { ["iverilog"]=compiler,["vvp"]=simulator };
    }

    internal static void ValidateAdapter(RunManifest config,string work,CocotbAdapter adapter)
    {
        var comparer=OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        bool Same(string left,string right) => comparer.Equals(RunnerManifestCommand.RealPath(Path.GetFullPath(left,work)),RunnerManifestCommand.RealPath(Path.GetFullPath(right,work)));
        var entry=adapter.NativeEntry();
        if (!Same(config.IcarusBootstrap,adapter.Bootstrap) || !Same(config.GpiLibrary,entry.Library) || config.GpiEntryPoint!=entry.Entry)
            throw new InvalidDataException("Parity manifest adapter differs from the selected installed adapter.");
        static (string Path,string Entry) Split(string? value)
        {
            var index=value?.LastIndexOf(':') ?? -1;
            if (index<=0 || index==value!.Length-1) throw new InvalidDataException("Invalid GPI implementation.");
            return (value[..index],value[(index+1)..]);
        }
        var actual=Split(config.GpiImpl);var installed=Split(adapter.Implementation);
        if (!Same(actual.Path,installed.Path) || actual.Entry!=installed.Entry) throw new InvalidDataException("Parity manifest implementation differs from the selected installed adapter.");
    }
}
