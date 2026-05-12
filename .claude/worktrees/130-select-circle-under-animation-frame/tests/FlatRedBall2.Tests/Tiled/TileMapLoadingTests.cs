using System;
using FlatRedBall2.Tiled;
using MonoGame.Extended.Tilemaps;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Tiled;

// Verifies that TileMap loads TMX bytes through an injectable seam — proof that the engine
// no longer hard-codes a File.IO path and can route reads through TitleContainer (required
// for KNI Blazor / WASM, where there is no filesystem).
public class TileMapLoadingTests
{
    [Fact]
    public void Constructor_TmxLoader_ReceivesProvidedPathAndProducesTileMap()
    {
        var originalLoader = TileMap.TmxLoader;
        try
        {
            string requestedPath = "Content/Tiled/Level1.tmx";
            string? observedPath = null;

            // Hand-built Tilemap so the test never touches the disk or a GraphicsDevice.
            var fakeTilemap = new Tilemap(
                name: "fake", width: 2, height: 2,
                tileWidth: 16, tileHeight: 16,
                orientation: TilemapOrientation.Orthogonal);
            fakeTilemap.Layers.Add(new TilemapTileLayer("Main", 2, 2, 16, 16));

            TileMap.TmxLoader = (path, _) =>
            {
                observedPath = path;
                return fakeTilemap;
            };

            // graphicsDevice is unused by the seam in this test.
            var tileMap = new TileMap(requestedPath, graphicsDevice: null!);

            observedPath.ShouldBe(requestedPath);
            tileMap.Width.ShouldBe(32f);
            tileMap.Height.ShouldBe(32f);
        }
        finally
        {
            TileMap.TmxLoader = originalLoader;
        }
    }

    [Fact]
    public void TmxLoader_DefaultIsNotNull()
    {
        // Sanity check: the default loader is wired up so a real ctor call has something to invoke.
        TileMap.TmxLoader.ShouldNotBeNull();
    }
}
