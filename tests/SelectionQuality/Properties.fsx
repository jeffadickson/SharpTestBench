#r "nuget: FsCheck, 3.6.0"
#r "../../src/SharpTestBench/bin/Release/net10.0/SharpTestBench.dll"
open System
open System.Text.RegularExpressions
open FsCheck
open FsCheck.FSharp
open SharpTestBench
// Fixed seed and bounded UTF-16 inputs make failures replayable. FsCheck shrinks
// arrays by deletion, retaining the smallest counterexample for literal matching.
let seed = 0x53454C323030UL
let alphabet = [|'a';'A';'Ω';'é';'\n';'\u0000';'.';'[';'$';'\\';'+';'中'|]
let chars = Gen.choose (0, 128) |> Gen.bind (fun n -> Gen.arrayOfLength n (Gen.elements alphabet))
let shrink (xs:char[]) = seq { for n in 0 .. xs.Length-1 do yield Array.removeAt n xs }
let strings = Arb.fromGenShrink (Gen.map (fun (c:char[]) -> String(c)) chars, fun (s:string) -> shrink (s.ToCharArray()) |> Seq.map (fun c -> String(c)))
let config name = Config.QuickThrowOnFailure.WithMaxTest(1000).WithReplay(Some { Rnd=Rnd(seed); Size=None }).WithName(name)
let literal (s:string) = "\\A" + Regex.Escape(s) + "\\z"
Check.One(config "literal equality", Prop.forAll strings (fun expected ->
 Prop.forAll strings (fun actual -> RegressionSelection([|literal expected|],matchTimeout=TimeSpan.FromSeconds(1.)).Matches(actual) = String.Equals(expected,actual,StringComparison.Ordinal))))
Check.One(config "OR algebra", Prop.forAll strings (fun first ->
 Prop.forAll strings (fun second ->
  Prop.forAll strings (fun candidate ->
   RegressionSelection([|literal first;literal second|],matchTimeout=TimeSpan.FromSeconds(1.)).Matches(candidate) =
    (String.Equals(first,candidate,StringComparison.Ordinal) || String.Equals(second,candidate,StringComparison.Ordinal))))))
Check.One(config "snapshot isolation", Prop.forAll strings (fun original ->
 let patterns=[|literal original|]
 let selection=RegressionSelection(patterns,matchTimeout=TimeSpan.FromSeconds(1.))
 patterns[0] <- "(?!)"
 selection.Matches(original)))
printfn "STB-VER-002: FsCheck 3.6.0; seed=%d; three properties x 1000 cases; no failed counterexamples." seed// Malformed-input fuzzing is separate from algebraic properties: regex syntax
// rejection and explicit timeout are valid outcomes, but unexpected exceptions fail.
let random=Random(20261024)
let syntax="ab[](){}?*+|\\^$.Ω"
let mutable accepted=0
let mutable rejected=0
let mutable timedOut=0
for caseIndex in 1..2000 do
 let pattern=String(Array.init (random.Next(41)) (fun _ -> syntax[random.Next(syntax.Length)]))
 try
  let selection=RegressionSelection([|pattern|],matchTimeout=TimeSpan.FromMilliseconds(1.))
  accepted<-accepted+1
  try selection.Matches(String('a',128)+"B") |> ignore
  with :? RegexMatchTimeoutException -> timedOut<-timedOut+1
 with :? ArgumentException -> rejected<-rejected+1
if accepted+rejected<>2000 then failwith "Fuzz accounting mismatch"
printfn "Malformed fuzz: seed=20261024 cases=2000 accepted=%d rejected=%d matchTimeouts=%d; no unexpected exceptions." accepted rejected timedOut