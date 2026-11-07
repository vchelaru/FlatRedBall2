using AnimationEditor.Core.Sequences;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class TimelineScrollBarsTests
{
    private const double Header = TimelineViewport.HeaderWidth;

    [Fact]
    public void Horizontal_LongSequence_ExtentIsLengthPlusMarginMinusTheView()
    {
        double pps = 100;
        double view = 600;
        double length = 20;

        var range = TimelineScrollBars.Horizontal(0, pps, Header + view, length);

        range.ViewportSize.ShouldBe(view / pps, 1e-9);
        range.Maximum.ShouldBe(length + TimelineScrollBars.EndMarginPixels / pps - view / pps, 1e-9);
        range.Value.ShouldBe(0);
        range.CanScroll.ShouldBeTrue();
    }

    [Fact]
    public void Horizontal_ValueIsTheScrollOffset()
    {
        var range = TimelineScrollBars.Horizontal(7.5, 100, Header + 600, 20);

        range.Value.ShouldBe(7.5);
    }

    [Fact]
    public void Horizontal_ZoomingIn_GrowsTheExtentInPixels_AndShrinksTheThumb()
    {
        var wide = TimelineScrollBars.Horizontal(0, 100, Header + 600, 20);
        var zoomed = TimelineScrollBars.Horizontal(0, 400, Header + 600, 20);

        double widePixels = (wide.Maximum + wide.ViewportSize) * 100;
        double zoomedPixels = (zoomed.Maximum + zoomed.ViewportSize) * 400;
        zoomedPixels.ShouldBeGreaterThan(widePixels);
        zoomed.ThumbFraction.ShouldBeLessThan(wide.ThumbFraction);
    }

    [Fact]
    public void Horizontal_SequenceShorterThanTheView_CannotScroll()
    {
        var range = TimelineScrollBars.Horizontal(0, 100, Header + 1200, 2);

        range.Maximum.ShouldBe(0);
        range.CanScroll.ShouldBeFalse();
        range.ThumbFraction.ShouldBe(1, 1e-9);
    }

    [Fact]
    public void Horizontal_ZeroLengthSequence_CannotScroll()
    {
        var range = TimelineScrollBars.Horizontal(0, 120, Header + 800, 0);

        range.CanScroll.ShouldBeFalse();
        range.Value.ShouldBe(0);
    }

    [Fact]
    public void Horizontal_AtMaxZoom_StaysFiniteAndTheThumbStaysOnTheTrack()
    {
        double length = 3;
        var range = TimelineScrollBars.Horizontal(1.0, TimelineViewport.MaxPixelsPerSecond, Header + 800, length);

        range.ViewportSize.ShouldBe(800 / TimelineViewport.MaxPixelsPerSecond, 1e-9);
        range.Maximum.ShouldBeGreaterThan(0);
        range.Value.ShouldBeInRange(0, range.Maximum);
        range.ThumbFraction.ShouldBeInRange(0, 1);
    }

    [Fact]
    public void Horizontal_ScrolledPastTheEnd_KeepsTheThumbOnTheTrack()
    {
        var range = TimelineScrollBars.Horizontal(500, 100, Header + 600, 20);

        range.Value.ShouldBe(500);
        range.Maximum.ShouldBeGreaterThanOrEqualTo(500);
    }

    [Fact]
    public void Horizontal_ControlNarrowerThanTheHeader_HasNoViewAndNoNaN()
    {
        var range = TimelineScrollBars.Horizontal(0, 100, Header - 10, 5);

        double.IsNaN(range.ThumbFraction).ShouldBeFalse();
        range.ViewportSize.ShouldBe(0);
    }

    [Fact]
    public void Vertical_FewLanes_CannotScroll()
    {
        var viewport = new TimelineViewport();
        viewport.SetLaneHeights(new[] { 48.0, 48.0 });
        double content = viewport.ContentHeight(2);

        var range = TimelineScrollBars.Vertical(0, content, content + 40);

        range.CanScroll.ShouldBeFalse();
        range.Maximum.ShouldBe(0);
    }

    [Fact]
    public void Vertical_ManyLanesWithAnEventRow_ExtentFollowsTheContentHeight()
    {
        var viewport = new TimelineViewport();
        viewport.SetLaneHeights(new[] { 60.0, 48.0, 60.0, 48.0, 48.0 });
        double content = viewport.ContentHeight(5);
        double controlHeight = 200;

        var range = TimelineScrollBars.Vertical(30, content, controlHeight);

        range.ViewportSize.ShouldBe(controlHeight - TimelineViewport.RulerHeight);
        range.Maximum.ShouldBe(content - controlHeight);
        range.Value.ShouldBe(30);
        range.CanScroll.ShouldBeTrue();
        // The viewport's own vertical clamp agrees with the bar's maximum.
        viewport.ScrollVertically(10_000, 5, controlHeight);
        viewport.ScrollY.ShouldBe(range.Maximum);
    }

    [Fact]
    public void Vertical_ControlShorterThanTheRuler_StaysFinite()
    {
        var range = TimelineScrollBars.Vertical(0, 300, 10);

        range.ViewportSize.ShouldBe(0);
        double.IsNaN(range.ThumbFraction).ShouldBeFalse();
    }
}
