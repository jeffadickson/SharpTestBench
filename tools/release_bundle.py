#!/usr/bin/env python3
"""Assemble only validated packages from one completed native CI run; no network/push."""
import argparse, hashlib, json, shutil, zipfile
from pathlib import Path
import xml.etree.ElementTree as ET
RIDS=('linux-x64','win-x64','osx-arm64')

def metadata(path):
    with zipfile.ZipFile(path) as z:
        names=[n for n in z.namelist() if n.endswith('.nuspec')]
        if len(names)!=1:raise ValueError('Expected one nuspec: '+str(path))
        root=ET.fromstring(z.read(names[0]));m=root.find('{*}metadata')
        return m.findtext('{*}id'),m.findtext('{*}version')

def assemble(artifacts, output, revision):
    if output.exists() and any(output.iterdir()):raise ValueError('Output directory must be empty')
    selected=[];version=None
    for rid in RIDS:
        manifests=list(artifacts.rglob(f'package-evidence-{rid}/packages.json'))
        if len(manifests)!=1:raise ValueError('Missing or duplicate validated evidence: '+rid)
        folder=manifests[0].parent;index=json.loads(manifests[0].read_text())
        if index['source_revision']!=revision or index['source_dirty']:raise ValueError('Unverified source revision: '+rid)
        if index['rid']!=rid:raise ValueError('RID mismatch')
        if version is None:version=index['version']
        if version!=index['version']:raise ValueError('Mixed package versions')
        rows=json.loads((folder/'results.json').read_text())
        if len(rows)!=1 or rows[0]['Status']!='passed' or not rows[0]['Name'].endswith('ResetAndCount'):raise ValueError('Installed consumer did not pass expected test: '+rid)
        if set(json.loads((folder/'negative-checks.json').read_text()))!={'rid','checksum'}:raise ValueError('Missing negative checks')
        native=json.loads((folder/'manifest.json').read_text())
        if native['source_revision']!=revision or native['source_dirty'] or native['package_version']!=version:raise ValueError('Native provenance mismatch')
        wanted={'SharpTestBench.Native.'+rid}
        if rid=='linux-x64':wanted.update(('SharpTestBench','SharpTestBench.Generators'))
        found=set()
        for filename,digest in index['sha256'].items():
            if Path(filename).name!=filename:raise ValueError('Unsafe package name')
            paths=list(artifacts.rglob(f'packages-{rid}/{filename}'))
            if len(paths)!=1:raise ValueError('Missing or duplicate package '+filename)
            path=paths[0]
            if hashlib.sha256(path.read_bytes()).hexdigest()!=digest:raise ValueError('Package checksum mismatch: '+filename)
            package_id,package_version=metadata(path)
            if package_version!=version:raise ValueError('Stale package in feed: '+filename)
            if package_id in wanted:
                if package_id in found:raise ValueError('Duplicate package identity')
                found.add(package_id);selected.append((path,digest,package_id))
        if found!=wanted:raise ValueError('Missing required package identities: '+rid)
    if len(selected)!=5:raise ValueError('Release must contain exactly five packages')
    output.mkdir(parents=True,exist_ok=True)
    manifest=dict(version=version,source_revision=revision,packages=[])
    for source,digest,package_id in selected:
        shutil.copy2(source,output/source.name)
        manifest['packages'].append(dict(id=package_id,file=source.name,sha256=digest))
    (output/'release-manifest.json').write_text(json.dumps(manifest,indent=2))
    (output/'SHA256SUMS').write_text(''.join(f'{digest}  {source.name}\n' for source,digest,_ in selected))
    return manifest

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--artifacts',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--revision',required=True);a=p.parse_args()
    print(json.dumps(assemble(a.artifacts,a.output,a.revision),indent=2))
if __name__=='__main__':main()
