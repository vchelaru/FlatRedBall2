using System;
using System.Security.Cryptography;
using System.Text;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Builds the disk cache file name for a project-tree thumbnail (issue #839). <paramref
/// name="sourceIdentity"/> is hashed only to keep the file name filesystem-safe; the invalidation
/// key is <paramref name="size"/>/<paramref name="modified"/> plus <see cref="RenderVersion"/>,
/// plainly embedded in the name. Size/Modified are the same <see cref="FolderEntrySnapshot"/> pair
/// <c>FolderSnapshotDiff</c> and the hot-reload watcher already use elsewhere in this codebase, and
/// cover a changed source; <see cref="RenderVersion"/> covers a changed renderer. A cache lookup is
/// a filename match, so drift in any of the three produces a different name, i.e. a cache miss that
/// regenerates the thumbnail.
/// </summary>
public static class AchxThumbnailCacheKey
{
    /// <summary>
    /// Identifies the rendering that produced a cached thumbnail. Size/Modified only invalidate the
    /// cache when the <em>source</em> changes, so without this a build that renders thumbnails
    /// differently keeps serving every file written by the old one -- the improvement stays
    /// invisible until each .achx happens to be touched. <b>Bump this whenever
    /// <c>ThumbnailService.RenderFrameThumbnail</c> changes what it draws</b> (v1: filtered
    /// minification, issue #1013). Superseded files are cleaned up by
    /// <c>ProjectTreeThumbnailService.TrySaveToDisk</c>'s existing same-hash glob, so a bump
    /// replaces them rather than accumulating.
    /// </summary>
    public const int RenderVersion = 1;

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
        return $"{hash}_{sizePart}_{modifiedPart}_v{RenderVersion}.png";
    }
}
