#!/usr/bin/env python3
"""Verify elaboration metadata through C# views and independent HDL selections."""
import argparse, json, os, subprocess, sys
from pathlib import Path
from icarus_layouts import ROOT, export


def main():
    """Require real simulator success and rejection of a same-width, reversed-dimension document."""
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--ivl-root',type=Path,required=True);p.add_argument('--include',type=Path,required=True)
    a=p.parse_args();work=ROOT/'build/icarus-layout-check';work.mkdir(parents=True,exist_ok=True)
    source=ROOT/'tests/Integration/top.sv';metadata=work/'layouts.json'
    data=export([source],'top',metadata,a.ivl_root,a.include)
    rows={tuple(row['path']):row for row in data['signals']}
    assert rows['top','packed_matrix']['dimensions']==[[1,0],[2,0],[7,0]]
    assert rows['top','packed_mixed']['dimensions']==[[-1,0],[3,1],[7,0]]
    # A flattened packed struct does not establish field names or boundaries.
    assert rows['top','packed_packet']['dimensions']==[[15,0]]
    sdk=Path(os.environ['CS_DOTNET_ROOT'])/('dotnet.exe' if sys.platform == 'win32' else 'dotnet') if 'CS_DOTNET_ROOT' in os.environ else 'dotnet'
    subprocess.run([str(sdk),'build',str(ROOT/'tests/LayoutMetadata'),'-c','Release'],check=True)
    def run(label, path, expected=None):
        """Retain diagnostics and distinguish scoreboard rejection from unrelated setup failures."""
        folder=work/label;folder.mkdir(exist_ok=True)
        command=[sys.executable,str(ROOT/'tools/run.py'),'--no-build','--assembly',
            str(ROOT/'tests/LayoutMetadata/bin/Release/net10.0/LayoutMetadata.dll'),
            '--entry-type','Entry, LayoutMetadata','--source',str(source),'--top','top','--work-dir',str(folder)]
        result=subprocess.run(command,env=dict(os.environ,STB_LAYOUT_METADATA=str(path)),text=True,capture_output=True,timeout=120)
        log=result.stdout+result.stderr;(folder/'driver.log').write_text(log)
        if expected:
            if result.returncode==0 or expected not in log:raise RuntimeError(log)
        elif result.returncode or 'CS_SUMMARY 1 passed 0 failed' not in log:raise RuntimeError(log)
    run('valid',metadata)
    rows['top','packed_mixed']['dimensions'][0].reverse()
    wrong=work/'wrong-order.json';wrong.write_text(json.dumps(data))
    run('wrong-order',wrong,'LAYOUT_SELECTION_MISMATCH')
    data=json.loads(metadata.read_text())
    data['provenance']['sources'][str(source)]='0'*64
    stale=work/'stale.json';stale.write_text(json.dumps(data))
    run('stale',stale,'LAYOUT_SOURCE_CHANGED')
    print('ICARUS_LAYOUT_PASS original dimensions, HDL selections, reversed-order and stale-source negatives')

if __name__=='__main__':main()
