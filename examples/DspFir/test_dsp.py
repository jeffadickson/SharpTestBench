"""Released-cocotb FIR scoreboard; arbitrary-precision dot product reference."""
import gc, json, os, time
from pathlib import Path
import cocotb
from cocotb.triggers import Timer, ReadOnly

@cocotb.test()
async def fir_scoreboard(dut):
    """Compare every visible handshake and output, retaining pending inputs on stalls."""
    assert cocotb.__version__ == '2.1.0'
    plan = json.loads(Path(os.environ['DSP_PLAN']).read_text())
    pending = None
    history = [0, 0, 0]
    expected, occupied = 0, False
    accepted = consumed = stalls = 0
    trace = []
    counts = [g['collections'] for g in gc.get_stats()]
    start = time.perf_counter()
    for cycle, row in enumerate(plan):
        if row['Reset']: pending = None
        elif pending is None and row['Offer']: pending = row['Sample']
        for name, value in dict(clk=0, reset=row['Reset'], out_ready=row['Ready'],
            in_valid=pending is not None, sample=(pending or 0) & 65535,
            fault=int(os.getenv('DSP_FAULT', '0'))).items():
            getattr(dut, name).value = int(value)
        await Timer(1, unit='step'); await ReadOnly()
        if cycle:
            can_accept = not occupied or row['Ready']
            actual_valid = bool(dut.out_valid.value)
            raw = int(dut.result.value) if actual_valid else 0
            actual = raw - (1 << 32) if raw & (1 << 31) else raw
            assert bool(dut.in_ready.value) == can_accept and actual_valid == occupied and (not occupied or actual == expected), f'DSP_MISMATCH cycle={cycle} expected={expected} actual={actual}'
            trace.append([cycle, int(can_accept), int(actual_valid), actual])
            if not row['Reset']:
                consumed += occupied and row['Ready']
                stalls += occupied and not row['Ready']
                if can_accept:
                    occupied = pending is not None
                    if occupied:
                        expected = sum(x * k for x, k in zip([pending] + history, [-3, 7, 11, -2]))
                        history = [pending] + history[:2]
                        pending = None
                        accepted += 1
        if row['Reset']: occupied, expected, history = False, 0, [0, 0, 0]
        await Timer(1, unit='step'); dut.clk.value = 1
        await Timer(1, unit='step'); await ReadOnly(); await Timer(1, unit='step')
    assert not occupied and pending is None and accepted and stalls, 'DSP_DRAIN'
    Path(os.environ['DSP_REPORT']).write_text(json.dumps(dict(trace=trace, accepted=accepted,
        consumed=consumed, stalls=stalls, cycles=len(plan), seconds=time.perf_counter()-start,
        gc_collections=[g['collections'] - n for g, n in zip(gc.get_stats(), counts)])))
