using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.IO;

public enum AchxConversionStatus { Converted, SkippedTargetExists, Failed }

/// <param name="TargetPath">The <c>.achj</c> path; null only when the source path itself was unusable.</param>
/// <param name="Error">Set when <see cref="Status"/> is <see cref="AchxConversionStatus.Failed"/>.</param>
public sealed record AchxConversionResult(
    string SourcePath, string? TargetPath, AchxConversionStatus Status, string? Error = null);

/// <summary>
/// Converts <c>.achx</c> (XML) files to <c>.achj</c> (JSON) next to the source (#1376). Only the
/// serialization changes: the coordinate type, texture names, and every frame value are written
/// as parsed, so no texture needs to exist and the editor's UV/Pixel load handling is not involved.
/// Never overwrites an existing file and never deletes the source -- what happens to the
/// <c>.achx</c> afterward is the caller's decision.
/// </summary>
public static class AchxToAchjConverter
{
    public static AchxConversionResult Convert(string achxPath)
    {
        var targetPath = Path.ChangeExtension(achxPath, ".achj");
        if (File.Exists(targetPath))
            return new AchxConversionResult(achxPath, targetPath, AchxConversionStatus.SkippedTargetExists);

        try
        {
            var text = File.ReadAllText(achxPath);
            if (AchxConflictMarkerDetector.HasConflictMarkers(text))
                throw new InvalidDataException(AchxConflictMarkerDetector.ConflictMarkerMessage);

            AnimationChainListSave.FromString(text).SaveJson(targetPath);
            return new AchxConversionResult(achxPath, targetPath, AchxConversionStatus.Converted);
        }
        catch (Exception ex)
        {
            return new AchxConversionResult(achxPath, targetPath, AchxConversionStatus.Failed, ex.Message);
        }
    }

    /// <summary>
    /// Converts every <c>.achx</c> under <paramref name="folderPath"/>, recursively, skipping
    /// anything under a <c>bin</c> or <c>obj</c> folder (build output copies, same rule as the
    /// Project tree's default exclusion).
    /// </summary>
    public static IReadOnlyList<AchxConversionResult> ConvertFolder(string folderPath) =>
        Directory.EnumerateFiles(folderPath, "*.achx", SearchOption.AllDirectories)
            .Where(f => !BinObjPathFilter.IsExcluded(Path.GetRelativePath(folderPath, f)))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(Convert)
            .ToList();
}
