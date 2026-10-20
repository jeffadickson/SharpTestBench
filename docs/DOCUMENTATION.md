# API documentation

C++ uses Doxygen XML rendered by **Sphinx + Breathe**. C# uses standard **XML
 documentation comments** (`///`, sometimes called XMLdoc or xdoc); there is no
additional C# documentation runtime dependency.

## Build

Install Doxygen (Ubuntu: `apt-get install doxygen`, macOS: `brew install doxygen`,
Windows: install Doxygen and put its executable on PATH), Python 3.12 and .NET 10.
From the repository root:

```sh
python -m pip install -r docs/api/requirements.txt
dotnet run --project tools/SharpTestBench.Tools -c Release -- documentation
```

Open `build/docs/html/index.html`. Use `--dotnet /path/to/dotnet` when necessary.
`--managed-only` checks XML comments without Doxygen/Sphinx. This build does not
load a simulator. CI publishes the HTML, Doxygen XML, managed XML and build logs
as the `api-documentation` artifact; it does not deploy a public website.

## Authoring

Document project-owned native declarations and helpers with Doxygen comments.
Describe buffer sizes, ownership, thread restrictions, status codes and callback
lifetime rules. The reference includes static helpers, but excludes vendored
upstream headers. Doxygen and Sphinx warnings fail the documentation job.

Use C# `<summary>`, `<param>`, `<typeparam>`, `<returns>`, `<exception>` and
`<remarks>` where relevant. Describe simulator phase restrictions, cancellation,
X/Z handling and unavailable backend behavior rather than restating a member's
name. Use `<inheritdoc/>` only when the inherited contract actually applies.
All repository C# projects emit XML beside their assemblies. Library XML ships
with runtime and generator NuGet packages for editor IntelliSense.

The initial public library comment backlog is listed in
`api/csharp-undocumented.txt`. CS1591 stays visible as a warning; other compiler
warnings retain each project's normal policy. The documentation script rejects
new undocumented runtime/generator members. Remove resolved entries from the
baseline while adding meaningful comments. Tests and examples also emit XML but
are not part of the public API backlog gate. This initial integration does not
claim that all existing C# members have complete prose documentation.

References: [Breathe](https://breathe.readthedocs.io/en/stable/),
[C# XML comments](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/).
