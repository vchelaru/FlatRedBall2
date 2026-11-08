using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Duplicating timeline chains in place (after the selection) and tracks (directly in front of the topmost one).</summary>
public class SequenceDocumentTimelineDuplicateTests
{
    // No source file exists, so every chain is missing, and a missing chain is drawn one second long whatever its loops.
    private static SequenceChainReferenceSave Chain(string name, double start, float loops = 1) =>
        new() { Source = "gone.achx", Chain = name, Start = start, Loops = loops };

    private static SequenceDocument Open(TestHelpers.TempDir dir, params SequenceTrackSave[] tracks)
    {
        var sequence = new SequenceSave { Name = "Idle" };
        sequence.Tracks.AddRange(tracks);
        var save = new SequenceListSave { Sources = { "gone.achx" } };
        save.Sequences.Add(sequence);
        string path = Path.Combine(dir.Path, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    private static SequenceTrackSave Track(string name, params SequenceChainReferenceSave[] chains)
    {
        var track = new SequenceTrackSave { Name = name, FlipHorizontal = true, Visible = false };
        track.UnknownProperties["tag"] = "boss";
        track.Chains.AddRange(chains);
        return track;
    }

    private static (string Chain, double Start)[] Chains(SequenceDocument document, int track) =>
        document.Save.Sequences[0].Tracks[track].Chains.Select(c => (c.Chain, c.Start)).ToArray();

    [Fact]
    public void DuplicateChains_PlacesTheCopiesOnTheSameTrack_TheEarliestRightAfterTheLatestEndOfTheSelection()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, Track("Body", Chain("Walk", 0), Chain("Run", 1.5), Chain("Jump", 9)));
        var undo = new UndoManager();

        var result = document.DuplicateChains(undo, 0, new[] { new TimelineChainId(0, 1), new TimelineChainId(0, 0) });

        // Each missing chain is drawn 1 s: the selection spans 0 to 2.5, so the copies start at 2.5 and 4.0.
        Chains(document, 0).ShouldBe(new[] { ("Walk", 0.0), ("Run", 1.5), ("Jump", 9.0), ("Walk", 2.5), ("Run", 4.0) });
        result.Chains.ShouldBe(new[] { new TimelineChainId(0, 3), new TimelineChainId(0, 4) });
        undo.UndoHistory.Select(c => c.Description).ShouldBe(new[] { "Duplicate 2 Chains" });
        undo.Undo();
        Chains(document, 0).Length.ShouldBe(3);
    }

    [Fact]
    public void DuplicateChains_AcrossTracks_KeepsEachCopyOnItsOwnTrack_WithTheSameShift()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, Track("Body", Chain("Walk", 0)), Track("Head", Chain("Hat", 0.5, loops: 3)));

        var result = document.DuplicateChains(new UndoManager(), 0, new[] { new TimelineChainId(0, 0), new TimelineChainId(1, 0) });

        // Latest end 1.5 (Hat starts at 0.5, 1 s long); earliest start 0, so both shift by 1.5.
        Chains(document, 0).ShouldBe(new[] { ("Walk", 0.0), ("Walk", 1.5) });
        Chains(document, 1).ShouldBe(new[] { ("Hat", 0.5), ("Hat", 2.0) });
        document.Save.Sequences[0].Tracks[1].Chains[1].Loops.ShouldBe(3f);
        result.Chains.ShouldBe(new[] { new TimelineChainId(0, 1), new TimelineChainId(1, 1) });
    }

    [Fact]
    public void DuplicateChains_OfOneChain_KeepsAnExactCopyThatStartsWhereTheOriginalEnds_AndNamesItInTheLabel()
    {
        using var dir = new TestHelpers.TempDir();
        var original = Chain("Walk", 2);
        original.UnknownProperties["tint"] = "red";
        var document = Open(dir, Track("Body", original));
        var undo = new UndoManager();

        document.DuplicateChains(undo, 0, new[] { new TimelineChainId(0, 0) });

        Chains(document, 0).ShouldBe(new[] { ("Walk", 2.0), ("Walk", 3.0) });
        document.Save.Sequences[0].Tracks[0].Chains[1].UnknownProperties["tint"]!.GetValue<string>().ShouldBe("red");
        undo.UndoHistory.Select(c => c.Description).ShouldBe(new[] { "Duplicate 'Walk'" });
    }

    [Fact]
    public void DuplicateTracks_InsertsCopiesInFrontOfTheTopmostSelectedTrack_WithFreeNames_KeepingEverything()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, Track("Body", Chain("Walk", 0.25, 2)), Track("Head"), Track("Tail"));
        var undo = new UndoManager();

        var result = document.DuplicateTracks(undo, 0, new[] { 1, 0 });

        document.Save.Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "Body 2", "Head 2", "Body", "Head", "Tail" });
        var copy = document.Save.Sequences[0].Tracks[0];
        (copy.FlipHorizontal, copy.Visible).ShouldBe((true, false));
        copy.UnknownProperties["tag"]!.GetValue<string>().ShouldBe("boss");
        copy.Chains.Select(c => (c.Chain, c.Start, c.Loops, c.Source)).ShouldBe(new[] { ("Walk", 0.25, 2f, "gone.achx") });
        result.Tracks.ShouldBe(new[] { 0, 1 });
        undo.UndoHistory.Select(c => c.Description).ShouldBe(new[] { "Duplicate 2 Tracks" });
        undo.Undo();
        document.Save.Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "Body", "Head", "Tail" });
    }

    [Fact]
    public void DuplicateTracks_OneTrack_LandsRightInFrontOfIt()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, Track("Body"), Track("Head"));
        var undo = new UndoManager();

        document.DuplicateTracks(undo, 0, new[] { 0 });

        document.Save.Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "Body 2", "Body", "Head" });
        undo.UndoHistory.Select(c => c.Description).ShouldBe(new[] { "Duplicate Track 'Body'" });
    }

    [Fact]
    public void DuplicateTracks_OfATrackInTheMiddle_PutsTheCopyOneLaneAboveIt_LeavesTheOthersAlone_AndSelectsTheCopy()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, Track("A"), Track("B", Chain("Walk", 0)), Track("C"));
        var undo = new UndoManager();

        var result = document.DuplicateTracks(undo, 0, new[] { 1 });

        document.Save.Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "A", "B 2", "B", "C" });
        Chains(document, 1).ShouldBe(new[] { ("Walk", 0.0) });
        Chains(document, 2).ShouldBe(new[] { ("Walk", 0.0) });
        result.Tracks.ShouldBe(new[] { 1 });
    }

    [Fact]
    public void DuplicateTracks_OfTwoNonAdjacentTracks_PutsBothCopiesAboveTheTopmostOne_InTheirOriginalOrder()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, Track("A"), Track("B"), Track("C"), Track("D"));

        var result = document.DuplicateTracks(new UndoManager(), 0, new[] { 3, 1 });

        document.Save.Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "A", "B 2", "D 2", "B", "C", "D" });
        result.Tracks.ShouldBe(new[] { 1, 2 });
    }

    [Fact]
    public void DuplicateTracks_Undo_RestoresTheTracks_AndRedoPutsTheCopiesBack()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, Track("A"), Track("B"), Track("C"));
        var undo = new UndoManager();
        document.DuplicateTracks(undo, 0, new[] { 1 });

        undo.Undo();

        document.Save.Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "A", "B", "C" });
        undo.Redo();
        document.Save.Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "A", "B 2", "B", "C" });
    }
}
