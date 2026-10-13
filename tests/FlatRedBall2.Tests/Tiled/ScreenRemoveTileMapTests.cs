using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using FlatRedBall2.Collision;
using FlatRedBall2.Tiled;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Tilemaps;
using MonoGame.Extended.Tilemaps.Rendering;
using Shouldly;
using Xunit;
using XnaVec2 = Microsoft.Xna.Framework.Vector2;

namespace FlatRedBall2.Tests.Tiled;

// A TileMap, its generated TileShapes and the relationships on them are removable at runtime,
// the way an entity is: nothing keeps drawing, ticking or colliding after Remove.
public class ScreenRemoveTileMapTests
{
    private class Marker : Entity { }
    private class TestScreen : Screen { }

    private static TestScreen NewScreen() => new TestScreen { Engine = new FlatRedBallService() };

    private static FrameTime Frame(float dt = 1f / 60f) =>
        new FrameTime(TimeSpan.FromSeconds(dt), TimeSpan.FromSeconds(dt), TimeSpan.Zero, TimeSpan.Zero);

    // 4x4 map with one "Solid" tile painted at the given TMX cell.
    private static Tilemap SolidTilemap(int col, int row)
    {
        var tilemap = new Tilemap("test", 4, 4, 16, 16, TilemapOrientation.Orthogonal);
        var tileset = new TilemapTileset("ts", null!, 16, 16, 1, 1) { FirstGlobalId = 1 };
        tileset.AddTileData(new TilemapTileData(0) { Class = "Solid" });
        tilemap.Tilesets.Add(tileset);
        var layer = new TilemapTileLayer("Main", 4, 4, 16, 16);
        layer.SetTile(col, row, new TilemapTile(globalId: 1));
        tilemap.Layers.Add(layer);
        return tilemap;
    }

    // An actor whose body overlaps the left edge of the one tile in `tiles`.
    private static Entity ActorOverlapping(TileShapes tiles)
    {
        var tile = tiles.Rectangles.Single();
        var actor = new Entity { X = tile.X - 6f, Y = tile.Y };
        actor.Add(new AARect { Width = 8f, Height = 8f });
        return actor;
    }

    [Fact]
    public void Remove_TileMap_RemovesLayerRenderablesFromRenderList()
    {
        var screen = NewScreen();
        var tilemap = SolidTilemap(0, 0);
        var tileLayer = (TilemapTileLayer)tilemap.Layers[0];
        var renderable = new TileMapLayerRenderable(new TilemapSpriteBatchRenderer(), tileLayer);
        var layers = new List<TileMapLayer> { new("Main", renderable, tileLayer, tilemap.Tilesets) };
        var map = new TileMap(64, 64, 16, 16, layers);
        screen.Add(map);
        screen.RenderList.ShouldContain(renderable);

        screen.Remove(map);

        screen.RenderList.ShouldNotContain(renderable);
    }

    [Fact]
    public void Remove_TileMap_ThenAddAgain_RestoresLayerRenderable()
    {
        var screen = NewScreen();
        var tilemap = SolidTilemap(0, 0);
        var tileLayer = (TilemapTileLayer)tilemap.Layers[0];
        var renderable = new TileMapLayerRenderable(new TilemapSpriteBatchRenderer(), tileLayer);
        var map = new TileMap(64, 64, 16, 16, new List<TileMapLayer> { new("Main", renderable, tileLayer, tilemap.Tilesets) });
        screen.Add(map);

        screen.Remove(map);
        screen.Add(map);

        screen.RenderList.Count(r => ReferenceEquals(r, renderable)).ShouldBe(1);
    }

    [Fact]
    public void Remove_TileMap_NeverAdded_DoesNotThrow()
    {
        var screen = NewScreen();

        Should.NotThrow(() => screen.Remove(new TileMap(SolidTilemap(0, 0))));
    }

    [Fact]
    public void Remove_TileMap_StopsLazySpawning()
    {
        var screen = NewScreen();
        var factory = new Factory<Marker>(screen) { LazySpawn = LazySpawnMode.OneShot };
        var map = LazyCoinMap(factory);
        screen.Add(map);

        screen.Remove(map);
        screen.Update(Frame());

        factory.Count.ShouldBe(0);
    }

    [Fact]
    public void Remove_TileMap_StopsTileAnimation()
    {
        var screen = NewScreen();
        var tilemap = new Tilemap("test", 2, 2, 16, 16, TilemapOrientation.Orthogonal);
        var tileset = new TilemapTileset("ts", null!, 16, 16, 2, 2) { FirstGlobalId = 1 };
        var anim = new TilemapTileAnimation(new[]
        {
            new TilemapTileAnimationFrame(0, 0.5f),
            new TilemapTileAnimationFrame(1, 0.5f),
        });
        tileset.AddTileData(new TilemapTileData(0) { Animation = anim });
        tilemap.Tilesets.Add(tileset);
        var layer = new TilemapTileLayer("Main", 2, 2, 16, 16);
        layer.SetTile(0, 0, new TilemapTile(globalId: 1));
        tilemap.Layers.Add(layer);
        var map = new TileMap(tilemap);
        screen.Add(map);

        screen.Remove(map);
        screen.Update(Frame(0.6f));

        anim.CurrentFrameIndex.ShouldBe(0);
    }

    [Fact]
    public void Remove_TileMap_RemovesGeneratedShapesFromRenderList()
    {
        var screen = NewScreen();
        var map = new TileMap(SolidTilemap(0, 0));
        var tiles = map.GenerateCollisionFromClass("Solid");
        var tile = tiles.Rectangles.Single();
        screen.Add(map);
        screen.Add(tiles);
        screen.RenderList.ShouldContain(tile);

        screen.Remove(map);

        screen.RenderList.ShouldNotContain(tile);
    }

    [Fact]
    public void Remove_TileMap_RemovesRelationshipsOnItsGeneratedShapes()
    {
        var screen = NewScreen();
        var map = new TileMap(SolidTilemap(0, 0));
        var tiles = map.GenerateCollisionFromClass("Solid");
        var actor = ActorOverlapping(tiles);
        screen.Register(actor);
        screen.AddCollisionRelationship(new[] { actor }, tiles).MoveFirstOnCollision();
        screen.Add(map);
        float startX = actor.X;

        screen.Remove(map);
        screen.Update(Frame());

        actor.X.ShouldBe(startX);
    }

    [Fact]
    public void Remove_TileMap_LeavesRelationshipsOnOtherShapes()
    {
        var screen = NewScreen();
        var removed = new TileMap(SolidTilemap(0, 0));
        var kept = new TileMap(SolidTilemap(3, 3));
        removed.GenerateCollisionFromClass("Solid");
        var keptTiles = kept.GenerateCollisionFromClass("Solid");
        var actor = ActorOverlapping(keptTiles);
        screen.Register(actor);
        screen.AddCollisionRelationship(new[] { actor }, keptTiles).MoveFirstOnCollision();
        float startX = actor.X;

        screen.Remove(removed);
        screen.Update(Frame());

        actor.X.ShouldBeLessThan(startX);
    }

    [Fact]
    public void Remove_TileMap_SwapForSecondMap_ActorCollidesWithSecondOnly()
    {
        var screen = NewScreen();
        var mapA = new TileMap(SolidTilemap(0, 0));
        var tilesA = mapA.GenerateCollisionFromClass("Solid");
        var mapB = new TileMap(SolidTilemap(3, 3));
        var tilesB = mapB.GenerateCollisionFromClass("Solid");
        var actorAtA = ActorOverlapping(tilesA);
        var actorAtB = ActorOverlapping(tilesB);
        screen.Register(actorAtA);
        screen.Register(actorAtB);
        screen.Add(mapA);
        screen.AddCollisionRelationship(new[] { actorAtA, actorAtB }, tilesA).MoveFirstOnCollision();
        float aStart = actorAtA.X, bStart = actorAtB.X;

        screen.Remove(mapA);
        screen.Add(mapB);
        screen.AddCollisionRelationship(new[] { actorAtA, actorAtB }, tilesB).MoveFirstOnCollision();
        screen.Update(Frame());

        actorAtA.X.ShouldBe(aStart);
        actorAtB.X.ShouldBeLessThan(bStart);
    }

    [Fact]
    public void Remove_TileMap_FromCollisionHandler_FinishesPassAndStopsCollidingAfter()
    {
        var screen = NewScreen();
        var map = new TileMap(SolidTilemap(0, 0));
        var tiles = map.GenerateCollisionFromClass("Solid");
        var actor = ActorOverlapping(tiles);
        var trigger = new Entity { X = actor.X, Y = actor.Y };
        trigger.Add(new AARect { Width = 8f, Height = 8f });
        screen.Register(actor);
        screen.Register(trigger);
        screen.Add(map);
        var tileRel = screen.AddCollisionRelationship(new[] { actor }, tiles);
        tileRel.MoveFirstOnCollision();
        var triggerRel = screen.AddCollisionRelationship(new[] { actor }, new[] { trigger });
        triggerRel.CollisionOccurred += (_, _) => screen.Remove(map);
        int tileHits = 0;
        tileRel.CollisionOccurred += (_, _) => tileHits++;

        Should.NotThrow(() => screen.Update(Frame()));
        int hitsAfterFirstFrame = tileHits;
        hitsAfterFirstFrame.ShouldBeGreaterThan(0);
        screen.Update(Frame());

        tileHits.ShouldBe(hitsAfterFirstFrame);
    }

    [Fact]
    public void Remove_TileMap_DuringLazySpawnTick_StillTicksOtherMaps()
    {
        var screen = NewScreen();
        var factory = new Factory<Marker>(screen) { LazySpawn = LazySpawnMode.OneShot };
        var first = LazyCoinMap(factory);
        var second = LazyCoinMap(factory);
        screen.Add(first);
        screen.Add(second);
        factory.EntitySpawned += _ => screen.Remove(first);

        screen.Update(Frame());

        factory.Count.ShouldBe(2);
    }

    private static TileMap LazyCoinMap(Factory<Marker> factory)
    {
        var tilemap = new Tilemap("test", 4, 4, 16, 16, TilemapOrientation.Orthogonal);
        var tileset = new TilemapTileset("ts", null!, 16, 16, 1, 1) { FirstGlobalId = 1 };
        tileset.AddTileData(new TilemapTileData(0) { Class = "Coin" });
        tilemap.Tilesets.Add(tileset);
        tilemap.Layers.Add(new TilemapTileLayer("Main", 4, 4, 16, 16));
        var objLayer = new TilemapObjectLayer("Entities");
        objLayer.AddObject(new TilemapTileObject(1, new XnaVec2(16f, 48f), new TilemapTile(globalId: 1), new XnaVec2(16, 16)));
        tilemap.Layers.Add(objLayer);
        var map = new TileMap(tilemap);
        map.CreateEntities("Coin", factory);
        return map;
    }

    [Fact]
    public void Remove_TileMap_ReleasesMapReference()
    {
        var screen = NewScreen();
        var weak = AddThenRemoveMap(screen);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        weak.IsAlive.ShouldBeFalse();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AddThenRemoveMap(Screen screen)
    {
        var map = new TileMap(SolidTilemap(0, 0));
        var tiles = map.GenerateCollisionFromClass("Solid");
        screen.Add(map);
        screen.Add(tiles);
        screen.Remove(map);
        return new WeakReference(map);
    }

    [Fact]
    public void Remove_TileMapLayer_RemovesOnlyThatLayerRenderable()
    {
        var screen = NewScreen();
        var tilemap = SolidTilemap(0, 0);
        var tileLayer = (TilemapTileLayer)tilemap.Layers[0];
        var other = new TilemapTileLayer("Other", 4, 4, 16, 16);
        var renderer = new TilemapSpriteBatchRenderer();
        var r1 = new TileMapLayerRenderable(renderer, tileLayer);
        var r2 = new TileMapLayerRenderable(renderer, other);
        var l1 = new TileMapLayer("Main", r1, tileLayer, tilemap.Tilesets);
        var l2 = new TileMapLayer("Other", r2, other, tilemap.Tilesets);
        screen.Add(l1);
        screen.Add(l2);

        screen.Remove(l1);

        screen.RenderList.ShouldNotContain(r1);
        screen.RenderList.ShouldContain(r2);
    }

    [Fact]
    public void Remove_TileShapes_RemovesTilesAndStopsSyncingNewOnes()
    {
        var screen = NewScreen();
        var tiles = new TileShapes { GridSize = 16f };
        tiles.AddTileAtCell(0, 0);
        screen.Add(tiles);
        var existing = tiles.Rectangles.Single();

        screen.Remove(tiles);
        tiles.AddTileAtCell(1, 0);

        screen.RenderList.ShouldNotContain(existing);
        screen.RenderList.Count.ShouldBe(0);
    }

    [Fact]
    public void Add_TileShapes_Twice_RegistersEachTileOnce()
    {
        var screen = NewScreen();
        var tiles = new TileShapes { GridSize = 16f };
        tiles.AddTileAtCell(0, 0);

        screen.Add(tiles);
        screen.Add(tiles);
        tiles.AddTileAtCell(1, 0);

        screen.RenderList.Count.ShouldBe(2);
    }
}
