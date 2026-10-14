using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

// TileShapes.AssignSurroundedTilesSolidOutward on and off. The pass must not change how an actor that is not
// deeply embedded is resolved; for embedded actors it gives a way out, with the gaps pinned below.
public class TileResolutionSurroundedPassTests
{
    private const float Tol = 1e-3f;
    private const int RestPasses = 12;

    private static TileShapes Built(string map, bool applyPass)
    {
        var tiles = TileWorld.Map(TileResolutionMaps.Get(map));
        if (applyPass) tiles.AssignSurroundedTilesSolidOutward();
        return tiles;
    }

    private static IEnumerable<Vector2> Starts(string map, ActorSpec spec)
    {
        var (w, h) = TileResolutionMaps.Size(map);
        return TileWorld.ScanPositions(0f, w * 16f, 0f, h * 16f, 3f);
    }

    private static bool CenterInTile(TileShapes tiles, Vector2 p)
    {
        var (c, r) = TileWorld.CellOf(p.X, p.Y);
        return TileWorld.HasTile(tiles, c, r);
    }

    public static IEnumerable<object[]> PassMapActorPairs() =>
        new[] { "block2x2", "block3x3", "block7x7", "thickfloor", "stairs", "lBig", "plus", "room" }
            .SelectMany(m => new[] { "sq4", "sq8", "sq12", "circ8", "circ12", "poly8", "rect6x14" }.Select(a => new object[] { m, a }));

    // ── The pass does not alter the first pass of ordinary (non-embedded) resolution ──────

    [Theory]
    [MemberData(nameof(PassMapActorPairs))]
    public void Pass_ActorWhoseCenterIsInOpenSpace_FirstPassIsIdenticalWithAndWithoutIt(string map, string actor)
    {
        var spec = TileResolutionActors.Get(actor);
        var off = Built(map, false);
        var on = Built(map, true);
        var failures = new List<string>();

        foreach (var p in Starts(map, spec))
        {
            if (CenterInTile(off, p) || !TileWorld.Penetrates(spec, p.X, p.Y, off)) continue;
            var a = TileWorld.ResolveOnce(spec, off, p.X, p.Y);
            var b = TileWorld.ResolveOnce(spec, on, p.X, p.Y);
            if (MathF.Abs(a.X - b.X) > 0.01f || MathF.Abs(a.Y - b.Y) > 0.01f)
                failures.Add($"{map}/{actor} ({p.X:0.##},{p.Y:0.##}) off=({a.X:0.##},{a.Y:0.##}) on=({b.X:0.##},{b.Y:0.##})");
        }

        failures.ShouldBeEmpty(string.Join(Environment.NewLine, failures.Take(5)));
    }

    // ── Embedded actors ──────────────────────────────────────────────────────────

    // Without the pass an actor deep inside a block is simply stuck, wherever its center is (the sides of the
    // tiles it overlaps are all suppressed or refuse because its center is on their inner side).
    [Fact]
    public void WithoutPass_ActorAtTheCenterOfA3x3Block_IsNeverMoved()
    {
        var tiles = Built("block3x3", applyPass: false); // block cells 3..5 -> x 48..96, center (72, 72)
        var spec = ActorSpec.Square(8f);

        var passes = TileWorld.ResolvePasses(spec, tiles, 72f, 72f, RestPasses);

        passes.ShouldAllBe(p => p == new Vector2(72f, 72f));
    }

    // With the pass the same actor is walked out through the nearest open side, one tile layer per pass.
    [Fact]
    public void WithPass_ActorAtTheCenterOfA3x3Block_IsPushedOutTheTopInTwoPasses()
    {
        var tiles = Built("block3x3", applyPass: true);
        var spec = ActorSpec.Square(8f);

        var passes = TileWorld.ResolvePasses(spec, tiles, 72f, 72f, 4);

        passes[0].ShouldBe(new Vector2(72f, 88f));
        passes[1].ShouldBe(new Vector2(72f, 104f)); // clear: block top is y=96, actor half 8
        passes[2].ShouldBe(passes[1]);
    }

    public static IEnumerable<object[]> FullyResolvedByThePass() =>
        new[] { "block3x3", "thickfloor" }
            .SelectMany(m => new[] { "sq4", "sq8", "sq12", "circ8", "circ12", "poly8", "diamond10", "rect6x14" }.Select(a => new object[] { m, a }))
            .Concat(new[] { "block7x7" }.SelectMany(m => new[] { "sq4", "sq8", "sq12", "circ12", "diamond10", "rect6x14" }.Select(a => new object[] { m, a })));

    [Theory]
    [MemberData(nameof(FullyResolvedByThePass))]
    public void WithPass_ActorAnywhereInsideTheBlock_EndsOutside(string map, string actor)
    {
        var spec = TileResolutionActors.Get(actor);
        var tiles = Built(map, applyPass: true);
        var failures = new List<string>();

        foreach (var p in Starts(map, spec))
        {
            if (!CenterInTile(tiles, p) || !TileWorld.Penetrates(spec, p.X, p.Y, tiles)) continue;
            var last = TileWorld.ResolvePasses(spec, tiles, p.X, p.Y, RestPasses)[^1];
            if (TileWorld.Penetrates(spec, last.X, last.Y, tiles))
                failures.Add($"{map}/{actor} start=({p.X:0.##},{p.Y:0.##}) stuck at ({last.X:0.##},{last.Y:0.##})");
        }

        failures.ShouldBeEmpty(string.Join(Environment.NewLine, failures.Take(5)));
    }

    // ── Known gaps: embedded actors the pass leaves stuck ───────────────────────────

    // The corner tile of a block has no center-side guard (as in FlatRedBall 1: it is not an L-shaped corner), so an
    // actor small enough to sit entirely in its inner quadrant is pushed out through the shallower exposed face,
    // pass or no pass. (Before the per-tile flag it was never pushed.)
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Block3x3_SmallActorInsideACornerTilesInnerQuadrant_IsPushedOutOfTheBlock(bool applyPass)
    {
        var tiles = Built("block3x3", applyPass); // corner tile (3,3): x 48..64, y 48..64, center (56, 56), faces Down|Left
        var spec = ActorSpec.Square(4f);
        var start = new Vector2(57.6f, 57.4f);

        var passes = TileWorld.ResolvePasses(spec, tiles, start.X, start.Y, RestPasses);

        passes[0].X.ShouldBe(57.6f, Tol);
        passes[0].Y.ShouldBe(44f, Tol); // Down (13.4) is shallower than Left (13.6)
        TileWorld.Penetrates(spec, passes[^1].X, passes[^1].Y, tiles).ShouldBeFalse();
    }

    // In a large block the pass routes an equal-sized actor toward a corner tile; it now continues through that
    // tile's exposed face instead of parking on its center (it used to stay embedded).
    [Fact]
    public void Block7x7_EqualSizedActor_IsRoutedThroughACornerTileAndLeavesTheBlock()
    {
        var tiles = Built("block7x7", applyPass: true);
        var spec = ActorSpec.Square(8f);

        var passes = TileWorld.ResolvePasses(spec, tiles, 57.6f, 66.4f, RestPasses);

        passes[0].X.ShouldBe(40f, Tol);
        passes[0].Y.ShouldBe(40f, Tol); // the corner tile's center
        passes[^1].X.ShouldBe(40f, Tol);
        passes[^1].Y.ShouldBe(24f, Tol);
        TileWorld.Penetrates(spec, passes[^1].X, passes[^1].Y, tiles).ShouldBeFalse();
    }

    // The pass lifts the center-side guard on the neighbors of the tiles it fills in. Those tiles then act on any
    // overlap, however small, so an actor resting flush against the left arm's underside (left with a sub-ULP overlap
    // by the first pass) must still be treated as touching: it stays put on the next pass, with or without the pass
    // applied (it used to be shoved 13.6 units sideways only with the pass).
    [Fact]
    public void Plus_ActorFlushUnderAnArmNextToTheFilledInCenter_IsNotMovedOnLaterPasses()
    {
        var spec = ActorSpec.Square(4f);
        var off = Built("plus", applyPass: false);
        var on = Built("plus", applyPass: true);

        var withoutPass = TileWorld.ResolvePasses(spec, off, 57.61f, 60.37f, 3);
        var withPass = TileWorld.ResolvePasses(spec, on, 57.61f, 60.37f, 3);

        withoutPass.ShouldAllBe(p => MathF.Abs(p.X - 57.61f) < Tol && MathF.Abs(p.Y - 60f) < Tol);
        withPass.ShouldAllBe(p => MathF.Abs(p.X - 57.61f) < Tol && MathF.Abs(p.Y - 60f) < Tol);
    }
}
