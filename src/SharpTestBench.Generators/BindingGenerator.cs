using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace SharpTestBench.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class BindingGenerator : IIncrementalGenerator
{
    static readonly DiagnosticDescriptor Invalid = new("STBGEN001", "Invalid HDL binding manifest",
        "{0}", "SharpTestBench", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var files = context.AdditionalTextsProvider.Where(file => file.Path.EndsWith(".stb.xml", StringComparison.OrdinalIgnoreCase))
            .Select((file, token) => (Path: file.Path, Text: file.GetText(token)?.ToString() ?? ""))
            .WithTrackingName("BindingManifest");
        context.RegisterSourceOutput(files, (output, file) => Generate(output, file.Path, file.Text));
    }

    static bool Identifier(string name) => SyntaxFacts.IsValidIdentifier(name) && SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None;
    static string Required(XElement element, string name) => (string?)element.Attribute(name) is string value && value.Length > 0
        ? value : throw new FormatException($"{element.Name}: missing {name}");
    static void Attributes(XElement element, params string[] allowed)
    {
        if (element.Attributes().Any(a => !allowed.Contains(a.Name.ToString())))
            throw new FormatException($"{element.Name}: unknown attribute");
    }
    static string Literal(string value) => SymbolDisplay.FormatLiteral(value, true);

    static void Generate(SourceProductionContext context, string path, string text)
    {
        try
        {
            if (text.Length > 1024 * 1024) throw new FormatException("Manifest exceeds 1 MiB");
            using var reader = XmlReader.Create(new System.IO.StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            var root = XDocument.Load(reader).Root ?? throw new FormatException("Missing Bindings element");
            if (root.Name != "Bindings") throw new FormatException("Root must be Bindings");
            Attributes(root, "namespace", "class");
            var ns = Required(root, "namespace"); var name = Required(root, "class");
            if (!ns.Split('.').All(Identifier) || !Identifier(name) || name.StartsWith("Width", StringComparison.Ordinal)) throw new FormatException("Invalid namespace or class identifier");
            var members = new List<(string Name, int Width, string Expression)>();
            var names = new HashSet<string>(StringComparer.Ordinal) { name };
            foreach (var signal in root.Elements())
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (signal.Name != "Signal") throw new FormatException("Only Signal entries are supported");
                Attributes(signal, "name", "width", "direction");
                var direction = (string?)signal.Attribute("direction");
                if (direction != null && direction != "Input" && direction != "Output" && direction != "InOut")
                    throw new FormatException("Direction must be Input, Output or InOut");
                var member = Required(signal, "name");
                if (!Identifier(member) || !names.Add(member) || member.StartsWith("Width", StringComparison.Ordinal))
                    throw new FormatException("Invalid, duplicate or reserved member name: " + member);
                if (!int.TryParse(Required(signal, "width"), NumberStyles.None, CultureInfo.InvariantCulture, out int width) || width < 1 || width > 1048576)
                    throw new FormatException("Width must be an integer from 1 through 1048576");
                var expression = "root";
                int depth = 0;
                foreach (var step in signal.Elements())
                {
                    if (++depth > 64 || step.HasElements) throw new FormatException("Invalid hierarchy step or depth exceeds 64");
                    if (step.Name == "Child")
                    {
                        Attributes(step, "name");
                        expression += ".Child(" + Literal(Required(step, "name")) + ")";
                    }
                    else if (step.Name == "Index")
                    {
                        Attributes(step, "value");
                        if (!int.TryParse(Required(step, "value"), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int index))
                            throw new FormatException("Index must fit Int32");
                        expression += ".At(" + index.ToString(CultureInfo.InvariantCulture) + ")";
                    }
                    else throw new FormatException("Hierarchy steps must be Child or Index");
                }
                if (depth == 0) throw new FormatException("Signal requires an explicit hierarchy path");
                if (direction != null) expression += ".RequireDirection(global::SharpTestBench.PortDirection." + direction + ")";
                members.Add((member, width, expression));
                if (members.Count > 10000) throw new FormatException("Too many signals");
            }
            var source = new StringBuilder("// <auto-generated/>\n#nullable enable\nnamespace " + ns + " {\npublic sealed class " + name + " {\n");
            foreach (int width in members.Select(m => m.Width).Distinct().OrderBy(w => w))
                source.Append("public readonly struct Width").Append(width).Append(" : global::SharpTestBench.IWidth { public static int Bits => ").Append(width).Append("; }\n");
            foreach (var member in members.OrderBy(m => m.Name, StringComparer.Ordinal))
                source.Append("public global::SharpTestBench.Signal<Width").Append(member.Width).Append("> ").Append(member.Name).Append(" { get; }\n");
            source.Append("public ").Append(name).Append("(global::SharpTestBench.HdlObject root) {\n");
            foreach (var member in members.OrderBy(m => m.Name, StringComparer.Ordinal))
                source.Append("this.").Append(member.Name).Append(" = ").Append(member.Expression).Append(".Signal<Width").Append(member.Width).Append(">();\n");
            source.Append("}\n}\n}\n");
            // Namespace/class identifies the output. Duplicate manifest declarations are
            // invalid, including across files, and get the compiler's duplicate-source diagnostic.
            context.AddSource(ns + "." + name + ".g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
        }
        catch (Exception error) when (error is FormatException || error is XmlException || error is ArgumentException)
        {
            context.ReportDiagnostic(Diagnostic.Create(Invalid,
                Location.Create(path, new TextSpan(0, 0), new LinePositionSpan(new LinePosition(0, 0), new LinePosition(0, 0))), error.Message));
        }
    }
}
