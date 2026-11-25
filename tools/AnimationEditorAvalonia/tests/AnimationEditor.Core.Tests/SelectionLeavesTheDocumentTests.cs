using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>Whatever a command, its undo or its redo takes out of the document is no longer selected.</summary>
[Collection("SequentialSingletons")]
public class SelectionLeavesTheDocumentTests
{
    [Theory]
    [InlineData("chain", "frame")]
    [InlineData("chain", "shape")]
    [InlineData("frame", "frame")]
    [InlineData("frame", "shape")]
    [InlineData("shape", "shape")]
    [InlineData("selection", "frame")]
    [InlineData("selection", "shape")]
    public void RedoOfADelete_DeselectsWhatItTookAway(string deleted, string selected)
    {
        var ctx = TestHelpers.SetupFreshAcls();
        TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var run = TestHelpers.MakeChain(ctx.Acls, "Run", 2);
        var frame = run.Frames[0];
        var rect = new AARectSave { Name = "Hit" };
        frame.ShapesSave!.Add(rect);
        switch (deleted)
        {
            case "chain": ctx.AppCommands.DeleteAnimationChains([run]); break;
            case "frame": ctx.AppCommands.DeleteFrames([frame]); break;
            case "shape": ctx.AppCommands.DeleteShapes([rect]); break;
            default: ctx.AppCommands.DeleteSelection([run], [], []); break;
        }
        ctx.UndoManager.Undo();
        Select(ctx, frame, selected == "shape" ? rect : null);

        ctx.UndoManager.Redo();

        ShouldHaveNothingElseSelected(ctx);
    }

    [Fact]
    public void UndoOfADuplicate_DeselectsTheFrameInsideIt()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var walk = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var copy = ctx.AppCommands.DuplicateChains([walk])[0];
        Select(ctx, copy.Frames[0], null);

        ctx.UndoManager.Undo();

        ShouldHaveNothingElseSelected(ctx);
    }

    // As a click on the row does: the bag first, then the frame, then the shape.
    private static void Select(TestServices ctx, AnimationFrameSave frame, AARectSave? rect)
    {
        ctx.SelectedState.SelectedNodes = [(object?)rect ?? frame];
        ctx.SelectedState.SelectedFrame = frame;
        ctx.SelectedState.SelectedNodes = [(object?)rect ?? frame];
        ctx.SelectedState.SelectShape(rect);
    }

    private static void ShouldHaveNothingElseSelected(TestServices ctx)
    {
        var state = ctx.SelectedState;
        var frames = ctx.Acls.AnimationChains.SelectMany(c => c.Frames).ToList();
        var present = new HashSet<object>(ctx.Acls.AnimationChains.Cast<object>().Concat(frames).Concat(frames.SelectMany(f => f.ShapesSave?.Shapes ?? [])));
        state.SelectedNodes.Concat(new object?[] { state.SelectedChain, state.SelectedFrame, state.SelectedShape }.OfType<object>())
            .ShouldAllBe(node => present.Contains(node));
    }
}
