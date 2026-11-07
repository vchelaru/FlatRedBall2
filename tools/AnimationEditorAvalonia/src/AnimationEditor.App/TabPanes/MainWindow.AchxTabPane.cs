using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Hotkeys;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Utilities;
using AnimationEditor.Core.ViewModels;
using AnimationEditor.Views.Dialogs;
using System.Linq;
using System.Threading.Tasks;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.App;

public partial class MainWindow
{
    /// <summary>
    /// An <c>.achx</c>/<c>.achj</c>/<c>.tsx</c> document tab, Untitled ones included, and the
    /// editor's state when no tab is open. The editor holds one live document at a time: leaving a
    /// tab snapshots its model, selection, undo history and tree expand state onto the
    /// <see cref="TabEntry"/>, and loading a tab puts them back.
    /// </summary>
    private sealed class AchxTabPane : ITabPane
    {
        private readonly MainWindow _window;

        public AchxTabPane(MainWindow window) => _window = window;

        public void Show()
        {
            _window.AchxEditorPane.IsVisible = true;
            _window.AnimationsBlock.IsVisible = true;
        }

        public void Hide()
        {
            _window.AchxEditorPane.IsVisible = false;
            _window.AnimationsBlock.IsVisible = false;
        }

        public void CaptureLeaving(TabEntry tab)
        {
            _window._tabController.CaptureLeavingTab(tab);
            _window.SaveCompanionFile();
        }

        public async Task<bool> LoadAsync(TabEntry tab, TabLoad load)
        {
            if (load == TabLoad.Restore)
                return await _window._appCommands.OpenProjectWorkflowAsync(tab.Path.FullPath);

            if (IsUntitledTab(tab))
            {
                _window.ActivateUntitledTabContent(tab);
                return true;
            }

            // A failed reload (missing texture, declined UV conversion, unreadable file) leaves
            // the previous tab's document still live -- restoring *this* tab's undo snapshot onto
            // it would apply the wrong tab's history to the wrong document.
            bool activated = await _window._appCommands.ActivateTabContentAsync(tab);
            if (activated && tab.UndoSnapshot != null)
                _window._undoManager.RestoreSnapshot(tab.UndoSnapshot);
            return activated;
        }

        /// <summary>
        /// Prompts to save first when <paramref name="tab"/> is an untitled tab with actual content
        /// (issue #928); an untitled tab that was never edited (or was edited back to empty) closes
        /// silently. "Save" runs the normal Save-As flow; cancelling that dialog keeps the tab open
        /// rather than closing with no file written.
        /// </summary>
        public async Task<TabEntry?> PrepareCloseAsync(TabEntry tab)
        {
            if (!IsUntitledTab(tab) || !UntitledTabHasContent(tab)) return tab;

            var choice = await _window.ShowSaveDiscardCancelDialogAsync(
                $"\"{tab.DisplayName}\" has unsaved changes. Save before closing?",
                "Unsaved Changes");
            if (choice == SaveDiscardCancelChoice.Cancel) return null;
            if (choice == SaveDiscardCancelChoice.Discard) return tab;

            if (tab != _window._tabManager.ActiveTab)
                await _window.ActivateTabAsync(tab);

            await _window._appCommands.SaveCurrentAnimationChainListAsync();
            if (string.IsNullOrEmpty(_window._projectManager.FileName))
                return null; // Save As was cancelled -- leave the tab open, nothing was written.

            // A successful save renames the sentinel tab in place (the CurrentFileChanged
            // handler's Rename), which replaces the TabEntry instance -- re-resolve through
            // ActiveTab rather than closing the now-stale `tab` reference.
            return _window._tabManager.ActiveTab ?? tab;
        }

        /// <summary>
        /// True when <paramref name="tab"/>'s document has content worth prompting to save (issue
        /// #928): the live model for the active tab, or the snapshot captured when a background
        /// tab was last left (<see cref="TabController.CaptureLeavingTab"/>).
        /// </summary>
        private bool UntitledTabHasContent(TabEntry tab) =>
            (tab == _window._tabManager.ActiveTab ? _window._projectManager.AnimationChainListSave : tab.CachedEditorModel)
                is { AnimationChains.Count: > 0 };

        /// <summary>
        /// Closing an untitled tab is an explicit discard of its unsaved content, so clear the
        /// crash-recovery file autosave-on-edit wrote for it, or the next launch offers to restore
        /// already-discarded content (#927).
        /// </summary>
        public void Closing(TabEntry tab)
        {
            if (IsUntitledTab(tab))
                _window._ioManager.DeleteRecoveryFile();
        }

        public TabStatus Status(TabEntry? tab)
        {
            var (label, brush) = _window._undoManager.SaveState switch
            {
                SaveState.AutoSaveOn => ("Auto Save On", _autoSaveBrush),
                SaveState.Failed     => ("Auto Save Failed", _failedBrush),
                _                    => ("Not saved", _unsavedBrush),
            };
            string fileName = string.IsNullOrEmpty(_window._projectManager.FileName)
                ? string.Empty
                : new FilePath(_window._projectManager.FileName).NoPath;
            return new TabStatus(label, brush, fileName, Counts());
        }

        private string Counts()
        {
            var acls = _window._projectManager.AnimationChainListSave;
            var selectedState = _window._selectedState;
            if (acls == null || acls.AnimationChains.Count == 0)
                return string.Empty;
            // Multiple chains selected: a whole-file total or a per-chain breakdown is noise, so
            // just report the selection count (#623).
            if (selectedState.SelectedChains.Count >= 2)
                return $"{selectedState.SelectedChains.Count} chains selected";
            if (selectedState.SelectedFrames.Count >= 2)
                return $"{selectedState.SelectedFrames.Count} frames selected";
            int totalFrames = acls.AnimationChains.Sum(c => c.Frames.Count);
            string totalTime = TimelineBuilder.FormatSeconds(TimelineBuilder.TotalSeconds(acls));
            return $"{Plural.Format(acls.AnimationChains.Count, "chain")} · {Plural.Format(totalFrames, "frame")} · {totalTime}";
        }

        public string? TitlePath(TabEntry? tab) => _window._projectManager.FileName;

        public bool Accepts(HotkeyScope scope) => true;

        public bool CanDetach => true;

        public bool HasProjectPanelRow(TabEntry tab) => !IsUntitledTab(tab);
    }
}
