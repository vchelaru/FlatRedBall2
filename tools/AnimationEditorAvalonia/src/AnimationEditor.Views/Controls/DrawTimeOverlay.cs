using AnimationEditor.Core.Rendering;
using Avalonia.Skia;
using SkiaSharp;
using System;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Shared draw-time diagnostics overlay (#514): renders a rolling-average ms/frame + approximate
/// fps readout in the top-left corner of a Skia canvas. Used by both <see cref="PreviewControl"/>
/// and <see cref="TextureViewport"/> via <see cref="TimeAndDraw"/>, each gated by its own
/// <see cref="DiagnosticsOverlayHost"/> so the cost only lands where you're profiling. Thin Skia
/// wiring — the averaging math it displays is covered by <c>RollingAverageTests</c>.
/// </summary>
internal static class DrawTimeOverlay
{
    /// <summary>
    /// Runs <paramref name="render"/> against the lease's canvas and, when
    /// <paramref name="sampler"/> is non-null, times it and draws the readout on top. Every
    /// <c>ICustomDrawOperation</c> that wants diagnostics goes through here so the timing scope and
    /// the GPU/CPU backend label are decided in one place instead of per panel.
    /// </summary>
    public static void TimeAndDraw(ISkiaSharpApiLease lease, RollingAverage? sampler, Action<SKCanvas> render)
    {
        if (sampler is null)
        {
            render(lease.SkCanvas);
            return;
        }

        // Time only the Skia render — it runs on the compositor/render thread, where the frame cost
        // actually lands (the UI-thread Render() just builds the snapshot).
        var sw = System.Diagnostics.Stopwatch.StartNew();
        render(lease.SkCanvas);
        sw.Stop();
        sampler.Add(sw.Elapsed.TotalMilliseconds);
        // GrContext is non-null only on the GPU (ANGLE) backend; null = software raster.
        Draw(lease.SkCanvas, sampler.Average, lease.GrContext != null ? "GPU" : "CPU");
    }

    private static void Draw(SKCanvas canvas, double avgMs, string? note = null)
    {
        string baseText = avgMs > 0
            ? $"draw: {avgMs:F2} ms  (~{1000.0 / avgMs:F0} fps)"
            : "draw: —";
        string text = note is null ? baseText : $"{baseText}  [{note}]";

        using var font = new SKFont { Size = 12f };
        float textW = font.MeasureText(text);
        var box = new SKRect(4f, 4f, 4f + textW + 12f, 4f + 20f);

        using var bg = new SKPaint { Color = new SKColor(0, 0, 0, 210) };
        canvas.DrawRect(box, bg);
        using var fg = new SKPaint { Color = new SKColor(0, 255, 80), IsAntialias = true };
        canvas.DrawText(text, box.Left + 6f, box.Bottom - 6f, font, fg);
    }
}
