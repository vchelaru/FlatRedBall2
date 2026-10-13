using System.Linq;
using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

public class TileShapesSurroundedOutwardTests
{
    private const float Grid = 16f;

    // Solid block of cells [colMin..colMax] x [rowMin..rowMax].
    private static TileShapes Block(int colMin, int rowMin, int colMax, int rowMax)
    {
        var tiles = new TileShapes { GridSize = Grid };
        for (int c = colMin; c <= colMax; c++)
            for (int r = rowMin; r <= rowMax; r++)
                tiles.AddTileAtCell(c, r);
        return tiles;
    }

    private static (Entity actor, CollisionRelationship<Entity, TileShapes> rel) Actor(
        TileShapes tiles, IAttachable shape, float x, float y)
    {
        var actor = new Entity();
        actor.Add(shape);
        actor.X = x; actor.Y = y;
        var rel = new CollisionRelationship<Entity, TileShapes>(new[] { actor }, new[] { tiles });
        rel.MoveFirstOnCollision();
        return (actor, rel);
    }

    private static int RunUntilFree(Entity actor, CollisionRelationship<Entity, TileShapes> rel, TileShapes tiles, int maxFrames)
    {
        for (int i = 1; i <= maxFrames; i++)
        {
            rel.RunCollisions();
            if (!actor.CollidesWith(tiles)) return i;
        }
        return -1;
    }

    // ── Without the pass ─────────────────────────────────────────────────────

    [Fact]
    public void WithoutPass_CircleDeepInBlock_StaysStuck()
    {
        var tiles = Block(0, 0, 6, 6);
        var (actor, rel) = Actor(tiles, new Circle { Radius = 10f }, 22f, 56f);

        rel.RunCollisions();

        actor.X.ShouldBe(22f);
        actor.Y.ShouldBe(56f);
    }

    // ── After the pass ───────────────────────────────────────────────────────

    // Directions spread one tile layer per frame, so an actor 1.5 tiles deep needs a few frames.
    [Fact]
    public void AfterPass_CircleDeepInBlock_EndsOutsideBlock()
    {
        var tiles = Block(0, 0, 6, 6);
        tiles.AssignSurroundedTilesSolidOutward();
        var (actor, rel) = Actor(tiles, new Circle { Radius = 10f }, 22f, 56f);

        RunUntilFree(actor, rel, tiles, maxFrames: 4).ShouldBeGreaterThan(0);

        actor.X.ShouldBeLessThan(0f); // nearest exit is the left face
    }

    [Fact]
    public void AfterPass_AARectDeepInBlock_EndsOutsideBlock()
    {
        var tiles = Block(0, 0, 6, 6);
        tiles.AssignSurroundedTilesSolidOutward();
        var (actor, rel) = Actor(tiles, new AARect { Width = 12f, Height = 12f }, 22f, 56f);

        RunUntilFree(actor, rel, tiles, maxFrames: 4).ShouldBeGreaterThan(0);

        actor.X.ShouldBeLessThan(0f);
    }

    // Bodies smaller than a tile end each hop with their center in the inner half of the next
    // tile; that must not stall the push.
    [Theory]
    [InlineData(6f)]
    [InlineData(8f)]
    public void AfterPass_BodySmallerThanATile_AARect_EndsOutsideBlock(float halfSize)
    {
        var tiles = Block(0, 0, 6, 6);
        tiles.AssignSurroundedTilesSolidOutward();
        var (actor, rel) = Actor(tiles, new AARect { Width = halfSize * 2f, Height = halfSize * 2f }, 22f, 56f);

        RunUntilFree(actor, rel, tiles, maxFrames: 6).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void AfterPass_BodySmallerThanATile_Circle_EndsOutsideBlock()
    {
        var tiles = Block(0, 0, 6, 6);
        tiles.AssignSurroundedTilesSolidOutward();
        var (actor, rel) = Actor(tiles, new Circle { Radius = 6f }, 22f, 56f);

        RunUntilFree(actor, rel, tiles, maxFrames: 6).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void WithoutPass_AARectDeepInBlock_StaysStuck()
    {
        var tiles = Block(0, 0, 6, 6);
        var (actor, rel) = Actor(tiles, new AARect { Width = 12f, Height = 12f }, 22f, 56f);

        rel.RunCollisions();

        actor.X.ShouldBe(22f);
        actor.Y.ShouldBe(56f);
    }

    [Fact]
    public void AfterPass_CircleAtBlockCenter_EventuallyLeavesBlock()
    {
        var tiles = Block(0, 0, 6, 6);
        tiles.AssignSurroundedTilesSolidOutward();
        var (actor, rel) = Actor(tiles, new Circle { Radius = 6f }, 56f, 56f);

        RunUntilFree(actor, rel, tiles, maxFrames: 10).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void AfterPass_NoSurroundedTiles_SolidSidesUnchanged()
    {
        // A 2-thick wall: every tile touches open space on at least one side.
        var tiles = Block(0, 0, 9, 1);
        var before = tiles.Rectangles.Select(r => r.SolidSides).ToList();

        tiles.AssignSurroundedTilesSolidOutward();

        tiles.Rectangles.Select(r => r.SolidSides).ToList().ShouldBe(before);
    }

    [Fact]
    public void AfterPass_EmptyCollection_DoesNotThrow()
    {
        Should.NotThrow(() => new TileShapes().AssignSurroundedTilesSolidOutward());
    }

    [Fact]
    public void AfterPass_EveryTileInBlockHasAtLeastOneSolidSide()
    {
        var tiles = Block(0, 0, 6, 6);

        tiles.AssignSurroundedTilesSolidOutward();

        tiles.Rectangles.ShouldAllBe(r => r.SolidSides != SolidSides.None);
    }

    [Fact]
    public void AfterPass_NonEmbeddedCircleSlidingAlongFlatWall_DoesNotSnag()
    {
        // 3-thick wall, so the middle row is fully surrounded (None) before the pass.
        var tiles = Block(0, 0, 9, 2);
        tiles.AssignSurroundedTilesSolidOutward();

        // Wall top is y = 48. Circle centre 1 unit inside the push-out distance, sliding right.
        var (actor, rel) = Actor(tiles, new Circle { Radius = 10f }, 20f, 57f);
        for (float x = 20f; x <= 140f; x += 4f)
        {
            actor.X = x; actor.Y = 57f;
            rel.RunCollisions();

            actor.X.ShouldBe(x, 0.001f, $"sideways push at x={x}");
            actor.Y.ShouldBe(58f, 0.001f, $"unexpected vertical result at x={x}");
        }
    }

    [Fact]
    public void AfterPass_NonEmbeddedRectSlidingAlongFlatWall_DoesNotSnag()
    {
        var tiles = Block(0, 0, 9, 2);
        tiles.AssignSurroundedTilesSolidOutward();

        var (actor, rel) = Actor(tiles, new AARect { Width = 10f, Height = 10f }, 20f, 52f);
        for (float x = 20f; x <= 140f; x += 4f)
        {
            actor.X = x; actor.Y = 52f;
            rel.RunCollisions();

            actor.X.ShouldBe(x, 0.001f, $"sideways push at x={x}");
            actor.Y.ShouldBe(53f, 0.001f, $"unexpected vertical result at x={x}");
        }
    }

    // ── Map bounds ───────────────────────────────────────────────────────────

    // 5x5 block against the left edge of a 10x10-cell map (world 0..160).
    private static TileShapes EdgeBlock() => Block(0, 2, 4, 6);

    [Fact]
    public void WithoutBounds_DeepActorNextToMapEdge_IsPushedOffMap()
    {
        var tiles = EdgeBlock();
        tiles.AssignSurroundedTilesSolidOutward();
        var (actor, rel) = Actor(tiles, new Circle { Radius = 6f }, 22f, 70f);

        RunUntilFree(actor, rel, tiles, maxFrames: 10).ShouldBeGreaterThan(0);

        actor.X.ShouldBeLessThan(0f);
    }

    [Fact]
    public void WithBounds_DeepActorNextToMapEdge_IsNeverPushedOffMap()
    {
        var tiles = EdgeBlock();
        // Normal directions first: left-column tiles keep Left (it points off-map), as the
        // pass leaves non-surrounded tiles alone.
        tiles.AssignSurroundedTilesSolidOutward(mapLeft: 0f, mapTop: 160f, mapRight: 160f, mapBottom: 0f);
        var (actor, rel) = Actor(tiles, new Circle { Radius = 6f }, 22f, 70f);

        int frames = RunUntilFree(actor, rel, tiles, maxFrames: 10);

        frames.ShouldBeGreaterThan(0);
        actor.X.ShouldBeGreaterThanOrEqualTo(0f);
        (actor.Y < 32f || actor.Y > 112f).ShouldBeTrue("should exit through the top or bottom face");
    }

    [Fact]
    public void WithBounds_NoTileInheritsADirectionThatLeadsOffMap()
    {
        var tiles = EdgeBlock();

        tiles.AssignSurroundedTilesSolidOutward(mapLeft: 0f, mapTop: 160f, mapRight: 160f, mapBottom: 0f);

        // Interior tiles (cols 1..3) must not gain Left: the only exit that way is off-map.
        for (int c = 1; c <= 3; c++)
            for (int r = 3; r <= 5; r++)
                (tiles.GetTileAtCell(c, r)!.SolidSides & SolidSides.Left).ShouldBe(SolidSides.None, $"cell ({c},{r})");
    }

    // ── Corner-clip limit (FRB1 RepositionHalfSize) ──────────────────────────

    [Fact]
    public void AfterPass_FilledInTileInSolidBlock_IsNotLimitedToHalf()
    {
        var tiles = Block(0, 0, 6, 6);

        tiles.AssignSurroundedTilesSolidOutward();

        tiles.GetTileAtCell(3, 3)!.RepositionHalfSize.ShouldBeFalse();
        tiles.GetTileAtCell(0, 3)!.RepositionHalfSize.ShouldBeFalse(); // neighbor of a filled-in tile
    }

    [Fact]
    public void AfterPass_LShapedCornerTile_KeepsTheHalfLimit()
    {
        // Plus shape: the center is surrounded and every diagonal around it is empty (L-shaped).
        var tiles = new TileShapes { GridSize = Grid };
        foreach (var (c, r) in new[] { (1, 1), (0, 1), (2, 1), (1, 0), (1, 2) })
            tiles.AddTileAtCell(c, r);

        tiles.AssignSurroundedTilesSolidOutward();

        tiles.GetTileAtCell(1, 1)!.SolidSides.ShouldNotBe(SolidSides.None);
        tiles.GetTileAtCell(1, 1)!.RepositionHalfSize.ShouldBeTrue();
        tiles.GetTileAtCell(0, 1)!.RepositionHalfSize.ShouldBeFalse();
    }

    [Fact]
    public void WithoutPass_AllTilesKeepTheHalfLimit()
    {
        var tiles = Block(0, 0, 6, 6);

        tiles.Rectangles.ShouldAllBe(r => r.RepositionHalfSize);
    }

    [Fact]
    public void AddingATileAfterPass_ResetsTheHalfLimitOnNeighbors()
    {
        var tiles = Block(0, 0, 6, 6);
        tiles.AssignSurroundedTilesSolidOutward();

        tiles.AddTileAtCell(7, 3);

        tiles.GetTileAtCell(6, 3)!.RepositionHalfSize.ShouldBeTrue();
    }
}
