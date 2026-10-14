using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

// The main invariants (TileResolutionInvariantTests) repeated with AssignSurroundedTilesSolidOutward applied,
// on the layouts that have tiles for it to change plus a few without.
public class TileResolutionInvariantAfterPassTests
{
    private static readonly string[] Maps =
    {
        "block2x2", "block3x3", "block7x7", "thickfloor", "stairs", "lBig", "plus", "room", "cave",
        "tJunction", "tInverted", "checker", "floor", "wall",
    };

    public static IEnumerable<object[]> Pairs() =>
        TileResolutionActors.WellBehavedMapActorPairs().Where(p => Maps.Contains((string)p[0]) && !IsKnownQuirk((string)p[0], (string)p[1]));

    // plus + circles: a flush circle is shoved sideways by the next pass, see
    // TileResolutionSurroundedPassTests.Quirk_Plus_*. stairs + the 40-wide actors: the oversized actor is
    // carried through a step by the chain of pushes.
    private static bool IsKnownQuirk(string map, string actor) =>
        TileResolutionActors.IsKnownQuirk(map, actor)
        || (map == "plus" && actor.StartsWith("circ", StringComparison.Ordinal))
        || (map == "stairs" && (actor == "sq20" || actor == "circ20"));

    private static TileShapes Built(string map)
    {
        var tiles = TileWorld.Map(TileResolutionMaps.Get(map));
        tiles.AssignSurroundedTilesSolidOutward();
        return tiles;
    }

    private static IEnumerable<Vector2> Starts(string map, ActorSpec spec)
    {
        var (w, h) = TileResolutionMaps.Size(map);
        return TileWorld.ScanPositions(-spec.HalfW, w * 16f + spec.HalfW, -spec.HalfH, h * 16f + spec.HalfH, 4f);
    }

    private static bool CenterInTile(TileShapes tiles, Vector2 p)
    {
        var (c, r) = TileWorld.CellOf(p.X, p.Y);
        return TileWorld.HasTile(tiles, c, r);
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void AfterPass_PenetratingActor_EndsOutsideEveryTileWithinFourPasses(string map, string actor)
    {
        var spec = TileResolutionActors.Get(actor);
        var tiles = Built(map);
        var failures = new List<string>();

        foreach (var p in Starts(map, spec))
        {
            if (CenterInTile(tiles, p) || !TileWorld.Penetrates(spec, p.X, p.Y, tiles)) continue;
            var last = TileWorld.ResolvePasses(spec, tiles, p.X, p.Y, 4)[^1];
            if (TileWorld.Penetrates(spec, last.X, last.Y, tiles))
                failures.Add($"{map}/{actor} start=({p.X:0.##},{p.Y:0.##}) still overlapping at ({last.X:0.##},{last.Y:0.##})");
        }

        failures.ShouldBeEmpty(string.Join(Environment.NewLine, failures.Take(5)));
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void AfterPass_Resolution_NeverMovesTheCenterThroughATile(string map, string actor)
    {
        var spec = TileResolutionActors.Get(actor);
        var tiles = Built(map);
        var failures = new List<string>();

        foreach (var p in Starts(map, spec))
        {
            if (CenterInTile(tiles, p) || !TileWorld.Penetrates(spec, p.X, p.Y, tiles)) continue;
            var prev = p;
            foreach (var q in TileWorld.ResolvePasses(spec, tiles, p.X, p.Y, 4))
            {
                if (!TileWorld.SegmentIsClear(tiles, prev, q))
                {
                    failures.Add($"{map}/{actor} start=({p.X:0.##},{p.Y:0.##}) center crossed a tile going to ({q.X:0.##},{q.Y:0.##})");
                    break;
                }
                prev = q;
            }
        }

        failures.ShouldBeEmpty(string.Join(Environment.NewLine, failures.Take(5)));
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void AfterPass_ActorNotOverlappingAnyTile_IsNeverMoved(string map, string actor)
    {
        var spec = TileResolutionActors.Get(actor);
        var tiles = Built(map);
        var failures = new List<string>();

        foreach (var p in Starts(map, spec))
        {
            if (CenterInTile(tiles, p) || TileWorld.Penetrates(spec, p.X, p.Y, tiles)) continue;
            var q = TileWorld.ResolveOnce(spec, tiles, p.X, p.Y);
            if (MathF.Abs(q.X - p.X) > 0.011f || MathF.Abs(q.Y - p.Y) > 0.011f)
                failures.Add($"{map}/{actor} start=({p.X:0.##},{p.Y:0.##}) phantom push to ({q.X:0.##},{q.Y:0.##})");
        }

        failures.ShouldBeEmpty(string.Join(Environment.NewLine, failures.Take(5)));
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void AfterPass_SettledActor_IsStableUnderFurtherPasses(string map, string actor)
    {
        var spec = TileResolutionActors.Get(actor);
        var tiles = Built(map);
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
                failures.Add($"{map}/{actor} start=({p.X:0.##},{p.Y:0.##}) settled at ({settled.X:0.###},{settled.Y:0.###}) then moved to ({after.X:0.###},{after.Y:0.###})");
        }

        failures.ShouldBeEmpty(string.Join(Environment.NewLine, failures.Take(5)));
    }
}
