using AnimationEditor.Core.IO;
using AnimationEditor.Core.Paths;

namespace AnimationEditor.Editing;

/// <summary>Per-file outcomes of a batch conversion plus the aggregate <see cref="EditResult"/>.</summary>
public sealed record AchxToAchjBatchResult(IReadOnlyList<AchxConversionResult> Files, EditResult Result)
{
    public IEnumerable<AchxConversionResult> Converted => Files.Where(f => f.Status == AchxConversionStatus.Converted);
    public IEnumerable<AchxConversionResult> Skipped => Files.Where(f => f.Status == AchxConversionStatus.SkippedTargetExists);
    public IEnumerable<AchxConversionResult> Failed => Files.Where(f => f.Status == AchxConversionStatus.Failed);
}

/// <summary>
/// Batch <c>.achx</c> to <c>.achj</c> conversion (#1376). Writes only the new <c>.achj</c> files:
/// never overwrites one, never touches the source, and writes nothing for a file that fails.
/// What happens to the originals (recycling, closing tabs) is the caller's decision.
/// </summary>
public static class AchxToAchjBatch
{
    /// <summary>
    /// Every <c>.achx</c> under <paramref name="folderPath"/>, recursively and sorted, skipping
    /// anything under a <c>bin</c> or <c>obj</c> folder.
    /// </summary>
    public static IReadOnlyList<string> FindSources(string folderPath) =>
        FolderWalk.Files(folderPath)
            .Where(f => Path.GetExtension(f).Equals(".achx", StringComparison.OrdinalIgnoreCase))
            .Where(f => !BinObjPathFilter.IsExcluded(Path.GetRelativePath(folderPath, f)))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static AchxToAchjBatchResult Convert(IEnumerable<string> sources)
    {
        var files = new List<AchxConversionResult>();
        var changed = new List<string>();
        var errors = new List<string>();
        var warnings = new List<string>();

        foreach (var source in sources)
        {
            var result = AchxToAchjConverter.Convert(source);
            files.Add(result);
            var name = new FilePath(source).NoPath;
            switch (result.Status)
            {
                case AchxConversionStatus.Converted:
                    changed.Add(result.TargetPath!);
                    break;
                case AchxConversionStatus.SkippedTargetExists:
                    warnings.Add($"{name}: skipped (an .achj already exists)");
                    break;
                default:
                    errors.Add($"{name}: {result.Error}");
                    break;
            }
        }

        return new AchxToAchjBatchResult(files, new EditResult(changed, errors, warnings));
    }
}
