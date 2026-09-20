using AnimationEditor.Core.Paths;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.IO;

namespace AnimationEditor.Core.Models
{
    /// <summary>
    /// Per-tab in-memory editor model cache so tab switches can swap project state
    /// without re-reading and re-parsing the <c>.achx</c> from disk.
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
            tab.CachedDiskWriteTimeUtc = TryReadDiskWriteTimeUtc(tab.Path);
            tab.CachedTsxState = pm.CaptureTsxState();
        }

        /// <summary>
        /// Returns <c>true</c> when <paramref name="tab"/> has a cached model and the on-disk
        /// file has not changed since the cache was captured.
        /// </summary>
        public static bool HasFreshCache(TabEntry tab)
        {
            if (tab.CachedEditorModel == null)
                return false;

            if (tab.CachedDiskWriteTimeUtc == null)
                return true;

            var diskTime = TryReadDiskWriteTimeUtc(tab.Path);
            if (diskTime == null)
                return true;

            return diskTime.Value <= tab.CachedDiskWriteTimeUtc.Value;
        }

        /// <summary>Discards the cached model so the next activation reloads from disk.</summary>
        public static void Invalidate(TabEntry tab)
        {
            tab.CachedEditorModel = null;
            tab.CachedDiskWriteTimeUtc = null;
        }

        /// <summary>Refreshes only the on-disk timestamp after a save.</summary>
        public static void RefreshDiskTimestamp(TabEntry tab)
        {
            tab.CachedDiskWriteTimeUtc = TryReadDiskWriteTimeUtc(tab.Path);
        }

        /// <summary>Copies cached editor state into <paramref name="pm"/>.</summary>
        public static void ApplyToProject(TabEntry tab, IProjectManager pm)
        {
            pm.AnimationChainListSave = tab.CachedEditorModel;
            pm.OnDiskCoordinateType = tab.CachedOnDiskCoordinateType;
            pm.FileName = string.IsNullOrEmpty(tab.Path.Original) ? null : tab.Path.FullPath;
            pm.RestoreTsxState(tab.CachedTsxState);
        }

        private static DateTime? TryReadDiskWriteTimeUtc(FilePath path)
        {
            if (string.IsNullOrEmpty(path.Original) || !path.Exists())
                return null;

            return File.GetLastWriteTimeUtc(path.FullPath);
        }
    }
}
