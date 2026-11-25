using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.HotReload;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

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

    // ── The watcher's report ────────────────────────────────────────────────

    private SpyHotReloadWatcher WireSpy()
    {
        var spy = new SpyHotReloadWatcher();
        _ctx.AppCommands.HotReloadWatcher = spy;
        _ctx.AppCommands.WireHotReloadWatcher();
        return spy;
    }

    private string OnlyChainName => _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.ShouldHaveSingleItem().Name;

    private void Rewrite(string path, string chainName) =>
        File.Copy(WriteMinimalAchx(chainName), path, overwrite: true);

    [Fact]
    public void AchxChangedOnDisk_AfterAFailedSave_KeepsTheEditsAndTheFile_UntilAReload()
    {
        var spy = WireSpy();
        var path = WriteMinimalAchx("Before");
        _ctx.AppCommands.LoadAnimationChain(path);
        _ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0].Name = "Renamed";
        _ctx.UndoManager.MarkSaveFailed();
        var problems = new List<string>();
        _ctx.AppCommands.SaveFailed += problems.Add;
        Rewrite(path, "Outside");

        spy.RaiseAchxChanged(path);

        OnlyChainName.ShouldBe("Renamed");
        problems.ShouldHaveSingleItem().ShouldContain("Use File > Reload from Disk");
        _ctx.AppCommands.SaveCurrentAnimationChainList();
        AnimationChainListSave.FromFile(path).AnimationChains[0].Name.ShouldBe("Outside");

        _ctx.AppCommands.ReloadAchxFromDisk(path);
        OnlyChainName.ShouldBe("Outside");
        Rewrite(path, "Later");
        spy.RaiseAchxChanged(path);
        OnlyChainName.ShouldBe("Later");
    }

    // The save pause after a reload that failed is a failed save too.
    [Fact]
    public void AchxChangedOnDisk_WhenABrokenFileWithEditsPausedIsFixed_KeepsTheEdits()
    {
        var spy = WireSpy();
        var path = WriteMinimalAchx("Before");
        _ctx.AppCommands.LoadAnimationChain(path);
        File.WriteAllText(path, "<AnimationChainArrayS");
        spy.RaiseAchxChanged(path);
        _ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0].Name = "Renamed";
        var problems = new List<string>();
        _ctx.AppCommands.SaveFailed += problems.Add;
        _ctx.AppCommands.SaveCurrentAnimationChainList();
        Rewrite(path, "Fixed");

        spy.RaiseAchxChanged(path);

        OnlyChainName.ShouldBe("Renamed");
        problems.Count.ShouldBe(2);
        problems.ShouldAllBe(p => p.Contains("File > Reload from Disk"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)] // An Untitled document has no file; the watcher still has the previous one's.
    public void AchxChangedOnDisk_ForAFileThatIsNoLongerOpen_IsIgnored(bool nextHasAFile)
    {
        var spy = WireSpy();
        var queue = new List<Action>();
        _ctx.AppCommands.DoOnUiThread = queue.Add;
        var left = WriteMinimalAchx("Left");
        _ctx.AppCommands.LoadAnimationChain(left);
        spy.RaiseAchxChanged(left);
        if (nextHasAFile)
            _ctx.AppCommands.LoadAnimationChain(WriteMinimalAchx("Next"));
        else
        {
            _ctx.ProjectManager.ResetToBlankDocument();
            _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(new AnimationChainSave { Name = "Next" });
        }
        string? open = _ctx.ProjectManager.FileName;

        queue.ForEach(action => action());

        OnlyChainName.ShouldBe("Next");
        _ctx.ProjectManager.FileName.ShouldBe(open);
    }

    // The watcher reports a canonical path, the document keeps the path it was opened with.
    [Fact]
    public void AchxChangedOnDisk_UnderAnotherSpellingOfTheOpenPath_Reloads()
    {
        var spy = WireSpy();
        var path = WriteMinimalAchx("Before");
        Directory.CreateDirectory(Path.Combine(_dir.Path, "sub"));
        _ctx.AppCommands.LoadAnimationChain(Path.Combine(_dir.Path, "sub", "..", "Before.achx"));
        Rewrite(path, "Outside");

        spy.RaiseAchxChanged(path.Replace('\\', '/'));

        OnlyChainName.ShouldBe("Outside");
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

    /// <summary>Also reused by <c>AppCommandsHotReloadTsxTests</c> to verify the tsx open path
    /// wires the watcher the same way the achx open path does.</summary>
    internal sealed class SpyHotReloadWatcher : IHotReloadWatcher
    {
        public string? LastStartAchxPath;
        public List<string>? LastStartPngPaths;

        public event Action<string>? AchxChangedOnDisk;
        public void RaiseAchxChanged(string path) => AchxChangedOnDisk?.Invoke(path);
        public event Action<string>? PngChangedOnDisk { add { } remove { } }
        public event Action<string>? AchxDeletedOnDisk { add { } remove { } }
        public event Action<string>? SourceChangedOnDisk { add { } remove { } }

        public bool IsEnabled { get; set; } = true;

        public void StartWatching(string achxPath, IEnumerable<string> pngPaths)
        {
            LastStartAchxPath = achxPath;
            LastStartPngPaths = new List<string>(pngPaths);
        }

        public void UpdatePngList(IEnumerable<string> newPngPaths) { }
        public void UpdateSourceList(IEnumerable<string> sourcePaths) { }
        public void StopWatching() { }
        public readonly List<string> OwnSaves = new();
        public void RunOwnSave(string filePath, Action write)
        {
            OwnSaves.Add(filePath);
            write();
        }
        public void Dispose() { }
    }
}
