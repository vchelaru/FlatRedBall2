using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using FlatRedBall2.Animation.Content;
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

    private string WriteAchx(string fileName, string chainName)
    {
        var path = Path.Combine(_dir.Path, fileName);
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        acls.AnimationChains.Add(new AnimationChainSave { Name = chainName });
        acls.Save(path);
        return path;
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

        public FilePath[] ReferencedPngs => _inner.ReferencedPngs;
        public string? FileName { get => _inner.FileName; set => _inner.FileName = value; }
        public TextureCoordinateType OnDiskCoordinateType
        {
            get => _inner.OnDiskCoordinateType;
            set => _inner.OnDiskCoordinateType = value;
        }

        public void LoadAnimationChain(
            FilePath fileName,
            AnimationChainListSave? preParsed = null,
            IReadOnlyDictionary<string, (int Width, int Height)>? knownTextureSizes = null)
        {
            LoadCallCount++;
            _inner.LoadAnimationChain(fileName, preParsed, knownTextureSizes);
        }

        public void SaveAnimationChainList(string targetPath) =>
            _inner.SaveAnimationChainList(targetPath);

        public void SaveAnimationChainList(System.IO.Stream stream) =>
            _inner.SaveAnimationChainList(stream);

        public string? ResolveFilesPanelRoot() => _inner.ResolveFilesPanelRoot();

        public (int Width, int Height)? GetTextureSizeInPixels(string textureName) =>
            _inner.GetTextureSizeInPixels(textureName);

        public IReadOnlyList<string> FindMissingTextures(AnimationChainListSave acls, string achxDirectory) =>
            _inner.FindMissingTextures(acls, achxDirectory);
    }
}
