import os
from pathlib import Path
import cocotb
from cocotb.triggers import Timer,RisingEdge,ReadOnly,ReadWrite,ValueChange,Combine,with_timeout
@cocotb.test()
async def timing(dut):
 dut.clk.value=0;dut.data.value=42
 await Timer(2)
 seen=[]
 async def monitor():
  await RisingEdge(dut.clk)
  seen.append('edge:'+str(int(dut.sampled.value)))
  await ReadOnly()
  seen.append('settled:'+str(int(dut.sampled.value)))
 task=cocotb.start_soon(monitor())
 await Timer(1);dut.clk.value=1
 await task
 Path(os.environ['PARITY_TRACE']).write_text('\n'.join(seen)+'\n')
 assert seen==['edge:0','settled:42']
 await Timer(1)
 dut.data.value=1;dut.data.value=2;dut.data.value=3
 await ReadWrite();await ReadOnly()
 assert int(dut.data.value)==3

@cocotb.test()
async def delta_chain(dut):
 dut.delta_source.value=0
 await Timer(1)
 seen=[]
 for i in range(1,21):
  now=cocotb.utils.get_sim_time(unit="step")
  dut.delta_source.value=i
  dut.delta_source.value=i+32
  await with_timeout(Combine(ValueChange(dut.delta1),ValueChange(dut.delta3)),10,"step")
  await ReadOnly()
  assert cocotb.utils.get_sim_time(unit="step")==now
  assert int(dut.delta3.value)==((i+32)^7)
  seen.append(f"{i}:{int(dut.delta1.value)}:{int(dut.delta3.value)}")
  await Timer(1)
 Path(os.environ['PARITY_TRACE']+'.delta').write_text('\n'.join(seen)+'\n')
