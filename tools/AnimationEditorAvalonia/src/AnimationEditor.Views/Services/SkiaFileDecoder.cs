using System;
using System.IO;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Paths;
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
    /// <summary>The decoded bitmap, or null when the file is missing, unreadable, not an image or too large.</summary>
    public static SKBitmap? DecodeFile(string path) => DecodeFile(path, out _);

    /// <summary>
    /// Like <see cref="DecodeFile(string)"/>, and also explains a failure the user can act on:
    /// <paramref name="error"/> is set for an image too large to decode (see <see cref="ImageSizeLimit"/>),
    /// and null for a missing or unreadable file or a non-image, which callers report their own way.
    /// Never throws for an oversized image; Skia throws "Unable to allocate pixels" there.
    /// </summary>
    public static SKBitmap? DecodeFile(string path, out string? error)
    {
        error = null;
        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }

        // SKBitmap.Decode(byte[]) throws when no codec accepts the bytes (a zero-byte or
        // truncated file); callers expect null there, the way the path overload behaved.
        using SKData data = SKData.CreateCopy(bytes);
        using SKCodec? codec = SKCodec.Create(data);
        if (codec == null) return null;

        // Check the header's size before decoding: the decode is what throws for a huge image.
        SKImageInfo info = codec.Info;
        if (ImageSizeLimit.Exceeds(info.Width, info.Height))
        {
            error = ImageSizeLimit.Describe(new FilePath(path).NoPath, info.Width, info.Height);
            return null;
        }

        try { return SKBitmap.Decode(codec); }
        catch (Exception ex)
        {
            // Under the size limit but still not allocatable (the machine is out of memory).
            error = $"Could not decode {new FilePath(path).NoPath}: {ex.Message}";
            return null;
        }
    }
}
