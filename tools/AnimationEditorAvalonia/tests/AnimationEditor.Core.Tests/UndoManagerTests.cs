using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class UndoManagerTests
{
    private static void Reset() => UndoManager.Self.Clear();

    // ── CanUndo / CanRedo initial state ───────────────────────────────────────

    [Fact]
    public void CanRedo_AfterClear_IsFalse()
    {
        Reset();
        Assert.False(UndoManager.Self.CanRedo);
    }

    [Fact]
    public void CanUndo_AfterClear_IsFalse()
    {
        Reset();
        Assert.False(UndoManager.Self.CanUndo);
    }

    // ── Clear ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Clear_EmptiesBothStacks()
    {
        Reset();
        var cmd = new StubCommand();
        UndoManager.Self.Record(cmd);
        UndoManager.Self.Undo();   // moves cmd to redo stack

        UndoManager.Self.Clear();

        Assert.False(UndoManager.Self.CanUndo);
        Assert.False(UndoManager.Self.CanRedo);
    }

    // ── Record ────────────────────────────────────────────────────────────────

    [Fact]
    public void Record_AfterUndo_ClearsRedoStack()
    {
        Reset();
        var first = new StubCommand();
        UndoManager.Self.Record(first);
        UndoManager.Self.Undo();          // first is now on redo stack
        Assert.True(UndoManager.Self.CanRedo);

        UndoManager.Self.Record(new StubCommand());  // recording clears redo

        Assert.False(UndoManager.Self.CanRedo);
    }

    [Fact]
    public void Record_PushesToUndoStack()
    {
        Reset();
        UndoManager.Self.Record(new StubCommand());
        Assert.True(UndoManager.Self.CanUndo);
    }

    // ── Redo ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Redo_CallsRedoOnCommand()
    {
        Reset();
        var cmd = new StubCommand();
        UndoManager.Self.Record(cmd);
        UndoManager.Self.Undo();

        UndoManager.Self.Redo();

        Assert.Equal(1, cmd.RedoCalls);
    }

    [Fact]
    public void Redo_PushesBackToUndoStack()
    {
        Reset();
        UndoManager.Self.Record(new StubCommand());
        UndoManager.Self.Undo();

        UndoManager.Self.Redo();

        Assert.True(UndoManager.Self.CanUndo);
    }

    [Fact]
    public void Redo_WhenEmpty_DoesNotThrow()
    {
        Reset();
        var ex = Record.Exception(() => UndoManager.Self.Redo());
        Assert.Null(ex);
    }

    // ── Undo ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Undo_CallsUndoOnCommand()
    {
        Reset();
        var cmd = new StubCommand();
        UndoManager.Self.Record(cmd);

        UndoManager.Self.Undo();

        Assert.Equal(1, cmd.UndoCalls);
    }

    [Fact]
    public void Undo_PushesToRedoStack()
    {
        Reset();
        UndoManager.Self.Record(new StubCommand());

        UndoManager.Self.Undo();

        Assert.True(UndoManager.Self.CanRedo);
    }

    [Fact]
    public void Undo_WhenEmpty_DoesNotThrow()
    {
        Reset();
        var ex = Record.Exception(() => UndoManager.Self.Undo());
        Assert.Null(ex);
    }

    // ── Stub ──────────────────────────────────────────────────────────────────

    private sealed class StubCommand : IUndoableCommand
    {
        public int UndoCalls { get; private set; }
        public int RedoCalls { get; private set; }
        public void Undo() => UndoCalls++;
        public void Redo() => RedoCalls++;
    }
}
