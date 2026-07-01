using SkiaSharp;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Shared draw-time diagnostics overlay (#514): renders a rolling-average ms/frame + approximate
/// fps readout in the top-left corner of a Skia canvas. Used by both <see cref="PreviewControl"/>
/// and <see cref="WireframeControl"/>, each gated by its own <c>ShowDrawDiagnostics</c> toggle so
/// the cost only lands where you're profiling. Thin Skia wiring — the averaging math it displays
/// is covered by <c>RollingAverageTests</c>.
/// </summary>
internal static class DrawTimeOverlay
{
    public static void Draw(SKCanvas canvas, double avgMs, string? note = null)
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
