using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>
/// Dragging sequences in the list rewrites the file's sequence order. One undo entry for the whole block; undo and redo
/// put back both the order and the selection, and the document's selected index follows the primary sequence by name.
/// </summary>
public class SequenceDocumentMoveSequencesTests
{
    private static SequenceDocument Open(TestHelpers.TempDir dir, params string[] names)
    {
        var save = new SequenceListSave();
        save.Sequences.AddRange(names.Select(name => new SequenceSave { Name = name }));
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

    private static string[] SavedNames(SequenceDocument document) =>
        SequenceListSave.FromFile(document.Path.FullPath).Sequences.Select(s => s.Name).ToArray();

    private static string[] Labels(UndoManager undo) => undo.UndoHistory.Select(c => c.Description).ToArray();

    [Fact]
    public void MoveSequences_OneRow_ReordersTheFileAndSaves_AsOneNamedUndoEntry()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "A", "B", "C", "D");
        var undo = new UndoManager();
        Select(document, "A");

        bool moved = document.MoveSequences(undo, new[] { 0 }, 3);

        moved.ShouldBeTrue();
        Names(document).ShouldBe(new[] { "B", "C", "A", "D" });
        SavedNames(document).ShouldBe(new[] { "B", "C", "A", "D" });
        Labels(undo).ShouldBe(new[] { "Move Sequence 'A'" });
    }

    [Fact]
    public void MoveSequences_SeveralRows_MoveAsABlock_UnderOneEntryNamedForTheCount()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "A", "B", "C", "D", "E");
        var undo = new UndoManager();
        Select(document, "B", "D", "C");

        document.MoveSequences(undo, document.SelectedIndices, 0);

        Names(document).ShouldBe(new[] { "B", "C", "D", "A", "E" });
        Labels(undo).ShouldBe(new[] { "Move 3 Sequences" });
    }

    [Fact]
    public void MoveSequences_KeepsTheSelectionAndThePrimary_AndSelectedSequenceFollowsThePrimaryByName()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "A", "B", "C", "D", "E");
        var undo = new UndoManager();
        Select(document, "D", "B");

        document.MoveSequences(undo, document.SelectedIndices, 5);

        Names(document).ShouldBe(new[] { "A", "C", "E", "B", "D" });
        document.Selection.Names.ShouldBe(new[] { "D", "B" });
        document.Selection.Primary.ShouldBe("D");
        document.SelectedSequence.ShouldBe(4);
    }

    [Fact]
    public void MoveSequences_Undo_RestoresTheOrderAndSelection_RedoMovesAgain()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "A", "B", "C", "D", "E");
        var undo = new UndoManager();
        Select(document, "D", "B");
        document.MoveSequences(undo, document.SelectedIndices, 0);

        undo.Undo();

        Names(document).ShouldBe(new[] { "A", "B", "C", "D", "E" });
        SavedNames(document).ShouldBe(new[] { "A", "B", "C", "D", "E" });
        document.Selection.Names.ShouldBe(new[] { "D", "B" });
        document.Selection.Primary.ShouldBe("D");
        document.SelectedSequence.ShouldBe(3);

        undo.Redo();

        Names(document).ShouldBe(new[] { "B", "D", "A", "C", "E" });
        document.Selection.Names.ShouldBe(new[] { "D", "B" });
        document.Selection.Primary.ShouldBe("D");
        document.SelectedSequence.ShouldBe(1);
    }

    [Fact]
    public void MoveSequences_DropOnItself_IsANoOp_NoUndoEntry_NoChange()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "A", "B", "C");
        var undo = new UndoManager();
        Select(document, "B");

        document.MoveSequences(undo, new[] { 1 }, 1).ShouldBeFalse();
        document.MoveSequences(undo, new[] { 1 }, 2).ShouldBeFalse();

        Labels(undo).ShouldBeEmpty();
        Names(document).ShouldBe(new[] { "A", "B", "C" });
        document.SelectedSequence.ShouldBe(1);
    }

    [Fact]
    public void MoveSequences_ChangesNothingInsideTheSequences()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "A", "B");
        document.Save.Sequences[0].Loop = true;
        var undo = new UndoManager();
        Select(document, "A");

        document.MoveSequences(undo, new[] { 0 }, 2);

        document.Save.Sequences.Single(s => s.Name == "A").Loop.ShouldBeTrue();
        document.Save.Sequences.Single(s => s.Name == "B").Loop.ShouldBeFalse();
    }
}
