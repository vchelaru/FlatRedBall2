using AnimationEditor.Core.Tiled;
using DotTiled;
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
    public void Apply_RemovedChain_ClearsStaleTileAnimationAndTrackingProperties()
    {
        var tileset = EmptyTileset();
        var firstSync = new[] { Result("Walk", 0, new MappedFrame(0, 100)) };
        TilesetAnimationSync.Apply(tileset, firstSync, SourceLabel);

        // The "Walk" chain no longer exists in the source (deleted or renamed away from tile 0).
        var syncResult = TilesetAnimationSync.Apply(tileset, [], SourceLabel);

        var tile = tileset.Tiles.Single(t => t.ID == 0);
        Assert.Empty(tile.Animation);
        Assert.DoesNotContain(tile.Properties, p => p.Name is "achjAnimationName" or "achjSourceFile");
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

        var oldTile = tileset.Tiles.Single(t => t.ID == 0);
        Assert.Empty(oldTile.Animation);

        var newTile = tileset.Tiles.Single(t => t.ID == 5);
        Assert.Equal((uint)5, newTile.Animation.Single().TileID);
        Assert.Equal("Walk", newTile.GetProperty<StringProperty>("achjAnimationName").Value);
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
}
