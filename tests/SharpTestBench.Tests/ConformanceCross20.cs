using SharpTestBench;
using Xunit;
using System.Globalization;
public sealed partial class ConformanceCrossTests
{
    [Fact] public void C016_LengthPrefixedNamesCannotCollideAtAxisBoundaries()
    {var l=new CoverageBin<int>[] {new("a:b",x=>x==0),new("a",x=>x==1)};var r=new CoverageBin<int>[] {new("c",x=>x==0),new("b:c",x=>x==1)};var p=new FunctionalCoverage().AddCross("p",l,r);Assert.Equal(4,p.Snapshot().Bins.Select(b=>b.Name).Distinct(StringComparer.Ordinal).Count());p.Sample((0,0));p.Sample((1,1));Assert.Equal(new ulong[]{1,0,0,1},p.Snapshot().Bins.Select(b=>b.Hits));}
    [Fact] public void C017_CellIdentityIsInvariantAcrossCurrentCultures()
    {var old=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("ar-SA");var p=Cross(new());Assert.Equal("4:zero4:zero",p.Snapshot().Bins[0].Name);CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("tr-TR");Assert.Equal(p.Snapshot().Bins.Select(b=>b.Name),Cross(new()).Snapshot().Bins.Select(b=>b.Name));}finally{CultureInfo.CurrentCulture=old;}}
    [Fact] public void C018_DifferentAxisTypesPreserveTupleCoordinates()
    {var p=new FunctionalCoverage().AddCross("p",new CoverageBin<string>[] {new("read",s=>s=="read"),new("write",s=>s=="write")},new CoverageBin<bool>[] {new("ready",b=>b),new("stall",b=>!b)});p.Sample(("write",false));Assert.Equal(new ulong[]{0,0,0,1},p.Snapshot().Bins.Select(b=>b.Hits));}
    [Fact] public void C019_InvalidClassifierDefinitionsCannotReserveAName()
    {var c=new FunctionalCoverage();CoverageBin<int>[][] bad=[[null!],[new(" ",x=>true)],[new("x",null!)],[new("x",x=>true,0)]];foreach(var axis in bad){Assert.Throws<ArgumentException>(()=>c.AddCross("p",axis,Axis()));Assert.Empty(c.Snapshot().Points);}Assert.NotNull(c.AddCross("p",Axis(),Axis()));}
    [Fact] public void C020_ForeignThreadCannotRegisterOrReplaceACross()
    {var c=new FunctionalCoverage();Exception? failure=null;var t=new Thread(()=>{try{c.AddCross("p",Axis(),Axis());}catch(Exception error){failure=error;}});t.Start();t.Join();Assert.IsType<InvalidOperationException>(failure);Assert.Empty(c.Snapshot().Points);Assert.NotNull(c.AddCross("p",Axis(),Axis()));}
}
