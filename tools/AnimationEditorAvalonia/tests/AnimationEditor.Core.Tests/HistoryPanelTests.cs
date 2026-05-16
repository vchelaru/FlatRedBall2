using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class HistoryPanelTests
{
    private readonly UndoManager _undo = new();

    // ── UndoHistory ───────────────────────────────────────────────────────────

    [Fact]
    public void UndoHistory_WhenEmpty_IsEmpty()
    {
        Assert.Empty(_undo.UndoHistory);
    }

    [Fact]
    public void UndoHistory_AfterRecord_ContainsEntry()
    {
        _undo.Record(new StubCommand("A"));

        Assert.Single(_undo.UndoHistory);
    }

    [Fact]
    public void UndoHistory_ReturnsEntriesOldestFirst()
    {
        _undo.Record(new StubCommand("A"));
        _undo.Record(new StubCommand("B"));
        _undo.Record(new StubCommand("C"));

        var history = _undo.UndoHistory;

        Assert.Equal("A", history[0].Description);
        Assert.Equal("B", history[1].Description);
        Assert.Equal("C", history[2].Description);
    }

    [Fact]
    public void UndoHistory_AfterUndo_ShrinksBy1()
    {
        _undo.Record(new StubCommand("A"));
        _undo.Record(new StubCommand("B"));

        _undo.Undo();

        Assert.Single(_undo.UndoHistory);
        Assert.Equal("A", _undo.UndoHistory[0].Description);
    }

    // ── RedoHistory ───────────────────────────────────────────────────────────

    [Fact]
    public void RedoHistory_WhenEmpty_IsEmpty()
    {
        Assert.Empty(_undo.RedoHistory);
    }

    [Fact]
    public void RedoHistory_AfterUndo_ContainsUndoneEntry()
    {
        _undo.Record(new StubCommand("A"));
        _undo.Undo();

        Assert.Single(_undo.RedoHistory);
        Assert.Equal("A", _undo.RedoHistory[0].Description);
    }

    [Fact]
    public void RedoHistory_ReturnsEntriesFirstToRedoFirst()
    {
        // Record A, B, C — then undo all three.
        _undo.Record(new StubCommand("A"));
        _undo.Record(new StubCommand("B"));
        _undo.Record(new StubCommand("C"));
        _undo.Undo(); // C goes to redo
        _undo.Undo(); // B goes to redo
        _undo.Undo(); // A goes to redo

        // Redo order: A first, then B, then C.
        var redo = _undo.RedoHistory;
        Assert.Equal("A", redo[0].Description);
        Assert.Equal("B", redo[1].Description);
        Assert.Equal("C", redo[2].Description);
    }

    [Fact]
    public void RedoHistory_AfterRedo_ShrinksBy1()
    {
        _undo.Record(new StubCommand("A"));
        _undo.Record(new StubCommand("B"));
        _undo.Undo();
        _undo.Undo();

        _undo.Redo();

        Assert.Single(_undo.RedoHistory);
        Assert.Equal("B", _undo.RedoHistory[0].Description);
    }

    // ── Command Descriptions ─────────────────────────────────────────────────

    [Fact]
    public void AddChainCommand_Description_ContainsChainName()
    {
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Walk" };
        var ctx = TestHelpers.SetupFreshAcls();
        var cmd = new AddChainCommand(chain, ctx.Acls, ctx.AppCommands, ctx.ApplicationEvents, ctx.SelectedState);

        Assert.Contains("Walk", cmd.Description);
    }

    [Fact]
    public void RenameChainCommand_Description_ContainsBothNames()
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        var ctx = TestHelpers.SetupFreshAcls();
        var cmd = new RenameChainCommand(chain, "Walk", "Run", ctx.AppCommands, ctx.ApplicationEvents);

        Assert.Contains("Walk", cmd.Description);
        Assert.Contains("Run", cmd.Description);
    }

    [Fact]
    public void DeleteChainsCommand_SingleChain_Description_ContainsChainName()
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        var ctx = TestHelpers.SetupFreshAcls();
        var cmd = new DeleteChainsCommand(new[] { chain }, ctx.Acls, ctx.AppCommands, ctx.ApplicationEvents, ctx.SelectedState);

        Assert.Contains("Walk", cmd.Description);
    }

    [Fact]
    public void DeleteChainsCommand_MultipleChains_Description_ContainsCount()
    {
        var chains = new[]
        {
            new AnimationChainSave { Name = "Walk" },
            new AnimationChainSave { Name = "Run" },
        };
        var ctx = TestHelpers.SetupFreshAcls();
        var cmd = new DeleteChainsCommand(chains, ctx.Acls, ctx.AppCommands, ctx.ApplicationEvents, ctx.SelectedState);

        Assert.Contains("2", cmd.Description);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private sealed class StubCommand(string description) : IUndoableCommand
    {
        public string Description { get; } = description;
        public bool Do() => true;
        public void Undo() { }
    }
}
