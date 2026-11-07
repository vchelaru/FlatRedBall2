using AnimationEditor.App.Services;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AnimationEditor.App.Controls;

/// <summary>
/// A <c>.seqj</c> tab's library: each source as a titled group of chain chips. A chip shows its chain's first frame
/// and plays while hovered. Files dropped on it are reported through <see cref="FilesDropped"/> and chip drags through
/// <see cref="ChipDragMoved"/>/<see cref="ChipDragEnded"/>; the host decides what to add, so this control never edits
/// the document.
/// </summary>
public sealed class SequenceLibraryControl : UserControl
{
    private readonly StackPanel _groups = new() { Margin = new Thickness(8), Spacing = 12 };
    private readonly Border _frame = new() { BorderThickness = new Thickness(1) };
    private readonly Dictionary<(string Source, string Chain), LibraryChipView> _chips = new();
    private readonly Dictionary<string, Control> _headers = new();
    private SequenceDocument? _document;
    private string? _shownKey;
    private readonly Dictionary<string, Action> _groupApply = new(StringComparer.OrdinalIgnoreCase);

    public SequenceLibraryControl()
    {
        var title = new TextBlock
        {
            Text = "LIBRARY", Margin = new Thickness(10, 0), FontSize = 11, FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        BindResource(title, TextBlock.ForegroundProperty, "InkMid");
        var header = new Border { Height = 36, BorderThickness = new Thickness(0, 0, 0, 1), Child = title };
        BindResource(header, Border.BackgroundProperty, "BgRail");
        BindResource(header, Border.BorderBrushProperty, "LineBrush");
        DockPanel.SetDock(header, Dock.Top);

        var body = new DockPanel();
        body.Children.Add(header);
        body.Children.Add(new ScrollViewer
        {
            Content = _groups,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        });
        _frame.Child = body;
        _frame.BorderBrush = Brushes.Transparent;
        BindResource(_frame, Border.BackgroundProperty, "BgPanel");
        Content = _frame;

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragEnterEvent, (_, _) => _frame.BorderBrush = this.FindResource("Accent") as IBrush);
        AddHandler(DragDrop.DragLeaveEvent, (_, _) => ClearDropHighlight());
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    public ThumbnailService? Thumbnails { get; set; }

    /// <summary>
    /// The sources whose chips are hidden. View state the host persists: toggling it never touches the document.
    /// </summary>
    public CollapsedSourceSet CollapsedSources { get; } = new();

    /// <summary>True when <paramref name="source"/> is shown with its chips hidden.</summary>
    public bool IsCollapsed(string source) => CollapsedSources.IsCollapsed(source);

    /// <summary>Collapses or expands <paramref name="source"/>, as a click on its chevron or title does.</summary>
    public void ToggleSource(string source)
    {
        CollapsedSources.Toggle(source);
        if (_groupApply.TryGetValue(source, out var apply)) apply();
    }

    /// <summary>Raised with the local paths of files dropped on the library. Marks the drop handled.</summary>
    public event Action<IReadOnlyList<string>>? FilesDropped;

    /// <summary>Raised when the user clicks a source's remove button, with the source as written in the file.</summary>
    public event Action<string>? RemoveSourceRequested;

    /// <summary>
    /// Raised by a "Go to Definition" menu item. A chip sends its source and chain; a source's header sends the source
    /// alone (empty <see cref="SequenceChainReferenceSave.Chain"/>), so the host opens the file without selecting a chain.
    /// </summary>
    public event Action<SequenceChainReferenceSave>? GoToDefinitionRequested;

    /// <summary>
    /// Raised on each pointer move while a chip is dragged. The drag stays inside the window (pointer capture, not OS
    /// drag-drop), so the host reads the position against whatever control it drops on.
    /// </summary>
    public event Action<LibraryChip, PointerEventArgs>? ChipDragMoved;

    /// <summary>Raised when a chip drag ends: with the release, or null when the drag was cancelled.</summary>
    public event Action<LibraryChip, PointerEventArgs?>? ChipDragEnded;

    internal void RaiseGoToDefinition(SequenceChainReferenceSave reference) => GoToDefinitionRequested?.Invoke(reference);

    internal void RaiseChipDragMoved(LibraryChip chip, PointerEventArgs e) => ChipDragMoved?.Invoke(chip, e);

    internal void RaiseChipDragEnded(LibraryChip chip, PointerEventArgs? e) => ChipDragEnded?.Invoke(chip, e);

    /// <summary>What the library shows, from the last <see cref="Show"/>.</summary>
    public IReadOnlyList<LibrarySource> Sources { get; private set; } = Array.Empty<LibrarySource>();

    /// <summary>The chip for <paramref name="chain"/> of <paramref name="source"/>, or null when it isn't shown.</summary>
    public LibraryChipView? ChipFor(string source, string chain) => _chips.GetValueOrDefault((source, chain));

    /// <summary>The header row of <paramref name="source"/>, or null when it isn't shown.</summary>
    public Control? HeaderFor(string source) => _headers.GetValueOrDefault(source);

    /// <summary>
    /// Shows <paramref name="document"/>'s sources. Rebuilds the chips only when the document or its source list changed,
    /// so a chip that is playing keeps playing through unrelated edits.
    /// </summary>
    public void Show(SequenceDocument? document)
    {
        string key = document is null ? "" : string.Join("\n", document.Save.Sources);
        if (document is not null) CollapsedSources.Prune(document.Save.Sources);
        if (ReferenceEquals(document, _document) && key == _shownKey)
        {
            // The host may have loaded another collapsed set for the same document.
            foreach (var apply in _groupApply.Values) apply();
            return;
        }
        _document = document;
        _shownKey = key;
        Sources = document is null ? Array.Empty<LibrarySource>() : SequenceLibrary.Build(document);
        Rebuild();
    }

    private void Rebuild()
    {
        _groups.Children.Clear();
        _chips.Clear();
        _groupApply.Clear();
        _headers.Clear();
        if (_document is null) return;
        if (Sources.Count == 0)
        {
            var empty = new TextBlock
            {
                Name = "LibraryEmptyMessage",
                Text = "No sources yet. Drag an .achx or .achj file here from the Animations tab to use its chains.",
                TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(2, 4),
            };
            BindResource(empty, TextBlock.ForegroundProperty, "InkMid");
            _groups.Children.Add(empty);
            return;
        }

        foreach (var source in Sources)
            _groups.Children.Add(BuildGroup(source));
    }

    private Control BuildGroup(LibrarySource source)
    {
        var name = new TextBlock
        {
            Text = source.IsMissing ? $"⚠ {SourceTitle.Format(source.Source)}" : SourceTitle.Format(source.Source),
            FontWeight = FontWeight.SemiBold, FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center,
        };
        BindResource(name, TextBlock.ForegroundProperty, source.IsMissing ? "IconWarning" : "Ink");
        ToolTip.SetTip(name, source.Source);
        var detail = new TextBlock
        {
            Text = source.IsMissing ? "file not found" : $"{source.Chips.Count} {(source.Chips.Count == 1 ? "chain" : "chains")}",
            FontSize = 11, Margin = new Thickness(6, 0), VerticalAlignment = VerticalAlignment.Center,
        };
        BindResource(detail, TextBlock.ForegroundProperty, source.IsMissing ? "IconWarning" : "InkMid");
        var remove = new Button
        {
            Content = "✕", FontSize = 10, Padding = new Thickness(5, 1), Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center, Tag = source.Source,
        };
        BindResource(remove, Button.ForegroundProperty, "InkMid");
        ToolTip.SetTip(remove, "Remove from library");
        Avalonia.Automation.AutomationProperties.SetName(remove, $"Remove {source.Source}");
        remove.Click += (_, _) => RemoveSourceRequested?.Invoke(source.Source);

        // A transparent background makes the whole row hit-testable, so a click anywhere on it toggles and a right-click opens the menu.
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), Background = Brushes.Transparent };
        var definition = new MenuItem { Header = "Go to Definition", IsEnabled = !source.IsMissing };
        definition.Click += (_, _) => GoToDefinitionRequested?.Invoke(new SequenceChainReferenceSave { Source = source.Source });
        var removeItem = new MenuItem { Header = "Remove Source" };
        removeItem.Click += (_, _) => RemoveSourceRequested?.Invoke(source.Source);
        header.ContextMenu = new ContextMenu { Items = { definition, removeItem } };
        _headers[source.Source] = header;
        Control toggle;
        Path? chevron = null;
        if (!source.IsMissing)
        {
            // Same chevron the Animations tree draws (the Fluent TreeViewItem geometries, tinted like its rows).
            chevron = new Path
            {
                Width = 10, Height = 10, Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            };
            BindResource(chevron, Shape.FillProperty, "TreeViewItemForeground");
            var button = new Button
            {
                Content = chevron, Width = 20, Padding = new Thickness(0, 2), Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center, Focusable = true,
            };
            button.Click += (_, _) => ToggleSource(source.Source);
            toggle = button;
            header.PointerPressed += (_, e) =>
            {
                // The remove button is inside the header and has its own click.
                if (!e.GetCurrentPoint(header).Properties.IsLeftButtonPressed || IsWithin(e.Source, remove)) return;
                ToggleSource(source.Source);
                e.Handled = true;
            };
        }
        else
        {
            toggle = new Panel { Width = 20 };
        }
        header.Children.Add(toggle);
        Grid.SetColumn(name, 1);
        header.Children.Add(name);
        Grid.SetColumn(detail, 2);
        header.Children.Add(detail);
        Grid.SetColumn(remove, 3);
        header.Children.Add(remove);

        var group = new StackPanel { Spacing = 6 };
        group.Children.Add(header);
        Control chipsPanel = new WrapPanel { IsVisible = false };
        if (source.Chips.Count > 0)
        {
            var chips = new WrapPanel();
            foreach (var chip in source.Chips)
            {
                var view = new LibraryChipView(chip, _document!, Thumbnails, this) { Margin = new Thickness(0, 0, 6, 6) };
                _chips[(chip.Source, chip.Chain.Name)] = view;
                chips.Children.Add(view);
            }
            group.Children.Add(chips);
            chipsPanel = chips;
        }

        void Apply()
        {
            bool collapsed = IsCollapsed(source.Source);
            chipsPanel.IsVisible = !collapsed && source.Chips.Count > 0;
            if (chevron is not null)
            {
                BindResource(chevron, Path.DataProperty, collapsed ? "TreeViewItemCollapsedChevronPathData" : "TreeViewItemExpandedChevronPathData");
                string verb = collapsed ? "Expand" : "Collapse";
                ToolTip.SetTip(toggle, $"{verb} {source.Source}");
                Avalonia.Automation.AutomationProperties.SetName(toggle, $"{verb} {source.Source}");
            }
        }
        Apply();
        _groupApply[source.Source] = Apply;
        return group;
    }

    private static bool IsWithin(object? candidate, Visual ancestor) =>
        candidate is Visual visual && (ReferenceEquals(visual, ancestor) || ancestor.IsVisualAncestorOf(visual));

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DroppedPaths(e).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        ClearDropHighlight();
        var paths = DroppedPaths(e);
        if (paths.Count == 0) return;
        // Handled keeps the window's own file drop from also opening the file in a tab.
        e.Handled = true;
        FilesDropped?.Invoke(paths);
    }

    private static List<string> DroppedPaths(DragEventArgs e) =>
        e.DataTransfer.TryGetFiles()?.Select(file => file.Path.LocalPath).Where(path => path.Length > 0).ToList()
        ?? new List<string>();

    private void ClearDropHighlight() => _frame.BorderBrush = Brushes.Transparent;

    // A theme token can be a Color (IconWarning) rather than a brush, so a Color is wrapped in one.
    internal static void BindResource(Control control, AvaloniaProperty property, string key) =>
        control.Bind(property, control.GetResourceObservable(key, value => value is Color color ? new SolidColorBrush(color) : value));
}

/// <summary>
/// One chain in the library: its first frame and its name. Plays the chain, looped, while the pointer is over it.
/// </summary>
public sealed class LibraryChipView : Border
{
    private const int ThumbWidth = 80;
    private const int ThumbHeight = 42;

    private const double DragThreshold = 4;

    private readonly SequenceDocument _document;
    private readonly ThumbnailService? _thumbnails;
    private readonly SequenceLibraryControl _library;
    private Point? _pressedAt;
    private bool _dragging;
    private readonly ResolvedFrameColor[] _colors;
    private readonly Image _image = new() { Stretch = Stretch.None };
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1 / 30.0) };
    private readonly Stopwatch _elapsed = new();

    internal LibraryChipView(LibraryChip chip, SequenceDocument document, ThumbnailService? thumbnails, SequenceLibraryControl library)
    {
        Chip = chip;
        _document = document;
        _thumbnails = thumbnails;
        _library = library;
        _colors = EffectiveFrameColor.ResolveAll(chip.Chain.Frames);

        Width = 86;
        CornerRadius = new CornerRadius(5);
        BorderThickness = new Thickness(1);
        ClipToBounds = true;
        SequenceLibraryControl.BindResource(this, BorderBrushProperty, "LineBrush");
        SequenceLibraryControl.BindResource(this, BackgroundProperty, "BgRail");
        ToolTip.SetTip(this, chip.Chain.Name);
        var definition = new MenuItem { Header = "Go to Definition" };
        definition.Click += (_, _) => _library.RaiseGoToDefinition(
            new SequenceChainReferenceSave { Source = chip.Source, Chain = chip.Chain.Name });
        var menu = new ContextMenu { Items = { definition } };
        ContextMenu = menu;

        // The thumbnail's square corners would paint over the outline's rounded top corners (ClipToBounds clips to the
        // outer radius, which still leaves the stroke exposed), so it takes the inner radius: outer 5 minus the 1px border.
        var thumb = new Border { Height = 46, CornerRadius = new CornerRadius(4, 4, 0, 0), Child = _image };
        SequenceLibraryControl.BindResource(thumb, BackgroundProperty, "BgCanvas");
        var name = new TextBlock
        {
            Text = chip.Chain.Name, FontSize = 11, Padding = new Thickness(6, 3),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        SequenceLibraryControl.BindResource(name, TextBlock.ForegroundProperty, "Ink");
        var stack = new StackPanel();
        stack.Children.Add(thumb);
        stack.Children.Add(name);
        Child = stack;

        _clock.Tick += (_, _) => ShowFrameAt(_elapsed.Elapsed.TotalSeconds);
        PointerEntered += (_, _) => StartPlaying();
        PointerExited += (_, _) => StopPlaying();
        DetachedFromVisualTree += (_, _) => StopPlaying();
        ShowFrame(0);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _pressedAt = e.GetPosition(this);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_pressedAt is not { } pressed) return;
        var point = e.GetPosition(this);
        if (!_dragging && Math.Abs(point.X - pressed.X) < DragThreshold && Math.Abs(point.Y - pressed.Y) < DragThreshold) return;
        _dragging = true;
        Opacity = 0.6;
        _library.RaiseChipDragMoved(Chip, e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pressedAt is null) return;
        bool dragged = _dragging;
        EndPress();
        e.Pointer.Capture(null);
        if (dragged) _library.RaiseChipDragEnded(Chip, e);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_pressedAt is null) return;
        bool dragged = _dragging;
        EndPress();
        if (dragged) _library.RaiseChipDragEnded(Chip, null);
    }

    private void EndPress()
    {
        _pressedAt = null;
        _dragging = false;
        Opacity = 1;
    }

    public LibraryChip Chip { get; }

    /// <summary>The frame the chip is showing.</summary>
    public int FrameIndex { get; private set; } = -1;

    public bool IsPlaying => _clock.IsEnabled;

    private void StartPlaying()
    {
        if (Chip.Chain.Frames.Count < 2) return;
        SequenceLibraryControl.BindResource(this, BorderBrushProperty, "Accent");
        _elapsed.Restart();
        _clock.Start();
    }

    private void StopPlaying()
    {
        SequenceLibraryControl.BindResource(this, BorderBrushProperty, "LineBrush");
        _clock.Stop();
        ShowFrame(0);
    }

    private void ShowFrameAt(double seconds)
    {
        // Loops while hovered whatever the chain's own Loop flag, so a one-shot chain can be watched more than once.
        double cycle = ChainClock.TotalLength(Chip.Chain);
        ShowFrame(ChainClock.Locate(Chip.Chain, cycle > 0 ? seconds % cycle : 0).FrameIndex);
    }

    private void ShowFrame(int index)
    {
        var frames = Chip.Chain.Frames;
        if (index == FrameIndex || index >= frames.Count) return;
        FrameIndex = index;
        var frame = frames[index];
        _image.Source = _thumbnails?.GetFrameThumbnail(
            _document.ResolveTexturePath(Chip.Source, frame), frame, _colors[index], ThumbWidth, ThumbHeight);
    }
}
