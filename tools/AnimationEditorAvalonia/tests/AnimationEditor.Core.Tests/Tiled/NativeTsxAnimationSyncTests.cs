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
    public void Apply_StaleClearExistingUpdateAndNewTileAllInOneCall_DictionaryLookupMatchesSequentialScan()
    {
        // Exercises Apply's dictionary-based tile lookup against all three code paths in a single
        // call: a stale tile that must be found and cleared, an existing tile that must be found
        // and updated, and a brand-new tile added mid-loop -- pinning that the O(1) dictionary
        // lookup (built once from tileset.Tiles at the top of Apply) produces the exact same
        // result as the prior O(n) Single/FirstOrDefault scan of tileset.Tiles.
        var tileset = EmptyTileset();
        NativeTsxAnimationSync.Apply(tileset, [
            Result("Walk", 0, [new MappedFrame(0, 100)]),
            Result("Old", 5, [new MappedFrame(5, 100)]),
        ]);

        var syncResult = NativeTsxAnimationSync.Apply(tileset, [
            Result("Walk", 0, [new MappedFrame(0, 200), new MappedFrame(1, 200)]),
            Result("New", 10, [new MappedFrame(10, 100)]),
        ]);

        var updatedTile = tileset.Tiles.Single(t => t.ID == 0);
        Assert.Equal([((uint)0, 200), ((uint)1, 200)], updatedTile.Animation.Select(f => (f.TileID, f.Duration)));

        var staleTile = tileset.Tiles.Single(t => t.ID == 5);
        Assert.Empty(staleTile.Animation);
        Assert.DoesNotContain(staleTile.Properties, p => p.Name is "Name" or "ParentId");

        var newTile = tileset.Tiles.Single(t => t.ID == 10);
        Assert.Equal([((uint)10, 100)], newTile.Animation.Select(f => (f.TileID, f.Duration)));

        Assert.True(syncResult.Changed);
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

    [Fact]
    public void Apply_TilesetHasDuplicateTileIds_ThrowsClearErrorInsteadOfRawDictionaryException()
    {
        // Apply builds a tile-id-keyed dictionary once up front (for O(1) lookups). A corrupt/
        // hand-edited tsx with two <tile> elements sharing one id used to hit .ToDictionary's own
        // unchecked ArgumentException ("An item with the same key has already been added") instead
        // of this codebase's established "fail loud with a clear message" precedent (Columns<=0,
        // tile-id collisions between chains, etc).
        var tileset = EmptyTileset();
        tileset.Tiles.Add(new Tile { ID = 5, Width = 0, Height = 0 });
        tileset.Tiles.Add(new Tile { ID = 5, Width = 0, Height = 0 });

        var exception = Assert.Throws<InvalidOperationException>(() => NativeTsxAnimationSync.Apply(tileset, []));

        Assert.Contains("5", exception.Message);
    }

    [Fact]
    public void Apply_TileOwnedByAchxPushSource_StaleClearingLeavesItUntouched()
    {
        // A tile carrying "achjSourceFile" was written by the achx-push feature (issue #1133),
        // a save pipeline entirely independent of this native-tsx project (issue #1140) -- see
        // TiledAnimationToAchjMapper's matching load-side exclusion. Since that tile is now never
        // absorbed into this project's own AnimationChainListSave, it never appears in `results`,
        // and the "any previously-animated tile absent from results is stale, full stop" clearing
        // rule (this class's own doc comment) would otherwise wipe another feature's animation the
        // very next time this project saves, regardless of whether anything the user actually
        // edited has anything to do with that tile.
        var tileset = EmptyTileset();
        var achxOwnedTile = new Tile { ID = 5, Width = 0, Height = 0 };
        achxOwnedTile.Animation.Add(new Frame { TileID = 5, Duration = 100 });
        achxOwnedTile.Properties.Add(new StringProperty { Name = "achjAnimationName", Value = "Fireball" });
        achxOwnedTile.Properties.Add(new StringProperty { Name = "achjSourceFile", Value = "../Fireball.achx" });
        tileset.Tiles.Add(achxOwnedTile);

        var syncResult = NativeTsxAnimationSync.Apply(tileset, []);

        var tile = tileset.Tiles.Single(t => t.ID == 5);
        Assert.Single(tile.Animation);
        Assert.Equal("Fireball", tile.GetProperty<StringProperty>("achjAnimationName").Value);
        Assert.Equal("../Fireball.achx", tile.GetProperty<StringProperty>("achjSourceFile").Value);
        Assert.False(syncResult.Changed);
    }

    [Fact]
    public void Apply_ChainGeometryClaimsAchxPushOwnedTile_ThrowsInsteadOfSilentlyOverwriting()
    {
        // Symmetric to Apply_TwoChainsClaimSameEntryTile_ThrowsInsteadOfSilentlyOverwriting, but
        // the other claimant is a tile owned by the achx-push feature rather than another
        // native-tsx chain. Two independent, un-coordinated save pipelines (achx-push and
        // native-tsx) computing the same tile id for two different animations is the same
        // "can't silently pick a winner" situation this codebase already fails loudly for.
        var tileset = EmptyTileset();
        var achxOwnedTile = new Tile { ID = 0, Width = 0, Height = 0 };
        achxOwnedTile.Animation.Add(new Frame { TileID = 0, Duration = 100 });
        achxOwnedTile.Properties.Add(new StringProperty { Name = "achjSourceFile", Value = "../Fireball.achx" });
        tileset.Tiles.Add(achxOwnedTile);

        var results = new[] { Result("Walk", 0, [new MappedFrame(0, 100)]) };

        var exception = Assert.Throws<InvalidOperationException>(() => NativeTsxAnimationSync.Apply(tileset, results));
        Assert.Contains("../Fireball.achx", exception.Message);
    }
}
