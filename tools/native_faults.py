#!/usr/bin/env python3
"""Fresh-process tests of ABI mismatch, missing symbols and callback allocation failure.
Fake GPI is confined to build/native-faults; never used by the simulator runner.
"""
import json
import os
from pathlib import Path
import re
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[1]
SDK = Path(os.environ['CS_DOTNET_ROOT']) if 'CS_DOTNET_ROOT' in os.environ else Path(shutil.which('dotnet')).resolve().parent
BUILD = ROOT / 'build/native-faults'


def run(args, **kw):
    try:
        return subprocess.run([str(x) for x in args], check=True, text=True, capture_output=True, **kw)
    except subprocess.CalledProcessError as error:
        # Preserve the child diagnostic when a fresh-process probe fails in CI.
        print(error.stdout or '', end='')
        print(error.stderr or '', end='')
        raise


def main():
    BUILD.mkdir(parents=True, exist_ok=True)
    host = ROOT / 'build/libsharptestbench_host.so'
    declarations = re.findall(r'^(.+?) cs_v1_(\w+)\((.*?)\);$', (ROOT / 'native/abi.h').read_text(), re.M)
    fake = '#include "abi.h"\n#include <stdlib.h>\n'
    for ret, name, args in declarations:
        if name == 'gpi_get_signal_value_long':
            ret = 'long'
        value = {'gpi_get_root_handle': '(void *)1', 'gpi_get_object_type': '10',
                 'gpi_get_signal_value_long': '-2147483649L', 'gpi_get_num_elems': 'getenv("CS_FAKE_WIDTH") ? atoi(getenv("CS_FAKE_WIDTH")) : 9', 'gpi_get_simulator_product': 'getenv("CS_FAKE_PRODUCT") ? getenv("CS_FAKE_PRODUCT") : "Icarus Verilog fault fixture"',
                 'gpi_get_signal_value_binstr': 'getenv("CS_FAKE_LOGIC") ? getenv("CS_FAKE_LOGIC") : "10XZ01XZ1"',
                 'gpi_is_constant': 'getenv("CS_FAKE_CONSTANT") != NULL'}.get(name, '0')
        fake += f'{ret} {name}({args}) {{' + (f'return {value};' if ret != 'void' else '') + '}\n'
    for case, source, filename in [
        ('registration', fake, 'libgpi.so'),
        ('missing', 'void unrelated(void) {}', 'libgpi.so'),
        ('legacy', 'unsigned cs_abi_version(void) { return 1; }', 'libsharptestbench_host.so'),
        ('version', 'unsigned cs_abi_version(void) { return 99; }', 'libsharptestbench_host.so')]:
        folder = BUILD / case
        folder.mkdir(exist_ok=True)
        (folder / 'fake.c').write_text(source)
        run(['gcc', '-shared', '-fPIC', '-I', ROOT / 'native', folder / 'fake.c', '-o', folder / filename])
        if case not in ('version', 'legacy'):
            shutil.copy2(host, folder / host.name)
    (BUILD / 'Faults.csproj').write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
<TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings>
<Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup>
<Reference Include="SharpTestBench"><HintPath>{ROOT}/src/SharpTestBench/bin/Release/net10.0/SharpTestBench.dll</HintPath></Reference>
</ItemGroup></Project>''')
    (BUILD / 'Program.cs').write_text('''using SharpTestBench;
using System.Reflection;
string mode = args[0];
if (mode != "registration" && mode != "unknown") {
 try { _ = new SimRuntime(); throw new Exception("Bad ABI was accepted"); }
 catch (NotSupportedException e) {
  string wanted = mode == "legacy" ? "predates packed-plane support" : mode == "version" ? "native ABI 99; expected 1" : "missing GPI symbol: gpi_register_start_of_sim_time_callback";
  if (!e.Message.Contains(wanted)) throw;
  Console.WriteLine("PASS " + mode + ": " + e.Message);
 }
 return;
}
var r = new SimRuntime();
if (mode == "unknown") {
 if (r.SupportsFourState || r.SupportsNineState || r.SupportsForceRelease) throw new Exception("Optimistic unknown support");
 var signal = HdlObject.Root().Signal();
 var typed = HdlObject.Root().Signal<W8>();
 Action[] writes = {
  () => signal.WriteBits("XXXXXXXX", WriteAction.Immediate),
  () => signal.WriteBits("UUUUUUUU", WriteAction.Immediate),
  () => typed.Write(LogicVector<W8>.Parse("XXXXXXXX"), WriteAction.Immediate),
  () => signal.WriteBits("00000000", WriteAction.Force),
  () => signal.WriteBits("00000000", WriteAction.Release)
 };
 foreach (var write in writes) {
  try { write(); throw new Exception("Unverified write accepted"); }
  catch (NotSupportedException e) when (e.Message.Contains("Unverified")) { }
 }
 if (r.PendingCallbacks != 0) throw new Exception("Rejected operation queued work");
 Console.WriteLine("PASS unknown: five raw/typed state and force/release guards reject unverified operations");
 return;
}
var kindType = typeof(SimRuntime).Assembly.GetType("SharpTestBench.CallbackKind")!;
var register = typeof(SimRuntime).GetMethod("Register", BindingFlags.NonPublic | BindingFlags.Instance)!;
int count = 0;
foreach (object kind in Enum.GetValues(kindType)) {
 try {
  register.Invoke(r, new object[] { kind, (Action)(() => throw new Exception("Unexpected callback")), 1UL, (nint)1, 0, null! });
  throw new Exception("Failed native registration was accepted");
 } catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException error && error.Message.StartsWith("Cannot register ")) { }
 if (r.PendingCallbacks != 0) throw new Exception("Registration leaked");
 if (r.LastSimulatorCallback != null) throw new Exception("Failed registration fabricated callback history");
 count++;
}
// Check the actual table too: PendingCallbacks intentionally excludes the End callback.
var registrations = (System.Collections.IDictionary)typeof(SimRuntime).GetField("registrations", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(r)!;
if (registrations.Count != 0) throw new Exception("End callback registration leaked");
var drain = typeof(SimRuntime).GetMethod("Drain", BindingFlags.NonPublic | BindingFlags.Instance)!;
for (int i = 0; i < 100; i++) {
 var task = Triggers.Timer(1).AsTask();
 drain.Invoke(r, null);
 if (!task.IsFaulted || task.Exception?.InnerException is not InvalidOperationException || r.PendingCallbacks != 0)
  throw new Exception("Failed trigger did not settle cleanly");
}
try { HdlObject.Root().Signal().WriteBits("000000001"); throw new Exception("Failed write registration accepted"); }
catch (InvalidOperationException e) when (e.Message == "Cannot register ReadWrite") { }
foreach (string field in new[] { "writes", "writePreviews", "appliedPreviews" }) {
 var pending = (System.Collections.IDictionary)typeof(SimRuntime).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(r)!;
 if (pending.Count != 0) throw new Exception("Failed write left pending state: " + field);
}
if (HdlObject.Root().Integer().Read() != -2147483649L) throw new Exception("Native long was truncated");
Console.WriteLine($"PASS registration: {count} callback kinds cleanly rejected; 100 failed timer tasks settled; failed write leaves no state; signed 64-bit transport passed");
''')
    run([SDK / 'dotnet', 'build', BUILD / 'Faults.csproj', '-c', 'Release'])
    results = []
    for case in ['version', 'legacy', 'missing', 'registration', 'unknown']:
        env = dict(os.environ, LD_LIBRARY_PATH=str(BUILD / ('registration' if case == 'unknown' else case)))
        if case == 'unknown':
            env.update(CS_FAKE_PRODUCT='Unknown Simulator', CS_FAKE_WIDTH='8')
        p = run([SDK / 'dotnet', BUILD / 'bin/Release/net10.0/Faults.dll', case], env=env)
        results.append({'case': case, 'stdout': p.stdout.strip(), 'stderr': p.stderr.strip(), 'exit': p.returncode})
        print(p.stdout.strip())
    p = run([shutil.which('python3'), ROOT / 'tools/packed_boundary.py', BUILD / 'registration' / host.name],
            env=dict(os.environ, LD_LIBRARY_PATH=str(BUILD / 'registration')))
    print(p.stdout.strip())
    results.append({'case': 'packed-boundary', 'stdout': p.stdout.strip(), 'stderr': p.stderr.strip(), 'exit': p.returncode})
    (ROOT / 'build/native-faults.json').write_text(json.dumps(results, indent=2) + '\n')


if __name__ == '__main__':
    main()
