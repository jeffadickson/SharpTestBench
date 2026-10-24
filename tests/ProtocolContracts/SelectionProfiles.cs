using SharpTestBench;
using static SharpTestBench.Triggers;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Globalization;

// Each recipe is selected through its attested managed entry type, not a new
// environment schema. Existing list/stage policy is set deliberately in-process.
internal static class SelectionProfiles
{
 internal static int Initialize(int recipe)
 {
  try
  {
   // Body markers are a UTF-8 evidence protocol, including Unicode case names.
   Console.OutputEncoding=new System.Text.UTF8Encoding(false);
   RegressionMode mode=recipe is 13 or 14 or 16 or 20 or 28 or 29 or 30?RegressionMode.Testcase:RegressionMode.Regression;
   string[] patterns=recipe switch
   {
    1=>["A_Pass"],2=>[@"^ProbeCases\.A_Pass$"],3=>[@"^(ProbeCases\.A_Pass|ProbeSibling\.A_Pass)$"],
    4=>[@"^ProbeCases\.A_Pass$",@"^ProbeCases\.D_Param\[1\]$"],5=>[@"^ProbeSibling\.A_Pass$"],
    6=>[@"^ProbeCases\.D_Param\[1\]$"],7=>[@"^Entry\.R00[12]_",@"^ProbeCases\.D_Param\[[01]\]$"],8=>[@"(?i)^probecases\.i_case$"],
    9=>[@"^ProbeCases\.Ω_Unicode$"],10=>[@"(?<=ProbeCases\.)A_Pass$"],
    11=>[@"^ProbeCases\.(?!B_|C_|E_|F_|G_|H_|I_|Ω_)"],12=>[@"^ProbeContainer\+Nested\.A_Same$"],
    13=>[@"^ProbeCases\.B_ExplicitSkip$"],14=>[@"^ProbeCases\.H_SkipExpected$"],15=>[@"^ProbeCases\.E_Expected$"],
    16=>[@"^ProbeCases\.F_Fail$",@"^ProbeLater\.A_Skip$"],17=>["^never$",@"^ProbeCases\.A_Pass$"],
    18=>[],19=>[@"^ProbeCases\.D_Param\[1\]$"],20=>[@"^ProbeCases\.B_ExplicitSkip$"],21=>["^never$"],
    22=>["["],23=>[],24=>[],25=>["^never$"],26=>[@"^ProbeCases\.A_Pass$",@"^ProbeAdversarial\.(a+)+$"],
    27=>[@"^ProbeCases\.B_ExplicitSkip$"],28=>[@"^ProbeCases\.C_Backend$"],29=>[@"^ProbeCases\.F_Fail$"],30=>[@"^ProbeCases\.G_SkipExpectedLeak$"],
    _=>throw new ArgumentOutOfRangeException(nameof(recipe))
   };
   if(recipe==8)CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("tr-TR");
   if(recipe==16)Environment.SetEnvironmentVariable("CS_STAGE_FAILURE_POLICY","stop-after-stage");
   if(recipe is 18 or 19 or 20)Environment.SetEnvironmentVariable("CS_LIST_TESTS","1");
   if(recipe==23)mode=(RegressionMode)99;
   // Generous semantic budgets avoid scheduler preemption masquerading as a
   // selection failure. Recipe26 deliberately times out an exponential pattern.
   var selection=new RegressionSelection(patterns,mode,recipe==24?TimeSpan.Zero:TimeSpan.FromSeconds(1));
   return Regression.Initialize(recipe==25?DuplicateAssembly():typeof(Entry).Assembly,selection);
  }
  catch(Exception error){Console.Error.WriteLine("SELECTION_INIT_REJECTED:"+error.GetType().Name);return 1;}
 }
 static Assembly DuplicateAssembly()
 {
  var assembly=AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("SelectionCollision"),AssemblyBuilderAccess.Run);
  var type=assembly.DefineDynamicModule("fixture").DefineType("Collision",TypeAttributes.Public|TypeAttributes.Abstract|TypeAttributes.Sealed);
  foreach(bool parameterized in new[]{false,true})
  {
   var method=type.DefineMethod("Clash",MethodAttributes.Public|MethodAttributes.Static,typeof(Task),parameterized?[typeof(TestContext),typeof(int)]:[typeof(TestContext)]);
   method.SetCustomAttribute(new CustomAttributeBuilder(typeof(SimTestAttribute).GetConstructor(Type.EmptyTypes)!,[]));
   if(parameterized)method.SetCustomAttribute(new CustomAttributeBuilder(typeof(SimCaseAttribute).GetConstructor([typeof(object[])])!,[new object[]{1}]));
   var il=method.GetILGenerator();il.Emit(OpCodes.Ldstr,"SELECTION_BODY_ENTERED:Collision.Clash");il.Emit(OpCodes.Call,typeof(Console).GetMethod(nameof(Console.WriteLine),[typeof(string)])!);
   il.Emit(OpCodes.Call,typeof(Task).GetProperty(nameof(Task.CompletedTask))!.GetMethod!);il.Emit(OpCodes.Ret);
  }
  _=type.CreateType();return assembly;
 }
}
/// <summary>Native selection targets; ordinary protocol entry filters these out.</summary>
public static class ProbeCases
{
 internal static async Task Body(TestContext c,string identity)
 {
  Console.WriteLine("SELECTION_BODY_ENTERED:"+identity);
  var signal=c.Dut.Child("probe_value").Signal();string bits=Convert.ToString((int)(c.Seed&255),2).PadLeft(8,'0');
  signal.WriteBits(bits,WriteAction.Immediate);await Timer(1);
  if(signal.ReadBits()!=bits)throw new InvalidDataException("SELECTION native body readback");
 }
 [SimTest(Stage=100)] public static Task A_Pass(TestContext c)=>Body(c,"ProbeCases.A_Pass");
 [SimTest(Stage=100,Skip=true)] public static Task B_ExplicitSkip(TestContext c)=>Body(c,"ProbeCases.B_ExplicitSkip");
 [SimTest(Stage=100,Simulators=["UnavailableFixtureBackend"])] public static Task C_Backend(TestContext c)=>Body(c,"ProbeCases.C_Backend");
 [SimTest(Stage=100)] [SimCase(0)] [SimCase(1)] public static Task D_Param(TestContext c,int index)=>Body(c,$"ProbeCases.D_Param[{index}]");
 [SimTest(Stage=100,ExpectedException=typeof(InvalidOperationException))] public static async Task E_Expected(TestContext c){await Body(c,"ProbeCases.E_Expected");throw new InvalidOperationException("SELECTION expected exception");}
 [SimTest(Stage=100)] public static async Task F_Fail(TestContext c){await Body(c,"ProbeCases.F_Fail");throw new InvalidDataException("SELECTION unexpected failure");}
 [SimTest(Stage=100,Skip=true,ExpectedException=typeof(InvalidOperationException),CleanupTimeoutSteps=10)] public static async Task G_SkipExpectedLeak(TestContext c)
 {
  // Teardown snapshots leaks after a one-step timer; this uncancellable callback
  // remains far in the future. Both readiness and owned-job state precede the throw.
  await Body(c,"ProbeCases.G_SkipExpectedLeak");var ready=new SimEvent();
  _=c.StartSoon(async _=>{ready.Set();await Timer(1_000_000).WithCancellation(CancellationToken.None);},"uncancellable-selection-probe");
  await ready.Wait();throw new InvalidOperationException("SELECTION expected exception before leak");
 }
 [SimTest(Stage=100,Skip=true,ExpectedException=typeof(InvalidOperationException))] public static async Task H_SkipExpected(TestContext c){await Body(c,"ProbeCases.H_SkipExpected");throw new InvalidOperationException("SELECTION skipped expected exception");}
 [SimTest(Stage=100)] public static Task I_Case(TestContext c)=>Body(c,"ProbeCases.I_Case");
 [SimTest(Stage=100)] public static Task Ω_Unicode(TestContext c)=>Body(c,"ProbeCases.Ω_Unicode");
}
public static class ProbeContainer{public static class Nested{[SimTest(Stage=100)]public static Task A_Same(TestContext c)=>ProbeCases.Body(c,"ProbeContainer+Nested.A_Same");}}
public static class ProbeSibling{[SimTest(Stage=100)]public static Task A_Pass(TestContext c)=>ProbeCases.Body(c,"ProbeSibling.A_Pass");}
public static class ProbeLater{[SimTest(Stage=101,Skip=true)]public static Task A_Skip(TestContext c)=>ProbeCases.Body(c,"ProbeLater.A_Skip");}
public static class ProbeAdversarial{[SimTest(Stage=102)]public static Task aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaB(TestContext c)=>ProbeCases.Body(c,"ProbeAdversarial."+new string('a',128)+"B");}
/// <summary>Attested initialization recipe 1.</summary>
public static class ScenarioEntry01{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(1);}

/// <summary>Attested initialization recipe 2.</summary>
public static class ScenarioEntry02{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(2);}

/// <summary>Attested initialization recipe 3.</summary>
public static class ScenarioEntry03{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(3);}

/// <summary>Attested initialization recipe 4.</summary>
public static class ScenarioEntry04{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(4);}

/// <summary>Attested initialization recipe 5.</summary>
public static class ScenarioEntry05{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(5);}

/// <summary>Attested initialization recipe 6.</summary>
public static class ScenarioEntry06{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(6);}

/// <summary>Attested initialization recipe 7.</summary>
public static class ScenarioEntry07{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(7);}

/// <summary>Attested initialization recipe 8.</summary>
public static class ScenarioEntry08{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(8);}

/// <summary>Attested initialization recipe 9.</summary>
public static class ScenarioEntry09{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(9);}

/// <summary>Attested initialization recipe 10.</summary>
public static class ScenarioEntry10{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(10);}

/// <summary>Attested initialization recipe 11.</summary>
public static class ScenarioEntry11{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(11);}

/// <summary>Attested initialization recipe 12.</summary>
public static class ScenarioEntry12{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(12);}

/// <summary>Attested initialization recipe 13.</summary>
public static class ScenarioEntry13{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(13);}

/// <summary>Attested initialization recipe 14.</summary>
public static class ScenarioEntry14{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(14);}

/// <summary>Attested initialization recipe 15.</summary>
public static class ScenarioEntry15{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(15);}

/// <summary>Attested initialization recipe 16.</summary>
public static class ScenarioEntry16{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(16);}

/// <summary>Attested initialization recipe 17.</summary>
public static class ScenarioEntry17{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(17);}

/// <summary>Attested initialization recipe 18.</summary>
public static class ScenarioEntry18{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(18);}

/// <summary>Attested initialization recipe 19.</summary>
public static class ScenarioEntry19{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(19);}

/// <summary>Attested initialization recipe 20.</summary>
public static class ScenarioEntry20{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(20);}

/// <summary>Attested initialization recipe 21.</summary>
public static class ScenarioEntry21{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(21);}

/// <summary>Attested initialization recipe 22.</summary>
public static class ScenarioEntry22{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(22);}

/// <summary>Attested initialization recipe 23.</summary>
public static class ScenarioEntry23{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(23);}

/// <summary>Attested initialization recipe 24.</summary>
public static class ScenarioEntry24{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(24);}

/// <summary>Attested initialization recipe 25.</summary>
public static class ScenarioEntry25{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(25);}

/// <summary>Attested initialization recipe 26.</summary>
public static class ScenarioEntry26{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(26);}

/// <summary>Attested initialization recipe 27.</summary>
public static class ScenarioEntry27{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(27);}

/// <summary>Attested initialization recipe 28.</summary>
public static class ScenarioEntry28{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(28);}

/// <summary>Attested initialization recipe 29.</summary>
public static class ScenarioEntry29{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(29);}

/// <summary>Attested initialization recipe 30.</summary>
public static class ScenarioEntry30{[UnmanagedCallersOnly]public static int Initialize(nint api,int size)=>SelectionProfiles.Initialize(30);}
