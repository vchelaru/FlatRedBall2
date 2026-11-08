using AnimationEditor.Core.IO;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AnimationEditor.App.Services;

/// <summary>
/// Draws the composite thumbnail of a sequence: its sprites (see <see cref="SequenceThumbnailScene"/>) in lane order, the top
/// lane in front, fitted into a maximum size and centred on the sprites' shared origin. The one renderer behind the Open
/// Project Folder <c>.seqj</c> rows and the sequence list rows. A single sprite draws exactly like an <c>.achx</c> row's
/// first-frame thumbnail (<see cref="ThumbnailService.RenderFrameThumbnail"/>), so the two kinds of row match.
/// </summary>
internal static class SequenceThumbnailRenderer
{
    /// <summary>
    /// Renders <paramref name="layers"/> into at most <paramref name="maxWidth"/> by <paramref name="maxHeight"/> pixels. A
    /// layer whose texture is missing or cannot be decoded is left out; null when nothing is left to draw.
    /// </summary>
    /// <param name="framesArePixels">The layers' frames still hold pixel coordinates (a source read without the editor's
    /// UV conversion), converted here from each texture's size.</param>
    /// <param name="decode">Reads and decodes a layer's <see cref="SequenceThumbnailLayer.TexturePath"/>; null for a missing file.</param>
    public static async Task<SKBitmap?> RenderAsync(
        IReadOnlyList<SequenceThumbnailLayer> layers, bool framesArePixels, Func<string, Task<SKBitmap?>> decode,
        int maxWidth, int maxHeight)
    {
        var textures = new Dictionary<string, SKBitmap?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var drawn = new List<(SKBitmap Texture, AnimationFrameSave Frame, SequenceThumbnailLayer Layer)>();
            foreach (var layer in layers)
            {
                if (string.IsNullOrEmpty(layer.TexturePath)) continue;
                if (!textures.TryGetValue(layer.TexturePath, out var texture))
                    textures[layer.TexturePath] = texture = await decode(layer.TexturePath);
                if (texture is null) continue;
                var frame = framesArePixels ? PixelFrameUvConverter.ToUv(layer.Frame, texture.Width, texture.Height) : layer.Frame;
                drawn.Add((texture, frame, layer));
            }
            if (drawn.Count == 0) return null;

            if (drawn.Count == 1)
                return ThumbnailService.RenderFrameThumbnail(drawn[0].Texture, drawn[0].Frame, drawn[0].Layer.Color, maxWidth, maxHeight);

            var sizes = new List<ThumbnailSpriteRect>();
            foreach (var (texture, frame, layer) in drawn)
            {
                float w = (frame.RightCoordinate - frame.LeftCoordinate) * texture.Width;
                float h = (frame.BottomCoordinate - frame.TopCoordinate) * texture.Height;
                sizes.Add(new ThumbnailSpriteRect(w, h, layer.OffsetX, layer.OffsetY));
            }
            if (ThumbnailLayoutCalculator.Fit(sizes, maxWidth, maxHeight) is not { } layout) return null;

            var composite = new SKBitmap(layout.Width, layout.Height);
            using var canvas = new SKCanvas(composite);
            canvas.Clear(SKColors.Transparent);
            for (int i = 0; i < drawn.Count; i++)
            {
                var place = layout.Placements[i];
                int w = Math.Max(1, (int)Math.Round(place.Width)), h = Math.Max(1, (int)Math.Round(place.Height));
                using var sprite = ThumbnailService.RenderFrameThumbnail(drawn[i].Texture, drawn[i].Frame, drawn[i].Layer.Color, w, h);
                if (sprite is null) continue;
                using var image = SKImage.FromBitmap(sprite);
                canvas.DrawImage(image, SKRect.Create(place.X, place.Y, place.Width, place.Height), new SKSamplingOptions(SKFilterMode.Linear));
            }
            return composite;
        }
        finally
        {
            foreach (var texture in textures.Values) texture?.Dispose();
        }
    }
}
