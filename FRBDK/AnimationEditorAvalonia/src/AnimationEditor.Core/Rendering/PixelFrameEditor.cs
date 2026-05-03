using FlatRedBall.Content.AnimationChain;

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
    /// The frame is clamped so that it remains fully within [0, textureWidth].
    /// </summary>
    public static void SetX(AnimationFrameSave frame, int newXPixels, int textureWidth)
    {
        if (frame        == null) throw new ArgumentNullException(nameof(frame));
        if (textureWidth <= 0)    throw new ArgumentOutOfRangeException(nameof(textureWidth), "Must be > 0");

        int frameWidth  = Math.Max(1, RoundToPixel(frame.RightCoordinate, textureWidth)
                                    - RoundToPixel(frame.LeftCoordinate,  textureWidth));
        int clampedLeft = Math.Clamp(newXPixels, 0, textureWidth - frameWidth);
        frame.LeftCoordinate  = clampedLeft              / (float)textureWidth;
        frame.RightCoordinate = (clampedLeft + frameWidth) / (float)textureWidth;
    }

    /// <summary>
    /// Moves the frame vertically to the given pixel Y position, preserving
    /// the current height.  Both Top and Bottom are shifted by the same delta.
    /// The frame is clamped so that it remains fully within [0, textureHeight].
    /// </summary>
    public static void SetY(AnimationFrameSave frame, int newYPixels, int textureHeight)
    {
        if (frame         == null) throw new ArgumentNullException(nameof(frame));
        if (textureHeight <= 0)    throw new ArgumentOutOfRangeException(nameof(textureHeight), "Must be > 0");

        int frameHeight = Math.Max(1, RoundToPixel(frame.BottomCoordinate, textureHeight)
                                    - RoundToPixel(frame.TopCoordinate,    textureHeight));
        int clampedTop  = Math.Clamp(newYPixels, 0, textureHeight - frameHeight);
        frame.TopCoordinate    = clampedTop               / (float)textureHeight;
        frame.BottomCoordinate = (clampedTop + frameHeight) / (float)textureHeight;
    }

    /// <summary>
    /// Sets the frame's width in pixels.  LeftCoordinate is unchanged;
    /// RightCoordinate = LeftCoordinate + clamp(newWidth, 1, textureWidth-leftPx) / textureWidth.
    /// </summary>
    public static void SetWidth(AnimationFrameSave frame, int newWidthPixels, int textureWidth)
    {
        if (frame        == null) throw new ArgumentNullException(nameof(frame));
        if (textureWidth <= 0)    throw new ArgumentOutOfRangeException(nameof(textureWidth), "Must be > 0");

        int leftPixel  = RoundToPixel(frame.LeftCoordinate, textureWidth);
        int maxWidth   = Math.Max(1, textureWidth - leftPixel);
        int clampedW   = Math.Clamp(newWidthPixels, 1, maxWidth);
        frame.RightCoordinate = frame.LeftCoordinate + clampedW / (float)textureWidth;
    }

    /// <summary>
    /// Sets the frame's height in pixels.  TopCoordinate is unchanged;
    /// BottomCoordinate = TopCoordinate + clamp(newHeight, 1, textureHeight-topPx) / textureHeight.
    /// </summary>
    public static void SetHeight(AnimationFrameSave frame, int newHeightPixels, int textureHeight)
    {
        if (frame         == null) throw new ArgumentNullException(nameof(frame));
        if (textureHeight <= 0)    throw new ArgumentOutOfRangeException(nameof(textureHeight), "Must be > 0");

        int topPixel   = RoundToPixel(frame.TopCoordinate, textureHeight);
        int maxHeight  = Math.Max(1, textureHeight - topPixel);
        int clampedH   = Math.Clamp(newHeightPixels, 1, maxHeight);
        frame.BottomCoordinate = frame.TopCoordinate + clampedH / (float)textureHeight;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Rounds a UV coordinate to the nearest pixel boundary.</summary>
    public static float Round(float coord, int dimension)
        => (float)Math.Round(coord * dimension) / dimension;

    private static int RoundToPixel(float coord, int dimension)
        => (int)Math.Round(coord * dimension);
}
