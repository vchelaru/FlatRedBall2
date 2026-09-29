using AnimationEditor.Core.Rendering;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Tests for <see cref="WheelZoomAccumulator"/> (#1236): trackpads send many tiny wheel deltas,
/// which must add up to one zoom step per mouse-notch's worth instead of one step per event.
/// </summary>
public class WheelZoomAccumulatorTests
{
    [Fact]
    public void Consume_OneNotch_ReturnsOneStep()
    {
        var accumulator = new WheelZoomAccumulator();
        Assert.Equal(1, accumulator.Consume(1.0));
        Assert.Equal(-1, accumulator.Consume(-1.0));
    }

    [Fact]
    public void Consume_TinyDeltas_StepOnlyOnceTheyAddUpToANotch()
    {
        var accumulator = new WheelZoomAccumulator();
        for (int i = 0; i < 9; i++)
        {
            Assert.Equal(0, accumulator.Consume(0.1));
        }
        Assert.Equal(1, accumulator.Consume(0.1));
    }

    [Fact]
    public void Consume_MultiNotchDelta_ReturnsEveryWholeNotch()
    {
        var accumulator = new WheelZoomAccumulator();
        Assert.Equal(2, accumulator.Consume(2.5));
        Assert.Equal(1, accumulator.Consume(0.5));
    }

    [Fact]
    public void Consume_Reversal_DropsTheCarriedRemainder()
    {
        var accumulator = new WheelZoomAccumulator();
        accumulator.Consume(0.9);
        Assert.Equal(0, accumulator.Consume(-0.5));
        Assert.Equal(-1, accumulator.Consume(-0.5));
    }
}
