using System;
using System.Collections.Generic;
using System.Linq;
using AnimationEditor.Core.Paths;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Narrows the Files-panel PNG list to the textures referenced by the open .achx — the
/// "This File" scope toggle (issue #615). The result is always a subset of the full
/// (Project-scope) scan, so a referenced texture living outside the scanned root is not
/// surfaced, matching the fact that Project scope wouldn't list it either.
/// </summary>
/// <remarks>
/// Matching is by <b>absolute path</b>, not relative: <c>TextureName</c> is stored relative
/// to the .achx folder, whereas <see cref="PngFileEntry.RelativePath"/> is relative to the
/// Files-panel root, and the two roots differ whenever the panel browses a parent
/// <c>Content</c> folder. Both sides are normalized through <see cref="FilePath"/> so the
/// comparison is case-insensitive and slash/<c>../</c>-agnostic across host OSes.
/// </remarks>
public static class FilesPanelScopeFilter
{
    /// <param name="allFiles">The full project scan (Project scope).</param>
    /// <param name="referencedTextureNames">
    /// Texture names from the open .achx, as returned by
    /// <see cref="Data.TextureListBuilder.GetAvailableTextures"/> — relative to the .achx folder.
    /// </param>
    /// <param name="achxFolder">The directory containing the .achx, used to resolve relative names.</param>
    public static IReadOnlyList<PngFileEntry> FilterToReferenced(
        IReadOnlyList<PngFileEntry> allFiles,
        IReadOnlyList<string> referencedTextureNames,
        string? achxFolder)
    {
        if (allFiles.Count == 0 || referencedTextureNames.Count == 0)
            return Array.Empty<PngFileEntry>();

        var referenced = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in referencedTextureNames)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;
            referenced.Add(ResolveToAbsolute(name, achxFolder).Standardized);
        }

        return allFiles
            .Where(f => referenced.Contains(new FilePath(f.AbsolutePath).Standardized))
            .ToList();
    }

    private static FilePath ResolveToAbsolute(string textureName, string? achxFolder)
    {
        if (string.IsNullOrEmpty(achxFolder) || !IsRelative(textureName))
            return new FilePath(textureName);

        var folder = achxFolder.Replace('\\', '/');
        if (!folder.EndsWith("/"))
            folder += "/";
        return new FilePath(folder + textureName);
    }

    private static bool IsRelative(string path) =>
        !(path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':')
        && !path.StartsWith("/")
        && !path.StartsWith("\\");
}
