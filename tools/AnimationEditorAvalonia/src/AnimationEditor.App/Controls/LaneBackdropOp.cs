using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
using System;

namespace AnimationEditor.App.Controls;

/// <summary>One lane of the backdrop: where it sits, how tall it is, and whether it takes the stripe color.</summary>
internal readonly record struct LaneBand(double Top, double Height, bool Striped);

/// <summary>
/// The timeline's backdrop in one operation: the alternate-lane stripes, the line under each lane and the vertical
/// grid lines. That is about eighty draw nodes a frame through the drawing context; here it is rectangles in one batch.
/// Positions are layout units under Avalonia's own transform and clip, the same geometry a one pixel stroke would cover.
/// </summary>
internal sealed class LaneBackdropOp : ICustomDrawOperation
{
    private readonly LaneBand[] _lanes;
    private readonly double[] _gridX;
    private readonly double _header, _ruler, _width, _height;
    private readonly SKColor _stripe, _line;

    public LaneBackdropOp(LaneBand[] lanes, double[] gridX, double header, double ruler, double width, double height, SKColor stripe, SKColor line)
    {
        _lanes = lanes;
        _gridX = gridX;
        _header = header;
        _ruler = ruler;
        _width = width;
        _height = height;
        _stripe = stripe;
        _line = line;
        Bounds = new Rect(header, ruler, Math.Max(0, width - header), Math.Max(0, height - ruler));
    }

    public Rect Bounds { get; }
    public bool HitTest(Point p) => false;
    public bool Equals(ICustomDrawOperation? other) => false;
    public void Dispose() { }

    public void Render(ImmediateDrawingContext context)
    {
        var lease = context.TryGetFeature<ISkiaSharpApiLeaseFeature>()?.Lease();
        if (lease is null) return;
        using (lease)
        {
            var canvas = lease.SkCanvas;
            using var stripe = new SKPaint { IsAntialias = true, Color = _stripe };
            foreach (var lane in _lanes)
                if (lane.Striped)
                    canvas.DrawRect(new SKRect((float)_header, (float)lane.Top, (float)_width, (float)(lane.Top + lane.Height)), stripe);
            using var line = new SKPaint { IsAntialias = true, Color = _line };
            foreach (var lane in _lanes)
            {
                float y = (float)(lane.Top + lane.Height - 0.5);
                canvas.DrawRect(new SKRect((float)_header, y - 0.5f, (float)_width, y + 0.5f), line);
            }
            foreach (double x in _gridX)
                canvas.DrawRect(new SKRect((float)(x - 0.5), (float)_ruler, (float)(x + 0.5), (float)_height), line);
        }
    }
}
