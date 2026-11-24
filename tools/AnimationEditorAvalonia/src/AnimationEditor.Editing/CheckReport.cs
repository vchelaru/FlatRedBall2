using AnimationEditor.Core.Export;
using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

public enum CheckSeverity { Error, Warning }

/// <param name="Code">Stable kebab-case identifier: missing-texture, duplicate-chain-name, duplicate-shape-name, unit-undefined, region-outside-texture, empty-chain, zero-length-frame, texture-case.</param>
public sealed record CheckIssue(CheckSeverity Severity, string Code, string Message);

/// <summary>
/// Validation of a loaded animation file (#1811). Errors are things a game would trip on (a texture that
/// is not there, a name that is not unique); warnings are legal but suspect. Edits are judged by
/// <see cref="NewErrors"/>: a file that already had errors can still be edited, but not if the edit makes more errors of any code. Warnings
/// never block an edit; <see cref="RaisedWarnings"/> says which an edit made more of.
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
                $"{group.Count()} chains are named '{group.Key}'."));

        var textures = new Dictionary<string, Texture>();
        for (int c = 0; c < doc.AnimationChains.Count; c++)
        {
            var chain = doc.AnimationChains[c];
            if (chain.Frames.Count == 0)
                issues.Add(new(CheckSeverity.Warning, "empty-chain", $"Chain '{chain.Name}' has no frames."));
            for (int f = 0; f < chain.Frames.Count; f++)
            {
                var frame = chain.Frames[f];
                if (!string.IsNullOrEmpty(frame.TextureName))
                {
                    if (!textures.TryGetValue(frame.TextureName, out var texture))
                        textures[frame.TextureName] = texture = Texture.Read(frame.TextureName, folder);
                    texture.Frames.Add($"{chain.Name}/{f}");
                    if (RegionProblem(frame, doc.CoordinateType, texture.Size) is { } problem)
                        issues.Add(new(CheckSeverity.Warning, "region-outside-texture", $"Frame {chain.Name}/{f}: {problem}"));
                }

                if (chain.Frames.Count > 1 && !(frame.FrameLength > 0))
                    issues.Add(new(CheckSeverity.Warning, "zero-length-frame",
                        $"Frame {chain.Name}/{f} has a length of 0 or less in a chain of {chain.Frames.Count} frames."));

                var shapes = frame.ShapesSave?.Shapes.OfType<ShapeSave>() ?? [];
                foreach (var dup in shapes.GroupBy(s => s.Name).Where(g => g.Count() > 1))
                    issues.Add(new(CheckSeverity.Error, "duplicate-shape-name",
                        $"{dup.Count()} shapes are named '{dup.Key}' in frame {chain.Name}/{f}."));
            }
        }

        foreach (var (name, texture) in textures)
        {
            var onDisk = texture.Exists ? TextureCase.NameOnDisk(name, folder) : null;
            if (texture.Exists && onDisk is null) continue;
            var shown = string.Join(", ", texture.Frames.Take(3)) + (texture.Frames.Count > 3 ? $" and {texture.Frames.Count - 3} more" : "");
            issues.Add(onDisk is null
                ? new(CheckSeverity.Error, "missing-texture", $"Texture '{name}' not found (frames: {shown}).")
                : new(CheckSeverity.Warning, "texture-case",
                    $"Texture '{name}' is '{onDisk}' on disk, so it is not found where file names are case-sensitive (frames: {shown})."));
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

    /// <summary>
    /// The errors of each code that <paramref name="after"/> has more of than <paramref name="before"/>: those whose message
    /// is not in <paramref name="before"/>, or all of the code's when every message is. Only counts decide, so an edit that
    /// moves an error to another address or fixes one and causes another of the same code gives none.
    /// </summary>
    public static IReadOnlyList<CheckIssue> NewErrors(CheckReport before, CheckReport after)
    {
        var added = new List<CheckIssue>();
        if (after.Ok) return added; // the usual case; the grouping below is slow to start up
        foreach (var errors in after.Errors.GroupBy(i => i.Code))
        {
            var was = before.Errors.Where(i => i.Code == errors.Key).ToList();
            if (errors.Count() <= was.Count) continue;
            var fresh = errors.Except(was).ToList();
            added.AddRange(fresh.Count > 0 ? fresh : errors);
        }
        return added;
    }

    /// <summary>
    /// One line per warning code that <paramref name="after"/> has more of than <paramref name="before"/>. Only counts are
    /// compared, so an edit that fixes one warning and causes another of the same code gives no line.
    /// </summary>
    internal static IEnumerable<string> RaisedWarnings(CheckReport before, CheckReport after) =>
        from warnings in after.Warnings.GroupBy(i => i.Code)
        let was = before.Warnings.Count(i => i.Code == warnings.Key)
        where warnings.Count() > was
        select $"The edit raised check warnings [{warnings.Key}] from {was} to {warnings.Count()}; run `aecli check`.";
}
