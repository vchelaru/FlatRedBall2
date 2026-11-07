using AnimationEditor.Core.Hotkeys;
using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Core.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.App;

public partial class MainWindow
{
    /// <summary>File &gt; New Sequence File: asks where, writes a file with one empty sequence, and opens it.</summary>
    private async Task NewSequenceFileAsync()
    {
        var path = await _appCommands.FileDialogService.PickSaveFileAsync("New Sequence File", "seqj",
            new[] { new FileTypeChoice("seqj", "Sequence") });
        if (string.IsNullOrEmpty(path)) return;
        var save = new SequenceListSave();
        save.Sequences.Add(new SequenceSave { Name = "Sequence" });
        try
        {
            save.Save(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowErrorBanner($"Could not create {new FilePath(path).NoPath}: {ex.Message}");
            return;
        }
        await LoadAnimationFileAsync(path);
    }

    /// <summary>
    /// A <c>.seqj</c> tab: the file's sequences in the top-left list, and the library, timeline and composited
    /// preview in place of the achx editor. Each open file keeps its own <see cref="SequenceDocument"/> for the
    /// window's lifetime and its undo history on the <see cref="TabEntry"/>, like an achx tab, so undo, redo and
    /// save work here while every achx-only command stays off.
    /// </summary>
    private sealed class SequenceTabPane : ITabPane
    {
        private readonly MainWindow _window;
        private readonly Dictionary<FilePath, SequenceDocument> _documents = new();
        private readonly Dictionary<FilePath, int> _selectedIndex = new();
        private HiddenSidebarTabs? _hiddenTabs;
        private SequenceDocument? _document;
        private bool _refreshing;

        public SequenceTabPane(MainWindow window) => _window = window;

        /// <summary>
        /// Shows the sequence list where the ANIMATIONS tree goes and the sequence editor where the achx editor goes.
        /// The Inspector hides: it edits the achx document, which stays loaded behind this tab.
        /// </summary>
        public void Show()
        {
            var w = _window;
            if (_hiddenTabs is null)
            {
                _hiddenTabs = new HiddenSidebarTabs(w.SidebarTabs, w.ProjectTab, w.InspectorTab);
                WireControls();
            }
            w.SequenceListPane.IsVisible = true;
            w.SequenceEditorPane.IsVisible = true;
            _hiddenTabs.Hide();
        }

        public void Hide()
        {
            var w = _window;
            Detach();
            w.SequenceListPane.IsVisible = false;
            w.SequenceEditorPane.IsVisible = false;
            _hiddenTabs?.Restore();
        }

        public void CaptureLeaving(TabEntry tab) => tab.UndoSnapshot = _window._undoManager.TakeSnapshot();

        public Task<bool> LoadAsync(TabEntry tab, TabLoad load)
        {
            if (!_documents.TryGetValue(tab.Path, out var document))
            {
                try
                {
                    document = SequenceDocument.Load(tab.Path.FullPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                {
                    _window.ShowErrorBanner($"Could not open {tab.DisplayName}: {ex.Message}");
                    return Task.FromResult(false);
                }
                _documents[tab.Path] = document;
            }

            Detach();
            _document = document;
            _document.Changed += Refresh;
            // The undo stack is shared by every tab; this tab's own history lives in its snapshot.
            _window._undoManager.Clear();
            if (tab.UndoSnapshot != null)
                _window._undoManager.RestoreSnapshot(tab.UndoSnapshot);
            Refresh();
            return Task.FromResult(true);
        }

        public Task<TabEntry?> PrepareCloseAsync(TabEntry tab) => Task.FromResult<TabEntry?>(tab);

        // Every edit already saved the file; this writes it again, which also retries a failed save.
        public Task SaveAsync(TabEntry? tab)
        {
            _document?.SaveToDisk();
            _window.UpdateStatusBar();
            return Task.CompletedTask;
        }

        public void Closing(TabEntry tab)
        {
            if (_documents.TryGetValue(tab.Path, out var document) && document == _document)
                Detach();
            _documents.Remove(tab.Path);
            _selectedIndex.Remove(tab.Path);
        }

        public TabStatus Status(TabEntry? tab)
        {
            if (_document is null) return new TabStatus(string.Empty, null, tab?.DisplayName ?? string.Empty, string.Empty);
            var (label, brush) = _document.SaveError is null
                ? ("Auto Save On", _autoSaveBrush)
                : ("Auto Save Failed", _failedBrush);
            return new TabStatus(label, brush, _document.Path.NoPath, Plural.Format(_document.Save.Sequences.Count, "sequence"));
        }

        public string? TitlePath(TabEntry? tab) => tab?.Path.FullPath;

        public bool Accepts(HotkeyScope scope) => scope is HotkeyScope.App or HotkeyScope.Document;

        // Detaching re-opens the file in a fresh editor window, which only knows how to load achx.
        public bool CanDetach => false;

        public bool HasProjectPanelRow(TabEntry tab) => true;

        private void WireControls()
        {
            var w = _window;
            w.AddSequenceButton.Click += (_, _) => AddSequence();
            w.SequenceList.SelectionChanged += (_, _) =>
            {
                if (!_refreshing && _document != null)
                    _selectedIndex[_document.Path] = w.SequenceList.SelectedIndex;
            };
            w.SequenceRenameItem.Click += (_, _) => _ = RenameSelectedAsync();
            w.SequenceDeleteItem.Click += (_, _) => DeleteSelected();
        }

        private void AddSequence()
        {
            if (_document is null) return;
            _document.AddSequence(_window._undoManager);
            Select(_document.Save.Sequences.Count - 1);
        }

        private async Task RenameSelectedAsync()
        {
            if (_document is not { } document || _window.SequenceList.SelectedIndex is not (>= 0 and var index)) return;
            string oldName = document.Save.Sequences[index].Name;
            string? name = await _window._appCommands.PromptStringAsync("Rename Sequence", "Sequence name:", oldName);
            if (name is null || document != _document) return;
            if (document.RenameSequence(_window._undoManager, index, name) is { } error)
                _window.ShowErrorBanner(error);
        }

        private void DeleteSelected()
        {
            if (_document is null || _window.SequenceList.SelectedIndex is not (>= 0 and var index)) return;
            _document.DeleteSequence(_window._undoManager, index);
        }

        private void Select(int index)
        {
            if (_document is null) return;
            _selectedIndex[_document.Path] = index;
            Refresh();
        }

        private void Detach()
        {
            if (_document != null)
                _document.Changed -= Refresh;
            _document = null;
        }

        /// <summary>Rebuilds the list from the document, keeping the selection by position.</summary>
        private void Refresh()
        {
            if (_document is null) return;
            var names = _document.Save.Sequences.Select(sequence => sequence.Name).ToList();
            int selected = _selectedIndex.TryGetValue(_document.Path, out int kept) ? kept : 0;
            selected = Math.Min(selected, names.Count - 1);
            _refreshing = true;
            try
            {
                _window.SequenceList.ItemsSource = names;
                _window.SequenceList.SelectedIndex = selected;
            }
            finally
            {
                _refreshing = false;
            }
            _selectedIndex[_document.Path] = selected;
            _window.UpdateStatusBar();
        }
    }
}
