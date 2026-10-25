using AnimationEditor.Core.CommandsAndState;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>PingPong and Reverse (#1538) apply to every selected animation at once, as one undo step.</summary>
[Collection("SequentialSingletons")]
public class AppCommandsSetChainsPlaybackModeTests
{
    [Fact]
    public void SetChainsPingPong_AllSelectedChainsChange_AsOneUndoStep()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var a = TestHelpers.MakeChain(ctx.Acls, "A", frameCount: 1);
        var b = TestHelpers.MakeChain(ctx.Acls, "B", frameCount: 1);

        ctx.AppCommands.SetChainsPingPong(new[] { a, b }, true);

        Assert.True(a.PingPong);
        Assert.True(b.PingPong);
        Assert.Equal("Enable ping-pong on 2 animations", ctx.UndoManager.UndoHistory.Last().Description);

        ctx.UndoManager.Undo();

        Assert.False(a.PingPong);
        Assert.False(b.PingPong);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void SetChainsReverse_OneChain_IsUndoableAndNamesIt()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var a = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 1);

        ctx.AppCommands.SetChainsReverse(new[] { a }, true);

        Assert.True(a.Reverse);
        Assert.Equal("Enable reverse on 'Walk'", ctx.UndoManager.UndoHistory.Last().Description);
        ctx.UndoManager.Undo();
        Assert.False(a.Reverse);
        ctx.UndoManager.Redo();
        Assert.True(a.Reverse);
    }

    [Fact]
    public void SetChainsReverse_ChainAlreadyAtTheTarget_IsLeftAloneByUndo()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var a = TestHelpers.MakeChain(ctx.Acls, "A", frameCount: 1);
        var b = TestHelpers.MakeChain(ctx.Acls, "B", frameCount: 1);
        a.Reverse = true;

        ctx.AppCommands.SetChainsReverse(new[] { a, b }, true);
        ctx.UndoManager.Undo();

        Assert.True(a.Reverse);
        Assert.False(b.Reverse);
    }

    [Fact]
    public void SetChainsPingPong_NothingToChange_RecordsNothing()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var a = TestHelpers.MakeChain(ctx.Acls, "A", frameCount: 1);

        ctx.AppCommands.SetChainsPingPong(new[] { a }, false);

        Assert.False(ctx.UndoManager.CanUndo);
    }
}
