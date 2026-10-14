using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

// Concrete scenes on small layouts: inside (concave) corners, L-shapes, T-junctions, plus shapes,
// ledges and pits. Expected positions are derived by hand from the tile geometry (16-unit cells,
// cell (c, r) spans x in [16c, 16c+16], y in [16r, 16r+16]).
public class TileResolutionLayoutTests
{
    private const float Tol = 1e-3f;

    private static void ShouldBeAt(Vector2 actual, float x, float y, string? because = null)
    {
        actual.X.ShouldBe(x, Tol, because);
        actual.Y.ShouldBe(y, Tol, because);
    }

    // ── Inside corners: L-shapes in all four orientations ──────────────────────────
    // The actor sits in the notch of the L, 2 units into the wall and 3 into the floor/ceiling. It must be
    // pushed out of both faces, and must not be shoved through the L's outer corner tile.

    // (id, layout rows, start, expected end). The notch cell is the one empty cell of the 2x2.
    public static IEnumerable<object[]> LNotchCases()
    {
        foreach (var half in new[] { 6f, 8f, 12f })
        {
            // Wall face and floor/ceiling face both meet at the 16,16 point (the notch's inner corner).
            // bottom-left L: notch top-right, floor y=16 below, wall x=16 on the left.
            yield return new object[] { "lBottomLeft", new[] { "#.", "##" }, 16f + half - 2f, 16f + half - 3f, 16f + half, 16f + half };
            // bottom-right L: notch top-left, floor below, wall x=16 on the right.
            yield return new object[] { "lBottomRight", new[] { ".#", "##" }, 16f - half + 2f, 16f + half - 3f, 16f - half, 16f + half };
            // top-left L: notch bottom-right, ceiling y=16 above, wall x=16 on the left.
            yield return new object[] { "lTopLeft", new[] { "##", "#." }, 16f + half - 2f, 16f - half + 3f, 16f + half, 16f - half };
            // top-right L: notch bottom-left, ceiling above, wall x=16 on the right.
            yield return new object[] { "lTopRight", new[] { "##", ".#" }, 16f - half + 2f, 16f - half + 3f, 16f - half, 16f - half };
        }
    }

    [Theory]
    [MemberData(nameof(LNotchCases))]
    public void LShape_RectInNotch_IsPushedOutOfBothFacesAndNotThroughTheCornerTile(
        string name, string[] rows, float startX, float startY, float endX, float endY)
    {
        var tiles = TileWorld.Map(rows);
        // Reverse engineer the half-size from the expected end: |end - 16| is the half extent on each axis.
        var spec = ActorSpec.Square(MathF.Abs(endX - 16f));

        var end = TileWorld.ResolveOnce(spec, tiles, startX, startY);

        ShouldBeAt(end, endX, endY, name);
    }

    [Theory]
    [MemberData(nameof(LNotchCases))]
    public void LShape_CircleInNotch_IsPushedOutOfBothFacesAndNotThroughTheCornerTile(
        string name, string[] rows, float startX, float startY, float endX, float endY)
    {
        var tiles = TileWorld.Map(rows);
        var spec = ActorSpec.Circle(MathF.Abs(endX - 16f));

        var end = TileWorld.ResolveOnce(spec, tiles, startX, startY);

        ShouldBeAt(end, endX, endY, name);
    }

    [Theory]
    [MemberData(nameof(LNotchCases))]
    public void LShape_PolyRectInNotch_IsPushedOutOfBothFacesAndNotThroughTheCornerTile(
        string name, string[] rows, float startX, float startY, float endX, float endY)
    {
        var tiles = TileWorld.Map(rows);
        var half = MathF.Abs(endX - 16f);
        var spec = ActorSpec.PolyRect(half, half);

        var end = TileWorld.ResolveOnce(spec, tiles, startX, startY);

        ShouldBeAt(end, endX, endY, name);
    }

    // ── Plus and T shapes: every inside corner behaves like an L notch ─────────────

    // Plus layout cells: top arm (1,2), left arm (0,1), center (1,1), right arm (2,1), bottom arm (1,0).
    public static IEnumerable<object[]> PlusNotches()
    {
        // (name, notch corner point (wall x, floor/ceiling y), sx = side of the wall relative to the actor, sy)
        // Notch at top-left cell (0,2): wall is the top arm's left face at x=16 (actor to the left), floor is the
        // left arm's top at y=32 (actor above).
        yield return new object[] { "topLeft", 16f, 32f, -1, +1 };
        yield return new object[] { "topRight", 32f, 32f, +1, +1 };
        yield return new object[] { "bottomLeft", 16f, 16f, -1, -1 };
        yield return new object[] { "bottomRight", 32f, 16f, +1, -1 };
    }

    [Theory]
    [MemberData(nameof(PlusNotches))]
    public void Plus_RectInEachNotch_IsPushedOutOfBothArms(string name, float cornerX, float cornerY, int sx, int sy)
    {
        var tiles = TileWorld.Map(".#.", "###", ".#.");
        float half = 6f;
        var spec = ActorSpec.Square(half);
        // Actor center is on the open side of both faces (sx, sy), embedded 2 into the wall and 3 into the floor/ceiling.
        float startX = cornerX + sx * (half - 2f), startY = cornerY + sy * (half - 3f);

        var end = TileWorld.ResolveOnce(spec, tiles, startX, startY);

        ShouldBeAt(end, cornerX + sx * half, cornerY + sy * half, name);
    }

    // T layout: bar on top row, stem below the middle. Notches sit under the bar on either side of the stem.
    [Theory]
    [InlineData(-1, 16f, 32f)] // left notch: stem left face x=16 ... actor to the left of the stem
    [InlineData(+1, 32f, 32f)] // right notch
    public void TJunction_RectUnderTheBarBesideTheStem_IsPushedOutOfBarAndStem(int sx, float stemFaceX, float barBottomY)
    {
        var tiles = TileWorld.Map("###", ".#.", ".#.");
        float half = 6f;
        var spec = ActorSpec.Square(half);
        // Embedded 2 into the stem face and 3 into the bar's underside.
        float startX = stemFaceX + sx * (half - 2f);
        float startY = barBottomY - half + 3f;

        var end = TileWorld.ResolveOnce(spec, tiles, startX, startY);

        ShouldBeAt(end, stemFaceX + sx * half, barBottomY - half);
    }

    [Theory]
    [InlineData(-1, 16f, 16f)]
    [InlineData(+1, 32f, 16f)]
    public void InvertedTJunction_RectOnTheBarBesideTheStem_IsPushedOutOfBarAndStem(int sx, float stemFaceX, float barTopY)
    {
        var tiles = TileWorld.Map(".#.", ".#.", "###");
        float half = 6f;
        var spec = ActorSpec.Square(half);
        float startX = stemFaceX + sx * (half - 2f);
        float startY = barTopY + half - 3f;

        var end = TileWorld.ResolveOnce(spec, tiles, startX, startY);

        ShouldBeAt(end, stemFaceX + sx * half, barTopY + half);
    }

    // ── Room: concave corners all around ────────────────────────────────────────

    [Theory]
    [InlineData(+1, +1)]
    [InlineData(+1, -1)]
    [InlineData(-1, +1)]
    [InlineData(-1, -1)]
    public void Room_RectInEachInsideCorner_IsPushedOutOfBothWalls(int cornerSx, int cornerSy)
    {
        // 7x5 room: interior x 16..96, y 16..64.
        var tiles = TileWorld.Map("#######", "#.....#", "#.....#", "#.....#", "#######");
        float half = 6f;
        var spec = ActorSpec.Square(half);
        float wallX = cornerSx > 0 ? 16f : 96f;   // the interior face the actor leans on
        float floorY = cornerSy > 0 ? 16f : 64f;
        int openX = cornerSx > 0 ? +1 : -1;       // interior side relative to that wall
        int openY = cornerSy > 0 ? +1 : -1;
        float startX = wallX + openX * (half - 2f), startY = floorY + openY * (half - 3f);

        var end = TileWorld.ResolveOnce(spec, tiles, startX, startY);

        ShouldBeAt(end, wallX + openX * half, floorY + openY * half);
    }

    // ── One-tile ledge ────────────────────────────────────────────────────────

    // Layout: floor (row 0, x 0..48) with a one-tile ledge on the right (cell (2,1): x 32..48, y 16..32).
    private static readonly string[] OneTileLedge = { "..#", "###" };

    [Fact]
    public void Ledge_ActorWalkingIntoTheLedgeWall_IsPushedBackByTheOverlapOnly()
    {
        var tiles = TileWorld.Map(OneTileLedge);
        var spec = ActorSpec.Square(6f);
        // Standing on the floor (bottom y=16, center y=22), 2 units into the ledge's left face at x=32.
        var end = TileWorld.ResolveOnce(spec, tiles, 32f - 6f + 2f, 22f);

        ShouldBeAt(end, 26f, 22f);
    }

    [Fact]
    public void Ledge_ActorPressedIntoTheLedgeWallAndFloor_IsPushedOutOfBoth()
    {
        var tiles = TileWorld.Map(OneTileLedge);
        var spec = ActorSpec.Square(6f);

        var end = TileWorld.ResolveOnce(spec, tiles, 28f, 22f - 1f);

        ShouldBeAt(end, 26f, 22f);
    }

    [Fact]
    public void Ledge_ActorLandingOnTopCentered_RestsOnTheLedgeTop()
    {
        var tiles = TileWorld.Map(OneTileLedge);
        var spec = ActorSpec.Square(6f);

        var end = TileWorld.ResolveOnce(spec, tiles, 40f, 32f + 6f - 3f);

        ShouldBeAt(end, 40f, 38f);
    }

    // Jumping up beside the ledge with the feet 3 below its top and only 2 units over it: the shallower
    // axis is the horizontal one, so the actor is shoved back off the ledge instead of mounting it.
    [Fact]
    public void Ledge_FeetJustBelowTheTopWithSmallHorizontalOverlap_IsShovedBackNotLiftedOntoIt()
    {
        var tiles = TileWorld.Map(OneTileLedge);
        var spec = ActorSpec.Square(6f);

        var end = TileWorld.ResolveOnce(spec, tiles, 32f - 6f + 2f, 32f + 6f - 3f);

        ShouldBeAt(end, 26f, 35f);
    }

    // Same height but with a deeper horizontal overlap (5 > 3): lifted onto the ledge.
    [Fact]
    public void Ledge_FeetJustBelowTheTopWithLargeHorizontalOverlap_IsLiftedOntoTheLedge()
    {
        var tiles = TileWorld.Map(OneTileLedge);
        var spec = ActorSpec.Square(6f);

        var end = TileWorld.ResolveOnce(spec, tiles, 32f - 6f + 5f, 32f + 6f - 3f);

        ShouldBeAt(end, 31f, 38f);
    }

    [Fact]
    public void Ledge_ActorPastTheLedgeEdgeStandingOnTheFloor_IsNotMoved()
    {
        var tiles = TileWorld.Map(OneTileLedge);
        var spec = ActorSpec.Square(6f);
        var start = new Vector2(32f - 6f, 22f); // touching the ledge face

        var end = TileWorld.ResolveOnce(spec, tiles, start.X, start.Y);

        end.ShouldBe(start);
    }

    // ── Stairs ───────────────────────────────────────────────────────────────

    // Heights: col0 = 16, col1 = 32, col2 = 48, col3 = 64.
    private static readonly string[] Stairs = { "...#", "..##", ".###", "####" };

    [Theory]
    [InlineData(8f, 16f)]   // middle of step 0
    [InlineData(24f, 32f)]  // middle of step 1
    [InlineData(40f, 48f)]  // middle of step 2
    [InlineData(56f, 64f)]  // middle of step 3
    public void Stairs_ActorEmbeddedOneUnitIntoAStepTop_IsLiftedOntoItWithoutMovingSideways(float x, float stepTop)
    {
        var tiles = TileWorld.Map(Stairs);
        var spec = ActorSpec.Square(4f); // 8 wide: fits inside a 16-wide step with margin on both sides

        var end = TileWorld.ResolveOnce(spec, tiles, x, stepTop + 4f - 1f);

        ShouldBeAt(end, x, stepTop + 4f);
    }

    // Straddling the riser: the actor overlaps the next step's wall by 2 and is pushed back along it.
    [Theory]
    [InlineData(0, 16f)]
    [InlineData(1, 32f)]
    [InlineData(2, 48f)]
    public void Stairs_ActorStandingOnAStepWithItsEdgeInTheRiser_IsPushedBackFromTheRiser(int step, float stepTop)
    {
        var tiles = TileWorld.Map(Stairs);
        var spec = ActorSpec.Square(4f);
        float riserX = 16f * (step + 1);

        var end = TileWorld.ResolveOnce(spec, tiles, riserX - 4f + 2f, stepTop + 4f);

        ShouldBeAt(end, riserX - 4f, stepTop + 4f);
    }

    // ── Pits ────────────────────────────────────────────────────────────────

    // One-tile-wide pit: walls (cols 0-1 and 3-4, rows 1-2), pit floor tile at (2, 0). Pit interior x 32..48, floor y=16.
    private static readonly string[] OneWidePit = { "##.##", "##.##", "#####" };

    [Theory]
    [InlineData(38f)]  // touching left wall
    [InlineData(40f)]  // centered
    [InlineData(42f)]  // touching right wall
    public void Pit_NarrowerActorInsideThePit_IsNotMoved(float x)
    {
        var tiles = TileWorld.Map(OneWidePit);
        var spec = ActorSpec.Square(6f);

        var end = TileWorld.ResolveOnce(spec, tiles, x, 16f + 6f + 10f);

        ShouldBeAt(end, x, 32f);
    }

    [Theory]
    [InlineData(34f, 38f)] // overlapping the left wall -> pushed right
    [InlineData(36f, 38f)]
    [InlineData(44f, 42f)] // overlapping the right wall -> pushed left
    [InlineData(46f, 42f)]
    public void Pit_NarrowerActorOverlappingAPitWall_IsPushedBackIntoThePit(float startX, float expectedX)
    {
        var tiles = TileWorld.Map(OneWidePit);
        var spec = ActorSpec.Square(6f);

        var end = TileWorld.ResolveOnce(spec, tiles, startX, 32f);

        ShouldBeAt(end, expectedX, 32f);
    }

    [Theory]
    [InlineData(40f)]
    [InlineData(38f)]
    [InlineData(42f)]
    public void Pit_ActorSinkingIntoThePitFloor_LandsOnTheFloorWithoutShiftingSideways(float x)
    {
        var tiles = TileWorld.Map(OneWidePit);
        var spec = ActorSpec.Square(6f);

        var end = TileWorld.ResolveOnce(spec, tiles, x, 22f - 3f);

        ShouldBeAt(end, x, 22f);
    }

    // Exactly as wide as the pit: any lateral offset is corrected back to the center.
    [Theory]
    [InlineData(38.5f)]
    [InlineData(39.5f)]
    [InlineData(40.5f)]
    [InlineData(41.5f)]
    public void Pit_ActorExactlyAsWideAsThePit_IsRecenteredByTheWalls(float x)
    {
        var tiles = TileWorld.Map(OneWidePit);
        var spec = ActorSpec.Square(8f);

        var end = TileWorld.ResolveOnce(spec, tiles, x, 16f + 8f + 10f);

        ShouldBeAt(end, 40f, 34f);
    }

    // Wider than the pit and descending onto its lips with a penetration shallower than the horizontal overlap:
    // lands on the lips.
    [Fact]
    public void Pit_WiderActorCenteredOverThePitWithShallowPenetration_RestsOnTheLips()
    {
        var tiles = TileWorld.Map(OneWidePit);
        var spec = ActorSpec.Square(10f); // 20 wide over a 16 pit: overlaps each lip by 2
        float lipTop = 48f;

        var end = TileWorld.ResolveOnce(spec, tiles, 40f, lipTop + 10f - 1f);

        ShouldBeAt(end, 40f, lipTop + 10f);
    }

    // Quirk: with the penetration deeper than the lip overlap, both lips push sideways toward each other (equal
    // and opposite) and the push that is evaluated first wins. The first pass shoves the actor 2 units right
    // even though the pit and the actor are symmetric; the second pass then lifts it onto the lips.
    [Fact]
    public void Quirk_Pit_WiderActorCenteredOverThePitWithDeeperPenetration_IsShovedRightThenLandsOnTheLips()
    {
        var tiles = TileWorld.Map(OneWidePit);
        var spec = ActorSpec.Square(10f);
        float lipTop = 48f;

        var passes = TileWorld.ResolvePasses(spec, tiles, 40f, lipTop + 10f - 3f, 3);

        ShouldBeAt(passes[0], 42f, lipTop + 10f - 3f);
        ShouldBeAt(passes[1], 42f, lipTop + 10f);
        ShouldBeAt(passes[2], 42f, lipTop + 10f);
    }

    // ── Diagonal-only neighbours ────────────────────────────────────────────────

    // Tiles A (cell 0,0) and B (cell 1,1) touch only at the corner (16,16). The cell (1,0) is open.
    private static readonly string[] DiagonalPair = { ".#", "#." };

    [Fact]
    public void DiagonalNeighbours_SquareInTheOpenCellBelowAndBesideBoth_IsPushedOutOfBoth()
    {
        var tiles = TileWorld.Map(DiagonalPair);
        var spec = ActorSpec.Square(6f);
        // Center (24, 8): right of A (x 0..16) and below B (y 16..32); 2 into A and 3 into B... set up overlaps
        // of 2 on A's right face and 3 on B's bottom face.
        var end = TileWorld.ResolveOnce(spec, tiles, 16f + 6f - 2f, 16f - 6f + 3f);

        ShouldBeAt(end, 22f, 10f);
    }

    // Quirk: a tall actor wedged under the upper diagonal tile is pushed sideways by that tile (its shallower
    // axis) straight into the lower tile, ending with its center inside tile A. A push down was available.
    [Fact]
    public void Quirk_DiagonalNeighbours_TallRectUnderTheUpperTile_IsPushedSidewaysIntoTheLowerTile()
    {
        var tiles = TileWorld.Map(DiagonalPair);
        var spec = ActorSpec.Rect(6f, 14f);

        var end = TileWorld.ResolveOnce(spec, tiles, 18f, 13f);

        // B (right-above) pushes left by its X overlap 8, because 8 < its Y overlap 11.
        ShouldBeAt(end, 10f, 13f);
        TileWorld.HasTile(tiles, 0, 0).ShouldBeTrue();
        var (cellX, cellY) = TileWorld.CellOf(end.X, end.Y);
        (cellX, cellY).ShouldBe((0, 0), "the resolved center is inside tile A");
    }

    // ── Float noise ──────────────────────────────────────────────────────────

    // After being pushed against a face the actor can sit with a sub-ULP overlap. That must read as touching: the
    // next pass leaves it where it is instead of pushing it sideways (it used to be shoved ~9.6 units, because the
    // directional separation rounded the near face to zero while the raw overlap test still reported a collision).
    // See TileResolutionTouchingContactTests for the focused cases.
    [Fact]
    public void OneUlpOverlapAfterAPush_IsNotMovedByTheNextPass()
    {
        // Plus layout, left arm is cell (3,4) when padded by 3; use the padded map from the catalog.
        var tiles = TileWorld.Map(TileResolutionMaps.Get("plus"));
        var spec = ActorSpec.Square(8f);

        var passes = TileWorld.ResolvePasses(spec, tiles, 49.61f, 58.37f, 2);

        passes[0].X.ShouldBe(49.61f, 1e-4f);
        passes[0].Y.ShouldBe(56f, 1e-3f); // pushed down against the arm's underside, with ULP-level residue
        passes[1].X.ShouldBe(passes[0].X, 1e-6f);
        passes[1].Y.ShouldBe(passes[0].Y, 1e-6f);
    }
}
