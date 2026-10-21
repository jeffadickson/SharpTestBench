// Local test wrapper. Upstream module is retained verbatim with its MIT notice.
`timescale 1ns/1ps
module priority_fixture(
input wire [31:0] requests,
input wire fault,
input wire [3:0] fault_config,
input wire [1:0] fault_kind,
output wire w2_msb_valid,
output wire [31:0] w2_msb_index,
output wire [31:0] w2_msb_onehot,
output wire w2_lsb_valid,
output wire [31:0] w2_lsb_index,
output wire [31:0] w2_lsb_onehot,
output wire w3_msb_valid,
output wire [31:0] w3_msb_index,
output wire [31:0] w3_msb_onehot,
output wire w3_lsb_valid,
output wire [31:0] w3_lsb_index,
output wire [31:0] w3_lsb_onehot,
output wire w5_msb_valid,
output wire [31:0] w5_msb_index,
output wire [31:0] w5_msb_onehot,
output wire w5_lsb_valid,
output wire [31:0] w5_lsb_index,
output wire [31:0] w5_lsb_onehot,
output wire w8_msb_valid,
output wire [31:0] w8_msb_index,
output wire [31:0] w8_msb_onehot,
output wire w8_lsb_valid,
output wire [31:0] w8_lsb_index,
output wire [31:0] w8_lsb_onehot,
output wire w17_msb_valid,
output wire [31:0] w17_msb_index,
output wire [31:0] w17_msb_onehot,
output wire w17_lsb_valid,
output wire [31:0] w17_lsb_index,
output wire [31:0] w17_lsb_onehot,
output wire w32_msb_valid,
output wire [31:0] w32_msb_index,
output wire [31:0] w32_msb_onehot,
output wire w32_lsb_valid,
output wire [31:0] w32_lsb_index,
output wire [31:0] w32_lsb_onehot
);
wire w2_msb_raw_valid;
wire [0:0] w2_msb_raw_index;
wire [1:0] w2_msb_raw_onehot;
priority_encoder #(.WIDTH(2), .LSB_HIGH_PRIORITY(0)) w2_msb_dut (
 .input_unencoded(requests[1:0]), .output_valid(w2_msb_raw_valid),
 .output_encoded(w2_msb_raw_index), .output_unencoded(w2_msb_raw_onehot));
assign w2_msb_valid = w2_msb_raw_valid ^ (fault && fault_config == 4'd0 && fault_kind == 2'd0);
assign w2_msb_index = w2_msb_raw_index ^ ((fault && fault_config == 4'd0 && fault_kind == 2'd1) ? 32'd1 : 32'd0);
assign w2_msb_onehot = w2_msb_raw_onehot ^ ((fault && fault_config == 4'd0 && fault_kind == 2'd2) ? 32'd1 : 32'd0);
wire w2_lsb_raw_valid;
wire [0:0] w2_lsb_raw_index;
wire [1:0] w2_lsb_raw_onehot;
priority_encoder #(.WIDTH(2), .LSB_HIGH_PRIORITY(1)) w2_lsb_dut (
 .input_unencoded(requests[1:0]), .output_valid(w2_lsb_raw_valid),
 .output_encoded(w2_lsb_raw_index), .output_unencoded(w2_lsb_raw_onehot));
assign w2_lsb_valid = w2_lsb_raw_valid ^ (fault && fault_config == 4'd1 && fault_kind == 2'd0);
assign w2_lsb_index = w2_lsb_raw_index ^ ((fault && fault_config == 4'd1 && fault_kind == 2'd1) ? 32'd1 : 32'd0);
assign w2_lsb_onehot = w2_lsb_raw_onehot ^ ((fault && fault_config == 4'd1 && fault_kind == 2'd2) ? 32'd1 : 32'd0);
wire w3_msb_raw_valid;
wire [1:0] w3_msb_raw_index;
wire [2:0] w3_msb_raw_onehot;
priority_encoder #(.WIDTH(3), .LSB_HIGH_PRIORITY(0)) w3_msb_dut (
 .input_unencoded(requests[2:0]), .output_valid(w3_msb_raw_valid),
 .output_encoded(w3_msb_raw_index), .output_unencoded(w3_msb_raw_onehot));
assign w3_msb_valid = w3_msb_raw_valid ^ (fault && fault_config == 4'd2 && fault_kind == 2'd0);
assign w3_msb_index = w3_msb_raw_index ^ ((fault && fault_config == 4'd2 && fault_kind == 2'd1) ? 32'd1 : 32'd0);
assign w3_msb_onehot = w3_msb_raw_onehot ^ ((fault && fault_config == 4'd2 && fault_kind == 2'd2) ? 32'd1 : 32'd0);
wire w3_lsb_raw_valid;
wire [1:0] w3_lsb_raw_index;
wire [2:0] w3_lsb_raw_onehot;
priority_encoder #(.WIDTH(3), .LSB_HIGH_PRIORITY(1)) w3_lsb_dut (
 .input_unencoded(requests[2:0]), .output_valid(w3_lsb_raw_valid),
 .output_encoded(w3_lsb_raw_index), .output_unencoded(w3_lsb_raw_onehot));
assign w3_lsb_valid = w3_lsb_raw_valid ^ (fault && fault_config == 4'd3 && fault_kind == 2'd0);
assign w3_lsb_index = w3_lsb_raw_index ^ ((fault && fault_config == 4'd3 && fault_kind == 2'd1) ? 32'd1 : 32'd0);
assign w3_lsb_onehot = w3_lsb_raw_onehot ^ ((fault && fault_config == 4'd3 && fault_kind == 2'd2) ? 32'd1 : 32'd0);
wire w5_msb_raw_valid;
wire [2:0] w5_msb_raw_index;
wire [4:0] w5_msb_raw_onehot;
priority_encoder #(.WIDTH(5), .LSB_HIGH_PRIORITY(0)) w5_msb_dut (
 .input_unencoded(requests[4:0]), .output_valid(w5_msb_raw_valid),
 .output_encoded(w5_msb_raw_index), .output_unencoded(w5_msb_raw_onehot));
assign w5_msb_valid = w5_msb_raw_valid ^ (fault && fault_config == 4'd4 && fault_kind == 2'd0);
assign w5_msb_index = w5_msb_raw_index ^ ((fault && fault_config == 4'd4 && fault_kind == 2'd1) ? 32'd1 : 32'd0);
assign w5_msb_onehot = w5_msb_raw_onehot ^ ((fault && fault_config == 4'd4 && fault_kind == 2'd2) ? 32'd1 : 32'd0);
wire w5_lsb_raw_valid;
wire [2:0] w5_lsb_raw_index;
wire [4:0] w5_lsb_raw_onehot;
priority_encoder #(.WIDTH(5), .LSB_HIGH_PRIORITY(1)) w5_lsb_dut (
 .input_unencoded(requests[4:0]), .output_valid(w5_lsb_raw_valid),
 .output_encoded(w5_lsb_raw_index), .output_unencoded(w5_lsb_raw_onehot));
assign w5_lsb_valid = w5_lsb_raw_valid ^ (fault && fault_config == 4'd5 && fault_kind == 2'd0);
assign w5_lsb_index = w5_lsb_raw_index ^ ((fault && fault_config == 4'd5 && fault_kind == 2'd1) ? 32'd1 : 32'd0);
assign w5_lsb_onehot = w5_lsb_raw_onehot ^ ((fault && fault_config == 4'd5 && fault_kind == 2'd2) ? 32'd1 : 32'd0);
wire w8_msb_raw_valid;
wire [2:0] w8_msb_raw_index;
wire [7:0] w8_msb_raw_onehot;
priority_encoder #(.WIDTH(8), .LSB_HIGH_PRIORITY(0)) w8_msb_dut (
 .input_unencoded(requests[7:0]), .output_valid(w8_msb_raw_valid),
 .output_encoded(w8_msb_raw_index), .output_unencoded(w8_msb_raw_onehot));
assign w8_msb_valid = w8_msb_raw_valid ^ (fault && fault_config == 4'd6 && fault_kind == 2'd0);
assign w8_msb_index = w8_msb_raw_index ^ ((fault && fault_config == 4'd6 && fault_kind == 2'd1) ? 32'd1 : 32'd0);
assign w8_msb_onehot = w8_msb_raw_onehot ^ ((fault && fault_config == 4'd6 && fault_kind == 2'd2) ? 32'd1 : 32'd0);
wire w8_lsb_raw_valid;
wire [2:0] w8_lsb_raw_index;
wire [7:0] w8_lsb_raw_onehot;
priority_encoder #(.WIDTH(8), .LSB_HIGH_PRIORITY(1)) w8_lsb_dut (
 .input_unencoded(requests[7:0]), .output_valid(w8_lsb_raw_valid),
 .output_encoded(w8_lsb_raw_index), .output_unencoded(w8_lsb_raw_onehot));
assign w8_lsb_valid = w8_lsb_raw_valid ^ (fault && fault_config == 4'd7 && fault_kind == 2'd0);
assign w8_lsb_index = w8_lsb_raw_index ^ ((fault && fault_config == 4'd7 && fault_kind == 2'd1) ? 32'd1 : 32'd0);
assign w8_lsb_onehot = w8_lsb_raw_onehot ^ ((fault && fault_config == 4'd7 && fault_kind == 2'd2) ? 32'd1 : 32'd0);
wire w17_msb_raw_valid;
wire [4:0] w17_msb_raw_index;
wire [16:0] w17_msb_raw_onehot;
priority_encoder #(.WIDTH(17), .LSB_HIGH_PRIORITY(0)) w17_msb_dut (
 .input_unencoded(requests[16:0]), .output_valid(w17_msb_raw_valid),
 .output_encoded(w17_msb_raw_index), .output_unencoded(w17_msb_raw_onehot));
assign w17_msb_valid = w17_msb_raw_valid ^ (fault && fault_config == 4'd8 && fault_kind == 2'd0);
assign w17_msb_index = w17_msb_raw_index ^ ((fault && fault_config == 4'd8 && fault_kind == 2'd1) ? 32'd1 : 32'd0);
assign w17_msb_onehot = w17_msb_raw_onehot ^ ((fault && fault_config == 4'd8 && fault_kind == 2'd2) ? 32'd1 : 32'd0);
wire w17_lsb_raw_valid;
wire [4:0] w17_lsb_raw_index;
wire [16:0] w17_lsb_raw_onehot;
priority_encoder #(.WIDTH(17), .LSB_HIGH_PRIORITY(1)) w17_lsb_dut (
 .input_unencoded(requests[16:0]), .output_valid(w17_lsb_raw_valid),
 .output_encoded(w17_lsb_raw_index), .output_unencoded(w17_lsb_raw_onehot));
assign w17_lsb_valid = w17_lsb_raw_valid ^ (fault && fault_config == 4'd9 && fault_kind == 2'd0);
assign w17_lsb_index = w17_lsb_raw_index ^ ((fault && fault_config == 4'd9 && fault_kind == 2'd1) ? 32'd1 : 32'd0);
assign w17_lsb_onehot = w17_lsb_raw_onehot ^ ((fault && fault_config == 4'd9 && fault_kind == 2'd2) ? 32'd1 : 32'd0);
wire w32_msb_raw_valid;
wire [4:0] w32_msb_raw_index;
wire [31:0] w32_msb_raw_onehot;
priority_encoder #(.WIDTH(32), .LSB_HIGH_PRIORITY(0)) w32_msb_dut (
 .input_unencoded(requests[31:0]), .output_valid(w32_msb_raw_valid),
 .output_encoded(w32_msb_raw_index), .output_unencoded(w32_msb_raw_onehot));
assign w32_msb_valid = w32_msb_raw_valid ^ (fault && fault_config == 4'd10 && fault_kind == 2'd0);
assign w32_msb_index = w32_msb_raw_index ^ ((fault && fault_config == 4'd10 && fault_kind == 2'd1) ? 32'd1 : 32'd0);
assign w32_msb_onehot = w32_msb_raw_onehot ^ ((fault && fault_config == 4'd10 && fault_kind == 2'd2) ? 32'd1 : 32'd0);
wire w32_lsb_raw_valid;
wire [4:0] w32_lsb_raw_index;
wire [31:0] w32_lsb_raw_onehot;
priority_encoder #(.WIDTH(32), .LSB_HIGH_PRIORITY(1)) w32_lsb_dut (
 .input_unencoded(requests[31:0]), .output_valid(w32_lsb_raw_valid),
 .output_encoded(w32_lsb_raw_index), .output_unencoded(w32_lsb_raw_onehot));
assign w32_lsb_valid = w32_lsb_raw_valid ^ (fault && fault_config == 4'd11 && fault_kind == 2'd0);
assign w32_lsb_index = w32_lsb_raw_index ^ ((fault && fault_config == 4'd11 && fault_kind == 2'd1) ? 32'd1 : 32'd0);
assign w32_lsb_onehot = w32_lsb_raw_onehot ^ ((fault && fault_config == 4'd11 && fault_kind == 2'd2) ? 32'd1 : 32'd0);
endmodule
