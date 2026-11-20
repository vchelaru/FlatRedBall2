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
/// A track flagged continuous keeps its chain's playback phase across a looping sequence's wrap (see
/// <see cref="SequenceTimeline.ActiveAtPass"/>); the clock counts the wraps, the scene samples by pass, and the flag is an
/// undoable Inspector edit that copies travel with.
/// </summary>
public class SequenceContinuousTrackTests
{
    // body.achj: Walk, two 0.25 s frames (0.5 s). fx.achj: Spark, two 0.5 s frames (1 s).
    private static SequenceDocument Load(TestHelpers.TempDir dir, params SequenceSave[] sequences)
    {
        WriteSource(Path.Combine(dir.Path, "body.achj"), "Walk", 0.25f, 0.25f);
        WriteSource(Path.Combine(dir.Path, "fx.achj"), "Spark", 0.5f, 0.5f);
        var save = new SequenceListSave { Sources = { "body.achj", "fx.achj" } };
        save.Sequences.AddRange(sequences);
        string path = Path.Combine(dir.Path, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    private static void WriteSource(string path, string chain, params float[] lengths)
    {
        var list = new AnimationChainListSave();
        var save = new AnimationChainSave { Name = chain, Loop = true };
        foreach (float length in lengths)
            save.Frames.Add(new AnimationFrameSave { TextureName = "t.png", FrameLength = length, RightCoordinate = 1, BottomCoordinate = 1 });
        list.AnimationChains.Add(save);
        list.SaveJson(path);
    }

    private static SequenceTrackSave Track(string name, bool continuous, params (string Source, string Chain, double Start, float Loops)[] chains)
    {
        var track = new SequenceTrackSave { Name = name, Continuous = continuous };
        foreach (var (source, chain, start, loops) in chains)
            track.Chains.Add(new SequenceChainReferenceSave { Source = source, Chain = chain, Start = start, Loops = loops });
        return track;
    }

    private static SequenceSave Sequence(params SequenceTrackSave[] tracks)
    {
        var sequence = new SequenceSave { Name = "Idle", Loop = true };
        sequence.Tracks.AddRange(tracks);
        return sequence;
    }

    private static string Describe(IEnumerable<SequenceSprite> sprites) =>
        string.Join(", ", sprites.Select(s => $"{s.Track}:{s.Chain.Name}#{s.FrameIndex}@{s.ElapsedInFrame:0.###}"));

    // ── Clock and playback count the wraps ────────────────────────────────────

    [Fact]
    public void Clock_CountsEachWrap_AndNotAdvancesWithinAPass()
    {
        var clock = new SequenceClock();
        clock.Play(length: 2);

        clock.Advance(1.5, length: 2, loop: true);
        clock.Pass.ShouldBe(0);
        clock.Advance(1.0, length: 2, loop: true);
        clock.Pass.ShouldBe(1);
        clock.Advance(4.5, length: 2, loop: true);

        clock.Pass.ShouldBe(3);
        clock.Time.ShouldBe(1.0, 1e-9);
    }

    [Fact]
    public void Clock_SeekStopAndReplayFromTheEnd_StartPassZero()
    {
        var clock = new SequenceClock();
        clock.Play(length: 2);
        clock.Advance(5, length: 2, loop: true);

        clock.Seek(0.5, length: 2);
        clock.Pass.ShouldBe(0);

        clock.Advance(2, length: 2, loop: true);
        clock.Pass.ShouldBe(1);
        clock.Stop();
        clock.Pass.ShouldBe(0);
    }

    [Fact]
    public void Clock_ANonLoopingSequence_NeverCountsAPass()
    {
        var clock = new SequenceClock();
        clock.Play(length: 2);

        clock.Advance(5, length: 2, loop: false);

        clock.Pass.ShouldBe(0);
    }

    [Fact]
    public void Playback_ReportsEachSequencesPass()
    {
        var playback = new SequencePlayback();
        playback.Select(new[] { "Short", "Long" }, name => name == "Short" ? 1 : 3);
        playback.Play(name => name == "Short" ? 1 : 3);

        for (int i = 0; i < 5; i++) playback.AdvanceTick(0.25, name => name == "Short" ? 1 : 3, _ => true);

        playback.PassOf("Short").ShouldBe(1);
        playback.PassOf("Long").ShouldBe(0);
        playback.PassOf("Missing").ShouldBe(0);
    }

    // ── The scene samples by pass ─────────────────────────────────────────────

    // Walk x 2.5 = 1.25 s long (the whole sequence); a pass-1 sample 0.1 s in is at Walk's 1.35 s mark, 0.35 into a 0.5 s cycle.
    [Theory]
    [InlineData(true, 0, "0:Walk#0@0.1")]
    [InlineData(true, 1, "0:Walk#1@0.1")]
    [InlineData(true, 2, "0:Walk#0@0.1")]
    [InlineData(false, 1, "0:Walk#0@0.1")]
    [InlineData(false, 2, "0:Walk#0@0.1")]
    public void At_AContinuousTrack_ContinuesItsChainsPhaseAcrossPasses(bool continuous, int pass, string expected)
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, Sequence(Track("Body", continuous, ("body.achj", "Walk", 0, 2.5f))));

        Describe(SequenceScene.At(document, 0, 0.1, pass)).ShouldBe(expected);
    }

    [Fact]
    public void At_AContinuousTrackNextToAnOrdinaryOne_OnlyTheFlaggedOneCarries()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, Sequence(
            Track("Fx", false, ("fx.achj", "Spark", 0, 1.25f)),
            Track("Body", true, ("body.achj", "Walk", 0, 2.5f))));

        // Spark is 1.25 s long too; Body's frame at pass 1 is Walk's second.
        Describe(SequenceScene.At(document, 0, 0.1, 1)).ShouldBe("1:Walk#1@0.1, 0:Spark#0@0.1");
    }

    [Fact]
    public void At_ADifferentChainPlayingAtTheWrap_RestartsThatTrack()
    {
        using var dir = new TestHelpers.TempDir();
        // Walk (0.5 s) then Spark (1 s): Spark is playing when the 1.5 s sequence wraps, so the first block restarts.
        var document = Load(dir, Sequence(Track("Body", true, ("body.achj", "Walk", 0, 1), ("fx.achj", "Spark", 0.5, 1))));

        Describe(SequenceScene.At(document, 0, 0.1, 1)).ShouldBe("0:Walk#0@0.1");
    }

    [Fact]
    public void At_ANonLoopingSequence_IgnoresThePass()
    {
        using var dir = new TestHelpers.TempDir();
        var sequence = Sequence(Track("Body", true, ("body.achj", "Walk", 0, 2.5f)));
        sequence.Loop = false;
        var document = Load(dir, sequence);

        Describe(SequenceScene.At(document, 0, 0.1, 1)).ShouldBe("0:Walk#0@0.1");
    }

    [Fact]
    public void At_SeveralSequences_EachSampledAtItsOwnPass()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir,
            Sequence(Track("Body", true, ("body.achj", "Walk", 0, 2.5f))),
            Sequence(Track("Body", true, ("body.achj", "Walk", 0, 2.5f))));

        var sprites = SequenceScene.At(document, new[] { new SequenceTime(0, 0.1, 1), new SequenceTime(1, 0.1, 0) });

        Describe(sprites).ShouldBe("0:Walk#1@0.1, 0:Walk#0@0.1");
    }

    // ── The document edit ─────────────────────────────────────────────────────

    private static SequenceDocument LoadTwoTracks(TestHelpers.TempDir dir) =>
        Load(dir, Sequence(
            Track("Body", false, ("body.achj", "Walk", 0, 1)),
            Track("Fx", false, ("fx.achj", "Spark", 0, 1))));

    private static List<bool> SavedFlags(SequenceDocument document) =>
        SequenceListSave.FromFile(document.Path.FullPath).Sequences[0].Tracks.Select(t => t.Continuous).ToList();

    [Fact]
    public void SetTracksContinuous_SetsTheWholeSet_InOneUndoEntry_WithTheToggleLabel()
    {
        using var dir = new TestHelpers.TempDir();
        var document = LoadTwoTracks(dir);
        var undo = new UndoManager();

        document.SetTracksContinuous(undo, 0, new[] { 0, 1 }, true);

        SavedFlags(document).ShouldBe(new[] { true, true });
        undo.UndoHistory.Select(c => c.Description).ShouldBe(new[] { "Turn Continuous On for 2 Tracks" });
        undo.Undo();
        SavedFlags(document).ShouldBe(new[] { false, false });
        undo.Redo();
        SavedFlags(document).ShouldBe(new[] { true, true });
    }

    [Fact]
    public void SetTracksContinuous_OneTrack_NamesIt_AndAnUnchangedValueRecordsNothing()
    {
        using var dir = new TestHelpers.TempDir();
        var document = LoadTwoTracks(dir);
        var undo = new UndoManager();

        document.SetTracksContinuous(undo, 0, new[] { 1 }, false);
        undo.UndoHistory.ShouldBeEmpty();

        document.SetTracksContinuous(undo, 0, new[] { 1 }, true);
        undo.UndoHistory.Select(c => c.Description).ShouldBe(new[] { "Turn Continuous On for 'Fx'" });
        SavedFlags(document).ShouldBe(new[] { false, true });
    }

    [Fact]
    public void BulkState_IsNullWhenTheTracksDisagree()
    {
        using var dir = new TestHelpers.TempDir();
        var document = LoadTwoTracks(dir);
        document.SetTracksContinuous(new UndoManager(), 0, new[] { 1 }, true);
        var sequence = document.Save.Sequences[0];

        SequenceBulkState.ForTracks(sequence, new[] { 0, 1 }).Continuous.ShouldBeNull();
        SequenceBulkState.ForTracks(sequence, new[] { 1 }).Continuous.ShouldBe(true);
        SequenceBulkState.ForTracks(sequence, new[] { 0 }).Continuous.ShouldBe(false);
    }

    [Fact]
    public void DuplicateTracks_KeepsTheFlag()
    {
        using var dir = new TestHelpers.TempDir();
        var document = LoadTwoTracks(dir);
        document.SetTracksContinuous(new UndoManager(), 0, new[] { 0 }, true);

        document.DuplicateTracks(new UndoManager(), 0, new[] { 0 });

        SavedFlags(document).Count(flag => flag).ShouldBe(2);
    }

    [Fact]
    public void PasteChains_ThatMakeAMissingTrack_GiveItTheSourceTracksFlag()
    {
        using var dir = new TestHelpers.TempDir();
        var source = LoadTwoTracks(dir);
        source.SetTracksContinuous(new UndoManager(), 0, new[] { 0 }, true);
        TimelineClipboard.TryRead(source.CopyChains(0, new[] { new TimelineChainId(0, 0) }), out var content, out _).ShouldBeTrue();

        using var otherDir = new TestHelpers.TempDir();
        var target = Load(otherDir, Sequence(Track("Fx", false, ("fx.achj", "Spark", 0, 1))));
        target.PasteChains(new UndoManager(), 0, content!, selectedTrack: null, time: 0);

        var tracks = SequenceListSave.FromFile(target.Path.FullPath).Sequences[0].Tracks;
        tracks.Single(t => t.Name == "Body").Continuous.ShouldBeTrue();
        tracks.Single(t => t.Name == "Fx").Continuous.ShouldBeFalse();
    }
}
