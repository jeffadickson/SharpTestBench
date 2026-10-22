`timescale 1ns/1ps
// Local deterministic FIFO fixture, independently checked against System.Collections.Generic.Queue.
module closure_fifo #(parameter integer DEPTH=3)(input wire clk,rst,push,pop,input wire [7:0] din,output wire [7:0] dout,output wire full,empty,output reg [31:0] count=0);
 reg [7:0] mem[0:DEPTH-1];
 integer rd=0,wr=0;
 wire take=pop&&!empty;
 wire put=push&&(!full||take);
 assign full=count==DEPTH;
 assign empty=count==0;
 assign dout=empty?8'b0:mem[rd];
 always @(posedge clk) begin
  if(rst) begin count<=0;rd<=0;wr<=0;end
  else begin
   if(take) rd<=rd==DEPTH-1?0:rd+1;
   if(put) begin mem[wr]<=din;wr<=wr==DEPTH-1?0:wr+1;end
   case({put,take}) 2'b10:count<=count+1;2'b01:count<=count-1;default:count<=count;endcase
  end
 end
endmodule
module closure_top;
 reg clk=0,rst=0,push=0,pop=0;
 reg [7:0] din=0;
 wire [7:0] dout;
 wire full,empty;
 wire [31:0] count;
 closure_fifo fifo(clk,rst,push,pop,din,dout,full,empty,count);
 reg [31:0] packed_word=0;
 wire [15:0] packed_low=packed_word[15:0];
 reg signed [7:0] signed_data=0;
 reg [0:7] ascending=0;
 integer integer_value=0;
 real real_value=0.0;
 reg [7:0] memory[0:3];
 initial begin memory[0]=0;memory[1]=1;memory[2]=2;memory[3]=3;end
 parameter [7:0] CONSTANT=37;
endmodule
