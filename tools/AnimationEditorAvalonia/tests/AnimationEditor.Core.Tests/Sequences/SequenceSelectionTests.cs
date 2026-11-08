using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>
/// The sequence list's multi-selection: selection order, the primary, and how it follows edits, undo, redo and renames.
/// Several selected sequences only play together in the preview; the primary is the one everything else acts on.
/// </summary>
public class SequenceSelectionTests
{
    private static readonly string[] Names = { "Idle", "Walk", "Attack", "Death" };

    [Fact]
    public void Apply_KeepsTheListsOrder_AndTheFirstSelectedIsThePrimary()
    {
        var selection = new SequenceSelection();

        selection.Apply(new[] { "Walk", "Death", "Idle" });

        selection.Names.ShouldBe(new[] { "Walk", "Death", "Idle" });
        selection.Primary.ShouldBe("Walk");
    }

    [Fact]
    public void Apply_ExtendingTheSelection_KeepsThePrimary_EvenWhenTheNewOneComesFirst()
    {
        var selection = new SequenceSelection();
        selection.Apply(new[] { "Walk" });

        selection.Apply(new[] { "Idle", "Walk" });

        selection.Primary.ShouldBe("Walk");
    }

    [Fact]
    public void Apply_DeselectingThePrimary_MakesTheFirstRemainingOneThePrimary()
    {
        var selection = new SequenceSelection();
        selection.Apply(new[] { "Walk", "Death", "Idle" });

        selection.Apply(new[] { "Death", "Idle" });

        selection.Primary.ShouldBe("Death");
    }

    [Fact]
    public void Apply_AnEmptyList_SelectsNothing()
    {
        var selection = new SequenceSelection();
        selection.Apply(new[] { "Walk" });

        selection.Apply(new string[0]);

        selection.Names.ShouldBeEmpty();
        selection.Primary.ShouldBeNull();
    }

    [Fact]
    public void Reconcile_ASequenceThatNoLongerExists_DropsOut()
    {
        var selection = new SequenceSelection();
        selection.Apply(new[] { "Walk", "Death", "Idle" });

        selection.Reconcile(new[] { "Idle", "Walk", "Attack" }, primaryIndex: 1);

        selection.Names.ShouldBe(new[] { "Walk", "Idle" });
        selection.Primary.ShouldBe("Walk");
    }

    [Fact]
    public void Reconcile_AReorder_KeepsTheSelectionByName_NotByPosition()
    {
        var selection = new SequenceSelection();
        selection.Apply(new[] { "Idle", "Death" });

        selection.Reconcile(new[] { "Death", "Attack", "Walk", "Idle" }, primaryIndex: 3);

        selection.Names.ShouldBe(new[] { "Idle", "Death" });
        selection.Primary.ShouldBe("Idle");
    }

    [Fact]
    public void Reconcile_ThePrimaryMovingToAnotherSelectedSequence_KeepsTheRest()
    {
        // Deleting the primary: the neighbour that takes its place becomes the selected sequence.
        var selection = new SequenceSelection();
        selection.Apply(new[] { "Walk", "Attack" });

        selection.Reconcile(new[] { "Idle", "Attack", "Death" }, primaryIndex: 1);

        selection.Names.ShouldBe(new[] { "Attack" });
        selection.Primary.ShouldBe("Attack");
    }

    [Fact]
    public void Reconcile_ThePrimaryMovingOutsideTheSelection_CollapsesToIt()
    {
        // An add, duplicate or paste selects the new sequence, as a plain click on it would.
        var selection = new SequenceSelection();
        selection.Apply(new[] { "Idle", "Walk" });

        selection.Reconcile(new[] { "Idle", "Walk", "Attack" }, primaryIndex: 2);

        selection.Names.ShouldBe(new[] { "Attack" });
        selection.Primary.ShouldBe("Attack");
    }

    [Fact]
    public void Reconcile_AnEmptyFile_SelectsNothing()
    {
        var selection = new SequenceSelection();
        selection.Apply(new[] { "Walk" });

        selection.Reconcile(new string[0], primaryIndex: -1);

        selection.Names.ShouldBeEmpty();
        selection.Primary.ShouldBeNull();
    }

    [Fact]
    public void Rename_FollowsTheSequence_InPlaceInTheOrder()
    {
        var selection = new SequenceSelection();
        selection.Apply(new[] { "Walk", "Death", "Idle" });

        selection.Rename("Walk", "Stroll");
        selection.Rename("Idle", "Rest");

        selection.Names.ShouldBe(new[] { "Stroll", "Death", "Rest" });
        selection.Primary.ShouldBe("Stroll");
    }

    // ---- through the document

    private static (SequenceDocument Document, UndoManager Undo) Open(TestHelpers.TempDir dir)
    {
        var save = new SequenceListSave();
        foreach (string name in Names) save.Sequences.Add(new SequenceSave { Name = name });
        string path = Path.Combine(dir.Path, "boss.seqj");
        save.Save(path);
        return (SequenceDocument.Load(path), new UndoManager());
    }

    [Fact]
    public void RenamingASelectedSequence_KeepsItSelected_AndUndoAndRedoDoToo()
    {
        using var dir = new TestHelpers.TempDir();
        var (document, undo) = Open(dir);
        document.Selection.Apply(new[] { "Idle", "Attack" });
        document.SelectedSequence = 0;

        // The inline rename of a row keeps the selection; the renamed row is not the primary.
        document.RenameSequence(undo, 2, "Strike", keepSelection: true);
        document.Selection.Names.ShouldBe(new[] { "Idle", "Strike" });

        undo.Undo();
        document.Selection.Names.ShouldBe(new[] { "Idle", "Attack" });
        undo.Redo();
        document.Selection.Names.ShouldBe(new[] { "Idle", "Strike" });
    }

    [Fact]
    public void TheSelection_IsNotSavedToTheFile()
    {
        using var dir = new TestHelpers.TempDir();
        var (document, undo) = Open(dir);
        string before = File.ReadAllText(document.Path.FullPath);

        document.Selection.Apply(new[] { "Idle", "Walk", "Death" });
        document.SaveToDisk();

        File.ReadAllText(document.Path.FullPath).ShouldBe(before);
        undo.CanUndo.ShouldBeFalse();
    }
}
