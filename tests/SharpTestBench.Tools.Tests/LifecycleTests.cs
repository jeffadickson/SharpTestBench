using System.Text.Json;
using SharpTestBench.Tools;
using Xunit;
public sealed class LifecycleTests
{
    [Theory]
    [InlineData("normal", "", 0)]
    [InlineData("both", "body marker; cleanup marker", 1)]
    [InlineData("timeout", "exceeded 10", 1)]
    [InlineData("cleanup-timeout", "cleanup timed out", 1)]
    [InlineData("cancel-error", "cancellation marker", 1)]
    [InlineData("hang", "regression aborted", 1)]
    public void CleanupRequiresCompleteEvidence(string mode, string error, int exitCode)
    {
        var rows = new List<object> { new { Name = "Cleanup_A_Run", Status = mode == "normal" ? "passed" : "failed", Error = error } };
        if (mode != "hang") rows.Add(new { Name = "Cleanup_B_Verify", Status = "passed", Error = "" });
        using var evidence = JsonDocument.Parse(JsonSerializer.Serialize(rows));
        LifecycleCommands.CleanupEvidence(mode, exitCode, evidence.RootElement);
        Assert.Throws<InvalidDataException>(() => LifecycleCommands.CleanupEvidence(mode, exitCode == 0 ? 1 : 0, evidence.RootElement));
        using var empty = JsonDocument.Parse("[]");
        Assert.Throws<InvalidDataException>(() => LifecycleCommands.CleanupEvidence(mode, exitCode, empty.RootElement));
    }
    [Theory]
    [InlineData(false, false, 0, "passed", "", "test")]
    [InlineData(true, false, 1, "failed", "Test leaked tasks or callbacks", "test")]
    [InlineData(false, true, 1, "failed", "", "simulator-termination")]
    public void LifecycleRejectsFatalDiagnosticsAndWrongExit(bool negative, bool early, int exitCode, string status, string error, string name)
    {
        using var evidence = JsonDocument.Parse(JsonSerializer.Serialize(new[] { new { Name = name, Status = status, Error = error } }));
        Assert.True(LifecycleCommands.LifecycleEvidence(exitCode, "", evidence.RootElement, negative, early));
        Assert.False(LifecycleCommands.LifecycleEvidence(exitCode, "FATAL LIBRARY ERROR", evidence.RootElement, negative, early));
        Assert.False(LifecycleCommands.LifecycleEvidence(exitCode == 0 ? 1 : 0, "", evidence.RootElement, negative, early));
    }
    [Fact]
    public void WorkspaceContentionPreservesOriginalLock()
    {
        var directory = Path.Combine(Path.GetTempPath(), "stb-lock-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using (var owner = new WorkspaceLock(directory))
            {
                var path = Path.Combine(directory, ".sharptestbench-run.lock"); string ReadLock() { using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); using var reader = new StreamReader(input); return reader.ReadToEnd(); } var original = ReadLock();
                Assert.Contains("already in use", Assert.Throws<IOException>(() => new WorkspaceLock(directory)).Message);
                Assert.Equal(original, ReadLock());
            }
            Assert.False(File.Exists(Path.Combine(directory, ".sharptestbench-run.lock")));
        }
        finally { Directory.Delete(directory); }
    }
}
