using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>
/// Which sequence is selected after an add, rename or delete, and after undoing or redoing it: the selection follows
/// the sequence that changed. It lives on the document, so undo needs no help from the window.
/// </summary>
public class SequenceDocumentSelectionTests
{
    private static (SequenceDocument Document, UndoManager Undo) Open(TestHelpers.TempDir dir, params string[] names)
    {
        var save = new SequenceListSave();
        save.Sequences.AddRange(names.Select(name => new SequenceSave { Name = name }));
        string path = Path.Combine(dir.Path, "boss.seqj");
        save.Save(path);
        return (SequenceDocument.Load(path), new UndoManager());
    }

    [Fact]
    public void AddSequence_SelectsTheNewOne_UndoGoesBackToThePreviousSelection_RedoToTheNewOne()
    {
        using var dir = new TestHelpers.TempDir();
        var (document, undo) = Open(dir, "A", "B");
        document.SelectedSequence = 0;

        document.AddSequence(undo);
        document.SelectedSequence.ShouldBe(2);
        undo.Undo();
        document.SelectedSequence.ShouldBe(0);
        undo.Redo();
        document.SelectedSequence.ShouldBe(2);
    }

    [Fact]
    public void RenameSequence_UndoAndRedoSelectTheRenamedSequence()
    {
        using var dir = new TestHelpers.TempDir();
        var (document, undo) = Open(dir, "A", "B", "C");
        document.SelectedSequence = 1;
        document.RenameSequence(undo, 1, "Beta");
        document.SelectedSequence = 2;

        undo.Undo();
        document.SelectedSequence.ShouldBe(1);
        document.SelectedSequence = 0;
        undo.Redo();
        document.SelectedSequence.ShouldBe(1);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    public void DeleteSequence_SelectsTheNeighbourItLeaves_UndoReselectsTheDeletedOne_RedoTheNeighbourAgain(int deleted, int selectedAfter)
    {
        using var dir = new TestHelpers.TempDir();
        var (document, undo) = Open(dir, "A", "B", "C");
        document.SelectedSequence = deleted;

        document.DeleteSequence(undo, deleted);
        document.SelectedSequence.ShouldBe(selectedAfter);
        undo.Undo();
        document.SelectedSequence.ShouldBe(deleted);
        document.Save.Sequences.Select(s => s.Name).ShouldBe(new[] { "A", "B", "C" });
        undo.Redo();
        document.SelectedSequence.ShouldBe(selectedAfter);
    }

    [Fact]
    public void DeleteSequence_OfTheOnlyOne_SelectsNothing_UndoSelectsItAgain()
    {
        using var dir = new TestHelpers.TempDir();
        var (document, undo) = Open(dir, "Only");

        document.DeleteSequence(undo, 0);
        document.SelectedSequence.ShouldBe(-1);
        undo.Undo();
        document.SelectedSequence.ShouldBe(0);
    }
}
