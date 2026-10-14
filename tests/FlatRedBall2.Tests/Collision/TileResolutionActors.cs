using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;

namespace FlatRedBall2.Tests.Collision;

// Actor catalog shared by the scan-style tests. Tile cells are 16 units, so sq8/circ8 are exactly a
// tile wide, the *4 actors are smaller than a tile and the *12/*20 actors are larger.
internal static class TileResolutionActors
{
    public static readonly IReadOnlyDictionary<string, ActorSpec> All = new Dictionary<string, ActorSpec>
    {
        ["sq4"] = ActorSpec.Square(4f),
        ["sq8"] = ActorSpec.Square(8f),
        ["sq12"] = ActorSpec.Square(12f),
        ["sq20"] = ActorSpec.Square(20f),
        ["rect6x14"] = ActorSpec.Rect(6f, 14f),
        ["rect14x6"] = ActorSpec.Rect(14f, 6f),
        ["circ4"] = ActorSpec.Circle(4f),
        ["circ8"] = ActorSpec.Circle(8f),
        ["circ12"] = ActorSpec.Circle(12f),
        ["circ20"] = ActorSpec.Circle(20f),
        ["poly8"] = ActorSpec.PolyRect(8f, 8f),
        ["diamond10"] = ActorSpec.Diamond(10f),
        // Tip-down and tip-up triangles, and a square rotated 30 degrees (all convex polygons, so they use the polygon path).
        ["triDown8"] = ActorSpec.Convex("Triangle tip down 8", new Vector2(-8f, 8f), new Vector2(0f, -8f), new Vector2(8f, 8f)),
        ["triUp8"] = ActorSpec.Convex("Triangle tip up 8", new Vector2(-8f, -8f), new Vector2(8f, -8f), new Vector2(0f, 8f)),
        ["rot30"] = ActorSpec.Convex("Square rotated 30", Rotated(8f, 30f)),
    };

    public static ActorSpec Get(string id) => All[id];

    private static Vector2[] Rotated(float half, float degrees)
    {
        float r = degrees * MathF.PI / 180f, c = MathF.Cos(r), s = MathF.Sin(r);
        return new[] { new Vector2(-half, -half), new Vector2(half, -half), new Vector2(half, half), new Vector2(-half, half) }
            .Select(p => new Vector2(p.X * c - p.Y * s, p.X * s + p.Y * c)).ToArray();
    }

    // Largest actor extent (full width or height) that still fits in the narrowest gap of a layout.
    // Layouts with no tight gap return a very large limit.
    private static float FitLimit(string map) => map switch
    {
        "pit1" => 16f,
        "pit2" => 32f,
        "pillarGap1" => 16f,
        "checker" => 16f,
        "room" => 32f,
        "roomWithDoor" => 16f,
        "overhang" => 32f,
        "cave" => 16f,
        _ => 1000f,
    };

    public static bool Fits(string map, ActorSpec spec) =>
        System.Math.Max(spec.HalfW, spec.HalfH) * 2f <= FitLimit(map);

    /// <summary>(map, actor) pairs for scan theories: every layout against every actor that fits it.</summary>
    public static IEnumerable<object[]> MapActorPairs() =>
        TileResolutionMaps.All.Keys
            .SelectMany(m => All.Where(a => Fits(m, a.Value)).Select(a => new object[] { m, a.Key }));

    /// <summary>Same as <see cref="MapActorPairs"/> minus combos pinned separately as quirks.</summary>
    public static IEnumerable<object[]> WellBehavedMapActorPairs() =>
        MapActorPairs().Where(p => !IsKnownQuirk((string)p[0], (string)p[1]));

    // diagonalPair (two tiles touching only at a corner) with an actor that is not a square or circle, and the
    // 16-wide checker cells with a triangle (it fits only exactly): see
    // TileResolutionLayoutTests.Quirk_DiagonalNeighbours_*.
    public static bool IsKnownQuirk(string map, string actor) =>
        (map == "diagonalPair" && actor is "rect6x14" or "rect14x6" or "diamond10" or "triDown8" or "triUp8" or "rot30")
        || (map == "checker" && actor is "triDown8" or "triUp8");

    // 40x40 actors (2.5 tiles across) beside blocks, slabs, ledges and stairs: pushes by the plain tiles they overlap by a
    // sliver add up differently since the center-side guard became a per-tile flag (#1472). Parity with FlatRedBall 1 is
    // unknown, so these combinations are left out of the first-pass-bound, mirror and center-crossing invariants and
    // pinned in TileResolutionOversizedActorTests instead.
    public static bool IsOversizedParityUnknown(string map, string actor) =>
        actor is "sq20" or "circ20"
        && map is "block2x2" or "block3x3" or "block7x7" or "thickfloor" or "ledge" or "stairs";

    /// <summary><see cref="WellBehavedMapActorPairs"/> minus <see cref="IsOversizedParityUnknown"/>.</summary>
    public static IEnumerable<object[]> WellBehavedPairsExceptOversized() =>
        WellBehavedMapActorPairs().Where(p => !IsOversizedParityUnknown((string)p[0], (string)p[1]));
}
