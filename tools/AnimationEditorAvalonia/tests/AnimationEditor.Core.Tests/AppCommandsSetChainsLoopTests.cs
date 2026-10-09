using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>Loop applies to every selected animation at once, as one undo step.</summary>
[Collection("SequentialSingletons")]
public class AppCommandsSetChainsLoopTests
{
    [Fact]
    public void AllSelectedChainsChange_AsOneUndoStep()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var a = TestHelpers.MakeChain(ctx.Acls, "A", frameCount: 1);
        var b = TestHelpers.MakeChain(ctx.Acls, "B", frameCount: 1);
        a.Loop = true;
        b.Loop = true;

        ctx.AppCommands.SetChainsLoop(new[] { a, b }, false);

        Assert.False(a.Loop);
        Assert.False(b.Loop);

        ctx.UndoManager.Undo();

        Assert.True(a.Loop);
        Assert.True(b.Loop);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void ChainAlreadyAtTheTarget_IsLeftAloneByUndo()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var a = TestHelpers.MakeChain(ctx.Acls, "A", frameCount: 1);
        var b = TestHelpers.MakeChain(ctx.Acls, "B", frameCount: 1);
        a.Loop = false;
        b.Loop = true;

        ctx.AppCommands.SetChainsLoop(new[] { a, b }, false);
        ctx.UndoManager.Undo();

        Assert.False(a.Loop);
        Assert.True(b.Loop);
    }

    [Fact]
    public void NothingToChange_RecordsNothing()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var a = TestHelpers.MakeChain(ctx.Acls, "A", frameCount: 1);
        a.Loop = true;

        ctx.AppCommands.SetChainsLoop(new[] { a }, true);

        Assert.False(ctx.UndoManager.CanUndo);
    }
}
