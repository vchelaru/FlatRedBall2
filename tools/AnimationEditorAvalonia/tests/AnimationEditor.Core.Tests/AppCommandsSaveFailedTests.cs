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
/// Failed" status with no reason. In a native tsx project the everyday trigger is duplicating a
/// chain -- the copy animates the same cells, so it claims the same tile and
/// <c>NativeTsxAnimationSync.ValidateNoTileIdCollisions</c> refuses the whole save -- and the user
/// had no way to learn that the copy has to move to other cells first.
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

    [Fact]
    public void DuplicateChains_InNativeTsxProject_TileCollisionIsReportedThroughSaveFailed()
    {
        var path = Path.Combine(_dir.Path, "Heroes.tsx");
        File.WriteAllText(path, TsxFixtureXml);
        _ctx.ProjectManager.LoadTsxProject(new FilePath(path));
        var chain = _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single();
        string? reported = null;
        _ctx.AppCommands.SaveFailed += message => reported = message;

        _ctx.AppCommands.DuplicateChains([chain]);

        Assert.Equal(SaveState.Failed, _ctx.UndoManager.SaveState);
        Assert.NotNull(reported);
        Assert.Contains("ID:0", reported);
        Assert.Contains("ID:0Copy", reported);
        // Nothing was written: the original tile is exactly as the fixture had it.
        var tileset = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        Assert.Equal([0u], tileset.Tiles.Where(t => t.Animation.Count > 0).Select(t => t.ID));

        // Undoing the duplicate clears the collision, so the next autosave succeeds again.
        _ctx.UndoManager.Undo();
        Assert.Equal(SaveState.AutoSaveOn, _ctx.UndoManager.SaveState);
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
