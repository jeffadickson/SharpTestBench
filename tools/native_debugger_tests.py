#!/usr/bin/env python3
"""Linux GDB startup/finish breakpoint check, separate from managed attachment."""
import json, os, shlex, shutil, subprocess, sys
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]

def main():
    if sys.platform != 'linux': raise SystemExit('Native debugger check currently requires Linux')
    gdb = shutil.which('gdb')
    vvp = os.environ.get('CS_VVP') or shutil.which('vvp')
    if not gdb or not vvp: raise SystemExit('Install gdb and Icarus first')
    work = ROOT / 'build/native-debugger-check'
    work.mkdir(parents=True, exist_ok=True)
    commands = work / 'commands.gdb'
    commands.write_text('''set pagination off
set confirm off
set breakpoint pending on
handle SIG34 nostop noprint pass
handle SIGPWR nostop noprint pass
break sharptestbench_initialize
commands
silent
printf "STB_NATIVE_START\\n"
bt 4
continue
end
break cs_v1_gpi_finish
commands
silent
printf "STB_NATIVE_FINISH\\n"
bt 4
continue
end
run
''')
    wrapper = work / 'debug-vvp'
    wrapper.write_text('#!/bin/sh\nexec '+ ' '.join(shlex.quote(x) for x in
        [gdb, '--batch', '--return-child-result', '-x', str(commands), '--args', vvp]) + ' "$@"\n')
    wrapper.chmod(0o755)
    run = subprocess.run([sys.executable, str(ROOT / 'tools/run.py'), '--no-build', '--sim', 'icarus',
        '--assembly', str(ROOT / 'examples/Counter/bin/Release/net10.0/Counter.dll'),
        '--source', str(ROOT / 'examples/Counter/counter.sv'), '--top', 'counter',
        '--entry-type', 'Entry, Counter', '--work-dir', str(work), '--label', 'native-debugger'],
        env=dict(os.environ, CS_VVP=str(wrapper)), text=True, capture_output=True, timeout=150)
    output = run.stdout + run.stderr
    (work / 'driver.log').write_text(output)
    if run.returncode or any(marker not in output for marker in ('STB_NATIVE_START','STB_NATIVE_FINISH','exited normally')):
        raise RuntimeError('Native debugger check failed:\n'+output)
    if 'fatal library error' in output.lower(): raise RuntimeError('Historical fatal diagnostic reproduced')
    (work / 'evidence.json').write_text(json.dumps({'startup_breakpoint': True, 'finish_breakpoint': True,
        'normal_exit': True, 'fatal_diagnostic': False, 'scope': 'separate native debugger session; not simultaneous managed/native debugging'}, indent=2))
    print('NATIVE_DEBUGGER_PASS')

if __name__ == '__main__': main()
