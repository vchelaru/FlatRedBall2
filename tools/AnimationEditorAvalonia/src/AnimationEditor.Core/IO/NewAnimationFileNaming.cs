using AnimationEditor.Core.Paths;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Naming rules for "New Animation File" on a Project-tree folder row (issue #1018): which
/// extension a new file gets, what name it starts out with, and whether a typed name is legal.
/// Pure string logic so both hosts share it and neither needs a filesystem to test it.
/// </summary>
public static class NewAnimationFileNaming
{
    /// <summary>Extension used when a project has no existing animation files, or an equal
    /// number of each (issue #872 made JSON the going-forward default).</summary>
    public const string JsonExtension = "achj";

    public const string XmlExtension = "achx";

    private const string DefaultStem = "NewAnimation";

    /// <summary>Outcome of <see cref="Resolve"/>: exactly one of the two is non-null.</summary>
    /// <param name="FileName">The file name to create, extension included.</param>
    /// <param name="Error">User-facing reason the typed name was rejected.</param>
    public readonly record struct NameResult(string? FileName, string? Error);

    /// <summary>
    /// The extension a new file in this project should get: whichever of <c>.achx</c>/<c>.achj</c>
    /// the project already uses more of, with <c>.achj</c> winning a tie or an empty project.
    /// Pass every animation file in the project, not just the target folder's -- the convention is
    /// a project-wide property, and the folder being right-clicked is often empty.
    /// </summary>
    public static string ResolveExtension(IEnumerable<string> projectFileNames)
    {
        int achx = 0, achj = 0;
        foreach (var name in projectFileNames)
        {
            if (HasExtension(name, XmlExtension)) achx++;
            else if (HasExtension(name, JsonExtension)) achj++;
        }

        return achx > achj ? XmlExtension : JsonExtension;
    }

    /// <summary>
    /// Name the inline editor starts on: "NewAnimation", or the first numbered variant whose stem
    /// isn't already taken in <paramref name="siblingFileNames"/>. Suffixing is only ever applied
    /// to this suggestion -- a name the user actually types is rejected on collision rather than
    /// silently renamed.
    /// </summary>
    public static string SuggestFileName(IEnumerable<string> siblingFileNames, string extension)
    {
        var taken = StemSet(siblingFileNames);

        if (!taken.Contains(DefaultStem)) return DefaultStem + "." + extension;

        for (int suffix = 2; ; suffix++)
        {
            var candidate = DefaultStem + suffix;
            if (!taken.Contains(candidate)) return candidate + "." + extension;
        }
    }

    /// <summary>
    /// Validates a name typed into the inline editor and turns it into a file name.
    /// <paramref name="typedName"/> may include an <c>.achx</c>/<c>.achj</c> extension, which wins
    /// over <paramref name="projectExtension"/>; anything else is treated as the stem. A stem
    /// already used by a sibling is rejected whichever extension it carries, so <c>Player.achj</c>
    /// collides with an existing <c>Player.achx</c>.
    /// </summary>
    public static NameResult Resolve(
        string typedName, IEnumerable<string> siblingFileNames, string projectExtension)
    {
        var trimmed = (typedName ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return new NameResult(null, "Enter a name.");

        var extension = projectExtension;
        if (HasExtension(trimmed, XmlExtension) || HasExtension(trimmed, JsonExtension))
        {
            extension = trimmed[(trimmed.LastIndexOf('.') + 1)..].ToLowerInvariant();
            trimmed = trimmed[..trimmed.LastIndexOf('.')];
        }

        if (trimmed.Length == 0)
            return new NameResult(null, "Enter a name.");

        if (trimmed.IndexOfAny(InvalidNameChars) >= 0)
            return new NameResult(null, "Name contains invalid characters.");

        if (StemSet(siblingFileNames).Contains(trimmed))
            return new NameResult(null, $"\"{trimmed}\" already exists in this folder.");

        return new NameResult(trimmed + "." + extension, null);
    }

    // Path.GetInvalidFileNameChars() is the host OS's list, so a name legal on Linux but not on
    // Windows would pass here and produce a project other contributors can't check out. The set
    // below is the union both platforms have to live with.
    private static readonly char[] InvalidNameChars = "\\/:*?\"<>|".ToCharArray();

    private static HashSet<string> StemSet(IEnumerable<string> fileNames) =>
        fileNames
            .Where(AchxFolderScanner.IsAchxPath)
            .Select(name => new FilePath(name).NoPathNoExtension)
            .Where(stem => !string.IsNullOrEmpty(stem))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool HasExtension(string fileName, string extension) =>
        fileName.EndsWith("." + extension, StringComparison.OrdinalIgnoreCase);
}
