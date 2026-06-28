using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnimationEditor.Core.Paths;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Recursively enumerates PNG files under a folder, grouped by immediate subfolder path.
/// </summary>
public static class PngFolderScanner
{
    /// <summary>
    /// Scans <paramref name="rootFolder"/> and its subfolders for <c>.png</c> files
    /// (case-insensitive extension match). Returns an empty list when the folder is
    /// missing or null.
    /// </summary>
    /// <summary>
    /// Returns every PNG under <paramref name="rootFolder"/>, recursively.
    /// </summary>
    public static IReadOnlyList<PngFileEntry> ListFiles(string? rootFolder)
    {
        if (string.IsNullOrEmpty(rootFolder) || !Directory.Exists(rootFolder))
            return Array.Empty<PngFileEntry>();

        var normalizedRoot = Path.GetFullPath(rootFolder);
        var files = new List<PngFileEntry>();

        foreach (var file in Directory.EnumerateFiles(normalizedRoot, "*", SearchOption.AllDirectories))
        {
            if (!Path.GetExtension(file).Equals(".png", StringComparison.OrdinalIgnoreCase))
                continue;

            string relative = Path.GetRelativePath(normalizedRoot, file).Replace('\\', '/');
            files.Add(new PngFileEntry(file, relative));
        }

        return files;
    }

    public static IReadOnlyList<PngFolderGroup> Scan(string? rootFolder)
    {
        var groups = new Dictionary<string, List<PngFileEntry>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in ListFiles(rootFolder))
        {
            int slash = file.RelativePath.LastIndexOf('/');
            string subfolder = slash < 0 ? string.Empty : file.RelativePath[..slash];

            if (!groups.TryGetValue(subfolder, out var list))
                groups[subfolder] = list = new List<PngFileEntry>();

            list.Add(file);
        }

        return groups
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new PngFolderGroup(
                FormatGroupLabel(kv.Key),
                kv.Value
                    .OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .ToList();
    }

    private static string FormatGroupLabel(string subfolderPath)
    {
        if (string.IsNullOrEmpty(subfolderPath))
            return "(root)";

        return subfolderPath;
    }

    internal static bool IsPngPath(string path) =>
        Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase);
}

public sealed record PngFileEntry(string AbsolutePath, string RelativePath)
{
    public string FileName => new FilePath(AbsolutePath).NoPath;
}

public sealed record PngFolderGroup(string Label, IReadOnlyList<PngFileEntry> Files);
