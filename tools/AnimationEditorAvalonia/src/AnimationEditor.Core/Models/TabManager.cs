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
        /// Opens <paramref name="path"/> as a new tab, or focuses its existing tab if it is
        /// already open. The <see cref="ActiveTab"/> is updated in either case.
        /// </summary>
        public TabOpenResult OpenOrFocus(FilePath path)
        {
            var existing = FindTab(path);
            if (existing != null)
            {
                SetActive(existing);
                return TabOpenResult.Focused;
            }

            var entry = new TabEntry(path);
            _tabs.Add(entry);
            SetActive(entry);
            return TabOpenResult.Opened;
        }

        /// <summary>
        /// Activates the tab for <paramref name="path"/>. No-op if the path is not open.
        /// </summary>
        public void Activate(FilePath path)
        {
            var tab = FindTab(path);
            if (tab != null)
                SetActive(tab);
        }

        /// <summary>
        /// Closes the tab for <paramref name="path"/>. No-op if the path is not open.
        /// When the active tab is closed, the next tab is activated; if none follows,
        /// the previous tab is activated; if none remain, <see cref="ActiveTab"/> becomes <c>null</c>.
        /// </summary>
        public void Close(FilePath path)
        {
            var tab = FindTab(path);
            if (tab == null) return;

            int idx = _tabs.IndexOf(tab);
            _tabs.RemoveAt(idx);

            if (tab != ActiveTab)
                return; // no active-tab change needed

            if (_tabs.Count == 0)
            {
                SetActive(null);
                return;
            }

            // Prefer the tab that moved into this slot; fall back to the one before.
            int nextIdx = Math.Min(idx, _tabs.Count - 1);
            SetActive(_tabs[nextIdx]);
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
            foreach (var p in paths)
                _tabs.Add(new TabEntry(new FilePath(p)));

            if (_tabs.Count == 0)
            {
                SetActive(null);
                return;
            }

            TabEntry? desired = activePath != null ? FindTab(new FilePath(activePath)) : null;
            SetActive(desired ?? _tabs[0]);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private TabEntry? FindTab(FilePath path) =>
            _tabs.FirstOrDefault(t => t.Path == path);

        private void SetActive(TabEntry? tab)
        {
            ActiveTab = tab;
            ActiveChanged?.Invoke(tab);
        }
    }
}
