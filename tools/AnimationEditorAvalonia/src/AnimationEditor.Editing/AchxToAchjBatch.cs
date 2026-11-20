using AnimationEditor.Core.IO;

namespace AnimationEditor.Editing;

/// <summary>Per-file outcomes of a batch conversion plus the aggregate <see cref="EditResult"/>.</summary>
public sealed record AchxToAchjBatchResult(IReadOnlyList<AchxConversionResult> Files, EditResult Result)
{
    public IEnumerable<AchxConversionResult> Converted => Files.Where(f => f.Status == AchxConversionStatus.Converted);
    public IEnumerable<AchxConversionResult> Skipped => Files.Where(f => f.Status == AchxConversionStatus.SkippedTargetExists);
    public IEnumerable<AchxConversionResult> Failed => Files.Where(f => f.Status == AchxConversionStatus.Failed);
}

public static class AchxToAchjBatch
{
    public static IReadOnlyList<string> FindSources(string folderPath) => throw new NotImplementedException();

    public static AchxToAchjBatchResult Convert(IEnumerable<string> sources) => throw new NotImplementedException();
}
