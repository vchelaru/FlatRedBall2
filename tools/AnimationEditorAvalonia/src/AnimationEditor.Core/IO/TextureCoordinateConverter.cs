using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Converts a whole <see cref="AnimationChainListSave"/> between pixel and UV frame coordinates, reading each
/// texture's size from its PNG header so Core needs no image-decode dependency. The editor keeps UV in memory;
/// <see cref="AnimationFileLoader"/> converts on load and <see cref="ProjectManager"/> converts around each Pixel save.
/// </summary>
internal static class TextureCoordinateConverter
{
    /// <summary>
    /// Convert <paramref name="acls"/> to <paramref name="target"/> coordinate space
    /// in place and return the per-texture size cache used (so a paired round-trip
    /// can reuse it). No-op if already in <paramref name="target"/> space.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Converting toward <see cref="TextureCoordinateType.Pixel"/> (i.e. preparing a persisted
    /// on-disk write) needs every referenced texture's pixel size resolved up front: writing a
    /// file with <c>CoordinateType=Pixel</c> while a frame's texture size couldn't be resolved
    /// would leave that frame's coordinates un-converted (still UV-scale) under a header that
    /// claims otherwise, corrupting the file (#1135). Converting toward UV -- on load, or when
    /// reversing an in-memory model back after a successful Pixel save -- stays lenient: a
    /// texture that hasn't been built yet is an expected, tolerated state while editing, and no
    /// persisted artifact is at risk.
    /// </exception>
    internal static Dictionary<string, (int W, int H)> ConvertCoordinates(
        AnimationChainListSave acls,
        string achxDirectory,
        TextureCoordinateType target,
        Dictionary<string, (int W, int H)>? sizeCache = null)
    {
        sizeCache ??= new Dictionary<string, (int W, int H)>(System.StringComparer.OrdinalIgnoreCase);

        if (acls.CoordinateType == target) return sizeCache;

        bool toPixel = target == TextureCoordinateType.Pixel;

        if (toPixel)
        {
            var unresolved = new List<string>();
            foreach (var chain in acls.AnimationChains)
            {
                foreach (var frame in chain.Frames)
                {
                    if (string.IsNullOrEmpty(frame.TextureName)) continue;
                    if (sizeCache.ContainsKey(frame.TextureName)) continue;

                    if (TryResolveTextureSize(frame.TextureName, achxDirectory, sizeCache, out var size))
                        sizeCache[frame.TextureName] = size;
                    else
                        unresolved.Add(frame.TextureName);
                }
            }

            if (unresolved.Count > 0)
            {
                throw new InvalidOperationException(
                    "Cannot save with CoordinateType=Pixel: texture size could not be resolved " +
                    $"for: {string.Join(", ", unresolved.Distinct())}. Fix or rebuild the missing " +
                    $"{Plural.Noun(unresolved.Distinct().Count(), "texture")}, or switch the on-disk coordinate format to UV.");
            }
        }

        foreach (var chain in acls.AnimationChains)
        {
            foreach (var frame in chain.Frames)
            {
                if (string.IsNullOrEmpty(frame.TextureName)) continue;

                if (!sizeCache.TryGetValue(frame.TextureName, out var size))
                {
                    // Only reached when target == UV -- the toPixel branch above already
                    // guaranteed every frame's texture is in sizeCache or threw.
                    if (!TryResolveTextureSize(frame.TextureName, achxDirectory, sizeCache, out size))
                        continue;
                    sizeCache[frame.TextureName] = size;
                }

                if (size.W <= 0 || size.H <= 0) continue;

                if (toPixel)
                {
                    frame.LeftCoordinate   *= size.W;
                    frame.RightCoordinate  *= size.W;
                    frame.TopCoordinate    *= size.H;
                    frame.BottomCoordinate *= size.H;
                }
                else
                {
                    frame.LeftCoordinate   /= size.W;
                    frame.RightCoordinate  /= size.W;
                    frame.TopCoordinate    /= size.H;
                    frame.BottomCoordinate /= size.H;
                }
            }
        }

        acls.CoordinateType = target;
        return sizeCache;
    }

    /// <summary>
    /// Resolves <paramref name="textureName"/> to a pixel size via <paramref name="sizeCache"/>,
    /// falling back to a PNG header read under <paramref name="achxDirectory"/>. Returns <see langword="false"/>
    /// without touching <paramref name="sizeCache"/> when neither resolves.
    /// </summary>
    private static bool TryResolveTextureSize(
        string textureName,
        string achxDirectory,
        Dictionary<string, (int W, int H)> sizeCache,
        out (int W, int H) size)
    {
        if (sizeCache.TryGetValue(textureName, out size)) return true;

        var path = System.IO.Path.IsPathRooted(textureName)
            ? textureName
            : System.IO.Path.Combine(achxDirectory, textureName);

        var read = TryReadPngSize(path);
        if (read == null)
        {
            size = default;
            return false;
        }

        size = read.Value;
        return true;
    }

    internal static (int W, int H)? TryReadPngSize(string path)
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
            return (w, h);
        }
        catch
        {
            return null;
        }
    }
}
