using System;
using System.Collections.Generic;
using System.Linq;
using FlatRedBall2.Collision;
using FlatRedBall2.Tiled;
using MonoGame.Extended.Tilemaps;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Tiled;

// Map swap: one TileShapes owned by the game is cleared and refilled from each new map, so the
// collision relationships holding it never have to be replaced.
public class TileMapRefillTests
{
    private class TestScreen : Screen { }

    private static TestScreen NewScreen() => new TestScreen { Engine = new FlatRedBallService() };

    private static FrameTime Frame(float dt = 1f / 60f) =>
        new FrameTime(TimeSpan.FromSeconds(dt), TimeSpan.FromSeconds(dt), TimeSpan.Zero, TimeSpan.Zero);

    // 4x4 map with one "Solid" tile (also carrying property "Hard") at the given TMX cell.
    private static Tilemap SolidTilemap(int col, int row, int tileSize = 16)
    {
        var tilemap = new Tilemap("test", 4, 4, tileSize, tileSize, TilemapOrientation.Orthogonal);
        var tileset = new TilemapTileset("ts", null!, tileSize, tileSize, 1, 1) { FirstGlobalId = 1 };
        var data = new TilemapTileData(0) { Class = "Solid" };
        data.Properties.SetBool("Hard", true);
        tileset.AddTileData(data);
        tilemap.Tilesets.Add(tileset);
        var layer = new TilemapTileLayer("Main", 4, 4, tileSize, tileSize);
        layer.SetTile(col, row, new TilemapTile(globalId: 1));
        tilemap.Layers.Add(layer);
        return tilemap;
    }

    private static TileMap SolidMap(int col, int row) => new TileMap(SolidTilemap(col, row));

    // A fresh actor whose body overlaps the left edge of the 16px tile at the given TMX cell.
    private static Entity ActorAt(int col, int row)
    {
        var scratch = new TileShapes();
        SolidMap(col, row).GenerateCollisionFromClass(scratch, "Solid");
        var tile = scratch.Rectangles.Single();
        var actor = new Entity { X = tile.X - 6f, Y = tile.Y };
        actor.Add(new AARect { Width = 8f, Height = 8f });
        return actor;
    }

    [Fact]
    public void Refill_FromMap_RelationshipCollidesAndHandlerFires()
    {
        var screen = NewScreen();
        var tiles = new TileShapes();
        SolidMap(0, 0).GenerateCollisionFromClass(tiles, "Solid");
        var actor = ActorAt(0, 0);
        screen.Register(actor);
        int hits = 0;
        var rel = screen.AddCollisionRelationship(new[] { actor }, tiles);
        rel.MoveFirstOnCollision();
        rel.CollisionOccurred += (_, _) => hits++;

        screen.Update(Frame());

        hits.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void ClearAndRefill_FromSecondMap_CollidesWithSecondOnly_HandlerAndResponseIntact()
    {
        var screen = NewScreen();
        var tiles = new TileShapes();
        SolidMap(0, 0).GenerateCollisionFromClass(tiles, "Solid");
        var actorAtA = ActorAt(0, 0);
        var actorAtB = ActorAt(3, 3);
        screen.Register(actorAtA);
        screen.Register(actorAtB);
        var rel = screen.AddCollisionRelationship(new[] { actorAtA, actorAtB }, tiles);
        rel.MoveFirstOnCollision();
        var hit = new List<Entity>();
        rel.CollisionOccurred += (a, _) => hit.Add(a);
        float aStart = actorAtA.X, bStart = actorAtB.X;

        tiles.Clear();
        SolidMap(3, 3).GenerateCollisionFromClass(tiles, "Solid");
        screen.Update(Frame());

        actorAtA.X.ShouldBe(aStart);
        actorAtB.X.ShouldBeLessThan(bStart);
        hit.ShouldBe(new[] { actorAtB });
    }

    [Fact]
    public void ClearAndRefill_RenderList_HoldsOnlyNewMapsShapes()
    {
        var screen = NewScreen();
        var tiles = new TileShapes();
        SolidMap(0, 0).GenerateCollisionFromClass(tiles, "Solid");
        screen.Add(tiles);
        var tileA = tiles.Rectangles.Single();
        screen.RenderList.ShouldContain(tileA);

        tiles.Clear();
        SolidMap(3, 3).GenerateCollisionFromClass(tiles, "Solid");

        screen.RenderList.ShouldNotContain(tileA);
        screen.RenderList.ShouldContain(tiles.Rectangles.Single());
        screen.RenderList.Count.ShouldBe(1);
    }

    [Fact]
    public void ClearAndRefill_PairTouchingClearedShape_EndsOnce()
    {
        var screen = NewScreen();
        var tiles = new TileShapes();
        SolidMap(0, 0).GenerateCollisionFromClass(tiles, "Solid");
        var actor = ActorAt(0, 0);
        screen.Register(actor);
        int started = 0, ended = 0;
        var rel = screen.AddCollisionRelationship(new[] { actor }, tiles);
        rel.CollisionStarted += (_, _) => started++;
        rel.CollisionEnded += (_, _) => ended++;
        screen.Update(Frame());
        started.ShouldBe(1);

        tiles.Clear();
        SolidMap(3, 3).GenerateCollisionFromClass(tiles, "Solid");
        screen.Update(Frame());
        screen.Update(Frame());

        ended.ShouldBe(1);
        started.ShouldBe(1);
    }

    [Fact]
    public void ClearAndRefill_SameMapTwice_HoldsOneCopyAndFollowsMapOnce()
    {
        var map = SolidMap(1, 1);
        var tiles = new TileShapes();
        map.GenerateCollisionFromClass(tiles, "Solid");
        tiles.Clear();
        map.GenerateCollisionFromClass(tiles, "Solid");
        float x0 = tiles.Rectangles.Single().X;

        map.X += 100f;

        tiles.Rectangles.Single().X.ShouldBe(x0 + 100f);
    }

    [Fact]
    public void Refill_FromSecondMap_FirstMapNoLongerMovesOrReloadsIt()
    {
        var mapA = SolidMap(0, 0);
        var tiles = new TileShapes();
        mapA.GenerateCollisionFromClass(tiles, "Solid");
        tiles.Clear();
        SolidMap(3, 3).GenerateCollisionFromClass(tiles, "Solid");
        var tile = tiles.Rectangles.Single();
        float x0 = tile.X;

        mapA.X += 100f;
        mapA.TryReload(SolidTilemap(0, 0)).ShouldBeTrue();

        tiles.Rectangles.Single().ShouldBeSameAs(tile);
        tile.X.ShouldBe(x0);
    }

    [Fact]
    public void Refill_FromSecondMap_FollowsSecondMapPosition()
    {
        var tiles = new TileShapes();
        SolidMap(0, 0).GenerateCollisionFromClass(tiles, "Solid");
        tiles.Clear();
        var mapB = SolidMap(3, 3);
        mapB.GenerateCollisionFromClass(tiles, "Solid");
        float x0 = tiles.Rectangles.Single().X;

        mapB.X += 40f;

        tiles.Rectangles.Single().X.ShouldBe(x0 + 40f);
    }

    [Fact]
    public void Refill_PlacesShapesAtMapsCurrentPosition()
    {
        var reference = SolidMap(2, 2);
        reference.X = 50f;
        reference.Y = 30f;
        var expected = reference.GenerateCollisionFromClass("Solid").Rectangles.Single();
        var map = SolidMap(2, 2);
        map.X = 50f;
        map.Y = 30f;
        var tiles = new TileShapes();

        map.GenerateCollisionFromClass(tiles, "Solid");

        var tile = tiles.Rectangles.Single();
        tile.X.ShouldBe(expected.X);
        tile.Y.ShouldBe(expected.Y);
    }

    [Fact]
    public void Refill_FromMapWithDifferentTileSize_AdoptsGridSize()
    {
        var tiles = new TileShapes();
        SolidMap(0, 0).GenerateCollisionFromClass(tiles, "Solid");
        tiles.Clear();

        new TileMap(SolidTilemap(0, 0, tileSize: 32)).GenerateCollisionFromClass(tiles, "Solid");

        tiles.GridSize.ShouldBe(32f);
        tiles.Rectangles.Single().Width.ShouldBe(32f);
    }

    [Fact]
    public void Refill_IntoNonEmptyCollection_Throws()
    {
        var map = SolidMap(0, 0);
        var tiles = new TileShapes();
        map.GenerateCollisionFromClass(tiles, "Solid");

        Should.Throw<InvalidOperationException>(() => map.GenerateCollisionFromClass(tiles, "Solid"));
    }

    [Fact]
    public void Clear_EmptyCollection_DoesNotThrow()
    {
        var tiles = new TileShapes();

        Should.NotThrow(() => tiles.Clear());
        tiles.Rectangles.ShouldBeEmpty();
    }

    [Fact]
    public void Refill_ByProperty_FillsAndTracks()
    {
        var map = SolidMap(1, 1);
        var tiles = new TileShapes();

        map.GenerateCollisionFromProperty(tiles, "Hard");
        float x0 = tiles.Rectangles.Single().X;
        map.X += 10f;

        tiles.Rectangles.Single().X.ShouldBe(x0 + 10f);
    }

    [Fact]
    public void Refill_WithLayerName_RestrictsToThatLayer()
    {
        var map = SolidMap(1, 1);
        var tiles = new TileShapes();

        map.GenerateCollisionFromClass(tiles, "Solid", "Main");

        tiles.Rectangles.Count().ShouldBe(1);
    }

    [Fact]
    public void Refill_ThenMapTryReload_RebuildsTheCallersCollectionInPlace()
    {
        var map = SolidMap(0, 0);
        var tiles = new TileShapes();
        map.GenerateCollisionFromClass(tiles, "Solid");
        var before = tiles.Rectangles.Single();

        map.TryReload(SolidTilemap(2, 2)).ShouldBeTrue();

        var after = tiles.Rectangles.Single();
        after.ShouldNotBeSameAs(before);
        after.X.ShouldNotBe(before.X);
    }

    [Fact]
    public void Refill_FromIsometricMap_Throws()
    {
        var map = new TileMap(new Tilemap("iso", 4, 4, 64, 32, TilemapOrientation.Isometric));

        Should.Throw<NotSupportedException>(() => map.GenerateCollisionFromClass(new TileShapes(), "Solid"));
    }
}
