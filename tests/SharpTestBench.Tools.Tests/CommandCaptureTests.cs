using System.Diagnostics;
using System.Text.Json;
using SharpTestBench.Tools;
using Xunit;

[Collection("Parity process environment")]
public sealed class CommandCaptureTests
{
    static string Python=>Environment.GetEnvironmentVariable("CS_PYTHON")??"python";
    static CommandResult Run(string script,TimeSpan? timeout=null,bool check=false)=>new CommandRunner().Run([Python,"-c",script],Environment.CurrentDirectory,capture:true,check:check,timeout:timeout??TimeSpan.FromSeconds(20));
    [Fact] public void StdoutOverflowIsBoundedAndTerminatesCapturedChild()
    {
        var error=Assert.Throws<CommandCaptureLimitException>(()=>Run("import os; [os.write(1,b'x'*1048576) for _ in range(17)]"));
        Assert.Equal(BoundedCapture.MaximumBytes,error.Diagnostics.Output.Length);Assert.True(error.Diagnostics.StdoutLimited);
        Assert.False(error.Diagnostics.StderrLimited);Assert.True(error.Diagnostics.Settled);
    }
    [Fact] public void StderrHasItsOwnCaptureBudget()
    {
        var error=Assert.Throws<CommandCaptureLimitException>(()=>Run("import os; [os.write(2,b'e'*1048576) for _ in range(17)]"));
        Assert.Equal(BoundedCapture.MaximumBytes,error.Diagnostics.Error.Length);Assert.True(error.Diagnostics.StderrLimited);
        Assert.False(error.Diagnostics.StdoutLimited);Assert.True(error.Diagnostics.Settled);
    }
    [Fact] public void ImmediateExitOverflowCannotReturnTruncatedSuccess()
    {
        for(int attempt=0;attempt<2;attempt++)
            Assert.Throws<CommandCaptureLimitException>(()=>Run("import os;os.write(1,b'x'*17825792)"));
    }
    [Fact] public void SimultaneousStreamsDrainWithoutHidingNonzeroExit()
    {
        var result=Run("import os,threading,sys; t=threading.Thread(target=lambda:os.write(1,b'o'*2097152));t.start();os.write(2,b'e'*2097152);t.join();sys.exit(7)");
        Assert.Equal(7,result.ExitCode);Assert.Equal(2097152,result.Output.Length);Assert.Equal(2097152,result.Error.Length);
    }
    // Capture semantics are checked after a controlled child publishes readiness,
    // with startup margin and an ownership-bound process termination assertion.
    [Fact] public void TimeoutRetainsBothPartialStreamsAndSettlesCaptures()
    {
        string ownership=Path.Combine(Path.GetTempPath(),"stb-capture-deadline-"+Guid.NewGuid().ToString("N"));
        string fixture=Path.Combine(ToolContext.FindRoot(),"tests/RunnerProcessFixture/bin/Release/net10.0/RunnerProcessFixture.dll");
        Process? Owned()
        {
            if(!File.Exists(ownership))return null;
            string[] fields=File.ReadAllLines(ownership);
            Process process;try{process=Process.GetProcessById(int.Parse(fields[0]));}catch(ArgumentException){return null;}
            if(!process.HasExited&&process.StartTime.ToUniversalTime().Ticks!=long.Parse(fields[1])){process.Dispose();throw new InvalidOperationException("Capture PID ownership changed");}
            return process;
        }
        try
        {
            var timer=Stopwatch.StartNew();
            var error=Assert.Throws<CommandDeadlineException>(()=>new CommandRunner().Run(["dotnet",fixture,"--capture-deadline",ownership],Environment.CurrentDirectory,capture:true,check:false,timeout:TimeSpan.FromSeconds(10)));
            Assert.Contains("before stdout",error.Diagnostics.Output);Assert.Contains("before stderr",error.Diagnostics.Error);
            Assert.True(error.Diagnostics.Settled);Assert.False(error.Diagnostics.StdoutLimited);Assert.False(error.Diagnostics.StderrLimited);
            Assert.True(timer.Elapsed<TimeSpan.FromSeconds(22),"Deadline and bounded cleanup must not wait for the60-second child");
            Assert.True(File.Exists(ownership),"Controlled child must publish its ownership before timeout");
            using var owned=Owned();Assert.True(owned is null||owned.HasExited,"Command timeout must terminate the controlled root process");
        }
        finally
        {
            using var owned=Owned();if(owned is not null&&!owned.HasExited){owned.Kill(entireProcessTree:true);owned.WaitForExit(5000);}
            File.Delete(ownership);
        }
    }
    [Fact] public void LiteralArgumentsUnicodeEnvironmentAndWorkingDirectorySurviveCapture()
    {
        var root=Path.Combine(Path.GetTempPath(),"stb capture Ω "+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var literal="literal $(no shell); & | Ω";
            var result=new CommandRunner().Run([Python,"-c","import sys,os,json;sys.stdout.reconfigure(encoding='utf-8');print(json.dumps([sys.argv[1],os.getcwd(),os.environ.get('STB_CAPTURE_PROBE')],ensure_ascii=False))",literal],root,
                new Dictionary<string,string?> { ["STB_CAPTURE_PROBE"]="é Ω" },capture:true,timeout:TimeSpan.FromSeconds(20));
            using var json=JsonDocument.Parse(result.Output);Assert.Equal(literal,json.RootElement[0].GetString());
            Assert.Equal(root,json.RootElement[1].GetString());Assert.Equal("é Ω",json.RootElement[2].GetString());
        }
        finally { if(Path.GetDirectoryName(root)!=Path.TrimEndingDirectorySeparator(Path.GetTempPath()))throw new InvalidOperationException("Invalid capture temp root");Directory.Delete(root,true); }
    }
    [Fact] public void InvalidDeadlineIsRejectedBeforeStartingAChild()
    {
        Assert.Throws<ArgumentOutOfRangeException>(()=>Run("raise RuntimeError('must not start')",TimeSpan.FromSeconds(-5)));
    }
    [Fact] public void NonzeroExceptionMessageIsBoundedIndependentlyOfRetainedStderr()
    {
        var error=Assert.Throws<IOException>(()=>Run("import os,sys;os.write(2,b'x'*5000);sys.exit(7)",check:true));
        Assert.Contains("(7)",error.Message);Assert.True(error.Message.Length<4300);
    }
    // STB-VER-002: prove the named inherited-pipe condition rather than relying on
    // a one-second interpreter start and a fixed three-second descendant sleep.
    [Fact] public async Task ExitedParentWithInheritedPipeHasBoundedLocalCleanup()
    {
        var gate=Path.Combine(Path.GetTempPath(),"stb-capture-gate-"+Guid.NewGuid().ToString("N"));
        var fixture=Path.Combine(ToolContext.FindRoot(),"tests/RunnerProcessFixture/bin/Release/net10.0/RunnerProcessFixture.dll");
        using var rescue=new CancellationTokenSource();
        bool watchdogReleased=false;
        var watchdog=Task.Run(async()=>
        {
            try{await Task.Delay(TimeSpan.FromSeconds(20),rescue.Token);watchdogReleased=true;File.WriteAllText(gate,"watchdog");}
            catch(OperationCanceledException) when(rescue.IsCancellationRequested){}
        },TestContext.Current.CancellationToken);
        try
        {
            var timer=Stopwatch.StartNew();
            var error=Assert.Throws<CommandDeadlineException>(()=>new CommandRunner().Run(
                ["dotnet",fixture,"--capture-parent",gate],Environment.CurrentDirectory,
                capture:true,check:false,timeout:TimeSpan.FromSeconds(10)));
            Assert.Contains("parent exited",error.Diagnostics.Output);
            Assert.True(File.Exists(gate+".ready"),"Descendant must confirm inherited streams before parent exit");
            Assert.True(error.Diagnostics.Settled);
            Assert.True(timer.Elapsed<TimeSpan.FromSeconds(18),"Local pipe cleanup must finish before the rescue release");
            Assert.False(watchdogReleased,"Watchdog release is a failure, not a passing bounded cleanup");
        }
        finally
        {
            File.WriteAllText(gate,"test cleanup");rescue.Cancel();await watchdog;
            if(File.Exists(gate+".pid"))
            {
                try{using var owned=Process.GetProcessById(int.Parse(File.ReadAllText(gate+".pid")));Assert.True(owned.WaitForExit(5000),"Controlled descendant must exit after gate release");}
                catch(ArgumentException){}
            }
            foreach(var path in new[]{gate,gate+".ready",gate+".pid"})File.Delete(path);
        }
    }
}