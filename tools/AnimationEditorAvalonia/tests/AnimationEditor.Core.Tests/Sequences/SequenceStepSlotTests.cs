using AnimationEditor.Core.Sequences;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>The gap Alt+Up and Alt+Down drop the selection on, so a key moves exactly one place like a drag onto the next gap.</summary>
public class SequenceStepSlotTests
{
    [Fact]
    public void SequencesUp_DropAboveTheRowAboveTheFirstSelected() =>
        SequenceReorder.StepSlot(5, new[] { 2, 3 }, -1).ShouldBe(1);

    [Fact]
    public void SequencesDown_DropBelowTheRowBelowTheLastSelected() =>
        SequenceReorder.StepSlot(5, new[] { 1, 2 }, +1).ShouldBe(4);

    [Fact]
    public void SequencesAtTheTopOrBottom_HaveNoGap()
    {
        SequenceReorder.StepSlot(5, new[] { 0, 1 }, -1).ShouldBeNull();
        SequenceReorder.StepSlot(5, new[] { 3, 4 }, +1).ShouldBeNull();
        SequenceReorder.StepSlot(0, new int[0], +1).ShouldBeNull();
    }

    [Fact]
    public void ASequenceStepUp_IsOnePlaceInThePlan()
    {
        var slot = SequenceReorder.StepSlot(5, new[] { 1 }, -1).ShouldNotBeNull();
        SequenceReorder.Plan(5, new[] { 1 }, slot).ShouldNotBeNull().Order.ShouldBe(new[] { 1, 0, 2, 3, 4 });
    }

    [Fact]
    public void ASequenceStepDown_IsOnePlaceInThePlan()
    {
        var slot = SequenceReorder.StepSlot(4, new[] { 1 }, +1).ShouldNotBeNull();
        SequenceReorder.Plan(4, new[] { 1 }, slot).ShouldNotBeNull().Order.ShouldBe(new[] { 0, 2, 1, 3 });
    }

    [Theory]
    [InlineData(2, 5, -1, 1)]
    [InlineData(2, 5, +1, 4)]
    public void ATrackStep_IsTheSlotOneLaneAway(int from, int count, int delta, int slot)
    {
        TrackReorder.StepSlot(from, count, delta).ShouldBe(slot);
        TrackReorder.TargetLane(from, slot).ShouldBe(from + delta);
    }

    [Fact]
    public void ATrackInTheFirstOrLastLane_HasNoGapThatWay()
    {
        TrackReorder.StepSlot(0, 3, -1).ShouldBeNull();
        TrackReorder.StepSlot(2, 3, +1).ShouldBeNull();
    }
}
