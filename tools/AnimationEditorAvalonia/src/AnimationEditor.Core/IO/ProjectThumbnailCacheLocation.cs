using System.IO;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Desktop-only disk cache directory for project-tree thumbnails (issue #839), mirroring
/// <see cref="AppSettingsLocation"/>'s shape. Not used on the browser build, which has no
/// persistent filesystem to cache to -- <c>ProjectTreeThumbnailService</c> falls back to an
/// in-memory-only cache there.
/// </summary>
public static class ProjectThumbnailCacheLocation
{
    public const string FolderName = "AnimationEditor";
    public const string SubfolderName = "ThumbnailCache";

    /// <param name="applicationDataRoot">See <see cref="AppSettingsLocation.ForApplicationDataRoot"/>.</param>
    public static string ForApplicationDataRoot(string applicationDataRoot) =>
        Path.Combine(applicationDataRoot, FolderName, SubfolderName);
}
