using AnimationEditor.App.Controls;
using AnimationEditor.Core.Diff;
using Avalonia;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// PNG diff-region reveal bounce (#606 / #803). The curve is covered by Core
/// <c>RevealAnimation</c> tests; these cover the control host: <c>frame: true</c> starts the
/// reveal, <c>frame: false</c> does not, and settle lands at rest.
/// </summary>
public class PngDiffRevealTests
{
    private static PngPreviewControl MakeControl()
    {
        var ctrl = new PngPreviewControl();
        ctrl.Measure(new Size(400, 300));
        ctrl.Arrange(new Rect(0, 0, 400, 300));
        return ctrl;
    }

    private static readonly PixelRegion[] OneRegion =
    {
        new(10, 10, 20, 20, ChangedPixelCount: 4),
    };

    [AvaloniaFact]
    public void SetDiffRegions_FrameFalse_DoesNotStartReveal()
    {
        var ctrl = MakeControl();

        ctrl.SetDiffRegions(OneRegion, frame: false);

        Assert.False(ctrl.IsDiffRevealAnimating);
        Assert.Equal(1f, ctrl.DiffRevealProgress);
    }

    [AvaloniaFact]
    public void SetDiffRegions_FrameTrue_StartsRevealAtZero()
    {
        var ctrl = MakeControl();

        ctrl.SetDiffRegions(OneRegion, frame: true);

        Assert.True(ctrl.IsDiffRevealAnimating);
        Assert.Equal(0f, ctrl.DiffRevealProgress);
    }

    [AvaloniaFact]
    public void SettleDiffReveal_AfterFrameTrue_LandsAtRestAndStops()
    {
        var ctrl = MakeControl();
        ctrl.SetDiffRegions(OneRegion, frame: true);

        ctrl.SettleDiffReveal();

        Assert.False(ctrl.IsDiffRevealAnimating);
        Assert.Equal(1f, ctrl.DiffRevealProgress);
    }
}
