`timescale 1ns/1ps
// AXI4-Lite-shaped, one outstanding read/write. Test fixture, not production IP.
// Independent AW/W capture; responses stay stable until accepted. Reset clears RAM.
module axi_memory(
 input logic clk, reset_n,
 input logic awvalid, output wire awready, input logic [11:0] awaddr,
 input logic wvalid, output wire wready, input logic [31:0] wdata, input logic [3:0] wstrb,
 output logic bvalid, input logic bready, output logic [1:0] bresp,
 input logic arvalid, output wire arready, input logic [11:0] araddr,
 output logic rvalid, input logic rready, output logic [31:0] rdata, output logic [1:0] rresp,
 input logic stall_aw, stall_w, stall_ar, input logic [1:0] fault
);
 logic [31:0] memory [0:63];
 logic awheld, wheld;
 logic [11:0] address;
 logic [31:0] data;
 logic [3:0] strobe;
 assign awready=reset_n && !awheld && !bvalid && !stall_aw;
 assign wready=reset_n && !wheld && !bvalid && !stall_w;
 assign arready=reset_n && !rvalid && !stall_ar;
 function automatic bit legal(input logic [11:0] a);
   return a < 256 && a[1:0] == 0;
 endfunction
 always @(posedge clk) begin
   if (!reset_n) begin
     awheld<=0; wheld<=0; bvalid<=0; rvalid<=0; bresp<=0; rresp<=0; rdata<=0;
     for (integer i=0;i<64;i=i+1) memory[i]<=0;
   end else begin
     if (awvalid && awready) begin address<=awaddr; awheld<=1; end
     if (wvalid && wready) begin data<=wdata; strobe<=wstrb; wheld<=1; end
     if (bvalid && bready) bvalid<=0;
     if (awheld && wheld && !bvalid) begin
       if (legal(address))
         for (integer lane=0;lane<4;lane=lane+1)
           if (strobe[lane] || fault==2) memory[address[7:2]][8*lane+:8]<=data[8*lane+:8];
       bresp<=legal(address) ? 0 : 2;
       bvalid<=fault!=3; awheld<=0; wheld<=0;
     end
     if (rvalid && rready) rvalid<=0;
     if (arvalid && arready) begin
       rdata<=legal(araddr) ? (memory[araddr[7:2]] ^ (fault==1 ? 32'b1 : 32'b0)) : 0;
       rresp<=legal(araddr) ? 0 : 2;
       rvalid<=1;
     end
   end
 end
endmodule
