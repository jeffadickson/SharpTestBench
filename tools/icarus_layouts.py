#!/usr/bin/env python3
"""Export elaborated packed ranges with an optional C++20 Icarus target on Linux, macOS and Windows."""
import argparse, hashlib, json, os, platform, re, shutil, subprocess, sys, tempfile
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]


def target_flags(system, compiler_version):
    """Select native plugin linkage and vectorization without hardcoding a CPU ISA.

    macOS bundles resolve public ivl symbols from their loading executable.
    Windows uses a PE DLL with static compiler runtimes and an ivl import library.
    Native toolchains select ARM64 on ARM64 hosts; no x86 instruction flags apply.
    """
    profile = ['-std=c++20', '-O3', '-flto']
    profile += (['-fvectorize', '-fslp-vectorize'] if 'clang' in compiler_version.lower()
                else ['-ftree-loop-vectorize', '-ftree-slp-vectorize'])
    if system == 'darwin': return profile + ['-bundle', '-fPIC', '-undefined', 'dynamic_lookup']
    if system == 'win32': return profile + ['-shared', '-static']
    if system == 'linux': return profile + ['-shared', '-fPIC']
    raise ValueError('Unsupported Icarus target platform: ' + system)


def export(sources, top, output, base, include):
    """Stage a private compiler target, validate its output, then atomically publish metadata."""
    if sys.platform not in ('linux', 'darwin', 'win32'):
        raise RuntimeError('Unsupported Icarus target platform: ' + sys.platform)
    sources = [Path(p).resolve() for p in sources]
    output = Path(output).resolve(); output.parent.mkdir(parents=True, exist_ok=True)
    base = Path(base).resolve(); include = Path(include).resolve()
    with tempfile.TemporaryDirectory(prefix='stb-layouts-') as folder:
        stage = Path(folder)
        # Windows symlinks require privileges or Developer Mode. Copy this small
        # private compiler directory instead; never mutate the installed compiler.
        for p in base.iterdir():
            if sys.platform == 'win32':
                if p.is_dir(): shutil.copytree(p, stage/p.name)
                else: shutil.copy2(p, stage/p.name)
            else: (stage/p.name).symlink_to(p, target_is_directory=p.is_dir())
        helper = stage/'stb_layouts.tgt'
        compiler_cxx = os.environ.get('CXX', 'clang++' if sys.platform == 'darwin' else 'g++')
        version = subprocess.check_output([compiler_cxx, '--version'], text=True).splitlines()[0]
        flags = target_flags(sys.platform, version)
        imports = []
        if sys.platform == 'win32':
            # MinGW links target imports against ivl.exe (the documented target
            # ABI), not the iverilog driver or the VVP runtime. Generate only the
            # public function imports this helper actually calls. The loader
            # rejects incompatible/missing exports when Icarus loads the target.
            definition = stage/'stb_ivl.def'
            names = sorted(set(re.findall(r'\b(ivl_\w+)\s*\(',
                (ROOT/'native/icarus_layouts.cpp').read_text())))
            definition.write_text('LIBRARY ivl.exe\nEXPORTS\n' + '\n'.join(names) + '\n')
            library = stage/'stb_ivl.a'
            subprocess.run([os.environ.get('DLLTOOL', 'dlltool'), '-d', str(definition),
                            '-l', str(library)], check=True)
            imports = [str(library)]
        command = [compiler_cxx, *flags, '-Wall', '-Wextra', '-Werror',
            '-I', str(include), str(ROOT/'native/icarus_layouts.cpp'), *imports, '-o', str(helper)]
        subprocess.run(command, check=True)
        (stage/'stb_layouts.conf').write_text('flag:DLL='+helper.as_posix()+'\n')
        raw = stage/'layouts.json'
        compiler = os.environ.get('CS_IVERILOG','iverilog')
        subprocess.run([compiler,'-B',str(stage),'-g2012','-s',top,'-t','stb_layouts','-o',str(raw),*map(str,sources)],check=True)
        data = json.loads(raw.read_text())
        if data.get('schema') != 1 or not data.get('signals'): raise RuntimeError('No packed metadata exported')
        paths = [tuple(row['path']) for row in data['signals']]
        if len(paths) != len(set(paths)): raise RuntimeError('Ambiguous exported paths')
        data['provenance'] = dict(top=top, sources={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in sources},
            helper_sha256=hashlib.sha256((ROOT/'native/icarus_layouts.cpp').read_bytes()).hexdigest(),
            header_sha256=hashlib.sha256((include/'ivl_target.h').read_bytes()).hexdigest(),
            compiler_profile=flags, compiler=version, platform=sys.platform, architecture=platform.machine(),
            limitation='Separate elaboration; not authenticated against a simulation binary. No automatic runtime import.')
        # Failed compiler runs never replace an existing exported document.
        temporary = output.with_suffix(output.suffix+'.tmp')
        try:
            temporary.write_text(json.dumps(data,indent=2)+'\n'); temporary.replace(output)
        finally: temporary.unlink(missing_ok=True)
    return data


def main():
    """Require explicit compiler/include locations; do not search unrelated installed compilers."""
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--source',type=Path,action='append',required=True)
    p.add_argument('--top',required=True);p.add_argument('--output',type=Path,required=True)
    p.add_argument('--ivl-root',type=Path,required=True);p.add_argument('--include',type=Path,required=True)
    a=p.parse_args();data=export(a.source,a.top,a.output,a.ivl_root,a.include)
    print('PACKED_LAYOUTS_EXPORTED',len(data['signals']))

if __name__=='__main__':main()
