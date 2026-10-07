using System.IO;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Disk cache directory for project-tree thumbnails (issue #839), mirroring
/// <see cref="AppSettingsLocation"/>'s shape.
/// </summary>
public static class ProjectThumbnailCacheLocation
{
    public const string FolderName = "AnimationEditor";
    public const string SubfolderName = "ThumbnailCache";

    /// <param name="applicationDataRoot">See <see cref="AppSettingsLocation.ForApplicationDataRoot"/>.</param>
    public static string ForApplicationDataRoot(string applicationDataRoot) =>
        Path.Combine(applicationDataRoot, FolderName, SubfolderName);
}
