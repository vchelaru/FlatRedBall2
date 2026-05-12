namespace AnimationEditor.Core.Rendering;

/// <summary>
/// Computes the pixel bounding box for a new animation frame created by a
/// Ctrl+click in plain mode (no grid, no magic-wand).
/// </summary>
public static class PlainClickFrameRegionCalculator
{
    /// <summary>
    /// Minimum frame side length used when no last-frame reference exists
    /// or the last frame is smaller than this on a given axis.
    /// </summary>
    public const int MinSize = 16;

    /// <summary>
    /// Returns the pixel bounding box for a new frame centered at
    /// (<paramref name="texX"/>, <paramref name="texY"/>) in texture space.
    /// <para>
    /// The frame dimensions are <c>max(16, <paramref name="lastFramePixelW"/>)</c> ×
    /// <c>max(16, <paramref name="lastFramePixelH"/>)</c>, clamped so the result
    /// stays inside [0, <paramref name="bitmapW"/>] × [0, <paramref name="bitmapH"/>].
    /// </para>
    /// </summary>
    /// <param name="texX">Click X in texture-pixel coordinates.</param>
    /// <param name="texY">Click Y in texture-pixel coordinates.</param>
    /// <param name="bitmapW">Texture width in pixels.</param>
    /// <param name="bitmapH">Texture height in pixels.</param>
    /// <param name="lastFramePixelW">Pixel width of the last frame in the selected animation, or 0 if none.</param>
    /// <param name="lastFramePixelH">Pixel height of the last frame in the selected animation, or 0 if none.</param>
    public static (int minX, int minY, int maxX, int maxY) Compute(
        float texX, float texY,
        int bitmapW, int bitmapH,
        int lastFramePixelW = 0, int lastFramePixelH = 0)
    {
        int w = Math.Max(MinSize, lastFramePixelW);
        int h = Math.Max(MinSize, lastFramePixelH);
        int cx = (int)texX;
        int cy = (int)texY;

        int minX = Math.Max(0, cx - w / 2);
        int minY = Math.Max(0, cy - h / 2);
        int maxX = Math.Min(bitmapW, minX + w);
        int maxY = Math.Min(bitmapH, minY + h);
        return (minX, minY, maxX, maxY);
    }
}
