using System;
using FlatRedBall2.Tiled;
using MonoGame.Extended.Tilemaps;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Tiled;

/// <summary>
/// Tiled tile animations advance only if the screen drives the renderer's Update each frame.
/// Runs headless: the MonoGame.Extended renderer needs no GraphicsDevice to load or update.
/// </summary>
public class ScreenTileAnimationTickTests
{
    private class TestScreen : Screen { }

    private static FrameTime Frame(float scaled, float? unscaled = null) =>
        new FrameTime(TimeSpan.FromSeconds(scaled), TimeSpan.FromSeconds(unscaled ?? scaled), TimeSpan.Zero, TimeSpan.Zero);

    // Two frames of 0.5s each; CurrentFrameIndex flips 0 -> 1 at 0.5s and wraps at 1.0s.
    private static (Tilemap map, TilemapTileAnimation anim) BuildAnimatedTilemap()
    {
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
        return (tilemap, anim);
    }

    private static TestScreen NewScreen() => new TestScreen { Engine = new FlatRedBallService() };

    [Fact]
    public void Update_AdvancesTileAnimation()
    {
        var screen = NewScreen();
        var (tilemap, anim) = BuildAnimatedTilemap();
        screen.Add(new TileMap(tilemap));

        screen.Update(Frame(0.3f));
        anim.CurrentFrameIndex.ShouldBe(0);
        screen.Update(Frame(0.3f));
        anim.CurrentFrameIndex.ShouldBe(1);
    }

    [Fact]
    public void Update_WhilePaused_FreezesTileAnimation()
    {
        var screen = NewScreen();
        var (tilemap, anim) = BuildAnimatedTilemap();
        screen.Add(new TileMap(tilemap));
        screen.PauseThisScreen();

        screen.Update(Frame(0.6f));
        anim.CurrentFrameIndex.ShouldBe(0);

        screen.UnpauseThisScreen();
        screen.Update(Frame(0.6f));
        anim.CurrentFrameIndex.ShouldBe(1);
    }

    [Fact]
    public void Update_UsesScaledDelta_NotWallClock()
    {
        var screen = NewScreen();
        var (tilemap, anim) = BuildAnimatedTilemap();
        screen.Add(new TileMap(tilemap));

        // Wall-clock 0.6s but TimeScale 0.5 => 0.3s of game time: still on frame 0.
        screen.Update(Frame(scaled: 0.3f, unscaled: 0.6f));
        anim.CurrentFrameIndex.ShouldBe(0);

        // TimeScale 0 freezes it entirely.
        screen.Update(Frame(scaled: 0f, unscaled: 0.6f));
        anim.CurrentFrameIndex.ShouldBe(0);

        // TimeScale 2 doubles it: 0.3 wall-clock => 0.6s of game time.
        screen.Update(Frame(scaled: 0.6f, unscaled: 0.3f));
        anim.CurrentFrameIndex.ShouldBe(1);
    }

    [Fact]
    public void TwoTileMapsSharingOneTilemap_AdvanceOnceNotTwice()
    {
        var screen = NewScreen();
        var (tilemap, anim) = BuildAnimatedTilemap();
        screen.Add(new TileMap(tilemap));
        screen.Add(new TileMap(tilemap));

        // Animation state lives on the shared tile data; double-ticking would reach 0.6s => frame 1.
        screen.Update(Frame(0.3f));
        anim.CurrentFrameIndex.ShouldBe(0);
    }

    [Fact]
    public void AddingSameTileMapTwice_AdvancesOnce()
    {
        var screen = NewScreen();
        var (tilemap, anim) = BuildAnimatedTilemap();
        var map = new TileMap(tilemap);
        screen.Add(map);
        screen.Add(map);

        screen.Update(Frame(0.3f));
        anim.CurrentFrameIndex.ShouldBe(0);
    }

    [Fact]
    public void TwoTileMapsWithSeparateTilemaps_EachAdvance()
    {
        var screen = NewScreen();
        var (tilemapA, animA) = BuildAnimatedTilemap();
        var (tilemapB, animB) = BuildAnimatedTilemap();
        screen.Add(new TileMap(tilemapA));
        screen.Add(new TileMap(tilemapB));

        screen.Update(Frame(0.6f));
        animA.CurrentFrameIndex.ShouldBe(1);
        animB.CurrentFrameIndex.ShouldBe(1);
    }

    [Fact]
    public void NewScreen_DoesNotTickMapsFromOldScreen()
    {
        var oldScreen = NewScreen();
        var (tilemap, anim) = BuildAnimatedTilemap();
        oldScreen.Add(new TileMap(tilemap));

        var newScreen = NewScreen();
        newScreen.Update(Frame(0.6f));
        anim.CurrentFrameIndex.ShouldBe(0);
    }

    [Fact]
    public void ReloadTilemap_KeepsAnimating()
    {
        var screen = NewScreen();
        var (tilemap, anim) = BuildAnimatedTilemap();
        var map = new TileMap(tilemap);
        screen.Add(map);

        map.TryReload(BuildAnimatedTilemap().map).ShouldBeTrue();

        screen.Update(Frame(0.6f));
        anim.CurrentFrameIndex.ShouldBe(1);
    }

    [Fact]
    public void TileMapNotAddedToScreen_DoesNotAnimate()
    {
        var screen = NewScreen();
        var (tilemap, anim) = BuildAnimatedTilemap();
        _ = new TileMap(tilemap);

        screen.Update(Frame(0.6f));
        anim.CurrentFrameIndex.ShouldBe(0);
    }
}
