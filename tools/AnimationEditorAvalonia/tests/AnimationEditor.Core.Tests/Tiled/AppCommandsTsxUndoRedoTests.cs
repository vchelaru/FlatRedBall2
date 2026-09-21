using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using DotTiled;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests.Tiled;

/// <summary>
/// Fresh-eyes pass #23 (plan/1147-tsx-sync-hardening/phase-01-bug-sweep.md): every command in a
/// native tsx project autosaves on Do, Undo and Redo, so each of those must leave the file in
/// the state the model shows. The passes since #1155 added save-time behavior (owner transfer,
/// colliding-claim yielding, lossy-data warnings, the stale-on-disk guard) that was only ever
/// exercised on the Do side. This drives the real commands through Undo and Redo and reads the
/// tsx back after every step.
/// </summary>
[Collection("SequentialSingletons")]
public class AppCommandsTsxUndoRedoTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = new();
    private readonly List<IReadOnlyList<string>> _warnings = new();

    public void Dispose() => _dir.Dispose();

    // Two single-tile chains: "Walk" on tile 0 (frames 0, 1) and "Idle" on tile 8 (frames 8, 9).
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

    private AnimationChainSave OpenTsx(string chainName = "Walk")
    {
        _path = Path.Combine(_dir.Path, "Heroes.tsx");
        File.WriteAllText(_path, TsxFixtureXml);
        _ctx.ProjectManager.LoadTsxProject(new FilePath(_path));
        _ctx.AppCommands.TsxSaveCompletedWithWarnings += w => _warnings.Add(w);
        return _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single(c => c.Name == chainName);
    }

    private Tileset Disk() => DotTiled.Serialization.Loader.Default().LoadTileset(_path);

    private static (uint, int)[] Anim(Tileset t, uint id) =>
        t.Tiles.Single(x => x.ID == id).Animation.Select(f => (f.TileID, f.Duration)).ToArray();

    private static string? NameOf(Tileset t, uint id) =>
        t.Tiles.SingleOrDefault(x => x.ID == id)?.Properties.OfType<StringProperty>().FirstOrDefault(p => p.Name == "Name")?.Value;

    private void AssertSaved() => Assert.Equal(SaveState.AutoSaveOn, _ctx.UndoManager.SaveState);

    [Fact]
    public void DuplicateChain_UndoRemovesTheYieldingCopy_RedoBringsItBack()
    {
        var walk = OpenTsx();

        _ctx.AppCommands.DuplicateChains([walk]);
        Assert.Single(_warnings); // the copy yields tile 0 to Walk
        _warnings.Clear();

        _ctx.UndoManager.Undo();
        AssertSaved();
        Assert.Empty(_warnings);
        Assert.Equal(2, _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Count);
        Assert.Equal([0u, 8u], Disk().Tiles.Where(t => t.Animation.Count > 0).Select(t => t.ID));

        _ctx.UndoManager.Redo();
        AssertSaved();
        Assert.Single(_warnings);
        Assert.Equal(3, _ctx.ProjectManager.AnimationChainListSave.AnimationChains.Count);
    }

    [Fact]
    public void MoveDuplicateThenUndo_CopyYieldsAgainAndItsMovedTileIsReleased_RedoRestoresIt()
    {
        var walk = OpenTsx();
        var copy = _ctx.AppCommands.DuplicateChains([walk]).Single();
        _ctx.AppCommands.SetFramePixelRegion(copy.Frames, pixelX: null, pixelY: 16, pixelW: null, pixelH: null, bmpW: 64, bmpH: 64);
        Assert.Equal([((uint)4, 200), ((uint)5, 200)], Anim(Disk(), 4));
        _warnings.Clear();

        _ctx.UndoManager.Undo(); // copy back on Walk's cells
        AssertSaved();
        Assert.Single(_warnings);
        Assert.Equal([((uint)0, 200), ((uint)1, 200)], Anim(Disk(), 0));
        Assert.Equal("Walk", NameOf(Disk(), 0));

        _ctx.UndoManager.Redo();
        AssertSaved();
        Assert.Equal([((uint)4, 200), ((uint)5, 200)], Anim(Disk(), 4));
        Assert.Equal("WalkCopy", NameOf(Disk(), 4));
    }

    [Fact]
    public void MoveChainOntoAnotherChainsTile_UndoRestoresBoth_RedoYieldsAgain()
    {
        var walk = OpenTsx();

        // Walk moves down two rows onto Idle's tiles (8, 9): it yields, Idle keeps them.
        _ctx.AppCommands.SetFramePixelRegion(walk.Frames, pixelX: null, pixelY: 32, pixelW: null, pixelH: null, bmpW: 64, bmpH: 64);
        Assert.Single(_warnings);
        Assert.Equal("Idle", NameOf(Disk(), 8));
        Assert.Equal("Walk", NameOf(Disk(), 0)); // its last good state stays
        _warnings.Clear();

        _ctx.UndoManager.Undo();
        AssertSaved();
        Assert.Empty(_warnings);
        Assert.Equal([((uint)0, 200), ((uint)1, 200)], Anim(Disk(), 0));
        Assert.Equal("Idle", NameOf(Disk(), 8));

        _ctx.UndoManager.Redo();
        Assert.Single(_warnings);
        Assert.Equal("Idle", NameOf(Disk(), 8));
    }

    [Fact]
    public void DeleteChain_UndoRestoresItsTile_RedoClearsItAgain()
    {
        var walk = OpenTsx();

        _ctx.AppCommands.DeleteAnimationChains([walk]);
        Assert.DoesNotContain(Disk().Tiles, t => t.ID == 0);

        _ctx.UndoManager.Undo();
        AssertSaved();
        Assert.Equal([((uint)0, 200), ((uint)1, 200)], Anim(Disk(), 0));
        Assert.Equal("Walk", NameOf(Disk(), 0));

        _ctx.UndoManager.Redo();
        Assert.DoesNotContain(Disk().Tiles, t => t.ID == 0);
        Assert.Equal("Idle", NameOf(Disk(), 8));
    }

    [Fact]
    public void DeleteAllFrames_UndoRevivesTheSameTile_RedoClearsItAgain()
    {
        var walk = OpenTsx();

        _ctx.AppCommands.DeleteFrames(walk.Frames.ToList());
        Assert.DoesNotContain(Disk().Tiles, t => t.ID == 0);

        _ctx.UndoManager.Undo();
        AssertSaved();
        Assert.Equal([((uint)0, 200), ((uint)1, 200)], Anim(Disk(), 0));

        _ctx.UndoManager.Redo();
        Assert.DoesNotContain(Disk().Tiles, t => t.ID == 0);
    }

    [Fact]
    public void RenameChain_UndoRestoresTheNameOnDisk()
    {
        var walk = OpenTsx();

        _ctx.AppCommands.RenameChain(walk, "Run");
        Assert.Equal("Run", NameOf(Disk(), 0));

        _ctx.UndoManager.Undo();
        AssertSaved();
        Assert.Equal("Walk", NameOf(Disk(), 0));

        _ctx.UndoManager.Redo();
        Assert.Equal("Run", NameOf(Disk(), 0));
    }

    [Fact]
    public void ResizeGrowLeft_UndoMovesOwnerBack_RedoMovesItAgain()
    {
        var idle = OpenTsx("Idle"); // tile 8 (col 0) can't grow left, so move it right first
        // Frame 0 to tile 9 and frame 1 to tile 10 (X is absolute per frame), then grow frame 0's
        // left edge back over tile 8 so its owner transfers there.
        _ctx.AppCommands.SetFramePixelRegion([idle.Frames[0]], pixelX: 16, pixelY: null, pixelW: null, pixelH: null, bmpW: 64, bmpH: 64);
        _ctx.AppCommands.SetFramePixelRegion([idle.Frames[1]], pixelX: 32, pixelY: null, pixelW: null, pixelH: null, bmpW: 64, bmpH: 64);
        Assert.Equal("Idle", NameOf(Disk(), 9));
        _ctx.AppCommands.SetFramePixelRegion([idle.Frames[0]], pixelX: 0, pixelY: null, pixelW: 32, pixelH: null, bmpW: 64, bmpH: 64);
        Assert.Equal("Idle", NameOf(Disk(), 8));
        Assert.Equal([((uint)9, 200), ((uint)11, 200)], Anim(Disk(), 9)); // satellite of the 2-wide group

        _ctx.UndoManager.Undo();
        AssertSaved();
        Assert.Equal("Idle", NameOf(Disk(), 9));
        Assert.DoesNotContain(Disk().Tiles, t => t.ID == 8);

        _ctx.UndoManager.Redo();
        Assert.Equal("Idle", NameOf(Disk(), 8));
        Assert.Equal(8, Disk().Tiles.Single(t => t.ID == 9).GetProperty<IntProperty>("ParentId").Value);
    }
}
