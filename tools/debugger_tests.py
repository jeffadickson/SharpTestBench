#!/usr/bin/env python3
"""Opt-in NetCoreDbg DAP validation inside Icarus; build DebugIntegration Debug first."""
import argparse, json, os, queue, subprocess, sys, threading, time
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--debugger', required=True, type=Path)
    args = parser.parse_args()
    work = ROOT / 'build/debugger-check'
    work.mkdir(parents=True, exist_ok=True)
    (work / 'evidence.json').write_text(json.dumps({'status': 'started; validation incomplete'}))
    pid_file = work / 'pid'
    pid_file.unlink(missing_ok=True)
    env = dict(os.environ, STB_DEBUG_PID=str(pid_file), STB_DEBUG_PTRACE='1')
    transcript = []
    sim_log = (work / 'driver.log').open('w')
    sim = subprocess.Popen([sys.executable, str(ROOT / 'tools/run.py'), '--no-build', '--sim', 'icarus',
        '--assembly', str(ROOT / 'tests/DebugIntegration/bin/Debug/net10.0/DebugIntegration.dll'),
        '--source', str(ROOT / 'examples/Counter/counter.sv'), '--top', 'counter',
        '--entry-type', 'Entry, DebugIntegration', '--work-dir', str(work), '--label', 'debugger'],
        env=env, stdout=sim_log, stderr=subprocess.STDOUT)
    debugger = None
    try:
        deadline = time.monotonic() + 30
        while not pid_file.exists():
            if sim.poll() is not None or time.monotonic() > deadline: raise RuntimeError('Simulator did not become attachable')
            time.sleep(.05)
        debugger = subprocess.Popen([str(args.debugger.resolve()), '--interpreter=vscode'], stdin=subprocess.PIPE,
                                    stdout=subprocess.PIPE, stderr=(work / 'debugger-stderr.log').open('w'))
        messages = queue.Queue()
        def reader():
            try:
                while True:
                    headers = {}
                    while True:
                        line = debugger.stdout.readline()
                        if not line: return
                        if line == b'\r\n': break
                        key, value = line.decode().split(':', 1)
                        headers[key.lower()] = value.strip()
                    message = json.loads(debugger.stdout.read(int(headers['content-length'])))
                    transcript.append(message)
                    messages.put(message)
            finally: messages.put({'event': 'eof'})
        threading.Thread(target=reader, daemon=True).start()
        seq = 0
        pending = []
        def receive(predicate):
            deadline = time.monotonic() + 30
            while True:
                for i, msg in enumerate(pending):
                    if predicate(msg): return pending.pop(i)
                msg = messages.get(timeout=max(.01, deadline-time.monotonic()))
                if msg.get('event') == 'eof': raise RuntimeError('Debugger exited')
                if predicate(msg): return msg
                pending.append(msg)
        def send(command, arguments=None, wait=True):
            nonlocal seq
            seq += 1
            current = seq
            data = json.dumps(dict(seq=seq, type='request', command=command, arguments=arguments or {})).encode()
            debugger.stdin.write(f'Content-Length: {len(data)}\r\n\r\n'.encode()+data)
            debugger.stdin.flush()
            if wait:
                response = receive(lambda m: m.get('request_seq') == current)
                if not response.get('success'): raise RuntimeError(response)
                return response.get('body', {})
        send('initialize', {'clientID': 'SharpTestBench', 'adapterID': 'coreclr', 'linesStartAt1': True, 'columnsStartAt1': True})
        send('attach', {'processId': int(pid_file.read_text()), 'justMyCode': False}, wait=False)
        receive(lambda m: m.get('event') == 'initialized')
        source = ROOT / 'tests/DebugIntegration/Entry.cs'
        line = next(i for i, s in enumerate(source.read_text().splitlines(), 1) if 'STB_BREAKPOINT' in s)
        send('setBreakpoints', {'source': {'path': str(source)}, 'breakpoints': [{'line': line}]})
        send('setExceptionBreakpoints', {'filters': ['all']})
        send('configurationDone')
        stopped = receive(lambda m: m.get('event') == 'stopped')
        assert stopped['body']['reason'] == 'breakpoint', stopped
        thread = stopped['body']['threadId']
        frames = send('stackTrace', {'threadId': thread})['stackFrames']
        value = send('evaluate', {'expression': 'inspectedValue', 'frameId': frames[0]['id'], 'context': 'watch'})
        assert value['result'] == '41', value
        send('continue', {'threadId': thread})
        stopped = receive(lambda m: m.get('event') == 'stopped')
        assert stopped['body']['reason'] == 'exception', stopped
        exception = send('exceptionInfo', {'threadId': stopped['body']['threadId']})
        assert 'STB_DEBUG_EXPECTED_EXCEPTION' in json.dumps(exception), exception
        send('setExceptionBreakpoints', {'filters': []})
        send('continue', {'threadId': stopped['body']['threadId']})
        assert sim.wait(timeout=30) == 1, 'Expected regression failure from fixture exception'
        results = json.loads((work / 'debugger-results.json').read_text())
        assert len(results) == 1 and results[0]['Status'] == 'failed', results
        assert 'STB_DEBUG_EXPECTED_EXCEPTION' in json.dumps(results), results
        (work / 'evidence.json').write_text(json.dumps({'breakpoint': True, 'local': value,
            'exception': exception, 'shutdown': 'expected test failure; simulator exited'}, indent=2))
        print('DEBUGGER_PASS')
    finally:
        if debugger is not None:
            debugger.kill(); debugger.wait()
        if sim.poll() is None:
            # Let run.py collect failure output and release its workspace lock.
            if pid_file.exists():
                try: os.kill(int(pid_file.read_text()), 9)
                except ProcessLookupError: pass
            try: sim.wait(timeout=5)
            except subprocess.TimeoutExpired:
                sim.kill(); sim.wait()
        sim_log.close()
        (work / 'dap-transcript.json').write_text(json.dumps(transcript, indent=2))

if __name__ == '__main__': main()
