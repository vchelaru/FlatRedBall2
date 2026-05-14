using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class UndoManagerTests
{
    private readonly UndoManager _undo = new();

    // ── CanUndo / CanRedo initial state ───────────────────────────────────────

    [Fact]
    public void CanRedo_AfterClear_IsFalse()
    {
        Assert.False(_undo.CanRedo);
    }

    [Fact]
    public void CanUndo_AfterClear_IsFalse()
    {
        Assert.False(_undo.CanUndo);
    }

    // ── Clear ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Clear_EmptiesBothStacks()
    {
        var cmd = new StubCommand();
        _undo.Record(cmd);
        _undo.Undo();   // moves cmd to redo stack

        _undo.Clear();

        Assert.False(_undo.CanUndo);
        Assert.False(_undo.CanRedo);
    }

    // ── Record ────────────────────────────────────────────────────────────────

    [Fact]
    public void Record_AfterUndo_ClearsRedoStack()
    {
        var first = new StubCommand();
        _undo.Record(first);
        _undo.Undo();          // first is now on redo stack
        Assert.True(_undo.CanRedo);

        _undo.Record(new StubCommand());  // recording clears redo

        Assert.False(_undo.CanRedo);
    }

    [Fact]
    public void Record_PushesToUndoStack()
    {
        _undo.Record(new StubCommand());
        Assert.True(_undo.CanUndo);
    }

    // ── Redo ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Redo_CallsRedoOnCommand()
    {
        var cmd = new StubCommand();
        _undo.Record(cmd);
        _undo.Undo();

        _undo.Redo();

        Assert.Equal(1, cmd.RedoCalls);
    }

    [Fact]
    public void Redo_PushesBackToUndoStack()
    {
        _undo.Record(new StubCommand());
        _undo.Undo();

        _undo.Redo();

        Assert.True(_undo.CanUndo);
    }

    [Fact]
    public void Redo_WhenEmpty_DoesNotThrow()
    {
        var ex = Record.Exception(() => _undo.Redo());
        Assert.Null(ex);
    }

    // ── Undo ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Undo_CallsUndoOnCommand()
    {
        var cmd = new StubCommand();
        _undo.Record(cmd);

        _undo.Undo();

        Assert.Equal(1, cmd.UndoCalls);
    }

    [Fact]
    public void Undo_PushesToRedoStack()
    {
        _undo.Record(new StubCommand());

        _undo.Undo();

        Assert.True(_undo.CanRedo);
    }

    [Fact]
    public void Undo_WhenEmpty_DoesNotThrow()
    {
        var ex = Record.Exception(() => _undo.Undo());
        Assert.Null(ex);
    }

    // ── Stub ──────────────────────────────────────────────────────────────────

    private sealed class StubCommand : IUndoableCommand
    {
        public int UndoCalls { get; private set; }
        public int RedoCalls { get; private set; }
        public bool Do() => true;
        public void Undo() => UndoCalls++;
        public void Redo() => RedoCalls++;
    }
}
