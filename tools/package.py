#!/usr/bin/env python3
"""Build a local NuGet package and a consumer with no project/source reference.

Run the Icarus and NVC suites first to generate both binding files. Nothing is published.
"""
import os
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[1]
SDK = Path(os.environ["CS_DOTNET_ROOT"]) if "CS_DOTNET_ROOT" in os.environ else Path(shutil.which("dotnet")).resolve().parent


def run(*args):
    subprocess.run([str(x) for x in args], check=True)


def main():
    build = ROOT / "build"
    build.mkdir(exist_ok=True)
    headers = sorted((SDK / "packs/Microsoft.NETCore.App.Host.linux-x64").glob("*/runtimes/linux-x64/native"))[-1]
    temporary = build / f"host-package-{os.getpid()}.so"
    try:
        run("gcc", "-shared", "-fPIC", "-Wl,-soname,libsharptestbench_host.so", "-Wall", "-Wextra", "-Werror", ROOT / "native/host.c", ROOT / "native/abi.c", "-I", ROOT / "native/upstream", "-I", headers,
            "-ldl", "-o", temporary)
        temporary.replace(build / "libsharptestbench_host.so")
    finally:
        temporary.unlink(missing_ok=True)
    feed = build / "packages"
    run(SDK / "dotnet", "pack", ROOT / "src/SharpTestBench/SharpTestBench.csproj", "-c", "Release", "-o", feed)
    consumer = build / "package-consumer"
    consumer.mkdir(exist_ok=True)
    # An isolated packages path prevents an older local package with the same version being reused.
    cache = consumer / "nuget-cache"
    if cache.exists():
        shutil.rmtree(cache)
    for name in ("DutBindings.cs", "VhdlBindings.cs"):
        if not (build / name).is_file():
            raise SystemExit(f"Run integration suites first: missing {name}")
        shutil.copy2(build / name, consumer / name)
    (consumer / "NuGet.Config").write_text('<configuration><packageSources><clear/><add key="local" value="' + str(feed) + '"/></packageSources></configuration>')
    (consumer / "PackageCheck.csproj").write_text('''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
<TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors><GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>
<RestorePackagesPath>nuget-cache</RestorePackagesPath></PropertyGroup><ItemGroup>
<PackageReference Include="SharpTestBench" Version="0.8.0-alpha"/></ItemGroup></Project>''')
    (consumer / "Entry.cs").write_text('''using SharpTestBench;
using System.Runtime.InteropServices;
public static class Entry {
 [UnmanagedCallersOnly] public static int Initialize(nint p, int n) => Regression.Initialize(typeof(Entry).Assembly);
 [SimTest] public static async Task InstalledPackage(TestContext c) {
  var packed = c.Dut.Child("data").Signal<W8>();
  bool verilator = c.Runtime.Simulator.Contains("Verilator", StringComparison.OrdinalIgnoreCase);
  var expected = LogicVector<W8>.Parse(verilator ? "10000011" : "10XZ0011");
  packed.Write(expected);
  if (c.Runtime.Simulator.Contains("NVC", StringComparison.OrdinalIgnoreCase)) {
   var dut = new GeneratedVhdl.VhdlBindings(c.Dut);
   dut.words[3].Write(new UInt<GeneratedVhdl.Width8>(211));
   dut.packet.payload.Write(new UInt<GeneratedVhdl.Width8>(23));
   dut.text_data.Write("READY");
   await Triggers.ReadOnly();
   if (dut.words[3].ReadUInt().Value != 211 || dut.packet.payload.ReadUInt().Value != 23 || dut.text_data.Read() != "READY")
    throw new Exception("Packaged VHDL bindings failed");
  } else {
   var dut = new Generated.DutBindings(c.Dut);
   var signal = c.Dut.Child("packed_matrix").Signal<Generated.Width48>();
   var matrix = verilator ? signal.PackedInferred<W8>() : signal.Packed<W8>(HdlRange.DownTo(1, 0), HdlRange.DownTo(2, 0));
   if (verilator && !HdlObject.Packages().Objects.Any(o => o.Name.Contains("discovery_pkg")))
    throw new Exception("Packaged HDL package discovery failed");
   matrix[1, 1].Write(new UInt<W8>(0xC9));
   dut.wide.Write(new UInt<Generated.Width256>((System.Numerics.BigInteger.One << 255) | 123));
   dut.memory[2].Write(new UInt<Generated.Width8>(199));
   dut.unit0.local_data.Write(new UInt<Generated.Width8>(71));
   await Triggers.ReadOnly();
   if (c.Dut.Child("matrix_middle").Signal<W8>().ReadUInt().Value != 0xC9 || dut.wide.Read().Value != ((System.Numerics.BigInteger.One << 255) | 123) || dut.memory[2].Read().Value != 199 || dut.unit0.local_data.Read().Value != 71)
    throw new Exception("Packaged Verilog bindings failed");
  }
  if (packed.Read() != expected) throw new Exception("Packaged packed-plane X/Z transfer failed");
 }
}''')
    run(SDK / "dotnet", "restore", consumer / "PackageCheck.csproj", "--force", "--no-cache", "--configfile", consumer / "NuGet.Config")
    run(SDK / "dotnet", "build", consumer / "PackageCheck.csproj", "-c", "Release", "--no-restore")
    print("PACKAGE_BUILD_PASS: isolated NuGet consumer and both generated binding trees compile")


if __name__ == "__main__":
    main()
