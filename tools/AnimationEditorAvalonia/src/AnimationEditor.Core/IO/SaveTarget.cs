using AnimationEditor.Core.Paths;
using System;
using System.Linq;

namespace AnimationEditor.Core.IO;

/// <summary>Checks a path picked to save to. A save dialog's file types do not limit what the user types.</summary>
public static class SaveTarget
{
    /// <summary>
    /// Whether <paramref name="path"/>'s extension belongs to a type the editor opens (a document or an image) that is
    /// not one of <paramref name="ownExtensions"/>. No extension, or one the editor does not know, is not.
    /// </summary>
    public static bool IsAnotherType(string path, params string[] ownExtensions)
    {
        string extension = new FilePath(path).Extension;
        return !ownExtensions.Contains(extension)
            && OpenFileFilters.AnimationFiles.Concat(OpenFileFilters.Textures)
                .Any(filter => filter.Patterns.Contains("*." + extension, StringComparer.Ordinal));
    }
}
