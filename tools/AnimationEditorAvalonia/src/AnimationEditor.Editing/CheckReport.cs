using AnimationEditor.Core.Export;
using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

public enum CheckSeverity { Error, Warning }

/// <param name="Code">Stable kebab-case identifier: missing-texture, duplicate-chain-name, duplicate-shape-name, unit-undefined, region-outside-texture, empty-chain, zero-length-frame, texture-case.</param>
/// <param name="Subject">What the issue is about, independent of where it is (a texture or shape name, or the chain or frame object itself, which edits keep); how an issue is recognised after an edit renames the chain it sits in. Defaults to the message.</param>
public sealed record CheckIssue(CheckSeverity Severity, string Code, string Message, object? Subject = null);

/// <summary>
/// Validation of a loaded animation file (#1811). Errors are things a game would trip on (a texture that
/// is not there, a name that is not unique); warnings are legal but suspect. Edits are judged by
/// <see cref="NewErrors"/>: a file that already had errors can still be edited, but an edit may not add one. The
/// <see cref="NewWarnings"/> of an edit are only reported.
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

        var names = new List<string>();
        foreach (var chain in doc.AnimationChains) names.Add(chain.Name);
        foreach (var (name, count) in Repeats(names))
            issues.Add(new(CheckSeverity.Error, "duplicate-chain-name", $"{count} chains are named '{name}'.", name));

        var textures = new Dictionary<string, Texture>();
        for (int c = 0; c < doc.AnimationChains.Count; c++)
        {
            var chain = doc.AnimationChains[c];
            if (chain.Frames.Count == 0)
                issues.Add(new(CheckSeverity.Warning, "empty-chain", $"Chain '{chain.Name}' has no frames.", chain));
            for (int f = 0; f < chain.Frames.Count; f++)
            {
                var frame = chain.Frames[f];
                if (!string.IsNullOrEmpty(frame.TextureName))
                {
                    if (!textures.TryGetValue(frame.TextureName, out var texture))
                        textures[frame.TextureName] = texture = Texture.Read(frame.TextureName, folder);
                    texture.Frames.Add($"{chain.Name}/{f}");
                    if (RegionProblem(frame, doc.CoordinateType, texture.Size) is { } problem)
                        issues.Add(new(CheckSeverity.Warning, "region-outside-texture", $"Frame {chain.Name}/{f}: {problem}", frame));
                }

                if (chain.Frames.Count > 1 && !(frame.FrameLength > 0))
                    issues.Add(new(CheckSeverity.Warning, "zero-length-frame",
                        $"Frame {chain.Name}/{f} has a length of 0 or less in a chain of {chain.Frames.Count} frames.", frame));

                if (frame.ShapesSave is not { Shapes.Count: > 1 } saved) continue;
                names.Clear();
                foreach (var shape in saved.Shapes)
                    if (shape is ShapeSave named) names.Add(named.Name);
                foreach (var (name, count) in Repeats(names))
                    issues.Add(new(CheckSeverity.Error, "duplicate-shape-name",
                        $"{count} shapes are named '{name}' in frame {chain.Name}/{f}.", name));
            }
        }

        foreach (var (name, texture) in textures)
        {
            var onDisk = texture.Exists ? TextureCase.NameOnDisk(name, folder) : null;
            if (texture.Exists && onDisk is null) continue;
            var shown = string.Join(", ", texture.Frames.Take(3)) + (texture.Frames.Count > 3 ? $" and {texture.Frames.Count - 3} more" : "");
            issues.Add(onDisk is null
                ? new(CheckSeverity.Error, "missing-texture", $"Texture '{name}' not found (frames: {shown}).", name)
                : new(CheckSeverity.Warning, "texture-case",
                    $"Texture '{name}' is '{onDisk}' on disk, so it is not found where file names are case-sensitive (frames: {shown}).", name));
        }

        return new CheckReport(issues);
    }

    /// <summary>The textures of <paramref name="doc"/> that are not on disk, each once; <paramref name="filePath"/> is the file it came from.</summary>
    internal static IEnumerable<string> MissingTextures(AnimationChainListSave doc, string filePath)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? string.Empty;
        return doc.AnimationChains.SelectMany(c => c.Frames).Select(f => f.TextureName).Where(t => !string.IsNullOrEmpty(t)).Distinct()
            .Where(t => !File.Exists(Path.Combine(folder, t)));
    }

    // Each name that appears more than once, with its count. Grouping runs only when there is one: it is slow to start up.
    private static IReadOnlyList<(string Name, int Count)> Repeats(List<string> names) =>
        new HashSet<string>(names).Count == names.Count
            ? []
            : names.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => (g.Key, g.Count())).ToList();

    // Why the frame's region cannot be drawn, or null when it can or the texture's size is unknown (not a PNG).
    private static string? RegionProblem(AnimationFrameSave frame, TextureCoordinateType type, (int W, int H)? size)
    {
        if (size is not { W: > 0, H: > 0 } s || !FrameSourceRect.TryResolve(frame, type, _ => s, out var r)) return null;
        if (r is { X: >= 0, Y: >= 0, Width: > 0, Height: > 0 } && r.X + r.Width <= s.W && r.Y + r.Height <= s.H) return null;
        return $"the region {r.X},{r.Y},{r.Width},{r.Height} is not inside the {s.W}x{s.H} texture '{frame.TextureName}'.";
    }

    /// <summary>One texture the document names: whether it is there, its size when it is a PNG, and the frames using it.</summary>
    private sealed record Texture(bool Exists, (int W, int H)? Size)
    {
        public List<string> Frames { get; } = [];

        public static Texture Read(string name, string folder)
        {
            var path = Path.Combine(folder, name); // an absolute name stays absolute
            return File.Exists(path) ? new(true, TextureCoordinateConverter.TryReadPngSize(path)) : new(false, null);
        }
    }

    /// <summary>The errors of <paramref name="after"/> that <paramref name="before"/> did not already have.</summary>
    public static IReadOnlyList<CheckIssue> NewErrors(CheckReport before, CheckReport after)
    {
        if (after.Ok) return []; // the usual case, and cheaper than building the set
        var known = before.Errors.Select(i => (i.Code, i.Subject ?? i.Message)).ToHashSet();
        return after.Errors.Where(i => !known.Contains((i.Code, i.Subject ?? i.Message))).ToList();
    }

    /// <summary>
    /// The warnings of <paramref name="after"/> beyond those <paramref name="before"/> had. Unlike errors, the same code and
    /// subject can occur many times (every cut-short block of one chain), so they are counted; the ones whose message is new come first.
    /// </summary>
    public static IReadOnlyList<CheckIssue> NewWarnings(CheckReport before, CheckReport after)
    {
        if (!after.Warnings.Any()) return [];
        var known = before.Warnings.ToLookup(i => (i.Code, i.Subject ?? i.Message), i => i.Message);
        return after.Warnings.GroupBy(i => (i.Code, i.Subject ?? i.Message)).SelectMany(group =>
        {
            var old = known[group.Key].ToList();
            return group.OrderBy(i => old.Contains(i.Message)).Take(group.Count() - old.Count);
        }).ToList();
    }

    /// <summary><see cref="NewWarnings"/> as the lines an edit reports.</summary>
    internal static IEnumerable<string> NewWarningLines(CheckReport before, CheckReport after) =>
        NewWarnings(before, after).Select(i => $"The edit introduced a check warning [{i.Code}]: {i.Message}");
}
