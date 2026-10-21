using System.Text.Json;
try
{
    var text = Console.In.ReadToEnd();
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    if (args is ["json-file"])
    {
        using var payload = JsonInput.Parse(text);
        using var document = JsonInput.Parse(JsonInput.Read(payload.RootElement.GetProperty("path").GetString()!));
        Console.WriteLine(JsonSerializer.Serialize(new { valid = true }));
    }
    else if (args is ["fingerprint"])
    {
        using var document = JsonInput.Parse(text);
        var data = document.RootElement;
        var fingerprints = InputEvidence.Files(data.GetProperty("paths").Deserialize<string[]>()!);
        if (data.TryGetProperty("before", out var before)) InputEvidence.Verify(before.Deserialize<InputFingerprint[]>()!, fingerprints);
        Console.WriteLine(JsonSerializer.Serialize(new { valid = true, fingerprints }));
    }
    else if (args is ["evidence"])
    {
        using var document = JsonInput.Parse(text);
        var data = document.RootElement;
        EvidenceWriter.Write(data.GetProperty("path").GetString()!, data.GetProperty("value"));
        Console.WriteLine(JsonSerializer.Serialize(new { valid = true }));
    }
    else if (args is ["plan"])
    {
        var config = JsonInput.Manifest(text);
        ManifestValidation.Validate(config);
        var arguments = CompilePlan.Arguments(Directory.GetCurrentDirectory(), config, "top.vvp");
        Console.WriteLine(JsonSerializer.Serialize(new { valid = true, arguments, environment = CompilePlan.Environment(config) }));
    }
    else if (args is ["json"])
    {
        using var document = JsonInput.Parse(text);
        Console.WriteLine(JsonSerializer.Serialize(new { valid = true }));
    }
    else if (args is ["process"])
    {
        using var doc = JsonDocument.Parse(text);
        var data = doc.RootElement;
        var environment = data.TryGetProperty("environment", out var env) ? env.Deserialize<Dictionary<string,string>>() : null;
        await ProcessExecution.Run(data.GetProperty("executable").GetString()!,
            data.GetProperty("arguments").Deserialize<string[]>()!, data.GetProperty("directory").GetString()!,
            environment, data.GetProperty("phase").GetString()!, data.GetProperty("timeout").GetInt32(),
            data.TryGetProperty("maxLogBytes", out var budget) ? budget.GetInt32() : 16 * 1024 * 1024);
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
            ExpectedNames: root.GetProperty("names").Deserialize<string[]>(),
            ExpectedSeeds: root.TryGetProperty("expectedSeeds", out var seeds) ? seeds.Deserialize<Dictionary<string, ulong>>() : null);
        var result = ResultAttestation.Verify(root.GetProperty("rows"), config);
        Console.WriteLine(JsonSerializer.Serialize(new { valid = true, passed = result.Passed, failed = result.Failed }));
    }
    else
    {
        var config = JsonInput.Manifest(text);
        ManifestValidation.Validate(config);
        Console.WriteLine(JsonSerializer.Serialize(new { valid = true }));
    }
}
catch (Exception error)
{
    Console.WriteLine(JsonSerializer.Serialize(new { valid = false, error = error.Message, kind = error is RunnerProcessException process ? process.Kind : error is TimeoutException ? "timeout" : error.GetType().Name }));
}
