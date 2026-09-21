using AnimationEditor.Core.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tiled;

/// <summary>
/// Searches a directory tree for <c>.tiledsync</c> companion files (see
/// <see cref="IO.IoManager.AddAssociatedTiledTilesetPath"/>) that already associate some achx/achj
/// with a given <c>.tsx</c> path -- used by <see
/// cref="AnimationEditor.Core.ProjectManager.LoadTsxProject"/> to refuse opening that <c>.tsx</c>
/// natively while achx-push already targets it (issue #1147), instead of letting the two features
/// silently fight over the same file.
/// </summary>
/// <remarks>
/// A <c>.tiledsync</c> file lives next to its OWNING achx/achj, never next to the tsx it targets --
/// so there is no single companion path to check directly for a given tsx. This recursively scans
/// the tsx's own directory and subdirectories for every <c>.tiledsync</c> file and resolves each
/// one's relative paths against its own folder. An achx/achj living outside that directory tree (a
/// sibling or parent folder) is not found -- narrower than a perfect search, but consistent with how
/// every other relative path in this file format is already resolved from the achx's own folder,
/// and matches the ordinary case of an achx/achj and its tsx living in one content folder.
/// </remarks>
public static class TiledSyncAssociationScanner
{
    /// <summary>
    /// Returns the absolute achx/achj path for every <c>.tiledsync</c> association pointing at
    /// <paramref name="tsxPath"/> -- or, if no sibling <c>.achx</c>/<c>.achj</c> file is found next
    /// to a matching <c>.tiledsync</c>, the <c>.tiledsync</c> path itself. Empty when none are
    /// found. A corrupt <c>.tiledsync</c> encountered during the scan is skipped rather than
    /// surfaced: it can't prove an association either way, and this is an exploratory scan, not the
    /// normal load-and-sync flow where a parse failure has real meaning.
    /// </summary>
    public static IReadOnlyList<string> FindAssociationsTargeting(string tsxPath)
    {
        var targetTsx = new FilePath(tsxPath);
        var tsxDir = targetTsx.GetDirectoryContainingThis();
        if (!Directory.Exists(tsxDir.FullPath))
            return Array.Empty<string>();

        var owners = new List<string>();
        foreach (var syncFile in Directory.EnumerateFiles(tsxDir.FullPath, "*.tiledsync", SearchOption.AllDirectories))
        {
            AETiledSyncSave? settings;
            try
            {
                var json = File.ReadAllText(syncFile);
                settings = JsonSerializer.Deserialize(json, AETiledSyncJsonContext.Default.AETiledSyncSave);
            }
            catch
            {
                continue;
            }
            if (settings == null || settings.TiledTilesetPaths.Count == 0)
                continue;

            var syncFolder = new FilePath(syncFile).GetDirectoryContainingThis();
            bool matches = settings.TiledTilesetPaths
                .Any(relative => new FilePath(syncFolder.FullPath + relative) == targetTsx);
            if (matches)
                owners.Add(FindOwningAchxPath(syncFile));
        }
        return owners;
    }

    private static string FindOwningAchxPath(string tiledSyncFile)
    {
        var stem = tiledSyncFile.Substring(0, tiledSyncFile.Length - ".tiledsync".Length);
        if (File.Exists(stem + ".achx")) return stem + ".achx";
        if (File.Exists(stem + ".achj")) return stem + ".achj";
        return tiledSyncFile;
    }
}
