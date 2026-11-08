using AnimationEditor.Core.Sequences;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>The Move Up / Down / To Top / To Bottom menu items: which moves a selection has, and the gap each drops on.</summary>
public class SequenceMoveSlotTests
{
    [Fact]
    public void ASequenceInTheMiddle_CanMoveEveryWay_InTheTreesOrder() =>
        SequenceReorder.AvailableMoves(5, new[] { 2 }).ShouldBe(new[] { ReorderMove.ToTop, ReorderMove.Up, ReorderMove.Down, ReorderMove.ToBottom });

    [Fact]
    public void TheFirstSequence_CannotMoveUp_AndTheLastCannotMoveDown()
    {
        SequenceReorder.AvailableMoves(5, new[] { 0 }).ShouldBe(new[] { ReorderMove.Down, ReorderMove.ToBottom });
        SequenceReorder.AvailableMoves(5, new[] { 4 }).ShouldBe(new[] { ReorderMove.ToTop, ReorderMove.Up });
    }

    [Fact]
    public void AnOnlySequence_OrNoSelection_HasNoMoves()
    {
        SequenceReorder.AvailableMoves(1, new[] { 0 }).ShouldBeEmpty();
        SequenceReorder.AvailableMoves(3, new int[0]).ShouldBeEmpty();
    }

    [Fact]
    public void ABlockAtTheTop_CannotMoveUp_ButASplitBlockCanStillGoToTop()
    {
        SequenceReorder.AvailableMoves(5, new[] { 0, 1 }).ShouldBe(new[] { ReorderMove.Down, ReorderMove.ToBottom });
        // Rows 0 and 2 are selected: Up is blocked by row 0, yet To Top gathers row 2 up beside it.
        SequenceReorder.AvailableMoves(5, new[] { 0, 2 }).ShouldBe(new[] { ReorderMove.ToTop, ReorderMove.Down, ReorderMove.ToBottom });
    }

    [Theory]
    [InlineData(ReorderMove.ToTop, 0)]
    [InlineData(ReorderMove.Up, 1)]
    [InlineData(ReorderMove.Down, 5)]
    [InlineData(ReorderMove.ToBottom, 6)]
    public void Slot_IsTheGapTheMoveDropsOn_ForABlockInTheMiddle(ReorderMove move, int slot) =>
        SequenceReorder.MoveSlot(6, new[] { 2, 3 }, move).ShouldBe(slot);

    [Fact]
    public void ToTopAndUp_OfTheTopTrack_HaveNoGap_AndDownAndToBottom_OfTheBottomOne()
    {
        TrackReorder.MoveSlot(0, 3, ReorderMove.ToTop).ShouldBeNull();
        TrackReorder.MoveSlot(0, 3, ReorderMove.Up).ShouldBeNull();
        TrackReorder.MoveSlot(2, 3, ReorderMove.Down).ShouldBeNull();
        TrackReorder.MoveSlot(2, 3, ReorderMove.ToBottom).ShouldBeNull();
    }

    [Fact]
    public void ATrackMoveUp_IsTowardLaneZero_TheFrontLane_AsAltUpIs()
    {
        TrackReorder.MoveSlot(2, 4, ReorderMove.Up).ShouldBe(TrackReorder.StepSlot(2, 4, -1));
        TrackReorder.TargetLane(2, TrackReorder.MoveSlot(2, 4, ReorderMove.Up)!.Value).ShouldBe(1);
        TrackReorder.TargetLane(2, TrackReorder.MoveSlot(2, 4, ReorderMove.ToTop)!.Value).ShouldBe(0);
        TrackReorder.TargetLane(1, TrackReorder.MoveSlot(1, 4, ReorderMove.Down)!.Value).ShouldBe(2);
        TrackReorder.TargetLane(1, TrackReorder.MoveSlot(1, 4, ReorderMove.ToBottom)!.Value).ShouldBe(3);
    }

    [Fact]
    public void ATrackInTheMiddle_CanMoveEveryWay_AndTheEndsOnlyOneWay()
    {
        TrackReorder.AvailableMoves(4, 1).ShouldBe(new[] { ReorderMove.ToTop, ReorderMove.Up, ReorderMove.Down, ReorderMove.ToBottom });
        TrackReorder.AvailableMoves(4, 0).ShouldBe(new[] { ReorderMove.Down, ReorderMove.ToBottom });
        TrackReorder.AvailableMoves(4, 3).ShouldBe(new[] { ReorderMove.ToTop, ReorderMove.Up });
        TrackReorder.AvailableMoves(1, 0).ShouldBeEmpty();
    }
}
