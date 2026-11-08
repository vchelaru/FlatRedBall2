using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>
/// The plural Inspector edits for a multi-selection of tracks or chains: each applies to the whole set in one snapshot
/// (one undo entry), a relative edit applies to each item's own value, and <see cref="SequenceBulkState"/> says which
/// fields the set disagrees on.
/// </summary>
public class SequenceDocumentBulkEditTests
{
    // Tracks Body (X 1, Y 2), Weapon (X 3, Y 2), Shadow (X 3, Y 2, flipped, hidden); each holds one chain.
    private static SequenceDocument Load(TestHelpers.TempDir dir)
    {
        var save = new SequenceListSave { Sources = { "hero.achj", "boss.achj" } };
        var sequence = new SequenceSave { Name = "Attack" };
        void AddTrack(string name, float x, float y, string source, string chain, double start, float loops, bool flip = false, bool visible = true)
        {
            var track = new SequenceTrackSave { Name = name, X = x, Y = y, FlipHorizontal = flip, Visible = visible };
            track.Chains.Add(new SequenceChainReferenceSave { Source = source, Chain = chain, Start = start, Loops = loops });
            sequence.Tracks.Add(track);
        }
        AddTrack("Body", 1, 2, "hero.achj", "Walk", 0.5, 1);
        AddTrack("Weapon", 3, 2, "hero.achj", "Walk", 1, 2);
        AddTrack("Shadow", 3, 2, "boss.achj", "Idle", 2, 2, flip: true, visible: false);
        save.Sequences.Add(sequence);
        string path = Path.Combine(dir.Path, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    private static SequenceSave Saved(SequenceDocument document) => SequenceListSave.FromFile(document.Path.FullPath).Sequences[0];

    private static readonly int[] AllTracks = { 0, 1, 2 };
    private static readonly TimelineChainId[] AllChains = { new(0, 0), new(1, 0), new(2, 0) };

    [Fact]
    public void TrackState_NullsWhatTheTracksDisagreeOn()
    {
        using var dir = new TestHelpers.TempDir();
        var sequence = Load(dir).Save.Sequences[0];

        SequenceBulkState.ForTracks(sequence, AllTracks).ShouldBe(new TrackBulkState(null, 2f, null, null));
        SequenceBulkState.ForTracks(sequence, new[] { 1, 2 }).X.ShouldBe(3f);
        SequenceBulkState.ForTracks(sequence, new[] { 0, 1 }).Visible.ShouldBe(true);
        SequenceBulkState.ForTracks(sequence, new[] { 0, 1 }).Flip.ShouldBe(false);
    }

    [Fact]
    public void ChainState_NullsWhatTheChainsDisagreeOn()
    {
        using var dir = new TestHelpers.TempDir();
        var sequence = Load(dir).Save.Sequences[0];

        var all = SequenceBulkState.ForChains(sequence, AllChains);
        (all.Source, all.Chain, all.Start, all.Loops).ShouldBe((null, null, null, null));
        var two = SequenceBulkState.ForChains(sequence, new TimelineChainId[] { new(0, 0), new(1, 0) });
        (two.Source, two.Chain, two.Start, two.Loops).ShouldBe(("hero.achj", "Walk", null, null));
        SequenceBulkState.ForChains(sequence, new TimelineChainId[] { new(1, 0), new(2, 0) }).Loops.ShouldBe(2f);
    }

    [Fact]
    public void SetTracksOffset_AbsoluteX_SetsItOnEveryTrack_KeepsEachY_AsOneUndoStep()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();

        document.SetTracksOffset(undo, 0, AllTracks, 5m, null);

        Saved(document).Tracks.Select(t => (t.X, t.Y)).ShouldBe(new[] { (5f, 2f), (5f, 2f), (5f, 2f) });
        undo.UndoHistory.Select(c => c.Description).ShouldBe(new[] { "Set Offset X of 3 tracks to 5" });
        undo.Undo();
        Saved(document).Tracks.Select(t => (t.X, t.Y)).ShouldBe(new[] { (1f, 2f), (3f, 2f), (3f, 2f) });
        undo.Redo();
        Saved(document).Tracks.Select(t => t.X).ShouldBe(new[] { 5f, 5f, 5f });
    }

    [Fact]
    public void SetTracksOffset_RelativeEdit_AppliesToEachTracksOwnValue()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();

        NumericEdit.TryParse("+ 4", out var plusFour).ShouldBeTrue();
        document.SetTracksOffset(undo, 0, AllTracks, plusFour, null);

        Saved(document).Tracks.Select(t => t.X).ShouldBe(new[] { 5f, 7f, 7f });
        undo.UndoHistory.Single().Description.ShouldBe("Set Offset X of 3 tracks");
    }

    [Fact]
    public void SetTracksOffset_BothComponents_NamesBoth_AndSubsetLeavesTheOthersAlone()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();

        document.SetTracksOffset(undo, 0, new[] { 0, 2 }, 9m, -1m);

        Saved(document).Tracks.Select(t => (t.X, t.Y)).ShouldBe(new[] { (9f, -1f), (3f, 2f), (9f, -1f) });
        undo.UndoHistory.Single().Description.ShouldBe("Set Offset of 2 tracks to (9, -1)");
    }

    [Fact]
    public void SetTracksOffset_XThenYInARow_MergeIntoOneUndoStep_UntilSealed()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();

        document.SetTracksOffset(undo, 0, AllTracks, 5m, null);
        document.SetTracksOffset(undo, 0, AllTracks, 6m, null);
        document.SetTracksOffset(undo, 0, AllTracks, null, 8m);
        undo.UndoHistory.Count().ShouldBe(1);
        undo.SealCoalescing();
        document.SetTracksOffset(undo, 0, AllTracks, 7m, null);
        undo.UndoHistory.Count().ShouldBe(2);

        undo.Undo();
        Saved(document).Tracks.Select(t => (t.X, t.Y)).ShouldBe(new[] { (6f, 8f), (6f, 8f), (6f, 8f) });
        undo.Undo();
        Saved(document).Tracks.Select(t => (t.X, t.Y)).ShouldBe(new[] { (1f, 2f), (3f, 2f), (3f, 2f) });
    }

    [Fact]
    public void SetTracksOffset_NothingChanges_RecordsNothing()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();

        document.SetTracksOffset(undo, 0, new[] { 1, 2 }, 3m, 2m);
        document.SetTracksOffset(undo, 0, AllTracks, null, null);

        undo.UndoHistory.ShouldBeEmpty();
    }

    [Fact]
    public void SetTracksVisible_AndFlip_ApplyToTheWholeSet_OneStepEach()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();

        document.SetTracksVisible(undo, 0, AllTracks, true);
        document.SetTracksFlip(undo, 0, AllTracks, true);

        Saved(document).Tracks.Select(t => (t.Visible, t.FlipHorizontal)).ShouldBe(new[] { (true, true), (true, true), (true, true) });
        undo.UndoHistory.Select(c => c.Description).ShouldBe(new[] { "Show 3 tracks", "Turn Flip On for 3 tracks" });
        undo.Undo();
        Saved(document).Tracks.Select(t => t.FlipHorizontal).ShouldBe(new[] { false, false, true });
        undo.Undo();
        Saved(document).Tracks.Select(t => t.Visible).ShouldBe(new[] { true, true, false });
    }

    [Fact]
    public void SetChainsStart_AbsoluteSetsAll_RelativeShiftsEach_OneStepAndUndoRestoresAll()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();

        document.SetChainsStart(undo, 0, AllChains, 4m).ShouldBeNull();
        Saved(document).Tracks.Select(t => t.Chains[0].Start).ShouldBe(new[] { 4d, 4d, 4d });
        undo.SealCoalescing();
        NumericEdit.TryParse("+ 1", out var plusOne).ShouldBeTrue();
        document.SetChainsStart(undo, 0, AllChains, plusOne).ShouldBeNull();
        Saved(document).Tracks.Select(t => t.Chains[0].Start).ShouldBe(new[] { 5d, 5d, 5d });

        undo.UndoHistory.Select(c => c.Description).ShouldBe(new[] { "Set Start of 3 chains to 4", "Set Start of 3 chains" });
        undo.Undo();
        undo.Undo();
        Saved(document).Tracks.Select(t => t.Chains[0].Start).ShouldBe(new[] { 0.5d, 1d, 2d });
    }

    [Fact]
    public void SetChainsLoops_SetsAll_AndRefusesAValueThatWouldBreakAnyChain_ChangingNone()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();

        document.SetChainsLoops(undo, 0, AllChains, 3m).ShouldBeNull();
        Saved(document).Tracks.Select(t => t.Chains[0].Loops).ShouldBe(new[] { 3f, 3f, 3f });
        undo.UndoHistory.Single().Description.ShouldBe("Set Cycles of 3 chains to 3");
        undo.SealCoalescing();

        NumericEdit.TryParse("- 3", out var minusThree).ShouldBeTrue();
        document.SetChainsLoops(undo, 0, new TimelineChainId[] { new(0, 0), new(1, 0) }, minusThree).ShouldBe("Cycles must be above 0.");
        document.SetChainsLoops(undo, 0, AllChains, 0m).ShouldBe("Cycles must be above 0.");
        Saved(document).Tracks.Select(t => t.Chains[0].Loops).ShouldBe(new[] { 3f, 3f, 3f });
        undo.UndoHistory.Count().ShouldBe(1);
    }

    [Fact]
    public void SetChainsStart_RefusesANegativeResult_ChangingNone()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);
        var undo = new UndoManager();

        NumericEdit.TryParse("- 1", out var minusOne).ShouldBeTrue();
        document.SetChainsStart(undo, 0, AllChains, minusOne).ShouldBe("Start must be 0 or more.");

        Saved(document).Tracks.Select(t => t.Chains[0].Start).ShouldBe(new[] { 0.5d, 1d, 2d });
        undo.UndoHistory.ShouldBeEmpty();
    }
}
