using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>
/// A looping target's length is a sum of float frame lengths, so "4 times 0.1 s" is a hair over 0.4. A time within the
/// slack before that end is the start of the next loop, in every wrap and in its pass count.
/// </summary>
public class SequenceLoopWrapTests
{
    // Four 0.1f frames summed as double: 0.4000000059604645.
    private const double TargetLength = 4 * (double)0.1f;

    private static SequenceReferenceSave Clip(double start, double? length) => new() { Sequence = "T", Start = start, Length = length };

    private static SequenceSave Target(bool loop = true) => new() { Name = "T", Loop = loop };

    [Theory]
    [InlineData(0.4)]
    [InlineData(0.8)]
    [InlineData(1.2)]
    [InlineData(0.3999991)] // inside the slack
    public void WrapTime_LoopingWithinTheSlackOfAMultiple_IsTheStartOfTheNextLoop(double time)
    {
        SequenceTimeline.WrapTime(time, TargetLength, loop: true).ShouldBe(0);
    }

    [Theory]
    [InlineData(0.3999, 0.3999)]
    [InlineData(0.7999, 0.3999)]
    [InlineData(0.4001, 0.0001)]
    public void WrapTime_LoopingOutsideTheSlack_IsTheTimeInsideTheLoop(double time, double expected)
    {
        SequenceTimeline.WrapTime(time, TargetLength, loop: true).ShouldBe(expected, 1e-7);
    }

    [Fact]
    public void ReferenceTime_LaterClipAtAMultipleOfTheTarget_RestartsTheTarget()
    {
        SequenceTimeline.ReferenceTime(Clip(1, 3), Target(), TargetLength, parentTime: 1.8).ShouldBe(0);
    }

    [Fact]
    public void ReferenceTime_NonLoopingTargetAtItsLength_HoldsItsFinalPose()
    {
        double hold = TargetLength - SequenceTimeline.HoldEpsilon;

        SequenceTimeline.ReferenceTime(Clip(0, 2), Target(loop: false), TargetLength, parentTime: 0.4).ShouldBe(hold);
        SequenceTimeline.ReferenceTime(Clip(0, 2), Target(loop: false), TargetLength, parentTime: 0.3999).ShouldBe(0.3999);
    }

    [Theory]
    [InlineData(1, 0.0, 5)] // the holder's wrap itself: 2 s in
    [InlineData(1, 0.4, 6)]
    [InlineData(3, 0.8, 17)]
    public void ReferenceTimeAtPass_ContinuousAtAMultipleOfTheTarget_StartsTheNextTargetPass(int pass, double time, int expectedPass)
    {
        var track = new SequenceTrackSave { Continuous = true, References = [Clip(0, 2)] };

        SequenceTimeline.ReferenceTimeAtPass(track, track.References[0], Target(), TargetLength, time, pass, 2, _ => TargetLength, out int targetPass)
            .ShouldBe(0);
        targetPass.ShouldBe(expectedPass);
    }

    [Fact]
    public void ReferenceTimeAtPass_QuotientRoundsUpToAWholeNumber_CountsTheLoopOnce()
    {
        // 1.0 / 0.1 is 10 in double, but 0.1 is stored a hair above, so 1.0 % 0.1 is 0.09999999999999995.
        var track = new SequenceTrackSave { Continuous = true, References = [Clip(0, 1)] };

        SequenceTimeline.ReferenceTimeAtPass(track, track.References[0], Target(), 0.1, 0, 1, 1, _ => 0.1, out int targetPass).ShouldBe(0);
        targetPass.ShouldBe(10);
    }
}
