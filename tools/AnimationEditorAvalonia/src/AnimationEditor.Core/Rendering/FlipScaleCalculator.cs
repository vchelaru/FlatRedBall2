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
    /// (x, y) from the frame's pivot maps to (a*x + b*y, c*x + d*y). This matrix applies
    /// directly to SkiaSharp canvas coordinates (Y-down, origin top-left) — unlike
    /// <c>TileMapCollisions.ApplyFlips</c>, which converts Tiled's Y-down pixel data to FRB2's
    /// Y-up local space before applying its own diagonal step. Diagonal flip alone is therefore
    /// the plain swap (x, y) to (y, x) here, not the negated (-y, -x) form that's correct in
    /// Y-up space — the plain swap is what fixes the top-left/bottom-right corners and swaps
    /// top-right/bottom-left, matching Tiled's actual diagonal-flip semantics. H/V then mirror
    /// on top of that.
    /// </summary>
    public static (float a, float b, float c, float d) ComputeMatrix(
        bool flipHorizontal, bool flipVertical, bool flipDiagonal)
    {
        if (!flipDiagonal)
            return (flipHorizontal ? -1f : 1f, 0f, 0f, flipVertical ? -1f : 1f);

        return (0f, flipHorizontal ? -1f : 1f, flipVertical ? -1f : 1f, 0f);
    }
}
