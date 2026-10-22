`timescale 1ns/1ps
// Locally authored acceptance hardware: two-stage elastic transport,
// byte-enabled register bank, and signed-step saturating accumulator.
module conformance_top;
 reg clk=0, rst=1;
 reg p_svalid=0, p_mready=0;
 reg [7:0] p_sdata=0;
 reg p_v0=0,p_v1=0;
 reg [7:0] p_d0=0,p_d1=0;
 wire p_r1=!p_v1 || p_mready;
 wire p_sready=!p_v0 || p_r1;
 wire p_mvalid=p_v1;
 wire [7:0] p_mdata=p_d1;
 wire [1:0] p_count={1'b0,p_v0}+{1'b0,p_v1};
 always @(posedge clk) begin
  if(rst) begin p_v0<=0;p_v1<=0;p_d0<=0;p_d1<=0;end
  else begin
   if(p_r1) begin p_v1<=p_v0;if(p_v0)p_d1<=p_d0;end
   if(p_sready) begin p_v0<=p_svalid;if(p_svalid)p_d0<=p_sdata;end
  end
 end
 reg rb_write=0;
 reg [2:0] rb_addr=0;
 reg [3:0] rb_be=0;
 reg [31:0] rb_wdata=0;
 reg [31:0] bank[0:2];
 parameter [31:0] DEVICE_ID=32'hC0DE2026;
 wire [31:0] rb_rdata=rb_addr==0?bank[0]:rb_addr==1?bank[1]:rb_addr==2?bank[2]:rb_addr==3?DEVICE_ID:32'b0;
 wire rb_error=(rb_addr>3)||(rb_write&&rb_addr==3);
 wire [31:0] rb_word0=bank[0],rb_word1=bank[1],rb_word2=bank[2];
 wire [7:0] rb_low=rb_rdata[7:0],rb_high=rb_rdata[31:24];
 integer lane;
 always @(posedge clk) begin
  if(rst)begin bank[0]<=0;bank[1]<=0;bank[2]<=0;end
  else if(rb_write&&rb_addr<3)
   for(lane=0;lane<4;lane=lane+1)
    if(rb_be[lane])bank[rb_addr][lane*8+:8]<=rb_wdata[lane*8+:8];
 end
 reg a_enable=0,a_load=0,a_clear=0;
 reg signed [7:0] a_delta=0;
 reg [7:0] a_load_value=0;
 reg [7:0] a_value=0;
 reg a_overflow=0;
 wire signed [9:0] a_sum=$signed({2'b00,a_value})+$signed({{2{a_delta[7]}},a_delta});
 always @(posedge clk) begin
  if(rst)begin a_value<=0;a_overflow<=0;end
  else if(a_load)begin a_value<=a_load_value;a_overflow<=0;end
  else begin
   if(a_clear)a_overflow<=0;
   if(a_enable)begin
    if(a_sum>255)begin a_value<=255;a_overflow<=1;end
    else if(a_sum<0)begin a_value<=0;a_overflow<=1;end
    else a_value<=a_sum[7:0];
   end
  end
 end
 real real_value=0.0;
 integer signed_value=-7;
 wire [31:0] signed_alias=signed_value;
 reg [0:7] ascending=0;
 wire ascending_left=ascending[0],ascending_right=ascending[7];
endmodule
