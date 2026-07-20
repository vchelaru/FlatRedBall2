using System;
using System.Collections.Generic;
using System.IO;

namespace AnimationEditor.Core.Paths;

/// <summary>
/// Combines a frame's <see cref="FlatRedBall2.Animation.Content.AnimationFrameSave.TextureName"/>
/// with the root-relative directory of the .achx that references it, producing the path to look
/// up among a recursively-listed browser Open Folder grant. Purely string-based -- unlike
/// <see cref="FilePath"/>, it never resolves against <see cref="Environment.CurrentDirectory"/>,
/// which has no meaningful value in the browser build.
/// </summary>
public static class RootRelativePath
{
    /// <summary>
    /// Returns the normalized root-relative path for <paramref name="textureName"/> resolved from
    /// <paramref name="achxDirectory"/> (itself root-relative; <c>""</c> for the granted root).
    /// Returns <see langword="null"/> when <paramref name="textureName"/> is rooted/absolute, or a
    /// <c>../</c> chain walks back past the granted root -- either means the texture lives outside
    /// the folder the user granted access to, which this type cannot resolve on its own.
    /// </summary>
    public static string? Combine(string achxDirectory, string textureName)
    {
        if (string.IsNullOrEmpty(textureName)) return null;
        if (IsRooted(textureName)) return null;

        var stack = new List<string>(Split(achxDirectory));

        foreach (var part in Split(textureName))
        {
            if (part == ".") continue;
            if (part == "..")
            {
                if (stack.Count == 0) return null;
                stack.RemoveAt(stack.Count - 1);
            }
            else
            {
                stack.Add(part);
            }
        }

        return string.Join("/", stack);
    }

    /// <summary>Directory portion of a root-relative path (everything before the last <c>/</c>), or
    /// <c>""</c> when <paramref name="rootRelativePath"/> has no directory segment.</summary>
    public static string DirectoryOf(string rootRelativePath)
    {
        var normalized = rootRelativePath.Replace('\\', '/');
        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash < 0 ? "" : normalized[..lastSlash];
    }

    private static string[] Split(string path) =>
        path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);

    private static bool IsRooted(string path) =>
        Path.IsPathRooted(path) || (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':');
}
