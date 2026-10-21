namespace SharpTestBench.Tools;

internal static class LegacyCompileOptions
{
    internal static RunManifest Create(Options options,string simulator,string top,string[] sources)
    {
        string[] names=["--hdl-standard","--include-dir","--define","--parameter"];
        if (simulator != "icarus" && names.Any(options.Has)) throw new ArgumentException("Compiler options currently require Icarus.");
        var defines=new Dictionary<string,string?>(StringComparer.Ordinal);
        foreach (var raw in options.All("--define"))
        {
            var parts=raw.Split('=',2);
            if (!defines.TryAdd(parts[0],parts.Length==1 ? null : parts[1])) throw new ArgumentException("Duplicate define: "+parts[0]);
        }
        var parameters=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach (var raw in options.All("--parameter"))
        {
            var parts=raw.Split('=',2);
            if (parts.Length!=2) throw new ArgumentException("Parameter requires NAME=value.");
            if (!parameters.TryAdd(parts[0],parts[1])) throw new ArgumentException("Duplicate parameter: "+parts[0]);
        }
        var config=new RunManifest(1,"icarus",top,sources,"","","","","", "",
            HdlStandard:options.Get("--hdl-standard","2012")!,IncludeDirectories:options.All("--include-dir"),Defines:defines,Parameters:parameters);
        HdlCompileValidation.Validate(config);
        return config with { IncludeDirectories=config.IncludeDirectories!.Select(Path.GetFullPath).ToArray() };
    }
}
