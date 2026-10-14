"""Verify generated bindings compile; wrong widths and signedness do not."""
import os,shutil,subprocess
from pathlib import Path
root=Path(__file__).resolve().parents[1];out=root/'build/typecheck';out.mkdir(parents=True,exist_ok=True)
sdk=Path(os.environ['CS_DOTNET_ROOT']) if 'CS_DOTNET_ROOT' in os.environ else Path(shutil.which('dotnet')).resolve().parent
(out/'Check.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include="{root}/src/SharpTestBench/SharpTestBench.csproj"/><Compile Include="../DutBindings.cs"/></ItemGroup></Project>')
(out/'Wrong.cs').write_text('using SharpTestBench; public class Check { public void Good(UnsignedSignal<W8> s) {s.Write(new UInt<W8>(1));} public void Field(PackedField<W16,W4> f) {f.Write(new UInt<W4>(1));}}')
def build():return subprocess.run([str(sdk/'dotnet'),'build',str(out/'Check.csproj'),'-c','Release','--nologo'],capture_output=True,text=True)
p=build();assert p.returncode==0,p.stdout+p.stderr
logs=['Generated bindings + valid typed call: PASS']
for name,expr in [('width','new UInt<W16>(1)'),('signedness','new SInt<W8>(1)')]:
 (out/'Wrong.cs').write_text(f'using SharpTestBench; public class Check {{ public void Wrong(UnsignedSignal<W8> s) {{s.Write({expr});}}}}')
 p=build();assert p.returncode!=0 and 'CS1503' in p.stdout,p.stdout+p.stderr;logs.append(name+': correctly rejected by compiler\n'+p.stdout)
(out/'Wrong.cs').write_text('using SharpTestBench; public class Check { public void Wrong(PackedField<W16,W4> field) {field.Write(new UInt<W8>(1));}}')
p=build();assert p.returncode!=0 and 'CS1503' in p.stdout,p.stdout+p.stderr;logs.append('packed field width: correctly rejected by compiler\n'+p.stdout)
(out/'Wrong.cs').unlink();(root/'build/typecheck.log').write_text('\n'.join(logs));print('TYPECHECK_PASS generated bindings compile; incompatible widths/signs and packed-field widths rejected')
