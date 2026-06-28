using FlatRedBall2.Animation.Content;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AnimationEditor.Core.Export;

/// <summary>Result of a PixiJS export: the JSON text plus any non-fatal warnings to surface.</summary>
public sealed class PixiJsExportResult
{
    public PixiJsExportResult(string json, IReadOnlyList<string> warnings, IReadOnlyList<string> referencedTextures)
    {
        Json = json;
        Warnings = warnings;
        ReferencedTextures = referencedTextures;
    }

    /// <summary>The serialized PixiJS spritesheet JSON.</summary>
    public string Json { get; }

    /// <summary>Human-readable warnings (dropped duration, multiple textures, missing PNGs).</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// Distinct texture names referenced by the exported frames, in first-seen order. The app
    /// layer copies these alongside the JSON when exporting to a different directory so the
    /// PixiJS loader can resolve <c>meta.image</c>.
    /// </summary>
    public IReadOnlyList<string> ReferencedTextures { get; }
}

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
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Converts <paramref name="acls"/> to a PixiJS spritesheet. <paramref name="textureSizeResolver"/>
    /// maps a frame's <see cref="AnimationFrameSave.TextureName"/> to its pixel size, or <c>null</c>
    /// when the PNG can't be read; frames whose size is unresolvable (UV input only) are skipped with
    /// a warning.
    /// </summary>
    public static PixiJsExportResult Export(
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

                if (!TryBuildRect(frame, acls.CoordinateType, textureSizeResolver, out var rect))
                {
                    warnings.Add($"Frame {i} of '{chain.Name}' was skipped: texture " +
                                 $"'{frame.TextureName}' could not be read to convert UV coordinates to pixels.");
                    continue;
                }

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

        return new PixiJsExportResult(JsonSerializer.Serialize(sheet, Options), warnings, orderedTextures);
    }

    private static bool TryBuildRect(
        AnimationFrameSave frame,
        TextureCoordinateType coordinateType,
        Func<string, (int Width, int Height)?> textureSizeResolver,
        out PixiJsRect rect)
    {
        rect = new PixiJsRect();

        if (coordinateType == TextureCoordinateType.Pixel)
        {
            // Coordinates are already pixels; round to integer rect.
            int left = Round(frame.LeftCoordinate);
            int top = Round(frame.TopCoordinate);
            rect = new PixiJsRect
            {
                X = left,
                Y = top,
                W = Round(frame.RightCoordinate) - left,
                H = Round(frame.BottomCoordinate) - top,
            };
            return true;
        }

        // UV input: scale by the texture size. Round the edges (not the width) so adjacent
        // frames tile to exact pixel boundaries with no gaps or overlaps.
        var size = textureSizeResolver(frame.TextureName);
        if (size is not { Width: > 0, Height: > 0 }) return false;

        int leftPx = Round(frame.LeftCoordinate * size.Value.Width);
        int rightPx = Round(frame.RightCoordinate * size.Value.Width);
        int topPx = Round(frame.TopCoordinate * size.Value.Height);
        int bottomPx = Round(frame.BottomCoordinate * size.Value.Height);
        rect = new PixiJsRect
        {
            X = leftPx,
            Y = topPx,
            W = rightPx - leftPx,
            H = bottomPx - topPx,
        };
        return true;
    }

    private static int Round(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    private static string MakeUnique(string candidate, IEnumerable<string> existing)
    {
        var taken = new HashSet<string>(existing, StringComparer.Ordinal);
        if (!taken.Contains(candidate)) return candidate;

        int suffix = 2;
        while (taken.Contains($"{candidate}_{suffix}")) suffix++;
        return $"{candidate}_{suffix}";
    }
}
