using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Editing.Tests;

public class SequenceEditTests : IDisposable
{
    private readonly SeqWorkspace _w = new();

    public void Dispose() => _w.Dispose();

    private static string Snap(SequenceSession s) => s.Document.Save.ToJsonString();
    private static SequenceSave Seq(SequenceSession s, string name) => s.Document.Save.Sequences.Single(q => q.Name == name);

    /// <summary>Runs <paramref name="op"/>; a success is exactly one undo step that undo reverts, a failure changes nothing.</summary>
    private static EditResult Run(SequenceSession s, Func<EditResult> op, bool expectChange = true)
    {
        var before = Snap(s);
        var steps = s.Undo.UndoHistory.Count;
        var result = op();
        if (!result.Success || !expectChange)
        {
            Snap(s).ShouldBe(before);
            s.Undo.UndoHistory.Count.ShouldBe(steps);
            return result;
        }
        Snap(s).ShouldNotBe(before);
        s.Undo.UndoHistory.Count.ShouldBe(steps + 1);
        s.Undo.Undo();
        Snap(s).ShouldBe(before);
        s.Undo.Redo();
        return result;
    }

    [Fact]
    public void AddSequence_DefaultName_IsUnique_AndNamedIsUsed()
    {
        var s = _w.Open();

        var named = Run(s, () => SequenceEdit.AddSequence(s.Document, s.Undo, "Jump"));
        var defaulted = Run(s, () => SequenceEdit.AddSequence(s.Document, s.Undo, null));

        named.ChangedItems.ShouldBe(["Jump"]);
        defaulted.ChangedItems.ShouldBe(["Sequence"]);
        s.Document.Save.Sequences.Select(q => q.Name).ShouldBe(["Run", "Rest", "Jump", "Sequence"]);
    }

    [Theory]
    [InlineData("Run", EditErrorCode.DuplicateName)]
    [InlineData("  ", EditErrorCode.EmptyName)]
    [InlineData("a/b", EditErrorCode.InvalidAddress)]
    public void AddSequence_BadName_Fails(string name, EditErrorCode code)
    {
        var s = _w.Open();

        var r = Run(s, () => SequenceEdit.AddSequence(s.Document, s.Undo, name));

        r.Success.ShouldBeFalse();
        r.CodeAt(0).ShouldBe(code);
    }

    [Fact]
    public void RenameSequence_RenamesAndFollowsReferences()
    {
        var s = _w.Open();
        SequenceEdit.AddReferenceTrack(s.Document, s.Undo, "Rest", "Run", 0, null).Success.ShouldBeTrue();

        var r = Run(s, () => SequenceEdit.RenameSequence(s.Document, s.Undo, "Run", "Sprint"));

        r.Success.ShouldBeTrue();
        Seq(s, "Rest").Tracks[0].References![0].Sequence.ShouldBe("Sprint");
    }

    [Fact]
    public void RenameSequence_Errors()
    {
        var s = _w.Open();

        Run(s, () => SequenceEdit.RenameSequence(s.Document, s.Undo, "Nope", "X")).CodeAt(0).ShouldBe(EditErrorCode.NotFound);
        Run(s, () => SequenceEdit.RenameSequence(s.Document, s.Undo, "Run", "Rest")).CodeAt(0).ShouldBe(EditErrorCode.DuplicateName);
        Run(s, () => SequenceEdit.RenameSequence(s.Document, s.Undo, "Run", " ")).CodeAt(0).ShouldBe(EditErrorCode.EmptyName);
        Run(s, () => SequenceEdit.RenameSequence(s.Document, s.Undo, "Run", "Run"), expectChange: false).Success.ShouldBeTrue();
    }

    [Fact]
    public void DeleteSequence_RemovesIt_AndFailsForMissing()
    {
        var s = _w.Open();

        Run(s, () => SequenceEdit.DeleteSequence(s.Document, s.Undo, "Rest")).Success.ShouldBeTrue();
        Run(s, () => SequenceEdit.DeleteSequence(s.Document, s.Undo, "Rest")).CodeAt(0).ShouldBe(EditErrorCode.NotFound);

        s.Document.Save.Sequences.Select(q => q.Name).ShouldBe(["Run"]);
    }

    [Fact]
    public void ReorderSequence_UsesFinalPosition()
    {
        var s = _w.Open();

        Run(s, () => SequenceEdit.ReorderSequence(s.Document, s.Undo, "Run", 1)).Success.ShouldBeTrue();
        s.Document.Save.Sequences.Select(q => q.Name).ShouldBe(["Rest", "Run"]);
        Run(s, () => SequenceEdit.ReorderSequence(s.Document, s.Undo, "Run", 1), expectChange: false).Success.ShouldBeTrue();
        Run(s, () => SequenceEdit.ReorderSequence(s.Document, s.Undo, "Run", 2)).CodeAt(0).ShouldBe(EditErrorCode.IndexOutOfRange);
    }

    [Fact]
    public void SetSequenceLoop_SetsFlag_AndSameValueIsNoStep()
    {
        var s = _w.Open();

        Run(s, () => SequenceEdit.SetSequenceLoop(s.Document, s.Undo, "Run", false)).Success.ShouldBeTrue();
        Run(s, () => SequenceEdit.SetSequenceLoop(s.Document, s.Undo, "Run", false), expectChange: false).Success.ShouldBeTrue();

        Seq(s, "Run").Loop.ShouldBeFalse();
    }

    [Fact]
    public void AddTrack_DefaultAtFront_AtIndex_AndNamed()
    {
        var s = _w.Open();

        Run(s, () => SequenceEdit.AddTrack(s.Document, s.Undo, "Run", "Arm", null)).Success.ShouldBeTrue();
        Run(s, () => SequenceEdit.AddTrack(s.Document, s.Undo, "Run", "Leg", 2)).Success.ShouldBeTrue();
        var plain = Run(s, () => SequenceEdit.AddTrack(s.Document, s.Undo, "Run", null, null));

        plain.ChangedItems.ShouldBe(["Run/Track"]);
        Seq(s, "Run").Tracks.Select(t => t.Name).ShouldBe(["Track", "Arm", "Body", "Leg", "Shadow"]);
    }

    [Fact]
    public void AddTrack_Errors()
    {
        var s = _w.Open();

        Run(s, () => SequenceEdit.AddTrack(s.Document, s.Undo, "Nope", null, null)).CodeAt(0).ShouldBe(EditErrorCode.NotFound);
        Run(s, () => SequenceEdit.AddTrack(s.Document, s.Undo, "Run", "Body", null)).CodeAt(0).ShouldBe(EditErrorCode.DuplicateName);
        Run(s, () => SequenceEdit.AddTrack(s.Document, s.Undo, "Run", "Arm", 9)).CodeAt(0).ShouldBe(EditErrorCode.IndexOutOfRange);
    }

    [Fact]
    public void RenameTrack_ByNameOrIndex_AndRejectsDuplicates()
    {
        var s = _w.Open();

        Run(s, () => SequenceEdit.RenameTrack(s.Document, s.Undo, "Run/Shadow", "Dark")).Success.ShouldBeTrue();
        Run(s, () => SequenceEdit.RenameTrack(s.Document, s.Undo, "Run/0", "Torso")).Success.ShouldBeTrue();
        Run(s, () => SequenceEdit.RenameTrack(s.Document, s.Undo, "Run/Torso", "Dark")).CodeAt(0).ShouldBe(EditErrorCode.DuplicateName);
        Run(s, () => SequenceEdit.RenameTrack(s.Document, s.Undo, "Run/Torso", "")).CodeAt(0).ShouldBe(EditErrorCode.EmptyName);

        Seq(s, "Run").Tracks.Select(t => t.Name).ShouldBe(["Torso", "Dark"]);
    }

    [Fact]
    public void DeleteTrack_RemovesIt()
    {
        var s = _w.Open();

        Run(s, () => SequenceEdit.DeleteTrack(s.Document, s.Undo, "Run/Body")).Success.ShouldBeTrue();
        Run(s, () => SequenceEdit.DeleteTrack(s.Document, s.Undo, "Run/Body")).CodeAt(0).ShouldBe(EditErrorCode.NotFound);
        Run(s, () => SequenceEdit.DeleteTrack(s.Document, s.Undo, "Run/5")).CodeAt(0).ShouldBe(EditErrorCode.IndexOutOfRange);

        Seq(s, "Run").Tracks.Select(t => t.Name).ShouldBe(["Shadow"]);
    }

    [Fact]
    public void ReorderTrack_UsesFinalPosition()
    {
        var s = _w.Open();

        Run(s, () => SequenceEdit.ReorderTrack(s.Document, s.Undo, "Run/Body", 1)).Success.ShouldBeTrue();
        Seq(s, "Run").Tracks.Select(t => t.Name).ShouldBe(["Shadow", "Body"]);
        Run(s, () => SequenceEdit.ReorderTrack(s.Document, s.Undo, "Run/Body", 1), expectChange: false).Success.ShouldBeTrue();
        Run(s, () => SequenceEdit.ReorderTrack(s.Document, s.Undo, "Run/Body", 2)).CodeAt(0).ShouldBe(EditErrorCode.IndexOutOfRange);
    }

    [Fact]
    public void SetTrackContinuous_SetsFlag_AndSameValueIsNoStep()
    {
        var s = _w.Open();

        Run(s, () => SequenceEdit.SetTrackContinuous(s.Document, s.Undo, "Run/Body", true)).Success.ShouldBeTrue();
        Run(s, () => SequenceEdit.SetTrackContinuous(s.Document, s.Undo, "Run/Body", true), expectChange: false).Success.ShouldBeTrue();
        Run(s, () => SequenceEdit.SetTrackContinuous(s.Document, s.Undo, "Run/Nope", true)).CodeAt(0).ShouldBe(EditErrorCode.NotFound);

        Seq(s, "Run").Tracks[0].Continuous.ShouldBeTrue();
    }

    [Fact]
    public void AddReferenceTrack_AddsTrackNamedAfterTarget_AtIndex()
    {
        var s = _w.Open();

        var r = Run(s, () => SequenceEdit.AddReferenceTrack(s.Document, s.Undo, "Rest", "Run", 1.5, 1));

        r.Success.ShouldBeTrue();
        var track = Seq(s, "Rest").Tracks[1];
        track.Name.ShouldBe("Run");
        track.References![0].Sequence.ShouldBe("Run");
        track.References![0].Start.ShouldBe(1.5);
    }

    [Fact]
    public void AddReferenceTrack_Errors()
    {
        var s = _w.Open();
        SequenceEdit.AddReferenceTrack(s.Document, s.Undo, "Rest", "Run", 0, null).Success.ShouldBeTrue();

        Run(s, () => SequenceEdit.AddReferenceTrack(s.Document, s.Undo, "Run", "Rest", 0, null)).CodeAt(0).ShouldBe(EditErrorCode.WouldCycle);
        Run(s, () => SequenceEdit.AddReferenceTrack(s.Document, s.Undo, "Run", "Nope", 0, null)).CodeAt(0).ShouldBe(EditErrorCode.NotFound);
        Run(s, () => SequenceEdit.AddReferenceTrack(s.Document, s.Undo, "Run", "Rest", -1, null)).CodeAt(0).ShouldBe(EditErrorCode.InvalidValue);
    }

    [Fact]
    public void AddSource_AddsRelativePath_AndRejectsBadOnes()
    {
        var s = _w.Open();

        Run(s, () => SequenceEdit.AddSource(s.Document, s.Undo, "villain.achj")).Success.ShouldBeTrue();
        Run(s, () => SequenceEdit.AddSource(s.Document, s.Undo, "villain.achj")).CodeAt(0).ShouldBe(EditErrorCode.DuplicateName);
        Run(s, () => SequenceEdit.AddSource(s.Document, s.Undo, "missing.achj")).CodeAt(0).ShouldBe(EditErrorCode.NotFound);
        Run(s, () => SequenceEdit.AddSource(s.Document, s.Undo, "notes.txt")).Success.ShouldBeFalse();

        s.Document.Save.Sources.ShouldBe(["hero.achj", "villain.achj"]);
    }

    [Fact]
    public void RemoveSource_UnusedGoes_UsedIsRefused()
    {
        var s = _w.Open();
        SequenceEdit.AddSource(s.Document, s.Undo, "villain.achj").Success.ShouldBeTrue();

        Run(s, () => SequenceEdit.RemoveSource(s.Document, s.Undo, "villain.achj")).Success.ShouldBeTrue();
        Run(s, () => SequenceEdit.RemoveSource(s.Document, s.Undo, "hero.achj")).CodeAt(0).ShouldBe(EditErrorCode.SourceInUse);
        Run(s, () => SequenceEdit.RemoveSource(s.Document, s.Undo, "nope.achj")).CodeAt(0).ShouldBe(EditErrorCode.NotFound);
    }

    [Fact]
    public void ReplaceSource_RepointsBlocks()
    {
        var s = _w.Open();

        var r = Run(s, () => SequenceEdit.ReplaceSource(s.Document, s.Undo, "hero.achj", "villain.achj"));

        r.Success.ShouldBeTrue();
        s.Document.Save.Sources.ShouldBe(["villain.achj"]);
        Seq(s, "Run").Tracks[0].Chains.ShouldAllBe(c => c.Source == "villain.achj");
        Run(s, () => SequenceEdit.ReplaceSource(s.Document, s.Undo, "nope.achj", "hero.achj")).CodeAt(0).ShouldBe(EditErrorCode.NotFound);
        Run(s, () => SequenceEdit.ReplaceSource(s.Document, s.Undo, "villain.achj", "missing.achj")).CodeAt(0).ShouldBe(EditErrorCode.NotFound);
    }

    [Fact]
    public void AddBlock_AppendsWithStartAndLength_ReportsAddress()
    {
        var s = _w.Open();

        var r = Run(s, () => SequenceEdit.AddBlock(s.Document, s.Undo, "Run/Shadow", "hero.achj", "Walk", 2.5, 3));

        r.ChangedItems.ShouldBe(["Run/Shadow/1"]);
        var block = Seq(s, "Run").Tracks[1].Chains[1];
        (block.Source, block.Chain, block.Start, block.Length).ShouldBe(("hero.achj", "Walk", 2.5, (double?)3));
    }

    [Fact]
    public void AddBlock_OmittedStart_GoesToTrackEnd_UsingAutoLength()
    {
        var s = _w.Open();

        // Run/Body ends with Walk auto from 1: one cycle of 0.4 s.
        var r = Run(s, () => SequenceEdit.AddBlock(s.Document, s.Undo, "Run/Body", null, "Idle", null, null));

        r.ChangedItems.ShouldBe(["Run/Body/2"]);
        var block = Seq(s, "Run").Tracks[0].Chains[2];
        (block.Source, block.Chain, block.Start).ShouldBe(("hero.achj", "Idle", 1.4));
    }

    [Fact]
    public void AddBlock_OmittedStart_UsesLastBlockInTimeNotInList()
    {
        var s = _w.Open();
        // Listed last but earliest in time, with a set length that would end past the other block's end if mistaken for last.
        Seq(s, "Rest").Tracks[0].Chains.Add(new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Walk", Start = 0, Length = 9 });
        Seq(s, "Rest").Tracks[0].Chains[0].Start = 2;

        SequenceEdit.AddBlock(s.Document, s.Undo, "Rest/Body", null, "Idle", null, null).Success.ShouldBeTrue();

        // The block that starts last (Idle at 2, auto 0.5 s) decides the end.
        Seq(s, "Rest").Tracks[0].Chains[2].Start.ShouldBe(2.5);
    }

    [Fact]
    public void AddBlock_OmittedStart_OnEmptyTrack_IsZero()
    {
        var s = _w.Open();
        SequenceEdit.AddTrack(s.Document, s.Undo, "Rest", "Fresh", null).Success.ShouldBeTrue();

        SequenceEdit.AddBlock(s.Document, s.Undo, "Rest/Fresh", null, "Walk", null, 1).Success.ShouldBeTrue();

        Seq(s, "Rest").Tracks[0].Chains.Single().Start.ShouldBe(0);
    }

    [Fact]
    public void AddBlock_OmittedStart_AfterLastBlockWithSetLength_UsesItsLength()
    {
        var s = _w.Open();
        // Rest/Body: Idle@0 auto; give it a set length 3 (longer than its cycle).
        Seq(s, "Rest").Tracks[0].Chains[0].Length = 3;

        SequenceEdit.AddBlock(s.Document, s.Undo, "Rest/Body", "hero.achj", "Walk", null, null).Success.ShouldBeTrue();

        Seq(s, "Rest").Tracks[0].Chains[1].Start.ShouldBe(3);
    }

    [Fact]
    public void AddBlock_OmittedSource_OneSource_UsesIt_ManyOrNone_Errors()
    {
        var s = _w.Open();
        SequenceEdit.AddBlock(s.Document, s.Undo, "Rest/Body", null, "Walk", 1, null).Success.ShouldBeTrue();
        Seq(s, "Rest").Tracks[0].Chains[1].Source.ShouldBe("hero.achj");

        SequenceEdit.AddSource(s.Document, s.Undo, "villain.achj").Success.ShouldBeTrue();
        var many = Run(s, () => SequenceEdit.AddBlock(s.Document, s.Undo, "Rest/Body", null, "Walk", 1, null));
        many.Success.ShouldBeFalse();
        many.CodeAt(0).ShouldBe(EditErrorCode.InvalidValue);
        many.Errors[0].ShouldContain("hero.achj");
        many.Errors[0].ShouldContain("villain.achj");

        SequenceEdit.RemoveSource(s.Document, s.Undo, "villain.achj").Success.ShouldBeTrue();
        s.Document.Save.Sources.Clear();
        Run(s, () => SequenceEdit.AddBlock(s.Document, s.Undo, "Rest/Body", null, "Walk", 1, null)).CodeAt(0).ShouldBe(EditErrorCode.NotFound);
    }

    [Fact]
    public void AddBlock_Errors()
    {
        var s = _w.Open();
        SequenceEdit.AddReferenceTrack(s.Document, s.Undo, "Rest", "Run", 0, null).Success.ShouldBeTrue();

        Run(s, () => SequenceEdit.AddBlock(s.Document, s.Undo, "Run/Body", "hero.achj", "Nope", 0, 1)).CodeAt(0).ShouldBe(EditErrorCode.NotFound);
        Run(s, () => SequenceEdit.AddBlock(s.Document, s.Undo, "Run/Body", "villain.achj", "Walk", 0, 1)).CodeAt(0).ShouldBe(EditErrorCode.NotFound);
        Run(s, () => SequenceEdit.AddBlock(s.Document, s.Undo, "Run/Body", "hero.achj", "Walk", -1, 1)).CodeAt(0).ShouldBe(EditErrorCode.InvalidValue);
        Run(s, () => SequenceEdit.AddBlock(s.Document, s.Undo, "Run/Body", "hero.achj", "Walk", 0, 0)).CodeAt(0).ShouldBe(EditErrorCode.InvalidValue);
        Run(s, () => SequenceEdit.AddBlock(s.Document, s.Undo, "Rest/Run", "hero.achj", "Walk", 0, 1)).CodeAt(0).ShouldBe(EditErrorCode.WrongTrackKind);
        Run(s, () => SequenceEdit.AddBlock(s.Document, s.Undo, "Run/Nope", "hero.achj", "Walk", 0, 1)).CodeAt(0).ShouldBe(EditErrorCode.NotFound);
    }

    [Fact]
    public void SetBlockStartAndLength_ChangeBlock_RepeatedEditsAreSeparateSteps()
    {
        var s = _w.Open();

        Run(s, () => SequenceEdit.SetBlockStart(s.Document, s.Undo, "Run/Body/1", 4)).Success.ShouldBeTrue();
        Run(s, () => SequenceEdit.SetBlockStart(s.Document, s.Undo, "Run/Body/1", 5)).Success.ShouldBeTrue();
        Run(s, () => SequenceEdit.SetBlockLength(s.Document, s.Undo, "Run/Body/1", 0.5)).Success.ShouldBeTrue();
        Run(s, () => SequenceEdit.SetBlockLength(s.Document, s.Undo, "Run/Body/1", 0.5), expectChange: false).Success.ShouldBeTrue();

        var block = Seq(s, "Run").Tracks[0].Chains[1];
        (block.Start, block.Length).ShouldBe((5.0, (double?)0.5));
    }

    [Fact]
    public void SetBlock_Errors()
    {
        var s = _w.Open();

        Run(s, () => SequenceEdit.SetBlockStart(s.Document, s.Undo, "Run/Body/2", 1)).CodeAt(0).ShouldBe(EditErrorCode.IndexOutOfRange);
        Run(s, () => SequenceEdit.SetBlockStart(s.Document, s.Undo, "Run/Body/0", -1)).CodeAt(0).ShouldBe(EditErrorCode.InvalidValue);
        Run(s, () => SequenceEdit.SetBlockStart(s.Document, s.Undo, "Run/Body/0", double.NaN)).CodeAt(0).ShouldBe(EditErrorCode.InvalidValue);
        Run(s, () => SequenceEdit.SetBlockLength(s.Document, s.Undo, "Run/Body/0", 0)).CodeAt(0).ShouldBe(EditErrorCode.InvalidValue);
        Run(s, () => SequenceEdit.SetBlockStart(s.Document, s.Undo, "Run/Body", 1)).Success.ShouldBeFalse();
        Run(s, () => SequenceEdit.SetBlockStart(s.Document, s.Undo, "Run/Body/x", 1)).Success.ShouldBeFalse();
    }

    [Fact]
    public void ReferenceTrack_ClipIsBlockZero()
    {
        var s = _w.Open();
        SequenceEdit.AddReferenceTrack(s.Document, s.Undo, "Rest", "Run", 0, null).Success.ShouldBeTrue();

        Run(s, () => SequenceEdit.SetBlockStart(s.Document, s.Undo, "Rest/Run/0", 2)).Success.ShouldBeTrue();
        Run(s, () => SequenceEdit.SetBlockLength(s.Document, s.Undo, "Rest/Run/0", 3)).Success.ShouldBeTrue();
        Run(s, () => SequenceEdit.SetBlockStart(s.Document, s.Undo, "Rest/Run/1", 2)).CodeAt(0).ShouldBe(EditErrorCode.IndexOutOfRange);

        var clip = Seq(s, "Rest").Tracks[0].References![0];
        (clip.Start, clip.Length).ShouldBe((2.0, (double?)3));

        // Deleting the last clip leaves an empty reference track (the track is not deleted).
        Run(s, () => SequenceEdit.DeleteBlock(s.Document, s.Undo, "Rest/Run/0")).Success.ShouldBeTrue();
        Seq(s, "Rest").Tracks[0].References.ShouldNotBeNull().ShouldBeEmpty();
    }

    [Fact]
    public void ReferenceTrack_AddBlockAppendsClipsAfterTheLastOne()
    {
        var s = _w.Open();
        SequenceEdit.AddReferenceTrack(s.Document, s.Undo, "Rest", "Run", 0, null).Success.ShouldBeTrue();

        Run(s, () => SequenceEdit.AddBlock(s.Document, s.Undo, "Rest/Run", null, null, null, null, sequence: "Run")).Success.ShouldBeTrue();
        Run(s, () => SequenceEdit.AddBlock(s.Document, s.Undo, "Rest/Run", null, null, 9, 2, sequence: "Run")).Success.ShouldBeTrue();

        var clips = Seq(s, "Rest").Tracks[0].References!;
        clips.Select(c => (c.Start, c.Length)).ShouldBe([(0.0, (double?)null), (clips[1].Start, null), (9.0, (double?)2)], ignoreOrder: false);
        clips[1].Start.ShouldBeGreaterThan(0);
        Run(s, () => SequenceEdit.AddBlock(s.Document, s.Undo, "Rest/Run", null, "Walk", null, null)).CodeAt(0).ShouldBe(EditErrorCode.WrongTrackKind);
        Run(s, () => SequenceEdit.AddBlock(s.Document, s.Undo, "Run/Body", null, "Walk", null, null, sequence: "Rest")).CodeAt(0).ShouldBe(EditErrorCode.WrongTrackKind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CopyTrack_WithinItsOwnSequence_ReturnsTheCopysNameAtEveryIndex(int index)
    {
        var s = _w.Open();

        var r = Run(s, () => SequenceEdit.CopyTrack(s.Document, s.Undo, "Run/Body", "Run", index));

        r.ChangedItems.ShouldBe(["Run/Body 2"]);
        Seq(s, "Run").Tracks[index].Name.ShouldBe("Body 2");
    }

    [Fact]
    public void CopyTrack_IntoAnEmptySequence_ReturnsTheCopy()
    {
        var s = _w.Open();
        s.Document.AddSequence(s.Undo, "Fr");

        Run(s, () => SequenceEdit.CopyTrack(s.Document, s.Undo, "Run/Body", "Fr", null)).ChangedItems.ShouldBe(["Fr/Body"]);

        Seq(s, "Fr").Tracks.Single().Chains.Count.ShouldBe(2);
    }

    [Fact]
    public void SetTrackVisible_HidesAndShowsTheTrack_AndIsANoOpWhenAlreadySo()
    {
        var s = _w.Open();

        Run(s, () => SequenceEdit.SetTrackVisible(s.Document, s.Undo, "Run/Body", false)).ChangedItems.ShouldBe(["Run/Body"]);
        Run(s, () => SequenceEdit.SetTrackVisible(s.Document, s.Undo, "Run/Body", false), expectChange: false).ChangedItems.ShouldBeEmpty();

        Seq(s, "Run").Tracks.Select(t => t.Visible).ShouldBe([false, true]);
    }

    [Theory]
    [InlineData("Run/Body/0", 0.8, 0.8)]  // starts at 0, 0.8 long
    [InlineData("Run/Body/1", 1.4, null)] // starts at 1, auto length, one cycle is 0.4
    public void DuplicateBlock_NoToOrStart_LandsRightAfterTheBlockLikeTheEditor(string block, double start, double? length)
    {
        var s = _w.Open();

        Run(s, () => SequenceEdit.DuplicateBlock(s.Document, s.Undo, block, null, null)).ChangedItems.ShouldBe(["Run/Body/2"]);

        var copy = Seq(s, "Run").Tracks[0].Chains[2];
        (copy.Start, copy.Length).ShouldBe((start, length));
    }

    [Fact]
    public void DuplicateBlock_ToAnotherTrack_KeepsTheStartUnlessGiven_AndUnknownProperties()
    {
        var s = _w.Open();
        s.Document.Save.Sequences[0].Tracks[0].Chains[0].UnknownProperties["tag"] = "kept";

        Run(s, () => SequenceEdit.DuplicateBlock(s.Document, s.Undo, "Run/Body/0", "Rest/Body", 2.5)).ChangedItems.ShouldBe(["Rest/Body/1"]);
        Run(s, () => SequenceEdit.DuplicateBlock(s.Document, s.Undo, "Run/Body/1", "Run/Shadow", null)).ChangedItems.ShouldBe(["Run/Shadow/1"]);
        Run(s, () => SequenceEdit.DuplicateBlock(s.Document, s.Undo, "Run/Body/1", "Run/Body", 3)).ChangedItems.ShouldBe(["Run/Body/2"]);

        var copy = Seq(s, "Rest").Tracks[0].Chains[1];
        (copy.Source, copy.Chain, copy.Start, copy.Length, (string?)copy.UnknownProperties["tag"]).ShouldBe(("hero.achj", "Walk", 2.5, (double?)0.8, "kept"));
        Seq(s, "Run").Tracks[1].Chains[1].Start.ShouldBe(1);
        Seq(s, "Run").Tracks[0].Chains.Select(c => c.Start).ShouldBe([0.0, 1.0, 3.0]);
    }

    [Fact]
    public void MoveBlock_ToAnotherTrack_IsTakenOutAndAppended()
    {
        var s = _w.Open();
        s.Document.Save.Sequences[0].Tracks[0].Chains[0].UnknownProperties["tag"] = "kept";

        Run(s, () => SequenceEdit.MoveBlock(s.Document, s.Undo, "Run/Body/0", "Run/Shadow", null)).ChangedItems.ShouldBe(["Run/Shadow/1"]);
        Run(s, () => SequenceEdit.MoveBlock(s.Document, s.Undo, "Run/Body/0", "Rest/0", 2.5)).ChangedItems.ShouldBe(["Rest/Body/1"]);

        Seq(s, "Run").Tracks[0].Chains.ShouldBeEmpty();
        var moved = Seq(s, "Run").Tracks[1].Chains[1];
        (moved.Chain, moved.Start, moved.Length, (string?)moved.UnknownProperties["tag"]).ShouldBe(("Walk", 0.0, (double?)0.8, "kept"));
        Seq(s, "Rest").Tracks[0].Chains.Select(c => (c.Chain, c.Start)).ShouldBe([("Idle", 0.0), ("Walk", 2.5)]);
    }

    [Fact]
    public void MoveBlock_OnItsOwnTrack_KeepsItsIndex()
    {
        var s = _w.Open();

        Run(s, () => SequenceEdit.MoveBlock(s.Document, s.Undo, "Run/Body/0", null, 4)).ChangedItems.ShouldBe(["Run/Body/0"]);
        Run(s, () => SequenceEdit.MoveBlock(s.Document, s.Undo, "Run/Body/0", "Run/Body", null), expectChange: false).Success.ShouldBeTrue();

        Seq(s, "Run").Tracks[0].Chains.Select(c => c.Start).ShouldBe([4.0, 1.0]);
    }

    [Fact]
    public void ReferenceTrack_ClipsMoveAndDuplicate_AndCyclesAreRefused()
    {
        var s = _w.Open();
        SequenceEdit.AddReferenceTrack(s.Document, s.Undo, "Rest", "Run", 0.5, null).Success.ShouldBeTrue();
        s.Document.AddSequence(s.Undo, "Fr");
        SequenceEdit.AddReferenceTrack(s.Document, s.Undo, "Run", "Fr", 0, null).Success.ShouldBeTrue();
        SequenceEdit.AddReferenceTrack(s.Document, s.Undo, "Rest", "Fr", 0, 1).Success.ShouldBeTrue();
        s.Document.Save.Sequences[1].Tracks[0].References![0].UnknownProperties["tag"] = "kept";

        // Run is 1.4 s long.
        Run(s, () => SequenceEdit.DuplicateBlock(s.Document, s.Undo, "Rest/Run/0", null, null)).ChangedItems.ShouldBe(["Rest/Run/1"]);
        Run(s, () => SequenceEdit.DuplicateBlock(s.Document, s.Undo, "Rest/Run/0", "Rest/Fr", null)).ChangedItems.ShouldBe(["Rest/Fr/1"]);
        Run(s, () => SequenceEdit.MoveBlock(s.Document, s.Undo, "Rest/Run/0", "Rest/Fr", 7)).ChangedItems.ShouldBe(["Rest/Fr/2"]);

        var clips = Seq(s, "Rest").Tracks[0].References!;
        clips.Select(c => (c.Sequence, c.Start, (string?)c.UnknownProperties["tag"])).ShouldBe([("Run", 1.9, "kept")]);
        Seq(s, "Rest").Tracks[1].References!.Select(c => (c.Sequence, c.Start)).ShouldBe([("Fr", 0.0), ("Run", 0.5), ("Run", 7.0)]);

        Run(s, () => SequenceEdit.MoveBlock(s.Document, s.Undo, "Rest/Run/0", "Run/Fr", null)).CodeAt(0).ShouldBe(EditErrorCode.WouldCycle);
        Run(s, () => SequenceEdit.DuplicateBlock(s.Document, s.Undo, "Rest/Run/0", "Run/Fr", null)).CodeAt(0).ShouldBe(EditErrorCode.WouldCycle);
    }

    [Fact]
    public void ReferenceTrack_ClipThatAlreadyCycles_CanMoveInItsSequence_ButNotBeCopied()
    {
        var save = SeqWorkspace.Build();
        save.Sequences[1].Tracks.Add(new SequenceTrackSave { Name = "Loop", References = [new SequenceReferenceSave { Sequence = "Rest" }] });
        save.Sequences[1].Tracks.Add(new SequenceTrackSave { Name = "Other", References = [] });
        save.Save(_w.SeqPath);
        var s = _w.Open();

        Run(s, () => SequenceEdit.DuplicateBlock(s.Document, s.Undo, "Rest/Loop/0", null, null)).CodeAt(0).ShouldBe(EditErrorCode.WouldCycle);
        Run(s, () => SequenceEdit.MoveBlock(s.Document, s.Undo, "Rest/Loop/0", "Rest/Other", null)).ChangedItems.ShouldBe(["Rest/Other/0"]);
    }

    [Theory]
    [InlineData("Run/Body/0", "Rest/Run", null, EditErrorCode.WrongTrackKind)]
    [InlineData("Rest/Run/0", "Run/Body", null, EditErrorCode.WrongTrackKind)]
    [InlineData("Run/Body/0", "Run/Shadow", -1.0, EditErrorCode.InvalidValue)]
    [InlineData("Run/Body/0", "Run/Nope", null, EditErrorCode.NotFound)]
    [InlineData("Run/Body/9", "Run/Shadow", null, EditErrorCode.IndexOutOfRange)]
    public void MoveAndDuplicateBlock_Refusals(string block, string to, double? start, EditErrorCode code)
    {
        var s = _w.Open();
        SequenceEdit.AddReferenceTrack(s.Document, s.Undo, "Rest", "Run", 0, null).Success.ShouldBeTrue();

        Run(s, () => SequenceEdit.MoveBlock(s.Document, s.Undo, block, to, start), expectChange: false).CodeAt(0).ShouldBe(code);
        Run(s, () => SequenceEdit.DuplicateBlock(s.Document, s.Undo, block, to, start), expectChange: false).CodeAt(0).ShouldBe(code);
    }

    [Fact]
    public void MoveBlock_NeitherToNorStart_Fails()
    {
        var s = _w.Open();

        Run(s, () => SequenceEdit.MoveBlock(s.Document, s.Undo, "Run/Body/0", null, null), expectChange: false).CodeAt(0).ShouldBe(EditErrorCode.InvalidValue);
    }

    [Fact]
    public void DeleteBlock_RemovesIt_AndShiftsLaterIndexes()
    {
        var s = _w.Open();

        Run(s, () => SequenceEdit.DeleteBlock(s.Document, s.Undo, "Run/Body/0")).Success.ShouldBeTrue();
        Run(s, () => SequenceEdit.DeleteBlock(s.Document, s.Undo, "Run/Body/1")).CodeAt(0).ShouldBe(EditErrorCode.IndexOutOfRange);

        Seq(s, "Run").Tracks[0].Chains.Select(c => c.Start).ShouldBe([1.0]);
    }

    [Fact]
    public void Edits_DoNotWriteTheFile_UntilSessionSaves()
    {
        var s = _w.Open();
        var original = _w.Bytes;

        SequenceEdit.SetSequenceLoop(s.Document, s.Undo, "Run", false).Success.ShouldBeTrue();

        _w.Bytes.ShouldBe(original);
        s.Save().Success.ShouldBeTrue();
        _w.Bytes.ShouldNotBe(original);
        SequenceListSave.FromFile(_w.SeqPath).Sequences[0].Loop.ShouldBeFalse();
    }
}
