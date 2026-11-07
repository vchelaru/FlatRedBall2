using SkiaSharp;
using System;

namespace AnimationEditor.App.Controls;

/// <summary>
/// The open-hand and closed-hand cursors drawn in code, because Windows has no stock grab cursor (<c>IDC_HAND</c> is the
/// pointing link hand). A dark fill inside a white outline reads on light and dark backgrounds. Drawn in a 32 unit box
/// and scaled to <c>size</c> pixels, so any display scale stays sharp.
/// </summary>
internal static class GrabCursorArt
{
    private const float Box = 32;
    private static readonly SKColor Fill = new(0x1B, 0x1B, 0x1F);
    private const float OutlineUnits = 3;

    /// <summary>The pixel the pointer's position maps to: the middle of the hand.</summary>
    public static (int X, int Y) HotspotFor(int size) => (size / 2, size / 2);

    /// <summary>Draws the hand into a new premultiplied <paramref name="size"/> by <paramref name="size"/> bitmap.</summary>
    public static SKBitmap Render(bool closed, int size)
    {
        var bitmap = new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(size / Box);
        if (closed) DrawClosed(canvas); else DrawOpen(canvas);
        return bitmap;
    }

    /// <summary>The bitmap as straight (not premultiplied) BGRA bytes, which is what a Windows alpha cursor wants.</summary>
    public static byte[] ToStraightBgra(SKBitmap bitmap)
    {
        var bytes = new byte[bitmap.Width * bitmap.Height * 4];
        int i = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var c = bitmap.GetPixel(x, y); // SkiaSharp returns straight color
                bytes[i++] = c.Blue;
                bytes[i++] = c.Green;
                bytes[i++] = c.Red;
                bytes[i++] = c.Alpha;
            }
        return bytes;
    }

    private static void DrawOpen(SKCanvas canvas)
    {
        float[] tops = { 8, 5.5f, 5, 7 };
        void Shapes(SKPaint paint)
        {
            canvas.DrawRoundRect(new SKRect(10, 15, 24, 27), 3.5f, 3.5f, paint);
            for (int i = 0; i < 4; i++)
            {
                float left = 10.2f + 3.45f * i;
                canvas.DrawRoundRect(new SKRect(left, tops[i], left + 3.2f, 19), 1.6f, 1.6f, paint);
            }
            canvas.Save();
            canvas.RotateDegrees(-35, 8.5f, 20.5f);
            canvas.DrawRoundRect(new SKRect(6.8f, 16, 10.2f, 25), 1.7f, 1.7f, paint);
            canvas.Restore();
        }
        Outlined(Shapes);
    }

    private static void DrawClosed(SKCanvas canvas)
    {
        void Shapes(SKPaint paint)
        {
            canvas.DrawRoundRect(new SKRect(8, 15, 24, 26), 4, 4, paint);
            for (int i = 0; i < 4; i++) canvas.DrawCircle(10.4f + 3.8f * i, 14.5f, 2.1f, paint);
        }
        Outlined(Shapes);

        // Curled fingers: thin white creases between the knuckles, and the thumb laid across them.
        using var crease = new SKPaint { Color = SKColors.White, IsAntialias = true, StrokeWidth = 0.9f, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
        for (int i = 0; i < 3; i++) canvas.DrawLine(12.3f + 3.8f * i, 15.5f, 12.3f + 3.8f * i, 20, crease);
        using var thumbOutline = new SKPaint { Color = SKColors.White, IsAntialias = true, Style = SKPaintStyle.StrokeAndFill, StrokeWidth = 1.6f };
        using var thumb = new SKPaint { Color = Fill, IsAntialias = true };
        var thumbRect = new SKRect(8.6f, 20.2f, 17.2f, 24.2f);
        canvas.DrawRoundRect(thumbRect, 2, 2, thumbOutline);
        canvas.DrawRoundRect(thumbRect, 2, 2, thumb);
    }

    // Every shape gets a wide white stroke first, then the dark fill, so the union has one clean outline.
    private static void Outlined(Action<SKPaint> shapes)
    {
        using var outline = new SKPaint { Color = SKColors.White, IsAntialias = true, Style = SKPaintStyle.StrokeAndFill, StrokeWidth = OutlineUnits };
        using var fill = new SKPaint { Color = Fill, IsAntialias = true };
        shapes(outline);
        shapes(fill);
    }
}
