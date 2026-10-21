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
    [Fact] public void TimeoutRetainsBothPartialStreamsAndSettlesCaptures()
    {
        var timer=Stopwatch.StartNew();
        var error=Assert.Throws<CommandDeadlineException>(()=>Run("import sys,time;print('before stdout',flush=True);print('before stderr',file=sys.stderr,flush=True);time.sleep(6)",TimeSpan.FromSeconds(1)));
        Assert.Contains("before stdout",error.Diagnostics.Output);Assert.Contains("before stderr",error.Diagnostics.Error);
        Assert.True(error.Diagnostics.Settled);Assert.True(timer.Elapsed<TimeSpan.FromSeconds(9));
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
    [Fact] public void ExitedParentWithInheritedPipeHasBoundedLocalCleanup()
    {
        var path=Path.Combine(Path.GetTempPath(),"stb-pipe-owner-"+Guid.NewGuid().ToString("N")+".txt");
        try
        {
            var script="import subprocess,sys;child=subprocess.Popen([sys.executable,'-c','import time;time.sleep(3)'],stdout=sys.stdout,stderr=sys.stderr);open("+JsonSerializer.Serialize(path)+",'w').write(str(child.pid));print('parent exited',flush=True)";
            var timer=Stopwatch.StartNew();
            var error=Assert.Throws<CommandDeadlineException>(()=>Run(script,TimeSpan.FromSeconds(1)));
            Assert.Contains("parent exited",error.Diagnostics.Output);Assert.True(error.Diagnostics.Settled);
            Assert.True(timer.Elapsed<TimeSpan.FromSeconds(9));
            try { using var child=Process.GetProcessById(int.Parse(File.ReadAllText(path)));Assert.True(child.WaitForExit(5000)); }catch(ArgumentException) { }
        }
        finally { File.Delete(path); }
    }
}
