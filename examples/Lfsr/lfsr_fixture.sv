`timescale 1ns/1ps
module lfsr_crc32_loop(input wire [63:0] state_in,data_in,
 input wire [1:0] fault,output wire [63:0] state_out,data_out);
 wire [31:0] raw_state;
 wire [7:0] raw_data;
 lfsr #(.LFSR_WIDTH(32),.LFSR_POLY(32'h4c11db7),.DATA_WIDTH(8),
 .LFSR_CONFIG("GALOIS"),.LFSR_FEED_FORWARD(0),.REVERSE(1),.STYLE("LOOP")) dut
 (.state_in(state_in[31:0]),.data_in(data_in[7:0]),.state_out(raw_state),.data_out(raw_data));
 assign state_out=raw_state ^ {63'b0,fault[0]};
 assign data_out=raw_data ^ {63'b0,fault[1]};
endmodule
module lfsr_crc32_reduction(input wire [63:0] state_in,data_in,
 input wire [1:0] fault,output wire [63:0] state_out,data_out);
 wire [31:0] raw_state;
 wire [7:0] raw_data;
 lfsr #(.LFSR_WIDTH(32),.LFSR_POLY(32'h4c11db7),.DATA_WIDTH(8),
 .LFSR_CONFIG("GALOIS"),.LFSR_FEED_FORWARD(0),.REVERSE(1),.STYLE("REDUCTION")) dut
 (.state_in(state_in[31:0]),.data_in(data_in[7:0]),.state_out(raw_state),.data_out(raw_data));
 assign state_out=raw_state ^ {63'b0,fault[0]};
 assign data_out=raw_data ^ {63'b0,fault[1]};
endmodule
module lfsr_crc16_loop(input wire [63:0] state_in,data_in,
 input wire [1:0] fault,output wire [63:0] state_out,data_out);
 wire [15:0] raw_state;
 wire [7:0] raw_data;
 lfsr #(.LFSR_WIDTH(16),.LFSR_POLY(16'h1021),.DATA_WIDTH(8),
 .LFSR_CONFIG("GALOIS"),.LFSR_FEED_FORWARD(0),.REVERSE(0),.STYLE("LOOP")) dut
 (.state_in(state_in[15:0]),.data_in(data_in[7:0]),.state_out(raw_state),.data_out(raw_data));
 assign state_out=raw_state ^ {63'b0,fault[0]};
 assign data_out=raw_data ^ {63'b0,fault[1]};
endmodule
module lfsr_crc16_reduction(input wire [63:0] state_in,data_in,
 input wire [1:0] fault,output wire [63:0] state_out,data_out);
 wire [15:0] raw_state;
 wire [7:0] raw_data;
 lfsr #(.LFSR_WIDTH(16),.LFSR_POLY(16'h1021),.DATA_WIDTH(8),
 .LFSR_CONFIG("GALOIS"),.LFSR_FEED_FORWARD(0),.REVERSE(0),.STYLE("REDUCTION")) dut
 (.state_in(state_in[15:0]),.data_in(data_in[7:0]),.state_out(raw_state),.data_out(raw_data));
 assign state_out=raw_state ^ {63'b0,fault[0]};
 assign data_out=raw_data ^ {63'b0,fault[1]};
endmodule
module lfsr_crc32c_loop(input wire [63:0] state_in,data_in,
 input wire [1:0] fault,output wire [63:0] state_out,data_out);
 wire [31:0] raw_state;
 wire [31:0] raw_data;
 lfsr #(.LFSR_WIDTH(32),.LFSR_POLY(32'h1edc6f41),.DATA_WIDTH(32),
 .LFSR_CONFIG("GALOIS"),.LFSR_FEED_FORWARD(0),.REVERSE(1),.STYLE("LOOP")) dut
 (.state_in(state_in[31:0]),.data_in(data_in[31:0]),.state_out(raw_state),.data_out(raw_data));
 assign state_out=raw_state ^ {63'b0,fault[0]};
 assign data_out=raw_data ^ {63'b0,fault[1]};
endmodule
module lfsr_crc32c_reduction(input wire [63:0] state_in,data_in,
 input wire [1:0] fault,output wire [63:0] state_out,data_out);
 wire [31:0] raw_state;
 wire [31:0] raw_data;
 lfsr #(.LFSR_WIDTH(32),.LFSR_POLY(32'h1edc6f41),.DATA_WIDTH(32),
 .LFSR_CONFIG("GALOIS"),.LFSR_FEED_FORWARD(0),.REVERSE(1),.STYLE("REDUCTION")) dut
 (.state_in(state_in[31:0]),.data_in(data_in[31:0]),.state_out(raw_state),.data_out(raw_data));
 assign state_out=raw_state ^ {63'b0,fault[0]};
 assign data_out=raw_data ^ {63'b0,fault[1]};
endmodule
module lfsr_prbs7_loop(input wire [63:0] state_in,data_in,
 input wire [1:0] fault,output wire [63:0] state_out,data_out);
 wire [6:0] raw_state;
 wire [7:0] raw_data;
 lfsr #(.LFSR_WIDTH(7),.LFSR_POLY(7'h41),.DATA_WIDTH(8),
 .LFSR_CONFIG("FIBONACCI"),.LFSR_FEED_FORWARD(0),.REVERSE(0),.STYLE("LOOP")) dut
 (.state_in(state_in[6:0]),.data_in(data_in[7:0]),.state_out(raw_state),.data_out(raw_data));
 assign state_out=raw_state ^ {63'b0,fault[0]};
 assign data_out=raw_data ^ {63'b0,fault[1]};
endmodule
module lfsr_prbs7_reduction(input wire [63:0] state_in,data_in,
 input wire [1:0] fault,output wire [63:0] state_out,data_out);
 wire [6:0] raw_state;
 wire [7:0] raw_data;
 lfsr #(.LFSR_WIDTH(7),.LFSR_POLY(7'h41),.DATA_WIDTH(8),
 .LFSR_CONFIG("FIBONACCI"),.LFSR_FEED_FORWARD(0),.REVERSE(0),.STYLE("REDUCTION")) dut
 (.state_in(state_in[6:0]),.data_in(data_in[7:0]),.state_out(raw_state),.data_out(raw_data));
 assign state_out=raw_state ^ {63'b0,fault[0]};
 assign data_out=raw_data ^ {63'b0,fault[1]};
endmodule
module lfsr_scrambler_loop(input wire [63:0] state_in,data_in,
 input wire [1:0] fault,output wire [63:0] state_out,data_out);
 wire [57:0] raw_state;
 wire [63:0] raw_data;
 lfsr #(.LFSR_WIDTH(58),.LFSR_POLY(58'h8000000001),.DATA_WIDTH(64),
 .LFSR_CONFIG("FIBONACCI"),.LFSR_FEED_FORWARD(0),.REVERSE(1),.STYLE("LOOP")) dut
 (.state_in(state_in[57:0]),.data_in(data_in[63:0]),.state_out(raw_state),.data_out(raw_data));
 assign state_out=raw_state ^ {63'b0,fault[0]};
 assign data_out=raw_data ^ {63'b0,fault[1]};
endmodule
module lfsr_scrambler_reduction(input wire [63:0] state_in,data_in,
 input wire [1:0] fault,output wire [63:0] state_out,data_out);
 wire [57:0] raw_state;
 wire [63:0] raw_data;
 lfsr #(.LFSR_WIDTH(58),.LFSR_POLY(58'h8000000001),.DATA_WIDTH(64),
 .LFSR_CONFIG("FIBONACCI"),.LFSR_FEED_FORWARD(0),.REVERSE(1),.STYLE("REDUCTION")) dut
 (.state_in(state_in[57:0]),.data_in(data_in[63:0]),.state_out(raw_state),.data_out(raw_data));
 assign state_out=raw_state ^ {63'b0,fault[0]};
 assign data_out=raw_data ^ {63'b0,fault[1]};
endmodule
module lfsr_descrambler_loop(input wire [63:0] state_in,data_in,
 input wire [1:0] fault,output wire [63:0] state_out,data_out);
 wire [57:0] raw_state;
 wire [63:0] raw_data;
 lfsr #(.LFSR_WIDTH(58),.LFSR_POLY(58'h8000000001),.DATA_WIDTH(64),
 .LFSR_CONFIG("FIBONACCI"),.LFSR_FEED_FORWARD(1),.REVERSE(1),.STYLE("LOOP")) dut
 (.state_in(state_in[57:0]),.data_in(data_in[63:0]),.state_out(raw_state),.data_out(raw_data));
 assign state_out=raw_state ^ {63'b0,fault[0]};
 assign data_out=raw_data ^ {63'b0,fault[1]};
endmodule
module lfsr_descrambler_reduction(input wire [63:0] state_in,data_in,
 input wire [1:0] fault,output wire [63:0] state_out,data_out);
 wire [57:0] raw_state;
 wire [63:0] raw_data;
 lfsr #(.LFSR_WIDTH(58),.LFSR_POLY(58'h8000000001),.DATA_WIDTH(64),
 .LFSR_CONFIG("FIBONACCI"),.LFSR_FEED_FORWARD(1),.REVERSE(1),.STYLE("REDUCTION")) dut
 (.state_in(state_in[57:0]),.data_in(data_in[63:0]),.state_out(raw_state),.data_out(raw_data));
 assign state_out=raw_state ^ {63'b0,fault[0]};
 assign data_out=raw_data ^ {63'b0,fault[1]};
endmodule
module lfsr_galois_ff_loop(input wire [63:0] state_in,data_in,
 input wire [1:0] fault,output wire [63:0] state_out,data_out);
 wire [15:0] raw_state;
 wire [15:0] raw_data;
 lfsr #(.LFSR_WIDTH(16),.LFSR_POLY(16'h8005),.DATA_WIDTH(16),
 .LFSR_CONFIG("GALOIS"),.LFSR_FEED_FORWARD(1),.REVERSE(1),.STYLE("LOOP")) dut
 (.state_in(state_in[15:0]),.data_in(data_in[15:0]),.state_out(raw_state),.data_out(raw_data));
 assign state_out=raw_state ^ {63'b0,fault[0]};
 assign data_out=raw_data ^ {63'b0,fault[1]};
endmodule
module lfsr_galois_ff_reduction(input wire [63:0] state_in,data_in,
 input wire [1:0] fault,output wire [63:0] state_out,data_out);
 wire [15:0] raw_state;
 wire [15:0] raw_data;
 lfsr #(.LFSR_WIDTH(16),.LFSR_POLY(16'h8005),.DATA_WIDTH(16),
 .LFSR_CONFIG("GALOIS"),.LFSR_FEED_FORWARD(1),.REVERSE(1),.STYLE("REDUCTION")) dut
 (.state_in(state_in[15:0]),.data_in(data_in[15:0]),.state_out(raw_state),.data_out(raw_data));
 assign state_out=raw_state ^ {63'b0,fault[0]};
 assign data_out=raw_data ^ {63'b0,fault[1]};
endmodule
module lfsr_minimal_loop(input wire [63:0] state_in,data_in,
 input wire [1:0] fault,output wire [63:0] state_out,data_out);
 wire [0:0] raw_state;
 wire [0:0] raw_data;
 lfsr #(.LFSR_WIDTH(1),.LFSR_POLY(1'h1),.DATA_WIDTH(1),
 .LFSR_CONFIG("FIBONACCI"),.LFSR_FEED_FORWARD(0),.REVERSE(0),.STYLE("LOOP")) dut
 (.state_in(state_in[0:0]),.data_in(data_in[0:0]),.state_out(raw_state),.data_out(raw_data));
 assign state_out=raw_state ^ {63'b0,fault[0]};
 assign data_out=raw_data ^ {63'b0,fault[1]};
endmodule
module lfsr_minimal_reduction(input wire [63:0] state_in,data_in,
 input wire [1:0] fault,output wire [63:0] state_out,data_out);
 wire [0:0] raw_state;
 wire [0:0] raw_data;
 lfsr #(.LFSR_WIDTH(1),.LFSR_POLY(1'h1),.DATA_WIDTH(1),
 .LFSR_CONFIG("FIBONACCI"),.LFSR_FEED_FORWARD(0),.REVERSE(0),.STYLE("REDUCTION")) dut
 (.state_in(state_in[0:0]),.data_in(data_in[0:0]),.state_out(raw_state),.data_out(raw_data));
 assign state_out=raw_state ^ {63'b0,fault[0]};
 assign data_out=raw_data ^ {63'b0,fault[1]};
endmodule
module lfsr_crc64_loop(input wire [63:0] state_in,data_in,
 input wire [1:0] fault,output wire [63:0] state_out,data_out);
 wire [63:0] raw_state;
 wire [7:0] raw_data;
 lfsr #(.LFSR_WIDTH(64),.LFSR_POLY(64'h42f0e1eba9ea3693),.DATA_WIDTH(8),
 .LFSR_CONFIG("GALOIS"),.LFSR_FEED_FORWARD(0),.REVERSE(0),.STYLE("LOOP")) dut
 (.state_in(state_in),.data_in(data_in[7:0]),.state_out(raw_state),.data_out(raw_data));
 assign state_out=raw_state ^ {63'b0,fault[0]};
 assign data_out=raw_data ^ {63'b0,fault[1]};
endmodule
module lfsr_crc64_reduction(input wire [63:0] state_in,data_in,
 input wire [1:0] fault,output wire [63:0] state_out,data_out);
 wire [63:0] raw_state;
 wire [7:0] raw_data;
 lfsr #(.LFSR_WIDTH(64),.LFSR_POLY(64'h42f0e1eba9ea3693),.DATA_WIDTH(8),
 .LFSR_CONFIG("GALOIS"),.LFSR_FEED_FORWARD(0),.REVERSE(0),.STYLE("REDUCTION")) dut
 (.state_in(state_in),.data_in(data_in[7:0]),.state_out(raw_state),.data_out(raw_data));
 assign state_out=raw_state ^ {63'b0,fault[0]};
 assign data_out=raw_data ^ {63'b0,fault[1]};
endmodule
module lfsr_crc64xz_loop(input wire [63:0] state_in,data_in,
 input wire [1:0] fault,output wire [63:0] state_out,data_out);
 wire [63:0] raw_state;
 wire [7:0] raw_data;
 lfsr #(.LFSR_WIDTH(64),.LFSR_POLY(64'h42f0e1eba9ea3693),.DATA_WIDTH(8),
 .LFSR_CONFIG("GALOIS"),.LFSR_FEED_FORWARD(0),.REVERSE(1),.STYLE("LOOP")) dut
 (.state_in(state_in),.data_in(data_in[7:0]),.state_out(raw_state),.data_out(raw_data));
 assign state_out=raw_state ^ {63'b0,fault[0]};
 assign data_out=raw_data ^ {63'b0,fault[1]};
endmodule
module lfsr_crc64xz_reduction(input wire [63:0] state_in,data_in,
 input wire [1:0] fault,output wire [63:0] state_out,data_out);
 wire [63:0] raw_state;
 wire [7:0] raw_data;
 lfsr #(.LFSR_WIDTH(64),.LFSR_POLY(64'h42f0e1eba9ea3693),.DATA_WIDTH(8),
 .LFSR_CONFIG("GALOIS"),.LFSR_FEED_FORWARD(0),.REVERSE(1),.STYLE("REDUCTION")) dut
 (.state_in(state_in),.data_in(data_in[7:0]),.state_out(raw_state),.data_out(raw_data));
 assign state_out=raw_state ^ {63'b0,fault[0]};
 assign data_out=raw_data ^ {63'b0,fault[1]};
endmodule
