"""Independent arbitrary-precision interpreter checking a custom CPU's retirement interface."""
import gc, json, os, time
from pathlib import Path
import cocotb
from cocotb.triggers import Timer, ReadOnly

@cocotb.test()
async def cpu_scoreboard(dut):
    """Verify program execution and stalled memory requests against a software ISA model."""
    assert cocotb.__version__ == '2.1.0'
    program = json.loads(Path(os.environ['CPU_PLAN']).read_text())
    regs, memory = [0] * 8, [0] * 256
    pc = commits = memory_stalls = pauses = 0
    random = 0x12345678
    trace = []
    for name, value in dict(clk=0, reset=1, enable=0, mem_ready=0, instruction=0,
        mem_rdata=0, fault=int(os.getenv('CPU_FAULT','0'))).items(): getattr(dut,name).value = value
    await Timer(1, unit='step'); dut.clk.value = 1
    await Timer(1, unit='step'); await ReadOnly(); await Timer(1, unit='step')
    counts = [g['collections'] for g in gc.get_stats()]
    start = time.perf_counter()
    done = False
    for cycle in range(2000000):
        assert 0 <= pc < len(program), 'CPU_MISMATCH: PC outside program'
        ins = program[pc]
        op, rd, rs, rt = ins >> 28, (ins >> 25) & 7, (ins >> 22) & 7, (ins >> 19) & 7
        imm = (ins & 0x7fff) - (ins & 0x8000)
        random = (random * 1664525 + 1013904223) & 0xffffffff
        run, available = bool(random & 7), bool(random & 24)
        mem = op in (4,5)
        fire = run and (not mem or available)
        writes = fire and op in (1,2,3,4) and rd != 0
        address = (regs[rs] + imm) % 256
        for name, value in dict(clk=0, reset=0, enable=run, mem_ready=available,
            instruction=ins, mem_rdata=memory[address]).items(): getattr(dut,name).value = int(value)
        await Timer(1, unit='step'); await ReadOnly()
        assert int(dut.pc.value) == pc and bool(dut.mem_valid.value) == (run and mem), f'CPU_MISMATCH: request cycle={cycle}'
        if run and mem:
            assert bool(dut.mem_write.value) == (op == 5) and int(dut.mem_address.value) == address, 'CPU_MISMATCH: address'
            if op == 5: assert int(dut.mem_wdata.value) == regs[rt], 'CPU_MISMATCH: store data'
        value = 0
        if op == 1: value = imm % (1 << 32)
        elif op == 2: value = (regs[rs] + regs[rt]) % (1 << 32)
        elif op == 3: value = regs[rs] ^ regs[rt]
        elif op == 4: value = memory[address]
        next_pc = pc
        if fire: next_pc = pc + imm if op == 6 and regs[rs] != regs[rt] else pc + 1
        done = fire and op == 15
        memory_stalls += run and mem and not available
        pauses += not run
        if fire:
            if op == 5: memory[address] = regs[rt]
            if writes: regs[rd] = value
            commits += 1
        await Timer(1, unit='step'); dut.clk.value = 1
        await Timer(1, unit='step'); await ReadOnly()
        assert bool(dut.retired.value) == fire and bool(dut.retire_write.value) == writes and int(dut.pc.value) == next_pc and bool(dut.halted.value) == done, f'CPU_MISMATCH: retirement cycle={cycle}'
        if writes: assert int(dut.retire_rd.value) == rd and int(dut.retire_data.value) == value, 'CPU_MISMATCH: register data'
        trace.append([cycle, pc, int(fire), rd if writes else 0, value if writes else 0, next_pc, int(run and mem), int(available)])
        pc = next_pc
        await Timer(1, unit='step')
        if done: break
    assert done and memory_stalls and pauses, 'CPU_TIMEOUT'
    Path(os.environ['CPU_REPORT']).write_text(json.dumps(dict(trace=trace, commits=commits,
        memoryStalls=memory_stalls, pauses=pauses, memory=memory, cycles=len(trace),
        seconds=time.perf_counter()-start,
        gc_collections=[g['collections']-n for g,n in zip(gc.get_stats(),counts)])))
