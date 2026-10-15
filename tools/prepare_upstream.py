#!/usr/bin/env python3
"""Extract pinned source and set archive build metadata without relying on Git tags."""
import hashlib
import json
from pathlib import Path
import tarfile
import tomllib

ROOT = Path(__file__).resolve().parents[1]


def configure_native_build(source):
    """Apply a checked build-only overlay; keep all pinned implementation files intact."""
    cmake = source / 'CMakeLists.txt'
    original = cmake.read_text()
    text = original
    replacements = {
        'set(CMAKE_CXX_STANDARD 11)': 'set(CMAKE_CXX_STANDARD 20)',
        '"-std=c++11"': '"-std=c++20"',
        '"/MD /O2 /Ob2 /DNDEBUG"': '"/MD /O2 /Ob2 /Zi /Zo /DNDEBUG"',
    }
    for before, after in replacements.items():
        if text.count(before) != 1:
            raise RuntimeError('Pinned CMake layout changed; review C++20 overlay: ' + before)
        text = text.replace(before, after)
    anchor = 'set(CMAKE_CXX_STANDARD_REQUIRED ON)'
    overlay = r'''set(CMAKE_CXX_EXTENSIONS OFF)
# SharpTestBench release profile: require IPO, retain debugging, preserve real semantics.
set(CMAKE_BUILD_TYPE Release CACHE STRING "SharpTestBench native configuration" FORCE)
include(CheckIPOSupported)
check_ipo_supported(RESULT STB_IPO_SUPPORTED OUTPUT STB_IPO_ERROR LANGUAGES CXX)
if(NOT STB_IPO_SUPPORTED)
    message(FATAL_ERROR "SharpTestBench requires native IPO: ${STB_IPO_ERROR}")
endif()
set(CMAKE_INTERPROCEDURAL_OPTIMIZATION_RELEASE ON)
if(NOT MSVC)
    set(CMAKE_CXX_FLAGS_RELEASE "-O3 -g -DNDEBUG" CACHE STRING "Native release flags" FORCE)
else()
    string(APPEND CMAKE_SHARED_LINKER_FLAGS_RELEASE " /DEBUG:FULL /OPT:REF /OPT:ICF")
endif()
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)
'''
    if text.count(anchor) != 1:
        raise RuntimeError('Pinned CMake standard configuration changed')
    text = text.replace(anchor, anchor + '\n' + overlay)
    cmake.write_text(text)
    evidence = {'language': 'C++20', 'configuration': 'Release', 'ipo_required': True,
                'original_cmake_sha256': hashlib.sha256(original.encode()).hexdigest(),
                'configured_cmake_sha256': hashlib.sha256(cmake.read_bytes()).hexdigest(),
                'implementation_sources_modified': False}
    (source.parent / 'native-profile.json').write_text(json.dumps(evidence, indent=2) + '\n')


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
    # scikit-build otherwise strips Unix debug information during installation.
    section = '[tool.scikit-build]'
    if text.count(section) != 1:
        raise RuntimeError('Pinned scikit-build configuration changed')
    text = text.replace(section, section + '\ncmake.build-type = "Release"\ninstall.strip = false')
    tomllib.loads(text)
    project.write_text(text)
    configure_native_build(project.parent)
    print(project.parent)


if __name__ == '__main__':
    main()
