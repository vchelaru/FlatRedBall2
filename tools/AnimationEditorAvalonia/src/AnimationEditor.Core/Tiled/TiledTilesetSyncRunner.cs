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

        foreach (var tsxPath in tsxAbsolutePaths)
        {
            try
            {
                var tileset = DotTiled.Serialization.Loader.Default().LoadTileset(tsxPath);
                var tilesetInfo = BuildTilesetInfo(tileset);
                var sourceLabel = new FilePath(achxFilePath).RelativeTo(new FilePath(tsxPath).GetDirectoryContainingThis());

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

    private static TilesetAnimationInfo BuildTilesetInfo(Tileset tileset)
    {
        var image = tileset.Image.HasValue ? tileset.Image.Value : null;
        return new TilesetAnimationInfo
        {
            TileWidth = tileset.TileWidth,
            TileHeight = tileset.TileHeight,
            ColumnCount = tileset.Columns,
            Margin = tileset.Margin,
            TileSpacing = tileset.Spacing,
            ImageFileName = image?.Source.HasValue == true ? image.Source.Value : "",
            TextureWidth = image?.Width.HasValue == true ? image.Width.Value : null,
            TextureHeight = image?.Height.HasValue == true ? image.Height.Value : null,
        };
    }
}
