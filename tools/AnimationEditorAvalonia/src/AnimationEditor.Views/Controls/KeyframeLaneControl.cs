using AnimationEditor.Core.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AnimationEditor.Views.Controls;

/// <summary>
/// One keyframe lane under the timeline cells (#1478, #1489): the easing curve of every keyframe-to-keyframe
/// span (a stroked line over a faint fill), a solid hold line after the last keyframe of each run, faint ticks at
/// the frame boundaries, and an amber diamond on the left edge of each keyframe's frame. It only draws
/// a <see cref="KeyframeLane"/>; the lane's geometry is computed by <see cref="KeyframeLaneBuilder"/>.
/// It sits beside the cells' ScrollViewer rather than inside it so the first diamond can spill past the
/// strip's left edge, and follows the scroll through <see cref="ScrollOffset"/>.
/// </summary>
public sealed class KeyframeLaneControl : Control
{
    public static readonly StyledProperty<KeyframeLane?> LaneProperty =
        AvaloniaProperty.Register<KeyframeLaneControl, KeyframeLane?>(nameof(Lane));

    public static readonly StyledProperty<double> ScrollOffsetProperty =
        AvaloniaProperty.Register<KeyframeLaneControl, double>(nameof(ScrollOffset));

    private const double CurveInset = 3.0;
    private const int CurveSteps = 32;

    static KeyframeLaneControl()
    {
        AffectsRender<KeyframeLaneControl>(LaneProperty, ScrollOffsetProperty);
        HeightProperty.OverrideDefaultValue<KeyframeLaneControl>(KeyframeLaneBuilder.LaneHeight);
    }

    /// <summary>What to draw; <c>null</c> for nothing.</summary>
    public KeyframeLane? Lane
    {
        get => GetValue(LaneProperty);
        set => SetValue(LaneProperty, value);
    }

    /// <summary>The horizontal scroll offset of the cells this lane sits under.</summary>
    public double ScrollOffset
    {
        get => GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ActualThemeVariantChanged -= OnThemeChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnThemeChanged(object? sender, EventArgs e) => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        double h = KeyframeLaneBuilder.LaneHeight;
        context.FillRectangle(Brush("BgCanvas", Colors.Black), new Rect(0, 0, Bounds.Width, h));
        var line = Res("LineBrush", Colors.Gray);
        context.DrawLine(new Pen(new SolidColorBrush(line), 1), new Point(0, 0.5), new Point(Bounds.Width, 0.5));

        if (Lane is not { } lane) return;

        double clipLeft = KeyframeLaneBuilder.ClipLeft(ScrollOffset);
        using var clip = context.PushClip(new Rect(clipLeft, 0, Bounds.Width - clipLeft, h));
        using var scroll = context.PushTransform(Matrix.CreateTranslation(-ScrollOffset, 0));

        var amber = Res("AccentSoft", Colors.Goldenrod);
        var ink = Res("InkDim", Colors.Gray);
        double top = CurveInset, bottom = h - CurveInset;

        var tickPen = new Pen(new SolidColorBrush(line, 0.6), 1);
        foreach (double x in lane.TickXs)
            context.DrawLine(tickPen, new Point(x - 0.5, 1), new Point(x - 0.5, h));

        var curvePen = new Pen(new SolidColorBrush(amber, 0.85), 1.25, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var fillBrush = new SolidColorBrush(amber, 0.16);
        foreach (var span in lane.Spans)
        {
            double width = span.EndX - span.StartX;
            if (width <= 0) continue;
            var points = KeyframeLaneBuilder.CurvePoints(span.Curve, CurveSteps)
                .Select(p => new Point(span.StartX + p.T * width, bottom - p.Progress * (bottom - top)))
                .ToList();

            var fill = new StreamGeometry();
            using (var g = fill.Open())
            {
                g.BeginFigure(new Point(span.StartX, bottom), true);
                foreach (var p in points) g.LineTo(p);
                g.LineTo(new Point(span.EndX, bottom));
                g.EndFigure(true);
            }
            context.DrawGeometry(fillBrush, null, fill);

            var stroke = new StreamGeometry();
            using (var g = stroke.Open())
            {
                g.BeginFigure(points[0], false);
                foreach (var p in points.Skip(1)) g.LineTo(p);
                g.EndFigure(false);
            }
            context.DrawGeometry(null, curvePen, stroke);
        }

        var holdPen = CreateHoldPen(ink);
        foreach (var hold in lane.Holds)
        {
            if (hold.EndX > hold.StartX)
                context.DrawLine(holdPen, new Point(hold.StartX, top), new Point(hold.EndX, top));
        }

        var diamondBrush = new SolidColorBrush(amber);
        double r = KeyframeLaneBuilder.DiamondHalfWidth, cy = h / 2;
        foreach (double x in lane.KeyframeXs)
        {
            var diamond = new StreamGeometry();
            using (var g = diamond.Open())
            {
                g.BeginFigure(new Point(x, cy - r), true);
                g.LineTo(new Point(x + r, cy));
                g.LineTo(new Point(x, cy + r));
                g.LineTo(new Point(x - r, cy));
                g.EndFigure(true);
            }
            context.DrawGeometry(diamondBrush, null, diamond);
        }
    }

    /// <summary>The pen for a hold, the flat line after a run's last keyframe: solid, as a dashed line would say the same thing.</summary>
    public static Pen CreateHoldPen(Color ink) => new(new SolidColorBrush(ink, 0.9), 1.25);

    private Color Res(string key, Color fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is ISolidColorBrush b ? b.Color : fallback;

    private IBrush Brush(string key, Color fallback) => new SolidColorBrush(Res(key, fallback));
}
