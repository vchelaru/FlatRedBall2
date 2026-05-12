using System;

namespace AnimationEditor.Core.Rendering;

/// <summary>
/// Pure, SkiaSharp-free hit-tester for collision shapes in the preview panel.
/// All coordinates are in screen space (already transformed from world space).
/// </summary>
public static class PreviewShapeHitTester
{
    /// <summary>
    /// Returns <c>true</c> if the click point is inside or within
    /// <paramref name="tolerance"/> pixels of a circle's edge.
    /// </summary>
    public static bool HitsCircle(
        float ptX, float ptY,
        float centerX, float centerY,
        float screenRadius,
        float tolerance = 5f)
    {
        float dx = ptX - centerX;
        float dy = ptY - centerY;
        float limit = screenRadius + tolerance;
        return dx * dx + dy * dy <= limit * limit;
    }

    /// <summary>
    /// Returns <c>true</c> if the click point is inside or within
    /// <paramref name="tolerance"/> pixels of an axis-aligned rectangle's edge.
    /// </summary>
    public static bool HitsRect(
        float ptX, float ptY,
        float centerX, float centerY,
        float halfW, float halfH,
        float tolerance = 5f)
    {
        return MathF.Abs(ptX - centerX) <= halfW + tolerance
            && MathF.Abs(ptY - centerY) <= halfH + tolerance;
    }
}
