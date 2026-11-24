using AnimationEditor.Core.Paths;
using System;
using System.IO;

namespace AnimationEditor.Core.Models
{
    /// <summary>
    /// What the file at <paramref name="Path"/> held when a document was last read from or written to it. It belongs
    /// to that document: the open one, or a tab's cached one.
    /// </summary>
    public sealed record DiskState(FilePath Path, byte[]? Hash, DateTime? WriteTimeUtc)
    {
        /// <summary>Reads the file's current state. A file that can't be read has no hash, a missing one no time either.</summary>
        public static DiskState Read(FilePath path) => new(
            path,
            HotReload.HotReloadWatcher.TryHash(path.FullPath),
            File.Exists(path.FullPath) ? File.GetLastWriteTimeUtc(path.FullPath) : null);

        /// <summary>
        /// Whether <paramref name="path"/>'s content differs. Not its write time: two writes in one clock tick
        /// share a time, and a copied-in file keeps its own older one. The time decides only when the content
        /// can't be read (another process holds the file).
        /// </summary>
        public bool HasChanged(FilePath path)
        {
            var now = Read(path);
            if (now.Hash != null && Hash != null)
                return !now.Hash.AsSpan().SequenceEqual(Hash);
            return now.WriteTimeUtc > WriteTimeUtc;
        }
    }
}
