using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

public class TimelineSnapTests
{
    private static SnapTarget T(double time, SnapKind kind = SnapKind.ChainEdge) => new(time, kind);

    [Theory]
    [InlineData(SnapKind.ChainEdge)]
    [InlineData(SnapKind.Playhead)]
    [InlineData(SnapKind.Zero)]
    [InlineData(SnapKind.OwnCycle)]
    [InlineData(SnapKind.FrameBoundary)]
    public void Edge_EveryKindSnapsWithinTheDistance_AndNotOutsideIt(SnapKind kind)
    {
        // 100 px/s: the 8 px snap distance is 0.08 s.
        var targets = new[] { T(2.0, kind) };

        var inside = TimelineSnap.Edge(2.07, targets, pixelsPerSecond: 100, bypass: false);
        var outside = TimelineSnap.Edge(2.09, targets, pixelsPerSecond: 100, bypass: false);

        inside.Time.ShouldBe(2.0, 1e-12);
        inside.Target!.Value.Kind.ShouldBe(kind);
        outside.Time.ShouldBe(2.09);
        outside.Target.ShouldBeNull();
    }

    [Fact]
    public void Edge_SnapDistanceIsInPixels_SoItShrinksInSecondsAsYouZoomIn()
    {
        var targets = new[] { T(2.0) };

        // 0.05 s away: 5 px at 100 px/s snaps; 100 px at 2000 px/s does not.
        TimelineSnap.Edge(2.05, targets, 100, false).IsSnapped.ShouldBeTrue();
        TimelineSnap.Edge(2.05, targets, 2000, false).IsSnapped.ShouldBeFalse();
        // 0.003 s away is 6 px at 2000 px/s: still reachable at the highest zoom.
        TimelineSnap.Edge(2.003, targets, 2000, false).Time.ShouldBe(2.0, 1e-12);
    }

    [Fact]
    public void Edge_EquidistantTargets_HigherPriorityWins()
    {
        // 128 px/s keeps every number exact: reach 0.0625 s, so the edge sits exactly in reach of both.
        var targets = new[] { T(1.0, SnapKind.Playhead), T(1.125, SnapKind.ChainEdge) };

        var result = TimelineSnap.Edge(1.0625, targets, pixelsPerSecond: 128, bypass: false);

        result.Target!.Value.Kind.ShouldBe(SnapKind.ChainEdge);
        result.Time.ShouldBe(1.125);
    }

    [Fact]
    public void Edge_Bypass_NeverSnaps()
    {
        TimelineSnap.Edge(2.01, new[] { T(2.0) }, 100, bypass: true).ShouldBe(new SnapResult(2.01, null));
    }

    [Fact]
    public void Edge_IgnoresTargetsBelowTheMinimum_AndNeverReturnsLessThanIt()
    {
        var targets = new[] { T(1.0), T(5.0) };

        // The target at 1.0 would give an illegal loop count: ignored. The raw time is raised to the minimum.
        TimelineSnap.Edge(1.02, targets, 100, false, minTime: 1.5).ShouldBe(new SnapResult(1.5, null));
        TimelineSnap.Edge(5.02, targets, 100, false, minTime: 1.5).Time.ShouldBe(5.0, 1e-12);
    }

    [Fact]
    public void Thin_TwoTargetsCloserThanTwiceTheDistance_KeepsTheHigherPriority()
    {
        // 100 px/s: 2x the snap distance is 0.16 s. These are 0.1 s (10 px) apart.
        var thinned = TimelineSnap.Thin(new[] { T(1.0, SnapKind.Playhead), T(1.1, SnapKind.ChainEdge), T(3.0, SnapKind.Zero), T(3.1, SnapKind.OwnCycle) }, 100);

        thinned.Select(t => t.Kind).ShouldBe(new[] { SnapKind.ChainEdge, SnapKind.Zero });
    }

    [Fact]
    public void Thin_EqualPriority_KeepsTheEarlierTime()
    {
        var thinned = TimelineSnap.Thin(new[] { T(1.1), T(1.0) }, 100);

        thinned.Select(t => t.Time).ShouldBe(new[] { 1.0 });
    }

    [Fact]
    public void Thin_ZoomedInFarEnough_KeepsBothOfTheSamePair()
    {
        var targets = new[] { T(1.0, SnapKind.Playhead), T(1.1, SnapKind.ChainEdge) };

        TimelineSnap.Thin(targets, 100).Count.ShouldBe(1);
        TimelineSnap.Thin(targets, 1000).Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(60)]
    [InlineData(120)]
    [InlineData(500)]
    [InlineData(2000)]
    public void Thin_AtAnyZoom_NeverLeavesTwoTargetsCloserThanTwiceTheDistance(double pixelsPerSecond)
    {
        var kinds = Enum.GetValues<SnapKind>();
        var targets = Enumerable.Range(0, 300).Select(i => T(i * 0.0371 % 7.3, kinds[i % kinds.Length])).ToList();
        double gap = 2 * TimelineSnap.SnapPixels / pixelsPerSecond;

        var thinned = TimelineSnap.Thin(targets, pixelsPerSecond);

        thinned.ShouldNotBeEmpty();
        for (int i = 1; i < thinned.Count; i++)
            (thinned[i].Time - thinned[i - 1].Time).ShouldBeGreaterThanOrEqualTo(gap - 1e-12);
        // Whatever was dropped sits within the gap of something kept at the same or higher priority.
        foreach (var dropped in targets.Except(thinned))
            thinned.Any(kept => Math.Abs(kept.Time - dropped.Time) < gap && kept.Kind <= dropped.Kind).ShouldBeTrue();
    }

    [Fact]
    public void Move_StartSnapsToAnotherChainsEnd()
    {
        var result = TimelineSnap.Move(start: 2.03, length: 0.5, new[] { T(2.0) }, 100, bypass: false);

        result.Time.ShouldBe(2.0, 1e-12);
        result.Target!.Value.Time.ShouldBe(2.0);
    }

    [Fact]
    public void Move_TheMovedChainsOwnEndSnapsToAnotherChainsStart()
    {
        // A 0.5 s chain at 1.03 ends at 1.53; a chain starts at 1.5, so the moved chain's start becomes 1.0.
        var result = TimelineSnap.Move(start: 1.03, length: 0.5, new[] { T(1.5) }, 100, bypass: false);

        result.Time.ShouldBe(1.0, 1e-12);
        result.Target!.Value.Time.ShouldBe(1.5);
    }

    [Fact]
    public void Move_WhenBothEndsAreInReach_TheNearerOneWins()
    {
        var targets = new[] { T(1.0), T(1.5) };

        // Start 0.04 from 1.0, end 0.01 from 1.5.
        TimelineSnap.Move(1.04, 0.47, targets, 100, false).Time.ShouldBe(1.03, 1e-12);
    }

    [Fact]
    public void Move_SnapsToTimeZeroAndThePlayhead()
    {
        TimelineSnap.Move(0.04, 1, new[] { T(0, SnapKind.Zero) }, 100, false).Time.ShouldBe(0);
        TimelineSnap.Move(4.03, 1, new[] { T(4.0, SnapKind.Playhead) }, 100, false).Target!.Value.Kind.ShouldBe(SnapKind.Playhead);
    }

    [Fact]
    public void Move_AnEndSnapThatWouldStartBeforeZero_IsNotOffered()
    {
        // A 5 s chain whose end is near a target at 2.0 would have to start at -3.
        var result = TimelineSnap.Move(-2.98, 5, new[] { T(2.0) }, 100, false);

        result.ShouldBe(new SnapResult(0, null));
    }

    [Fact]
    public void Move_Bypass_IsFreeEvenNextToATarget()
    {
        TimelineSnap.Move(2.03, 0.5, new[] { T(2.0) }, 100, bypass: true).ShouldBe(new SnapResult(2.03, null));
    }

    [Fact]
    public void Move_NothingNear_RoundsToTheGridAtLowZoomAndIsFreeAtHighZoom()
    {
        TimelineSnap.Move(1.234, 0.5, Array.Empty<SnapTarget>(), 120, false).Time.ShouldBe(1.25, 1e-12);
        // 0.05 s is 50 px at 1000 px/s: too wide to be a grid, so the start stays where the pointer put it.
        TimelineSnap.Move(1.234, 0.5, Array.Empty<SnapTarget>(), 1000, false).ShouldBe(new SnapResult(1.234, null));
        TimelineSnap.Move(-0.4, 0.5, Array.Empty<SnapTarget>(), 1000, false).Time.ShouldBe(0);
    }

    [Fact]
    public void Move_ATargetInReachBeatsTheGrid()
    {
        // Grid would say 1.0; the target at 1.013 is closer to the pointer's 1.02.
        TimelineSnap.Move(1.02, 0.5, new[] { T(1.013) }, 120, false).Time.ShouldBe(1.013, 1e-12);
    }

    [Fact]
    public void Snap_SameInputs_GiveIdenticalResults()
    {
        var targets = Enumerable.Range(0, 50).Select(i => T(i * 0.31, (SnapKind)(i % 5))).ToList();

        TimelineSnap.Edge(3.1, targets, 150, false).ShouldBe(TimelineSnap.Edge(3.1, targets, 150, false));
        TimelineSnap.Move(3.1, 0.7, targets, 150, false).ShouldBe(TimelineSnap.Move(3.1, 0.7, targets, 150, false));
    }

    [Fact]
    public void CycleMarks_NarrowCycles_OfferEveryNthMarkFromTheStart_WhateverThePointerIsAt()
    {
        // 0.1 s cycles at 120 px/s are 12 px: marks must be 16 px apart, so every second one (0.2, 0.4, ...).
        foreach (double pointer in new[] { 0.9, 1.0, 1.13, 1.31 })
        {
            var marks = TimelineSnap.CycleMarks(start: 0.5, cycleLength: 0.1, pointer, 120).ToList();
            marks.ShouldNotBeEmpty();
            foreach (var mark in marks)
            {
                double steps = (mark.Time - 0.5) / 0.2;
                Math.Abs(steps - Math.Round(steps)).ShouldBeLessThan(1e-9);
            }
        }
    }

    [Fact]
    public void CycleMarks_WideCycles_OfferTheWholeCyclesAroundThePointer()
    {
        var marks = TimelineSnap.CycleMarks(start: 1, cycleLength: 0.5, time: 2.2, 120).Select(m => m.Time).ToList();

        marks.ShouldContain(2.0, 1e-9);
        marks.ShouldContain(2.5, 1e-9);
        marks.ShouldAllBe(t => t >= 1.5 - 1e-9);
    }

    [Fact]
    public void FrameMarks_OnlyWhenACycleIsWideEnoughToHoldThem()
    {
        var offsets = new[] { 0.1, 0.2, 0.3, 0.4 };

        TimelineSnap.FrameMarks(0, 0.5, offsets, 0.55, pixelsPerSecond: 1000).Select(m => m.Time).ShouldContain(0.6, 1e-9);
        TimelineSnap.FrameMarks(0, 0.5, offsets, 0.55, pixelsPerSecond: 20).ShouldBeEmpty();
    }

    [Fact]
    public void GridIsUsable_OnlyWhileOneStepIsNarrow()
    {
        TimelineSnap.GridIsUsable(120).ShouldBeTrue();
        TimelineSnap.GridIsUsable(2000).ShouldBeFalse();
    }
}
