using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AnimationEditor.Views.Controls;

/// <summary>
/// The event flag on a chain timeline frame cell, at the left edge of the cell in the index label row: the sequence
/// timeline's flag (<see cref="EventFlag"/>), the same for one event or many. Pointer presses bubble up unhandled to the
/// strip's scrub surface; it is hit-testable only so its tooltip can show.
/// </summary>
public sealed class EventFlagGlyph : Control
{
    public EventFlagGlyph()
    {
        Width = EventMarkers.MarkerPixels + 2;
        Height = 12;
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    private IBrush Resolve(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Gray;

    public override void Render(DrawingContext context)
    {
        var cool = Resolve("AccentCool");
        var poleColor = cool is ISolidColorBrush solid ? new SolidColorBrush(solid.Color, 0.9) : cool;
        double poleX = 0.5;
        context.DrawLine(new Pen(poleColor, 1), new Point(poleX, 0), new Point(poleX, Height));
        EventFlag.DrawTriangle(context, poleX, 1, Resolve("Ink"), Resolve("BgCanvas"));
    }
}
