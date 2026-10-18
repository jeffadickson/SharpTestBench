using System.Text.Json;
try
{
    var text = Console.In.ReadToEnd();
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    if (args is ["process"])
    {
        using var doc = JsonDocument.Parse(text);
        var data = doc.RootElement;
        var environment = data.TryGetProperty("environment", out var env) ? env.Deserialize<Dictionary<string,string>>() : null;
        await ProcessExecution.Run(data.GetProperty("executable").GetString()!,
            data.GetProperty("arguments").Deserialize<string[]>()!, data.GetProperty("directory").GetString()!,
            environment, data.GetProperty("phase").GetString()!, data.GetProperty("timeout").GetInt32());
        Console.WriteLine(JsonSerializer.Serialize(new { valid = true }));
    }
    else if (args is ["workspace"])
    {
        using var doc = JsonDocument.Parse(text);
        var data = doc.RootElement;
        using var lease = RunWorkspace.Acquire(data.GetProperty("directory").GetString()!);
        if (data.GetProperty("contend").GetBoolean())
        {
            using var second = RunWorkspace.Acquire(data.GetProperty("directory").GetString()!);
        }
        else lease.ClearArtifacts();
        Console.WriteLine(JsonSerializer.Serialize(new { valid = true }));
    }
    else if (args is ["output"])
    {
        using var doc = JsonDocument.Parse(text);
        var data = doc.RootElement;
        var output = ManifestValidation.ResolveOutput(data.GetProperty("root").GetString()!, data.GetProperty("output").GetString()!);
        Console.WriteLine(JsonSerializer.Serialize(new { valid = true, output }));
    }
    else if (args is ["results"])
    {
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        var config = new RunManifest(1, "icarus", "top", ["top.sv"], "a.dll", "h.so", "b.vpi", "g.so", "sdk", "out",
            ExpectedTests: root.GetProperty("count").GetInt32(),
            ExpectedNames: root.GetProperty("names").Deserialize<string[]>());
        var result = ResultAttestation.Verify(root.GetProperty("rows"), config);
        Console.WriteLine(JsonSerializer.Serialize(new { valid = true, passed = result.Passed, failed = result.Failed }));
    }
    else
    {
        var config = JsonSerializer.Deserialize<RunManifest>(text, options) ?? throw new InvalidDataException("Empty manifest");
        ManifestValidation.Validate(config);
        Console.WriteLine(JsonSerializer.Serialize(new { valid = true }));
    }
}
catch (Exception error)
{
    Console.WriteLine(JsonSerializer.Serialize(new { valid = false, error = error.Message, kind = error is RunnerProcessException process ? process.Kind : error is TimeoutException ? "timeout" : error.GetType().Name }));
}
