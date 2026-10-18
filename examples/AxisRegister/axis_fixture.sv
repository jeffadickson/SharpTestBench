`timescale 1ns/1ps
module axis_mode0(input wire clk, rst, fault,
input wire [15:0] s_axis_tdata, input wire [1:0] s_axis_tkeep,
input wire s_axis_tvalid, output wire s_axis_tready, input wire s_axis_tlast,
input wire [3:0] s_axis_tid, s_axis_tdest, input wire [1:0] s_axis_tuser,
output wire [15:0] m_axis_tdata, output wire [1:0] m_axis_tkeep,
output wire m_axis_tvalid, input wire m_axis_tready, output wire m_axis_tlast,
output wire [3:0] m_axis_tid, m_axis_tdest, output wire [1:0] m_axis_tuser);
wire [15:0] raw_data;
assign m_axis_tdata = raw_data ^ {15'b0,fault};
axis_register #(.DATA_WIDTH(16), .KEEP_ENABLE(1), .KEEP_WIDTH(2),
 .LAST_ENABLE(1), .ID_ENABLE(1), .ID_WIDTH(4),
 .DEST_ENABLE(1), .DEST_WIDTH(4), .USER_ENABLE(1), .USER_WIDTH(2),
 .REG_TYPE(0)) dut (.clk(clk), .rst(rst), .s_axis_tdata(s_axis_tdata), .s_axis_tkeep(s_axis_tkeep), .s_axis_tvalid(s_axis_tvalid), .s_axis_tready(s_axis_tready), .s_axis_tlast(s_axis_tlast), .s_axis_tid(s_axis_tid), .s_axis_tdest(s_axis_tdest), .s_axis_tuser(s_axis_tuser), .m_axis_tdata(raw_data), .m_axis_tkeep(m_axis_tkeep), .m_axis_tvalid(m_axis_tvalid), .m_axis_tready(m_axis_tready), .m_axis_tlast(m_axis_tlast), .m_axis_tid(m_axis_tid), .m_axis_tdest(m_axis_tdest), .m_axis_tuser(m_axis_tuser));
endmodule
module axis_mode1(input wire clk, rst, fault,
input wire [15:0] s_axis_tdata, input wire [1:0] s_axis_tkeep,
input wire s_axis_tvalid, output wire s_axis_tready, input wire s_axis_tlast,
input wire [3:0] s_axis_tid, s_axis_tdest, input wire [1:0] s_axis_tuser,
output wire [15:0] m_axis_tdata, output wire [1:0] m_axis_tkeep,
output wire m_axis_tvalid, input wire m_axis_tready, output wire m_axis_tlast,
output wire [3:0] m_axis_tid, m_axis_tdest, output wire [1:0] m_axis_tuser);
wire [15:0] raw_data;
assign m_axis_tdata = raw_data ^ {15'b0,fault};
axis_register #(.DATA_WIDTH(16), .KEEP_ENABLE(1), .KEEP_WIDTH(2),
 .LAST_ENABLE(1), .ID_ENABLE(1), .ID_WIDTH(4),
 .DEST_ENABLE(1), .DEST_WIDTH(4), .USER_ENABLE(1), .USER_WIDTH(2),
 .REG_TYPE(1)) dut (.clk(clk), .rst(rst), .s_axis_tdata(s_axis_tdata), .s_axis_tkeep(s_axis_tkeep), .s_axis_tvalid(s_axis_tvalid), .s_axis_tready(s_axis_tready), .s_axis_tlast(s_axis_tlast), .s_axis_tid(s_axis_tid), .s_axis_tdest(s_axis_tdest), .s_axis_tuser(s_axis_tuser), .m_axis_tdata(raw_data), .m_axis_tkeep(m_axis_tkeep), .m_axis_tvalid(m_axis_tvalid), .m_axis_tready(m_axis_tready), .m_axis_tlast(m_axis_tlast), .m_axis_tid(m_axis_tid), .m_axis_tdest(m_axis_tdest), .m_axis_tuser(m_axis_tuser));
endmodule
module axis_mode2(input wire clk, rst, fault,
input wire [15:0] s_axis_tdata, input wire [1:0] s_axis_tkeep,
input wire s_axis_tvalid, output wire s_axis_tready, input wire s_axis_tlast,
input wire [3:0] s_axis_tid, s_axis_tdest, input wire [1:0] s_axis_tuser,
output wire [15:0] m_axis_tdata, output wire [1:0] m_axis_tkeep,
output wire m_axis_tvalid, input wire m_axis_tready, output wire m_axis_tlast,
output wire [3:0] m_axis_tid, m_axis_tdest, output wire [1:0] m_axis_tuser);
wire [15:0] raw_data;
assign m_axis_tdata = raw_data ^ {15'b0,fault};
axis_register #(.DATA_WIDTH(16), .KEEP_ENABLE(1), .KEEP_WIDTH(2),
 .LAST_ENABLE(1), .ID_ENABLE(1), .ID_WIDTH(4),
 .DEST_ENABLE(1), .DEST_WIDTH(4), .USER_ENABLE(1), .USER_WIDTH(2),
 .REG_TYPE(2)) dut (.clk(clk), .rst(rst), .s_axis_tdata(s_axis_tdata), .s_axis_tkeep(s_axis_tkeep), .s_axis_tvalid(s_axis_tvalid), .s_axis_tready(s_axis_tready), .s_axis_tlast(s_axis_tlast), .s_axis_tid(s_axis_tid), .s_axis_tdest(s_axis_tdest), .s_axis_tuser(s_axis_tuser), .m_axis_tdata(raw_data), .m_axis_tkeep(m_axis_tkeep), .m_axis_tvalid(m_axis_tvalid), .m_axis_tready(m_axis_tready), .m_axis_tlast(m_axis_tlast), .m_axis_tid(m_axis_tid), .m_axis_tdest(m_axis_tdest), .m_axis_tuser(m_axis_tuser));
endmodule
module axis_disabled(input wire clk, rst, fault,
input wire [15:0] s_axis_tdata, input wire [1:0] s_axis_tkeep,
input wire s_axis_tvalid, output wire s_axis_tready, input wire s_axis_tlast,
input wire [3:0] s_axis_tid, s_axis_tdest, input wire [1:0] s_axis_tuser,
output wire [15:0] m_axis_tdata, output wire [1:0] m_axis_tkeep,
output wire m_axis_tvalid, input wire m_axis_tready, output wire m_axis_tlast,
output wire [3:0] m_axis_tid, m_axis_tdest, output wire [1:0] m_axis_tuser);
wire [15:0] raw_data;
assign m_axis_tdata = raw_data ^ {15'b0,fault};
axis_register #(.DATA_WIDTH(16), .KEEP_ENABLE(0), .KEEP_WIDTH(2),
 .LAST_ENABLE(0), .ID_ENABLE(0), .ID_WIDTH(4),
 .DEST_ENABLE(0), .DEST_WIDTH(4), .USER_ENABLE(0), .USER_WIDTH(2),
 .REG_TYPE(2)) dut (.clk(clk), .rst(rst), .s_axis_tdata(s_axis_tdata), .s_axis_tkeep(s_axis_tkeep), .s_axis_tvalid(s_axis_tvalid), .s_axis_tready(s_axis_tready), .s_axis_tlast(s_axis_tlast), .s_axis_tid(s_axis_tid), .s_axis_tdest(s_axis_tdest), .s_axis_tuser(s_axis_tuser), .m_axis_tdata(raw_data), .m_axis_tkeep(m_axis_tkeep), .m_axis_tvalid(m_axis_tvalid), .m_axis_tready(m_axis_tready), .m_axis_tlast(m_axis_tlast), .m_axis_tid(m_axis_tid), .m_axis_tdest(m_axis_tdest), .m_axis_tuser(m_axis_tuser));
endmodule
