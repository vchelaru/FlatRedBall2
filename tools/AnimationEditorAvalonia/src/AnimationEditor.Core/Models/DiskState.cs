using System;
using System.IO;

namespace AnimationEditor.Core.Models
{
    /// <summary>
    /// What a document's file held when the editor last read or wrote it, for detecting an external edit
    /// made while its tab was in the background or just before it was left.
    /// </summary>
    internal sealed record DiskState(byte[]? Hash, DateTime? WriteTimeUtc)
    {
        public static DiskState Read(string path) => new(
            HotReload.HotReloadWatcher.TryHash(path),
            File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null);

        /// <summary>
        /// Whether <paramref name="path"/>'s content differs. Not its write time: two writes in one clock tick
        /// share a time, and a copied-in file keeps its own older one. The time decides only when the content
        /// can't be read (another process holds the file).
        /// </summary>
        public bool HasChanged(string path)
        {
            var now = Read(path);
            if (now.Hash != null && Hash != null)
                return !now.Hash.AsSpan().SequenceEqual(Hash);
            return now.WriteTimeUtc > WriteTimeUtc;
        }
    }
}
