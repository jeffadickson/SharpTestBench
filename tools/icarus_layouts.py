#!/usr/bin/env python3
"""Export elaborated packed ranges with an optional C++20 Icarus target (Linux only)."""
import argparse, hashlib, json, os, subprocess, sys, tempfile
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]


def export(sources, top, output, base, include):
    """Stage a private compiler target, validate its output, then atomically publish metadata."""
    if sys.platform != 'linux': raise RuntimeError('Icarus elaboration helper currently validated only on Linux')
    sources = [Path(p).resolve() for p in sources]
    output = Path(output).resolve(); output.parent.mkdir(parents=True, exist_ok=True)
    base = Path(base).resolve(); include = Path(include).resolve()
    with tempfile.TemporaryDirectory(prefix='stb-layouts-') as folder:
        stage = Path(folder)
        for p in base.iterdir(): (stage/p.name).symlink_to(p, target_is_directory=p.is_dir())
        helper = stage/'stb_layouts.tgt'
        command = [os.environ.get('CXX','g++'), '-std=c++20', '-O3', '-flto', '-ftree-loop-vectorize',
            '-ftree-slp-vectorize', '-shared', '-fPIC', '-Wall', '-Wextra', '-Werror',
            '-I', str(include), str(ROOT/'native/icarus_layouts.cpp'), '-o', str(helper)]
        subprocess.run(command, check=True)
        (stage/'stb_layouts.conf').write_text('flag:DLL='+str(helper)+'\n')
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
            compiler_profile=command[1:10],
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
