using System.Diagnostics;
using System.Text.Json;
namespace SharpTestBench.Tools;
internal sealed class OracleException(string kind, string message) : Exception(message) { public string Kind { get; } = kind; }
internal sealed class PythonInterop : IDisposable
{
    // Object handles keep upstream Python values in their own runtime; no managed implementation supplies oracle results.
    const string Bridge = """
import builtins,importlib,json,sys
objects={};serial=0
def decode(value):
 if isinstance(value,dict) and set(value)=={'@'}:return objects[value['@']]
 if isinstance(value,list):return [decode(v) for v in value]
 if isinstance(value,dict):return {k:decode(v) for k,v in value.items()}
 return value
def encode(value):
 global serial
 if value is None or isinstance(value,(bool,int,float,str)):return value
 if isinstance(value,(list,tuple)):return [encode(v) for v in value]
 if isinstance(value,dict):return {str(k):encode(v) for k,v in value.items()}
 serial+=1;objects[serial]=value;return {'@':serial}
for line in sys.stdin:
 try:
  request=json.loads(line);op=request['op'];args=decode(request.get('args',[]));kwargs=decode(request.get('kwargs',{}))
  if op=='call':
   target=importlib.import_module(request['module']) if request['module']!='builtins' else builtins
   for part in request['name'].split('.'):target=getattr(target,part)
   result=target(*args,**kwargs)
  elif op=='method':result=getattr(decode(request['target']),request['name'])(*args,**kwargs)
  elif op=='attr':result=getattr(decode(request['target']),request['name'])
  elif op=='setattr':result=setattr(decode(request['target']),request['name'],args[0])
  elif op=='getitem':result=decode(request['target'])[args[0]]
  elif op=='setitem':decode(request['target'])[args[0]]=args[1];result=None
  else:raise ValueError('Unknown interop operation')
  response={'ok':True,'result':encode(result)}
 except Exception as error:response={'ok':False,'kind':type(error).__name__,'message':str(error)}
 print(json.dumps(response,separators=(',',':')),flush=True)
""";
    readonly Process process; readonly Task<string> error;
    public PythonInterop(string? python = null)
    {
        var settings = new ProcessStartInfo(python ?? Environment.GetEnvironmentVariable("CS_PYTHON") ?? "python") { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        settings.ArgumentList.Add("-u"); settings.ArgumentList.Add("-c"); settings.ArgumentList.Add(Bridge);
        process = Process.Start(settings) ?? throw new IOException("Unable to start upstream Python interop"); error = process.StandardError.ReadToEndAsync();
    }
    JsonElement Request(object request)
    {
        process.StandardInput.WriteLine(JsonSerializer.Serialize(request)); process.StandardInput.Flush();
        var line = process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult() ?? throw new EndOfStreamException("Python oracle interop exited");
        using var response = StrictJson.Parse(line); var root = response.RootElement;
        if (!root.GetProperty("ok").GetBoolean()) throw new OracleException(root.GetProperty("kind").GetString()!, root.GetProperty("message").GetString()!);
        return root.GetProperty("result").Clone();
    }
    public JsonElement Call(string module, string name, object?[]? args = null, Dictionary<string, object?>? kwargs = null) => Request(new { op = "call", module, name, args = args ?? [], kwargs = kwargs ?? [] });
    public JsonElement Method(JsonElement target, string name, object?[]? args = null, Dictionary<string, object?>? kwargs = null) => Request(new { op = "method", target, name, args = args ?? [], kwargs = kwargs ?? [] });
    public JsonElement Attribute(JsonElement target, string name) => Request(new { op = "attr", target, name });
    public void SetAttribute(JsonElement target, string name, object value) => Request(new { op = "setattr", target, name, args = new[] { value } });
    public JsonElement Item(JsonElement target, object key) => Request(new { op = "getitem", target, args = new[] { key } });
    public void SetItem(JsonElement target, object key, object value) => Request(new { op = "setitem", target, args = new[] { key, value } });
    public string String(JsonElement target) => Call("builtins", "str", [target]).GetString()!;
    public JsonElement List(JsonElement target) => Call("builtins", "list", [target]);
    public void Dispose()
    {
        process.StandardInput.Close(); if (!process.WaitForExit(5000)) { process.Kill(true); process.WaitForExit(5000); }
        var diagnostic = error.GetAwaiter().GetResult(); var code = process.ExitCode; process.Dispose(); if (code != 0) throw new IOException("Upstream Python interop failed: " + diagnostic);
    }
}
internal static class OracleFixturesCommand
{
    static void Hashes(ToolContext context, PythonInterop api, params string[] modules)
    {
        using var review = StrictJson.Read(context.At("docs/API_REVIEWS.json"));
        foreach (var name in modules)
        {
            var module = api.Call("importlib", "import_module", [name]); var file = api.Attribute(module, "__file__").GetString()!;
            if (CiCommands.Hash(file) != review.RootElement.GetProperty("source_hashes").GetProperty(name).GetString()) throw new InvalidDataException("Installed " + name + " differs from reviewed pinned source");
        }
    }
    public static void Execute(ToolContext context, string command, string[] arguments)
    {
        var options = new Options(arguments, "--check"); options.Only(command == "queue-reference" ? ["--label", "--build"] : ["--check"]);
        using var api = new PythonInterop();
        if (command == "queue-reference") { Queue(context, api, options); return; }
        object result;
        if (command == "array-reference") { Hashes(context, api, "cocotb.types._range", "cocotb.types._abstract_array", "cocotb.types._array"); result = Arrays(api); }
        else { Hashes(context, api, "cocotb.types._logic", "cocotb.types._logic_array", "cocotb.types._resolve"); result = Logic(api); }
        var path = context.At("tests/SharpTestBench.Tests/" + (command == "array-reference" ? "array" : "logic") + "-reference.json");
        if (options.Has("--check")) { using var existing = StrictJson.Read(path); if (!JsonElement.DeepEquals(existing.RootElement, JsonSerializer.SerializeToElement(result))) throw new InvalidDataException("Pinned reference fixture drift; regenerate and review"); }
        else StrictJson.Write(path, result);
        Console.WriteLine(command.ToUpperInvariant().Replace('-', '_') + "_PASS pinned upstream oracle");
    }
    static List<object> Arrays(PythonInterop api)
    {
        var cases = new List<object>();
        for (int left = -3; left <= 3; left++) for (int right = -3; right <= 3; right++) foreach (bool ascending in new[] { true, false })
        {
            var span = api.Call("cocotb.types", "Range", [left, ascending ? "to" : "downto", right]); var indices = api.List(span); var count = indices.GetArrayLength(); var values = Enumerable.Range(0, count).Select(index => index % 3).ToArray();
            var array = api.Call("cocotb.types", "Array", [values], new() { ["range"] = span }); var found = new List<object?>();
            for (int value = 0; value < 4; value++) { try { found.Add(api.Method(array, "index", [value])); } catch (OracleException error) when (error.Kind == "ValueError") { found.Add(null); } }
            var counts = Enumerable.Range(0, 4).Select(value => api.Method(array, "count", [value])).ToArray();
            if (count >= 2) api.SetItem(array, api.Call("builtins", "slice", [indices[0], indices[1]]), new[] { 7, 8 });
            var after = api.List(array); api.SetAttribute(array, "range", api.Call("cocotb.types", "Range", [10, "to", 10 + count - 1]));
            var reindexed = api.List(api.Attribute(array, "range")).EnumerateArray().Select(index => api.Item(array, index)).ToArray();
            cases.Add(new { left, right, ascending, indices, values, found, counts, after_slice = after, reindexed });
        }
        return cases;
    }
    static void Queue(ToolContext context, PythonInterop api, Options options)
    {
        var source = context.At("build/upstream/cocotb-source/src/cocotb/queue.py"); using var review = StrictJson.Read(context.At("docs/API_REVIEWS.json"));
        if (CiCommands.Hash(source) != review.RootElement.GetProperty("source_hashes").GetProperty("cocotb.queue").GetString()) throw new InvalidDataException("Pinned queue source hash mismatch");
        // Import the exact reviewed source, separately from the installed package.
        var spec = api.Call("importlib.util", "spec_from_file_location", ["queue_reference_upstream", source]); var module = api.Call("importlib.util", "module_from_spec", [spec]); api.Method(api.Attribute(spec, "loader"), "exec_module", [module]);
        var expected = new Dictionary<string, List<int[]>>();
        foreach (var (name, type) in new[] { ("fifo", "Queue"), ("lifo", "LifoQueue"), ("priority", "PriorityQueue"), ("reverse", "PriorityQueue") })
        {
            // Calling a module member is a direct upstream API invocation.
            var queue = api.Method(module, type, kwargs: new() { ["maxsize"] = 4 }); var rows = expected[name] = [];
            int Size() => api.Method(queue, "qsize").GetInt32(); int Flag(string method) => api.Method(queue, method).GetBoolean() ? 1 : 0;
            foreach (var value in new[] { 4, -2, 4, 1 }) { api.Method(queue, "put_nowait", [name == "reverse" ? -value : value]); rows.Add([value, Size(), Flag("empty"), Flag("full")]); }
            while (Flag("empty") == 0) { var value = api.Method(queue, "get_nowait").GetInt32(); rows.Add([name == "reverse" ? -value : value, Size(), Flag("empty"), Flag("full")]); }
        }
        var build = Path.GetFullPath(options.Get("--build", context.At("build"))!); using var actual = StrictJson.Read(Path.Combine(build, options.Require("--label") + "-trace.txt.queues.json"));
        if (!JsonElement.DeepEquals(actual.RootElement, JsonSerializer.SerializeToElement(expected))) throw new InvalidDataException("Queue ordering/occupancy mismatch"); Console.WriteLine("QUEUE_REFERENCE_PASS 32 upstream ordering/occupancy rows");
    }
    static object Logic(PythonInterop api)
    {
        const string states = "UX01ZWLH-"; string[] policies = ["weak", "zeros", "ones"];
        var resolve = api.Call("importlib", "import_module", ["cocotb.types._resolve"]);
        var preview = api.Call("importlib", "import_module", ["cocotb.preview"]);
        var strictFeature = api.Attribute(api.Attribute(preview, "Feature"), "STRICT_RESOLVE");
        if (api.Attribute(resolve, "RESOLVE_X").ValueKind != JsonValueKind.Null || api.Call("cocotb.preview", "is_enabled", [strictFeature]).GetBoolean()) throw new InvalidDataException("Oracle requires default resolution and STRICT_RESOLVE disabled");
        Dictionary<string, string> Resolved(JsonElement value) => policies.ToDictionary(policy => policy, policy => api.String(api.Method(value, "resolve", [policy])));
        var scalar = states.Select(symbol => { var value = api.Call("cocotb.types", "Logic", [symbol.ToString()]); return new { symbol = symbol.ToString(), resolvable = api.Attribute(value, "is_resolvable"), resolved = Resolved(value) }; }).ToArray();
        var vectors = new List<Dictionary<string, object?>>();
        foreach (var text in states.SelectMany(a => states.Select(b => "" + a + b)).Concat(new[] { states, "", "0000000000000001", "100000000", "LHHL" }))
        {
            var value = api.Call("cocotb.types", "LogicArray", [text]); var row = new Dictionary<string, object?> { ["bits"] = text, ["resolvable"] = api.Attribute(value, "is_resolvable"), ["resolved"] = Resolved(value) };
            try
            {
                row["unsigned"] = api.String(api.Method(value, "to_unsigned")); row["signed"] = api.String(api.Method(value, "to_signed"));
                try { foreach (var order in new[] { "big", "little" }) row[order] = api.List(api.Method(value, "to_bytes", kwargs: new() { ["byteorder"] = order })); }
                catch (OracleException error) when (error.Kind == "OverflowError") { row["bytes_error"] = "OverflowError"; }
            }
            catch (OracleException error) when (error.Kind == "ValueError") { row["unsigned"] = null; }
            vectors.Add(row);
        }
        var integers = new List<Dictionary<string, object?>>();
        foreach (int width in new[] { 1, 2, 7, 8, 9, 16, 65, 128 })
        {
            var limit = System.Numerics.BigInteger.One << (width - 1);
            foreach (var number in new[] { -limit - 1, -limit, -1, 0, 1, limit - 1, limit, (System.Numerics.BigInteger.One << width) + 3 }) foreach (bool signed in new[] { false, true }) foreach (bool wrap in new[] { false, true })
            {
                var row = new Dictionary<string, object?> { ["width"] = width, ["value"] = number.ToString(System.Globalization.CultureInfo.InvariantCulture), ["signed"] = signed, ["wrap"] = wrap };
                try
                {
                    var value = api.Call("cocotb.types", signed ? "LogicArray.from_signed" : "LogicArray.from_unsigned", [api.Call("builtins", "int", [row["value"]]), api.Call("cocotb.types", "Range", [-3, "to", width - 4])], new() { ["on_overflow"] = wrap ? "wrap" : "error" });
                    row["bits"] = api.String(value); row["unsigned"] = api.String(api.Method(value, "to_unsigned")); row["signed_value"] = api.String(api.Method(value, "to_signed")); foreach (var order in new[] { "big", "little" }) row[order] = api.List(api.Method(value, "to_bytes", kwargs: new() { ["byteorder"] = order }));
                }
                catch (OracleException error) when (error.Kind == "ValueError") { row["bits"] = null; }
                integers.Add(row);
            }
        }
        var transforms = new List<object>();
        foreach (var a in states) foreach (var b in states)
        {
            var left = api.Call("cocotb.types", "LogicArray", [a + states, api.Call("cocotb.types", "Range", [-4, "to", 5])]); var right = api.Call("cocotb.types", "LogicArray", [b + new string(states.Reverse().ToArray()), api.Call("cocotb.types", "Range", [20, "downto", 11])]);
            var and = api.Call("operator", "and_", [left, right]); var range = api.Attribute(and, "range");
            transforms.Add(new { left = api.String(left), right = api.String(right), and_bits = api.String(and), or_bits = api.String(api.Call("operator", "or_", [left, right])), xor_bits = api.String(api.Call("operator", "xor", [left, right])), not_bits = api.String(api.Call("operator", "invert", [left])), result_bounds = new[] { api.Attribute(range, "left"), api.Attribute(range, "right") } });
        }
        var collections = new List<object>(); var collectionText = states + "0L1HX";
        foreach (var span in new[] { api.Call("cocotb.types", "Range", [-3, "to", collectionText.Length - 4]), api.Call("cocotb.types", "Range", [8, "downto", 9 - collectionText.Length]) })
        {
            var value = api.Call("cocotb.types", "LogicArray", [collectionText, span]); var indices = api.List(span); var slices = new List<object>();
            foreach (var (start, stop, replacement) in new[] { (0, 2, "10X"), (3, 5, "ZLH"), (collectionText.Length - 1, collectionText.Length - 1, "-") })
            {
                var changed = api.Call("cocotb.types", "LogicArray", [collectionText, span]); var slice = api.Call("builtins", "slice", [indices[start], indices[stop]]); api.SetItem(changed, slice, api.Call("cocotb.types", "LogicArray", [replacement])); slices.Add(new { left = indices[start], right = indices[stop], replacement, extracted = api.String(api.Item(value, slice)), result = api.String(changed) });
            }
            collections.Add(new { bits = collectionText, left = api.Attribute(span, "left"), right = api.Attribute(span, "right"), ascending = api.Attribute(span, "direction").GetString() == "to", found = states.Select(symbol => api.Method(value, "index", [api.Call("cocotb.types", "Logic", [symbol.ToString()])])).ToArray(), counts = states.Select(symbol => api.Method(value, "count", [api.Call("cocotb.types", "Logic", [symbol.ToString()])])).ToArray(), reversed = string.Concat(api.List(api.Call("builtins", "reversed", [value])).EnumerateArray().Select(api.String)), slices });
        }
        return new { scalar, vectors, integers, transforms, collections };
    }
}
