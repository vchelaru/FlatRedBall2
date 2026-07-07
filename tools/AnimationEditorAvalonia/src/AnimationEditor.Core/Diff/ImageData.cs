namespace AnimationEditor.Core.Diff;

/// <summary>
/// A decoded image as tightly-packed, row-major RGBA bytes (four bytes per pixel: R, G, B, A).
/// Deliberately decoupled from any image library so the diff logic stays Skia-free and unit-testable;
/// the SkiaSharp decode into this shape lives in the Views layer.
/// </summary>
public sealed class ImageData
{
    /// <param name="rgba">
    /// Row-major RGBA, length must equal <paramref name="width"/> × <paramref name="height"/> × 4.
    /// </param>
    public ImageData(int width, int height, byte[] rgba)
    {
        if (rgba.Length != width * height * 4)
            throw new System.ArgumentException(
                $"rgba length {rgba.Length} does not match {width}×{height}×4.", nameof(rgba));
        Width = width;
        Height = height;
        Rgba = rgba;
    }

    public int Width { get; }
    public int Height { get; }

    /// <summary>Row-major RGBA bytes; index of pixel (x, y) channel c is <c>(y*Width + x)*4 + c</c>.</summary>
    public byte[] Rgba { get; }
}
