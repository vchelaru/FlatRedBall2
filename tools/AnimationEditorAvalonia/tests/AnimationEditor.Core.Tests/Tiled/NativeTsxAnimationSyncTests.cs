using AnimationEditor.Core.Tiled;
using DotTiled;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

public class NativeTsxAnimationSyncTests
{
    private static Tileset EmptyTileset() => new()
    {
        Name = "Heroes",
        TileWidth = 16,
        TileHeight = 16,
        TileCount = 256,
        Columns = 4,
    };

    private static MultiTileMappingResult Result(
        string chainName, uint entryTileId, MappedFrame[] anchorFrames, params TiledSatelliteMapping[] satellites) => new()
    {
        ChainName = chainName,
        AnchorFrames = anchorFrames,
        EntryTileId = entryTileId,
        Satellites = satellites,
        Warnings = [],
    };

    [Fact]
    public void Apply_ChainWithExplicitName_WritesNamePropertyOnAnchorTile()
    {
        var tileset = EmptyTileset();
        var results = new[] { Result("Walk", 0, [new MappedFrame(0, 100), new MappedFrame(1, 100)]) };

        NativeTsxAnimationSync.Apply(tileset, results);

        var tile = tileset.Tiles.Single(t => t.ID == 0);
        Assert.Equal("Walk", tile.GetProperty<StringProperty>("Name").Value);
        Assert.Equal([((uint)0, 100), ((uint)1, 100)], tile.Animation.Select(f => (f.TileID, f.Duration)));
    }

    [Fact]
    public void Apply_ChainWithSyntheticIdName_WritesNoNameProperty()
    {
        var tileset = EmptyTileset();
        var results = new[] { Result("ID:0", 0, [new MappedFrame(0, 100)]) };

        NativeTsxAnimationSync.Apply(tileset, results);

        var tile = tileset.Tiles.Single(t => t.ID == 0);
        Assert.DoesNotContain(tile.Properties, p => p.Name == "Name");
    }

    [Fact]
    public void Apply_MultiTileGroup_WritesParentIdOnSatelliteTile()
    {
        var tileset = EmptyTileset();
        var satellite = new TiledSatelliteMapping(1, [new MappedFrame(1, 100), new MappedFrame(2, 100)]);
        var results = new[] { Result("Walk", 0, [new MappedFrame(0, 100), new MappedFrame(1, 100)], satellite) };

        NativeTsxAnimationSync.Apply(tileset, results);

        var anchorTile = tileset.Tiles.Single(t => t.ID == 0);
        Assert.Equal("Walk", anchorTile.GetProperty<StringProperty>("Name").Value);

        var satelliteTile = tileset.Tiles.Single(t => t.ID == 1);
        Assert.Equal(0, satelliteTile.GetProperty<IntProperty>("ParentId").Value);
        Assert.Equal([((uint)1, 100), ((uint)2, 100)], satelliteTile.Animation.Select(f => (f.TileID, f.Duration)));
        Assert.DoesNotContain(satelliteTile.Properties, p => p.Name == "Name");
    }

    [Fact]
    public void Apply_RemovedChain_ClearsStaleTileAnimationAndProperties()
    {
        var tileset = EmptyTileset();
        var firstSync = new[] { Result("Walk", 0, [new MappedFrame(0, 100)]) };
        NativeTsxAnimationSync.Apply(tileset, firstSync);

        var syncResult = NativeTsxAnimationSync.Apply(tileset, []);

        var tile = tileset.Tiles.Single(t => t.ID == 0);
        Assert.Empty(tile.Animation);
        Assert.DoesNotContain(tile.Properties, p => p.Name is "Name" or "ParentId");
        Assert.True(syncResult.Changed);
    }

    [Fact]
    public void Apply_TileAnimatedDirectlyInTiledWithNoNameProperty_IsLeftUntouchedWhenReapplied()
    {
        // A tile animated by hand in Tiled (no Name/ParentId property) that round-trips
        // through load -> Map -> Apply unchanged should not be reported as changed.
        var tileset = EmptyTileset();
        var tiledAuthoredTile = new Tile { ID = 3, Width = 0, Height = 0 };
        tiledAuthoredTile.Animation.Add(new Frame { TileID = 3, Duration = 300 });
        tileset.Tiles.Add(tiledAuthoredTile);

        var results = new[] { Result("ID:3", 3, [new MappedFrame(3, 300)]) };
        var syncResult = NativeTsxAnimationSync.Apply(tileset, results);

        Assert.False(syncResult.Changed);
    }
}
