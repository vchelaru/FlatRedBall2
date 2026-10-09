using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Delete acts on every selected item, whatever mix of animations, frames and shapes. A locked
/// animation can itself be deleted, but frames and shapes inside it are modifications and stay.
/// </summary>
[Collection("SequentialSingletons")]
public class AppCommandsDeleteSelectionTests
{
    private static readonly List<AnimationChainSave> NoChains = new();
    private static readonly List<AnimationFrameSave> NoFrames = new();
    private static readonly List<object> NoShapes = new();

    [Fact]
    public void ChainAndFrameOfAnotherChain_BothDeleted_AsOneUndoStep()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var a = TestHelpers.MakeChain(ctx.Acls, "A", frameCount: 2);
        var b = TestHelpers.MakeChain(ctx.Acls, "B", frameCount: 2);
        var b1 = b.Frames[0];

        ctx.AppCommands.DeleteSelection(new[] { a }, new[] { b1 }, NoShapes);

        Assert.DoesNotContain(a, ctx.Acls.AnimationChains);
        Assert.Single(b.Frames);
        Assert.DoesNotContain(b1, b.Frames);

        ctx.UndoManager.Undo();

        Assert.Contains(a, ctx.Acls.AnimationChains);
        Assert.Equal(2, b.Frames.Count);
        Assert.Same(b1, b.Frames[0]);
    }

    [Fact]
    public void LockedChain_IsDeleted_ButItsSelectedFramesStayInTheUndoneChain()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var locked = TestHelpers.MakeChain(ctx.Acls, "Locked", frameCount: 2);
        locked.IsLocked = true;
        var other = TestHelpers.MakeChain(ctx.Acls, "Other", frameCount: 2);

        ctx.AppCommands.DeleteSelection(new[] { locked }, new[] { other.Frames[1] }, NoShapes);

        Assert.DoesNotContain(locked, ctx.Acls.AnimationChains);
        Assert.Single(other.Frames);
        ctx.UndoManager.Undo();
        Assert.Contains(locked, ctx.Acls.AnimationChains);
        Assert.Equal(2, locked.Frames.Count);
    }

    [Fact]
    public void FrameInLockedChain_IsKept_WhileUnlockedChainsFrameAndWholeChainAreDeleted()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var locked = TestHelpers.MakeChain(ctx.Acls, "Locked", frameCount: 2);
        locked.IsLocked = true;
        var a = TestHelpers.MakeChain(ctx.Acls, "A", frameCount: 1);
        var c = TestHelpers.MakeChain(ctx.Acls, "C", frameCount: 2);

        ctx.AppCommands.DeleteSelection(new[] { a }, new[] { locked.Frames[0], c.Frames[0] }, NoShapes);

        Assert.DoesNotContain(a, ctx.Acls.AnimationChains);
        Assert.Equal(2, locked.Frames.Count);
        Assert.Single(c.Frames);
    }

    [Fact]
    public void FrameOfADeletedChain_IsNotDeletedTwice_AndUndoRestoresTheWholeChain()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var a = TestHelpers.MakeChain(ctx.Acls, "A", frameCount: 3);

        ctx.AppCommands.DeleteSelection(new[] { a }, new[] { a.Frames[1] }, NoShapes);

        Assert.Empty(ctx.Acls.AnimationChains);
        ctx.UndoManager.Undo();
        Assert.Single(ctx.Acls.AnimationChains);
        Assert.Equal(3, a.Frames.Count);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void ChainAndShapeOfAnotherChain_BothDeleted()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var a = TestHelpers.MakeChain(ctx.Acls, "A", frameCount: 1);
        var b = TestHelpers.MakeChain(ctx.Acls, "B", frameCount: 1);
        var circle = new CircleSave { Name = "c", Radius = 4 };
        b.Frames[0].ShapesSave!.Add(circle);

        ctx.AppCommands.DeleteSelection(new[] { a }, NoFrames, new object[] { circle });

        Assert.DoesNotContain(a, ctx.Acls.AnimationChains);
        Assert.Empty(b.Frames[0].ShapesSave!.Shapes);
        ctx.UndoManager.Undo();
        Assert.Single(b.Frames[0].ShapesSave!.Shapes);
        Assert.Contains(a, ctx.Acls.AnimationChains);
    }

    [Fact]
    public void ShapeInLockedChain_IsKept()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var locked = TestHelpers.MakeChain(ctx.Acls, "Locked", frameCount: 1);
        var circle = new CircleSave { Name = "c", Radius = 4 };
        locked.Frames[0].ShapesSave!.Add(circle);
        locked.IsLocked = true;
        var other = TestHelpers.MakeChain(ctx.Acls, "Other", frameCount: 2);

        ctx.AppCommands.DeleteSelection(NoChains, new[] { other.Frames[0] }, new object[] { circle });

        Assert.Single(locked.Frames[0].ShapesSave!.Shapes);
        Assert.Single(other.Frames);
    }

    [Fact]
    public void OnlyLockedFramesSelected_DoesNothing_AndRecordsNothing()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var locked = TestHelpers.MakeChain(ctx.Acls, "Locked", frameCount: 2);
        locked.IsLocked = true;

        ctx.AppCommands.DeleteSelection(NoChains, new[] { locked.Frames[0] }, NoShapes);

        Assert.Equal(2, locked.Frames.Count);
        Assert.False(ctx.UndoManager.CanUndo);
    }
}
