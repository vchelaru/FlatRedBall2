using System.Globalization;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AnimationEditor.Views.Controls;

/// <summary>
/// The event flag on a chain timeline frame cell, at the left edge of the cell, in the index label row: the sequence timeline's flag (<see cref="EventFlag"/>), or its count
/// pill when the frame has two or more events. Pointer presses bubble up unhandled to the strip's scrub surface; it is hit-testable only so its tooltip can show.
/// </summary>
public sealed class EventFlagGlyph : Control
{
    public static readonly StyledProperty<string?> CountTextProperty =
        AvaloniaProperty.Register<EventFlagGlyph, string?>(nameof(CountText));

    /// <summary>The number to show in a pill; blank or null draws the plain flag.</summary>
    public string? CountText
    {
        get => GetValue(CountTextProperty);
        set => SetValue(CountTextProperty, value);
    }

    static EventFlagGlyph()
    {
        AffectsRender<EventFlagGlyph>(CountTextProperty);
    }

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
        var ink = Resolve("Ink");
        var canvas = Resolve("BgCanvas");
        var cool = Resolve("AccentCool");
        var poleColor = cool is ISolidColorBrush solid ? new SolidColorBrush(solid.Color, 0.9) : cool;
        double poleX = 0.5;
        context.DrawLine(new Pen(poleColor, 1), new Point(poleX, 0), new Point(poleX, Height));

        if (string.IsNullOrEmpty(CountText))
        {
            EventFlag.DrawTriangle(context, poleX, 1, ink, canvas);
            return;
        }

        var count = new FormattedText(CountText, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, 8, canvas) { };
        double width = System.Math.Max(EventMarkers.MarkerPixels, count.Width + 5);
        var pill = new Rect(poleX, 1, width, Height - 2);
        context.FillRectangle(ink, pill, 4);
        context.DrawText(count, new Point(pill.X + (pill.Width - count.Width) / 2, pill.Y + (pill.Height - count.Height) / 2));
    }
}
