"""Independent Python scoreboard for the same AXI-Lite-shaped fixture and plan."""
import gc, json, os, time, tracemalloc
from pathlib import Path
import cocotb
from cocotb.triggers import Timer, ReadOnly

class Bus:
    def __init__(self, dut):
        self.dut = dut
        self.random = 0x12345678
        self.cycles = self.write_stalls = self.read_stalls = 0
        self.fault = int(os.getenv('AXI_FAULT', '0'))
    async def step(self, *, reset=True, aw=False, w=False, ar=False, bready=False, rready=False, address=0, data=0, strobe=0):
        self.random = (self.random * 1664525 + 1013904223) & 0xffffffff
        values = dict(clk=0, reset_n=reset, awvalid=aw, wvalid=w, arvalid=ar, bready=bready, rready=rready,
                      awaddr=address, araddr=address, wdata=data, wstrb=strobe, fault=self.fault,
                      stall_aw=(self.random & 3)==0, stall_w=(self.random & 12)==0, stall_ar=(self.random & 48)==0)
        for name, value in values.items(): getattr(self.dut, name).value = int(value)
        await Timer(1, unit='step'); await ReadOnly()
        names = ('awready','wready','arready','bvalid','bresp','rvalid','rdata','rresp')
        output = {name: int(getattr(self.dut, name).value) if reset else 0 for name in names}
        self.write_stalls += bool(output['bvalid'] and not bready)
        self.read_stalls += bool(output['rvalid'] and not rready)
        await Timer(1, unit='step'); self.dut.clk.value = 1
        await Timer(1, unit='step'); await ReadOnly()
        await Timer(1, unit='step')
        self.cycles += 1
        return output
    async def reset(self):
        await self.step(reset=False); await self.step(reset=False)
        output = await self.step()
        assert not output['bvalid'] and not output['rvalid'], 'AXI_RESET: stale response'
    async def cut(self, phase):
        for _ in range(64):
            output = await self.step(address=0, data=0xdeadbeef, strobe=15,
                aw=phase in ('aw','b'), w=phase in ('w','b'), ar=phase=='r')
            if output[dict(aw='awready', w='wready', b='bvalid', r='rvalid')[phase]]:
                await self.reset(); return
        raise AssertionError('AXI_TIMEOUT: reset phase')

@cocotb.test()
async def axi_memory_scoreboard(dut):
    assert cocotb.__version__ == '2.1.0'
    commands = json.loads(Path(os.environ['AXI_PLAN']).read_text())
    bus = Bus(dut)
    # Byte-addressed reference deliberately differs from the HDL/C# word-array layout.
    memory = bytearray(256)
    trace = []
    profile = os.getenv('AXI_PROFILE') == '1'
    if profile: tracemalloc.start()
    counts = [g['collections'] for g in gc.get_stats()]
    start_time = time.perf_counter()
    await bus.reset()
    for command in commands:
        start = bus.cycles
        op, address, data, strobe = (command[k] for k in ('Op','Address','Data','Strobe'))
        if op.startswith('reset_'):
            await bus.cut(op[6:]); memory[:] = bytes(256)
            trace.append([op, bus.cycles-start]); continue
        writing = op == 'write'
        legal = address < 256 and address % 4 == 0
        accepted = set()
        stalled = None
        for cycle in range(128):
            aw = writing and 'aw' not in accepted and cycle >= command['AwDelay']
            w = writing and 'w' not in accepted and cycle >= command['WDelay']
            ar = not writing and 'ar' not in accepted and cycle >= command['AwDelay']
            ready = cycle >= command['ReadyDelay']
            o = await bus.step(address=address, data=data, strobe=strobe, aw=aw, w=w, ar=ar,
                               bready=writing and ready, rready=not writing and ready)
            response = (o['bvalid'], o['bresp']) if writing else (o['rvalid'], o['rresp'], o['rdata'])
            if stalled is not None: assert response == stalled, 'AXI_STABILITY: response changed under backpressure'
            stalled = response if response[0] and not ready else None
            for channel, valid in [('aw',aw),('w',w),('ar',ar)]:
                if valid and o[channel+'ready']: accepted.add(channel)
            if response[0] and ready:
                assert {'aw','w'}.issubset(accepted) if writing else 'ar' in accepted, 'AXI_ORDER: response without request'
                assert response[1] == (0 if legal else 2), 'AXI_RESPONSE: wrong response code'
                if writing and legal:
                    for lane in range(4):
                        if strobe & (1<<lane): memory[address+lane] = (data >> (8*lane)) & 255
                if not writing:
                    expected = int.from_bytes(memory[address:address+4], 'little') if legal else 0
                    assert o['rdata'] == expected, 'AXI_DATA: scoreboard mismatch'
                trace.append([op,address,data,strobe,response[1],0 if writing else o['rdata'],bus.cycles-start])
                break
        else: raise AssertionError('AXI_TIMEOUT: transaction did not complete')
    elapsed = time.perf_counter() - start_time
    assert bus.write_stalls and bus.read_stalls, 'AXI_COVERAGE: no response backpressure'
    peak = tracemalloc.get_traced_memory()[1] if profile else None
    if profile: tracemalloc.stop()
    report = dict(trace=trace, cycles=bus.cycles, write_stalls=bus.write_stalls, read_stalls=bus.read_stalls,
                  seconds=elapsed, traced_peak_bytes=peak, allocation_profile=profile,
                  gc_collections=[g['collections']-n for g,n in zip(gc.get_stats(),counts)])
    Path(os.environ['AXI_REPORT']).write_text(json.dumps(report))
