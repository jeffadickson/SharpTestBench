// Generated from elaborated HDL. Constructors check widths and signs.
using CocotbSharp;
namespace @Generated;
public readonly struct Width1:IWidth { public static int Bits => 1; }
public readonly struct Width4:IWidth { public static int Bits => 4; }
public readonly struct Width8:IWidth { public static int Bits => 8; }
public readonly struct Width16:IWidth { public static int Bits => 16; }
public readonly struct Width32:IWidth { public static int Bits => 32; }
public readonly struct Width256:IWidth { public static int Bits => 256; }
public sealed class @DutBindings_Scope0 {
 public UnsignedSignal<Width8> @local_data {get;}
 public @DutBindings_Scope0(HdlObject root) {
  @local_data = root.Child("local_data").Unsigned<Width8>();
 }
}
public sealed class @DutBindings {
 public UnsignedSignal<Width8> @PARAMETER {get;}
 public UnsignedSignal<Width8> @ascending {get;}
 public UnsignedSignal<Width32> @bench32 {get;}
 public UnsignedSignal<Width1> @clk {get;}
 public UnsignedSignal<Width8> @data {get;}
 public UnsignedSignal<Width8> @doubled {get;}
 public UnsignedSignal<Width8> @in_data {get;}
 public UnsignedSignal<Width1> @in_ready {get;}
 public UnsignedSignal<Width1> @in_valid {get;}
 public UnsignedSignal<Width4> @logic_data {get;}
 public HdlArray<UnsignedSignal<Width8>> @memory {get;}
 public UnsignedSignal<Width8> @out_data {get;}
 public UnsignedSignal<Width1> @out_ready {get;}
 public UnsignedSignal<Width1> @out_valid {get;}
 public RealSignal @real_data {get;}
 public UnsignedSignal<Width1> @reset {get;}
 public UnsignedSignal<Width8> @sampled {get;}
 public SignedSignal<Width16> @signed_data {get;}
 public @DutBindings_Scope0 @unit0 {get;}
 public UnsignedSignal<Width256> @wide {get;}
 public @DutBindings(HdlObject root) {
  @PARAMETER = root.Child("PARAMETER").Unsigned<Width8>();
  @ascending = root.Child("ascending").Unsigned<Width8>();
  @bench32 = root.Child("bench32").Unsigned<Width32>();
  @clk = root.Child("clk").Unsigned<Width1>();
  @data = root.Child("data").Unsigned<Width8>();
  @doubled = root.Child("doubled").Unsigned<Width8>();
  @in_data = root.Child("in_data").Unsigned<Width8>();
  @in_ready = root.Child("in_ready").Unsigned<Width1>();
  @in_valid = root.Child("in_valid").Unsigned<Width1>();
  @logic_data = root.Child("logic_data").Unsigned<Width4>();
  @memory = root.Child("memory").Array(element1 => element1.Unsigned<Width8>());
  @out_data = root.Child("out_data").Unsigned<Width8>();
  @out_ready = root.Child("out_ready").Unsigned<Width1>();
  @out_valid = root.Child("out_valid").Unsigned<Width1>();
  @real_data = root.Child("real_data").Real();
  @reset = root.Child("reset").Unsigned<Width1>();
  @sampled = root.Child("sampled").Unsigned<Width8>();
  @signed_data = root.Child("signed_data").Signed<Width16>();
  @unit0 = new @DutBindings_Scope0(root.Child("unit0"));
  @wide = root.Child("wide").Unsigned<Width256>();
 }
}
