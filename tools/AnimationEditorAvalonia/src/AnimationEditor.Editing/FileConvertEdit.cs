using AnimationEditor.Core.Conversions;
using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>
/// Converts an animation file between <c>.achx</c> (XML) and <c>.achj</c> (JSON) next to the source (#1811).
/// Only the serialization changes: coordinates, unit, texture names and every frame value are written as parsed.
/// Never overwrites the target and never touches the source. What the other format does not keep is a warning, and a file
/// whose animations would come out different is refused (<see cref="EditErrorCode.WouldLoseData"/>), with the check Convert
/// to .achj uses (<see cref="ConversionLoss"/>).
/// </summary>
public static class FileConvertEdit
{
    /// <param name="path">The source .achx or .achj.</param>
    /// <param name="to">"achj" or "achx"; must differ from the source's format.</param>
    /// <returns>The new file's path as the single changed item.</returns>
    public static EditResult Convert(string path, string to)
    {
        to = to.ToLowerInvariant();
        if (to is not ("achj" or "achx"))
            return Fail(EditErrorCode.InvalidValue, $"Unknown target format '{to}': use achj or achx.");

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext != ".achx" && ext != ".achj")
            return Fail(EditErrorCode.UnsupportedFile, $"Only .achx and .achj files can be converted: {path}");
        if (!File.Exists(path))
            return Fail(EditErrorCode.NotFound, $"File not found: {path}");
        if (ext == "." + to)
            return Fail(EditErrorCode.AlreadyExists, $"{Path.GetFileName(path)} is already an .{to} file.");

        if (to == "achj")
        {
            var batch = AchxToAchjBatch.Convert([path]);
            return batch.Skipped.Any()
                ? Fail(EditErrorCode.AlreadyExists, $"{Path.GetFileName(Path.ChangeExtension(path, ".achj"))} already exists; remove it first.")
                : batch.Result;
        }

        var target = Path.ChangeExtension(path, ".achx");
        if (File.Exists(target))
            return Fail(EditErrorCode.AlreadyExists, $"{Path.GetFileName(target)} already exists; remove it first.");
        try
        {
            var text = File.ReadAllText(path);
            var list = AnimationChainListSave.FromJsonString(text);
            var loss = ConversionLoss.ToAchx(text, list);
            if (loss.Difference is { } difference)
                return Fail(EditErrorCode.WouldLoseData, Refusal(Path.GetFileName(path), difference));
            list.Save(target);
            return new EditResult([target], [], Warnings(Path.GetFileName(path), loss).ToList());
        }
        catch (Exception ex)
        {
            if (File.Exists(target) && new FileInfo(target).Length == 0) File.Delete(target);
            return Fail(EditErrorCode.IoFailed, $"Could not convert {Path.GetFileName(path)}: {ex.Message}");
        }
    }

    internal static string Refusal(string name, ConversionDifference difference) =>
        $"{name} is not converted: {difference.What}{(difference.Where is null ? "" : $" ({difference.Where})")} would not be kept. Remove or change it first.";

    internal static IEnumerable<string> Warnings(string name, ConversionLoss loss) =>
        ConversionDrop.Describe(loss.Drops).Select(notKept => $"{name}: {notKept}.");

    private static EditResult Fail(EditErrorCode code, string message) => new([], [message], [], [code]);
}
