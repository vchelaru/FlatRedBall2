using AnimationEditor.Core;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using FlatRedBall2.AnimationEditorCommon;
using System;
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
/// clear <see cref="IProjectManager.IsNativeTsxProject"/>/<see cref="IProjectManager.TsxTileSize"/>
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
        // IsNativeTsxProject/TsxTileSize for THIS tab, not leave whatever the achx tab left behind.
        Assert.True(_ctx.AppCommands.TryActivateTabFromCache(tsxTab));
        Assert.True(_ctx.ProjectManager.IsNativeTsxProject);
        Assert.Equal((16, 16), _ctx.ProjectManager.TsxTileSize);

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
        Assert.Null(_ctx.ProjectManager.TsxTileSize);
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
        Assert.Null(_ctx.ProjectManager.TsxTileSize);

        // And switching back onward to the tsx tab must restore it correctly.
        Assert.True(_ctx.AppCommands.TryActivateTabFromCache(tsxTab));
        Assert.True(_ctx.ProjectManager.IsNativeTsxProject);
        Assert.Equal((16, 16), _ctx.ProjectManager.TsxTileSize);
    }
}
