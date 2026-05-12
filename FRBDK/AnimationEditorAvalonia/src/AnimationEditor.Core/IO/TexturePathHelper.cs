using System.IO;

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

        return Path.GetRelativePath(achxFolder, absoluteTexturePath).Replace('\\', '/');
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
        if (!Path.IsPathRooted(framePath)) return framePath;
        if (string.IsNullOrEmpty(achxPath)) return framePath;

        string achxFolder = Path.GetDirectoryName(achxPath) ?? string.Empty;
        if (string.IsNullOrEmpty(achxFolder)) return framePath;

        string rel;
        try { rel = Path.GetRelativePath(achxFolder, framePath).Replace('\\', '/'); }
        catch (System.ArgumentException) { return framePath; }

        // Path.GetRelativePath returns the absolute path unchanged when it cannot make a relative
        // path (e.g., the texture is on a different drive than the .achx).
        return Path.IsPathRooted(rel) ? framePath : rel;
    }
}
