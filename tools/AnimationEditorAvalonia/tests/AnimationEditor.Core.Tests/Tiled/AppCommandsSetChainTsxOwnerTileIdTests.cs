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
    // Tile 4 is blank/unused.
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
}
