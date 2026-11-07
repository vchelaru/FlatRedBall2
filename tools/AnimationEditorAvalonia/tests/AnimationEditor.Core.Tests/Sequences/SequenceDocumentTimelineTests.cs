using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>The timeline's edits on a <see cref="SequenceDocument"/>: placing, moving, resizing and removing chains and tracks.</summary>
public class SequenceDocumentTimelineTests
{
    // hero.achj holds Walk: two 0.25 s frames, so one cycle is 0.5 s.
    private static SequenceDocument Load(TestHelpers.TempDir dir, params string[] trackNames)
    {
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        list.AnimationChains.Add(new AnimationChainSave
        {
            Name = "Walk",
            Frames = { new AnimationFrameSave { FrameLength = 0.25f }, new AnimationFrameSave { FrameLength = 0.25f } },
        });
        list.SaveJson(Path.Combine(dir.Path, "hero.achj"));

        var save = new SequenceListSave { Sources = { "hero.achj" } };
        var sequence = new SequenceSave { Name = "Attack" };
        sequence.Tracks.AddRange(trackNames.Select(name => new SequenceTrackSave { Name = name }));
        save.Sequences.Add(sequence);
        string path = Path.Combine(dir.Path, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    [Fact]
    public void CycleLength_KnownChainIsItsLength_MissingChainIsNull()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);

        document.CycleLength(new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Walk" })!.Value.ShouldBe(0.5, 1e-6);
        document.CycleLength(new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Gone" }).ShouldBeNull();
        document.CycleLength(new SequenceChainReferenceSave { Source = "gone.achj", Chain = "Walk" }).ShouldBeNull();
    }

    [Fact]
    public void PlaceChain_OnATrackAndOnTheNewTrackRow_EachIsOneUndoStepAndSaves()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, "Body");
        var undo = new UndoManager();

        document.PlaceChain(undo, 0, track: 0, "hero.achj", "Walk", start: 1.5);
        document.PlaceChain(undo, 0, track: null, "hero.achj", "Walk", start: 0.5);

        var tracks = SequenceListSave.FromFile(document.Path.FullPath).Sequences[0].Tracks;
        tracks.Select(t => t.Name).ShouldBe(new[] { "Body", "Walk" });
        tracks[0].Chains.Single().Start.ShouldBe(1.5);
        tracks[1].Chains.Single().Start.ShouldBe(0.5);
        undo.UndoHistory.Select(c => c.Description).ShouldBe(new[] { "Add 'Walk' to 'Body'", "Add Track 'Walk'" });
        undo.Undo();
        document.Save.Sequences[0].Tracks.Count.ShouldBe(1);
    }

    [Fact]
    public void Gesture_LiveEditsCommitAsOneUndoStep_AndAnUnchangedGestureRecordsNothing()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, "Body");
        var undo = new UndoManager();
        document.PlaceChain(undo, 0, 0, "hero.achj", "Walk", 0);

        string before = document.BeginGesture();
        var reference = document.Save.Sequences[0].Tracks[0].Chains[0];
        // A drag writes the live model on every move.
        reference.Start = 0.5;
        reference.Start = 1;
        document.CommitGesture(undo, "Move 'Walk'", before);
        document.CommitGesture(undo, "Move 'Walk'", document.BeginGesture());

        undo.UndoHistory.Select(c => c.Description).ShouldBe(new[] { "Add 'Walk' to 'Body'", "Move 'Walk'" });
        SequenceListSave.FromFile(document.Path.FullPath).Sequences[0].Tracks[0].Chains[0].Start.ShouldBe(1);
        undo.Undo();
        document.Save.Sequences[0].Tracks[0].Chains[0].Start.ShouldBe(0);
    }

    [Fact]
    public void CancelGesture_RestoresTheModelWithoutAnUndoStep()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, "Body");
        var undo = new UndoManager();
        document.PlaceChain(undo, 0, 0, "hero.achj", "Walk", 0);
        string before = document.BeginGesture();
        document.Save.Sequences[0].Tracks[0].Chains[0].Loops = 3;

        document.CancelGesture(before);

        document.Save.Sequences[0].Tracks[0].Chains[0].Loops.ShouldBe(1);
        undo.UndoHistory.Count.ShouldBe(1);
    }

    [Fact]
    public void DeleteChainDeleteTrackAndRenameTrack_AreUndoable_AndADuplicateTrackNameIsRefused()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, "Body", "Arms");
        var undo = new UndoManager();
        document.PlaceChain(undo, 0, 0, "hero.achj", "Walk", 0);

        document.DeleteChain(undo, 0, track: 0, chain: 0);
        document.RenameTrack(undo, 0, 1, "Body").ShouldBe("A track named \"Body\" already exists.");
        document.RenameTrack(undo, 0, 1, " Legs ").ShouldBeNull();
        document.DeleteTrack(undo, 0, 0);

        document.Save.Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "Legs" });
        undo.UndoHistory.Select(c => c.Description).ShouldBe(new[]
            { "Add 'Walk' to 'Body'", "Remove 'Walk' from 'Body'", "Rename Track 'Arms' to 'Legs'", "Delete Track 'Body'" });
    }
}
