using AnimationEditor.App.Models;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.HotReload;
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
        private Controls.SequenceListDragController? _listDrag;

        // One clock per selected sequence, each on its own length and loop flag. Replaced on every load, since names are per file.
        private SequencePlayback _playback = new();
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
        // Real time between ticks, since a DispatcherTimer fires later than its interval (see #526).
        private readonly TickClock _tick = new(Stopwatch.GetTimestamp, Stopwatch.Frequency);

        // Watches the shown document's file, its sources and their textures, and only while its tab is in front: a hidden
        // tab catches up in LoadAsync, so a source edited in its own tab never races this. A second instance of the achx
        // tab's HotReloadWatcher, whose own-save guard and coalescing are reused as they are.
        private readonly HotReloadWatcher _watcher = new();
        private IReadOnlyList<string> _watchedSources = Array.Empty<string>();
        private IReadOnlyList<string> _watchedTextures = Array.Empty<string>();
        private bool _watcherDisposed;

        public SequenceTabPane(MainWindow window)
        {
            _window = window;
            // The watcher raises on its timer thread; each reaction re-checks the document on the UI thread.
            _watcher.AchxChangedOnDisk += path => Dispatcher.UIThread.InvokeAsync(() => OnFileChangedOnDisk(path));
            _watcher.AchxDeletedOnDisk += path => Dispatcher.UIThread.InvokeAsync(() => OnFileDeletedOnDisk(path));
            _watcher.SourceChangedOnDisk += _ => Dispatcher.UIThread.InvokeAsync(OnSourceChangedOnDisk);
            _watcher.PngChangedOnDisk += path => Dispatcher.UIThread.InvokeAsync(() => OnTextureChangedOnDisk(path));
        }

        private bool IsShown(string path) =>
            !_watcherDisposed && _document is { } document && document.Path == new FilePath(path);

        /// <summary>The sequence file itself was rewritten (git pull, another editor, an agent): read it again, keeping what still applies.</summary>
        private void OnFileChangedOnDisk(string path)
        {
            if (IsShown(path)) ReloadShownFromDisk(_document!);
        }

        private void ReloadShownFromDisk(SequenceDocument document)
        {
            var timeline = _window.SequenceTimeline;
            string? sequenceName = CurrentSequence?.Name;
            var chains = timeline.SelectedChains.ToList();
            var tracks = timeline.SelectedTracks.ToList();
            var result = document.ReloadFromDisk();
            if (result.Error is { } error)
            {
                // Mid-write or damaged: the open document stays, and the next change event tries again.
                _window.ShowErrorBanner($"Could not reload {document.Path.NoPath}: {error}");
                return;
            }
            if (!result.Changed) return;
            // The history describes the old file; undoing into it would write that back over the new one.
            _window._undoManager.Clear();
            // The timeline drops its selection when the sequence moved in the list; the same sequence keeps what still exists.
            if (sequenceName is not null && CurrentSequence?.Name == sequenceName
                && timeline.SelectedChains.Count == 0 && timeline.SelectedTracks.Count == 0)
                RestoreTimelineSelection(CurrentSequence, chains, tracks);
            _window.ShowToast($"Reloaded {document.Path.NoPath}");
        }

        private void RestoreTimelineSelection(SequenceSave sequence, List<TimelineChainId> chains, List<int> tracks)
        {
            var timeline = _window.SequenceTimeline;
            var liveTracks = tracks.Where(track => track < sequence.Tracks.Count).ToList();
            var liveChains = chains.Where(id => id.Track < sequence.Tracks.Count && id.Chain < sequence.Tracks[id.Track].Chains.Count).ToList();
            if (liveTracks.Count > 0) timeline.SelectTracks(liveTracks);
            else if (liveChains.Count > 0) timeline.SelectChains(liveChains);
        }

        private void OnFileDeletedOnDisk(string path)
        {
            if (IsShown(path)) ShowDeletedToast(new FilePath(path));
        }

        private void ShowDeletedToast(FilePath path) => _window.ShowToast($"'{path.NoPath}' was deleted from disk.");

        /// <summary>A listed source file changed or vanished: re-read it, so chains, frames, timings and the missing flag follow.</summary>
        private void OnSourceChangedOnDisk()
        {
            if (!_watcherDisposed) _document?.ReloadChangedSources();
        }

        private void OnTextureChangedOnDisk(string path)
        {
            // Raises PathInvalidated, which the filmstrip, the library chips and the preview redraw from.
            if (!_watcherDisposed && _document is not null) _window._thumbnailService.InvalidatePath(path);
        }

        private void StartWatching(SequenceDocument document)
        {
            if (_watcherDisposed) return;
            _watcher.IsEnabled = _window.MenuEnableHotReload.IsChecked != false;
            _watchedTextures = document.WatchedTextureFiles;
            _watchedSources = document.WatchedSourceFiles;
            _watcher.StartWatching(document.Path.FullPath, _watchedTextures);
            _watcher.UpdateSourceList(_watchedSources);
        }

        // Sources and textures come and go with edits, undo and a source's own reload; the watch follows.
        private void SyncWatching()
        {
            if (_watcherDisposed || _document is not { } document) return;
            var sources = document.WatchedSourceFiles;
            if (!sources.SequenceEqual(_watchedSources))
            {
                _watchedSources = sources;
                _watcher.UpdateSourceList(sources);
            }
            var textures = document.WatchedTextureFiles;
            if (!textures.SequenceEqual(_watchedTextures))
            {
                _watchedTextures = textures;
                _watcher.UpdatePngList(textures);
            }
        }

        private void DisposeWatcher()
        {
            _watcherDisposed = true;
            _watcher.Dispose();
        }

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

        public void CaptureLeaving(TabEntry tab)
        {
            // Before the snapshot, so the rename is in this tab's history.
            CommitPendingRename();
            tab.UndoSnapshot = _window._undoManager.TakeSnapshot();
        }

        public Task<bool> LoadAsync(TabEntry tab, TabLoad load)
        {
            if (_documents.TryGetValue(tab.Path, out var known))
            {
                // The file or a source changed while this tab was away (a git pull, a source edited in its own tab): no
                // watcher ran for it, so read what differs again before showing, or the next save would overwrite it.
                CatchUpWithDisk(tab, known);
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
                // Our own saves go through the watcher's guard so their change events do not reload the document.
                document.SaveRunner = _watcher.RunOwnSave;
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
            _document.SequenceRenamed += OnSequenceRenamed;
            _playback = new SequencePlayback();
            // The undo stack is shared by every tab; this tab's own history lives in its snapshot.
            _window._undoManager.Clear();
            if (tab.UndoSnapshot != null)
                _window._undoManager.RestoreSnapshot(tab.UndoSnapshot);
            Refresh();
            RestoreZoom();
            StartWatching(document);
            return Task.FromResult(true);
        }

        // Same reactions as the live path, for a tab that was not in front when its file changed. Runs before the
        // document is shown, so nothing observes the intermediate state and the Refresh that follows picks it up.
        private void CatchUpWithDisk(TabEntry tab, SequenceDocument document)
        {
            var result = document.ReloadIfChangedOnDisk();
            if (result.Deleted)
                ShowDeletedToast(document.Path);
            else if (result.Error is { } error)
                _window.ShowErrorBanner($"Could not reload {document.Path.NoPath}: {error}");
            else if (document.StaleOnDiskReason is { } stale)
                // Still the same unreadable file: saving stays paused, so say so again.
                _window.ShowErrorBanner($"Could not reload {document.Path.NoPath}: {stale}");
            else if (result.Changed)
            {
                // The history describes the old file; undoing into it would write that back over the new one.
                tab.UndoSnapshot = null;
                _window.ShowToast($"Reloaded {document.Path.NoPath}");
            }
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
            CommitPendingRename();
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
        /// Delete removes what was last clicked: every selected sequence when the list holds the keyboard focus, else the timeline's
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
        /// Ctrl+C and Edit &gt; Copy put the selection on the clipboard as JSON: every selected sequence when the sequence list
        /// is the focused surface, the selected chains or tracks when the timeline is.
        /// </summary>
        public void CopySelection()
        {
            if (_document is not { } document) return;
            switch (Surface)
            {
                case ClipboardSurface.List when document.SelectedIndices.Count > 0:
                    _window.CopyTextToClipboard(document.CopySequences(document.SelectedIndices));
                    break;
                case ClipboardSurface.Timeline when _window.SequenceTimeline.CopySelectionText() is { } text:
                    _window.CopyTextToClipboard(text);
                    break;
            }
        }

        /// <summary>
        /// Ctrl+V and Edit &gt; Paste read the clipboard and paste what it holds, whichever pane has the focus
        /// (<see cref="PasteRoute"/>): copied sequences into the list, copied chains or tracks into the selected sequence's
        /// timeline. A paste that cannot apply says why in a toast; a damaged copy shows the error banner.
        /// </summary>
        public void PasteSelection()
        {
            if (_document is not { } document) return;
            _ = _window.RunGuardedAsync(() => PasteAsync(document), "Paste");
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

        /// <summary>Ctrl+D and Edit &gt; Duplicate: every selected sequence (list) or the selected chains or tracks (timeline).</summary>
        public void DuplicateSelection()
        {
            var surface = Surface;
            if (surface == ClipboardSurface.List)
                DuplicateSelected();
            else if (surface == ClipboardSurface.Timeline)
                _window.SequenceTimeline.DuplicateSelected();
        }

        /// <summary>File &gt; Reload from Disk. Unlike the watcher it reads the file whatever it holds; an identical file changes nothing.</summary>
        public void ReloadFromDisk()
        {
            if (_document is not { } document) return;
            if (!File.Exists(document.Path.FullPath))
                ShowDeletedToast(document.Path);
            else
                ReloadShownFromDisk(document);
        }

        public bool CanCopyPaste => Surface != ClipboardSurface.None;

        private async Task PasteAsync(SequenceDocument document)
        {
            if (TopLevel.GetTopLevel(_window)?.Clipboard is not { } clipboard) return;
            string? text = await clipboard.TryGetTextAsync();
            if (document != _document) return;
            var route = PasteRoute.For(text);
            switch (route.Kind)
            {
                case PasteRouteKind.Sequences:
                    var result = document.PasteSequences(_window._undoManager, route.Sequences!);
                    // The pasted sequences start from their beginning.
                    StopPlayback();
                    if (result.Errors.Count > 0) _window.ShowErrorBanner(string.Join(" ", result.Errors));
                    FocusSelectedRow();
                    break;
                case PasteRouteKind.Timeline:
                    PasteIntoTimeline(document, route.Timeline!);
                    break;
                default:
                    if (route.IsDamaged)
                        _window.ShowErrorBanner(route.Message!);
                    else
                        _window.ShowToast(route.Message!);
                    break;
            }
        }

        private void PasteIntoTimeline(SequenceDocument document, TimelineClipboardContent content)
        {
            string noun = content.Kind == TimelineClipboardKind.Chains ? "chains" : "tracks";
            if (document.Save.Sequences.Count == 0)
            {
                _window.ShowToast($"Can't paste the copied {noun}: the file has no sequence to paste into. Add a sequence first.");
                return;
            }
            // The pasted items become the timeline's selection, so it takes the keyboard: with the list focused, Delete would remove the sequence instead.
            _window.SequenceTimeline.Focus();
            if (!_window.SequenceTimeline.Paste(content))
                _window.ShowToast($"Nothing to paste: the copied {noun} are empty.");
        }

        // Rebuilding the list drops its focus once the old rows are gone; lay it out now and take it back so the keys keep working.
        private void FocusSelectedRow()
        {
            _window.SequenceList.UpdateLayout();
            _window.SequenceList.ContainerFromIndex(SequenceIndex)?.Focus();
        }

        public void TogglePlayback()
        {
            if (_playback.IsPlaying)
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
            w.SequenceList.SelectionChanged += (_, _) => OnSequenceListSelectionChanged();
            // Clicking the row that is already selected raises no SelectionChanged, so the press itself says "the
            // sequence": it drops the timeline selection and brings the sequence fields forward.
            w.SequenceList.AddHandler(InputElement.PointerPressedEvent, OnSequenceListPressed, RoutingStrategies.Bubble, handledEventsToo: true);
            w.SequenceList.AddHandler(InputElement.PointerPressedEvent, OnSequenceListPressedTunnel, RoutingStrategies.Tunnel);
            w.SequenceList.DoubleTapped += OnSequenceListDoubleTapped;
            _listDrag = new Controls.SequenceListDragController(w.SequenceList, () => _document?.SelectedIndices ?? Array.Empty<int>(),
                CommitPendingRename, MoveSequences, w.ShowDropLine, w.RemoveDropLine,
                point => w.SequenceList.TranslatePoint(point, w.DragOverlayCanvas));
            w.SequenceList.AddHandler(InlineRenameBox.CommittedEvent, OnSequenceRenameCommitted);
            w.SequenceList.AddHandler(InlineRenameBox.CancelledEvent, OnSequenceRenameCancelled);
            // A press that does not move the focus (a library chip, a toolbar button, blank space) would leave a rename open.
            w.AddHandler(InputElement.PointerPressedEvent, OnWindowPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            // Edit > Copy and Paste follow the focus, so they are re-evaluated whenever it moves.
            w.AddHandler(InputElement.GotFocusEvent, OnWindowGotFocus, RoutingStrategies.Bubble, handledEventsToo: true);
            w.SequenceTimelinePlayhead.Attach(w.SequenceTimeline);
            w.SequenceTimelinePopups.Attach(w.SequenceTimeline);
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
            w.SequenceLibrary.AddSourceRequested += () => _ = AddSourcesFromPickerAsync();
            w.SequenceLibrary.AddChipRequested += AddChip;
            w.SequenceLibrary.CanAddToSelectedTrack = () => w.SequenceTimeline.CanAddToSelectedTrack;
            w.SequenceLibrary.RemoveSourceRequested += RemoveSource;
            w.SequenceLibrary.SourceMoveRequested += MoveSource;
            w.SequenceLibrary.ConnectDropLine(w.ShowDropLine, w.RemoveDropLine,
                point => w.SequenceLibrary.GroupsPanel.TranslatePoint(point, w.DragOverlayCanvas));
            w.SequenceLibrary.CollapsedSources.Changed += StoreCollapsedSources;
            w.SequenceLibrary.GoToDefinitionRequested += reference => _ = GoToDefinitionAsync(reference);

            w.MenuEnableHotReload.Click += (_, _) => _watcher.IsEnabled = w.MenuEnableHotReload.IsChecked != false;
            // The watcher's timer and folder watchers are rooted by the runtime, so they must not outlive the window.
            w.Closed += (_, _) =>
            {
                try { DisposeWatcher(); }
                catch (Exception ex) { Debug.WriteLine($"[Sequence] watcher dispose failed: {ex.Message}"); }
            };

            _timer.Tick += (_, _) => OnTick();
            w.SequencePlayButton.Click += (_, _) => TogglePlayback();
            w.SequenceStopButton.Click += (_, _) => StopPlayback();
            w.SequenceLoopToggle.Click += (_, _) => SetLoop(w.SequenceLoopToggle.IsChecked == true);
            // The timeline shows the primary sequence, so a scrub moves its playhead alone; the others keep theirs.
            w.SequenceTimeline.Scrubbed += time =>
            {
                if (_document?.Selection.Primary is { } primary)
                    _playback.Seek(primary, time, w.SequenceTimeline.SequenceLength);
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

        // Ctrl+click toggles a row, and toggling the only selected one off would leave nothing selected, which the list
        // never shows: the click does nothing instead.
        private void OnSequenceListPressedTunnel(object? sender, PointerPressedEventArgs e)
        {
            var list = _window.SequenceList;
            if (!e.GetCurrentPoint(list).Properties.IsLeftButtonPressed || list.SelectedItems!.Count != 1) return;
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Meta)) return;
            if (e.Source is Visual source && source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is { DataContext: { } row }
                && ReferenceEquals(row, list.SelectedItems[0]))
                e.Handled = true;
        }

        private void OnSequenceListPressed(object? sender, PointerPressedEventArgs e)
        {
            var w = _window;
            if (_document is null || !e.GetCurrentPoint(w.SequenceList).Properties.IsLeftButtonPressed) return;
            if (e.Source is not Visual source || source.FindAncestorOfType<ListBoxItem>() is null || source.FindAncestorOfType<InlineRenameBox>() is not null || source is InlineRenameBox) return;
            // Extending the selection leaves the primary sequence, and so the timeline selection, alone.
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)) return;
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
            var hit = e.Source is Visual source ? source.FindAncestorOfType<ListBoxItem>(includeSelf: true) : null;
            bool onSequence = hit is not null;
            // The commands act on the whole selection, so a row hit inside it leaves it alone; a row outside it becomes the selection.
            if (hit?.DataContext is SequenceListItem hitRow && !w.SequenceList.SelectedItems!.Contains(hitRow) && _items.IndexOf(hitRow) is >= 0 and var hitIndex)
                w.SequenceList.SelectedIndex = hitIndex;
            menu.Items.Clear();
            int selectedCount = w.SequenceList.SelectedItems!.Count;
            var groups = SequenceListMenuLayout.For(onSequence);
            for (int i = 0; i < groups.Count; i++)
            {
                if (i > 0) menu.Items.Add(new Separator());
                foreach (var command in groups[i])
                {
                    var onClick = command switch
                    {
                        SequenceListMenuCommand.CopyQualifiedName => (Action)CopyQualifiedName,
                        SequenceListMenuCommand.Rename => () => _ = RenameSelectedAsync(),
                        SequenceListMenuCommand.DuplicateSequence => DuplicateSelected,
                        SequenceListMenuCommand.Delete => DeleteSelected,
                        SequenceListMenuCommand.AddSequence => AddSequence,
                        _ => throw new ArgumentOutOfRangeException(nameof(command), command, null),
                    };
                    var item = new MenuItem
                    {
                        Header = SequenceListMenuLayout.Header(command, selectedCount),
                        IsEnabled = SequenceListMenuLayout.IsEnabled(command, selectedCount),
                    };
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
            if (source.FindAncestorOfType<ListBoxItem>() is not { DataContext: SequenceListItem item } container || !CanRenameSequence) return;
            item.IsEditing = true;
            container.GetVisualDescendants().OfType<InlineRenameBox>().FirstOrDefault()?.Begin(item.Name);
            e.Handled = true;
        }

        private void OnSequenceRenameCommitted(object? sender, RoutedEventArgs e)
        {
            if (e.Source is not InlineRenameBox { DataContext: SequenceListItem item } box) return;
            // Enter commits with the box still focused and hands focus back to the list; a click-away already moved it
            // to whatever was clicked, which keeps it.
            bool refocusList = box.IsFocused;
            item.IsEditing = false;
            if (refocusList) _window.SequenceList.Focus();
            var document = _document;
            string text = box.Text ?? string.Empty;
            // A click on another row loses the box's focus in the middle of that press, before the row is selected.
            // Renaming rebuilds the list, which would drop the press's selection, so the rename waits for the press to
            // finish and then leaves whatever it selected alone.
            Dispatcher.UIThread.Post(() => ApplySequenceRename(document, item, text));
        }

        private void ApplySequenceRename(SequenceDocument? document, SequenceListItem item, string text)
        {
            int index = _items.IndexOf(item);
            if (document is null || document != _document || index < 0) return;
            if (document.RenameSequence(_window._undoManager, index, text, keepSelection: true) is { } error)
                _window.ShowErrorBanner(error);
        }

        /// <summary>True while a sequence row or a track header is being renamed in place.</summary>
        private bool HasPendingRename => _items.Any(item => item.IsEditing) || _window.SequenceTimeline.IsRenamingTrack;

        /// <summary>
        /// Applies the name typed into any open in-place rename (a sequence row or a track header), as Enter would. This
        /// is the one path for every way of leaving a rename that Enter and Escape are not: another tab or file, closing
        /// the tab or the window, undo and redo, Back and Forward, a hotkey, and a press that moves no focus. A name
        /// that fails validation shows the banner and keeps the old name, as on a click-away. Does nothing when no
        /// rename is open.
        /// </summary>
        public void CommitPendingRename()
        {
            if (_document is not { } document) return;
            for (int index = 0; index < _items.Count; index++)
            {
                if (_items[index].IsEditing) CommitListRename(document, index);
            }
            _window.SequenceTimeline.CommitRename();
        }

        private void CommitListRename(SequenceDocument document, int index)
        {
            var item = _items[index];
            // The box is the only holder of the typed text. Abandon it first: hiding it (IsEditing false) takes its
            // focus, and a box that is still editing would then commit a second time, later.
            if (_window.SequenceList.ContainerFromIndex(index) is Control container
                && container.GetVisualDescendants().OfType<InlineRenameBox>().FirstOrDefault() is { IsEditing: true } box)
            {
                string text = box.Text ?? string.Empty;
                box.Abandon();
                item.IsEditing = false;
                ApplySequenceRename(document, item, text);
            }
            else
            {
                item.IsEditing = false;
            }
        }

        // A press anywhere but in the rename box itself. When it takes the focus off the box, the box's own focus-loss
        // commit has run by the time this post does; when it does not (a library chip, blank space), this is the commit.
        private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_window._visiblePane != this || _document is null || !HasPendingRename) return;
            if (e.Source is Visual source && (source is InlineRenameBox || source.FindAncestorOfType<InlineRenameBox>() is not null)) return;
            var editing = _items.FirstOrDefault(item => item.IsEditing);
            int? track = _window.SequenceTimeline.RenamingTrack;
            // Posted, and limited to what was open now: a double-click's second press begins a new rename that this must not end.
            Dispatcher.UIThread.Post(() =>
            {
                if (_document is not { } document) return;
                if (editing is { IsEditing: true } && _items.IndexOf(editing) is >= 0 and var index) CommitListRename(document, index);
                if (track is { } t && _window.SequenceTimeline.RenamingTrack == t) _window.SequenceTimeline.CommitRename();
            });
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
                w.SequenceTimeline.SelectionSummary, CanRenameSequence);
        }

        /// <summary>Rename is for a single selected sequence: with several, no rename starts from the menu, the inline box or the Inspector.</summary>
        private bool CanRenameSequence => SequenceListMenuLayout.CanRename(_document?.SelectedIndices.Count ?? 0);

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
                _playback.Clamp(LengthOf);
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

        /// <summary>The primary sequence's index: what the timeline, Inspector and every editing command act on. -1 for none.</summary>
        public int PrimaryIndex => SequenceIndex;

        private int SequenceIndex => _document?.SelectedSequence ?? -1;

        private SequenceSave? CurrentSequence =>
            _document is { } document && SequenceIndex >= 0 && SequenceIndex < document.Save.Sequences.Count
                ? document.Save.Sequences[SequenceIndex]
                : null;

        private int IndexOf(string? name) =>
            _document is { } document && name is not null ? document.Save.Sequences.FindIndex(sequence => sequence.Name == name) : -1;

        private double LengthOf(string name) => _document?.SequenceLength(IndexOf(name)) ?? 0;

        private bool LoopOf(string name) =>
            _document is { } document && IndexOf(name) is >= 0 and var index && document.Save.Sequences[index].Loop;

        private void OnSequenceRenamed(string from, string to) => _playback.Rename(from, to);

        /// <summary>
        /// The list's selection changed by the user: Ctrl+click and Shift+click extend it, a plain click narrows it to one row.
        /// The first selected row stays primary until it is deselected. The timeline only reloads when the primary changed.
        /// </summary>
        private void OnSequenceListSelectionChanged()
        {
            var w = _window;
            if (_refreshing || _document is not { } document) return;
            int previousPrimary = document.SelectedSequence;
            var names = w.SequenceList.SelectedItems!.OfType<SequenceListItem>().Select(item => item.Name).ToList();
            document.Selection.Apply(names);
            document.SelectedSequence = IndexOf(document.Selection.Primary);
            RememberSelection();
            // Selecting another sequence starts it from its beginning; extending the selection leaves the others playing.
            _playback.Select(document.Selection.Names, LengthOf);
            if (!_playback.IsPlaying) _timer.Stop();
            if (document.SelectedSequence != previousPrimary)
                w.SequenceTimeline.Show(document, document.SelectedSequence, w._undoManager);
            ShowInspector();
            ShowPlayhead();
        }

        private void Play()
        {
            _playback.Play(LengthOf);
            if (_playback.IsPlaying)
            {
                _tick.Reset();
                _tick.Tick();
                _timer.Start();
            }
            ShowPlayhead();
        }

        private void PausePlayback()
        {
            _playback.Pause();
            _timer.Stop();
            ShowPlayhead();
        }

        private void StopPlayback()
        {
            _playback.Stop();
            _timer.Stop();
            ShowPlayhead();
        }

        private void OnTick()
        {
            _playback.AdvanceTick(_tick.Tick(), LengthOf, LoopOf);
            if (!_playback.IsPlaying) _timer.Stop();
            ShowPlayhead();
        }

        /// <summary>
        /// Moves the playheads, the preview, the time readout and the play button to the clocks. The timeline and the
        /// readout follow the primary sequence; the preview composites every selected sequence at its own time, in
        /// selection order from the back (<see cref="SequenceScene.At(SequenceDocument, IEnumerable{SequenceTime})"/>).
        /// </summary>
        private void ShowPlayhead()
        {
            var w = _window;
            double time = _document?.Selection.Primary is { } primary ? _playback.TimeOf(primary) : 0;
            w.SequenceTimeline.Playhead = time;
            w.SequencePreview.Composite = _document is not { } document
                ? null
                : SequenceScene.At(document, document.Selection.Names
                    .Select(name => new SequenceTime(IndexOf(name), _playback.TimeOf(name))).ToList());
            w.SequenceTimeText.Text = string.Format(CultureInfo.InvariantCulture, "{0:0.00} s / {1:0.00} s",
                time, w.SequenceTimeline.SequenceLength);
            w.SequencePlayIcon.Path = _playback.IsPlaying
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
            if (_document is not { } document || SequenceIndex is not (>= 0 and var sequence)) return;
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

        // Same refusals and one undo entry as a drop; a cancelled dialog changes nothing.
        private async Task AddSourcesFromPickerAsync()
        {
            if (_document is not { } document) return;
            var paths = await _window._appCommands.FileDialogService.PickOpenFilesAsync("Add Source", OpenFileFilters.SequenceSources);
            if (paths.Count == 0 || document != _document) return;
            AddSources(paths);
        }

        // A double-click, Enter or the chip's menu: where the chain lands is the timeline's rule; a refusal is said, never silent.
        private void AddChip(LibraryChip chip, ChipAddMode mode)
        {
            if (_document is null) return;
            if (_window.SequenceTimeline.AddChip(chip, mode) is { } refusal)
                _window.ShowToast(refusal);
        }

        private void RemoveSource(string source)
        {
            if (_document?.RemoveSource(_window._undoManager, source) is { } error)
                _window.ShowErrorBanner(error);
        }

        // Dropping a source's group on a gap: one undo entry, and an edit rather than a place to come back to, so no Back entry.
        private void MoveSource(int index, int slot)
        {
            if (_document is not { } document) return;
            using (_window._navigation.Suppress())
                document.MoveSources(_window._undoManager, new[] { index }, slot);
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
            if (document != _document || index < 0 || index != SequenceIndex) return;
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
            if (_document is not { } document || document.SelectedIndices.Count == 0) return;
            var names = document.DuplicateSequences(_window._undoManager, document.SelectedIndices);
            // The copies start from their beginning.
            StopPlayback();
            // Name a lone copy right away: its name is selected, so typing replaces it and Escape keeps it. Several copies
            // are not renamed in place (rename is for the primary alone). Posted so a closing menu has handed focus back
            // first and cannot take it from the box.
            if (names.Count == 1)
                Dispatcher.UIThread.Post(() => BeginRenameNewSequence(document, names[0]), DispatcherPriority.Background);
        }

        private async Task RenameSelectedAsync()
        {
            if (_document is not { } document || SequenceIndex is not (>= 0 and var index) || !CanRenameSequence) return;
            string oldName = document.Save.Sequences[index].Name;
            string? name = await _window._appCommands.PromptStringAsync("Rename Sequence", "Sequence name:", oldName);
            if (name is null || document != _document) return;
            if (document.RenameSequence(_window._undoManager, index, name) is { } error)
                _window.ShowErrorBanner(error);
        }

        private void DeleteSelected()
        {
            if (_document is not { } document || document.SelectedIndices.Count == 0) return;
            document.DeleteSequences(_window._undoManager, document.SelectedIndices);
        }

        private void Detach()
        {
            // A wheel zoom still easing toward its preset is remembered where it was heading, for this document.
            if (_document != null)
                _window.SequenceTimeline.SettleZoomAnimation();
            if (_document != null)
            {
                _document.Changed -= Refresh;
                _document.SequenceRenamed -= OnSequenceRenamed;
            }
            _document = null;
            _watcher.StopWatching();
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

        // Dropping the dragged rows on a gap: one undo entry, and a move is an edit rather than a place to come back to,
        // so it records no Back entry.
        private void MoveSequences(IReadOnlyList<int> indices, int slot)
        {
            if (_document is not { } document) return;
            using (_window._navigation.Suppress())
                document.MoveSequences(_window._undoManager, indices, slot);
            FocusSelectedRow();
        }

        /// <summary>Rebuilds the list from the document, keeping the selection by position.</summary>
        private void Refresh()
        {
            if (_document is null) return;
            // Its rows are about to be replaced; a drag still holding the old ones would drop on the wrong sequences.
            _listDrag?.CancelDrag();
            _items =_document.Save.Sequences.Select(sequence => new SequenceListItem(sequence.Name)).ToList();
            // The document says which sequence an edit, undo or redo left selected.
            int selected = Math.Min(_document.SelectedSequence, _items.Count - 1);
            _document.SelectedSequence = selected;
            // Selected sequences that no longer exist drop out; a selected sequence outside the multi-selection narrows it.
            _document.Selection.Reconcile(SequenceNames(_document), selected);
            var rows = _document.Selection.Names.Select(IndexOf).ToList();
            _refreshing = true;
            try
            {
                var list = _window.SequenceList;
                list.ItemsSource = _items;
                // The list's own selection order is the draw order, so the rows go back in the order they were selected.
                list.SelectedIndex = rows.Count > 0 ? rows[0] : selected;
                foreach (int row in rows.Skip(1))
                    list.Selection.Select(row);
            }
            finally
            {
                _refreshing = false;
            }
            RememberSelection();
            _playback.Select(_document.Selection.Names, LengthOf);
            if (!_playback.IsPlaying) _timer.Stop();
            // The settings copy is current after every toggle, so reloading it on each refresh is safe.
            _window.SequenceLibrary.CollapsedSources.Load(
                _window._appSettings.CollapsedLibrarySources.GetValueOrDefault(_document.Path.Standardized));
            _window.SequenceLibrary.Show(_document);
            SyncWatching();
            // Rebuilding the timeline raises ContentChanged, which refreshes the inspector, Loop toggle, playhead and preview.
            _window.SequenceTimeline.Show(_document, selected, _window._undoManager);
            _window.UpdateStatusBar();
        }
    }
}
