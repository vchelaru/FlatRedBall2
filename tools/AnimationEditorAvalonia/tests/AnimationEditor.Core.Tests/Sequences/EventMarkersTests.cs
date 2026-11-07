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
