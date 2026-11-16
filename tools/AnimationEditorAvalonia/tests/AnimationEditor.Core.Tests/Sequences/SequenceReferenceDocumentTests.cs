using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Reference tracks (a track that plays another sequence of the file) on a <see cref="SequenceDocument"/>: editing and compositing.</summary>
public class SequenceReferenceDocumentTests
{
    // Idle: Body (Walk, 0.5 s) + Earth reference. Earth: Ring (Spark, 1 s, looping). Action: Hit (Walk).
    private static SequenceDocument Load(TestHelpers.TempDir dir, bool earthLoops = true)
    {
        WriteSource(Path.Combine(dir.Path, "body.achj"), "Walk", "body.png", 0.25f, 0.25f);
        WriteSource(Path.Combine(dir.Path, "fx.achj"), "Spark", "spark.png", 1f);
        var save = new SequenceListSave { Sources = { "body.achj", "fx.achj" } };
        save.Sequences.Add(Sequence("Idle", true, ChainTrack("Body", "body.achj", "Walk")));
        save.Sequences.Add(Sequence("Earth", earthLoops, ChainTrack("Ring", "fx.achj", "Spark")));
        save.Sequences.Add(Sequence("Action", true, ChainTrack("Hit", "body.achj", "Walk")));
        string path = Path.Combine(dir.Path, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    private static void WriteSource(string path, string chain, string texture, params float[] lengths)
    {
        var list = new AnimationChainListSave();
        var save = new AnimationChainSave { Name = chain };
        foreach (float length in lengths)
            save.Frames.Add(new AnimationFrameSave { TextureName = texture, FrameLength = length, RightCoordinate = 1, BottomCoordinate = 1 });
        list.AnimationChains.Add(save);
        list.SaveJson(path);
    }

    private static SequenceSave Sequence(string name, bool loop, params SequenceTrackSave[] tracks)
    {
        var sequence = new SequenceSave { Name = name, Loop = loop };
        sequence.Tracks.AddRange(tracks);
        return sequence;
    }

    private static SequenceTrackSave ChainTrack(string name, string source, string chain) =>
        new() { Name = name, Chains = { new SequenceChainReferenceSave { Source = source, Chain = chain } } };

    [Fact]
    public void AddSequenceReference_PutsANewFrontTrackAtTheStart_AsOneUndoStep()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();

        document.AddSequenceReference(undo, 0, "Earth", start: 0.25).ShouldBeNull();

        var tracks = SequenceListSave.FromFile(document.Path.FullPath).Sequences[0].Tracks;
        tracks.Select(t => t.Name).ShouldBe(new[] { "Earth", "Body" });
        tracks[0].Reference!.Sequence.ShouldBe("Earth");
        tracks[0].Reference!.Start.ShouldBe(0.25);
        tracks[0].Reference!.Loops.ShouldBe(1f);
        undo.UndoHistory.Select(c => c.Description).ShouldBe(new[] { "Add Reference 'Earth'" });
        undo.Undo();
        document.Save.Sequences[0].Tracks.Count.ShouldBe(1);
    }

    [Fact]
    public void AddSequenceReference_ToItselfOrBackIntoACycle_IsRefusedAndChangesNothing()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        document.AddSequenceReference(undo, 0, "Earth", 0).ShouldBeNull();

        document.AddSequenceReference(undo, 0, "Idle", 0).ShouldNotBeNull();
        document.AddSequenceReference(undo, 1, "Idle", 0).ShouldNotBeNull();

        document.Save.Sequences[1].Tracks.Count.ShouldBe(1);
        undo.UndoHistory.Count().ShouldBe(1);
    }

    [Fact]
    public void ReferenceCandidates_LeavesOutTheSequenceItselfAndAnyThatWouldCycle()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        document.AddSequenceReference(new UndoManager(), 0, "Earth", 0);

        document.ReferenceCandidates(0).ShouldBe(new[] { "Earth", "Action" });
        document.ReferenceCandidates(1).ShouldBe(new[] { "Action" }, "Idle plays Earth, so Earth may not play Idle");
    }

    [Fact]
    public void RenameSequence_PointsReferencesAtTheNewName()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        document.AddSequenceReference(undo, 0, "Earth", 0);

        document.RenameSequence(undo, 1, "Ground").ShouldBeNull();

        document.Save.Sequences[0].Tracks[0].Reference!.Sequence.ShouldBe("Ground");
        undo.Undo();
        document.Save.Sequences[0].Tracks[0].Reference!.Sequence.ShouldBe("Earth");
    }

    [Fact]
    public void SetReferenceStartAndLoops_EditTheClip_AndRefuseBadValues()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        document.AddSequenceReference(undo, 0, "Earth", 0);

        document.SetReferenceStart(undo, 0, 0, 1.5).ShouldBeNull();
        document.SetReferenceLoops(undo, 0, 0, 2.5f).ShouldBeNull();
        document.SetReferenceStart(undo, 0, 0, -1).ShouldNotBeNull();
        document.SetReferenceLoops(undo, 0, 0, 0).ShouldNotBeNull();

        var reference = document.Save.Sequences[0].Tracks[0].Reference!;
        reference.Start.ShouldBe(1.5);
        reference.Loops.ShouldBe(2.5f);
    }

    [Fact]
    public void PasteTracks_ReferenceThatWouldCycle_IsRefusedWithAMessage()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        document.AddSequenceReference(undo, 0, "Earth", 0);
        string text = document.CopyTracks(0, new[] { 0 });
        TimelineClipboard.TryRead(text, out var content, out _).ShouldBeTrue();

        // Earth would play itself through the pasted track.
        var result = document.PasteTracks(undo, 1, content!, selectedTrack: null);

        result.Errors.ShouldNotBeEmpty();
        document.Save.Sequences[1].Tracks.Count.ShouldBe(1);
    }

    [Fact]
    public void ReferenceTrack_CopyAndPasteIntoAnotherSequence_KeepsTheReference()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        document.AddSequenceReference(undo, 0, "Earth", 0.5);
        TimelineClipboard.TryRead(document.CopyTracks(0, new[] { 0 }), out var content, out _).ShouldBeTrue();

        document.PasteTracks(undo, 2, content!, selectedTrack: null).Errors.ShouldBeEmpty();

        document.Save.Sequences[2].Tracks[0].Reference!.Sequence.ShouldBe("Earth");
        document.Save.Sequences[2].Tracks[0].Reference!.Start.ShouldBe(0.5);
    }

    [Fact]
    public void SequenceLength_ReferenceTrack_ExtendsTheHoldingSequenceToTheClipEnd()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);

        document.AddSequenceReference(new UndoManager(), 0, "Earth", 0);
        document.SetReferenceLoops(new UndoManager(), 0, 0, 9);

        document.SequenceLength(0).ShouldBe(9, 1e-6);   // 9 cycles of Earth's 1 s, past Body's 0.5 s
    }

    [Fact]
    public void At_SequenceOfOnlyReferences_PlaysTheInnermostChain()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        string name = document.AddSequence(undo);                         // holds nothing but a reference to Earth
        int middle = document.Save.Sequences.FindIndex(s => s.Name == name);
        document.Save.Sequences[middle].Tracks.Clear();
        document.AddSequenceReference(undo, middle, "Earth", 0);
        document.AddSequenceReference(undo, 0, name, 0);

        document.SequenceLength(middle).ShouldBe(1, 1e-6);
        SequenceScene.At(document, 0, 0.1).Select(s => s.Chain.Name).ShouldContain("Spark");
    }

    [Fact]
    public void At_ReferenceTrack_ShowsTheTargetsSpritesInTheTracksPlace()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        document.AddSequenceReference(new UndoManager(), 0, "Earth", 0);

        var sprites = SequenceScene.At(document, 0, 0.1);

        // Body (lane 1) is behind, the reference (lane 0, in front) draws Earth's Ring.
        sprites.Select(s => s.Chain.Name).ShouldBe(new[] { "Walk", "Spark" });
        sprites.Select(s => s.Track).ShouldBe(new[] { 1, 0 });
        sprites[1].Sequence.ShouldBe(0);
    }

    [Fact]
    public void At_ReferenceTrack_RestartsWithTheParent_AndLoopsTheTargetAcrossItsClip()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        document.AddSequenceReference(new UndoManager(), 0, "Earth", 0.2);
        document.SetReferenceLoops(new UndoManager(), 0, 0, 2);   // Earth is 1 s, so the clip runs 0.2 .. 2.2

        SequenceScene.At(document, 0, 0.1).Any(s => s.Chain.Name == "Spark").ShouldBeFalse("before the clip starts");
        SequenceScene.At(document, 0, 0.3).Any(s => s.Chain.Name == "Spark").ShouldBeTrue();
        SequenceScene.At(document, 0, 2.3).Any(s => s.Chain.Name == "Spark").ShouldBeFalse("after two cycles");
    }

    [Fact]
    public void At_HiddenReferenceTrack_ShowsNothingOfTheTarget()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        document.AddSequenceReference(undo, 0, "Earth", 0);
        document.SetTrackVisible(undo, 0, 0, false);

        SequenceScene.At(document, 0, 0.1).Select(s => s.Chain.Name).ShouldBe(new[] { "Walk" });
    }

    [Fact]
    public void At_SeveralSequencesSelected_EachCompositesItsOwnReferences()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        document.AddSequenceReference(undo, 0, "Earth", 0);
        document.AddSequenceReference(undo, 2, "Earth", 0);

        var sprites = SequenceScene.At(document, new[] { new SequenceTime(0, 0.1), new SequenceTime(2, 0.1) });

        sprites.Select(s => (s.Sequence, s.Chain.Name)).ShouldBe(new[] { (0, "Walk"), (0, "Spark"), (2, "Walk"), (2, "Spark") });
    }

    [Fact]
    public void At_NonLoopingTarget_HoldsItsFinalPoseUntilTheClipEnds()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, earthLoops: false);
        var undo = new UndoManager();
        document.AddSequenceReference(undo, 0, "Earth", 0);
        document.SetReferenceLoops(undo, 0, 0, 2);

        SequenceScene.At(document, 0, 1.5).Any(s => s.Chain.Name == "Spark").ShouldBeTrue("held after its one cycle");
        SequenceScene.At(document, 0, 2.5).Any(s => s.Chain.Name == "Spark").ShouldBeFalse();
    }
}
