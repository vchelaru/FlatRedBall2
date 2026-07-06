using AnimationEditor.Core.Diff;
using AnimationEditor.Core.Git;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace AnimationEditor.App.Services;

/// <summary>Status plus the ordered revision list for the Diff/Blame panel of one PNG.</summary>
public sealed record PngBlameResult(GitHistoryStatus Status, IReadOnlyList<GitRevision> Entries);

/// <summary>
/// Backs the PNG Diff/Blame view: loads a file's git history (via <see cref="GitCli"/>), then on
/// demand computes the changed-region boxes for a chosen revision by decoding that revision's blob
/// and its parent's, pixel-diffing (<see cref="PixelDiff"/>), and clustering (<see cref="RegionMerger"/>).
/// <para>
/// Decoding and the pixel diff are the expensive steps, so both are cached: decoded blobs by git
/// object id, and the change mask by (revision, tolerance). Dragging the merge-distance slider only
/// re-runs the cheap clustering against the cached mask; changing tolerance re-diffs the cached
/// decoded images without re-reading git. <see cref="Load"/> resets all caches for the new file.
/// </para>
/// </summary>
public sealed class PngBlameService
{
    private readonly GitCli _git;

    private string _absolutePath = "";
    private string _repoRoot = "";
    private IReadOnlyList<GitRevision> _entries = Array.Empty<GitRevision>();

    // Decoded blobs keyed by git object spec ("hash:path", "HEAD:path") or the working-tree marker;
    // FIFO-capped so long histories don't accumulate 64 MB-per-sheet decodes without bound.
    private readonly Dictionary<string, ImageData?> _decodeCache = new();
    private readonly Queue<string> _decodeOrder = new();
    private const int DecodeCacheCap = 8;

    // Only the most-recently-used change mask is kept: the common gesture is "pick a revision, then
    // drag the distance slider", which reuses one mask; changing revision/tolerance replaces it.
    private (int entryIndex, int tolerance)? _maskKey;
    private ChangeMask? _mask;

    private const string WorkingTreeDecodeKey = "\0workingtree";

    public PngBlameService(GitCli? git = null) => _git = git ?? new GitCli();

    /// <summary>
    /// Loads the git history for <paramref name="absolutePath"/> and prepares the revision list. When
    /// the file has uncommitted changes, a synthetic "Working tree" entry is prepended so the animator
    /// can inspect an as-yet-uncommitted re-export against HEAD. Resets all caches.
    /// </summary>
    public PngBlameResult Load(string absolutePath)
    {
        _absolutePath = absolutePath;
        _decodeCache.Clear();
        _decodeOrder.Clear();
        _maskKey = null;
        _mask = null;

        var history = _git.LoadHistory(absolutePath);
        _repoRoot = history.RepositoryRoot;

        if (history.Status != GitHistoryStatus.Ok)
        {
            _entries = Array.Empty<GitRevision>();
            return new PngBlameResult(history.Status, _entries);
        }

        var entries = new List<GitRevision>(history.Revisions);
        // Prepend the uncommitted-state entry (diffed against HEAD) when the file differs from HEAD
        // and there is a HEAD commit to diff against.
        if (entries.Count > 0 && _git.HasUncommittedChanges(_repoRoot, absolutePath))
            entries.Insert(0, GitRevision.WorkingTree(entries[0].PathAtCommit ?? ""));

        _entries = entries;
        return new PngBlameResult(GitHistoryStatus.Ok, _entries);
    }

    /// <summary>
    /// Changed-region boxes for the revision at <paramref name="entryIndex"/>, compared to the
    /// previous (older) revision. Returns an empty list when nothing changed or the blobs can't be
    /// decoded. The oldest revision (the initial add) diffs against an empty image, so all its content
    /// reads as new.
    /// </summary>
    public IReadOnlyList<PixelRegion> ComputeRegions(int entryIndex, int tolerance, int distanceThreshold)
    {
        if (entryIndex < 0 || entryIndex >= _entries.Count)
            return Array.Empty<PixelRegion>();

        // Per-stage timing (#606 slowness diagnosis) — Debug.WriteLine is stripped from Release builds.
        // Watch the Visual Studio Output window (Debug pane) and click a revision to read the breakdown.
        var total = Stopwatch.StartNew();
        var mask = GetOrBuildMask(entryIndex, tolerance);
        if (mask is null)
        {
            Debug.WriteLine("[PngBlame] no mask (blobs undecodable)");
            return Array.Empty<PixelRegion>();
        }

        var sw = Stopwatch.StartNew();
        var regions = RegionMerger.Merge(mask, distanceThreshold);
        long mergeMs = sw.ElapsedMilliseconds;

        long changedPixels = 0;
        foreach (var r in regions) changedPixels += r.ChangedPixelCount;

        Debug.WriteLine($"[PngBlame]   region-merge: {mergeMs}ms " +
            $"({changedPixels:N0} changed px → {regions.Count} regions, dist={distanceThreshold})");
        Debug.WriteLine($"[PngBlame] ComputeRegions TOTAL: {total.ElapsedMilliseconds}ms " +
            $"(entry={entryIndex}, tol={tolerance})");
        return regions;
    }

    private ChangeMask? GetOrBuildMask(int entryIndex, int tolerance)
    {
        if (_maskKey == (entryIndex, tolerance) && _mask is not null)
        {
            Debug.WriteLine($"[PngBlame] mask entry={entryIndex} tol={tolerance}: CACHE HIT (slider-only re-merge)");
            return _mask;
        }

        var after = DecodeAfter(entryIndex);
        var before = DecodeBefore(entryIndex);
        if (after is null && before is null)
            return null;

        var sw = Stopwatch.StartNew();
        _mask = PixelDiff.Compute(before, after, tolerance);
        long px = (long)_mask.Width * _mask.Height;
        Debug.WriteLine($"[PngBlame]   pixel-diff: {sw.ElapsedMilliseconds}ms " +
            $"({_mask.Width}×{_mask.Height} = {px:N0} px)");

        _maskKey = (entryIndex, tolerance);
        return _mask;
    }

    // "After" is this entry's own content: the on-disk file for the working-tree entry, else the blob.
    private ImageData? DecodeAfter(int entryIndex)
    {
        var entry = _entries[entryIndex];
        if (entry.IsWorkingTree)
            return DecodeCached(WorkingTreeDecodeKey, () => ReadFileBytes(_absolutePath));
        return DecodeCached($"{entry.Hash}:{entry.PathAtCommit}",
            () => _git.GetBlobBytes(_repoRoot, entry.Hash, entry.PathAtCommit ?? ""));
    }

    // "Before" is the next-older entry (always a commit, since the working-tree entry is only ever
    // first). Null for the oldest revision — the initial add has no parent.
    private ImageData? DecodeBefore(int entryIndex)
    {
        int olderIndex = entryIndex + 1;
        if (olderIndex >= _entries.Count)
            return null;
        var older = _entries[olderIndex];
        return DecodeCached($"{older.Hash}:{older.PathAtCommit}",
            () => _git.GetBlobBytes(_repoRoot, older.Hash, older.PathAtCommit ?? ""));
    }

    private ImageData? DecodeCached(string key, Func<byte[]?> fetchBytes)
    {
        if (_decodeCache.TryGetValue(key, out var cached))
        {
            Debug.WriteLine($"[PngBlame]   blob '{key}': CACHE HIT");
            return cached;
        }

        // Time git-fetch (subprocess + I/O) separately from SkiaSharp decode — they need different fixes.
        var sw = Stopwatch.StartNew();
        var bytes = fetchBytes();
        long fetchMs = sw.ElapsedMilliseconds;

        sw.Restart();
        var image = Decode(bytes);
        long decodeMs = sw.ElapsedMilliseconds;

        Debug.WriteLine($"[PngBlame]   blob '{key}': git-fetch {fetchMs}ms ({(bytes?.Length ?? 0):N0} bytes), " +
            $"decode {decodeMs}ms ({image?.Width ?? 0}×{image?.Height ?? 0})");

        _decodeCache[key] = image;
        _decodeOrder.Enqueue(key);
        if (_decodeOrder.Count > DecodeCacheCap)
            _decodeCache.Remove(_decodeOrder.Dequeue());
        return image;
    }

    private static byte[]? ReadFileBytes(string path)
    {
        try { return File.ReadAllBytes(path); }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    // Decodes PNG bytes into normalized, unpremultiplied RGBA so per-channel diffing is meaningful
    // regardless of the source's native pixel layout.
    private static ImageData? Decode(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
            return null;

        using var bitmap = SKBitmap.Decode(bytes);
        if (bitmap is null)
            return null;

        using var pixmap = bitmap.PeekPixels();
        if (pixmap is null)
            return null;

        var info = new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var rgba = new byte[info.Width * info.Height * 4];
        var handle = GCHandle.Alloc(rgba, GCHandleType.Pinned);
        try
        {
            if (!pixmap.ReadPixels(info, handle.AddrOfPinnedObject(), info.Width * 4, 0, 0))
                return null;
        }
        finally
        {
            handle.Free();
        }
        return new ImageData(info.Width, info.Height, rgba);
    }
}
