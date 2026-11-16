using AnimationEditor.App.Services;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Draws one composite cell of a reference clip's filmstrip: the sprites of a <see cref="ScenePose"/> back to front, fitted
/// to a square the height of the film row and centred on their shared origin, as the sequence list's thumbnails fit them
/// (<see cref="ThumbnailLayoutCalculator"/>) with each frame's flips applied as the filmstrip atlas applies them. Synchronous,
/// from the shared decoded sheets; nothing here disposes a sheet.
/// </summary>
internal static class ReferenceCellRenderer
{
    private static readonly SKSamplingOptions Linear = new(SKFilterMode.Linear);

    /// <summary>The bitmap of <paramref name="pose"/> at most <paramref name="size"/> pixels square, or null when no sprite has a readable texture.</summary>
    public static SKBitmap? Render(ScenePose pose, int size, Func<string, SKBitmap?> sheetOf)
    {
        var sprites = new List<(PoseLayer Layer, SKBitmap Sheet)>(pose.Layers.Count);
        var sizes = new List<ThumbnailSpriteRect>(pose.Layers.Count);
        foreach (var layer in pose.Layers)
        {
            if (string.IsNullOrEmpty(layer.Texture) || sheetOf(layer.Texture) is not { } sheet) continue;
            float w = Math.Abs(layer.Right - layer.Left) * sheet.Width, h = Math.Abs(layer.Bottom - layer.Top) * sheet.Height;
            if (w <= 0 || h <= 0) continue;
            sprites.Add((layer, sheet));
            sizes.Add(layer.FlipD ? new ThumbnailSpriteRect(h, w, layer.OffsetX, layer.OffsetY) : new ThumbnailSpriteRect(w, h, layer.OffsetX, layer.OffsetY));
        }
        if (sprites.Count == 0 || ThumbnailLayoutCalculator.Fit(sizes, size, size) is not { } layout) return null;

        var composite = new SKBitmap(layout.Width, layout.Height);
        using var canvas = new SKCanvas(composite);
        canvas.Clear(SKColors.Transparent);
        for (int i = 0; i < sprites.Count; i++)
        {
            var (layer, sheet) = sprites[i];
            var place = layout.Placements[i];
            int w = Math.Max(1, (int)Math.Round(place.Width)), h = Math.Max(1, (int)Math.Round(place.Height));
            var plain = new AnimationFrameSave
            {
                LeftCoordinate = layer.Left, RightCoordinate = layer.Right, TopCoordinate = layer.Top, BottomCoordinate = layer.Bottom,
            };
            // The unflipped crop; a diagonal flip turns it a quarter, so it is cut at the swapped size.
            using var thumb = ThumbnailService.RenderFrameThumbnail(sheet, plain, layer.Color, layer.FlipD ? h : w, layer.FlipD ? w : h);
            if (thumb is null) continue;
            using var image = SKImage.FromBitmap(thumb);
            if (!(layer.FlipH || layer.FlipV || layer.FlipD))
            {
                canvas.DrawImage(image, SKRect.Create(place.X, place.Y, place.Width, place.Height), Linear);
                continue;
            }
            var (a, b, c, d) = FlipScaleCalculator.ComputeMatrix(layer.FlipH, layer.FlipV, layer.FlipD);
            float sourceX = thumb.Width / 2f, sourceY = thumb.Height / 2f;
            float destX = place.X + place.Width / 2f, destY = place.Y + place.Height / 2f;
            canvas.Save();
            canvas.SetMatrix(new SKMatrix(
                a, b, destX - (a * sourceX + b * sourceY),
                c, d, destY - (c * sourceX + d * sourceY),
                0, 0, 1));
            canvas.DrawImage(image, 0, 0, Linear);
            canvas.Restore();
        }
        return composite;
    }
}
