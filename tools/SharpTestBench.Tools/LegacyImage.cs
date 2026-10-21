using System.Security.Cryptography;

namespace SharpTestBench.Tools;

internal sealed record LegacyImage(string Role, string Path, long Bytes, string Sha256)
{
    internal static LegacyImage Capture(string path)
    {
        path=System.IO.Path.GetFullPath(path);
        using var stream=File.OpenRead(path);var length=stream.Length;
        if (length==0) throw new IOException("Icarus compiler did not produce a nonempty output image.");
        var hash=Convert.ToHexString(SHA256.HashData(stream));
        if (stream.Length!=length) throw new InvalidDataException("Legacy compiled image changed while hashing.");
        return new("compiled-image",path,length,hash);
    }
    internal void Verify()
    {
        if (this!=Capture(Path)) throw new InvalidDataException("Legacy compiled image changed during simulation.");
    }
}
