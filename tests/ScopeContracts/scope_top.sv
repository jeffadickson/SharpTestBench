`timescale 1ns/1ps
// Locally authored fixtures. Single elastic checksum stage, independent of prior batches.
module scope_top;
  reg clk=0, rst=0;
  reg f_bit=0;
  reg [7:0] f_byte=0;
  wire [7:0] f_complement=~f_byte;
  wire f_parity=^f_byte;
  reg [95:0] f_wide=0;
  integer f_integer=0;
  real f_real=0.0;
  reg [7:0] f_driver=0;
  wire [7:0] f_driven=f_driver;
  string f_text="scope";
  integer f_memory [0:1];
  initial begin f_memory[0]=32'h12345678;f_memory[1]=32'h76543210;end
  wire [31:0] f_memory_alias=f_memory[0]^f_memory[1];
  parameter [7:0] F_CONSTANT=8'h3c;
  wire [7:0] f_constant_alias=F_CONSTANT;
  typedef enum logic [2:0] {IDLE=0, BUSY=3, DONE=7} state_t;
  state_t f_enum=IDLE;
  reg s_valid=0,s_last=0,m_ready=0;
  reg [7:0] s_data=0;
  reg m_valid=0,m_last=0;
  wire s_ready=!m_valid||m_ready;
  reg [7:0] m_data=0,m_sequence=0;
  reg [7:0] checksum=0,sequence_number=0;
  always @(posedge clk) begin
    if(rst) begin m_valid<=0;m_last<=0;m_data<=0;m_sequence<=0;checksum<=0;sequence_number<=0;end
    else if(s_ready) begin
      m_valid<=s_valid;
      if(s_valid) begin
        m_data<=s_data^checksum; m_last<=s_last; m_sequence<=sequence_number;
        if(s_last) begin checksum<=0;sequence_number<=sequence_number+1'b1;end
        else checksum<=checksum^s_data;
      end
    end
  end
endmodule
