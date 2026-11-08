using AnimationEditor.Core.Keyframes;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AnimationEditor.Core.Sequences;

/// <summary>One sprite of a sequence thumbnail: its frame, its texture, and the sampled offset (Y up, in pixels) and color.</summary>
public sealed record SequenceThumbnailLayer(
    AnimationFrameSave Frame, string? TexturePath, float OffsetX, float OffsetY, ResolvedFrameColor Color);

/// <summary>
/// Picks the instant a sequence's thumbnail shows, and the sprites there. Shared by the Open Project Folder rows and the
/// sequence list rows, so both show the same picture.
/// </summary>
public static class SequenceThumbnailScene
{
    /// <summary>
    /// The first instant where something is visible: 0 when any track shows a sprite then, otherwise the earliest start of a
    /// visible track's chain that has frames (a sequence whose tracks all start later). Null when nothing ever shows: no
    /// track, every track hidden, or no chain that resolves to a chain with frames.
    /// </summary>
    public static double? ChooseTime(SequenceSave sequence, ISequenceChainLookup lookup)
    {
        if (SequenceScene.At(sequence, 0, 0, lookup).Count > 0) return 0;
        var starts = sequence.Tracks
            .Where(track => track.Visible)
            .SelectMany(track => track.Chains)
            .Where(reference => reference.Start > 0 && lookup.FindChain(reference) is { Frames.Count: > 0 })
            .Select(reference => reference.Start)
            .Distinct()
            .OrderBy(start => start);
        foreach (double start in starts)
            if (SequenceScene.At(sequence, 0, start, lookup).Count > 0) return start;
        return null;
    }

    /// <summary>The sprites at <see cref="ChooseTime"/>, back to front (the top lane last); empty when nothing ever shows.</summary>
    public static IReadOnlyList<SequenceThumbnailLayer> LayersFor(SequenceSave sequence, ISequenceChainLookup lookup)
    {
        if (ChooseTime(sequence, lookup) is not { } time) return [];
        return SequenceScene.At(sequence, 0, time, lookup)
            .Select(sprite =>
            {
                var sample = sprite.Sample();
                return new SequenceThumbnailLayer(sprite.Frame, sprite.TexturePath, sample.OffsetX, sample.OffsetY, sample.Color);
            })
            .ToList();
    }
}

/// <summary>One sprite's size in pixels and its offset from the entity origin (Y up).</summary>
public readonly record struct ThumbnailSpriteRect(float Width, float Height, float OffsetX, float OffsetY);

/// <summary>Where a sprite lands in a thumbnail, in the thumbnail's own pixels (Y down).</summary>
public readonly record struct ThumbnailPlacement(float X, float Y, float Width, float Height);

/// <summary>The size of a composite thumbnail and where each sprite sits in it.</summary>
public sealed record ThumbnailLayout(int Width, int Height, float Scale, IReadOnlyList<ThumbnailPlacement> Placements);

/// <summary>
/// Fits a multi-sprite entity into a thumbnail the way a single frame is fitted: the sprites' union (each centred on its offset
/// from the shared origin) is scaled to fit the maximum size, keeping the aspect ratio.
/// </summary>
public static class ThumbnailLayoutCalculator
{
    public static ThumbnailLayout? Fit(IReadOnlyList<ThumbnailSpriteRect> sprites, int maxWidth, int maxHeight)
    {
        if (sprites.Count == 0 || sprites.Any(s => s.Width <= 0 || s.Height <= 0)) return null;
        float left = sprites.Min(s => s.OffsetX - s.Width / 2f);
        float right = sprites.Max(s => s.OffsetX + s.Width / 2f);
        float top = sprites.Min(s => -s.OffsetY - s.Height / 2f);
        float bottom = sprites.Max(s => -s.OffsetY + s.Height / 2f);
        float boundsW = right - left, boundsH = bottom - top;
        float scale = Math.Min(maxWidth / boundsW, maxHeight / boundsH);
        int width = Math.Max(1, (int)(boundsW * scale));
        int height = Math.Max(1, (int)(boundsH * scale));
        var placements = sprites
            .Select(s => new ThumbnailPlacement(
                (s.OffsetX - s.Width / 2f - left) * scale, (-s.OffsetY - s.Height / 2f - top) * scale,
                s.Width * scale, s.Height * scale))
            .ToList();
        return new ThumbnailLayout(width, height, scale, placements);
    }
}

/// <summary>
/// The disk cache file name of a <c>.seqj</c> thumbnail. A thumbnail depends on the <c>.seqj</c>, every source it
/// references and every texture those sources use, so the caller passes one line per such input (its content hash, or its
/// path with size and last-write time) and any change to any line gives a different name, i.e. a cache miss.
/// </summary>
public static class SequenceThumbnailCacheKey
{
    /// <summary>Bump when the composite is drawn differently, so files from the old drawing are not served.</summary>
    public const int RenderVersion = 1;

    /// <summary>One input line for a file: its path (relative to the <c>.seqj</c> folder), size and last-write time.</summary>
    public static string FileInput(string kind, string path, ulong? size, DateTimeOffset? modified) =>
        $"{kind}:{path.Replace('\\', '/').ToLowerInvariant()}|{size?.ToString() ?? "-"}|{modified?.UtcTicks.ToString() ?? "-"}";

    /// <summary>
    /// <c>{identity hash}_{inputs hash}_v{version}.png</c>. The part before the first underscore depends only on
    /// <paramref name="sourceIdentity"/>, so a service that deletes files with that prefix replaces the superseded entry.
    /// </summary>
    public static string BuildFileName(string sourceIdentity, IEnumerable<string> inputs) =>
        $"{Hash(sourceIdentity.Replace('\\', '/').ToLowerInvariant())}_{Fingerprint(inputs)}_v{RenderVersion}.png";

    /// <summary>A short hash of the inputs, insensitive to their order.</summary>
    public static string Fingerprint(IEnumerable<string> inputs) =>
        Hash(string.Join('\n', inputs.OrderBy(line => line, StringComparer.Ordinal)));

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
}
