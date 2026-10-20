using System.Text.Json.Nodes;
using SharpTestBench.Tools;
using Xunit;

public sealed class WorkerArtifactJsonTests
{
    [Theory]
    [InlineData("array")]
    [InlineData("null")]
    [InlineData("number")]
    [InlineData("hash-number")]
    [InlineData("unknown-field")]
    [InlineData("duplicate-root")]
    [InlineData("duplicate-file")]
    public void RejectsMalformedArtifactMetadata(string mutation)
    {
        var root = Path.Combine(Path.GetTempPath(), "worker json " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var plan = new WorkerPublish("runner", "linux-x64", "native-aot", root, []);
            File.WriteAllText(Path.Combine(root, "SharpTestBench.Runner"), "apphost");
            WorkerArtifact.Write(plan); WorkerArtifact.Verify(plan);
            var path = Path.Combine(root, "worker-build.json");
            var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            switch (mutation)
            {
                case "array": node["files"] = new JsonArray(); break;
                case "null": node["files"] = null; break;
                case "number": node["files"] = 1; break;
                case "hash-number": node["files"]!["SharpTestBench.Runner"] = 1; break;
                case "unknown-field": node["unexpected"] = true; break;
            }
            var text = node.ToJsonString();
            if (mutation == "duplicate-root") text = text.Insert(1, "\"schemaVersion\":1,");
            if (mutation == "duplicate-file") text = text.Replace("\"files\":{", "\"files\":{\"SharpTestBench.Runner\":\"duplicate\",");
            File.WriteAllText(path, text);
            Assert.Throws<InvalidDataException>(() => WorkerArtifact.Verify(plan));
        }
        finally { Directory.Delete(root, true); }
    }
}
