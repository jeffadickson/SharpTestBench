#!/usr/bin/env python3
"""Validate installed Linux packages in a separate runtime-only Docker image."""
import argparse, hashlib, json, shutil, subprocess
from pathlib import Path
import xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[1]
SDK='mcr.microsoft.com/dotnet/sdk:10.0.402@sha256:0d99ebf1ccad15037b5c98c9fe8979d6b8dbac23812feae2277a0b277b5c5ffa'
RUNTIME='mcr.microsoft.com/dotnet/runtime:10.0.13@sha256:939555726cae4c916d212323ac3c0792996dcac2614f00e82cc40fcf059e1af3'

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--artifacts',type=Path,required=True);a=p.parse_args()
    evidence=list(a.artifacts.rglob('package-evidence-linux-x64/packages.json'))
    if len(evidence)!=1:raise RuntimeError('Expected one Linux package evidence set')
    index=json.loads(evidence[0].read_text());version=index['version']
    if index['source_dirty']:raise RuntimeError('Container gate requires clean CI packages')
    context=ROOT/'build/container-consumer';context.mkdir(parents=True,exist_ok=True)
    feed=context/'packages';feed.mkdir(exist_ok=True)
    for old in feed.glob('*.nupkg'):old.unlink()
    for name,digest in index['sha256'].items():
        paths=list(a.artifacts.rglob('packages-linux-x64/'+name))
        if len(paths)!=1 or hashlib.sha256(paths[0].read_bytes()).hexdigest()!=digest:raise RuntimeError('Missing/corrupt package: '+name)
        shutil.copy2(paths[0],feed/name)
    for name in ('Tests.cs','Counter.stb.xml','counter.sv'):shutil.copy2(ROOT/'examples/Counter'/name,context/name)
    project=ET.Element('Project',Sdk='Microsoft.NET.Sdk');props=ET.SubElement(project,'PropertyGroup')
    for k,v in dict(TargetFramework='net10.0',ImplicitUsings='enable',Nullable='enable',GenerateRuntimeConfigurationFiles='true',RestorePackagesPath='/cache').items():ET.SubElement(props,k).text=v
    items=ET.SubElement(project,'ItemGroup')
    for name in ('SharpTestBench','SharpTestBench.Generators','SharpTestBench.Native.linux-x64'):ET.SubElement(items,'PackageReference',Include=name,Version=version)
    ET.SubElement(items,'AdditionalFiles',Include='Counter.stb.xml');ET.ElementTree(project).write(context/'Counter.csproj',encoding='unicode')
    (context/'Dockerfile').write_text(f'''FROM {SDK} AS compile
WORKDIR /consumer
COPY packages /feed
COPY Counter.csproj Tests.cs Counter.stb.xml counter.sv ./
RUN dotnet build -c Release --source /feed --disable-build-servers -m:1
FROM {RUNTIME}
RUN apt-get update && apt-get install -y --no-install-recommends python3 iverilog && rm -rf /var/lib/apt/lists/*
COPY --from=compile /consumer/bin/Release/net10.0/ /test/
COPY --from=compile /cache/sharptestbench.native.linux-x64/{version}/ /package/
COPY counter.sv /test/counter.sv
ENV CS_DOTNET_ROOT=/usr/share/dotnet
WORKDIR /results
ENTRYPOINT ["python3", "-I", "/package/tools/installed_run.py", "--native-dir", "/package/runtimes/linux-x64/native", "--assembly", "/test/Counter.dll", "--source", "/test/counter.sv", "--top", "counter", "--entry-type", "Entry, Counter", "--work-dir", "/results"]
''')
    result=ROOT/'build/container-evidence';result.mkdir(parents=True,exist_ok=True)
    tag='sharptestbench-consumer:'+index['source_revision'][:12]
    with (result/'build.log').open('w') as log:subprocess.run(['docker','build','--platform','linux/amd64','-t',tag,str(context)],stdout=log,stderr=subprocess.STDOUT,check=True,timeout=600)
    with (result/'driver.log').open('w') as log:subprocess.run(['docker','run','--rm','--platform','linux/amd64','--network','none','--read-only','--tmpfs','/tmp','--mount',f'type=bind,source={result},target=/results',tag],stdout=log,stderr=subprocess.STDOUT,check=True,timeout=120)
    rows=json.loads((result/'results.json').read_text())
    if len(rows)!=1 or rows[0]['Name']!='ResetAndCount' or rows[0]['Status']!='passed':raise RuntimeError('Wrong/missing installed test result')
    (result/'environment.json').write_text(json.dumps(dict(sdk=SDK,runtime=RUNTIME,source_revision=index['source_revision'],version=version,network='disabled during simulation',repository_mount=False,cocotb_python_installed=False),indent=2))
    print('CONTAINER_CONSUMER_PASS')
if __name__=='__main__':main()
