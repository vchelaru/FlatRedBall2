using AnimationEditor.Core.Sequences;
using Shouldly;
using System;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Hide / Show on a track follows the track selection when the clicked track is in a multi-selection.</summary>
public class TrackVisibilityPlanTests
{
    private static TrackVisibilityPlan Plan(int[] selected, int clicked, params int[] hidden) =>
        TrackVisibilityPlan.For(selected, clicked, hidden.Contains);

    [Fact]
    public void AClickedTrackInsideTheSelection_ActsOnTheWholeSelection()
    {
        var plan = Plan(new[] { 0, 1, 2 }, 1);
        plan.Tracks.ShouldBe(new[] { 0, 1, 2 });
        plan.Visible.ShouldBeFalse();
    }

    [Fact]
    public void AClickedTrackOutsideTheSelection_ActsOnThatTrackOnly()
    {
        var plan = Plan(new[] { 0, 1 }, 2);
        plan.Tracks.ShouldBe(new[] { 2 });
        plan.Visible.ShouldBeFalse();
    }

    [Fact]
    public void ASingleSelectedTrack_OrNoSelection_ActsOnTheClickedTrackAlone()
    {
        Plan(new[] { 1 }, 1, 1).Tracks.ShouldBe(new[] { 1 });
        Plan(new[] { 1 }, 1, 1).Visible.ShouldBeTrue();
        Plan(Array.Empty<int>(), 0).Tracks.ShouldBe(new[] { 0 });
    }

    [Fact]
    public void AMixedSelection_HidesAll_WhileAnyIsVisible_AndShowsAll_OnlyWhenAllAreHidden()
    {
        Plan(new[] { 0, 1, 2 }, 0, 0, 1).Visible.ShouldBeFalse();
        Plan(new[] { 0, 1, 2 }, 2, 0, 1).Visible.ShouldBeFalse();
        Plan(new[] { 0, 1, 2 }, 2, 0, 1, 2).Visible.ShouldBeTrue();
    }
}
