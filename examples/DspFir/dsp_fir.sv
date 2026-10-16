`timescale 1ns/1ps
// Four signed taps and a one-entry elastic output stage. State advances only
// when an input is accepted; output and valid hold through downstream stalls.
module dsp_fir(
 input logic clk, reset, in_valid, out_ready,
 input logic signed [15:0] sample,
 input logic [1:0] fault,
 output wire in_ready,
 output logic out_valid,
 output logic signed [31:0] result
);
 logic signed [15:0] d1, d2, d3;
 logic signed [31:0] sum;
 assign in_ready = !out_valid || out_ready;
 // Explicit sign extension makes the accumulator width independent of context.
 always_comb sum = -3 * $signed(sample) + 7 * $signed(d1)
                     + 11 * $signed(d2) - 2 * $signed(d3);
 always @(posedge clk) begin
  if (reset) begin d1<=0; d2<=0; d3<=0; result<=0; out_valid<=0; end
  else if (in_ready) begin
   out_valid <= in_valid;
   if (in_valid) begin
    result <= fault == 1 ? sum ^ 32'h1 :
              fault == 2 ? sum - (sample[15] ? 32'sd196608 : 32'sd0) : sum;
    d3<=d2; d2<=d1; d1<=sample;
   end
  end else if (fault == 3) out_valid <= 0; // Deliberate loss under backpressure.
 end
endmodule
