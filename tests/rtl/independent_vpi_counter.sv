// Standalone cocotb-free VPI integration fixture.
module top;
  reg clk = 0;
  reg reset = 1;
  reg enable = 1;
  wire [7:0] count;
  counter dut(.clk(clk), .reset(reset), .enable(enable), .count(count));
  always #5 clk = ~clk;
  initial begin
    repeat (2) @(negedge clk);
    reset = 0;
    repeat (6) @(negedge clk);
    $finish;
  end
endmodule
