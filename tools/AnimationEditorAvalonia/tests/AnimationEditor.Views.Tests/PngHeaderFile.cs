using System.Buffers.Binary;

namespace AnimationEditor.Views.Tests;

/// <summary>
/// Writes a PNG that declares a huge size in its IHDR but carries no real pixel data, so a test
/// can hand the editor an "oversized image" without allocating the gigabytes a real one needs.
/// The codec reads the size from the header; decoding it is what would fail.
/// </summary>
internal static class PngHeaderFile
{
    public static string Write(string name, int width, int height)
    {
        string path = Path.Combine(Path.GetTempPath(), "AnimationEditorViewsTests", Guid.NewGuid().ToString("N"), name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var ms = new MemoryStream();
        ms.Write(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A });

        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr[0..4], width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr[4..8], height);
        ihdr[8] = 8;    // bit depth
        ihdr[9] = 6;    // RGBA
        WriteChunk(ms, "IHDR", ihdr);
        WriteChunk(ms, "IDAT", new byte[] { 0x78, 0x01, 0x00 });
        WriteChunk(ms, "IEND", Array.Empty<byte>());

        File.WriteAllBytes(path, ms.ToArray());
        return path;
    }

    public static void Delete(string path) => Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);

    private static void WriteChunk(Stream s, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
        s.Write(len);

        byte[] body = new byte[4 + data.Length];
        for (int i = 0; i < 4; i++) body[i] = (byte)type[i];
        data.CopyTo(body.AsSpan(4));
        s.Write(body);

        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(body));
        s.Write(crc);
    }

    private static uint Crc32(byte[] bytes)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in bytes)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        return ~crc;
    }
}
