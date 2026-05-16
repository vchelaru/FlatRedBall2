using AnimationEditor.Core.CommandsAndState.Commands;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class UndoManagerSaveStateTests
{
    private readonly UndoManager _undo = new();

    // ── Initial state ─────────────────────────────────────────────────────────

    [Fact]
    public void SaveState_IsUnsaved_Initially()
    {
        Assert.Equal(SaveState.Unsaved, _undo.SaveState);
    }

    // ── MarkSaved ─────────────────────────────────────────────────────────────

    [Fact]
    public void MarkSaved_SetsSaveStateToAutoSaveOn()
    {
        _undo.MarkSaved();

        Assert.Equal(SaveState.AutoSaveOn, _undo.SaveState);
    }

    [Fact]
    public void MarkSaved_FiresStackChanged()
    {
        int count = 0;
        _undo.StackChanged += () => count++;

        _undo.MarkSaved();

        Assert.Equal(1, count);
    }

    [Fact]
    public void MarkSaved_IsIdempotent()
    {
        _undo.MarkSaved();
        _undo.MarkSaved();

        Assert.Equal(SaveState.AutoSaveOn, _undo.SaveState);
    }

    // ── MarkSaveFailed ────────────────────────────────────────────────────────

    [Fact]
    public void MarkSaveFailed_SetsSaveStateToFailed()
    {
        _undo.MarkSaveFailed();

        Assert.Equal(SaveState.Failed, _undo.SaveState);
    }

    [Fact]
    public void MarkSaveFailed_FiresStackChanged()
    {
        int count = 0;
        _undo.StackChanged += () => count++;

        _undo.MarkSaveFailed();

        Assert.Equal(1, count);
    }

    [Fact]
    public void MarkSaved_AfterFailed_RestoredToAutoSaveOn()
    {
        _undo.MarkSaveFailed();
        _undo.MarkSaved();

        Assert.Equal(SaveState.AutoSaveOn, _undo.SaveState);
    }

    // ── Clear ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Clear_ResetsSaveStateToUnsaved_WhenPreviouslySaved()
    {
        _undo.MarkSaved();

        _undo.Clear();

        Assert.Equal(SaveState.Unsaved, _undo.SaveState);
    }

    [Fact]
    public void Clear_ResetsSaveStateToUnsaved_WhenPreviouslyFailed()
    {
        _undo.MarkSaveFailed();

        _undo.Clear();

        Assert.Equal(SaveState.Unsaved, _undo.SaveState);
    }

    // ── Record / Undo / Redo do not affect save state ─────────────────────────

    [Fact]
    public void Record_DoesNotChangeSaveState_WhenAutoSaveOn()
    {
        _undo.MarkSaved();

        _undo.Record(new StubCommand());

        Assert.Equal(SaveState.AutoSaveOn, _undo.SaveState);
    }

    [Fact]
    public void Undo_DoesNotChangeSaveState_WhenAutoSaveOn()
    {
        _undo.Record(new StubCommand());
        _undo.MarkSaved();

        _undo.Undo();

        Assert.Equal(SaveState.AutoSaveOn, _undo.SaveState);
    }

    [Fact]
    public void Redo_DoesNotChangeSaveState_WhenAutoSaveOn()
    {
        _undo.Record(new StubCommand());
        _undo.Undo();
        _undo.MarkSaved();

        _undo.Redo();

        Assert.Equal(SaveState.AutoSaveOn, _undo.SaveState);
    }

    // ── Stub ──────────────────────────────────────────────────────────────────

    private sealed class StubCommand : IUndoableCommand
    {
        public string Description => "Stub";
        public bool Do() => true;
        public void Undo() { }
        public void Redo() { }
    }
}
