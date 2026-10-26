using System;

namespace AnimationEditor.Core.IO;

/// <summary>
/// The largest image the editor can open. Skia decodes to RGBA and sizes the pixel buffer with an
/// <c>int</c>, so an image whose <c>width * height * 4</c> passes <see cref="int.MaxValue"/> (about
/// 23170 x 23170) fails to decode. The GPU max texture size (16384 here) is not the limit: larger
/// images are tiled and draw correctly, just slowly once past the 512 MB texture cache (#1553).
/// </summary>
public static class ImageSizeLimit
{
    private const int BytesPerPixel = 4;

    /// <summary>True when an image this size cannot be decoded.</summary>
    public static bool Exceeds(int width, int height) =>
        (long)width * height * BytesPerPixel > int.MaxValue;

    /// <summary>The user-facing error for an oversized image, naming the file, its size and the limit.</summary>
    public static string Describe(string fileName, int width, int height)
    {
        double gigabytes = (double)width * height * BytesPerPixel / (1024.0 * 1024 * 1024);
        return $"{fileName} is {width} x {height} pixels ({gigabytes:0.0} GB decoded). " +
               "The editor can open images up to 2 GB decoded (width x height x 4 bytes, about 23170 x 23170).";
    }
}
