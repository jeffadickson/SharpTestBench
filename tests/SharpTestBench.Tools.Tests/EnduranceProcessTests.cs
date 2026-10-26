using System.Diagnostics;
using SharpTestBench.Tools;
using Xunit;

/// <summary>Real PowerShell children verify byte limits, process deadlines and shell-free argument handling.</summary>
public sealed class EnduranceProcessTests
{
    [Fact]public async Task ExitedParentCannotLeaveInheritedReadersBlocked()
    {
        string gate=Path.Combine(Path.GetTempPath(),"stb-endurance-pipe-"+Guid.NewGuid().ToString("N"));
        string fixture=Path.Combine(ToolContext.FindRoot(),"tests/RunnerProcessFixture/bin/Release/net10.0/RunnerProcessFixture.dll");
        using var rescue=new CancellationTokenSource();bool rescued=false;
        var watchdog=Task.Run(async()=>
        {
            try{await Task.Delay(TimeSpan.FromSeconds(20),rescue.Token);rescued=true;File.WriteAllText(gate,"watchdog");}
            catch(OperationCanceledException)when(rescue.IsCancellationRequested){}
        },TestContext.Current.CancellationToken);
        try
        {
            var clock=Stopwatch.StartNew();var result=EnduranceProcess.Run("dotnet",[fixture,"--capture-parent",gate],Path.GetTempPath(),TimeSpan.FromSeconds(10),4096,cancellation:TestContext.Current.CancellationToken);
            Assert.Contains("parent exited",result.Output);Assert.True(File.Exists(gate+".ready"));
            Assert.True(result.Settled);Assert.True(result.Truncated);Assert.False(result.Passed);
            Assert.True(clock.Elapsed<TimeSpan.FromSeconds(18));Assert.False(rescued);
        }
        finally
        {
            File.WriteAllText(gate,"cleanup");rescue.Cancel();await watchdog;
            if(File.Exists(gate+".pid"))
            {
                try{using var owned=Process.GetProcessById(int.Parse(File.ReadAllText(gate+".pid")));Assert.True(owned.WaitForExit(5000));}catch(ArgumentException){}
            }
            foreach(string path in new[]{gate,gate+".ready",gate+".pid"})File.Delete(path);
        }
    }
    [Fact]public void NoisyChildDrainsButOnlyRetainsTheConfiguredPrefix()
    {
        var result=EnduranceProcess.Run("pwsh",["-NoProfile","-Command","[Console]::Out.Write(('x' * 20000)); [Console]::Error.Write(('y' * 20000))"],Path.GetTempPath(),TimeSpan.FromSeconds(20),4096,cancellation:TestContext.Current.CancellationToken);
        Assert.True(result.Succeeded);Assert.False(result.Passed);Assert.True(result.Truncated);Assert.Equal(4096,result.Stdout.Length);Assert.Equal(4096,result.Stderr.Length);Assert.True(result.Settled);
    }
    [Fact]public void ChildDeadlineCannotWaitForAWholeSleepingWorkload()
    {
        var clock=Stopwatch.StartNew();var result=EnduranceProcess.Run("pwsh",["-NoProfile","-Command","Start-Sleep -Seconds 60"],Path.GetTempPath(),TimeSpan.FromSeconds(1),4096,cancellation:TestContext.Current.CancellationToken);
        Assert.True(result.TimedOut);Assert.False(result.Succeeded);Assert.True(clock.Elapsed<TimeSpan.FromSeconds(20));
    }
    [Fact]public void InvalidProcessBoundsRejectBeforeLaunching()
    {
        Assert.Throws<ArgumentException>(()=>EnduranceProcess.Run("not executed",[],Path.GetTempPath(),TimeSpan.Zero,cancellation:TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(()=>EnduranceProcess.Run("not executed",[],Path.GetTempPath(),TimeSpan.FromSeconds(1),1,cancellation:TestContext.Current.CancellationToken));
    }
}
