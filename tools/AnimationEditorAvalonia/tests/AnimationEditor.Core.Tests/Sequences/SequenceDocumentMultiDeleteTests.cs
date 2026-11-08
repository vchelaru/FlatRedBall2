using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Deleting several chains or tracks of the timeline at once: one undo entry, in any selection order.</summary>
public class SequenceDocumentMultiDeleteTests
{
    private static SequenceDocument Open(TestHelpers.TempDir dir, params SequenceTrackSave[] tracks)
    {
        var sequence = new SequenceSave { Name = "Idle" };
        sequence.Tracks.AddRange(tracks);
        var save = new SequenceListSave();
        save.Sequences.Add(sequence);
        string path = Path.Combine(dir.Path, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    private static SequenceTrackSave Track(string name, params string[] chains)
    {
        var track = new SequenceTrackSave { Name = name };
        for (int i = 0; i < chains.Length; i++)
            track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = chains[i], Start = i });
        return track;
    }

    [Fact]
    public void DeleteChains_RemovesEveryGivenChainAcrossTracks_AsOneUndoEntry()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, Track("Body", "A", "B", "C", "D"), Track("Head", "E", "F"));
        var undo = new UndoManager();

        document.DeleteChains(undo, 0, new[] { new TimelineChainId(0, 3), new TimelineChainId(0, 0), new TimelineChainId(1, 1) });

        document.Save.Sequences[0].Tracks.Select(t => string.Concat(t.Chains.Select(c => c.Chain))).ShouldBe(new[] { "BC", "E" });
        undo.UndoHistory.Select(c => c.Description).ShouldBe(new[] { "Remove 3 Chains" });
        undo.Undo();
        document.Save.Sequences[0].Tracks.Select(t => string.Concat(t.Chains.Select(c => c.Chain))).ShouldBe(new[] { "ABCD", "EF" });
        undo.Redo();
        document.Save.Sequences[0].Tracks[0].Chains.Count.ShouldBe(2);
    }

    [Fact]
    public void DeleteChains_OneChain_KeepsTheSingleChainLabel()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, Track("Body", "A", "B"));
        var undo = new UndoManager();

        document.DeleteChains(undo, 0, new[] { new TimelineChainId(0, 1) });

        undo.UndoHistory.Select(c => c.Description).ShouldBe(new[] { "Remove 'B' from 'Body'" });
    }

    [Fact]
    public void DeleteTracks_RemovesEveryGivenTrackWithItsChains_AsOneUndoEntry()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, Track("Body", "A"), Track("Head", "B"), Track("Tail", "C"));
        var undo = new UndoManager();

        document.DeleteTracks(undo, 0, new[] { 2, 0 });

        document.Save.Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "Head" });
        undo.UndoHistory.Select(c => c.Description).ShouldBe(new[] { "Delete 2 Tracks" });
        undo.Undo();
        document.Save.Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "Body", "Head", "Tail" });
    }
}
