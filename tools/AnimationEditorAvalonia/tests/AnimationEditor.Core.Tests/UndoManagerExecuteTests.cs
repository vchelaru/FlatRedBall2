using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Covers <see cref="UndoManager.Execute"/> — the execute-through-command chokepoint
/// that runs <see cref="IUndoableCommand.Do"/> and records the command in one step.
/// </summary>
public class UndoManagerExecuteTests
{
    private readonly UndoManager _undo = new();

    [Fact]
    public void Execute_RunsDoAndPushesToUndoStack()
    {
        var cmd = new SpyCommand(doResult: true);

        _undo.Execute(cmd);

        Assert.Equal(1, cmd.DoCalls);
        Assert.True(_undo.CanUndo);
    }

    [Fact]
    public void Execute_ClearsRedoStack()
    {
        _undo.Execute(new SpyCommand(doResult: true));
        _undo.Undo();                       // moves the command to the redo stack
        Assert.True(_undo.CanRedo);

        _undo.Execute(new SpyCommand(doResult: true));

        Assert.False(_undo.CanRedo);
    }

    [Fact]
    public void Execute_WhenDoReturnsFalse_DoesNotRecordEntry()
    {
        // Do() returning false means the command was a no-op (e.g. a reorder that
        // produced an identical list) — it must not pollute the undo stack.
        var cmd = new SpyCommand(doResult: false);

        _undo.Execute(cmd);

        Assert.Equal(1, cmd.DoCalls);
        Assert.False(_undo.CanUndo);
    }

    private sealed class SpyCommand : IUndoableCommand
    {
        private readonly bool _doResult;

        public string Description => "Spy";
        public SpyCommand(bool doResult) => _doResult = doResult;

        public int DoCalls { get; private set; }

        public bool Do() { DoCalls++; return _doResult; }
        public void Undo() { }
        public void Redo() { }
    }
}
