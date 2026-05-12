using System;
using System.Collections.Generic;
using FlatRedBall2.Tiled;
using MonoGame.Extended.Tilemaps;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Tiled;

public class TileMapLayerSetTileTests
{
    // Builds a Tilemap with one tileset (FirstGlobalId = 1) containing the given tile-data entries
    // by localId/Class, plus one tile layer named "Main".
    private static Tilemap BuildTilemap(
        int widthTiles,
        int heightTiles,
        int tileSize,
        (int localId, string className)[] tileDataEntries)
    {
        var tilemap = new Tilemap(
            name: "test",
            width: widthTiles,
            height: heightTiles,
            tileWidth: tileSize,
            tileHeight: tileSize,
            orientation: TilemapOrientation.Orthogonal);

        int tileCount = 0;
        foreach (var (localId, _) in tileDataEntries)
            if (localId + 1 > tileCount) tileCount = localId + 1;
        if (tileCount == 0) tileCount = 1;

        var tileset = new TilemapTileset(
            name: "ts", texture: null!, tileWidth: tileSize, tileHeight: tileSize,
            tileCount: tileCount, columns: tileCount);
        tileset.FirstGlobalId = 1;
        foreach (var (localId, className) in tileDataEntries)
            tileset.AddTileData(new TilemapTileData(localId) { Class = className });
        tilemap.Tilesets.Add(tileset);

        tilemap.Layers.Add(new TilemapTileLayer("Main", widthTiles, heightTiles, tileSize, tileSize));
        return tilemap;
    }

    [Fact]
    public void RemoveTile_ClearsCell()
    {
        var tilemap = BuildTilemap(4, 4, 16, new[] { (0, "Solid") });
        var liveLayer = (TilemapTileLayer)tilemap.Layers[0];
        liveLayer.SetTile(2, 2, new TilemapTile(globalId: 1));

        var tileMap = new TileMap(tilemap);
        tileMap.GetLayer("Main").RemoveTile(2, 2);

        liveLayer.GetTile(2, 2).HasValue.ShouldBeFalse();
    }

    [Fact]
    public void SetTile_ByClassName_AmbiguousAndPickRandomFalse_Throws()
    {
        // Two tiles share class "Dirt" — strict lookup must refuse to guess.
        var tilemap = BuildTilemap(4, 4, 16, new[] { (0, "Dirt"), (1, "Dirt") });
        var tileMap = new TileMap(tilemap);

        Should.Throw<InvalidOperationException>(() =>
            tileMap.GetLayer("Main").SetTile(0, 0, "Dirt"));
    }

    [Fact]
    public void SetTile_ByClassName_AmbiguousAndPickRandomTrue_PicksOneOfTheMatches()
    {
        var tilemap = BuildTilemap(4, 4, 16, new[] { (0, "Dirt"), (1, "Dirt"), (2, "Other") });
        var liveLayer = (TilemapTileLayer)tilemap.Layers[0];

        var tileMap = new TileMap(tilemap);
        tileMap.GetLayer("Main").SetTile(0, 0, "Dirt", pickRandom: true);

        // Random pick must land on one of the two "Dirt" global IDs (1 or 2), never "Other" (3).
        var picked = liveLayer.GetTile(0, 0)!.Value.GlobalId;
        var validIds = new HashSet<int> { 1, 2 };
        validIds.ShouldContain(picked);
    }

    [Fact]
    public void SetTile_ByClassName_NoMatch_Throws()
    {
        var tilemap = BuildTilemap(4, 4, 16, new[] { (0, "Solid") });
        var tileMap = new TileMap(tilemap);

        Should.Throw<InvalidOperationException>(() =>
            tileMap.GetLayer("Main").SetTile(0, 0, "Nonexistent"));
    }

    [Fact]
    public void SetTile_ByClassName_UniqueMatch_PaintsThatGlobalId()
    {
        var tilemap = BuildTilemap(4, 4, 16,
            new[] { (0, "Solid"), (1, "UsedBlock") });
        var liveLayer = (TilemapTileLayer)tilemap.Layers[0];

        var tileMap = new TileMap(tilemap);
        tileMap.GetLayer("Main").SetTile(1, 1, "UsedBlock");

        // localId 1 in tileset with FirstGlobalId 1 → globalId 2.
        liveLayer.GetTile(1, 1)!.Value.GlobalId.ShouldBe(2);
    }

    [Fact]
    public void SetTile_ByGlobalId_ReplacesCellWithThatTile()
    {
        var tilemap = BuildTilemap(4, 4, 16, new[] { (0, "Solid") });
        var liveLayer = (TilemapTileLayer)tilemap.Layers[0];

        var tileMap = new TileMap(tilemap);
        tileMap.GetLayer("Main").SetTile(2, 3, 1);

        liveLayer.GetTile(2, 3)!.Value.GlobalId.ShouldBe(1);
    }
}
