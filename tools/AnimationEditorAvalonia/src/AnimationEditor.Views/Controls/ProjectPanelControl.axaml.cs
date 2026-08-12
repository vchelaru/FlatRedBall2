using AnimationEditor.App.Services;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AnimationEditor.Views.Controls;

/// <summary>
/// Displays the recursively-discovered <c>.achx</c> tree for a picked project folder (#770).
/// Platform-agnostic: everything it renders comes from <see cref="AchxFolderScanner"/> /
/// <see cref="AchxFolderTreeBuilder"/> over <see cref="IEditorFolder"/>, so this same control is
/// shared unmodified by desktop (real filesystem) and the browser build (native folder handle) --
/// including first-frame thumbnails (issue #839), generated lazily via
/// <see cref="ProjectTreeThumbnailService"/> after <see cref="SetEntries"/>.
/// </summary>
public partial class ProjectPanelControl : UserControl
{
    private const int ThumbnailSize = 28;

    private IReadOnlyList<AchxFileEntry> _allEntries = Array.Empty<AchxFileEntry>();
    private string _searchQuery = string.Empty;
    private ProjectTreeThumbnailService? _thumbnailService;
    private CancellationTokenSource? _thumbnailLoadCts;

    // Guards against re-entrant SelectionChanged while ClearSearchAndReveal restores the
    // selection post-rebuild -- without it, that restore would re-fire FileSelected for the
    // same entry a second time.
    private bool _isRestoringSelectionAfterSearchClear;

    public ObservableCollection<AchxTreeNodeVm> TreeRoots { get; } = new();

    /// <summary>Raised when the user clicks a file row.</summary>
    public event Action<AchxFileEntry>? FileSelected;

    /// <summary>
    /// Completes once every thumbnail from the most recent <see cref="Rebuild"/> has finished
    /// loading (or been cancelled by a newer one). Test seam for awaiting the async thumbnail
    /// load -- production code never needs to await this.
    /// </summary>
    public Task ThumbnailLoadTask { get; private set; } = Task.CompletedTask;

    public ProjectPanelControl()
    {
        InitializeComponent();
        DataContext = this;
        ExcludeBinObjCheck.IsCheckedChanged += (_, _) => Rebuild();
        ProjectTree.SelectionChanged += OnTreeSelectionChanged;
        Rebuild();
    }

    /// <summary>
    /// Wires the thumbnail generator (issue #839). Not passed to the constructor because
    /// <see cref="AnimationEditor.Views"/> controls are constructed by XAML with no arguments;
    /// call this once, before the first real <see cref="SetEntries"/>.
    /// </summary>
    public void Initialize(ProjectTreeThumbnailService thumbnailService) =>
        _thumbnailService = thumbnailService;

    /// <summary>
    /// Replaces the scanned entries (e.g. after a fresh Open Project Folder pick) and rebuilds
    /// the tree respecting the current "Exclude bin/obj" checkbox state. Pass every entry
    /// unfiltered -- toggling the checkbox re-filters this cached list rather than re-scanning.
    /// </summary>
    public void SetEntries(IReadOnlyList<AchxFileEntry> entries)
    {
        _allEntries = entries;
        Rebuild();
    }

    public void Clear() => SetEntries(Array.Empty<AchxFileEntry>());

    private void Rebuild()
    {
        TreeRoots.Clear();

        var excludeBinObj = ExcludeBinObjCheck.IsChecked == true;
        var files = excludeBinObj
            ? _allEntries.Where(f => !BinObjPathFilter.IsExcluded(f.RelativePath)).ToList()
            : _allEntries.ToList();
        files = AchxSearchFilter.Filter(files, _searchQuery).ToList();

        EmptyMessage.IsVisible = files.Count == 0;
        ProjectTree.IsVisible = files.Count > 0;
        EmptyMessage.Text = _allEntries.Count == 0
            ? "File → Open Project Folder… to browse its .achx files."
            : string.IsNullOrWhiteSpace(_searchQuery)
                ? "No .achx files match the current filter."
                : "No .achx files match your search.";

        foreach (var node in AchxFolderTreeBuilder.Build(files))
            TreeRoots.Add(AchxTreeNodeVm.FromNode(node));

        StartThumbnailLoad();
    }

    /// <summary>
    /// Kicks off thumbnail generation for every file row now in <see cref="TreeRoots"/>, a few at
    /// a time (the throttle lives in <see cref="ProjectTreeThumbnailService"/>) so rows fill in
    /// as they finish instead of blocking the tree. Cancels any load still running from a
    /// previous <see cref="Rebuild"/> (e.g. the user typed in search again) first, so a stale
    /// generation can't overwrite a node from the new tree.
    /// </summary>
    private void StartThumbnailLoad()
    {
        _thumbnailLoadCts?.Cancel();
        _thumbnailLoadCts?.Dispose();
        if (_thumbnailService is null) { ThumbnailLoadTask = Task.CompletedTask; return; }

        var cts = new CancellationTokenSource();
        _thumbnailLoadCts = cts;

        var fileNodes = new List<AchxTreeNodeVm>();
        CollectFileNodes(TreeRoots, fileNodes);

        ThumbnailLoadTask = Task.WhenAll(fileNodes.Select(node => LoadOneThumbnailAsync(node, cts.Token)));
    }

    private static void CollectFileNodes(IEnumerable<AchxTreeNodeVm> nodes, List<AchxTreeNodeVm> results)
    {
        foreach (var node in nodes)
        {
            if (node.IsFile) results.Add(node);
            else CollectFileNodes(node.Children, results);
        }
    }

    private async Task LoadOneThumbnailAsync(AchxTreeNodeVm node, CancellationToken cancellationToken)
    {
        Bitmap? thumbnail;
        try
        {
            thumbnail = await _thumbnailService!.GetThumbnailAsync(
                node.Entry!, ThumbnailSize, ThumbnailSize, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!cancellationToken.IsCancellationRequested)
            node.Thumbnail = thumbnail;
    }

    /// <summary>
    /// Re-generates the thumbnail for a single already-visible node after its file changed on
    /// disk (issue #839 follow-up: a save wasn't reflected in the tree at all). No-op if
    /// <paramref name="entry"/> isn't currently in the tree, or before <see cref="Initialize"/>.
    /// Callers fire-and-forget this; it's <c>async Task</c> only so tests can await it.
    /// </summary>
    public async Task InvalidateThumbnail(AchxFileEntry entry)
    {
        if (_thumbnailService is null) return;

        var node = FindNode(TreeRoots, entry);
        if (node is null) return;

        _thumbnailService.InvalidateEntry(entry);
        await LoadOneThumbnailAsync(node, CancellationToken.None);
    }

    private void OnSearchQueryChanged(object? sender, string query)
    {
        _searchQuery = query;
        Rebuild();
    }

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isRestoringSelectionAfterSearchClear) return;
        if (ProjectTree.SelectedItem is not AchxTreeNodeVm { IsFile: true, Entry: { } entry }) return;

        FileSelected?.Invoke(entry);

        // The pick came from a filtered result -- clear the search so the tree returns to its
        // full contents, then re-select/reveal the same entry in it rather than leaving the
        // user looking at an empty selection in a suddenly-repopulated tree.
        if (!string.IsNullOrEmpty(_searchQuery))
            ClearSearchAndReveal(entry);
    }

    private void ClearSearchAndReveal(AchxFileEntry entry)
    {
        ProjectSearchBox.Clear(); // synchronously fires QueryChanged("") -> Rebuild()

        var match = FindNode(TreeRoots, entry);
        if (match is null) return;

        _isRestoringSelectionAfterSearchClear = true;
        try
        {
            ProjectTree.SelectedItem = match;
            ProjectTree.ScrollIntoView(match);
        }
        finally
        {
            _isRestoringSelectionAfterSearchClear = false;
        }
    }

    private static AchxTreeNodeVm? FindNode(IEnumerable<AchxTreeNodeVm> nodes, AchxFileEntry entry)
    {
        foreach (var node in nodes)
        {
            if (ReferenceEquals(node.Entry, entry)) return node;

            var found = FindNode(node.Children, entry);
            if (found is not null) return found;
        }
        return null;
    }

    private void OnFolderExpanderToggled(object? sender, EventArgs e)
    {
        if (sender is not Control control) return;
        var item = control.FindAncestorOfType<TreeViewItem>(includeSelf: true);
        if (item?.DataContext is not AchxTreeNodeVm { IsFolder: true } node) return;

        node.IsExpanded = !node.IsExpanded;
    }
}

/// <summary>Tree node view-model for <see cref="ProjectPanelControl"/>'s <c>TreeView</c>.</summary>
public sealed class AchxTreeNodeVm : INotifyPropertyChanged
{
    private bool _isExpanded = true;
    private Bitmap? _thumbnail;

    public string Name { get; }
    public AchxFileEntry? Entry { get; }
    public bool IsFolder => Entry is null;
    public bool IsFile => Entry is not null;
    public ObservableCollection<AchxTreeNodeVm> Children { get; } = new();

    public bool IsFolderOpen => _isExpanded;

    /// <summary>First-frame preview (issue #839), populated asynchronously after the row appears --
    /// null until <see cref="ProjectTreeThumbnailService"/> finishes (or if it can't produce one),
    /// in which case the row keeps showing the generic chain icon.</summary>
    public Bitmap? Thumbnail
    {
        get => _thumbnail;
        set
        {
            if (ReferenceEquals(_thumbnail, value)) return;
            _thumbnail = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasThumbnail));
            OnPropertyChanged(nameof(ShowFallbackIcon));
        }
    }

    public bool HasThumbnail => _thumbnail is not null;
    public bool ShowFallbackIcon => IsFile && _thumbnail is null;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsFolderOpen));
        }
    }

    private AchxTreeNodeVm(string name, AchxFileEntry? entry)
    {
        Name = name;
        Entry = entry;
    }

    public static AchxTreeNodeVm FromNode(AchxTreeNode node)
    {
        var vm = new AchxTreeNodeVm(node.Name, node.Entry);
        foreach (var child in node.Children)
            vm.Children.Add(FromNode(child));
        return vm;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
