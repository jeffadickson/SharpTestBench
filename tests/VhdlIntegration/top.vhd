library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;
entity top is generic (COUNT : integer := 37); end;
architecture rtl of top is
 signal driver_a, driver_b : std_logic := 'Z';
 signal resolved_bus : std_logic;
 signal delta_source, delta1, delta2, delta3 : std_logic_vector(7 downto 0) := (others=>'0');
 signal clk : std_logic := '0';
 signal data : std_logic_vector(7 downto 0) := (others=>'0');
 signal sampled : std_logic_vector(7 downto 0) := (others=>'0');
 signal logic_data : std_logic_vector(3 downto 0) := (others=>'0');
 signal nine : std_logic_vector(2 to 10) := (others=>'0');
 signal empty_vector : std_logic_vector(3 to 2);
 signal integer_data : integer := -17;
 signal real_data : real := 1.5;
 signal text_data : string(2 to 6) := "HELLO";
 type mode_t is (idle, active, done);
 signal mode_data : mode_t := idle;
 type word_array is array(2 to 4) of std_logic_vector(7 downto 0);
 signal words : word_array := (others=>(others=>'0'));
 type grid_array is array(1 to 2) of word_array;
 signal grid : grid_array := (others=>(others=>(others=>'0')));
 type packet_t is record
   valid : std_logic;
   payload : std_logic_vector(7 downto 0);
 end record;
 signal packet : packet_t := ('0', x"00");
begin
 resolved_bus <= driver_a;
 resolved_bus <= driver_b;
 delta1 <= delta_source xor x"01";
 delta2 <= delta1 xor x"02";
 delta3 <= delta2 xor x"04";
 process(clk) begin if rising_edge(clk) then sampled <= data;end if;end process;
end;
