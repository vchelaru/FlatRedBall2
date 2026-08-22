using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AnimationEditor.Core.IO;

/// <summary>
/// One frame matched by <see cref="AchxPngUsageScanner"/>: the chain it belongs to (found inside
/// <paramref name="Entry"/>'s parsed <c>.achx</c>) and its rect in texture-space pixels of the
/// target PNG, already converted from UV if the source file used that <c>CoordinateType</c>.
/// </summary>
public sealed record PngUsageFrameMatch(
    AchxFileEntry Entry,
    AnimationChainSave Chain,
    AnimationFrameSave Frame,
    float Left, float Top, float Right, float Bottom);

/// <summary>
/// Finds every animation-chain frame across a project's <c>.achx</c>/<c>.achj</c> files that
/// references a given target PNG (issue #953's "Analyze Image Usage" overlay) — which chain(s)
/// use which regions of a spritesheet.
/// </summary>
public static class AchxPngUsageScanner
{
    /// <summary>
    /// Parses <paramref name="entry"/> and returns every frame whose <c>TextureName</c> resolves
    /// (relative to <see cref="AchxFileEntry.ParentFolder"/> — that file's own folder, not any
    /// other project root) to <paramref name="isTargetTexture"/>. Frame rects come back in
    /// texture-space pixels of a <paramref name="targetTextureWidth"/>×<paramref name="targetTextureHeight"/>
    /// image. Returns empty (never throws) for an unreadable/malformed file or an unresolvable
    /// texture reference — mirrors <c>ProjectTreeThumbnailService</c>'s "fall back to nothing"
    /// contract for the same class of failure.
    /// </summary>
    public static async Task<IReadOnlyList<PngUsageFrameMatch>> FindMatchesAsync(
        AchxFileEntry entry,
        Func<IEditorFile, bool> isTargetTexture,
        int targetTextureWidth,
        int targetTextureHeight)
    {
        AnimationChainListSave acls;
        try
        {
            string achxText;
            await using (var stream = await entry.File.OpenReadAsync())
            using (var reader = new StreamReader(stream))
                achxText = await reader.ReadToEndAsync();
            acls = AnimationChainListSave.FromString(achxText);
        }
        catch
        {
            return Array.Empty<PngUsageFrameMatch>();
        }

        var matches = new List<PngUsageFrameMatch>();

        // Most .achx files point every frame at one texture; cache each distinct TextureName's
        // resolve+match result so a chain with many frames doesn't re-resolve the same relative
        // path once per frame.
        var isMatchByTextureName = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach (var chain in acls.AnimationChains)
        {
            foreach (var frame in chain.Frames)
            {
                if (string.IsNullOrEmpty(frame.TextureName)) continue;

                if (!isMatchByTextureName.TryGetValue(frame.TextureName, out var isMatch))
                {
                    var textureFile = await entry.ParentFolder.ResolveRelativeFileAsync(frame.TextureName);
                    isMatch = textureFile is not null && isTargetTexture(textureFile);
                    isMatchByTextureName[frame.TextureName] = isMatch;
                }

                if (!isMatch) continue;

                var pixelFrame = acls.CoordinateType == TextureCoordinateType.Pixel
                    ? frame
                    : PixelFrameUvConverter.ToPixel(frame, targetTextureWidth, targetTextureHeight);

                matches.Add(new PngUsageFrameMatch(
                    entry, chain, frame,
                    pixelFrame.LeftCoordinate, pixelFrame.TopCoordinate,
                    pixelFrame.RightCoordinate, pixelFrame.BottomCoordinate));
            }
        }

        return matches;
    }
}
