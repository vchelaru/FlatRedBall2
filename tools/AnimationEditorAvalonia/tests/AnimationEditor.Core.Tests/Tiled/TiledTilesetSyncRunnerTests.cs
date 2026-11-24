using AnimationEditor.Core.Tiled;
using DotTiled;
using DotTiled.Serialization;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests.Tiled;

public class TiledTilesetSyncRunnerTests
{
    // 4 columns, 16x16 tiles, no image pixel size needed since the achx below uses Pixel
    // coordinates (ProjectManager.AnimationChainListSave is always UV in memory, but the mapper
    // itself supports Pixel too -- see AchjToTiledAnimationMapperTests for the UV path).
    private static string WriteFixtureTileset(string tempDir, string fileName = "Heroes.tsx", string imageSource = "Heroes.png")
    {
        var tileset = new Tileset
        {
            Name = "Heroes",
            TileWidth = 16,
            TileHeight = 16,
            TileCount = 64,
            Columns = 4,
            Image = new Image { Source = imageSource },
        };
        var path = Path.Combine(tempDir, fileName);
        TsxWriter.Write(tileset, path);
        return path;
    }

    private static AnimationChainListSave AchjWithWalkChain()
    {
        var save = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "Heroes.png", FrameLength = 0.1f,
            LeftCoordinate = 0, TopCoordinate = 0, RightCoordinate = 16, BottomCoordinate = 16,
        });
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "Heroes.png", FrameLength = 0.1f,
            LeftCoordinate = 16, TopCoordinate = 0, RightCoordinate = 32, BottomCoordinate = 16,
        });
        save.AnimationChains.Add(chain);
        return save;
    }

    [Fact]
    public void SyncAll_MatchingChain_WritesTileAnimationBackToTsxFile()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var tsxPath = WriteFixtureTileset(tempDir);
        var achxPath = Path.Combine(tempDir, "Hero.achx");
        var achj = AchjWithWalkChain();

        var outcomes = TiledTilesetSyncRunner.SyncAll(achj, achxPath, [tsxPath]);

        Assert.True(outcomes[0].Success);
        Assert.Equal(1, outcomes[0].AppliedCount);

        var reloaded = Loader.Default().LoadTileset(tsxPath);
        var tile0 = reloaded.Tiles.Single(t => t.ID == 0);
        Assert.Equal([((uint)0, 100), ((uint)1, 100)], tile0.Animation.Select(f => (f.TileID, f.Duration)));
        Assert.Equal("Walk", tile0.GetProperty<StringProperty>("achjAnimationName").Value);
    }

    // No image file exists in any case: the comparison is by path only.
    [Theory]
    [InlineData("Hero.achx", "Heroes.png", "Heroes.png", true)]
    [InlineData("Hero.achx", "Heroes.png", "sub/Heroes.png", false)]
    [InlineData("Hero.achx", "Heroes.png", "<root>sub/Heroes.png", false)]
    [InlineData("Hero.achx", "Heroes.png", "<root>Heroes.png", true)]
    [InlineData("Hero.achx", "<root>Heroes.png", "Heroes.png", true)]
    [InlineData("Hero.achx", "Heroes.png", "HEROES.PNG", true)]
    [InlineData("Hero.achx", "Heroes.png", "sub/../Heroes.png", true)]
    [InlineData("Hero.achx", "images/Heroes.png", "images/Heroes.png", true)]
    [InlineData("Hero.achx", "images/Heroes.png", "Heroes.png", false)]
    // A frame's texture is relative to the .achx, the image to the .tsx.
    [InlineData("anims/Hero.achx", "Heroes.png", "../Heroes.png", true)]
    [InlineData("anims/Hero.achx", "Heroes.png", "Heroes.png", false)]
    [InlineData("anims/Hero.achx", "anims/Heroes.png", "Heroes.png", true)]
    [InlineData("anims/Hero.achx", "images/Heroes.png", "images/Heroes.png", false)]
    [InlineData("Hero.achx", "//server/share/Heroes.png", "//server/share/heroes.png", true)]
    [InlineData("Hero.achx", "//server/share/Heroes.png", "//server/other/Heroes.png", false)]
    public void SyncAll_FrameTexture_IsComparedByResolvedPath(
        string achxName, string imageSource, string textureName, bool isPushed)
    {
        var root = Directory.CreateTempSubdirectory().FullName + Path.DirectorySeparatorChar;
        var tsxPath = WriteFixtureTileset(root, imageSource: imageSource.Replace("<root>", root));
        var achj = AchjWithWalkChain();
        foreach (var frame in achj.AnimationChains[0].Frames)
            frame.TextureName = textureName.Replace("<root>", root);

        var outcome = TiledTilesetSyncRunner.SyncAll(achj, Path.Combine(root, achxName), [tsxPath]).Single();

        outcome.Error.ShouldBeNull();
        outcome.AppliedCount.ShouldBe(isPushed ? 1 : 0);
        outcome.Changed.ShouldBe(isPushed);
        outcome.Warnings.Count.ShouldBe(isPushed ? 0 : 1);
    }

    // Pushed as Walk first, then every case but the last leaves tile 0 without an animation.
    [Theory]
    [InlineData("Walk", "sub/Heroes.png", "sub/Heroes.png", "Tile 0 was cleared.")]
    [InlineData("Walk", "sub/HEROES.PNG", "sub/Heroes.png", "Tile 0 was cleared.")]
    [InlineData("Renamed", "sub/Heroes.png", "sub/Heroes.png", "Tile 0 was cleared.")]
    [InlineData("Walk", "sub/Heroes.png", "Heroes.png", "Tile 0 was cleared.")] // moves to tile 1
    [InlineData("Walk", "Heroes.png", "sub/Heroes.png", null)]
    public void SyncAll_FramesOnASameNamedImage_WarnsNamingBothImagesAndAClearedTile(
        string chainName, string firstTexture, string secondTexture, string? clearedTile)
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var tsxPath = WriteFixtureTileset(tempDir);
        var achxPath = Path.Combine(tempDir, "Hero.achx");
        var achj = AchjWithWalkChain();
        TiledTilesetSyncRunner.SyncAll(achj, achxPath, [tsxPath]);

        var chain = achj.AnimationChains[0];
        chain.Name = chainName;
        chain.Frames[0].TextureName = firstTexture;
        chain.Frames[1].TextureName = secondTexture;
        var warning = TiledTilesetSyncRunner.SyncAll(achj, achxPath, [tsxPath]).Single().Warnings.Single();

        warning.ShouldContain($"chain \"{chainName}\"");
        warning.ShouldContain("/sub/heroes.png\"", Case.Insensitive);
        warning.ShouldContain("image is \"" + new FilePath(Path.Combine(tempDir, "Heroes.png")).FullPath + "\"");
        if (clearedTile == null) warning.ShouldNotContain("Tile ");
        else warning.ShouldEndWith(clearedTile);
        Loader.Default().LoadTileset(tsxPath).Tiles.Any(t => t.ID == 0).ShouldBe(clearedTile == null);

        // Nothing is left to clear, but the frames still are not pushed.
        TiledTilesetSyncRunner.SyncAll(achj, achxPath, [tsxPath]).Single().Warnings.Single().ShouldNotContain("Tile ");
    }

    [Fact]
    public void SyncAll_FramesPointedBackAtTheImage_ArePushedAgain()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var tsxPath = WriteFixtureTileset(tempDir);
        var achxPath = Path.Combine(tempDir, "Hero.achx");
        TiledTilesetSyncRunner.SyncAll(AchjWithWalkChain(), achxPath, [tsxPath]);
        var pushed = File.ReadAllText(tsxPath);
        var onACopy = AchjWithWalkChain();
        foreach (var frame in onACopy.AnimationChains[0].Frames)
            frame.TextureName = "sub/Heroes.png";
        TiledTilesetSyncRunner.SyncAll(onACopy, achxPath, [tsxPath]);

        var outcome = TiledTilesetSyncRunner.SyncAll(AchjWithWalkChain(), achxPath, [tsxPath]).Single();

        outcome.Warnings.ShouldBeEmpty();
        File.ReadAllText(tsxPath).ShouldBe(pushed);
    }

    [Fact]
    public void SyncAll_ChainMovedToADifferentlyNamedImage_ClearsTheTileItHadPushedWithoutWarning()
    {
        // Most chains of a big project belong to another tileset, so this stays silent.
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var tsxPath = WriteFixtureTileset(tempDir);
        var achxPath = Path.Combine(tempDir, "Hero.achx");
        var achj = AchjWithWalkChain();
        TiledTilesetSyncRunner.SyncAll(achj, achxPath, [tsxPath]);

        foreach (var frame in achj.AnimationChains[0].Frames)
            frame.TextureName = "Other.png";
        var outcome = TiledTilesetSyncRunner.SyncAll(achj, achxPath, [tsxPath]).Single();

        outcome.Warnings.ShouldBeEmpty();
        Loader.Default().LoadTileset(tsxPath).Tiles.ShouldBeEmpty();
    }

    [Fact]
    public void SyncAll_TwoChainsOfTheSameNameOneMovedAndOneOnACopy_LeavesNoStaleTile()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var tsxPath = WriteFixtureTileset(tempDir);
        var achxPath = Path.Combine(tempDir, "Hero.achx");
        var achj = AchjWithWalkChain();
        var second = AchjWithWalkChain().AnimationChains[0];
        second.Frames.RemoveAt(0);
        achj.AnimationChains.Add(second);
        TiledTilesetSyncRunner.SyncAll(achj, achxPath, [tsxPath]);

        achj.AnimationChains[0].Frames.RemoveAt(1);
        achj.AnimationChains[0].Frames[0].LeftCoordinate = 48;
        achj.AnimationChains[0].Frames[0].RightCoordinate = 64;
        second.Frames[0].TextureName = "sub/Heroes.png";
        var outcome = TiledTilesetSyncRunner.SyncAll(achj, achxPath, [tsxPath]).Single();

        Loader.Default().LoadTileset(tsxPath).Tiles.Select(t => t.ID).ShouldBe([3u]);
        outcome.Warnings.Single().ShouldEndWith("Tile 1 was cleared.");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SyncAll_TextureIsAnotherAssociatedTilesetsImage_DoesNotWarn(bool bothAssociated)
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var forest = Directory.CreateDirectory(Path.Combine(tempDir, "forest")).FullName;
        var cave = Directory.CreateDirectory(Path.Combine(tempDir, "cave")).FullName;
        var forestTsx = WriteFixtureTileset(forest, "tiles.tsx", "tiles.png");
        var caveTsx = WriteFixtureTileset(cave, "tiles.tsx", "tiles.png");
        var achj = AchjWithWalkChain();
        foreach (var frame in achj.AnimationChains[0].Frames)
            frame.TextureName = "forest/Tiles.png";

        var outcome = TiledTilesetSyncRunner.SyncAll(achj, Path.Combine(tempDir, "Hero.achx"),
            bothAssociated ? [caveTsx, forestTsx] : [caveTsx])[0];

        outcome.Warnings.Count.ShouldBe(bothAssociated ? 0 : 1);
        outcome.Changed.ShouldBeFalse();
    }

    [Fact]
    public void SyncAll_AnotherAssociatedTilesetCannotBeRead_StillWarnsAndPushes()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var tsxPath = WriteFixtureTileset(tempDir);
        var achj = AchjWithWalkChain();
        achj.AnimationChains[0].Frames[1].TextureName = "sub/Heroes.png";

        var outcomes = TiledTilesetSyncRunner.SyncAll(achj, Path.Combine(tempDir, "Hero.achx"),
            [Path.Combine(tempDir, "sub", "Missing.tsx"), tsxPath]);

        outcomes.Select(o => o.Success).ShouldBe([false, true]);
        outcomes[1].AppliedCount.ShouldBe(1);
        outcomes[1].Warnings.Count.ShouldBe(1);
    }

    [Fact]
    public void SyncAll_ChainRemovedSinceLastSync_RemovesStaleTileOnReSyncRatherThanLeavingAnEmptyStub()
    {
        // A tile with nothing left after clearing (issue found via ChibiCthulhuTiles.tsx: a
        // removed animation left a bare <tile id="0"/> stub behind, one per animation ever
        // removed) must disappear from the file entirely on re-sync, not just lose its animation.
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var tsxPath = WriteFixtureTileset(tempDir);
        var achxPath = Path.Combine(tempDir, "Hero.achx");
        TiledTilesetSyncRunner.SyncAll(AchjWithWalkChain(), achxPath, [tsxPath]);

        var emptyAchj = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        TiledTilesetSyncRunner.SyncAll(emptyAchj, achxPath, [tsxPath]);

        var reloaded = Loader.Default().LoadTileset(tsxPath);
        Assert.DoesNotContain(reloaded.Tiles, t => t.ID == 0);
    }

    [Fact]
    public void SyncAll_ChainRemovedSinceLastSync_LeavesOtherAnimatedTilesUntouched()
    {
        // Removing one animation's stale tile must patch out only that tile's own line -- a
        // sibling <tile> (e.g. tile 4 from a second chain) must survive with its animation intact,
        // proving the removal doesn't disturb TsxWriter's in-place line patching for neighbors.
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var tsxPath = WriteFixtureTileset(tempDir);
        var achxPath = Path.Combine(tempDir, "Hero.achx");

        var save = AchjWithWalkChain();
        var idleChain = new AnimationChainSave { Name = "Idle" };
        idleChain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "Heroes.png", FrameLength = 0.1f,
            LeftCoordinate = 0, TopCoordinate = 16, RightCoordinate = 16, BottomCoordinate = 32,
        });
        save.AnimationChains.Add(idleChain);
        TiledTilesetSyncRunner.SyncAll(save, achxPath, [tsxPath]);

        // "Walk" (tile 0) is gone from the achx; "Idle" (tile 4) still exists.
        var onlyIdle = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        onlyIdle.AnimationChains.Add(idleChain);
        TiledTilesetSyncRunner.SyncAll(onlyIdle, achxPath, [tsxPath]);

        var reloaded = Loader.Default().LoadTileset(tsxPath);
        Assert.DoesNotContain(reloaded.Tiles, t => t.ID == 0);
        var idleTile = reloaded.Tiles.Single(t => t.ID == 4);
        Assert.Equal([((uint)4, 100)], idleTile.Animation.Select(f => (f.TileID, f.Duration)));
    }

    [Fact]
    public void SyncAll_ReSyncWithNoChanges_DoesNotRewriteTsxFile()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var tsxPath = WriteFixtureTileset(tempDir);
        var achxPath = Path.Combine(tempDir, "Hero.achx");
        var achj = AchjWithWalkChain();
        TiledTilesetSyncRunner.SyncAll(achj, achxPath, [tsxPath]);

        // If SyncAll writes again despite nothing changing, File.Create on a read-only file throws
        // and the runner reports it as a failure -- a cheap, deterministic way to prove the write
        // was skipped without relying on file-timestamp granularity.
        File.SetAttributes(tsxPath, FileAttributes.ReadOnly);
        try
        {
            var outcomes = TiledTilesetSyncRunner.SyncAll(achj, achxPath, [tsxPath]);

            Assert.True(outcomes[0].Success);
        }
        finally
        {
            File.SetAttributes(tsxPath, FileAttributes.Normal);
        }
    }

    [Fact]
    public void SyncAll_UnreadableTsxPath_ReportsFailureWithoutThrowing()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var achxPath = Path.Combine(tempDir, "Hero.achx");
        var missingTsxPath = Path.Combine(tempDir, "DoesNotExist.tsx");

        var outcomes = TiledTilesetSyncRunner.SyncAll(AchjWithWalkChain(), achxPath, [missingTsxPath]);

        Assert.False(outcomes[0].Success);
        Assert.NotNull(outcomes[0].Error);
    }

    [Fact]
    public void SyncAll_SecondTsxInBatchThrows_FirstTsxWriteAlreadyOnDiskStaysFullyCorrect()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var goodTsxPath = WriteFixtureTileset(tempDir, "Heroes.tsx");
        var brokenTsxPath = Path.Combine(tempDir, "DoesNotExist.tsx");
        var achxPath = Path.Combine(tempDir, "Hero.achx");

        var outcomes = TiledTilesetSyncRunner.SyncAll(AchjWithWalkChain(), achxPath, [goodTsxPath, brokenTsxPath]);

        Assert.True(outcomes[0].Success);
        Assert.False(outcomes[1].Success);
        Assert.NotNull(outcomes[1].Error);

        // Reload from disk (not the in-memory outcome) to prove tsx #1's completed write wasn't
        // half-applied or corrupted by tsx #2's later failure in the same batch.
        var reloaded = Loader.Default().LoadTileset(goodTsxPath);
        var tile0 = reloaded.Tiles.Single(t => t.ID == 0);
        Assert.Equal([((uint)0, 100), ((uint)1, 100)], tile0.Animation.Select(f => (f.TileID, f.Duration)));
        Assert.Equal("Walk", tile0.GetProperty<StringProperty>("achjAnimationName").Value);
    }

    // Pins that SyncAll's blanket `catch (Exception ex)` doesn't flatten the failure to a generic
    // message -- it stores the exact original exception (type + message intact) on the outcome, so
    // a genuine collision (InvalidOperationException, from TilesetAnimationSync's ownership check)
    // stays distinguishable from an I/O failure (e.g. FileNotFoundException, asserted separately
    // above in SyncAll_UnreadableTsxPath_ReportsFailureWithoutThrowing) by callers that inspect
    // outcome.Error's runtime type rather than just its Message.
    [Fact]
    public void SyncAll_TwoChainsClaimSameEntryTile_OutcomeErrorPreservesExactExceptionTypeAndMessage()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var tsxPath = WriteFixtureTileset(tempDir);
        var achxPath = Path.Combine(tempDir, "Hero.achx");
        var save = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var walk = new AnimationChainSave { Name = "Walk" };
        walk.Frames.Add(new AnimationFrameSave
        {
            TextureName = "Heroes.png", FrameLength = 0.1f,
            LeftCoordinate = 0, TopCoordinate = 0, RightCoordinate = 16, BottomCoordinate = 16,
        });
        var idle = new AnimationChainSave { Name = "Idle" };
        idle.Frames.Add(new AnimationFrameSave
        {
            TextureName = "Heroes.png", FrameLength = 0.1f,
            LeftCoordinate = 0, TopCoordinate = 0, RightCoordinate = 16, BottomCoordinate = 16,
        });
        save.AnimationChains.Add(walk);
        save.AnimationChains.Add(idle);

        var outcomes = TiledTilesetSyncRunner.SyncAll(save, achxPath, [tsxPath]);

        Assert.False(outcomes[0].Success);
        var error = Assert.IsType<System.InvalidOperationException>(outcomes[0].Error);
        Assert.Contains("Walk", error.Message);
        Assert.Contains("Idle", error.Message);
    }
}
