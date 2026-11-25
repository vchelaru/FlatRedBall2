using AnimationEditor.Core.IO;
using AnimationEditor.Core.Tests;
using DotTiled;
using DotTiled.Serialization;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests.Tiled;

/// <summary>
/// End-to-end coverage of the save-hook wiring (issue #1133): saving the current .achx, when it
/// has an associated .tsx, syncs the .tsx on disk in the same call. <see cref="AchjToTiledAnimationMapperTests"/>,
/// <see cref="TilesetAnimationSyncTests"/>, and <see cref="TiledTilesetSyncRunnerTests"/> already
/// cover the mapping/apply/write pieces in isolation -- this only proves they're actually wired
/// together through <c>AppCommands.SaveCurrentAnimationChainList</c>.
/// </summary>
public class AppCommandsTiledSyncTests
{
    private static string WriteFixtureTileset(string tempDir)
    {
        var path = Path.Combine(tempDir, "Heroes.tsx");
        File.WriteAllText(path, "<?xml version=\"1.0\" encoding=\"UTF-8\"?><tileset name=\"Heroes\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"64\" columns=\"4\"><image source=\"Heroes.png\" width=\"64\" height=\"16\"/></tileset>");
        return path;
    }

    [Fact]
    public void AddAssociatedTiledTileset_NoProjectSavedYet_DoesNotThrow()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.ProjectManager.FileName = null;

        var ex = Record.Exception(() => ctx.AppCommands.AddAssociatedTiledTileset("C:/Some/Heroes.tsx"));

        Assert.Null(ex);
    }

    // ── Native-tsx / achx-push coexistence (issue #1147) ────────────────────────────
    // Symmetric to ProjectManagerTsxProjectTests' LoadTsxProject guard: a .tsx currently open as a
    // native-tsx project in some tab must not also be pointed at by a new achx-push association.

    [Fact]
    public void AddAssociatedTiledTileset_TargetTsxOpenAsNativeProjectElsewhere_ThrowsInsteadOfSilentlyCoexisting()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var achxPath = Path.Combine(dir.Path, "Hero.achx");
        var tsxPath = WriteFixtureTileset(dir.Path);
        ctx.ProjectManager.FileName = achxPath;
        ctx.AppCommands.IsTsxPathOpenAsNativeProject = path => new FilePath(path) == new FilePath(tsxPath);

        var ex = Assert.Throws<InvalidOperationException>(
            () => ctx.AppCommands.AddAssociatedTiledTileset(tsxPath));

        Assert.Contains(tsxPath, ex.Message);
        Assert.Empty(ctx.IoManager.GetAssociatedTiledTilesetPaths(achxPath));
    }

    [Fact]
    public async Task AddAssociatedTiledTilesetViaDialogAsync_TargetTsxOpenAsNativeProjectElsewhere_RaisesTiledSyncFailedInsteadOfAssociating()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var achxPath = Path.Combine(dir.Path, "Hero.achx");
        var tsxPath = WriteFixtureTileset(dir.Path);
        ctx.ProjectManager.FileName = achxPath;
        ctx.AppCommands.FileDialogService = new StubFileDialogService(tsxPath);
        ctx.AppCommands.IsTsxPathOpenAsNativeProject = path => new FilePath(path) == new FilePath(tsxPath);

        string? failedPath = null;
        Exception? failedEx = null;
        ctx.AppCommands.TiledSyncFailed += (path, ex) => { failedPath = path; failedEx = ex; };
        await ctx.AppCommands.AddAssociatedTiledTilesetViaDialogAsync();

        Assert.Equal(new FilePath(tsxPath), new FilePath(failedPath!));
        Assert.IsType<InvalidOperationException>(failedEx);
        Assert.Empty(ctx.IoManager.GetAssociatedTiledTilesetPaths(achxPath));
    }

    [Fact]
    public void AddAssociatedTiledTileset_DelegateNotWired_AssociatesNormally()
    {
        // Happy-path guard: a host that hasn't wired IsTsxPathOpenAsNativeProject (null delegate)
        // must keep associating exactly as before -- permissive default, matching
        // CanvasDefaultTexturePath's own null-means-"no info" convention.
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var achxPath = Path.Combine(dir.Path, "Hero.achx");
        var tsxPath = WriteFixtureTileset(dir.Path);
        ctx.ProjectManager.FileName = achxPath;

        ctx.AppCommands.AddAssociatedTiledTileset(tsxPath);

        Assert.Single(ctx.IoManager.GetAssociatedTiledTilesetPaths(achxPath));
    }

    [Fact]
    public void AddAssociatedTiledTileset_DelegateSaysNotOpenElsewhere_AssociatesNormally()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var achxPath = Path.Combine(dir.Path, "Hero.achx");
        var tsxPath = WriteFixtureTileset(dir.Path);
        ctx.ProjectManager.FileName = achxPath;
        ctx.AppCommands.IsTsxPathOpenAsNativeProject = _ => false;

        ctx.AppCommands.AddAssociatedTiledTileset(tsxPath);

        Assert.Single(ctx.IoManager.GetAssociatedTiledTilesetPaths(achxPath));
    }

    [Fact]
    public void SaveCurrentAnimationChainList_AssociatedTsx_SyncsAnimationOntoTile()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var tsxPath = WriteFixtureTileset(dir.Path);
        var achxPath = Path.Combine(dir.Path, "Hero.achx");
        ctx.ProjectManager.FileName = achxPath;
        ctx.AppCommands.AddAssociatedTiledTileset(tsxPath);

        // Tile 0 occupies pixels [0,16)x[0,16) of the 64x16 fixture texture.
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "Heroes.png", FrameLength = 0.1f,
            LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 16f, BottomCoordinate = 16f,
        });

        ctx.AppCommands.SaveCurrentAnimationChainList(achxPath);

        var reloaded = Loader.Default().LoadTileset(tsxPath);
        var tile0 = reloaded.Tiles.Single(t => t.ID == 0);
        Assert.Equal("Walk", tile0.GetProperty<StringProperty>("achjAnimationName").Value);
    }

    [Fact]
    public async Task AddAssociatedTiledTilesetViaDialogAsync_DialogCancelled_DoesNotAssociate()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        ctx.ProjectManager.FileName = Path.Combine(dir.Path, "Hero.achx");
        ctx.AppCommands.FileDialogService = new StubFileDialogService(null);

        await ctx.AppCommands.AddAssociatedTiledTilesetViaDialogAsync();

        Assert.Empty(ctx.IoManager.GetAssociatedTiledTilesetPaths(ctx.ProjectManager.FileName));
    }

    [Fact]
    public async Task AddAssociatedTiledTilesetViaDialogAsync_DialogReturnsPath_AssociatesTileset()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var tsxPath = WriteFixtureTileset(dir.Path);
        ctx.ProjectManager.FileName = Path.Combine(dir.Path, "Hero.achx");
        ctx.AppCommands.FileDialogService = new StubFileDialogService(tsxPath);

        await ctx.AppCommands.AddAssociatedTiledTilesetViaDialogAsync();

        var associated = ctx.IoManager.GetAssociatedTiledTilesetPaths(ctx.ProjectManager.FileName);
        Assert.Single(associated);
        Assert.Equal(new FilePath(tsxPath), new FilePath(associated[0]));
    }

    [Fact]
    public async Task AddAssociatedTiledTilesetViaDialogAsync_NoProjectSavedYet_DoesNotThrow()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.ProjectManager.FileName = null;
        ctx.AppCommands.FileDialogService = new StubFileDialogService("C:/Some/Heroes.tsx");

        var ex = await Record.ExceptionAsync(() => ctx.AppCommands.AddAssociatedTiledTilesetViaDialogAsync());

        Assert.Null(ex);
    }

    [Fact]
    public void SaveCurrentAnimationChainList_NoAssociatedTsx_DoesNotThrow()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var achxPath = Path.Combine(dir.Path, "Hero.achx");
        ctx.ProjectManager.FileName = achxPath;

        var ex = Record.Exception(() => ctx.AppCommands.SaveCurrentAnimationChainList(achxPath));

        Assert.Null(ex);
    }

    [Fact]
    public void SaveCurrentAnimationChainList_AssociatedTsxMissingFromDisk_RaisesTiledSyncFailed()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var achxPath = Path.Combine(dir.Path, "Hero.achx");
        var missingTsxPath = Path.Combine(dir.Path, "DoesNotExist.tsx");
        ctx.ProjectManager.FileName = achxPath;
        ctx.AppCommands.AddAssociatedTiledTileset(missingTsxPath);

        string? failedPath = null;
        ctx.AppCommands.TiledSyncFailed += (path, _) => failedPath = path;
        ctx.AppCommands.SaveCurrentAnimationChainList(achxPath);

        Assert.Equal(new FilePath(missingTsxPath), new FilePath(failedPath!));
        // The .achx save itself must still have succeeded despite the Tiled sync failure.
        Assert.True(File.Exists(achxPath));
    }

    // ── TiledSyncSucceeded: must reflect an actual write, not just "sync ran" ──────────
    // Autosave fires on nearly every edit, and most of those saves have nothing new to sync
    // (TilesetAnimationSyncResult.Changed false) -- firing "succeeded" on every one of them would
    // be a constant, meaningless flicker in the status bar. It should only fire when a write
    // actually happened.

    [Fact]
    public void SaveCurrentAnimationChainList_AssociatedTsxNewAnimation_RaisesTiledSyncSucceeded()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var tsxPath = WriteFixtureTileset(dir.Path);
        var achxPath = Path.Combine(dir.Path, "Hero.achx");
        ctx.ProjectManager.FileName = achxPath;
        ctx.AppCommands.AddAssociatedTiledTileset(tsxPath);
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "Heroes.png", FrameLength = 0.1f,
            LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 16f, BottomCoordinate = 16f,
        });

        bool succeeded = false;
        ctx.AppCommands.TiledSyncSucceeded += (_, __) => succeeded = true;
        ctx.AppCommands.SaveCurrentAnimationChainList(achxPath);

        Assert.True(succeeded);
    }

    [Fact]
    public void SaveCurrentAnimationChainList_AssociatedTsxAlreadyUpToDate_DoesNotRaiseTiledSyncSucceeded()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var tsxPath = WriteFixtureTileset(dir.Path);
        var achxPath = Path.Combine(dir.Path, "Hero.achx");
        ctx.ProjectManager.FileName = achxPath;
        ctx.AppCommands.AddAssociatedTiledTileset(tsxPath);
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "Heroes.png", FrameLength = 0.1f,
            LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 16f, BottomCoordinate = 16f,
        });
        ctx.AppCommands.SaveCurrentAnimationChainList(achxPath);

        bool succeeded = false;
        ctx.AppCommands.TiledSyncSucceeded += (_, __) => succeeded = true;
        ctx.AppCommands.SaveCurrentAnimationChainList(achxPath);

        Assert.False(succeeded);
    }

    private static AnimationFrameSave Tile0Frame(string textureName) =>
        new() { TextureName = textureName, FrameLength = 0.1f, RightCoordinate = 16f, BottomCoordinate = 16f };

    [Fact]
    public void SaveCurrentAnimationChainList_ChainLeftOutOfTheTileset_WarnsOnEverySaveUntilItIsFixed()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var achxPath = Path.Combine(dir.Path, "Hero.achx");
        ctx.ProjectManager.FileName = achxPath;
        ctx.AppCommands.AddAssociatedTiledTileset(WriteFixtureTileset(dir.Path));
        var frame = Tile0Frame("Heroes.png");
        TestHelpers.MakeChain(ctx.Acls, "Walk").Frames.Add(frame);
        var raised = new List<IReadOnlyList<string>>();
        ctx.AppCommands.TiledSyncWarned += raised.Add;
        IReadOnlyList<int> Save()
        {
            raised.Clear();
            ctx.AppCommands.SaveCurrentAnimationChainList(achxPath);
            return raised.Select(r => r.Count).ToList();
        }

        Save().ShouldBeEmpty();

        frame.TextureName = "sub/Heroes.png";
        Save().ShouldBe([1]);
        raised[0].Single().ShouldStartWith("Heroes.tsx: chain \"Walk\": ");
        Save().ShouldBe([0, 1]);

        frame.TextureName = "Heroes.png";
        Save().ShouldBe([0]);
        Save().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SaveCurrentAnimationChainList_AfterAWarning_WithNothingToWarnOf_ClearsTheWarningBeforeAnythingElse(bool hasOtherTileset)
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var achxPath = Path.Combine(dir.Path, "Hero.achx");
        ctx.ProjectManager.FileName = achxPath;
        ctx.AppCommands.AddAssociatedTiledTileset(WriteFixtureTileset(dir.Path));
        TestHelpers.MakeChain(ctx.Acls, "Walk").Frames.Add(Tile0Frame("sub/Heroes.png"));
        ctx.AppCommands.SaveCurrentAnimationChainList(achxPath);
        File.Delete(Path.Combine(dir.Path, "Hero.tiledsync"));
        if (hasOtherTileset) ctx.AppCommands.AddAssociatedTiledTileset(Path.Combine(dir.Path, "Missing.tsx"));
        var order = new List<string>();
        ctx.AppCommands.TiledSyncWarned += w => order.Add($"warned {w.Count}");
        ctx.AppCommands.TiledSyncFailed += (_, _) => order.Add("failed");

        ctx.AppCommands.SaveCurrentAnimationChainList(achxPath);

        order.ShouldBe(hasOtherTileset ? ["warned 0", "failed"] : ["warned 0"]);
    }

    [Fact]
    public void SaveCurrentAnimationChainList_UnreadableSyncFileAfterAWarning_ClearsTheWarningBeforeTheFailure()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var achxPath = Path.Combine(dir.Path, "Hero.achx");
        ctx.ProjectManager.FileName = achxPath;
        ctx.AppCommands.AddAssociatedTiledTileset(WriteFixtureTileset(dir.Path));
        TestHelpers.MakeChain(ctx.Acls, "Walk").Frames.Add(Tile0Frame("sub/Heroes.png"));
        ctx.AppCommands.SaveCurrentAnimationChainList(achxPath);
        File.WriteAllText(Path.Combine(dir.Path, "Hero.tiledsync"), "{ not valid json");
        var order = new List<string>();
        ctx.AppCommands.TiledSyncWarned += w => order.Add($"warned {w.Count}");
        ctx.AppCommands.TiledSyncFailed += (_, _) => order.Add("failed");

        ctx.AppCommands.SaveCurrentAnimationChainList(achxPath);

        order.ShouldBe(["warned 0", "failed"]);
    }

    [Fact]
    public void SaveCurrentAnimationChainList_TwoTilesetsWithTheSameFileName_WarningsTellThemApart()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var achxPath = Path.Combine(dir.Path, "Hero.achx");
        ctx.ProjectManager.FileName = achxPath;
        foreach (var folder in new[] { "forest", "cave" })
            ctx.AppCommands.AddAssociatedTiledTileset(WriteFixtureTileset(Directory.CreateDirectory(Path.Combine(dir.Path, folder)).FullName));
        TestHelpers.MakeChain(ctx.Acls, "Walk").Frames.Add(Tile0Frame("Heroes.png"));
        IReadOnlyList<string> raised = [];
        ctx.AppCommands.TiledSyncWarned += w => raised = w;

        ctx.AppCommands.SaveCurrentAnimationChainList(achxPath);

        raised.Select(w => w.Split(':')[0]).ShouldBe(["forest/Heroes.tsx", "cave/Heroes.tsx"]);
    }

    // Before textures were compared by path, a chain on forest's image was also pushed into cave's tileset.
    [Fact]
    public void SaveCurrentAnimationChainList_TileAnotherTilesetsChainHadPushedByName_IsRemovedWithoutWarning()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var achxPath = Path.Combine(dir.Path, "Hero.achx");
        ctx.ProjectManager.FileName = achxPath;
        var forestTsx = WriteFixtureTileset(Directory.CreateDirectory(Path.Combine(dir.Path, "forest")).FullName);
        var caveTsx = WriteFixtureTileset(Directory.CreateDirectory(Path.Combine(dir.Path, "cave")).FullName);
        var cave = Loader.Default().LoadTileset(caveTsx);
        cave.Tiles.Add(new Tile
        {
            ID = 0, Width = 0, Height = 0,
            Animation = [new Frame { TileID = 0, Duration = 100 }],
            Properties =
            [
                new StringProperty { Name = "achjAnimationName", Value = "Tree" },
                new StringProperty { Name = "achjSourceFile", Value = "../Hero.achx" },
            ],
        });
        AnimationEditor.Core.Tiled.TsxWriter.Write(cave, caveTsx);
        ctx.AppCommands.AddAssociatedTiledTileset(forestTsx);
        ctx.AppCommands.AddAssociatedTiledTileset(caveTsx);
        TestHelpers.MakeChain(ctx.Acls, "Tree").Frames.Add(Tile0Frame("forest/Heroes.png"));
        var order = new List<string>();
        ctx.AppCommands.TiledSyncSucceeded += (path, _) => order.Add("success " + new FilePath(path).RelativeTo(new FilePath(dir.Path)));
        ctx.AppCommands.TiledSyncWarned += _ => order.Add("warning");
        ctx.AppCommands.TiledSyncFailed += (_, _) => order.Add("failure");

        ctx.AppCommands.SaveCurrentAnimationChainList(achxPath);

        order.ShouldBe(["success forest/Heroes.tsx", "success cave/Heroes.tsx"]);
        Loader.Default().LoadTileset(caveTsx).Tiles.ShouldBeEmpty();
        Loader.Default().LoadTileset(forestTsx).Tiles.Single().ID.ShouldBe(0u);
    }

    [Fact]
    public void SaveCurrentAnimationChainList_SeveralTilesets_RaisesSuccessThenWarningThenFailure()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var achxPath = Path.Combine(dir.Path, "Hero.achx");
        ctx.ProjectManager.FileName = achxPath;
        ctx.AppCommands.AddAssociatedTiledTileset(Path.Combine(dir.Path, "Missing.tsx"));
        ctx.AppCommands.AddAssociatedTiledTileset(WriteFixtureTileset(dir.Path));
        var walk = TestHelpers.MakeChain(ctx.Acls, "Walk");
        walk.Frames.Add(new AnimationFrameSave { TextureName = "Heroes.png", FrameLength = 0.1f, RightCoordinate = 16f, BottomCoordinate = 16f });
        walk.Frames.Add(new AnimationFrameSave { TextureName = "sub/Heroes.png", FrameLength = 0.1f, RightCoordinate = 16f, BottomCoordinate = 16f });
        var order = new List<string>();
        ctx.AppCommands.TiledSyncSucceeded += (_, _) => order.Add("success");
        ctx.AppCommands.TiledSyncWarned += _ => order.Add("warning");
        ctx.AppCommands.TiledSyncFailed += (_, _) => order.Add("failure");

        ctx.AppCommands.SaveCurrentAnimationChainList(achxPath);

        order.ShouldBe(["success", "warning", "failure"]);
    }

    // The push already reported a tile it could not take over, but nothing showed it.
    [Fact]
    public void SaveCurrentAnimationChainList_TileOwnedByAnotherSource_RaisesTiledSyncWarned()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var tsxPath = WriteFixtureTileset(dir.Path);
        foreach (var name in new[] { "Other.achx", "Hero.achx" })
        {
            ctx.ProjectManager.FileName = Path.Combine(dir.Path, name);
            ctx.AppCommands.AddAssociatedTiledTileset(tsxPath);
        }
        TestHelpers.MakeChain(ctx.Acls, "Walk").Frames.Add(new AnimationFrameSave { TextureName = "Heroes.png", FrameLength = 0.1f, RightCoordinate = 16f, BottomCoordinate = 16f });
        ctx.AppCommands.SaveCurrentAnimationChainList(Path.Combine(dir.Path, "Other.achx"));
        IReadOnlyList<string> raised = [];
        ctx.AppCommands.TiledSyncWarned += w => raised = w;

        ctx.AppCommands.SaveCurrentAnimationChainList(Path.Combine(dir.Path, "Hero.achx"));

        raised.Single().ShouldContain("already owned by \"Other.achx\"");
    }

    // One .tiledsync serves both files, so both push.
    [Fact]
    public void SaveCurrentAnimationChainList_AchxAndAchjOfOneName_TheFirstKeepsTheTileAndTheOtherWarnsEverySave()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var tsxPath = WriteFixtureTileset(dir.Path);
        var achxPath = Path.Combine(dir.Path, "Hero.achx");
        var achjPath = Path.Combine(dir.Path, "Hero.achj");
        ctx.ProjectManager.FileName = achxPath;
        ctx.AppCommands.AddAssociatedTiledTileset(tsxPath);
        ctx.IoManager.GetAssociatedTiledTilesetPaths(achjPath).Count.ShouldBe(1);
        var chain = TestHelpers.MakeChain(ctx.Acls, "FromAchx");
        chain.Frames.Add(Tile0Frame("Heroes.png"));
        IReadOnlyList<string> raised = [];
        ctx.AppCommands.TiledSyncWarned += w => raised = w;

        foreach (var (path, warns) in new[] { (achxPath, false), (achjPath, true), (achxPath, false), (achjPath, true) })
        {
            ctx.ProjectManager.FileName = path;
            chain.Name = path == achxPath ? "FromAchx" : "FromAchj";
            ctx.AppCommands.SaveCurrentAnimationChainList(path);

            raised.Count.ShouldBe(warns ? 1 : 0);
            if (warns) raised.Single().ShouldContain("already owned by \"Hero.achx\"");
            var tile = Loader.Default().LoadTileset(tsxPath).Tiles.Single();
            tile.GetProperty<StringProperty>("achjSourceFile").Value.ShouldBe("Hero.achx");
            tile.GetProperty<StringProperty>("achjAnimationName").Value.ShouldBe("FromAchx");
        }
    }

    // The .achx it was saved from still has Bee, whose tile only the .achx may clear.
    [Fact]
    public async Task SaveAs_AchxToAchjOfTheSameName_WarnsAndLeavesTheTilesUntilTheAchxIsDeleted()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var tsxPath = WriteFixtureTileset(dir.Path);
        var achxPath = Path.Combine(dir.Path, "Hero.achx");
        var achjPath = Path.Combine(dir.Path, "Hero.achj");
        ctx.ProjectManager.FileName = achxPath;
        ctx.AppCommands.AddAssociatedTiledTileset(tsxPath);
        TestHelpers.MakeChain(ctx.Acls, "Walk").Frames.Add(Tile0Frame("Heroes.png"));
        var bee = TestHelpers.MakeChain(ctx.Acls, "Bee");
        var beeFrame = Tile0Frame("Heroes.png");
        beeFrame.LeftCoordinate = 16f;
        beeFrame.RightCoordinate = 32f;
        bee.Frames.Add(beeFrame);
        ctx.AppCommands.SaveCurrentAnimationChainList(achxPath);
        Loader.Default().LoadTileset(tsxPath).Tiles.Count.ShouldBe(2);
        var pushed = File.ReadAllText(tsxPath);
        ctx.Acls.AnimationChains.Remove(bee);
        ctx.AppCommands.FileDialogService = new StubFileDialogService(achjPath);
        IReadOnlyList<string> raised = [];
        ctx.AppCommands.TiledSyncWarned += w => raised = w;

        (await ctx.AppCommands.SaveCurrentAnimationChainListAsync()).ShouldBeTrue();

        raised.Single().ShouldContain("already owned by \"Hero.achx\"");
        File.ReadAllText(tsxPath).ShouldBe(pushed);

        File.Delete(achxPath);
        ctx.AppCommands.SaveCurrentAnimationChainList(achjPath);

        raised.ShouldBeEmpty();
        Loader.Default().LoadTileset(tsxPath).Tiles.Single().GetProperty<StringProperty>("achjSourceFile").Value.ShouldBe("Hero.achj");
    }

    [Fact]
    public void SaveCurrentAnimationChainList_CorruptTiledSyncFile_RaisesTiledSyncFailed()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var achxPath = Path.Combine(dir.Path, "Hero.achx");
        File.WriteAllText(Path.Combine(dir.Path, "Hero.tiledsync"), "{ not valid json");
        ctx.ProjectManager.FileName = achxPath;

        string? failedPath = null;
        ctx.AppCommands.TiledSyncFailed += (path, _) => failedPath = path;
        ctx.AppCommands.SaveCurrentAnimationChainList(achxPath);

        Assert.Equal(new FilePath(achxPath), new FilePath(failedPath!));
        // The .achx save itself must still have succeeded despite the corrupt .tiledsync.
        Assert.True(File.Exists(achxPath));
    }
}
