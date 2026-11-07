using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Two-way binding between a <see cref="SequenceTimelineControl"/> and a pair of <c>ScrollBar</c>s. The bars are laid
/// out beside the control (a column and a row of the host grid), so the control's <c>Bounds</c> stay exactly the
/// visible lanes: hit-testing, drop planning and the scroll limits never reserve room for a bar. Only the bars'
/// alignment with the lanes comes from <see cref="TimelineViewport"/>.
/// </summary>
public static class TimelineScrollBinder
{
    // A click on an arrow scrolls this far across, the wheel's step.
    private const double ArrowStepPixels = 60;

    public static void Attach(SequenceTimelineControl timeline, ScrollBar horizontal, ScrollBar vertical)
    {
        horizontal.Margin = new Thickness(TimelineViewport.HeaderWidth, 0, 0, 0);
        vertical.Margin = new Thickness(0, TimelineViewport.RulerHeight, 0, 0);
        bool suppress = false;

        horizontal.ValueChanged += (_, _) =>
        {
            if (suppress) return;
            suppress = true;
            timeline.ScrollToSeconds(horizontal.Value);
            suppress = false;
        };
        vertical.ValueChanged += (_, _) =>
        {
            if (suppress) return;
            suppress = true;
            timeline.ScrollToY(vertical.Value);
            suppress = false;
        };

        void Refresh()
        {
            if (suppress) return;
            suppress = true;
            var across = timeline.HorizontalScroll;
            Apply(horizontal, across, ArrowStepPixels / timeline.Viewport.PixelsPerSecond);
            horizontal.Visibility = ScrollBarVisibility.Visible;
            horizontal.IsEnabled = across.CanScroll;

            var down = timeline.VerticalScroll;
            Apply(vertical, down, TimelineViewport.LaneHeight);
            vertical.Visibility = down.CanScroll ? ScrollBarVisibility.Visible : ScrollBarVisibility.Hidden;
            suppress = false;
        }

        timeline.ViewChanged += Refresh;
        timeline.PropertyChanged += (_, e) =>
        {
            if (e.Property == Visual.BoundsProperty) Refresh();
        };
        Refresh();
    }

    // Order matters: Minimum and Maximum before Value, so RangeBase doesn't coerce it.
    private static void Apply(ScrollBar bar, TimelineScrollRange range, double smallChange)
    {
        bar.Minimum = 0;
        bar.Maximum = range.Maximum;
        bar.ViewportSize = range.ViewportSize;
        bar.SmallChange = smallChange;
        bar.LargeChange = range.ViewportSize;
        bar.Value = range.Value;
    }
}
