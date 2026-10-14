using System.Text;
using System.Text.Json;

namespace SharpTestBench;
/// <summary>Generates typed bindings from recursively elaborated scopes and arrays.</summary>
public static class Bindings
{
    public static string Generate(HdlObject root, string className = "DutBindings", int maxDepth = 32, int maxObjects = 10000, string namespaceName = "Generated")
    {
        ArgumentNullException.ThrowIfNull(root);
        if (string.IsNullOrEmpty(namespaceName) || !namespaceName.Split('.').All(Valid))
            throw new ArgumentException("Invalid namespace");
        if (!Valid(className))
            throw new ArgumentException("Invalid C# class name");
        if (maxDepth < 1 || maxObjects < 1)
            throw new ArgumentOutOfRangeException();
        var widths = new SortedSet<int>();
        var classes = new List<string>();
        var visiting = new HashSet<nint>();
        int sequence = 0, count = 0;
        (string Type, string Expression) Describe(HdlObject obj, string expression, int depth, string? scopeName = null)
        {
            if (depth > maxDepth || ++count > maxObjects)
                throw new InvalidOperationException("Binding generation limit exceeded");
            switch (obj.Kind)
            {
                case ObjectKind.Logic:
                case ObjectKind.LogicArray:
                case ObjectKind.Packed:
                    int width = obj.Signal().Width;
                    // Null vectors have no positive compile-time IWidth representation.
                    if (width == 0)
                        return ("Signal", expression + ".Signal()");
                    widths.Add(width);
                    if (obj.DeclaredSignedness is not bool signed)
                        return ($"Signal<Width{width}>", $"{expression}.Signal<Width{width}>()");
                    string sign = signed ? "Signed" : "Unsigned";
                    return ($"{sign}Signal<Width{width}>", $"{expression}.{sign}<Width{width}>()");
                case ObjectKind.Integer:
                case ObjectKind.Enum:
                    return ("IntegerSignal", expression + ".Integer()");
                case ObjectKind.Real:
                    return ("RealSignal", expression + ".Real()");
                case ObjectKind.String:
                case ObjectKind.FixedString:
                    return ("StringSignal", expression + ".String()");
                case ObjectKind.Array:
                case ObjectKind.Memory:
                    var range = obj.Range;
                    if (range.IsNull)
                        return ("HdlArray<HdlObject>", expression + ".Array(element => element)");
                    string variable = "element" + depth;
                    var element = Describe(obj.At(range.Left), variable, depth + 1);
                    return ($"HdlArray<{element.Type}>", $"{expression}.Array({variable} => {element.Expression})");
                case ObjectKind.GenerateArray:
                    // Generate instances may have different field names/widths per index.
                    return ("HdlArray<HdlObject>", expression + ".Array(element => element)");
                case ObjectKind.Module:
                case ObjectKind.Structure:
                case ObjectKind.Package:
                    if (!visiting.Add(obj.Handle))
                        throw new InvalidOperationException("Cyclic HDL hierarchy");
                    string name = scopeName ?? className + "_Scope" + sequence++;
                    var used = new HashSet<string>(StringComparer.Ordinal)
                    {
                        name
                    };
                    var members = new List<(string Name, string Type, string Expression)>();
                    foreach (var child in obj.Children().DistinctBy(x => x.Name).OrderBy(x => x.Name, StringComparer.Ordinal))
                    {
                        string property = Safe(child.Name);
                        if (!used.Add(property))
                            throw new InvalidOperationException($"Sanitized HDL name collision: {child.Name}");
                        var binding = Describe(child, "root.Child(" + JsonSerializer.Serialize(child.Name) + ")", depth + 1);
                        members.Add((property, binding.Type, binding.Expression));
                    }

                    visiting.Remove(obj.Handle);
                    var source = new StringBuilder($"public sealed class @{name} {{\n");
                    foreach (var member in members)
                        source.AppendLine($" public {member.Type} @{member.Name} {{get;}}");
                    source.AppendLine($" public @{name}(HdlObject root) {{");
                    foreach (var member in members)
                        source.AppendLine($"  @{member.Name} = {member.Expression};");
                    source.AppendLine(" }\n}");
                    classes.Add(source.ToString());
                    return ("@" + name, $"new @{name}({expression})");
                default:
                    return ("HdlObject", expression);
            }
        }

        if (root.Kind is not (ObjectKind.Module or ObjectKind.Structure or ObjectKind.Package))
            throw new ArgumentException("Binding root must be a scope");
        Describe(root, "root", 0, className);
        if (widths.Any(w => className == "Width" + w))
            throw new ArgumentException("Class name conflicts with generated width type");
        var output = new StringBuilder("// Generated from elaborated HDL. Constructors check widths and signs.\nusing SharpTestBench;\nnamespace " + string.Join(".", namespaceName.Split('.').Select(s => "@" + s)) + ";\n");
        foreach (int width in widths)
            output.AppendLine($"public readonly struct Width{width}:IWidth {{ public static int Bits => {width}; }}");
        foreach (var source in classes)
            output.Append(source);
        return output.ToString();
    }

    static bool Valid(string s) => s.Length > 0 && (char.IsLetter(s[0]) || s[0] == '_') && s.All(c => char.IsLetterOrDigit(c) || c == '_');
    static string Safe(string s)
    {
        var result = new string (s.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
        return result.Length == 0 ? "signal" : char.IsDigit(result[0]) ? "_" + result : result;
    }
}
