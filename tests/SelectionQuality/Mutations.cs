#:property PublishAot=false
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
// Compile real production selector text in isolation. A compiler rejection is an
// invalid mutant, never a killed mutant. The control must pass the same probes.
string root=Path.GetFullPath(args.Length==1?args[0]:Directory.GetCurrentDirectory());
string directory=Path.Combine(root,"build/selection200-mutations");Directory.CreateDirectory(directory);
// A failed new attempt must never leave a previous success at the advertised path.
File.Delete(Path.Combine(root,"build/selection200-mutation-results.json"));
string source=File.ReadAllText(Path.Combine(root,"src/SharpTestBench/RegressionSelection.cs"));
source=source[..source.IndexOf("internal sealed record PlannedTest",StringComparison.Ordinal)].Replace("namespace SharpTestBench;","namespace MutationCandidate;");
File.WriteAllText(Path.Combine(directory,"Mutation.csproj"),"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>");
File.WriteAllText(Path.Combine(directory,"Program.cs"),"""
using MutationCandidate;
using System.Text.RegularExpressions;
class Program
{
 static int Main()
 {
  try
  {
   var either=Semantic(["^A$","^B$"]);
   Require(either.Matches("A") && either.Matches("B") && !either.Matches("C"),"OR");
   Require(!Semantic(["A"]).Matches("a"),"case sensitivity");
   bool modeRejected=false;try{_ = Semantic([],(RegressionMode)99);}catch(ArgumentOutOfRangeException){modeRejected=true;}
   Require(modeRejected,"mode");
   Require(Semantic().Matches("anything"),"empty alternatives");
   bool nullRejected=false;try{_ = Semantic([null!]);}catch(ArgumentException){nullRejected=true;}
   Require(nullRejected,"null pattern");
   bool limitRejected=false;try{_ = Semantic(Enumerable.Repeat("a",65));}catch(ArgumentException){limitRejected=true;}
   Require(limitRejected,"count bound");
   bool lengthRejected=false;try{_ = Semantic([new string('a',4097)]);}catch(ArgumentException){lengthRejected=true;}
   Require(lengthRejected,"length bound");
   return 0;
  }
  catch(Exception error){Console.WriteLine("PROBE_DETECTED:"+error.Message);return 42;}
 }
 static RegressionSelection Semantic(IEnumerable<string> patterns,RegressionMode mode=RegressionMode.Regression)=>new(patterns,mode,TimeSpan.FromSeconds(1));
 static RegressionSelection Semantic()=>new([],matchTimeout:TimeSpan.FromSeconds(1));
 static void Require(bool condition,string probe){if(!condition)throw new Exception(probe);}
}
""");
var variants=new (string Name,string From,string To)[]{
 ("control","", ""),
 ("OR replaced by AND","expressions.Any(","expressions.All("),
 ("case insensitive default","RegexOptions.CultureInvariant, timeout","RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, timeout"),
 ("undefined mode accepted","if (!Enum.IsDefined(mode))","if (false)"),
 ("empty alternatives reject","expressions.Length == 0 ||","expressions.Length != 0 &&"),
 ("null patterns silently accepted","if (pattern is null) throw new ArgumentException(\"Regex patterns must not be null\", nameof(patterns));","if (pattern is null) continue;"),
 ("pattern count relaxed","MaximumPatterns = 64","MaximumPatterns = 65"),
 ("pattern length relaxed","MaximumPatternLength = 4096","MaximumPatternLength = 4097")};
var evidence=new JsonArray();
foreach(var variant in variants)
{
 if(variant.Name!="control" && !source.Contains(variant.From,StringComparison.Ordinal))throw new Exception("Missing mutation anchor");
 File.WriteAllText(Path.Combine(directory,"Selection.cs"),variant.Name=="control"?source:source.Replace(variant.From,variant.To));
 var build=Run(["build",Path.Combine(directory,"Mutation.csproj"),"-c","Release","--disable-build-servers","-m:1"]);
 File.WriteAllText(Path.Combine(directory,variants.ToList().IndexOf(variant)+"-build.log"),build.Text);
 if(build.Code!=0)throw new Exception("Invalid mutant: "+variant.Name);
 var probe=Run([Path.Combine(directory,"bin/Release/net10.0/Mutation.dll")]);
 File.WriteAllText(Path.Combine(directory,variants.ToList().IndexOf(variant)+"-probe.log"),probe.Code+"\n"+probe.Text);
 string expectedProbe=variant.Name switch
 {"control"=>"","OR replaced by AND"=>"OR","case insensitive default"=>"case sensitivity","undefined mode accepted"=>"mode","empty alternatives reject"=>"empty alternatives","null patterns silently accepted"=>"null pattern","pattern count relaxed"=>"count bound","pattern length relaxed"=>"length bound",_=>throw new InvalidOperationException("Unknown mutant")};
 bool expected=variant.Name=="control"?probe.Code==0:probe.Code==42 && probe.Text.Trim()=="PROBE_DETECTED:"+expectedProbe;
 evidence.Add(new JsonObject{["name"]=variant.Name,["exitCode"]=probe.Code,["probe"]=probe.Text.Trim(),["expectedProbe"]=expectedProbe,["expected"]=expected});
 if(!expected)throw new Exception("Survived mutation or failing control: "+variant.Name);
 Console.WriteLine(variant.Name+": verified");
}
var report=new JsonObject{["schemaVersion"]=1,["productionSourceSha256"]=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,"src/SharpTestBench/RegressionSelection.cs")))).ToLowerInvariant(),["scope"]="Public selector configuration/matching only; planner and simulator runtime are outside this mutation collection",["variants"]=evidence};
File.WriteAllText(Path.Combine(root,"build/selection200-mutation-results.json"),report.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));
(int Code,string Text) Run(string[] arguments)
{
 var start=new ProcessStartInfo(Path.Combine(Environment.GetEnvironmentVariable("CS_DOTNET_ROOT")??Environment.GetEnvironmentVariable("DOTNET_ROOT")??throw new InvalidOperationException("Select the pinned .NET SDK first"),OperatingSystem.IsWindows()?"dotnet.exe":"dotnet")){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
 foreach(var argument in arguments)start.ArgumentList.Add(argument);
 using var process=Process.Start(start)!;var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
 process.WaitForExit();return(process.ExitCode,output.GetAwaiter().GetResult()+error.GetAwaiter().GetResult());
}