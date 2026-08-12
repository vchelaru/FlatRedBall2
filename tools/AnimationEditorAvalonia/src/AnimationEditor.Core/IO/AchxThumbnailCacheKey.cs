using System;
using System.Security.Cryptography;
using System.Text;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Builds the disk cache file name for a project-tree thumbnail (issue #839). <paramref
/// name="sourceIdentity"/> is hashed only to keep the file name filesystem-safe; the invalidation
/// key is <paramref name="size"/>/<paramref name="modified"/> plainly embedded in the name -- the
/// same <see cref="FolderEntrySnapshot"/> pair <c>FolderSnapshotDiff</c> and the hot-reload
/// watcher already use elsewhere in this codebase, so a cache lookup is a filename match: any
/// drift in either value produces a different name, i.e. a cache miss that regenerates the
/// thumbnail.
/// </summary>
public static class AchxThumbnailCacheKey
{
    /// <param name="sourceIdentity">
    /// A string that identifies the source <c>.achx</c>. Callers pass <c>AchxFileEntry.RelativePath</c>
    /// (relative to the scanned Open Project Folder root) rather than a real absolute path --
    /// <c>IEditorFolder</c> has no stable absolute identity on the browser build. Two different
    /// projects could theoretically share a relative path, but a collision also needs matching
    /// <paramref name="size"/>/<paramref name="modified"/> to actually serve a wrong thumbnail,
    /// and even then it self-corrects the next time either file changes.
    /// </param>
    public static string BuildFileName(string sourceIdentity, ulong? size, DateTimeOffset? modified)
    {
        var normalized = sourceIdentity.Replace('\\', '/').ToLowerInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..16];
        var sizePart = size?.ToString() ?? "0";
        var modifiedPart = modified?.UtcTicks.ToString() ?? "0";
        return $"{hash}_{sizePart}_{modifiedPart}.png";
    }
}
