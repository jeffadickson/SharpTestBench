`timescale 1ns/1ps
// Locally authored qualification fixtures; these are bounded test designs, not
// certification of an external bus or CRC wire protocol.
module protocol_top;
 reg clk=0,rst=0;
 reg [7:0] probe_value=0;
 // One elastic CRC stream stage. A packet starts at zero; accepted last resets
 // the accumulator for the next packet. Polynomial x^8+x^2+x+1, MSB first.
 reg c_valid=0,c_last=0,c_ready=0;
 reg [7:0] c_data=0;
 reg c_out_valid=0,c_out_last=0;
 reg [7:0] c_out_data=0,c_out_crc=0;
 reg [15:0] c_out_length=0,c_length=0;
 reg [7:0] c_crc=0;
 wire c_in_ready=!c_out_valid||c_ready;
 function automatic [7:0] crc_byte(input [7:0] state,input [7:0] data);
  reg [7:0] value;integer i;
  begin value=state^data;for(i=0;i<8;i=i+1)value=value[7]?(value<<1)^8'h07:value<<1;crc_byte=value;end
 endfunction
 always @(posedge clk) begin
  if(rst)begin c_out_valid<=0;c_out_last<=0;c_out_data<=0;c_out_crc<=0;c_out_length<=0;c_length<=0;c_crc<=0;end
  else if(c_in_ready)begin
   c_out_valid<=c_valid;
   if(c_valid)begin
    c_out_data<=c_data;c_out_crc<=crc_byte(c_crc,c_data);c_out_length<=c_length+1'b1;c_out_last<=c_last;
    if(c_last)begin c_crc<=0;c_length<=0;end
    else begin c_crc<=crc_byte(c_crc,c_data);c_length<=c_length+1'b1;end
   end
  end
 end
 // Four-way rotating priority. Grant is combinational, disabled requests are
 // ignored, and only acknowledged winners advance the pointer. No owner lock.
 reg [3:0] a_request=0;
 reg a_enable=0,a_ack=0;
 reg [1:0] a_pointer=0;
 reg [3:0] a_grant;
 integer offset,index;reg found;
 always @* begin
  a_grant=0;found=0;index=0;
  for(offset=0;offset<4;offset=offset+1)begin
   index=(a_pointer+offset)%4;
   if(a_enable&&!found&&a_request[index])begin a_grant[index]=1'b1;found=1;end
  end
 end
 always @(posedge clk) begin
  if(rst)a_pointer<=0;
  else if(a_enable&&a_ack)begin
   if(a_grant[0])a_pointer<=1;else if(a_grant[1])a_pointer<=2;
   else if(a_grant[2])a_pointer<=3;else if(a_grant[3])a_pointer<=0;
  end
 end
 // Eight 16-bit words with independent little-byte enables and synchronous
 // read-before-write collision semantics. Reset dominates all read/write inputs.
 reg b_read=0,b_write=0;
 reg [2:0] b_address=0;
 reg [1:0] b_mask=0;
 reg [15:0] b_data=0,b_result=0;
 reg [15:0] b_memory[0:7];integer word_index;
 initial for(word_index=0;word_index<8;word_index=word_index+1)b_memory[word_index]=0;
 always @(posedge clk)begin
  if(rst)begin b_result<=0;for(word_index=0;word_index<8;word_index=word_index+1)b_memory[word_index]<=0;end
  else begin
   if(b_read)b_result<=b_memory[b_address];
   if(b_write)begin
    if(b_mask[0])b_memory[b_address][7:0]<=b_data[7:0];
    if(b_mask[1])b_memory[b_address][15:8]<=b_data[15:8];
   end
  end
 end
 wire [15:0] b_obs0=b_memory[0],b_obs1=b_memory[1],b_obs2=b_memory[2],b_obs3=b_memory[3];
 wire [15:0] b_obs4=b_memory[4],b_obs5=b_memory[5],b_obs6=b_memory[6],b_obs7=b_memory[7];
endmodule