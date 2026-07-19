using System.Linq;
using AnimationEditor.Core.CommandsAndState.Commands;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class HistoryRowBuilderTests
{
    [Fact]
    public void BuildRows_EmptyUndoAndRedo_YieldsNoRows()
    {
        var rows = HistoryRowBuilder.BuildRows(new StubCommand[0], new StubCommand[0]);

        Assert.Empty(rows);
    }

    [Fact]
    public void BuildRows_OnlyUndo_MarksLastEntryAsCurrent()
    {
        var undo = new[] { new StubCommand("A"), new StubCommand("B"), new StubCommand("C") };

        var rows = HistoryRowBuilder.BuildRows(undo, new StubCommand[0]).ToList();

        Assert.Equal(new[] { "A", "B", "C" }, rows.Select(r => r.Description));
        Assert.Equal(new[] { false, false, true }, rows.Select(r => r.IsCurrent));
        Assert.All(rows, r => Assert.False(r.IsRedo));
    }

    [Fact]
    public void BuildRows_SingleUndoEntry_IsCurrent()
    {
        var rows = HistoryRowBuilder.BuildRows(new[] { new StubCommand("Only") }, new StubCommand[0]).ToList();

        var row = Assert.Single(rows);
        Assert.True(row.IsCurrent);
        Assert.False(row.IsRedo);
    }

    [Fact]
    public void BuildRows_OnlyRedo_NoneAreCurrentAndAllAreRedo()
    {
        var redo = new[] { new StubCommand("NextRedo"), new StubCommand("FutureRedo") };

        var rows = HistoryRowBuilder.BuildRows(new StubCommand[0], redo).ToList();

        Assert.Equal(new[] { "NextRedo", "FutureRedo" }, rows.Select(r => r.Description));
        Assert.All(rows, r => Assert.False(r.IsCurrent));
        Assert.All(rows, r => Assert.True(r.IsRedo));
    }

    [Fact]
    public void BuildRows_UndoAndRedoBothPopulated_OrdersUndoThenRedo()
    {
        var undo = new[] { new StubCommand("A"), new StubCommand("B") };
        var redo = new[] { new StubCommand("C"), new StubCommand("D") };

        var rows = HistoryRowBuilder.BuildRows(undo, redo).ToList();

        Assert.Equal(new[] { "A", "B", "C", "D" }, rows.Select(r => r.Description));
        Assert.Equal(new[] { false, true, false, false }, rows.Select(r => r.IsCurrent));
        Assert.Equal(new[] { false, false, true, true }, rows.Select(r => r.IsRedo));
    }

    private sealed class StubCommand : IUndoableCommand
    {
        public StubCommand(string description = "Stub") => Description = description;
        public string Description { get; }
        public bool Do() => true;
        public void Undo() { }
    }
}
