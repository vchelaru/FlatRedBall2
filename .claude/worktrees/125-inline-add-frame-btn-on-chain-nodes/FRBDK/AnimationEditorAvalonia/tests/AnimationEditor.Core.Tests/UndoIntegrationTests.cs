using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Cross-cutting scenarios: multi-step ordering, lifecycle clear, and StackChanged event.
/// </summary>
[Collection("SequentialSingletons")]
public class UndoIntegrationTests
{
    // ── Multi-step ordering ───────────────────────────────────────────────────

    [Fact]
    public async Task MultiStep_AddChainThenAddFrame_UndoInOrder()
    {
        var acls = TestHelpers.SetupFreshAcls();

        // Step 1: add a chain
        await AppCommands.Self.AddAnimationChain();
        var chain = acls.AnimationChains[0];

        // Step 2: add a frame to it
        AppCommands.Self.AddFrame(chain, "sprite.png");
        Assert.Single(chain.Frames);

        // Undo frame first
        UndoManager.Self.Undo();
        Assert.Empty(chain.Frames);
        Assert.Single(acls.AnimationChains);

        // Undo chain second
        UndoManager.Self.Undo();
        Assert.Empty(acls.AnimationChains);
    }

    [Fact]
    public async Task MultiStep_UndoThenRedo_RestoresFullHistory()
    {
        var acls = TestHelpers.SetupFreshAcls();

        await AppCommands.Self.AddAnimationChain();
        var chain = acls.AnimationChains[0];
        AppCommands.Self.AddFrame(chain, "a.png");
        AppCommands.Self.AddFrame(chain, "b.png");

        // Undo both frames
        UndoManager.Self.Undo();
        UndoManager.Self.Undo();
        Assert.Empty(chain.Frames);

        // Redo both frames
        UndoManager.Self.Redo();
        UndoManager.Self.Redo();
        Assert.Equal(2, chain.Frames.Count);
    }

    [Fact]
    public void MultiStep_UndoAcrossSelectionChange_AppliesCorrectly()
    {
        var acls = TestHelpers.SetupFreshAcls();
        var chainA = TestHelpers.MakeChain(acls, "A");
        var chainB = TestHelpers.MakeChain(acls, "B");

        // Rename chain A while it's selected
        AppCommands.Self.RenameChain(chainA, "A_renamed");

        // Switch selection to chain B (simulates the user clicking elsewhere)
        SelectedState.Self.SelectedChain = chainB;

        // Undo should still restore chain A's name
        UndoManager.Self.Undo();

        Assert.Equal("A", chainA.Name);
        Assert.Equal("B", chainB.Name); // B unaffected
    }

    // ── NewFile clears history ────────────────────────────────────────────────

    [Fact]
    public async Task NewFile_ClearsUndoStack()
    {
        var acls = TestHelpers.SetupFreshAcls();
        await AppCommands.Self.AddAnimationChain();
        Assert.True(UndoManager.Self.CanUndo);

        AppCommands.Self.NewFile();

        Assert.False(UndoManager.Self.CanUndo);
    }

    [Fact]
    public async Task NewFile_ClearsRedoStack()
    {
        var acls = TestHelpers.SetupFreshAcls();
        await AppCommands.Self.AddAnimationChain();
        UndoManager.Self.Undo(); // moves to redo stack
        Assert.True(UndoManager.Self.CanRedo);

        AppCommands.Self.NewFile();

        Assert.False(UndoManager.Self.CanRedo);
    }

    // ── StackChanged event ────────────────────────────────────────────────────

    [Fact]
    public void StackChanged_FiresOnRecord()
    {
        TestHelpers.SetupFreshAcls();
        int count = 0;
        UndoManager.Self.StackChanged += () => count++;

        UndoManager.Self.Record(new StubCmd());

        Assert.Equal(1, count);
        UndoManager.Self.StackChanged -= () => count++; // cleanup (delegate identity — use flag pattern instead)
    }

    [Fact]
    public void StackChanged_FiresOnUndo()
    {
        TestHelpers.SetupFreshAcls();
        int count = 0;
        void Handler() => count++;
        UndoManager.Self.StackChanged += Handler;
        UndoManager.Self.Record(new StubCmd());
        count = 0; // reset after Record

        UndoManager.Self.Undo();

        Assert.Equal(1, count);
        UndoManager.Self.StackChanged -= Handler;
    }

    [Fact]
    public void StackChanged_FiresOnRedo()
    {
        TestHelpers.SetupFreshAcls();
        int count = 0;
        void Handler() => count++;
        UndoManager.Self.StackChanged += Handler;
        UndoManager.Self.Record(new StubCmd());
        UndoManager.Self.Undo();
        count = 0; // reset after Record + Undo

        UndoManager.Self.Redo();

        Assert.Equal(1, count);
        UndoManager.Self.StackChanged -= Handler;
    }

    [Fact]
    public void StackChanged_FiresOnClear()
    {
        TestHelpers.SetupFreshAcls();
        int count = 0;
        void Handler() => count++;
        UndoManager.Self.StackChanged += Handler;
        UndoManager.Self.Record(new StubCmd());
        count = 0;

        UndoManager.Self.Clear();

        Assert.Equal(1, count);
        UndoManager.Self.StackChanged -= Handler;
    }

    // ── Stub ──────────────────────────────────────────────────────────────────

    private sealed class StubCmd : IUndoableCommand
    {
        public void Undo() { }
        public void Redo() { }
    }
}
