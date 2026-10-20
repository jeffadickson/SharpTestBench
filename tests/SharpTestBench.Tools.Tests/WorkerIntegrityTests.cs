using SharpTestBench.Tools;
using Xunit;

public sealed class WorkerIntegrityTests
{
    [Theory]
    [InlineData("core.bin", "managed")]
    [InlineData("core.bin", "runtime")]
    [InlineData("core.bin", "removed")]
    [InlineData("core.bin", "extra")]
    [InlineData("core.bin", "identity")]
    [InlineData("codec.dll", "managed")]
    [InlineData("codec.dll", "runtime")]
    [InlineData("codec.dll", "removed")]
    [InlineData("codec.dll", "extra")]
    [InlineData("codec.dll", "identity")]
    [InlineData("config.json", "managed")]
    [InlineData("config.json", "runtime")]
    [InlineData("config.json", "removed")]
    [InlineData("config.json", "extra")]
    [InlineData("config.json", "identity")]
    [InlineData("native.so", "managed")]
    [InlineData("native.so", "runtime")]
    [InlineData("native.so", "removed")]
    [InlineData("native.so", "extra")]
    [InlineData("native.so", "identity")]
    [InlineData("message.resources", "managed")]
    [InlineData("message.resources", "runtime")]
    [InlineData("message.resources", "removed")]
    [InlineData("message.resources", "extra")]
    [InlineData("message.resources", "identity")]
    [InlineData("a file.bin", "managed")]
    [InlineData("a file.bin", "runtime")]
    [InlineData("a file.bin", "removed")]
    [InlineData("a file.bin", "extra")]
    [InlineData("a file.bin", "identity")]
    [InlineData("lambda.bin", "managed")]
    [InlineData("lambda.bin", "runtime")]
    [InlineData("lambda.bin", "removed")]
    [InlineData("lambda.bin", "extra")]
    [InlineData("lambda.bin", "identity")]
    [InlineData("nested-name.dat", "managed")]
    [InlineData("nested-name.dat", "runtime")]
    [InlineData("nested-name.dat", "removed")]
    [InlineData("nested-name.dat", "extra")]
    [InlineData("nested-name.dat", "identity")]
    public void CompleteFileIntegrityIsRequired(string component, string mutation)
    {
        var root = Path.Combine(Path.GetTempPath(), "worker integrity " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var plan = new WorkerPublish("tools", "linux-x64", "self-contained-readytorun", root, []);
            File.WriteAllText(Path.Combine(root, "SharpTestBench.Tools"), "apphost");
            File.WriteAllText(Path.Combine(root, "SharpTestBench.Tools.dll"), "managed-code");
            Directory.CreateDirectory(Path.Combine(root, "runtime"));
            var path = Path.Combine(root, "runtime", component); File.WriteAllText(path, "runtime-code");
            WorkerArtifact.Write(plan); WorkerArtifact.Verify(plan);
            switch (mutation)
            {
                case "managed": File.AppendAllText(Path.Combine(root, "SharpTestBench.Tools.dll"), "changed"); break;
                case "runtime": File.AppendAllText(path, "changed"); break;
                case "removed": File.Delete(path); break;
                case "extra": File.WriteAllText(Path.Combine(root, "extra.bin"), "extra"); break;
                case "identity": plan = plan with { Rid = "linux-arm64" }; break;
                default: throw new InvalidOperationException(mutation);
            }
            Assert.Throws<InvalidDataException>(() => WorkerArtifact.Verify(plan));
        }
        finally { Directory.Delete(root, true); }
    }
}
