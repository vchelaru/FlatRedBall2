using AnimationEditor.App.Models;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Hotkeys;
using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Core.Utilities;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
        await CreateSequenceFileAsync(path);
    }

    /// <summary>Writes a file with one empty sequence at <paramref name="path"/> and opens it. Shared by File &gt; New Sequence File and the Animations tab's folder menu.</summary>
    internal async Task CreateSequenceFileAsync(string path)
    {
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
    /// save work here while every achx-only command stays off. The playhead is a <see cref="SequenceClock"/> driven
    /// by a timer; each tick hands the preview the sprites <see cref="SequenceScene.At"/> finds.
    /// </summary>
    private sealed class SequenceTabPane : ITabPane
    {
        private readonly MainWindow _window;
        private readonly Dictionary<FilePath, SequenceDocument> _documents = new();
        private bool _wired;
        private SequenceDocument? _document;
        private bool _refreshing;
        private bool _restoringZoom;
        private List<SequenceListItem> _items = new();

        private readonly SequenceClock _clock = new();
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
        // Real time between ticks, since a DispatcherTimer fires later than its interval (see #526).
        private readonly TickClock _tick = new(Stopwatch.GetTimestamp, Stopwatch.Frequency);

        public SequenceTabPane(MainWindow window) => _window = window;

        /// <summary>
        /// Shows the sequence list where the ANIMATIONS tree goes and the sequence editor where the achx editor goes.
        /// The Inspector tab stays, showing the sequence inspector in place of the achx one, which edits the achx
        /// document that stays loaded behind this tab.
        /// </summary>
        public void Show()
        {
            var w = _window;
            if (!_wired)
            {
                _wired = true;
                WireControls();
            }
            w.SequenceListPane.IsVisible = true;
            w.SequenceEditorPane.IsVisible = true;
            w.AchxInspector.IsVisible = false;
            w.SequenceInspector.IsVisible = true;
        }

        public void Hide()
        {
            var w = _window;
            StopPlayback();
            Detach();
            w.SequencePreview.Composite = null;
            w.SequenceLibrary.Show(null);
            w.SequenceTimeline.Show(null, -1, null);
            w.SequenceListPane.IsVisible = false;
            w.SequenceEditorPane.IsVisible = false;
            w.SequenceInspector.IsVisible = false;
            w.AchxInspector.IsVisible = true;
        }

        public void CaptureLeaving(TabEntry tab) => tab.UndoSnapshot = _window._undoManager.TakeSnapshot();

        public Task<bool> LoadAsync(TabEntry tab, TabLoad load)
        {
            if (_documents.TryGetValue(tab.Path, out var known))
            {
                // A source edited in its own tab (or on disk) while this tab was away: read it again before showing.
                known.ReloadChangedSources();
            }
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
                // Open and tab restore both create the document here, so this is where the remembered sequence applies.
                document.SelectedSequence = SequenceSelectionMemory.IndexToRestore(
                    _window._appSettings.SelectedSequences, document.Path.Standardized, SequenceNames(document));
                // The one place a document is created (open, restore and back/forward all come through here), and
                // later tab switches reuse it, so the notice shows once per open. It stays until dismissed.
                if (NewerVersionNotice.MessageFor(document.Save) is { } notice)
                    _window.Notifications.ShowWarningBanner(notice);
            }

            Detach();
            _document = document;
            _document.Changed += Refresh;
            _clock.Stop();
            // The undo stack is shared by every tab; this tab's own history lives in its snapshot.
            _window._undoManager.Clear();
            if (tab.UndoSnapshot != null)
                _window._undoManager.RestoreSnapshot(tab.UndoSnapshot);
            Refresh();
            RestoreZoom();
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

        /// <summary>
        /// Delete removes what was last clicked: a sequence when the list holds the keyboard focus, else the timeline's
        /// selected chain or track. Clicking a row drops the timeline selection and clicking the timeline moves the
        /// focus there, so the two never both apply.
        /// </summary>
        public void DeleteSelection()
        {
            if (SequenceListHasFocus)
            {
                DeleteSelected();
                FocusSelectedRow();
            }
            else
            {
                _window.SequenceTimeline.DeleteSelected();
            }
        }

        private bool SequenceListHasFocus =>
            _window.FocusManager?.GetFocusedElement() is Visual focused
            && (focused == _window.SequenceList || _window.SequenceList.IsVisualAncestorOf(focused));

        // Opening a menu moves the keyboard focus onto its item, so Edit > Copy and Paste ask which surface held the focus
        // just before the menu took it. Hotkeys never see a menu item focused and read the live focus.
        private ClipboardSurface _surfaceBeforeMenu;

        private enum ClipboardSurface { None, List, Timeline }

        /// <summary>
        /// Where Copy, Cut, Paste and Duplicate act: the sequence list when it holds the focus, the timeline when it does,
        /// else nowhere. The same last-clicked-surface idea as <see cref="DeleteSelection"/>; clicking a list row or the
        /// timeline moves the focus there, so the two never both apply.
        /// </summary>
        private ClipboardSurface Surface =>
            _document is null ? ClipboardSurface.None
            : FocusIsInMenu ? _surfaceBeforeMenu
            : LiveSurface;

        private ClipboardSurface LiveSurface =>
            SequenceListHasFocus ? ClipboardSurface.List
            : TimelineHasFocus ? ClipboardSurface.Timeline
            : ClipboardSurface.None;

        private bool TimelineHasFocus =>
            _window.FocusManager?.GetFocusedElement() is Visual focused
            && (focused == _window.SequenceTimeline || _window.SequenceTimeline.IsVisualAncestorOf(focused));

        private bool FocusIsInMenu =>
            _window.FocusManager?.GetFocusedElement() is Visual focused
            && (focused is MenuItem || focused.FindAncestorOfType<MenuItem>() is not null);

        /// <summary>
        /// Ctrl+C and Edit &gt; Copy put the selection on the clipboard as JSON: the selected sequence when the sequence list is
        /// the focused surface, the selected chains or tracks when the timeline is.
        /// </summary>
        public void CopySelection()
        {
            if (_document is not { } document) return;
            switch (Surface)
            {
                case ClipboardSurface.List when CurrentSequence is not null:
                    _window.CopyTextToClipboard(document.CopySequence(SequenceIndex));
                    break;
                case ClipboardSurface.Timeline when _window.SequenceTimeline.CopySelectionText() is { } text:
                    _window.CopyTextToClipboard(text);
                    break;
            }
        }

        /// <summary>
        /// Ctrl+V and Edit &gt; Paste read a copied sequence (list) or copied chains or tracks (timeline) from the clipboard
        /// and insert them. Text that is not the right kind of copy is ignored without a word; a damaged copy shows the error banner.
        /// </summary>
        public void PasteSelection()
        {
            if (_document is not { } document) return;
            var surface = Surface;
            if (surface == ClipboardSurface.List)
                _ = _window.RunGuardedAsync(() => PasteAsync(document), "Paste");
            else if (surface == ClipboardSurface.Timeline)
                _ = _window.RunGuardedAsync(() => PasteIntoTimelineAsync(document), "Paste");
        }

        /// <summary>Ctrl+X and Edit &gt; Cut: copies, then removes, as one undo entry for the removal.</summary>
        public void CutSelection()
        {
            var surface = Surface;
            if (surface == ClipboardSurface.None) return;
            CopySelection();
            if (surface == ClipboardSurface.List)
            {
                DeleteSelected();
                FocusSelectedRow();
            }
            else
            {
                _window.SequenceTimeline.DeleteSelected();
            }
        }

        /// <summary>Ctrl+D and Edit &gt; Duplicate: the selected sequence (list) or the selected chains or tracks (timeline).</summary>
        public void DuplicateSelection()
        {
            var surface = Surface;
            if (surface == ClipboardSurface.List)
                DuplicateSelected();
            else if (surface == ClipboardSurface.Timeline)
                _window.SequenceTimeline.DuplicateSelected();
        }

        public bool CanCopyPaste => Surface != ClipboardSurface.None;

        private async Task PasteIntoTimelineAsync(SequenceDocument document)
        {
            if (TopLevel.GetTopLevel(_window)?.Clipboard is not { } clipboard) return;
            string? text = await clipboard.TryGetTextAsync();
            if (document != _document) return;
            if (!TimelineClipboard.TryRead(text, out var content, out var error))
            {
                if (error is not null) _window.ShowErrorBanner(error);
                return;
            }
            _window.SequenceTimeline.Paste(content!);
        }

        private async Task PasteAsync(SequenceDocument document)
        {
            if (TopLevel.GetTopLevel(_window)?.Clipboard is not { } clipboard) return;
            string? text = await clipboard.TryGetTextAsync();
            if (document != _document) return;
            if (!SequenceClipboard.TryRead(text, out var content, out var error))
            {
                if (error is not null) _window.ShowErrorBanner(error);
                return;
            }
            var result = document.PasteSequence(_window._undoManager, content!);
            // The pasted sequence starts from its beginning.
            StopPlayback();
            if (result.Errors.Count > 0) _window.ShowErrorBanner(string.Join(" ", result.Errors));
            FocusSelectedRow();
        }

        // Rebuilding the list drops its focus once the old rows are gone; lay it out now and take it back so the keys keep working.
        private void FocusSelectedRow()
        {
            _window.SequenceList.UpdateLayout();
            _window.SequenceList.ContainerFromIndex(_window.SequenceList.SelectedIndex)?.Focus();
        }

        public void TogglePlayback()
        {
            if (_clock.IsPlaying)
                PausePlayback();
            else
                Play();
        }

        // The library takes files dragged from the browser, so a press there must not switch tabs.
        public bool BrowserClickOpensPreview => false;

        // Detaching re-opens the file in a fresh editor window, which only knows how to load achx.
        public bool CanDetach => false;

        public bool HasProjectPanelRow(TabEntry tab) => true;

        private void WireControls()
        {
            var w = _window;
            w.AddSequenceButton.Click += (_, _) => AddSequence();
            w.SequenceList.SelectionChanged += (_, _) =>
            {
                if (_refreshing || _document is null) return;
                _document.SelectedSequence = w.SequenceList.SelectedIndex;
                RememberSelection();
                w.SequenceTimeline.Show(_document, w.SequenceList.SelectedIndex, w._undoManager);
                ShowInspector();
                // Another sequence starts from its beginning.
                StopPlayback();
            };
            // Clicking the row that is already selected raises no SelectionChanged, so the press itself says "the
            // sequence": it drops the timeline selection and brings the sequence fields forward.
            w.SequenceList.AddHandler(InputElement.PointerPressedEvent, OnSequenceListPressed, RoutingStrategies.Bubble, handledEventsToo: true);
            w.SequenceList.DoubleTapped += OnSequenceListDoubleTapped;
            w.SequenceList.AddHandler(InlineRenameBox.CommittedEvent, OnSequenceRenameCommitted);
            w.SequenceList.AddHandler(InlineRenameBox.CancelledEvent, OnSequenceRenameCancelled);
            // Edit > Copy and Paste follow the focus, so they are re-evaluated whenever it moves.
            w.AddHandler(InputElement.GotFocusEvent, OnWindowGotFocus, RoutingStrategies.Bubble, handledEventsToo: true);
            w.SequenceTimeline.ErrorRaised += w.ShowErrorBanner;
            w.SequenceTimeline.RenameTrackRequested += index => _ = RenameTrackAsync(index);
            w.SequenceTimeline.SelectionChanged += OnTimelineSelectionChanged;
            w.SequenceTimeline.ContentChanged += OnTimelineContentChanged;
            w.SequenceTimeline.GoToDefinitionRequested += reference => _ = GoToDefinitionAsync(reference);
            w.SequenceInspector.ErrorRaised += w.ShowErrorBanner;
            w.SequenceInspector.GoToDefinitionRequested += reference => _ = GoToDefinitionAsync(reference);
            w.SequenceLibrary.ChipDragMoved += (chip, e) =>
            {
                w.SequenceTimeline.UpdateChipDrag(chip, TimelinePoint(e), e.KeyModifiers.HasFlag(KeyModifiers.Alt));
                w.SequenceLibrary.DragFeedback = w.SequenceTimeline.ChipDragFeedbackAt(chip, TimelinePoint(e));
            };
            w.SequenceLibrary.ChipDragEnded += (chip, e) =>
            {
                w.SequenceTimeline.UpdateChipDrag(null, null);
                if (e != null && TimelinePoint(e) is { } point)
                    w.SequenceTimeline.DropChip(chip, point);
            };
            // Tunnel, so the menu is built before the ContextMenu's own handler opens it.
            w.SequenceList.AddHandler(Control.ContextRequestedEvent, OnSequenceListContextRequested, RoutingStrategies.Tunnel);
            w.SequenceLibrary.Thumbnails = w._thumbnailService;
            w.SequenceLibrary.AutoplayAll = w._appSettings.LibraryAutoplayAll;
            w.SequenceLibrary.AutoplayAllToggled += on =>
            {
                w._appSettings.LibraryAutoplayAll = on;
                w.SaveSettingsFile();
            };
            w.SequenceTimeline.Thumbnails = w._thumbnailService;
            // A texture replaced on disk: the preview redraws its sprites from the re-decoded sheet.
            w._thumbnailService.PathInvalidated += _ => { if (_document is not null) ShowPlayhead(); };
            w.SequenceLibrary.FilesDropped += AddSources;
            w.SequenceLibrary.RemoveSourceRequested += RemoveSource;
            w.SequenceLibrary.CollapsedSources.Changed += StoreCollapsedSources;
            w.SequenceLibrary.GoToDefinitionRequested += reference => _ = GoToDefinitionAsync(reference);

            _timer.Tick += (_, _) => OnTick();
            w.SequencePlayButton.Click += (_, _) => TogglePlayback();
            w.SequenceStopButton.Click += (_, _) => StopPlayback();
            w.SequenceLoopToggle.Click += (_, _) => SetLoop(w.SequenceLoopToggle.IsChecked == true);
            w.SequenceTimeline.Scrubbed += time =>
            {
                _clock.Seek(time, w.SequenceTimeline.SequenceLength);
                ShowPlayhead();
            };
            w.SequenceTimelineZoom.Attach(w.SequenceTimeline);
            w.SequenceTimeline.ZoomChanged += _ => RememberZoom();
            Controls.TimelineScrollBinder.Attach(w.SequenceTimeline, w.SequenceTimelineHScroll, w.SequenceTimelineVScroll);

            var preview = w.SequencePreview;
            w.SequencePreviewZoom.Attach(preview);
            preview.ShowShapes = w.SequenceShowShapes.IsChecked == true;
            preview.ShowOrigin = w.SequenceShowOrigin.IsChecked == true;
            preview.ShowBoundingBox = w.SequenceShowBoundingBox.IsChecked == true;
            w.SequenceShowShapes.IsCheckedChanged += (_, _) => preview.ShowShapes = w.SequenceShowShapes.IsChecked == true;
            w.SequenceShowOrigin.IsCheckedChanged += (_, _) => preview.ShowOrigin = w.SequenceShowOrigin.IsChecked == true;
            w.SequenceShowBoundingBox.IsCheckedChanged += (_, _) => preview.ShowBoundingBox = w.SequenceShowBoundingBox.IsChecked == true;
        }

        /// <summary>
        /// Selecting on the timeline brings the Inspector forward, unless the History or Shortcuts tab is showing: the
        /// user is reading that while editing.
        /// </summary>
        private void OnTimelineSelectionChanged()
        {
            var w = _window;
            ShowInspector();
            bool selected = w.SequenceTimeline.SelectedChains.Count > 0 || w.SequenceTimeline.SelectedTracks.Count > 0;
            if (selected) BringInspectorForward();
        }

        private void BringInspectorForward()
        {
            var w = _window;
            if (w.SidebarTabs.SelectedItem != w.HistoryTab && w.SidebarTabs.SelectedItem != w.ShortcutsTab)
                w.SidebarTabs.SelectedItem = w.InspectorTab;
        }

        private void OnWindowGotFocus(object? sender, RoutedEventArgs e)
        {
            if (_window._visiblePane != this) return;
            if (!FocusIsInMenu) _surfaceBeforeMenu = _document is null ? ClipboardSurface.None : LiveSurface;
            _window.UpdateDocumentMenuItems();
        }

        private void OnSequenceListPressed(object? sender, PointerPressedEventArgs e)
        {
            var w = _window;
            if (_document is null || !e.GetCurrentPoint(w.SequenceList).Properties.IsLeftButtonPressed) return;
            if (e.Source is not Visual source || source.FindAncestorOfType<ListBoxItem>() is null || source.FindAncestorOfType<InlineRenameBox>() is not null || source is InlineRenameBox) return;
            w.SequenceTimeline.ClearSelection();
            ShowInspector();
            BringInspectorForward();
        }

        /// <summary>
        /// Fills the menu from what was hit: a sequence row offers Rename and Delete, blank space only Add Sequence.
        /// A right-click on a row has already selected it, so Rename and Delete act on the row that was hit.
        /// </summary>
        private void OnSequenceListContextRequested(object? sender, ContextRequestedEventArgs e)
        {
            var w = _window;
            if (w.SequenceList.ContextMenu is not { } menu) return;
            bool onSequence = e.Source is Visual source && source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not null;
            menu.Items.Clear();
            var groups = SequenceListMenuLayout.For(onSequence);
            for (int i = 0; i < groups.Count; i++)
            {
                if (i > 0) menu.Items.Add(new Separator());
                foreach (var command in groups[i])
                {
                    var (header, onClick) = command switch
                    {
                        SequenceListMenuCommand.CopyQualifiedName => ("Copy Qualified Name", (Action)CopyQualifiedName),
                        SequenceListMenuCommand.Rename => ("Rename…", () => _ = RenameSelectedAsync()),
                        SequenceListMenuCommand.DuplicateSequence => ("Duplicate Sequence", DuplicateSelected),
                        SequenceListMenuCommand.Delete => ("Delete", DeleteSelected),
                        SequenceListMenuCommand.AddSequence => ("Add Sequence", AddSequence),
                        _ => throw new ArgumentOutOfRangeException(nameof(command), command, null),
                    };
                    var item = new MenuItem { Header = header };
                    item.Click += (_, _) => onClick();
                    menu.Items.Add(item);
                }
            }
        }

        /// <summary>Copies "<c>Name in path/to/File.seqj</c>" for the selected sequence, the same text the ANIMATIONS tree copies for a chain.</summary>
        private void CopyQualifiedName()
        {
            if (_document is { } document && CurrentSequence is { } sequence)
                _window.CopyTextToClipboard(QualifiedName.Format(document.Path.FullPath, sequence.Name));
        }

        /// <summary>Double-clicking a sequence swaps its name for the same inline box the ANIMATIONS tree uses.</summary>
        private void OnSequenceListDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (_document is null || e.Source is not Visual source || source is InlineRenameBox || source.FindAncestorOfType<InlineRenameBox>() is not null) return;
            if (source.FindAncestorOfType<ListBoxItem>() is not { DataContext: SequenceListItem item } container) return;
            item.IsEditing = true;
            container.GetVisualDescendants().OfType<InlineRenameBox>().FirstOrDefault()?.Begin(item.Name);
            e.Handled = true;
        }

        private void OnSequenceRenameCommitted(object? sender, RoutedEventArgs e)
        {
            if (e.Source is not InlineRenameBox { DataContext: SequenceListItem item } box) return;
            item.IsEditing = false;
            _window.SequenceList.Focus();
            int index = _items.IndexOf(item);
            if (_document is { } document && index >= 0
                && document.RenameSequence(_window._undoManager, index, box.Text ?? string.Empty) is { } error)
                _window.ShowErrorBanner(error);
        }

        private void OnSequenceRenameCancelled(object? sender, RoutedEventArgs e)
        {
            if (e.Source is not InlineRenameBox { DataContext: SequenceListItem item }) return;
            item.IsEditing = false;
            _window.SequenceList.Focus();
        }

        /// <summary>Points the inspector at the timeline's selection. It reads the document, so a drag shows live.</summary>
        private void ShowInspector()
        {
            var w = _window;
            w.SequenceInspector.Show(_document, SequenceIndex, w._undoManager, w.SequenceTimeline.Selection, w.SequenceTimeline.SelectedTrack,
                w.SequenceTimeline.SelectionSummary);
        }

        /// <summary>
        /// The one path from what the sequence says to what every view shows. The timeline rebuilds from the document
        /// after each edit, undo, redo, load and sequence switch, and on every step of a chain drag, and raises
        /// <see cref="SequenceTimelineControl.ContentChanged"/> each time; the inspector, the Loop toggle, the playhead
        /// and the preview all follow from here, so none needs its own call after an edit.
        /// </summary>
        private void OnTimelineContentChanged()
        {
            var w = _window;
            ShowInspector();
            // Hiding the tab empties the timeline after detaching; the stopped readout stays as it was.
            if (_document is null) return;
            w.SequenceLoopToggle.IsChecked = CurrentSequence?.Loop == true;
            // An edit can shorten the sequence under the playhead. A drag step does not move it: Escape restores the
            // length, and the playhead should be where the user left it.
            if (!w.SequenceTimeline.IsDraggingChain)
                _clock.Seek(_clock.Time, w.SequenceTimeline.SequenceLength);
            ShowPlayhead();
        }

        /// <summary>Opens the reference's source file as a tab and selects its chain there (none when the chain is empty). A missing source or chain shows the error banner instead.</summary>
        private async Task GoToDefinitionAsync(SequenceChainReferenceSave reference)
        {
            if (_document is not { } document) return;
            var (path, error) = document.LocateDefinition(reference);
            if (path is null)
            {
                _window.ShowErrorBanner(error!);
                return;
            }
            await _window.OpenAndSelectChainAsync(path, reference.Chain);
        }

        private int SequenceIndex => _window.SequenceList.SelectedIndex;

        private SequenceSave? CurrentSequence =>
            _document is { } document && SequenceIndex >= 0 && SequenceIndex < document.Save.Sequences.Count
                ? document.Save.Sequences[SequenceIndex]
                : null;

        private void Play()
        {
            _clock.Play(_window.SequenceTimeline.SequenceLength);
            if (_clock.IsPlaying)
            {
                _tick.Reset();
                _tick.Tick();
                _timer.Start();
            }
            ShowPlayhead();
        }

        private void PausePlayback()
        {
            _clock.Pause();
            _timer.Stop();
            ShowPlayhead();
        }

        private void StopPlayback()
        {
            _clock.Stop();
            _timer.Stop();
            ShowPlayhead();
        }

        private void OnTick()
        {
            _clock.AdvanceTick(_tick.Tick(), _window.SequenceTimeline.SequenceLength, CurrentSequence?.Loop == true);
            if (!_clock.IsPlaying) _timer.Stop();
            ShowPlayhead();
        }

        /// <summary>Moves the playhead, the preview, the time readout and the play button to the clock.</summary>
        private void ShowPlayhead()
        {
            var w = _window;
            double time = _clock.Time;
            w.SequenceTimeline.Playhead = time;
            w.SequencePreview.Composite = _document is null ? null : SequenceScene.At(_document, SequenceIndex, time);
            w.SequenceTimeText.Text = string.Format(CultureInfo.InvariantCulture, "{0:0.00} s / {1:0.00} s",
                time, w.SequenceTimeline.SequenceLength);
            w.SequencePlayIcon.Path = _clock.IsPlaying
                ? "avares://AnimationEditor.Views/Assets/icons/svg/IconPause.svg"
                : "avares://AnimationEditor.Views/Assets/icons/svg/IconPlay.svg";
        }

        private void SetLoop(bool loop)
        {
            if (_document is null || SequenceIndex < 0) return;
            _document.SetLoop(_window._undoManager, SequenceIndex, loop);
        }

        /// <summary>Where <paramref name="e"/> is on the timeline, or null when it is outside it.</summary>
        private Point? TimelinePoint(PointerEventArgs e)
        {
            var timeline = _window.SequenceTimeline;
            var point = e.GetPosition(timeline);
            return new Rect(timeline.Bounds.Size).Contains(point) ? point : null;
        }

        private async Task RenameTrackAsync(int track)
        {
            if (_document is not { } document || _window.SequenceList.SelectedIndex is not (>= 0 and var sequence)) return;
            string oldName = document.Save.Sequences[sequence].Tracks[track].Name;
            string? name = await _window._appCommands.PromptStringAsync("Rename Track", "Track name:", oldName);
            if (name is null || document != _document) return;
            if (document.RenameTrack(_window._undoManager, sequence, track, name) is { } error)
                _window.ShowErrorBanner(error);
        }

        private void AddSources(IReadOnlyList<string> paths)
        {
            if (_document is null) return;
            var errors = _document.AddSources(_window._undoManager, paths);
            if (errors.Count > 0)
                _window.ShowErrorBanner(string.Join(" ", errors));
        }

        private void RemoveSource(string source)
        {
            if (_document?.RemoveSource(_window._undoManager, source) is { } error)
                _window.ShowErrorBanner(error);
        }

        private void AddSequence()
        {
            if (_document is not { } document) return;
            string name = document.AddSequence(_window._undoManager);
            // The new sequence starts from its beginning.
            StopPlayback();
            // Name it right away: the default is selected, so typing replaces it and Escape keeps it. Posted so a
            // closing menu has handed focus back first and cannot take it from the box.
            Dispatcher.UIThread.Post(() => BeginRenameNewSequence(document, name), DispatcherPriority.Background);
        }

        /// <summary>
        /// Scrolls the sequence called <paramref name="name"/> into view and swaps its name for the inline box, with the
        /// text selected. Does nothing when the document or the list has moved on since the sequence was added.
        /// </summary>
        private void BeginRenameNewSequence(SequenceDocument document, string name)
        {
            var list = _window.SequenceList;
            int index = _items.FindIndex(item => item.Name == name);
            if (document != _document || index < 0 || index != list.SelectedIndex) return;
            list.ScrollIntoView(index);
            list.UpdateLayout();
            if (list.ContainerFromIndex(index) is not ListBoxItem container) return;
            _items[index].IsEditing = true;
            container.GetVisualDescendants().OfType<InlineRenameBox>().FirstOrDefault()?.Begin(name);
        }

        /// <summary>
        /// Inserts a copy of the selected sequence right after it and opens the copy's name for editing in place, the way
        /// Add Sequence does.
        /// </summary>
        private void DuplicateSelected()
        {
            if (_document is not { } document || CurrentSequence is null) return;
            string name = document.DuplicateSequence(_window._undoManager, SequenceIndex);
            // The copy starts from its beginning.
            StopPlayback();
            // Name it right away: the copy's name is selected, so typing replaces it and Escape keeps it. Posted so a
            // closing menu has handed focus back first and cannot take it from the box.
            Dispatcher.UIThread.Post(() => BeginRenameNewSequence(document, name), DispatcherPriority.Background);
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

        private void Detach()
        {
            // A wheel zoom still easing toward its preset is remembered where it was heading, for this document.
            if (_document != null)
                _window.SequenceTimeline.SettleZoomAnimation();
            if (_document != null)
                _document.Changed -= Refresh;
            _document = null;
        }

        // Collapsed sources persist per .seqj file. They are view state, so no undo entry and the file is not touched.
        private void StoreCollapsedSources()
        {
            if (_document is null) return;
            var key = _document.Path.Standardized;
            var keys = _window.SequenceLibrary.CollapsedSources.Keys;
            if (keys.Count == 0) _window._appSettings.CollapsedLibrarySources.Remove(key);
            else _window._appSettings.CollapsedLibrarySources[key] = keys.ToList();
            _window.SaveSettingsFile();
        }

        private static List<string> SequenceNames(SequenceDocument document) => document.Save.Sequences.Select(sequence => sequence.Name).ToList();

        // The selected sequence persists per .seqj file, by name, so a rename of it carries over. View state again:
        // no undo entry and the file is not touched. Called after every selection or edit, so it follows undo and redo too.
        private void RememberSelection()
        {
            if (_document is null) return;
            if (SequenceSelectionMemory.Remember(_window._appSettings.SelectedSequences, _document.Path.Standardized,
                    SequenceNames(_document), _document.SelectedSequence))
                _window.SaveSettingsFile();
        }

        // The timeline zoom persists per .seqj file as a whole percent (#1665). View state again: no undo entry, the file
        // is not touched. Every zoom path (wheel, +/-, typed value) raises ZoomChanged; a wheel ease raises it every tick,
        // so only the settled zoom is stored, and the settings are written only when the stored percent changed.
        private void RememberZoom()
        {
            var timeline = _window.SequenceTimeline;
            if (_restoringZoom || _document is null || timeline.IsZoomAnimating) return;
            if (TimelineZoomMemory.Remember(_window._appSettings.TimelineZooms, _document.Path.Standardized, timeline.Zoom * 100.0))
                _window.SaveSettingsFile();
        }

        // Applies the file's remembered zoom without easing. Open, tab restore, back/forward and tab switches all come
        // through LoadAsync, and the one timeline control is shared by every .seqj tab, so each load sets its own zoom.
        private void RestoreZoom()
        {
            if (_document is null) return;
            _restoringZoom = true;
            try
            {
                _window.SequenceTimeline.RestoreZoomPercent(
                    TimelineZoomMemory.PercentToRestore(_window._appSettings.TimelineZooms, _document.Path.Standardized));
            }
            finally
            {
                _restoringZoom = false;
            }
        }

        /// <summary>Rebuilds the list from the document, keeping the selection by position.</summary>
        private void Refresh()
        {
            if (_document is null) return;
            _items = _document.Save.Sequences.Select(sequence => new SequenceListItem(sequence.Name)).ToList();
            // The document says which sequence an edit, undo or redo left selected.
            int selected = Math.Min(_document.SelectedSequence, _items.Count - 1);
            _refreshing = true;
            try
            {
                _window.SequenceList.ItemsSource = _items;
                _window.SequenceList.SelectedIndex = selected;
            }
            finally
            {
                _refreshing = false;
            }
            _document.SelectedSequence = selected;
            RememberSelection();
            // The settings copy is current after every toggle, so reloading it on each refresh is safe.
            _window.SequenceLibrary.CollapsedSources.Load(
                _window._appSettings.CollapsedLibrarySources.GetValueOrDefault(_document.Path.Standardized));
            _window.SequenceLibrary.Show(_document);
            // Rebuilding the timeline raises ContentChanged, which refreshes the inspector, Loop toggle, playhead and preview.
            _window.SequenceTimeline.Show(_document, selected, _window._undoManager);
            _window.UpdateStatusBar();
        }
    }
}
