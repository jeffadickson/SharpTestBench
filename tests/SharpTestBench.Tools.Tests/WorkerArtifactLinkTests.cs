using SharpTestBench.Tools;
using Xunit;

public sealed class WorkerArtifactLinkTests
{
    [Theory(Skip = "Unix symbolic-link semantics", SkipWhen = nameof(IsWindows))]
    [InlineData("root")]
    [InlineData("file")]
    [InlineData("directory")]
    public void LinkedArtifactsCannotBeHashedAsOwnedFiles(string kind)
    {
        var parent = Path.Combine(Path.GetTempPath(), "worker artifact links " + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(parent, "worker"); var outside = Path.Combine(parent, "outside");
        Directory.CreateDirectory(root); Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "payload"), "outside");
        var link = kind == "root" ? Path.Combine(parent, "alias") : Path.Combine(root, "link");
        if (kind == "file") File.CreateSymbolicLink(link, Path.Combine(outside, "payload"));
        else Directory.CreateSymbolicLink(link, kind == "root" ? root : outside);
        try
        {
            Assert.Throws<InvalidDataException>(() => WorkerArtifact.Files(kind == "root" ? link : root));
            Assert.Equal("outside", File.ReadAllText(Path.Combine(outside, "payload")));
        }
        finally
        {
            if (kind == "file") File.Delete(link); else Directory.Delete(link);
            Directory.Delete(parent, true);
        }
    }
    public static bool IsWindows => OperatingSystem.IsWindows();
}
