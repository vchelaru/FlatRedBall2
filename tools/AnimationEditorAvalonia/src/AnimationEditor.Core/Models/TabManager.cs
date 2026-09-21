using AnimationEditor.Core.Paths;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Models
{
    /// <summary>
    /// Manages the set of open tabs in the Animation Editor.
    /// Each tab corresponds to one open <c>.achx</c> file.
    /// Per-tab view state (zoom, pan, grid) is persisted separately in each
    /// file's companion <c>.aeproperties</c> file and therefore lives outside this class.
    /// </summary>
    public class TabManager
    {
        private readonly List<TabEntry> _tabs = new();
        private int _untitledCounter;

        // MRU back-stack (#911): the tab activated before the current one, most-recent on top.
        // Only ever holds tabs that were genuinely switched away from, never a tab being closed --
        // see the Contains guard in SetActive. Entries for tabs closed in the background go stale
        // and are skipped (not removed) lazily by PopValidHistoryEntry.
        private readonly Stack<TabEntry> _activationHistory = new();

        // Sentinel paths use this prefix so they are distinguishable from real on-disk paths.
        // Moved here from MainWindow (#898) so both hosts, and TabController, can generate
        // "no on-disk file yet" tab paths without duplicating the counter.
        private const string UntitledSentinelPrefix = "__untitled__:";

        /// <summary>All open tabs, in the order they were opened.</summary>
        public IReadOnlyList<TabEntry> Tabs => _tabs;

        /// <summary>The currently active tab, or <c>null</c> when no files are open.</summary>
        public TabEntry? ActiveTab { get; private set; }

        /// <summary>
        /// The full paths of all open tabs, in order. Suitable for serialisation into
        /// <see cref="AnimationEditor.Core.Models.AppSettingsModel.OpenTabPaths"/>.
        /// </summary>
        public IReadOnlyList<string> OpenTabPaths =>
            _tabs.Select(t => t.Path.FullPath).ToArray();

        /// <summary>
        /// Raised whenever <see cref="ActiveTab"/> changes.
        /// The argument is the new active tab (may be <c>null</c> when all tabs are closed).
        /// </summary>
        public event Action<TabEntry?>? ActiveChanged;

        /// <summary>
        /// Raised whenever the open-tab set changes in a way worth persisting: a tab is opened,
        /// focused, activated, closed, moved, renamed, registered, or the whole list is restored.
        /// Unlike <see cref="ActiveChanged"/> this also fires for changes that leave
        /// <see cref="ActiveTab"/> untouched — closing a background tab or reordering — so session
        /// persistence can save on every change instead of only on graceful window close (issue #439).
        /// </summary>
        public event Action? TabsChanged;

    /// <summary>
    /// Opens <paramref name="path"/> as a new tab with an optional display-name override,
    /// or focuses its existing tab if it is already open.
    /// </summary>
    public TabOpenResult OpenOrFocus(FilePath path, string? displayNameOverride)
    {
        var existing = FindTab(path);
        if (existing != null)
        {
            // An explicit open of an already-previewed file (#841) is a deliberate action --
            // File > Open, a recent-files pick, drag-drop -- so it promotes the preview tab to
            // permanent rather than leaving it reusable.
            existing.IsPreview = false;
            SetActive(existing);
            RaiseTabsChanged();
            return TabOpenResult.Focused;
        }

        var entry = new TabEntry(path, displayNameOverride);
        _tabs.Add(entry);
        SetActive(entry);
        RaiseTabsChanged();
        return TabOpenResult.Opened;
    }

    /// <summary>
    /// Opens <paramref name="path"/> as a new tab, or focuses its existing tab if it is
    /// already open. The <see cref="ActiveTab"/> is updated in either case.
    /// </summary>
    public TabOpenResult OpenOrFocus(FilePath path) => OpenOrFocus(path, null);

    /// <summary>
    /// Opens <paramref name="path"/> as the single reusable "preview" tab (issue #841): if a
    /// preview tab already exists it is replaced in place, so a click in the Open Project
    /// Folder tree never accumulates more than one extra tab. Focuses <paramref name="path"/>'s
    /// tab without changing its preview/permanent state if it is already open (whether preview
    /// or permanent). Permanent tabs are never touched.
    /// </summary>
    public TabOpenResult OpenPreview(FilePath path, string? displayNameOverride = null)
    {
        var existing = FindTab(path);
        if (existing != null)
        {
            SetActive(existing);
            RaiseTabsChanged();
            return TabOpenResult.Focused;
        }

        var entry = new TabEntry(path, displayNameOverride) { IsPreview = true };
        var previewTab = _tabs.FirstOrDefault(t => t.IsPreview);
        if (previewTab != null)
            _tabs[_tabs.IndexOf(previewTab)] = entry;
        else
            _tabs.Add(entry);

        SetActive(entry);
        RaiseTabsChanged();
        return TabOpenResult.Opened;
    }

    /// <summary>
    /// Promotes <paramref name="path"/>'s preview tab (issue #841) to a permanent tab -- the
    /// tree row was double-clicked, the file was edited, or its tab was dragged. No-op if
    /// <paramref name="path"/> is not open or is not currently a preview tab.
    /// </summary>
    public void Promote(FilePath path)
    {
        var tab = FindTab(path);
        if (tab is not { IsPreview: true }) return;
        tab.IsPreview = false;
        RaiseTabsChanged();
    }

    /// <summary>
    /// Generates a new, unique "no on-disk file yet" sentinel path for an Untitled tab.
    /// Unique per <see cref="TabManager"/> instance (each host owns one instance, so a
    /// counter local to this class is enough to avoid collisions within that host's session).
    /// </summary>
    public string NewUntitledSentinelPath() => $"{UntitledSentinelPrefix}{++_untitledCounter}";

    /// <summary>True when <paramref name="path"/> was produced by <see cref="NewUntitledSentinelPath"/>.</summary>
    public static bool IsUntitledSentinel(string? path) =>
        path?.StartsWith(UntitledSentinelPrefix, StringComparison.Ordinal) == true;

    /// <summary>
    /// Computes the next unique "Untitled" display name given the set of names already in
    /// use.  Returns <c>"Untitled"</c> if available, then <c>"Untitled (1)"</c>,
    /// <c>"Untitled (2)"</c>, etc.
    /// </summary>
    public static string ComputeUntitledDisplayName(IReadOnlyList<string> existingDisplayNames)
    {
        const string baseName = "Untitled";
        if (!existingDisplayNames.Contains(baseName))
            return baseName;
        for (int i = 1; ; i++)
        {
            var candidate = $"{baseName} ({i})";
            if (!existingDisplayNames.Contains(candidate))
                return candidate;
        }
    }


        /// <summary>
        /// Activates the tab for <paramref name="path"/>. No-op if the path is not open.
        /// </summary>
        public void Activate(FilePath path)
        {
            var tab = FindTab(path);
            if (tab != null)
            {
                SetActive(tab);
                RaiseTabsChanged();
            }
        }

        /// <summary>
        /// Closes the tab for <paramref name="path"/>. No-op if the path is not open.
        /// When the active tab is closed, the tab that was active immediately before it
        /// (issue #911's MRU back-stack) is reactivated, skipping any entries that were
        /// themselves closed in the meantime. If no such entry remains, falls back to the
        /// next tab, or the previous tab if none follows; if no tabs remain, <see cref="ActiveTab"/>
        /// becomes <c>null</c>.
        /// </summary>
        public void Close(FilePath path)
        {
            var tab = FindTab(path);
            if (tab == null) return;

            int idx = _tabs.IndexOf(tab);
            _tabs.RemoveAt(idx);
            PurgeFromHistory(tab);

            // Re-pick the active tab only when the one closed was active. A background-tab
            // close leaves ActiveTab as-is but still changes the open-tab set, so TabsChanged
            // fires regardless (ActiveChanged would not).
            if (tab == ActiveTab)
            {
                var previous = PopValidHistoryEntry();
                if (previous != null)
                    SetActive(previous);
                else if (_tabs.Count == 0)
                    SetActive(null);
                else
                    // Prefer the tab that moved into this slot; fall back to the one before.
                    SetActive(_tabs[Math.Min(idx, _tabs.Count - 1)]);
            }

            RaiseTabsChanged();
        }

        /// <summary>
        /// Pops <see cref="_activationHistory"/> until it finds an entry still present in
        /// <see cref="_tabs"/>, discarding stale entries for tabs closed in the background
        /// along the way. Returns <c>null</c> if the history holds no still-open tab.
        /// </summary>
        private TabEntry? PopValidHistoryEntry()
        {
            while (_activationHistory.Count > 0)
            {
                var candidate = _activationHistory.Pop();
                if (_tabs.Contains(candidate))
                    return candidate;
            }
            return null;
        }

        /// <summary>
        /// Removes every occurrence of <paramref name="tab"/> from <see cref="_activationHistory"/>.
        /// Closing a tab already makes it unreachable via <see cref="PopValidHistoryEntry"/> (it is
        /// no longer in <see cref="_tabs"/>), but without this it would still sit in the stack --
        /// pinning its <see cref="TabEntry.CachedEditorModel"/> and undo state alive -- until some
        /// later close happened to pop deep enough to discard it. Called for every close, not just
        /// active-tab closes, so a background-tab close can't leave that behind either.
        /// </summary>
        private void PurgeFromHistory(TabEntry tab)
        {
            if (!_activationHistory.Contains(tab)) return;

            // Stack<T> enumerates top-to-bottom; reverse before re-pushing so the surviving
            // entries land back in their original order (and the original top stays on top).
            var remaining = _activationHistory.Where(t => t != tab).Reverse().ToArray();
            _activationHistory.Clear();
            foreach (var t in remaining)
                _activationHistory.Push(t);
        }

        /// <summary>
        /// Replaces the current tab list with entries rebuilt from <paramref name="paths"/>.
        /// The tab whose path equals <paramref name="activePath"/> (if any) becomes active;
        /// otherwise the first tab is active. If <paramref name="paths"/> is empty, all tabs
        /// are cleared and <see cref="ActiveTab"/> is set to <c>null</c>.
        /// </summary>
        public void RestoreFrom(IReadOnlyList<string> paths, string? activePath)
        {
            _tabs.Clear();
            // A restored session starts fresh -- old TabEntry references would never be found by
            // PopValidHistoryEntry anyway (new entries are distinct instances), but drop them here
            // rather than let them sit in the stack until popped.
            _activationHistory.Clear();
            foreach (var p in paths)
                _tabs.Add(new TabEntry(new FilePath(p)));

            if (_tabs.Count == 0)
            {
                SetActive(null);
            }
            else
            {
                TabEntry? desired = activePath != null ? FindTab(new FilePath(activePath)) : null;
                SetActive(desired ?? _tabs[0]);
            }

            RaiseTabsChanged();
        }

        // ── Private helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Adds <paramref name="path"/> as a tab at position 0 without making it active
        /// and without raising <see cref="ActiveChanged"/>. Intended for preserving the
        /// currently open file as the first tab before a different file is opened.
        /// Does nothing if <paramref name="path"/> is already tracked.
        /// </summary>
        /// <param name="displayNameOverride">
        /// Tab label override — use <c>"Untitled"</c> for unsaved files with no on-disk path.
        /// </param>
        public void RegisterBackground(FilePath path, string? displayNameOverride = null)
        {
            if (FindTab(path) != null) return;
            _tabs.Insert(0, new TabEntry(path, displayNameOverride));
            RaiseTabsChanged();
        }

        /// <summary>
        /// Moves the tab for <paramref name="path"/> to <paramref name="newIndex"/>, clamped
        /// to [0, Count-1]. No-op if <paramref name="path"/> is not tracked or is already at
        /// the target index. Does not change <see cref="ActiveTab"/>.
        /// </summary>
        public void Move(FilePath path, int newIndex)
        {
            var tab = FindTab(path);
            if (tab == null) return;
            int current = _tabs.IndexOf(tab);
            int target = Math.Clamp(newIndex, 0, _tabs.Count - 1);
            if (current == target) return;
            _tabs.RemoveAt(current);
            _tabs.Insert(target, tab);
            RaiseTabsChanged();
        }

        /// <summary>
        /// Replaces the tab for <paramref name="oldPath"/> with a new entry at the same
        /// position using <paramref name="newPath"/>. If the tab was active it remains so.
        /// No-op if <paramref name="oldPath"/> is not tracked.
        /// Does not raise <see cref="ActiveChanged"/> (the active tab identity may have changed
        /// but the user experience is a simple rename — callers should rebuild the strip).
        /// </summary>
        public void Rename(FilePath oldPath, FilePath newPath)
        {
            var tab = FindTab(oldPath);
            if (tab == null) return;
            int idx = _tabs.IndexOf(tab);
            var replacement = new TabEntry(newPath)
            {
                CachedEditorModel = tab.CachedEditorModel,
                CachedOnDiskCoordinateType = tab.CachedOnDiskCoordinateType,
                CachedDiskWriteTimeUtc = tab.CachedDiskWriteTimeUtc,
                UndoSnapshot = tab.UndoSnapshot,
                CachedTsxState = tab.CachedTsxState,
                CachedTextureSizeState = tab.CachedTextureSizeState,
                CachedReferencedPngs = tab.CachedReferencedPngs,
            };
            _tabs[idx] = replacement;
            if (ActiveTab == tab)
                ActiveTab = replacement;
            RaiseTabsChanged();
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private TabEntry? FindTab(FilePath path) =>
            _tabs.FirstOrDefault(t => t.Path == path);

        private void SetActive(TabEntry? tab)
        {
            // Only push a genuine switch-away, and only while the outgoing tab is still open --
            // this guard is what keeps a tab being closed (already removed from _tabs by the
            // time Close calls SetActive) from being pushed onto its own back-stack.
            if (ActiveTab != null && tab != ActiveTab && _tabs.Contains(ActiveTab))
                _activationHistory.Push(ActiveTab);

            ActiveTab = tab;
            ActiveChanged?.Invoke(tab);
        }

        private void RaiseTabsChanged() => TabsChanged?.Invoke();
    }
}
