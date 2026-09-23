using AnimationEditor.Core;
using DotTiled;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.IO;
using System.Linq;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests.Tiled;

/// <summary>
/// <see cref="IAppCommands.SetChainTsxOwnerTileId"/> (issue #1182) through the undo stack and
/// autosave, mirroring <c>AppCommandsTsxUndoRedoTests</c>'s "every command autosaves on Do, Undo
/// and Redo" discipline.
/// </summary>
[Collection("SequentialSingletons")]
public class AppCommandsSetChainTsxOwnerTileIdTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = new();

    public void Dispose() => _dir.Dispose();

    // Two single-tile chains: "Walk" on tile 0 (frames 0, 1) and "Idle" on tile 8 (frames 8, 9).
    // Tile 12 has no Name property, so it loads as chain.Name == "ID:12" (TiledAnimationToAchjMapper's
    // synthetic placeholder). Tile 4 is blank/unused.
    private const string TsxFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
         <tile id="0">
          <properties>
           <property name="Name" value="Walk"/>
          </properties>
          <animation>
           <frame tileid="0" duration="200"/>
           <frame tileid="1" duration="200"/>
          </animation>
         </tile>
         <tile id="8">
          <properties>
           <property name="Name" value="Idle"/>
          </properties>
          <animation>
           <frame tileid="8" duration="200"/>
           <frame tileid="9" duration="200"/>
          </animation>
         </tile>
         <tile id="12">
          <animation>
           <frame tileid="12" duration="200"/>
           <frame tileid="13" duration="200"/>
          </animation>
         </tile>
        </tileset>
        """;

    private string _path = "";

    private AnimationChainSave OpenTsx(string chainName)
    {
        _path = Path.Combine(_dir.Path, "Heroes.tsx");
        File.WriteAllText(_path, TsxFixtureXml);
        _ctx.ProjectManager.LoadTsxProject(new FilePath(_path));
        return _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single(c => c.Name == chainName);
    }

    private Tileset Disk() => DotTiled.Serialization.Loader.Default().LoadTileset(_path);

    [Fact]
    public void ValidTileId_MovesTheAnimationOnDisk_UndoRestoresIt_RedoMovesItAgain()
    {
        var walk = OpenTsx("Walk");

        var error = _ctx.AppCommands.SetChainTsxOwnerTileId(walk, 4);
        Assert.Null(error);
        var afterDo = Disk();
        Assert.Equal([((uint)0, 200), ((uint)1, 200)], afterDo.Tiles.Single(t => t.ID == 4).Animation.Select(f => (f.TileID, f.Duration)));
        Assert.True(afterDo.Tiles.SingleOrDefault(t => t.ID == 0) is null or { Animation.Count: 0 });

        _ctx.UndoManager.Undo();
        var afterUndo = Disk();
        Assert.Equal([((uint)0, 200), ((uint)1, 200)], afterUndo.Tiles.Single(t => t.ID == 0).Animation.Select(f => (f.TileID, f.Duration)));
        Assert.True(afterUndo.Tiles.SingleOrDefault(t => t.ID == 4) is null or { Animation.Count: 0 });

        _ctx.UndoManager.Redo();
        var afterRedo = Disk();
        Assert.Equal([((uint)0, 200), ((uint)1, 200)], afterRedo.Tiles.Single(t => t.ID == 4).Animation.Select(f => (f.TileID, f.Duration)));
    }

    [Fact]
    public void TileAlreadyOwnedByAnotherChain_ReturnsErrorAndDoesNotAutosave()
    {
        var walk = OpenTsx("Walk");
        var onDiskBefore = File.ReadAllText(_path);

        var error = _ctx.AppCommands.SetChainTsxOwnerTileId(walk, 8); // "Idle"'s tile

        Assert.NotNull(error);
        Assert.Contains("Idle", error);
        Assert.Equal(onDiskBefore, File.ReadAllText(_path));
    }

    // Regression: a repeated "Sync to First Frame" click used to push a fresh undo entry every
    // time even when the owner tile hadn't changed. Tile 4 is a real change from Walk's loaded
    // owner (0), so the first set is a real change; the second, identical set is a true no-op.
    [Fact]
    public void RepeatedIdenticalValue_PushesOneUndoEntryThenNoneAfter()
    {
        var walk = OpenTsx("Walk");
        Assert.False(_ctx.ProjectManager.IsTsxOwnerTileIdAlreadySet(walk, 4));

        var firstError = _ctx.AppCommands.SetChainTsxOwnerTileId(walk, 4);
        Assert.Null(firstError);
        Assert.Single(_ctx.UndoManager.UndoHistory);
        Assert.True(_ctx.ProjectManager.IsTsxOwnerTileIdAlreadySet(walk, 4));

        var secondError = _ctx.AppCommands.SetChainTsxOwnerTileId(walk, 4);
        Assert.Null(secondError);
        Assert.Single(_ctx.UndoManager.UndoHistory); // no second entry pushed

        _ctx.UndoManager.Undo();
        Assert.False(_ctx.UndoManager.CanUndo);
    }

    // Regression: the tree label for an unnamed chain (its Name is just the "ID:{tileId}"
    // placeholder TiledAnimationToAchjMapper assigns) went stale after Sync moved the owner tile --
    // worse, the stale text would get baked in as a permanent explicit Name property on the next
    // save, since NativeTsxAnimationSync only omits the Name property when it still matches the
    // *current* tile's synthetic name.
    [Fact]
    public void SyncToNewTile_RenamesSyntheticPlaceholder_UndoRestoresOldName_RedoRenamesAgain()
    {
        var chain = OpenTsx("ID:12");

        var error = _ctx.AppCommands.SetChainTsxOwnerTileId(chain, 5);
        Assert.Null(error);
        Assert.Equal("ID:5", chain.Name);
        // Still synthetic -- no explicit Name property baked onto disk.
        Assert.DoesNotContain(Disk().Tiles.Single(t => t.ID == 5).Properties, p => p.Name == "Name");

        _ctx.UndoManager.Undo();
        Assert.Equal("ID:12", chain.Name);
        Assert.Equal(12u, _ctx.ProjectManager.GetTsxOwnerTileId(chain));

        _ctx.UndoManager.Redo();
        Assert.Equal("ID:5", chain.Name);
        Assert.Equal(5u, _ctx.ProjectManager.GetTsxOwnerTileId(chain));
    }

    // A chain with a real (user- or file-given) name is never renamed just because its owner tile
    // moves -- only the synthetic "ID:{tileId}" placeholder gets kept in sync.
    [Fact]
    public void SyncToNewTile_OnRealName_DoesNotRename()
    {
        var walk = OpenTsx("Walk");

        var error = _ctx.AppCommands.SetChainTsxOwnerTileId(walk, 4);
        Assert.Null(error);
        Assert.Equal("Walk", walk.Name);

        _ctx.UndoManager.Undo();
        Assert.Equal("Walk", walk.Name);
    }

    // False-match guard at the full command stack: a manual rename to text that happens to LOOK
    // synthetic ("ID:999") must still mark the chain explicit -- whether a name is "real" is
    // decided by the rename actually happening, never by re-inspecting the string's shape -- so a
    // later Sync must never touch it again.
    [Fact]
    public void ManualRename_ToTextThatLooksSynthetic_StopsFutureAutoRename()
    {
        var chain = OpenTsx("ID:12");

        Assert.True(_ctx.AppCommands.RenameChain(chain, "ID:999"));
        Assert.Equal("ID:999", chain.Name);

        var error = _ctx.AppCommands.SetChainTsxOwnerTileId(chain, 5);
        Assert.Null(error);
        Assert.Equal("ID:999", chain.Name); // untouched, even though it looks like a stale placeholder
    }

    // Undo of a manual rename must restore the chain's synthetic-tracking too, not just its Name
    // text -- otherwise a chain that goes back to looking unnamed after Undo would stay "stuck"
    // (explicit-but-wrong) instead of resuming auto-follow.
    [Fact]
    public void UndoOfManualRename_RestoresSyntheticTracking_SyncFollowsAgain()
    {
        var chain = OpenTsx("ID:12");

        Assert.True(_ctx.AppCommands.RenameChain(chain, "Hero"));
        _ctx.UndoManager.Undo(); // back to "ID:12", and (if restored correctly) synthetic again

        var error = _ctx.AppCommands.SetChainTsxOwnerTileId(chain, 5);
        Assert.Null(error);
        Assert.Equal("ID:5", chain.Name); // resumed following -- proves synthetic tracking came back
    }
}
