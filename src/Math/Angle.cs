using System;
using System.Numerics;

namespace FlatRedBall2.Math;

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

    public static Angle Between(Angle a, Angle b)
    {
        var diff = (b._radians - a._radians + MathF.PI) % (2f * MathF.PI) - MathF.PI;
        return new Angle(diff);
    }

    public static Angle Lerp(Angle a, Angle b, float t)
    {
        var diff = Between(a, b)._radians;
        return new Angle(a._radians + diff * t).Normalized();
    }

    public static Angle operator +(Angle a, Angle b) => new Angle(a._radians + b._radians);
    public static Angle operator -(Angle a, Angle b) => new Angle(a._radians - b._radians);
    public static Angle operator *(Angle a, float scalar) => new Angle(a._radians * scalar);
    public static bool operator ==(Angle a, Angle b) => a._radians == b._radians;
    public static bool operator !=(Angle a, Angle b) => a._radians != b._radians;

    public bool Equals(Angle other) => _radians == other._radians;
    public override bool Equals(object? obj) => obj is Angle other && Equals(other);
    public override int GetHashCode() => _radians.GetHashCode();
    public override string ToString() => $"Angle({Degrees:F2}°)";
}
