using AnimationEditor.Core.Paths;
using System;
using System.Linq;

namespace AnimationEditor.Core.IO;

/// <summary>Checks a path picked to save to. A save dialog's file types do not limit what the user types.</summary>
public static class SaveTarget
{
    /// <summary>
    /// Whether <paramref name="path"/>'s extension belongs to a type the editor opens (a document or an image) that is
    /// not one of <paramref name="ownExtensions"/>. No extension, or one the editor does not know, is not. A known
    /// extension followed by dots or spaces is never an own one: Windows drops them and writes the file without them.
    /// </summary>
    public static bool IsAnotherType(string path, params string[] ownExtensions)
    {
        string extension = new FilePath(path.TrimEnd('.', ' ')).Extension;
        return !ownExtensions.Contains(new FilePath(path).Extension)
            && OpenFileFilters.AnimationFiles.Concat(OpenFileFilters.Textures)
                .Any(filter => filter.Patterns.Contains("*." + extension, StringComparer.Ordinal));
    }

    /// <summary>The refusal shown for <paramref name="path"/>; <paramref name="kind"/> is what it may be, with its article ("a .tsx").</summary>
    public static string WrongTypeMessage(string path, string kind) =>
        $"{new FilePath(path).NoPath} is not {kind} file. Give it {kind} extension.";

    /// <summary>The refusal shown for a <paramref name="path"/> that another tab of the window holds.</summary>
    public static string OpenInAnotherTabMessage(string path) =>
        $"{new FilePath(path).NoPath} is open in another tab. Close that tab first.";
}
