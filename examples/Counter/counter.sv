module counter(input logic clk, reset, enable, output logic [7:0] count);
    always @(posedge clk)
        if (reset) count <= 0;
        else if (enable) count <= count + 1;
endmodule
