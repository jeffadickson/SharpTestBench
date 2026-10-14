import time
import cocotb
from cocotb.triggers import Timer
@cocotb.test()
async def benchmark(dut):
 n=100000;data=dut.bench32
 for _ in range(10000):await Timer(1)
 start=time.perf_counter()
 for _ in range(n):await Timer(1)
 print('MEASURE timer',time.perf_counter()-start,flush=True)
 x=1;start=time.perf_counter()
 for _ in range(n):
  x=(x*1664525+1013904223)&0xffffffff
  data.value=x
  await Timer(1)
  assert int(data.value)==x
 print('MEASURE rw32',time.perf_counter()-start,'checksum='+str(x),flush=True)
