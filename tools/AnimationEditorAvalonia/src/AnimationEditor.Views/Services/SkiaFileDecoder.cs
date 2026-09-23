using System;
using System.IO;
using SkiaSharp;

namespace AnimationEditor.Views.Services;

/// <summary>
/// Decodes an image file through .NET's file API instead of <see cref="SKBitmap.Decode(string)"/>:
/// Skia opens a path natively and quietly returns null for a Windows path past the classic
/// 260-character limit, which .NET reads without complaint. Every file decode in the editor goes
/// through here so a deeply nested project loads the same as a shallow one.
/// </summary>
public static class SkiaFileDecoder
{
    /// <summary>The decoded bitmap, or null when the file is missing, unreadable or not an image.</summary>
    public static SKBitmap? DecodeFile(string path)
    {
        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }

        // SKBitmap.Decode(byte[]) throws when no codec accepts the bytes (a zero-byte or
        // truncated file); callers expect null there, the way the path overload behaved.
        using SKData data = SKData.CreateCopy(bytes);
        using SKCodec? codec = SKCodec.Create(data);
        return codec == null ? null : SKBitmap.Decode(codec);
    }
}
