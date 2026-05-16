using System.IO;
using AnimationEditor.Core.Paths;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Utility methods for converting texture file paths between absolute and relative
/// forms in the context of an .achx project file.
/// </summary>
public static class TexturePathHelper
{
    /// <summary>
    /// Computes the path to store for a texture, relative to the .achx folder when possible.
    /// Returns a forward-slash relative path — including <c>../</c> paths for textures outside
    /// the .achx folder on the same drive — or the original absolute path when no relative path
    /// can be expressed (e.g., the texture is on a different drive).
    /// </summary>
    /// <param name="absoluteTexturePath">The absolute path of the texture file.</param>
    /// <param name="achxFolder">
    /// The directory of the .achx file. If empty, <paramref name="absoluteTexturePath"/> is
    /// returned unchanged.
    /// </param>
    public static string ComputeStorePath(string absoluteTexturePath, string achxFolder)
    {
        if (string.IsNullOrEmpty(achxFolder))
            return absoluteTexturePath;

        // Route through FilePath so Windows drive prefixes are recognized as absolute on Linux too.
        return new FilePath(absoluteTexturePath).RelativeTo(new FilePath(achxFolder));
    }

    /// <summary>
    /// Computes the path to display for a frame texture. If the stored path is already relative,
    /// it is returned unchanged. If it is absolute and a relative path can be computed from the
    /// .achx location, the relative form is returned instead — making the property panel
    /// friendlier regardless of how the path was originally stored.
    /// </summary>
    /// <param name="framePath">
    /// The <see cref="FlatRedBall2.Animation.Content.AnimationFrameSave.TextureName"/> value.
    /// </param>
    /// <param name="achxPath">
    /// The full path to the .achx file, or <see langword="null"/> if the project has not been saved.
    /// </param>
    public static string ComputeDisplayPath(string? framePath, string? achxPath)
    {
        if (string.IsNullOrEmpty(framePath)) return string.Empty;
        // Use FilePath's drive-aware absoluteness check (Path.IsPathRooted misses C:/... on Linux).
        var frameFilePath = new FilePath(framePath);
        if (!IsAbsolute(framePath)) return framePath;
        if (string.IsNullOrEmpty(achxPath)) return framePath;

        var achxFolder = new FilePath(achxPath).GetDirectoryContainingThis();
        if (string.IsNullOrEmpty(achxFolder.FullPath)) return framePath;

        string rel;
        try { rel = frameFilePath.RelativeTo(achxFolder); }
        catch (System.ArgumentException) { return framePath; }

        // RelativeTo returns the absolute path unchanged when it cannot make a relative path
        // (e.g., the texture is on a different drive than the .achx).
        return IsAbsolute(rel) ? framePath : rel;
    }

    /// <summary>
    /// Resolves a display path (which may be relative or absolute) to an absolute path
    /// using the .achx folder as the base for relative paths. This is the inverse of
    /// <see cref="ComputeDisplayPath"/>: a relative display path is combined with
    /// <paramref name="achxFolder"/> and normalized via <see cref="Path.GetFullPath"/>.
    /// </summary>
    /// <param name="displayPath">
    /// The path as shown in the texture name TextBox — may be relative (relative to the
    /// .achx folder) or absolute.
    /// </param>
    /// <param name="achxFolder">
    /// The directory of the .achx file. If empty, <paramref name="displayPath"/> is
    /// returned unchanged.
    /// </param>
    public static string ResolveDisplayPath(string displayPath, string achxFolder)
    {
        if (string.IsNullOrEmpty(displayPath)) return string.Empty;
        if (IsAbsolute(displayPath)) return displayPath;
        if (string.IsNullOrEmpty(achxFolder)) return displayPath;
        return Path.GetFullPath(Path.Combine(achxFolder, displayPath));
    }

    private static bool IsAbsolute(string path)
        => Path.IsPathRooted(path)
           || (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':');
}
