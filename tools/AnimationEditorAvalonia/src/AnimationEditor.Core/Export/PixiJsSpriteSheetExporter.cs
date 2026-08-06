using FlatRedBall2.Animation.Content;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace AnimationEditor.Core.Export;

/// <summary>
/// Pure converter from the editor's save model to a PixiJS spritesheet manifest. Stateless and
/// dependency-free so it can be unit-tested directly: feed it an <see cref="AnimationChainListSave"/>
/// plus a texture-size resolver and assert on the returned JSON. The file dialog / disk write live
/// in the app layer.
/// </summary>
/// <remarks>
/// Fidelity gaps (PixiJS spritesheets cannot carry these, so they are dropped with a warning):
/// per-frame duration, flip flags, and multiple source textures (PixiJS <c>meta.image</c> is a
/// single sheet). Coordinates are read from the in-memory model: UV (0–1) coords are multiplied by
/// the resolved texture size; Pixel coords are used directly (so the resolver is only consulted for
/// UV input and for <c>sourceSize</c>).
/// </remarks>
public static class PixiJsSpriteSheetExporter
{
    /// <summary>
    /// Converts <paramref name="acls"/> to a PixiJS spritesheet. <paramref name="textureSizeResolver"/>
    /// maps a frame's <see cref="AnimationFrameSave.TextureName"/> to its pixel size, or <c>null</c>
    /// when the PNG can't be read; frames whose size is unresolvable (UV input only) are skipped with
    /// a warning.
    /// </summary>
    public static ExportResult Export(
        AnimationChainListSave acls,
        Func<string, (int Width, int Height)?> textureSizeResolver)
    {
        ArgumentNullException.ThrowIfNull(acls);
        ArgumentNullException.ThrowIfNull(textureSizeResolver);

        var sheet = new PixiJsSpriteSheet();
        var warnings = new List<string>();
        var distinctTextures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var orderedTextures = new List<string>();
        bool anyDurationDropped = false;

        foreach (var chain in acls.AnimationChains)
        {
            // Frame keys live in one global map; disambiguate so duplicate chain names can't
            // silently overwrite each other's frames.
            string animationName = MakeUnique(chain.Name, sheet.Animations.Keys);
            var frameKeys = new List<string>(chain.Frames.Count);

            for (int i = 0; i < chain.Frames.Count; i++)
            {
                var frame = chain.Frames[i];

                if (frame.FrameLength != 0f) anyDurationDropped = true;

                if (!string.IsNullOrEmpty(frame.TextureName) && distinctTextures.Add(frame.TextureName))
                    orderedTextures.Add(frame.TextureName);

                if (!FrameRectResolver.TryBuildRect(frame, acls.CoordinateType, textureSizeResolver, out var pixelRect))
                {
                    warnings.Add($"Frame {i} of '{chain.Name}' was skipped: texture " +
                                 $"'{frame.TextureName}' could not be read to convert UV coordinates to pixels.");
                    continue;
                }

                var rect = ToPixiRect(pixelRect);
                string frameKey = MakeUnique($"{chain.Name}_{i}", sheet.Frames.Keys);
                sheet.Frames[frameKey] = new PixiJsFrameData
                {
                    Frame = rect,
                    SourceSize = new PixiJsSize { W = rect.W, H = rect.H },
                    SpriteSourceSize = new PixiJsRect { X = 0, Y = 0, W = rect.W, H = rect.H },
                };
                frameKeys.Add(frameKey);
            }

            sheet.Animations[animationName] = frameKeys;
        }

        string firstTexture = orderedTextures.Count > 0 ? orderedTextures[0] : string.Empty;
        sheet.Meta.Image = firstTexture;

        if (orderedTextures.Count > 1)
            warnings.Add($"This .achx references {orderedTextures.Count} textures, but a PixiJS " +
                         $"spritesheet is a single sheet; meta.image was set to '{firstTexture}'.");
        if (anyDurationDropped)
            warnings.Add("Per-frame durations are not part of the PixiJS spritesheet format and were dropped.");

        return new ExportResult(
            JsonSerializer.Serialize(sheet, PixiJsJsonContext.Default.PixiJsSpriteSheet), warnings, orderedTextures);
    }

    private static PixiJsRect ToPixiRect(FramePixelRect rect) =>
        new() { X = rect.X, Y = rect.Y, W = rect.W, H = rect.H };

    private static string MakeUnique(string candidate, IEnumerable<string> existing)
    {
        var taken = new HashSet<string>(existing, StringComparer.Ordinal);
        if (!taken.Contains(candidate)) return candidate;

        int suffix = 2;
        while (taken.Contains($"{candidate}_{suffix}")) suffix++;
        return $"{candidate}_{suffix}";
    }
}
