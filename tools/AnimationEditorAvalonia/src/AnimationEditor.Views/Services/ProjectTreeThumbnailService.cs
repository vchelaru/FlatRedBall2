using AnimationEditor.Core.IO;
using AnimationEditor.Core.Sequences;
using Avalonia.Media.Imaging;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AnimationEditor.App.Services;

/// <summary>
/// Generates first-frame thumbnails for the Open Project Folder <c>.achx</c> tree (issue #839) --
/// unlike <see cref="ThumbnailService"/>, which only ever serves the currently-open project, this
/// resolves each frame's texture relative to <em>that file's own</em> folder via
/// <see cref="AchxFileEntry.ParentFolder"/>, since none of these files are loaded into
/// <c>ProjectManager</c>.
/// <para>
/// Calls are throttled to a small concurrency cap so opening a folder with hundreds of
/// <c>.achx</c> files doesn't spike CPU or block the tree -- see <see cref="_concurrency"/>.
/// </para>
/// </summary>
public sealed class ProjectTreeThumbnailService
{
    private const int MaxConcurrentGenerations = 4;

    private readonly SemaphoreSlim _concurrency = new(MaxConcurrentGenerations);
    private readonly string? _diskCacheDirectory;
    private readonly Dictionary<(AchxFileEntry Entry, int MaxWidth, int MaxHeight), Bitmap?> _memoryCache = new();

    /// <param name="diskCacheDirectory">
    /// Persistent cache directory. Pass <see langword="null"/> to disable disk caching (tests)
    /// -- generation still happens, just re-runs every
    /// session instead of being backed by disk.
    /// </param>
    public ProjectTreeThumbnailService(string? diskCacheDirectory) =>
        _diskCacheDirectory = diskCacheDirectory;

    /// <summary>
    /// Drops <paramref name="entry"/>'s cached bitmap(s) (every requested size) so the next
    /// <see cref="GetThumbnailAsync"/> call re-parses and re-decodes instead of returning stale
    /// art. Callers should invoke this after a save completes for a file also present in the
    /// project tree (issue #839 follow-up: nothing did this before, so an edited-and-saved frame
    /// kept showing its old thumbnail until the folder was reopened). Does not touch the disk
    /// cache directly -- the next generation's own <see cref="TrySaveToDisk"/> already replaces
    /// the stale on-disk file once it writes the new one (same hash prefix, new size/modified
    /// suffix).
    /// </summary>
    public void InvalidateEntry(AchxFileEntry entry)
    {
        foreach (var key in _memoryCache.Keys.Where(k => k.Entry == entry).ToList())
            _memoryCache.Remove(key);
    }

    /// <summary>
    /// Returns a thumbnail for <paramref name="entry"/>'s first frame, or <see langword="null"/>
    /// if the file has no frames, fails to parse, or its texture can't be resolved/decoded --
    /// callers fall back to a generic icon in that case, same as a missing PNG anywhere else in
    /// this codebase.
    /// </summary>
    public async Task<Bitmap?> GetThumbnailAsync(
        AchxFileEntry entry, int maxWidth, int maxHeight, CancellationToken cancellationToken = default)
    {
        var memoryKey = (entry, maxWidth, maxHeight);
        if (_memoryCache.TryGetValue(memoryKey, out var cached))
            return cached;

        await _concurrency.WaitAsync(cancellationToken);
        try
        {
            // Re-check: another caller may have generated this while we waited on the gate.
            if (_memoryCache.TryGetValue(memoryKey, out cached))
                return cached;

            // Task.Run is load-bearing, not decoration: SemaphoreSlim.WaitAsync above and small
            // local-file reads inside LoadOrGenerateAsync routinely complete synchronously, so
            // without it the achx parse + PNG decode + Skia crop -- all CPU-bound, nothing left
            // to actually yield on -- run inline on whichever thread called GetThumbnailAsync.
            // ProjectPanelControl fires the whole per-folder load loop from the UI thread, so
            // that froze the app on a folder with many .achx files (issue #839 follow-up).
            var bitmap = await Task.Run(
                () => LoadOrGenerateAsync(entry, maxWidth, maxHeight, cancellationToken),
                cancellationToken);

            _memoryCache[memoryKey] = bitmap;
            return bitmap;
        }
        finally
        {
            _concurrency.Release();
        }
    }

    private async Task<Bitmap?> LoadOrGenerateAsync(
        AchxFileEntry entry, int maxWidth, int maxHeight, CancellationToken cancellationToken)
    {
        if (entry.IsSequence)
            return await LoadOrGenerateSequenceAsync(entry, maxWidth, maxHeight, cancellationToken);

        string? cacheFilePath = null;
        if (_diskCacheDirectory is not null)
        {
            var snapshot = await entry.File.GetBasicPropertiesAsync();
            var cacheFileName = AchxThumbnailCacheKey.BuildFileName(
                entry.RelativePath, snapshot.Size, snapshot.Modified);
            cacheFilePath = Path.Combine(_diskCacheDirectory, cacheFileName);

            var cached = TryLoadFromDisk(cacheFilePath);
            if (cached is not null)
                return cached;
        }

        var bitmap = await GenerateAsync(entry, maxWidth, maxHeight, cancellationToken);

        if (bitmap is not null && cacheFilePath is not null)
            TrySaveToDisk(cacheFilePath, bitmap);

        return bitmap;
    }

    /// <summary>
    /// A <c>.seqj</c> row: the composite of its first sequence at the first instant where something shows
    /// (<see cref="SequenceThumbnailScene"/>), drawn by <see cref="SequenceThumbnailRenderer"/>. Null when the file is
    /// unreadable, has no sequence, or nothing in the first sequence resolves to a chain with frames. The disk cache key
    /// covers the <c>.seqj</c>'s content, every source it references and every texture those sources use, so a change to any
    /// of them misses the cache; a missing file is part of the key too, so adding it later misses as well.
    /// </summary>
    private async Task<Bitmap?> LoadOrGenerateSequenceAsync(
        AchxFileEntry entry, int maxWidth, int maxHeight, CancellationToken cancellationToken)
    {
        SequenceListSave save;
        string seqjText;
        try
        {
            await using var stream = await entry.File.OpenReadAsync();
            using var reader = new StreamReader(stream);
            seqjText = await reader.ReadToEndAsync(cancellationToken);
            save = SequenceListSave.FromJsonString(seqjText);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null; // Unreadable or malformed .seqj: the row simply has no thumbnail.
        }
        if (save.Sequences.Count == 0) return null;
        var sequence = save.Sequences[0];

        var lookup = new EditorFileSequenceLookup();
        var seqjStamp = await entry.File.GetBasicPropertiesAsync();
        var inputs = new List<string>
        {
            $"seqj:{SequenceThumbnailCacheKey.Fingerprint([seqjText])}",
            SequenceThumbnailCacheKey.FileInput("seqj", entry.RelativePath, seqjStamp.Size, seqjStamp.Modified),
            $"size:{maxWidth}x{maxHeight}",
        };
        var references = sequence.Tracks.SelectMany(track => track.Chains).ToList();
        foreach (var source in references.Select(reference => reference.Source).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var file = await entry.ParentFolder.ResolveRelativeFileAsync(source);
            if (file is null)
            {
                inputs.Add($"src:{source.ToLowerInvariant()}|missing");
                continue;
            }
            var stamp = await file.GetBasicPropertiesAsync();
            inputs.Add(SequenceThumbnailCacheKey.FileInput("src", source, stamp.Size, stamp.Modified));
            try
            {
                string text;
                await using (var stream = await file.OpenReadAsync())
                using (var reader = new StreamReader(stream))
                    text = await reader.ReadToEndAsync(cancellationToken);
                lookup.Add(source, AnimationChainListSave.FromString(text));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A source that will not parse is a missing source: what else resolves is still drawn.
            }
        }

        foreach (var chainSource in references.GroupBy(reference => reference.Source))
        {
            foreach (var chain in chainSource.Select(lookup.FindChain).OfType<AnimationChainSave>().Distinct())
            {
                foreach (var name in chain.Frames.Select(frame => frame.TextureName).Where(name => !string.IsNullOrEmpty(name)).Distinct())
                {
                    var path = RelativeFilePaths.ResolveFrom(chainSource.Key, name!);
                    var texture = await entry.ParentFolder.ResolveRelativeFileAsync(path);
                    var stamp = texture is null ? default : await texture.GetBasicPropertiesAsync();
                    inputs.Add(texture is null
                        ? $"png:{path.ToLowerInvariant()}|missing"
                        : SequenceThumbnailCacheKey.FileInput("png", path, stamp.Size, stamp.Modified));
                }
            }
        }

        var layers = SequenceThumbnailScene.LayersFor(sequence, lookup);
        if (layers.Count == 0) return null;

        string? cacheFilePath = null;
        if (_diskCacheDirectory is not null)
        {
            cacheFilePath = Path.Combine(_diskCacheDirectory, SequenceThumbnailCacheKey.BuildFileName(entry.RelativePath, inputs));
            if (TryLoadFromDisk(cacheFilePath) is { } cached) return cached;
        }

        using var composite = await SequenceThumbnailRenderer.RenderAsync(
            layers, lookup.IsPixelFrame, async path => await DecodeOrNullAsync(entry.ParentFolder, path), maxWidth, maxHeight);
        if (composite is null) return null;

        var bitmap = ThumbnailService.ToAvaloniaBitmap(composite);
        if (cacheFilePath is not null) TrySaveToDisk(cacheFilePath, bitmap);
        return bitmap;
    }

    private static async Task<SKBitmap?> DecodeOrNullAsync(IEditorFolder folder, string path)
    {
        try
        {
            return await folder.ResolveRelativeFileAsync(path) is { } file ? await DecodeAsync(file) : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static async Task<Bitmap?> GenerateAsync(
        AchxFileEntry entry, int maxWidth, int maxHeight, CancellationToken cancellationToken)
    {
        AnimationChainListSave acls;
        try
        {
            string achxText;
            await using (var achxStream = await entry.File.OpenReadAsync())
            using (var reader = new StreamReader(achxStream))
                achxText = await reader.ReadToEndAsync(cancellationToken);
            acls = AnimationChainListSave.FromString(achxText);
        }
        catch
        {
            return null; // Unreadable or malformed .achx -- caller falls back to the generic icon.
        }

        var frame = AchxThumbnailFrameSelector.SelectFirstFrame(acls);
        if (frame is null || string.IsNullOrEmpty(frame.TextureName))
            return null;

        var textureFile = await entry.ParentFolder.ResolveRelativeFileAsync(frame.TextureName);
        if (textureFile is null)
            return null;

        using var textureBitmap = await DecodeAsync(textureFile);
        if (textureBitmap is null)
            return null;

        var uvFrame = acls.CoordinateType == TextureCoordinateType.Pixel
            ? PixelFrameUvConverter.ToUv(frame, textureBitmap.Width, textureBitmap.Height)
            : frame;

        using var thumb = ThumbnailService.RenderFrameThumbnail(
            textureBitmap, uvFrame, default, maxWidth, maxHeight);
        return thumb is null ? null : ThumbnailService.ToAvaloniaBitmap(thumb);
    }

    private static async Task<SKBitmap?> DecodeAsync(IEditorFile file)
    {
        await using var stream = await file.OpenReadAsync();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return SKBitmap.Decode(buffer.ToArray());
    }

    private static Bitmap? TryLoadFromDisk(string cacheFilePath)
    {
        if (!File.Exists(cacheFilePath)) return null;
        try
        {
            return new Bitmap(cacheFilePath);
        }
        catch
        {
            return null; // Corrupt/truncated cache file -- fall through and regenerate.
        }
    }

    /// <summary>
    /// Best-effort disk write: the cache is an optimization, not a correctness requirement, so an
    /// I/O failure (read-only volume, disk full, concurrent access) is swallowed. Also removes any
    /// other cached file for the same source (same hash prefix, stale size/modified suffix) so
    /// repeatedly editing one <c>.achx</c> doesn't accumulate orphaned cache files forever.
    /// </summary>
    private void TrySaveToDisk(string cacheFilePath, Bitmap bitmap)
    {
        try
        {
            Directory.CreateDirectory(_diskCacheDirectory!);

            var hashPrefix = Path.GetFileName(cacheFilePath).Split('_')[0];
            foreach (var stale in Directory.EnumerateFiles(_diskCacheDirectory!, $"{hashPrefix}_*.png"))
                File.Delete(stale);

            using var fileStream = File.Create(cacheFilePath);
            bitmap.Save(fileStream);
        }
        catch
        {
            // Optimization only -- see summary above.
        }
    }
}
