using System;
using FlatRedBall2.Tiled;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Tilemaps;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Tiled;

// Tests for the TileMap resource cache, ParseTmx, and the (Tilemap, GraphicsDevice)
// constructor added in the content-prefetch PR.
public class TileMapResourceCacheTests
{
    private static Tilemap CreateFakeTilemap(
        int width = 2, int height = 2,
        int tileWidth = 16, int tileHeight = 16,
        string? layerName = "Main")
    {
        var tilemap = new Tilemap(
            name: "fake", width: width, height: height,
            tileWidth: tileWidth, tileHeight: tileHeight,
            orientation: TilemapOrientation.Orthogonal);
        if (layerName is not null)
            tilemap.Layers.Add(new TilemapTileLayer(layerName, width, height, tileWidth, tileHeight));
        return tilemap;
    }

    // -- ParseTmx --

    [Fact]
    public void ParseTmx_DelegatesToTmxLoader_WithProvidedPath()
    {
        var originalLoader = TileMap.TmxLoader;
        try
        {
            string? observedPath = null;
            GraphicsDevice? observedDevice = null;
            var fake = CreateFakeTilemap();

            TileMap.TmxLoader = (path, device) =>
            {
                observedPath = path;
                observedDevice = device;
                return fake;
            };

            // graphicsDevice is null here — the seam doesn't use it in this test.
            var result = TileMap.ParseTmx("Content/Maps/level1.tmx", graphicsDevice: null!);

            observedPath.ShouldBe("Content/Maps/level1.tmx");
            observedDevice.ShouldBeNull();
            result.ShouldBeSameAs(fake);
        }
        finally
        {
            TileMap.TmxLoader = originalLoader;
        }
    }

    [Fact]
    public void ParseTmx_ReturnsTilemap_FromLoader()
    {
        var originalLoader = TileMap.TmxLoader;
        try
        {
            var fake = CreateFakeTilemap(width: 5, height: 3, tileWidth: 32, tileHeight: 32);
            TileMap.TmxLoader = (_, _) => fake;

            var result = TileMap.ParseTmx("any.tmx", graphicsDevice: null!);

            result.Width.ShouldBe(5);
            result.Height.ShouldBe(3);
        }
        finally
        {
            TileMap.TmxLoader = originalLoader;
        }
    }

    // -- InvalidateResourceCache --

    [Fact]
    public void InvalidateResourceCache_ClearsCache_NextLoadCallsLoaderAgain()
    {
        var originalLoader = TileMap.TmxLoader;
        try
        {
            int callCount = 0;
            TileMap.TmxLoader = (_, _) =>
            {
                callCount++;
                return CreateFakeTilemap();
            };

            // First call — loader invoked.
            TileMap.ParseTmx("test.tmx", graphicsDevice: null!);
            callCount.ShouldBe(1);

            // Second call — still invoked (ParseTmx always delegates to TmxLoader;
            // the byte-level cache lives inside DefaultTmxLoader's CachingResolve).
            TileMap.ParseTmx("test.tmx", graphicsDevice: null!);
            callCount.ShouldBe(2);

            // InvalidateResourceCache clears the internal byte cache. With a replaced
            // TmxLoader the byte cache isn't exercised, but the method must not throw
            // and must be callable at any time.
            TileMap.InvalidateResourceCache();

            TileMap.ParseTmx("test.tmx", graphicsDevice: null!);
            callCount.ShouldBe(3);
        }
        finally
        {
            TileMap.TmxLoader = originalLoader;
        }
    }

    [Fact]
    public void InvalidateResourceCache_CanBeCalledMultipleTimes()
    {
        var originalLoader = TileMap.TmxLoader;
        try
        {
            TileMap.TmxLoader = (_, _) => CreateFakeTilemap();

            // Must not throw even when called repeatedly with nothing loaded.
            TileMap.InvalidateResourceCache();
            TileMap.InvalidateResourceCache();
            TileMap.InvalidateResourceCache();

            // Subsequent load still works.
            var result = TileMap.ParseTmx("x.tmx", graphicsDevice: null!);
            result.ShouldNotBeNull();
        }
        finally
        {
            TileMap.TmxLoader = originalLoader;
        }
    }

    // -- TileMap(Tilemap, GraphicsDevice) constructor --

    [Fact]
    public void Constructor_FromTilemap_SetsWidthAndHeight()
    {
        var fake = CreateFakeTilemap(width: 10, height: 8, tileWidth: 32, tileHeight: 16);

        // The internal (Tilemap) ctor doesn't need a GraphicsDevice.
        var tileMap = new TileMap(fake);

        tileMap.Width.ShouldBe(10 * 32);
        tileMap.Height.ShouldBe(8 * 16);
    }

    [Fact]
    public void Constructor_FromTilemap_SetsTileDimensions()
    {
        var fake = CreateFakeTilemap(tileWidth: 64, tileHeight: 48);

        var tileMap = new TileMap(fake);

        tileMap.TileWidth.ShouldBe(64);
        tileMap.TileHeight.ShouldBe(48);
    }

    [Fact]
    public void Constructor_FromTilemap_PopulatesLayers()
    {
        var fake = CreateFakeTilemap(layerName: "Ground");

        var tileMap = new TileMap(fake);

        tileMap.Layers.Count.ShouldBe(1);
        tileMap.Layers[0].Name.ShouldBe("Ground");
    }

    [Fact]
    public void Constructor_FromTilemap_MultipleLayers()
    {
        var fake = CreateFakeTilemap(layerName: null);
        fake.Layers.Add(new TilemapTileLayer("Background", 2, 2, 16, 16));
        fake.Layers.Add(new TilemapTileLayer("Foreground", 2, 2, 16, 16));

        var tileMap = new TileMap(fake);

        tileMap.Layers.Count.ShouldBe(2);
        tileMap.GetLayer("Background").ShouldNotBeNull();
        tileMap.GetLayer("Foreground").ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_FromTilemap_RespectsOffset()
    {
        var fake = CreateFakeTilemap();

        var tileMap = new TileMap(fake, x: 100f, y: 200f);

        tileMap.X.ShouldBe(100f);
        tileMap.Y.ShouldBe(200f);
    }

    [Fact]
    public void Constructor_FromTilemap_DefaultOffsetIsOrigin()
    {
        var fake = CreateFakeTilemap();

        var tileMap = new TileMap(fake);

        tileMap.X.ShouldBe(0f);
        tileMap.Y.ShouldBe(0f);
    }

    [Fact]
    public void Constructor_FromTilemap_LayerByNameIsCaseInsensitive()
    {
        var fake = CreateFakeTilemap(layerName: null);
        fake.Layers.Add(new TilemapTileLayer("MyLayer", 2, 2, 16, 16));

        var tileMap = new TileMap(fake);

        tileMap.GetLayer("mylayer").ShouldNotBeNull();
        tileMap.GetLayer("MYLAYER").ShouldNotBeNull();
    }

    // -- TileMap(Tilemap) + ParseTmx round-trip --

    [Fact]
    public void ParseTmx_ThenTilemapConstructor_ProducesSameDimensions()
    {
        var originalLoader = TileMap.TmxLoader;
        try
        {
            var fake = CreateFakeTilemap(width: 7, height: 5, tileWidth: 24, tileHeight: 24);
            TileMap.TmxLoader = (_, _) => fake;

            // Simulate the real workflow: parse, then construct from the result.
            var parsed = TileMap.ParseTmx("level.tmx", graphicsDevice: null!);
            var tileMap = new TileMap(parsed);

            tileMap.Width.ShouldBe(fake.Width * fake.TileWidth);
            tileMap.Height.ShouldBe(fake.Height * fake.TileHeight);
            tileMap.TileWidth.ShouldBe(24);
            tileMap.TileHeight.ShouldBe(24);
        }
        finally
        {
            TileMap.TmxLoader = originalLoader;
        }
    }
}
