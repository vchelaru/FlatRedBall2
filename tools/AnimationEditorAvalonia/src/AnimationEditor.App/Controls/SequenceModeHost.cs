using AnimationEditor.App.Services;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Spike: everything a <c>.seqj</c> tab shows, built in code. <see cref="ListPane"/> replaces the ANIMATIONS tree,
/// <see cref="InspectorPane"/> replaces the Inspector tab's content, and <see cref="EditorPane"/> (library + timeline over
/// the composited preview) replaces the achx editor pane. MainWindow only swaps visibility and routes the tab.
/// </summary>
public sealed class SequenceModeHost
{
    private readonly IUndoManager _undo;
    private readonly ThumbnailService _thumbnails;
    private readonly Action<string> _openFile;
    private readonly ListBox _sequenceList = new() { Name = "SequenceList" };
    private readonly StackPanel _library = new() { Name = "SequenceLibrary", Spacing = 6, Margin = new Thickness(6) };
    private readonly StackPanel _inspector = new() { Name = "SequenceInspector", Spacing = 6, Margin = new Thickness(10) };
    private readonly Button _playButton = new() { Name = "SequencePlay", Content = "▶ Play" };
    private readonly CheckBox _loopCheck = new() { Name = "SequenceLoop", Content = "Loop" };
    private readonly TextBlock _timeText = new() { VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray };
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch _clockWatch = new();
    private bool _refreshing;
    private bool _inspectorEditing;
    private string _sourcesShown = "";

    public SequenceModeHost(IUndoManager undo, ThumbnailService thumbnails, Action<string> openFile)
    {
        _undo = undo;
        _thumbnails = thumbnails;
        _openFile = openFile;
        Timeline = new SequenceTimelineControl { Name = "SequenceTimeline", UndoManager = undo };
        Preview = new SequenceCanvasControl { Name = "SequencePreview", Thumbnails = thumbnails };

        ListPane = BuildListPane();
        InspectorPane = new ScrollViewer { Content = _inspector };
        EditorPane = BuildEditorPane();

        Timeline.SelectionChanged += _ => { if (!_inspectorEditing) RebuildInspector(); };
        Timeline.ScrubRequested += time => { Pause(); SetTime(time); };
        _clock.Tick += (_, _) => OnClockTick();
        _playButton.Click += (_, _) => { if (IsPlaying) Pause(); else Play(); };
        _loopCheck.IsCheckedChanged += (_, _) =>
        {
            if (_refreshing || Sequence is null || Sequence.Loop == (_loopCheck.IsChecked == true)) return;
            bool loop = _loopCheck.IsChecked == true;
            Document!.Edit(_undo, loop ? "Loop Sequence" : "Don't Loop Sequence", s => s.Sequences[SequenceIndex].Loop = loop);
        };
    }

    public SequenceDocument? Document { get; private set; }
    public SequenceTimelineControl Timeline { get; }
    public SequenceCanvasControl Preview { get; }
    public Control ListPane { get; }
    public Control InspectorPane { get; }
    public Control EditorPane { get; }
    public int SequenceIndex { get; private set; }
    public double Time { get; private set; }
    public bool IsPlaying => _clock.IsEnabled;

    public SequenceSave? Sequence =>
        Document is { } d && SequenceIndex >= 0 && SequenceIndex < d.Save.Sequences.Count ? d.Save.Sequences[SequenceIndex] : null;

    public void Show(SequenceDocument document)
    {
        if (Document is not null) Document.Changed -= Refresh;
        Document = document;
        Document.Changed += Refresh;
        Timeline.Document = document;
        SequenceIndex = document.Save.Sequences.Count > 0 ? 0 : -1;
        Time = 0;
        _sourcesShown = "";
        Refresh();
    }

    public void Hide()
    {
        Pause();
        if (Document is not null) Document.Changed -= Refresh;
    }

    public void AddSequence()
    {
        if (Document is null) return;
        string name = UniqueName(Document.Save.Sequences.Select(s => s.Name), "Sequence");
        Document.Edit(_undo, $"Add Sequence '{name}'", s => s.Sequences.Add(new SequenceSave { Name = name }));
        SelectSequence(Document.Save.Sequences.Count - 1);
    }

    public void SelectSequence(int index)
    {
        SequenceIndex = index;
        Timeline.Select(null);
        SetTime(0);
        Refresh();
    }

    /// <summary>What a file dropped on the library does. Returns an error to show, or null.</summary>
    public string? AddSource(string absolutePath) => Document?.AddSource(_undo, absolutePath);

    public void Play()
    {
        if (Sequence is null) return;
        _clockWatch.Restart();
        _clock.Start();
        _playButton.Content = "❚❚ Pause";
    }

    public void Pause()
    {
        _clock.Stop();
        _playButton.Content = "▶ Play";
    }

    /// <summary>Advances the clock as if <paramref name="seconds"/> of real time passed. Tests step playback with this.</summary>
    public void Advance(double seconds)
    {
        if (Sequence is not { } sequence || Document is null) return;
        double length = SequenceTimeline.Length(sequence, Document.CycleLength);
        double next = Time + seconds;
        if (!sequence.Loop && next >= length) { SetTime(length); Pause(); return; }
        SetTime(SequenceTimeline.WrapTime(next, length, sequence.Loop));
    }

    public void SetTime(double time)
    {
        Time = Math.Max(0, time);
        Timeline.Playhead = Time;
        Preview.Layers = Sequence is { } sequence && Document is not null ? Document.Sample(sequence, Time) : Array.Empty<SequenceLayer>();
        double length = Sequence is { } s && Document is not null ? SequenceTimeline.Length(s, Document.CycleLength) : 0;
        _timeText.Text = $"{Time:0.00} / {length:0.00} s";
    }

    private void OnClockTick()
    {
        double elapsed = _clockWatch.Elapsed.TotalSeconds;
        _clockWatch.Restart();
        Advance(elapsed);
    }

    private void Refresh()
    {
        if (Document is null) return;
        _refreshing = true;
        try
        {
            if (SequenceIndex >= Document.Save.Sequences.Count) SequenceIndex = Document.Save.Sequences.Count - 1;
            _sequenceList.ItemsSource = Document.Save.Sequences.Select(s => s.Name).ToList();
            _sequenceList.SelectedIndex = SequenceIndex;
            Timeline.SequenceIndex = SequenceIndex;
            Timeline.InvalidateMeasure();
            Timeline.InvalidateVisual();
            _loopCheck.IsChecked = Sequence?.Loop == true;
            string sources = string.Join("|", Document.Save.Sources);
            if (sources != _sourcesShown) { _sourcesShown = sources; RebuildLibrary(); }
            if (!_inspectorEditing) RebuildInspector();
            SetTime(Time);
        }
        finally { _refreshing = false; }
    }

    // ── Left column: sequence list ───────────────────────────────────────────

    private Control BuildListPane()
    {
        var add = new Button { Name = "AddSequenceButton", Content = "+ Add Sequence", HorizontalAlignment = HorizontalAlignment.Left };
        add.Click += (_, _) => AddSequence();
        _sequenceList.SelectionChanged += (_, _) =>
        {
            if (!_refreshing && _sequenceList.SelectedIndex >= 0 && _sequenceList.SelectedIndex != SequenceIndex)
                SelectSequence(_sequenceList.SelectedIndex);
        };
        var header = new TextBlock { Text = "SEQUENCES", Margin = new Thickness(8, 6), Foreground = Brushes.Gray, FontSize = 11 };
        var panel = new DockPanel { IsVisible = false };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(add, Dock.Top);
        panel.Children.Add(header);
        add.Margin = new Thickness(8, 0, 8, 6);
        panel.Children.Add(add);
        panel.Children.Add(_sequenceList);
        return panel;
    }

    // ── Top half: library + timeline; bottom half: preview ───────────────────

    private Control BuildEditorPane()
    {
        var libraryScroll = new ScrollViewer { Content = _library, Width = 230 };
        var libraryBorder = new Border
        {
            Child = new DockPanel
            {
                Children =
                {
                    Docked(new TextBlock { Text = "LIBRARY  (drop .achx/.achj here)", Margin = new Thickness(8, 6), FontSize = 11, Foreground = Brushes.Gray }, Dock.Top),
                    libraryScroll,
                },
            },
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x36, 0x39, 0x40)),
            BorderThickness = new Thickness(0, 0, 1, 0),
        };
        DragDrop.SetAllowDrop(libraryBorder, true);
        libraryBorder.AddHandler(DragDrop.DragOverEvent, (_, e) =>
            e.DragEffects = e.DataTransfer.TryGetFiles() is { Length: > 0 } ? DragDropEffects.Copy : DragDropEffects.None);
        libraryBorder.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            foreach (var file in e.DataTransfer.TryGetFiles() ?? Array.Empty<Avalonia.Platform.Storage.IStorageItem>())
                AddSource(file.Path.LocalPath);
        });

        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        top.Children.Add(libraryBorder);
        var timelineScroll = new ScrollViewer { Content = Timeline, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetColumn(timelineScroll, 1);
        top.Children.Add(timelineScroll);

        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(6, 4) };
        toolbar.Children.Add(_playButton);
        toolbar.Children.Add(_loopCheck);
        toolbar.Children.Add(_timeText);

        var bottom = new DockPanel();
        bottom.Children.Add(Docked(toolbar, Dock.Top));
        bottom.Children.Add(Preview);

        var grid = new Grid { RowDefinitions = new RowDefinitions("*,4,*"), IsVisible = false, Background = new SolidColorBrush(Color.FromRgb(0x16, 0x17, 0x1a)) };
        grid.Children.Add(top);
        var splitter = new GridSplitter { ResizeDirection = GridResizeDirection.Rows };
        Grid.SetRow(splitter, 1);
        grid.Children.Add(splitter);
        Grid.SetRow(bottom, 2);
        grid.Children.Add(bottom);
        return grid;
    }

    private static Control Docked(Control control, Dock dock)
    {
        DockPanel.SetDock(control, dock);
        return control;
    }

    private void RebuildLibrary()
    {
        _library.Children.Clear();
        if (Document is null) return;
        foreach (var source in Document.Save.Sources)
        {
            bool missing = Document.IsSourceMissing(source);
            _library.Children.Add(new TextBlock
            {
                Text = missing ? $"! {source} (missing)" : source,
                Foreground = missing ? Brushes.IndianRed : Brushes.Gainsboro,
                FontWeight = FontWeight.SemiBold,
            });
            var chips = new WrapPanel();
            foreach (var chain in Document.SourceList(source)?.AnimationChains ?? new List<AnimationChainSave>())
                chips.Children.Add(BuildChip(source, chain));
            _library.Children.Add(chips);
        }
    }

    private Control BuildChip(string source, AnimationChainSave chain)
    {
        var canvas = new SequenceCanvasControl { Thumbnails = _thumbnails, FitToBounds = true, Width = 64, Height = 52, IsHitTestVisible = false };
        var chip = new Border
        {
            Name = $"Chip:{source}:{chain.Name}",
            Width = 70,
            Margin = new Thickness(2),
            Padding = new Thickness(2),
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.FromRgb(0x27, 0x2a, 0x2f)),
            Child = new StackPanel
            {
                Children =
                {
                    canvas,
                    new TextBlock { Text = chain.Name, FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis, IsHitTestVisible = false },
                },
            },
        };
        var track = new SequenceTrackSave();
        void ShowAt(double time)
        {
            if (chain.Frames.Count == 0) return;
            var (index, elapsed) = ChainClock.Locate(chain, time % Math.Max(1e-6, ChainClock.TotalLength(chain)));
            canvas.Layers = new[] { new SequenceLayer(0, track, chain, index, elapsed, chain.Frames[index], Document!.ResolveTexture(source, chain.Frames[index])) };
        }
        ShowAt(0);

        // Plays on hover.
        var hoverClock = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        var hoverWatch = new Stopwatch();
        hoverClock.Tick += (_, _) => ShowAt(hoverWatch.Elapsed.TotalSeconds);
        chip.PointerEntered += (_, _) => { hoverWatch.Restart(); hoverClock.Start(); };
        chip.PointerExited += (_, _) => { hoverClock.Stop(); ShowAt(0); };

        // In-window drag to the timeline (pointer capture, not OS drag-drop, so headless tests can drive it).
        Point? pressedAt = null;
        chip.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(chip).Properties.IsLeftButtonPressed) return;
            pressedAt = e.GetPosition(chip);
            e.Pointer.Capture(chip);
            e.Handled = true;
        };
        chip.PointerMoved += (_, e) =>
        {
            if (pressedAt is null) return;
            chip.Opacity = 0.6;
        };
        chip.PointerReleased += (_, e) =>
        {
            if (pressedAt is null) return;
            pressedAt = null;
            chip.Opacity = 1;
            e.Pointer.Capture(null);
            var point = e.GetPosition(Timeline);
            if (new Rect(Timeline.Bounds.Size).Contains(point))
                Timeline.DropChain(point, source, chain.Name);
        };
        return chip;
    }

    // ── Inspector ────────────────────────────────────────────────────────────

    private void RebuildInspector()
    {
        _inspector.Children.Clear();
        if (Document is null || Sequence is not { } sequence || Timeline.Selection is not { } selection
            || selection.TrackIndex >= sequence.Tracks.Count)
        {
            _inspector.Children.Add(new TextBlock { Text = "Select a track or chain on the timeline.", Foreground = Brushes.Gray });
            return;
        }
        int ti = selection.TrackIndex;
        var track = sequence.Tracks[ti];
        if (selection.ChainIndex < 0 || selection.ChainIndex >= track.Chains.Count)
        {
            _inspector.Children.Add(Header($"Track '{track.Name}'"));
            var name = new TextBox { Name = "TrackName", Text = track.Name };
            name.LostFocus += (_, _) => InspectorEdit($"Rename Track '{name.Text}'", s => Track(s, ti).Name = name.Text ?? "");
            _inspector.Children.Add(Row("Name", name));
            _inspector.Children.Add(Row("X", Number("TrackX", track.X, v => InspectorEdit("Set Track X", s => Track(s, ti).X = (float)v))));
            _inspector.Children.Add(Row("Y", Number("TrackY", track.Y, v => InspectorEdit("Set Track Y", s => Track(s, ti).Y = (float)v))));
            _inspector.Children.Add(Row("Z-order", Number("TrackZ", track.Z, v => InspectorEdit("Set Track Z", s => Track(s, ti).Z = (int)v), increment: 1)));
            var flip = new CheckBox { Name = "TrackFlip", Content = "Flip horizontally", IsChecked = track.FlipHorizontal };
            flip.IsCheckedChanged += (_, _) => InspectorEdit("Flip Track", s => Track(s, ti).FlipHorizontal = flip.IsChecked == true);
            _inspector.Children.Add(flip);
            return;
        }

        int ci = selection.ChainIndex;
        var reference = track.Chains[ci];
        _inspector.Children.Add(Header($"Chain '{reference.Chain}'"));
        _inspector.Children.Add(Row("Source", new TextBlock { Text = reference.Source }));
        if (Document.FindChain(reference) is null)
            _inspector.Children.Add(new TextBlock { Text = "Missing: the source or chain can't be found. Data kept.", Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap });
        _inspector.Children.Add(Row("Start (s)", Number("ChainStart", reference.Start, v => InspectorEdit("Set Chain Start", s => Ref(s, ti, ci).Start = Math.Max(0, (double)v)))));
        _inspector.Children.Add(Row("Loops", Number("ChainLoops", reference.Loops, v => InspectorEdit("Set Chain Loops", s => Ref(s, ti, ci).Loops = Math.Max(SequenceTimeline.MinimumLoops, (float)v)))));
        var goTo = new Button { Content = "Go to Definition" };
        goTo.Click += (_, _) => _openFile(Document.ToAbsolute(reference.Source)); // Spike: opens the file, doesn't select the chain.
        _inspector.Children.Add(goTo);
    }

    private SequenceTrackSave Track(SequenceListSave s, int track) => s.Sequences[SequenceIndex].Tracks[track];
    private SequenceChainReferenceSave Ref(SequenceListSave s, int track, int chain) => Track(s, track).Chains[chain];

    private void InspectorEdit(string description, Action<SequenceListSave> mutate)
    {
        if (_refreshing || Document is null) return;
        _inspectorEditing = true;
        try { Document.Edit(_undo, description, mutate); }
        finally { _inspectorEditing = false; }
    }

    private static Control Header(string text) => new TextBlock { Text = text, FontWeight = FontWeight.SemiBold, FontSize = 13 };

    private static Control Row(string label, Control field)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("80,*") };
        grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(field, 1);
        grid.Children.Add(field);
        return grid;
    }

    private static NumericUpDown Number(string name, double value, Action<decimal> changed, double increment = 0.1)
    {
        var field = new NumericUpDown { Name = name, Value = (decimal)value, Increment = (decimal)increment, FormatString = "0.###" };
        field.ValueChanged += (_, e) => { if (e.NewValue is { } v) changed(v); };
        return field;
    }

    private static string UniqueName(IEnumerable<string> existing, string stem)
    {
        var taken = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(stem)) return stem;
        for (int i = 2; ; i++)
            if (!taken.Contains(stem + i)) return stem + i;
    }
}
