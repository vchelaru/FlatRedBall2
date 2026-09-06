using System;

namespace AnimationEditor.Core.Rendering;

/// <summary>
/// Pure Shift-constrained-drag math (#1022): given a total drag delta from the drag's
/// starting point, zero out the axis with the smaller magnitude so the drag moves purely
/// horizontally or vertically — the same convention as Photoshop/Figma constrained drags.
/// </summary>
public static class AxisLock
{
    /// <summary>
    /// Applies axis locking to a world-space drag delta. When <paramref name="locked"/> is
    /// <c>false</c>, returns <paramref name="dx"/>/<paramref name="dy"/> unchanged. When
    /// <c>true</c>, zeroes whichever of the two has the smaller absolute magnitude; on an
    /// exact tie both axes are left free rather than arbitrarily picking one.
    /// </summary>
    public static (float Dx, float Dy) Apply(float dx, float dy, bool locked)
    {
        if (!locked) return (dx, dy);

        float absDx = MathF.Abs(dx);
        float absDy = MathF.Abs(dy);

        if (absDx > absDy) return (dx, 0f);
        if (absDy > absDx) return (0f, dy);
        return (dx, dy);
    }
}
