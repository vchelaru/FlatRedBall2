using AnimationEditor.Core;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Reproduces the TabEditorCache tab-switch gap in
/// plan/1147-tsx-sync-hardening/phase-01-bug-sweep.md's TODO: a cache-hit tab switch
/// (<see cref="AnimationEditor.Core.CommandsAndState.AppCommands.TryActivateTabFromCache"/>)
/// never went through <c>LoadTsxProject</c>/<c>LoadAnimationChain</c>, so it couldn't restore or
/// clear <see cref="IProjectManager.IsNativeTsxProject"/>/<see cref="IProjectManager.TsxTileGrid"/>
/// -- those kept reflecting whichever tab was *live-loaded* last, not the tab actually being
/// switched to.
/// </summary>
[Collection("SequentialSingletons")]
public class TabSwitchCacheTsxTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = new();

    public void Dispose() => _dir.Dispose();

    // 4 columns, 16x16 tiles, one animated tile with no Name property.
    private const string TsxFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
         <tile id="0">
          <animation>
           <frame tileid="0" duration="200"/>
           <frame tileid="1" duration="200"/>
          </animation>
         </tile>
        </tileset>
        """;

    private string WriteTsx(string fileName)
    {
        var path = Path.Combine(_dir.Path, fileName);
        File.WriteAllText(path, TsxFixtureXml);
        return path;
    }

    private string WriteAchx(string fileName, string chainName)
    {
        var path = Path.Combine(_dir.Path, fileName);
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        acls.AnimationChains.Add(new AnimationChainSave { Name = chainName });
        acls.Save(path);
        return path;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TryActivateTabFromCache_WhenTheTsxChangedOnDisk_ReloadsUnlessItsSaveFailed(bool saveFailed)
    {
        string path = WriteTsx("Heroes.tsx");
        var tab = new TabEntry(new FilePath(path));
        await _ctx.AppCommands.OpenTsxWorkflowAsync(path);
        if (saveFailed) _ctx.UndoManager.MarkSaveFailed();
        string outside = TsxFixtureXml.Replace("200", "300");
        File.WriteAllText(path, outside);
        _ctx.AppCommands.CaptureTabEditorState(tab); // Left before the watcher reported the edit.

        Assert.Equal(saveFailed, _ctx.AppCommands.TryActivateTabFromCache(tab));

        if (!saveFailed) return;
        _ctx.AppCommands.SaveCurrentAnimationChainList();
        Assert.Equal(outside, File.ReadAllText(path));
    }

    // A cross-document cut writes a background tab's document while another one is current (#1994).
    [Theory]
    [InlineData("Other.achx")]
    [InlineData("Other.tsx")]
    public async Task SaveDocument_BackgroundTsxTab_WritesTheTilesetAndLeavesTheCurrentDocumentAlone(string currentName)
    {
        // Tile 5 is not its own first frame, so its tile survives only through the tab's own tile ids.
        string path = Path.Combine(_dir.Path, "Heroes.tsx");
        File.WriteAllText(path, TsxFixtureXml.Replace("</tileset>",
            " <tile id=\"5\">\n  <animation>\n   <frame tileid=\"6\" duration=\"300\"/>\n  </animation>\n </tile>\n</tileset>"));
        var tab = new TabEntry(new FilePath(path));
        await _ctx.AppCommands.OpenTsxWorkflowAsync(path);
        _ctx.AppCommands.CaptureTabEditorState(tab);
        bool currentIsTsx = currentName.EndsWith(".tsx");
        string currentPath = currentIsTsx ? WriteTsx(currentName) : WriteAchx(currentName, "Idle");
        if (currentIsTsx) await _ctx.AppCommands.OpenTsxWorkflowAsync(currentPath);
        else await _ctx.AppCommands.OpenAchxWorkflowAsync(currentPath);
        byte[] currentBytes = File.ReadAllBytes(currentPath);
        var currentModel = _ctx.ProjectManager.AnimationChainListSave;
        var currentTsxState = _ctx.ProjectManager.CaptureTsxState();
        tab.CachedEditorModel!.AnimationChains.RemoveAt(0);
        tab.CachedEditorModel.AnimationChains[0].Loop = false;
        IReadOnlyList<string> warnings = [];
        _ctx.AppCommands.TsxSaveCompletedWithWarnings += (_, raised) => warnings = raised;

        _ctx.AppCommands.SaveDocument(tab).ShouldBeTrue();

        AnimatedTileIds(path).ShouldBe([5u]);
        warnings.ShouldHaveSingleItem().ShouldContain("loop off");
        _ctx.ProjectManager.AnimationChainListSave.ShouldBeSameAs(currentModel);
        _ctx.ProjectManager.CaptureTsxState().ShouldBe(currentTsxState);
        File.ReadAllBytes(currentPath).ShouldBe(currentBytes);
        // The tab's own later save still knows tile 5.
        _ctx.AppCommands.TryActivateTabFromCache(tab).ShouldBeTrue();
        _ctx.ProjectManager.SaveTsxProject();
        AnimatedTileIds(path).ShouldBe([5u]);

        static IEnumerable<uint> AnimatedTileIds(string tsx) =>
            DotTiled.Serialization.Loader.Default().LoadTileset(tsx).Tiles
                .Where(tile => tile.Animation.Count > 0).Select(tile => tile.ID);
    }

    [Fact]
    public async Task SaveDocument_BackgroundTsxTabSavedTwice_KeepsANewChainOnTheTileOfItsFirstSave()
    {
        string path = WriteTsx("Heroes.tsx");
        var tab = new TabEntry(new FilePath(path));
        await _ctx.AppCommands.OpenTsxWorkflowAsync(path);
        _ctx.AppCommands.CaptureTabEditorState(tab);
        await _ctx.AppCommands.OpenAchxWorkflowAsync(WriteAchx("Other.achx", "Idle"));
        var onTile5 = new AnimationFrameSave { TextureName = "Heroes.png", FrameLength = 0.1f,
            LeftCoordinate = 16, RightCoordinate = 32, TopCoordinate = 16, BottomCoordinate = 32 };
        var added = new AnimationChainSave { Name = "New" };
        added.Frames.Add(onTile5);
        tab.CachedEditorModel!.AnimationChains.Add(added);
        _ctx.AppCommands.SaveDocument(tab).ShouldBeTrue();

        (onTile5.LeftCoordinate, onTile5.RightCoordinate) = (32, 48); // tile 6
        _ctx.AppCommands.SaveDocument(tab).ShouldBeTrue();

        File.ReadAllText(path).ShouldContain("<tile id=\"5\">");
        File.ReadAllText(path).ShouldNotContain("<tile id=\"6\">");
    }

    [Fact]
    public async Task LastSaveStored_AfterABackgroundTsxSave_StillAnswersForTheCurrentDocument()
    {
        string path = WriteTsx("Heroes.tsx");
        var tab = new TabEntry(new FilePath(path));
        await _ctx.AppCommands.OpenTsxWorkflowAsync(path);
        _ctx.AppCommands.CaptureTabEditorState(tab);
        await _ctx.AppCommands.OpenTsxWorkflowAsync(WriteTsx("Other.tsx"));
        var notStored = _ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0];
        notStored.Loop = false;
        _ctx.AppCommands.SaveCurrentAnimationChainList();
        _ctx.AppCommands.LastSaveStored([notStored]).ShouldBeFalse();

        _ctx.AppCommands.SaveDocument(tab).ShouldBeTrue();

        _ctx.AppCommands.LastSaveStored([notStored]).ShouldBeFalse();
    }

    [Fact]
    public async Task LastSaveStored_BackInATsxThatDidNotStoreAChain_AfterAnotherTsxWasSaved_SaysSo()
    {
        string path = WriteTsx("Heroes.tsx");
        var tab = new TabEntry(new FilePath(path));
        await _ctx.AppCommands.OpenTsxWorkflowAsync(path);
        var notStored = _ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0];
        notStored.Loop = false;
        _ctx.AppCommands.SaveCurrentAnimationChainList();
        _ctx.AppCommands.CaptureTabEditorState(tab);
        await _ctx.AppCommands.OpenTsxWorkflowAsync(WriteTsx("Other.tsx"));
        _ctx.AppCommands.SaveCurrentAnimationChainList();

        _ctx.AppCommands.TryActivateTabFromCache(tab).ShouldBeTrue();

        _ctx.AppCommands.LastSaveStored([notStored]).ShouldBeFalse();
    }

    [Fact]
    public async Task LastSaveStored_BackInATsxWhoseBackgroundSaveStoredTheChain_SaysSo()
    {
        string path = WriteTsx("Heroes.tsx");
        var tab = new TabEntry(new FilePath(path));
        await _ctx.AppCommands.OpenTsxWorkflowAsync(path);
        var chain = _ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0];
        chain.Loop = false;
        _ctx.AppCommands.SaveCurrentAnimationChainList();
        _ctx.AppCommands.LastSaveStored([chain]).ShouldBeFalse();
        _ctx.AppCommands.CaptureTabEditorState(tab);
        await _ctx.AppCommands.OpenAchxWorkflowAsync(WriteAchx("Other.achx", "Idle"));
        chain.Loop = true;
        _ctx.AppCommands.SaveDocument(tab).ShouldBeTrue();

        _ctx.AppCommands.TryActivateTabFromCache(tab).ShouldBeTrue();

        _ctx.AppCommands.LastSaveStored([chain]).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveDocument_WhenAListenerThrows_Fails()
    {
        string path = WriteTsx("Heroes.tsx");
        var tab = new TabEntry(new FilePath(path));
        await _ctx.AppCommands.OpenTsxWorkflowAsync(path);
        _ctx.AppCommands.CaptureTabEditorState(tab);
        tab.CachedEditorModel!.AnimationChains[0].Loop = false;
        var messages = new List<string>();
        _ctx.AppCommands.SaveFailed += messages.Add;
        _ctx.AppCommands.TsxSaveCompletedWithWarnings += (_, _) => throw new InvalidOperationException("boom");

        _ctx.AppCommands.SaveDocument(tab).ShouldBeFalse();

        tab.SaveFailed.ShouldBeTrue();
        messages.ShouldHaveSingleItem().ShouldContain("boom");
    }

    // A pasted frame's texture is an absolute path, which must be matched against the tab's own folder.
    [Fact]
    public async Task SaveDocument_BackgroundTsxTabInAnotherFolder_StoresAChainWithAnAbsoluteTexture()
    {
        string path = WriteTsx("Heroes.tsx");
        var tab = new TabEntry(new FilePath(path));
        await _ctx.AppCommands.OpenTsxWorkflowAsync(path);
        _ctx.AppCommands.CaptureTabEditorState(tab);
        Directory.CreateDirectory(Path.Combine(_dir.Path, "other"));
        await _ctx.AppCommands.OpenAchxWorkflowAsync(WriteAchx("other/Other.achx", "Idle"));
        var pasted = new AnimationChainSave { Name = "New" };
        pasted.Frames.Add(new AnimationFrameSave { TextureName = Path.Combine(_dir.Path, "Heroes.png"), FrameLength = 0.1f,
            LeftCoordinate = 16, RightCoordinate = 32, TopCoordinate = 16, BottomCoordinate = 32 });
        tab.CachedEditorModel!.AnimationChains.Add(pasted);

        _ctx.AppCommands.SaveDocument(tab).ShouldBeTrue();

        File.ReadAllText(path).ShouldContain("<tile id=\"5\">");
    }

    [Fact]
    public async Task SaveDocument_BackgroundTsxTabThatCannotBeWritten_KeepsTheFileAndTheCurrentDocument()
    {
        // Tile 5 has a name of the user's, so a chain moved onto it makes the save throw.
        string path = Path.Combine(_dir.Path, "Heroes.tsx");
        File.WriteAllText(path, TsxFixtureXml.Replace("</tileset>",
            $" <tile id=\"5\"><properties><property name=\"Name\" value=\"Mine\"/></properties></tile></tileset>"));
        byte[] before = File.ReadAllBytes(path);
        var tab = new TabEntry(new FilePath(path));
        await _ctx.AppCommands.OpenTsxWorkflowAsync(path);
        _ctx.AppCommands.CaptureTabEditorState(tab);
        await _ctx.AppCommands.OpenAchxWorkflowAsync(WriteAchx("Other.achx", "Idle"));
        var currentModel = _ctx.ProjectManager.AnimationChainListSave;
        var tabTsxState = tab.CachedTsxState;
        var onTile5 = new AnimationChainSave { Name = "New" };
        onTile5.Frames.Add(new AnimationFrameSave { TextureName = "Heroes.png", FrameLength = 0.1f,
            LeftCoordinate = 16, RightCoordinate = 32, TopCoordinate = 16, BottomCoordinate = 32 });
        tab.CachedEditorModel!.AnimationChains.Add(onTile5);
        string? failure = null;
        _ctx.AppCommands.SaveFailed += message => failure = message;

        _ctx.AppCommands.SaveDocument(tab).ShouldBeFalse();

        tab.SaveFailed.ShouldBeTrue();
        failure.ShouldNotBeNull().ShouldContain("Heroes.tsx");
        File.ReadAllBytes(path).ShouldBe(before);
        tab.CachedTsxState.ShouldBe(tabTsxState);
        _ctx.ProjectManager.AnimationChainListSave.ShouldBeSameAs(currentModel);
        _ctx.ProjectManager.IsNativeTsxProject.ShouldBeFalse();
    }

    [Fact]
    public async Task TryActivateTabFromCache_TsxThenAchxThenBackToTsx_RestoresNativeTsxStateAndSaveWritesEdit()
    {
        string tsxPath = WriteTsx("Heroes.tsx");
        string achxPath = WriteAchx("Plain.achx", "Idle");
        var tsxTab = new TabEntry(new FilePath(tsxPath));
        var achxTab = new TabEntry(new FilePath(achxPath));

        await _ctx.AppCommands.OpenTsxWorkflowAsync(tsxPath);
        _ctx.AppCommands.CaptureTabEditorState(tsxTab);

        await _ctx.AppCommands.OpenAchxWorkflowAsync(achxPath);
        _ctx.AppCommands.CaptureTabEditorState(achxTab);

        // The reported bug: switching back to the tsx tab via the cache path must restore
        // IsNativeTsxProject/TsxTileGrid for THIS tab, not leave whatever the achx tab left behind.
        Assert.True(_ctx.AppCommands.TryActivateTabFromCache(tsxTab));
        Assert.True(_ctx.ProjectManager.IsNativeTsxProject);
        Assert.Equal(new TileGrid(16, 16), _ctx.ProjectManager.TsxTileGrid);

        // A save against the cache-restored tab must act on THIS tab's tileset, not no-op
        // against stale/cleared state -- prove it by making an edit and confirming it lands.
        _ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0].Name = "Renamed";
        _ctx.ProjectManager.SaveTsxProject();

        var reloaded = DotTiled.Serialization.Loader.Default().LoadTileset(tsxPath);
        Assert.Contains(reloaded.Tiles,
            t => t.Properties.OfType<DotTiled.StringProperty>().Any(p => p.Name == "Name" && p.Value == "Renamed"));

        // Switching onward to the achx tab must in turn clear the tsx state the previous
        // cache-restore just set -- not leak it forward either.
        Assert.True(_ctx.AppCommands.TryActivateTabFromCache(achxTab));
        Assert.False(_ctx.ProjectManager.IsNativeTsxProject);
        Assert.Null(_ctx.ProjectManager.TsxTileGrid);
    }

    // Every prior tab-switch-cache test in this sweep pairs one tsx tab with one achx tab. This
    // covers the untested combination -- two DISTINCT tsx tabs -- switching back and forth
    // repeatedly, editing and saving each in turn, to prove CaptureTsxState/RestoreTsxState keep
    // each tab's own tileset/entry-tile-id tracking fully isolated rather than one tab's saves
    // leaking into the other's file or tile-identity hints.
    [Fact]
    public async Task TryActivateTabFromCache_TwoDistinctTsxTabsSwitchedBackAndForthRepeatedly_EachSavesOnlyItsOwnFile()
    {
        string pathA = WriteTsx("HeroesA.tsx");
        string pathB = WriteTsx("HeroesB.tsx");
        var tabA = new TabEntry(new FilePath(pathA));
        var tabB = new TabEntry(new FilePath(pathB));

        await _ctx.AppCommands.OpenTsxWorkflowAsync(pathA);
        _ctx.AppCommands.CaptureTabEditorState(tabA);

        await _ctx.AppCommands.OpenTsxWorkflowAsync(pathB);
        _ctx.AppCommands.CaptureTabEditorState(tabB);

        // Switch back to A, edit, save -- must land in HeroesA.tsx only.
        Assert.True(_ctx.AppCommands.TryActivateTabFromCache(tabA));
        Assert.True(_ctx.ProjectManager.IsNativeTsxProject);
        _ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0].Name = "RenamedA";
        _ctx.AppCommands.SaveCurrentAnimationChainList();
        _ctx.AppCommands.CaptureTabEditorState(tabA);

        // Switch to B, edit, save -- must land in HeroesB.tsx only, and must not carry forward
        // A's edit or A's tileset.
        Assert.True(_ctx.AppCommands.TryActivateTabFromCache(tabB));
        Assert.True(_ctx.ProjectManager.IsNativeTsxProject);
        _ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0].Name = "RenamedB";
        _ctx.AppCommands.SaveCurrentAnimationChainList();
        _ctx.AppCommands.CaptureTabEditorState(tabB);

        // Switch back to A again -- must still reflect A's own edit, not B's.
        Assert.True(_ctx.AppCommands.TryActivateTabFromCache(tabA));
        Assert.Equal("RenamedA", _ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0].Name);

        var reloadedA = DotTiled.Serialization.Loader.Default().LoadTileset(pathA);
        Assert.Contains(reloadedA.Tiles,
            t => t.Properties.OfType<DotTiled.StringProperty>().Any(p => p.Name == "Name" && p.Value == "RenamedA"));
        Assert.DoesNotContain(reloadedA.Tiles,
            t => t.Properties.OfType<DotTiled.StringProperty>().Any(p => p.Name == "Name" && p.Value == "RenamedB"));

        var reloadedB = DotTiled.Serialization.Loader.Default().LoadTileset(pathB);
        Assert.Contains(reloadedB.Tiles,
            t => t.Properties.OfType<DotTiled.StringProperty>().Any(p => p.Name == "Name" && p.Value == "RenamedB"));
        Assert.DoesNotContain(reloadedB.Tiles,
            t => t.Properties.OfType<DotTiled.StringProperty>().Any(p => p.Name == "Name" && p.Value == "RenamedA"));
    }

    [Fact]
    public async Task TryActivateTabFromCache_AchxThenTsxThenBackToAchx_ClearsNativeTsxStateThenRestoresTsxOnReturn()
    {
        string achxPath = WriteAchx("Plain.achx", "Idle");
        string tsxPath = WriteTsx("Heroes.tsx");
        var achxTab = new TabEntry(new FilePath(achxPath));
        var tsxTab = new TabEntry(new FilePath(tsxPath));

        await _ctx.AppCommands.OpenAchxWorkflowAsync(achxPath);
        _ctx.AppCommands.CaptureTabEditorState(achxTab);

        await _ctx.AppCommands.OpenTsxWorkflowAsync(tsxPath);
        _ctx.AppCommands.CaptureTabEditorState(tsxTab);

        // The symmetric direction: switching back to the achx tab via the cache path must
        // clear the tsx state the (live-loaded) tsx tab left behind, not report it as still native-tsx.
        Assert.True(_ctx.AppCommands.TryActivateTabFromCache(achxTab));
        Assert.False(_ctx.ProjectManager.IsNativeTsxProject);
        Assert.Null(_ctx.ProjectManager.TsxTileGrid);

        // And switching back onward to the tsx tab must restore it correctly.
        Assert.True(_ctx.AppCommands.TryActivateTabFromCache(tsxTab));
        Assert.True(_ctx.ProjectManager.IsNativeTsxProject);
        Assert.Equal(new TileGrid(16, 16), _ctx.ProjectManager.TsxTileGrid);
    }
}
