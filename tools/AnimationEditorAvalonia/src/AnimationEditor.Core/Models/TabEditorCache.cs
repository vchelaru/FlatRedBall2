using AnimationEditor.Core.Paths;
using System;

namespace AnimationEditor.Core.Models
{
    /// <summary>
    /// Per-tab in-memory editor model cache so tab switches can swap project state
    /// without re-parsing the <c>.achx</c> from disk.
    /// </summary>
    public static class TabEditorCache
    {
        /// <summary>
        /// Stores the current <see cref="IProjectManager"/> model on <paramref name="tab"/>.
        /// The tab keeps the same object references so undo snapshots stay valid.
        /// </summary>
        public static void CaptureFromProject(TabEntry tab, IProjectManager pm)
        {
            tab.CachedEditorModel = pm.AnimationChainListSave;
            tab.CachedOnDiskCoordinateType = pm.OnDiskCoordinateType;
            tab.CachedDiskHash = TryHashDisk(tab.Path);
            tab.CachedTsxState = pm.CaptureTsxState();
            tab.CachedReferencedPngs = pm.ReferencedPngs;
        }

        /// <summary>
        /// Returns <c>true</c> when <paramref name="tab"/> has a cached model and the on-disk
        /// file's content has not changed since the cache was captured. Not its write time: two
        /// writes in one clock tick share a time, and a copied-in file keeps its own older one.
        /// </summary>
        public static bool HasFreshCache(TabEntry tab)
        {
            if (tab.CachedEditorModel == null)
                return false;

            if (tab.CachedDiskHash == null)
                return true;

            var diskHash = TryHashDisk(tab.Path);
            if (diskHash == null)
                return true;

            return diskHash.AsSpan().SequenceEqual(tab.CachedDiskHash);
        }

        /// <summary>Discards the cached model so the next activation reloads from disk.</summary>
        public static void Invalidate(TabEntry tab)
        {
            tab.CachedEditorModel = null;
            tab.CachedDiskHash = null;
            tab.SaveFailed = false;
        }

        /// <summary>Copies cached editor state into <paramref name="pm"/>.</summary>
        public static void ApplyToProject(TabEntry tab, IProjectManager pm)
        {
            pm.AnimationChainListSave = tab.CachedEditorModel;
            pm.OnDiskCoordinateType = tab.CachedOnDiskCoordinateType;
            pm.FileName = string.IsNullOrEmpty(tab.Path.Original) ? null : tab.Path.FullPath;
            pm.RestoreTsxState(tab.CachedTsxState);
            pm.ReferencedPngs = tab.CachedReferencedPngs;
        }

        private static byte[]? TryHashDisk(FilePath path) =>
            string.IsNullOrEmpty(path.Original) ? null : HotReload.HotReloadWatcher.TryHash(path.FullPath);
    }
}
