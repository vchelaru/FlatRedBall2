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
/// Fresh-eyes pass #16 (plan/1147-tsx-sync-hardening/phase-01-bug-sweep.md): a save that throws
/// was swallowed by <see cref="AppCommands.SaveCurrentAnimationChainList"/> into a bare "Auto Save
/// Failed" status with no reason. Pass #22 then made the everyday trigger -- duplicating a chain
/// in a native tsx project -- not throw at all (see the first test).
/// </summary>
[Collection("SequentialSingletons")]
public class AppCommandsSaveFailedTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = new();

    public void Dispose() => _dir.Dispose();

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

    // #1147 pass #22: a duplicate no longer fails the whole save. The copy animates the same
    // cells as its source, so it can't have a tile yet; the save writes everything else, reports
    // the copy by name, and picks it up on the first save after its frames are moved to free
    // cells -- the normal "duplicate, then move" workflow.
    [Fact]
    public void DuplicateChains_InNativeTsxProject_SavesTheRestAndReportsTheCopyUntilItsFramesMove()
    {
        var path = Path.Combine(_dir.Path, "Heroes.tsx");
        File.WriteAllText(path, TsxFixtureXml);
        _ctx.ProjectManager.LoadTsxProject(new FilePath(path));
        var chain = _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single();
        string? saveFailure = null;
        System.Collections.Generic.IReadOnlyList<string>? warnings = null;
        _ctx.AppCommands.SaveFailed += message => saveFailure = message;
        _ctx.AppCommands.TsxSaveCompletedWithWarnings += w => warnings = w;

        var copy = _ctx.AppCommands.DuplicateChains([chain]).Single();

        Assert.Equal(SaveState.AutoSaveOn, _ctx.UndoManager.SaveState);
        Assert.Null(saveFailure);
        var warning = Assert.Single(warnings!);
        Assert.Contains("ID:0Copy", warning);
        Assert.Contains("ID:0", warning);
        var tileset = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        Assert.Equal([0u], tileset.Tiles.Where(t => t.Animation.Count > 0).Select(t => t.ID));

        // Move the copy one row down (tiles 4, 5) -- via the same command the inspector uses.
        _ctx.AppCommands.SetFramePixelRegion(copy.Frames, pixelX: null, pixelY: 16, pixelW: null, pixelH: null, bmpW: 64, bmpH: 64);

        Assert.Equal(SaveState.AutoSaveOn, _ctx.UndoManager.SaveState);
        tileset = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        Assert.Equal([((uint)4, 200), ((uint)5, 200)], tileset.Tiles.Single(t => t.ID == 4).Animation.Select(f => (f.TileID, f.Duration)));
        Assert.Equal("ID:0Copy", tileset.Tiles.Single(t => t.ID == 4).GetProperty<DotTiled.StringProperty>("Name").Value);
    }

    [Fact]
    public void SaveCurrentAnimationChainList_AchxWriteThrows_IsReportedThroughSaveFailed()
    {
        _ctx.ProjectManager.FileName = Path.Combine(_dir.Path, "missing-folder", "Test.achx");
        string? reported = null;
        _ctx.AppCommands.SaveFailed += message => reported = message;

        _ctx.AppCommands.SaveCurrentAnimationChainList();

        Assert.Equal(SaveState.Failed, _ctx.UndoManager.SaveState);
        Assert.NotNull(reported);
    }
}
