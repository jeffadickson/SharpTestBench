using SharpTestBench;
using static SharpTestBench.Triggers;
public static partial class Entry
{
    [SimTest] public static async Task R021_SiblingPackedDepositsComposeWithoutLostUpdates(TestContext c)
    {var word=c.Dut.Child("packed_word").Signal<WordWidth>();word.Write(new UInt<WordWidth>(0));word.Field<NibbleWidth>(0).Write(new UInt<NibbleWidth>(10));word.Field<NibbleWidth>(4).Write(new UInt<NibbleWidth>(11));await ReadOnly();Check(word.ReadUInt().Value==0xBA,"sibling nibble composition");}
    [SimTest] public static async Task R022_StructMembersUseCalculatedMsbFirstOffsets(TestContext c)
    {var word=c.Dut.Child("packed_word").Signal<WordWidth>();var view=word.Struct(new(new PackedMember("upper",16),new PackedMember("lower",16)));view.Field<HalfWidth>("upper").Write(new UInt<HalfWidth>(0x1234));view.Field<HalfWidth>("lower").Write(new UInt<HalfWidth>(0xABCD));await ReadOnly();Check(word.ReadUInt().Value==0x1234ABCD,"explicit struct field positions");}
    [SimTest] public static async Task R023_WrongFieldWidthCannotReplacePendingDeposit(TestContext c)
    {var word=c.Dut.Child("packed_word").Signal<WordWidth>();word.Write(new UInt<WordWidth>(0x76543210));var view=word.Struct(new(new PackedMember("upper",16),new PackedMember("lower",16)));Throws<ArgumentException>(()=>view.Field<ByteWidth>("lower"));await ReadOnly();Check(word.ReadUInt().Value==0x76543210,"failed field conversion preserves write");}
    [SimTest] public static async Task R024_MixedDirectionPackedDimensionsMapToExpectedByteOrder(TestContext c)
    {var word=c.Dut.Child("packed_word").Signal<WordWidth>();var bytes=word.Packed<ByteWidth>(new HdlRange(0,1,true),new HdlRange(1,0,false));bytes[0,1].Write(new UInt<ByteWidth>(0x11));bytes[0,0].Write(new UInt<ByteWidth>(0x22));bytes[1,1].Write(new UInt<ByteWidth>(0x33));bytes[1,0].Write(new UInt<ByteWidth>(0x44));await ReadOnly();Check(word.ReadUInt().Value==0x11223344,"explicit rank2 mixed direction");}
    [SimTest] public static async Task R025_InvalidPackedRankAndIndexPreserveQueuedValue(TestContext c)
    {var word=c.Dut.Child("packed_word").Signal<WordWidth>();word.Write(new UInt<WordWidth>(0xDEADBEEF));var bytes=word.Packed<ByteWidth>(new HdlRange(0,1,true),new HdlRange(1,0,false));int callbacks=c.Runtime.PendingCallbacks;Throws<ArgumentException>(()=>{_ = bytes[0,1,0];});Throws<IndexOutOfRangeException>(()=>{_ = bytes[2,0];});Check(callbacks==c.Runtime.PendingCallbacks,"bad index adds no callback");await ReadOnly();Check(word.ReadUInt().Value==0xDEADBEEF,"bad index preserves native deposit");}
    [SimTest] public static async Task R026_SignedInterpretationPreservesRawTwosComplement(TestContext c)
    {var value=c.Dut.Child("signed_data").Signal<ByteWidth>();value.Write(new SInt<ByteWidth>(-128));await ReadOnly();Check(value.ReadSInt().Value==-128&&value.ReadUInt().Value==128,"explicit signed and unsigned interpretations");}
    [SimTest] public static async Task R027_RealDepositRetainsNegativeFraction(TestContext c)
    {var value=c.Dut.Child("real_value").Real();value.Write(-2.75);await ReadOnly();Check(value.Read()==-2.75,"native real roundtrip");}
    [SimTest] public static async Task R028_HdlIntegerRetainsNegativeValueForObservedRepresentation(TestContext c)
    {var obj=c.Dut.Child("integer_value");
      if(obj.Kind is ObjectKind.Integer or ObjectKind.Enum){var value=obj.Integer();value.Write(-12345);await ReadOnly();Check(value.Read()==-12345,"native integer sign roundtrip");}
      else{Check(obj.Kind is (ObjectKind.Packed or ObjectKind.LogicArray)&&obj.Signal().Width==32,$"backend integer representation observed kind={obj.Kind}, width={obj.Signal().Width}; expected32-bit packed vector");var value=obj.Signal<WordWidth>();value.Write(new SInt<WordWidth>(-12345));await ReadOnly();Check(value.ReadSInt().Value==-12345,"packed backend integer sign roundtrip");}}
    [SimTest] public static async Task R029_ArrayOffsetAndIndexShareConvertedHandle(TestContext c)
    {var memory=c.Dut.Child("memory").Array(x=>x.Signal<ByteWidth>());Check(memory.Count==4&&ReferenceEquals(memory[2],memory.ByOffset(2)),"typed array cached conversion");memory[2].Write(new UInt<ByteWidth>(92));await ReadOnly();Check(memory.ByOffset(2).ReadUInt().Value==92,"cached indexed native readback");}
    [SimTest] public static async Task R030_AscendingHdlRangeRetainsDeclaredIndexDirection(TestContext c)
    {var value=c.Dut.Child("ascending").Signal<ByteWidth>();value.WriteBits("10010110");await ReadOnly();var bits=value.ReadLogic();Check(bits.Range.Left==0&&bits.Range.Right==7&&bits.ToString()=="10010110"&&c.Dut.Child("ascending_left").Signal().ReadBit()&&!c.Dut.Child("ascending_right").Signal().ReadBit(),"ascending signal range and displayed ordering");}
}
