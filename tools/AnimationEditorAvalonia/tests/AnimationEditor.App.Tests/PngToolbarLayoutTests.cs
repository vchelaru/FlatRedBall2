using AnimationEditor.App.Controls;
using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// The PNG diff toolbar (#606) must match the wireframe/preview toolbars in two ways: every control —
/// the Grouping knob and the zoom widget — stacks from the left in one panel (not the zoom pinned to
/// the far right), and the bar is the same height as the .achx editor's toolbar. The height is the
/// subtle one: the Fluent Slider reserves a 50px layout box around a 20px thumb and doesn't re-center
/// the thumb when forced shorter, so a naive Height clips it while its natural size makes the bar too
/// tall. The fix trims the excess with a negative margin; this test pins both the left-stacking and
/// the height parity so neither regresses.
/// </summary>
public class PngToolbarLayoutTests
{
    [AvaloniaFact]
    public void PngToolbar_StacksZoomLeft_AndMatchesAchxToolbarHeight()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Width = 1400;
        window.Height = 900;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            // Reference: the .achx toolbar height (the bar that hosts the wireframe zoom).
            var wfZoom = window.FindControl<ZoomControl>("WireframeZoom")
                ?? throw new InvalidOperationException("WireframeZoom not found");
            double achxBarHeight = wfZoom.GetVisualAncestors().OfType<Border>().First().Bounds.Height;

            // Reveal the PNG pane so its toolbar lays out (it's collapsed by default).
            var pngGrid = window.FindControl<Grid>("PngPaneGrid")
                ?? throw new InvalidOperationException("PngPaneGrid not found");
            pngGrid.IsVisible = true;
            Dispatcher.UIThread.RunJobs();

            var slider = window.FindControl<Slider>("DiffGroupingSlider")
                ?? throw new InvalidOperationException("DiffGroupingSlider not found");
            var zoom = window.FindControl<ZoomControl>("PngZoom")
                ?? throw new InvalidOperationException("PngZoom not found");

            // Left-anchored: the zoom widget shares the grouping slider's container (a WrapPanel, as
            // the .achx toolbars use) and sits after it, so both flow from the left — not the
            // right-pushed column the toolbar used before.
            Assert.IsType<WrapPanel>(slider.Parent);
            Assert.Same(slider.Parent, zoom.Parent);
            var panel = (WrapPanel)slider.Parent!;
            Assert.True(panel.Children.IndexOf(zoom) > panel.Children.IndexOf(slider),
                "PngZoom should sit to the right of the Grouping slider within the same left-anchored panel");

            // No forced Height: the slider keeps its natural (thumb-centered, unclipped) height and is
            // trimmed via margin, not squished via Height (which clips the thumb).
            Assert.True(double.IsNaN(slider.Height),
                $"DiffGroupingSlider must keep its natural height; found Height={slider.Height}");

            // Height parity: the PNG bar must be the same height as the .achx toolbar. This is what
            // catches the negative-margin value drifting if the Fluent slider's box height changes.
            double pngBarHeight = zoom.GetVisualAncestors().OfType<Border>().First().Bounds.Height;
            Assert.True(Math.Abs(pngBarHeight - achxBarHeight) <= 1.0,
                $"PNG toolbar height {pngBarHeight} should match the .achx toolbar height {achxBarHeight}");
        }
        finally { window.Close(); }
    }
}
