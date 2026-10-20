using System.Text.Json;

namespace SharpTestBench.Tools;

internal static class PrepareUpstreamCommand
{
    static string ReplaceOnce(string text, string before, string after)
    {
        if (text.Split(before, StringSplitOptions.None).Length != 2) throw new InvalidDataException("Pinned upstream packaging layout changed: " + before);
        return text.Replace(before, after, StringComparison.Ordinal);
    }
    public static void Execute(ToolContext context, string[] arguments)
    {
        if (arguments.Length != 0) throw new ArgumentException("prepare-upstream accepts no arguments");
        using var metadata = StrictJson.Read(context.At("third_party/UPSTREAM.json"));
        var info = metadata.RootElement;
        var archive = context.At("third_party/" + info.GetProperty("source_archive").GetString());
        if (CiCommands.Hash(archive) != info.GetProperty("source_sha256").GetString()) throw new InvalidDataException("Upstream archive checksum differs from pinned provenance");
        var destination = context.At("build/upstream");
        CiCommands.Extract(archive, destination);
        var source = Path.Combine(destination, "cocotb-source");
        var project = Path.Combine(source, "pyproject.toml");
        var text = File.ReadAllText(project).Replace("\r\n", "\n", StringComparison.Ordinal);
        text = ReplaceOnce(text, "dynamic = [\"version\"]", "version = " + JsonSerializer.Serialize(info.GetProperty("version").GetString()));
        text = ReplaceOnce(text, "[tool.scikit-build.metadata.version]\nprovider = \"setuptools_git_versioning.scikit_metadata\"", "");
        text = ReplaceOnce(text, "[tool.scikit-build]", "[tool.scikit-build]\ncmake.build-type = \"Release\"\ninstall.strip = false");
        File.WriteAllText(project, text);
        StrictJson.Write(Path.Combine(destination, "native-profile.json"), ConfigureNativeBuild(source));
        Console.WriteLine(source);
    }
    public static object ConfigureNativeBuild(string source)
    {
        var cmake = Path.Combine(source, "CMakeLists.txt");
        var original = File.ReadAllText(cmake).Replace("\r\n", "\n", StringComparison.Ordinal);
        var text = original;
        text = ReplaceOnce(text, "set(CMAKE_WINDOWS_EXPORT_ALL_SYMBOLS ON)", "set(CMAKE_WINDOWS_EXPORT_ALL_SYMBOLS OFF)");
        text = ReplaceOnce(text, "set(CMAKE_CXX_STANDARD 11)", "set(CMAKE_CXX_STANDARD 20)");
        text = ReplaceOnce(text, "/machine:$<IF:$<EQUAL:${CMAKE_SIZEOF_VOID_P},8>,X64,X86>", "/machine:${STB_IMPORT_MACHINE}");
        text = ReplaceOnce(text, "\"-std=c++11\"", "\"-std=c++20\"");
        text = ReplaceOnce(text, "\"/MD /O2 /Ob2 /DNDEBUG\"", "\"/MD /O2 /Ob2 /Zi /Zo /DNDEBUG\"");
        text = ReplaceOnce(text, "set(CMAKE_CXX_STANDARD_REQUIRED ON)", "set(CMAKE_CXX_STANDARD_REQUIRED ON)\n" + "set(CMAKE_CXX_EXTENSIONS OFF)\nif(CMAKE_GENERATOR_PLATFORM MATCHES \"^[Aa][Rr][Mm]64$\" OR\n   CMAKE_SYSTEM_PROCESSOR MATCHES \"^([Aa][Rr][Mm]64|[Aa][Aa][Rr][Cc][Hh]64)$\" OR\n   CMAKE_CXX_COMPILER_ARCHITECTURE_ID STREQUAL \"ARM64\")\n    set(STB_IMPORT_MACHINE ARM64)\nelseif(CMAKE_SIZEOF_VOID_P EQUAL 8)\n    set(STB_IMPORT_MACHINE X64)\nelse()\n    set(STB_IMPORT_MACHINE X86)\nendif()\n# SharpTestBench release profile: require IPO, retain debugging, preserve real semantics.\nset(CMAKE_BUILD_TYPE Release CACHE STRING \"SharpTestBench native configuration\" FORCE)\ninclude(CheckIPOSupported)\ncheck_ipo_supported(RESULT STB_IPO_SUPPORTED OUTPUT STB_IPO_ERROR LANGUAGES CXX)\nif(NOT STB_IPO_SUPPORTED)\n    message(FATAL_ERROR \"SharpTestBench requires native IPO: ${STB_IPO_ERROR}\")\nendif()\nset(CMAKE_INTERPROCEDURAL_OPTIMIZATION_RELEASE ON)\nif(CMAKE_CXX_COMPILER_ID MATCHES \"Clang\")\n    set(STB_VECTOR_FLAGS \"-fvectorize -fslp-vectorize\")\nelseif(CMAKE_CXX_COMPILER_ID STREQUAL \"GNU\")\n    set(STB_VECTOR_FLAGS \"-ftree-loop-vectorize -ftree-slp-vectorize\")\nendif()\nif(NOT MSVC)\n    set(CMAKE_CXX_FLAGS_RELEASE \"-O3 -g -DNDEBUG ${STB_VECTOR_FLAGS}\" CACHE STRING \"Native release flags\" FORCE)\nelse()\n    # MSVC /O2 enables its auto-vectorizer; preserve portable default ISA.\n    string(APPEND CMAKE_CXX_FLAGS_RELEASE \" /Qvec-report:1\")\n    string(APPEND CMAKE_SHARED_LINKER_FLAGS_RELEASE \" /DEBUG:FULL /OPT:REF /OPT:ICF\")\nendif()\nset(CMAKE_EXPORT_COMPILE_COMMANDS ON)\n");
        File.WriteAllText(cmake, text);
        static string HashText(string value) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
        return new { language = "C++20", configuration = "Release", ipo_required = true, original_cmake_sha256 = HashText(original), configured_cmake_sha256 = CiCommands.Hash(cmake), implementation_sources_modified = false };
    }

}
