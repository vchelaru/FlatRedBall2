using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

// Invariants that must hold for tile collision resolution, swept over many start positions on
// every layout in TileResolutionMaps against actors smaller than, equal to and larger than a tile.
// A start is "penetrating" when the actor overlaps a tile by more than TileWorld.Eps (measured with
// the harness's own geometry, not the dispatcher) while its center is in an empty cell.
public class TileResolutionInvariantTests
{
    private const float ScanStep = 4f;

    public static IEnumerable<object[]> Pairs() => TileResolutionActors.WellBehavedMapActorPairs();

    private static IEnumerable<Vector2> Starts(string map, ActorSpec spec)
    {
        var (w, h) = TileResolutionMaps.Size(map);
        return TileWorld.ScanPositions(-spec.HalfW, w * 16f + spec.HalfW, -spec.HalfH, h * 16f + spec.HalfH, ScanStep);
    }

    private static bool CenterInTile(TileShapes tiles, Vector2 p)
    {
        var (c, r) = TileWorld.CellOf(p.X, p.Y);
        return TileWorld.HasTile(tiles, c, r);
    }

    private static string Describe(string map, string actor, Vector2 start, string detail) =>
        $"{map}/{actor} start=({start.X:0.##},{start.Y:0.##}) {detail}";

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Scan_PenetratingActor_EndsOutsideEveryTileWithinFourPasses(string map, string actor)
    {
        var spec = TileResolutionActors.Get(actor);
        var tiles = TileWorld.Map(TileResolutionMaps.Get(map));
        var failures = new List<string>();

        foreach (var p in Starts(map, spec))
        {
            if (CenterInTile(tiles, p) || !TileWorld.Penetrates(spec, p.X, p.Y, tiles)) continue;
            var last = TileWorld.ResolvePasses(spec, tiles, p.X, p.Y, 4)[^1];
            if (TileWorld.Penetrates(spec, last.X, last.Y, tiles))
                failures.Add(Describe(map, actor, p, $"still overlapping at ({last.X:0.##},{last.Y:0.##})"));
        }

        failures.ShouldBeEmpty(string.Join("\n", failures.Take(5)));
    }

    // The first pass can add up to a full pass of push per axis; this guards against runaway pushes
    // (e.g. a corner clip shoving an actor a whole tile away).
    [Theory]
    [MemberData(nameof(Pairs))]
    public void Scan_FirstPass_DisplacementIsBoundedByActorExtentPlusHalfATile(string map, string actor)
    {
        var spec = TileResolutionActors.Get(actor);
        var tiles = TileWorld.Map(TileResolutionMaps.Get(map));
        float boundX = spec.HalfW + TileWorld.Grid / 2f + 0.01f;
        float boundY = spec.HalfH + TileWorld.Grid / 2f + 0.01f;
        var failures = new List<string>();

        foreach (var p in Starts(map, spec))
        {
            if (CenterInTile(tiles, p) || !TileWorld.Penetrates(spec, p.X, p.Y, tiles)) continue;
            var first = TileWorld.ResolveOnce(spec, tiles, p.X, p.Y);
            if (MathF.Abs(first.X - p.X) > boundX || MathF.Abs(first.Y - p.Y) > boundY)
                failures.Add(Describe(map, actor, p, $"moved to ({first.X:0.##},{first.Y:0.##})"));
        }

        failures.ShouldBeEmpty(string.Join("\n", failures.Take(5)));
    }

    // Starting with the center in open space, no pass may carry the center through a tile.
    [Theory]
    [MemberData(nameof(Pairs))]
    public void Scan_Resolution_NeverMovesTheCenterThroughATile(string map, string actor)
    {
        var spec = TileResolutionActors.Get(actor);
        var tiles = TileWorld.Map(TileResolutionMaps.Get(map));
        var failures = new List<string>();

        foreach (var p in Starts(map, spec))
        {
            if (CenterInTile(tiles, p) || !TileWorld.Penetrates(spec, p.X, p.Y, tiles)) continue;
            var prev = p;
            foreach (var q in TileWorld.ResolvePasses(spec, tiles, p.X, p.Y, 4))
            {
                if (!TileWorld.SegmentIsClear(tiles, prev, q))
                {
                    failures.Add(Describe(map, actor, p, $"center crossed a tile going to ({q.X:0.##},{q.Y:0.##})"));
                    break;
                }
                prev = q;
            }
        }

        failures.ShouldBeEmpty(string.Join("\n", failures.Take(5)));
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Scan_ActorNotOverlappingAnyTile_IsNeverMoved(string map, string actor)
    {
        var spec = TileResolutionActors.Get(actor);
        var tiles = TileWorld.Map(TileResolutionMaps.Get(map));
        var failures = new List<string>();

        foreach (var p in Starts(map, spec))
        {
            if (CenterInTile(tiles, p) || TileWorld.Penetrates(spec, p.X, p.Y, tiles)) continue;
            var q = TileWorld.ResolveOnce(spec, tiles, p.X, p.Y);
            if (MathF.Abs(q.X - p.X) > 0.011f || MathF.Abs(q.Y - p.Y) > 0.011f)
                failures.Add(Describe(map, actor, p, $"phantom push to ({q.X:0.##},{q.Y:0.##})"));
        }

        failures.ShouldBeEmpty(string.Join("\n", failures.Take(5)));
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Scan_Resolution_IsDeterministic(string map, string actor)
    {
        var spec = TileResolutionActors.Get(actor);
        var tiles = TileWorld.Map(TileResolutionMaps.Get(map));
        var failures = new List<string>();

        foreach (var p in Starts(map, spec))
        {
            if (CenterInTile(tiles, p) || !TileWorld.Penetrates(spec, p.X, p.Y, tiles)) continue;
            var a = TileWorld.ResolvePasses(spec, tiles, p.X, p.Y, 6);
            var b = TileWorld.ResolvePasses(spec, tiles, p.X, p.Y, 6);
            if (!a.SequenceEqual(b))
                failures.Add(Describe(map, actor, p, "two identical runs differ"));
        }

        failures.ShouldBeEmpty(string.Join(Environment.NewLine, failures.Take(5)));
    }

    // A rest position (clear of every tile after some pass) must be a fixed point. The settled position is
    // rounded to 3 decimals first: the engine's own push can leave a 1-ULP overlap that behaves differently,
    // see TileResolutionSceneTests.Quirk_OneUlpOverlap_*.
    [Theory]
    [MemberData(nameof(Pairs))]
    public void Scan_SettledActor_IsStableUnderFurtherPasses(string map, string actor)
    {
        var spec = TileResolutionActors.Get(actor);
        var tiles = TileWorld.Map(TileResolutionMaps.Get(map));
        var failures = new List<string>();

        foreach (var p in Starts(map, spec))
        {
            if (CenterInTile(tiles, p) || !TileWorld.Penetrates(spec, p.X, p.Y, tiles)) continue;
            var passes = TileWorld.ResolvePasses(spec, tiles, p.X, p.Y, 6);
            int clearAt = passes.FindIndex(q => !TileWorld.Penetrates(spec, q.X, q.Y, tiles));
            if (clearAt < 0) continue;

            var settled = new Vector2(MathF.Round(passes[clearAt].X, 3), MathF.Round(passes[clearAt].Y, 3));
            var after = TileWorld.ResolvePasses(spec, tiles, settled.X, settled.Y, 3)[^1];
            if (MathF.Abs(after.X - settled.X) > 0.011f || MathF.Abs(after.Y - settled.Y) > 0.011f)
                failures.Add(Describe(map, actor, p, $"settled at ({settled.X:0.###},{settled.Y:0.###}) then moved to ({after.X:0.###},{after.Y:0.###})"));
        }

        failures.ShouldBeEmpty(string.Join(Environment.NewLine, failures.Take(5)));
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Scan_MirroredLayout_GivesMirroredResult(string map, string actor)
    {
        // Only for actors that are their own mirror image in both axes.
        if (actor is "triDown8" or "triUp8" or "rot30") return;
        var spec = TileResolutionActors.Get(actor);
        var rows = TileResolutionMaps.Get(map);
        var (w, h) = TileResolutionMaps.Size(map);
        var tiles = TileWorld.Map(rows);
        var flippedX = TileWorld.Map(rows.Select(r => new string(r.Reverse().ToArray())).ToArray());
        var flippedY = TileWorld.Map(rows.Reverse().ToArray());
        float width = w * 16f, height = h * 16f;
        var failures = new List<string>();

        foreach (var p in Starts(map, spec))
        {
            if (CenterInTile(tiles, p) || !TileWorld.Penetrates(spec, p.X, p.Y, tiles)) continue;
            var q = TileWorld.ResolveOnce(spec, tiles, p.X, p.Y);

            var mx = TileWorld.ResolveOnce(spec, flippedX, width - p.X, p.Y);
            if (MathF.Abs(width - mx.X - q.X) > 0.01f || MathF.Abs(mx.Y - q.Y) > 0.01f)
                failures.Add(Describe(map, actor, p, $"X-mirror differs: ({q.X:0.##},{q.Y:0.##}) vs ({width - mx.X:0.##},{mx.Y:0.##})"));

            var my = TileWorld.ResolveOnce(spec, flippedY, p.X, height - p.Y);
            if (MathF.Abs(my.X - q.X) > 0.01f || MathF.Abs(height - my.Y - q.Y) > 0.01f)
                failures.Add(Describe(map, actor, p, $"Y-mirror differs: ({q.X:0.##},{q.Y:0.##}) vs ({my.X:0.##},{height - my.Y:0.##})"));
        }

        failures.ShouldBeEmpty(string.Join("\n", failures.Take(5)));
    }

    // TileShapes.X/Y place the grid; moving it must move the result by exactly the same amount.
    [Theory]
    [MemberData(nameof(Pairs))]
    public void Scan_TileShapesOrigin_TranslatesTheResult(string map, string actor)
    {
        var spec = TileResolutionActors.Get(actor);
        var rows = TileResolutionMaps.Get(map);
        var tiles = TileWorld.Map(rows);
        const float ox = -1234.5f, oy = 777.25f;
        var moved = TileWorld.MapAt(ox, oy, rows);
        var failures = new List<string>();

        foreach (var p in Starts(map, spec))
        {
            if (CenterInTile(tiles, p) || !TileWorld.Penetrates(spec, p.X, p.Y, tiles)) continue;
            var q = TileWorld.ResolveOnce(spec, tiles, p.X, p.Y);
            var m = TileWorld.ResolveOnce(spec, moved, p.X + ox, p.Y + oy);
            if (MathF.Abs(m.X - ox - q.X) > 0.05f || MathF.Abs(m.Y - oy - q.Y) > 0.05f)
                failures.Add(Describe(map, actor, p, $"translated result differs: ({q.X:0.##},{q.Y:0.##}) vs ({m.X - ox:0.##},{m.Y - oy:0.##})"));
        }

        failures.ShouldBeEmpty(string.Join("\n", failures.Take(5)));
    }
}
