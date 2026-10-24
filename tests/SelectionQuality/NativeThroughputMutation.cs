#:property PublishAot=false
#:property AssemblyName=SharpTestBench.Tools.Tests
#:property JsonSerializerIsReflectionEnabledByDefault=true
#:project ../../tools/SharpTestBench.Tools/SharpTestBench.Tools.csproj
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using SharpTestBench.Tools;
// A verification harness, not a production worker. The existing friend identity
// permits reuse of attested orchestration without creating a new RPC/API schema.
string root=Path.GetFullPath(args.Length==1?args[0]:Directory.GetCurrentDirectory());
var context=new ToolContext(root);string os=OperatingSystem.IsWindows()?"windows":"linux";
string completion=context.At($"build/selection200-throughput-mutation-{os}.json");File.Delete(completion);
string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
string loaded=typeof(ToolContext).Assembly.Location,tools=context.At("tools/SharpTestBench.Tools/bin/Release/net10.0/SharpTestBench.Tools.dll");
if(Hash(loaded)!=Hash(tools))throw new InvalidDataException("Run this harness in Release against the current verified Tools build");
foreach(string suffix in new[]{".deps.json",".runtimeconfig.json"})
{
 string name="SharpTestBench.Tools"+suffix,source=Path.Combine(Path.GetDirectoryName(tools)!,name),target=Path.Combine(Path.GetDirectoryName(loaded)!,name);
 if(Path.GetFullPath(source)!=Path.GetFullPath(target))File.Copy(source,target,true);
}
string rtl=context.At("tests/ProtocolContracts/protocol_top.sv"),assembly=context.At("tests/ProtocolContracts/bin/Release/net10.0/ProtocolContracts.dll");
string original=File.ReadAllText(rtl).Replace("\r\n","\n"),rtlHash=Hash(rtl),assemblyHash=Hash(assembly);
const string ready="wire c_in_ready=!c_out_valid||c_ready;",end="  end\n end\n // Four-way rotating priority.";
if(original.Split(ready,StringSplitOptions.None).Length!=2||original.Split(end,StringSplitOptions.None).Length!=2)throw new InvalidDataException("RTL mutation anchors changed");
// This coherent defect consumes an old output while refusing simultaneous input
// replacement, creating a bubble. A generic retrying scoreboard can tolerate it.
string changed=original.Replace(ready,"wire c_in_ready=!c_out_valid;",StringComparison.Ordinal)
 .Replace(end,"  end else if(c_ready) c_out_valid<=0;\n end\n // Four-way rotating priority.",StringComparison.Ordinal);
var variants=new JsonArray();
foreach(bool mutant in new[]{false,true})
{
 var report=ParityCiCommand.InWorkspace(context,"selection200-throughput-"+(mutant?"bubble":"control"),(work,environment)=>
 {
  string source=rtl;
  if(mutant)
  {
   source=Path.Combine(work,"protocol_bubble.sv");
   if(File.Exists(source)&&File.ReadAllText(source).Replace("\r\n","\n")!=changed)throw new InvalidDataException("Preserve unknown mutation source");
   File.WriteAllText(source,changed);
  }
  var manifest=RunnerManifestCommand.Make(root,"counter",83,RunnerManifestCommand.InstalledAdapter(context),Path.Combine(work,"native"));
  manifest["top"]="protocol_top";manifest["sources"]=new[]{source};manifest["assembly"]=assembly;manifest["entryType"]="Entry, ProtocolContracts";
  RunnerManifestCommand.BindSingleCase(manifest,"Entry.R013_ContinuousAcceptedBurst",95);
  var value=ParityCiCommand.Compare(context,work,manifest,83,environment,null,null,mutant?"continuous input acceptance each offered cycle":null,null);
  // A timed-out compiler, crash, incomplete capture or unrelated failed assertion
  // cannot become a killed RTL mutant merely because both front ends exit one.
  VerifyProcesses(work);
  value["fixture"]="protocol-throughput-sensitivity";value["variant"]=mutant?"coherent replacement bubble":"unchanged control";
  value["hdlSourceSha256"]=Hash(source);value["fixtureAssemblySha256"]=assemblyHash;
  return value;
 });variants.Add(JsonSerializer.SerializeToNode(report));
}
if(Hash(rtl)!=rtlHash||Hash(assembly)!=assemblyHash||Hash(loaded)!=Hash(tools))throw new InvalidDataException("Verification inputs changed during mutation qualification");
var result=new JsonObject{["schemaVersion"]=1,["status"]="verified",["scope"]="One locally authored RTL fixture mutant, separate from production C# mutants and HDL line/branch coverage",["baseSeed"]=83,["caseSeed"]=95,["variants"]=variants};
File.WriteAllText(completion,result.ToJsonString(new JsonSerializerOptions{WriteIndented=true}).Replace("\r\n","\n")+"\n");
Console.WriteLine("Unchanged throughput control passed and coherent RTL bubble was detected through both actual runners.");
static void VerifyProcesses(string work)
{
 using(var file=StrictJson.Read(Path.Combine(work,"legacy/legacy-compile-process.json")))
 {
  var row=file.RootElement;if(row.GetProperty("status").GetString()!="completed"||row.GetProperty("exitCode").GetInt32()!=0||row.GetProperty("timedOut").GetBoolean()||!row.GetProperty("diagnosticsComplete").GetBoolean()||!row.GetProperty("captureSettled").GetBoolean())throw new InvalidDataException("Legacy compilation did not complete");
 }
 using(var file=StrictJson.Read(Path.Combine(work,"legacy/legacy-simulate-capture.json")))
 {
  var row=file.RootElement;if(row.GetProperty("status").GetString()!="completed"||row.GetProperty("exitCode").GetInt32() is not (0 or 1)||!row.GetProperty("diagnosticsComplete").GetBoolean()||!row.GetProperty("captureSettled").GetBoolean()||row.GetProperty("stdoutLimited").GetBoolean()||row.GetProperty("stderrLimited").GetBoolean())throw new InvalidDataException("Legacy simulation did not reach intended boundary");
 }
 foreach(string phase in new[]{"compile","simulate"})
 {
  using var file=StrictJson.Read(Path.Combine(work,"native",phase+"-process.json"));var row=file.RootElement;
  if(!row.GetProperty("Started").GetBoolean()||row.GetProperty("TimedOut").GetBoolean()||row.GetProperty("Stdout").ValueKind!=JsonValueKind.Object||row.GetProperty("Stderr").ValueKind!=JsonValueKind.Object||row.GetProperty("Stdout").GetProperty("Truncated").GetBoolean()||row.GetProperty("Stderr").GetProperty("Truncated").GetBoolean()||(phase=="compile"?row.GetProperty("ExitCode").GetInt32()!=0:row.GetProperty("ExitCode").GetInt32() is not (0 or 1)))throw new InvalidDataException("Native process did not reach intended boundary");
 }
}