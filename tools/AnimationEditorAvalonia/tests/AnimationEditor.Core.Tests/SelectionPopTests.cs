using AnimationEditor.Core.Rendering;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Tests for <see cref="SelectionPop"/> — the pure shrink-to-rest easing that drives the
/// wireframe selection-outline bump (#542). No UI, no timer.
/// </summary>
public class SelectionPopTests
{
    [Fact]
    public void IsSettled_AtRest_ReturnsTrue()
    {
        Assert.True(SelectionPop.IsSettled(SelectionPop.RestAmount));
    }

    [Fact]
    public void IsSettled_AtStartAmount_ReturnsFalse()
    {
        Assert.False(SelectionPop.IsSettled(SelectionPop.StartAmount));
    }

    [Fact]
    public void OutlineExpandPx_AtStart_ReturnsMaxExpand()
    {
        Assert.Equal(SelectionPop.MaxExpandPx, SelectionPop.OutlineExpandPx(SelectionPop.StartAmount));
    }

    [Fact]
    public void OutlineExpandPx_AtRest_ReturnsZero()
    {
        Assert.Equal(0f, SelectionPop.OutlineExpandPx(SelectionPop.RestAmount));
    }

    [Fact]
    public void Step_FromStart_MovesTowardRestWithoutFinishingInOneTick()
    {
        float next = SelectionPop.Step(SelectionPop.StartAmount, 0.016f);
        Assert.True(next < SelectionPop.StartAmount && next > SelectionPop.RestAmount,
            $"expected amount in ({SelectionPop.RestAmount}, {SelectionPop.StartAmount}), got {next}");
    }

    [Fact]
    public void Step_RepeatedApplication_ConvergesExactlyAndTerminates()
    {
        float amount = SelectionPop.StartAmount;
        int iterations = 0;
        while (!SelectionPop.IsSettled(amount) && iterations < 1000)
        {
            amount = SelectionPop.Step(amount, 0.016f);
            iterations++;
        }
        Assert.Equal(SelectionPop.RestAmount, amount);
        Assert.True(iterations < 60, $"pop should settle within ~1 s of 60 fps ticks; took {iterations}");
    }

    [Fact]
    public void StrokeWidth_AtStart_ReturnsPeak()
    {
        Assert.Equal(SelectionPop.PeakStrokeWidth, SelectionPop.StrokeWidth(SelectionPop.StartAmount));
    }

    [Fact]
    public void StrokeWidth_AtRest_ReturnsRest()
    {
        Assert.Equal(SelectionPop.RestStrokeWidth, SelectionPop.StrokeWidth(SelectionPop.RestAmount));
    }
}
