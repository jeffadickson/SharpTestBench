#!/usr/bin/env python3
"""Check local release hashes and refuse versions already present on nuget.org."""
import argparse, hashlib, json, urllib.request, urllib.error
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--bundle',type=Path,required=True);a=p.parse_args()
m=json.loads((a.bundle/'release-manifest.json').read_text())
for package in m['packages']:
    path=a.bundle/package['file']
    if Path(package['file']).name!=package['file'] or hashlib.sha256(path.read_bytes()).hexdigest()!=package['sha256']:raise SystemExit('Release checksum mismatch')
    url='https://api.nuget.org/v3-flatcontainer/'+package['id'].lower()+'/index.json'
    try:
        with urllib.request.urlopen(url,timeout=30) as response: versions=json.load(response)['versions']
    except urllib.error.HTTPError as error:
        if error.code!=404:raise
        versions=[]
    if m['version'].lower() in [v.lower() for v in versions]:raise SystemExit('Version already exists: '+package['id'])
print('RELEASE_PREFLIGHT_PASS: all five versions absent; publishing remains a separate step')
