using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

// An actor that is touching a tile, or overlaps it by a few float ULPs (what a push leaves behind), must get no
// push at all along any face. The raw overlap test can still report a collision at that scale while the
// per-face separation rounds the near face to zero; the remaining faces must then not be used to push the actor
// across the tile.
public class TileResolutionTouchingContactTests
{
    private const float Tol = 1e-3f;

    // Lone tile at cell (2,2): x 32..48, y 32..48, center (40, 40).
    private static TileShapes TileWithSides(SolidSides sides)
    {
        var tiles = TileWorld.Map(".....", ".....", "..#..", ".....", ".....");
        tiles.GetTileAtCell(2, 2)!.SolidSides = sides;
        return tiles;
    }

    private static ActorSpec Spec(string id) => id == "poly4" ? ActorSpec.PolyRect(4f, 4f) : TileResolutionActors.Get(id);

    // (face the actor touches, outward direction of that face)
    private static readonly (SolidSides face, float dx, float dy)[] Faces =
    {
        (SolidSides.Up, 0f, 1f), (SolidSides.Down, 0f, -1f), (SolidSides.Left, -1f, 0f), (SolidSides.Right, 1f, 0f),
    };

    public static IEnumerable<object[]> Cases()
    {
        foreach (var id in new[] { "sq4", "circ4", "poly4" })
            foreach (var (face, dx, dy) in Faces)
            {
                // Side sets: every face, all but the touched one, the touched one alone or with others, and sets without it.
                var perpendicular = (dx == 0f ? SolidSides.Left : SolidSides.Up);
                var opposite = face switch { SolidSides.Up => SolidSides.Down, SolidSides.Down => SolidSides.Up, SolidSides.Left => SolidSides.Right, _ => SolidSides.Left };
                var sets = new[] { SolidSides.All, SolidSides.All & ~face, face, face | perpendicular, face | opposite, opposite, perpendicular, opposite | perpendicular };
                foreach (var sides in sets)
                    foreach (int ulps in new[] { -2, -1, 0, 1, 2, 3, 4, 5 })
                    {
                        // Real (positive) overlap through a face that is not solid is an ordinary collision handled by the
                        // other faces, not a contact; only touching/negative nudges apply there.
                        if (ulps > 0 && (sides & face) == 0) continue;
                        yield return new object[] { id, face, dx, dy, sides, ulps };
                    }
            }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Touching_ActorAtMostAFewUlpsIntoAFace_IsNotPushedAtAll(string id, SolidSides touched, float dx, float dy, SolidSides sides, int ulps)
    {
        var tiles = TileWithSides(sides);
        var spec = Spec(id);
        // Several positions along the face, both off-center, so the "other half" of the tile is exercised too.
        foreach (float along in new[] { -5f, -1.5f, 2f, 6f })
        {
            float x = 40f + dx * (8f + spec.HalfW) + (dx == 0f ? along : 0f);
            float y = 40f + dy * (8f + spec.HalfH) + (dy == 0f ? along : 0f);
            // Nudge toward the tile: ulps > 0 overlaps, ulps < 0 leaves a gap.
            for (int i = 0; i < System.Math.Abs(ulps); i++)
            {
                if (dx != 0f) x = ulps > 0 ? (dx > 0 ? MathF.BitDecrement(x) : MathF.BitIncrement(x)) : (dx > 0 ? MathF.BitIncrement(x) : MathF.BitDecrement(x));
                else y = ulps > 0 ? (dy > 0 ? MathF.BitDecrement(y) : MathF.BitIncrement(y)) : (dy > 0 ? MathF.BitIncrement(y) : MathF.BitDecrement(y));
            }

            var end = TileWorld.ResolveOnce(spec, tiles, x, y);

            MathF.Abs(end.X - x).ShouldBeLessThan(Tol, $"{id} {touched} sides={sides} ulps={ulps} along={along}: moved to ({end.X:0.####},{end.Y:0.####}) from ({x:R},{y:R})");
            MathF.Abs(end.Y - y).ShouldBeLessThan(Tol, $"{id} {touched} sides={sides} ulps={ulps} along={along}: moved to ({end.X:0.####},{end.Y:0.####}) from ({x:R},{y:R})");
        }
    }

    // Exactly the scenario the engine produces itself: take the position a push leaves behind (with its float residue)
    // and run another pass. It must not move, with no rounding applied. Complements
    // TileResolutionInvariantTests.Scan_SettledActor_IsStableUnderFurtherPasses, which rounds the rest position first.
    public static IEnumerable<object[]> ScanPairs() => TileResolutionActors.WellBehavedMapActorPairs();

    [Theory]
    [MemberData(nameof(ScanPairs))]
    public void Scan_RestPositionLeftByThePush_IsNotMovedByTheNextPass_WithoutRounding(string map, string actor)
    {
        var spec = TileResolutionActors.Get(actor);
        var tiles = TileWorld.Map(TileResolutionMaps.Get(map));
        var (w, h) = TileResolutionMaps.Size(map);
        var failures = new List<string>();

        foreach (var p in TileWorld.ScanPositions(-spec.HalfW, w * 16f + spec.HalfW, -spec.HalfH, h * 16f + spec.HalfH, 4f))
        {
            var (c, r) = TileWorld.CellOf(p.X, p.Y);
            if (TileWorld.HasTile(tiles, c, r) || !TileWorld.Penetrates(spec, p.X, p.Y, tiles)) continue;
            var passes = TileWorld.ResolvePasses(spec, tiles, p.X, p.Y, 6);
            int clearAt = passes.FindIndex(q => !TileWorld.Penetrates(spec, q.X, q.Y, tiles));
            if (clearAt < 0 || clearAt == passes.Count - 1) continue;

            var rest = passes[clearAt];
            var later = passes[^1];
            if (MathF.Abs(later.X - rest.X) > 0.011f || MathF.Abs(later.Y - rest.Y) > 0.011f)
                failures.Add($"{map}/{actor} start=({p.X:0.##},{p.Y:0.##}) rest=({rest.X:R},{rest.Y:R}) then ({later.X:0.###},{later.Y:0.###})");
        }

        failures.ShouldBeEmpty(string.Join(Environment.NewLine, failures.Take(5)));
    }
}
