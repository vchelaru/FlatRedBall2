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

    [Fact]
    public void RenameSequence_KeepSelection_LeavesTheSelectionAlone_ButUndoAndRedoStillSelectTheRenamedOne()
    {
        using var dir = new TestHelpers.TempDir();
        var (document, undo) = Open(dir, "A", "B", "C");
        document.SelectedSequence = 2;

        document.RenameSequence(undo, 0, "Alpha", keepSelection: true);
        document.SelectedSequence.ShouldBe(2);
        undo.Undo();
        document.SelectedSequence.ShouldBe(0);
        undo.Redo();
        document.SelectedSequence.ShouldBe(0);
    }

    [Fact]
    public void RenameTrack_SelectsItsSequence_UnlessKeepSelection_AndUndoAndRedoAlwaysDo()
    {
        using var dir = new TestHelpers.TempDir();
        var (document, undo) = Open(dir, "A", "B");
        document.Save.Sequences[0].Tracks.Add(new SequenceTrackSave { Name = "Body" });
        document.Save.Sequences[0].Tracks.Add(new SequenceTrackSave { Name = "Arms" });
        document.SelectedSequence = 1;

        document.RenameTrack(undo, 0, 1, "Legs", keepSelection: true);
        document.SelectedSequence.ShouldBe(1);
        undo.Undo();
        document.SelectedSequence.ShouldBe(0);
        document.SelectedSequence = 1;
        undo.Redo();
        document.SelectedSequence.ShouldBe(0);

        document.SelectedSequence = 1;
        document.RenameTrack(undo, 0, 0, "Torso");
        document.SelectedSequence.ShouldBe(0);
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

        document.DeleteSequences(undo, new[] { deleted });
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

        document.DeleteSequences(undo, new[] { 0 });
        document.SelectedSequence.ShouldBe(-1);
        undo.Undo();
        document.SelectedSequence.ShouldBe(0);
    }

    // Three sequences; B (index 1) has a Body track holding a Walk chain, so every timeline edit has something to hit.
    private static (SequenceDocument Document, UndoManager Undo) OpenWithTimeline(TestHelpers.TempDir dir)
    {
        var save = new SequenceListSave { Sources = { "hero.achj" } };
        save.Sequences.Add(new SequenceSave { Name = "A" });
        var sequence = new SequenceSave { Name = "B" };
        sequence.Tracks.Add(new SequenceTrackSave { Name = "Body", Chains = { new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Walk" } } });
        save.Sequences.Add(sequence);
        save.Sequences.Add(new SequenceSave { Name = "C" });
        string path = Path.Combine(dir.Path, "boss.seqj");
        save.Save(path);
        return (SequenceDocument.Load(path), new UndoManager());
    }

    public static TheoryData<string> EditsOfSequenceB() => new()
    {
        "SetLoop", "PlaceChainOnTrack", "PlaceChainOnNewTrack", "DeleteChain", "DeleteTrack", "RenameTrack",
        "SetTrackOffset", "SetTrackFlip", "SetTrackVisible", "SetChainStart", "SetChainLoops", "MoveGesture", "EdgeGesture",
    };

    [Theory]
    [MemberData(nameof(EditsOfSequenceB))]
    public void EveryEditOfASequence_UndoAndRedoSelectThatSequence(string edit)
    {
        using var dir = new TestHelpers.TempDir();
        var (document, undo) = OpenWithTimeline(dir);

        switch (edit)
        {
            case "SetLoop": document.SetLoop(undo, 1, true); break;
            case "PlaceChainOnTrack": document.PlaceChain(undo, 1, 0, "hero.achj", "Walk", 2); break;
            case "PlaceChainOnNewTrack": document.PlaceChain(undo, 1, null, "hero.achj", "Walk", 2); break;
            case "DeleteChain": document.DeleteChain(undo, 1, 0, 0); break;
            case "DeleteTrack": document.DeleteTrack(undo, 1, 0); break;
            case "RenameTrack": document.RenameTrack(undo, 1, 0, "Torso"); break;
            case "SetTrackOffset": document.SetTrackOffset(undo, 1, 0, 4, 5); break;
            case "SetTrackFlip": document.SetTrackFlip(undo, 1, 0, true); break;
            case "SetTrackVisible": document.SetTrackVisible(undo, 1, 0, false); break;
            case "SetChainStart": document.SetChainStart(undo, 1, 0, 0, 1.5); break;
            case "SetChainLoops": document.SetChainLoops(undo, 1, 0, 0, 3); break;
            case "MoveGesture":
            case "EdgeGesture":
                string before = document.BeginGesture();
                var reference = document.Save.Sequences[1].Tracks[0].Chains[0];
                if (edit == "MoveGesture") reference.Start = 1; else reference.Loops = 3;
                document.CommitGesture(undo, edit, before);
                break;
        }
        undo.UndoHistory.Count.ShouldBe(1);

        document.SelectedSequence = 0;
        undo.Undo();
        document.SelectedSequence.ShouldBe(1);
        document.SelectedSequence = 2;
        undo.Redo();
        document.SelectedSequence.ShouldBe(1);
    }

    [Fact]
    public void AddSources_DoesNotTouchTheSelection()
    {
        using var dir = new TestHelpers.TempDir();
        var (document, undo) = OpenWithTimeline(dir);
        document.SelectedSequence = 2;

        document.AddSources(undo, new[] { Path.Combine(dir.Path, "other.achj") });
        undo.Undo();

        document.SelectedSequence.ShouldBe(2);
    }
}
