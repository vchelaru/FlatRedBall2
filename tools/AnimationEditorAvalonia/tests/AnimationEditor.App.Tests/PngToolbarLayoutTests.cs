using AnimationEditor.App.Controls;
using System;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// The PNG diff toolbar (#606) must match the wireframe/preview toolbars: every control — the
/// Grouping knob and the zoom widget — stacks from the left in one panel, rather than the zoom being
/// pushed to the far right. And the Grouping slider must keep its natural (theme) height: an explicit
/// height shorter than the Fluent thumb clips it, and the clipped thumb then overflows under the
/// canvas below. Both are structural, so they hold even while the collapsed pane isn't laid out.
/// </summary>
public class PngToolbarLayoutTests
{
    [AvaloniaFact]
    public void PngToolbar_ZoomStacksLeftWithGrouping_AndSliderKeepsNaturalHeight()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        try
        {
            var slider = window.FindControl<Slider>("DiffGroupingSlider")
                ?? throw new InvalidOperationException("DiffGroupingSlider not found");
            var zoom = window.FindControl<ZoomControl>("PngZoom")
                ?? throw new InvalidOperationException("PngZoom not found");

            // Left-anchored: the zoom widget shares the grouping slider's container (a WrapPanel,
            // as the .achx toolbars use) and sits after it, so both flow from the left — not the
            // right-pushed column the toolbar used before.
            Assert.IsType<WrapPanel>(slider.Parent);
            Assert.Same(slider.Parent, zoom.Parent);
            var panel = (WrapPanel)slider.Parent!;
            Assert.True(panel.Children.IndexOf(zoom) > panel.Children.IndexOf(slider),
                "PngZoom should sit to the right of the Grouping slider within the same left-anchored panel");

            // Natural height: forcing an explicit Height below the Fluent thumb's design size clips it.
            Assert.True(double.IsNaN(slider.Height),
                $"DiffGroupingSlider must keep its natural height so the thumb isn't clipped; found Height={slider.Height}");
        }
        finally { window.Close(); }
    }
}
