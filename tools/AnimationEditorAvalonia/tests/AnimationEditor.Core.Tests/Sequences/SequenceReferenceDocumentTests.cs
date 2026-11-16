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

    private static SequenceTrackSave RefTrack(string name, string target, double start = 0, float loops = 1) =>
        new() { Name = name, Reference = new SequenceReferenceSave { Sequence = target, Start = start, Loops = loops } };

    private static bool Shows(SequenceDocument document, int sequence, double time, string chain) =>
        SequenceScene.At(document, sequence, time).Any(s => s.Chain.Name == chain);

    private static TimelineReference Clip(SequenceDocument document, int sequence) =>
        TimelineReferences.For(document.Save.Sequences[sequence], document.Save.Sequences, document.CycleLength).Single();

    [Fact]
    public void PasteSequences_HolderIntoTheSameFile_KeepsItsReference()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        document.AddSequenceReference(undo, 0, "Earth", 0.5);
        SequenceClipboard.TryRead(document.CopySequences(new[] { 0 }), out var content, out _).ShouldBeTrue();

        var names = document.PasteSequences(undo, content!).Names;

        int copy = document.Save.Sequences.FindIndex(s => s.Name == names[0]);
        document.Save.Sequences[copy].Tracks[0].Reference!.Sequence.ShouldBe("Earth");
        document.Save.Sequences[copy].Tracks[0].Reference!.Start.ShouldBe(0.5);
        Shows(document, copy, 0.6, "Spark").ShouldBeTrue();
    }

    [Fact]
    public void DeleteSequences_Target_LeavesTheReferenceMissing_AndUndoBringsItBack()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        document.AddSequenceReference(undo, 0, "Earth", 0);
        document.SetReferenceLoops(undo, 0, 0, 3);

        document.DeleteSequences(undo, new[] { 1 });

        document.Save.Sequences[0].Tracks[0].Reference!.Sequence.ShouldBe("Earth", "a missing target is kept as written");
        Clip(document, 0).IsMissing.ShouldBeTrue();
        document.SequenceLength(0).ShouldBe(0.5, 1e-6);
        Shows(document, 0, 0.1, "Spark").ShouldBeFalse();

        undo.Undo();

        Clip(document, 0).IsBroken.ShouldBeFalse();
        document.SequenceLength(0).ShouldBe(3, 1e-6);
        Shows(document, 0, 0.1, "Spark").ShouldBeTrue();
    }

    [Fact]
    public void DuplicateSequences_Holder_CopyPlaysTheSameTarget_AndEditsStaySeparate()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        document.AddSequenceReference(undo, 0, "Earth", 0.25);

        string copyName = document.DuplicateSequences(undo, new[] { 0 }).Single();
        int copy = document.Save.Sequences.FindIndex(s => s.Name == copyName);
        document.SetReferenceStart(undo, copy, 0, 2).ShouldBeNull();

        document.Save.Sequences[copy].Tracks[0].Reference!.Sequence.ShouldBe("Earth");
        document.Save.Sequences[0].Tracks[0].Reference!.Start.ShouldBe(0.25);
        Shows(document, copy, 2.1, "Spark").ShouldBeTrue();
    }

    [Fact]
    public void DuplicateTracks_ReferenceTrack_CopyIsItsOwnReference()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        document.AddSequenceReference(undo, 0, "Earth", 0);

        document.DuplicateTracks(undo, 0, new[] { 0 });
        document.SetReferenceLoops(undo, 0, 0, 4).ShouldBeNull();

        var tracks = document.Save.Sequences[0].Tracks;
        tracks.Count(t => t.Reference?.Sequence == "Earth").ShouldBe(2);
        tracks.Select(t => t.Name).Distinct().Count().ShouldBe(tracks.Count);
        tracks[1].Reference!.Loops.ShouldBe(1f);
        document.SequenceLength(0).ShouldBe(4, 1e-6);
    }

    [Fact]
    public void UndoAndRedo_AddSequenceReference_RemoveAndRestoreThePlayingClip()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        document.AddSequenceReference(undo, 0, "Earth", 0);
        document.SetReferenceLoops(undo, 0, 0, 2);

        undo.Undo();
        undo.Undo();
        Shows(document, 0, 0.1, "Spark").ShouldBeFalse();
        document.SequenceLength(0).ShouldBe(0.5, 1e-6);

        undo.Redo();
        undo.Redo();
        Shows(document, 0, 0.1, "Spark").ShouldBeTrue();
        document.SequenceLength(0).ShouldBe(2, 1e-6);
        SequenceListSave.FromFile(document.Path.FullPath).Sequences[0].Tracks[0].Reference!.Loops.ShouldBe(2f);
    }

    [Fact]
    public void SetReferenceLoops_Fractional_CutsTheClipPartWayThroughTheTarget()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        document.AddSequenceReference(undo, 0, "Earth", 0.5);

        document.SetReferenceLoops(undo, 0, 0, 0.5f).ShouldBeNull();

        Clip(document, 0).Length.ShouldBe(0.5, 1e-6);
        document.SequenceLength(0).ShouldBe(1.0, 1e-6);
        Shows(document, 0, 0.9, "Spark").ShouldBeTrue();
    }

    [Theory]
    [InlineData(-2.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AddSequenceReference_NegativeOrNotANumberStart_StartsAtZero(double start)
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);

        document.AddSequenceReference(new UndoManager(), 0, "Earth", start).ShouldBeNull();

        document.Save.Sequences[0].Tracks[0].Reference!.Start.ShouldBe(0);
    }

    [Fact]
    public void AddSequenceReference_SlotPastTheEnd_GoesToTheBottom_AndATakenNameGetsAFreeOne()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        document.RenameTrack(undo, 0, 0, "Earth");

        document.AddSequenceReference(undo, 0, "Earth", 0, slot: 99).ShouldBeNull();

        var tracks = document.Save.Sequences[0].Tracks;
        tracks.Count.ShouldBe(2);
        tracks[1].Reference!.Sequence.ShouldBe("Earth");
        tracks[1].Name.ShouldNotBe("Earth");
    }

    [Fact]
    public void SetTrackVisible_HiddenReferenceTrack_KeepsTheSequenceLength()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        document.AddSequenceReference(undo, 0, "Earth", 0);
        document.SetReferenceLoops(undo, 0, 0, 3);

        document.SetTrackVisible(undo, 0, 0, false);

        document.SequenceLength(0).ShouldBe(3, 1e-6);
    }

    [Fact]
    public void At_HiddenTrackInsideTheTarget_HidesOnlyThatSprite_AndKeepsTheClip()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        document.AddSequenceReference(undo, 0, "Earth", 0);
        document.SetReferenceLoops(undo, 0, 0, 2);

        document.SetTrackVisible(undo, 1, 0, false);

        SequenceScene.At(document, 0, 0.1).Select(s => s.Chain.Name).ShouldBe(new[] { "Walk" });
        document.SequenceLength(0).ShouldBe(2, 1e-6);
        Clip(document, 0).Rows.Single().Visible.ShouldBeFalse();
    }

    [Fact]
    public void At_NonLoopingHolderPastItsLength_HoldsTheReferencedSprite()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        document.AddSequenceReference(undo, 0, "Earth", 0);
        document.SetReferenceLoops(undo, 0, 0, 2);
        document.SetLoop(undo, 0, false);

        Shows(document, 0, 5, "Spark").ShouldBeTrue("the held final pose is the last instant of the clip");
        Shows(document, 0, 5, "Walk").ShouldBeFalse("Body's chain ended at 0.5");
    }

    [Fact]
    public void MoveTrack_ReferenceTrack_ChangesOnlyTheDrawOrder()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        document.AddSequenceReference(undo, 0, "Earth", 0);

        document.MoveTrack(undo, 0, 0, 2).ShouldBe(1);

        document.Save.Sequences[0].Tracks[1].Reference!.Sequence.ShouldBe("Earth");
        SequenceScene.At(document, 0, 0.1).Select(s => (s.Chain.Name, s.Track)).ShouldBe(new[] { ("Spark", 1), ("Walk", 0) });
    }

    [Fact]
    public void RenameSequence_TargetPlayedBySeveralSequences_UpdatesEvery_AndRedoAgain()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        document.AddSequenceReference(undo, 0, "Earth", 0);
        document.AddSequenceReference(undo, 2, "Earth", 0);

        document.RenameSequence(undo, 1, "Ground").ShouldBeNull();
        undo.Undo();
        undo.Redo();

        document.Save.Sequences[0].Tracks[0].Reference!.Sequence.ShouldBe("Ground");
        document.Save.Sequences[2].Tracks[0].Reference!.Sequence.ShouldBe("Ground");
        Shows(document, 2, 0.1, "Spark").ShouldBeTrue();
    }

    [Fact]
    public void ReloadFromDisk_ExternalRetarget_PlaysTheNewTarget()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        document.AddSequenceReference(new UndoManager(), 0, "Earth", 0);
        var onDisk = SequenceListSave.FromFile(document.Path.FullPath);
        onDisk.Sequences[0].Tracks[0].Reference!.Sequence = "Action";
        onDisk.Save(document.Path.FullPath);

        document.ReloadFromDisk().Changed.ShouldBeTrue();

        SequenceScene.At(document, 0, 0.1).Select(s => s.Chain.Name).ShouldBe(new[] { "Walk", "Walk" });
        Clip(document, 0).IsBroken.ShouldBeFalse();
    }

    [Fact]
    public void ReloadFromDisk_TargetRenamedOutsideTheEditor_LeavesTheReferenceMissing()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        document.AddSequenceReference(new UndoManager(), 0, "Earth", 0);
        var onDisk = SequenceListSave.FromFile(document.Path.FullPath);
        onDisk.Sequences[1].Name = "Ground";
        onDisk.Save(document.Path.FullPath);

        var result = document.ReloadFromDisk();

        result.Error.ShouldBeNull();
        Clip(document, 0).IsMissing.ShouldBeTrue();
        Shows(document, 0, 0.1, "Spark").ShouldBeFalse();
    }

    [Fact]
    public void Load_FileWithNestedReferences_SavesBackUnchanged()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        document.AddSequenceReference(undo, 2, "Earth", 0.5);
        document.AddSequenceReference(undo, 0, "Action", 0.25);
        string written = File.ReadAllText(document.Path.FullPath);

        var reloaded = SequenceDocument.Load(document.Path.FullPath);
        reloaded.SetLoop(undo, 1, false);
        reloaded.SetLoop(undo, 1, true);

        File.ReadAllText(reloaded.Path.FullPath).ShouldBe(written);
        SequenceScene.At(reloaded, 0, 0.8).Any(s => s.Chain.Name == "Spark").ShouldBeTrue("Idle > Action > Earth");
    }

    [Fact]
    public void RenameSequence_OntoADanglingReferenceThatWouldCloseACycle_IsRefused()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        document.AddSequenceReference(undo, 0, "Earth", 0);
        // Earth holds a reference to a sequence that does not exist yet (edited on disk, or its target was deleted).
        document.Save.Sequences[1].Tracks.Add(RefTrack("Back", "Calm"));

        // Renaming Idle to "Calm" would make Idle > Earth > Idle.
        document.RenameSequence(undo, 0, "Calm").ShouldNotBeNull();

        document.Save.Sequences[0].Name.ShouldBe("Idle");
        SequenceReferences.FindCycle(document.Save.Sequences).ShouldBeNull();
    }

    [Fact]
    public void PasteSequences_ThatWouldCloseACycle_IsRefusedWithAMessage()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        document.Save.Sequences[1].Tracks.Add(RefTrack("Back", "Boss"));
        var boss = new SequenceSave { Name = "Boss", Loop = true, Tracks = { RefTrack("Earth", "Earth") } };
        SequenceClipboard.TryRead(SequenceClipboard.Write(boss, dir.Path), out var content, out _).ShouldBeTrue();

        // A pasted "Boss" that plays Earth, which already plays "Boss", would close Boss > Earth > Boss.
        var result = document.PasteSequences(undo, content!);

        SequenceReferences.FindCycle(document.Save.Sequences).ShouldBeNull();
        result.Errors.ShouldNotBeEmpty("the paste did not happen, so it must say why");
        result.Names.ShouldAllBe(name => document.Save.Sequences.Any(s => s.Name == name), "it must not report sequences it did not paste");
    }
}
