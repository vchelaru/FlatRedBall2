using AnimationEditor.App.Services;
using AnimationEditor.Core.Keyframes;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Draws several sprites at once, each from its own texture, around an entity origin: the sequence preview, and (with
/// <see cref="FitToBounds"/>) a library chip. Reuses <see cref="PreviewControl.DrawFrameCore"/> per layer.
/// Spike: no pan, guides, rulers or shapes.
/// </summary>
public sealed class SequenceCanvasControl : Control
{
    private IReadOnlyList<SequenceLayer> _layers = Array.Empty<SequenceLayer>();

    public ThumbnailService? Thumbnails { get; set; }

    /// <summary>Scale-to-fit the first layer, ignoring offsets. For chips.</summary>
    public bool FitToBounds { get; set; }

    public float Zoom { get; set; } = 2f;

    public bool ShowOrigin { get; set; } = true;

    public IReadOnlyList<SequenceLayer> Layers
    {
        get => _layers;
        set
        {
            _layers = value;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        float width = (float)Bounds.Width, height = (float)Bounds.Height;
        if (width <= 0 || height <= 0 || Thumbnails is null) return;

        var items = new List<DrawItem>();
        foreach (var layer in _layers)
        {
            if (Thumbnails.GetImage(layer.TexturePath) is not { } image) continue;
            var sample = KeyframeSampler.Sample(layer.Chain, layer.FrameIndex, layer.ElapsedInFrame);
            float sign = layer.Track.FlipHorizontal ? -1 : 1;
            var m = FlipScaleCalculator.ComputeMatrix(layer.Frame.FlipHorizontal, layer.Frame.FlipVertical, layer.Frame.FlipDiagonal);
            // The track mirror composes on top of the frame's own flip: negate the first row.
            (float, float, float, float)? flip = layer.Track.FlipHorizontal ? (-m.a, -m.b, m.c, m.d) : null;
            items.Add(new DrawItem(layer.Frame, sample.Color, image,
                layer.Track.X + sign * sample.OffsetX, layer.Track.Y + sample.OffsetY, flip));
        }

        float zoom = Zoom;
        if (FitToBounds && items.Count > 0)
        {
            var (_, _, sw, sh) = PreviewControl.ComputeSourceRect(items[0].Frame, items[0].Image.Width, items[0].Image.Height);
            zoom = Math.Min(4f, Math.Min((width - 4) / Math.Max(1, sw), (height - 4) / Math.Max(1, sh)));
            items[0] = items[0] with { X = 0, Y = 0 };
            items.RemoveRange(1, items.Count - 1);
        }

        context.Custom(new DrawOp(new Rect(0, 0, width, height), items, zoom, FitToBounds ? false : ShowOrigin));
    }

    private sealed record DrawItem(AnimationFrameSave Frame, ResolvedFrameColor Color, SKImage Image, float X, float Y,
        (float a, float b, float c, float d)? Flip);

    private sealed class DrawOp(Rect bounds, List<DrawItem> items, float zoom, bool showOrigin) : ICustomDrawOperation
    {
        public Rect Bounds => bounds;
        public bool Equals(ICustomDrawOperation? other) => false;
        public bool HitTest(Point p) => true;
        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature is null) return;
            using var lease = feature.Lease();
            var canvas = lease.SkCanvas;
            float cx = (float)bounds.Width / 2, cy = (float)bounds.Height / 2;
            canvas.Save();
            canvas.ClipRect(new SKRect(0, 0, (float)bounds.Width, (float)bounds.Height));
            canvas.Clear(new SKColor(0x1b, 0x1d, 0x20));
            foreach (var item in items)
            {
                PreviewControl.DrawFrameCore(canvas, item.Frame, item.Color, item.Image,
                    cx + item.X * zoom, cy - item.Y * zoom, zoom, alpha: 1f, showOutline: false,
                    gpuContext: lease.GrContext, flipMatrixOverride: item.Flip);
            }
            if (showOrigin)
            {
                using var paint = new SKPaint { Color = new SKColor(255, 255, 255, 60), StrokeWidth = 1 };
                canvas.DrawLine(cx, 0, cx, (float)bounds.Height, paint);
                canvas.DrawLine(0, cy, (float)bounds.Width, cy, paint);
            }
            canvas.Restore();
        }
    }
}
