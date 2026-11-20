using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

public enum CheckSeverity { Error, Warning }

/// <param name="Code">Stable kebab-case identifier: missing-texture, duplicate-chain-name, duplicate-shape-name, unit-undefined.</param>
/// <param name="Subject">What the issue is about, independent of where it is (a texture or shape name); how an issue is recognised after an edit renames the chain it sits in. Defaults to the message.</param>
public sealed record CheckIssue(CheckSeverity Severity, string Code, string Message, string? Subject = null);

/// <summary>
/// Validation of a loaded animation file (#1811). Errors are things a game would trip on (a texture that
/// is not there, a name that is not unique); warnings are legal but suspect. Edits are judged by
/// <see cref="NewErrors"/>: a file that already had errors can still be edited, but an edit may not add one.
/// </summary>
public sealed record CheckReport(IReadOnlyList<CheckIssue> Issues)
{
    public IEnumerable<CheckIssue> Errors => Issues.Where(i => i.Severity == CheckSeverity.Error);
    public IEnumerable<CheckIssue> Warnings => Issues.Where(i => i.Severity == CheckSeverity.Warning);
    public bool Ok => !Errors.Any();

    /// <summary>Checks <paramref name="doc"/>; <paramref name="filePath"/> is the file it came from, which relative textures resolve against.</summary>
    public static CheckReport Run(AnimationChainListSave doc, string filePath)
    {
        var issues = new List<CheckIssue>();
        var folder = Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? string.Empty;

        if (doc.TimeMeasurementUnit == TimeMeasurementUnit.Undefined)
            issues.Add(new(CheckSeverity.Warning, "unit-undefined",
                "TimeMeasurementUnit is Undefined; frame lengths are read as seconds."));

        foreach (var group in doc.AnimationChains.GroupBy(c => c.Name).Where(g => g.Count() > 1))
            issues.Add(new(CheckSeverity.Error, "duplicate-chain-name",
                $"{group.Count()} chains are named '{group.Key}'.", group.Key));

        var missing = new Dictionary<string, List<string>>();
        for (int c = 0; c < doc.AnimationChains.Count; c++)
        {
            var chain = doc.AnimationChains[c];
            for (int f = 0; f < chain.Frames.Count; f++)
            {
                var frame = chain.Frames[f];
                if (!string.IsNullOrEmpty(frame.TextureName))
                {
                    var path = Path.IsPathRooted(frame.TextureName) ? frame.TextureName : Path.Combine(folder, frame.TextureName);
                    if (!File.Exists(path))
                    {
                        if (!missing.TryGetValue(frame.TextureName, out var where)) missing[frame.TextureName] = where = [];
                        where.Add($"{chain.Name}/{f}");
                    }
                }

                var shapes = frame.ShapesSave?.Shapes.OfType<ShapeSave>() ?? [];
                foreach (var dup in shapes.GroupBy(s => s.Name).Where(g => g.Count() > 1))
                    issues.Add(new(CheckSeverity.Error, "duplicate-shape-name",
                        $"{dup.Count()} shapes are named '{dup.Key}' in frame {chain.Name}/{f}.", dup.Key));
            }
        }

        foreach (var (texture, where) in missing)
        {
            var shown = string.Join(", ", where.Take(3)) + (where.Count > 3 ? $" and {where.Count - 3} more" : "");
            issues.Add(new(CheckSeverity.Error, "missing-texture", $"Texture '{texture}' not found (frames: {shown}).", texture));
        }

        return new CheckReport(issues);
    }

    /// <summary>The errors of <paramref name="after"/> that <paramref name="before"/> did not already have.</summary>
    public static IReadOnlyList<CheckIssue> NewErrors(CheckReport before, CheckReport after)
    {
        var known = before.Errors.Select(i => (i.Code, i.Subject ?? i.Message)).ToHashSet();
        return after.Errors.Where(i => !known.Contains((i.Code, i.Subject ?? i.Message))).ToList();
    }
}
