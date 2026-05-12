using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.Rendering;

/// <summary>
/// Pure-math helper for editing <c>AnimationFrameSave</c> UV coordinates when
/// the property inspector is in <b>Pixel</b> mode.
///
/// Mirrors the logic in the WinForms <c>AnimationFrameDisplayer.CoordinateChange</c>:
/// <list type="bullet">
///   <item>SetX  — shifts <em>both</em> LeftCoordinate and RightCoordinate by the pixel delta (preserves width).</item>
///   <item>SetY  — shifts <em>both</em> TopCoordinate and BottomCoordinate by the pixel delta (preserves height).</item>
///   <item>SetWidth  — adjusts RightCoordinate = LeftCoordinate + newWidth/textureWidth (keeps Left fixed).</item>
///   <item>SetHeight — adjusts BottomCoordinate = TopCoordinate + newHeight/textureHeight (keeps Top fixed).</item>
/// </list>
/// All coordinates are rounded to the nearest pixel after editing
/// (<c>round(coord * dimension) / dimension</c>) to avoid floating-point drift.
/// </summary>
public static class PixelFrameEditor
{
    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Moves the frame horizontally to the given pixel X position, preserving
    /// the current width.  Both Left and Right are shifted by the same delta.
    /// The frame may extend beyond the texture boundaries (UV coordinates outside [0, 1]).
    /// </summary>
    public static void SetX(AnimationFrameSave frame, int newXPixels, int textureWidth)
    {
        if (frame        == null) throw new ArgumentNullException(nameof(frame));
        if (textureWidth <= 0)    throw new ArgumentOutOfRangeException(nameof(textureWidth), "Must be > 0");

        int frameWidth = Math.Max(1, RoundToPixel(frame.RightCoordinate, textureWidth)
                                   - RoundToPixel(frame.LeftCoordinate,  textureWidth));
        frame.LeftCoordinate  = newXPixels              / (float)textureWidth;
        frame.RightCoordinate = (newXPixels + frameWidth) / (float)textureWidth;
    }

    /// <summary>
    /// Moves the frame vertically to the given pixel Y position, preserving
    /// the current height.  Both Top and Bottom are shifted by the same delta.
    /// The frame may extend beyond the texture boundaries (UV coordinates outside [0, 1]).
    /// </summary>
    public static void SetY(AnimationFrameSave frame, int newYPixels, int textureHeight)
    {
        if (frame         == null) throw new ArgumentNullException(nameof(frame));
        if (textureHeight <= 0)    throw new ArgumentOutOfRangeException(nameof(textureHeight), "Must be > 0");

        int frameHeight = Math.Max(1, RoundToPixel(frame.BottomCoordinate, textureHeight)
                                    - RoundToPixel(frame.TopCoordinate,    textureHeight));
        frame.TopCoordinate    = newYPixels               / (float)textureHeight;
        frame.BottomCoordinate = (newYPixels + frameHeight) / (float)textureHeight;
    }

    /// <summary>
    /// Sets the frame's width in pixels.  LeftCoordinate is unchanged;
    /// RightCoordinate = LeftCoordinate + max(1, newWidth) / textureWidth.
    /// Width is clamped to a minimum of 1 pixel; no upper bound is applied
    /// (the frame may extend past the right texture edge).
    /// </summary>
    public static void SetWidth(AnimationFrameSave frame, int newWidthPixels, int textureWidth)
    {
        if (frame        == null) throw new ArgumentNullException(nameof(frame));
        if (textureWidth <= 0)    throw new ArgumentOutOfRangeException(nameof(textureWidth), "Must be > 0");

        int clampedW = Math.Max(1, newWidthPixels);
        frame.RightCoordinate = frame.LeftCoordinate + clampedW / (float)textureWidth;
    }

    /// <summary>
    /// Sets the frame's height in pixels.  TopCoordinate is unchanged;
    /// BottomCoordinate = TopCoordinate + max(1, newHeight) / textureHeight.
    /// Height is clamped to a minimum of 1 pixel; no upper bound is applied
    /// (the frame may extend past the bottom texture edge).
    /// </summary>
    public static void SetHeight(AnimationFrameSave frame, int newHeightPixels, int textureHeight)
    {
        if (frame         == null) throw new ArgumentNullException(nameof(frame));
        if (textureHeight <= 0)    throw new ArgumentOutOfRangeException(nameof(textureHeight), "Must be > 0");

        int clampedH = Math.Max(1, newHeightPixels);
        frame.BottomCoordinate = frame.TopCoordinate + clampedH / (float)textureHeight;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Rounds a UV coordinate to the nearest pixel boundary.</summary>
    public static float Round(float coord, int dimension)
        => (float)Math.Round(coord * dimension) / dimension;

    private static int RoundToPixel(float coord, int dimension)
        => (int)Math.Round(coord * dimension);
}
