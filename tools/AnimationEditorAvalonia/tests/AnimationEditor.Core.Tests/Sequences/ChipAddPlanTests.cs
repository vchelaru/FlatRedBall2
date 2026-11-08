using AnimationEditor.Core.Sequences;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Adding a library chip without dragging it: a new track, or the one selected track, starting at the playhead.</summary>
public class ChipAddPlanTests
{
    private static readonly TimelineChainId[] NoChains = Array.Empty<TimelineChainId>();
    private static readonly int[] NoTracks = Array.Empty<int>();

    [Fact]
    public void NewTrack_StartsAtThePlayhead_OnNoExistingTrack_WhateverIsSelected()
    {
        var plan = ChipAddPlan.For(ChipAddMode.NewTrack, new[] { new TimelineChainId(1, 0) }, NoTracks, 1.25);

        plan.CanAdd.ShouldBeTrue();
        plan.Track.ShouldBeNull();
        plan.Start.ShouldBe(1.25);
    }

    [Fact]
    public void ANegativePlayhead_StartsAtZero()
    {
        ChipAddPlan.For(ChipAddMode.NewTrack, NoChains, NoTracks, -0.5).Start.ShouldBe(0);
    }

    [Fact]
    public void SelectedTrack_WithOneTrackHeaderSelected_TargetsThatTrack()
    {
        var plan = ChipAddPlan.For(ChipAddMode.SelectedTrack, NoChains, new[] { 2 }, 0.4);

        plan.CanAdd.ShouldBeTrue();
        plan.Track.ShouldBe(2);
        plan.Start.ShouldBe(0.4);
    }

    [Fact]
    public void SelectedTrack_WithChainsOnOneTrackSelected_TargetsThatTrack()
    {
        var chains = new[] { new TimelineChainId(1, 0), new TimelineChainId(1, 3) };

        ChipAddPlan.For(ChipAddMode.SelectedTrack, chains, NoTracks, 0).Track.ShouldBe(1);
    }

    [Fact]
    public void SelectedTrack_WithNothingSelected_IsRefusedWithAReason()
    {
        var plan = ChipAddPlan.For(ChipAddMode.SelectedTrack, NoChains, NoTracks, 0);

        plan.CanAdd.ShouldBeFalse();
        plan.Track.ShouldBeNull();
        plan.Refusal.ShouldBe(ChipAddPlan.NeedsOneTrackMessage);
    }

    [Fact]
    public void SelectedTrack_WithTwoTracksSelected_IsRefused()
    {
        ChipAddPlan.For(ChipAddMode.SelectedTrack, NoChains, new[] { 0, 1 }, 0).CanAdd.ShouldBeFalse();
        ChipAddPlan.For(ChipAddMode.SelectedTrack, new[] { new TimelineChainId(0, 0), new TimelineChainId(2, 0) }, NoTracks, 0)
            .CanAdd.ShouldBeFalse();
    }
}
