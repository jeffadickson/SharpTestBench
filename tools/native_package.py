#!/usr/bin/env python3
"""Pack managed/generator/native NuGets and run an external consumer using only the local feed."""
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
from cocotb_tools import config
from native_build import build_host, sdk_root, HOST_NAME

ROOT = Path(__file__).resolve().parents[1]
VERSION = '0.8.0-alpha'
def run(*args, **kwargs): subprocess.run(list(map(str, args)), check=True, **kwargs)
def main():
    sdk = sdk_root()
    arch = {'x86_64':'x64','amd64':'x64','aarch64':'arm64','arm64':'arm64'}[platform.machine().lower()]
    rid = ('win' if os.name == 'nt' else 'osx' if sys.platform == 'darwin' else 'linux') + '-' + arch
    build = ROOT / 'build'
    feed = build / ('packages-' + rid)
    feed.mkdir(parents=True, exist_ok=True)
    host = build_host(ROOT, sdk, build)
    for project in ['SharpTestBench', 'SharpTestBench.Generators']:
        run(sdk / 'dotnet', 'pack', ROOT / f'src/{project}/{project}.csproj', '-c', 'Release', '-o', feed, '-p:RestoreLockedMode=true', '--disable-build-servers', '-m:1')
    stage = build / ('native-package-' + rid)
    stage.mkdir(exist_ok=True)
    native = stage / 'native'
    native.mkdir(exist_ok=True)
    gpi_path, gpi_entry = config.gpi_entry_point().rsplit(',', 1)
    impl_path, impl_entry = config.gpi_impl('icarus','vpi').rsplit(':', 1)
    bootstrap = Path(config.lib_entry('vpi','icarus'))
    for source in [host, Path(gpi_path), Path(impl_path), bootstrap]: shutil.copy2(source, native / source.name)
    bootstrap_name = bootstrap.name
    if os.name == 'nt':
        bootstrap_name = bootstrap.stem + '.vpi'
        shutil.copy2(bootstrap, native / bootstrap_name)
    names = {host.name, Path(gpi_path).name, Path(impl_path).name, bootstrap.name, bootstrap_name}
    manifest = {'rid':rid, 'host':HOST_NAME, 'gpi':Path(gpi_path).name, 'gpi_entry':gpi_entry,
                'implementation':Path(impl_path).name,'implementation_entry':impl_entry, 'bootstrap':bootstrap_name,
                'upstream':json.loads((ROOT / 'third_party/UPSTREAM.json').read_text()),
                'sha256':{name:hashlib.sha256((native / name).read_bytes()).hexdigest() for name in sorted(names)}}
    (native / 'native-manifest.json').write_text(json.dumps(manifest, indent=2))
    package_id = 'SharpTestBench.Native.' + rid
    project = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
    props = ET.SubElement(project,'PropertyGroup')
    for key,value in {'TargetFramework':'net10.0','PackageId':package_id,'Version':VERSION,'IncludeBuildOutput':'false',
                      'SuppressDependenciesWhenPacking':'true','PackageLicenseExpression':'MIT AND BSD-3-Clause','NoWarn':'NU5128'}.items():
        ET.SubElement(props,key).text=value
    items = ET.SubElement(project,'ItemGroup')
    for source,destination in [(str(native / '*'),'runtimes/'+rid+'/native/'),(str(ROOT/'LICENSE'),'licenses/'),
                               (str(ROOT/'third_party/COCOTB-LICENSE'),'licenses/'),(str(ROOT/'tools/installed_run.py'),'tools/')]:
        ET.SubElement(items,'None',Include=source,Pack='true',PackagePath=destination)
    ET.ElementTree(project).write(stage/'Native.csproj',encoding='unicode')
    run(sdk/'dotnet','pack',stage/'Native.csproj','-c','Release','-o',feed,'--disable-build-servers','-m:1')
    # A temporary directory outside the checkout proves no project/native source references.
    with tempfile.TemporaryDirectory(prefix='sharptestbench-consumer-') as temp:
        consumer = Path(temp)
        for name in ['Tests.cs','counter.sv','Counter.stb.xml']:
            shutil.copy2(ROOT/'examples/Counter'/name,consumer/name)
        shutil.copy2(ROOT/'global.json',consumer/'global.json')
        config_xml=ET.Element('configuration');sources=ET.SubElement(config_xml,'packageSources');ET.SubElement(sources,'clear')
        ET.SubElement(sources,'add',key='local',value=str(feed))
        ET.ElementTree(config_xml).write(consumer/'NuGet.Config',encoding='unicode')
        project=ET.Element('Project',Sdk='Microsoft.NET.Sdk');props=ET.SubElement(project,'PropertyGroup')
        for key,value in {'TargetFramework':'net10.0','ImplicitUsings':'enable','Nullable':'enable','GenerateRuntimeConfigurationFiles':'true','RestorePackagesPath':str(consumer/'cache')}.items():ET.SubElement(props,key).text=value
        items=ET.SubElement(project,'ItemGroup')
        for name in ['SharpTestBench','SharpTestBench.Generators',package_id]:ET.SubElement(items,'PackageReference',Include=name,Version=VERSION)
        ET.SubElement(items,'AdditionalFiles',Include='Counter.stb.xml')
        ET.ElementTree(project).write(consumer/'Counter.csproj',encoding='unicode')
        run(sdk/'dotnet','build',consumer/'Counter.csproj','-c','Release','--disable-build-servers','-m:1',cwd=consumer)
        installed=consumer/'cache'/package_id.lower()/VERSION
        run(sys.executable,'-I',installed/'tools/installed_run.py','--native-dir',installed/'runtimes'/rid/'native',
            '--assembly',consumer/'bin/Release/net10.0/Counter.dll','--source',consumer/'counter.sv','--top','counter',
            '--entry-type','Entry, Counter','--work-dir',consumer/'simulation',cwd=consumer)
        evidence=build/('package-evidence-'+rid)
        evidence.mkdir(exist_ok=True)
        for name in ['results.json','results.xml','run.log']:shutil.copy2(consumer/'simulation'/name,evidence/name)
        (evidence/'manifest.json').write_text(json.dumps(manifest,indent=2))
    print('NATIVE_PACKAGE_PASS',rid,'isolated installed counter with packaged generator and native dependencies')
if __name__=='__main__':main()
