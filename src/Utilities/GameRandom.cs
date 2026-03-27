using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FlatRedBall2.Math;

namespace FlatRedBall2.Utilities;

/// <summary>
/// Derives from <see cref="Random"/> and provides additional utility methods commonly used in games.
/// Access the shared instance via <see cref="FlatRedBallService.Random"/>.
/// </summary>
public class GameRandom : Random
{
    public GameRandom() : base() { }

    public GameRandom(int seed) : base(seed) { }

    /// <summary>
    /// Returns a random element from a list. The list must have at least one item.
    /// </summary>
    public T In<T>(IReadOnlyList<T> list)
    {
#if DEBUG
        if (list == null) throw new ArgumentNullException(nameof(list));
        if (list.Count == 0) throw new InvalidOperationException("Cannot get a random element from an empty list.");
#endif
        return list[Next(list.Count)];
    }

    /// <summary>
    /// Returns <paramref name="numberToReturn"/> unique elements chosen at random from <paramref name="list"/>.
    /// </summary>
    public IList<T> MultipleIn<T>(IReadOnlyList<T> list, int numberToReturn)
    {
#if DEBUG
        if (numberToReturn > list.Count)
            throw new ArgumentException(
                $"Cannot return {numberToReturn} items from a list of {list.Count} elements.");
#endif
        var remaining = list.ToList();
        var result = new List<T>(numberToReturn);
        for (int i = 0; i < numberToReturn; i++)
        {
            int index = Next(remaining.Count);
            result.Add(remaining[index]);
            remaining.RemoveAt(index);
        }
        return result;
    }

    /// <summary>
    /// Returns a random float in [<paramref name="lowerBound"/>, <paramref name="upperBound"/>] (inclusive).
    /// </summary>
    public float Between(float lowerBound, float upperBound) =>
        lowerBound + (float)NextDouble() * (upperBound - lowerBound);

    /// <summary>
    /// Returns a random double in [<paramref name="lowerBound"/>, <paramref name="upperBound"/>] (inclusive).
    /// </summary>
    public double Between(double lowerBound, double upperBound) =>
        lowerBound + NextDouble() * (upperBound - lowerBound);

    /// <summary>
    /// Returns a random int in [<paramref name="lowerInclusive"/>, <paramref name="upperExclusive"/>) (upper bound exclusive).
    /// </summary>
    public int Between(int lowerInclusive, int upperExclusive) =>
        lowerInclusive + Next(upperExclusive - lowerInclusive);

    /// <summary>
    /// Returns a random <see cref="Angle"/> uniformly distributed over a full circle (0 to 2π).
    /// </summary>
    public Angle NextAngle() => Angle.FromRadians((float)(NextDouble() * MathF.PI * 2f));

    /// <summary>
    /// Returns a random bool.
    /// </summary>
    public bool NextBool() => Next(2) == 0;

    /// <summary>
    /// Returns +1f or -1f at random.
    /// </summary>
    public float NextSign() => NextBool() ? 1f : -1f;

    /// <summary>
    /// Returns a <see cref="Vector2"/> in a random direction with length uniformly chosen
    /// from [<paramref name="minLength"/>, <paramref name="maxLength"/>].
    /// </summary>
    public Vector2 RadialVector2(float minLength, float maxLength) =>
        NextAngle().ToVector2() * Between(minLength, maxLength);

    /// <summary>
    /// Returns a <see cref="Vector2"/> whose direction lies within the angular wedge
    /// [<paramref name="minAngle"/>, <paramref name="maxAngle"/>] and whose length lies within
    /// [<paramref name="minLength"/>, <paramref name="maxLength"/>].
    /// </summary>
    public Vector2 WedgeVector2(float minLength, float maxLength, Angle minAngle, Angle maxAngle)
    {
        var angle = Angle.FromRadians(Between(minAngle.Radians, maxAngle.Radians));
        var length = Between(minLength, maxLength);
        return angle.ToVector2() * length;
    }

    /// <summary>
    /// Returns a uniformly distributed random point inside a circle of the given <paramref name="radius"/>.
    /// Uses square-root sampling to avoid bunching at the center.
    /// </summary>
    public Vector2 PointInCircle(float radius)
    {
        var r = (float)System.Math.Sqrt(Between(0f, 1f)) * radius;
        return NextAngle().ToVector2() * r;
    }
}
