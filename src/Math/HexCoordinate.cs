using System.Collections.Generic;

namespace FlatRedBall2.Math;

/// <summary>Identifies a hexagonal grid cell using axial coordinates.</summary>
public readonly record struct HexCoordinate(int Q, int R)
{
    private static readonly HexCoordinate[] NeighborOffsets =
    [
        new(1, 0),
        new(0, 1),
        new(-1, 1),
        new(-1, 0),
        new(0, -1),
        new(1, -1),
    ];

    /// <summary>Returns the six adjacent axial cells counter-clockwise (Y+ up), starting at +Q.</summary>
    public IReadOnlyList<HexCoordinate> GetNeighbors()
    {
        var result = new HexCoordinate[NeighborOffsets.Length];
        for (int i = 0; i < NeighborOffsets.Length; i++)
        {
            var offset = NeighborOffsets[i];
            result[i] = new HexCoordinate(checked(Q + offset.Q), checked(R + offset.R));
        }
        return result;
    }

    /// <summary>Returns the number of cell-to-cell steps to <paramref name="other"/>.</summary>
    public int DistanceTo(HexCoordinate other)
    {
        long q = (long)Q - other.Q;
        long r = (long)R - other.R;
        long s = -q - r;
        return checked((int)((System.Math.Abs(q) + System.Math.Abs(r) + System.Math.Abs(s)) / 2));
    }
}
