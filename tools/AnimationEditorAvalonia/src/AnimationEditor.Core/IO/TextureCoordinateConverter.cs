using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Converts frame regions between the pixels the editor keeps in memory and the fractions (UV) of a legacy file,
/// reading each texture's size from its PNG header so Core needs no image-decode dependency. Pixel files never
/// come through here.
/// </summary>
internal static class TextureCoordinateConverter
{
    /// <summary>
    /// Converts <paramref name="list"/> from UV to pixels in place; no-op when it already holds pixels. A UV region
    /// on a texture whose size can't be read has no pixel value, so it becomes <see cref="FrameRegion.Unset"/>.
    /// </summary>
    internal static void ToPixels(AnimationChainListSave list, Func<string, (int Width, int Height)?> textureSize)
    {
        if (list.CoordinateType == TextureCoordinateType.Pixel) return;

        var sizeOf = Cached(textureSize);
        foreach (var frame in Frames(list))
        {
            var region = FrameRegion.Of(frame);
            var pixels = sizeOf(frame.TextureName) is { } size
                ? new FrameRegion(region.Left * size.Width, region.Top * size.Height, region.Right * size.Width, region.Bottom * size.Height)
                : FrameRegion.Unset;
            pixels.ApplyTo(frame);
        }
        list.CoordinateType = TextureCoordinateType.Pixel;
    }

    /// <summary>
    /// Runs <paramref name="write"/> with <paramref name="list"/> in UV, then restores its pixels exactly. A list
    /// already in UV is written as it is.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A region needs the size of a texture that can't be read. Nothing has been changed or written.
    /// </exception>
    internal static void WriteAsUv(AnimationChainListSave list, Func<string, (int Width, int Height)?> textureSize, Action write)
    {
        if (list.CoordinateType == TextureCoordinateType.UV)
        {
            write();
            return;
        }

        var sizeOf = Cached(textureSize);
        ThrowIfUnsized(list, sizeOf, "save with UV coordinates");

        var pixels = Frames(list).Select(frame => (Frame: frame, Region: FrameRegion.Of(frame))).ToList();
        try
        {
            foreach (var (frame, region) in pixels)
            {
                if (sizeOf(frame.TextureName) is { } size)
                    new FrameRegion(region.Left / size.Width, region.Top / size.Height, region.Right / size.Width, region.Bottom / size.Height)
                        .ApplyTo(frame);
            }
            list.CoordinateType = TextureCoordinateType.UV;
            write();
        }
        finally
        {
            foreach (var (frame, region) in pixels) region.ApplyTo(frame);
            list.CoordinateType = TextureCoordinateType.Pixel;
        }
    }

    /// <summary>Throws when a frame's region is set but its texture's size can't be read.</summary>
    internal static void ThrowIfUnsized(AnimationChainListSave list, Func<string, (int Width, int Height)?> textureSize, string action)
    {
        var unsized = Frames(list)
            .Where(frame => !string.IsNullOrEmpty(frame.TextureName) && FrameRegion.Of(frame) != FrameRegion.Unset
                && textureSize(frame.TextureName) is null)
            .Select(frame => frame.TextureName).Distinct().ToList();
        if (unsized.Count > 0)
            throw new InvalidOperationException(
                $"Cannot {action}: texture size could not be resolved for: {string.Join(", ", unsized)}. " +
                $"Fix or rebuild the missing {Plural.Noun(unsized.Count, "texture")}.");
    }

    /// <summary>Reads sizes of textures named relative to <paramref name="directory"/>; null for an empty name or an unreadable PNG.</summary>
    internal static Func<string, (int Width, int Height)?> SizesIn(string directory) => name =>
        string.IsNullOrEmpty(name)
            ? null
            : TryReadPngSize(System.IO.Path.IsPathRooted(name) ? name : System.IO.Path.Combine(directory, name));

    private static Func<string, (int Width, int Height)?> Cached(Func<string, (int Width, int Height)?> textureSize)
    {
        var sizes = new Dictionary<string, (int Width, int Height)?>(StringComparer.OrdinalIgnoreCase);
        return name =>
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (!sizes.TryGetValue(name, out var size)) sizes[name] = size = textureSize(name);
            return size;
        };
    }

    private static IEnumerable<AnimationFrameSave> Frames(AnimationChainListSave list) =>
        list.AnimationChains.SelectMany(chain => chain.Frames);

    /// <summary>The size in a PNG's header, or null when the file is missing, not a PNG, or has no area.</summary>
    internal static (int Width, int Height)? TryReadPngSize(string path)
    {
        try
        {
            using var fs = System.IO.File.OpenRead(path);
            System.Span<byte> hdr = stackalloc byte[24];
            if (fs.Read(hdr) != 24) return null;

            // PNG signature: 89 50 4E 47 0D 0A 1A 0A, then 8 bytes IHDR header,
            // then width (BE int32) and height (BE int32).
            if (hdr[0] != 0x89 || hdr[1] != 0x50 || hdr[2] != 0x4E || hdr[3] != 0x47)
                return null;

            int w = (hdr[16] << 24) | (hdr[17] << 16) | (hdr[18] << 8) | hdr[19];
            int h = (hdr[20] << 24) | (hdr[21] << 16) | (hdr[22] << 8) | hdr[23];
            return w > 0 && h > 0 ? (w, h) : null;
        }
        catch
        {
            return null;
        }
    }
}
