#r "nuget: FsCheck, 3.6.0"
#r "../../src/SharpTestBench/bin/Release/net10.0/SharpTestBench.dll"
open System
open System.Text.Json
open System.Text.RegularExpressions
open FsCheck
open FsCheck.FSharp
open SharpTestBench

// Each invocation is a bounded, seed-addressable batch. FsCheck performs its
// normal shrinking. The first failing input is emitted before shrinking, so a
// parent deadline can still preserve a reproducer if minimization is interrupted.
let args = fsi.CommandLineArgs |> Array.skip 1
let pairs = args |> Array.chunkBySize 2
if pairs |> Array.exists (fun pair -> pair.Length <> 2) then failwith "Expected option/value pairs"
let options = pairs |> Array.map (fun pair -> pair[0],pair[1]) |> Map.ofArray
if options.Count <> pairs.Length || options |> Map.exists (fun key _ -> not (List.contains key ["--seed";"--property-cases";"--fuzz-cases"])) then failwith "Unknown or duplicate worker option"
let seed = UInt64.Parse(options["--seed"],Globalization.CultureInfo.InvariantCulture)
let cases = Int32.Parse(options["--property-cases"],Globalization.CultureInfo.InvariantCulture)
let fuzz = Int32.Parse(options["--fuzz-cases"],Globalization.CultureInfo.InvariantCulture)
if cases < 1 || cases > 10000 || fuzz < 1 || fuzz > 100000 then failwith "Worker counts outside bounds"
let mutable properties = 0
let mutable fuzzCompleted = 0
let mutable accepted = 0
let mutable rejected = 0
let mutable timeouts = 0
let mutable lastFailure : string = null
let mutable diagnostic : string = null
let mutable firstFailure = true
let note (inputs:string[]) =
 let value = JsonSerializer.Serialize(inputs)
 lastFailure <- value
 if firstFailure then
  firstFailure <- false
  printfn "ENDURANCE_FAILING_INPUT %s" value
let check inputs action =
 try
  let passed = action()
  if not passed then note inputs
  passed
 with error -> note inputs; reraise()
let alphabet = [|'a';'A';'Ω';'é';'\n';'\u0000';'.';'[';'$';'\\';'+';'中'|]
let chars = Gen.choose (0,128) |> Gen.bind (fun n -> Gen.arrayOfLength n (Gen.elements alphabet))
let shrink (xs:char[]) = seq { for n in 0..xs.Length-1 do yield Array.removeAt n xs }
let strings = Arb.fromGenShrink (Gen.map (fun (xs:char[]) -> String(xs)) chars,fun (s:string) -> shrink (s.ToCharArray()) |> Seq.map String)
let config name ordinal = Config.QuickThrowOnFailure.WithMaxTest(cases).WithReplay(Some {Rnd=Rnd(seed + uint64 ordinal);Size=None}).WithName(name)
let literal text = "\\A" + Regex.Escape(text) + "\\z"
try
 Check.One(config "endurance literal equality" 0, Prop.forAll strings (fun expected -> Prop.forAll strings (fun actual ->
  check [|expected;actual|] (fun () -> RegressionSelection([|literal expected|],matchTimeout=TimeSpan.FromSeconds(1.)).Matches(actual) = String.Equals(expected,actual,StringComparison.Ordinal)))))
 properties <- properties + 1
 Check.One(config "endurance OR algebra" 1, Prop.forAll strings (fun first -> Prop.forAll strings (fun second -> Prop.forAll strings (fun candidate ->
  check [|first;second;candidate|] (fun () -> RegressionSelection([|literal first;literal second|],matchTimeout=TimeSpan.FromSeconds(1.)).Matches(candidate) = (String.Equals(first,candidate,StringComparison.Ordinal) || String.Equals(second,candidate,StringComparison.Ordinal)))))))
 properties <- properties + 1
 Check.One(config "endurance snapshot isolation" 2, Prop.forAll strings (fun original ->
  check [|original|] (fun () ->
   let patterns = [|literal original|]
   let selection = RegressionSelection(patterns,matchTimeout=TimeSpan.FromSeconds(1.))
   patterns[0] <- "(?!)"
   selection.Matches(original))))
 properties <- properties + 1
 let random = Random(int (seed &&& 0x7fffffffUL))
 let syntax = "ab[](){}?*+|\\^$.Ω"
 for _ in 1..fuzz do
  let pattern = String(Array.init (random.Next(41)) (fun _ -> syntax[random.Next(syntax.Length)]))
  try
   let selection = RegressionSelection([|pattern|],matchTimeout=TimeSpan.FromMilliseconds(1.))
   accepted <- accepted + 1
   try selection.Matches(String('a',128)+"B") |> ignore
   with :? RegexMatchTimeoutException -> timeouts <- timeouts + 1
  with
  | :? ArgumentException -> rejected <- rejected + 1
  | error -> note [|pattern|]; reraise()
  fuzzCompleted <- fuzzCompleted + 1
with error -> diagnostic <- error.ToString().Substring(0,min 8192 (error.ToString().Length))
let status = if isNull diagnostic then "passed" else "failed"
let result = {|Seed=seed;PropertiesCompleted=properties;PropertyCasesCompleted=properties*cases;FuzzCasesCompleted=fuzzCompleted;Accepted=accepted;Rejected=rejected;MatchTimeouts=timeouts;Status=status;LastFailingInput=lastFailure;FailureDiagnostic=diagnostic|}
printfn "ENDURANCE_BATCH %s" (JsonSerializer.Serialize(result))
if status <> "passed" then exit 1
