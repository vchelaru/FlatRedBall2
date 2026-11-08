using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Sort Sequences Alphabetically: the same order as the ANIMATIONS tree's sort, one undo entry, selection kept by name.</summary>
public class SequenceDocumentSortTests
{
    private static SequenceDocument Open(TestHelpers.TempDir dir, params string[] names)
    {
        var save = new SequenceListSave();
        save.Sequences.AddRange(names.Select(name => new SequenceSave { Name = name, Loop = false }));
        string path = Path.Combine(dir.Path, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    private static void Select(SequenceDocument document, params string[] names)
    {
        document.Selection.Apply(names);
        document.SelectedSequence = document.Save.Sequences.FindIndex(s => s.Name == document.Selection.Primary);
    }

    private static string[] Names(SequenceDocument document) => document.Save.Sequences.Select(s => s.Name).ToArray();

    private static string[] Labels(UndoManager undo) => undo.UndoHistory.Select(c => c.Description).ToArray();

    [Fact]
    public void SortSequences_OrdersByNameIgnoringCase_SavesAndIsOneUndoEntry()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "walk", "Attack", "idle", "Boss");
        var undo = new UndoManager();

        document.SortSequences(undo).ShouldBeTrue();

        Names(document).ShouldBe(new[] { "Attack", "Boss", "idle", "walk" });
        SequenceListSave.FromFile(document.Path.FullPath).Sequences.Select(s => s.Name).ShouldBe(new[] { "Attack", "Boss", "idle", "walk" });
        Labels(undo).ShouldBe(new[] { "Sort Sequences Alphabetically" });
    }

    [Fact]
    public void SortSequences_KeepsTheSameSequencesSelected_AndThePrimary()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "D", "C", "B", "A");
        var undo = new UndoManager();
        Select(document, "C", "A");

        document.SortSequences(undo);

        Names(document).ShouldBe(new[] { "A", "B", "C", "D" });
        document.Selection.Names.ShouldBe(new[] { "C", "A" });
        document.Selection.Primary.ShouldBe("C");
        document.SelectedSequence.ShouldBe(2);
        document.SelectedIndices.OrderBy(i => i).ShouldBe(new[] { 0, 2 });
    }

    [Fact]
    public void SortSequences_Undo_PutsBackTheOrderAndTheSelection_RedoSortsAgain()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "D", "C", "B", "A");
        var undo = new UndoManager();
        Select(document, "C", "A");
        document.SortSequences(undo);

        undo.Undo();

        Names(document).ShouldBe(new[] { "D", "C", "B", "A" });
        document.Selection.Names.ShouldBe(new[] { "C", "A" });
        document.Selection.Primary.ShouldBe("C");
        document.SelectedSequence.ShouldBe(1);

        undo.Redo();

        Names(document).ShouldBe(new[] { "A", "B", "C", "D" });
        document.Selection.Primary.ShouldBe("C");
        document.SelectedSequence.ShouldBe(2);
    }

    [Fact]
    public void SortSequences_AlreadySorted_IsANoOp_WithNoUndoEntry()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "A", "b", "C");
        var undo = new UndoManager();
        Select(document, "b");

        document.SortSequences(undo).ShouldBeFalse();

        Labels(undo).ShouldBeEmpty();
        Names(document).ShouldBe(new[] { "A", "b", "C" });
        document.SelectedSequence.ShouldBe(1);
    }

    [Fact]
    public void SortSequences_ChangesNothingInsideTheSequences()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "B", "A");
        document.Save.Sequences[0].Loop = true;
        var undo = new UndoManager();

        document.SortSequences(undo);

        document.Save.Sequences.Single(s => s.Name == "B").Loop.ShouldBeTrue();
        document.Save.Sequences.Single(s => s.Name == "A").Loop.ShouldBeFalse();
    }
}
