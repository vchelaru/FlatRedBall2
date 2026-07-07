namespace AnimationEditor.Core.Diff;

/// <summary>
/// Per-pixel "changed vs. the previous revision" flags over a <see cref="Width"/>×<see cref="Height"/>
/// grid. Produced once per revision by <see cref="PixelDiff"/> (the expensive step) and consumed by
/// <see cref="RegionMerger"/> — which re-runs cheaply every time the merge-distance slider moves,
/// so the pixel diff is not recomputed on each slider tick.
/// <para>
/// Flags are stored as a packed bitset (one bit per pixel) rather than a <c>bool[]</c>, so a whole
/// history's masks can be cached for instant revision switching: a 4096² mask is ~2 MB packed vs.
/// ~16 MB as bytes. The packing also lets <see cref="RegionMerger"/> skip 64 unchanged pixels per
/// zero word.
/// </para>
/// </summary>
public sealed class ChangeMask
{
    private readonly ulong[] _bits;

    /// <param name="changed">Row-major changed flags; length must be <paramref name="width"/> × <paramref name="height"/>.</param>
    public ChangeMask(int width, int height, bool[] changed)
    {
        Width = width;
        Height = height;
        _bits = new ulong[(changed.Length + 63) >> 6];
        for (int i = 0; i < changed.Length; i++)
            if (changed[i])
                _bits[i >> 6] |= 1UL << (i & 63);
    }

    public int Width { get; }
    public int Height { get; }

    /// <summary>Packed changed bits (one per pixel, row-major). Exposed for <see cref="RegionMerger"/>'s word scan.</summary>
    public ulong[] Bits => _bits;

    public bool IsChanged(int x, int y)
    {
        int i = y * Width + x;
        return (_bits[i >> 6] & (1UL << (i & 63))) != 0;
    }

    /// <summary>True when at least one pixel changed — the revision touched this file's rendered content.</summary>
    public bool HasAnyChange
    {
        get
        {
            foreach (var word in _bits)
                if (word != 0) return true;
            return false;
        }
    }
}
