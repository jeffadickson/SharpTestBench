`timescale 1ns/1ps
// Small verification fixture: eight 32-bit registers, word-addressed instruction
// input and 256-word external memory. This is a custom ISA, not a RISC-V core.
// A memory instruction retires only when ready; enable can pause any instruction.
module cpu_core(
 input logic clk, reset, enable, mem_ready,
 input logic [31:0] instruction, mem_rdata,
 input logic [7:0] fault,
 output logic [31:0] pc,
 output logic halted, retired, retire_write,
 output logic [7:0] retire_rd,
 output logic [31:0] retire_data,
 output wire mem_valid, mem_write,
 output wire [7:0] mem_address,
 output wire [31:0] mem_wdata
);
 logic [31:0] regs[0:7];
 wire [3:0] op = instruction[31:28];
 wire [2:0] rd = instruction[27:25], rs = instruction[24:22], rt = instruction[21:19];
 wire signed [31:0] immediate = {{16{instruction[15]}},instruction[15:0]};
 wire memory_op = op == 4 || op == 5;
 wire [31:0] address = regs[rs] + immediate;
 assign mem_valid = enable && !halted && !reset && memory_op;
 assign mem_write = op == 5;
 assign mem_address = address[7:0];
 assign mem_wdata = regs[rt] ^ (fault == 3 ? 32'h1 : 32'h0);
 logic [31:0] value;
 always @* begin
  case (op)
   1: value = immediate;
   2: value = (regs[rs] + regs[rt]) ^ (fault == 1 ? 32'h1 : 32'h0);
   3: value = regs[rs] ^ regs[rt];
   4: value = mem_rdata;
   default: value = 0;
  endcase
 end
 // Retirement signals describe the edge just executed, including writes to the
 // architectural register file. Register zero is immutable.
 always @(posedge clk) begin
  if (reset) begin
   pc<=0; halted<=0; retired<=0; retire_write<=0; retire_rd<=0; retire_data<=0;
   for (integer i=0;i<8;i=i+1) regs[i]<=0;
  end else begin
   retired<=0; retire_write<=0;
   if (enable && !halted && (!memory_op || mem_ready)) begin
    retired<=1; pc<=pc+1;
    retire_rd<={5'b0,rd}; retire_data<=value;
    if (op>=1 && op<=4 && rd!=0) begin regs[rd]<=value; retire_write<=1; end
    if (op==6 && regs[rs]!=regs[rt]) pc<=pc+immediate+(fault==2 ? 1 : 0);
    if (op==15) halted<=1;
   end
  end
 end
endmodule
