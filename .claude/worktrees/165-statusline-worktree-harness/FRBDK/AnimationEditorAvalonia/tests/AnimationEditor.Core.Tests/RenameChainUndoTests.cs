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
        var acls = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(acls, "OldName");

        AppCommands.Self.RenameChain(chain, "NewName");
        Assert.Equal("NewName", chain.Name);

        UndoManager.Self.Undo();

        Assert.Equal("OldName", chain.Name);
    }

    [Fact]
    public void RenameChain_UndoThenRedo_ReappliesNewName()
    {
        var acls = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(acls, "Alpha");

        AppCommands.Self.RenameChain(chain, "Beta");
        UndoManager.Self.Undo();
        Assert.Equal("Alpha", chain.Name);

        UndoManager.Self.Redo();

        Assert.Equal("Beta", chain.Name);
    }

    [Fact]
    public void RenameChain_MultipleRenames_UndoEachInOrder()
    {
        var acls = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(acls, "A");

        AppCommands.Self.RenameChain(chain, "B");
        AppCommands.Self.RenameChain(chain, "C");

        UndoManager.Self.Undo();
        Assert.Equal("B", chain.Name);

        UndoManager.Self.Undo();
        Assert.Equal("A", chain.Name);
    }
}
