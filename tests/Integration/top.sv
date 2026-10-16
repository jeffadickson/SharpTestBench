`timescale 1ns/1ps
package discovery_pkg;
 parameter integer constant_marker=37;
 integer marker=37;
endpackage
module top;
 reg adjustable_clk=0;
 reg [31:0] perf_a=0, perf_clock_count=0;
 reg [7:0] perf_b=0;
 reg perf_clock=0;
 always @(posedge perf_clock) perf_clock_count <= perf_clock_count + 1;
 wire [31:0] package_marker=discovery_pkg::marker;
 initial if ($test$plusargs("CS_EARLY_FINISH")) begin #0.002; $finish; end
 parameter [7:0] PARAMETER=37;
 reg [7:0] delta_source=0, delta1=0, delta2=0, delta3=0;
 always @(delta_source) delta1 <= delta_source ^ 8'h01;
 always @(delta1) delta2 <= delta1 ^ 8'h02;
 always @(delta2) delta3 <= delta2 ^ 8'h04;
 reg clk=0, reset=0;
 reg [7:0] data=0;
 reg [31:0] bench32=0;
 reg [1:0][2:0][7:0] packed_matrix='0;
 reg [-1:0][3:1][7:0] packed_mixed='0;
 wire [7:0] matrix_middle=packed_matrix[1][1];
 wire [7:0] matrix_right=packed_matrix[0][0];
 wire [7:0] mixed_first=packed_mixed[-1][3];
 wire [7:0] mixed_last=packed_mixed[0][1];
 typedef struct packed {logic [3:0] tag; logic [7:0] payload; logic [3:0] flags;} packet_t;
 packet_t packed_packet='0;
 wire [7:0] packet_payload=packed_packet.payload;
 reg [255:0] wide=0;
 reg [72:0] odd_wide=0;
 reg [4095:0] huge=0;
 // Crosses managed pooling/native stack thresholds and has partial-byte padding.
 reg [8192:0] pooled_wide=0;
 reg signed [15:0] signed_data=0;
 reg [3:0] logic_data=0;
 wire [7:0] doubled=data<<1;
 reg [7:0] sampled=0;
 reg [7:0] group_slots[0:69];
 initial for (integer i=0;i<70;i=i+1) group_slots[i]=0;
 reg [7:0] memory[0:3];
 initial begin memory[0]=0;memory[1]=0;memory[2]=0;memory[3]=0;end
 reg [0:7] ascending=0;
 real real_data=0.0;
 leaf unit0();
 always @(posedge clk) if(reset) sampled<=0;else sampled<=data;
 reg in_valid=0,out_ready=0;
 reg [7:0] in_data=0;
 reg out_valid=0;
 wire in_ready=!out_valid||out_ready;
 reg [7:0] out_data=0;
 always @(posedge clk) begin
  if(reset) out_valid<=0;
  else if(in_ready) begin out_valid<=in_valid;if(in_valid)out_data<=in_data;end
 end
endmodule
module leaf;
 reg [7:0] local_data=7;
endmodule
