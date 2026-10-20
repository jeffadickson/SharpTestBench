using SharpTestBench.Tools;
using Xunit;

public sealed class WorkerLinkTests
{
    [Theory(Skip = "Unix symbolic-link semantics", SkipWhen = nameof(IsWindows))]
    [InlineData("output", "tools")]
    [InlineData("output", "runner")]
    [InlineData("rid", "tools")]
    [InlineData("rid", "runner")]
    [InlineData("leaf", "tools")]
    [InlineData("leaf", "runner")]
    public void LinkedPublishComponentsCannotRedirectWrites(string component, string worker)
    {
        var parent = Path.Combine(Path.GetTempPath(), "worker links " + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(parent, "checkout"); var target = Path.Combine(parent, "outside");
        Directory.CreateDirectory(Path.Combine(root, "build", "workers")); Directory.CreateDirectory(target);
        string link;
        if (component == "output") link = Path.Combine(root, "build", "workers", "linked");
        else if (component == "rid") link = Path.Combine(root, "build", "workers", "linux-x64");
        else { Directory.CreateDirectory(Path.Combine(root, "build", "workers", "linux-x64")); link = Path.Combine(root, "build", "workers", "linux-x64", worker); }
        Directory.CreateSymbolicLink(link, target);
        try
        {
            Assert.Throws<ArgumentException>(() => WorkerPublishCommand.Plan(root, "linux-x64", worker, "linux-x64", component == "output" ? "build/workers/linked" : null));
            Assert.Empty(Directory.GetFileSystemEntries(target));
        }
        finally { Directory.Delete(link); Directory.Delete(parent, true); }
    }
    public static bool IsWindows => OperatingSystem.IsWindows();
}
