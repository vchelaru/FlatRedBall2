namespace AnimationEditor.Core.Rendering;

/// <summary>
/// Computes the flip transform needed to render a frame with H/V/diagonal flip flags applied.
/// Extracted from <c>PreviewControl.DrawFrameCore</c> so the decision logic can be unit-tested
/// without a SkiaSharp canvas.
/// </summary>
public static class FlipScaleCalculator
{
    /// <summary>
    /// Returns <c>true</c> when any flip is active (i.e. a Save/transform/Restore is required).
    /// </summary>
    public static bool IsFlipped(bool flipHorizontal, bool flipVertical, bool flipDiagonal = false)
        => flipHorizontal || flipVertical || flipDiagonal;

    /// <summary>
    /// Returns the 2x2 linear transform coefficients (a, b, c, d) such that a point offset
    /// (x, y) from the frame's pivot maps to (a*x + b*y, c*x + d*y). Diagonal flip alone maps
    /// (x, y) to (-y, -x) — the same transpose used by <c>TileMapCollisions.ApplyFlips</c> for
    /// Tiled's diagonal tile-flip flag — and H/V then mirror on top of that.
    /// </summary>
    public static (float a, float b, float c, float d) ComputeMatrix(
        bool flipHorizontal, bool flipVertical, bool flipDiagonal)
    {
        if (!flipDiagonal)
            return (flipHorizontal ? -1f : 1f, 0f, 0f, flipVertical ? -1f : 1f);

        return (0f, flipHorizontal ? 1f : -1f, flipVertical ? 1f : -1f, 0f);
    }
}
