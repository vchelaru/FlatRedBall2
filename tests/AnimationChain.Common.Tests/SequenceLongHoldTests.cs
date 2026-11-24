using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>
/// The last instant of a sequence stays outside the boundary slack, which reaches <see cref="SequenceTimeline.HoldEpsilon"/>
/// at 100 s, so whatever plays to the end is what the hold and the carry see. Ends are a microsecond short of the length,
/// the float error of a summed end, because an exact end at exactly 100 s survived the old rule.
/// </summary>
public class SequenceLongHoldTests
{
    private const double FloatError = 1e-6;

    private static SequenceTrackSave ChainTrack(double end) => new()
    {
        Continuous = true,
        Chains = { new SequenceChainReferenceSave { Source = "s", Chain = "A", Length = end } },
    };

    private static SequenceTrackSave ReferenceTrack(double end) => new()
    {
        Continuous = true,
        References = [new SequenceReferenceSave { Sequence = "T", Length = end }],
    };

    [Theory]
    [InlineData(100)]
    [InlineData(120)]
    [InlineData(200)]
    [InlineData(3600)]
    public void PoseTime_LongNonLoopingSequenceAtItsEnd_IsBeforeItsBlocksEnd(double length)
    {
        double hold = SequenceTimeline.PoseTime(length, length, loop: false);

        SequenceTimeline.ActiveAt(ChainTrack(length - FloatError), hold, _ => 0.4, out _).ShouldNotBeNull();
        SequenceTimeline.ActiveClipAt(ReferenceTrack(length - FloatError), hold, _ => 2).ShouldNotBeNull();
        // Still only the last instant: a block that ended 10 ms earlier stays hidden.
        SequenceTimeline.ActiveAt(ChainTrack(length - 0.01), hold, _ => 0.4, out _).ShouldBeNull();
    }

    [Theory]
    [InlineData(100)]
    [InlineData(120)]
    [InlineData(200)]
    [InlineData(3600)]
    public void ReferenceTime_LongNonLoopingTargetPastItsEnd_IsBeforeItsBlocksEnd(double length)
    {
        var target = new SequenceSave { Name = "T", Loop = false };
        var clip = new SequenceReferenceSave { Sequence = "T", Length = length + 10 };

        double? hold = SequenceTimeline.ReferenceTime(clip, target, length, parentTime: length + 5);

        SequenceTimeline.ActiveAt(ChainTrack(length - FloatError), hold!.Value, _ => 0.4, out _).ShouldNotBeNull();
    }

    [Theory]
    [InlineData(100)]
    [InlineData(120)]
    [InlineData(200)]
    [InlineData(3600)]
    public void CarryOver_LongSequence_SeesTheBlockPlayingAtTheWrap(double length)
    {
        SequenceTimeline.CarryOver(ChainTrack(length - FloatError), length, 1, _ => 0.7).ShouldBe(length);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(120)]
    [InlineData(200)]
    [InlineData(3600)]
    public void ReferenceCarryOver_LongHolder_SeesTheClipPlayingAtTheWrap(double length)
    {
        SequenceTimeline.ReferenceCarryOver(ReferenceTrack(length - FloatError), length, 1, _ => 0.7, _ => true).ShouldBe(length);
    }

    [Theory]
    [InlineData(0.00005, 0)]
    [InlineData(0.4, 0.4 - 1e-4)]
    [InlineData(2, 2 - 1e-4)]
    [InlineData(60, 60 - 1e-4)]
    [InlineData(99.999, 99.999 - 1e-4)]
    public void PoseTime_NonLoopingSequenceUnderAHundredSeconds_HoldsATenthOfAMillisecondBeforeTheEnd(double length, double expected)
    {
        SequenceTimeline.PoseTime(length, length, loop: false).ShouldBe(expected);
    }
}
