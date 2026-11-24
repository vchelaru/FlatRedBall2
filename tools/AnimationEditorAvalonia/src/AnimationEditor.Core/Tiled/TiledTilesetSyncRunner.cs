using AnimationEditor.Core.IO;
using AnimationEditor.Core.Paths;
using DotTiled;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.Tiled;

/// <summary>Result of syncing one .achx/.achj into one associated .tsx file.</summary>
public sealed record TiledTilesetSyncOutcome
{
    public required string TsxPath { get; init; }
    public required bool Success { get; init; }
    public int AppliedCount { get; init; }
    public bool Changed { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public Exception? Error { get; init; }

    public static TiledTilesetSyncOutcome SuccessOutcome(string tsxPath, int appliedCount, bool changed, IReadOnlyList<string> warnings) =>
        new() { TsxPath = tsxPath, Success = true, AppliedCount = appliedCount, Changed = changed, Warnings = warnings };

    public static TiledTilesetSyncOutcome FailureOutcome(string tsxPath, Exception error) =>
        new() { TsxPath = tsxPath, Success = false, Error = error };
}

/// <summary>
/// Orchestrates syncing one .achx/.achj's animation chains into its associated .tsx tileset files
/// (<see cref="IO.IIoManager.GetAssociatedTiledTilesetPaths"/>): for each association, loads the
/// tileset via DotTiled, maps the chains onto tile animations (<see cref="AchjToTiledAnimationMapper"/>),
/// applies them (<see cref="TilesetAnimationSync"/> -- including clearing tiles a since-removed or
/// -renamed chain used to own), and writes the tileset back out (<see cref="TsxWriter"/>).
/// </summary>
/// <remarks>
/// A single .tsx file failing to load/write (missing file, unsupported DotTiled feature) does not
/// abort the rest of the batch -- its outcome is reported as a failure and the loop continues, so
/// one broken association can't block every other tileset from getting synced on save.
/// </remarks>
public static class TiledTilesetSyncRunner
{
    public static IReadOnlyList<TiledTilesetSyncOutcome> SyncAll(
        AnimationChainListSave achj, string achxFilePath, IReadOnlyList<string> tsxAbsolutePaths)
    {
        var outcomes = new List<TiledTilesetSyncOutcome>();
        // Lazy, because it is only read when a texture has an image's file name but another path.
        var images = new Lazy<HashSet<FilePath>>(() => LoadImages(tsxAbsolutePaths));

        foreach (var tsxPath in tsxAbsolutePaths)
        {
            try
            {
                var tileset = TsxLoader.LoadTileset(tsxPath);
                var achxFile = new FilePath(achxFilePath);
                var tsxFolder = new FilePath(tsxPath).GetDirectoryContainingThis();
                var tilesetInfo = BuildTilesetInfo(tileset, tsxFolder, achxFile.GetDirectoryContainingThis())
                    with { IsAnotherTilesetsImage = path => images.Value.Contains(path) };
                var sourceLabel = achxFile.RelativeTo(tsxFolder);

                var results = AchjToTiledAnimationMapper.Map(achj, tilesetInfo, tallySkips: true);
                var syncResult = TilesetAnimationSync.Apply(tileset, results, sourceLabel);
                if (syncResult.Changed)
                    TsxWriter.Write(tileset, tsxPath);

                outcomes.Add(TiledTilesetSyncOutcome.SuccessOutcome(tsxPath, syncResult.AppliedCount, syncResult.Changed, syncResult.Warnings));
            }
            catch (Exception ex)
            {
                outcomes.Add(TiledTilesetSyncOutcome.FailureOutcome(tsxPath, ex));
            }
        }

        return outcomes;
    }

    private static HashSet<FilePath> LoadImages(IReadOnlyList<string> tsxPaths)
    {
        var images = new HashSet<FilePath>();
        foreach (var tsxPath in tsxPaths)
        {
            try
            {
                var tsxFolder = new FilePath(tsxPath).GetDirectoryContainingThis();
                images.Add(new FilePath(ResolveImage(TsxLoader.LoadTileset(tsxPath), tsxFolder)));
            }
            catch
            {
                // Its own sync reports it.
            }
        }
        return images;
    }

    private static string ResolveImage(Tileset tileset, FilePath tsxFolder) =>
        tileset.Image.HasValue && tileset.Image.Value.Source.HasValue
            ? TexturePathHelper.ResolveDisplayPath(tileset.Image.Value.Source.Value, tsxFolder.FullPath)
            : "";

    /// <summary>The image is resolved here, against the .tsx folder, because the frames' texture names are
    /// relative to <paramref name="achxFolder"/> instead.</summary>
    private static TilesetAnimationInfo BuildTilesetInfo(Tileset tileset, FilePath tsxFolder, FilePath achxFolder)
    {
        var image = tileset.Image.HasValue ? tileset.Image.Value : null;
        return new TilesetAnimationInfo
        {
            TileWidth = tileset.TileWidth,
            TileHeight = tileset.TileHeight,
            ColumnCount = tileset.Columns,
            TileCount = tileset.TileCount,
            Margin = tileset.Margin,
            TileSpacing = tileset.Spacing,
            ImageFileName = ResolveImage(tileset, tsxFolder),
            Folder = achxFolder.FullPath,
            TextureWidth = image?.Width.HasValue == true ? image.Width.Value : null,
            TextureHeight = image?.Height.HasValue == true ? image.Height.Value : null,
        };
    }
}
