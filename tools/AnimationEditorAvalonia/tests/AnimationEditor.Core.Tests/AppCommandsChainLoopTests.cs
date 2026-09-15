using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// <c>AppCommands.SetChainLoop</c> (issue #1120) sets <see cref="FlatRedBall2.AnimationEditorCommon.AnimationChainSave.Loop"/>
/// and is undoable, same as <c>SetChainLocked</c>.
/// </summary>
[Collection("SequentialSingletons")]
public class AppCommandsChainLoopTests
{
    [Fact]
    public void SetChainLoop_SetsLoopAndIsUndoable()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 0);

        ctx.AppCommands.SetChainLoop(chain, false);

        Assert.False(chain.Loop);
        Assert.True(ctx.UndoManager.CanUndo);

        ctx.UndoManager.Undo();

        Assert.True(chain.Loop);
    }
}
