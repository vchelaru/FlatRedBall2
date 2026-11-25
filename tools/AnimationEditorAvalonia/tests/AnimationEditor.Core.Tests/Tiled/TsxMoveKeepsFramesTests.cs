using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests.Tiled;

/// <summary>
/// No cut or drag takes a frame out of a <c>.tsx</c>: a move into an animation the save would skip is refused (#2152).
/// Blink is on tile 5 with frames 5 and 6; Glow is on tile 9 with frame 9.
/// </summary>
[Collection("SequentialSingletons")]
public class TsxMoveKeepsFramesTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = new();
    private string _path = "";

    public void Dispose() => _dir.Dispose();

    private AnimationChainSave Chain(string name) => _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single(c => c.Name == name);

    private AnimationChainSave Open()
    {
        _path = Path.Combine(_dir.Path, "Heroes.tsx");
        File.WriteAllText(_path, """
            <?xml version="1.0" encoding="UTF-8"?>
            <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
             <image source="Heroes.png" width="64" height="64"/>
             <tile id="5">
              <properties>
               <property name="Name" value="Blink"/>
              </properties>
              <animation>
               <frame tileid="5" duration="100"/>
               <frame tileid="6" duration="100"/>
              </animation>
             </tile>
             <tile id="9">
              <properties>
               <property name="Name" value="Glow"/>
              </properties>
              <animation>
               <frame tileid="9" duration="100"/>
              </animation>
             </tile>
            </tileset>
            """);
        _ctx.ProjectManager.LoadTsxProject(new FilePath(_path));
        Save().ShouldBe("5 6 9");
        return _ctx.AppCommands.AddAnimationChainWithName("New")!;
    }

    /// <summary>Saves, as every edit does in the editor; returns the tiles of every frame in the file.</summary>
    private string Save()
    {
        _ctx.ProjectManager.SaveTsxProject();
        return string.Join(" ", Regex.Matches(File.ReadAllText(_path), "<frame tileid=\"(\\d+)\"").Select(m => m.Groups[1].Value));
    }

    private void Move(string how, AnimationFrameSave[] frames, AnimationChainSave source, AnimationChainSave target, int index = 0)
    {
        if (how == "cut") _ctx.AppCommands.PasteFramesCut(target, frames, index);
        else _ctx.AppCommands.MoveFrames(frames, source, target, index);
    }

    [Theory, InlineData("cut"), InlineData("drag")]
    public void FirstFrameIntoAnEmptyAnimation_IsRefused(string how)
    {
        var target = Open();
        Save();
        var undo = _ctx.UndoManager.UndoHistory.Count();

        Move(how, [Chain("Blink").Frames[0]], Chain("Blink"), target);

        Chain("Blink").Frames.Count.ShouldBe(2);
        target.Frames.ShouldBeEmpty();
        _ctx.UndoManager.UndoHistory.Count().ShouldBe(undo);
        Save().ShouldBe("5 6 9");
    }

    /// <summary>A drag puts the frames down in their order in the animation, whatever order they were selected in.</summary>
    [Fact]
    public void FramesDraggedInSelectionOrder_AreRefusedByTheFirstOfTheirAnimation()
    {
        var target = Open();
        _ctx.AppCommands.AddFrameFromPixelBounds(Chain("Blink"), "Heroes.png", 48, 16, 64, 32);
        Save().ShouldBe("5 6 7 9");

        Move("drag", [Chain("Blink").Frames[1], Chain("Blink").Frames[0]], Chain("Blink"), target);

        target.Frames.ShouldBeEmpty();
        Save().ShouldBe("5 6 7 9");
    }

    /// <summary>The tile need not be the source's: Glow holds tile 9, which Blink's second frame shows here.</summary>
    [Theory, InlineData("cut"), InlineData("drag")]
    public void FrameShowingAThirdAnimationsTile_IsRefused(string how)
    {
        var target = Open();
        new FrameRegion(16, 32, 32, 48).ApplyTo(Chain("Blink").Frames[1]);
        Save().ShouldBe("5 9 9");

        Move(how, [Chain("Blink").Frames[1]], Chain("Blink"), target);

        target.Frames.ShouldBeEmpty();
        Save().ShouldBe("5 9 9");
    }

    /// <summary>The copy of Blink is not in the file, so a stored frame moved into it would not be either.</summary>
    [Theory, InlineData("cut"), InlineData("drag")]
    public void StoredFrameIntoAnAnimationThatIsNotInTheFile_IsRefused(string how)
    {
        Open();
        var copy = _ctx.AppCommands.DuplicateChain(Chain("Blink"))!;
        Save().ShouldBe("5 6 9");

        Move(how, [Chain("Glow").Frames[0]], Chain("Glow"), copy, 2);

        Chain("Glow").Frames.Count.ShouldBe(1);
        Save().ShouldBe("5 6 9");
    }

    [Theory, InlineData("cut"), InlineData("drag")]
    public void FrameOfAnAnimationThatIsNotInTheFile_Moves(string how)
    {
        var target = Open();
        var copy = _ctx.AppCommands.DuplicateChain(Chain("Blink"))!;
        Save();

        Move(how, [copy.Frames[0]], copy, target);

        target.Frames.Count.ShouldBe(1);
        Save().ShouldBe("5 6 6 9"); // what is left of the copy is on free tile 6
    }

    [Theory, InlineData("cut"), InlineData("drag")]
    public void FrameOnAFreeTileIntoAnEmptyAnimation_MovesAndIsSaved_ThroughUndoAndRedo(string how)
    {
        var target = Open();
        Save();

        Move(how, [Chain("Blink").Frames[1]], Chain("Blink"), target);

        Save().ShouldBe("5 6 9");
        target.Frames.Count.ShouldBe(1);
        _ctx.UndoManager.Undo();
        Save().ShouldBe("5 6 9");
        Chain("Blink").Frames.Count.ShouldBe(2);
        _ctx.UndoManager.Redo();
        Save().ShouldBe("5 6 9");
        target.Frames.Count.ShouldBe(1);
    }

    /// <summary>Nothing is left on tile 5, so the target can take it.</summary>
    [Theory, InlineData("cut"), InlineData("drag")]
    public void EveryFrameIntoAnEmptyAnimation_MovesAndIsSaved(string how)
    {
        var target = Open();
        Save();

        Move(how, [.. Chain("Blink").Frames], Chain("Blink"), target);

        target.Frames.Count.ShouldBe(2);
        Save().ShouldBe("5 6 9");
    }

    [Theory, InlineData("cut"), InlineData("drag")]
    public void AfterTheOwnerTileChanges_TheRefusedFrameMoves(string how)
    {
        var target = Open();
        _ctx.AppCommands.SetChainTsxOwnerTileId(Chain("Blink"), 1).ShouldBeNull();
        Save();

        Move(how, [Chain("Blink").Frames[0]], Chain("Blink"), target);

        target.Frames.Count.ShouldBe(1);
        Save().ShouldBe("6 5 9");
    }

    /// <summary>A copy takes nothing out of the file, so it stays in memory with the save's warning, as Add Frame does.</summary>
    [Fact]
    public void CopiedFrameIntoAnEmptyAnimation_IsAdded()
    {
        var target = Open();

        _ctx.AppCommands.PasteFrames(target, [Chain("Blink").Frames[0]]).ShouldBeTrue();

        target.Frames.Count.ShouldBe(1);
        Save().ShouldBe("5 6 9");
    }

    [Fact]
    public void FrameOfALockedAnimation_IsCopied()
    {
        var target = Open();
        _ctx.AppCommands.SetChainLocked(Chain("Blink"), true);

        _ctx.AppCommands.PasteFramesCut(target, [Chain("Blink").Frames[0]], 0);

        target.Frames.Count.ShouldBe(1);
        Save().ShouldBe("5 6 9");
    }

    [Theory, InlineData("cut"), InlineData("drag")]
    public void ReorderingTheFramesOfAnAnimation_IsNotAMoveOutOfTheFile(string how)
    {
        Open();

        Move(how, [Chain("Blink").Frames[0]], Chain("Blink"), Chain("Blink"), 2);

        Save().ShouldBe("6 5 9");
    }
}
