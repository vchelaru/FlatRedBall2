using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Verifies the shared "leaving tab" capture couplet lives in one place so neither host can
/// drop one of the three captured pieces and reintroduce #687 (lost tree expand state).
/// </summary>
public class TabControllerTests
{
    private sealed class StubCommand : IUndoableCommand
    {
        public string Description => "Stub";
        public bool Do() => true;
        public void Undo() { }
    }

    [Fact]
    public void CaptureLeavingTab_CapturesUndoEditorAndTreeExpandState()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        TestHelpers.MakeChain(ctx.Acls, "Idle");
        // A recorded command makes the undo snapshot non-trivial, proving it was captured.
        ctx.UndoManager.Execute(new StubCommand());

        var expandState = new Dictionary<object, bool> { [new object()] = true };
        var controller = new TabController(ctx.UndoManager, ctx.AppCommands, () => expandState);
        var tab = new TabEntry(new FilePath(""));

        controller.CaptureLeavingTab(tab);

        Assert.Single(tab.UndoSnapshot!.UndoStack);           // undo history captured
        Assert.NotNull(tab.CachedEditorModel);                // in-memory editor model captured
        Assert.Same(expandState, tab.CachedTreeExpandState);  // #687 tree expand state captured
    }
}
