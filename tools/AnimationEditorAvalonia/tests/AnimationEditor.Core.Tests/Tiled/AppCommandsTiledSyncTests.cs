using AnimationEditor.Core.IO;
using AnimationEditor.Core.Tests;
using DotTiled;
using DotTiled.Serialization;
using FlatRedBall2.AnimationEditorCommon;
using System;
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
        var tileset = new Tileset
        {
            Name = "Heroes", TileWidth = 16, TileHeight = 16, TileCount = 64, Columns = 4,
            // Width/height are required for the mapper to convert the achx's UV frame
            // coordinates back to pixels -- ProjectManager.AnimationChainListSave is always UV
            // in memory (see ProjectManager's own doc comment), so this test's frame is UV too.
            Image = new Image { Source = "Heroes.png", Width = 64, Height = 16 },
        };
        var path = Path.Combine(tempDir, "Heroes.tsx");
        AnimationEditor.Core.Tiled.TsxWriter.Write(tileset, path);
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
        // "Heroes.png" isn't a real file on disk (only the .tsx fixture is) -- this test is about
        // Tiled sync wiring, not on-disk pixel-coordinate conversion (#1135), so stay in UV.
        ctx.ProjectManager.OnDiskCoordinateType = TextureCoordinateType.UV;
        ctx.AppCommands.AddAssociatedTiledTileset(tsxPath);

        // Tile 0 occupies pixels [0,16)x[0,16) of the 64x16 fixture texture -> UV [0,0.25)x[0,1).
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "Heroes.png", FrameLength = 0.1f,
            LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 0.25f, BottomCoordinate = 1f,
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
        // "Heroes.png" isn't a real file on disk (only the .tsx fixture is) -- this test is about
        // Tiled sync wiring, not on-disk pixel-coordinate conversion (#1135), so stay in UV.
        ctx.ProjectManager.OnDiskCoordinateType = TextureCoordinateType.UV;
        ctx.AppCommands.AddAssociatedTiledTileset(tsxPath);
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "Heroes.png", FrameLength = 0.1f,
            LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 0.25f, BottomCoordinate = 1f,
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
            LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 0.25f, BottomCoordinate = 1f,
        });
        ctx.AppCommands.SaveCurrentAnimationChainList(achxPath);

        bool succeeded = false;
        ctx.AppCommands.TiledSyncSucceeded += (_, __) => succeeded = true;
        ctx.AppCommands.SaveCurrentAnimationChainList(achxPath);

        Assert.False(succeeded);
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
