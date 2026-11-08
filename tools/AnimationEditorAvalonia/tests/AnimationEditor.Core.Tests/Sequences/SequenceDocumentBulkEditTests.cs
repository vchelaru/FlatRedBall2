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
    // Tracks Body, Weapon and Shadow (flipped, hidden); each holds one chain.
    private static SequenceDocument Load(TestHelpers.TempDir dir)
    {
        var save = new SequenceListSave { Sources = { "hero.achj", "boss.achj" } };
        var sequence = new SequenceSave { Name = "Attack" };
        void AddTrack(string name, string source, string chain, double start, float loops, bool flip = false, bool visible = true)
        {
            var track = new SequenceTrackSave { Name = name, FlipHorizontal = flip, Visible = visible };
            track.Chains.Add(new SequenceChainReferenceSave { Source = source, Chain = chain, Start = start, Loops = loops });
            sequence.Tracks.Add(track);
        }
        AddTrack("Body", "hero.achj", "Walk", 0.5, 1);
        AddTrack("Weapon", "hero.achj", "Walk", 1, 2);
        AddTrack("Shadow", "boss.achj", "Idle", 2, 2, flip: true, visible: false);
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

        SequenceBulkState.ForTracks(sequence, AllTracks).ShouldBe(new TrackBulkState(null, null));
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
