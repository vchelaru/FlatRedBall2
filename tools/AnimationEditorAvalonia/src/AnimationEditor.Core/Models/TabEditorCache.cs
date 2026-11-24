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
            tab.CachedTsxState = pm.CaptureTsxState();
            tab.CachedReferencedPngs = pm.ReferencedPngs;
        }

        /// <summary>Discards the cached model so the next activation reloads from disk.</summary>
        public static void Invalidate(TabEntry tab)
        {
            tab.CachedEditorModel = null;
            tab.CachedDiskState = null;
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
    }
}
