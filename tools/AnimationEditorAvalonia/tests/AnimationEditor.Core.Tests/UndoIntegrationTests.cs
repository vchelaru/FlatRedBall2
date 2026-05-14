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
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;

        // Step 1: add a chain
        await ctx.AppCommands.AddAnimationChain();
        var chain = acls.AnimationChains[0];

        // Step 2: add a frame to it
        ctx.AppCommands.AddFrame(chain, "sprite.png");
        Assert.Single(chain.Frames);

        // Undo frame first
        ctx.UndoManager.Undo();
        Assert.Empty(chain.Frames);
        Assert.Single(acls.AnimationChains);

        // Undo chain second
        ctx.UndoManager.Undo();
        Assert.Empty(acls.AnimationChains);
    }

    [Fact]
    public async Task MultiStep_UndoThenRedo_RestoresFullHistory()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;

        await ctx.AppCommands.AddAnimationChain();
        var chain = acls.AnimationChains[0];
        ctx.AppCommands.AddFrame(chain, "a.png");
        ctx.AppCommands.AddFrame(chain, "b.png");

        // Undo both frames
        ctx.UndoManager.Undo();
        ctx.UndoManager.Undo();
        Assert.Empty(chain.Frames);

        // Redo both frames
        ctx.UndoManager.Redo();
        ctx.UndoManager.Redo();
        Assert.Equal(2, chain.Frames.Count);
    }

    [Fact]
    public void MultiStep_UndoAcrossSelectionChange_AppliesCorrectly()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chainA = TestHelpers.MakeChain(acls, "A");
        var chainB = TestHelpers.MakeChain(acls, "B");

        // Rename chain A while it's selected
        ctx.AppCommands.RenameChain(chainA, "A_renamed");

        // Switch selection to chain B (simulates the user clicking elsewhere)
        ctx.SelectedState.SelectedChain = chainB;

        // Undo should still restore chain A's name
        ctx.UndoManager.Undo();

        Assert.Equal("A", chainA.Name);
        Assert.Equal("B", chainB.Name); // B unaffected
    }

    // ── NewFile clears history ────────────────────────────────────────────────

    [Fact]
    public async Task NewFile_ClearsUndoStack()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        await ctx.AppCommands.AddAnimationChain();
        Assert.True(ctx.UndoManager.CanUndo);

        ctx.AppCommands.NewFile();

        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public async Task NewFile_ClearsRedoStack()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        await ctx.AppCommands.AddAnimationChain();
        ctx.UndoManager.Undo(); // moves to redo stack
        Assert.True(ctx.UndoManager.CanRedo);

        ctx.AppCommands.NewFile();

        Assert.False(ctx.UndoManager.CanRedo);
    }

    // ── StackChanged event ────────────────────────────────────────────────────

    [Fact]
    public void StackChanged_FiresOnRecord()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        int count = 0;
        ctx.UndoManager.StackChanged += () => count++;

        ctx.UndoManager.Record(new StubCmd());

        Assert.Equal(1, count);
        ctx.UndoManager.StackChanged -= () => count++; // cleanup (delegate identity — use flag pattern instead)
    }

    [Fact]
    public void StackChanged_FiresOnUndo()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        int count = 0;
        void Handler() => count++;
        ctx.UndoManager.StackChanged += Handler;
        ctx.UndoManager.Record(new StubCmd());
        count = 0; // reset after Record

        ctx.UndoManager.Undo();

        Assert.Equal(1, count);
        ctx.UndoManager.StackChanged -= Handler;
    }

    [Fact]
    public void StackChanged_FiresOnRedo()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        int count = 0;
        void Handler() => count++;
        ctx.UndoManager.StackChanged += Handler;
        ctx.UndoManager.Record(new StubCmd());
        ctx.UndoManager.Undo();
        count = 0; // reset after Record + Undo

        ctx.UndoManager.Redo();

        Assert.Equal(1, count);
        ctx.UndoManager.StackChanged -= Handler;
    }

    [Fact]
    public void StackChanged_FiresOnClear()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        int count = 0;
        void Handler() => count++;
        ctx.UndoManager.StackChanged += Handler;
        ctx.UndoManager.Record(new StubCmd());
        count = 0;

        ctx.UndoManager.Clear();

        Assert.Equal(1, count);
        ctx.UndoManager.StackChanged -= Handler;
    }

    // ── Stub ──────────────────────────────────────────────────────────────────

    private sealed class StubCmd : IUndoableCommand
    {
        public bool Do() => true;
        public void Undo() { }
        public void Redo() { }
    }
}
