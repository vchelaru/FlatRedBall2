using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Dragging and nudging tracks from the preview: <see cref="TrackMoveGesture"/> and <see cref="SequenceDocument.NudgeTracks"/>.</summary>
public class TrackMoveTests
{
    private static SequenceDocument Load(TestHelpers.TempDir dir)
    {
        var save = new SequenceListSave();
        var sequence = new SequenceSave { Name = "Attack" };
        sequence.Tracks.Add(new SequenceTrackSave { Name = "Body", X = 1, Y = 2 });
        sequence.Tracks.Add(new SequenceTrackSave { Name = "Arm", X = -4, Y = 0.5f });
        sequence.Tracks.Add(new SequenceTrackSave { Name = "Fx", X = 0, Y = 0 });
        save.Sequences.Add(sequence);
        string path = Path.Combine(dir.Path, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    private static (float X, float Y) At(SequenceDocument document, int track) =>
        (document.Save.Sequences[0].Tracks[track].X, document.Save.Sequences[0].Tracks[track].Y);

    private static (float X, float Y) Saved(SequenceDocument document, int track)
    {
        var saved = SequenceListSave.FromFile(document.Path.FullPath).Sequences[0].Tracks[track];
        return (saved.X, saved.Y);
    }

    private static string[] Labels(UndoManager undo) => undo.UndoHistory.Select(c => c.Description).ToArray();

    [Fact]
    public void Gesture_UpdateMovesTheDocumentLive_AndCommitIsOneNamedUndoEntry()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        int applied = 0;
        var gesture = new TrackMoveGesture(document, undo, 0, new[] { 0 }, () => applied++);

        gesture.Update(3.2f, -1f, axisLock: false);
        gesture.Update(5.4f, -2f, axisLock: false);

        At(document, 0).ShouldBe((6f, 0f));
        applied.ShouldBe(2);
        undo.UndoHistory.ShouldBeEmpty();

        gesture.Commit();

        Labels(undo).ShouldBe(new[] { "Move Track 'Body'" });
        Saved(document, 0).ShouldBe((6f, 0f));
        undo.Undo();
        At(document, 0).ShouldBe((1f, 2f));
    }

    [Fact]
    public void Gesture_SeveralTracks_MoveTogetherByTheSameDelta_AsOneEntry()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        var gesture = new TrackMoveGesture(document, undo, 0, new[] { 2, 0 }, null);

        gesture.Update(10f, 5f, axisLock: false);
        gesture.Commit();

        gesture.Tracks.ShouldBe(new[] { 0, 2 });
        At(document, 0).ShouldBe((11f, 7f));
        At(document, 1).ShouldBe((-4f, 0.5f));
        At(document, 2).ShouldBe((10f, 5f));
        Labels(undo).ShouldBe(new[] { "Move 2 tracks" });
        undo.Undo();
        (At(document, 0), At(document, 2)).ShouldBe(((1f, 2f), (0f, 0f)));
    }

    [Fact]
    public void Gesture_Cancel_PutsEverythingBack_WithNoUndoEntry()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        var gesture = new TrackMoveGesture(document, undo, 0, new[] { 0, 1 }, null);

        gesture.Update(8f, 8f, axisLock: false);
        gesture.Cancel();

        At(document, 0).ShouldBe((1f, 2f));
        At(document, 1).ShouldBe((-4f, 0.5f));
        Saved(document, 1).ShouldBe((-4f, 0.5f));
        undo.UndoHistory.ShouldBeEmpty();
    }

    [Fact]
    public void Gesture_EndingWhereItStarted_RecordsNothing()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        var gesture = new TrackMoveGesture(document, undo, 0, new[] { 2 }, null);

        gesture.Update(0.4f, -0.3f, axisLock: false);
        gesture.Commit();

        undo.UndoHistory.ShouldBeEmpty();
    }

    [Fact]
    public void Gesture_ShiftKeepsTheLargerAxis_AndUsesTheDeltaFromThePress()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();
        var gesture = new TrackMoveGesture(document, undo, 0, new[] { 2 }, null);

        gesture.Update(9f, 2f, axisLock: true);
        gesture.Update(2f, 9f, axisLock: true);

        At(document, 2).ShouldBe((0f, 9f));
    }

    [Fact]
    public void Nudge_KeepsFractionalOffsetsAsTheyAre_AndLabelsLikeADrag()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();

        document.NudgeTracks(undo, 0, new[] { 1 }, 1f, 0f);

        At(document, 1).ShouldBe((-3f, 0.5f));
        Labels(undo).ShouldBe(new[] { "Move Track 'Arm'" });
    }

    [Fact]
    public void Nudge_HoldingAKey_IsOneUndoEntry_UntilSealed()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();

        for (int i = 0; i < 30; i++) document.NudgeTracks(undo, 0, new[] { 0, 1 }, 1f, 0f);

        Labels(undo).ShouldBe(new[] { "Move 2 tracks" });
        At(document, 0).ShouldBe((31f, 2f));
        undo.Undo();
        At(document, 0).ShouldBe((1f, 2f));
        At(document, 1).ShouldBe((-4f, 0.5f));
        undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void Nudge_AfterSealing_StartsANewEntry()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();

        document.NudgeTracks(undo, 0, new[] { 0 }, 1f, 0f);
        undo.SealCoalescing();
        document.NudgeTracks(undo, 0, new[] { 0 }, 1f, 0f);

        Labels(undo).Length.ShouldBe(2);
        undo.Undo();
        At(document, 0).ShouldBe((2f, 2f));
    }

    [Fact]
    public void Nudge_OfAnotherTrackSet_OrAfterAnotherEdit_DoesNotMerge()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();

        document.NudgeTracks(undo, 0, new[] { 0 }, 1f, 0f);
        document.NudgeTracks(undo, 0, new[] { 1 }, 1f, 0f);
        document.SetTrackFlip(undo, 0, 1, true);
        document.NudgeTracks(undo, 0, new[] { 1 }, 1f, 0f);

        Labels(undo).Length.ShouldBe(4);
    }

    [Fact]
    public void Nudge_ShiftStep_IsTenUnits_AndSavesToDisk()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();

        var (dx, dy) = TrackPlacement.NudgeStep(NudgeDirection.Up, shift: true);
        document.NudgeTracks(undo, 0, new[] { 2 }, dx, dy);

        Saved(document, 2).ShouldBe((0f, 10f));
    }

    [Fact]
    public void DescribeMove_NamesOneTrack_OrCountsSeveral()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);

        document.DescribeMove(0, new[] { 1 }).ShouldBe("Move Track 'Arm'");
        document.DescribeMove(0, new[] { 0, 1, 2 }).ShouldBe("Move 3 tracks");
    }
}
