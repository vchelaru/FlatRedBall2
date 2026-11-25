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
    /// on a texture whose size can't be read has no pixel value, so it becomes <see cref="FrameRegion.Unset"/>; a frame
    /// with no texture is left alone.
    /// </summary>
    internal static void ToPixels(AnimationChainListSave list, Func<string, (int Width, int Height)?> textureSize)
    {
        if (list.CoordinateType == TextureCoordinateType.Pixel) return;

        var sizeOf = Cached(textureSize);
        foreach (var frame in Frames(list))
        {
            if (string.IsNullOrEmpty(frame.TextureName)) continue;
            var region = FrameRegion.Of(frame);
            (sizeOf(frame.TextureName) is { } size ? ToPixels(region, size) : FrameRegion.Unset).ApplyTo(frame);
        }
        list.CoordinateType = TextureCoordinateType.Pixel;
    }

    /// <summary>
    /// Runs <paramref name="write"/> with <paramref name="list"/> in <paramref name="format"/>, then restores the list
    /// exactly as it was. Needs no texture size when the list already is in that format.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A region needs the size of a texture that can't be read. Nothing has been changed or written.
    /// </exception>
    internal static void WriteAs(
        AnimationChainListSave list, TextureCoordinateType format, Func<string, (int Width, int Height)?> textureSize, Action write)
    {
        if (list.CoordinateType == format)
        {
            write();
            return;
        }

        var sizeOf = Cached(textureSize);
        ThrowIfUnsized(list, sizeOf, $"save with {format} coordinates");

        var original = list.CoordinateType;
        var regions = Frames(list).Select(frame => (Frame: frame, Region: FrameRegion.Of(frame))).ToList();
        try
        {
            foreach (var (frame, region) in regions)
            {
                if (sizeOf(frame.TextureName) is { } size)
                    (format == TextureCoordinateType.Pixel ? ToPixels(region, size) : ToUv(region, size)).ApplyTo(frame);
            }
            list.CoordinateType = format;
            write();
        }
        finally
        {
            foreach (var (frame, region) in regions) region.ApplyTo(frame);
            list.CoordinateType = original;
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
            : TryReadPngSize(System.IO.Path.IsPathRooted(name) ? name : System.IO.Path.Combine(directory, name.Replace('\\', '/')));

    private static FrameRegion ToPixels(FrameRegion uv, (int Width, int Height) size) =>
        new(uv.Left * size.Width, uv.Top * size.Height, uv.Right * size.Width, uv.Bottom * size.Height);

    private static FrameRegion ToUv(FrameRegion pixels, (int Width, int Height) size) =>
        new(pixels.Left / size.Width, pixels.Top / size.Height, pixels.Right / size.Width, pixels.Bottom / size.Height);

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
