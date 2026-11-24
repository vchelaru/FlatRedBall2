using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Verifies per-tab in-memory model caching avoids redundant disk loads on tab switch.
/// </summary>
[Collection("SequentialSingletons")]
public class TabSwitchCacheTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly CountingProjectManager _pm = new();
    private readonly TestServices _ctx;

    public TabSwitchCacheTests()
    {
        _ctx = new TestServices(_pm);
    }

    public void Dispose() => _dir.Dispose();

    private string WriteAchx(string fileName, string chainName, int frameCount = 0)
    {
        var path = Path.Combine(_dir.Path, fileName);
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var chain = new AnimationChainSave { Name = chainName };
        for (int i = 0; i < frameCount; i++)
            chain.Frames.Add(new AnimationFrameSave { TextureName = $"tex{i}.png", FrameLength = 0.1f });
        acls.AnimationChains.Add(chain);
        acls.Save(path);
        return path;
    }

    [Fact]
    public void CaptureTabEditorState_PreservesEditsAcrossCacheRoundTrip()
    {
        string path = WriteAchx("edited.achx", "Idle");
        var tab = new TabEntry(new FilePath(path));

        _pm.LoadAnimationChain(new FilePath(path));
        _ctx.AppCommands.CaptureTabEditorState(tab);

        _pm.AnimationChainListSave!.AnimationChains[0].Name = "Renamed";
        _ctx.AppCommands.CaptureTabEditorState(tab);

        TabEditorCache.ApplyToProject(tab, _pm);
        Assert.Equal("Renamed", _pm.AnimationChainListSave!.AnimationChains[0].Name);
    }

    [Fact]
    public async Task TryActivateTabFromCache_AfterInitialLoad_DoesNotReloadFromDisk()
    {
        string pathA = WriteAchx("a.achx", "Walk");
        string pathB = WriteAchx("b.achx", "Run");
        var tabA = new TabEntry(new FilePath(pathA));
        var tabB = new TabEntry(new FilePath(pathB));

        await _ctx.AppCommands.OpenAchxWorkflowAsync(pathA);
        _ctx.AppCommands.CaptureTabEditorState(tabA);
        Assert.Equal(1, _pm.LoadCallCount);

        await _ctx.AppCommands.OpenAchxWorkflowAsync(pathB);
        _ctx.AppCommands.CaptureTabEditorState(tabB);
        Assert.Equal(2, _pm.LoadCallCount);

        Assert.True(_ctx.AppCommands.TryActivateTabFromCache(tabA));
        Assert.Equal(2, _pm.LoadCallCount);
        Assert.Equal("Walk", _ctx.SelectedState.SelectedChain!.Name);
    }

    [Fact]
    public async Task TryActivateTabFromCache_WhenDiskFileIsNewer_ReturnsFalseAndReloads()
    {
        string path = WriteAchx("stale.achx", "Before");
        var tab = new TabEntry(new FilePath(path));

        await _ctx.AppCommands.OpenAchxWorkflowAsync(path);
        _ctx.AppCommands.CaptureTabEditorState(tab);
        Assert.Equal(1, _pm.LoadCallCount);

        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        acls.AnimationChains.Add(new AnimationChainSave { Name = "After" });
        acls.Save(path);

        Assert.False(_ctx.AppCommands.TryActivateTabFromCache(tab));
        Assert.Equal(1, _pm.LoadCallCount);

        await _ctx.AppCommands.ActivateTabContentAsync(tab);
        Assert.Equal(2, _pm.LoadCallCount);
        Assert.Equal("After", _ctx.SelectedState.SelectedChain!.Name);
    }

    // Two writes in one clock tick share a time, and a copied-in file keeps its own older one.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task TryActivateTabFromCache_WhenDiskContentChangedWithoutANewerTime_ReturnsFalse(int daysFromCaptured)
    {
        var captured = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        string path = WriteAchx("stale.achx", "Before");
        File.SetLastWriteTimeUtc(path, captured);
        var tab = new TabEntry(new FilePath(path));
        await _ctx.AppCommands.OpenAchxWorkflowAsync(path);
        _ctx.AppCommands.CaptureTabEditorState(tab);

        WriteAchx("stale.achx", "Behind"); // Same length as "Before".
        File.SetLastWriteTimeUtc(path, captured.AddDays(daysFromCaptured));

        _ctx.AppCommands.TryActivateTabFromCache(tab).ShouldBeFalse();
    }

    [Fact]
    public async Task TryActivateTabFromCache_WhenDiskFileIsNewerWithTheSameContent_ReturnsTrue()
    {
        string path = WriteAchx("touched.achx", "Walk");
        var tab = new TabEntry(new FilePath(path));
        await _ctx.AppCommands.OpenAchxWorkflowAsync(path);
        _ctx.AppCommands.CaptureTabEditorState(tab);

        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

        _ctx.AppCommands.TryActivateTabFromCache(tab).ShouldBeTrue();
    }

    [Fact]
    public async Task TryActivateTabFromCache_WhenLockedOnLeaveThenEdited_ReturnsFalse()
    {
        string path = WriteAchx("locked.achx", "Before");
        var tab = new TabEntry(new FilePath(path));
        await _ctx.AppCommands.OpenAchxWorkflowAsync(path);
        using (Lock(path))
            _ctx.AppCommands.CaptureTabEditorState(tab);

        WriteAchx("locked.achx", "After");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

        _ctx.AppCommands.TryActivateTabFromCache(tab).ShouldBeFalse();
    }

    [Fact]
    public async Task TryActivateTabFromCache_WhenEditedAndLockedOnReturn_ReturnsFalse()
    {
        string path = WriteAchx("locked.achx", "Before");
        var tab = new TabEntry(new FilePath(path));
        await _ctx.AppCommands.OpenAchxWorkflowAsync(path);
        _ctx.AppCommands.CaptureTabEditorState(tab);

        WriteAchx("locked.achx", "After");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

        using (Lock(path))
            _ctx.AppCommands.TryActivateTabFromCache(tab).ShouldBeFalse();
    }

    // A lock is what usually makes a save fail, so the edits that save lost must survive the switch.
    [Fact]
    public async Task TryActivateTabFromCache_WhenSaveFailedOnALockedFile_KeepsTheEdits()
    {
        string path = WriteAchx("locked.achx", "Before");
        var tab = new TabEntry(new FilePath(path));
        await _ctx.AppCommands.OpenAchxWorkflowAsync(path);
        _pm.AnimationChainListSave!.AnimationChains[0].Name = "Renamed";
        _ctx.UndoManager.MarkSaveFailed();
        using (Lock(path))
            _ctx.AppCommands.CaptureTabEditorState(tab);

        _ctx.AppCommands.TryActivateTabFromCache(tab).ShouldBeTrue();
        _pm.AnimationChainListSave!.AnimationChains[0].Name.ShouldBe("Renamed");
        tab.SaveFailed.ShouldBeTrue();
    }

    // The watcher reports an edit 200 ms late, so the tab can be left before the editor has seen it.
    [Fact]
    public async Task TryActivateTabFromCache_WhenEditedBeforeLeavingAndNotYetReloaded_ReturnsFalse()
    {
        string path = WriteAchx("edited.achx", "Before");
        var tab = new TabEntry(new FilePath(path));
        await _ctx.AppCommands.OpenAchxWorkflowAsync(path);

        WriteAchx("edited.achx", "Behind");
        _ctx.AppCommands.CaptureTabEditorState(tab);

        _ctx.AppCommands.TryActivateTabFromCache(tab).ShouldBeFalse();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task TryActivateTabFromCache_AfterAnOwnSave_IsFreshUnlessEditedSince(bool editedSince, bool expected)
    {
        var ctx = new TestServices(); // CountingProjectManager never writes.
        string path = WriteAchx("saved.achx", "Before");
        var tab = new TabEntry(new FilePath(path));
        await ctx.AppCommands.OpenAchxWorkflowAsync(path);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0].Name = "Renamed";
        ctx.AppCommands.SaveCurrentAnimationChainList();

        if (editedSince) WriteAchx("saved.achx", "Outside");
        ctx.AppCommands.CaptureTabEditorState(tab);

        ctx.AppCommands.TryActivateTabFromCache(tab).ShouldBe(expected);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TryActivateTabFromCache_AfterAHotReload_IsFresh(bool reloads)
    {
        string path = WriteAchx("reloaded.achx", "Before");
        var tab = new TabEntry(new FilePath(path));
        await _ctx.AppCommands.OpenAchxWorkflowAsync(path);
        if (reloads) WriteAchx("reloaded.achx", "After");
        else File.WriteAllText(path, "<AnimationChainArrayS");
        _ctx.AppCommands.ReloadAchxFromDisk(path);
        _ctx.AppCommands.CaptureTabEditorState(tab);

        _ctx.AppCommands.TryActivateTabFromCache(tab).ShouldBeTrue();
        _pm.AnimationChainListSave!.AnimationChains[0].Name.ShouldBe(reloads ? "After" : "Before");
    }

    // A cross-document cut writes a background tab's own document.
    [Fact]
    public async Task TryActivateTabFromCache_AfterSaveDocumentWroteTheTabsFile_IsFresh()
    {
        var ctx = new TestServices(); // CountingProjectManager never writes.
        string path = WriteAchx("cut.achx", "Before");
        var tab = new TabEntry(new FilePath(path));
        await ctx.AppCommands.OpenAchxWorkflowAsync(path);
        ctx.AppCommands.CaptureTabEditorState(tab);
        tab.CachedEditorModel!.AnimationChains.Clear();

        ctx.AppCommands.SaveDocument(tab.CachedEditorModel, path, tab.CachedOnDiskCoordinateType).ShouldBeTrue();

        AnimationChainListSave.FromFile(path).AnimationChains.ShouldBeEmpty();
        ctx.AppCommands.TryActivateTabFromCache(tab).ShouldBeTrue();
        ctx.ProjectManager.AnimationChainListSave.ShouldBeSameAs(tab.CachedEditorModel);
    }

    [Fact]
    public async Task TryActivateTabFromCache_WhenSaveFailedAndTheFileChanged_KeepsTheEditsAndPausesSaving()
    {
        var ctx = new TestServices(); // CountingProjectManager never writes.
        string path = WriteAchx("failed.achx", "Before");
        var tab = new TabEntry(new FilePath(path));
        await ctx.AppCommands.OpenAchxWorkflowAsync(path);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0].Name = "Renamed";
        ctx.UndoManager.MarkSaveFailed();
        ctx.AppCommands.CaptureTabEditorState(tab);
        WriteAchx("failed.achx", "Outside");
        var problems = new List<string>();
        ctx.AppCommands.SaveFailed += problems.Add;

        ctx.AppCommands.TryActivateTabFromCache(tab).ShouldBeTrue();

        ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0].Name.ShouldBe("Renamed");
        problems.ShouldHaveSingleItem().ShouldContain("changed on disk");
        ctx.AppCommands.SaveCurrentAnimationChainList();
        problems.Count.ShouldBe(2);
        ctx.UndoManager.SaveState.ShouldBe(SaveState.Failed);
        // A cut out of this tab's document writes it too.
        ctx.AppCommands.SaveDocument(tab.CachedEditorModel!, path, tab.CachedOnDiskCoordinateType).ShouldBeFalse();
        AnimationChainListSave.FromFile(path).AnimationChains[0].Name.ShouldBe("Outside");

        ctx.AppCommands.ReloadAchxFromDisk(path);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0].Name.ShouldBe("Outside");
        ctx.AppCommands.SaveCurrentAnimationChainList();
        ctx.UndoManager.SaveState.ShouldBe(SaveState.AutoSaveOn);
    }

    [Fact]
    public async Task TryActivateTabFromCache_WhenSaveFailedAndTheFileIsUnchanged_RaisesNothingAndSavesAgain()
    {
        var ctx = new TestServices(); // CountingProjectManager never writes.
        string path = WriteAchx("failed.achx", "Before");
        var tab = new TabEntry(new FilePath(path));
        await ctx.AppCommands.OpenAchxWorkflowAsync(path);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0].Name = "Renamed";
        ctx.UndoManager.MarkSaveFailed();
        ctx.AppCommands.CaptureTabEditorState(tab);
        var problems = new List<string>();
        ctx.AppCommands.SaveFailed += problems.Add;

        ctx.AppCommands.TryActivateTabFromCache(tab).ShouldBeTrue();
        ctx.AppCommands.SaveCurrentAnimationChainList();

        problems.ShouldBeEmpty();
        AnimationChainListSave.FromFile(path).AnimationChains[0].Name.ShouldBe("Renamed");
    }

    private static FileStream Lock(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.None);

    [Fact]
    public async Task TryActivateTabFromCache_WithSelectedFrame_RestoresFrameSelection()
    {
        const string chainName = "Walk";
        const int selectedFrameIndex = 1;

        string pathA = WriteAchx("a.achx", chainName, frameCount: 3);
        string pathB = WriteAchx("b.achx", "Run");
        var tabA = new TabEntry(new FilePath(pathA));
        var tabB = new TabEntry(new FilePath(pathB));

        await _ctx.AppCommands.OpenAchxWorkflowAsync(pathA);
        var chain = _pm.AnimationChainListSave!.AnimationChains[0];
        Assert.Equal(chainName, chain.Name);
        _ctx.SelectedState.SelectedChain = chain;
        _ctx.SelectedState.SelectedFrame = chain.Frames[selectedFrameIndex];
        _ctx.AppCommands.CaptureTabEditorState(tabA);

        await _ctx.AppCommands.OpenAchxWorkflowAsync(pathB);
        _ctx.AppCommands.CaptureTabEditorState(tabB);

        Assert.True(_ctx.AppCommands.TryActivateTabFromCache(tabA));
        Assert.Equal(chainName, _ctx.SelectedState.SelectedChain!.Name);
        Assert.NotNull(_ctx.SelectedState.SelectedFrame);
        Assert.Same(
            _ctx.SelectedState.SelectedChain.Frames[selectedFrameIndex],
            _ctx.SelectedState.SelectedFrame);
    }

    private sealed class CountingProjectManager : IProjectManager
    {
        private readonly ProjectManager _inner = new();

        public int LoadCallCount { get; private set; }

        public AnimationChainListSave? AnimationChainListSave
        {
            get => _inner.AnimationChainListSave;
            set => _inner.AnimationChainListSave = value;
        }

        public TileMapInformationList TileMapInformationList
        {
            get => _inner.TileMapInformationList;
            set => _inner.TileMapInformationList = value;
        }

        public FilePath[] ReferencedPngs { get => _inner.ReferencedPngs; set => _inner.ReferencedPngs = value; }
        public string? FileName { get => _inner.FileName; set => _inner.FileName = value; }
        public string? ProjectFolderPath { get => _inner.ProjectFolderPath; set => _inner.ProjectFolderPath = value; }
        public TextureCoordinateType OnDiskCoordinateType
        {
            get => _inner.OnDiskCoordinateType;
            set => _inner.OnDiskCoordinateType = value;
        }

        public bool IsNativeTsxProject => _inner.IsNativeTsxProject;
        public TileGrid? TsxTileGrid => _inner.TsxTileGrid;
        public void LoadTsxProject(FilePath fileName) => _inner.LoadTsxProject(fileName);
        public IReadOnlyList<string> SaveTsxProject(string? targetPath = null) => _inner.SaveTsxProject(targetPath);
        public IReadOnlyList<string> GetChainNamesWithTsxIssues() => _inner.GetChainNamesWithTsxIssues();
        public uint? GetTsxOwnerTileId(AnimationChainSave chain) => _inner.GetTsxOwnerTileId(chain);
        public string? TrySetTsxOwnerTileId(AnimationChainSave chain, uint tileId) => _inner.TrySetTsxOwnerTileId(chain, tileId);
        public bool IsTsxOwnerTileIdAlreadySet(AnimationChainSave chain, uint tileId) => _inner.IsTsxOwnerTileIdAlreadySet(chain, tileId);
        public bool MarkChainNameExplicit(AnimationChainSave chain) => _inner.MarkChainNameExplicit(chain);
        public bool IsChainNameAuto(AnimationChainSave chain) => _inner.IsChainNameAuto(chain);
        public bool MakeChainNameAuto(AnimationChainSave chain) => _inner.MakeChainNameAuto(chain);
        public uint? ComputeFrameTileId(AnimationFrameSave frame) => _inner.ComputeFrameTileId(frame);
        public object? CaptureTsxState() => _inner.CaptureTsxState();
        public void RestoreTsxState(object? state) => _inner.RestoreTsxState(state);
        public void ResetToBlankDocument() => _inner.ResetToBlankDocument();

        public void LoadAnimationChain(
            FilePath fileName,
            AnimationChainListSave? preParsed = null)
        {
            LoadCallCount++;
            _inner.LoadAnimationChain(fileName, preParsed);
        }

        public void SaveAnimationChainList(AnimationChainListSave document, string targetPath, TextureCoordinateType diskFormat) { }
        public void SaveAnimationChainList(string targetPath) =>
            _inner.SaveAnimationChainList(targetPath);

        public string? ResolveFilesPanelRoot() => _inner.ResolveFilesPanelRoot();

        public (int Width, int Height)? GetTextureSizeInPixels(string textureName) =>
            _inner.GetTextureSizeInPixels(textureName);

        public IReadOnlyList<string> FindMissingTextures(AnimationChainListSave acls, string achxDirectory) =>
            _inner.FindMissingTextures(acls, achxDirectory);
    }
}
