using System;
using System.Collections.Generic;
using System.Numerics;

namespace AnimationEditor.Core.Diff;

/// <summary>
/// Groups the scattered changed pixels of a <see cref="ChangeMask"/> into a small number of
/// rectangular <see cref="PixelRegion"/>s, so a revision that touched two unrelated frames shows two
/// meaningful boxes instead of a scatter of single-pixel squares.
/// <para>
/// Proximity is approximated with a uniform grid: the mask is bucketed into cells whose size equals
/// the distance threshold, and occupied cells that are 8-neighbours (including diagonally) merge into
/// one region. Two changed pixels closer than the threshold in both axes always land in the same or
/// adjacent cells and therefore merge; the exact cutoff is grid-approximate, which is fine for a
/// live-tuned slider. Each region's box tightly encloses the actual changed pixels it contains, not
/// the coarser cells.
/// </para>
/// </summary>
public static class RegionMerger
{
    /// <summary>
    /// Merges the changed pixels of <paramref name="mask"/> into regions. Regions are returned largest
    /// first (by changed-pixel count). An empty list is returned when nothing changed.
    /// </summary>
    /// <param name="distanceThreshold">
    /// Proximity in pixels: changed pixels within this distance of each other merge into one region.
    /// Clamped to a minimum of 1.
    /// </param>
    public static IReadOnlyList<PixelRegion> Merge(ChangeMask mask, int distanceThreshold)
    {
        int cellSize = Math.Max(1, distanceThreshold);
        int width = mask.Width;
        int cols = (width + cellSize - 1) / cellSize;
        var bits = mask.Bits;

        // One accumulator per occupied grid cell (sparse — only cells with a changed pixel), keyed
        // by cell index cy*cols + cx. Each holds the tight pixel bounds and count within that cell.
        // Scan the packed bitset a word at a time, skipping 64 unchanged pixels per zero word, and
        // derive (x, y) only for pixels that actually changed — so a few-thousand-pixel change on a
        // multi-megapixel sheet costs almost nothing (#606).
        var cells = new Dictionary<int, Accum>();
        for (int w = 0; w < bits.Length; w++)
        {
            ulong word = bits[w];
            int baseIdx = w << 6;
            while (word != 0)
            {
                int idx = baseIdx + BitOperations.TrailingZeroCount(word);
                word &= word - 1;   // clear the lowest set bit

                int x = idx % width;
                int y = idx / width;
                int key = (y / cellSize) * cols + (x / cellSize);
                if (cells.TryGetValue(key, out var acc))
                    cells[key] = acc.Add(x, y);
                else
                    cells[key] = new Accum(x, y, x, y, 1);
            }
        }

        if (cells.Count == 0)
            return System.Array.Empty<PixelRegion>();

        // Union occupied cells that are 8-neighbours (incl. diagonals), then aggregate each component.
        var uf = new UnionFind(cells.Keys);
        foreach (int key in cells.Keys)
        {
            int cx = key % cols, cy = key / cols;
            // Only the four "forward" neighbours are needed — the reverse pairs are covered when
            // those cells are visited, and union is symmetric.
            TryUnion(uf, cells, key, cx + 1, cy, cols);
            TryUnion(uf, cells, key, cx - 1, cy + 1, cols);
            TryUnion(uf, cells, key, cx, cy + 1, cols);
            TryUnion(uf, cells, key, cx + 1, cy + 1, cols);
        }

        var byRoot = new Dictionary<int, Accum>();
        foreach (var (key, acc) in cells)
        {
            int root = uf.Find(key);
            byRoot[root] = byRoot.TryGetValue(root, out var existing) ? existing.Union(acc) : acc;
        }

        var regions = new List<PixelRegion>(byRoot.Count);
        foreach (var acc in byRoot.Values)
            regions.Add(new PixelRegion(acc.MinX, acc.MinY, acc.MaxX, acc.MaxY, acc.Count));

        // Largest cluster first so the animator's eye lands on the biggest change.
        regions.Sort((a, b) => b.ChangedPixelCount.CompareTo(a.ChangedPixelCount));
        return regions;
    }

    private static void TryUnion(UnionFind uf, Dictionary<int, Accum> cells, int key, int nx, int ny, int cols)
    {
        if (nx < 0 || ny < 0 || nx >= cols) return;
        int neighbor = ny * cols + nx;
        if (cells.ContainsKey(neighbor))
            uf.Union(key, neighbor);
    }

    private readonly record struct Accum(int MinX, int MinY, int MaxX, int MaxY, int Count)
    {
        public Accum Add(int x, int y) => new(
            Math.Min(MinX, x), Math.Min(MinY, y), Math.Max(MaxX, x), Math.Max(MaxY, y), Count + 1);

        public Accum Union(Accum o) => new(
            Math.Min(MinX, o.MinX), Math.Min(MinY, o.MinY),
            Math.Max(MaxX, o.MaxX), Math.Max(MaxY, o.MaxY), Count + o.Count);
    }

    // Standard union-find over a sparse set of integer keys (cell indices).
    private sealed class UnionFind
    {
        private readonly Dictionary<int, int> _parent = new();

        public UnionFind(IEnumerable<int> keys)
        {
            foreach (int k in keys) _parent[k] = k;
        }

        public int Find(int k)
        {
            int root = k;
            while (_parent[root] != root) root = _parent[root];
            while (_parent[k] != root) { int next = _parent[k]; _parent[k] = root; k = next; }
            return root;
        }

        public void Union(int a, int b)
        {
            int ra = Find(a), rb = Find(b);
            if (ra != rb) _parent[ra] = rb;
        }
    }
}
