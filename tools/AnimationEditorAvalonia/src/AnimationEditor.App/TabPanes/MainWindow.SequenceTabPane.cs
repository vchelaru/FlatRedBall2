using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Hotkeys;
using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Core.Utilities;
using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
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
        private readonly Dictionary<FilePath, int> _selectedIndex = new();
        private HiddenSidebarTabs? _hiddenTabs;
        private SequenceDocument? _document;
        private bool _refreshing;

        private readonly SequenceClock _clock = new();
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
        // Real time between ticks, since a DispatcherTimer fires later than its interval (see #526).
        private readonly TickClock _tick = new(Stopwatch.GetTimestamp, Stopwatch.Frequency);

        // A stall (window drag, GC pause) should not jump playback by the whole frozen span.
        private const double MaxTickSeconds = 0.25;

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
            StopPlayback();
            Detach();
            w.SequencePreview.Composite = null;
            w.SequenceLibrary.Show(null);
            w.SequenceTimeline.Show(null, -1, null);
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
            _clock.Stop();
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

        public void DeleteSelection() => _window.SequenceTimeline.DeleteSelected();

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
                _selectedIndex[_document.Path] = w.SequenceList.SelectedIndex;
                w.SequenceTimeline.Show(_document, w.SequenceList.SelectedIndex, w._undoManager);
                // Another sequence starts from its beginning.
                StopPlayback();
            };
            w.SequenceTimeline.RenameTrackRequested += index => _ = RenameTrackAsync(index);
            w.SequenceLibrary.ChipDragMoved += (chip, e) => w.SequenceTimeline.UpdateChipDrag(chip, TimelinePoint(e));
            w.SequenceLibrary.ChipDragEnded += (chip, e) =>
            {
                w.SequenceTimeline.UpdateChipDrag(null, null);
                if (e != null && TimelinePoint(e) is { } point)
                    w.SequenceTimeline.DropChip(chip, point);
            };
            w.SequenceRenameItem.Click += (_, _) => _ = RenameSelectedAsync();
            w.SequenceDeleteItem.Click += (_, _) => DeleteSelected();
            w.SequenceLibrary.Thumbnails = w._thumbnailService;
            w.SequenceLibrary.FilesDropped += AddSources;
            w.SequenceLibrary.RemoveSourceRequested += RemoveSource;
            w.SequenceLibrary.CollapsedSources.Changed += StoreCollapsedSources;

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

            var preview = w.SequencePreview;
            w.SequencePreviewZoom.Attach(preview);
            preview.ShowShapes = w.SequenceShowShapes.IsChecked == true;
            preview.ShowOrigin = w.SequenceShowOrigin.IsChecked == true;
            preview.ShowBoundingBox = w.SequenceShowBoundingBox.IsChecked == true;
            w.SequenceShowShapes.IsCheckedChanged += (_, _) => preview.ShowShapes = w.SequenceShowShapes.IsChecked == true;
            w.SequenceShowOrigin.IsCheckedChanged += (_, _) => preview.ShowOrigin = w.SequenceShowOrigin.IsChecked == true;
            w.SequenceShowBoundingBox.IsCheckedChanged += (_, _) => preview.ShowBoundingBox = w.SequenceShowBoundingBox.IsChecked == true;
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
            double seconds = Math.Min(_tick.Tick(), MaxTickSeconds);
            _clock.Advance(seconds, _window.SequenceTimeline.SequenceLength, CurrentSequence?.Loop == true);
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
            // The settings copy is current after every toggle, so reloading it on each refresh is safe.
            _window.SequenceLibrary.CollapsedSources.Load(
                _window._appSettings.CollapsedLibrarySources.GetValueOrDefault(_document.Path.Standardized));
            _window.SequenceLibrary.Show(_document);
            _window.SequenceTimeline.Show(_document, selected, _window._undoManager);
            _window.SequenceLoopToggle.IsChecked = CurrentSequence?.Loop == true;
            // An edit can shorten the sequence under the playhead.
            _clock.Seek(_clock.Time, _window.SequenceTimeline.SequenceLength);
            ShowPlayhead();
            _window.UpdateStatusBar();
        }
    }
}
