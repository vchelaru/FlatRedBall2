using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>
/// Dragging a source group in the library rewrites <see cref="SequenceListSave.Sources"/> only. One undo entry; undo and
/// redo put the order back; chains refer to a source by its path string, so no sequence changes.
/// </summary>
public class SequenceDocumentMoveSourcesTests
{
    private static SequenceDocument Open(TestHelpers.TempDir dir, params string[] sources)
    {
        var save = new SequenceListSave();
        save.Sources.AddRange(sources);
        var sequence = new SequenceSave { Name = "Attack" };
        var track = new SequenceTrackSave { Name = "T" };
        foreach (string source in sources)
            track.Chains.Add(new SequenceChainReferenceSave { Source = source, Chain = "Walk", Start = 0, Loops = 1 });
        sequence.Tracks.Add(track);
        save.Sequences.Add(sequence);
        save.Sequences.Add(new SequenceSave { Name = "Idle" });
        string path = Path.Combine(dir.Path, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    private static string[] SavedSources(SequenceDocument document) =>
        SequenceListSave.FromFile(document.Path.FullPath).Sources.ToArray();

    private static string[] Labels(UndoManager undo) => undo.UndoHistory.Select(c => c.Description).ToArray();

    [Fact]
    public void MoveSources_OneSource_ReordersTheFileAndSaves_AsOneNamedUndoEntry()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "a.achx", "b.achx", "c.achx", "d.achx");
        var undo = new UndoManager();

        bool moved = document.MoveSources(undo, new[] { 0 }, 3);

        moved.ShouldBeTrue();
        document.Save.Sources.ShouldBe(new[] { "b.achx", "c.achx", "a.achx", "d.achx" });
        SavedSources(document).ShouldBe(new[] { "b.achx", "c.achx", "a.achx", "d.achx" });
        Labels(undo).ShouldBe(new[] { "Move Source 'a.achx'" });
    }

    [Fact]
    public void MoveSources_TheLabelUsesTheFileNameNotTheFolders()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "chars/hero.achx", "b.achx");
        var undo = new UndoManager();

        document.MoveSources(undo, new[] { 0 }, 2);

        Labels(undo).ShouldBe(new[] { "Move Source 'hero.achx'" });
    }

    [Fact]
    public void MoveSources_ToTheTop_PutsTheSourceFirst()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "a.achx", "b.achx", "c.achx");
        var undo = new UndoManager();

        document.MoveSources(undo, new[] { 2 }, 0);

        document.Save.Sources.ShouldBe(new[] { "c.achx", "a.achx", "b.achx" });
    }

    [Fact]
    public void MoveSources_Undo_RestoresTheOrder_RedoMovesAgain_AndBothSave()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "a.achx", "b.achx", "c.achx");
        var undo = new UndoManager();
        document.MoveSources(undo, new[] { 0 }, 3);

        undo.Undo();

        document.Save.Sources.ShouldBe(new[] { "a.achx", "b.achx", "c.achx" });
        SavedSources(document).ShouldBe(new[] { "a.achx", "b.achx", "c.achx" });

        undo.Redo();

        document.Save.Sources.ShouldBe(new[] { "b.achx", "c.achx", "a.achx" });
        SavedSources(document).ShouldBe(new[] { "b.achx", "c.achx", "a.achx" });
    }

    [Fact]
    public void MoveSources_ChangesNothingButTheSourceOrder_ChainsKeepTheirSourcePathAndSelectionStays()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "a.achx", "b.achx", "c.achx");
        var undo = new UndoManager();
        document.SelectedSequence = 1;
        string sequencesBefore = System.Text.Json.JsonSerializer.Serialize(
            document.Save.Sequences.Select(s => (s.Name, Chains: s.Tracks.SelectMany(t => t.Chains).Select(c => c.Source).ToArray())));

        document.MoveSources(undo, new[] { 0 }, 3);

        System.Text.Json.JsonSerializer.Serialize(
            document.Save.Sequences.Select(s => (s.Name, Chains: s.Tracks.SelectMany(t => t.Chains).Select(c => c.Source).ToArray())))
            .ShouldBe(sequencesBefore);
        document.SelectedSequence.ShouldBe(1);
        document.Save.Sequences.Select(s => s.Name).ShouldBe(new[] { "Attack", "Idle" });
    }

    [Fact]
    public void MoveSources_DropOnItsOwnGapOrNextToItself_IsANoOp_NoUndoEntry()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "a.achx", "b.achx", "c.achx");
        var undo = new UndoManager();

        document.MoveSources(undo, new[] { 1 }, 1).ShouldBeFalse();
        document.MoveSources(undo, new[] { 1 }, 2).ShouldBeFalse();
        document.MoveSources(undo, new[] { 5 }, 0).ShouldBeFalse();

        Labels(undo).ShouldBeEmpty();
        document.Save.Sources.ShouldBe(new[] { "a.achx", "b.achx", "c.achx" });
    }

    [Fact]
    public void MoveSources_GoesThroughTheSaveRunner_SoTheLiveReloadCanIgnoreItsOwnWrite()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, "a.achx", "b.achx");
        var undo = new UndoManager();
        var runs = new List<string>();
        document.SaveRunner = (path, write) =>
        {
            runs.Add(path);
            write();
        };

        document.MoveSources(undo, new[] { 0 }, 2);

        runs.ShouldBe(new[] { document.Path.FullPath });
    }
}
