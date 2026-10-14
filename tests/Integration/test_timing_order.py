import os
from pathlib import Path
import cocotb
from cocotb.handle import Force, Release, Immediate
from cocotb.triggers import Timer, ReadOnly, ReadWrite, First, Combine
from cocotb.simtime import get_sim_time

@cocotb.test()
async def scheduled_write_ordering(dut):
    trace = []
    dut.data.value = 10
    await ReadOnly()
    await Timer(1)
    dut.data.value = 20
    dut.data.value = Immediate(30)
    trace.append(f'immediate:{int(dut.data.value)}')
    await ReadOnly()
    trace.append(f'deferred:{int(dut.data.value)}')
    assert int(dut.data.value) == 20
    await Timer(1)
    await ReadWrite()
    dut.data.value = 40
    dut.data.value = 50
    await ReadOnly()
    trace.append(f'readwrite:{int(dut.data.value)}')
    assert int(dut.data.value) == 50
    await Timer(1)
    start = get_sim_time(unit='step')
    await First(Timer(2), Timer(2))
    trace.append(f'first:{get_sim_time(unit="step") - start}')
    start = get_sim_time(unit='step')
    await Combine(Timer(2), Timer(2))
    trace.append(f'combine:{get_sim_time(unit="step") - start}')
    Path(os.environ['PARITY_TRACE'] + '.scheduled').write_text('\n'.join(trace) + '\n')

@cocotb.test(skip=os.environ['TIMING_SIM'] != 'icarus')
async def force_deposit_ordering(dut):
    trace = []
    dut.data.value = 10
    await Timer(1)
    dut.data.value = 20
    dut.data.value = Force(77)
    await ReadOnly()
    trace.append(f'force-before-flush:{int(dut.data.value)}')
    assert int(dut.data.value) == 77
    await Timer(1)
    dut.data.value = 30
    dut.data.value = Release()
    await ReadOnly()
    trace.append(f'release-before-flush:{int(dut.data.value)}')
    assert int(dut.data.value) == 30
    await Timer(1)
    await ReadWrite()
    dut.data.value = 40
    dut.data.value = Force(88)
    dut.data.value = 50
    await ReadOnly()
    trace.append(f'forced-readwrite:{int(dut.data.value)}')
    assert int(dut.data.value) == 88
    await Timer(1)
    dut.data.value = Release()
    dut.data.value = 60
    await ReadOnly()
    trace.append(f'released:{int(dut.data.value)}')
    assert int(dut.data.value) == 60
    Path(os.environ['PARITY_TRACE'] + '.force').write_text('\n'.join(trace) + '\n')
