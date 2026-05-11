using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class RenameChainUndoTests
{
    // ── RenameChain + Undo ────────────────────────────────────────────────────

    [Fact]
    public void RenameChain_Undo_RestoresOldName()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "OldName");

        ctx.AppCommands.RenameChain(chain, "NewName");
        Assert.Equal("NewName", chain.Name);

        ctx.UndoManager.Undo();

        Assert.Equal("OldName", chain.Name);
    }

    [Fact]
    public void RenameChain_UndoThenRedo_ReappliesNewName()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Alpha");

        ctx.AppCommands.RenameChain(chain, "Beta");
        ctx.UndoManager.Undo();
        Assert.Equal("Alpha", chain.Name);

        ctx.UndoManager.Redo();

        Assert.Equal("Beta", chain.Name);
    }

    [Fact]
    public void RenameChain_MultipleRenames_UndoEachInOrder()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "A");

        ctx.AppCommands.RenameChain(chain, "B");
        ctx.AppCommands.RenameChain(chain, "C");

        ctx.UndoManager.Undo();
        Assert.Equal("B", chain.Name);

        ctx.UndoManager.Undo();
        Assert.Equal("A", chain.Name);
    }
}
