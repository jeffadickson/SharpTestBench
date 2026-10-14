// Generated from elaborated HDL. Constructors check widths and signs.
using CocotbSharp;
namespace @GeneratedVhdl;
public readonly struct Width1:IWidth { public static int Bits => 1; }
public readonly struct Width4:IWidth { public static int Bits => 4; }
public readonly struct Width8:IWidth { public static int Bits => 8; }
public readonly struct Width9:IWidth { public static int Bits => 9; }
public sealed class @VhdlBindings_Scope0 {
 public Signal<Width8> @payload {get;}
 public Signal<Width1> @valid {get;}
 public @VhdlBindings_Scope0(HdlObject root) {
  @payload = root.Child("payload").Signal<Width8>();
  @valid = root.Child("valid").Signal<Width1>();
 }
}
public sealed class @VhdlBindings {
 public IntegerSignal @COUNT {get;}
 public Signal<Width1> @clk {get;}
 public Signal<Width8> @data {get;}
 public Signal @empty_vector {get;}
 public HdlArray<HdlArray<Signal<Width8>>> @grid {get;}
 public IntegerSignal @integer_data {get;}
 public Signal<Width4> @logic_data {get;}
 public IntegerSignal @mode_data {get;}
 public Signal<Width9> @nine {get;}
 public @VhdlBindings_Scope0 @packet {get;}
 public RealSignal @real_data {get;}
 public Signal<Width8> @sampled {get;}
 public StringSignal @text_data {get;}
 public HdlArray<Signal<Width8>> @words {get;}
 public @VhdlBindings(HdlObject root) {
  @COUNT = root.Child("COUNT").Integer();
  @clk = root.Child("clk").Signal<Width1>();
  @data = root.Child("data").Signal<Width8>();
  @empty_vector = root.Child("empty_vector").Signal();
  @grid = root.Child("grid").Array(element1 => element1.Array(element2 => element2.Signal<Width8>()));
  @integer_data = root.Child("integer_data").Integer();
  @logic_data = root.Child("logic_data").Signal<Width4>();
  @mode_data = root.Child("mode_data").Integer();
  @nine = root.Child("nine").Signal<Width9>();
  @packet = new @VhdlBindings_Scope0(root.Child("packet"));
  @real_data = root.Child("real_data").Real();
  @sampled = root.Child("sampled").Signal<Width8>();
  @text_data = root.Child("text_data").String();
  @words = root.Child("words").Array(element1 => element1.Signal<Width8>());
 }
}
