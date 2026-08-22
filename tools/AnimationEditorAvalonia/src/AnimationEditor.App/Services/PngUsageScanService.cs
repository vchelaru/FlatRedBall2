using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Rendering;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.App.Services;

/// <summary>
/// Orchestrates the "Analyze Image Usage" scan (issue #953): finds every animation-chain frame
/// under a project root that references a given PNG, and groups the matches by chain with one
/// color per chain for <see cref="PngPreviewControl.SetUsageRegions"/>.
/// <para>
/// Lives in <c>AnimationEditor.App</c> rather than alongside <see cref="AchxPngUsageScanner"/> in
/// Core (or in <c>AnimationEditor.Views</c> like <c>ProjectTreeThumbnailService</c>) because it
/// needs <see cref="DiskEditorFile.FullPath"/> to identify "is this resolved texture the PNG the
/// user has open" by <see cref="FilePath"/> equality — a desktop-only concept neither Core nor
/// Views has any notion of. The per-file scan/match logic itself stays path-agnostic in Core via
/// an injected <c>isTargetTexture</c> predicate; only that predicate's concrete implementation
/// (here) knows about paths.
/// </para>
/// </summary>
public sealed class PngUsageScanService
{
    // Mirrors ProjectTreeThumbnailService's concurrency cap: a project with many .achx files
    // shouldn't spike CPU or block the UI thread parsing all of them at once.
    private const int MaxConcurrentScans = 4;

    /// <summary>
    /// Scans every <c>.achx</c>/<c>.achj</c> under <paramref name="rootFolder"/> for frames whose
    /// texture resolves to <paramref name="targetPngPath"/>, and groups the results by chain —
    /// one <see cref="UsageChainRegions"/> per chain, in a distinct color, sorted deterministically
    /// so re-running the scan on an unchanged project yields the same colors.
    /// </summary>
    public async Task<IReadOnlyList<UsageChainRegions>> ScanAsync(
        IEditorFolder rootFolder,
        string targetPngPath,
        int targetTextureWidth,
        int targetTextureHeight,
        CancellationToken cancellationToken = default)
    {
        var entries = await AchxFolderScanner.ScanAsync(rootFolder);

        var targetPath = new FilePath(targetPngPath);
        bool IsTargetTexture(IEditorFile file) =>
            file is DiskEditorFile disk && new FilePath(disk.FullPath) == targetPath;

        using var concurrency = new SemaphoreSlim(MaxConcurrentScans);
        var perFileTasks = entries.Select(async entry =>
        {
            await concurrency.WaitAsync(cancellationToken);
            try
            {
                // Task.Run: parsing + per-frame texture resolution is CPU/IO-bound work with
                // nothing to yield on by itself (same reasoning as ProjectTreeThumbnailService).
                return await Task.Run(
                    () => AchxPngUsageScanner.FindMatchesAsync(
                        entry, IsTargetTexture, targetTextureWidth, targetTextureHeight),
                    cancellationToken);
            }
            finally
            {
                concurrency.Release();
            }
        });

        var allMatches = (await Task.WhenAll(perFileTasks)).SelectMany(m => m).ToList();

        var groups = allMatches
            .GroupBy(m => (m.Entry, m.Chain))
            .OrderBy(g => g.Key.Entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Key.Chain.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<UsageChainRegions>(groups.Count);
        for (int i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            var (r, g, b) = ChainUsageColorPalette.GetColor(i);
            var rects = group
                .Select(m => new UsageFrameRegion(
                    group.Key.Chain.Frames.IndexOf(m.Frame) + 1,
                    new SKRect(
                        Math.Min(m.Left, m.Right), Math.Min(m.Top, m.Bottom),
                        Math.Max(m.Left, m.Right), Math.Max(m.Top, m.Bottom))))
                .ToList();

            result.Add(new UsageChainRegions(group.Key.Entry, group.Key.Chain, new SKColor(r, g, b), rects));
        }

        return result;
    }
}
