`timescale 1ns/1ps
module contract_top;
 reg clk=0;
 reg [7:0] data=0;
 reg [7:0] memory[0:3];
 initial begin memory[0]=0;memory[1]=0;memory[2]=0;memory[3]=0;end
 real real_value=0.0;
 parameter [7:0] CONSTANT=37;
 wire [7:0] mirrored=data;
endmodule
