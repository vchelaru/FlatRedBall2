using AnimationEditor.Core.Conversions;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Paths;
using System.Xml.Linq;

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
/// never overwrites one, never touches the source, and writes nothing for a file that fails. One that an <c>.achj</c> cannot
/// hold exactly (<see cref="ConversionLoss"/>) fails with <see cref="EditErrorCode.WouldLoseData"/>.
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
        var codes = new List<EditErrorCode>();
        var check = new AchxElementCheck();

        foreach (var source in sources)
        {
            var name = new FilePath(source).NoPath;
            var target = Path.ChangeExtension(source, ".achj");
            if (!File.Exists(target))
            {
                var (problem, code, hasComments) = Check(source, name, check);
                if (problem is not null)
                {
                    files.Add(new AchxConversionResult(source, target, AchxConversionStatus.Failed, problem));
                    errors.Add(problem);
                    codes.Add(code);
                    continue;
                }
                if (hasComments) warnings.Add($"{name}: comments are not kept.");
            }
            var result = AchxToAchjConverter.Convert(source);
            files.Add(result);
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
                    codes.Add(EditErrorCode.IoFailed);
                    break;
            }
        }

        return new AchxToAchjBatchResult(files, new EditResult(changed, errors, warnings, codes));
    }

    /// <summary>Why the file is not converted, if it is not, and whether it has comments.</summary>
    private static (string? Problem, EditErrorCode Code, bool HasComments) Check(string source, string name, AchxElementCheck check)
    {
        try
        {
            var text = File.ReadAllText(source);
            var list = AnimationFileLoader.Parse(text, new FilePath(source));
            var document = XDocument.Parse(text);
            return ConversionLoss.ToAchj(document, list, check) is { } loss
                ? (FileConvertEdit.Refusal(name, loss), EditErrorCode.WouldLoseData, false)
                : (null, EditErrorCode.Unspecified, AchxElementCheck.HasComments(document));
        }
        catch (Exception ex)
        {
            return ($"{name}: {ex.Message}", EditErrorCode.IoFailed, false);
        }
    }
}
