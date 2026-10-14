"""Released cocotb 2.1 semantic oracle: no assumptions about same-time tie order."""
import json
import os
from pathlib import Path
from asyncio import CancelledError
import cocotb
from cocotb.triggers import Timer, gather, select
from cocotb.simtime import get_sim_time

async def value(number, delay):
    await Timer(delay, unit='step')
    return number

cleanup_finished = False

async def cleanup():
    global cleanup_finished
    try:
        await Timer(100, unit='step')
        return 99
    finally:
        await Timer(3, unit='step')
        cleanup_finished = True

async def failure():
    await Timer(2, unit='step')
    raise ValueError('root')

@cocotb.test()
async def structured_reference(dut):
    global cleanup_finished
    cleanup_finished = False
    assert cocotb.__version__ == '2.1.0', cocotb.__version__
    trace = {'ordered': list(await gather(value(10, 4), value(20, 1))), 'empty': len(await gather())}
    start = get_sim_time(unit='step')
    index, result = await select(value(11, 2), cleanup())
    trace['selection'] = [index, result, int(get_sim_time(unit='step') - start), int(cleanup_finished)]
    borrowed = cocotb.start_soon(value(77, 9))
    await select(borrowed, value(88, 1))
    trace['borrowed_alive'] = not borrowed.done()
    await borrowed
    start = get_sim_time(unit='step')
    try:
        await gather(failure(), cleanup())
    except ValueError:
        trace['failure_after_cleanup'] = int(get_sim_time(unit='step') - start)
    start = get_sim_time(unit='step')
    outer = cocotb.start_soon(gather(cleanup()))
    await Timer(1, unit='step')
    outer.cancel()
    await Timer(1, unit='step')
    outer.cancel()
    try:
        await outer
    except CancelledError:
        trace['parent_cleanup'] = int(get_sim_time(unit='step') - start)
    async def suppress():
        try:
            await Timer(100, unit='step')
        except CancelledError:
            pass
        await Timer(2, unit='step')
        return 2
    _, result = await select(value(1, 1), suppress())
    trace['suppression_winner'] = result
    async def cancel_child():
        raise CancelledError()
    try:
        await gather(cancel_child(), value(2, 100))
    except CancelledError:
        trace['child_cancel'] = True
    async def cleanup_failure():
        try:
            await Timer(100, unit='step')
            return 2
        finally:
            raise ValueError('cleanup')
    try:
        await select(value(1, 1), cleanup_failure())
        trace['cleanup_failure'] = 'ignored'
    except Exception:
        trace['cleanup_failure'] = 'propagated'
    Path(os.environ['STRUCTURED_TRACE']).write_text(json.dumps(trace, sort_keys=True))
