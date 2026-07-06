namespace AnimationEditor.Core.Diff;

/// <summary>
/// Per-pixel "changed vs. the previous revision" flags over a <see cref="Width"/>×<see cref="Height"/>
/// grid. Produced once per revision by <see cref="PixelDiff"/> (the expensive step) and consumed by
/// <see cref="RegionMerger"/> — which re-runs cheaply every time the merge-distance slider moves,
/// so the pixel diff is not recomputed on each slider tick.
/// </summary>
public sealed class ChangeMask
{
    public ChangeMask(int width, int height, bool[] changed)
    {
        Width = width;
        Height = height;
        Changed = changed;
    }

    public int Width { get; }
    public int Height { get; }

    /// <summary>Row-major changed flags; <c>Changed[y*Width + x]</c> is true when pixel (x, y) differs.</summary>
    public bool[] Changed { get; }

    public bool IsChanged(int x, int y) => Changed[y * Width + x];

    /// <summary>True when at least one pixel changed — the revision touched this file's rendered content.</summary>
    public bool HasAnyChange
    {
        get
        {
            foreach (var c in Changed)
                if (c) return true;
            return false;
        }
    }
}
