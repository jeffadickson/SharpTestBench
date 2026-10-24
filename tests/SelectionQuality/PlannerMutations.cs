#:property PublishAot=false
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
// Isolate the actual planner source while referencing only the production public
// metadata types. No simulator is started and no production binary is rewritten.
string root=Path.GetFullPath(args.Length==1?args[0]:Directory.GetCurrentDirectory());
string directory=Path.Combine(root,"build/selection200-planner-mutations");Directory.CreateDirectory(directory);
string reportPath=Path.Combine(root,"build/selection200-planner-mutation-results.json");File.Delete(reportPath);
string production=Path.Combine(root,"src/SharpTestBench/RegressionSelection.cs");
string source="using SharpTestBench;\n"+File.ReadAllText(production).Replace("namespace SharpTestBench;","namespace MutationCandidate;");
string reference=System.Security.SecurityElement.Escape(Path.Combine(root,"src/SharpTestBench/bin/Release/net10.0/SharpTestBench.dll"));
File.WriteAllText(Path.Combine(directory,"Planner.csproj"),"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><Reference Include=\"SharpTestBench\"><HintPath>"+reference+"</HintPath></Reference></ItemGroup></Project>");
File.WriteAllText(Path.Combine(directory,"Program.cs"),"""
using System.Reflection;
using SharpTestBench;
using Candidate=MutationCandidate.RegressionSelection;
class Program
{
 static MethodInfo M(string name)=>typeof(Fixture).GetMethod(name)!;
 static int Main()
 {
  try
  {
   MethodInfo[] methods=[M("A_Late"),M("Z_First"),M("M_Middle")];
   var all=MutationCandidate.RegressionPlanning.Build(methods,new Candidate(),83,_=>true);
   Require(all.Select(item=>item.Descriptor.Name).SequenceEqual(new[]{"Z_First","M_Middle","A_Late"}),"stage ordering");
   Require(Fixture.Invocations==0,"no body invocation");
   var empty=MutationCandidate.RegressionPlanning.Build(methods,new Candidate(["A_Late$"],matchTimeout:TimeSpan.FromSeconds(1)),83,item=>item.Stage<0);
   Require(empty.Count==0,"intersection");
   var selected=MutationCandidate.RegressionPlanning.Build(methods,new Candidate(["A_Late$"],matchTimeout:TimeSpan.FromSeconds(1)),83,_=>true);
   Require(selected.Count==1&&selected[0].Descriptor.Seed==85,"full-order seed");
   bool rejected=false;
   try{MutationCandidate.RegressionPlanning.Build(typeof(Collision).GetMethods(BindingFlags.Public|BindingFlags.Static),new Candidate(["^never$"],matchTimeout:TimeSpan.FromSeconds(1)),83,_=>true);}
   catch(InvalidOperationException error) when(error.Message.Contains("Duplicate qualified test identity")){rejected=true;}
   Require(rejected,"duplicate identity");return 0;
  }
  catch(Exception error){Console.WriteLine("PROBE_DETECTED:"+error.Message);return 42;}
 }
 static void Require(bool condition,string probe){if(!condition)throw new Exception(probe);}
}
static class Fixture
{
 internal static int Invocations;
 [SimTest(Stage=-2)]public static Task Z_First(TestContext context){Invocations++;return Task.CompletedTask;}
 [SimTest]public static Task M_Middle(TestContext context){Invocations++;return Task.CompletedTask;}
 [SimTest(Stage=2)]public static Task A_Late(TestContext context){Invocations++;return Task.CompletedTask;}
}
static class Collision
{
 [SimTest]public static Task Clash(TestContext context)=>Task.CompletedTask;
 [SimTest][SimCase(1)]public static Task Clash(TestContext context,int value)=>Task.CompletedTask;
}
""");
var variants=new (string Name,string From,string To,string Probe)[]{
 ("control","","",""),
 ("stage order removed",".OrderBy(method => method.GetCustomAttribute<SimTestAttribute>()!.Stage)",".OrderBy(method => 0)","stage ordering"),
 ("filtered cases lose ordinal seeds","unchecked(seed + ordinal++)","seed","full-order seed"),
 ("intersection broadened to OR","include(descriptor) && selection.Matches(qualifiedName)","include(descriptor) || selection.Matches(qualifiedName)","intersection"),
 ("duplicate identity guard removed","if (!identities.Add(qualifiedName))","if (false)","duplicate identity"),
 ("planning invokes a body","planned.Add(new(method, (object[])cases[index].Clone(), attribute, descriptor));","{ planned.Add(new(method, (object[])cases[index].Clone(), attribute, descriptor)); method.Invoke(null, [null]); }","no body invocation")};
var evidence=new JsonArray();
for(int index=0;index<variants.Length;index++)
{
 var variant=variants[index];if(index!=0&&!source.Contains(variant.From,StringComparison.Ordinal))throw new Exception("Missing planner mutation anchor");
 File.WriteAllText(Path.Combine(directory,"Selection.cs"),index==0?source:source.Replace(variant.From,variant.To));
 var build=Run(["build",Path.Combine(directory,"Planner.csproj"),"-c","Release","--disable-build-servers","-m:1"]);File.WriteAllText(Path.Combine(directory,index+"-build.log"),build.Text);
 if(build.Code!=0)throw new Exception("Invalid planner mutant: "+variant.Name);
 var probe=Run([Path.Combine(directory,"bin/Release/net10.0/Planner.dll")]);File.WriteAllText(Path.Combine(directory,index+"-probe.log"),probe.Code+"\n"+probe.Text);
 bool expected=index==0?probe.Code==0:probe.Code==42&&probe.Text.Trim()=="PROBE_DETECTED:"+variant.Probe;
 evidence.Add((JsonNode)new JsonObject{["name"]=variant.Name,["exitCode"]=probe.Code,["probe"]=probe.Text.Trim(),["expectedProbe"]=variant.Probe,["expected"]=expected});
 if(!expected)throw new Exception("Surviving planner mutant or failing control: "+variant.Name);Console.WriteLine(variant.Name+": verified");
}
var report=new JsonObject{["schemaVersion"]=1,["productionSourceSha256"]=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(production))).ToLowerInvariant(),["scope"]="Managed metadata planning only; runtime skip/isolation execution is separately qualified, not mutated here",["variants"]=evidence};
File.WriteAllText(reportPath,report.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));
(int Code,string Text) Run(string[] arguments)
{
 string sdk=Environment.GetEnvironmentVariable("CS_DOTNET_ROOT")??Environment.GetEnvironmentVariable("DOTNET_ROOT")??throw new InvalidOperationException("Select the pinned SDK first");
 var start=new ProcessStartInfo(Path.Combine(sdk,OperatingSystem.IsWindows()?"dotnet.exe":"dotnet")){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};foreach(string argument in arguments)start.ArgumentList.Add(argument);
 using var process=Process.Start(start)!;var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();process.WaitForExit();return(process.ExitCode,output.GetAwaiter().GetResult()+error.GetAwaiter().GetResult());
}