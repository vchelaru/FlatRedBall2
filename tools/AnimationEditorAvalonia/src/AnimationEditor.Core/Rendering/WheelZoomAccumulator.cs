using System;

namespace AnimationEditor.Core.Rendering;

/// <summary>
/// Turns wheel deltas into whole zoom steps, one per mouse-wheel notch (#1236). A trackpad sends
/// many small-delta events per gesture; stepping once per event instead of per notch's worth made
/// a light two-finger scroll race through every zoom level.
/// </summary>
public sealed class WheelZoomAccumulator
{
    /// <summary>Avalonia reports one physical wheel notch as a delta of 1.0.</summary>
    public const double NotchDelta = 1.0;

    private double _accumulated;

    /// <summary>
    /// Adds <paramref name="delta"/> and returns how many whole notches the running total crossed
    /// (signed), carrying the remainder. A delta against the carried direction drops the remainder
    /// first, so reversing never steps the old way.
    /// </summary>
    public int Consume(double delta)
    {
        if (Math.Sign(delta) == -Math.Sign(_accumulated))
        {
            _accumulated = 0;
        }

        _accumulated += delta;
        // Round off float drift first: ten 0.1 deltas sum to 0.9999999, which must still be a notch.
        int steps = (int)Math.Round(_accumulated / NotchDelta, 6);
        _accumulated -= steps * NotchDelta;
        return steps;
    }
}
