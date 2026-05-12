using System;
using System.Numerics;

namespace FlatRedBall2.Math;

/// <summary>
/// A unit-safe angle value. Stored internally as radians; expose either form via
/// <see cref="Degrees"/> / <see cref="Radians"/>. Construct via <see cref="FromDegrees"/>
/// or <see cref="FromRadians"/> — there is no implicit conversion from <see cref="float"/>,
/// so the call site always declares its units.
/// <para>
/// Standard math convention: 0° points right (+X), 90° points up (+Y); positive rotation
/// is counter-clockwise (Y+ up world space).
/// </para>
/// </summary>
public readonly struct Angle : IEquatable<Angle>
{
    private readonly float _radians;

    private Angle(float radians) => _radians = radians;

    /// <summary>Creates an angle from a value in degrees.</summary>
    public static Angle FromDegrees(float degrees) => new Angle(degrees * MathF.PI / 180f);

    /// <summary>Creates an angle from a value in radians.</summary>
    public static Angle FromRadians(float radians) => new Angle(radians);

    /// <summary>The angle value in degrees.</summary>
    public float Degrees => _radians * 180f / MathF.PI;

    /// <summary>The angle value in radians.</summary>
    public float Radians => _radians;

    /// <summary>
    /// Returns this angle reduced to the range (−π, π]. Use after accumulating rotation over
    /// many frames to prevent unbounded growth and the float precision loss it causes.
    /// </summary>
    public Angle Normalized()
    {
        var r = _radians % (2f * MathF.PI);
        if (r > MathF.PI) r -= 2f * MathF.PI;
        if (r < -MathF.PI) r += 2f * MathF.PI;
        return new Angle(r);
    }

    /// <summary>
    /// Returns the unit vector pointing in the direction this angle represents.
    /// Standard math convention: <c>Angle.Zero</c> points right (1, 0);
    /// <c>FromDegrees(90)</c> points up (0, 1). Positive rotation is counter-clockwise.
    /// </summary>
    public Vector2 ToVector2() => new Vector2(MathF.Cos(_radians), MathF.Sin(_radians));

    /// <summary>
    /// Returns the shortest signed rotation from <paramref name="a"/> to <paramref name="b"/>,
    /// in the range (−π, π]. Always picks the short way around, so rotating by the result
    /// never spins more than half a turn.
    /// </summary>
    public static Angle Between(Angle a, Angle b)
    {
        var diff = (b._radians - a._radians + MathF.PI) % (2f * MathF.PI) - MathF.PI;
        return new Angle(diff);
    }

    /// <summary>
    /// Linearly interpolates from <paramref name="a"/> to <paramref name="b"/> by <paramref name="t"/>
    /// along the shortest arc between them. <paramref name="t"/> = 0 returns <paramref name="a"/>;
    /// <paramref name="t"/> = 1 returns <paramref name="b"/> (normalized). Values outside [0, 1] extrapolate.
    /// </summary>
    public static Angle Lerp(Angle a, Angle b, float t)
    {
        var diff = Between(a, b)._radians;
        return new Angle(a._radians + diff * t).Normalized();
    }

    /// <summary>Adds two angles. The result is not normalized — call <see cref="Normalized"/> if needed.</summary>
    public static Angle operator +(Angle a, Angle b) => new Angle(a._radians + b._radians);
    /// <summary>Subtracts <paramref name="b"/> from <paramref name="a"/>. The result is not normalized.</summary>
    public static Angle operator -(Angle a, Angle b) => new Angle(a._radians - b._radians);
    /// <summary>Scales an angle (e.g. for per-frame integration: <c>RotationVelocity * dt</c>).</summary>
    public static Angle operator *(Angle a, float scalar) => new Angle(a._radians * scalar);
    /// <summary>Bitwise float equality on the underlying radian value. Does <b>not</b> normalize — 0° and 360° compare unequal.</summary>
    public static bool operator ==(Angle a, Angle b) => a._radians == b._radians;
    /// <summary>Inverse of <see cref="op_Equality"/>.</summary>
    public static bool operator !=(Angle a, Angle b) => a._radians != b._radians;

    /// <inheritdoc/>
    public bool Equals(Angle other) => _radians == other._radians;
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Angle other && Equals(other);
    /// <inheritdoc/>
    public override int GetHashCode() => _radians.GetHashCode();
    /// <inheritdoc/>
    public override string ToString() => $"Angle({Degrees:F2}°)";
}
