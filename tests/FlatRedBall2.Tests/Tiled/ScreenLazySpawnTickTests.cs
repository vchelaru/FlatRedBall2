using System;
using FlatRedBall2.Rendering;
using FlatRedBall2.Tiled;
using MonoGame.Extended.Tilemaps;
using Shouldly;
using Xunit;
using XnaVec2 = Microsoft.Xna.Framework.Vector2;

namespace FlatRedBall2.Tests.Tiled;

public class ScreenLazySpawnTickTests
{
    private class Marker : Entity { }
    private class TestScreen : Screen { }

    private static FrameTime Frame(float dt) =>
        new FrameTime(TimeSpan.FromSeconds(dt), TimeSpan.Zero, TimeSpan.Zero);

    private static Tilemap BuildTilemap(int tileSize = 16)
    {
        var tilemap = new Tilemap("test", 4, 4, tileSize, tileSize, TilemapOrientation.Orthogonal);
        var tileset = new TilemapTileset("ts", null!, tileSize, tileSize, 1, 1)
        {
            FirstGlobalId = 1
        };
        tileset.AddTileData(new TilemapTileData(0) { Class = "Coin" });
        tilemap.Tilesets.Add(tileset);

        var tileLayer = new TilemapTileLayer("Main", 4, 4, tileSize, tileSize);
        tilemap.Layers.Add(tileLayer);

        var objLayer = new TilemapObjectLayer("Entities");
        objLayer.AddObject(new TilemapTileObject(
            id: 1,
            position: new XnaVec2(16f, 48f),
            tile: new TilemapTile(globalId: 1),
            size: new XnaVec2(16, 16)));
        tilemap.Layers.Add(objLayer);

        return tilemap;
    }

    [Fact]
    public void ScreenAddTileMap_TicksLazySpawnManager_OnUpdate()
    {
        var screen = new TestScreen { Engine = new FlatRedBallService() };
        var factory = new Factory<Marker>(screen) { LazySpawn = LazySpawnMode.OneShot };

        var tileMap = new TileMap(BuildTilemap());
        tileMap.CreateEntities("Coin", factory);
        screen.Add(tileMap);

        // Camera default at origin, OrthogonalWidth/Height 1280x720 — the placeholder at world
        // (24, -40) (center of cell col=1,row=2 with origin Y=0) is well within the camera rect.
        screen.Update(Frame(1f / 60f));

        factory.Count.ShouldBe(1);
    }

    [Fact]
    public void TwoCameras_PlacementVisibleOnlyToSecondCamera_Spawns()
    {
        var screen = new TestScreen { Engine = new FlatRedBallService() };
        var factory = new Factory<Marker>(screen) { LazySpawn = LazySpawnMode.OneShot };

        var tileMap = new TileMap(BuildTilemap());
        tileMap.CreateEntities("Coin", factory);
        screen.Add(tileMap);

        // Move primary camera far away — placement (24, -40) is invisible to it.
        screen.Cameras[0].X = 10000f;
        // Add second camera centered on the placement.
        var second = new Camera { X = 24f, Y = -40f };
        screen.Cameras.Add(second);

        screen.Update(Frame(1f / 60f));

        factory.Count.ShouldBe(1);
    }

    [Fact]
    public void ScreenAddTileMap_DoesNotSpawn_WhenCameraFar()
    {
        var screen = new TestScreen { Engine = new FlatRedBallService() };
        var factory = new Factory<Marker>(screen) { LazySpawn = LazySpawnMode.OneShot };

        var tileMap = new TileMap(BuildTilemap());
        tileMap.CreateEntities("Coin", factory);
        screen.Add(tileMap);

        // Move camera far from the placeholder so even the 1280-wide rect doesn't reach it.
        screen.Camera.X = 10000f;

        screen.Update(Frame(1f / 60f));

        factory.Count.ShouldBe(0);
    }
}
