using AnimationEditor.Core.Sequences;
using Avalonia.Media.Imaging;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AnimationEditor.App.Services;

/// <summary>
/// The thumbnails of the sequence list rows inside a <c>.seqj</c> tab: each sequence's composite at the first instant where
/// something shows, from the open document (<see cref="SequenceThumbnailScene"/>), drawn by the same
/// <see cref="SequenceThumbnailRenderer"/> as the Open Project Folder rows. The sprites are chosen on the calling (UI)
/// thread, which is cheap; reading the files, decoding and drawing run on the thread pool, a few at a time. Results are cached in
/// memory under a key made of every drawn sprite and the size and last-write time of every texture it draws, so an edit
/// that does not change a row's picture, or a row moved by a reorder, costs nothing, and a changed texture misses.
/// </summary>
public sealed class SequenceListThumbnailService
{
    private const int MaxConcurrentGenerations = 2;
    private const int MaxCachedThumbnails = 512;

    private readonly SemaphoreSlim _concurrency = new(MaxConcurrentGenerations);
    private readonly object _gate = new();
    private readonly Dictionary<string, Bitmap> _cache = new();

    /// <summary>
    /// The thumbnail of sequence <paramref name="sequenceIndex"/> at most <paramref name="maxPixels"/> square, or null when
    /// nothing in it ever shows or no texture can be read. Never throws for a missing file.
    /// </summary>
    public async Task<Bitmap?> GetAsync(
        SequenceDocument document, int sequenceIndex, int maxPixels, CancellationToken cancellationToken = default)
    {
        if (sequenceIndex < 0 || sequenceIndex >= document.Save.Sequences.Count) return null;
        var layers = SequenceThumbnailScene.LayersFor(document.Save.Sequences[sequenceIndex], document);
        if (layers.Count == 0) return null;

        await _concurrency.WaitAsync(cancellationToken);
        try
        {
            // Task.Run is load-bearing: the stamps, file reads, decode and draw are all synchronous work that would otherwise
            // run inline on the UI thread whenever the semaphore wait completes synchronously.
            return await Task.Run(async () =>
            {
                var inputs = new List<string> { $"size:{maxPixels}" };
                inputs.AddRange(layers.Select(Describe));
                foreach (var texture in layers.Select(layer => layer.TexturePath).Where(path => path is not null).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var info = new FileInfo(texture!);
                    inputs.Add(info.Exists
                        ? SequenceThumbnailCacheKey.FileInput("png", texture!, (ulong)info.Length, info.LastWriteTimeUtc)
                        : $"png:{texture!.ToLowerInvariant()}|missing");
                }
                string key = SequenceThumbnailCacheKey.Fingerprint(inputs);
                lock (_gate)
                    if (_cache.TryGetValue(key, out var hit)) return hit;

                using var composite = await SequenceThumbnailRenderer.RenderAsync(layers, null, DecodeAsync, maxPixels, maxPixels);
                if (composite is null) return null;
                var bitmap = ThumbnailService.ToAvaloniaBitmap(composite);
                lock (_gate)
                {
                    if (_cache.Count >= MaxCachedThumbnails) _cache.Clear();
                    _cache[key] = bitmap;
                }
                return bitmap;
            }, cancellationToken);
        }
        finally
        {
            _concurrency.Release();
        }
    }

    private static string Describe(SequenceThumbnailLayer layer)
    {
        var f = layer.Frame;
        var c = layer.Color;
        return string.Create(CultureInfo.InvariantCulture,
            $"L:{layer.TexturePath?.ToLowerInvariant()}|{f.LeftCoordinate},{f.RightCoordinate},{f.TopCoordinate},{f.BottomCoordinate}|{f.FlipHorizontal},{f.FlipVertical}|{layer.OffsetX},{layer.OffsetY}|{c.Red},{c.Green},{c.Blue},{c.Alpha},{c.Operation}");
    }

    private static Task<SKBitmap?> DecodeAsync(string path)
    {
        try
        {
            return Task.FromResult(File.Exists(path) ? SKBitmap.Decode(File.ReadAllBytes(path)) : null);
        }
        catch (IOException)
        {
            return Task.FromResult<SKBitmap?>(null);
        }
    }
}
