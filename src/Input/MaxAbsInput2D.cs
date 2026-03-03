using System;

namespace FlatRedBall2.Input;

// TODO: Remove this class once the input system has proper multi-device support
// (e.g. a unified input binding layer). Using it as a short-term bridge between
// keyboard and gamepad until that exists.

/// <summary>
/// Combines two <see cref="I2DInput"/> sources by returning whichever has the
/// larger absolute value on each axis independently. Useful for merging keyboard
/// and gamepad so that either device drives the same behavior without conflict.
/// </summary>
public class MaxAbsInput2D : I2DInput
{
    private readonly I2DInput _a;
    private readonly I2DInput _b;

    public MaxAbsInput2D(I2DInput a, I2DInput b)
    {
        _a = a;
        _b = b;
    }

    public float X => MathF.Abs(_a.X) >= MathF.Abs(_b.X) ? _a.X : _b.X;
    public float Y => MathF.Abs(_a.Y) >= MathF.Abs(_b.Y) ? _a.Y : _b.Y;
}
