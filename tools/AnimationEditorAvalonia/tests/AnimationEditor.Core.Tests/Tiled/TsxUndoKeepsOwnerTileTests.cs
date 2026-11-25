using AnimationEditor.Core.Sequences;
using AnimationEditor.DocumentMatrix;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

/// <summary>Undoing every edit puts each animation back on the tile the file had it on, also when its frames are elsewhere.</summary>
[Collection("SequentialSingletons")]
public class TsxUndoKeepsOwnerTileTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = new();

    public void Dispose() => _dir.Dispose();

    // The animation's own tile is 7, its frames are tiles 8 and 9.
    private async Task<(string Path, AnimationChainSave Chain)> OpenAsync()
    {
        string path = Path.Combine(_dir.Path, "Heroes.tsx");
        File.WriteAllText(path, """
            <?xml version="1.0" encoding="UTF-8"?>
            <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
             <image source="Heroes.png" width="64" height="64"/>
             <tile id="7">
              <animation>
               <frame tileid="8" duration="100"/>
               <frame tileid="9" duration="100"/>
              </animation>
             </tile>
            </tileset>
            """);
        await _ctx.AppCommands.OpenTsxWorkflowAsync(path);
        return (path, _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single());
    }

    private void UndoAll()
    {
        while (_ctx.UndoManager.CanUndo) _ctx.UndoManager.Undo();
    }

    [Fact]
    public async Task Undo_FramesClearedThenOneAdded_PutsTheAnimationBackOnItsTile()
    {
        var (path, chain) = await OpenAsync();
        string text = File.ReadAllText(path);
        _ctx.AppCommands.DeleteFrames(chain.Frames.ToList());
        _ctx.AppCommands.AddFrame(chain);

        UndoAll();

        Documents.TileAnimations(path).ShouldBe(new[] { "7: 8@100 9@100" });
        File.ReadAllText(path).ShouldBe(text);
    }

    [Fact]
    public async Task Undo_AnotherAnimationTookTheTileMeanwhile_PutsTheAnimationBackOnItsTile()
    {
        var (path, chain) = await OpenAsync();
        string text = File.ReadAllText(path);
        var other = _ctx.AppCommands.AddAnimationChainWithName("Other")!;
        _ctx.AppCommands.AddFrameFromPixelBounds(other, "Heroes.png", 48, 16, 64, 32); // tile 7, so not saved yet
        _ctx.AppCommands.DeleteFrames(chain.Frames.ToList());

        UndoAll();

        Documents.TileAnimations(path).ShouldBe(new[] { "7: 8@100 9@100" });
        File.ReadAllText(path).ShouldBe(text);
    }

    // Two animations, each on its own first tile.
    private async Task<(string Path, AnimationChainSave Walk, AnimationChainSave Idle)> OpenTwoAsync()
    {
        string path = Documents.Write(_dir.Path, DocType.Tsx, "front");
        await _ctx.AppCommands.OpenTsxWorkflowAsync(path);
        _ctx.AppCommands.SaveCurrentAnimationChainList();
        var chains = _ctx.ProjectManager.AnimationChainListSave!.AnimationChains;
        return (path, chains[0], chains[1]);
    }

    [Fact]
    public async Task Undo_AnAnimationWhoseTileAnotherYieldedToWasDeleted_GivesItsTileBack()
    {
        var (path, _, idle) = await OpenTwoAsync();
        _ctx.AppCommands.DuplicateChains([idle]);
        string before = File.ReadAllText(path);

        _ctx.AppCommands.DeleteAnimationChains([idle]);
        string deleted = File.ReadAllText(path);
        _ctx.UndoManager.Undo();

        File.ReadAllText(path).ShouldBe(before);
        _ctx.UndoManager.Redo();
        File.ReadAllText(path).ShouldBe(deleted);
    }

    [Fact]
    public async Task Undo_TheFramesOfTheSourceOfACopyWereDeleted_GivesItsTileBack()
    {
        var (path, _, idle) = await OpenTwoAsync();
        _ctx.AppCommands.DuplicateChains([idle]);
        string before = File.ReadAllText(path);

        _ctx.AppCommands.DeleteFrames(idle.Frames.ToList());
        _ctx.UndoManager.Undo();

        File.ReadAllText(path).ShouldBe(before);
    }

    [Fact]
    public async Task Undo_InASequenceTab_LeavesTheTilesetOpenBehindItAlone()
    {
        var (_, walk, _) = await OpenTwoAsync();
        SequenceDocument.Load(Documents.Write(_dir.Path, DocType.Seqj, "scene")).AddSequence(_ctx.UndoManager);
        _ctx.ProjectManager.MakeChainNameAuto(walk);

        _ctx.UndoManager.Undo();

        _ctx.ProjectManager.IsChainNameAuto(walk).ShouldBeTrue();
    }

    // Every step of a typed or scrolled value is saved, so a waiting animation takes the tile at the first one.
    [Fact]
    public async Task Undo_OneEntryOfSeveralSteps_GivesBackTheTileTheFirstOneFreed()
    {
        string path = Path.Combine(_dir.Path, "Heroes.tsx");
        File.WriteAllText(path, """
            <?xml version="1.0" encoding="UTF-8"?>
            <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
             <image source="Heroes.png" width="64" height="64"/>
             <tile id="0">
              <animation>
               <frame tileid="0" duration="100"/>
              </animation>
             </tile>
             <tile id="1">
              <properties>
               <property name="ParentId" type="int" value="0"/>
              </properties>
              <animation>
               <frame tileid="1" duration="100"/>
              </animation>
             </tile>
            </tileset>
            """);
        await _ctx.AppCommands.OpenTsxWorkflowAsync(path);
        var wide = _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single();
        var waiting = _ctx.AppCommands.AddAnimationChainWithName("Waiting")!;
        _ctx.AppCommands.AddFrameFromPixelBounds(waiting, "Heroes.png", 16, 0, 32, 16);
        string before = File.ReadAllText(path);
        int entries = _ctx.UndoManager.UndoHistory.Count;

        _ctx.AppCommands.SetFramePixelRegion(wide.Frames, null, null, 16, null);
        _ctx.AppCommands.SetFramePixelRegion(wide.Frames, null, 16, null, null);
        Documents.TileAnimations(path).ShouldBe(new[] { "0: 4@100", "1: 1@100" });
        _ctx.UndoManager.Undo();

        _ctx.UndoManager.UndoHistory.Count.ShouldBe(entries);
        File.ReadAllText(path).ShouldBe(before);
    }
}
