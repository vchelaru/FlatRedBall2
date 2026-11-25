using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>
/// #2137 on the sequence timeline: zero-length frames are skipped, an animation of no length plays nothing (as
/// <see cref="SequenceTimeline"/> has it) and stays selectable, and nothing that repeats by a length loops forever: those
/// tests have a time limit, so a hang fails.
/// </summary>
public class ZeroLengthSequenceTests
{
    private const int TimeLimit = 10000;

    // The shortest cycle a frame length gives: one the game does not round to nothing.
    private const double Tiny = 1e-7;

    private static readonly FrameLengthUnit Sec = FrameLengthUnit.Seconds;

    private static AnimationChainSave Chain(string name, bool pingPong, bool reverse, params float[] lengths)
    {
        var chain = new AnimationChainSave { Name = name, PingPong = pingPong, Reverse = reverse };
        foreach (float length in lengths)
            chain.Frames.Add(new AnimationFrameSave { FrameLength = length, TextureName = name + ".png", RightCoordinate = 16, BottomCoordinate = 16, Events = { new AnimationFrameEvent { Name = "e" + chain.Frames.Count } } });
        return chain;
    }

    private static AnimationChainSave Chain(params float[] lengths) => Chain("A", false, false, lengths);

    private sealed class Lookup : ISequenceChainLookup
    {
        private readonly List<AnimationChainSave> _chains =
        [
            Chain("None", false, false, 0f, 0f), Chain("NonePingPong", true, true, 0f, 0f), Chain("Tiny", false, false, 0f, (float)Tiny, 0f),
            Chain("TinyPingPong", true, false, 0f, (float)Tiny, 0f), Chain("Walk", false, false, 0.5f, 0f, 0.5f),
        ];

        public AnimationChainSave? FindChain(SequenceChainReferenceSave reference) => _chains.Find(c => c.Name == reference.Chain);
        public string? ResolveTexturePath(string source, AnimationFrameSave frame) => frame.TextureName;
        public FrameLengthUnit UnitOf(SequenceChainReferenceSave reference) => Sec;

        public double? CycleLength(SequenceChainReferenceSave reference) => FindChain(reference) is { } chain ? ChainClock.TotalLength(chain, Sec) : null;
    }

    private static SequenceSave Sequence(string name, params SequenceTrackSave[] tracks)
    {
        var sequence = new SequenceSave { Name = name };
        sequence.Tracks.AddRange(tracks);
        return sequence;
    }

    private static SequenceTrackSave Blocks(string chain, double? length = null, bool continuous = false, double start = 0) =>
        new() { Name = chain, Continuous = continuous, Chains = { new SequenceChainReferenceSave { Source = "a.achx", Chain = chain, Start = start, Length = length } } };

    private static SequenceTrackSave Clip(string sequence, double? length = null, bool continuous = false) =>
        new() { Name = sequence, Continuous = continuous, References = [new SequenceReferenceSave { Sequence = sequence, Length = length }] };

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Filmstrip_ZeroLengthFrame_HasNoCell(bool pingPong, bool reverse)
    {
        var chain = Chain("A", pingPong, reverse, 0f, 0.5f, 0f, 0.5f, 0f);
        double cycle = ChainClock.TotalLength(chain, Sec);

        var cells = FilmstripLayout.Cells(FilmstripLayout.FramesOf(chain, Sec, _ => true), pingPong, reverse, 2 * cycle, 100, 25);

        cells.SelectMany(c => c.Visits).Select(v => v.Frame).Distinct().OrderBy(f => f).ShouldBe([1, 3]);
        cells.ShouldAllBe(c => !c.IsMerged);
        cells.Sum(c => c.Width).ShouldBe(2 * cycle * 100, 1e-6);
    }

    public static TheoryData<float[], bool, bool> ZeroFrames() => new()
    {
        { [0f, 0.5f, 0f, 0.5f, 0f], false, false }, { [0f, 0.5f, 0f, 0.5f, 0f], false, true },
        { [0f, 0.5f, 0f, 0.5f, 0f], true, false }, { [0f, 0.5f, 0f, 0.5f, 0f], true, true },
        { [0.5f, 0f], false, false }, { [0f, 0.5f], false, false }, { [0f, 0.5f], false, true }, { [0f, 0f, 0.5f], true, false },
    };

    private sealed class Frame : AnimationFrameBase { }

    // The marks on a block are the events AnimationPlayer raises while the block plays.
    [Theory]
    [MemberData(nameof(ZeroFrames))]
    public void EventMarks_ZeroLengthFrames_AreTheEventsTheGameRaises(float[] lengths, bool pingPong, bool reverse)
    {
        var save = Chain("A", pingPong, reverse, lengths);
        double played = 2 * ChainClock.TotalLength(save, Sec);
        var chain = new AnimationChain<Frame> { Name = "A", PingPong = pingPong, Reversed = reverse };
        foreach (var from in save.Frames)
        {
            var frame = new Frame { FrameLength = from.GetDuration(TimeMeasurementUnit.Second) };
            frame.Events.AddRange(from.Events);
            chain.Add(frame);
        }
        var raised = new List<string>();
        var player = new AnimationPlayer<Frame>(new AnimationChainList<Frame> { chain });
        player.FrameEventRaised += e => raised.Add(e.Name);
        player.Play("A");
        // Up to just before the block's end, which is the next block's.
        for (int step = 0; step < (int)Math.Round(played / 0.01) - 1; step++) player.Update(TimeSpan.FromSeconds(0.01));

        EventMarkers.Occurrences(EventMarkers.FramesOf(save, Sec), pingPong, reverse, played).Select(o => o.Name).ShouldBe(raised);
    }

    [Fact]
    public void FrameBoundaries_ZeroLengthFrames_AreNotBoundaries()
    {
        TimelineSnapTargets.FrameBoundaries(Chain(0f, 0.5f, 0f, 0.5f, 0f), Sec).ShouldBe([0.5]);
        TimelineSnapTargets.FrameBoundaries(Chain("A", true, false, 0.5f, 0f), Sec).ShouldBeEmpty();
        TimelineSnapTargets.FrameBoundaries(Chain(0f, 0f), Sec).ShouldBeEmpty();
    }

    // Set or not, a length does not make an animation of no length play; its block is drawn like an empty animation's.
    [Theory]
    [InlineData(null)]
    [InlineData(2.0)]
    public void Block_AnimationOfNoLength_PlaysNothingAndIsSelectable(double? length)
    {
        var lookup = new Lookup();
        var sequence = Sequence("S", Blocks("None", length));

        var block = TimelineLayout.Blocks(sequence, lookup.CycleLength).ShouldHaveSingleItem();
        var viewport = new TimelineViewport();
        var hit = TimelineLayout.HitTest(viewport, [block], 1, viewport.XAt(0) + 30, viewport.LaneTop(0) + TimelineLayout.FilmTop + 5);

        hit!.Value.Block.ShouldBe(block);
        hit.Value.Part.ShouldBe(TimelineHitPart.Body);
        block.IsMissing.ShouldBeFalse();
        //LABEL
        SequenceScene.At(sequence, 0, 0, lookup, [sequence]).ShouldBeEmpty();
        SequenceThumbnailScene.ChooseTime(sequence, lookup, [sequence]).ShouldBeNull();
        FilmstripLayout.Cells(FilmstripLayout.FramesOf(lookup.FindChain(block.Reference)!, Sec, _ => true), false, false, block.Played, 100, 25).ShouldBeEmpty();
    }

    [Fact]
    public void Block_ZeroLengthFrame_IsNotShown()
    {
        var lookup = new Lookup();
        var sequence = Sequence("S", Blocks("Walk", 2));

        Enumerable.Range(0, 20).Select(i => SequenceScene.At(sequence, 0, i * 0.1 + 0.05, lookup, [sequence]).Single().FrameIndex)
            .Distinct().ShouldBe([0, 2]);
    }

    // Looping sequences of no length or next to none, clips of them two levels deep, and continuous tracks.
    [Theory(Timeout = TimeLimit)]
    [InlineData("None", null)]
    [InlineData("None", 3600.0)]
    [InlineData("NonePingPong", 3600.0)]
    [InlineData("Tiny", null)]
    [InlineData("Tiny", 3600.0)]
    [InlineData("TinyPingPong", 3600.0)]
    public async Task Sequence_OfNoLengthOrNextToNone_EveryWalkReturns(string chain, double? length)
    {
        await Task.Run(() =>
        {
            var lookup = new Lookup();
            foreach (bool continuous in new[] { false, true })
            foreach (double? inner in new double?[] { null, length })
            {
                var leaf = Sequence("Leaf", Blocks(chain, inner, continuous));
                var middle = Sequence("Middle", Clip("Leaf", length, continuous));
                var top = Sequence("Top", Clip("Middle", length, continuous), Blocks(chain, length, continuous));
                List<SequenceSave> file = [leaf, middle, top];
                foreach (var sequence in file)
                {
                    double total = SequenceTimeline.Length(file, sequence, r => lookup.CycleLength(r) ?? 0);
                    var boundaries = PlayheadSteps.Boundaries(file, sequence, lookup.FindChain, lookup.UnitOf);
                    PlayheadSteps.Next(boundaries, 0);
                    PlayheadSteps.Previous(boundaries, total);
                    var viewport = new TimelineViewport();
                    foreach (var block in TimelineLayout.Blocks(sequence, lookup.CycleLength))
                    {
                        var frames = lookup.FindChain(block.Reference)!;
                        FilmstripLayout.Cells(FilmstripLayout.FramesOf(frames, Sec, _ => true), frames.PingPong, frames.Reverse, block.Played, 2000, 25);
                        EventMarkers.For(EventMarkers.FramesOf(frames, Sec), frames.PingPong, frames.Reverse, block.Played, 2000);
                        TimelineCulling.VisibleCycles(viewport, block.Start, block.Played, block.CycleLength, 2000).Count().ShouldBeLessThan(5000);
                        TimelineSnapTargets.ResizeEdge(block, 1, [], TimelineSnapTargets.FrameBoundaries(frames, Sec), 100, false);
                    }
                    foreach (var clip in TimelineReferences.For(sequence, file, lookup.CycleLength))
                    {
                        TimelineCulling.VisibleCycles(viewport, clip.Start, clip.Length, clip.TargetLength, 2000).Count().ShouldBeLessThan(5000);
                        ReferenceFilmstrip.Cells(clip.TargetLength, clip.Reference.Length, 100, 1000, 2000);
                        ReferenceFilmstrip.PoseAt(clip.Reference, SequenceReferences.Find(file, clip.Reference.Sequence)!, 0, lookup, file);
                    }
                    foreach (int pass in new[] { 0, 1, int.MaxValue })
                        foreach (double time in new[] { 0, total / 2, total, 1e6 })
                            SequenceScene.At(sequence, 0, time, lookup, file, pass);
                    SequenceThumbnailScene.ChooseTime(sequence, lookup, file);
                    var clock = new SequenceClock();
                    clock.Play(total);
                    for (int i = 0; i < 100; i++) clock.AdvanceTick(0.25, total, loop: true);
                    clock.Advance(1e6, total, loop: true);
                    clock.Advance(1e6, total, loop: false);
                }
            }
        });
    }

    // A clip of hours scrolled to its far end, and a block whose cycles no event or cell makes progress on.
    [Theory(Timeout = TimeLimit)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TinyCycle_LongBlock_WalksReturn(bool pingPong)
    {
        await Task.Run(() =>
        {
            foreach (double cycle in new[] { Tiny, 2 * Tiny })
            {
                ReferenceFilmstrip.Cells(cycle, 36000, 100, 3_000_000, 3_000_500);
                var viewport = new TimelineViewport();
                TimelineCulling.VisibleCycles(viewport, 0, 36000, cycle, 5000).Count().ShouldBeLessThan(20000);
                viewport.ScrollHorizontally(3000 * viewport.PixelsPerSecond);
                TimelineCulling.VisibleCycles(viewport, 0, 36000, cycle, 5000).Count().ShouldBeLessThan(20000);
            }
            FilmstripFrame[] one = [new(0, 8, 8), new(Tiny, 8, 8), new(0, 8, 8)];
            FilmstripLayout.Cells(one, pingPong, false, 36000, 100, 25);
            EventFrame[] silent = [new(0, []), new(Tiny, []), new(0, [])];
            EventMarkers.Occurrences(silent, pingPong, false, 36000);
            EventMarkers.Occurrences([new EventFrame(Tiny, ["e"])], pingPong, false, 36000);
        });
    }

    // A source reload can zero the lengths of a sequence that is playing.
    [Theory(Timeout = TimeLimit)]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Playback_SequenceLosesItsLengthWhilePlaying_Returns(bool loop)
    {
        var clock = new SequenceClock();
        clock.Play(2);
        clock.Advance(1.5, 2, loop);

        await Task.Run(() => { for (int i = 0; i < 100; i++) clock.AdvanceTick(0.25, 0, loop); });

        clock.Time.ShouldBe(0);
        clock.IsPlaying.ShouldBe(loop);
    }
}
