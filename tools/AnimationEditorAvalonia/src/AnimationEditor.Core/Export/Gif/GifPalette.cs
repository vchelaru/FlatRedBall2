using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Export.Gif;

/// <summary>
/// One shared palette for a whole GIF (shared so colors do not flicker between frames). Pixel art
/// with 256 or fewer colors keeps every color exactly; anything richer is reduced by median cut.
/// </summary>
public sealed class GifPalette
{
    /// <summary>Alpha at or above this is opaque; below it is transparent. GIF has no partial alpha.</summary>
    public const int AlphaThreshold = 128;

    private readonly byte[] _rgb;
    private readonly Dictionary<int, byte> _lookup;
    private readonly byte _fallbackIndex;

    private GifPalette(byte[] rgb, int count, int? transparentIndex, Dictionary<int, byte> lookup)
    {
        _rgb = rgb;
        Count = count;
        TransparentIndex = transparentIndex;
        _lookup = lookup;
        _fallbackIndex = (byte)(transparentIndex == 0 && count > 1 ? 1 : 0);
    }

    /// <summary>Number of used entries (the encoder pads the table to a power of two).</summary>
    public int Count { get; }

    /// <summary>The entry transparent pixels map to, or <c>null</c> when the GIF has no transparency.</summary>
    public int? TransparentIndex { get; }

    public (int R, int G, int B) ColorAt(int index) => (_rgb[index * 3], _rgb[index * 3 + 1], _rgb[index * 3 + 2]);

    /// <summary>The used entries as packed RGB bytes, padded with black to <paramref name="entries"/>.</summary>
    internal byte[] PaddedRgb(int entries)
    {
        var padded = new byte[entries * 3];
        Array.Copy(_rgb, padded, Count * 3);
        return padded;
    }

    /// <param name="frames">Straight (not premultiplied) RGBA buffers.</param>
    /// <param name="transparency">Reserves index 0 for pixels below <see cref="AlphaThreshold"/>.</param>
    public static GifPalette Build(IEnumerable<byte[]> frames, bool transparency)
    {
        var histogram = new Dictionary<int, int>();
        foreach (var rgba in frames)
        {
            for (int i = 0; i < rgba.Length; i += 4)
            {
                if (transparency && rgba[i + 3] < AlphaThreshold) continue;
                int key = rgba[i] << 16 | rgba[i + 1] << 8 | rgba[i + 2];
                histogram[key] = histogram.GetValueOrDefault(key) + 1;
            }
        }

        int first = transparency ? 1 : 0;
        int maxColors = 256 - first;
        var boxes = histogram.Count <= maxColors
            ? histogram.Select(kv => new List<(int Color, int Count)> { (kv.Key, kv.Value) }).ToList()
            : MedianCut(histogram.Select(kv => (kv.Key, kv.Value)).ToList(), maxColors);

        int count = Math.Max(1, boxes.Count) + first;
        var rgb = new byte[count * 3];
        var lookup = new Dictionary<int, byte>(histogram.Count);
        for (int b = 0; b < boxes.Count; b++)
        {
            long r = 0, g = 0, bl = 0, total = 0;
            foreach (var (color, n) in boxes[b])
            {
                r += (long)(color >> 16 & 0xFF) * n;
                g += (long)(color >> 8 & 0xFF) * n;
                bl += (long)(color & 0xFF) * n;
                total += n;
                lookup[color] = (byte)(b + first);
            }
            rgb[(b + first) * 3] = (byte)((r + total / 2) / total);
            rgb[(b + first) * 3 + 1] = (byte)((g + total / 2) / total);
            rgb[(b + first) * 3 + 2] = (byte)((bl + total / 2) / total);
        }
        return new GifPalette(rgb, count, transparency ? 0 : null, lookup);
    }

    /// <summary>Maps an RGBA buffer (built into this palette) to one palette index per pixel.</summary>
    public byte[] ToIndices(byte[] rgba)
    {
        var indices = new byte[rgba.Length / 4];
        for (int i = 0; i < indices.Length; i++)
        {
            if (TransparentIndex is { } transparent && rgba[i * 4 + 3] < AlphaThreshold)
            {
                indices[i] = (byte)transparent;
                continue;
            }
            int key = rgba[i * 4] << 16 | rgba[i * 4 + 1] << 8 | rgba[i * 4 + 2];
            indices[i] = _lookup.TryGetValue(key, out byte index) ? index : _fallbackIndex;
        }
        return indices;
    }

    private static List<List<(int Color, int Count)>> MedianCut(List<(int Color, int Count)> colors, int maxBoxes)
    {
        var boxes = new List<List<(int Color, int Count)>> { colors };
        while (boxes.Count < maxBoxes)
        {
            int best = -1;
            double bestScore = 0;
            for (int i = 0; i < boxes.Count; i++)
            {
                if (boxes[i].Count < 2) continue;
                var (_, range) = LongestAxis(boxes[i]);
                double score = (double)range * boxes[i].Sum(c => (long)c.Count);
                if (score > bestScore) { bestScore = score; best = i; }
            }
            if (best < 0) break;

            var box = boxes[best];
            var (axis, _) = LongestAxis(box);
            box.Sort((a, b) => Channel(a.Color, axis).CompareTo(Channel(b.Color, axis)));

            long half = box.Sum(c => (long)c.Count) / 2;
            long running = 0;
            int split = 1;
            for (int i = 0; i < box.Count - 1; i++)
            {
                running += box[i].Count;
                split = i + 1;
                if (running >= half) break;
            }
            boxes[best] = box.GetRange(0, split);
            boxes.Add(box.GetRange(split, box.Count - split));
        }
        return boxes;
    }

    private static (int Axis, int Range) LongestAxis(List<(int Color, int Count)> box)
    {
        int bestAxis = 0, bestRange = -1;
        for (int axis = 0; axis < 3; axis++)
        {
            int min = 255, max = 0;
            foreach (var (color, _) in box)
            {
                int v = Channel(color, axis);
                if (v < min) min = v;
                if (v > max) max = v;
            }
            if (max - min > bestRange) { bestRange = max - min; bestAxis = axis; }
        }
        return (bestAxis, bestRange);
    }

    private static int Channel(int color, int axis) => color >> (16 - axis * 8) & 0xFF;
}
