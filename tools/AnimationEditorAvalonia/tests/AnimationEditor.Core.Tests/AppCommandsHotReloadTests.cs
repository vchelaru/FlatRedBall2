using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.HotReload;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Verifies hot-reload behavior in <see cref="AppCommands"/>:
/// reload failures surface through <see cref="IAppCommands.HotReloadFailed"/>
/// and watcher synchronization starts/restarts correctly.
/// </summary>
[Collection("SequentialSingletons")]
public class AppCommandsHotReloadTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = TestHelpers.SetupFreshAcls();

    public void Dispose() => _dir.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string WriteMinimalAchx(string chainName = "Idle")
    {
        var path = Path.Combine(_dir.Path, $"{chainName}.achx");
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        acls.AnimationChains.Add(new AnimationChainSave { Name = chainName });
        acls.Save(path);
        return path;
    }

    private string WriteConflictMarkerFile()
    {
        var path = Path.Combine(_dir.Path, "conflict.achx");
        File.WriteAllText(path,
            "<<<<<<< HEAD\n<?xml version=\"1.0\"?><AnimationChainList />\n=======\n<?xml version=\"1.0\"?><AnimationChainList />\n>>>>>>>");
        return path;
    }

    private string WriteCorruptFile()
    {
        var path = Path.Combine(_dir.Path, "corrupt.achx");
        File.WriteAllText(path, "this is not valid xml");
        return path;
    }

    // ── ReloadAchxFromDisk: failure handling ─────────────────────────────────

    [Fact]
    public void ReloadAchxFromDisk_ConflictMarkerFile_FiresHotReloadFailed()
    {
        string? capturedPath = null;
        _ctx.AppCommands.HotReloadFailed += (path, _) => capturedPath = path;

        _ctx.AppCommands.ReloadAchxFromDisk(WriteConflictMarkerFile());

        Assert.NotNull(capturedPath);
    }

    [Fact]
    public void ReloadAchxFromDisk_ConflictMarkerFile_MessageMentionsConflict()
    {
        string? capturedMsg = null;
        _ctx.AppCommands.HotReloadFailed += (_, msg) => capturedMsg = msg;

        _ctx.AppCommands.ReloadAchxFromDisk(WriteConflictMarkerFile());

        Assert.NotNull(capturedMsg);
        Assert.Contains("conflict", capturedMsg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReloadAchxFromDisk_CorruptFile_FiresHotReloadFailed()
    {
        bool fired = false;
        _ctx.AppCommands.HotReloadFailed += (_, __) => fired = true;

        _ctx.AppCommands.ReloadAchxFromDisk(WriteCorruptFile());

        Assert.True(fired);
    }

    [Fact]
    public void ReloadAchxFromDisk_MangledFile_DoesNotChangeAnimationChainListSave()
    {
        var sentinel = new AnimationChainListSave();
        _ctx.ProjectManager.AnimationChainListSave = sentinel;

        _ctx.AppCommands.ReloadAchxFromDisk(WriteConflictMarkerFile());

        Assert.Same(sentinel, _ctx.ProjectManager.AnimationChainListSave);
    }

    [Fact]
    public void ReloadAchxFromDisk_MangledFile_DoesNotClearUndoStack()
    {
        var path = WriteMinimalAchx("Before");
        _ctx.AppCommands.LoadAnimationChain(path);
        _ctx.AppCommands.AddAnimationChainWithName("Extra");

        bool wasCleared = false;
        _ctx.UndoManager.StackChanged += () => wasCleared = true;

        _ctx.AppCommands.ReloadAchxFromDisk(WriteConflictMarkerFile());
        wasCleared = false;
        _ctx.AppCommands.ReloadAchxFromDisk(WriteConflictMarkerFile());

        Assert.False(wasCleared);
    }

    [Fact]
    public void ReloadAchxFromDisk_MangledFile_DoesNotFireLoadFailed()
    {
        bool loadFailedFired = false;
        _ctx.AppCommands.LoadFailed += (_, __) => loadFailedFired = true;

        _ctx.AppCommands.ReloadAchxFromDisk(WriteConflictMarkerFile());

        Assert.False(loadFailedFired);
    }

    [Fact]
    public void ReloadAchxFromDisk_ValidFile_LoadsNewContent()
    {
        var path = WriteMinimalAchx("Walk");

        _ctx.AppCommands.ReloadAchxFromDisk(path);

        var acls = _ctx.ProjectManager.AnimationChainListSave;
        Assert.NotNull(acls);
        Assert.Single(acls!.AnimationChains);
        Assert.Equal("Walk", acls.AnimationChains[0].Name);
    }

    [Fact]
    public void ReloadAchxFromDisk_ValidFile_DoesNotFireHotReloadFailed()
    {
        bool fired = false;
        _ctx.AppCommands.HotReloadFailed += (_, __) => fired = true;

        _ctx.AppCommands.ReloadAchxFromDisk(WriteMinimalAchx("Run"));

        Assert.False(fired);
    }

    // ── SyncHotReloadWatcher: watcher start/update behavior ──────────────────

    [Fact]
    public void SyncHotReloadWatcher_UnsavedProject_PassesEmptyAchxPath()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var spy = new SpyHotReloadWatcher();
        ctx.AppCommands.HotReloadWatcher = spy;
        ctx.ProjectManager.FileName = null;

        ctx.AppCommands.SyncHotReloadWatcher();

        Assert.Equal(string.Empty, spy.LastStartAchxPath);
    }

    [Fact]
    public void SyncHotReloadWatcher_UnsavedProjectWithAbsolutePng_StartsWatchingThatPng()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var spy = new SpyHotReloadWatcher();
        ctx.AppCommands.HotReloadWatcher = spy;
        ctx.ProjectManager.FileName = null;

        var pngPath = Path.Combine(Path.GetTempPath(), "Sprites", "hero.png");
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(TestHelpers.MakeFrame(pngPath));
        ctx.Acls.AnimationChains.Add(chain);

        ctx.AppCommands.SyncHotReloadWatcher();

        Assert.NotNull(spy.LastStartPngPaths);
        Assert.Contains(pngPath, spy.LastStartPngPaths!, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void SyncHotReloadWatcher_UnsavedProjectNoPngs_StartsWithEmptyPngList()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var spy = new SpyHotReloadWatcher();
        ctx.AppCommands.HotReloadWatcher = spy;
        ctx.ProjectManager.FileName = null;

        ctx.Acls.AnimationChains.Add(new AnimationChainSave { Name = "Idle" });

        ctx.AppCommands.SyncHotReloadWatcher();

        Assert.NotNull(spy.LastStartPngPaths);
        Assert.Empty(spy.LastStartPngPaths!);
    }

    [Fact]
    public void SyncHotReloadWatcher_SavedProject_PassesAchxPath()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var spy = new SpyHotReloadWatcher();
        ctx.AppCommands.HotReloadWatcher = spy;
        var achxPath = Path.Combine(Path.GetTempPath(), "proj", "anim.achx");
        ctx.ProjectManager.FileName = achxPath;

        ctx.AppCommands.SyncHotReloadWatcher();

        Assert.Equal(achxPath, spy.LastStartAchxPath);
    }

    [Fact]
    public void SyncHotReloadWatcher_SavedProjectWithRelativePng_ResolvesAbsolutePath()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var spy = new SpyHotReloadWatcher();
        ctx.AppCommands.HotReloadWatcher = spy;
        var achxPath = Path.Combine(Path.GetTempPath(), "proj", "anim.achx");
        ctx.ProjectManager.FileName = achxPath;

        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(TestHelpers.MakeFrame("sprites/run.png"));
        ctx.Acls.AnimationChains.Add(chain);

        ctx.AppCommands.SyncHotReloadWatcher();

        Assert.NotNull(spy.LastStartPngPaths);
        var expectedAbs = Path.Combine(Path.GetDirectoryName(achxPath)!, "sprites/run.png");
        Assert.Contains(expectedAbs, spy.LastStartPngPaths!, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void SyncHotReloadWatcher_SavedProjectWithAssociatedTsx_PassesTsxPaths()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var spy = new SpyHotReloadWatcher();
        ctx.AppCommands.HotReloadWatcher = spy;
        var achxPath = Path.Combine(_dir.Path, "anim.achx");
        var tsxPath = Path.Combine(_dir.Path, "Heroes.tsx");
        ctx.ProjectManager.FileName = achxPath;
        ctx.IoManager.AddAssociatedTiledTilesetPath(achxPath, tsxPath);

        ctx.AppCommands.SyncHotReloadWatcher();

        Assert.NotNull(spy.LastStartTsxPaths);
        Assert.Contains(new FilePath(tsxPath), spy.LastStartTsxPaths!.Select(p => new FilePath(p)));
    }

    // ── AddAssociatedTiledTileset: watcher sync ───────────────────────────────

    [Fact]
    public void AddAssociatedTiledTileset_NewAssociation_UpdatesWatcherTsxList()
    {
        var spy = new SpyHotReloadWatcher();
        _ctx.AppCommands.HotReloadWatcher = spy;
        var achxPath = Path.Combine(_dir.Path, "hero.achx");
        var tsxPath = Path.Combine(_dir.Path, "Heroes.tsx");
        _ctx.ProjectManager.FileName = achxPath;

        _ctx.AppCommands.AddAssociatedTiledTileset(tsxPath);

        Assert.NotNull(spy.LastUpdateAssociatedTsxPaths);
        Assert.Contains(new FilePath(tsxPath), spy.LastUpdateAssociatedTsxPaths!.Select(p => new FilePath(p)));
    }

    // ── WireHotReloadWatcher: Tiled-sync-source-changed wiring (issue #1139) ─

    [Fact]
    public void WireHotReloadWatcher_AssociatedTsxChangedOnDisk_RaisesTiledSyncSourceChangedOnDisk()
    {
        var spy = new SpyHotReloadWatcher();
        _ctx.AppCommands.HotReloadWatcher = spy;
        _ctx.AppCommands.WireHotReloadWatcher();

        string? raisedPath = null;
        _ctx.AppCommands.TiledSyncSourceChangedOnDisk += p => raisedPath = p;
        spy.RaiseAssociatedTsxChangedOnDisk("/some/Heroes.tsx");

        Assert.Equal("/some/Heroes.tsx", raisedPath);
    }

    [Fact]
    public void WireHotReloadWatcher_TiledSyncChangedOnDisk_RaisesTiledSyncSourceChangedOnDisk()
    {
        var spy = new SpyHotReloadWatcher();
        _ctx.AppCommands.HotReloadWatcher = spy;
        _ctx.ProjectManager.FileName = Path.Combine(_dir.Path, "hero.achx");
        _ctx.AppCommands.WireHotReloadWatcher();

        string? raisedPath = null;
        _ctx.AppCommands.TiledSyncSourceChangedOnDisk += p => raisedPath = p;
        spy.RaiseTiledSyncChangedOnDisk(Path.Combine(_dir.Path, "hero.tiledsync"));

        Assert.Equal(Path.Combine(_dir.Path, "hero.tiledsync"), raisedPath);
    }

    [Fact]
    public void WireHotReloadWatcher_TiledSyncChangedOnDisk_RefreshesWatcherTsxList()
    {
        var spy = new SpyHotReloadWatcher();
        _ctx.AppCommands.HotReloadWatcher = spy;
        var achxPath = Path.Combine(_dir.Path, "hero.achx");
        var tsxPath = Path.Combine(_dir.Path, "Heroes.tsx");
        _ctx.ProjectManager.FileName = achxPath;
        // Simulates a teammate's git pull adding this association directly to disk.
        _ctx.IoManager.AddAssociatedTiledTilesetPath(achxPath, tsxPath);
        _ctx.AppCommands.WireHotReloadWatcher();

        spy.RaiseTiledSyncChangedOnDisk(Path.Combine(_dir.Path, "hero.tiledsync"));

        Assert.NotNull(spy.LastUpdateAssociatedTsxPaths);
        Assert.Contains(new FilePath(tsxPath), spy.LastUpdateAssociatedTsxPaths!.Select(p => new FilePath(p)));
    }

    private sealed class SpyHotReloadWatcher : IHotReloadWatcher
    {
        public string? LastStartAchxPath;
        public List<string>? LastStartPngPaths;
        public List<string>? LastStartTsxPaths;
        public List<string>? LastUpdateAssociatedTsxPaths;

        public event Action<string>? AchxChangedOnDisk { add { } remove { } }
        public event Action<string>? PngChangedOnDisk { add { } remove { } }
        public event Action<string>? AchxDeletedOnDisk { add { } remove { } }
        public event Action<string>? TiledSyncChangedOnDisk;
        public event Action<string>? AssociatedTsxChangedOnDisk;

        public bool IsEnabled { get; set; } = true;

        public void StartWatching(string achxPath, IEnumerable<string> pngPaths, IEnumerable<string> tsxPaths)
        {
            LastStartAchxPath = achxPath;
            LastStartPngPaths = new List<string>(pngPaths);
            LastStartTsxPaths = new List<string>(tsxPaths);
        }

        public void UpdatePngList(IEnumerable<string> newPngPaths) { }
        public void UpdateAssociatedTsxPaths(IEnumerable<string> newTsxPaths) =>
            LastUpdateAssociatedTsxPaths = new List<string>(newTsxPaths);
        public void StopWatching() { }
        public void RecordOwnSave(string filePath) { }
        public void Dispose() { }

        public void RaiseTiledSyncChangedOnDisk(string path) => TiledSyncChangedOnDisk?.Invoke(path);
        public void RaiseAssociatedTsxChangedOnDisk(string path) => AssociatedTsxChangedOnDisk?.Invoke(path);
    }
}
