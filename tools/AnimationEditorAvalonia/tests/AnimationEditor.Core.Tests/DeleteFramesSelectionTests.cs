using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Deleting a frame must drop that frame from the selection. A stale
/// <see cref="ISelectedState.SelectedFrame"/> pointing at an orphaned frame keeps
/// the preview rendering its sprite and shapes. See issue #284.
/// </summary>
[Collection("SequentialSingletons")]
public class DeleteFramesSelectionTests
{
    [Fact]
    public void DeleteFrames_ClearsSelectedFrame_WhenSelectedFrameIsDeleted()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 3);
        var middle = chain.Frames[1];
        ctx.SelectedState.SelectedFrame = middle;

        ctx.AppCommands.DeleteFrames(new List<AnimationFrameSave> { middle });

        Assert.Null(ctx.SelectedState.SelectedFrame);
    }

    [Fact]
    public void DeleteFrames_KeepsSelectedFrame_WhenADifferentFrameIsDeleted()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 3);
        var first = chain.Frames[0];
        var last  = chain.Frames[2];
        ctx.SelectedState.SelectedFrame = last;

        ctx.AppCommands.DeleteFrames(new List<AnimationFrameSave> { first });

        Assert.Same(last, ctx.SelectedState.SelectedFrame);
    }

    [Fact]
    public void DeleteFrames_ClearsSelectedShape_WhenSelectedFrameWithShapeIsDeleted()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 2);
        var frame = chain.Frames[0];
        var rect = new AARectSave { Name = "Hitbox" };
        frame.ShapesSave!.Shapes.Add(rect);
        ctx.SelectedState.SelectedFrame = frame;
        ctx.SelectedState.SelectedRectangle = rect;

        ctx.AppCommands.DeleteFrames(new List<AnimationFrameSave> { frame });

        Assert.Null(ctx.SelectedState.SelectedFrame);
        Assert.Null(ctx.SelectedState.SelectedRectangle);
    }

    [Fact]
    public void DeleteFrames_RemovesDeletedFramesFromMultiSelection()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Run", frameCount: 4);
        ctx.SelectedState.SelectedChain = chain;
        var f0 = chain.Frames[0];
        var f2 = chain.Frames[2];
        ctx.SelectedState.SelectedNodes = new List<object> { f0, f2 };

        ctx.AppCommands.DeleteFrames(new List<AnimationFrameSave> { f0, f2 });

        Assert.Empty(ctx.SelectedState.SelectedNodes);
    }

    [Fact]
    public void DeleteFrames_Redo_ClearsSelectionAgain()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 2);
        var first = chain.Frames[0];
        ctx.SelectedState.SelectedFrame = first;

        ctx.AppCommands.DeleteFrames(new List<AnimationFrameSave> { first });
        ctx.UndoManager.Undo();

        // User re-selects the restored frame, then redoes the delete.
        ctx.SelectedState.SelectedFrame = first;
        ctx.UndoManager.Redo();

        Assert.Null(ctx.SelectedState.SelectedFrame);
    }
}
