using System.Text.RegularExpressions;

internal static class HdlCompileValidation
{
    internal static void Validate(RunManifest config)
    {
        if (config.HdlStandard is not ("2012" or "2005-sv" or "2005" or "2001" or "1995"))
            throw new InvalidDataException("Unsupported hdlStandard.");
        if (config.IncludeDirectories is { } includes)
        {
            if (includes.Length > 256) throw new InvalidDataException("Too many include directories.");
            foreach (var include in includes) Required("includeDirectory",include);
        }
        ValidateMap(config.Defines,"define",true);
        ValidateMap(config.Parameters,"parameter",false);
    }
    static void ValidateMap<T>(Dictionary<string,T>? values,string kind,bool allowNull)
    {
        if (values is null) return;
        if (values.Count > 256) throw new InvalidDataException("Too many "+kind+" options.");
        foreach (var (name,raw) in values)
        {
            if (!Regex.IsMatch(name,@"\A[A-Za-z_][A-Za-z0-9_$]*\z")) throw new InvalidDataException("Invalid "+kind+" name: "+name);
            var value=raw as string;
            if (value is null && allowNull) continue;
            Required(kind+" value",value);
        }
    }
    static void Required(string name,string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
            throw new InvalidDataException(name+" must be nonempty and contain no control characters.");
    }
}
