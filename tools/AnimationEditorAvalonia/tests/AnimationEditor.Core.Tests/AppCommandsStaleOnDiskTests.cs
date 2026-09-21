using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using System;
using System.IO;
using System.Linq;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Fresh-eyes pass #21 (plan/1147-tsx-sync-hardening/phase-01-bug-sweep.md): the whole point of a
/// native tsx project is coexisting with Tiled on one file. When Tiled saves something this
/// editor can't load (a wangset, say), the hot reload fails and the tab keeps its now-stale
/// in-memory tileset -- and the very next autosave wrote that stale tileset back over Tiled's
/// file, destroying whatever Tiled had just added. Same hole for an achx edited outside the
/// editor into something the parser rejects.
/// </summary>
[Collection("SequentialSingletons")]
public class AppCommandsStaleOnDiskTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = new();

    public void Dispose() => _dir.Dispose();

    private const string PlainTsxXml = """
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

    // What Tiled writes after the user paints a terrain set onto the same tileset: the animation
    // is untouched, a <wangsets> block (unsupported by TsxWriter) is added.
    private const string TsxWithWangsetXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
         <tile id="0">
          <animation>
           <frame tileid="0" duration="200"/>
           <frame tileid="1" duration="200"/>
          </animation>
         </tile>
         <wangsets>
          <wangset name="Ground" type="corner" tile="-1">
           <wangcolor name="Grass" color="#00ff00" tile="0" probability="1"/>
           <wangtile tileid="4" wangid="0,1,0,1,0,1,0,1"/>
          </wangset>
         </wangsets>
        </tileset>
        """;

    private string OpenTsx()
    {
        var path = Path.Combine(_dir.Path, "Heroes.tsx");
        File.WriteAllText(path, PlainTsxXml);
        _ctx.ProjectManager.LoadTsxProject(new FilePath(path));
        return path;
    }

    [Fact]
    public void SaveCurrentAnimationChainList_AfterHotReloadOfExternalChangeFailed_RefusesToOverwriteTheFile()
    {
        var path = OpenTsx();
        var chain = _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single();
        string? reloadFailure = null;
        string? saveFailure = null;
        _ctx.AppCommands.HotReloadFailed += (_, reason) => reloadFailure = reason;
        _ctx.AppCommands.SaveFailed += message => saveFailure = message;

        File.WriteAllText(path, TsxWithWangsetXml);          // Tiled saved
        _ctx.AppCommands.ReloadAchxFromDisk(path);            // watcher fired, reload can't load it
        Assert.NotNull(reloadFailure);

        chain.Frames[0].FrameLength = 0.3f;                   // user keeps editing
        _ctx.AppCommands.SaveCurrentAnimationChainList();     // autosave

        Assert.Equal(SaveState.Failed, _ctx.UndoManager.SaveState);
        Assert.NotNull(saveFailure);
        Assert.Contains("changed on disk", saveFailure);
        Assert.Contains("<wangsets>", File.ReadAllText(path)); // Tiled's work is still there
    }

    [Fact]
    public void SaveCurrentAnimationChainList_AfterALaterHotReloadSucceeds_SavesAgain()
    {
        var path = OpenTsx();
        File.WriteAllText(path, TsxWithWangsetXml);
        _ctx.AppCommands.ReloadAchxFromDisk(path);

        File.WriteAllText(path, PlainTsxXml);                 // user removed the wangset in Tiled
        _ctx.AppCommands.ReloadAchxFromDisk(path);
        var chain = _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single();
        chain.Frames[0].FrameLength = 0.3f;
        _ctx.AppCommands.SaveCurrentAnimationChainList();

        Assert.Equal(SaveState.AutoSaveOn, _ctx.UndoManager.SaveState);
        var reloaded = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        Assert.Equal([((uint)0, 300), ((uint)1, 200)], reloaded.Tiles.Single(t => t.ID == 0).Animation.Select(f => (f.TileID, f.Duration)));
    }

    // Closing the tab and opening the (fixed) file again is the other way back: a fresh load is
    // in sync with disk by definition, so the mark must not outlive it.
    [Fact]
    public async System.Threading.Tasks.Task SaveCurrentAnimationChainList_AfterReopeningTheFixedFile_SavesAgain()
    {
        var path = OpenTsx();
        File.WriteAllText(path, TsxWithWangsetXml);
        _ctx.AppCommands.ReloadAchxFromDisk(path);

        File.WriteAllText(path, PlainTsxXml);
        await _ctx.AppCommands.OpenTsxWorkflowAsync(path);
        _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single().Frames[0].FrameLength = 0.3f;
        _ctx.AppCommands.SaveCurrentAnimationChainList();

        Assert.Equal(SaveState.AutoSaveOn, _ctx.UndoManager.SaveState);
    }

    // Save As to another path is the user's way out: the stale file is left alone and the copy
    // is written.
    [Fact]
    public void SaveCurrentAnimationChainList_ToAnotherPathWhileStale_Writes()
    {
        var path = OpenTsx();
        File.WriteAllText(path, TsxWithWangsetXml);
        _ctx.AppCommands.ReloadAchxFromDisk(path);
        var copyPath = Path.Combine(_dir.Path, "HeroesCopy.tsx");

        _ctx.AppCommands.SaveCurrentAnimationChainList(copyPath);

        Assert.True(File.Exists(copyPath));
        Assert.Contains("<wangsets>", File.ReadAllText(path));
    }
}
