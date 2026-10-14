#!/usr/bin/env python3
"""Extract pinned source and set archive build metadata without relying on Git tags."""
import hashlib
import json
from pathlib import Path
import tarfile
import tomllib

ROOT = Path(__file__).resolve().parents[1]


def main():
    metadata = json.loads((ROOT / 'third_party/UPSTREAM.json').read_text())
    archive = ROOT / 'third_party' / metadata['source_archive']
    if hashlib.sha256(archive.read_bytes()).hexdigest() != metadata['source_sha256']:
        raise RuntimeError('Upstream archive checksum does not match pinned provenance')
    destination = ROOT / 'build/upstream'
    destination.mkdir(parents=True, exist_ok=True)
    with tarfile.open(archive) as source:
        source.extractall(destination, filter='data')
    project = destination / 'cocotb-source/pyproject.toml'
    text = project.read_text()
    dynamic = 'dynamic = ["version"]'
    provider = '[tool.scikit-build.metadata.version]\nprovider = "setuptools_git_versioning.scikit_metadata"'
    if text.count(dynamic) != 1 or text.count(provider) != 1:
        raise RuntimeError('Pinned upstream packaging layout changed; review preparation')
    text = text.replace(dynamic, 'version = ' + json.dumps(metadata['version'])).replace(provider, '')
    parsed = tomllib.loads(text)
    assert parsed['project']['version'] == metadata['version']
    project.write_text(text)
    print(project.parent)


if __name__ == '__main__':
    main()
