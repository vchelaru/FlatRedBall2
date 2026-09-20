using AnimationEditor.Core.Tiled;
using DotTiled;
using FlatRedBall2.AnimationEditorCommon;
using System;
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
        SourceChain = new AnimationChainSave { Name = chainName },
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
        var satellite = new TiledSatelliteMapping(1, [new MappedFrame(1, 100), new MappedFrame(2, 100)], (1, 0));
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

    [Fact]
    public void Apply_TwoChainsClaimSameEntryTile_ThrowsInsteadOfSilentlyOverwriting()
    {
        var tileset = EmptyTileset();
        var results = new[]
        {
            Result("Walk", 0, [new MappedFrame(0, 100)]),
            Result("Idle", 0, [new MappedFrame(4, 100)]),
        };

        Assert.Throws<InvalidOperationException>(() => NativeTsxAnimationSync.Apply(tileset, results));
    }

    [Fact]
    public void Apply_SatelliteCollidesWithAnotherChainsAnchor_ThrowsInsteadOfSilentlyOverwriting()
    {
        var tileset = EmptyTileset();
        var satellite = new TiledSatelliteMapping(1, [new MappedFrame(1, 100)], (1, 0));
        var results = new[]
        {
            Result("Walk", 0, [new MappedFrame(0, 100)], satellite),
            Result("Idle", 1, [new MappedFrame(1, 100)]),
        };

        Assert.Throws<InvalidOperationException>(() => NativeTsxAnimationSync.Apply(tileset, results));
    }

    [Fact]
    public void Apply_TwoMultiTileChainsSatellitesCollide_ThrowsNamingBothChainsAndTileId()
    {
        // Two genuinely multi-tile groups (anchor + satellite each) whose footprints happen to
        // overlap in the spritesheet such that only the *satellites* collide -- distinct from the
        // already-covered anchor-vs-anchor and satellite-vs-anchor cases.
        var tileset = EmptyTileset();
        var walkSatellite = new TiledSatelliteMapping(5, [new MappedFrame(5, 100)], (1, 0));
        var runSatellite = new TiledSatelliteMapping(5, [new MappedFrame(5, 100)], (1, 0));
        var results = new[]
        {
            Result("Walk", 0, [new MappedFrame(0, 100)], walkSatellite),
            Result("Run", 10, [new MappedFrame(10, 100)], runSatellite),
        };

        var exception = Assert.Throws<InvalidOperationException>(() => NativeTsxAnimationSync.Apply(tileset, results));

        Assert.Contains("Walk", exception.Message);
        Assert.Contains("Run", exception.Message);
        Assert.Contains("5", exception.Message);
    }

    [Fact]
    public void Apply_ExistingNamePropertyHasWrongType_IsReplacedNotDuplicated()
    {
        var tileset = EmptyTileset();
        var tile = new Tile { ID = 0, Width = 0, Height = 0 };
        // Simulate a hand-authored/corrupt file where "Name" was written as an int property.
        tile.Properties.Add(new IntProperty { Name = "Name", Value = 42 });
        tileset.Tiles.Add(tile);

        var results = new[] { Result("Walk", 0, [new MappedFrame(0, 100)]) };
        NativeTsxAnimationSync.Apply(tileset, results);

        var nameProperties = tile.Properties.Where(p => p.Name == "Name").ToList();
        var single = Assert.Single(nameProperties);
        var stringProperty = Assert.IsType<StringProperty>(single);
        Assert.Equal("Walk", stringProperty.Value);
    }

    [Fact]
    public void Apply_ExistingParentIdPropertyHasWrongType_IsReplacedNotDuplicated()
    {
        var tileset = EmptyTileset();
        var anchorTile = new Tile { ID = 0, Width = 0, Height = 0 };
        tileset.Tiles.Add(anchorTile);
        var satelliteTile = new Tile { ID = 1, Width = 0, Height = 0 };
        satelliteTile.Properties.Add(new StringProperty { Name = "ParentId", Value = "not-a-number" });
        tileset.Tiles.Add(satelliteTile);

        var satellite = new TiledSatelliteMapping(1, [new MappedFrame(1, 100)], (1, 0));
        var results = new[] { Result("Walk", 0, [new MappedFrame(0, 100)], satellite) };
        NativeTsxAnimationSync.Apply(tileset, results);

        var parentIdProperties = satelliteTile.Properties.Where(p => p.Name == "ParentId").ToList();
        var single = Assert.Single(parentIdProperties);
        var intProperty = Assert.IsType<IntProperty>(single);
        Assert.Equal(0, intProperty.Value);
    }
}
