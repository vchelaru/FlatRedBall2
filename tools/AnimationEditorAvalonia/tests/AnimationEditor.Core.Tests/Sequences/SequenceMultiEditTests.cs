using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>
/// Delete, duplicate, copy, cut and paste act on every selected sequence, as the <c>.achx</c> tab acts on every
/// selected chain: one undo entry for the whole set, undo restores the positions and the selection, and the pasted or
/// duplicated set ends up selected.
/// </summary>
public class SequenceMultiEditTests
{
    private static SequenceSave Sequence(string name)
    {
        var track = new SequenceTrackSave { Name = "Body" };
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Loops = 1 });
        var sequence = new SequenceSave { Name = name };
        sequence.Tracks.Add(track);
        return sequence;
    }

    private static SequenceDocument Open(string folder, string fileName, params string[] names)
    {
        Directory.CreateDirectory(folder);
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        save.Sequences.AddRange(names.Select(Sequence));
        string path = Path.Combine(folder, fileName);
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    private static SequenceDocument Open(TestHelpers.TempDir dir, params string[] names) => Open(dir.Path, "boss.seqj", names);

    /// <summary>Selects the named sequences in that order, as the list does (the first is the primary).</summary>
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
    public void DeleteSequences_RemovesEverySelectedOne_AsOneUndoEntry_AndSelectsTheNeighbourTakingTheFirstOnesPlace()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "A", "B", "C", "D", "E");
        var undo = new UndoManager();
        Select(document, "D", "B");

        document.DeleteSequences(undo, document.SelectedIndices);

        Names(document).ShouldBe(new[] { "A", "C", "E" });
        SavedNames(document).ShouldBe(new[] { "A", "C", "E" });
        Labels(undo).ShouldBe(new[] { "Delete 2 Sequences" });
        document.SelectedSequence.ShouldBe(1);
        document.Selection.Names.ShouldBe(new[] { "C" });
        document.Selection.Primary.ShouldBe("C");
    }

    [Fact]
    public void DeleteSequences_Undo_PutsEverySequenceBackAtItsPositionWithTheSelection_RedoDeletesThemAgain()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "A", "B", "C", "D", "E");
        var undo = new UndoManager();
        Select(document, "D", "B");
        document.DeleteSequences(undo, document.SelectedIndices);

        undo.Undo();

        Names(document).ShouldBe(new[] { "A", "B", "C", "D", "E" });
        SavedNames(document).ShouldBe(new[] { "A", "B", "C", "D", "E" });
        document.Selection.Names.ShouldBe(new[] { "D", "B" });
        document.Selection.Primary.ShouldBe("D");
        document.SelectedSequence.ShouldBe(3);

        undo.Redo();

        Names(document).ShouldBe(new[] { "A", "C", "E" });
        document.Selection.Names.ShouldBe(new[] { "C" });
        document.SelectedSequence.ShouldBe(1);
    }

    [Fact]
    public void DeleteSequences_OfOne_KeepsTheNamedLabel()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "A", "B", "C");
        var undo = new UndoManager();
        Select(document, "B");

        document.DeleteSequences(undo, document.SelectedIndices);

        Labels(undo).ShouldBe(new[] { "Delete Sequence 'B'" });
        document.SelectedSequence.ShouldBe(1);
        document.Selection.Names.ShouldBe(new[] { "C" });
    }

    [Fact]
    public void DeleteSequences_OfTheLastOnes_SelectsTheNewLast()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "A", "B", "C");
        Select(document, "B", "C");

        document.DeleteSequences(new UndoManager(), document.SelectedIndices);

        Names(document).ShouldBe(new[] { "A" });
        document.SelectedSequence.ShouldBe(0);
        document.Selection.Names.ShouldBe(new[] { "A" });
    }

    [Fact]
    public void DeleteSequences_OfEverySequence_LeavesNothingSelected_UndoSelectsThemAgain()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "A", "B", "C");
        var undo = new UndoManager();
        Select(document, "A", "B", "C");

        document.DeleteSequences(undo, document.SelectedIndices);

        Names(document).ShouldBeEmpty();
        Labels(undo).ShouldBe(new[] { "Delete 3 Sequences" });
        document.SelectedSequence.ShouldBe(-1);
        document.Selection.Names.ShouldBeEmpty();
        document.Selection.Primary.ShouldBeNull();

        undo.Undo();

        Names(document).ShouldBe(new[] { "A", "B", "C" });
        document.Selection.Names.ShouldBe(new[] { "A", "B", "C" });
        document.SelectedSequence.ShouldBe(0);
    }

    [Fact]
    public void DeleteSequences_ByAnIndexListWithRepeatsOrAnyOrder_RemovesEachOnce()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "A", "B", "C", "D");

        document.DeleteSequences(new UndoManager(), new[] { 2, 0, 2 });

        Names(document).ShouldBe(new[] { "B", "D" });
    }

    [Fact]
    public void DuplicateSequences_InsertsTheCopiesAsABlockAfterTheLastSelected_InDocumentOrder_AndSelectsThem()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "A", "B", "C", "D");
        var undo = new UndoManager();
        Select(document, "C", "A");

        var names = document.DuplicateSequences(undo, document.SelectedIndices);

        names.ShouldBe(new[] { "A 2", "C 2" });
        Names(document).ShouldBe(new[] { "A", "B", "C", "A 2", "C 2", "D" });
        SavedNames(document).ShouldBe(new[] { "A", "B", "C", "A 2", "C 2", "D" });
        Labels(undo).ShouldBe(new[] { "Duplicate 2 Sequences" });
        document.Selection.Names.ShouldBe(new[] { "A 2", "C 2" });
        document.Selection.Primary.ShouldBe("A 2");
        document.SelectedSequence.ShouldBe(3);
        // The copies share nothing with their originals.
        document.Save.Sequences[3].Tracks[0].Chains[0].Start = 9;
        document.Save.Sequences[0].Tracks[0].Chains[0].Start.ShouldBe(0);
    }

    [Fact]
    public void DuplicateSequences_Undo_RemovesTheCopiesAndReselectsTheOriginals_RedoBringsThemBack()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "A", "B", "C", "D");
        var undo = new UndoManager();
        Select(document, "C", "A");
        document.DuplicateSequences(undo, document.SelectedIndices);

        undo.Undo();

        Names(document).ShouldBe(new[] { "A", "B", "C", "D" });
        document.Selection.Names.ShouldBe(new[] { "C", "A" });
        document.Selection.Primary.ShouldBe("C");
        document.SelectedSequence.ShouldBe(2);

        undo.Redo();

        Names(document).ShouldBe(new[] { "A", "B", "C", "A 2", "C 2", "D" });
        document.Selection.Names.ShouldBe(new[] { "A 2", "C 2" });
    }

    [Fact]
    public void DuplicateSequences_GivesEveryCopyAFreeNameEvenWhenTheCopiesShareABase()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "Idle", "Idle 2", "Run");
        Select(document, "Idle", "Idle 2");

        var names = document.DuplicateSequences(new UndoManager(), document.SelectedIndices);

        names.ShouldBe(new[] { "Idle 3", "Idle 4" });
        Names(document).ShouldBe(new[] { "Idle", "Idle 2", "Idle 3", "Idle 4", "Run" });
    }

    [Fact]
    public void DuplicateSequences_OfOne_KeepsTheNamedLabelAndSelectsTheCopy()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "A", "B");
        var undo = new UndoManager();
        Select(document, "A");

        document.DuplicateSequences(undo, document.SelectedIndices).ShouldBe(new[] { "A 2" });

        Labels(undo).ShouldBe(new[] { "Duplicate Sequence 'A'" });
        document.SelectedSequence.ShouldBe(1);
        document.Selection.Names.ShouldBe(new[] { "A 2" });
    }

    [Fact]
    public void CopySequences_PutsEverySelectedSequenceOnTheClipboardInDocumentOrder_WithAbsoluteSources()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "A", "B", "C");
        Select(document, "C", "A");

        string text = document.CopySequences(document.SelectedIndices);

        SequenceClipboard.TryRead(text, out var content, out var error).ShouldBeTrue();
        error.ShouldBeNull();
        content!.Sequences.Select(s => s.Name).ShouldBe(new[] { "A", "C" });
        string hero = Path.Combine(dir.Path, "hero.achx").Replace('\\', '/');
        content.Sources.ShouldBe(new[] { hero });
        content.Sequences.SelectMany(s => s.Tracks).SelectMany(t => t.Chains).Select(c => c.Source).ShouldBe(new[] { hero, hero });
    }

    [Fact]
    public void PasteSequences_InTheSameFile_InsertsTheBlockAfterTheLastSelected_WithFreeNames_AsOneUndoEntry_AndSelectsThem()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "A", "B", "C", "D");
        var undo = new UndoManager();
        Select(document, "A", "B");
        SequenceClipboard.TryRead(document.CopySequences(document.SelectedIndices), out var content, out _).ShouldBeTrue();
        Select(document, "C");

        var result = document.PasteSequences(undo, content!);

        result.Names.ShouldBe(new[] { "A 2", "B 2" });
        result.Errors.ShouldBeEmpty();
        Names(document).ShouldBe(new[] { "A", "B", "C", "A 2", "B 2", "D" });
        SavedNames(document).ShouldBe(new[] { "A", "B", "C", "A 2", "B 2", "D" });
        Labels(undo).ShouldBe(new[] { "Paste 2 Sequences" });
        document.Selection.Names.ShouldBe(new[] { "A 2", "B 2" });
        document.Selection.Primary.ShouldBe("A 2");
        document.SelectedSequence.ShouldBe(3);

        undo.Undo();

        Names(document).ShouldBe(new[] { "A", "B", "C", "D" });
        document.Selection.Names.ShouldBe(new[] { "C" });
        document.SelectedSequence.ShouldBe(2);

        undo.Redo();

        Names(document).ShouldBe(new[] { "A", "B", "C", "A 2", "B 2", "D" });
        document.Selection.Names.ShouldBe(new[] { "A 2", "B 2" });
    }

    [Fact]
    public void PasteSequences_WithSeveralSelected_GoesAfterTheLastOfThemInTheList()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "A", "B", "C", "D");
        Select(document, "D");
        SequenceClipboard.TryRead(document.CopySequences(document.SelectedIndices), out var content, out _).ShouldBeTrue();
        Select(document, "B", "A");

        document.PasteSequences(new UndoManager(), content!);

        Names(document).ShouldBe(new[] { "A", "B", "D 2", "C", "D" });
    }

    [Fact]
    public void PasteSequences_WithNothingSelected_GoesAtTheEnd()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "A", "B", "C");
        Select(document, "A", "B");
        SequenceClipboard.TryRead(document.CopySequences(document.SelectedIndices), out var content, out _).ShouldBeTrue();
        document.Selection.Apply(new string[0]);
        document.SelectedSequence = -1;

        document.PasteSequences(new UndoManager(), content!);

        Names(document).ShouldBe(new[] { "A", "B", "C", "A 2", "B 2" });
    }

    [Fact]
    public void PasteSequences_IntoAnotherFile_RewritesSourcesRelativeToIt_ListsEachOnce_AndUndoesAsOneEntry()
    {
        using var dir = new TestHelpers.TempDir();
        var a = Open(Path.Combine(dir.Path, "a"), "boss.seqj", "Idle", "Run");
        var b = Open(Path.Combine(dir.Path, "b"), "other.seqj", "Walk");
        b.Save.Sources.Clear();
        b.Save.Sequences[0].Tracks[0].Chains.Clear();
        Select(a, "Idle", "Run");
        SequenceClipboard.TryRead(a.CopySequences(a.SelectedIndices), out var content, out _).ShouldBeTrue();
        var undo = new UndoManager();

        var result = b.PasteSequences(undo, content!);

        result.Names.ShouldBe(new[] { "Idle", "Run" });
        Names(b).ShouldBe(new[] { "Walk", "Idle", "Run" });
        b.Save.Sources.ShouldBe(new[] { "../a/hero.achx" });
        b.Save.Sequences.Skip(1).SelectMany(s => s.Tracks).SelectMany(t => t.Chains).Select(c => c.Source)
            .ShouldBe(new[] { "../a/hero.achx", "../a/hero.achx" });
        Labels(undo).ShouldBe(new[] { "Paste 2 Sequences" });
        b.Selection.Names.ShouldBe(new[] { "Idle", "Run" });

        undo.Undo();

        Names(b).ShouldBe(new[] { "Walk" });
        b.Save.Sources.ShouldBeEmpty();
    }

    [Fact]
    public void PasteSequences_OfSeveralWithTheSameName_NamesThemApart()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "A");
        var content = new SequenceClipboardContent(new[] { Sequence("X"), Sequence("X"), Sequence("A") }, new string[0]);
        Select(document, "A");

        var result = document.PasteSequences(new UndoManager(), content);

        result.Names.ShouldBe(new[] { "X", "X 2", "A 2" });
        Names(document).ShouldBe(new[] { "A", "X", "X 2", "A 2" });
    }

    [Fact]
    public void PasteSequences_OfOne_KeepsTheNamedLabel()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "A", "B");
        var undo = new UndoManager();
        Select(document, "A");
        SequenceClipboard.TryRead(document.CopySequences(document.SelectedIndices), out var content, out _).ShouldBeTrue();

        document.PasteSequences(undo, content!).Names.ShouldBe(new[] { "A 2" });

        Labels(undo).ShouldBe(new[] { "Paste Sequence 'A 2'" });
    }

    [Fact]
    public void SelectedIndices_AreTheSelectionsPositionsInSelectionOrder_OrJustTheSelectedSequence()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "A", "B", "C");

        document.SelectedSequence = 1;
        document.SelectedIndices.ShouldBe(new[] { 1 });

        Select(document, "C", "A");
        document.SelectedIndices.ShouldBe(new[] { 2, 0 });

        document.Selection.Apply(new string[0]);
        document.SelectedSequence = -1;
        document.SelectedIndices.ShouldBeEmpty();
    }
}
