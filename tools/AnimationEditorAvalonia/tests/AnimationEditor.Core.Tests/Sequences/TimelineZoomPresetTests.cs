using System.Collections.Generic;
using System.Linq;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Sequences;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class TimelineZoomPresetTests
{
    private const double Anchor = TimelineViewport.HeaderWidth + 300;

    private static double Percent(TimelineViewport viewport) =>
        viewport.PixelsPerSecond / TimelineViewport.DefaultPixelsPerSecond * 100;

    private static TimelineViewport At(double percent)
    {
        var viewport = new TimelineViewport();
        viewport.SetPixelsPerSecond(TimelineViewport.DefaultPixelsPerSecond * percent / 100);
        return viewport;
    }

    // One wheel notch: the shared stepper picks the target zoom, the viewport applies it around the pointer. The
    // control's ZoomAnimator composes the same two pieces, easing toward the target.
    private static void Step(TimelineViewport viewport, int direction)
    {
        float zoom = ZoomPresetStepper.StepZoom(
            viewport.Zoom, direction, ZoomPresetStepper.Presets, TimelineViewport.MinZoom, TimelineViewport.MaxZoom);
        viewport.SetPixelsPerSecondAround(Anchor, TimelineViewport.PixelsPerSecondForZoom(zoom));
    }

    private static void StepMany(TimelineViewport viewport, int direction, int count)
    {
        for (int i = 0; i < count; i++) Step(viewport, direction);
    }

    [Fact]
    public void StepOutToTheMinimum_ThenBackIn_LandsExactlyOn100()
    {
        var viewport = new TimelineViewport();
        var levelsOut = new List<double> { viewport.PixelsPerSecond };
        while (viewport.PixelsPerSecond > TimelineViewport.MinPixelsPerSecond)
        {
            Step(viewport, -1);
            levelsOut.Add(viewport.PixelsPerSecond);
        }

        var levelsIn = new List<double> { viewport.PixelsPerSecond };
        while (viewport.PixelsPerSecond < TimelineViewport.DefaultPixelsPerSecond)
        {
            Step(viewport, +1);
            levelsIn.Add(viewport.PixelsPerSecond);
        }

        viewport.PixelsPerSecond.ShouldBe(TimelineViewport.DefaultPixelsPerSecond);
        levelsIn.ShouldBe(Enumerable.Reverse(levelsOut).ToList(), "back in walks the same levels");
        levelsOut.Count.ShouldBeGreaterThan(5, "a fixed ladder, not one jump");
    }

    [Fact]
    public void StepOutFrom100_VisitsEveryPresetInOrder_ThenTheClamp()
    {
        var viewport = new TimelineViewport();

        foreach (int preset in new[] { 75, 66, 50, 33, 25, 16, 10 })
        {
            Step(viewport, -1);
            Percent(viewport).ShouldBe(preset, 1e-9);
        }

        Step(viewport, -1);
        viewport.PixelsPerSecond.ShouldBe(TimelineViewport.MinPixelsPerSecond, "5% is below the clamp, so the clamp is the last notch");
    }

    [Theory]
    [InlineData(103, +1, 150)]
    [InlineData(103, -1, 100)]
    [InlineData(87, +1, 100)]
    [InlineData(87, -1, 75)]
    [InlineData(9, +1, 10)]
    public void StepFromANonPresetZoom_GoesToTheNearestPresetInThatDirection(double from, int direction, double expected)
    {
        var viewport = At(from);

        Step(viewport, direction);

        Percent(viewport).ShouldBe(expected, 1e-9);
    }

    [Fact]
    public void StepOutAtTheMinimum_AndInAtTheMaximum_StayPut()
    {
        var viewport = new TimelineViewport();
        StepMany(viewport, -1, 40);
        viewport.PixelsPerSecond.ShouldBe(TimelineViewport.MinPixelsPerSecond);

        StepMany(viewport, +1, 40);
        viewport.PixelsPerSecond.ShouldBe(TimelineViewport.MaxPixelsPerSecond, "3200% is past the clamp, so the clamp is the last notch");
        Step(viewport, +1);
        viewport.PixelsPerSecond.ShouldBe(TimelineViewport.MaxPixelsPerSecond);
    }

    [Fact]
    public void StepInFromTheMaximumClamp_GoesBackToTheNeighborPreset()
    {
        var viewport = new TimelineViewport();
        StepMany(viewport, +1, 40);

        Step(viewport, -1);

        Percent(viewport).ShouldBe(1600, 1e-9);
    }

    [Theory]
    [InlineData(0, +1)]
    [InlineData(0, -1)]
    [InlineData(5, +1)]
    [InlineData(5, -1)]
    [InlineData(40, +1)]
    [InlineData(40, -1)]
    public void EveryStep_KeepsTheTimeUnderThePointerWhereItWas(double scrollSeconds, int direction)
    {
        var viewport = new TimelineViewport();
        viewport.SetScrollSeconds(scrollSeconds);

        for (int i = 0; i < 6; i++)
        {
            double before = viewport.TimeAt(Anchor);
            Step(viewport, direction);
            // Zooming out near the start clamps the scroll at 0, which moves the anchor: the one exception.
            if (viewport.ScrollSeconds > 0) viewport.TimeAt(Anchor).ShouldBe(before, 1e-9);
        }
    }

    [Fact]
    public void The100PercentLevel_IsAPreset()
    {
        ZoomPresetStepper.Presets.ShouldContain(100);
    }
}
