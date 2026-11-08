using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class EventMarkersTests
{
    // A frame that shows for `duration` seconds and carries the named events.
    private static EventFrame F(double duration, params string[] names) => new(duration, names);

    private static (string Name, double Time)[] Fired(IReadOnlyList<EventOccurrence> occurrences) =>
        occurrences.Select(o => (o.Name, Math.Round(o.Time, 6))).ToArray();

    [Fact]
    public void FramesOf_ReadsEachFramesLengthAndEventNames()
    {
        var withEvents = new AnimationFrameSave { FrameLength = 0.25f };
        withEvents.Events.Add(new FlatRedBall2.Animation.AnimationFrameEvent { Name = "Slam" });
        withEvents.Events.Add(new FlatRedBall2.Animation.AnimationFrameEvent { Name = "Shake", Data = "3" });
        var chain = new AnimationChainSave { Frames = { withEvents, new AnimationFrameSave { FrameLength = 0.5f } } };

        var frames = EventMarkers.FramesOf(chain);

        frames.Count.ShouldBe(2);
        frames[0].Duration.ShouldBe(0.25, 1e-6);
        frames[0].Names.ShouldBe(new[] { "Slam", "Shake" });
        frames[1].Duration.ShouldBe(0.5, 1e-6);
        frames[1].Names.ShouldBeEmpty();
    }

    [Fact]
    public void Occurrences_SitAtTheStartOfTheFrameThatCarriesThem()
    {
        var frames = new[] { F(0.1, "A"), F(0.2), F(0.1, "B") };

        var occurrences = EventMarkers.Occurrences(frames, pingPong: false, reverse: false, played: 0.4);

        Fired(occurrences).ShouldBe(new[] { ("A", 0.0), ("B", 0.3) });
        occurrences.Select(o => o.Frame).ShouldBe(new[] { 0, 2 });
    }

    [Fact]
    public void Occurrences_RepeatEveryLoopCycle()
    {
        // 0.4 s cycle played 1.0 s: two whole cycles and a partial third. B of the third cycle would be at 1.1 s.
        var frames = new[] { F(0.1, "A"), F(0.2), F(0.1, "B") };

        var occurrences = EventMarkers.Occurrences(frames, false, false, played: 1.0);

        Fired(occurrences).ShouldBe(new[] { ("A", 0.0), ("B", 0.3), ("A", 0.4), ("B", 0.7), ("A", 0.8) });
        occurrences.Select(o => o.Cycle).ShouldBe(new[] { 0, 0, 1, 1, 2 });
    }

    [Fact]
    public void Occurrences_ANonLoopingChain_FiresOnlyDuringItsFirstCycle_NotDuringTheHold()
    {
        // 0.4 s cycle in a 1.0 s block: A and B fire once; the held last frame (which carries B) does not fire again.
        var frames = new[] { F(0.1, "A"), F(0.2), F(0.1, "B") };

        var occurrences = EventMarkers.Occurrences(frames, false, false, played: 1.0, loop: false);

        Fired(occurrences).ShouldBe(new[] { ("A", 0.0), ("B", 0.3) });
    }

    [Fact]
    public void Occurrences_ANonLoopingChainCutBeforeItsCycleEnds_FollowsTheCutRule()
    {
        var frames = new[] { F(0.1, "A"), F(0.2), F(0.1, "B") };

        Fired(EventMarkers.Occurrences(frames, false, false, played: 0.3, loop: false)).ShouldBe(new[] { ("A", 0.0) });
    }

    [Fact]
    public void Occurrences_AFrameEnteredBeforeThePlayedEnd_FiresEvenIfItIsCutShort()
    {
        var frames = new[] { F(0.5, "A"), F(0.5, "B") };

        var occurrences = EventMarkers.Occurrences(frames, false, false, played: 0.7);

        Fired(occurrences).ShouldBe(new[] { ("A", 0.0), ("B", 0.5) });
    }

    [Fact]
    public void Occurrences_AFrameThatStartsAtOrPastThePlayedEnd_DoesNotFire()
    {
        // The cut chain stops at 0.5 s: C starts there and never plays, and neither does D.
        var frames = new[] { F(0.25, "A"), F(0.25, "B"), F(0.25, "C"), F(0.25, "D") };

        var occurrences = EventMarkers.Occurrences(frames, false, false, played: 0.5);

        Fired(occurrences).ShouldBe(new[] { ("A", 0.0), ("B", 0.25) });
    }

    [Fact]
    public void Occurrences_ARepeatingChainCutInTheMiddleOfACycle_DropsTheRestOfThatCycle()
    {
        var frames = new[] { F(0.25, "A"), F(0.25, "B") };

        var occurrences = EventMarkers.Occurrences(frames, false, false, played: 0.6);

        Fired(occurrences).ShouldBe(new[] { ("A", 0.0), ("B", 0.25), ("A", 0.5) });
    }

    [Fact]
    public void Occurrences_AReversedChain_PlaysTheFramesLastToFirst()
    {
        var frames = new[] { F(0.1, "A"), F(0.2), F(0.3, "B") };

        var occurrences = EventMarkers.Occurrences(frames, pingPong: false, reverse: true, played: 0.6);

        Fired(occurrences).ShouldBe(new[] { ("B", 0.0), ("A", 0.5) });
    }

    [Fact]
    public void Occurrences_APingPong_MirrorsOnTheReturnHalf_AndTheTurnaroundAndLoopPointAddNothing()
    {
        // Forward A M B, back B M A (the doubled end frames re-enter nothing), cycle 0.6 s, two cycles.
        var frames = new[] { F(0.1, "A"), F(0.1, "M"), F(0.1, "B") };

        var occurrences = EventMarkers.Occurrences(frames, pingPong: true, reverse: false, played: 1.2);

        Fired(occurrences).ShouldBe(new[]
        {
            ("A", 0.0), ("M", 0.1), ("B", 0.2),
            ("M", 0.4), ("A", 0.5),
            ("M", 0.7), ("B", 0.8),
            ("M", 1.0), ("A", 1.1),
        });
        occurrences.Select(o => o.IsReturn).ShouldBe(new[] { false, false, false, true, true, false, false, true, true });
    }

    [Fact]
    public void Occurrences_AReversedPingPong_StartsOnTheLastFrame()
    {
        var frames = new[] { F(0.1, "A"), F(0.1, "M"), F(0.1, "B") };

        var occurrences = EventMarkers.Occurrences(frames, pingPong: true, reverse: true, played: 0.6);

        Fired(occurrences).ShouldBe(new[] { ("B", 0.0), ("M", 0.1), ("A", 0.2), ("M", 0.4), ("B", 0.5) });
    }

    [Fact]
    public void Occurrences_AOneFrameLoop_FiresEachCycle_ButAOneFramePingPongOnlyOnce()
    {
        var frames = new[] { F(0.2, "X") };

        Fired(EventMarkers.Occurrences(frames, false, false, played: 0.6)).ShouldBe(new[] { ("X", 0.0), ("X", 0.2), ("X", 0.4) });
        Fired(EventMarkers.Occurrences(frames, true, false, played: 0.8)).ShouldBe(new[] { ("X", 0.0) });
    }

    [Fact]
    public void Occurrences_SeveralEventsOnOneFrame_AllFireAtItsStartInOrder()
    {
        var frames = new[] { F(0.1), F(0.1, "Hit", "Spark") };

        var occurrences = EventMarkers.Occurrences(frames, false, false, played: 0.2);

        Fired(occurrences).ShouldBe(new[] { ("Hit", 0.1), ("Spark", 0.1) });
    }

    [Fact]
    public void Occurrences_NoEventsOrNoLength_IsEmpty()
    {
        EventMarkers.Occurrences(new[] { F(0.1), F(0.1) }, false, false, 1).ShouldBeEmpty();
        EventMarkers.Occurrences(new[] { F(0, "A") }, false, false, 1).ShouldBeEmpty();
        EventMarkers.Occurrences(new[] { F(0.1, "A") }, false, false, 0).ShouldBeEmpty();
    }

    // ---- every cycle of a chain reference's block fires (#1730), through the real block geometry

    // A chain of 0.1 s frames, each carrying the events named at its index.
    private static AnimationChainSave Chain(int frameCount, params (int Frame, string Name)[] events)
    {
        var chain = new AnimationChainSave { Name = "C" };
        for (int i = 0; i < frameCount; i++)
        {
            var frame = new AnimationFrameSave { FrameLength = 0.1f };
            foreach (var (index, name) in events)
                if (index == i) frame.Events.Add(new FlatRedBall2.Animation.AnimationFrameEvent { Name = name });
            chain.Frames.Add(frame);
        }
        return chain;
    }

    // The occurrences of the first block of a one-track sequence holding `references`, as the timeline computes them.
    private static IReadOnlyList<EventOccurrence> FiredByFirstBlock(AnimationChainSave chain, params SequenceChainReferenceSave[] references)
    {
        var track = new SequenceTrackSave { Name = "T" };
        track.Chains.AddRange(references);
        var sequence = new SequenceSave { Name = "S" };
        sequence.Tracks.Add(track);
        double cycle = chain.Frames.Sum(f => (double)f.FrameLength) * (chain.PingPong ? 2 : 1);
        var block = TimelineLayout.Blocks(sequence, _ => cycle).First(b => ReferenceEquals(b.Reference, references[0]));
        return EventMarkers.Occurrences(EventMarkers.FramesOf(chain), chain.PingPong, chain.Reverse, block.Played, chain.Loop);
    }

    private static SequenceChainReferenceSave Ref(double start, float loops) => new() { Source = "s.achx", Chain = "C", Start = start, Loops = loops };

    [Fact]
    public void ABlockRepeatedThreeTimes_FiresAFrameThreeEventInEveryCycle()
    {
        // Frame 3 (index 2) of a 4 x 0.1 s chain: 0.2, 0.6 and 1.0 s from the block start.
        var occurrences = FiredByFirstBlock(Chain(4, (2, "Step")), Ref(0, 3));

        Fired(occurrences).ShouldBe(new[] { ("Step", 0.2), ("Step", 0.6), ("Step", 1.0) });
        occurrences.Select(o => o.Cycle).ShouldBe(new[] { 0, 1, 2 });
    }

    [Fact]
    public void ABlockCutByTheNextChain_FiresOnlyTheOccurrencesBeforeTheCut()
    {
        // The next chain starts at 0.7 s: the Step at 0.2 and 0.6 play, the one at 1.0 is in the cut part.
        var occurrences = FiredByFirstBlock(Chain(4, (2, "Step")), Ref(0, 3), Ref(0.7, 1));

        Fired(occurrences).ShouldBe(new[] { ("Step", 0.2), ("Step", 0.6) });
    }

    [Fact]
    public void ABlockCutInsideAFrame_DoesNotFireTheFrameThatNeverStarts()
    {
        // Cut at 0.55 s: the cycle-two Step frame would start at 0.6 s, after the cut.
        Fired(FiredByFirstBlock(Chain(4, (2, "Step")), Ref(0, 3), Ref(0.55, 1))).ShouldBe(new[] { ("Step", 0.2) });
    }

    [Fact]
    public void APingPongBlockRepeated_RaisesNothingAtTheTurnaroundOrTheLoopPoint()
    {
        // Frames 0 1 2 3 3 2 1 0 per cycle (0.8 s). The doubled ends re-enter nothing, so End fires once per cycle at 0.3 s
        // and Start on the way back at 0.7 s; the loop point (frame 0 into frame 0) raises no second Start at 0.8 s.
        var chain = Chain(4, (0, "Start"), (3, "End"));
        chain.PingPong = true;

        Fired(FiredByFirstBlock(chain, Ref(0, 2))).ShouldBe(new[]
        {
            ("Start", 0.0), ("End", 0.3), ("Start", 0.7),
            ("End", 1.1), ("Start", 1.5),
        });
    }

    [Fact]
    public void ALoopOffSequence_HoldingItsFinalPose_FiresNothingMoreThanItsBlocksPlay()
    {
        // Sequence Loop off only holds the pose past the length; the hold is no block, so it adds no occurrence.
        var chain = Chain(4, (2, "Step"));
        var track = new SequenceTrackSave { Name = "T" };
        track.Chains.Add(Ref(0, 2));
        var sequence = new SequenceSave { Name = "S", Loop = false };
        sequence.Tracks.Add(track);

        var blocks = TimelineLayout.Blocks(sequence, _ => 0.4);
        var occurrences = EventMarkers.Occurrences(EventMarkers.FramesOf(chain), false, false, blocks[0].Played, chain.Loop);

        blocks[0].PlayedEnd.ShouldBe(0.8, 1e-9);
        Fired(occurrences).ShouldBe(new[] { ("Step", 0.2), ("Step", 0.6) });
    }

    [Fact]
    public void TheMarkerRowOfARepeatedBlock_HasAMarkerPerCycle_AndCollidingOnesStillMerge()
    {
        var occurrences = FiredByFirstBlock(Chain(4, (2, "Step")), Ref(0, 3));

        EventMarkers.Group(occurrences, pixelsPerSecond: 100).Select(m => m.Count).ShouldBe(new[] { 1, 1, 1 });
        // Zoomed out to 10 px/s the three are 4 px apart: one flag counting all three.
        EventMarkers.Group(occurrences, pixelsPerSecond: 10).Select(m => m.Count).ShouldBe(new[] { 3 });
    }

    private static EventOccurrence At(string name, double time) => new(name, 0, time, 0, false);

    [Fact]
    public void Group_FarApartOccurrences_StaySeparateAtTheirPixelPositions()
    {
        var markers = EventMarkers.Group(new[] { At("A", 0), At("B", 0.3) }, pixelsPerSecond: 100, markerWidth: 9);

        markers.Select(m => m.X).ShouldBe(new[] { 0.0, 30.0 });
        markers.Select(m => m.Count).ShouldBe(new[] { 1, 1 });
    }

    [Fact]
    public void Group_OccurrencesCloserThanAMarkerWidth_MergeIntoOneCountedMarker()
    {
        // Hit and Spark are 0.06 s apart: 3.6 px at 60 px/s.
        var markers = EventMarkers.Group(new[] { At("Hit", 0.1), At("Spark", 0.16) }, pixelsPerSecond: 60, markerWidth: 9);

        markers.Count.ShouldBe(1);
        markers[0].Count.ShouldBe(2);
        markers[0].X.ShouldBe(6, 1e-9);
        markers[0].Occurrences.Select(o => o.Name).ShouldBe(new[] { "Hit", "Spark" });
    }

    [Fact]
    public void Group_ZoomingIn_SeparatesTheSameOccurrences()
    {
        var occurrences = new[] { At("Hit", 0.1), At("Spark", 0.16) };

        EventMarkers.Group(occurrences, pixelsPerSecond: 240, markerWidth: 9).Count.ShouldBe(2);
    }

    [Fact]
    public void Group_TwoEventsAtTheSameTime_MergeIntoOne()
    {
        var markers = EventMarkers.Group(new[] { At("Hit", 0.5), At("Spark", 0.5) }, pixelsPerSecond: 1000, markerWidth: 9);

        markers.Count.ShouldBe(1);
        markers[0].Count.ShouldBe(2);
    }

    [Fact]
    public void Group_ExactlyAMarkerWidthApart_DoesNotMerge()
    {
        // 0.2 s at 50 px/s is 10 px, the marker width: touching, not colliding.
        var markers = EventMarkers.Group(new[] { At("A", 0), At("B", 0.2) }, pixelsPerSecond: 50, markerWidth: 10);

        markers.Count.ShouldBe(2);
    }

    [Fact]
    public void Group_AMergedMarkerCoversOnlyAMarkerWidthFromItsFirstOccurrence()
    {
        // 0 px, 6 px and 12 px: the first two merge, the third is past the first's width.
        var markers = EventMarkers.Group(new[] { At("A", 0), At("B", 0.06), At("C", 0.12) }, pixelsPerSecond: 100, markerWidth: 9);

        markers.Select(m => m.Count).ShouldBe(new[] { 2, 1 });
    }

    [Fact]
    public void MarkerAt_FindsTheFlagUnderAPixel_AndNullElsewhere()
    {
        var markers = EventMarkers.Group(new[] { At("A", 0.1), At("B", 0.5) }, pixelsPerSecond: 100, markerWidth: 9);

        EventMarkers.MarkerAt(markers, 10, 9)!.Occurrences[0].Name.ShouldBe("A");
        EventMarkers.MarkerAt(markers, 18.5, 9)!.Occurrences[0].Name.ShouldBe("A");
        EventMarkers.MarkerAt(markers, 19.5, 9).ShouldBeNull();
        EventMarkers.MarkerAt(markers, 9, 9).ShouldBeNull();
        EventMarkers.MarkerAt(markers, 52, 9)!.Occurrences[0].Name.ShouldBe("B");
    }

    [Fact]
    public void For_IsOccurrencesThenGroup()
    {
        var frames = new[] { F(0.1, "A"), F(0.1, "B") };

        var markers = EventMarkers.For(frames, false, false, played: 0.2, pixelsPerSecond: 100, markerWidth: 9);

        markers.Select(m => m.X).ShouldBe(new[] { 0.0, 10.0 });
    }
}
