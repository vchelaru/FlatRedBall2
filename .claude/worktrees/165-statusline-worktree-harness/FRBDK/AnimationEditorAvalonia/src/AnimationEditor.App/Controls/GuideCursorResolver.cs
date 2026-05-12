using Avalonia.Input;
using System;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Pure mapping: given guide lines and camera state, returns the cursor type
/// to show at screen position (px, py). Returns <c>null</c> when no guide is
/// near the pointer and the platform default cursor should be used.
/// </summary>
public static class GuideCursorResolver
{
    private const float HitPx     = 4f;
    private const float RulerSize = 20f;

    /// <summary>
    /// Returns the <see cref="StandardCursorType"/> appropriate for the pointer
    /// position, or <c>null</c> if the pointer is not near any guide.
    /// </summary>
    /// <param name="px">Pointer X in control (screen) coordinates.</param>
    /// <param name="py">Pointer Y in control (screen) coordinates.</param>
    /// <param name="hGuides">Horizontal guide world-Y values.</param>
    /// <param name="vGuides">Vertical guide world-X values.</param>
    /// <param name="panX">Current horizontal pan offset.</param>
    /// <param name="panY">Current vertical pan offset.</param>
    /// <param name="zoom">Current zoom factor (must be &gt; 0).</param>
    /// <param name="viewWidth">Control width in pixels.</param>
    /// <param name="viewHeight">Control height in pixels.</param>
    public static StandardCursorType? CursorTypeAt(
        float px, float py,
        float[] hGuides, float[] vGuides,
        float panX, float panY, float zoom,
        float viewWidth, float viewHeight)
    {
        if (viewWidth <= RulerSize || viewHeight <= RulerSize || zoom <= 0f)
            return null;

        // Ruler strips → clicking there creates a new guide, not dragging an existing one.
        if (px < RulerSize || py < RulerSize)
            return null;

        float cx = (viewWidth  - RulerSize) / 2f + RulerSize + panX;
        float cy = (viewHeight - RulerSize) / 2f + RulerSize + panY;

        foreach (float wy in hGuides)
            if (MathF.Abs(py - (cy + wy * zoom)) < HitPx)
                return StandardCursorType.SizeNorthSouth;

        foreach (float wx in vGuides)
            if (MathF.Abs(px - (cx + wx * zoom)) < HitPx)
                return StandardCursorType.SizeWestEast;

        return null;
    }
}
