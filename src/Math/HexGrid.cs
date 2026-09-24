using System;
using System.Collections.Generic;
using System.Numerics;

namespace FlatRedBall2.Math;

/// <summary>Immutable world-space layout for axial hexagonal grid coordinates.</summary>
public sealed class HexGrid
{
    private const double Sqrt3 = 1.7320508075688772935;

    /// <summary>
    /// Creates a layout whose origin is the center of cell (0, 0).
    /// </summary>
    /// <param name="radius">The center-to-vertex distance of each cell.</param>
    /// <param name="orientation">The orientation of each cell.</param>
    /// <param name="origin">The world-space center of cell (0, 0).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the layout parameters are invalid.</exception>
    public HexGrid(float radius, HexOrientation orientation, Vector2 origin)
    {
        if (!float.IsFinite(radius) || radius <= 0f)
            throw new ArgumentOutOfRangeException(nameof(radius));
        if (!float.IsFinite(origin.X) || !float.IsFinite(origin.Y))
            throw new ArgumentOutOfRangeException(nameof(origin));
        if (!Enum.IsDefined(orientation))
            throw new ArgumentOutOfRangeException(nameof(orientation));

        Radius = radius;
        Orientation = orientation;
        Origin = origin;
    }

    /// <summary>The center-to-vertex distance of each cell.</summary>
    public float Radius { get; }
    /// <summary>Whether cells are pointy-top or flat-top.</summary>
    public HexOrientation Orientation { get; }
    /// <summary>The world-space center of cell (0, 0).</summary>
    public Vector2 Origin { get; }

    /// <summary>Returns the axial cell containing <paramref name="worldPosition"/>.</summary>
    public HexCoordinate GetCellAt(Vector2 worldPosition)
    {
        if (!float.IsFinite(worldPosition.X) || !float.IsFinite(worldPosition.Y))
            throw new ArgumentOutOfRangeException(nameof(worldPosition));

        var (q, r) = GetRoundedAxial(worldPosition);
        if (q < int.MinValue || q > int.MaxValue || r < int.MinValue || r > int.MaxValue)
            throw new OverflowException("The world position is outside the representable axial coordinate range.");

        return new HexCoordinate((int)q, (int)r);
    }

    // Like GetCellAt, but clamps to the int range instead of throwing. Collision queries use this
    // so an entity far outside the grid gets no candidates rather than an exception.
    internal HexCoordinate GetCellAtClamped(Vector2 worldPosition)
    {
        var (q, r) = GetRoundedAxial(worldPosition);
        return new HexCoordinate(ClampToInt(q), ClampToInt(r));
    }

    private static int ClampToInt(double value) =>
        value < int.MinValue ? int.MinValue : value > int.MaxValue ? int.MaxValue : (int)value;

    private (double Q, double R) GetRoundedAxial(Vector2 worldPosition)
    {
        double x = worldPosition.X - Origin.X;
        double y = worldPosition.Y - Origin.Y;
        double q;
        double r;

        if (Orientation == HexOrientation.PointyTop)
        {
            q = (Sqrt3 / 3d * x - y / 3d) / Radius;
            r = 2d / 3d * y / Radius;
        }
        else
        {
            q = 2d / 3d * x / Radius;
            r = (-x / 3d + Sqrt3 / 3d * y) / Radius;
        }

        return Round(q, r);
    }

    /// <summary>Returns the world-space center of <paramref name="coordinate"/>.</summary>
    public Vector2 GetCellCenter(HexCoordinate coordinate)
    {
        double x;
        double y;
        if (Orientation == HexOrientation.PointyTop)
        {
            x = Radius * Sqrt3 * (coordinate.Q + coordinate.R / 2d) + Origin.X;
            y = Radius * 3d / 2d * coordinate.R + Origin.Y;
        }
        else
        {
            x = Radius * 3d / 2d * coordinate.Q + Origin.X;
            y = Radius * Sqrt3 * (coordinate.R + coordinate.Q / 2d) + Origin.Y;
        }
        return ToVector2(x, y);
    }

    /// <summary>Returns the six counter-clockwise world-space vertices of <paramref name="coordinate"/>.</summary>
    public IReadOnlyList<Vector2> GetCellCorners(HexCoordinate coordinate)
    {
        var center = GetCellCenter(coordinate);
        var corners = new Vector2[6];
        double firstAngle = Orientation == HexOrientation.PointyTop ? System.Math.PI / 6d : 0d;
        for (int i = 0; i < corners.Length; i++)
        {
            double angle = firstAngle + i * System.Math.PI / 3d;
            corners[i] = ToVector2(center.X + Radius * System.Math.Cos(angle), center.Y + Radius * System.Math.Sin(angle));
        }
        return corners;
    }

    private static (double Q, double R) Round(double q, double r)
    {
        double s = -q - r;
        double roundedQ = System.Math.Round(q, MidpointRounding.AwayFromZero);
        double roundedR = System.Math.Round(r, MidpointRounding.AwayFromZero);
        double roundedS = System.Math.Round(s, MidpointRounding.AwayFromZero);
        double qError = System.Math.Abs(roundedQ - q);
        double rError = System.Math.Abs(roundedR - r);
        double sError = System.Math.Abs(roundedS - s);

        if (qError >= rError && qError >= sError)
            roundedQ = -roundedR - roundedS;
        else if (rError >= sError)
            roundedR = -roundedQ - roundedS;

        return (roundedQ, roundedR);
    }

    private static Vector2 ToVector2(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || x < -float.MaxValue || x > float.MaxValue || y < -float.MaxValue || y > float.MaxValue)
            throw new OverflowException("The hex layout result is outside the representable world coordinate range.");
        return new Vector2((float)x, (float)y);
    }
}
