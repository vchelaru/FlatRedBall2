using AnimationEditor.Core.Tiled;
using DotTiled;
using System;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

public class TilesetAnimationSyncTests
{
    private const string SourceLabel = "../Hero.achx";

    private static Tileset EmptyTileset() => new()
    {
        Name = "Heroes",
        TileWidth = 16,
        TileHeight = 16,
        TileCount = 256,
        Columns = 16,
    };

    private static ChainMappingResult Result(string chainName, uint entryTileId, params MappedFrame[] frames) => new()
    {
        ChainName = chainName,
        Frames = frames,
        EntryTileId = entryTileId,
        Warnings = [],
        SkipCounts = new SkipCounts(),
    };

    [Fact]
    public void Apply_ExistingTileFromDifferentSource_IsNotClearedOrOverwritten()
    {
        var tileset = EmptyTileset();
        var otherSourceTile = new Tile { ID = 0, Width = 0, Height = 0 };
        otherSourceTile.Animation.Add(new Frame { TileID = 0, Duration = 999 });
        otherSourceTile.Properties.Add(new StringProperty { Name = "achjSourceFile", Value = "../OtherChain.achx" });
        tileset.Tiles.Add(otherSourceTile);

        // This source's mapping doesn't touch tile 0 at all.
        var syncResult = TilesetAnimationSync.Apply(tileset, [], SourceLabel);

        var tile = tileset.Tiles.Single(t => t.ID == 0);
        Assert.Equal(999, tile.Animation.Single().Duration);
        Assert.Equal("../OtherChain.achx", tile.GetProperty<StringProperty>("achjSourceFile").Value);
        Assert.False(syncResult.Changed);
    }

    [Fact]
    public void Apply_NewChain_CreatesTileWithAnimationAndTrackingProperties()
    {
        var tileset = EmptyTileset();
        var results = new[] { Result("Walk", 0, new MappedFrame(0, 100), new MappedFrame(1, 100)) };

        var syncResult = TilesetAnimationSync.Apply(tileset, results, SourceLabel);

        var tile = tileset.Tiles.Single(t => t.ID == 0);
        Assert.Equal([((uint)0, 100), ((uint)1, 100)], tile.Animation.Select(f => (f.TileID, f.Duration)));
        Assert.Equal("Walk", tile.GetProperty<StringProperty>("achjAnimationName").Value);
        Assert.Equal(SourceLabel, tile.GetProperty<StringProperty>("achjSourceFile").Value);
        Assert.Equal(1, syncResult.AppliedCount);
        Assert.True(syncResult.Changed);
    }

    [Fact]
    public void Apply_ReSyncWithIdenticalMapping_ReturnsChangedFalse()
    {
        var tileset = EmptyTileset();
        var results = new[] { Result("Walk", 0, new MappedFrame(0, 100), new MappedFrame(1, 100)) };
        TilesetAnimationSync.Apply(tileset, results, SourceLabel);

        // Same mapping again -- nothing about tile 0's animation or properties should differ.
        var syncResult = TilesetAnimationSync.Apply(tileset, results, SourceLabel);

        Assert.False(syncResult.Changed);
    }

    [Fact]
    public void Apply_RemovedChain_TileHasNothingElse_RemovesTileEntirely()
    {
        // The tile only ever existed to carry "Walk"'s animation -- once that's gone there's
        // nothing left worth a <tile id="0"/> stub for. Leaving one behind accumulates one bare
        // stub per animation ever removed (issue found via ChibiCthulhuTiles.tsx).
        var tileset = EmptyTileset();
        var firstSync = new[] { Result("Walk", 0, new MappedFrame(0, 100)) };
        TilesetAnimationSync.Apply(tileset, firstSync, SourceLabel);

        // The "Walk" chain no longer exists in the source (deleted or renamed away from tile 0).
        var syncResult = TilesetAnimationSync.Apply(tileset, [], SourceLabel);

        Assert.DoesNotContain(tileset.Tiles, t => t.ID == 0);
        Assert.True(syncResult.Changed);
    }

    [Fact]
    public void Apply_RemovedChain_TileHasOtherProperty_KeepsTileButClearsAnimationAndTrackingProperties()
    {
        // A tile carrying a property this sync doesn't own (e.g. hand-authored gameplay data)
        // must survive the animation being removed -- only the achjAnimationName/achjSourceFile
        // tracking properties and the animation itself get cleared.
        var tileset = EmptyTileset();
        var firstSync = new[] { Result("Walk", 0, new MappedFrame(0, 100)) };
        TilesetAnimationSync.Apply(tileset, firstSync, SourceLabel);
        var tile = tileset.Tiles.Single(t => t.ID == 0);
        tile.Properties.Add(new StringProperty { Name = "Solid", Value = "true" });

        var syncResult = TilesetAnimationSync.Apply(tileset, [], SourceLabel);

        var survivingTile = tileset.Tiles.Single(t => t.ID == 0);
        Assert.Empty(survivingTile.Animation);
        Assert.DoesNotContain(survivingTile.Properties, p => p.Name is "achjAnimationName" or "achjSourceFile");
        Assert.Equal("true", survivingTile.GetProperty<StringProperty>("Solid").Value);
        Assert.True(syncResult.Changed);
    }

    [Fact]
    public void Apply_RemovedChain_TileHasNonDefaultType_KeepsTileAfterClearing()
    {
        // Tiled attributes other than <properties> (e.g. a per-tile "type") are just as much a
        // reason to keep the <tile> element as a property is -- IsTileEmpty must check them too.
        var tileset = EmptyTileset();
        var firstSync = new[] { Result("Walk", 0, new MappedFrame(0, 100)) };
        TilesetAnimationSync.Apply(tileset, firstSync, SourceLabel);
        var tile = tileset.Tiles.Single(t => t.ID == 0);
        tile.Type = "Chomper";

        var syncResult = TilesetAnimationSync.Apply(tileset, [], SourceLabel);

        var survivingTile = tileset.Tiles.Single(t => t.ID == 0);
        Assert.Empty(survivingTile.Animation);
        Assert.Equal("Chomper", survivingTile.Type);
        Assert.True(syncResult.Changed);
    }

    [Fact]
    public void Apply_RemovedChain_TileHasOnlyAWrongTypedTrackingProperty_IsRemoved()
    {
        var tileset = EmptyTileset();
        var tile = new Tile { ID = 0, Width = 0, Height = 0 };
        tile.Animation.Add(new Frame { TileID = 0, Duration = 100 });
        tile.Properties.Add(new IntProperty { Name = "achjAnimationName", Value = 42 });
        tile.Properties.Add(new StringProperty { Name = "achjSourceFile", Value = SourceLabel });
        tileset.Tiles.Add(tile);

        var syncResult = TilesetAnimationSync.Apply(tileset, [], SourceLabel);

        Assert.DoesNotContain(tileset.Tiles, t => t.ID == 0);
        Assert.True(syncResult.Changed);
    }

    [Fact]
    public void Apply_RemovedChain_TileHasOtherEmptyStringProperty_KeepsTile()
    {
        // IsTileEmpty must key off property *presence*, not value -- an empty string is still a
        // real property a human or another tool wrote and this sync doesn't own.
        var tileset = EmptyTileset();
        var firstSync = new[] { Result("Walk", 0, new MappedFrame(0, 100)) };
        TilesetAnimationSync.Apply(tileset, firstSync, SourceLabel);
        var tile = tileset.Tiles.Single(t => t.ID == 0);
        tile.Properties.Add(new StringProperty { Name = "Note", Value = "" });

        var syncResult = TilesetAnimationSync.Apply(tileset, [], SourceLabel);

        var survivingTile = tileset.Tiles.Single(t => t.ID == 0);
        Assert.Contains(survivingTile.Properties, p => p.Name == "Note");
        Assert.True(syncResult.Changed);
    }

    [Fact]
    public void Apply_RenamedChainMovesToDifferentTile_ClearsOldTileAndPopulatesNewTile()
    {
        var tileset = EmptyTileset();
        var firstSync = new[] { Result("Walk", 0, new MappedFrame(0, 100)) };
        TilesetAnimationSync.Apply(tileset, firstSync, SourceLabel);

        // Frames were re-authored so the same chain now starts at tile 5 instead of tile 0.
        var secondSync = new[] { Result("Walk", 5, new MappedFrame(5, 100)) };
        TilesetAnimationSync.Apply(tileset, secondSync, SourceLabel);

        Assert.DoesNotContain(tileset.Tiles, t => t.ID == 0);

        var newTile = tileset.Tiles.Single(t => t.ID == 5);
        Assert.Equal((uint)5, newTile.Animation.Single().TileID);
        Assert.Equal("Walk", newTile.GetProperty<StringProperty>("achjAnimationName").Value);
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
        var firstSync = new[]
        {
            Result("Walk", 0, new MappedFrame(0, 100)),
            Result("Old", 5, new MappedFrame(5, 100)),
        };
        TilesetAnimationSync.Apply(tileset, firstSync, SourceLabel);

        var secondSync = new[]
        {
            Result("Walk", 0, new MappedFrame(0, 200), new MappedFrame(1, 200)),
            Result("New", 10, new MappedFrame(10, 100)),
        };
        var syncResult = TilesetAnimationSync.Apply(tileset, secondSync, SourceLabel);

        var updatedTile = tileset.Tiles.Single(t => t.ID == 0);
        Assert.Equal([((uint)0, 200), ((uint)1, 200)], updatedTile.Animation.Select(f => (f.TileID, f.Duration)));

        Assert.DoesNotContain(tileset.Tiles, t => t.ID == 5);

        var newTile = tileset.Tiles.Single(t => t.ID == 10);
        Assert.Equal([((uint)10, 100)], newTile.Animation.Select(f => (f.TileID, f.Duration)));

        Assert.True(syncResult.Changed);
    }

    [Fact]
    public void Apply_UpdatedChainTiming_ReplacesFramesOnSameTile()
    {
        var tileset = EmptyTileset();
        var firstSync = new[] { Result("Walk", 0, new MappedFrame(0, 100), new MappedFrame(1, 100)) };
        TilesetAnimationSync.Apply(tileset, firstSync, SourceLabel);

        var secondSync = new[] { Result("Walk", 0, new MappedFrame(0, 250), new MappedFrame(1, 250)) };
        var syncResult = TilesetAnimationSync.Apply(tileset, secondSync, SourceLabel);

        var tile = tileset.Tiles.Single(t => t.ID == 0);
        Assert.All(tile.Animation, f => Assert.Equal(250, f.Duration));
        Assert.True(syncResult.Changed);
    }

    [Fact]
    public void Apply_TwoChainsInSameSourceClaimSameEntryTile_ThrowsInsteadOfSilentlyOverwriting()
    {
        var tileset = EmptyTileset();
        var results = new[]
        {
            Result("Walk", 0, new MappedFrame(0, 100)),
            Result("Idle", 0, new MappedFrame(4, 100)),
        };

        Assert.Throws<InvalidOperationException>(() => TilesetAnimationSync.Apply(tileset, results, SourceLabel));
    }

    [Fact]
    public void Apply_TileOwnedByDifferentSource_IsSkippedNotOverwritten()
    {
        var tileset = EmptyTileset();
        var firstSourceResults = new[] { Result("Walk", 0, new MappedFrame(0, 999)) };
        TilesetAnimationSync.Apply(tileset, firstSourceResults, "../Hero.achx");

        // A different achx's geometry happens to compute the same entry tile id.
        var secondSourceResults = new[] { Result("Idle", 0, new MappedFrame(4, 100)) };
        var syncResult = TilesetAnimationSync.Apply(tileset, secondSourceResults, "../OtherChain.achx");

        var tile = tileset.Tiles.Single(t => t.ID == 0);
        Assert.Equal(999, tile.Animation.Single().Duration);
        Assert.Equal("../Hero.achx", tile.GetProperty<StringProperty>("achjSourceFile").Value);
        Assert.Equal(0, syncResult.AppliedCount);
        Assert.Contains(syncResult.Warnings, w => w.Contains("already owned by"));
    }

    [Fact]
    public void Apply_TileHasHandAuthoredAnimationNoSourceProperty_IsSkippedNotOverwritten()
    {
        var tileset = EmptyTileset();
        var handAuthoredTile = new Tile { ID = 0, Width = 0, Height = 0 };
        handAuthoredTile.Animation.Add(new Frame { TileID = 0, Duration = 999 });
        // No achjSourceFile/achjAnimationName property at all -- this tile was never touched by
        // any achx sync; a human drew its keyframes directly in Tiled.
        tileset.Tiles.Add(handAuthoredTile);

        // This achx chain's frame geometry happens to compute the same entry tile id.
        var results = new[] { Result("Walk", 0, new MappedFrame(4, 100)) };
        var syncResult = TilesetAnimationSync.Apply(tileset, results, SourceLabel);

        var tile = tileset.Tiles.Single(t => t.ID == 0);
        Assert.Equal(999, tile.Animation.Single().Duration);
        Assert.DoesNotContain(tile.Properties, p => p.Name is "achjAnimationName" or "achjSourceFile");
        Assert.Equal(0, syncResult.AppliedCount);
        Assert.Contains(syncResult.Warnings, w => w.Contains("hand-authored") || w.Contains("already owned"));
    }

    [Fact]
    public void Apply_TileHasStaleAnimationNamePropertyButEmptyAnimationAndNoSourceProperty_IsClaimedNotPermanentlyBlocked()
    {
        var tileset = EmptyTileset();
        var staleTile = new Tile { ID = 0, Width = 0, Height = 0 };
        // Simulates a partially-written/crashed save or a pre-achjSourceFile schema version:
        // achjAnimationName survived but the animation frames and achjSourceFile did not.
        // Deliberately NOT treated as "owned" the way a tile with actual Animation.Count > 0 is
        // (see Apply_TileHasHandAuthoredAnimationNoSourceProperty_IsSkippedNotOverwritten above) --
        // there is no achjSourceFile value a future sync could ever match to un-block this tile, so
        // keying the ownership check off achjAnimationName alone would make it permanently
        // unreclaimable (skip+warn forever, never able to write achjSourceFile to satisfy its own
        // check). With no actual animation data at risk, self-healing by claiming and overwriting
        // the stale name is safer than a warning that can never resolve.
        staleTile.Properties.Add(new StringProperty { Name = "achjAnimationName", Value = "OldChain" });
        tileset.Tiles.Add(staleTile);

        var results = new[] { Result("Walk", 0, new MappedFrame(4, 100)) };
        var syncResult = TilesetAnimationSync.Apply(tileset, results, SourceLabel);

        var tile = tileset.Tiles.Single(t => t.ID == 0);
        Assert.Equal(1, syncResult.AppliedCount);
        Assert.Equal("Walk", tile.GetProperty<StringProperty>("achjAnimationName").Value);
        Assert.Equal(SourceLabel, tile.GetProperty<StringProperty>("achjSourceFile").Value);
    }

    [Fact]
    public void Apply_ExistingAnimationNamePropertyHasWrongType_IsReplacedNotDuplicated()
    {
        var tileset = EmptyTileset();
        var tile = new Tile { ID = 0, Width = 0, Height = 0 };
        tile.Properties.Add(new IntProperty { Name = "achjAnimationName", Value = 42 });
        tileset.Tiles.Add(tile);

        var results = new[] { Result("Walk", 0, new MappedFrame(0, 100)) };
        TilesetAnimationSync.Apply(tileset, results, SourceLabel);

        var matching = tile.Properties.Where(p => p.Name == "achjAnimationName").ToList();
        var single = Assert.Single(matching);
        var stringProperty = Assert.IsType<StringProperty>(single);
        Assert.Equal("Walk", stringProperty.Value);
    }

    [Fact]
    public void Apply_TilesetHasDuplicateTileIds_ThrowsClearErrorInsteadOfRawDictionaryException()
    {
        // Apply builds a tile-id-keyed dictionary once up front (for O(1) lookups). A corrupt/
        // hand-edited tsx with two <tile> elements sharing one id used to hit .ToDictionary's own
        // unchecked ArgumentException instead of this codebase's "fail loud with a clear message"
        // precedent (Columns<=0, tile-id collisions between chains, etc).
        var tileset = EmptyTileset();
        tileset.Tiles.Add(new Tile { ID = 5, Width = 0, Height = 0 });
        tileset.Tiles.Add(new Tile { ID = 5, Width = 0, Height = 0 });

        var exception = Assert.Throws<InvalidOperationException>(() => TilesetAnimationSync.Apply(tileset, [], SourceLabel));

        Assert.Contains("5", exception.Message);
    }
}
